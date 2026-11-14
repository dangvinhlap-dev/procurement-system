using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Proc.Application.Common;
using Proc.Application.Documents;
using Proc.Application.MasterData;
using Proc.Application.Purchasing;
using Proc.Domain.Organization;

namespace Proc.IntegrationTests;

/// <summary>
/// Phiếu kế hoạch nhu cầu (lát G5a, design.md mục 18.25): mỗi công ty lập nhu cầu năm × nhóm hàng (SL 12 tháng), chốt, điều chỉnh (phiếu cũ
/// thành Đã thay), hủy bắt buộc lý do; mỗi mặt hàng × năm của một công ty chỉ thuộc một phiếu còn hiệu lực.
/// </summary>
[Collection(ApiCollection.Name)]
public class DemandPlanTests
{
    private const string Url = "/api/demand-plans";
    private readonly ProcApiFactory _factory;

    public DemandPlanTests(ProcApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string UniqueCode(string prefix) => $"{prefix}{Guid.NewGuid():N}".ToUpperInvariant()[..8];

    private static int Year => TimeZoneInfo.ConvertTime(DateTime.UtcNow, VietnamTime.Zone).Year + 1;

    private static DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTime.UtcNow, VietnamTime.Zone));

    private static async Task<string?> TitleAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(Ct))?.Title;

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(Ct)}");
        return (await response.Content.ReadFromJsonAsync<T>(Ct))!;
    }

    private static decimal[] Months(params decimal[] first)
    {
        var months = new decimal[12];
        first.CopyTo(months, 0);
        return months;
    }

    [Fact]
    public async Task LapChotDieuChinhHuy_KhongTrungMatHangCungNamCungCongTy()
    {
        var userName = $"kh.{Guid.NewGuid():N}"[..16];
        await _factory.CreateUserAsync(userName, ProcApiFactory.TestPassword, ["TCT", "BHA"]);
        var bha = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, "BHA");
        var tct = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, "TCT");
        var cai = (await bha.GetFromJsonAsync<List<UomDto>>("/api/uoms", Ct))!.Single(u => u.Code == "CAI").Id;
        var category = await ReadAsync<ItemCategoryDetailDto>(await TestCategories.PostLeafAsync(tct,
            new CreateItemCategoryRequest(TestCodes.Category(), $"Nhóm kế hoạch {Guid.NewGuid():N}", null, null), Ct));
        var other = await ReadAsync<ItemCategoryDetailDto>(await TestCategories.PostLeafAsync(tct,
            new CreateItemCategoryRequest(TestCodes.Category(), $"Nhóm khác kế hoạch {Guid.NewGuid():N}", null, null), Ct));
        async Task<Guid> ItemAsync(Guid categoryId, string name) => (await ReadAsync<ItemDetailDto>(await tct.PostAsJsonAsync("/api/items",
            new CreateItemRequest(null, name, null, "Inventory", categoryId, cai, null, null, null, null, "10", null, false, false, null, null, []), Ct))).Id;
        var item = await ItemAsync(category.Id, "Găng tay kế hoạch");
        var item2 = await ItemAsync(category.Id, "Khẩu trang kế hoạch");
        var outside = await ItemAsync(other.Id, "Ống thép kế hoạch");

        SaveDemandPlanRequest Request(int year, params SaveDemandPlanLineRequest[] lines) => new(year, category.Id, "Theo định mức", lines);
        SaveDemandPlanLineRequest Line(Guid itemId, params decimal[] months) => new(null, itemId, cai, Months(months), null);

        // Năm đã qua, mặt hàng ngoài nhóm hàng, dòng toàn số 0, thiếu tháng.
        Assert.Contains("đã qua", await TitleAsync(await bha.PostAsJsonAsync(Url, Request(Year - 2, Line(item, 1)), Ct)));
        Assert.Contains("không thuộc nhóm hàng", await TitleAsync(await bha.PostAsJsonAsync(Url, Request(Year, Line(outside, 1)), Ct)));
        Assert.Contains("ít nhất một tháng", await TitleAsync(await bha.PostAsJsonAsync(Url, Request(Year, Line(item)), Ct)));
        Assert.Equal(HttpStatusCode.BadRequest,
            (await bha.PostAsJsonAsync(Url, Request(Year, new SaveDemandPlanLineRequest(null, item, cai, [1m], null)), Ct)).StatusCode);

        var draft = await ReadAsync<DemandPlanDto>(await bha.PostAsJsonAsync(Url, Request(Year, Line(item, 100, 120, 80), Line(item2, 5)), Ct));
        Assert.StartsWith("KHN", draft.No);
        Assert.Equal(("Draft", 2, true, 300m), (draft.Status, draft.Lines.Count, draft.Actions.CanEdit, draft.Lines[0].Total));

        // Cùng mặt hàng, cùng năm, cùng công ty: chặn; công ty khác lập được (mỗi pháp nhân một kế hoạch, tổng hợp gộp cộng lại).
        Assert.Contains(draft.No, await TitleAsync(await bha.PostAsJsonAsync(Url, Request(Year, Line(item, 1)), Ct)));
        var tctPlan = await ReadAsync<DemandPlanDto>(await tct.PostAsJsonAsync(Url, Request(Year, Line(item, 50)), Ct));
        Assert.DoesNotContain(await ReadAsync<List<DemandPlanListItemDto>>(await bha.GetAsync(Url, Ct)), r => r.Id == tctPlan.Id);
        // Năm khác thì không trùng.
        await ReadAsync<DemandPlanDto>(await bha.PostAsJsonAsync(Url, Request(Year + 1, Line(item, 1)), Ct));

        var confirmed = await ReadAsync<DemandPlanDto>(await bha.PostAsync($"{Url}/{draft.Id}/confirm", null, Ct));
        Assert.Equal(("Confirmed", false, true), (confirmed.Status, confirmed.Actions.CanEdit, confirmed.Actions.CanAdjust));
        Assert.NotNull(confirmed.ConfirmedByName);
        Assert.Contains("không ở trạng thái nháp",
            await TitleAsync(await bha.PutAsJsonAsync($"{Url}/{draft.Id}", Request(Year, Line(item, 1)), Ct)));

        // Điều chỉnh: phiếu nháp mới chép nội dung, không trùng với chính phiếu được điều chỉnh; chỉ một phiếu điều chỉnh.
        var adjust = await ReadAsync<DemandPlanDto>(await bha.PostAsync($"{Url}/{draft.Id}/adjust", null, Ct));
        Assert.Equal(("Draft", draft.Id, draft.No, 2), (adjust.Status, adjust.ReplacesId, adjust.ReplacesNo, adjust.Lines.Count));
        Assert.Contains("đã có phiếu điều chỉnh", await TitleAsync(await bha.PostAsync($"{Url}/{draft.Id}/adjust", null, Ct)));
        Assert.Contains("phiếu điều chỉnh",
            await TitleAsync(await bha.PostAsJsonAsync($"{Url}/{draft.Id}/cancel", new CancelDemandPlanRequest("Bỏ"), Ct)));
        var edited = await ReadAsync<DemandPlanDto>(await bha.PutAsJsonAsync($"{Url}/{adjust.Id}", Request(Year, Line(item, 150, 120, 80)), Ct));
        Assert.Equal(350m, Assert.Single(edited.Lines).Total);
        await ReadAsync<DemandPlanDto>(await bha.PostAsync($"{Url}/{adjust.Id}/confirm", null, Ct));
        var replaced = await ReadAsync<DemandPlanDto>(await bha.GetAsync($"{Url}/{draft.Id}", Ct));
        Assert.Equal(("Replaced", adjust.Id, false, false), (replaced.Status, replaced.ReplacedById, replaced.Actions.CanCancel, replaced.Actions.CanAdjust));
        // Phiếu cũ đã thay thì khẩu trang (bị bỏ khỏi phiếu điều chỉnh) lập lại được ở phiếu khác.
        await ReadAsync<DemandPlanDto>(await bha.PostAsJsonAsync(Url, Request(Year, Line(item2, 7)), Ct));

        // Có trong tra cứu chứng từ.
        var registry = await bha.GetFromJsonAsync<DocumentRegistryResult>(
            $"/api/document-registry?from={Today:yyyy-MM-dd}&to={Today:yyyy-MM-dd}&docTypes=KHN", Ct);
        Assert.Contains(registry!.Rows, r => r.Id == adjust.Id);

        // Hủy bắt buộc lý do.
        Assert.Equal(HttpStatusCode.BadRequest, (await tct.PostAsJsonAsync($"{Url}/{tctPlan.Id}/cancel", new CancelDemandPlanRequest(""), Ct)).StatusCode);
        var cancelled = await ReadAsync<DemandPlanDto>(await tct.PostAsJsonAsync($"{Url}/{tctPlan.Id}/cancel", new CancelDemandPlanRequest("Nhập nhầm"), Ct));
        Assert.Equal(("Cancelled", false), (cancelled.Status, cancelled.Actions.CanCancel));

        // Chỉ quyền xem: đọc được, không lập được.
        var role = UniqueCode("K");
        await _factory.CreateRoleAsync(role, Permissions.Purchase.DemandPlanView);
        var viewerName = $"kv.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(viewerName, "BHA", role);
        var viewer = await _factory.CreateClientInCompanyAsync(viewerName, ProcApiFactory.TestPassword, "BHA");
        var view = await ReadAsync<DemandPlanDto>(await viewer.GetAsync($"{Url}/{adjust.Id}", Ct));
        Assert.NotNull(view.Actions.ReadOnlyReason);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsJsonAsync(Url, Request(Year, Line(item, 1)), Ct)).StatusCode);
    }

    /// <summary>
    /// Lát G5b: tổng hợp nhu cầu gộp – toàn tập đoàn (quyền riêng) cộng mọi công ty, chỉ phiếu Đã chốt, SL quy về ĐVT cơ sở; không chọn toàn
    /// tập đoàn thì chỉ công ty đang làm việc.
    /// </summary>
    [Fact]
    public async Task TongHopNhuCau_ToanTapDoanGopCacCongTy_ChiPhieuDaChot_QuyVeDvtCoSo()
    {
        var userName = $"kt.{Guid.NewGuid():N}"[..16];
        await _factory.CreateUserAsync(userName, ProcApiFactory.TestPassword, ["TCT", "BHA", "NMI"]);
        var tct = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, "TCT");
        var bha = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, "BHA");
        var nmi = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, "NMI");
        var uoms = (await tct.GetFromJsonAsync<List<UomDto>>("/api/uoms", Ct))!;
        var (cai, thung) = (uoms.Single(u => u.Code == "CAI").Id, uoms.Single(u => u.Code == "THUNG").Id);
        // Kế hoạch lập theo nhóm cấp 3; mặt hàng ở hai nhóm con cấp 4 (mặt hàng chỉ gắn nhóm cấp 4 – design.md mục 5.7, N4-5).
        var category = await ReadAsync<ItemCategoryDetailDto>(await TestCategories.PostAtLevelAsync(tct,
            new CreateItemCategoryRequest(TestCodes.Category(), $"Nhóm tổng hợp {Guid.NewGuid():N}", null, null), 3, Ct));
        var child = await ReadAsync<ItemCategoryDetailDto>(await tct.PostAsJsonAsync("/api/item-categories",
            new CreateItemCategoryRequest(TestCodes.Category(), $"Nhóm con tổng hợp {Guid.NewGuid():N}", category.Id, null), Ct));
        var sibling = await ReadAsync<ItemCategoryDetailDto>(await tct.PostAsJsonAsync("/api/item-categories",
            new CreateItemCategoryRequest(TestCodes.Category(), $"Nhóm con nháp {Guid.NewGuid():N}", category.Id, null), Ct));
        async Task<ItemDetailDto> ItemAsync(Guid categoryId, string name) => await ReadAsync<ItemDetailDto>(await tct.PostAsJsonAsync("/api/items",
            new CreateItemRequest(null, name, null, "Inventory", categoryId, cai, null, null, null, null, "10", null, false, false, null, null,
                [new(thung, 12)]), Ct));
        var item = await ItemAsync(child.Id, "Bóng đèn tổng hợp");
        var draftOnly = await ItemAsync(sibling.Id, "Dây điện tổng hợp");

        SaveDemandPlanRequest Request(params SaveDemandPlanLineRequest[] lines) => new(Year, category.Id, null, lines);
        SaveDemandPlanLineRequest Line(Guid itemId, Guid uomId, params decimal[] months) => new(null, itemId, uomId, Months(months), null);
        async Task<DemandPlanDto> ConfirmAsync(HttpClient client, DemandPlanDto plan) =>
            await ReadAsync<DemandPlanDto>(await client.PostAsync($"{Url}/{plan.Id}/confirm", null, Ct));
        string SummaryUrl(Guid categoryId, bool all = true) => $"{Url}/summary?year={Year}&categoryId={categoryId}&allCompanies={all}";

        // BHA chốt rồi điều chỉnh (phiếu cũ Đã thay – không tính); NMI khai theo thùng; NMI còn phiếu nháp; TCT phiếu đã hủy.
        var bhaPlan = await ConfirmAsync(bha, await ReadAsync<DemandPlanDto>(await bha.PostAsJsonAsync(Url, Request(Line(item.Id, cai, 10, 20)), Ct)));
        var bhaAdjust = await ReadAsync<DemandPlanDto>(await bha.PostAsync($"{Url}/{bhaPlan.Id}/adjust", null, Ct));
        await ReadAsync<DemandPlanDto>(await bha.PutAsJsonAsync($"{Url}/{bhaAdjust.Id}", Request(Line(item.Id, cai, 15, 20)), Ct));
        await ConfirmAsync(bha, bhaAdjust);
        var nmiPlan = await ConfirmAsync(nmi, await ReadAsync<DemandPlanDto>(await nmi.PostAsJsonAsync(Url, Request(Line(item.Id, thung, 1, 2.5m)), Ct)));
        var nmiDraft = await ReadAsync<DemandPlanDto>(await nmi.PostAsJsonAsync(Url, Request(Line(draftOnly.Id, cai, 7)), Ct));
        var tctPlan = await ReadAsync<DemandPlanDto>(await tct.PostAsJsonAsync(Url, Request(Line(item.Id, cai, 1000)), Ct));
        await ReadAsync<DemandPlanDto>(await tct.PostAsJsonAsync($"{Url}/{tctPlan.Id}/cancel", new CancelDemandPlanRequest("Nhập nhầm"), Ct));

        // Toàn tập đoàn: gộp mọi công ty, lọc nhóm cha gồm nhóm con; 1 thùng = 12 cái, 2,5 thùng = 30 cái.
        var summary = await ReadAsync<DemandSummaryDto>(await tct.GetAsync(SummaryUrl(category.Id), Ct));
        Assert.True(summary.AllCompanies);
        Assert.Equal("TCT", summary.Companies[0].Code);
        var row = Assert.Single(summary.Rows);
        Assert.Equal((item.Id, "CAI", 77m), (row.ItemId, row.BaseUomCode, row.Total));
        Assert.Equal([27m, 50m], row.Months.Take(2));
        var ids = summary.Companies.ToDictionary(c => c.Code, c => c.CompanyId);
        var cells = row.Companies.ToDictionary(c => c.CompanyId);
        Assert.Equal(2, cells.Count);
        Assert.Equal((35m, bhaAdjust.No), (cells[ids["BHA"]].Total, Assert.Single(cells[ids["BHA"]].PlanNos)));
        Assert.Equal([12m, 30m], cells[ids["NMI"]].Months.Take(2));
        Assert.Equal(nmiPlan.No, Assert.Single(cells[ids["NMI"]].PlanNos));
        var counts = summary.Companies.ToDictionary(c => c.Code, c => (c.ConfirmedPlans, c.DraftPlans));
        Assert.Equal(((1, 1), (1, 0), (0, 0)), (counts["NMI"], counts["BHA"], counts["TCT"]));

        // Nháp chốt xong mới vào tổng hợp; lọc nhóm con chỉ còn mặt hàng của nhóm con.
        await ConfirmAsync(nmi, nmiDraft);
        summary = await ReadAsync<DemandSummaryDto>(await tct.GetAsync(SummaryUrl(category.Id), Ct));
        Assert.Equal(7m, summary.Rows.Single(r => r.ItemId == draftOnly.Id).Total);
        summary = await ReadAsync<DemandSummaryDto>(await tct.GetAsync(SummaryUrl(child.Id), Ct));
        Assert.Equal(item.Id, Assert.Single(summary.Rows).ItemId);

        // Không chọn toàn tập đoàn: chỉ nhu cầu của công ty đang làm việc (kể cả ở tổng công ty).
        var own = await ReadAsync<DemandSummaryDto>(await bha.GetAsync(SummaryUrl(category.Id, false), Ct));
        Assert.False(own.AllCompanies);
        Assert.Equal("BHA", Assert.Single(own.Companies).Code);
        Assert.Equal(35m, own.Rows.Single(r => r.ItemId == item.Id).Total);
        var head = await ReadAsync<DemandSummaryDto>(await tct.GetAsync(SummaryUrl(category.Id, false), Ct));
        Assert.Equal("TCT", Assert.Single(head.Companies).Code);
        Assert.Empty(head.Rows);

        // Chỉ quyền xem kế hoạch nhu cầu (không có quyền toàn tập đoàn): xem công ty mình được, toàn tập đoàn bị chặn.
        var viewRole = UniqueCode("K");
        await _factory.CreateRoleAsync(viewRole, Permissions.Purchase.DemandPlanView);
        var viewerName = $"kv.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(viewerName, "TCT", viewRole);
        var viewer = await _factory.CreateClientInCompanyAsync(viewerName, ProcApiFactory.TestPassword, "TCT");
        Assert.True((await viewer.GetAsync(SummaryUrl(category.Id, false), Ct)).IsSuccessStatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.GetAsync(SummaryUrl(category.Id), Ct)).StatusCode);

        // Không có quyền xem kế hoạch nhu cầu: bị chặn.
        var role = UniqueCode("K");
        await _factory.CreateRoleAsync(role, Permissions.Purchase.PrView);
        var otherName = $"kn.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(otherName, "TCT", role);
        var other = await _factory.CreateClientInCompanyAsync(otherName, ProcApiFactory.TestPassword, "TCT");
        Assert.Equal(HttpStatusCode.Forbidden, (await other.GetAsync(SummaryUrl(category.Id), Ct)).StatusCode);
    }

    /// <summary>
    /// Lát G5c: SL kế hoạch cho dòng RFQ / hợp đồng – cộng các tháng trong khoảng (qua hai năm) của phiếu Đã chốt, quy về ĐVT dòng;
    /// công ty đang làm việc hoặc toàn tập đoàn (quyền riêng).
    /// </summary>
    [Fact]
    public async Task LaySlKeHoach_CongThangTrongKhoang_QuyVeDvtDong()
    {
        var userName = $"kq.{Guid.NewGuid():N}"[..16];
        await _factory.CreateUserAsync(userName, ProcApiFactory.TestPassword, ["TCT", "BHA"]);
        var tct = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, "TCT");
        var bha = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, "BHA");
        var uoms = (await tct.GetFromJsonAsync<List<UomDto>>("/api/uoms", Ct))!;
        var (cai, thung, hop) = (uoms.Single(u => u.Code == "CAI").Id, uoms.Single(u => u.Code == "THUNG").Id, uoms.Single(u => u.Code == "HOP").Id);
        var category = await ReadAsync<ItemCategoryDetailDto>(await TestCategories.PostLeafAsync(tct,
            new CreateItemCategoryRequest(TestCodes.Category(), $"Nhóm SL kế hoạch {Guid.NewGuid():N}", null, null), Ct));
        var item = await ReadAsync<ItemDetailDto>(await tct.PostAsJsonAsync("/api/items",
            new CreateItemRequest(null, "Ốc vít SL kế hoạch", null, "Inventory", category.Id, cai, null, null, null, null, "10", null, false, false,
                null, null, [new(thung, 12)]), Ct));
        async Task PlanAsync(HttpClient client, int year, Guid uomId, decimal[] months)
        {
            var plan = await ReadAsync<DemandPlanDto>(await client.PostAsJsonAsync(Url,
                new SaveDemandPlanRequest(year, category.Id, null, [new SaveDemandPlanLineRequest(null, item.Id, uomId, months, null)]), Ct));
            await ReadAsync<DemandPlanDto>(await client.PostAsync($"{Url}/{plan.Id}/confirm", null, Ct));
        }

        var m = new decimal[12];
        (m[6], m[11]) = (24, 12);
        await PlanAsync(bha, Year, cai, m);
        await PlanAsync(bha, Year + 1, cai, Months(36));
        var t = new decimal[12];
        t[6] = 1;
        await PlanAsync(tct, Year, thung, t);
        // Phiếu nháp không tính.
        await ReadAsync<DemandPlanDto>(await tct.PostAsJsonAsync(Url,
            new SaveDemandPlanRequest(Year + 1, category.Id, null, [new SaveDemandPlanLineRequest(null, item.Id, cai, Months(500), null)]), Ct));

        PlannedQuantityRequest Request(DateOnly from, DateOnly to, bool all) =>
            new(from, to, all, [new(item.Id, thung), new(item.Id, cai), new(item.Id, hop)]);
        var july = new DateOnly(Year, 7, 1);
        var june = new DateOnly(Year + 1, 6, 30);

        // BHA, 01/07/năm – 30/06/năm sau (trọn tháng): 24 + 12 + 36 = 72 cái = 6 thùng; ĐVT không quy đổi (HOP) không có SL.
        var own = await ReadAsync<List<PlannedQuantityDto>>(await bha.PostAsJsonAsync($"{Url}/quantities", Request(july, june, false), Ct));
        Assert.Equal([6m, 72m, null], own.Select(r => r.Qty));
        Assert.Equal([72m, 72m, null], own.Select(r => r.BaseQty));
        Assert.Equal("CAI", own[0].BaseUomCode);
        var company = Assert.Single(own[0].Companies);
        Assert.Equal(("BHA", 6m, 2), (company.CompanyCode, company.Qty, company.PlanNos.Count));

        // Toàn tập đoàn (ở TCT): cộng 1 thùng của TCT → 84 cái = 7 thùng.
        var all = await ReadAsync<List<PlannedQuantityDto>>(await tct.PostAsJsonAsync($"{Url}/quantities", Request(july, june, true), Ct));
        Assert.Equal([7m, 84m], all.Take(2).Select(r => r.Qty));
        Assert.Equal(["BHA", "TCT"], all[1].Companies.Select(c => c.CompanyCode));

        // Khoảng không có kế hoạch: null; khoảng ngược, từ 36 tháng trở lên: lỗi (01/01/năm – 31/12/năm+2 vừa đủ 36 tháng).
        var none = await ReadAsync<List<PlannedQuantityDto>>(await bha.PostAsJsonAsync($"{Url}/quantities",
            Request(new DateOnly(Year, 8, 1), new DateOnly(Year, 11, 1), false), Ct));
        Assert.All(none, r => Assert.Null(r.Qty));
        Assert.True((await bha.PostAsJsonAsync($"{Url}/quantities",
            Request(new DateOnly(Year, 1, 1), new DateOnly(Year + 2, 12, 31), false), Ct)).IsSuccessStatusCode);
        Assert.Contains("Ngày cuối", await TitleAsync(await bha.PostAsJsonAsync($"{Url}/quantities", Request(june, july, false), Ct)));
        Assert.Contains("36 tháng", await TitleAsync(await bha.PostAsJsonAsync($"{Url}/quantities",
            Request(new DateOnly(Year, 1, 1), new DateOnly(Year + 3, 1, 1), false), Ct)));

        // Chỉ quyền xem kế hoạch: công ty mình được, toàn tập đoàn bị chặn.
        var role = UniqueCode("K");
        await _factory.CreateRoleAsync(role, Permissions.Purchase.DemandPlanView);
        var viewerName = $"kw.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(viewerName, "BHA", role);
        var viewer = await _factory.CreateClientInCompanyAsync(viewerName, ProcApiFactory.TestPassword, "BHA");
        Assert.True((await viewer.PostAsJsonAsync($"{Url}/quantities", Request(july, june, false), Ct)).IsSuccessStatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsJsonAsync($"{Url}/quantities", Request(july, june, true), Ct)).StatusCode);
    }
    /// <summary>
    /// SL kế hoạch chia theo ngày cho tháng đầu / cuối không trọn: hợp đồng 15/01 – 14/03 lấy 17/31 tháng 1 và 14/31 tháng 3; kèm SL theo
    /// ĐVT cơ sở để giao diện xử lý SL lẻ (dòng theo thùng 31 cái = 2,5833 thùng).
    /// </summary>
    [Fact]
    public async Task LaySlKeHoach_ThangDauCuoiKhongTron_ChiaTheoNgay()
    {
        var client = await _factory.CreateClientInCompanyAsync(ProcApiFactory.AdminUserName, ProcApiFactory.AdminPassword, "TCT");
        var uoms = (await client.GetFromJsonAsync<List<UomDto>>("/api/uoms", Ct))!;
        var (cai, thung) = (uoms.Single(u => u.Code == "CAI").Id, uoms.Single(u => u.Code == "THUNG").Id);
        var category = await ReadAsync<ItemCategoryDetailDto>(await TestCategories.PostLeafAsync(client,
            new CreateItemCategoryRequest(TestCodes.Category(), $"Nhóm chia ngày {Guid.NewGuid():N}", null, null), Ct));
        var item = await ReadAsync<ItemDetailDto>(await client.PostAsJsonAsync("/api/items",
            new CreateItemRequest(null, "Ốc vít chia ngày", null, "Inventory", category.Id, cai, null, null, null, null, "10", null, false, false,
                null, null, [new(thung, 12)]), Ct));
        // Tháng 1: 31 cái, tháng 2: 50 cái, tháng 3: 31 cái.
        var plan = await ReadAsync<DemandPlanDto>(await client.PostAsJsonAsync(Url,
            new SaveDemandPlanRequest(Year, category.Id, null, [new SaveDemandPlanLineRequest(null, item.Id, cai, Months(31, 50, 31), null)]), Ct));
        await ReadAsync<DemandPlanDto>(await client.PostAsync($"{Url}/{plan.Id}/confirm", null, Ct));

        // 15/01 – 14/03: 31 × 17/31 + 50 + 31 × 14/31 = 17 + 50 + 14 = 81 cái = 6,75 thùng.
        var result = await ReadAsync<List<PlannedQuantityDto>>(await client.PostAsJsonAsync($"{Url}/quantities",
            new PlannedQuantityRequest(new DateOnly(Year, 1, 15), new DateOnly(Year, 3, 14), false, [new(item.Id, thung), new(item.Id, cai)]), Ct));
        Assert.Equal([6.75m, 81m], result.Select(r => r.Qty));
        Assert.Equal([81m, 81m], result.Select(r => r.BaseQty));

        // Chỉ ngày 15/01 – 14/02: 17 cái + 50 × 14/28 (hoặc 14/29 năm nhuận).
        var february = DateTime.DaysInMonth(Year, 2);
        var part = await ReadAsync<List<PlannedQuantityDto>>(await client.PostAsJsonAsync($"{Url}/quantities",
            new PlannedQuantityRequest(new DateOnly(Year, 1, 15), new DateOnly(Year, 2, 14), false, [new(item.Id, cai)]), Ct));
        Assert.Equal(decimal.Round(17m + 50m * 14 / february, 4, MidpointRounding.AwayFromZero), part[0].Qty);
    }
}
