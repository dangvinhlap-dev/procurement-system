using System.Net;
using System.Net.Http.Json;
using Proc.Application.MasterData;
using Proc.Domain.Organization;

namespace Proc.IntegrationTests;

/// <summary>
/// Ngành hàng NCC chọn từ danh mục Nhóm hàng (design.md mục 5.3, 5.8; người dùng chốt 07/11/2026): lưu không cần duyệt, thay cả danh sách,
/// lọc danh sách NCC theo ngành hàng gồm nhóm con; người bị giới hạn nhóm hàng vẫn thấy đủ ngành hàng của NCC.
/// </summary>
[Collection(ApiCollection.Name)]
public class SupplierCategoryTests
{
    private readonly ProcApiFactory _factory;

    public SupplierCategoryTests(ProcApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string UniqueCode(string prefix) => $"{prefix}{Guid.NewGuid():N}".ToUpperInvariant()[..10];

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(Ct)}");
        return (await response.Content.ReadFromJsonAsync<T>(Ct))!;
    }

    [Fact]
    public async Task NganhHang_LuuKhongCanDuyet_LocGomNhomCon_NguoiGioiHanVanThayDu()
    {
        var userName = $"nh.{Guid.NewGuid():N}"[..16];
        await _factory.CreateUserAsync(userName, ProcApiFactory.TestPassword, ["TCT"]);
        var head = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, "TCT");
        async Task<ItemCategoryDetailDto> CategoryAsync(string name, Guid? parentId = null) =>
            await ReadAsync<ItemCategoryDetailDto>(await head.PostAsJsonAsync("/api/item-categories",
                new CreateItemCategoryRequest(null, $"{name} {Guid.NewGuid():N}", parentId, null), Ct));
        var root = await CategoryAsync("Ngành cơ khí");
        var child = await CategoryAsync("Bu lông", root.Id);
        var other = await CategoryAsync("Ngành điện");
        var stopped = await CategoryAsync("Ngành cũ");
        await ReadAsync<ItemCategoryDetailDto>(await head.PutAsJsonAsync($"/api/item-categories/{stopped.Id}",
            new UpdateItemCategoryRequest(stopped.Name, null, null, false), Ct));

        var groupId = (await head.GetFromJsonAsync<List<SupplierGroupDto>>("/api/supplier-groups", Ct))!.Single(g => g.Code == "TN").Id;
        SaveSupplierRequest Request(string name, IReadOnlyList<Guid>? categoryIds) =>
            new(null, groupId, name, null, false, null, null, null, null, null, null, "Ghi chú cũ", null, "Potential", null, null, [], [], categoryIds);
        var supplierName = $"NCC ngành hàng {Guid.NewGuid():N}";
        var created = await ReadAsync<SupplierDetailDto>(await head.PostAsJsonAsync("/api/suppliers", Request(supplierName, [child.Id]), Ct));
        Assert.Equal([child.Code], created.Categories.Select(c => c.Code));
        Assert.Equal("Ghi chú cũ", created.SupplyScope);

        // Lọc theo nhóm cha gồm nhóm con; nhóm khác không ra; cột ngành hàng trên danh sách.
        var byRoot = await ReadAsync<SupplierListResult>(await head.GetAsync($"/api/suppliers?categoryId={root.Id}", Ct));
        var row = Assert.Single(byRoot.Items, s => s.Id == created.Id);
        Assert.Equal(child.Name, row.Categories);
        var byOther = await ReadAsync<SupplierListResult>(await head.GetAsync($"/api/suppliers?categoryId={other.Id}", Ct));
        Assert.DoesNotContain(byOther.Items, s => s.Id == created.Id);

        // Không gửi ngành hàng (Excel cũ, API cũ) = giữ nguyên; gửi danh sách = thay cả danh sách, áp ngay không lập yêu cầu duyệt.
        var kept = await ReadAsync<SupplierDetailDto>(await head.PutAsJsonAsync($"/api/suppliers/{created.Id}", Request(supplierName, null), Ct));
        Assert.Equal([child.Code], kept.Categories.Select(c => c.Code));
        var replaced = await ReadAsync<SupplierDetailDto>(await head.PutAsJsonAsync($"/api/suppliers/{created.Id}",
            Request(supplierName, [other.Id, root.Id, other.Id]), Ct));
        Assert.Equal(new[] { root.Code, other.Code }.Order(StringComparer.Ordinal), replaced.Categories.Select(c => c.Code));
        Assert.Empty(replaced.PendingRequests ?? []);

        // Nhóm đã ngừng dùng không thêm mới được.
        var withStopped = await head.PutAsJsonAsync($"/api/suppliers/{created.Id}", Request(supplierName, [root.Id, stopped.Id]), Ct);
        Assert.Equal(HttpStatusCode.BadRequest, withStopped.StatusCode);

        // Người chỉ được nhóm "Ngành điện" vẫn thấy đủ ngành hàng của NCC (danh mục nhóm hàng không giới hạn phạm vi).
        var roleCode = UniqueCode("XN");
        await _factory.CreateRoleAsync(roleCode, Permissions.MasterData.SupplierView, Permissions.MasterData.ItemCategoryView);
        var limitedName = $"gh.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(limitedName, "TCT", roleCode, categoryCodes: [other.Code]);
        var limited = await _factory.CreateClientInCompanyAsync(limitedName, ProcApiFactory.TestPassword, "TCT");
        var seen = await ReadAsync<SupplierDetailDto>(await limited.GetAsync($"/api/suppliers/{created.Id}", Ct));
        Assert.Equal(replaced.Categories.Select(c => c.Code), seen.Categories.Select(c => c.Code));
    }
}
