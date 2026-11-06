using System.Net;
using System.Net.Http.Json;
using Proc.Application.Common;
using Proc.Application.MasterData;
using Proc.Application.Purchasing;
using Proc.Application.Reports;
using Proc.Domain.Organization;

namespace Proc.IntegrationTests;

/// <summary>
/// NCC đủ điều kiện theo mặt hàng (lát G1, design.md mục 18.25 – quyết định thành lập Ban cung ứng: mỗi mặt hàng tối thiểu 03 NCC):
/// chỉ đếm NCC Đã duyệt; NCC độc quyền đã duyệt còn tác dụng thì miễn; lọc nhóm hàng và "chỉ thiếu"; cần quyền xem NCC.
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

    /// <summary>NCC theo trạng thái; Đã duyệt thì đi hết quy trình duyệt NCC.</summary>
    private async Task<Guid> SupplierAsync(HttpClient head, Guid groupId, string name, string status)
    {
        var supplier = await ReadAsync<SupplierDetailDto>(await head.PostAsJsonAsync("/api/suppliers",
            new SaveSupplierRequest(null, groupId, name, null, false, null, null, null, null, null, null, null, null, status, null, null, [], []), Ct));
        if (status == "Approved") await _factory.FinishSupplierApprovalAsync(supplier.Id);
        return supplier.Id;
    }

    private async Task LinkAsync(HttpClient head, Guid supplierId, Guid itemId, Guid uomId, bool preferred = false) =>
        await ReadAsync<SupplierItemDto>(await head.PostAsJsonAsync($"/api/suppliers/{supplierId}/items",
            new SaveSupplierItemRequest(itemId, null, uomId, null, null, preferred), Ct));

    [Fact]
    public async Task DemNccDaDuyet_DocQuyenDuocMien_LocChiThieu_CanQuyenXemNcc()
    {
        var userName = $"cm.{Guid.NewGuid():N}"[..16];
        await _factory.CreateUserAsync(userName, ProcApiFactory.TestPassword, ["TCT"]);
        var head = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, "TCT");
        var cai = (await head.GetFromJsonAsync<List<UomDto>>("/api/uoms", Ct))!.Single(u => u.Code == "CAI").Id;
        var category = await ReadAsync<ItemCategoryDetailDto>(await head.PostAsJsonAsync("/api/item-categories",
            new CreateItemCategoryRequest(null, $"Nhóm đủ NCC {UniqueCode("")}", null, null), Ct));
        async Task<ItemDetailDto> Item(string name) => await ReadAsync<ItemDetailDto>(await head.PostAsJsonAsync("/api/items",
            new CreateItemRequest(null, name, null, "Inventory", category.Id, cai, null, null, null, null, "10", null, false, false, null, null, []), Ct));
        var enough = await Item("Gạo tám thơm");
        var shortItem = await Item("Dầu ăn 5 lít");
        var sole = await Item("Phụ tùng máy làm kem");

        var groupId = (await head.GetFromJsonAsync<List<SupplierGroupDto>>("/api/supplier-groups", Ct))!.Single(g => g.Code == "TN").Id;
        var a = await SupplierAsync(head, groupId, "NCC gạo An", "Approved");
        var b = await SupplierAsync(head, groupId, "NCC gạo Bình", "Approved");
        var c = await SupplierAsync(head, groupId, "NCC gạo Cường", "Approved");
        var potential = await SupplierAsync(head, groupId, "NCC dầu tiềm năng", "Potential");
        await LinkAsync(head, a, enough.Id, cai);
        await LinkAsync(head, b, enough.Id, cai, preferred: true);
        await LinkAsync(head, c, enough.Id, cai);
        await LinkAsync(head, a, shortItem.Id, cai);
        await LinkAsync(head, potential, shortItem.Id, cai);
        await LinkAsync(head, c, sole.Id, cai);

        // Phiếu NCC độc quyền đã duyệt (người khác duyệt) cho mặt hàng thứ ba.
        var ndq = await ReadAsync<SoleSourceDto>(await head.PostAsJsonAsync("/api/sole-sources",
            new SaveSoleSourceRequest(c, sole.Id, "Chỉ hãng này có phụ tùng", Today, Today.AddMonths(6)), Ct));
        await ReadAsync<SoleSourceDto>(await head.PostAsync($"/api/sole-sources/{ndq.Id}/submit", null, Ct));
        var approver = await UserWithAsync(Permissions.MasterData.SoleSourceView, Permissions.MasterData.SoleSourceApprove);
        await ReadAsync<SoleSourceDto>(await approver.PostAsJsonAsync($"/api/sole-sources/{ndq.Id}/approve", new ApprovalActionRequest(null), Ct));

        var report = await ReadAsync<SupplierCoverageResult>(await head.GetAsync($"/api/reports/supplier-coverage?categoryId={category.Id}", Ct));
        Assert.Equal((3, 3, 1), (report.MinSuppliers, report.ItemCount, report.ShortCount));
        // Thiếu nhiều lên đầu.
        Assert.Equal(shortItem.Id, report.Items[0].ItemId);
        var s = report.Items[0];
        Assert.Equal((1, 1, 0, 2, SupplierCoverageStatuses.Short, "NCC gạo An"), (s.ApprovedCount, s.PotentialCount, s.SuspendedCount, s.Missing, s.Status, s.ApprovedSuppliers));
        var e = report.Items.Single(r => r.ItemId == enough.Id);
        Assert.Equal((3, SupplierCoverageStatuses.Ok, "NCC gạo Bình; NCC gạo An; NCC gạo Cường"), (e.ApprovedCount, e.Status, e.ApprovedSuppliers));
        var so = report.Items.Single(r => r.ItemId == sole.Id);
        Assert.Equal((SupplierCoverageStatuses.SoleSource, 0, ndq.No), (so.Status, so.Missing, so.SoleSourceNo));

        // Chỉ thiếu; mức tối thiểu 1 thì mọi mặt hàng đủ; ngoài 1–20 bị chặn.
        var onlyShort = await ReadAsync<SupplierCoverageResult>(await head.GetAsync($"/api/reports/supplier-coverage?categoryId={category.Id}&onlyShort=true", Ct));
        Assert.Equal([shortItem.Id], onlyShort.Items.Select(r => r.ItemId));
        var one = await ReadAsync<SupplierCoverageResult>(await head.GetAsync($"/api/reports/supplier-coverage?categoryId={category.Id}&minSuppliers=1", Ct));
        Assert.All(one.Items, r => Assert.Equal(SupplierCoverageStatuses.Ok, r.Status));
        Assert.Equal(HttpStatusCode.BadRequest, (await head.GetAsync("/api/reports/supplier-coverage?minSuppliers=0", Ct)).StatusCode);

        // Quyền: xem NCC là đủ; không có thì 403.
        var viewer = await UserWithAsync(Permissions.MasterData.SupplierView);
        Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync($"/api/reports/supplier-coverage?categoryId={category.Id}", Ct)).StatusCode);
        var other = await UserWithAsync(Permissions.MasterData.ItemView);
        Assert.Equal(HttpStatusCode.Forbidden, (await other.GetAsync("/api/reports/supplier-coverage", Ct)).StatusCode);
    }
}
