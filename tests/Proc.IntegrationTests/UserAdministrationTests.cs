using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Proc.Application.Administration;
using Proc.Application.Auth;
using Proc.Application.Organization;
using Proc.Domain.Organization;

namespace Proc.IntegrationTests;

/// <summary>
/// Quản lý người dùng: tạo tài khoản, gán vai trò và phạm vi theo công ty, chống tự nâng quyền (design.md mục 9.1, 9.2, 9.5).
/// Dữ liệu mẫu: BHA có kho KT, KP1, KP2; NMI có KT, KP1; thukho.bha là Thủ kho ở BHA; muahang.nmi là NV mua hàng ở NMI.
/// </summary>
[Collection(ApiCollection.Name)]
public class UserAdministrationTests
{
    private readonly ProcApiFactory _factory;

    public UserAdministrationTests(ProcApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string UniqueName(string prefix) => $"{prefix}{Guid.NewGuid():N}"[..20];

    private Task<HttpClient> AdminAtAsync(string company) =>
        _factory.CreateClientInCompanyAsync(ProcApiFactory.AdminUserName, ProcApiFactory.AdminPassword, company);

    private static async Task<string?> TitleAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(Ct))?.Title;

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(Ct)}");
        return (await response.Content.ReadFromJsonAsync<T>(Ct))!;
    }

    private static async Task<Dictionary<string, Guid>> WarehouseIdsAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<UserAccessOptionsDto>("/api/users/access-options", Ct))!
        .Warehouses.ToDictionary(w => w.Code, w => w.Id);

    private static CreateUserRequest NewUser(string userName, params Guid[] roleIds) =>
        new(userName, "Nguyễn Văn Thử", null, ProcApiFactory.TestPassword, new UserAccessDto(roleIds, [], []));

    /// <summary>Tạo một người quản lý người dùng ở BHA với vai trò chỉ gồm các quyền cho trước.</summary>
    private async Task<HttpClient> ManagerAtBhaAsync(string[] permissions, string[]? warehouses = null)
    {
        var roleCode = UniqueName("QL").ToUpperInvariant()[..12];
        await _factory.CreateRoleAsync(roleCode, permissions);
        var userName = UniqueName("ql");
        await _factory.CreateScopedUserAsync(userName, "BHA", roleCode, warehouses);
        return await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, "BHA");
    }

    private static readonly string[] UserManagerPermissions =
    [
        Permissions.System.UserView, Permissions.System.UserManage, Permissions.Organization.WarehouseView,
    ];

    [Fact]
    public async Task DanhSach_HienVaiTroTaiCongTyDangLamViec()
    {
        var admin = await AdminAtAsync("BHA");

        var users = await admin.GetFromJsonAsync<List<UserListItemDto>>("/api/users", Ct);

        var thukho = Assert.Single(users!, u => u.UserName == "thukho.bha");
        Assert.Equal(["Thủ kho"], thukho.RoleNames);
        Assert.Equal(["BHA"], thukho.CompanyCodes);
        // muahang.nmi không làm việc ở BHA nhưng vẫn có trên danh sách để quản trị gán thêm được.
        var muahang = Assert.Single(users!, u => u.UserName == "muahang.nmi");
        Assert.Empty(muahang.RoleNames);
        Assert.Equal(["NMI"], muahang.CompanyCodes);
    }

    [Fact]
    public async Task DanhSach_KhongCoQuyenXemNguoiDung_Tra403()
    {
        var thukho = await _factory.CreateClientInCompanyAsync("thukho.bha", "Demo@2026", "BHA");
        Assert.Equal(HttpStatusCode.Forbidden, (await thukho.GetAsync("/api/users", Ct)).StatusCode);
    }

    [Fact]
    public async Task TaoNguoiDung_KemVaiTroVaKho_DangNhapThayDungPhamVi()
    {
        var admin = await AdminAtAsync("BHA");
        var warehouses = await WarehouseIdsAsync(admin);
        var userName = UniqueName("moi");
        var thukhoRole = await _factory.GetRoleIdAsync("THUKHO");

        var created = await ReadAsync<UserDetailDto>(await admin.PostAsJsonAsync("/api/users",
            new CreateUserRequest(userName, " Phạm Thị Mới ", "moi@example.com", ProcApiFactory.TestPassword,
                new UserAccessDto([thukhoRole], [warehouses["KP2"]], [])), Ct));

        Assert.Equal("Phạm Thị Mới", created.FullName);
        Assert.True(created.IsActive);
        Assert.Equal(["BHA"], created.CompanyCodes);

        var client = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, "BHA");
        var visible = await client.GetFromJsonAsync<List<WarehouseDto>>("/api/warehouses", Ct);
        Assert.Equal(["KP2"], visible!.Select(w => w.Code));
    }

    [Fact]
    public async Task TaoNguoiDung_TrungTenDangNhap_BaoTiengViet()
    {
        var admin = await AdminAtAsync("BHA");

        var response = await admin.PostAsJsonAsync("/api/users", NewUser("thukho.bha"), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("thukho.bha", await TitleAsync(response));
    }

    [Theory]
    [InlineData("Co Hoa", ProcApiFactory.TestPassword)]
    [InlineData("hople", "chicochu")]
    public async Task TaoNguoiDung_DuLieuKhongHopLe_Tra400(string userName, string password)
    {
        var admin = await AdminAtAsync("BHA");

        var response = await admin.PostAsJsonAsync("/api/users",
            new CreateUserRequest(userName, "Thử", null, password, new UserAccessDto([], [], [])), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GanQuyen_DoiVaiTro_CoHieuLucNgay()
    {
        var admin = await AdminAtAsync("BHA");
        var userName = UniqueName("doivt");
        var user = await ReadAsync<UserDetailDto>(await admin.PostAsJsonAsync("/api/users",
            NewUser(userName, await _factory.GetRoleIdAsync("THUKHO")), Ct));
        var nvmua = await _factory.GetRoleIdAsync("NVMUA");

        var updated = await ReadAsync<UserDetailDto>(await admin.PutAsJsonAsync(
            $"/api/users/{user.Id}/access", new UserAccessDto([nvmua], [], []), Ct));

        Assert.Equal([nvmua], updated.Access.RoleIds);
        // Token chỉ chứa định danh nên quyền mới có hiệu lực ngay ở request kế tiếp (design.md mục 9.4).
        var client = await _factory.CreateAuthenticatedClientAsync(userName, ProcApiFactory.TestPassword);
        var me = await client.GetFromJsonAsync<CurrentUserInfo>("/api/auth/me", Ct);
        Assert.Equal(["NVMUA"], Assert.Single(me!.Companies).RoleCodes);
    }

    [Fact]
    public async Task GanQuyen_BoHetVaiTro_GoKhoiCongTyVaXoaPhamVi()
    {
        var admin = await AdminAtAsync("BHA");
        var warehouses = await WarehouseIdsAsync(admin);
        var user = await ReadAsync<UserDetailDto>(await admin.PostAsJsonAsync("/api/users",
            new CreateUserRequest(UniqueName("go"), "Thử", null, ProcApiFactory.TestPassword,
                new UserAccessDto([await _factory.GetRoleIdAsync("THUKHO")], [warehouses["KP1"]], [])), Ct));

        var updated = await ReadAsync<UserDetailDto>(await admin.PutAsJsonAsync(
            $"/api/users/{user.Id}/access", new UserAccessDto([], [], []), Ct));

        Assert.Empty(updated.Access.RoleIds);
        Assert.Empty(updated.Access.WarehouseIds);
        Assert.Empty(updated.CompanyCodes);
    }

    [Fact]
    public async Task GanQuyen_KhoCuaCongTyKhac_BiTuChoi()
    {
        // Không tin client: Id kho của NMI gửi lên khi đang làm việc ở BHA phải bị chặn (CLAUDE.md mục 4).
        var nmiWarehouses = await WarehouseIdsAsync(await AdminAtAsync("NMI"));
        var admin = await AdminAtAsync("BHA");

        var response = await admin.PostAsJsonAsync("/api/users",
            new CreateUserRequest(UniqueName("kho"), "Thử", null, ProcApiFactory.TestPassword,
                new UserAccessDto([await _factory.GetRoleIdAsync("THUKHO")], [nmiWarehouses["KP1"]], [])), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Có kho không thuộc phạm vi của bạn tại công ty đang làm việc.", await TitleAsync(response));
    }

    [Fact]
    public async Task GanQuyen_TuSuaQuyenCuaMinh_BiTuChoi()
    {
        var admin = await AdminAtAsync("BHA");
        var me = (await admin.GetFromJsonAsync<List<UserListItemDto>>("/api/users", Ct))!.Single(u => u.UserName == "admin");

        var access = await admin.PutAsJsonAsync($"/api/users/{me.Id}/access", new UserAccessDto([], [], []), Ct);
        var deactivate = await admin.PutAsJsonAsync($"/api/users/{me.Id}", new UpdateUserRequest("Quản trị", null, false), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, access.StatusCode);
        Assert.Equal("Không được tự sửa quyền của chính mình.", await TitleAsync(access));
        Assert.Equal(HttpStatusCode.BadRequest, deactivate.StatusCode);
    }

    [Fact]
    public async Task QuanLyCongTy_GanVaiTroCoQuyenVuotQuyenMinh_Tra403()
    {
        var manager = await ManagerAtBhaAsync(UserManagerPermissions);

        // THUKHO có quyền ghi sổ kho mà người quản lý không có – gán được là tự nâng quyền qua người khác.
        var response = await manager.PostAsJsonAsync("/api/users",
            NewUser(UniqueName("leo"), await _factory.GetRoleIdAsync("THUKHO")), Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("Thủ kho", await TitleAsync(response));
    }

    [Fact]
    public async Task QuanLyCongTy_GanVaiTroTrongQuyenMinh_DuocPhep()
    {
        var manager = await ManagerAtBhaAsync(UserManagerPermissions);
        var viewer = await _factory.CreateRoleAsync(UniqueName("XK").ToUpperInvariant()[..12], Permissions.Organization.WarehouseView);

        var options = await manager.GetFromJsonAsync<UserAccessOptionsDto>("/api/users/access-options", Ct);
        Assert.True(options!.Roles.Single(r => r.Id == viewer).CanAssign);
        Assert.False(options.Roles.Single(r => r.Code == "THUKHO").CanAssign);
        Assert.False(options.Roles.Single(r => r.Code == Role.AdminCode).CanAssign);

        var response = await manager.PostAsJsonAsync("/api/users", NewUser(UniqueName("xem"), viewer), Ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task QuanLyBiGioiHanKho_KhongCapMoiKhoVaKhoNgoaiPhamVi()
    {
        var manager = await ManagerAtBhaAsync(UserManagerPermissions, warehouses: ["KP1"]);
        var viewer = await _factory.CreateRoleAsync(UniqueName("XK").ToUpperInvariant()[..12], Permissions.Organization.WarehouseView);
        var all = await WarehouseIdsAsync(await AdminAtAsync("BHA"));

        var options = await manager.GetFromJsonAsync<UserAccessOptionsDto>("/api/users/access-options", Ct);
        Assert.True(options!.MustLimitWarehouses);
        Assert.Equal(["KP1"], options.Warehouses.Select(w => w.Code));

        var allWarehouses = await manager.PostAsJsonAsync("/api/users", NewUser(UniqueName("mk"), viewer), Ct);
        var outside = await manager.PostAsJsonAsync("/api/users",
            new CreateUserRequest(UniqueName("ng"), "Thử", null, ProcApiFactory.TestPassword,
                new UserAccessDto([viewer], [all["KP2"]], [])), Ct);
        var inside = await manager.PostAsJsonAsync("/api/users",
            new CreateUserRequest(UniqueName("tr"), "Thử", null, ProcApiFactory.TestPassword,
                new UserAccessDto([viewer], [all["KP1"]], [])), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, allWarehouses.StatusCode);
        Assert.Equal("Bạn chỉ được làm việc với một số kho, nên phải chọn kho cụ thể cho người dùng.", await TitleAsync(allWarehouses));
        Assert.Equal(HttpStatusCode.BadRequest, outside.StatusCode);
        Assert.Equal(HttpStatusCode.Created, inside.StatusCode);
    }

    [Fact]
    public async Task QuanLyCongTy_KhongDatLaiMatKhauNguoiCoQuyenCaoHon()
    {
        var manager = await ManagerAtBhaAsync(UserManagerPermissions);
        var admin = await AdminAtAsync("BHA");
        var users = (await admin.GetFromJsonAsync<List<UserListItemDto>>("/api/users", Ct))!;
        var adminId = users.Single(u => u.UserName == "admin").Id;

        var response = await manager.PostAsJsonAsync(
            $"/api/users/{adminId}/reset-password", new ResetPasswordRequest("chiemquyen1"), Ct);
        var detail = await manager.GetFromJsonAsync<UserDetailDto>($"/api/users/{adminId}", Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.False(detail!.CanManageAccount);
    }

    [Fact]
    public async Task DatLaiMatKhau_MoKhoaTam_DangNhapBangMatKhauMoi()
    {
        var admin = await AdminAtAsync("BHA");
        var userName = UniqueName("khoa");
        var user = await ReadAsync<UserDetailDto>(await admin.PostAsJsonAsync("/api/users",
            NewUser(userName, await _factory.GetRoleIdAsync("THUKHO")), Ct));

        var anonymous = _factory.CreateClient();
        for (var i = 0; i < 5; i++)
            await anonymous.PostAsJsonAsync("/api/auth/login", new LoginRequest(userName, "sai-mat-khau-1"), Ct);
        var locked = await admin.GetFromJsonAsync<UserDetailDto>($"/api/users/{user.Id}", Ct);
        Assert.NotNull(locked!.LockoutEnd);

        var reset = await admin.PostAsJsonAsync($"/api/users/{user.Id}/reset-password", new ResetPasswordRequest("moimoi2026"), Ct);

        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await anonymous.PostAsJsonAsync("/api/auth/login", new LoginRequest(userName, "moimoi2026"), Ct)).StatusCode);
    }

    [Fact]
    public async Task NgungDung_KhongDangNhapDuocNua()
    {
        var admin = await AdminAtAsync("BHA");
        var userName = UniqueName("ngung");
        var user = await ReadAsync<UserDetailDto>(await admin.PostAsJsonAsync("/api/users",
            NewUser(userName, await _factory.GetRoleIdAsync("THUKHO")), Ct));

        var updated = await ReadAsync<UserDetailDto>(await admin.PutAsJsonAsync(
            $"/api/users/{user.Id}", new UpdateUserRequest("Người đã nghỉ", "nghi@example.com", false), Ct));

        Assert.False(updated.IsActive);
        Assert.Equal("nghi@example.com", updated.Email);
        var login = await _factory.CreateClient().PostAsJsonAsync(
            "/api/auth/login", new LoginRequest(userName, ProcApiFactory.TestPassword), Ct);
        Assert.Equal(HttpStatusCode.Forbidden, login.StatusCode);
    }

    [Fact]
    public async Task ChiTiet_KhongCoTaiKhoan_Tra404()
    {
        var admin = await AdminAtAsync("BHA");
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/users/{Guid.CreateVersion7()}", Ct)).StatusCode);
    }
}
