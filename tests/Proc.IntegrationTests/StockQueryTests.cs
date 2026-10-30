using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Proc.Application.Common;
using Proc.Application.Inventory;
using Proc.Application.MasterData;
using Proc.Application.Organization;

namespace Proc.IntegrationTests;

/// <summary>
/// Tồn kho hiện tại và thẻ kho (design.md mục 11, 12): giá trị tồn theo sổ kho, cảnh báo dưới mức tối thiểu, tồn lũy kế,
/// phạm vi kho của người xem.
/// </summary>
[Collection(ApiCollection.Name)]
public class StockQueryTests
{
    private readonly ProcApiFactory _factory;

    public StockQueryTests(ProcApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string UniqueCode(string prefix) => $"{prefix}{Guid.NewGuid():N}".ToUpperInvariant()[..10];

    private static DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTime.UtcNow, VietnamTime.Zone));

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(Ct)}");
        return (await response.Content.ReadFromJsonAsync<T>(Ct))!;
    }

    /// <summary>
    /// Mặt hàng mới (tồn tối thiểu 60 hộp) trong nhóm mới; nhập 48 hộp × 20.000 vào KT rồi chuyển 24 hộp sang KP1 – cả hai đã ghi sổ.
    /// </summary>
    private sealed record Fixture(HttpClient Bha, ItemDetailDto Item, string CategoryCode, Guid CategoryId, Guid Kt, Guid Kp1, InventoryDocumentDto Transfer);

    private async Task<Fixture> ArrangeAsync(decimal minStock = 60m)
    {
        var userName = $"qt.{Guid.NewGuid():N}"[..16];
        await _factory.CreateUserAsync(userName, ProcApiFactory.TestPassword, ["TCT", "BHA"]);
        var head = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, "TCT");
        var bha = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, "BHA");
        var uoms = (await head.GetFromJsonAsync<List<UomDto>>("/api/uoms", Ct))!.ToDictionary(u => u.Code, u => u.Id);
        var category = await ReadAsync<ItemCategoryDetailDto>(await head.PostAsJsonAsync("/api/item-categories",
            new CreateItemCategoryRequest(UniqueCode("T"), "Nhóm tồn kho", null, null), Ct));
        var item = await ReadAsync<ItemDetailDto>(await head.PostAsJsonAsync("/api/items",
            new CreateItemRequest($"{category.Code}-01", "Đai ốc xem tồn", null, "Inventory", category.Id, uoms["HOP"], null, null, null,
                null, "10", null, false, false, minStock, null, []), Ct));
        var groupId = (await head.GetFromJsonAsync<List<SupplierGroupDto>>("/api/supplier-groups", Ct))!.Single(g => g.Code == "TN").Id;
        var supplier = await ReadAsync<SupplierDetailDto>(await head.PostAsJsonAsync("/api/suppliers",
            new SaveSupplierRequest(UniqueCode("N"), groupId, "NCC xem tồn", null, false, null, null, null, null, null, null, null, null,
                "Approved", null, null, [], []), Ct));
        await ReadAsync<SupplierCompanyDto>(await bha.PutAsJsonAsync($"/api/suppliers/{supplier.Id}/company", new SaveSupplierCompanyRequest(null, null, true), Ct));
        var warehouses = (await bha.GetFromJsonAsync<List<WarehouseDetailDto>>("/api/warehouses", Ct))!;
        var kt = warehouses.Single(w => w.Code == "KT").Id;
        var kp1 = warehouses.Single(w => w.Code == "KP1").Id;
        var receipt = await ReadAsync<InventoryDocumentDto>(await bha.PostAsJsonAsync("/api/goods-receipts",
            new SaveReceiptRequest(Today, kt, supplier.Id, null, null, null, null, null, null,
                [new(null, item.Id, uoms["HOP"], 48, 20_000, "10", null, null, null)]), Ct));
        await ReadAsync<InventoryDocumentDto>(await bha.PostAsync($"/api/inventory-documents/{receipt.Id}/post", null, Ct));
        var transfer = await ReadAsync<InventoryDocumentDto>(await bha.PostAsJsonAsync("/api/stock-transfers",
            new SaveTransferRequest(Today, kt, kp1, null, null, null, [new(null, item.Id, uoms["HOP"], 24, null, null)]), Ct));
        transfer = await ReadAsync<InventoryDocumentDto>(await bha.PostAsync($"/api/inventory-documents/{transfer.Id}/post", null, Ct));
        return new Fixture(bha, item, category.Code, category.Id, kt, kp1, transfer);
    }

    private static string CardUrl(Fixture f, Guid? warehouseId, DateOnly from, DateOnly to) =>
        $"/api/stock-card?itemId={f.Item.Id}&from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}" + (warehouseId is { } w ? $"&warehouseId={w}" : "");

    [Fact]
    public async Task TonKho_TheoKho_GiaTriTheoSoKho_DuoiMucToiThieu()
    {
        var f = await ArrangeAsync();

        var result = await f.Bha.GetFromJsonAsync<StockOnHandResult>($"/api/stock-on-hand?categoryId={f.CategoryId}", Ct);

        var rows = result!.Items;
        Assert.Equal(["KT", "KP1"], rows.Select(r => r.WarehouseCode).OrderByDescending(c => c == "KT"));
        var kt = rows.Single(r => r.WarehouseCode == "KT");
        Assert.Equal((24m, 480_000m, 20_000m, "HOP", f.CategoryCode), (kt.Qty, kt.Amount, kt.UnitCost, kt.UomCode, kt.CategoryCode));
        // Tổng hai kho 48 < tồn tối thiểu 60 → cảnh báo trên mọi dòng của mặt hàng.
        Assert.All(rows, r => Assert.True(r.BelowMin));

        // Lọc một kho: chỉ hiện dòng KP1, cảnh báo vẫn theo tổng cả công ty (48 < 60).
        var onlyKp1 = await f.Bha.GetFromJsonAsync<StockOnHandResult>($"/api/stock-on-hand?categoryId={f.CategoryId}&warehouseId={f.Kp1}", Ct);
        Assert.Equal((24m, 480_000m, true), (Assert.Single(onlyKp1!.Items).Qty, onlyKp1.Items[0].Amount, onlyKp1.Items[0].BelowMin));
    }

    [Fact]
    public async Task TonToiThieu_SoVoiTongCaCongTy_KhongTheoKhoDangXem()
    {
        // Tồn tối thiểu 30; KT 24 + KP1 24 = 48 ≥ 30 → không cảnh báo, kể cả khi chỉ xem KP1 (24 < 30).
        var f = await ArrangeAsync(minStock: 30m);

        var all = await f.Bha.GetFromJsonAsync<StockOnHandResult>($"/api/stock-on-hand?categoryId={f.CategoryId}", Ct);
        var onlyKp1 = await f.Bha.GetFromJsonAsync<StockOnHandResult>($"/api/stock-on-hand?categoryId={f.CategoryId}&warehouseId={f.Kp1}", Ct);
        var userName = $"tk.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(userName, "BHA", "THUKHO", ["KP1"], [f.CategoryCode]);
        var keeper = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, "BHA");
        var keeperView = await keeper.GetFromJsonAsync<StockOnHandResult>("/api/stock-on-hand", Ct);

        Assert.All(all!.Items, r => Assert.False(r.BelowMin));
        Assert.False(Assert.Single(onlyKp1!.Items).BelowMin);
        Assert.False(Assert.Single(keeperView!.Items).BelowMin);
    }

    [Fact]
    public async Task TonKho_DongTonBangKhong_ChiHienKhiChon()
    {
        var f = await ArrangeAsync();
        var back = await ReadAsync<InventoryDocumentDto>(await f.Bha.PostAsJsonAsync("/api/stock-transfers",
            new SaveTransferRequest(Today, f.Kp1, f.Kt, null, null, null, [new(null, f.Item.Id, f.Item.BaseUomId, 24, null, null)]), Ct));
        await ReadAsync<InventoryDocumentDto>(await f.Bha.PostAsync($"/api/inventory-documents/{back.Id}/post", null, Ct));

        var hidden = await f.Bha.GetFromJsonAsync<StockOnHandResult>($"/api/stock-on-hand?categoryId={f.CategoryId}", Ct);
        var shown = await f.Bha.GetFromJsonAsync<StockOnHandResult>($"/api/stock-on-hand?categoryId={f.CategoryId}&includeZero=true", Ct);

        Assert.Equal("KT", Assert.Single(hidden!.Items).WarehouseCode);
        Assert.Equal(2, shown!.Items.Count);
    }

    [Fact]
    public async Task TheKho_MotKho_TonDau_PhatSinh_TonLuyKe()
    {
        var f = await ArrangeAsync();

        var card = await f.Bha.GetFromJsonAsync<StockCardDto>(CardUrl(f, f.Kt, Today.AddDays(-7), Today), Ct);

        Assert.Equal((0m, f.Item.Code, "HOP"), (card!.OpeningQty, card.ItemCode, card.UomCode));
        Assert.Equal([("GRN", 48m, 0m, 48m), ("TRF", 0m, 24m, 24m)], card.Entries.Select(e => (e.DocType, e.InQty, e.OutQty, e.BalanceQty)));
        Assert.Equal("KT → KP1", card.Entries[1].Description);
        Assert.Contains("NCC xem tồn", card.Entries[0].Description);
        Assert.False(card.Entries[1].IsCostFinal);
        Assert.Equal((48m, 24m, 24m, 480_000m), (card.InQty, card.OutQty, card.ClosingQty, card.ClosingAmount));

        // Ngày sau: không phát sinh, tồn đầu = tồn cuối hôm nay.
        var tomorrow = await f.Bha.GetFromJsonAsync<StockCardDto>(CardUrl(f, f.Kt, Today.AddDays(1), Today.AddDays(1)), Ct);
        Assert.Equal((24m, 480_000m, 24m), (tomorrow!.OpeningQty, tomorrow.OpeningAmount, tomorrow.ClosingQty));
        Assert.Empty(tomorrow.Entries);
    }

    [Fact]
    public async Task TheKho_MoiKho_ChuyenKhoKhongDoiTonChung()
    {
        var f = await ArrangeAsync();

        var card = await f.Bha.GetFromJsonAsync<StockCardDto>(CardUrl(f, null, Today, Today), Ct);

        Assert.Equal(3, card!.Entries.Count);
        Assert.Equal((48m, 48m, 960_000m), (card.Entries[^1].BalanceQty, card.ClosingQty, card.ClosingAmount));
    }

    [Fact]
    public async Task TheKho_KhoangNgaySai_BaoLoi()
    {
        var f = await ArrangeAsync();

        var reversed = await f.Bha.GetAsync(CardUrl(f, null, Today, Today.AddDays(-1)), Ct);
        var tooLong = await f.Bha.GetAsync(CardUrl(f, null, Today.AddYears(-3), Today), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, reversed.StatusCode);
        Assert.Equal("Thẻ kho xem tối đa hai năm mỗi lần.", (await tooLong.Content.ReadFromJsonAsync<ProblemDetails>(Ct))!.Title);
    }

    [Fact]
    public async Task ThuKhoChiKp1_ChiThayTonKp1_TheKhoKtKhongCo()
    {
        var f = await ArrangeAsync();
        var userName = $"tk.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(userName, "BHA", "THUKHO", ["KP1"], [f.CategoryCode]);
        var keeper = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, "BHA");

        var stock = await keeper.GetFromJsonAsync<StockOnHandResult>("/api/stock-on-hand", Ct);
        var card = await keeper.GetFromJsonAsync<StockCardDto>(CardUrl(f, null, Today, Today), Ct);

        Assert.Equal("KP1", Assert.Single(stock!.Items).WarehouseCode);
        // Chỉ thấy dòng nhập vào KP1 của phiếu chuyển; diễn giải vẫn ghi kho đi (mã kho ngoài phạm vi hiện "?").
        Assert.Equal(("TRF", 24m, 24m), (Assert.Single(card!.Entries).DocType, card.Entries[0].InQty, card.ClosingQty));
        Assert.Equal(HttpStatusCode.NotFound, (await keeper.GetAsync(CardUrl(f, f.Kt, Today, Today), Ct)).StatusCode);
    }

    [Fact]
    public async Task KhongCoQuyenXemTon_BiTuChoi()
    {
        var userName = $"nv.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(userName, "BHA", "NVMUA");
        var buyer = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, "BHA");

        Assert.Equal(HttpStatusCode.Forbidden, (await buyer.GetAsync("/api/stock-on-hand", Ct)).StatusCode);
    }
}
