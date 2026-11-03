using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PdfSharp.Pdf.IO;
using Proc.Application.Inventory;
using Proc.Application.MasterData;
using Proc.Application.Organization;
using Proc.Domain.Organization;
using Proc.Infrastructure.Persistence;
using Proc.Infrastructure.Security;

namespace Proc.IntegrationTests;

/// <summary>
/// Phiếu điều chỉnh / kiểm kê kho – ADJ (design.md mục 6.1, 6.7): tăng có giá nhập tay, giảm theo giá vốn, bắt buộc lý do và số biên bản
/// kiểm kê, quyền riêng; giá vốn cuối kỳ coi nhập điều chỉnh là "nhập có giá" (mục 7.3). Công ty mới, lịch năm 2025.
/// </summary>
[Collection(ApiCollection.Name)]
public class StockAdjustmentTests
{
    private readonly ProcApiFactory _factory;

    public StockAdjustmentTests(ProcApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string UniqueCode(string prefix) => $"{prefix}{Guid.NewGuid():N}".ToUpperInvariant()[..10];

    private static async Task<string?> TitleAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(Ct))?.Title;

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(Ct)}");
        return (await response.Content.ReadFromJsonAsync<T>(Ct))!;
    }

    private sealed record Fixture(HttpClient Client, Guid CompanyId, string CompanyCode, Guid Kt, Guid ItemId, Guid Uom, Guid SupplierId, Guid Jan);

    /// <summary>Công ty mới (kho KT), lịch 2025 mở tháng 1, một mặt hàng (ĐVT cái), một NCC áp dụng tại công ty.</summary>
    private async Task<Fixture> ArrangeAsync()
    {
        var userName = $"dc.{Guid.NewGuid():N}"[..16];
        await _factory.CreateUserAsync(userName, ProcApiFactory.TestPassword, ["TCT"]);
        var head = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, "TCT");
        var company = await ReadAsync<CompanyDetailDto>(await head.PostAsJsonAsync("/api/companies",
            new CreateCompanyRequest(UniqueCode("A"), "Công ty thử điều chỉnh kho", null, null, null, null, new DateOnly(2025, 1, 1), "KT", "Kho tổng"), Ct));
        var client = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, company.Code);
        var kt = (await client.GetFromJsonAsync<List<WarehouseDetailDto>>("/api/warehouses", Ct))!.Single().Id;
        var calendar = await ReadAsync<InventoryCalendarDto>(await client.PostAsJsonAsync("/api/inventory-periods/years", new CreateInventoryYearRequest(2025), Ct));
        var jan = calendar.Periods.Single(p => p.Month == 1).Id;
        await ReadAsync<InventoryPeriodDto>(await client.PostAsync($"/api/inventory-periods/{jan}/open", null, Ct));
        var uom = (await head.GetFromJsonAsync<List<UomDto>>("/api/uoms", Ct))!.Single(u => u.Code == "CAI").Id;
        var category = await ReadAsync<ItemCategoryDetailDto>(await head.PostAsJsonAsync("/api/item-categories",
            new CreateItemCategoryRequest(null, "Nhóm điều chỉnh", null, null), Ct));
        var item = await ReadAsync<ItemDetailDto>(await head.PostAsJsonAsync("/api/items",
            new CreateItemRequest(null, "Bu lông kiểm kê", null, "Inventory", category.Id, uom, null, null, null,
                null, "10", null, false, false, null, null, []), Ct));
        var groupId = (await head.GetFromJsonAsync<List<SupplierGroupDto>>("/api/supplier-groups", Ct))!.Single(g => g.Code == "TN").Id;
        var supplier = await ReadAsync<SupplierDetailDto>(await head.PostAsJsonAsync("/api/suppliers",
            new SaveSupplierRequest(null, groupId, "NCC điều chỉnh", null, false, null, null, null, null, null, null, null, null,
                "Approved", null, null, [], []), Ct));
        await _factory.FinishSupplierApprovalAsync(supplier.Id);
        await ReadAsync<SupplierCompanyDto>(await client.PutAsJsonAsync($"/api/suppliers/{supplier.Id}/company", new SaveSupplierCompanyRequest(null, null, true), Ct));
        return new Fixture(client, company.Id, company.Code, kt, item.Id, uom, supplier.Id, jan);
    }

    private static async Task<InventoryDocumentDto> PostedAsync(HttpClient client, HttpResponseMessage created)
    {
        var doc = await ReadAsync<InventoryDocumentDto>(created);
        return await ReadAsync<InventoryDocumentDto>(await client.PostAsync($"/api/inventory-documents/{doc.Id}/post", null, Ct));
    }

    private static SaveAdjustmentRequest Adjustment(Fixture f, DateOnly date, params SaveAdjustmentLineRequest[] lines) =>
        new(date, f.Kt, "Kiểm kê định kỳ tháng 1", "BBKK-01/2025", null, lines);

    private async Task<decimal> StockAsync(Fixture f)
    {
        using var scope = _factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<CurrentUserScope>().Initialize(Guid.Empty, f.CompanyId, [], true, [], true, []);
        var db = scope.ServiceProvider.GetRequiredService<ProcDbContext>();
        return await db.StockOnHand.Where(s => s.ItemId == f.ItemId).SumAsync(s => s.Qty, Ct);
    }

    [Fact]
    public async Task DieuChinhKho_TangGiaNhapTay_GiamTheoGiaVon_QuyenRieng_DongKyTinhGiaVon()
    {
        var f = await ArrangeAsync();
        await PostedAsync(f.Client, await f.Client.PostAsJsonAsync("/api/goods-receipts",
            new SaveReceiptRequest(new DateOnly(2025, 1, 5), f.Kt, f.SupplierId, null, null, null, null, null, null,
                [new(null, f.ItemId, f.Uom, 100, 10_000, "10", null, null, null)]), Ct));
        var increase = new SaveAdjustmentLineRequest(null, f.ItemId, f.Uom, 1, 10, 12_000, null, null, "Thừa khi kiểm kê");
        var decrease = new SaveAdjustmentLineRequest(null, f.ItemId, f.Uom, -1, 30, 0, null, null, "Thiếu khi kiểm kê");

        // Bắt buộc lý do và số biên bản kiểm kê (design.md mục 6.1).
        var noReason = await f.Client.PostAsJsonAsync("/api/stock-adjustments",
            Adjustment(f, new DateOnly(2025, 1, 20), increase) with { Reason = " ", CountReportNo = null }, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, noReason.StatusCode);
        // Dòng giảm không nhập giá: xuất theo giá vốn.
        Assert.Contains("không nhập đơn giá", await TitleAsync(await f.Client.PostAsJsonAsync("/api/stock-adjustments",
            Adjustment(f, new DateOnly(2025, 1, 20), decrease with { UnitPrice = 5_000 }), Ct)));

        var adj = await ReadAsync<InventoryDocumentDto>(await f.Client.PostAsJsonAsync("/api/stock-adjustments",
            Adjustment(f, new DateOnly(2025, 1, 20), increase, decrease), Ct));
        Assert.Equal(("ADJ", "Draft", "Kiểm kê định kỳ tháng 1", "BBKK-01/2025"), (adj.DocType, adj.Status, adj.Reason, adj.CountReportNo));
        Assert.Equal([1, -1], adj.Lines.Select(l => l.Direction));
        Assert.Equal(120_000m, adj.TotalAmount);
        Assert.Matches(@"^ADJ-", adj.No);

        // Quyền riêng: có quyền ghi sổ chứng từ kho thường nhưng không có quyền ghi sổ điều chỉnh thì không ghi sổ được.
        var roleCode = UniqueCode("K");
        await _factory.CreateRoleAsync(roleCode, Permissions.Inventory.DocumentView, Permissions.Inventory.DocumentCreate, Permissions.Inventory.DocumentPost);
        var keeperName = $"kk.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(keeperName, f.CompanyCode, roleCode);
        var keeper = await _factory.CreateClientInCompanyAsync(keeperName, ProcApiFactory.TestPassword, f.CompanyCode);
        Assert.False((await ReadAsync<InventoryDocumentDto>(await keeper.GetAsync($"/api/inventory-documents/{adj.Id}", Ct))).CanPost);
        Assert.Equal(HttpStatusCode.Forbidden, (await keeper.PostAsync($"/api/inventory-documents/{adj.Id}/post", null, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await keeper.PostAsJsonAsync("/api/stock-adjustments",
            Adjustment(f, new DateOnly(2025, 1, 20), increase), Ct)).StatusCode);

        var posted = await ReadAsync<InventoryDocumentDto>(await f.Client.PostAsync($"/api/inventory-documents/{adj.Id}/post", null, Ct));
        Assert.Equal("Posted", posted.Status);
        Assert.Equal(80m, await StockAsync(f));

        // Giảm quá tồn: chặn tồn âm, không ghi gì.
        var tooMuch = await ReadAsync<InventoryDocumentDto>(await f.Client.PostAsJsonAsync("/api/stock-adjustments",
            Adjustment(f, new DateOnly(2025, 1, 21), decrease with { Qty = 1_000 }), Ct));
        Assert.Contains("âm", await TitleAsync(await f.Client.PostAsync($"/api/inventory-documents/{tooMuch.Id}/post", null, Ct)));
        Assert.Equal(80m, await StockAsync(f));
        await ReadAsync<InventoryDocumentDto>(await f.Client.PostAsync($"/api/inventory-documents/{tooMuch.Id}/cancel", null, Ct));

        // Đóng kỳ: giá vốn = (1.000.000 + 10 × 12.000) ÷ (100 + 10) = 1.120.000 ÷ 110 = 10.181,818182;
        // dòng giảm nhận giá vốn kỳ: 30 × 10.181,818182 = 305.454,55; tồn cuối 80 × giá = 814.545,45.
        Assert.Equal("Closed", (await ReadAsync<InventoryPeriodDto>(await f.Client.PostAsync($"/api/inventory-periods/{f.Jan}/close", null, Ct))).Status);
        using (var scope = _factory.Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<CurrentUserScope>().Initialize(Guid.Empty, f.CompanyId, [], true, [], true, []);
            var db = scope.ServiceProvider.GetRequiredService<ProcDbContext>();
            Assert.Equal(10_181.818182m, await db.ItemPeriodCosts.Where(c => c.ItemId == f.ItemId).Select(c => c.UnitCost).SingleAsync(Ct));
            var rows = await db.StockLedger.Where(l => l.DocumentId == adj.Id).OrderByDescending(l => l.Direction).ToListAsync(Ct);
            Assert.Equal((1, 12_000m, 120_000m, true), (rows[0].Direction, rows[0].UnitCost, rows[0].Amount, rows[0].IsCostFinal));
            Assert.Equal((-1, 10_181.818182m, 305_454.55m, true), (rows[1].Direction, rows[1].UnitCost, rows[1].Amount, rows[1].IsCostFinal));
            Assert.Equal(814_545.45m, await db.StockPeriodBalances.Where(b => b.PeriodId == f.Jan).SumAsync(b => b.CloseAmount, Ct));
        }

        // Kỳ đã đóng: không bỏ ghi sổ phiếu điều chỉnh. Mở lại kỳ: dòng tăng giữ giá nhập tay, dòng giảm về tạm tính.
        Assert.Contains("không còn mở", await TitleAsync(await f.Client.PostAsync($"/api/inventory-documents/{adj.Id}/unpost", null, Ct)));
        await ReadAsync<InventoryPeriodDto>(await f.Client.PostAsJsonAsync($"/api/inventory-periods/{f.Jan}/reopen", new ReopenPeriodRequest("Kiểm tra lại"), Ct));
        using (var scope = _factory.Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<CurrentUserScope>().Initialize(Guid.Empty, f.CompanyId, [], true, [], true, []);
            var db = scope.ServiceProvider.GetRequiredService<ProcDbContext>();
            var finals = await db.StockLedger.Where(l => l.DocumentId == adj.Id).OrderByDescending(l => l.Direction).Select(l => l.IsCostFinal).ToListAsync(Ct);
            Assert.Equal([true, false], finals);
        }

        // Kỳ mở lại: bỏ ghi sổ được, tồn về như trước điều chỉnh.
        Assert.Equal("Draft", (await ReadAsync<InventoryDocumentDto>(await f.Client.PostAsync($"/api/inventory-documents/{adj.Id}/unpost", null, Ct))).Status);
        Assert.Equal(100m, await StockAsync(f));
    }

    [Fact]
    public async Task KiemKe_NhapSoThucDem_TuTinhChenhLechSoVoiTonSo_InBienBan()
    {
        var f = await ArrangeAsync();
        await PostedAsync(f.Client, await f.Client.PostAsJsonAsync("/api/goods-receipts",
            new SaveReceiptRequest(new DateOnly(2025, 1, 5), f.Kt, f.SupplierId, null, null, null, null, null, null,
                [new(null, f.ItemId, f.Uom, 100, 10_000, "10", null, null, null)]), Ct));

        // Bảng kiểm kê: tồn sổ của kho theo ĐVT cơ sở (design.md mục 18.23 – ADJ-3).
        var sheet = await ReadAsync<List<CountSheetRowDto>>(await f.Client.GetAsync($"/api/stock-adjustments/count-sheet?warehouseId={f.Kt}", Ct));
        var row = Assert.Single(sheet);
        Assert.Equal((f.ItemId, f.Uom, 100m), (row.ItemId, row.UomId, row.BookQty));

        // Client gửi chiều/SL sai: server tính lại theo thực đếm – 96 − 100 = giảm 4; tồn sổ ghi lại trên dòng.
        var counted = new SaveAdjustmentLineRequest(null, f.ItemId, f.Uom, 1, 1, 0, null, null, null, CountedQty: 96);
        var adj = await ReadAsync<InventoryDocumentDto>(await f.Client.PostAsJsonAsync("/api/stock-adjustments",
            Adjustment(f, new DateOnly(2025, 1, 20), counted), Ct));
        var line = Assert.Single(adj.Lines);
        Assert.Equal((-1, 4m, 100m, 96m, 0m), (line.Direction, line.Qty, line.BookQty, line.CountedQty, line.UnitPrice));

        // Thực đếm khớp tồn sổ: không có gì để điều chỉnh.
        Assert.Contains("không chênh lệch", await TitleAsync(await f.Client.PostAsJsonAsync("/api/stock-adjustments",
            Adjustment(f, new DateOnly(2025, 1, 20), counted with { CountedQty = 100 }), Ct)));
        // Thực đếm âm bị chặn.
        Assert.Equal(HttpStatusCode.BadRequest, (await f.Client.PostAsJsonAsync("/api/stock-adjustments",
            Adjustment(f, new DateOnly(2025, 1, 20), counted with { CountedQty = -1 }), Ct)).StatusCode);

        // In biên bản kiểm kê, điều chỉnh kho.
        var response = await f.Client.GetAsync($"/api/inventory-documents/{adj.Id}/pdf", Ct);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync(Ct));
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        var pdf = PdfReader.Open(new MemoryStream(await response.Content.ReadAsByteArrayAsync(Ct)), PdfDocumentOpenMode.Import);
        Assert.Equal(1, pdf.PageCount);

        await ReadAsync<InventoryDocumentDto>(await f.Client.PostAsync($"/api/inventory-documents/{adj.Id}/post", null, Ct));
        Assert.Equal(96m, await StockAsync(f));
    }
}
