using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Proc.Application.Common;
using Proc.Application.Inventory;
using Proc.Application.MasterData;
using Proc.Application.Organization;
using Proc.Application.Pricing;
using Proc.Application.Purchasing;
using Proc.Application.Sourcing;
using Proc.Domain.Organization;

namespace Proc.IntegrationTests;

/// <summary>
/// Tìm nguồn → RFQ → báo giá → so sánh → chọn thầu → PO nháp (design.md mục 8.3, 8.4). Mỗi test một công ty mới.
/// </summary>
[Collection(ApiCollection.Name)]
public class SourcingTests
{
    private readonly ProcApiFactory _factory;

    public SourcingTests(ProcApiFactory factory) => _factory = factory;

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

    private sealed record Fixture(HttpClient Client, string CompanyCode, Guid ItemId, Guid Cai, Guid Thung, Guid[] Suppliers);

    /// <summary>Công ty mới, mặt hàng (cái, thùng = 12 cái), 4 NCC đã duyệt áp dụng tại công ty. Người dùng: quản trị.</summary>
    private async Task<Fixture> ArrangeAsync()
    {
        var userName = $"dt.{Guid.NewGuid():N}"[..16];
        await _factory.CreateUserAsync(userName, ProcApiFactory.TestPassword, ["TCT"]);
        var head = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, "TCT");
        var company = await ReadAsync<CompanyDetailDto>(await head.PostAsJsonAsync("/api/companies",
            new CreateCompanyRequest(UniqueCode("D"), "Công ty thử đấu thầu", null, null, null, null, new DateOnly(Today.Year, Today.Month, 1), "KT", "Kho tổng"), Ct));
        var client = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, company.Code);
        var uoms = (await head.GetFromJsonAsync<List<UomDto>>("/api/uoms", Ct))!;
        var (cai, thung) = (uoms.Single(u => u.Code == "CAI").Id, uoms.Single(u => u.Code == "THUNG").Id);
        var category = await ReadAsync<ItemCategoryDetailDto>(await head.PostAsJsonAsync("/api/item-categories",
            new CreateItemCategoryRequest(UniqueCode("T"), "Nhóm đấu thầu", null, null), Ct));
        var item = await ReadAsync<ItemDetailDto>(await head.PostAsJsonAsync("/api/items",
            new CreateItemRequest($"{category.Code}-01", "Vòng bi 6305", null, "Inventory", category.Id, cai, null, null, null, null, "10", null, false, false,
                null, null, [new(thung, 12)]), Ct));
        var groupId = (await head.GetFromJsonAsync<List<SupplierGroupDto>>("/api/supplier-groups", Ct))!.Single(g => g.Code == "TN").Id;
        var suppliers = new List<Guid>();
        foreach (var name in new[] { "A", "B", "C", "D" })
        {
            var supplier = await ReadAsync<SupplierDetailDto>(await head.PostAsJsonAsync("/api/suppliers",
                new SaveSupplierRequest(UniqueCode("N"), groupId, $"NCC thầu {name}", null, false, null, null, null, null, null, null, null, null, "Approved", null, null, [], []), Ct));
            await ReadAsync<SupplierCompanyDto>(await client.PutAsJsonAsync($"/api/suppliers/{supplier.Id}/company", new SaveSupplierCompanyRequest(null, null, true), Ct));
            suppliers.Add(supplier.Id);
        }

        return new Fixture(client, company.Code, item.Id, cai, thung, [.. suppliers]);
    }

    private static SaveRfqRequest Rfq(Fixture f, Guid? sourcingId, params Guid[] suppliers) =>
        new(Today, "Vòng bi quý 4", Today.AddDays(7), sourcingId, new RfqWeightsDto(60, 20, 10, 5, 5), null,
            [new(null, f.ItemId, f.Cai, 100, null, null), new(null, f.ItemId, f.Thung, 10, null, null)], suppliers);

    private static SaveQuotationLineRequest Q(Guid lineId, decimal price, decimal discount = 0, string? vat = null, int? leadTime = null) =>
        new(lineId, price, discount, vat, leadTime, null);

    private static SaveQuotationRequest Quote(params List<SaveQuotationLineRequest> lines) =>
        new(Today, Today.AddDays(30), "Thanh toán 30 ngày", 8, 10, 10, 6, null, lines);

    [Fact]
    public async Task TimNguon_Rfq_BaoGia_ChonThau_SinhPo()
    {
        var f = await ArrangeAsync();
        var (a, b, c) = (f.Suppliers[0], f.Suppliers[1], f.Suppliers[2]);
        var sourcing = await ReadAsync<SourcingDto>(await f.Client.PostAsJsonAsync("/api/sourcing",
            new SaveSourcingRequest(Today, f.ItemId, null, "Chịu nhiệt 150°C", 220, Today.AddDays(10), "Anh Nam", null, [new(null, a, "Contacted", "Đã gửi hồ sơ")]), Ct));
        Assert.Matches($"^SRC-{f.CompanyCode}-{Today:yyMM}-\\d{{4}}$", sourcing.No);

        // Mời 2 NCC: lưu nháp được, gửi bị chặn (tối thiểu 3).
        var rfq = await ReadAsync<RfqDto>(await f.Client.PostAsJsonAsync("/api/rfqs", Rfq(f, sourcing.Id, a, b), Ct));
        Assert.Contains("ít nhất 3", await TitleAsync(await f.Client.PostAsync($"/api/rfqs/{rfq.Id}/send", null, Ct)));
        rfq = await ReadAsync<RfqDto>(await f.Client.PutAsJsonAsync($"/api/rfqs/{rfq.Id}",
            Rfq(f, sourcing.Id, a, b, c) with { Lines = rfq.Lines.Select(l => new SaveRfqLineRequest(l.Id, l.ItemId, l.UomId, l.Qty, null, null)).ToList() }, Ct));
        rfq = await ReadAsync<RfqDto>(await f.Client.PostAsync($"/api/rfqs/{rfq.Id}/send", null, Ct));
        Assert.Equal("Sent", rfq.Status);
        sourcing = await ReadAsync<SourcingDto>(await f.Client.GetAsync($"/api/sourcing/{sourcing.Id}", Ct));
        Assert.All(sourcing.Candidates, x => Assert.Equal("RfqSent", x.Status));
        Assert.Equal(3, sourcing.Candidates.Count);

        // Báo giá: dòng cái B rẻ nhất (48.000); dòng thùng A rẻ nhất (600.000 − 5% = 47.500/cái) so với C (590.000 = 49.166,67/cái).
        var (cai, thung) = (rfq.Lines[0].Id, rfq.Lines[1].Id);
        await ReadAsync<RfqDto>(await f.Client.PutAsJsonAsync($"/api/rfqs/{rfq.Id}/quotations/{a}", Quote(Q(cai, 50_000, 0, null, 7), Q(thung, 600_000, 5, "8", 7)), Ct));
        await ReadAsync<RfqDto>(await f.Client.PutAsJsonAsync($"/api/rfqs/{rfq.Id}/quotations/{b}", Quote(Q(cai, 48_000, 0, null, 14)), Ct));
        rfq = await ReadAsync<RfqDto>(await f.Client.PutAsJsonAsync($"/api/rfqs/{rfq.Id}/quotations/{c}", Quote(Q(cai, 52_000, 0, null, 10), Q(thung, 590_000, 0, null, 10)), Ct));
        var quotes = rfq.Suppliers.ToDictionary(s => s.SupplierId);
        Assert.True(quotes[b].Lines.Single().IsLowest);
        Assert.True(quotes[a].Lines.Single(l => l.RfqLineId == thung).IsLowest);
        Assert.Equal(47_500m, quotes[a].Lines.Single(l => l.RfqLineId == thung).BaseNetPrice);
        // Điểm giá A: dòng cái 48/50 × 10 = 9,6; dòng thùng 10 → 9,8. Tổng = (60×9,8 + 20×8 + 10×10 + 5×10 + 5×6)/100 = 9,28.
        Assert.Equal((9.8m, 9.28m), (quotes[a].PriceScore, quotes[a].TotalScore));
        Assert.Contains(sourcing.Candidates, x => x.SupplierId == a);
        Assert.Equal("Quoted", (await ReadAsync<SourcingDto>(await f.Client.GetAsync($"/api/sourcing/{sourcing.Id}", Ct))).Candidates.Single(x => x.SupplierId == a).Status);

        // Chọn A cho cả hai dòng: dòng cái không phải giá thấp nhất → phải có lý do.
        Assert.Contains("phải ghi lý do", await TitleAsync(await f.Client.PostAsJsonAsync($"/api/rfqs/{rfq.Id}/award",
            new AwardRfqRequest([new(cai, a, null), new(thung, a, null)]), Ct)));
        rfq = await ReadAsync<RfqDto>(await f.Client.PostAsJsonAsync($"/api/rfqs/{rfq.Id}/award",
            new AwardRfqRequest([new(cai, a, "Gộp một NCC để giao một lần"), new(thung, a, null)]), Ct));
        Assert.Equal(("Awarded", true), (rfq.Status, rfq.Actions.CanCreateOrders));
        Assert.Equal(5_000_000m + 5_700_000m, rfq.Suppliers.Single(s => s.SupplierId == a).AwardedAmount);

        // Sinh PO nháp cho A: giá theo báo giá, thuế suất trống thì theo mặt hàng; bấm lại thì báo đã có.
        var created = await ReadAsync<List<CreatedOrderDto>>(await f.Client.PostAsync($"/api/rfqs/{rfq.Id}/create-orders", null, Ct));
        var po = await ReadAsync<PurchaseOrderDto>(await f.Client.GetAsync($"/api/purchase-orders/{created.Single().PurchaseOrderId}", Ct));
        Assert.Equal(("Draft", a, 2), (po.Status, po.SupplierId, po.Lines.Count));
        Assert.Equal([(50_000m, 0m, "10"), (600_000m, 5m, "8")], po.Lines.Select(l => (l.UnitPrice, l.DiscountPct, l.VatCode)));
        Assert.Equal(10_700_000m, po.TotalAmount);
        Assert.Contains("đã có đơn mua hàng", await TitleAsync(await f.Client.PostAsync($"/api/rfqs/{rfq.Id}/create-orders", null, Ct)));
        Assert.Equal(po.No, (await ReadAsync<RfqDto>(await f.Client.GetAsync($"/api/rfqs/{rfq.Id}", Ct))).Suppliers.Single(s => s.SupplierId == a).PurchaseOrderNo);

        // Giá trúng thầu vào lịch sử giá.
        var compare = await ReadAsync<PriceComparisonDto>(await f.Client.GetAsync($"/api/price-comparison?itemId={f.ItemId}", Ct));
        Assert.Equal(2, compare.History.Count(h => h.Source == "Quotation" && h.DocumentNo == rfq.No));
    }

    [Fact]
    public async Task ChonThau_CanQuyenRiengVaChiKhiDangNhanBaoGia()
    {
        var f = await ArrangeAsync();
        var rfq = await ReadAsync<RfqDto>(await f.Client.PostAsJsonAsync("/api/rfqs", Rfq(f, null, f.Suppliers[0], f.Suppliers[1], f.Suppliers[2]), Ct));
        Assert.Contains("đang nhận báo giá", await TitleAsync(await f.Client.PutAsJsonAsync(
            $"/api/rfqs/{rfq.Id}/quotations/{f.Suppliers[0]}", Quote(Q(rfq.Lines[0].Id, 1, 0, null, null)), Ct)));
        await ReadAsync<RfqDto>(await f.Client.PostAsync($"/api/rfqs/{rfq.Id}/send", null, Ct));
        Assert.Contains("không có trong danh sách được mời", await TitleAsync(await f.Client.PutAsJsonAsync(
            $"/api/rfqs/{rfq.Id}/quotations/{f.Suppliers[3]}", Quote(Q(rfq.Lines[0].Id, 1, 0, null, null)), Ct)));

        var roleCode = UniqueCode("R");
        await _factory.CreateRoleAsync(roleCode, Permissions.Sourcing.RfqView, Permissions.Sourcing.RfqManage);
        var userName = $"rq.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(userName, f.CompanyCode, roleCode);
        var staff = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, f.CompanyCode);
        var seen = await ReadAsync<RfqDto>(await staff.GetAsync($"/api/rfqs/{rfq.Id}", Ct));
        Assert.False(seen.Actions.CanAward);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.PostAsJsonAsync($"/api/rfqs/{rfq.Id}/award", new AwardRfqRequest([]), Ct)).StatusCode);
    }
}
