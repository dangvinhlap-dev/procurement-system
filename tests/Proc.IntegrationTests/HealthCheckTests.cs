using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Proc.Infrastructure.Persistence;

namespace Proc.IntegrationTests;

/// <summary>
/// Test tích hợp trên PostgreSQL thật (database procurement_test): kết nối CSDL và endpoint /health.
/// </summary>
public class HealthCheckTests : IClassFixture<ProcApiFactory>
{
    private readonly ProcApiFactory _factory;

    public HealthCheckTests(ProcApiFactory factory) => _factory = factory;

    [Fact]
    public async Task DbContext_KetNoiDungDatabaseTest()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProcDbContext>();
        var ct = TestContext.Current.CancellationToken;

        Assert.True(await db.Database.CanConnectAsync(ct));

        var database = await db.Database
            .SqlQuery<string>($"SELECT current_database() AS \"Value\"")
            .SingleAsync(ct);
        Assert.Equal(ProcApiFactory.ExpectedDatabase, database);
    }

    [Fact]
    public async Task Health_TraVeHealthyKhiCsdlKetNoiDuoc()
    {
        var client = _factory.CreateClient();
        var ct = TestContext.Current.CancellationToken;

        var response = await client.GetAsync("/health", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<HealthBody>(ct);
        Assert.NotNull(body);
        Assert.Equal("Healthy", body.Status);
        // Phiên bản lấy từ Directory.Build.props, không ghi cứng để test không phải sửa mỗi lần phát hành.
        var v = typeof(Program).Assembly.GetName().Version!;
        Assert.Equal($"{v.Major}.{v.Minor}.{v.Build}", body.Version);
        Assert.Contains(body.Checks, c => c.Name == "database" && c.Status == "Healthy");
    }

    private sealed record HealthBody(string Status, string Version, List<HealthCheckBody> Checks);

    private sealed record HealthCheckBody(string Name, string Status);
}
