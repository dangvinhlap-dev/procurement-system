using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure;

/// <summary>
/// Đăng ký các dịch vụ của lớp Infrastructure vào DI container.
/// </summary>
public static class DependencyInjection
{
    /// <summary>Tên health check kiểm tra kết nối PostgreSQL.</summary>
    public const string DatabaseHealthCheckName = "database";

    /// <summary>
    /// Đăng ký <see cref="ProcDbContext"/> dùng PostgreSQL (Npgsql) và health check kết nối CSDL.
    /// </summary>
    /// <param name="services">DI container.</param>
    /// <param name="connectionString">Chuỗi kết nối PostgreSQL.</param>
    /// <returns>Chính <paramref name="services"/> để gọi nối tiếp.</returns>
    /// <exception cref="ArgumentException">Khi chuỗi kết nối rỗng.</exception>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddDbContext<ProcDbContext>(options => options.UseNpgsql(connectionString));

        // Health check thực sự mở kết nối tới CSDL, không chỉ kiểm tra cấu hình.
        services.AddHealthChecks()
            .AddDbContextCheck<ProcDbContext>(DatabaseHealthCheckName);

        return services;
    }
}
