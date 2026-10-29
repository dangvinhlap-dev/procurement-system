using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Proc.Application.Auth;
using Proc.Application.Common;
using Proc.Domain.Organization;
using Proc.Infrastructure.Identity;
using Proc.Infrastructure.Persistence;
using Proc.Infrastructure.Security;

namespace Proc.IntegrationTests;

/// <summary>
/// Dựng API trong bộ nhớ ở môi trường "Testing", trỏ tới database procurement_test (appsettings.Testing.json).
/// Có thể đổi chuỗi kết nối qua biến môi trường <c>ConnectionStrings__Procurement</c> (ví dụ trên máy CI).
/// Khi bắt đầu: chạy migration, xóa sạch dữ liệu rồi tạo lại dữ liệu mẫu (admin + 4 công ty).
/// </summary>
public sealed class ProcApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    /// <summary>Tên database bắt buộc khi chạy test – chặn test vô tình chạy trên CSDL dev hoặc thật.</summary>
    public const string ExpectedDatabase = "procurement_test";

    /// <summary>Tài khoản quản trị tạo sẵn (appsettings.Testing.json).</summary>
    public const string AdminUserName = "admin";

    /// <summary>Mật khẩu quản trị tạo sẵn.</summary>
    public const string AdminPassword = "Admin@2026";

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
    }

    /// <summary>Khởi động host (migration + seed) rồi đưa dữ liệu về trạng thái sạch.</summary>
    public async ValueTask InitializeAsync()
    {
        _ = Services;
        await ResetDatabaseAsync();
    }

    /// <summary>
    /// Xóa toàn bộ dữ liệu (giữ cấu trúc bảng và lịch sử migration) rồi tạo lại dữ liệu mẫu.
    /// </summary>
    /// <exception cref="InvalidOperationException">Khi đang trỏ tới database khác procurement_test.</exception>
    public async Task ResetDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProcDbContext>();

        var database = await db.Database.SqlQuery<string>($"SELECT current_database() AS \"Value\"").SingleAsync();
        if (database != ExpectedDatabase)
            throw new InvalidOperationException($"Test chỉ được chạy trên {ExpectedDatabase}, đang trỏ tới {database}.");

        var tables = await db.Database
            .SqlQuery<string>($"""
                SELECT format('%I.%I', table_schema, table_name) AS "Value"
                FROM information_schema.tables
                WHERE table_schema = 'public' AND table_type = 'BASE TABLE' AND table_name <> '__EFMigrationsHistory'
                """)
            .ToListAsync();
        if (tables.Count > 0)
        {
            // Tên bảng lấy từ information_schema và đã được format('%I') đặt trong ngoặc kép, không phải dữ liệu người dùng.
#pragma warning disable EF1002
            await db.Database.ExecuteSqlRawAsync($"TRUNCATE {string.Join(", ", tables)} RESTART IDENTITY CASCADE");
#pragma warning restore EF1002
        }

        await DatabaseInitializer.SeedAsync(scope.ServiceProvider, CancellationToken.None);
    }

    /// <summary>
    /// Tạo tài khoản thử, gán vai trò ADMIN tại các công ty cho trước.
    /// </summary>
    /// <param name="userName">Tên đăng nhập (nên duy nhất trong mỗi test).</param>
    /// <param name="password">Mật khẩu.</param>
    /// <param name="companyCodes">Mã công ty được gán.</param>
    /// <param name="isActive">Tài khoản còn dùng.</param>
    /// <returns>Id tài khoản.</returns>
    public async Task<Guid> CreateUserAsync(string userName, string password, string[] companyCodes, bool isActive = true)
    {
        using var scope = Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var db = scope.ServiceProvider.GetRequiredService<ProcDbContext>();

        var user = new AppUser { UserName = userName, FullName = $"Người thử {userName}", IsActive = isActive, CreatedAt = DateTimeOffset.UtcNow };
        var result = await users.CreateAsync(user, password);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));

        var role = await db.Roles.SingleAsync(r => r.Code == Role.AdminCode);
        var companies = await db.Companies.Where(c => companyCodes.Contains(c.Code)).ToListAsync();
        foreach (var company in companies)
            db.UserCompanyRoles.Add(UserCompanyRole.Assign(user.Id, company.Id, role.Id));
        await db.SaveChangesAsync();
        return user.Id;
    }

    /// <summary>
    /// Tạo tài khoản thử với một vai trò tại một công ty, kèm phạm vi kho và nhóm hàng (trống = tất cả).
    /// </summary>
    /// <param name="userName">Tên đăng nhập.</param>
    /// <param name="companyCode">Mã công ty.</param>
    /// <param name="roleCode">Mã vai trò (đã có).</param>
    /// <param name="warehouseCodes">Mã kho được phép tại công ty.</param>
    /// <param name="categoryCodes">Mã nhóm hàng được phép tại công ty.</param>
    /// <returns>Id tài khoản.</returns>
    public async Task<Guid> CreateScopedUserAsync(
        string userName,
        string companyCode,
        string roleCode,
        string[]? warehouseCodes = null,
        string[]? categoryCodes = null)
    {
        using var scope = Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var db = scope.ServiceProvider.GetRequiredService<ProcDbContext>();

        var user = new AppUser { UserName = userName, FullName = $"Người thử {userName}", CreatedAt = DateTimeOffset.UtcNow };
        var result = await users.CreateAsync(user, TestPassword);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));

        var company = await db.Companies.SingleAsync(c => c.Code == companyCode);
        var role = await db.Roles.SingleAsync(r => r.Code == roleCode);
        db.UserCompanyRoles.Add(UserCompanyRole.Assign(user.Id, company.Id, role.Id));

        // Đọc kho/nhóm hàng qua global query filter như mọi truy vấn khác: mở phạm vi của đúng công ty này.
        scope.ServiceProvider.GetRequiredService<CurrentUserScope>()
            .Initialize(Guid.Empty, company.Id, [], allWarehouses: true, [], allItemCategories: true, []);
        foreach (var code in warehouseCodes ?? [])
            db.UserWarehouseScopes.Add(UserWarehouseScope.Allow(user.Id, await db.Warehouses.SingleAsync(w => w.Code == code)));
        foreach (var code in categoryCodes ?? [])
        {
            var category = await db.ItemCategories.SingleAsync(c => c.Code == code);
            db.UserItemCategoryScopes.Add(UserItemCategoryScope.Allow(user.Id, company.Id, category.Id));
        }

        await db.SaveChangesAsync();
        return user.Id;
    }

    /// <summary>
    /// Tạo vai trò thử với danh sách quyền cho trước (bỏ qua nếu mã đã có).
    /// </summary>
    /// <param name="code">Mã vai trò.</param>
    /// <param name="permissions">Mã quyền.</param>
    /// <returns>Id vai trò.</returns>
    public async Task<Guid> CreateRoleAsync(string code, params string[] permissions)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProcDbContext>();
        var role = Role.Create(code, $"Vai trò thử {code}");
        db.Roles.Add(role);
        foreach (var permission in permissions)
            db.RolePermissions.Add(RolePermission.Grant(role.Id, permission));
        await db.SaveChangesAsync();
        return role.Id;
    }

    /// <summary>
    /// Lấy Id vai trò theo mã.
    /// </summary>
    /// <param name="code">Mã vai trò.</param>
    /// <returns>Id vai trò.</returns>
    public async Task<Guid> GetRoleIdAsync(string code)
    {
        using var scope = Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ProcDbContext>()
            .Roles.Where(r => r.Code == code).Select(r => r.Id).SingleAsync();
    }

    /// <summary>Mật khẩu dùng cho tài khoản tạo trong test.</summary>
    public const string TestPassword = "matkhau123";

    /// <summary>
    /// Lấy Id công ty theo mã.
    /// </summary>
    /// <param name="code">Mã công ty.</param>
    /// <returns>Id công ty.</returns>
    public async Task<Guid> GetCompanyIdAsync(string code)
    {
        using var scope = Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ProcDbContext>()
            .Companies.Where(c => c.Code == code).Select(c => c.Id).SingleAsync();
    }

    /// <summary>
    /// Đăng nhập và trả HttpClient đã gắn token, kèm header X-Company-Id nếu có mã công ty.
    /// </summary>
    /// <param name="userName">Tên đăng nhập.</param>
    /// <param name="password">Mật khẩu.</param>
    /// <param name="companyCode">Mã công ty làm việc; null thì không gửi header.</param>
    /// <returns>Client đã đăng nhập.</returns>
    public async Task<HttpClient> CreateClientInCompanyAsync(string userName, string password, string? companyCode)
    {
        var client = await CreateAuthenticatedClientAsync(userName, password);
        if (companyCode is not null)
            client.DefaultRequestHeaders.Add("X-Company-Id", (await GetCompanyIdAsync(companyCode)).ToString());
        return client;
    }

    /// <summary>
    /// Đăng nhập và trả HttpClient đã gắn token.
    /// </summary>
    /// <param name="userName">Tên đăng nhập.</param>
    /// <param name="password">Mật khẩu.</param>
    /// <returns>Client có header Authorization: Bearer.</returns>
    public async Task<HttpClient> CreateAuthenticatedClientAsync(string userName = AdminUserName, string password = AdminPassword)
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(userName, password));
        response.EnsureSuccessStatusCode();
        var login = await response.Content.ReadFromJsonAsync<LoginResult>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.AccessToken);
        return client;
    }
}

/// <summary>
/// Mọi test tích hợp dùng chung một API và chạy tuần tự, vì cùng ghi vào database procurement_test.
/// </summary>
[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ProcApiFactory>
{
    /// <summary>Tên collection.</summary>
    public const string Name = "Api";
}
