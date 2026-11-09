using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Proc.Application.MasterData;
using Proc.Application.Purchasing;
using Proc.Domain.Organization;

namespace Proc.IntegrationTests;

/// <summary>
/// Hồ sơ đánh giá NCC mới (lát G8, design.md mục 18.25; Q2): Phòng cung ứng lập, đánh giá → Văn phòng Ban đề xuất → Hội đồng duyệt qua yêu
/// cầu duyệt NCC; NCC tạo mới không lên Đã duyệt trên form NCC khi tổng công ty bắt buộc hồ sơ.
/// </summary>
[Collection(ApiCollection.Name)]
public class SupplierOnboardingTests
{
    private readonly ProcApiFactory _factory;

    public SupplierOnboardingTests(ProcApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));

    private static string UniqueCode(string prefix) => $"{prefix}{Guid.NewGuid():N}".ToUpperInvariant()[..8];

    private static async Task<string?> TitleAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(Ct))?.Title;

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(Ct)}");
        return (await response.Content.ReadFromJsonAsync<T>(Ct))!;
    }

    /// <summary>Người dùng ở tổng công ty với vai trò riêng của test.</summary>
    private async Task<HttpClient> UserAsync(params string[] permissions)
    {
        var roleCode = UniqueCode("R");
        await _factory.CreateRoleAsync(roleCode, [Permissions.MasterData.SupplierView, Permissions.MasterData.ItemCategoryView, .. permissions]);
        var userName = $"hs.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(userName, "TCT", roleCode);
        return await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, "TCT");
    }

    private static async Task<SupplierDetailDto> NewSupplierAsync(HttpClient head, string status = "Potential")
    {
        var groupId = (await head.GetFromJsonAsync<List<SupplierGroupDto>>("/api/supplier-groups", Ct))!.Single(g => g.Code == "TN").Id;
        return await ReadAsync<SupplierDetailDto>(await head.PostAsJsonAsync("/api/suppliers",
            new SaveSupplierRequest(null, groupId, $"NCC mới {Guid.NewGuid():N}"[..20], null, false, null, null, null, null, null, null, null, null,
                status, null, null, [], []), Ct));
    }

    private static SaveSupplierOnboardingRequest Save(
        Guid supplierId, Guid categoryId, string? result = "Pass", string? comment = null, bool license = true, int?[]? scores = null)
    {
        var s = scores ?? [4, 5, 4, 3, 4, 3];
        return new SaveSupplierOnboardingRequest(supplierId, categoryId, Today, "Hội chợ nông sản",
            license ? [new("LICENSE", true, Today.AddYears(2), "0312345678"), new("SAMPLE", true, null, "Mẫu đạt")] : [],
            s[0], s[1], s[2], s[3], s[4], s[5], result, comment, "Ghi chú hồ sơ");
    }

    private static Task<HttpResponseMessage> PostAsync(HttpClient client, string url, object? body = null) =>
        body is null ? client.PostAsync(url, null, Ct) : client.PostAsJsonAsync(url, body, Ct);

    [Fact]
    public async Task HoSoNccMoi_DanhGia_DeXuat_RutLai_HoiDongDuyet_NccDaDuyetCoNganhHang()
    {
        await _factory.AllowSupplierWithoutOnboardingAsync(false);
        try
        {
            var admin = await _factory.CreateClientInCompanyAsync(ProcApiFactory.AdminUserName, ProcApiFactory.AdminPassword, "TCT");
            var evaluator = await UserAsync(Permissions.MasterData.SupplierOnboardingManage, Permissions.MasterData.SupplierManage);
            var proposer = await UserAsync(Permissions.MasterData.SupplierOnboardingPropose);
            var approver = await UserAsync(Permissions.MasterData.SupplierApprove);
            var viewer = await UserAsync(Permissions.MasterData.SupplierOnboardingView);
            var category = await ReadAsync<ItemCategoryDetailDto>(await admin.PostAsJsonAsync("/api/item-categories",
                new CreateItemCategoryRequest(null, $"Rau củ {Guid.NewGuid():N}", null, null), Ct));
            var supplier = await NewSupplierAsync(evaluator);

            // NCC mới không lên Đã duyệt trên form NCC (kể cả khi tạo) – phải qua hồ sơ HSN.
            var blocked = await evaluator.PutAsJsonAsync($"/api/suppliers/{supplier.Id}", new SaveSupplierRequest(null, supplier.GroupId, supplier.Name,
                null, false, null, null, null, null, null, null, null, null, "Approved", null, null, [], []), Ct);
            Assert.Equal(HttpStatusCode.BadRequest, blocked.StatusCode);
            Assert.Contains("hồ sơ đánh giá NCC mới", await TitleAsync(blocked));
            Assert.Contains("hồ sơ đánh giá NCC mới", await TitleAsync(await admin.PostAsJsonAsync("/api/suppliers",
                new SaveSupplierRequest(null, supplier.GroupId, "NCC tạo thẳng Đã duyệt", null, false, null, null, null, null, null, null, null, null,
                    "Approved", null, null, [], []), Ct)));

            // Không có quyền lập → 403; Phòng cung ứng lập hồ sơ, mỗi NCC một hồ sơ mở.
            Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsJsonAsync("/api/supplier-onboardings", Save(supplier.Id, category.Id), Ct)).StatusCode);
            var created = await ReadAsync<SupplierOnboardingDto>(await evaluator.PostAsJsonAsync("/api/supplier-onboardings",
                Save(supplier.Id, category.Id, license: false), Ct));
            Assert.StartsWith("HSN-", created.No);
            Assert.Equal(("Draft", 5, "Ghi chú hồ sơ"), (created.Status, created.Documents.Count, created.Note));
            Assert.True(created.Actions.CanEdit);
            Assert.Contains("chưa kết thúc", await TitleAsync(await evaluator.PostAsJsonAsync("/api/supplier-onboardings", Save(supplier.Id, category.Id), Ct)));

            // Kết luận Đạt cần giấy phép kinh doanh; đủ thì hoàn thành, điểm bình quân 23 ÷ 6 = 3,83.
            Assert.Contains("giấy phép kinh doanh", await TitleAsync(await PostAsync(evaluator, $"/api/supplier-onboardings/{created.Id}/evaluate",
                Save(supplier.Id, category.Id, license: false))));
            var evaluated = await ReadAsync<SupplierOnboardingDto>(await PostAsync(evaluator, $"/api/supplier-onboardings/{created.Id}/evaluate",
                Save(supplier.Id, category.Id)));
            Assert.Equal(("Evaluated", 3.83m, true), (evaluated.Status, evaluated.Score, evaluated.Documents.Single(d => d.Code == "LICENSE").Provided));
            Assert.False(evaluated.Actions.CanEdit);

            // Người có cả quyền đề xuất nhưng là người đánh giá thì không tự đề xuất.
            var both = await UserAsync(Permissions.MasterData.SupplierOnboardingManage, Permissions.MasterData.SupplierOnboardingPropose);
            var other = await NewSupplierAsync(evaluator);
            var own = await ReadAsync<SupplierOnboardingDto>(await both.PostAsJsonAsync("/api/supplier-onboardings", Save(other.Id, category.Id), Ct));
            own = await ReadAsync<SupplierOnboardingDto>(await PostAsync(both, $"/api/supplier-onboardings/{own.Id}/evaluate", Save(other.Id, category.Id)));
            Assert.False(own.Actions.CanPropose);
            Assert.Contains("không tự đề xuất", await TitleAsync(await PostAsync(both, $"/api/supplier-onboardings/{own.Id}/propose",
                new ProposeSupplierOnboardingRequest(true, null))));

            // Văn phòng Ban đề xuất → lập yêu cầu duyệt NCC; rút lại → về Đã đánh giá; đề xuất lại.
            var proposed = await ReadAsync<SupplierOnboardingDto>(await PostAsync(proposer, $"/api/supplier-onboardings/{created.Id}/propose",
                new ProposeSupplierOnboardingRequest(true, "Giá tốt, mẫu đạt")));
            Assert.Equal(("Submitted", true, true), (proposed.Status, proposed.Recommended, proposed.Actions.CanWithdraw));
            Assert.StartsWith(supplier.Code, proposed.ChangeRequestNo);
            var change = await ReadAsync<SupplierChangeRequestDto>(await admin.GetAsync($"/api/supplier-change-requests/{proposed.ChangeRequestId}", Ct));
            Assert.Equal(("Submitted", "Đã duyệt – Theo hồ sơ đánh giá NCC mới " + created.No), (change.Status, change.ProposedSummary));
            var withdrawn = await ReadAsync<SupplierOnboardingDto>(await PostAsync(proposer, $"/api/supplier-onboardings/{created.Id}/withdraw"));
            Assert.Equal(("Evaluated", (bool?)null, (Guid?)null), (withdrawn.Status, withdrawn.Recommended, withdrawn.ChangeRequestId));
            Assert.Equal("Cancelled", (await ReadAsync<SupplierChangeRequestDto>(await admin.GetAsync($"/api/supplier-change-requests/{change.Id}", Ct))).Status);
            proposed = await ReadAsync<SupplierOnboardingDto>(await PostAsync(proposer, $"/api/supplier-onboardings/{created.Id}/propose",
                new ProposeSupplierOnboardingRequest(true, null)));
            Assert.Equal("Submitted", proposed.Status);

            // Hội đồng duyệt yêu cầu (chỉ chặng tổng công ty – NCC chưa áp dụng ở công ty nào) → NCC Đã duyệt, có ngành hàng là nhóm đề xuất.
            await ReadAsync<SupplierChangeRequestDto>(await approver.PostAsJsonAsync(
                $"/api/supplier-change-requests/{proposed.ChangeRequestId}/approve", new ApprovalActionRequest("Hội đồng đồng ý"), Ct));
            var done = await ReadAsync<SupplierOnboardingDto>(await viewer.GetAsync($"/api/supplier-onboardings/{created.Id}", Ct));
            Assert.Equal(("Approved", "Approved"), (done.Status, done.SupplierStatus));
            Assert.NotNull(done.ApprovedAt);
            var approved = await ReadAsync<SupplierDetailDto>(await admin.GetAsync($"/api/suppliers/{supplier.Id}", Ct));
            Assert.Contains(approved.Categories, c => c.Id == category.Id);
            Assert.Contains((await ReadAsync<List<SupplierOnboardingListItemDto>>(await viewer.GetAsync("/api/supplier-onboardings?status=Approved", Ct))),
                r => r.Id == created.Id);

            // NCC đã duyệt thì không lập hồ sơ mới.
            Assert.Contains("chỉ lập cho NCC Tiềm năng", await TitleAsync(await evaluator.PostAsJsonAsync("/api/supplier-onboardings", Save(supplier.Id, category.Id), Ct)));
        }
        finally
        {
            await _factory.AllowSupplierWithoutOnboardingAsync(true);
        }
    }

    [Fact]
    public async Task HoSoNccMoi_KhongDeXuat_TuChoi_Huy_LapLaiDuoc()
    {
        var admin = await _factory.CreateClientInCompanyAsync(ProcApiFactory.AdminUserName, ProcApiFactory.AdminPassword, "TCT");
        var evaluator = await UserAsync(Permissions.MasterData.SupplierOnboardingManage, Permissions.MasterData.SupplierManage);
        var proposer = await UserAsync(Permissions.MasterData.SupplierOnboardingPropose);
        var approver = await UserAsync(Permissions.MasterData.SupplierApprove);
        var category = await ReadAsync<ItemCategoryDetailDto>(await admin.PostAsJsonAsync("/api/item-categories",
            new CreateItemCategoryRequest(null, $"Gia vị {Guid.NewGuid():N}", null, null), Ct));

        // Kết luận Không đạt: Văn phòng Ban chỉ ghi được "Không đề xuất" (bắt buộc lý do) – hồ sơ kết thúc, lập hồ sơ mới được.
        var weak = await NewSupplierAsync(evaluator);
        var fail = await ReadAsync<SupplierOnboardingDto>(await evaluator.PostAsJsonAsync("/api/supplier-onboardings",
            Save(weak.Id, category.Id, "Fail", "Năng lực cung ứng thấp", scores: [3, 2, 2, 3, 3, 3]), Ct));
        fail = await ReadAsync<SupplierOnboardingDto>(await PostAsync(evaluator, $"/api/supplier-onboardings/{fail.Id}/evaluate",
            Save(weak.Id, category.Id, "Fail", "Năng lực cung ứng thấp", scores: [3, 2, 2, 3, 3, 3])));
        Assert.Contains("Không đạt", await TitleAsync(await PostAsync(proposer, $"/api/supplier-onboardings/{fail.Id}/propose",
            new ProposeSupplierOnboardingRequest(true, null))));
        Assert.Equal(HttpStatusCode.BadRequest, (await PostAsync(proposer, $"/api/supplier-onboardings/{fail.Id}/propose",
            new ProposeSupplierOnboardingRequest(false, null))).StatusCode);
        var notRecommended = await ReadAsync<SupplierOnboardingDto>(await PostAsync(proposer, $"/api/supplier-onboardings/{fail.Id}/propose",
            new ProposeSupplierOnboardingRequest(false, "Đã đủ 3 NCC tốt hơn")));
        Assert.Equal(("NotRecommended", false, (Guid?)null), (notRecommended.Status, notRecommended.Recommended, notRecommended.ChangeRequestId));
        var again = await ReadAsync<SupplierOnboardingDto>(await evaluator.PostAsJsonAsync("/api/supplier-onboardings", Save(weak.Id, category.Id), Ct));

        // Hủy hồ sơ Nháp (bắt buộc lý do); Đã đánh giá mở lại về Nháp được.
        Assert.Equal(HttpStatusCode.BadRequest, (await PostAsync(evaluator, $"/api/supplier-onboardings/{again.Id}/cancel",
            new CancelSupplierOnboardingRequest(""))).StatusCode);
        Assert.Equal("Cancelled", (await ReadAsync<SupplierOnboardingDto>(await PostAsync(evaluator, $"/api/supplier-onboardings/{again.Id}/cancel",
            new CancelSupplierOnboardingRequest("NCC xin rút")))).Status);

        // Hội đồng từ chối → hồ sơ Từ chối, NCC vẫn Tiềm năng.
        var supplier = await NewSupplierAsync(evaluator);
        var onboarding = await ReadAsync<SupplierOnboardingDto>(await evaluator.PostAsJsonAsync("/api/supplier-onboardings", Save(supplier.Id, category.Id), Ct));
        onboarding = await ReadAsync<SupplierOnboardingDto>(await PostAsync(evaluator, $"/api/supplier-onboardings/{onboarding.Id}/evaluate", Save(supplier.Id, category.Id)));
        onboarding = await ReadAsync<SupplierOnboardingDto>(await PostAsync(evaluator, $"/api/supplier-onboardings/{onboarding.Id}/reopen"));
        Assert.Equal(("Draft", (decimal?)null), (onboarding.Status, onboarding.Score));
        onboarding = await ReadAsync<SupplierOnboardingDto>(await PostAsync(evaluator, $"/api/supplier-onboardings/{onboarding.Id}/evaluate", Save(supplier.Id, category.Id)));
        onboarding = await ReadAsync<SupplierOnboardingDto>(await PostAsync(proposer, $"/api/supplier-onboardings/{onboarding.Id}/propose",
            new ProposeSupplierOnboardingRequest(true, null)));
        Assert.Contains("rút", await TitleAsync(await PostAsync(evaluator, $"/api/supplier-onboardings/{onboarding.Id}/cancel",
            new CancelSupplierOnboardingRequest("Nhầm"))));
        await ReadAsync<SupplierChangeRequestDto>(await approver.PostAsJsonAsync(
            $"/api/supplier-change-requests/{onboarding.ChangeRequestId}/reject", new ApprovalActionRequest("Chưa đủ năng lực tài chính"), Ct));
        var rejected = await ReadAsync<SupplierOnboardingDto>(await evaluator.GetAsync($"/api/supplier-onboardings/{onboarding.Id}", Ct));
        Assert.Equal(("Rejected", "Chưa đủ năng lực tài chính", "Potential"), (rejected.Status, rejected.RejectReason, rejected.SupplierStatus));
    }
}
