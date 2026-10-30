using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Proc.Application.Common;
using Proc.Application.Inventory;
using Proc.Application.MasterData;
using Proc.Application.Organization;
using Proc.Infrastructure.Persistence;
using Proc.Infrastructure.Security;

namespace Proc.IntegrationTests;

/// <summary>
/// Phiếu xuất trả NCC (design.md mục 10.5) và phiếu chuyển kho nội bộ (mục 10.6): phiếu nhập gốc, số còn trả được, giá gốc;
/// hai kho cùng công ty, giá vốn tạm tính, không tồn âm.
/// </summary>
[Collection(ApiCollection.Name)]
public class ReturnTransferTests
{
    private readonly ProcApiFactory _factory;

    public ReturnTransferTests(ProcApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string UniqueCode(string prefix) => $"{prefix}{Guid.NewGuid():N}".ToUpperInvariant()[..10];

    private static DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTime.UtcNow, VietnamTime.Zone));

    private static async Task<string?> TitleAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(Ct))?.Title;

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(Ct)}");
        return (await response.Content.ReadFromJsonAsync<T>(Ct))!;
    }

    /// <summary>Mặt hàng mới (Hộp, Thùng = 24); phiếu nhập 2 thùng × 480.000 đã ghi sổ vào kho KT của BHA (48 hộp, 20.000 đ/hộp).</summary>
    private sealed record Fixture(HttpClient Bha, ItemDetailDto Item, Guid Hop, Guid Thung, Guid Kt, Guid Kp1, InventoryDocumentDto Receipt);

    private async Task<Fixture> ArrangeAsync()
    {
        var userName = $"qt.{Guid.NewGuid():N}"[..16];
        await _factory.CreateUserAsync(userName, ProcApiFactory.TestPassword, ["TCT", "BHA"]);
        var head = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, "TCT");
        var bha = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, "BHA");
        var uoms = (await head.GetFromJsonAsync<List<UomDto>>("/api/uoms", Ct))!.ToDictionary(u => u.Code, u => u.Id);
        var category = await ReadAsync<ItemCategoryDetailDto>(await head.PostAsJsonAsync("/api/item-categories",
            new CreateItemCategoryRequest(UniqueCode("R"), "Nhóm trả/chuyển", null, null), Ct));
        var item = await ReadAsync<ItemDetailDto>(await head.PostAsJsonAsync("/api/items",
            new CreateItemRequest($"{category.Code}-01", "Bu lông trả/chuyển", null, "Inventory", category.Id, uoms["HOP"], null, null, null,
                null, "10", null, false, false, null, null, [new(uoms["THUNG"], 24m)]), Ct));
        var groupId = (await head.GetFromJsonAsync<List<SupplierGroupDto>>("/api/supplier-groups", Ct))!.Single(g => g.Code == "TN").Id;
        var supplier = await ReadAsync<SupplierDetailDto>(await head.PostAsJsonAsync("/api/suppliers",
            new SaveSupplierRequest(UniqueCode("N"), groupId, "NCC trả/chuyển", null, false, null, null, null, null, null, null, null, null,
                "Approved", null, null, [], []), Ct));
        await ReadAsync<SupplierCompanyDto>(await bha.PutAsJsonAsync($"/api/suppliers/{supplier.Id}/company", new SaveSupplierCompanyRequest(null, null, true), Ct));
        var warehouses = (await bha.GetFromJsonAsync<List<WarehouseDetailDto>>("/api/warehouses", Ct))!;
        var kt = warehouses.Single(w => w.Code == "KT").Id;
        var receipt = await ReadAsync<InventoryDocumentDto>(await bha.PostAsJsonAsync("/api/goods-receipts",
            new SaveReceiptRequest(Today, kt, supplier.Id, null, null, null, null, null, null,
                [new(null, item.Id, uoms["THUNG"], 2, 480_000, "10", null, null, null)]), Ct));
        receipt = await ReadAsync<InventoryDocumentDto>(await bha.PostAsync($"/api/inventory-documents/{receipt.Id}/post", null, Ct));
        return new Fixture(bha, item, uoms["HOP"], uoms["THUNG"], kt, warehouses.Single(w => w.Code == "KP1").Id, receipt);
    }

    private static Task<HttpResponseMessage> PostAsync(Fixture f, Guid id) => f.Bha.PostAsync($"/api/inventory-documents/{id}/post", null, Ct);

    private static Task<HttpResponseMessage> UnpostAsync(Fixture f, Guid id) => f.Bha.PostAsync($"/api/inventory-documents/{id}/unpost", null, Ct);

    private static Task<HttpResponseMessage> CreateReturnAsync(Fixture f, decimal boxes) =>
        f.Bha.PostAsJsonAsync("/api/goods-returns",
            new SaveReturnRequest(Today, f.Receipt.Id, "HD-DC-01", "Hàng lỗi", [new(null, f.Receipt.Lines[0].Id, boxes, null)]), Ct);

    private static Task<HttpResponseMessage> CreateTransferAsync(Fixture f, decimal boxes, Guid? to = null) =>
        f.Bha.PostAsJsonAsync("/api/stock-transfers",
            new SaveTransferRequest(Today, f.Kt, to ?? f.Kp1, "Nguyễn An", "Trần Bình", "Cấp cho xưởng", [new(null, f.Item.Id, f.Thung, boxes, null, null)]), Ct);

    /// <summary>Tồn theo kho và sổ kho của mặt hàng (đọc thẳng CSDL ở BHA).</summary>
    private async Task<(decimal Kt, decimal Kp1, List<(string DocType, int Direction, decimal Qty, decimal UnitCost, decimal Amount, bool Final)> Ledger)> StockAsync(Fixture f)
    {
        using var scope = _factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<CurrentUserScope>().Initialize(Guid.Empty, await _factory.GetCompanyIdAsync("BHA"), [], true, [], true, []);
        var db = scope.ServiceProvider.GetRequiredService<ProcDbContext>();
        var onHand = await db.StockOnHand.Where(s => s.ItemId == f.Item.Id).ToListAsync(Ct);
        var ledger = await db.StockLedger.Where(l => l.ItemId == f.Item.Id).OrderBy(l => l.Id).ToListAsync(Ct);
        return (onHand.Where(s => s.WarehouseId == f.Kt).Sum(s => s.Qty), onHand.Where(s => s.WarehouseId == f.Kp1).Sum(s => s.Qty),
            ledger.Select(l => (l.DocType, l.Direction, l.BaseQty, l.UnitCost, l.Amount, l.IsCostFinal)).ToList());
    }

    [Fact]
    public async Task TraNcc_TheoPhieuNhapGoc_GiaGoc_TruTon_ConTraDuocGiam()
    {
        var f = await ArrangeAsync();
        var before = await f.Bha.GetFromJsonAsync<List<ReturnableLineDto>>($"/api/goods-receipts/{f.Receipt.Id}/returnable", Ct);
        Assert.Equal((2m, 0m, 2m, 480_000m), (before![0].ReceivedQty, before[0].ReturnedQty, before[0].RemainingQty, before[0].UnitPrice));

        var ret = await ReadAsync<InventoryDocumentDto>(await CreateReturnAsync(f, 1));
        Assert.Matches($"^RTV-BHA-{Today:yyMM}-\\d{{4}}$", ret.No);
        Assert.Equal((f.Kt, f.Receipt.SupplierId, f.Receipt.Id, f.Receipt.No), (ret.FromWarehouseId, ret.SupplierId, ret.LinkedDocumentId, ret.LinkedDocumentNo));
        Assert.Equal((480_000m, 48_000m, f.Receipt.Lines[0].Id), (ret.TotalAmount, ret.TotalVat, ret.Lines[0].SourceLineId));

        await ReadAsync<InventoryDocumentDto>(await PostAsync(f, ret.Id));
        var stock = await StockAsync(f);
        Assert.Equal(24m, stock.Kt);
        Assert.Contains(("RTV", -1, 24m, 20_000m, 480_000m, true), stock.Ledger);
        var after = await f.Bha.GetFromJsonAsync<List<ReturnableLineDto>>($"/api/goods-receipts/{f.Receipt.Id}/returnable", Ct);
        Assert.Equal((1m, 1m), (after![0].ReturnedQty, after[0].RemainingQty));

        var tooMany = await CreateReturnAsync(f, 2);
        Assert.Contains("vượt số còn trả được 1", await TitleAsync(tooMany));

        // Phiếu nhập đã có phiếu trả ghi sổ thì không bỏ ghi sổ được; bỏ phiếu trả trước thì được.
        Assert.Contains("đã có phiếu trả NCC ghi sổ", await TitleAsync(await UnpostAsync(f, f.Receipt.Id)));
        await ReadAsync<InventoryDocumentDto>(await UnpostAsync(f, ret.Id));
        await ReadAsync<InventoryDocumentDto>(await UnpostAsync(f, f.Receipt.Id));
        Assert.Equal(0m, (await StockAsync(f)).Kt);
    }

    [Fact]
    public async Task TraNcc_HaiPhieuNhapTraLo_PhieuSauBiChanLucGhiSo()
    {
        var f = await ArrangeAsync();
        var first = await ReadAsync<InventoryDocumentDto>(await CreateReturnAsync(f, 2));
        var second = await ReadAsync<InventoryDocumentDto>(await CreateReturnAsync(f, 1));

        await ReadAsync<InventoryDocumentDto>(await PostAsync(f, first.Id));
        var blocked = await PostAsync(f, second.Id);

        Assert.Equal(HttpStatusCode.BadRequest, blocked.StatusCode);
        Assert.Contains("vượt số còn trả được 0", await TitleAsync(blocked));
    }

    [Fact]
    public async Task TraNcc_PhieuGocChuaGhiSo_KhongLapDuoc()
    {
        var f = await ArrangeAsync();
        await ReadAsync<InventoryDocumentDto>(await UnpostAsync(f, f.Receipt.Id));

        var response = await CreateReturnAsync(f, 1);

        Assert.Contains("phải là phiếu nhập mua đã ghi sổ", await TitleAsync(response));
    }

    [Fact]
    public async Task ChuyenKho_HaiDongSoCungGiaTamTinh_TruKhoDiCongKhoDen()
    {
        var f = await ArrangeAsync();

        var transfer = await ReadAsync<InventoryDocumentDto>(await CreateTransferAsync(f, 1));
        Assert.Matches($"^TRF-BHA-{Today:yyMM}-\\d{{4}}$", transfer.No);
        Assert.Equal((f.Kt, f.Kp1, "Trần Bình", 0m), (transfer.FromWarehouseId, transfer.ToWarehouseId, transfer.Receiver, transfer.TotalAmount));

        var available = await f.Bha.GetFromJsonAsync<List<StockAvailabilityDto>>($"/api/stock-on-hand/available?warehouseId={f.Kt}&itemIds={f.Item.Id}", Ct);
        Assert.Equal(48m, Assert.Single(available!).Qty);

        await ReadAsync<InventoryDocumentDto>(await PostAsync(f, transfer.Id));
        var stock = await StockAsync(f);
        Assert.Equal((24m, 24m), (stock.Kt, stock.Kp1));
        // Giá vốn tạm tính = 960.000 ÷ 48 = 20.000 đ/hộp; chưa chốt (đóng kỳ tính lại).
        Assert.Contains(("TRF", -1, 24m, 20_000m, 480_000m, false), stock.Ledger);
        Assert.Contains(("TRF", 1, 24m, 20_000m, 480_000m, false), stock.Ledger);

        await ReadAsync<InventoryDocumentDto>(await UnpostAsync(f, transfer.Id));
        var back = await StockAsync(f);
        Assert.Equal((48m, 0m), (back.Kt, back.Kp1));
    }

    [Fact]
    public async Task ChuyenKho_VuotTon_TrungKho_KhoCongTyKhac_BiChan()
    {
        var f = await ArrangeAsync();

        var tooMuch = await ReadAsync<InventoryDocumentDto>(await CreateTransferAsync(f, 3));
        var post = await PostAsync(f, tooMuch.Id);
        Assert.Contains("tại kho KT sẽ âm (-24)", await TitleAsync(post));
        Assert.Equal((48m, 0m), ((await StockAsync(f)).Kt, (await StockAsync(f)).Kp1));

        Assert.Equal("Kho xuất và kho nhập phải khác nhau.", await TitleAsync(await CreateTransferAsync(f, 1, f.Kt)));

        // Kho Sub/kho tổng của công ty khác như không tồn tại – không chuyển sang công ty khác được (CLAUDE.md mục 4).
        var admin = await _factory.CreateClientInCompanyAsync(ProcApiFactory.AdminUserName, ProcApiFactory.AdminPassword, "NMI");
        var nmiKt = (await admin.GetFromJsonAsync<List<WarehouseDetailDto>>("/api/warehouses", Ct))!.Single(w => w.Code == "KT").Id;
        Assert.Equal(HttpStatusCode.NotFound, (await CreateTransferAsync(f, 1, nmiKt)).StatusCode);
    }
}
