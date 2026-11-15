using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;
using Proc.Application.Common;
using Proc.Application.Operations;
using Proc.Application.Organization;
using Proc.Domain.Operations;
using Proc.Domain.Organization;
using Proc.Infrastructure.Operations;
using Proc.Infrastructure.Persistence;

namespace Proc.IntegrationTests;

/// <summary>
/// Lát QT4 – bảo trì CSDL (màn hình DBM, design.md mục 21.4, ke-hoach-qt.md QT4): số liệu bảng, VACUUM ANALYZE / REINDEX thật trên CSDL test qua
/// hàng chờ, phiên chờ khóa, ngắt phiên có xác nhận Q6 (từ chối khi thiếu mật khẩu / lý do / chữ xác nhận), lịch tuần, tham số, quyền.
/// </summary>
[Collection(ApiCollection.Name)]
public class DbMaintenanceTests
{
    private const string BasePath = "/api/ops/v1/db-maintenance";

    /// <summary>Bảng thử tạo riêng cho test (khóa, VACUUM, REINDEX) – không đụng bảng nghiệp vụ.</summary>
    private const string TestTable = "DbmTestLock";

    private readonly ProcApiFactory _factory;

    public DbMaintenanceTests(ProcApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(Ct)}");
        return (await response.Content.ReadFromJsonAsync<T>(Ct))!;
    }

    private Task<HttpClient> HeadAsync() =>
        _factory.CreateClientInCompanyAsync(ProcApiFactory.AdminUserName, ProcApiFactory.AdminPassword, "TCT");

    private string ConnectionString()
    {
        using var scope = _factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<ProcDbContext>().Database.GetConnectionString()!;
    }

    private async Task ClearRunsAsync()
    {
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ProcDbContext>().MaintenanceRuns.ExecuteDeleteAsync(Ct);
    }

    /// <summary>Tạo bảng thử có ít dòng (chạy lại test thì tạo lại từ đầu).</summary>
    private async Task CreateTestTableAsync()
    {
        await using var connection = new NpgsqlConnection(ConnectionString());
        await connection.OpenAsync(Ct);
        await using var cmd = new NpgsqlCommand(
            $"DROP TABLE IF EXISTS \"{TestTable}\"; CREATE TABLE \"{TestTable}\" (id int PRIMARY KEY, v text); " +
            $"INSERT INTO \"{TestTable}\" SELECT g, 'x' FROM generate_series(1, 200) g; DELETE FROM \"{TestTable}\" WHERE id <= 100;", connection);
        await cmd.ExecuteNonQueryAsync(Ct);
    }

    private static async Task<int> PidAsync(NpgsqlConnection connection)
    {
        await using var cmd = new NpgsqlCommand("SELECT pg_backend_pid()", connection);
        return (int)(await cmd.ExecuteScalarAsync(Ct))!;
    }

    private Task<int> ProcessAsync() => _factory.Services.GetRequiredService<IDbMaintenanceExecutor>().ProcessQueueAsync(Ct);

    private async Task<MaintenanceRunDto> QueueAsync(HttpClient client, MaintenanceKind kind, string? table) =>
        await ReadAsync<MaintenanceRunDto>(await client.PostAsJsonAsync($"{BasePath}/runs",
            new QueueMaintenanceRequest(kind, table is null ? null : "public", table), Ct));

    /// <summary>
    /// Tab Bảng: tên CSDL test, dung lượng; bảng thử có dòng chết, đúng dung lượng. VACUUM ANALYZE bảng thử qua hàng chờ: Xong, ghi dòng chết trước
    /// khi chạy, dung lượng trước / sau; bấm lần hai khi còn chờ thì 400; bảng không có / REINDEX không bảng thì 400. REINDEX CONCURRENTLY bảng thử
    /// và VACUUM cả CSDL: Xong. Lịch sử tìm theo ngày có đủ ba lần; nhật ký vận hành có MaintenanceFinished.
    /// </summary>
    [Fact]
    public async Task VacuumReindex_QuaHangCho_ChayThat()
    {
        await ClearRunsAsync();
        await CreateTestTableAsync();
        var head = await HeadAsync();

        var tables = await ReadAsync<DbTableStatsDto>(await head.GetAsync($"{BasePath}/tables", Ct));
        Assert.Equal(ProcApiFactory.ExpectedDatabase, tables.DatabaseName);
        Assert.True(tables.DatabaseBytes > 0);
        var test = Assert.Single(tables.Tables, t => t.Name == TestTable);
        Assert.Equal("public", test.Schema);
        Assert.True(test.TotalBytes >= test.TableBytes);

        var vacuum = await QueueAsync(head, MaintenanceKind.Vacuum, TestTable);
        Assert.Equal(("Queued", $"public.{TestTable}", ProcApiFactory.AdminUserName), (vacuum.Status, vacuum.Target, vacuum.RequestedByName));
        var again = await head.PostAsJsonAsync($"{BasePath}/runs", new QueueMaintenanceRequest(MaintenanceKind.Vacuum, "public", TestTable), Ct);
        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await head.PostAsJsonAsync($"{BasePath}/runs", new QueueMaintenanceRequest(MaintenanceKind.Vacuum, "public", "KhongCoBang"), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await head.PostAsJsonAsync($"{BasePath}/runs", new QueueMaintenanceRequest(MaintenanceKind.Reindex, null, null), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await head.PostAsJsonAsync($"{BasePath}/runs",
            new QueueMaintenanceRequest(MaintenanceKind.TerminateSession, null, null), Ct)).StatusCode);

        var reindex = await QueueAsync(head, MaintenanceKind.Reindex, TestTable);
        var all = await QueueAsync(head, MaintenanceKind.Vacuum, null);
        Assert.Null(all.Target);
        Assert.Equal(3, await ProcessAsync());

        var done = await ReadAsync<MaintenanceRunDto>(await head.GetAsync($"{BasePath}/runs/{vacuum.Id}", Ct));
        Assert.True(done.Status == "Succeeded", done.ErrorMessage);
        Assert.Equal("Xong", done.StatusLabel);
        Assert.NotNull(done.SizeBeforeBytes);
        Assert.NotNull(done.SizeAfterBytes);
        Assert.Contains("Dòng chết trước khi chạy", done.Note, StringComparison.Ordinal);
        foreach (var id in new[] { reindex.Id, all.Id })
        {
            var run = await ReadAsync<MaintenanceRunDto>(await head.GetAsync($"{BasePath}/runs/{id}", Ct));
            Assert.True(run.Status == "Succeeded", run.ErrorMessage);
        }

        var after = await ReadAsync<DbTableStatsDto>(await head.GetAsync($"{BasePath}/tables", Ct));
        Assert.NotNull(Assert.Single(after.Tables, t => t.Name == TestTable).LastVacuum);

        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, VietnamTime.Zone).DateTime);
        var history = await ReadAsync<MaintenanceRunSearchResult>(
            await head.GetAsync($"{BasePath}/runs?from={today.AddDays(-1):yyyy-MM-dd}&to={today.AddDays(1):yyyy-MM-dd}", Ct));
        Assert.Equal(3, history.Items.Count);
        Assert.False(history.ScheduleEnabled);
        Assert.Equal("Chủ nhật 03:00", history.ScheduleLabel);
        Assert.Null(history.NextScheduledAt);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ProcDbContext>();
            Assert.True(await db.OperationLogs.AnyAsync(l => l.Kind == OperationKinds.MaintenanceFinished && l.Target == $"REINDEX public.{TestTable}",
                Ct));
        }

        await ClearRunsAsync();
    }

    /// <summary>
    /// Phiên và khóa: phiên A giữ khóa bảng thử trong giao dịch, phiên B chờ → B có "chờ khóa của A", A "đang chặn 1 phiên", đèn vàng. Ngắt A:
    /// thiếu lý do / sai chữ xác nhận / sai mật khẩu đều 400 và A vẫn sống (sai mật khẩu ghi nhật ký DangerConfirmFailed); đúng (gõ thường "ngắt
    /// phiên" vẫn nhận) → Xong, B chạy tiếp được, lịch sử + nhật ký vận hành ghi lý do. Pid không còn thì 400.
    /// </summary>
    [Fact]
    public async Task NgatPhien_XacNhanQ6_GiaiPhongKhoa()
    {
        await ClearRunsAsync();
        await CreateTestTableAsync();
        var head = await HeadAsync();

        // Không dùng pool: kết nối bị ngắt không quay về pool làm hỏng test khác.
        var cs = new NpgsqlConnectionStringBuilder(ConnectionString()) { Pooling = false, ApplicationName = "dbm-test-a" }.ConnectionString;
        await using var holder = new NpgsqlConnection(cs);
        await holder.OpenAsync(Ct);
        var holderPid = await PidAsync(holder);
        await using (var begin = new NpgsqlCommand($"BEGIN; LOCK TABLE \"{TestTable}\" IN ACCESS EXCLUSIVE MODE;", holder))
            await begin.ExecuteNonQueryAsync(Ct);

        await using var waiter = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(cs) { ApplicationName = "dbm-test-b" }.ConnectionString);
        await waiter.OpenAsync(Ct);
        var waiterPid = await PidAsync(waiter);
        await using var waitCmd = new NpgsqlCommand($"SELECT count(*) FROM \"{TestTable}\"", waiter);
        var waiting = waitCmd.ExecuteScalarAsync(Ct);

        DbSessionsDto sessions;
        var tries = 0;
        do
        {
            await Task.Delay(200, Ct);
            sessions = await ReadAsync<DbSessionsDto>(await head.GetAsync($"{BasePath}/sessions", Ct));
        }
        while (!sessions.Sessions.Any(s => s.Pid == waiterPid && s.BlockedBy.Contains(holderPid)) && ++tries < 25);

        var blocked = Assert.Single(sessions.Sessions, s => s.Pid == waiterPid);
        Assert.Contains(holderPid, blocked.BlockedBy);
        Assert.Equal("Warning", blocked.Light);
        Assert.Contains($"chờ khóa của phiên {holderPid}", blocked.Note, StringComparison.Ordinal);
        var blocker = Assert.Single(sessions.Sessions, s => s.Pid == holderPid);
        Assert.Equal((1, "Warning", "dbm-test-a"), (blocker.BlockingCount, blocker.Light, blocker.ApplicationName));
        Assert.True(sessions.WaitingCount >= 1);
        Assert.False(blocker.IsSelf);

        async Task<HttpResponseMessage> Terminate(int pid, string? password, string? reason, string? confirmation) =>
            await head.PostAsJsonAsync($"{BasePath}/sessions/terminate", new TerminateSessionRequest(pid, password, reason, confirmation), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, (await Terminate(holderPid, ProcApiFactory.AdminPassword, "abc", "NGẮT PHIÊN")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Terminate(holderPid, ProcApiFactory.AdminPassword, "Phiên giữ khóa", "NGAT PHIEN")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Terminate(holderPid, null, "Phiên giữ khóa", "NGẮT PHIÊN")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Terminate(holderPid, "sai-mat-khau", "Phiên giữ khóa", "NGẮT PHIÊN")).StatusCode);
        Assert.False(waiting.IsCompleted);

        var run = await ReadAsync<MaintenanceRunDto>(await Terminate(holderPid, ProcApiFactory.AdminPassword, "Phiên giữ khóa bảng thử", "ngắt phiên"));
        Assert.True(run.Status == "Succeeded", run.ErrorMessage);
        Assert.Equal(("TerminateSession", holderPid, "Phiên giữ khóa bảng thử"), (run.Kind, run.SessionPid, run.Reason));
        Assert.Contains("dbm-test-a", run.Target, StringComparison.Ordinal);
        Assert.Equal(100L, Convert.ToInt64(await waiting.WaitAsync(TimeSpan.FromSeconds(10), Ct), System.Globalization.CultureInfo.InvariantCulture));

        Assert.Equal(HttpStatusCode.BadRequest, (await Terminate(holderPid, ProcApiFactory.AdminPassword, "Phiên giữ khóa", "NGẮT PHIÊN")).StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ProcDbContext>();
            var log = await db.OperationLogs.Where(l => l.Kind == OperationKinds.SessionTerminated).OrderByDescending(l => l.OccurredAt).FirstAsync(Ct);
            Assert.Equal(("Phiên giữ khóa bảng thử", OperationResult.Succeeded), (log.Reason, log.Result));
            Assert.True(await db.OperationLogs.AnyAsync(l => l.Kind == OperationKinds.DangerConfirmFailed, Ct));
        }

        await ClearRunsAsync();
    }

    /// <summary>
    /// Lịch tuần (mặc định Chủ nhật 03:00): Chủ nhật 22/11/2026 02:30 chưa tới giờ; 03:30 đặt VACUUM cả CSDL theo lịch, gọi lại không đặt trùng;
    /// lịch tắt thì không đặt. Tham số DB_MAINT_DAY / DB_MAINT_TIME sửa được, sai thì 400, bỏ trống về mặc định.
    /// </summary>
    [Fact]
    public async Task LichTuan_VaThamSo()
    {
        await ClearRunsAsync();
        var sunday = new DateTimeOffset(2026, 11, 22, 0, 0, 0, TimeSpan.FromHours(7));
        using (var scope = _factory.Services.CreateScope())
        {
            DbMaintenanceScheduler Scheduler(DateTimeOffset now, bool enabled) => new(
                scope.ServiceProvider.GetRequiredService<ProcDbContext>(), new FixedClock(now),
                scope.ServiceProvider.GetRequiredService<IDbMaintenanceQueueSignal>(),
                Options.Create(new DbMaintenanceOptions { ScheduleEnabled = enabled }));

            Assert.Equal(0, await Scheduler(sunday.AddHours(3.5), false).PlanAsync(Ct));
            Assert.Equal(0, await Scheduler(sunday.AddHours(2.5), true).PlanAsync(Ct));
            Assert.Equal(1, await Scheduler(sunday.AddHours(3.5), true).PlanAsync(Ct));
            Assert.Equal(0, await Scheduler(sunday.AddHours(4), true).PlanAsync(Ct));
            var run = Assert.Single(await scope.ServiceProvider.GetRequiredService<ProcDbContext>().MaintenanceRuns.AsNoTracking().ToListAsync(Ct));
            Assert.Equal((MaintenanceKind.Vacuum, MaintenanceTrigger.Schedule, new DateOnly(2026, 11, 22), (string?)null),
                (run.Kind, run.Trigger, run.ScheduledDate, run.Target));
        }

        await ClearRunsAsync();

        var head = await HeadAsync();
        var list = await ReadAsync<List<SystemParameterDto>>(await head.GetAsync("/api/system-parameters", Ct));
        Assert.Equal("7 – Chủ nhật", Assert.Single(list, p => p.Code == SystemParameterCodes.DbMaintenanceDay).Value);
        Assert.Equal("03:00", Assert.Single(list, p => p.Code == SystemParameterCodes.DbMaintenanceTime).Value);

        async Task<HttpResponseMessage> Put(string code, string value) =>
            await head.PutAsJsonAsync($"/api/system-parameters/{code}", new UpdateSystemParameterRequest(value), Ct);
        Assert.Equal("6 – Thứ bảy", (await ReadAsync<SystemParameterDto>(await Put(SystemParameterCodes.DbMaintenanceDay, "6"))).Value);
        Assert.Equal("04:30", (await ReadAsync<SystemParameterDto>(await Put(SystemParameterCodes.DbMaintenanceTime, "04:30"))).Value);
        Assert.Equal(HttpStatusCode.BadRequest, (await Put(SystemParameterCodes.DbMaintenanceDay, "0")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Put(SystemParameterCodes.DbMaintenanceTime, "24:10")).StatusCode);
        Assert.True((await ReadAsync<SystemParameterDto>(await Put(SystemParameterCodes.DbMaintenanceDay, ""))).IsDefault);
        Assert.True((await ReadAsync<SystemParameterDto>(await Put(SystemParameterCodes.DbMaintenanceTime, ""))).IsDefault);
    }

    /// <summary>Quyền: công ty thành viên 403; chỉ quyền xem thì xem được, không đặt / ngắt được; có quyền chạy thì đặt được.</summary>
    [Fact]
    public async Task Quyen_ChiTongCongTy_XemVaChay()
    {
        await ClearRunsAsync();
        await CreateTestTableAsync();
        var member = await _factory.CreateClientInCompanyAsync(ProcApiFactory.AdminUserName, ProcApiFactory.AdminPassword, "BHA");
        Assert.Equal(HttpStatusCode.Forbidden, (await member.GetAsync($"{BasePath}/tables", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.GetAsync($"{BasePath}/sessions", Ct)).StatusCode);

        var viewRole = $"M{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        await _factory.CreateRoleAsync(viewRole, Permissions.System.OpsMaintenanceView);
        var viewerName = $"mv.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(viewerName, "TCT", viewRole);
        var viewer = await _factory.CreateClientInCompanyAsync(viewerName, ProcApiFactory.TestPassword, "TCT");
        Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync($"{BasePath}/sessions", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsJsonAsync($"{BasePath}/runs",
            new QueueMaintenanceRequest(MaintenanceKind.Vacuum, "public", TestTable), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsJsonAsync($"{BasePath}/sessions/terminate",
            new TerminateSessionRequest(1, ProcApiFactory.TestPassword, "Phiên treo", "NGẮT PHIÊN"), Ct)).StatusCode);

        var runRole = $"N{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        await _factory.CreateRoleAsync(runRole, Permissions.System.OpsMaintenanceView, Permissions.System.OpsMaintenanceRun);
        var runnerName = $"mr.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(runnerName, "TCT", runRole);
        var runner = await _factory.CreateClientInCompanyAsync(runnerName, ProcApiFactory.TestPassword, "TCT");
        Assert.Equal(runnerName, (await QueueAsync(runner, MaintenanceKind.Vacuum, TestTable)).RequestedByName);
        await ClearRunsAsync();
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow => now.ToUniversalTime();
    }
}
