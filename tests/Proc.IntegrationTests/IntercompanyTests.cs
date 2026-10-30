using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Proc.Application.Common;
using Proc.Application.Inventory;
using Proc.Application.MasterData;
using Proc.Application.Organization;
using Proc.Application.Pricing;

namespace Proc.IntegrationTests;

/// <summary>
/// Mua bán nội bộ BHA → NMI (design.md mục 4, 6.3, 10.7, 10.8): bảng giá nội bộ, phiếu xuất bán từ kho tổng, phiếu nhập chờ nhận
/// sinh ở công ty mua, xác nhận nhận thiếu, chặn bỏ ghi sổ khi bên mua đã nhận, hàng đi đường, đối chiếu.
/// </summary>
[Collection(ApiCollection.Name)]
public class IntercompanyTests
{
    private readonly ProcApiFactory _factory;

    public IntercompanyTests(ProcApiFactory factory) => _factory = factory;

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

    /// <summary>Mặt hàng mới (cái); BHA nhập 50 cái × 10.000 vào KT; giá bán nội bộ BHA → NMI 15.000, VAT 10%.</summary>
    private sealed record Fixture(HttpClient Bha, HttpClient Nmi, Guid ItemId, Guid Cai, Guid BhaKt, Guid BhaKp1, Guid NmiId, Guid NmiKt, Guid BhaId);

    private async Task<Fixture> ArrangeAsync()
    {
        var userName = $"nb.{Guid.NewGuid():N}"[..16];
        await _factory.CreateUserAsync(userName, ProcApiFactory.TestPassword, ["TCT", "BHA", "NMI"]);
        var head = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, "TCT");
        var bha = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, "BHA");
        var nmi = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, "NMI");
        var cai = (await head.GetFromJsonAsync<List<UomDto>>("/api/uoms", Ct))!.Single(u => u.Code == "CAI").Id;
        var category = await ReadAsync<ItemCategoryDetailDto>(await head.PostAsJsonAsync("/api/item-categories",
            new CreateItemCategoryRequest(UniqueCode("I"), "Nhóm nội bộ", null, null), Ct));
        var item = await ReadAsync<ItemDetailDto>(await head.PostAsJsonAsync("/api/items",
            new CreateItemRequest($"{category.Code}-01", "Vòng bi nội bộ", null, "Inventory", category.Id, cai, null, null, null,
                null, "10", null, false, false, null, null, []), Ct));
        var groupId = (await head.GetFromJsonAsync<List<SupplierGroupDto>>("/api/supplier-groups", Ct))!.Single(g => g.Code == "TN").Id;
        var supplier = await ReadAsync<SupplierDetailDto>(await head.PostAsJsonAsync("/api/suppliers",
            new SaveSupplierRequest(UniqueCode("N"), groupId, "NCC nội bộ", null, false, null, null, null, null, null, null, null, null,
                "Approved", null, null, [], []), Ct));
        await ReadAsync<SupplierCompanyDto>(await bha.PutAsJsonAsync($"/api/suppliers/{supplier.Id}/company", new SaveSupplierCompanyRequest(null, null, true), Ct));
        var bhaWarehouses = (await bha.GetFromJsonAsync<List<WarehouseDetailDto>>("/api/warehouses", Ct))!;
        var nmiKt = (await nmi.GetFromJsonAsync<List<WarehouseDetailDto>>("/api/warehouses", Ct))!.Single(w => w.Code == "KT").Id;
        var bhaKt = bhaWarehouses.Single(w => w.Code == "KT").Id;
        var receipt = await ReadAsync<InventoryDocumentDto>(await bha.PostAsJsonAsync("/api/goods-receipts",
            new SaveReceiptRequest(Today, bhaKt, supplier.Id, null, null, null, null, null, null, [new(null, item.Id, cai, 50, 10_000, "10", null, null, null)]), Ct));
        await ReadAsync<InventoryDocumentDto>(await bha.PostAsync($"/api/inventory-documents/{receipt.Id}/post", null, Ct));
        var nmiId = await _factory.GetCompanyIdAsync("NMI");
        await ReadAsync<InternalPriceDto>(await bha.PostAsJsonAsync("/api/internal-prices",
            new SaveInternalPriceRequest(nmiId, item.Id, 15_000, "10", Today.AddDays(-30), null, null), Ct));
        return new Fixture(bha, nmi, item.Id, cai, bhaKt, bhaWarehouses.Single(w => w.Code == "KP1").Id, nmiId, nmiKt, await _factory.GetCompanyIdAsync("BHA"));
    }

    private static Task<HttpResponseMessage> CreateSaleAsync(Fixture f, decimal qty, Guid? from = null, Guid? buyer = null) =>
        f.Bha.PostAsJsonAsync("/api/intercompany-sales",
            new SaveSaleRequest(Today, from ?? f.BhaKt, buyer ?? f.NmiId, "HD-NB-01", Today, "Xe 29C-123.45", null, [new(null, f.ItemId, f.Cai, qty, null, null)]), Ct);

    private static Task<HttpResponseMessage> PostAsync(HttpClient client, Guid id) => client.PostAsync($"/api/inventory-documents/{id}/post", null, Ct);

    private static Task<HttpResponseMessage> UnpostAsync(HttpClient client, Guid id) => client.PostAsync($"/api/inventory-documents/{id}/unpost", null, Ct);

    private static async Task<decimal> StockAsync(HttpClient client, Guid warehouseId, Guid itemId) =>
        (await client.GetFromJsonAsync<StockOnHandResult>($"/api/stock-on-hand?warehouseId={warehouseId}&includeZero=true", Ct))!
        .Items.Where(r => r.ItemId == itemId).Sum(r => r.Qty);

    [Fact]
    public async Task BanNoiBo_GiaTheoBangGia_SinhPhieuChoNhan_NhanThieu_ChanBoGhiSo()
    {
        var f = await ArrangeAsync();

        var sale = await ReadAsync<InventoryDocumentDto>(await CreateSaleAsync(f, 10));
        Assert.Equal((15_000m, "10", 150_000m, 15_000m, "NMI"), (sale.Lines[0].UnitPrice, sale.Lines[0].VatCode, sale.TotalAmount, sale.TotalVat, sale.CounterCompanyCode));
        sale = await ReadAsync<InventoryDocumentDto>(await PostAsync(f.Bha, sale.Id));
        Assert.Equal(40m, await StockAsync(f.Bha, f.BhaKt, f.ItemId));
        Assert.Matches($"^ICR-NMI-{Today:yyMM}-\\d{{4}}$", sale.LinkedDocumentNo);
        Assert.Equal("Pending", sale.ReceiveStatus);

        // Công ty mua thấy phiếu chờ nhận ở kho tổng của mình, SL gửi 10, giá bán nội bộ.
        var receipt = await ReadAsync<InventoryDocumentDto>(await f.Nmi.GetAsync($"/api/inventory-documents/{sale.LinkedDocumentId}", Ct));
        Assert.Equal(("ICR", "Draft", f.NmiKt, "BHA", sale.No, 10m, 15_000m), (receipt.DocType, receipt.Status, receipt.ToWarehouseId, receipt.CounterCompanyCode, receipt.LinkedDocumentNo, receipt.Lines[0].SentQty!.Value, receipt.Lines[0].UnitPrice));
        // Bên nhận không hủy được phiếu do bên bán sinh ra.
        Assert.Contains("không hủy được", await TitleAsync(await f.Nmi.PostAsync($"/api/inventory-documents/{receipt.Id}/cancel", null, Ct)));

        Assert.Contains("SL thực nhận phải từ 0 đến SL gửi 10", await TitleAsync(await f.Nmi.PutAsJsonAsync($"/api/intercompany-receipts/{receipt.Id}",
            new ConfirmIntercompanyReceiptRequest(Today, null, [new(receipt.Lines[0].Id, 11, null, null)]), Ct)));
        await ReadAsync<InventoryDocumentDto>(await f.Nmi.PutAsJsonAsync($"/api/intercompany-receipts/{receipt.Id}",
            new ConfirmIntercompanyReceiptRequest(Today, "Vỡ 2 cái khi vận chuyển", [new(receipt.Lines[0].Id, 8, null, "Vỡ 2")]), Ct));
        receipt = await ReadAsync<InventoryDocumentDto>(await PostAsync(f.Nmi, receipt.Id));
        Assert.Equal(("Posted", 120_000m, "Short"), (receipt.Status, receipt.TotalAmount, receipt.ReceiveStatus));
        Assert.Equal(8m, await StockAsync(f.Nmi, f.NmiKt, f.ItemId));

        var seen = await ReadAsync<InventoryDocumentDto>(await f.Bha.GetAsync($"/api/inventory-documents/{sale.Id}", Ct));
        Assert.Equal("Short", seen.ReceiveStatus);
        Assert.Contains("đã xác nhận nhận hàng", await TitleAsync(await UnpostAsync(f.Bha, sale.Id)));

        var reconciliation = await f.Bha.GetFromJsonAsync<List<IntercompanyReconciliationDto>>(
            $"/api/reports/intercompany-reconciliation?from={Today.AddDays(-1):yyyy-MM-dd}&to={Today:yyyy-MM-dd}", Ct);
        var withNmi = reconciliation!.Single(r => r.CounterCompanyId == f.NmiId);
        Assert.True(withNmi.SoldAmount - withNmi.ReceivedByCounterAmount >= 30_000m);
    }

    [Fact]
    public async Task BoGhiSoPhieuBan_TruocKhiNhan_HuyPhieuChoNhan_HangDiDuong()
    {
        var f = await ArrangeAsync();
        var sale = await ReadAsync<InventoryDocumentDto>(await CreateSaleAsync(f, 5));
        sale = await ReadAsync<InventoryDocumentDto>(await PostAsync(f.Bha, sale.Id));

        var outgoing = await f.Bha.GetFromJsonAsync<List<GoodsInTransitDto>>("/api/reports/goods-in-transit", Ct);
        Assert.Contains(outgoing!, t => t.SaleId == sale.Id && t.Direction == "Out" && t.BuyerCode == "NMI");
        var incoming = await f.Nmi.GetFromJsonAsync<List<GoodsInTransitDto>>("/api/reports/goods-in-transit", Ct);
        Assert.Contains(incoming!, t => t.SaleId == sale.Id && t.Direction == "In" && t.ReceiptNo == sale.LinkedDocumentNo);

        await ReadAsync<InventoryDocumentDto>(await UnpostAsync(f.Bha, sale.Id));
        var receipt = await ReadAsync<InventoryDocumentDto>(await f.Nmi.GetAsync($"/api/inventory-documents/{sale.LinkedDocumentId}", Ct));
        Assert.Equal("Cancelled", receipt.Status);
        Assert.Equal(50m, await StockAsync(f.Bha, f.BhaKt, f.ItemId));
        Assert.Contains("không ở trạng thái nháp", await TitleAsync(await PostAsync(f.Nmi, receipt.Id)));

        // Ghi sổ lại sinh phiếu chờ nhận mới.
        var again = await ReadAsync<InventoryDocumentDto>(await PostAsync(f.Bha, sale.Id));
        Assert.NotEqual(sale.LinkedDocumentId, again.LinkedDocumentId);
    }

    [Fact]
    public async Task BanNoiBo_ChiKhoTong_PhaiCoGia_KhongBanChoChinhMinh()
    {
        var f = await ArrangeAsync();

        Assert.Contains("chỉ xuất từ kho tổng", await TitleAsync(await CreateSaleAsync(f, 1, from: f.BhaKp1)));
        Assert.Contains("khác công ty bán", await TitleAsync(await CreateSaleAsync(f, 1, buyer: f.BhaId)));
        var dtv = await _factory.GetCompanyIdAsync("DTV");
        Assert.Contains("chưa có giá bán nội bộ", await TitleAsync(await CreateSaleAsync(f, 1, buyer: dtv)));
    }

    [Fact]
    public async Task BangGiaNoiBo_KhongChongHieuLuc_GiaRiengThangGiaChung()
    {
        var f = await ArrangeAsync();

        var overlap = await f.Bha.PostAsJsonAsync("/api/internal-prices",
            new SaveInternalPriceRequest(f.NmiId, f.ItemId, 16_000, null, Today, null, null), Ct);
        Assert.Contains("chồng khoảng hiệu lực", await TitleAsync(overlap));

        await ReadAsync<InternalPriceDto>(await f.Bha.PostAsJsonAsync("/api/internal-prices",
            new SaveInternalPriceRequest(null, f.ItemId, 18_000, "8", Today.AddDays(-30), null, "Giá chung"), Ct));
        var dtv = await _factory.GetCompanyIdAsync("DTV");
        var forNmi = await f.Bha.GetFromJsonAsync<List<ResolvedInternalPriceDto>>($"/api/internal-prices/resolve?buyerCompanyId={f.NmiId}&date={Today:yyyy-MM-dd}&itemIds={f.ItemId}", Ct);
        var forDtv = await f.Bha.GetFromJsonAsync<List<ResolvedInternalPriceDto>>($"/api/internal-prices/resolve?buyerCompanyId={dtv}&date={Today:yyyy-MM-dd}&itemIds={f.ItemId}", Ct);
        Assert.Equal((15_000m, "10"), (Assert.Single(forNmi!).UnitPrice, forNmi![0].VatCode));
        Assert.Equal((18_000m, "8"), (Assert.Single(forDtv!).UnitPrice, forDtv![0].VatCode));

        // Công ty mua không thấy bảng giá của công ty bán.
        Assert.DoesNotContain((await f.Nmi.GetFromJsonAsync<List<InternalPriceDto>>("/api/internal-prices", Ct))!, p => p.ItemId == f.ItemId);
        Assert.Equal(HttpStatusCode.OK, (await f.Bha.GetAsync("/api/group-companies", Ct)).StatusCode);
    }
}
