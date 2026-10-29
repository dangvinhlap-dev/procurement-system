using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Proc.Application.Common;
using Proc.Domain.Organization;
using Proc.Infrastructure.Identity;

namespace Proc.Infrastructure.Persistence;

/// <summary>
/// Tùy chọn khởi tạo CSDL, đọc từ mục "Database" của appsettings.
/// </summary>
public sealed class DatabaseOptions
{
    /// <summary>
    /// Tự chạy migration khi khởi động. Chỉ bật ở Development/Testing; server thật nâng cấp CSDL bằng script
    /// trong db/scripts sau khi đã sao lưu (CLAUDE.md mục 8).
    /// </summary>
    public bool MigrateOnStartup { get; set; }
}

/// <summary>Một công ty cần tạo sẵn.</summary>
public sealed class SeedCompany
{
    /// <summary>Mã công ty.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Tên công ty.</summary>
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// Dữ liệu tạo sẵn để đăng nhập lần đầu, đọc từ mục "Seed" của appsettings. Mật khẩu trong appsettings.Development.json
/// chỉ dùng cho máy dev; server thật đặt qua biến môi trường Seed__AdminPassword rồi đổi ngay sau lần đăng nhập đầu.
/// </summary>
public sealed class SeedOptions
{
    /// <summary>Bật tạo dữ liệu khi khởi động.</summary>
    public bool Enabled { get; set; }

    /// <summary>Tên đăng nhập quản trị.</summary>
    public string AdminUserName { get; set; } = "admin";

    /// <summary>Họ tên quản trị.</summary>
    public string AdminFullName { get; set; } = "Quản trị hệ thống";

    /// <summary>Mật khẩu quản trị ban đầu.</summary>
    public string AdminPassword { get; set; } = string.Empty;

    /// <summary>Tổng công ty.</summary>
    public SeedCompany HeadOffice { get; set; } = new() { Code = "TCT", Name = "Tổng công ty" };

    /// <summary>Công ty thành viên mẫu – chỉ khai báo ở môi trường dev/test.</summary>
    public List<SeedCompany> DemoCompanies { get; set; } = [];
}

/// <summary>
/// Chạy migration và tạo dữ liệu ban đầu khi API khởi động (tùy cấu hình).
/// </summary>
public static class DatabaseInitializer
{
    /// <summary>
    /// Chạy migration (nếu Database:MigrateOnStartup) rồi tạo dữ liệu ban đầu (nếu Seed:Enabled).
    /// Gọi nhiều lần không sinh dữ liệu trùng.
    /// </summary>
    /// <param name="services">Service provider gốc của ứng dụng.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Task hoàn tất khi xong.</returns>
    /// <exception cref="InvalidOperationException">Khi không tạo được tài khoản quản trị (ví dụ mật khẩu không đạt quy tắc).</exception>
    public static async Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<ProcDbContext>();

        if (sp.GetRequiredService<IOptions<DatabaseOptions>>().Value.MigrateOnStartup)
            await db.Database.MigrateAsync(cancellationToken);

        var seed = sp.GetRequiredService<IOptions<SeedOptions>>().Value;
        if (seed.Enabled)
            await SeedAsync(db, sp.GetRequiredService<UserManager<AppUser>>(), sp.GetRequiredService<IClock>(), seed, cancellationToken);
    }

    /// <summary>
    /// Tạo tổng công ty, các công ty mẫu, vai trò Quản trị hệ thống và tài khoản quản trị được gán vào mọi công ty đó.
    /// </summary>
    /// <param name="db">DbContext.</param>
    /// <param name="userManager">Quản lý tài khoản.</param>
    /// <param name="clock">Đồng hồ.</param>
    /// <param name="seed">Cấu hình dữ liệu tạo sẵn.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Task hoàn tất khi xong.</returns>
    /// <exception cref="InvalidOperationException">Khi không tạo được tài khoản quản trị.</exception>
    public static async Task SeedAsync(
        ProcDbContext db,
        UserManager<AppUser> userManager,
        IClock clock,
        SeedOptions seed,
        CancellationToken cancellationToken)
    {
        var companies = new List<Company>();
        foreach (var (item, isHeadOffice) in new[] { (seed.HeadOffice, true) }.Concat(seed.DemoCompanies.Select(c => (c, false))))
        {
            var code = item.Code.Trim().ToUpperInvariant();
            var company = await db.Companies.FirstOrDefaultAsync(c => c.Code == code, cancellationToken);
            if (company is null)
            {
                company = Company.Create(code, item.Name, isHeadOffice);
                db.Companies.Add(company);
            }

            companies.Add(company);
        }

        var admin = await db.Roles.FirstOrDefaultAsync(r => r.Code == Role.AdminCode, cancellationToken);
        if (admin is null)
        {
            admin = Role.Create(Role.AdminCode, "Quản trị hệ thống");
            db.Roles.Add(admin);
        }

        await db.SaveChangesAsync(cancellationToken);

        var user = await userManager.FindByNameAsync(seed.AdminUserName);
        if (user is null)
        {
            user = new AppUser
            {
                UserName = seed.AdminUserName,
                FullName = seed.AdminFullName,
                DefaultCompanyId = companies[0].Id,
                CreatedAt = clock.UtcNow,
            };
            var created = await userManager.CreateAsync(user, seed.AdminPassword);
            if (!created.Succeeded)
            {
                throw new InvalidOperationException(
                    "Không tạo được tài khoản quản trị: " + string.Join("; ", created.Errors.Select(e => e.Description)));
            }
        }

        var assigned = await db.UserCompanyRoles
            .Where(x => x.UserId == user.Id && x.RoleId == admin.Id)
            .Select(x => x.CompanyId)
            .ToListAsync(cancellationToken);
        foreach (var company in companies.Where(c => !assigned.Contains(c.Id)))
            db.UserCompanyRoles.Add(UserCompanyRole.Assign(user.Id, company.Id, admin.Id));

        await db.SaveChangesAsync(cancellationToken);
    }
}
