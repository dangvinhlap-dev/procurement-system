using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Proc.Application.Common;
using Proc.Application.MasterData;
using Proc.Application.Notifications;
using Proc.Application.Organization;
using Proc.Application.Purchasing;

namespace Proc.IntegrationTests;

/// <summary>
/// Ủy quyền duyệt có thời hạn (lát O2, design.md mục 19.3): người được ủy quyền duyệt thay ở cấp người ủy quyền đang giữ, lịch sử ghi "duyệt
/// thay", người ủy quyền nhận thông báo; Trưởng phòng ban tự lập phiếu gửi được khi đang ủy quyền; thu hồi thì hết duyệt thay. Mỗi test một
/// công ty mới.
/// </summary>
[Collection(ApiCollection.Name)]
public class ApprovalDelegationTests
{
    private readonly ProcApiFactory _factory;

    public ApprovalDelegationTests(ProcApiFactory factory) => _factory = factory;

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

    /// <summary>Người dùng thử: Id + client đã vào công ty.</summary>
    private sealed record Member(Guid Id, HttpClient Client);

    private sealed record Fixture(HttpClient Buyer, Guid Kt, Guid ItemId, Guid Cai, Member Head, Member Director, Member Deputy, DepartmentDto Department);

    /// <summary>
    /// Công ty mới (người mua là quản trị), một mặt hàng, ba tài khoản quản trị (Trưởng phòng ban, GĐ khối, phó phòng); bộ phận PX1 thuộc khối SX;
    /// PR duyệt cấp 1 Trưởng phòng ban, cấp 2 GĐ khối.
    /// </summary>
    private async Task<Fixture> ArrangeAsync()
    {
        var userName = $"uq.{Guid.NewGuid():N}"[..16];
        await _factory.CreateUserAsync(userName, ProcApiFactory.TestPassword, ["TCT"]);
        var head = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, "TCT");
        var company = await ReadAsync<CompanyDetailDto>(await head.PostAsJsonAsync("/api/companies",
            new CreateCompanyRequest(UniqueCode("U"), "Công ty thử ủy quyền", null, null, null, null, new DateOnly(Today.Year, Today.Month, 1), "KT", "Kho tổng"), Ct));
        var buyer = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, company.Code);
        var kt = (await buyer.GetFromJsonAsync<List<WarehouseDetailDto>>("/api/warehouses", Ct))!.Single().Id;
        var cai = (await head.GetFromJsonAsync<List<UomDto>>("/api/uoms", Ct))!.Single(u => u.Code == "CAI").Id;
        var category = await ReadAsync<ItemCategoryDetailDto>(await head.PostAsJsonAsync("/api/item-categories",
            new CreateItemCategoryRequest(TestCodes.Category(), $"Nhóm thử ủy quyền {Guid.NewGuid():N}", null, null), Ct));
        var item = await ReadAsync<ItemDetailDto>(await head.PostAsJsonAsync("/api/items",
            new CreateItemRequest(null, "Bulong", null, "Inventory", category.Id, cai, null, null, null, null, "10", null, false, false, null, null, []), Ct));

        async Task<Member> MemberAsync(string prefix)
        {
            var name = $"{prefix}.{Guid.NewGuid():N}"[..16];
            var id = await _factory.CreateUserAsync(name, ProcApiFactory.TestPassword, [company.Code]);
            return new(id, await _factory.CreateClientInCompanyAsync(name, ProcApiFactory.TestPassword, company.Code));
        }

        var manager = await MemberAsync("tp");
        var director = await MemberAsync("gd");
        var deputy = await MemberAsync("pp");
        var division = await ReadAsync<DivisionDto>(await buyer.PostAsJsonAsync("/api/divisions", new CreateDivisionRequest("SX", "Khối sản xuất", director.Id), Ct));
        var department = await ReadAsync<DepartmentDto>(await buyer.PostAsJsonAsync("/api/departments",
            new CreateDepartmentRequest("PX1", "Phân xưởng 1", null, division.Id, manager.Id), Ct));
        await ReadAsync<ApprovalRuleDto>(await buyer.PostAsJsonAsync("/api/approval-rules", new SaveApprovalRuleRequest("PR", 1, 0, null, "DepartmentHead"), Ct));
        await ReadAsync<ApprovalRuleDto>(await buyer.PostAsJsonAsync("/api/approval-rules", new SaveApprovalRuleRequest("PR", 2, 0, null, "DivisionDirector"), Ct));
        return new Fixture(buyer, kt, item.Id, cai, manager, director, deputy, department);
    }

    private static async Task<PurchaseRequestDto> SubmittedPrAsync(Fixture f, HttpClient client)
    {
        var pr = await ReadAsync<PurchaseRequestDto>(await client.PostAsJsonAsync("/api/purchase-requests",
            new SavePurchaseRequestRequest(Today, f.Department.Id, "Bảo trì", Today.AddDays(7), f.Kt, "Normal", null, [new(null, f.ItemId, f.Cai, 2, null, null, null)]), Ct));
        return await ReadAsync<PurchaseRequestDto>(await client.PostAsync($"/api/purchase-requests/{pr.Id}/submit", null, Ct));
    }

    private static Task<HttpResponseMessage> ActAsync(HttpClient client, Guid prId, string action, string? comment = null) =>
        client.PostAsJsonAsync($"/api/purchase-requests/{prId}/{action}", new ApprovalActionRequest(comment), Ct);

    private static Task<HttpResponseMessage> DelegateAsync(Member from, Guid to, string? docType, DateOnly validFrom, DateOnly validTo) =>
        from.Client.PostAsJsonAsync("/api/approval-delegations",
            new CreateApprovalDelegationRequest(null, to, docType, validFrom, validTo, "Đi công tác", "Liên hệ qua điện thoại"), Ct);

    [Fact]
    public async Task NguoiDuocUyQuyen_DuyetThay_GhiLichSu_BaoNguoiUyQuyen()
    {
        var f = await ArrangeAsync();

        // Quy tắc lập: không ủy quyền cho mình, không lùi ngày, không trùng.
        Assert.Equal("Không ủy quyền cho chính mình.", await TitleAsync(await DelegateAsync(f.Head, f.Head.Id, "PR", Today, Today.AddDays(3))));
        Assert.Equal("Ngày bắt đầu ủy quyền không được trước hôm nay.",
            await TitleAsync(await DelegateAsync(f.Head, f.Deputy.Id, "PR", Today.AddDays(-1), Today.AddDays(3))));
        Assert.Contains("tối đa 90 ngày", await TitleAsync(await DelegateAsync(f.Head, f.Deputy.Id, null, Today, Today.AddDays(90))));
        var delegation = await ReadAsync<ApprovalDelegationDto>(await DelegateAsync(f.Head, f.Deputy.Id, "PR", Today, Today.AddDays(5)));
        Assert.Equal(("Active", "Đề nghị mua hàng", true), (delegation.Status, delegation.DocTypeName, delegation.CanRevoke));
        Assert.Equal(HttpStatusCode.BadRequest, (await DelegateAsync(f.Head, f.Deputy.Id, null, Today.AddDays(2), Today.AddDays(9))).StatusCode);
        // Người được ủy quyền thấy ủy quyền mình nhận nhưng không tự thu hồi khi không có quyền quản lý – ở đây là quản trị nên thu hồi được;
        // phép thử quyền thu hồi nằm ở test giao diện. Danh sách của phó phòng có ủy quyền này.
        Assert.Contains((await f.Deputy.Client.GetFromJsonAsync<List<ApprovalDelegationDto>>("/api/approval-delegations", Ct))!, d => d.Id == delegation.Id);

        var pr = await SubmittedPrAsync(f, f.Buyer);
        Assert.Equal("Submitted", pr.Status);
        // Người duyệt tiếp theo gồm cả người được ủy quyền, ghi rõ thay ai.
        Assert.Contains(pr.Approvals[0].NextApprovers!, n => n.Contains("(thay Người thử tp."));
        Assert.Contains((await f.Deputy.Client.GetFromJsonAsync<List<PendingApprovalDto>>("/api/approvals/pending", Ct))!, p => p.DocumentId == pr.Id);
        var waiting = (await f.Deputy.Client.GetFromJsonAsync<List<NotificationDto>>("/api/notifications", Ct))!;
        Assert.Contains(waiting, n => n.TargetId == pr.Id && n.Title.Contains("chờ bạn duyệt thay"));

        pr = await ReadAsync<PurchaseRequestDto>(await ActAsync(f.Deputy.Client, pr.Id, "approve", "Đồng ý thay TP"));
        var level1 = pr.Approvals.Single(a => a.Level == 1);
        Assert.Equal("Approved", level1.Status);
        Assert.Matches(@"^Người thử pp\.\S+ \(thay Người thử tp\.\S+\)$", level1.ActedByName);
        // Người ủy quyền được báo; Trưởng phòng ban không duyệt thêm cấp nào (đã "duyệt" qua người thay – lát R3).
        Assert.Contains((await f.Head.Client.GetFromJsonAsync<List<NotificationDto>>("/api/notifications", Ct))!,
            n => n.TargetId == pr.Id && n.Type == "DelegatedAction");
        Assert.Equal(HttpStatusCode.BadRequest, (await ActAsync(f.Deputy.Client, pr.Id, "approve")).StatusCode);
        pr = await ReadAsync<PurchaseRequestDto>(await ActAsync(f.Director.Client, pr.Id, "approve"));
        Assert.Equal("Approved", pr.Status);
    }

    [Fact]
    public async Task TruongPhongTuLap_GuiDuocKhiUyQuyen_ThuHoiThiHetDuyetThay()
    {
        var f = await ArrangeAsync();

        // Trưởng phòng ban tự lập phiếu bộ phận mình: chưa ủy quyền thì chặn gửi, đã ủy quyền thì gửi được và người được ủy quyền duyệt cấp 1.
        var draft = await ReadAsync<PurchaseRequestDto>(await f.Head.Client.PostAsJsonAsync("/api/purchase-requests",
            new SavePurchaseRequestRequest(Today, f.Department.Id, "Bảo trì", Today.AddDays(7), f.Kt, "Normal", null, [new(null, f.ItemId, f.Cai, 2, null, null, null)]), Ct));
        var blocked = await f.Head.Client.PostAsync($"/api/purchase-requests/{draft.Id}/submit", null, Ct);
        Assert.Contains("lập ủy quyền duyệt", await TitleAsync(blocked));
        var delegation = await ReadAsync<ApprovalDelegationDto>(await DelegateAsync(f.Head, f.Deputy.Id, null, Today, Today.AddDays(5)));
        var own = await ReadAsync<PurchaseRequestDto>(await f.Head.Client.PostAsync($"/api/purchase-requests/{draft.Id}/submit", null, Ct));
        Assert.Equal("Submitted", own.Status);
        own = await ReadAsync<PurchaseRequestDto>(await ActAsync(f.Deputy.Client, own.Id, "approve"));
        Assert.Contains("(thay ", own.Approvals.Single(a => a.Level == 1).ActedByName);

        // Thu hồi: phiếu mới chờ cấp 1 thì phó phòng hết duyệt thay, Trưởng phòng ban tự duyệt.
        var pr = await SubmittedPrAsync(f, f.Buyer);
        var revoked = await ReadAsync<ApprovalDelegationDto>(await f.Head.Client.PostAsync($"/api/approval-delegations/{delegation.Id}/revoke", null, Ct));
        Assert.Equal(("Revoked", false), (revoked.Status, revoked.CanRevoke));
        Assert.Equal(HttpStatusCode.BadRequest, (await f.Head.Client.PostAsync($"/api/approval-delegations/{delegation.Id}/revoke", null, Ct)).StatusCode);
        var denied = await ActAsync(f.Deputy.Client, pr.Id, "approve");
        Assert.StartsWith("Cấp 1 do Trưởng phòng ban PX1 (", await TitleAsync(denied));
        Assert.DoesNotContain((await f.Deputy.Client.GetFromJsonAsync<List<PendingApprovalDto>>("/api/approvals/pending", Ct))!, p => p.DocumentId == pr.Id);
        await ReadAsync<PurchaseRequestDto>(await ActAsync(f.Head.Client, pr.Id, "approve"));

        // Ủy quyền chưa tới ngày hiệu lực chưa cho duyệt thay.
        var later = await ReadAsync<ApprovalDelegationDto>(await DelegateAsync(f.Head, f.Deputy.Id, "PR", Today.AddDays(1), Today.AddDays(2)));
        Assert.Equal("Upcoming", later.Status);
        var next = await SubmittedPrAsync(f, f.Buyer);
        Assert.Equal(HttpStatusCode.BadRequest, (await ActAsync(f.Deputy.Client, next.Id, "approve")).StatusCode);
    }
}
