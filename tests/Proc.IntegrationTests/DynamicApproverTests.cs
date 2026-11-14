using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Proc.Application.Common;
using Proc.Application.MasterData;
using Proc.Application.Organization;
using Proc.Application.Purchasing;

namespace Proc.IntegrationTests;

/// <summary>
/// Người duyệt động Trưởng phòng ban / GĐ khối của phiếu (lát O1, design.md mục 19.3): cấu hình ở APRL, chỉ đúng người của bộ phận trên phiếu
/// duyệt, đổi người khi phiếu đang chờ thì người mới duyệt, chặn gửi khi chưa khai báo đủ. Mỗi test một công ty mới.
/// </summary>
[Collection(ApiCollection.Name)]
public class DynamicApproverTests
{
    private readonly ProcApiFactory _factory;

    public DynamicApproverTests(ProcApiFactory factory) => _factory = factory;

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

    private sealed record Fixture(
        HttpClient Buyer, string CompanyCode, Guid Kt, Guid ItemId, Guid Cai, Member Head, Member Director, Member Other, DivisionDto Division);

    /// <summary>Công ty mới (người mua là quản trị), một mặt hàng, ba tài khoản quản trị tại công ty, khối có GĐ khối.</summary>
    private async Task<Fixture> ArrangeAsync()
    {
        var userName = $"dd.{Guid.NewGuid():N}"[..16];
        await _factory.CreateUserAsync(userName, ProcApiFactory.TestPassword, ["TCT"]);
        var head = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, "TCT");
        var company = await ReadAsync<CompanyDetailDto>(await head.PostAsJsonAsync("/api/companies",
            new CreateCompanyRequest(UniqueCode("D"), "Công ty thử duyệt động", null, null, null, null, new DateOnly(Today.Year, Today.Month, 1), "KT", "Kho tổng"), Ct));
        var buyer = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, company.Code);
        var kt = (await buyer.GetFromJsonAsync<List<WarehouseDetailDto>>("/api/warehouses", Ct))!.Single().Id;
        var cai = (await head.GetFromJsonAsync<List<UomDto>>("/api/uoms", Ct))!.Single(u => u.Code == "CAI").Id;
        var category = await ReadAsync<ItemCategoryDetailDto>(await TestCategories.PostLeafAsync(head,
            new CreateItemCategoryRequest(TestCodes.Category(), $"Nhóm thử duyệt động {Guid.NewGuid():N}", null, null), Ct));
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
        var other = await MemberAsync("kh");
        var division = await ReadAsync<DivisionDto>(await buyer.PostAsJsonAsync("/api/divisions", new CreateDivisionRequest("SX", "Khối sản xuất", director.Id), Ct));
        return new Fixture(buyer, company.Code, kt, item.Id, cai, manager, director, other, division);
    }

    private static async Task<DepartmentDto> DepartmentAsync(Fixture f, string code, Guid? divisionId, Guid? managerId) =>
        await ReadAsync<DepartmentDto>(await f.Buyer.PostAsJsonAsync("/api/departments",
            new CreateDepartmentRequest(code, $"Bộ phận {code}", null, divisionId, managerId), Ct));

    private static async Task<PurchaseRequestDto> PrAsync(Fixture f, HttpClient client, Guid departmentId) =>
        await ReadAsync<PurchaseRequestDto>(await client.PostAsJsonAsync("/api/purchase-requests",
            new SavePurchaseRequestRequest(Today, departmentId, "Bảo trì", Today.AddDays(7), f.Kt, "Normal", null, [new(null, f.ItemId, f.Cai, 2, null, null, null)]), Ct));

    private static Task<HttpResponseMessage> ActAsync(HttpClient client, Guid prId, string action, string? comment = null) =>
        client.PostAsJsonAsync($"/api/purchase-requests/{prId}/{action}", new ApprovalActionRequest(comment), Ct);

    /// <summary>PR: cấp 1 Trưởng phòng ban, cấp 2 GĐ khối của phiếu.</summary>
    private static async Task ConfigureAsync(Fixture f)
    {
        var level1 = await ReadAsync<ApprovalRuleDto>(await f.Buyer.PostAsJsonAsync("/api/approval-rules",
            new SaveApprovalRuleRequest("PR", 1, 0, null, "DepartmentHead"), Ct));
        Assert.Equal((null, "Trưởng phòng ban của phiếu", "DepartmentHead"), (level1.RoleId, level1.RoleName, level1.ApproverKind));
        await ReadAsync<ApprovalRuleDto>(await f.Buyer.PostAsJsonAsync("/api/approval-rules",
            new SaveApprovalRuleRequest("PR", 2, 0, null, "DivisionDirector"), Ct));
    }

    [Fact]
    public async Task TruongPhongBan_RoiGdKhoi_ChiDungNguoiCuaBoPhan_DoiNguoiKhiDangCho()
    {
        var f = await ArrangeAsync();
        await ConfigureAsync(f);
        // Loại phiếu không có bộ phận chỉ chọn được người duyệt theo vai trò.
        var noDepartment = await f.Buyer.PostAsJsonAsync("/api/approval-rules", new SaveApprovalRuleRequest("PRL", 1, 0, null, "DivisionDirector"), Ct);
        Assert.Equal("Loại PRL không có bộ phận trên phiếu – chỉ chọn được người duyệt theo vai trò.", await TitleAsync(noDepartment));

        var department = await DepartmentAsync(f, "PX1", f.Division.Id, f.Head.Id);
        var pr = await PrAsync(f, f.Buyer, department.Id);
        pr = await ReadAsync<PurchaseRequestDto>(await f.Buyer.PostAsync($"/api/purchase-requests/{pr.Id}/submit", null, Ct));
        Assert.Equal("Submitted", pr.Status);
        Assert.Equal(["Trưởng phòng ban PX1", "GĐ khối SX"], pr.Approvals.Select(a => a.RoleName));
        Assert.Single(pr.Approvals[0].NextApprovers!);

        // Người có quyền duyệt nhưng không phải Trưởng phòng ban của bộ phận thì không duyệt được cấp 1.
        var wrong = await ActAsync(f.Other.Client, pr.Id, "approve");
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
        Assert.StartsWith("Cấp 1 do Trưởng phòng ban PX1 (", await TitleAsync(wrong));
        Assert.Equal(HttpStatusCode.BadRequest, (await ActAsync(f.Director.Client, pr.Id, "approve")).StatusCode);

        // Đổi Trưởng phòng ban khi phiếu đang chờ: người mới duyệt, người cũ hết quyền ở cấp này.
        await ReadAsync<DepartmentDto>(await f.Buyer.PutAsJsonAsync($"/api/departments/{department.Id}",
            new UpdateDepartmentRequest(department.Name, null, true, f.Division.Id, f.Other.Id), Ct));
        Assert.Equal(HttpStatusCode.BadRequest, (await ActAsync(f.Head.Client, pr.Id, "approve")).StatusCode);
        await ReadAsync<PurchaseRequestDto>(await ActAsync(f.Other.Client, pr.Id, "approve"));

        // Việc cần làm của GĐ khối có phiếu; người khác thì không.
        Assert.Contains((await f.Director.Client.GetFromJsonAsync<List<PendingApprovalDto>>("/api/approvals/pending", Ct))!, p => p.DocumentId == pr.Id);
        Assert.DoesNotContain((await f.Head.Client.GetFromJsonAsync<List<PendingApprovalDto>>("/api/approvals/pending", Ct))!, p => p.DocumentId == pr.Id);
        pr = await ReadAsync<PurchaseRequestDto>(await ActAsync(f.Director.Client, pr.Id, "approve"));
        Assert.Equal("Approved", pr.Status);
    }

    [Fact]
    public async Task ChuaKhaiDu_NguoiLapLaTruongPhong_TrungNguoi_ChanGuiDuyet()
    {
        var f = await ArrangeAsync();
        await ConfigureAsync(f);

        async Task<string?> SubmitBlockedAsync(HttpClient client, Guid departmentId)
        {
            var pr = await PrAsync(f, client, departmentId);
            var response = await client.PostAsync($"/api/purchase-requests/{pr.Id}/submit", null, Ct);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            // Bị chặn thì phiếu vẫn ở nháp.
            Assert.Equal("Draft", (await client.GetFromJsonAsync<PurchaseRequestDto>($"/api/purchase-requests/{pr.Id}", Ct))!.Status);
            return await TitleAsync(response);
        }

        var noManager = await DepartmentAsync(f, "PX2", f.Division.Id, null);
        Assert.Equal("Không gửi duyệt được – cấp 1 (Trưởng phòng ban PX2): Bộ phận PX2 chưa khai Trưởng phòng ban (Hệ thống → Bộ phận).",
            await SubmitBlockedAsync(f.Buyer, noManager.Id));

        var noDivision = await DepartmentAsync(f, "PX3", null, f.Head.Id);
        Assert.Equal("Không gửi duyệt được – cấp 2 (GĐ khối (bộ phận PX3)): Bộ phận PX3 chưa gán khối (Hệ thống → Bộ phận).",
            await SubmitBlockedAsync(f.Buyer, noDivision.Id));

        // Trưởng phòng ban tự lập phiếu của bộ phận mình: không tự duyệt được cấp của mình.
        var own = await DepartmentAsync(f, "PX4", f.Division.Id, f.Head.Id);
        Assert.Contains("đó là người lập phiếu", await SubmitBlockedAsync(f.Head.Client, own.Id));

        // Trưởng phòng ban cũng là GĐ khối: mỗi người chỉ duyệt một cấp trong lượt (lát R3) nên chặn ngay khi gửi.
        var same = await DepartmentAsync(f, "PX5", f.Division.Id, f.Director.Id);
        Assert.Contains("vừa là Trưởng phòng ban PX5 vừa là GĐ khối SX", await SubmitBlockedAsync(f.Buyer, same.Id));
    }
}
