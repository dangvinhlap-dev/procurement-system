using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Proc.Application.Administration;
using Proc.Application.Auth;
using Proc.Application.Common;
using Proc.Application.Documents;
using Proc.Application.Logging;
using Proc.Application.Organization;
using Proc.Infrastructure.Administration;
using Proc.Infrastructure.Documents;
using Proc.Infrastructure.Identity;
using Proc.Infrastructure.Logging;
using Proc.Infrastructure.Persistence;
using Proc.Infrastructure.Security;

namespace Proc.Infrastructure;

/// <summary>
/// Đăng ký các dịch vụ của lớp Infrastructure vào DI container.
/// </summary>
public static class DependencyInjection
{
    /// <summary>Tên health check kiểm tra kết nối PostgreSQL.</summary>
    public const string DatabaseHealthCheckName = "database";

    /// <summary>Số lần nhập sai mật khẩu liên tiếp trước khi khóa tạm tài khoản.</summary>
    public const int MaxFailedLoginAttempts = 5;

    /// <summary>Thời gian khóa tạm sau khi nhập sai quá số lần cho phép.</summary>
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Đăng ký DbContext (PostgreSQL), Identity, dịch vụ đăng nhập, đồng hồ và health check CSDL.
    /// Cấu hình JwtOptions, DatabaseOptions, SeedOptions do lớp Api bind từ appsettings.
    /// </summary>
    /// <param name="services">DI container.</param>
    /// <param name="connectionString">Chuỗi kết nối PostgreSQL.</param>
    /// <returns>Chính <paramref name="services"/> để gọi nối tiếp.</returns>
    /// <exception cref="ArgumentException">Khi chuỗi kết nối rỗng.</exception>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.TryAddSingleton<IClock, SystemClock>();
        // Api thay bằng người dùng lấy từ token; khi chạy ngoài HTTP (khởi tạo dữ liệu) thì không có người dùng.
        services.TryAddScoped<ICurrentUser, NoCurrentUser>();
        services.TryAddScoped<IRequestContext, NoRequestContext>();
        services.AddScoped<AuditFieldsInterceptor>();
        services.AddScoped<AuditLogInterceptor>();
        services.AddScoped<IAccessLog, AccessLogWriter>();
        services.AddScoped<ILogQueries, LogQueries>();
        services.AddScoped<IDocumentNumberService, DocumentNumberService>();

        // Một đối tượng phạm vi cho mỗi request: UserScopeLoader nạp, DbContext (filter) và Application đọc.
        services.AddScoped<CurrentUserScope>();
        services.AddScoped<ICurrentUserScope>(sp => sp.GetRequiredService<CurrentUserScope>());
        services.AddScoped<UserScopeLoader>();
        services.AddScoped<IOrganizationQueries, OrganizationQueries>();
        services.AddScoped<IUserAdministration, UserAdministration>();
        services.AddScoped<IRoleAdministration, RoleAdministration>();

        services.AddDbContext<ProcDbContext>((sp, options) => options
            .UseNpgsql(connectionString)
            // Thứ tự quan trọng: điền CreatedAt/UpdatedAt trước, rồi mới sinh nhật ký.
            .AddInterceptors(sp.GetRequiredService<AuditFieldsInterceptor>(), sp.GetRequiredService<AuditLogInterceptor>()));

        services.AddIdentityCore<AppUser>(options =>
            {
                // Quy tắc mật khẩu: tối thiểu 8 ký tự, có chữ thường và chữ số; không bắt buộc chữ hoa/ký tự đặc biệt
                // để người dùng dễ nhớ mà vẫn đủ mạnh khi kết hợp khóa tạm.
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;

                // Khóa tạm khi đăng nhập sai nhiều lần (design.md mục 2.2: "khóa tài khoản").
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = MaxFailedLoginAttempts;
                options.Lockout.DefaultLockoutTimeSpan = LockoutDuration;

                options.User.RequireUniqueEmail = false;
            })
            .AddEntityFrameworkStores<ProcDbContext>()
            .AddErrorDescriber<VietnameseIdentityErrorDescriber>();

        services.AddScoped<JwtTokenService>();
        services.AddScoped<IAuthService, AuthService>();

        // Health check thực sự mở kết nối tới CSDL, không chỉ kiểm tra cấu hình.
        services.AddHealthChecks()
            .AddDbContextCheck<ProcDbContext>(DatabaseHealthCheckName);

        return services;
    }

    /// <summary>Đồng hồ thật của hệ thống.</summary>
    private sealed class SystemClock : IClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    }

    /// <summary>Không có người dùng (chạy nền).</summary>
    private sealed class NoCurrentUser : ICurrentUser
    {
        public Guid? UserId => null;

        public string? UserName => null;
    }

    /// <summary>Không có request HTTP (chạy nền).</summary>
    private sealed class NoRequestContext : IRequestContext
    {
        public string? IpAddress => null;

        public string? UserAgent => null;

        public string? CorrelationId => null;
    }
}
