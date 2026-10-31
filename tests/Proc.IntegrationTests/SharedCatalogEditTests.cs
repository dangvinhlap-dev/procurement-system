using System.Net;
using System.Net.Http.Json;
using Proc.Application.Common;
using Proc.Application.MasterData;
using Proc.Application.Organization;
using Proc.Domain.Organization;

namespace Proc.IntegrationTests;

/// <summary>
/// Sửa thông tin danh mục dùng chung theo phân quyền (design.md mục 5.7): quyền <c>MasterData.Catalog.EditShared</c> sửa được ở công ty thành
/// viên (trừ mã); thêm mới vẫn chỉ ở tổng công ty; quyền quản lý danh mục mà không có quyền này thì ở công ty thành viên vẫn chỉ xem.
/// </summary>
[Collection(ApiCollection.Name)]
public class SharedCatalogEditTests
{
    private readonly ProcApiFactory _factory;

    public SharedCatalogEditTests(ProcApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string UniqueCode(string prefix) => $"{prefix}{Guid.NewGuid():N}".ToUpperInvariant()[..10];

    private static DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTime.UtcNow, VietnamTime.Zone));

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(Ct)}");
        return (await response.Content.ReadFromJsonAsync<T>(Ct))!;
    }

    /// <summary>Người dùng mới ở công ty thành viên với vai trò mới gồm các quyền cho trước.</summary>
    private async Task<HttpClient> MemberUserAsync(string companyCode, params string[] permissions)
    {
        var roleCode = UniqueCode("R");
        await _factory.CreateRoleAsync(roleCode, permissions);
        var userName = $"dm.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(userName, companyCode, roleCode);
        return await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, companyCode);
    }

    [Fact]
    public async Task SuaDanhMucDungChung_TheoQuyen_KhongDoiMa_ThemMoiChiOTongCongTy()
    {
        var head = await _factory.CreateClientInCompanyAsync(ProcApiFactory.AdminUserName, ProcApiFactory.AdminPassword, "TCT");
        var company = await ReadAsync<CompanyDetailDto>(await head.PostAsJsonAsync("/api/companies",
            new CreateCompanyRequest(UniqueCode("S"), "Công ty thử sửa danh mục", null, null, null, null, new DateOnly(Today.Year, Today.Month, 1), "KT", "Kho tổng"), Ct));
        var uom = await ReadAsync<UomDto>(await head.PostAsJsonAsync("/api/uoms", new CreateUomRequest(UniqueCode("U")[..8], "Cuộn"), Ct));
        var category = await ReadAsync<ItemCategoryDetailDto>(await head.PostAsJsonAsync("/api/item-categories",
            new CreateItemCategoryRequest(UniqueCode("C"), "Nhóm sửa tên", null, null), Ct));
        var item = await ReadAsync<ItemDetailDto>(await head.PostAsJsonAsync("/api/items",
            new CreateItemRequest($"{category.Code}-01", "Dây điện", null, "Inventory", category.Id, uom.Id, null, null, null, null, "10", null, false, false, null, null, []), Ct));
        var group = await ReadAsync<SupplierGroupDto>(await head.PostAsJsonAsync("/api/supplier-groups", new CreateSupplierGroupRequest(UniqueCode("G"), "Nhóm NCC sửa tên"), Ct));
        var supplier = await ReadAsync<SupplierDetailDto>(await head.PostAsJsonAsync("/api/suppliers",
            new SaveSupplierRequest(UniqueCode("N"), group.Id, "NCC cũ", null, false, null, null, null, null, null, null, null, null, "Approved", null, null, [], []), Ct));

        var editor = await MemberUserAsync(company.Code,
            Permissions.MasterData.CatalogEditShared, Permissions.MasterData.ItemView, Permissions.MasterData.ItemCategoryView,
            Permissions.MasterData.SupplierView, Permissions.System.CompanyView);

        // Ở công ty thành viên: sửa được tên mọi danh mục dùng chung; mã giữ nguyên.
        var seen = await ReadAsync<ItemDetailDto>(await editor.GetAsync($"/api/items/{item.Id}", Ct));
        Assert.True(seen.CanEdit);
        var renamed = await ReadAsync<ItemDetailDto>(await editor.PutAsJsonAsync($"/api/items/{item.Id}",
            new UpdateItemRequest("Dây điện 2x1,5", null, "Inventory", category.Id, uom.Id, null, null, null, null, "10", null, false, false, null, null, [], true), Ct));
        Assert.Equal((item.Code, "Dây điện 2x1,5"), (renamed.Code, renamed.Name));
        Assert.Equal("Cuộn dây", (await ReadAsync<UomDto>(await editor.PutAsJsonAsync($"/api/uoms/{uom.Id}", new UpdateUomRequest("Cuộn dây", true), Ct))).Name);
        Assert.Equal("Nhóm dây điện", (await ReadAsync<ItemCategoryDetailDto>(await editor.PutAsJsonAsync($"/api/item-categories/{category.Id}",
            new UpdateItemCategoryRequest("Nhóm dây điện", null, null, true), Ct))).Name);
        Assert.Equal("Nhóm NCC điện", (await ReadAsync<SupplierGroupDto>(await editor.PutAsJsonAsync($"/api/supplier-groups/{group.Id}",
            new UpdateSupplierGroupRequest("Nhóm NCC điện", true), Ct))).Name);
        var updatedSupplier = await ReadAsync<SupplierDetailDto>(await editor.PutAsJsonAsync($"/api/suppliers/{supplier.Id}",
            new SaveSupplierRequest("MA-KHAC", group.Id, "NCC mới", null, false, null, null, null, null, null, null, null, null, "Approved", null, null, [], []), Ct));
        Assert.Equal((supplier.Code, "NCC mới"), (updatedSupplier.Code, updatedSupplier.Name));
        var ownCompany = await ReadAsync<CompanyDetailDto>(await editor.PutAsJsonAsync($"/api/companies/{company.Id}",
            new UpdateCompanyRequest("Công ty đã đổi tên", null, null, null, null, new DateOnly(Today.Year, Today.Month, 1), true), Ct));
        Assert.Equal((company.Code, "Công ty đã đổi tên"), (ownCompany.Code, ownCompany.Name));
        var tct = await _factory.GetCompanyIdAsync("TCT");
        Assert.Equal(HttpStatusCode.NotFound, (await editor.PutAsJsonAsync($"/api/companies/{tct}",
            new UpdateCompanyRequest("Đổi tên tổng công ty", null, null, null, null, null, true), Ct)).StatusCode);

        // Thêm mới vẫn cần quyền quản lý và làm việc ở tổng công ty.
        Assert.Equal(HttpStatusCode.Forbidden, (await editor.PostAsJsonAsync("/api/uoms", new CreateUomRequest(UniqueCode("V")[..8], "Mới"), Ct)).StatusCode);

        // Có quyền quản lý hàng hóa nhưng không có quyền sửa danh mục dùng chung: ở công ty thành viên chỉ xem.
        var manager = await MemberUserAsync(company.Code, Permissions.MasterData.ItemManage, Permissions.MasterData.ItemView);
        Assert.False((await ReadAsync<ItemDetailDto>(await manager.GetAsync($"/api/items/{item.Id}", Ct))).CanEdit);
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.PutAsJsonAsync($"/api/items/{item.Id}",
            new UpdateItemRequest("Đổi tên trái phép", null, "Inventory", category.Id, uom.Id, null, null, null, null, "10", null, false, false, null, null, [], true), Ct)).StatusCode);
    }
}
