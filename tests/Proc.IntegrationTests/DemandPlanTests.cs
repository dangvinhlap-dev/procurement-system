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
        var category = await ReadAsync<ItemCategoryDetailDto>(await tct.PostAsJsonAsync("/api/item-categories",
            new CreateItemCategoryRequest(null, $"Nhóm kế hoạch {Guid.NewGuid():N}", null, null), Ct));
        var other = await ReadAsync<ItemCategoryDetailDto>(await tct.PostAsJsonAsync("/api/item-categories",
            new CreateItemCategoryRequest(null, $"Nhóm khác kế hoạch {Guid.NewGuid():N}", null, null), Ct));
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
}
