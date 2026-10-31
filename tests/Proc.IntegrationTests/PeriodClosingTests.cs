using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Proc.Application.Inventory;
using Proc.Application.MasterData;
using Proc.Application.Organization;
using Proc.Infrastructure.Persistence;
using Proc.Infrastructure.Security;

namespace Proc.IntegrationTests;

/// <summary>
/// Đóng kỳ, giá vốn bình quân cuối kỳ, mở lại kỳ (design.md mục 7.2–7.4). Mỗi test dùng một công ty mới với lịch năm 2025 để
/// đóng/mở kỳ không ảnh hưởng công ty mẫu mà test khác đang ghi sổ.
/// </summary>
[Collection(ApiCollection.Name)]
public class PeriodClosingTests
{
    private readonly ProcApiFactory _factory;

    public PeriodClosingTests(ProcApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string UniqueCode(string prefix) => $"{prefix}{Guid.NewGuid():N}".ToUpperInvariant()[..10];

    private static async Task<string?> TitleAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(Ct))?.Title;

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(Ct)}");
        return (await response.Content.ReadFromJsonAsync<T>(Ct))!;
    }

    /// <summary>Công ty mới (kho KT, kho phụ KP), lịch 2025 mở tháng 1 và 2, một mặt hàng (ĐVT cái), một NCC áp dụng tại công ty.</summary>
    private sealed record Fixture(HttpClient Client, Guid CompanyId, Guid Kt, Guid Kp, Guid ItemId, Guid Uom, Guid SupplierId, Guid Jan, Guid Feb, string CompanyCode);

    private async Task<Fixture> ArrangeAsync()
    {
        var userName = $"kt.{Guid.NewGuid():N}"[..16];
        await _factory.CreateUserAsync(userName, ProcApiFactory.TestPassword, ["TCT"]);
        var head = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, "TCT");
        var company = await ReadAsync<CompanyDetailDto>(await head.PostAsJsonAsync("/api/companies",
            new CreateCompanyRequest(UniqueCode("D"), "Công ty thử đóng kỳ", null, null, null, null, new DateOnly(2025, 1, 1), "KT", "Kho tổng"), Ct));
        var client = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, company.Code);

        var kt = (await client.GetFromJsonAsync<List<WarehouseDetailDto>>("/api/warehouses", Ct))!.Single().Id;
        var kp = (await ReadAsync<WarehouseDetailDto>(await client.PostAsJsonAsync("/api/warehouses",
            new CreateWarehouseRequest("KP", "Kho phụ", kt, null, null, false), Ct))).Id;
        var calendar = await ReadAsync<InventoryCalendarDto>(await client.PostAsJsonAsync("/api/inventory-periods/years", new CreateInventoryYearRequest(2025), Ct));
        var jan = calendar.Periods.Single(p => p.Month == 1).Id;
        var feb = calendar.Periods.Single(p => p.Month == 2).Id;
        await ReadAsync<InventoryPeriodDto>(await client.PostAsync($"/api/inventory-periods/{jan}/open", null, Ct));
        await ReadAsync<InventoryPeriodDto>(await client.PostAsync($"/api/inventory-periods/{feb}/open", null, Ct));

        var uom = (await head.GetFromJsonAsync<List<UomDto>>("/api/uoms", Ct))!.Single(u => u.Code == "CAI").Id;
        var category = await ReadAsync<ItemCategoryDetailDto>(await head.PostAsJsonAsync("/api/item-categories",
            new CreateItemCategoryRequest(null, "Nhóm đóng kỳ", null, null), Ct));
        var item = await ReadAsync<ItemDetailDto>(await head.PostAsJsonAsync("/api/items",
            new CreateItemRequest(null, "Vòng bi đóng kỳ", null, "Inventory", category.Id, uom, null, null, null,
                null, "10", null, false, false, null, null, []), Ct));
        var groupId = (await head.GetFromJsonAsync<List<SupplierGroupDto>>("/api/supplier-groups", Ct))!.Single(g => g.Code == "TN").Id;
        var supplier = await ReadAsync<SupplierDetailDto>(await head.PostAsJsonAsync("/api/suppliers",
            new SaveSupplierRequest(null, groupId, "NCC đóng kỳ", null, false, null, null, null, null, null, null, null, null,
                "Approved", null, null, [], []), Ct));
        await ReadAsync<SupplierCompanyDto>(await client.PutAsJsonAsync($"/api/suppliers/{supplier.Id}/company", new SaveSupplierCompanyRequest(null, null, true), Ct));
        return new Fixture(client, company.Id, kt, kp, item.Id, uom, supplier.Id, jan, feb, company.Code);
    }

    private static async Task<InventoryDocumentDto> PostedAsync(Fixture f, HttpResponseMessage created)
    {
        var doc = await ReadAsync<InventoryDocumentDto>(created);
        return await ReadAsync<InventoryDocumentDto>(await f.Client.PostAsync($"/api/inventory-documents/{doc.Id}/post", null, Ct));
    }

    private static Task<HttpResponseMessage> ReceiptAsync(Fixture f, DateOnly date, decimal qty, decimal price) =>
        f.Client.PostAsJsonAsync("/api/goods-receipts",
            new SaveReceiptRequest(date, f.Kt, f.SupplierId, null, null, null, null, null, null, [new(null, f.ItemId, f.Uom, qty, price, "10", null, null, null)]), Ct);

    private static Task<HttpResponseMessage> CloseAsync(Fixture f, Guid period) => f.Client.PostAsync($"/api/inventory-periods/{period}/close", null, Ct);

    private static Task<HttpResponseMessage> ReopenAsync(Fixture f, Guid period, string reason) =>
        f.Client.PostAsJsonAsync($"/api/inventory-periods/{period}/reopen", new ReopenPeriodRequest(reason), Ct);

    /// <summary>
    /// Tháng 1: nhập 100 × 10.000. Tháng 2: nhập 50 × 13.000, trả NCC 10 từ phiếu đó, chuyển 120 từ KT sang KP (design.md mục 7.3).
    /// </summary>
    private static async Task<InventoryDocumentDto> ArrangeDesignExampleAsync(Fixture f)
    {
        await PostedAsync(f, await ReceiptAsync(f, new DateOnly(2025, 1, 10), 100, 10_000));
        var february = await PostedAsync(f, await ReceiptAsync(f, new DateOnly(2025, 2, 5), 50, 13_000));
        await PostedAsync(f, await f.Client.PostAsJsonAsync("/api/goods-returns",
            new SaveReturnRequest(new DateOnly(2025, 2, 6), february.Id, null, "Hàng lỗi", [new(null, february.Lines[0].Id, 10, null)]), Ct));
        return await PostedAsync(f, await f.Client.PostAsJsonAsync("/api/stock-transfers",
            new SaveTransferRequest(new DateOnly(2025, 2, 7), f.Kt, f.Kp, null, null, null, [new(null, f.ItemId, f.Uom, 120, null, null)]), Ct));
    }

    private async Task<T> InCompanyAsync<T>(Fixture f, Func<ProcDbContext, Task<T>> query)
    {
        using var scope = _factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<CurrentUserScope>().Initialize(Guid.Empty, f.CompanyId, [], true, [], true, []);
        return await query(scope.ServiceProvider.GetRequiredService<ProcDbContext>());
    }

    [Fact]
    public async Task DongKy_GiaVonBinhQuan_ViDuThietKe_SoDuTheoKho()
    {
        var f = await ArrangeAsync();
        var transfer = await ArrangeDesignExampleAsync(f);

        Assert.Equal("Closed", (await ReadAsync<InventoryPeriodDto>(await CloseAsync(f, f.Jan))).Status);
        var feb = await ReadAsync<InventoryPeriodDto>(await CloseAsync(f, f.Feb));
        Assert.Equal("Closed", feb.Status);
        Assert.NotNull(feb.ClosedAt);

        var (costs, balances, transferRows) = await InCompanyAsync(f, async db => (
            await db.ItemPeriodCosts.Where(c => c.ItemId == f.ItemId).OrderBy(c => c.CalculatedAt).Select(c => c.UnitCost).ToListAsync(Ct),
            await db.StockPeriodBalances.Where(b => b.PeriodId == f.Feb).ToListAsync(Ct),
            await db.StockLedger.Where(l => l.DocumentId == transfer.Id).ToListAsync(Ct)));

        // Giá vốn tháng 1 = 10.000; tháng 2 = 1.520.000 ÷ 140 = 10.857,142857.
        Assert.Equal([10_000m, 10_857.142857m], costs);
        // Chuyển kho theo giá vốn kỳ, đã chốt: 120 × 10.857,142857 = 1.302.857,14 ở cả dòng xuất và dòng nhập.
        Assert.All(transferRows, r => Assert.Equal((10_857.142857m, 1_302_857.14m, true), (r.UnitCost, r.Amount, r.IsCostFinal)));
        var kt = balances.Single(b => b.WarehouseId == f.Kt);
        Assert.Equal((100m, 1_000_000m, 50m, 650_000m, 130m, 1_432_857.14m, 20m, 217_142.86m),
            (kt.OpenQty, kt.OpenAmount, kt.InQty, kt.InAmount, kt.OutQty, kt.OutAmount, kt.CloseQty, kt.CloseAmount));
        var kp = balances.Single(b => b.WarehouseId == f.Kp);
        Assert.Equal((0m, 120m, 120m, 1_302_857.14m), (kp.OpenQty, kp.InQty, kp.CloseQty, kp.CloseAmount));
        // Giá trị tồn cuối công ty = 140 × 10.857,142857 = 1.520.000.
        Assert.Equal(1_520_000m, balances.Sum(b => b.CloseAmount));

        // Kỳ đã đóng: không ghi sổ, không bỏ ghi sổ.
        var late = await ReadAsync<InventoryDocumentDto>(await ReceiptAsync(f, new DateOnly(2025, 2, 20), 1, 1));
        Assert.Contains("đã đóng", await TitleAsync(await f.Client.PostAsync($"/api/inventory-documents/{late.Id}/post", null, Ct)));
        Assert.Contains("không còn mở", await TitleAsync(await f.Client.PostAsync($"/api/inventory-documents/{transfer.Id}/unpost", null, Ct)));
    }

    [Fact]
    public async Task BaoCaoNxt_VaTheKho_DocSoDuKyDaDong()
    {
        var f = await ArrangeAsync();
        await ArrangeDesignExampleAsync(f);
        await ReadAsync<InventoryPeriodDto>(await CloseAsync(f, f.Jan));

        // Tháng 2 chưa đóng: tồn đầu lấy số dư tháng 1, giá trị chuyển kho là tạm tính.
        var open = await f.Client.GetFromJsonAsync<StockMovementResult>($"/api/reports/stock-movement?periodId={f.Feb}", Ct);
        Assert.False(open!.IsFinal);
        Assert.Equal((100m, 1_000_000m), (open.Items.Single(r => r.WarehouseId == f.Kt).OpenQty, open.Items.Single(r => r.WarehouseId == f.Kt).OpenAmount));

        await ReadAsync<InventoryPeriodDto>(await CloseAsync(f, f.Feb));
        var report = await f.Client.GetFromJsonAsync<StockMovementResult>($"/api/reports/stock-movement?periodId={f.Feb}", Ct);
        Assert.Equal(("02/2025", true), (report!.PeriodLabel, report.IsFinal));
        var kt = report.Items.Single(r => r.WarehouseId == f.Kt);
        Assert.Equal((100m, 50m, 650_000m, 10m, 130_000m, 120m, 1_302_857.14m, 20m, 217_142.86m),
            (kt.OpenQty, kt.PurchaseQty, kt.PurchaseAmount, kt.ReturnQty, kt.ReturnAmount, kt.TransferOutQty, kt.TransferOutAmount, kt.CloseQty, kt.CloseAmount));
        var kp = report.Items.Single(r => r.WarehouseId == f.Kp);
        Assert.Equal((120m, 1_302_857.14m, 120m), (kp.TransferInQty, kp.TransferInAmount, kp.CloseQty));

        // Thẻ kho từ tháng 3: tồn đầu = tồn cuối tháng 2 đã chốt.
        var card = await f.Client.GetFromJsonAsync<StockCardDto>(
            $"/api/stock-card?itemId={f.ItemId}&warehouseId={f.Kt}&from=2025-03-01&to=2025-03-31", Ct);
        Assert.Equal((20m, 217_142.86m), (card!.OpeningQty, card.OpeningAmount));
    }

    [Fact]
    public async Task DongKy_PhaiTuanTu_KhongConPhieuNhap()
    {
        var f = await ArrangeAsync();
        await PostedAsync(f, await ReceiptAsync(f, new DateOnly(2025, 1, 10), 5, 1_000));

        Assert.Equal("Phải đóng kỳ 01/2025 trước (đóng kỳ tuần tự theo tháng).", await TitleAsync(await CloseAsync(f, f.Feb)));

        var draft = await ReadAsync<InventoryDocumentDto>(await ReceiptAsync(f, new DateOnly(2025, 1, 15), 1, 1_000));
        var blocked = await CloseAsync(f, f.Jan);
        Assert.Equal(HttpStatusCode.BadRequest, blocked.StatusCode);
        Assert.Contains($"còn 1 phiếu nháp ({draft.No})", await TitleAsync(blocked));
        // Kiểm tra trước đóng lỗi thì kỳ vẫn Đang mở, ghi sổ tiếp được.
        await ReadAsync<InventoryDocumentDto>(await f.Client.PostAsync($"/api/inventory-documents/{draft.Id}/post", null, Ct));

        Assert.Equal("Closed", (await ReadAsync<InventoryPeriodDto>(await CloseAsync(f, f.Jan))).Status);
    }

    [Fact]
    public async Task MoLaiKy_ChiKyDongGanNhat_CanLyDo_XoaSoDu_GiaVeTamTinh()
    {
        var f = await ArrangeAsync();
        var transfer = await ArrangeDesignExampleAsync(f);
        await ReadAsync<InventoryPeriodDto>(await CloseAsync(f, f.Jan));
        await ReadAsync<InventoryPeriodDto>(await CloseAsync(f, f.Feb));

        Assert.Equal("Phải mở lại kỳ 02/2025 trước (chỉ mở lại kỳ đã đóng gần nhất).", await TitleAsync(await ReopenAsync(f, f.Jan, "Sửa giá")));
        Assert.Equal("Phải nhập lý do mở lại kỳ.", await TitleAsync(await ReopenAsync(f, f.Feb, " ")));

        // Kế toán (quyền xuất dữ liệu kế toán) nhận thông báo kỳ mở lại (design.md mục 7.4, 11.6).
        var accountantRole = $"KT{Guid.NewGuid():N}".ToUpperInvariant()[..10];
        await _factory.CreateRoleAsync(accountantRole, Proc.Domain.Organization.Permissions.Report.AccountingExport);
        var accountantName = $"ke.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(accountantName, f.CompanyCode, accountantRole);

        var reopened = await ReadAsync<InventoryPeriodDto>(await ReopenAsync(f, f.Feb, "Nhập sai giá phiếu tháng 2"));
        Assert.Equal(("Open", "Nhập sai giá phiếu tháng 2"), (reopened.Status, reopened.ReopenReason));
        var accountant = await _factory.CreateClientInCompanyAsync(accountantName, ProcApiFactory.TestPassword, f.CompanyCode);
        var note = Assert.Single(await NotificationApi.MineAsync(accountant, Ct));
        Assert.Equal(("PeriodReopened", "PER", f.Feb), (note.Type, note.ScreenCode, note.TargetId));
        Assert.Contains("02/2025", note.Message);
        Assert.Contains("Nhập sai giá phiếu tháng 2", note.Message);

        var (balances, costs, final) = await InCompanyAsync(f, async db => (
            await db.StockPeriodBalances.CountAsync(b => b.PeriodId == f.Feb, Ct),
            await db.ItemPeriodCosts.CountAsync(c => c.PeriodId == f.Feb, Ct),
            await db.StockLedger.Where(l => l.DocumentId == transfer.Id).Select(l => l.IsCostFinal).ToListAsync(Ct)));
        Assert.Equal((0, 0), (balances, costs));
        Assert.All(final, Assert.False);

        // Mở lại rồi bỏ ghi sổ được; đóng lại cho kết quả như cũ.
        await ReadAsync<InventoryDocumentDto>(await f.Client.PostAsync($"/api/inventory-documents/{transfer.Id}/unpost", null, Ct));
        await ReadAsync<InventoryDocumentDto>(await f.Client.PostAsync($"/api/inventory-documents/{transfer.Id}/post", null, Ct));
        Assert.Equal("Closed", (await ReadAsync<InventoryPeriodDto>(await CloseAsync(f, f.Feb))).Status);
    }

    [Fact]
    public async Task DongMoKy_CanQuyen()
    {
        var userName = $"tk.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(userName, "BHA", "THUKHO");
        var keeper = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, "BHA");

        Assert.Equal(HttpStatusCode.Forbidden, (await keeper.PostAsync($"/api/inventory-periods/{Guid.NewGuid()}/close", null, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await keeper.PostAsJsonAsync($"/api/inventory-periods/{Guid.NewGuid()}/reopen", new ReopenPeriodRequest("x"), Ct)).StatusCode);
    }

}
