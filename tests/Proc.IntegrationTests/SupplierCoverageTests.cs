using System.Net;
using System.Net.Http.Json;
using Proc.Application.Common;
using Proc.Application.MasterData;
using Proc.Application.Purchasing;
using Proc.Application.Reports;
using Proc.Domain.Organization;

namespace Proc.IntegrationTests;

/// <summary>
/// NCC đủ điều kiện theo nhóm hàng (lát G1 → G1b, design.md mục 18.25 – quyết định thành lập Ban cung ứng: tối thiểu 03 NCC; Q1 08/11/2026:
/// đếm theo nhóm hàng): NCC Đã duyệt có ngành hàng là nhóm/nhóm cha hoặc dòng NCC – sản phẩm của mặt hàng trong nhóm; nhóm mà mọi mặt hàng
/// có NCC độc quyền đã duyệt còn tác dụng thì miễn; lọc nhóm hàng và "chỉ thiếu"; cần quyền xem NCC.
/// </summary>
[Collection(ApiCollection.Name)]
public class SupplierCoverageTests
{
    private readonly ProcApiFactory _factory;

    public SupplierCoverageTests(ProcApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string UniqueCode(string prefix) => $"{prefix}{Guid.NewGuid():N}".ToUpperInvariant()[..8];

    private static DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTime.UtcNow, VietnamTime.Zone));

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(Ct)}");
        return (await response.Content.ReadFromJsonAsync<T>(Ct))!;
    }

    private async Task<HttpClient> UserWithAsync(params string[] permissions)
    {
        var roleCode = UniqueCode("V");
        await _factory.CreateRoleAsync(roleCode, permissions);
        var userName = $"cv.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(userName, "TCT", roleCode);
        return await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, "TCT");
    }

    /// <summary>NCC theo trạng thái, kèm ngành hàng (nếu có); Đã duyệt thì đi hết quy trình duyệt NCC.</summary>
    private async Task<Guid> SupplierAsync(HttpClient head, Guid groupId, string name, string status, params Guid[] categoryIds)
    {
        var supplier = await ReadAsync<SupplierDetailDto>(await head.PostAsJsonAsync("/api/suppliers",
            new SaveSupplierRequest(null, groupId, name, null, false, null, null, null, null, null, null, null, null, status, null, null, [], [],
                categoryIds), Ct));
        if (status == "Approved") await _factory.FinishSupplierApprovalAsync(supplier.Id);
        return supplier.Id;
    }

    private async Task LinkAsync(HttpClient head, Guid supplierId, Guid itemId, Guid uomId, bool preferred = false) =>
        await ReadAsync<SupplierItemDto>(await head.PostAsJsonAsync($"/api/suppliers/{supplierId}/items",
            new SaveSupplierItemRequest(itemId, null, uomId, null, null, preferred), Ct));

    [Fact]
    public async Task DemTheoNhomHang_NganhHangNhomCha_DocQuyenDuocMien_LocChiThieu_CanQuyenXemNcc()
    {
        var userName = $"cm.{Guid.NewGuid():N}"[..16];
        await _factory.CreateUserAsync(userName, ProcApiFactory.TestPassword, ["TCT"]);
        var head = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, "TCT");
        var cai = (await head.GetFromJsonAsync<List<UomDto>>("/api/uoms", Ct))!.Single(u => u.Code == "CAI").Id;
        async Task<ItemCategoryDetailDto> Category(string name, Guid? parentId) => await ReadAsync<ItemCategoryDetailDto>(await head.PostAsJsonAsync(
            "/api/item-categories", new CreateItemCategoryRequest(TestCodes.Category(), $"{name} {UniqueCode("")}", parentId, null), Ct));
        async Task<ItemDetailDto> Item(string name, Guid categoryId) => await ReadAsync<ItemDetailDto>(await head.PostAsJsonAsync("/api/items",
            new CreateItemRequest(null, name, null, "Inventory", categoryId, cai, null, null, null, null, "10", null, false, false, null, null, []), Ct));
        // Nhóm cha (không có mặt hàng) và ba nhóm con: đủ, thiếu, độc quyền.
        var parent = await Category("Nhóm đủ NCC", null);
        var enough = await Category("Gạo", parent.Id);
        var shortCategory = await Category("Dầu ăn", parent.Id);
        var soleCategory = await Category("Phụ tùng máy kem", parent.Id);
        var rice1 = await Item("Gạo tám thơm", enough.Id);
        var rice2 = await Item("Gạo nếp", enough.Id);
        var oil = await Item("Dầu ăn 5 lít", shortCategory.Id);
        var sole = await Item("Phụ tùng máy làm kem", soleCategory.Id);

        var groupId = (await head.GetFromJsonAsync<List<SupplierGroupDto>>("/api/supplier-groups", Ct))!.Single(g => g.Code == "TN").Id;
        var a = await SupplierAsync(head, groupId, "NCC gạo An", "Approved");
        var b = await SupplierAsync(head, groupId, "NCC gạo Bình", "Approved");
        // Ngành hàng nhóm cha tính cho mọi nhóm con; ngành hàng đúng nhóm con chỉ tính cho nhóm đó.
        var c = await SupplierAsync(head, groupId, "NCC tổng hợp Cường", "Approved", parent.Id);
        await SupplierAsync(head, groupId, "NCC dầu Dũng", "Approved", shortCategory.Id);
        var potential = await SupplierAsync(head, groupId, "NCC dầu tiềm năng", "Potential");
        // Mỗi mặt hàng gạo chỉ một NCC – theo mặt hàng là thiếu, theo nhóm hàng thì nhóm Gạo đủ 3 (An, Bình, Cường).
        await LinkAsync(head, a, rice1.Id, cai);
        await LinkAsync(head, b, rice2.Id, cai, preferred: true);
        await LinkAsync(head, potential, oil.Id, cai);
        await LinkAsync(head, c, sole.Id, cai);

        // Phiếu NCC độc quyền đã duyệt (người khác duyệt) cho mặt hàng duy nhất của nhóm thứ ba.
        var ndq = await ReadAsync<SoleSourceDto>(await head.PostAsJsonAsync("/api/sole-sources",
            new SaveSoleSourceRequest(c, sole.Id, "Chỉ hãng này có phụ tùng", Today, Today.AddMonths(6)), Ct));
        await ReadAsync<SoleSourceDto>(await head.PostAsync($"/api/sole-sources/{ndq.Id}/submit", null, Ct));
        var approver = await UserWithAsync(Permissions.MasterData.SoleSourceView, Permissions.MasterData.SoleSourceApprove);
        await ReadAsync<SoleSourceDto>(await approver.PostAsJsonAsync($"/api/sole-sources/{ndq.Id}/approve", new ApprovalActionRequest(null), Ct));

        var report = await ReadAsync<SupplierCoverageResult>(await head.GetAsync($"/api/reports/supplier-coverage?categoryId={parent.Id}", Ct));
        // Nhóm cha không có mặt hàng nên không thành dòng.
        Assert.Equal((3, 3, 1), (report.MinSuppliers, report.CategoryCount, report.ShortCount));
        // Thiếu nhiều lên đầu.
        var s = report.Categories[0];
        Assert.Equal((shortCategory.Id, 1, 2, 1, 0, 1, SupplierCoverageStatuses.Short, "NCC dầu Dũng; NCC tổng hợp Cường"),
            (s.CategoryId, s.ItemCount, s.ApprovedCount, s.PotentialCount, s.SuspendedCount, s.Missing, s.Status, s.ApprovedSuppliers));
        var e = report.Categories.Single(r => r.CategoryId == enough.Id);
        Assert.Equal((2, 3, SupplierCoverageStatuses.Ok, "NCC gạo Bình; NCC gạo An; NCC tổng hợp Cường"), (e.ItemCount, e.ApprovedCount, e.Status, e.ApprovedSuppliers));
        var so = report.Categories.Single(r => r.CategoryId == soleCategory.Id);
        Assert.Equal((1, SupplierCoverageStatuses.SoleSource, 0, 1, ndq.No), (so.ApprovedCount, so.Status, so.Missing, so.SoleSourceItemCount, so.SoleSourceNo));

        // Chỉ thiếu; mức tối thiểu 1 thì mọi nhóm đủ; ngoài 1–20 bị chặn.
        var onlyShort = await ReadAsync<SupplierCoverageResult>(await head.GetAsync($"/api/reports/supplier-coverage?categoryId={parent.Id}&onlyShort=true", Ct));
        Assert.Equal([shortCategory.Id], onlyShort.Categories.Select(r => r.CategoryId));
        var one = await ReadAsync<SupplierCoverageResult>(await head.GetAsync($"/api/reports/supplier-coverage?categoryId={parent.Id}&minSuppliers=1", Ct));
        Assert.All(one.Categories, r => Assert.Equal(SupplierCoverageStatuses.Ok, r.Status));
        Assert.Equal(HttpStatusCode.BadRequest, (await head.GetAsync("/api/reports/supplier-coverage?minSuppliers=0", Ct)).StatusCode);

        // Quyền: xem NCC là đủ; không có thì 403.
        var viewer = await UserWithAsync(Permissions.MasterData.SupplierView);
        Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync($"/api/reports/supplier-coverage?categoryId={parent.Id}", Ct)).StatusCode);
        var other = await UserWithAsync(Permissions.MasterData.ItemView);
        Assert.Equal(HttpStatusCode.Forbidden, (await other.GetAsync("/api/reports/supplier-coverage", Ct)).StatusCode);
    }
}
