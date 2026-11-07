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
public partial class SourcingTests
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

    private sealed record Fixture(HttpClient Client, string CompanyCode, Guid ItemId, Guid Cai, Guid Thung, Guid[] Suppliers, HttpClient Head);

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
            new CreateItemCategoryRequest(null, $"Nhóm đấu thầu {Guid.NewGuid():N}", null, null), Ct));
        var item = await ReadAsync<ItemDetailDto>(await head.PostAsJsonAsync("/api/items",
            new CreateItemRequest(null, "Vòng bi 6305", null, "Inventory", category.Id, cai, null, null, null, null, "10", null, false, false,
                null, null, [new(thung, 12)]), Ct));
        var groupId = (await head.GetFromJsonAsync<List<SupplierGroupDto>>("/api/supplier-groups", Ct))!.Single(g => g.Code == "TN").Id;
        var suppliers = new List<Guid>();
        foreach (var name in new[] { "A", "B", "C", "D" })
        {
            var supplier = await ReadAsync<SupplierDetailDto>(await head.PostAsJsonAsync("/api/suppliers",
                new SaveSupplierRequest(null, groupId, $"NCC thầu {name}", null, false, null, null, null, null, null, null, null, null, "Approved", null, null, [], []), Ct));
            await _factory.FinishSupplierApprovalAsync(supplier.Id);
            await ReadAsync<SupplierCompanyDto>(await client.PutAsJsonAsync($"/api/suppliers/{supplier.Id}/company", new SaveSupplierCompanyRequest(null, null, true), Ct));
            suppliers.Add(supplier.Id);
        }

        return new Fixture(client, company.Code, item.Id, cai, thung, [.. suppliers], head);
    }

    private static SaveRfqRequest Rfq(Fixture f, Guid? sourcingId, params Guid[] suppliers) =>
        new(Today, "Vòng bi quý 4", Today.AddDays(7), sourcingId, new RfqWeightsDto(60, 20, 10, 5, 5), null,
            [new(null, f.ItemId, f.Cai, 100, null, null), new(null, f.ItemId, f.Thung, 10, null, null)], suppliers);

    private static SaveQuotationLineRequest Q(Guid lineId, decimal price, decimal discount = 0, string? vat = null, int? leadTime = null) =>
        new(lineId, price, discount, vat, leadTime, null);

    private static SaveQuotationRequest Quote(params List<SaveQuotationLineRequest> lines) =>
        new(Today, Today.AddDays(30), "Thanh toán 30 ngày", 8, 10, 10, 6, null, lines);

    [Fact]
    public async Task ThamSoSoNccToiThieu_TheoCongTy_ApDungKhiGuiRfq()
    {
        var f = await ArrangeAsync();
        var (a, b) = (f.Suppliers[0], f.Suppliers[1]);
        var list = await ReadAsync<List<SystemParameterDto>>(await f.Client.GetAsync("/api/system-parameters", Ct));
        var rfqMin = list.Single(p => p.Code == SystemParameterCodes.RfqMinSuppliers);
        Assert.Equal(("3", true, true), (rfqMin.Value, rfqMin.IsDefault, rfqMin.Editable));
        Assert.False(list.Single(p => p.Code == SystemParameterCodes.CostingMethod).Editable);

        // Giá trị sai, tham số chỉ xem, mã lạ đều bị chặn.
        Assert.Equal(HttpStatusCode.BadRequest, (await f.Client.PutAsJsonAsync("/api/system-parameters/RFQ_MIN_SUPPLIERS", new UpdateSystemParameterRequest("0"), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await f.Client.PutAsJsonAsync("/api/system-parameters/RFQ_MIN_SUPPLIERS", new UpdateSystemParameterRequest("2,5"), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await f.Client.PutAsJsonAsync("/api/system-parameters/COSTING_METHOD", new UpdateSystemParameterRequest("FIFO"), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await f.Client.PutAsJsonAsync("/api/system-parameters/KHONG_CO", new UpdateSystemParameterRequest("1"), Ct)).StatusCode);

        // Hạ xuống 2: RFQ mời 2 NCC gửi được; công ty khác (tổng công ty) vẫn giữ mặc định 3.
        rfqMin = await ReadAsync<SystemParameterDto>(await f.Client.PutAsJsonAsync("/api/system-parameters/rfq_min_suppliers", new UpdateSystemParameterRequest("2"), Ct));
        Assert.Equal(("2", false), (rfqMin.Value, rfqMin.IsDefault));
        Assert.Equal("3", (await ReadAsync<List<SystemParameterDto>>(await f.Head.GetAsync("/api/system-parameters", Ct)))
            .Single(p => p.Code == SystemParameterCodes.RfqMinSuppliers).Value);
        var rfq = await ReadAsync<RfqDto>(await f.Client.PostAsJsonAsync("/api/rfqs", Rfq(f, null, a, b), Ct));
        Assert.Equal(2, rfq.MinSuppliers);
        Assert.Equal("Sent", (await ReadAsync<RfqDto>(await f.Client.PostAsync($"/api/rfqs/{rfq.Id}/send", null, Ct))).Status);

        // Về mặc định (giá trị trống): lại cần 3 NCC.
        rfqMin = await ReadAsync<SystemParameterDto>(await f.Client.PutAsJsonAsync("/api/system-parameters/RFQ_MIN_SUPPLIERS", new UpdateSystemParameterRequest(null), Ct));
        Assert.Equal(("3", true), (rfqMin.Value, rfqMin.IsDefault));
        rfq = await ReadAsync<RfqDto>(await f.Client.PostAsJsonAsync("/api/rfqs", Rfq(f, null, a, b), Ct));
        Assert.Contains("ít hơn 3", await TitleAsync(await f.Client.PostAsync($"/api/rfqs/{rfq.Id}/send", null, Ct)));
    }

    [Fact]
    public async Task TimNguon_Rfq_BaoGia_ChonThau_SinhPo()
    {
        var f = await ArrangeAsync();
        var (a, b, c) = (f.Suppliers[0], f.Suppliers[1], f.Suppliers[2]);
        var sourcing = await ReadAsync<SourcingDto>(await f.Client.PostAsJsonAsync("/api/sourcing",
            new SaveSourcingRequest(Today, f.ItemId, null, "Chịu nhiệt 150°C", 220, Today.AddDays(10), "Anh Nam", null, [new(null, a, "Contacted", "Đã gửi hồ sơ")]), Ct));
        Assert.Matches($"^SRC-{f.CompanyCode}-{Today:yyMM}-\\d{{4}}$", sourcing.No);

        // Mời 2 NCC: lưu nháp được, gửi không giải trình bị chặn (tối thiểu 3).
        var rfq = await ReadAsync<RfqDto>(await f.Client.PostAsJsonAsync("/api/rfqs", Rfq(f, sourcing.Id, a, b), Ct));
        Assert.Contains("ít hơn 3", await TitleAsync(await f.Client.PostAsync($"/api/rfqs/{rfq.Id}/send", null, Ct)));
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
            new AwardRfqRequest([new(cai, a, null), new(thung, a, null)], "Thử nghiệm: NCC khác không chào giá"), Ct)));
        rfq = await ReadAsync<RfqDto>(await f.Client.PostAsJsonAsync($"/api/rfqs/{rfq.Id}/award",
            new AwardRfqRequest([new(cai, a, "Gộp một NCC để giao một lần"), new(thung, a, null)], "Thử nghiệm: NCC khác không chào giá"), Ct));
        // Lát B3: kết quả chọn thầu chờ hội đồng duyệt – chưa sinh PO, chưa vào lịch sử giá.
        Assert.Equal(("AwardPending", false, true), (rfq.Status, rfq.Actions.CanCreateOrders, rfq.Actions.CanRecall));
        Assert.Equal(5_000_000m + 5_700_000m, rfq.Suppliers.Single(s => s.SupplierId == a).AwardedAmount);
        Assert.Equal(10_700_000m, rfq.AwardAmount);
        Assert.Contains("chưa chọn thầu", await TitleAsync(await f.Client.PostAsync($"/api/rfqs/{rfq.Id}/create-orders", null, Ct)));
        var history = await ReadAsync<PriceComparisonDto>(await f.Client.GetAsync($"/api/price-comparison?itemId={f.ItemId}", Ct));
        Assert.DoesNotContain(history.History, h => h.DocumentNo == rfq.No);
        var approver = await ApproverAsync(f);
        Assert.Equal("Awarded", (await ReadAsync<RfqDto>(await approver.PostAsJsonAsync($"/api/rfqs/{rfq.Id}/approve", new ApprovalActionRequest("Đồng ý"), Ct))).Status);
        rfq = await ReadAsync<RfqDto>(await f.Client.GetAsync($"/api/rfqs/{rfq.Id}", Ct));
        Assert.Equal(("Awarded", true), (rfq.Status, rfq.Actions.CanCreateOrders));

        // Sinh PO nháp cho A: giá theo báo giá, thuế suất trống thì theo mặt hàng; bấm lại thì báo đã có.
        var created = await ReadAsync<List<CreatedOrderDto>>(await f.Client.PostAsync($"/api/rfqs/{rfq.Id}/create-orders", null, Ct));
        var po = await ReadAsync<PurchaseOrderDto>(await f.Client.GetAsync($"/api/purchase-orders/{created.Single().PurchaseOrderId}", Ct));
        Assert.Equal(("Draft", a, 2), (po.Status, po.SupplierId, po.Lines.Count));
        Assert.Equal([(50_000m, 0m, "10"), (600_000m, 5m, "8")], po.Lines.Select(l => (l.UnitPrice, l.DiscountPct, l.VatCode)));
        Assert.Equal(10_700_000m, po.TotalAmount);
        // Nguồn giá: giá trúng thầu đã được hội đồng duyệt (design.md mục 18.10) – không lệch, không ngoài hợp đồng.
        Assert.All(po.Lines, l => Assert.Equal(("Rfq", rfq.No, false, false), (l.PriceSource, l.SourceRfqNo, l.IsPriceDeviation, l.IsOutOfContract)));
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

    /// <summary>Người duyệt kết quả chọn thầu tại công ty của fixture: chỉ có quyền xem RFQ và duyệt kết quả.</summary>
    private async Task<HttpClient> ApproverAsync(Fixture f)
    {
        var roleCode = UniqueCode("H");
        await _factory.CreateRoleAsync(roleCode, Permissions.Sourcing.RfqView, Permissions.Sourcing.RfqApproveAward);
        var userName = $"hd.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(userName, f.CompanyCode, roleCode);
        return await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, f.CompanyCode);
    }

    [Fact]
    public async Task GuiThieuNccCoGiaiTrinh_ChonThauPhaiQuaHoiDongDuyet()
    {
        // Lát B3 (M3, M4 – design.md mục 8.6): mời 2 NCC (tối thiểu 3) vẫn gửi được khi có giải trình; kết quả chọn thầu phải được duyệt.
        var f = await ArrangeAsync();
        var (a, b) = (f.Suppliers[0], f.Suppliers[1]);
        var rfq = await ReadAsync<RfqDto>(await f.Client.PostAsJsonAsync("/api/rfqs", Rfq(f, null, a, b), Ct));
        Assert.Contains("giải trình", await TitleAsync(await f.Client.PostAsJsonAsync($"/api/rfqs/{rfq.Id}/send", new SendRfqRequest(" "), Ct)));
        rfq = await ReadAsync<RfqDto>(await f.Client.PostAsJsonAsync($"/api/rfqs/{rfq.Id}/send", new SendRfqRequest("Chỉ 2 đại lý chính hãng"), Ct));
        Assert.Equal(("Sent", "Chỉ 2 đại lý chính hãng"), (rfq.Status, rfq.FewSuppliersReason));

        var (cai, thung) = (rfq.Lines[0].Id, rfq.Lines[1].Id);
        await ReadAsync<RfqDto>(await f.Client.PutAsJsonAsync($"/api/rfqs/{rfq.Id}/quotations/{a}", Quote(Q(cai, 50_000), Q(thung, 600_000)), Ct));
        await ReadAsync<RfqDto>(await f.Client.PutAsJsonAsync($"/api/rfqs/{rfq.Id}/quotations/{b}", Quote(Q(cai, 52_000)), Ct));
        rfq = await ReadAsync<RfqDto>(await f.Client.PostAsJsonAsync($"/api/rfqs/{rfq.Id}/award", new AwardRfqRequest([new(cai, a, null), new(thung, a, null)], "Thử nghiệm: NCC khác không chào giá"), Ct));
        Assert.Equal(("AwardPending", false), (rfq.Status, rfq.Actions.CanApprove));
        Assert.Single(rfq.Approvals);

        // Người chọn thầu không tự duyệt (dù có quyền); đang chờ duyệt thì không nhập báo giá, không hủy.
        Assert.Contains("không tự duyệt", await TitleAsync(await f.Client.PostAsJsonAsync($"/api/rfqs/{rfq.Id}/approve", new ApprovalActionRequest(null), Ct)));
        Assert.Contains("đang nhận báo giá", await TitleAsync(await f.Client.PutAsJsonAsync($"/api/rfqs/{rfq.Id}/quotations/{b}", Quote(Q(cai, 1)), Ct)));
        Assert.Contains("rút về", await TitleAsync(await f.Client.PostAsync($"/api/rfqs/{rfq.Id}/cancel", null, Ct)));

        // Hội đồng từ chối (bắt buộc lý do) → về Đang nhận báo giá, giữ lựa chọn cũ; bổ sung báo giá, chọn lại.
        var approver = await ApproverAsync(f);
        Assert.True((await ReadAsync<RfqDto>(await approver.GetAsync($"/api/rfqs/{rfq.Id}", Ct))).Actions.CanApprove);
        Assert.Equal(HttpStatusCode.BadRequest, (await approver.PostAsJsonAsync($"/api/rfqs/{rfq.Id}/reject", new ApprovalActionRequest(" "), Ct)).StatusCode);
        rfq = await ReadAsync<RfqDto>(await approver.PostAsJsonAsync($"/api/rfqs/{rfq.Id}/reject", new ApprovalActionRequest("Đàm phán lại giá dòng cái"), Ct));
        Assert.Equal(("Sent", "Đàm phán lại giá dòng cái", a), (rfq.Status, rfq.RejectReason, rfq.Lines[0].AwardedSupplierId));
        await ReadAsync<RfqDto>(await f.Client.PutAsJsonAsync($"/api/rfqs/{rfq.Id}/quotations/{a}", Quote(Q(cai, 49_000), Q(thung, 600_000)), Ct));

        // Gửi lại rồi rút về, gửi lần nữa: mỗi lượt một bước mới; người duyệt chỉ có quyền xem + duyệt vẫn duyệt được.
        rfq = await ReadAsync<RfqDto>(await f.Client.PostAsJsonAsync($"/api/rfqs/{rfq.Id}/award", new AwardRfqRequest([new(cai, a, null), new(thung, a, null)], "Thử nghiệm: NCC khác không chào giá"), Ct));
        rfq = await ReadAsync<RfqDto>(await f.Client.PostAsync($"/api/rfqs/{rfq.Id}/recall", null, Ct));
        Assert.Equal("Sent", rfq.Status);
        rfq = await ReadAsync<RfqDto>(await f.Client.PostAsJsonAsync($"/api/rfqs/{rfq.Id}/award", new AwardRfqRequest([new(cai, a, null), new(thung, a, null)], "Thử nghiệm: NCC khác không chào giá"), Ct));
        Assert.Null(rfq.RejectReason);
        rfq = await ReadAsync<RfqDto>(await approver.PostAsJsonAsync($"/api/rfqs/{rfq.Id}/approve", new ApprovalActionRequest(null), Ct));
        Assert.Equal(("Awarded", 4_900_000m + 6_000_000m), (rfq.Status, rfq.AwardAmount));
        Assert.Equal(["Rejected", "Skipped", "Approved"], rfq.Approvals.OrderBy(s => s.Round).Select(s => s.Status));
    }

    [Fact]
    public async Task HoSoTrinh_VaTaoBangGiaTuKetQuaThau()
    {
        // Lát B3b (design.md mục 8.6): đánh dấu hồ sơ trình trước khi gửi duyệt, khóa khi chờ duyệt; RFQ đã duyệt sinh bảng giá NCC nháp.
        var f = await ArrangeAsync();
        var (a, b, c) = (f.Suppliers[0], f.Suppliers[1], f.Suppliers[2]);
        var rfq = await ReadAsync<RfqDto>(await f.Client.PostAsJsonAsync("/api/rfqs", Rfq(f, null, a, b, c), Ct));
        Assert.Equal((7, true), (rfq.Dossier.Count, rfq.Actions.CanEditDossier));
        Assert.False(rfq.Dossier.Single(d => d.Code == "COMPARE").IsDone);
        Assert.Contains("không có", await TitleAsync(await f.Client.PutAsJsonAsync($"/api/rfqs/{rfq.Id}/dossier",
            new SaveRfqDossierRequest([new("KHAC", true, null)]), Ct)));
        rfq = await ReadAsync<RfqDto>(await f.Client.PutAsJsonAsync($"/api/rfqs/{rfq.Id}/dossier",
            new SaveRfqDossierRequest([new("minutes", true, "BB số 12"), new("BUDGET", true, null)]), Ct));
        Assert.Equal(("BB số 12", 2), (rfq.Dossier.Single(d => d.Code == "MINUTES").Note, rfq.Dossier.Count(d => d.IsDone)));

        rfq = await ReadAsync<RfqDto>(await f.Client.PostAsync($"/api/rfqs/{rfq.Id}/send", null, Ct));
        var (cai, thung) = (rfq.Lines[0].Id, rfq.Lines[1].Id);
        await ReadAsync<RfqDto>(await f.Client.PutAsJsonAsync($"/api/rfqs/{rfq.Id}/quotations/{a}", Quote(Q(cai, 50_000, 0, null, 7), Q(thung, 600_000, 5, "8", 7)), Ct));
        rfq = await ReadAsync<RfqDto>(await f.Client.PutAsJsonAsync($"/api/rfqs/{rfq.Id}/quotations/{b}", Quote(Q(cai, 48_000)), Ct));
        Assert.True(rfq.Dossier.Single(d => d.Code == "COMPARE").IsDone);

        // Mặc định thiếu mục vẫn chọn thầu được; công ty bật tham số RFQ_DOSSIER_REQUIRED thì phải đủ 7 mục mới chọn thầu.
        var award = new AwardRfqRequest([new(cai, b, null), new(thung, a, null)], "Thử nghiệm: NCC khác không chào giá");
        await ReadAsync<SystemParameterDto>(await f.Client.PutAsJsonAsync(
            $"/api/system-parameters/{SystemParameterCodes.RfqDossierRequired}", new UpdateSystemParameterRequest("true"), Ct));
        var blocked = await f.Client.PostAsJsonAsync($"/api/rfqs/{rfq.Id}/award", award, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, blocked.StatusCode);
        Assert.StartsWith("Hồ sơ trình còn thiếu:", await TitleAsync(blocked));
        Assert.Equal("Sent", (await ReadAsync<RfqDto>(await f.Client.GetAsync($"/api/rfqs/{rfq.Id}", Ct))).Status);
        rfq = await ReadAsync<RfqDto>(await f.Client.PutAsJsonAsync($"/api/rfqs/{rfq.Id}/dossier",
            new SaveRfqDossierRequest(rfq.Dossier.Where(d => !d.IsAutomatic).Select(d => new SaveRfqDossierItemRequest(d.Code, true, d.Note)).ToList()), Ct));
        rfq = await ReadAsync<RfqDto>(await f.Client.PostAsJsonAsync($"/api/rfqs/{rfq.Id}/award", award, Ct));
        Assert.False(rfq.Actions.CanEditDossier);
        Assert.Contains("không sửa được", await TitleAsync(await f.Client.PutAsJsonAsync($"/api/rfqs/{rfq.Id}/dossier",
            new SaveRfqDossierRequest([new("COST", true, null)]), Ct)));
        Assert.Contains("chưa được duyệt", await TitleAsync(await f.Client.PostAsync($"/api/rfqs/{rfq.Id}/create-price-lists", null, Ct)));

        await ReadAsync<RfqDto>(await (await ApproverAsync(f)).PostAsJsonAsync($"/api/rfqs/{rfq.Id}/approve", new ApprovalActionRequest(null), Ct));
        Assert.True((await ReadAsync<RfqDto>(await f.Client.GetAsync($"/api/rfqs/{rfq.Id}", Ct))).Actions.CanCreatePriceLists);
        var created = await ReadAsync<List<CreatedPriceListDto>>(await f.Client.PostAsync($"/api/rfqs/{rfq.Id}/create-price-lists", null, Ct));
        Assert.Equal(2, created.Count);
        var listA = await ReadAsync<PriceListDto>(await f.Client.GetAsync($"/api/price-lists/{created.Single(x => x.SupplierId == a).PriceListId}", Ct));
        // Bảng giá nháp của công ty, hiệu lực từ hôm nay tới hết hiệu lực báo giá; dòng thùng: giá, % CK, VAT theo báo giá.
        Assert.Equal(("Draft", false, Today, Today.AddDays(30)), (listA.Status, listA.IsGroupWide, listA.ValidFrom, listA.ValidTo));
        Assert.Equal([(f.Thung, 0m, 600_000m, 5m, "8")], listA.Lines.Select(l => (l.UomId, l.MinQty, l.UnitPrice, l.DiscountPct, l.VatCode)));
        Assert.Contains("đã có bảng giá", await TitleAsync(await f.Client.PostAsync($"/api/rfqs/{rfq.Id}/create-price-lists", null, Ct)));
        rfq = await ReadAsync<RfqDto>(await f.Client.GetAsync($"/api/rfqs/{rfq.Id}", Ct));
        Assert.Equal(listA.No, rfq.Suppliers.Single(s => s.SupplierId == a).PriceListNo);
        Assert.False(rfq.Actions.CanCreatePriceLists);
    }
}
