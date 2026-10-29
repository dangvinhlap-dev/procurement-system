using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Proc.Application.Administration;
using Proc.Application.Auth;
using Proc.Domain.Organization;

namespace Proc.IntegrationTests;

/// <summary>
/// Quản lý vai trò và quyền (design.md mục 9.1, 9.5): ADMIN cố định, mã quyền phải có trong danh mục,
/// không cấp được quyền mình không có.
/// </summary>
[Collection(ApiCollection.Name)]
public class RoleAdministrationTests
{
    private readonly ProcApiFactory _factory;

    public RoleAdministrationTests(ProcApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string UniqueCode(string prefix) => $"{prefix}{Guid.NewGuid():N}".ToUpperInvariant()[..12];

    private Task<HttpClient> AdminAsync() =>
        _factory.CreateClientInCompanyAsync(ProcApiFactory.AdminUserName, ProcApiFactory.AdminPassword, "BHA");

    private static async Task<string?> TitleAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(Ct))?.Title;

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(Ct)}");
        return (await response.Content.ReadFromJsonAsync<T>(Ct))!;
    }

    [Fact]
    public async Task DanhMucQuyen_GomTheoModuleTiengViet()
    {
        var admin = await AdminAsync();

        var groups = await admin.GetFromJsonAsync<List<PermissionGroupDto>>("/api/permissions", Ct);

        Assert.Equal("Hệ thống", groups![0].Name);
        Assert.Equal(Permissions.All.Count, groups.Sum(g => g.Permissions.Count));
        Assert.Contains(groups.Single(g => g.Module == "Purchase").Permissions, p => p.Name == "Duyệt đơn mua hàng");
    }

    [Fact]
    public async Task DanhSach_AdminDauTienVaCoDuQuyen()
    {
        var admin = await AdminAsync();

        var roles = await admin.GetFromJsonAsync<List<RoleListItemDto>>("/api/roles", Ct);

        Assert.Equal(Role.AdminCode, roles![0].Code);
        Assert.True(roles[0].IsSystem);
        Assert.Equal(Permissions.All.Count, roles[0].PermissionCount);
        var thukho = roles.Single(r => r.Code == "THUKHO");
        Assert.False(thukho.IsSystem);
        Assert.True(thukho.UserCount >= 1);
    }

    [Fact]
    public async Task DanhSach_KhongCoQuyenXemVaiTro_Tra403()
    {
        var thukho = await _factory.CreateClientInCompanyAsync("thukho.bha", "Demo@2026", "BHA");
        Assert.Equal(HttpStatusCode.Forbidden, (await thukho.GetAsync("/api/roles", Ct)).StatusCode);
    }

    [Fact]
    public async Task TaoVaSuaVaiTro_QuyenMoiCoHieuLucVoiNguoiDangGiu()
    {
        var admin = await AdminAsync();
        var code = UniqueCode("KT");

        var created = await ReadAsync<RoleDetailDto>(await admin.PostAsJsonAsync("/api/roles",
            new CreateRoleRequest(code.ToLowerInvariant(), "Kế toán kho", [Permissions.Inventory.StockView]), Ct));
        Assert.Equal(code, created.Code);
        Assert.True(created.CanEdit);

        var userName = $"kt{Guid.NewGuid():N}"[..20];
        await _factory.CreateScopedUserAsync(userName, "BHA", code);

        var updated = await ReadAsync<RoleDetailDto>(await admin.PutAsJsonAsync($"/api/roles/{created.Id}",
            new UpdateRoleRequest("Kế toán kho công ty", true,
                [Permissions.Inventory.StockView, Permissions.Inventory.PeriodView]), Ct));

        Assert.Equal("Kế toán kho công ty", updated.Name);
        Assert.Equal([Permissions.Inventory.PeriodView, Permissions.Inventory.StockView], updated.PermissionCodes);
        var client = await _factory.CreateAuthenticatedClientAsync(userName, ProcApiFactory.TestPassword);
        var me = await client.GetFromJsonAsync<CurrentUserInfo>("/api/auth/me", Ct);
        Assert.Contains(Permissions.Inventory.PeriodView, Assert.Single(me!.Companies).Permissions);
    }

    [Fact]
    public async Task TaoVaiTro_TrungMa_BaoTiengViet()
    {
        var admin = await AdminAsync();

        var response = await admin.PostAsJsonAsync("/api/roles", new CreateRoleRequest("THUKHO", "Thủ kho 2", []), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Mã vai trò THUKHO đã có.", await TitleAsync(response));
    }

    [Fact]
    public async Task TaoVaiTro_MaQuyenKhongCoTrongDanhMuc_Tra400()
    {
        var admin = await AdminAsync();

        var response = await admin.PostAsJsonAsync("/api/roles",
            new CreateRoleRequest(UniqueCode("SAI"), "Sai quyền", ["Purchase.PO.Delete"]), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Purchase.PO.Delete", await TitleAsync(response));
    }

    [Fact]
    public async Task SuaVaiTroAdmin_BiTuChoi()
    {
        var admin = await AdminAsync();
        var adminRole = await _factory.GetRoleIdAsync(Role.AdminCode);

        var detail = await admin.GetFromJsonAsync<RoleDetailDto>($"/api/roles/{adminRole}", Ct);
        var response = await admin.PutAsJsonAsync($"/api/roles/{adminRole}",
            new UpdateRoleRequest("Quản trị", true, [Permissions.System.UserView]), Ct);

        Assert.False(detail!.CanEdit);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Vai trò Quản trị hệ thống luôn có đủ mọi quyền, không sửa được.", await TitleAsync(response));
    }

    [Fact]
    public async Task QuanLyVaiTro_KhongCapQuyenMinhKhongCo()
    {
        var managerRole = UniqueCode("QLVT");
        await _factory.CreateRoleAsync(managerRole, Permissions.System.RoleView, Permissions.System.RoleManage, Permissions.Inventory.StockView);
        var userName = $"qlvt{Guid.NewGuid():N}"[..20];
        await _factory.CreateScopedUserAsync(userName, "BHA", managerRole);
        var manager = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, "BHA");

        var grantMore = await manager.PostAsJsonAsync("/api/roles",
            new CreateRoleRequest(UniqueCode("LEO"), "Leo quyền", [Permissions.Inventory.PeriodClose]), Ct);
        var editThukho = await manager.PutAsJsonAsync($"/api/roles/{await _factory.GetRoleIdAsync("THUKHO")}",
            new UpdateRoleRequest("Thủ kho", true, [Permissions.Inventory.StockView]), Ct);
        var allowed = await manager.PostAsJsonAsync("/api/roles",
            new CreateRoleRequest(UniqueCode("XEM"), "Xem tồn", [Permissions.Inventory.StockView]), Ct);

        Assert.Equal(HttpStatusCode.Forbidden, grantMore.StatusCode);
        Assert.Contains(Permissions.Inventory.PeriodClose, await TitleAsync(grantMore));
        Assert.Equal(HttpStatusCode.Forbidden, editThukho.StatusCode);
        Assert.Equal(HttpStatusCode.Created, allowed.StatusCode);
    }
}
