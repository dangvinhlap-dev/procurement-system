using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Proc.Application.Operations;
using Proc.Domain.Organization;
using Proc.Infrastructure.Persistence;

namespace Proc.IntegrationTests;

/// <summary>
/// Lát QT5 – chế độ bảo trì (ke-hoach-qt.md QT5): bật thì người dùng thường nhận 503 kèm câu thông báo, người có quyền <c>System.Ops*</c> vẫn vào;
/// đăng nhập, /health, API môi trường vẫn chạy và báo bảo trì; bật / tắt cần lý do và quyền riêng.
/// </summary>
[Collection(ApiCollection.Name)]
public class MaintenanceModeTests
{
    private const string ModePath = "/api/ops/v1/maintenance-mode";

    private readonly ProcApiFactory _factory;

    public MaintenanceModeTests(ProcApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(Ct)}");
        return (await response.Content.ReadFromJsonAsync<T>(Ct))!;
    }

    /// <summary>
    /// Thiếu lý do → 400; quyền xem vận hành không bật được (403). Bật: API môi trường ẩn danh có câu thông báo; người dùng thường vẫn đăng nhập được
    /// nhưng gọi API nhận 503 (code "maintenance", câu thông báo); admin vào bình thường; /health 200. Tắt: người dùng thường vào lại; nhật ký vận
    /// hành có 2 dòng bật / tắt.
    /// </summary>
    [Fact]
    public async Task BatTat_ChanNguoiThuong_ChoQuanTriVaoDuoc()
    {
        var head = await _factory.CreateClientInCompanyAsync(ProcApiFactory.AdminUserName, ProcApiFactory.AdminPassword, "TCT");
        var role = $"U{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        await _factory.CreateRoleAsync(role, Permissions.MasterData.ItemView);
        var name = $"mm.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(name, "TCT", role);
        var opsRole = $"V{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        await _factory.CreateRoleAsync(opsRole, Permissions.System.OpsView);
        var opsName = $"mo.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(opsName, "TCT", opsRole);
        var opsViewer = await _factory.CreateClientInCompanyAsync(opsName, ProcApiFactory.TestPassword, "TCT");

        Assert.Equal(HttpStatusCode.BadRequest,
            (await head.PutAsJsonAsync(ModePath, new SetMaintenanceModeRequest(true, null, "abc"), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await opsViewer.PutAsJsonAsync(ModePath, new SetMaintenanceModeRequest(true, null, "Cài bản mới"), Ct)).StatusCode);

        var on = await ReadAsync<MaintenanceModeDto>(await head.PutAsJsonAsync(ModePath,
            new SetMaintenanceModeRequest(true, "Đang cài bản 0.34.0, quay lại sau 15 phút.", "Cài bản 0.34.0"), Ct));
        try
        {
            Assert.True(on.IsOn);
            Assert.Equal(ProcApiFactory.AdminUserName, on.ChangedByName);

            var anonymous = _factory.CreateClient();
            var environment = await ReadAsync<EnvironmentStatusDto>(await anonymous.GetAsync("/api/ops/v1/environment", Ct));
            Assert.Equal("Đang cài bản 0.34.0, quay lại sau 15 phút.", environment.Maintenance?.Message);
            Assert.Equal(HttpStatusCode.OK, (await anonymous.GetAsync("/health", Ct)).StatusCode);

            var user = await _factory.CreateClientInCompanyAsync(name, ProcApiFactory.TestPassword, "TCT");
            var blocked = await user.GetAsync("/api/notifications", Ct);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, blocked.StatusCode);
            using var problem = JsonDocument.Parse(await blocked.Content.ReadAsStringAsync(Ct));
            Assert.Equal("maintenance", problem.RootElement.GetProperty("code").GetString());
            Assert.Equal("Đang cài bản 0.34.0, quay lại sau 15 phút.", problem.RootElement.GetProperty("title").GetString());
            Assert.Equal(HttpStatusCode.OK, (await user.GetAsync("/api/auth/me", Ct)).StatusCode);

            Assert.Equal(HttpStatusCode.OK, (await head.GetAsync("/api/notifications", Ct)).StatusCode);
            Assert.True((await ReadAsync<MaintenanceModeDto>(await opsViewer.GetAsync(ModePath, Ct))).IsOn);
        }
        finally
        {
            await ReadAsync<MaintenanceModeDto>(await head.PutAsJsonAsync(ModePath, new SetMaintenanceModeRequest(false, null, "Cài xong bản mới"), Ct));
        }

        var again = await _factory.CreateClientInCompanyAsync(name, ProcApiFactory.TestPassword, "TCT");
        Assert.Equal(HttpStatusCode.OK, (await again.GetAsync("/api/notifications", Ct)).StatusCode);
        Assert.Null((await ReadAsync<EnvironmentStatusDto>(await _factory.CreateClient().GetAsync("/api/ops/v1/environment", Ct))).Maintenance);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await head.PutAsJsonAsync(ModePath, new SetMaintenanceModeRequest(false, null, "Tắt lần hai"), Ct)).StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProcDbContext>();
        var reasons = await db.OperationLogs.Where(l => l.Kind == OperationKinds.MaintenanceModeChanged)
            .OrderByDescending(l => l.OccurredAt).Take(2).Select(l => l.Reason).ToListAsync(Ct);
        Assert.Equal(["Cài xong bản mới", "Cài bản 0.34.0"], reasons);
    }
}
