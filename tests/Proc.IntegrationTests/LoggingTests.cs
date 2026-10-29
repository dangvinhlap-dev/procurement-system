using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Proc.Application.Administration;
using Proc.Application.Auth;
using Proc.Application.Logging;
using Proc.Domain.Organization;
using Proc.Infrastructure.Persistence;

namespace Proc.IntegrationTests;

/// <summary>
/// Nhật ký thao tác (AuditLog) và nhật ký truy cập (AccessLog) – design.md mục 9.3, CLAUDE.md mục 4:
/// mọi thay đổi entity [Auditable] vào AuditLog qua SaveChangesInterceptor; AuditLog chỉ thêm, không sửa/xóa.
/// </summary>
[Collection(ApiCollection.Name)]
public class LoggingTests
{
    private static readonly TimeZoneInfo Vietnam = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
    private readonly ProcApiFactory _factory;

    public LoggingTests(ProcApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Vietnam).DateTime).ToString("yyyy-MM-dd");

    private static string UniqueCode(string prefix) => $"{prefix}{Guid.NewGuid():N}".ToUpperInvariant()[..12];

    private static string UniqueName(string prefix) => $"{prefix}{Guid.NewGuid():N}"[..20];

    private Task<HttpClient> AdminAtAsync(string company) =>
        _factory.CreateClientInCompanyAsync(ProcApiFactory.AdminUserName, ProcApiFactory.AdminPassword, company);

    private static async Task<List<AuditLogItemDto>> AuditAsync(HttpClient client, string filter = "")
    {
        var page = await client.GetFromJsonAsync<LogPage<AuditLogItemDto>>($"/api/audit-logs?from={Today}&to={Today}{filter}", Ct);
        return [.. page!.Items];
    }

    private static async Task<List<AccessLogItemDto>> AccessAsync(HttpClient client, string filter = "")
    {
        var page = await client.GetFromJsonAsync<LogPage<AccessLogItemDto>>($"/api/access-logs?from={Today}&to={Today}{filter}", Ct);
        return [.. page!.Items];
    }

    private static async Task<RoleDetailDto> CreateRoleAsync(HttpClient admin, string code) =>
        (await (await admin.PostAsJsonAsync("/api/roles",
            new CreateRoleRequest(code, "Vai trò ghi log", [Permissions.Inventory.StockView]), Ct)).Content
            .ReadFromJsonAsync<RoleDetailDto>(Ct))!;

    [Fact]
    public async Task TaoVaiTro_GhiAuditLogKemGiaTriMoi()
    {
        var admin = await AdminAtAsync("BHA");
        var code = UniqueCode("LOG");

        var role = await CreateRoleAsync(admin, code);

        var row = Assert.Single(await AuditAsync(admin, "&entityType=Role"), r => r.EntityLabel == code);
        Assert.Equal("Create", row.Action);
        Assert.Equal("Vai trò", row.EntityName);
        Assert.Equal(role.Id.ToString(), row.EntityId);
        Assert.Equal(ProcApiFactory.AdminUserName, row.UserName);

        var detail = await admin.GetFromJsonAsync<AuditLogDetailDto>($"/api/audit-logs/{row.Id}", Ct);
        var codeChange = Assert.Single(detail!.Changes, c => c.Field == "Code");
        Assert.Null(codeChange.OldValue);
        Assert.Equal(code, codeChange.NewValue);
        // Cột người tạo/sửa trùng với thông tin của chính dòng nhật ký nên không ghi lại.
        Assert.DoesNotContain(detail.Changes, c => c.Field is "CreatedAt" or "CreatedBy");
        Assert.False(string.IsNullOrEmpty(detail.CorrelationId));

        // Quyền của vai trò là bảng riêng, cũng được ghi.
        Assert.Contains(await AuditAsync(admin, "&entityType=RolePermission"),
            r => r.EntityId.StartsWith(role.Id.ToString()) && r.EntityLabel == Permissions.Inventory.StockView);
    }

    [Fact]
    public async Task SuaVaiTro_ChiGhiTruongThayDoi()
    {
        var admin = await AdminAtAsync("BHA");
        var code = UniqueCode("SUA");
        var role = await CreateRoleAsync(admin, code);

        await admin.PutAsJsonAsync($"/api/roles/{role.Id}",
            new UpdateRoleRequest("Tên mới", true, [Permissions.Inventory.StockView]), Ct);

        var row = Assert.Single(await AuditAsync(admin, "&entityType=Role&action=Update"), r => r.EntityLabel == code);
        Assert.Equal(["Name"], row.ChangedFields);
        var detail = await admin.GetFromJsonAsync<AuditLogDetailDto>($"/api/audit-logs/{row.Id}", Ct);
        Assert.Equal(new AuditFieldChangeDto("Name", "Vai trò ghi log", "Tên mới"), Assert.Single(detail!.Changes));
    }

    [Fact]
    public async Task DatLaiMatKhau_KhongGhiMaBamMatKhau()
    {
        var admin = await AdminAtAsync("BHA");
        var userName = UniqueName("log");
        var user = (await (await admin.PostAsJsonAsync("/api/users",
            new CreateUserRequest(userName, "Người ghi log", null, ProcApiFactory.TestPassword,
                new UserAccessDto([await _factory.GetRoleIdAsync("THUKHO")], [], [])), Ct)).Content
            .ReadFromJsonAsync<UserDetailDto>(Ct))!;

        await admin.PostAsJsonAsync($"/api/users/{user.Id}/reset-password", new ResetPasswordRequest("moimoi2026"), Ct);

        var rows = await AuditAsync(admin, $"&entityType=User");
        Assert.Contains(rows, r => r.EntityLabel == userName && r.Action == "Create");
        var update = Assert.Single(rows, r => r.EntityLabel == userName && r.Action == "Update");
        var detail = await admin.GetFromJsonAsync<AuditLogDetailDto>($"/api/audit-logs/{update.Id}", Ct);
        var password = Assert.Single(detail!.Changes, c => c.Field == "PasswordHash");
        Assert.Equal("***", password.OldValue);
        Assert.Equal("***", password.NewValue);
        Assert.DoesNotContain(detail.Changes, c => c.Field is "SecurityStamp" or "ConcurrencyStamp");

        // Gán vai trò khi tạo tài khoản cũng có dòng nhật ký riêng.
        Assert.Contains(await AuditAsync(admin, "&entityType=UserCompanyRole"), r => r.Action == "Create");
    }

    [Fact]
    public async Task AuditLog_KhongSuaKhongXoaDuoc()
    {
        await CreateRoleAsync(await AdminAtAsync("BHA"), UniqueCode("KHOA"));
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProcDbContext>();

        var update = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Database.ExecuteSqlRawAsync("""UPDATE "AuditLog" SET "Action" = 'Delete'""", Ct));
        var delete = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Database.ExecuteSqlRawAsync("""DELETE FROM "AuditLog" """, Ct));

        Assert.Contains("chỉ được thêm", update.MessageText);
        Assert.Contains("chỉ được thêm", delete.MessageText);
    }

    [Fact]
    public async Task AuditLog_LuuVaoPhanVungCuaThangHienTai()
    {
        await CreateRoleAsync(await AdminAtAsync("BHA"), UniqueCode("PV"));
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProcDbContext>();

        var partitions = await db.Database
            .SqlQuery<string>($"""SELECT DISTINCT tableoid::regclass::text AS "Value" FROM "AuditLog" """)
            .ToListAsync(Ct);

        var month = DateTime.UtcNow;
        Assert.Contains($"\"AuditLog_{month:yyyy_MM}\"", partitions);
        Assert.DoesNotContain("\"AuditLog_default\"", partitions);
    }

    [Fact]
    public async Task AuditLog_ChiThayCuaCongTyDangLamViec()
    {
        var code = UniqueCode("NMI");
        await CreateRoleAsync(await AdminAtAsync("NMI"), code);

        Assert.Contains(await AuditAsync(await AdminAtAsync("NMI"), "&entityType=Role"), r => r.EntityLabel == code);
        Assert.DoesNotContain(await AuditAsync(await AdminAtAsync("BHA"), "&entityType=Role"), r => r.EntityLabel == code);
    }

    [Fact]
    public async Task NhatKy_KhongCoQuyenXem_Tra403()
    {
        var thukho = await _factory.CreateClientInCompanyAsync("thukho.bha", "Demo@2026", "BHA");
        Assert.Equal(HttpStatusCode.Forbidden, (await thukho.GetAsync($"/api/audit-logs?from={Today}&to={Today}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await thukho.GetAsync($"/api/access-logs?from={Today}&to={Today}", Ct)).StatusCode);
    }

    [Fact]
    public async Task NhatKy_KhoangNgayNguoc_Tra400()
    {
        var admin = await AdminAtAsync("BHA");
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/audit-logs?from=2026-10-29&to=2026-10-01", Ct)).StatusCode);
    }

    [Fact]
    public async Task DangNhapSai_GhiAccessLog_TongCongTyThayTenKhongTonTai()
    {
        var ghost = UniqueName("khongco");
        await _factory.CreateClient().PostAsJsonAsync("/api/auth/login", new LoginRequest(ghost, "sai-mat-khau-1"), Ct);

        var atHeadOffice = await AccessAsync(await AdminAtAsync("TCT"), $"&userName={ghost}");
        var row = Assert.Single(atHeadOffice);
        Assert.Equal(AccessEvent.LoginFailed, row.Event);
        Assert.False(string.IsNullOrEmpty(row.IpAddress));
        // Công ty thành viên chỉ thấy nhật ký truy cập của người làm việc ở công ty mình.
        Assert.Empty(await AccessAsync(await AdminAtAsync("BHA"), $"&userName={ghost}"));
    }

    [Fact]
    public async Task DangNhap_DoiCongTy_DoiMatKhau_DangXuat_DeuGhiAccessLog()
    {
        var userName = UniqueName("truycap");
        await _factory.CreateScopedUserAsync(userName, "BHA", "THUKHO");
        var client = await _factory.CreateAuthenticatedClientAsync(userName, ProcApiFactory.TestPassword);
        var bha = await _factory.GetCompanyIdAsync("BHA");
        var nmi = await _factory.GetCompanyIdAsync("NMI");

        var switched = await client.PostAsJsonAsync("/api/auth/working-company", new SwitchCompanyRequest(bha), Ct);
        var notAllowed = await client.PostAsJsonAsync("/api/auth/working-company", new SwitchCompanyRequest(nmi), Ct);
        await client.PostAsJsonAsync("/api/auth/change-password", new ChangePasswordRequest("sai-mat-khau-1", "moimoi2026"), Ct);
        var logout = await client.PostAsync("/api/auth/logout", null, Ct);

        Assert.Equal(HttpStatusCode.NoContent, switched.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, notAllowed.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        var rows = await AccessAsync(await AdminAtAsync("BHA"), $"&userName={userName}");
        Assert.Equal(
            [AccessEvent.Logout, AccessEvent.PasswordChangeFailed, AccessEvent.CompanySwitched, AccessEvent.Login],
            rows.Select(r => r.Event));
        Assert.Equal("BHA", rows.Single(r => r.Event == AccessEvent.CompanySwitched).CompanyCode);
    }

    [Fact]
    public async Task DangNhapSaiNhieuLan_GhiBiKhoa()
    {
        var userName = UniqueName("khoalog");
        await _factory.CreateScopedUserAsync(userName, "BHA", "THUKHO");
        var anonymous = _factory.CreateClient();
        for (var i = 0; i < 5; i++)
            await anonymous.PostAsJsonAsync("/api/auth/login", new LoginRequest(userName, "sai-mat-khau-1"), Ct);

        var rows = await AccessAsync(await AdminAtAsync("BHA"), $"&userName={userName}");
        Assert.Equal(AccessEvent.LockedOut, rows[0].Event);
        Assert.Equal(4, rows.Count(r => r.Event == AccessEvent.LoginFailed));
    }
}
