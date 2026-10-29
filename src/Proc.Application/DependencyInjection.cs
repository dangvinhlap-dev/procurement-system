using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Proc.Application;

/// <summary>
/// Đăng ký các dịch vụ của lớp Application vào DI container.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Đăng ký mọi validator (FluentValidation) trong assembly Application.
    /// </summary>
    /// <param name="services">DI container.</param>
    /// <returns>Chính <paramref name="services"/> để gọi nối tiếp.</returns>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
        return services;
    }
}
