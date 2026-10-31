using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Proc.Application.Common;
using Proc.Application.Inventory;
using Proc.Application.MasterData;
using Proc.Application.Organization;
using Proc.Application.Pricing;
using Proc.Application.Purchasing;
using Proc.Domain.Organization;

namespace Proc.IntegrationTests;

/// <summary>
/// Bảng giá NCC, giá gợi ý cho PO và so sánh giá toàn tập đoàn (design.md mục 8.1, 8.2). Mỗi test dùng công ty, mặt hàng, NCC mới.
/// </summary>
[Collection(ApiCollection.Name)]
public class SupplierPricingTests
{
    private readonly ProcApiFactory _factory;

    public SupplierPricingTests(ProcApiFactory factory) => _factory = factory;

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

    private sealed record Fixture(string UserName, HttpClient Head, Guid ItemId, Guid Cai, Guid Thung, Guid SupplierId);

    private sealed record Member(HttpClient Client, string Code, Guid Kt);

    /// <summary>Người dùng quản trị ở tổng công ty; một mặt hàng (cái, thùng = 12 cái) và một NCC đã duyệt dùng chung.</summary>
    private async Task<Fixture> ArrangeAsync()
    {
        var userName = $"bg.{Guid.NewGuid():N}"[..16];
        await _factory.CreateUserAsync(userName, ProcApiFactory.TestPassword, ["TCT"]);
        var head = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, "TCT");
        var uoms = (await head.GetFromJsonAsync<List<UomDto>>("/api/uoms", Ct))!;
        var cai = uoms.Single(u => u.Code == "CAI").Id;
        var thung = uoms.Single(u => u.Code == "THUNG").Id;
        var category = await ReadAsync<ItemCategoryDetailDto>(await head.PostAsJsonAsync("/api/item-categories",
            new CreateItemCategoryRequest(UniqueCode("G"), "Nhóm so sánh giá", null, null), Ct));
        var item = await ReadAsync<ItemDetailDto>(await head.PostAsJsonAsync("/api/items",
            new CreateItemRequest($"{category.Code}-01", "Vòng bi 6205", null, "Inventory", category.Id, cai, null, null, null, null, "10", null, false, false,
                null, null, [new(thung, 12)]), Ct));
        var groupId = (await head.GetFromJsonAsync<List<SupplierGroupDto>>("/api/supplier-groups", Ct))!.Single(g => g.Code == "TN").Id;
        var supplier = await ReadAsync<SupplierDetailDto>(await head.PostAsJsonAsync("/api/suppliers",
            new SaveSupplierRequest(UniqueCode("N"), groupId, "NCC vòng bi", null, false, null, null, null, null, null, null, null, null, "Approved", null, null, [], []), Ct));
        return new Fixture(userName, head, item.Id, cai, thung, supplier.Id);
    }

    /// <summary>Công ty thành viên mới (kỳ tháng hiện tại mở), NCC của fixture áp dụng tại công ty.</summary>
    private async Task<Member> MemberAsync(Fixture f)
    {
        var company = await ReadAsync<CompanyDetailDto>(await f.Head.PostAsJsonAsync("/api/companies",
            new CreateCompanyRequest(UniqueCode("C"), "Công ty thử so sánh giá", null, null, null, null, new DateOnly(Today.Year, Today.Month, 1), "KT", "Kho tổng"), Ct));
        var client = await _factory.CreateClientInCompanyAsync(f.UserName, ProcApiFactory.TestPassword, company.Code);
        var calendar = await ReadAsync<InventoryCalendarDto>(await client.PostAsJsonAsync("/api/inventory-periods/years", new CreateInventoryYearRequest(Today.Year), Ct));
        await ReadAsync<InventoryPeriodDto>(await client.PostAsync($"/api/inventory-periods/{calendar.Periods.Single(p => p.Month == Today.Month).Id}/open", null, Ct));
        var kt = (await client.GetFromJsonAsync<List<WarehouseDetailDto>>("/api/warehouses", Ct))!.Single().Id;
        await ReadAsync<SupplierCompanyDto>(await client.PutAsJsonAsync($"/api/suppliers/{f.SupplierId}/company", new SaveSupplierCompanyRequest(null, null, true), Ct));
        return new Member(client, company.Code, kt);
    }

    private static SavePriceListLineRequest L(Guid itemId, Guid uomId, decimal minQty, decimal price, decimal discountPct, string? vat) =>
        new(null, itemId, uomId, minQty, price, discountPct, vat, null);

    private static SavePriceListRequest PriceList(Fixture f, bool groupWide, params SavePriceListLineRequest[] lines) =>
        new(f.SupplierId, "Bảng giá vòng bi", groupWide, Today.AddDays(-1), null, "Giao tại kho", null, lines);

    /// <summary>Nhập mua đã ghi sổ: qty cái × giá.</summary>
    private static async Task ReceiveAsync(Fixture f, Member m, decimal qty, decimal price)
    {
        var grn = await ReadAsync<InventoryDocumentDto>(await m.Client.PostAsJsonAsync("/api/goods-receipts",
            new SaveReceiptRequest(Today, m.Kt, f.SupplierId, null, null, null, null, null, null, [new(null, f.ItemId, f.Cai, qty, price, "10", null, null, null)]), Ct));
        await ReadAsync<InventoryDocumentDto>(await m.Client.PostAsync($"/api/inventory-documents/{grn.Id}/post", null, Ct));
    }

    [Fact]
    public async Task BangGia_GoiYGiaPo_BangRiengCongTyUuTienHonBangToanTapDoan()
    {
        var f = await ArrangeAsync();
        var m = await MemberAsync(f);

        // Tổng công ty: bảng toàn tập đoàn, 2 bậc theo cái và 1 dòng theo thùng.
        var group = await ReadAsync<PriceListDto>(await f.Head.PostAsJsonAsync("/api/price-lists", PriceList(f, true,
            L(f.ItemId, f.Cai, 0, 50_000, 0, null),
            L(f.ItemId, f.Cai, 100, 48_000, 0, null),
            L(f.ItemId, f.Thung, 0, 600_000, 5, "8")), Ct));
        Assert.Matches($"^PRL-TCT-{Today:yyMM}-\\d{{4}}$", group.No);
        Assert.Equal(47_500m, group.Lines[2].BaseNetPrice);
        Assert.Contains("ít nhất một dòng", await TitleAsync(await f.Head.PostAsJsonAsync("/api/price-lists", PriceList(f, true), Ct)));
        Assert.Contains("cùng bậc", await TitleAsync(await f.Head.PostAsJsonAsync("/api/price-lists", PriceList(f, true,
            L(f.ItemId, f.Cai, 0, 1, 0, null), L(f.ItemId, f.Cai, 0, 2, 0, null)), Ct)));

        var resolveUrl = $"/api/price-lists/resolve?supplierId={f.SupplierId}&date={Today:yyyy-MM-dd}&itemIds={f.ItemId}";
        Assert.Empty((await m.Client.GetFromJsonAsync<List<ResolvedSupplierPriceDto>>(resolveUrl, Ct))!);
        group = await ReadAsync<PriceListDto>(await f.Head.PostAsync($"/api/price-lists/{group.Id}/activate", null, Ct));
        Assert.Equal("Active", group.Status);
        Assert.Contains("không còn ở trạng thái nháp", await TitleAsync(await f.Head.PutAsJsonAsync($"/api/price-lists/{group.Id}",
            PriceList(f, true, L(f.ItemId, f.Cai, 0, 1, 0, null)), Ct)));

        // Công ty thành viên thấy bảng toàn tập đoàn (chỉ xem), giá gợi ý lấy từ đó; thuế suất trống thì theo mặt hàng (10).
        var resolved = (await m.Client.GetFromJsonAsync<List<ResolvedSupplierPriceDto>>(resolveUrl, Ct))!;
        Assert.Equal([(0m, 50_000m, "10"), (100m, 48_000m, "10"), (0m, 600_000m, "8")], resolved.Select(r => (r.MinQty, r.UnitPrice, r.VatCode)));
        var seen = await ReadAsync<PriceListDto>(await m.Client.GetAsync($"/api/price-lists/{group.Id}", Ct));
        Assert.False(seen.Actions.CanEdit);
        Assert.Equal(HttpStatusCode.Forbidden, (await m.Client.PostAsync($"/api/price-lists/{group.Id}/deactivate", null, Ct)).StatusCode);
        Assert.Contains("Chỉ tổng công ty", await TitleAsync(await m.Client.PostAsJsonAsync("/api/price-lists",
            PriceList(f, true, L(f.ItemId, f.Cai, 0, 1, 0, null)), Ct)));

        // Bảng riêng của công ty (thỏa thuận riêng) thắng bảng toàn tập đoàn khi đã hiệu lực; ngừng thì quay về bảng chung.
        var own = await ReadAsync<PriceListDto>(await m.Client.PostAsJsonAsync("/api/price-lists",
            PriceList(f, false, L(f.ItemId, f.Cai, 0, 45_000, 0, null)), Ct));
        Assert.Equal(3, (await m.Client.GetFromJsonAsync<List<ResolvedSupplierPriceDto>>(resolveUrl, Ct))!.Count);
        await ReadAsync<PriceListDto>(await m.Client.PostAsync($"/api/price-lists/{own.Id}/activate", null, Ct));
        Assert.Equal([45_000m], (await m.Client.GetFromJsonAsync<List<ResolvedSupplierPriceDto>>(resolveUrl, Ct))!.Select(r => r.UnitPrice));
        var list = (await m.Client.GetFromJsonAsync<List<PriceListListItemDto>>("/api/price-lists", Ct))!;
        Assert.Contains(list, l => l.Id == group.Id && !l.IsOwn && l.IsGroupWide);
        Assert.Contains(list, l => l.Id == own.Id && l.IsOwn);
        await ReadAsync<PriceListDto>(await m.Client.PostAsync($"/api/price-lists/{own.Id}/deactivate", null, Ct));
        Assert.Equal(3, (await m.Client.GetFromJsonAsync<List<ResolvedSupplierPriceDto>>(resolveUrl, Ct))!.Count);

        // Tổng công ty không thấy bảng riêng của công ty thành viên.
        Assert.Equal(HttpStatusCode.NotFound, (await f.Head.GetAsync($"/api/price-lists/{own.Id}", Ct)).StatusCode);
    }

    [Fact]
    public async Task SoSanhGia_ToanTapDoan_ToMauCongTyMuaCaoHonNguong()
    {
        var f = await ArrangeAsync();
        var a = await MemberAsync(f);
        var b = await MemberAsync(f);
        await ReceiveAsync(f, a, 10, 100_000);
        await ReceiveAsync(f, b, 10, 120_000);
        await ReceiveAsync(f, b, 30, 110_000);
        var group = await ReadAsync<PriceListDto>(await f.Head.PostAsJsonAsync("/api/price-lists",
            PriceList(f, true, L(f.ItemId, f.Thung, 0, 1_140_000, 0, null)), Ct));
        await ReadAsync<PriceListDto>(await f.Head.PostAsync($"/api/price-lists/{group.Id}/activate", null, Ct));

        // B: giá gần nhất cùng ngày, phiếu sau (110.000) – cao hơn A 10% đúng ngưỡng nên chưa tô; ngưỡng 5% thì tô.
        var all = await ReadAsync<PriceComparisonDto>(await a.Client.GetAsync($"/api/price-comparison?itemId={f.ItemId}&allCompanies=true&thresholdPct=5", Ct));
        Assert.True(all.AllCompanies);
        Assert.Equal((100_000m, 120_000m, 110_000m), (all.GroupMinPrice!.Value, all.GroupMaxPrice!.Value, all.GroupAvgPrice!.Value));
        var rows = all.Purchases.ToDictionary(p => p.CompanyCode);
        Assert.Equal((100_000m, 0m, false), (rows[a.Code].LastPrice, rows[a.Code].AboveGroupMinPct, rows[a.Code].IsAboveThreshold));
        Assert.Equal((10m, true, 40m), (rows[b.Code].AboveGroupMinPct, rows[b.Code].IsAboveThreshold, rows[b.Code].Qty12Months));
        Assert.Equal((110_000m, 120_000m), (rows[b.Code].MinPrice!.Value, rows[b.Code].MaxPrice!.Value));
        Assert.Equal(95_000m, all.PriceLists.Single().BaseNetPrice);
        Assert.Equal(4, all.History.Count);

        // Giá tham khảo PR chỉ lấy giá nhập của chính công ty: công ty C chưa nhập lần nào → 0.
        var c = await MemberAsync(f);
        var pr = await ReadAsync<PurchaseRequestDto>(await c.Client.PostAsJsonAsync("/api/purchase-requests",
            new SavePurchaseRequestRequest(Today, null, null, null, c.Kt, "Normal", null, [new(null, f.ItemId, f.Cai, 1, null, null, null)]), Ct));
        Assert.Equal(0m, pr.Lines[0].EstPrice);

        // Chỉ công ty mình: không thấy B.
        var mine = await ReadAsync<PriceComparisonDto>(await a.Client.GetAsync($"/api/price-comparison?itemId={f.ItemId}", Ct));
        Assert.Equal([a.Code], mine.Purchases.Select(p => p.CompanyCode));

        // Có quyền so sánh giá nhưng không có quyền xem toàn tập đoàn.
        var roleCode = UniqueCode("S");
        await _factory.CreateRoleAsync(roleCode, Permissions.Pricing.CompareView);
        var userName = $"ss.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(userName, a.Code, roleCode);
        var viewer = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, a.Code);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.GetAsync($"/api/price-comparison?itemId={f.ItemId}&allCompanies=true", Ct)).StatusCode);
        Assert.Single((await ReadAsync<PriceComparisonDto>(await viewer.GetAsync($"/api/price-comparison?itemId={f.ItemId}", Ct))).Purchases);
    }
}
