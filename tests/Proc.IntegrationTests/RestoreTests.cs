using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Proc.Application.Operations;
using Proc.Domain.Operations;
using Proc.Domain.Organization;
using Proc.Infrastructure.Operations;
using Proc.Infrastructure.Persistence;

namespace Proc.IntegrationTests;

/// <summary>
/// Lát QT6 – phục hồi (ke-hoach-qt.md QT6, design.md mục 21.4 RST): khôi phục pg_dump thật sang CSDL tên tạm + so sánh số dòng (không hoán đổi
/// CSDL test), mỗi lúc một lần phục hồi, chuẩn bị hoán đổi qua bộ thực thi (bật bảo trì, chép bảng vận hành sang bản khôi phục), xác nhận Q6, bỏ
/// bản khôi phục (xóa CSDL), kế hoạch về mốc thời gian khi chưa khai thư mục WAL, quyền.
/// </summary>
[Collection(ApiCollection.Name)]
public class RestoreTests
{
    private const string RestorePath = "/api/ops/v1/restore";
    private const string AgentPath = "/api/ops/v1/agent/commands";

    private readonly ProcApiFactory _factory;

    public RestoreTests(ProcApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(Ct)}");
        return (await response.Content.ReadFromJsonAsync<T>(Ct))!;
    }

    private static async Task<string> FailAsync(HttpResponseMessage response, HttpStatusCode expected)
    {
        var body = await response.Content.ReadAsStringAsync(Ct);
        Assert.True(response.StatusCode == expected, $"{(int)response.StatusCode}: {body}");
        return body;
    }

    private Task<HttpClient> HeadAsync() =>
        _factory.CreateClientInCompanyAsync(ProcApiFactory.AdminUserName, ProcApiFactory.AdminPassword, "TCT");

    /// <summary>Xóa lần phục hồi, sao lưu, bộ thực thi của test trước; xóa CSDL khôi phục còn sót.</summary>
    private async Task ClearAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProcDbContext>();
        var main = db.Database.GetConnectionString()!;
        foreach (var name in await db.RestoreRuns.Select(r => r.RestoreDatabase).ToListAsync(Ct))
            if (name != ProcApiFactory.ExpectedDatabase) await RestoreDatabases.DropAsync(main, name, ProcApiFactory.ExpectedDatabase, Ct);
        await db.RestoreRuns.ExecuteDeleteAsync(Ct);
        await db.BackupRuns.ExecuteDeleteAsync(Ct);
        await db.AgentCommands.ExecuteDeleteAsync(Ct);
        await db.AgentEnvironments.ExecuteDeleteAsync(Ct);
    }

    private static string Range => $"from={DateTime.UtcNow.AddDays(-1):yyyy-MM-dd}&to={DateTime.UtcNow.AddDays(1):yyyy-MM-dd}";

    /// <summary>Bản pg_dump thật của procurement_test (qua hàng chờ sao lưu).</summary>
    private async Task<Guid> DumpAsync(HttpClient head)
    {
        var queued = await ReadAsync<BackupRunDto>(await head.PostAsJsonAsync("/api/ops/v1/backup/runs", new QueueBackupRequest(BackupKind.Dump), Ct));
        Assert.Equal(1, await _factory.Services.GetRequiredService<IBackupExecutor>().ProcessQueueAsync(Ct));
        var run = await ReadAsync<BackupRunDto>(await head.GetAsync($"/api/ops/v1/backup/runs/{queued.Id}", Ct));
        Assert.True(run.Status == "Succeeded" && run.Verified == true, $"{run.Status}: {run.ErrorMessage} {run.VerifyMessage}");
        return run.Id;
    }

    private bool HasTools()
    {
        var tools = _factory.Services.GetRequiredService<PgToolRunner>();
        return File.Exists(tools.Resolve("pg_dump")) && File.Exists(tools.Resolve("pg_restore"));
    }

    /// <summary>
    /// pg_dump thật → khôi phục sang <c>procurement_test_restore_…</c> (CSDL test không bị đụng): đặt lần thứ hai khi lần đầu chưa kết thúc → 400;
    /// bộ chạy khôi phục xong → Chờ kiểm tra, bảng so sánh có bảng nghiệp vụ bằng số dòng, bảng vận hành đánh dấu riêng, cùng phiên bản CSDL nên hoán
    /// đổi được; CSDL khôi phục có thật, chủ là tài khoản ứng dụng. So sánh lại được. Bỏ → CSDL bị xóa, trạng thái Đã bỏ; nhật ký vận hành đủ dòng.
    /// </summary>
    [Fact]
    public async Task KhoiPhucPgDump_SoSanh_Bo()
    {
        if (!HasTools()) Assert.Skip("Máy test không có pg_dump / pg_restore – bỏ qua test chạy thật.");
        await ClearAsync();
        var head = await HeadAsync();
        var dumpId = await DumpAsync(head);

        var overview = await ReadAsync<RestoreOverviewDto>(await head.GetAsync($"{RestorePath}/overview", Ct));
        Assert.Equal(ProcApiFactory.ExpectedDatabase, overview.LiveDatabase);
        Assert.Contains(overview.Dumps, d => d.Id == dumpId && d.FileExists);
        Assert.False(overview.AgentReady);
        Assert.Contains("procurement", overview.OtherDatabases); // máy dev có CSDL procurement cạnh procurement_test

        await FailAsync(await head.PostAsJsonAsync($"{RestorePath}/runs", new QueueRestoreRequest(RestoreKind.Dump, dumpId, null, "abc"), Ct),
            HttpStatusCode.BadRequest);
        var queued = await ReadAsync<RestoreRunDto>(await head.PostAsJsonAsync($"{RestorePath}/runs",
            new QueueRestoreRequest(RestoreKind.Dump, dumpId, null, "Khôi phục thử bản đêm qua"), Ct));
        Assert.Equal(("Queued", "Đang chờ", "Từ pg_dump"), (queued.Status, queued.StatusLabel, queued.KindLabel));
        Assert.StartsWith("procurement_test_restore_", queued.RestoreDatabase, StringComparison.Ordinal);
        var again = await FailAsync(await head.PostAsJsonAsync($"{RestorePath}/runs",
            new QueueRestoreRequest(RestoreKind.Dump, dumpId, null, "Khôi phục lần hai"), Ct), HttpStatusCode.BadRequest);
        Assert.Contains("chưa kết thúc", again);

        Assert.Equal(1, await _factory.Services.GetRequiredService<IRestoreExecutor>().ProcessQueueAsync(Ct));
        var run = await ReadAsync<RestoreRunDto>(await head.GetAsync($"{RestorePath}/runs/{queued.Id}", Ct));
        Assert.True(run.Status == "Ready", $"{run.Status}: {run.ErrorMessage}");
        Assert.Equal(("Chờ kiểm tra", "Warning", true), (run.StatusLabel, run.Light, run.CanSwap));
        Assert.NotNull(run.Comparison);
        Assert.Equal(run.Comparison.LiveMigration, run.Comparison.RestoreMigration);
        Assert.Contains(run.Comparison.Rows, r => r is { Table: "Item", Light: "Ok" } && r.LiveRows == r.RestoreRows);
        Assert.Contains(run.Comparison.Rows, r => r is { Table: "RestoreRun", Operational: true, Light: "Neutral" });
        Assert.Contains("hoán đổi được", run.Note);

        using (var scope = _factory.Services.CreateScope())
        {
            var main = scope.ServiceProvider.GetRequiredService<ProcDbContext>().Database.GetConnectionString()!;
            await using var conn = new NpgsqlConnection(main);
            await conn.OpenAsync(Ct);
            await using var cmd = new NpgsqlCommand("SELECT pg_get_userbyid(datdba) FROM pg_database WHERE datname = @n", conn);
            cmd.Parameters.AddWithValue("n", run.RestoreDatabase);
            Assert.Equal(new NpgsqlConnectionStringBuilder(main).Username, await cmd.ExecuteScalarAsync(Ct));
        }

        var compared = await ReadAsync<RestoreRunDto>(await head.PostAsync($"{RestorePath}/runs/{run.Id}/compare", null, Ct));
        Assert.True(compared.Comparison!.ComparedAt >= run.Comparison.ComparedAt);
        var list = await ReadAsync<RestoreRunSearchResult>(await head.GetAsync($"{RestorePath}/runs?{Range}", Ct));
        Assert.Contains(list.Items, r => r.Id == run.Id && r.Comparison is null);

        var discarded = await ReadAsync<RestoreRunDto>(await head.PostAsync($"{RestorePath}/runs/{run.Id}/discard", null, Ct));
        Assert.Equal(("Discarded", "Đã bỏ", ProcApiFactory.AdminUserName), (discarded.Status, discarded.StatusLabel, discarded.DiscardedByName));
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ProcDbContext>();
            Assert.False(await RestoreDatabases.ExistsAsync(db.Database.GetConnectionString()!, run.RestoreDatabase, Ct));
            foreach (var kind in new[] { OperationKinds.RestoreQueued, OperationKinds.RestoreFinished, OperationKinds.RestoreDiscarded })
                Assert.True(await db.OperationLogs.AnyAsync(l => l.Kind == kind, Ct), kind);
        }

        await ClearAsync();
    }

    /// <summary>
    /// Hoán đổi (không đổi tên CSDL test thật – bộ thực thi giả bằng request có khóa): chưa khai bộ thực thi máy này → 400; sai chữ xác nhận → 400;
    /// đúng Q6 → Đang hoán đổi, có lệnh SwapDatabase kèm tên CSDL cũ / mới. Bộ thực thi lấy lệnh, xin chuẩn bị → bật chế độ bảo trì, bản khôi phục
    /// nhận bảng vận hành của CSDL đang chạy (lần phục hồi ở trạng thái Đang hoán đổi, lệnh Đang chạy, nhật ký vận hành đủ). Báo lỗi → về Chờ kiểm
    /// tra kèm lỗi; hoán đổi lại, báo xong → Đã hoán đổi, ghi tên CSDL cũ. Màn hình AGT không ra được lệnh hoán đổi.
    /// </summary>
    [Fact]
    public async Task HoanDoi_QuaBoThucThi_ChepBangVanHanh()
    {
        if (!HasTools()) Assert.Skip("Máy test không có pg_dump / pg_restore – bỏ qua test chạy thật.");
        await ClearAsync();
        var head = await HeadAsync();
        var dumpId = await DumpAsync(head);
        var queued = await ReadAsync<RestoreRunDto>(await head.PostAsJsonAsync($"{RestorePath}/runs",
            new QueueRestoreRequest(RestoreKind.Dump, dumpId, null, "Khôi phục để hoán đổi thử"), Ct));
        Assert.Equal(1, await _factory.Services.GetRequiredService<IRestoreExecutor>().ProcessQueueAsync(Ct));
        var swapPath = $"{RestorePath}/runs/{queued.Id}/swap";
        var noAgent = await FailAsync(await head.PostAsJsonAsync(swapPath,
            new SwapRestoreRequest(ProcApiFactory.AdminPassword, "Hoán đổi thử", "khôi phục"), Ct), HttpStatusCode.BadRequest);
        Assert.Contains("AGT", noAgent);

        // Bộ thực thi của máy này (môi trường Testing khai App:Environment = Dev).
        var env = await ReadAsync<AgentEnvironmentDto>(await head.PostAsJsonAsync("/api/ops/v1/agent-admin/environments",
            new SaveAgentEnvironmentRequest("dev", "Máy DEV", null), Ct));
        var key = (await ReadAsync<AgentKeyCreatedDto>(await head.PostAsJsonAsync($"/api/ops/v1/agent-admin/environments/{env.Id}/key",
            new CreateAgentKeyRequest(ProcApiFactory.AdminPassword, "TẠO KHÓA"), Ct))).Key;
        await FailAsync(await head.PostAsJsonAsync("/api/ops/v1/agent-admin/commands",
            new QueueAgentCommandRequest(env.Id, AgentCommandKind.SwapDatabase, "Hoán đổi từ AGT"), Ct), HttpStatusCode.BadRequest);

        await FailAsync(await head.PostAsJsonAsync(swapPath, new SwapRestoreRequest(ProcApiFactory.AdminPassword, "Hoán đổi thử", "khoi phuc"), Ct),
            HttpStatusCode.BadRequest);
        var swapping = await ReadAsync<RestoreRunDto>(await head.PostAsJsonAsync(swapPath,
            new SwapRestoreRequest(ProcApiFactory.AdminPassword, "Hoán đổi thử", "khôi phục"), Ct));
        Assert.Equal(("Swapping", "Đang hoán đổi"), (swapping.Status, swapping.StatusLabel));

        var agent = _factory.CreateClient();
        agent.DefaultRequestHeaders.Add(AgentHeaders.Key, key);
        var work = await ReadAsync<AgentWorkDto>(await agent.GetAsync($"{AgentPath}?agentVersion=1.1.0&appVersion=0.34.0&host=TEST", Ct));
        Assert.Equal(("SwapDatabase", swapping.AgentCommandId), (work.Kind, (Guid?)work.Id));
        Assert.Contains(queued.RestoreDatabase, work.Parameters, StringComparison.Ordinal);
        Assert.Contains("procurement_test_old_", work.Parameters, StringComparison.Ordinal);

        var mode = _factory.Services.GetRequiredService<IMaintenanceMode>();
        try
        {
            Assert.Equal(HttpStatusCode.NoContent, (await agent.PostAsync($"{AgentPath}/{work.Id}/prepare-swap", null, Ct)).StatusCode);
            Assert.True((await mode.GetAsync(Ct)).IsOn);
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ProcDbContext>();
                var main = db.Database.GetConnectionString()!;
                await using var conn = new NpgsqlConnection(RestoreDatabases.ForDatabase(main, queued.RestoreDatabase));
                await conn.OpenAsync(Ct);
                async Task<object?> Scalar(string sql)
                {
                    await using var c = new NpgsqlCommand(sql, conn);
                    return await c.ExecuteScalarAsync(Ct);
                }

                // Bản khôi phục có chính lần phục hồi (Đang hoán đổi), lệnh đang chạy, khóa bộ thực thi, trạng thái bảo trì bật, đủ nhật ký vận hành.
                Assert.Equal("Swapping", await Scalar($"SELECT \"Status\" FROM \"RestoreRun\" WHERE \"Id\" = '{queued.Id}'"));
                Assert.Equal("Running", await Scalar($"SELECT \"Status\" FROM \"AgentCommand\" WHERE \"Id\" = '{work.Id}'"));
                Assert.Equal(1L, await Scalar("SELECT count(*) FROM \"AgentEnvironment\" WHERE \"AgentKeyHash\" IS NOT NULL"));
                Assert.Equal(true, await Scalar("SELECT \"IsOn\" FROM \"MaintenanceModeState\""));
                Assert.Equal(await db.OperationLogs.LongCountAsync(Ct), await Scalar("SELECT count(*) FROM \"OperationLog\""));
            }

            Assert.Equal(HttpStatusCode.NoContent, (await agent.PostAsJsonAsync($"{AgentPath}/{work.Id}/result",
                new AgentResultRequest(false, "Không đổi tên được CSDL – đã trả về như cũ", "nhật ký"), Ct)).StatusCode);
            var failed = await ReadAsync<RestoreRunDto>(await head.GetAsync($"{RestorePath}/runs/{queued.Id}", Ct));
            Assert.Equal("Ready", failed.Status);
            Assert.Contains("Không đổi tên được", failed.ErrorMessage);

            var retry = await ReadAsync<RestoreRunDto>(await head.PostAsJsonAsync(swapPath,
                new SwapRestoreRequest(ProcApiFactory.AdminPassword, "Hoán đổi lần hai", "KHÔI PHỤC"), Ct));
            var work2 = await ReadAsync<AgentWorkDto>(await agent.GetAsync($"{AgentPath}?agentVersion=1.1.0", Ct));
            Assert.Equal(retry.AgentCommandId, work2.Id);
            Assert.Equal(HttpStatusCode.NoContent, (await agent.PostAsJsonAsync($"{AgentPath}/{work2.Id}/result",
                new AgentResultRequest(true, "Đã hoán đổi, /health báo 0.34.0", null), Ct)).StatusCode);
            var done = await ReadAsync<RestoreRunDto>(await head.GetAsync($"{RestorePath}/runs/{queued.Id}", Ct));
            Assert.Equal(("Swapped", "Đã hoán đổi", "Ok"), (done.Status, done.StatusLabel, done.Light));
            Assert.StartsWith("procurement_test_old_", done.OldDatabase, StringComparison.Ordinal);

            using var s2 = _factory.Services.CreateScope();
            var db2 = s2.ServiceProvider.GetRequiredService<ProcDbContext>();
            Assert.True(await db2.OperationLogs.AnyAsync(l => l.Kind == OperationKinds.RestoreSwapFinished, Ct));
        }
        finally
        {
            await mode.SetAsync(new SetMaintenanceModeRequest(false, null, "Kết thúc test hoán đổi"), "test", Ct);
            await ClearAsync();
        }
    }

    /// <summary>
    /// Về mốc thời gian khi máy chưa khai thư mục WAL / chưa có bản Full: kế hoạch báo không khôi phục được kèm lý do, đặt lần phục hồi → 400.
    /// Quyền: công ty thành viên → 403; chỉ quyền xem thì không đặt được.
    /// </summary>
    [Fact]
    public async Task KeHoachMocThoiGian_VaQuyen()
    {
        await ClearAsync();
        var head = await HeadAsync();
        var target = DateTimeOffset.UtcNow.AddHours(-1).ToString("O");
        var plan = await ReadAsync<RestorePlan>(await head.GetAsync($"{RestorePath}/plan?target={Uri.EscapeDataString(target)}", Ct));
        Assert.False(plan.Feasible);
        Assert.Contains(plan.Problems, p => p.Contains("Backup:WalArchivePath", StringComparison.Ordinal));
        Assert.Contains(plan.Problems, p => p.Contains("Chưa có bản sao lưu Full", StringComparison.Ordinal));
        await FailAsync(await head.PostAsJsonAsync($"{RestorePath}/runs",
            new QueueRestoreRequest(RestoreKind.PointInTime, null, DateTimeOffset.UtcNow.AddHours(-1), "Về mốc một giờ trước"), Ct),
            HttpStatusCode.BadRequest);

        var member = await _factory.CreateClientInCompanyAsync(ProcApiFactory.AdminUserName, ProcApiFactory.AdminPassword, "BHA");
        Assert.Equal(HttpStatusCode.Forbidden, (await member.GetAsync($"{RestorePath}/runs?{Range}", Ct)).StatusCode);

        var role = $"V{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        await _factory.CreateRoleAsync(role, Permissions.System.OpsRestoreView);
        var name = $"rv.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(name, "TCT", role);
        var viewer = await _factory.CreateClientInCompanyAsync(name, ProcApiFactory.TestPassword, "TCT");
        Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync($"{RestorePath}/runs?{Range}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsJsonAsync($"{RestorePath}/runs",
            new QueueRestoreRequest(RestoreKind.Dump, Guid.NewGuid(), null, "Khôi phục thử"), Ct)).StatusCode);
    }
}
