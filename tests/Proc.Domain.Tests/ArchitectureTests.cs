using System.Reflection;
using Proc.Application;
using Proc.Domain;

namespace Proc.Domain.Tests;

/// <summary>
/// Kiểm tra ràng buộc phụ thuộc giữa các lớp Clean Architecture (design.md mục 3.1):
/// Domain không tham chiếu EF Core/HTTP; Application không tham chiếu hạ tầng cụ thể.
/// </summary>
public class ArchitectureTests
{
    [Fact]
    public void Domain_KhongThamChieuHaTangHoacHttp()
    {
        var forbidden = new[] { "Microsoft.EntityFrameworkCore", "Npgsql", "Microsoft.AspNetCore", "Proc.Application", "Proc.Infrastructure", "Proc.Api" };

        var violations = ReferencedNames(typeof(DomainAssembly).Assembly)
            .Where(name => forbidden.Any(f => name.StartsWith(f, StringComparison.Ordinal)))
            .ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void Application_KhongThamChieuInfrastructureHoacApi()
    {
        // Application chỉ khai báo interface; SQL và EF Core cụ thể nằm ở Infrastructure.
        var forbidden = new[] { "Npgsql", "Microsoft.AspNetCore", "Proc.Infrastructure", "Proc.Api" };

        var violations = ReferencedNames(typeof(ApplicationAssembly).Assembly)
            .Where(name => forbidden.Any(f => name.StartsWith(f, StringComparison.Ordinal)))
            .ToList();

        Assert.Empty(violations);
    }

    private static IEnumerable<string> ReferencedNames(Assembly assembly) =>
        assembly.GetReferencedAssemblies().Select(a => a.Name ?? string.Empty);
}
