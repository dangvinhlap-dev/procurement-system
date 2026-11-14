using System.Net.Http.Json;
using Proc.Application.Common;
using Proc.Application.MasterData;
using Proc.Application.Purchasing;
using Proc.Domain.Organization;

namespace Proc.IntegrationTests;

/// <summary>
/// Định mức sử dụng trên PR (lát G11, design.md mục 18.25): định mức = SL kế hoạch nhu cầu đã chốt của công ty, lũy kế từ tháng 1 đến hết
/// tháng cần hàng; đã đề nghị = PR khác đã gửi/duyệt cộng các dòng của chính phiếu; vượt thì cảnh báo, không chặn.
/// </summary>
[Collection(ApiCollection.Name)]
public class UsageNormTests
{
    private readonly ProcApiFactory _factory;

    public UsageNormTests(ProcApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static int Year => TimeZoneInfo.ConvertTime(DateTime.UtcNow, VietnamTime.Zone).Year + 1;

    private static DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTime.UtcNow, VietnamTime.Zone));

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(Ct)}");
        return (await response.Content.ReadFromJsonAsync<T>(Ct))!;
    }

    [Fact]
    public async Task PrVuotDinhMuc_CanhBaoTheoKeHoachNhuCauLuyKe_KhongChanGui()
    {
        var userName = $"dm.{Guid.NewGuid():N}"[..16];
        await _factory.CreateUserAsync(userName, ProcApiFactory.TestPassword, ["TCT", "BHA"]);
        var bha = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, "BHA");
        var tct = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, "TCT");
        var uoms = (await bha.GetFromJsonAsync<List<UomDto>>("/api/uoms", Ct))!;
        var cai = uoms.Single(u => u.Code == "CAI").Id;
        var thung = uoms.Single(u => u.Code == "THUNG").Id;
        var parent = await ReadAsync<ItemCategoryDetailDto>(await tct.PostAsJsonAsync("/api/item-categories",
            new CreateItemCategoryRequest(TestCodes.Category(), $"Nhóm định mức {Guid.NewGuid():N}", null, null), Ct));
        var child = await ReadAsync<ItemCategoryDetailDto>(await tct.PostAsJsonAsync("/api/item-categories",
            new CreateItemCategoryRequest(TestCodes.Category(), $"Nhóm con định mức {Guid.NewGuid():N}", parent.Id, null), Ct));
        var noPlan = await ReadAsync<ItemCategoryDetailDto>(await tct.PostAsJsonAsync("/api/item-categories",
            new CreateItemCategoryRequest(TestCodes.Category(), $"Nhóm chưa kế hoạch {Guid.NewGuid():N}", null, null), Ct));
        async Task<Guid> ItemAsync(Guid categoryId, string name, params ItemUomConversionInput[] conversions) => (await ReadAsync<ItemDetailDto>(
            await tct.PostAsJsonAsync("/api/items", new CreateItemRequest(null, name, null, "Inventory", categoryId, cai, null, null, null, null, "10",
                null, false, false, null, null, conversions), Ct))).Id;
        var planned = await ItemAsync(child.Id, "Găng tay định mức", new ItemUomConversionInput(thung, 10));
        var unplanned = await ItemAsync(child.Id, "Khẩu trang ngoài kế hoạch");
        var unchecked_ = await ItemAsync(noPlan.Id, "Ống thép chưa kế hoạch");

        // Kế hoạch BHA (phiếu nhóm cha, mặt hàng thuộc nhóm con): 10 / 10 / 10 cái tháng 1–3; phiếu TCT không tính cho BHA.
        var months = new decimal[12];
        (months[0], months[1], months[2]) = (10, 10, 10);
        var plan = await ReadAsync<DemandPlanDto>(await bha.PostAsJsonAsync("/api/demand-plans",
            new SaveDemandPlanRequest(Year, parent.Id, null, [new(null, planned, cai, months, null)]), Ct));
        await ReadAsync<DemandPlanDto>(await bha.PostAsync($"/api/demand-plans/{plan.Id}/confirm", null, Ct));
        var tctPlan = await ReadAsync<DemandPlanDto>(await tct.PostAsJsonAsync("/api/demand-plans",
            new SaveDemandPlanRequest(Year, parent.Id, null, [new(null, planned, cai, months.Select(m => m * 100).ToArray(), null)]), Ct));
        await ReadAsync<DemandPlanDto>(await tct.PostAsync($"/api/demand-plans/{tctPlan.Id}/confirm", null, Ct));

        var department = await TestDepartments.CreateAsync(bha);
        SavePurchaseRequestRequest Pr(params SavePurchaseRequestLineRequest[] lines) =>
            new(Today, department, "Định mức", null, null, "Normal", null, lines);
        var feb = new DateOnly(Year, 2, 15);

        // PR1: 1,5 thùng = 15 cái tháng 2 – định mức lũy kế đến tháng 2 là 20 cái: trong định mức. Gửi duyệt.
        var pr1 = await ReadAsync<PurchaseRequestDto>(await bha.PostAsJsonAsync("/api/purchase-requests", Pr(new SavePurchaseRequestLineRequest(null, planned, thung, 1.5m, null, feb, null)), Ct));
        var line1 = Assert.Single(pr1.Lines);
        Assert.Equal(("Ok", 20m, 15m, new DateOnly(Year, 2, DateTime.DaysInMonth(Year, 2))), (line1.NormStatus, line1.NormQty, line1.NormRequestedQty, line1.NormThrough));
        await ReadAsync<PurchaseRequestDto>(await bha.PostAsync($"/api/purchase-requests/{pr1.Id}/submit", null, Ct));

        // PR2 nháp: 8 cái tháng 2 → 15 + 8 = 23 > 20 vượt; 5 cái tháng 3 → 28 ≤ 30; mặt hàng không có trong kế hoạch của nhóm đã lập
        // kế hoạch → ngoài kế hoạch; nhóm hàng chưa có kế hoạch đã chốt → không kiểm tra.
        var pr2 = await ReadAsync<PurchaseRequestDto>(await bha.PostAsJsonAsync("/api/purchase-requests", Pr(
            new(null, planned, cai, 8, null, new DateOnly(Year, 2, 20), null),
            new(null, planned, cai, 5, null, new DateOnly(Year, 3, 10), null),
            new(null, unplanned, cai, 1, null, feb, null),
            new(null, unchecked_, cai, 1, null, feb, null)), Ct));
        Assert.Equal(("Over", 20m, 23m), (pr2.Lines[0].NormStatus, pr2.Lines[0].NormQty, pr2.Lines[0].NormRequestedQty));
        Assert.Equal(("Ok", 30m, 28m), (pr2.Lines[1].NormStatus, pr2.Lines[1].NormQty, pr2.Lines[1].NormRequestedQty));
        Assert.Equal(("NoPlan", 0m), (pr2.Lines[2].NormStatus, pr2.Lines[2].NormQty));
        Assert.Null(pr2.Lines[3].NormStatus);

        // Chỉ cảnh báo: gửi duyệt được.
        var submitted = await ReadAsync<PurchaseRequestDto>(await bha.PostAsync($"/api/purchase-requests/{pr2.Id}/submit", null, Ct));
        Assert.Equal("Submitted", submitted.Status);

        // Hủy PR1: không còn tính vào đã đề nghị → PR2 dòng 1 về trong định mức (8 ≤ 20).
        await ReadAsync<PurchaseRequestDto>(await bha.PostAsJsonAsync($"/api/purchase-requests/{pr1.Id}/cancel", new CancelDocumentRequest("Thử hủy"), Ct));
        var after = await ReadAsync<PurchaseRequestDto>(await bha.GetAsync($"/api/purchase-requests/{pr2.Id}", Ct));
        Assert.Equal(("Ok", 8m), (after.Lines[0].NormStatus, after.Lines[0].NormRequestedQty));
    }
}
