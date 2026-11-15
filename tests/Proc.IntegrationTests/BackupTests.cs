using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;
using Microsoft.Extensions.Logging.Abstractions;
using Proc.Application.Common;
using Proc.Application.Operations;
using Proc.Application.Organization;
using Proc.Domain.Operations;
using Proc.Domain.Organization;
using Proc.Infrastructure.Documents;
using Proc.Infrastructure.Operations;
using Proc.Infrastructure.Persistence;

namespace Proc.IntegrationTests;

/// <summary>
/// Lát QT3b-1 – sao lưu (design.md mục 21.5, ke-hoach-qt.md QT3b): tham số lệnh công cụ PostgreSQL (đường dẫn có dấu cách, dấu tiếng Việt),
/// <c>pg_dump</c> chạy thật trên CSDL test + <c>pg_restore --list</c> + SHA256 + chép nơi thứ hai, chép bổ sung đính kèm, Full thiếu tài khoản bảo
/// trì báo lỗi rõ, chống chạy trùng (khóa advisory, bấm hai lần), ghi lỗi lần dừng giữa chừng, quyền. Thư mục sao lưu của test:
/// <see cref="ProcApiFactory.BackupRoot"/> (có dấu cách và dấu tiếng Việt).
/// </summary>
[Collection(ApiCollection.Name)]
public class BackupTests
{
    private const string RunsPath = "/api/ops/v1/backup/runs";

    private readonly ProcApiFactory _factory;

    public BackupTests(ProcApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(Ct)}");
        return (await response.Content.ReadFromJsonAsync<T>(Ct))!;
    }

    /// <summary>Xóa lịch sử sao lưu của các test trước (mỗi test bắt đầu với hàng chờ trống).</summary>
    private async Task ClearRunsAsync()
    {
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ProcDbContext>().BackupRuns.ExecuteDeleteAsync(Ct);
    }

    private async Task<BackupRunDto> QueueAsync(HttpClient client, BackupKind kind) =>
        await ReadAsync<BackupRunDto>(await client.PostAsJsonAsync(RunsPath, new QueueBackupRequest(kind), Ct));

    private Task<int> ProcessAsync() => _factory.Services.GetRequiredService<IBackupExecutor>().ProcessQueueAsync(Ct);

    private static async Task<BackupRunDto> GetAsync(HttpClient client, Guid id) =>
        await ReadAsync<BackupRunDto>(await client.GetAsync($"{RunsPath}/{id}", Ct));

    private Task<HttpClient> HeadAsync() =>
        _factory.CreateClientInCompanyAsync(ProcApiFactory.AdminUserName, ProcApiFactory.AdminPassword, "TCT");

    /// <summary>
    /// Tham số lệnh: mỗi phần tử một tham số (đường dẫn có dấu cách, dấu tiếng Việt giữ nguyên, không tự thêm ngoặc kép – ArgumentList lo phần
    /// đó); mật khẩu không nằm trong tham số; Diff có <c>--incremental</c>; đọc file WAL bắt đầu từ backup_label.
    /// </summary>
    [Fact]
    public void ThamSoLenh_DuongDanCoDauCachTiengViet_KhongChuaMatKhau()
    {
        var conn = PgConnectionInfo.Parse("Host=db.local;Port=5433;Database=procurement;Username=proc_maint;Password=bí mật");
        var dir = Path.Combine("E:", "Sao lưu", "full", "20261115-010000");
        var manifest = Path.Combine("E:", "Sao lưu", "full", "20261108-010000", "backup_manifest");

        var full = PgToolArgs.BaseBackup(conn, dir, "Procurement Full 20261115-010000", null);
        Assert.Equal(
            ["-h", "db.local", "-p", "5433", "-U", "proc_maint", "-D", dir, "-Fp", "-X", "stream", "-c", "fast", "-l",
                "Procurement Full 20261115-010000", "-w"],
            full);
        Assert.DoesNotContain(full, a => a.Contains("bí mật", StringComparison.Ordinal));

        var diff = PgToolArgs.BaseBackup(conn, dir, "Procurement Diff", manifest);
        Assert.Equal(["--incremental", manifest], diff.TakeLast(2));

        var dump = PgToolArgs.Dump(conn with { Host = null }, "procurement", Path.Combine("E:", "Sao lưu", "dump", "a b.dump"));
        Assert.Equal(["-p", "5433", "-U", "proc_maint", "-d", "procurement", "-Fc", "-f", Path.Combine("E:", "Sao lưu", "dump", "a b.dump"), "-w"], dump);
        Assert.Equal(["--list", "x y.dump"], PgToolArgs.RestoreList("x y.dump"));
        Assert.Equal(["-q", dir], PgToolArgs.VerifyBackup(dir));

        const string label = "START WAL LOCATION: 0/2000028 (file 00000001000000000000000a)\nCHECKPOINT LOCATION: 0/2000080\n";
        Assert.Equal("00000001000000000000000A", PgToolArgs.ParseStartWalFile(label));
        Assert.Null(PgToolArgs.ParseStartWalFile("không có dòng"));

        // Thư mục cấu hình được ưu tiên, kể cả có dấu cách.
        var bin = Path.Combine("C:", "Program Files", "PostgreSQL", "18", "bin");
        Assert.Equal(Path.Combine(bin, OperatingSystem.IsWindows() ? "pg_dump.exe" : "pg_dump"), PgToolRunner.Locate("pg_dump", bin, null, null));
    }

    /// <summary>
    /// pg_dump chạy thật trên procurement_test: vào hàng chờ, bộ chạy chạy xong, kiểm tra <c>pg_restore --list</c> đạt, SHA256 khớp file, file nằm
    /// trong thư mục có dấu cách / tiếng Việt, chép sang nơi thứ hai khớp; nhật ký vận hành có dòng kết thúc. Máy không có công cụ PostgreSQL thì bỏ qua.
    /// </summary>
    [Fact]
    public async Task PgDump_ChayThat_KiemTraSha256VaNoiThuHai()
    {
        var tools = _factory.Services.GetRequiredService<PgToolRunner>();
        if (!File.Exists(tools.Resolve("pg_dump")))
            Assert.Skip("Máy test không có pg_dump (khai Backup:PgBinPath hoặc PATH) – bỏ qua test chạy thật.");
        await ClearRunsAsync();
        var head = await HeadAsync();

        var queued = await QueueAsync(head, BackupKind.Dump);
        Assert.Equal(("Queued", "Đang chờ", "Manual", ProcApiFactory.AdminUserName), (queued.Status, queued.StatusLabel, queued.Trigger, queued.RequestedByName));
        Assert.Equal(1, await ProcessAsync());

        var run = await GetAsync(head, queued.Id);
        Assert.True(run.Status == "Succeeded", $"{run.Status}: {run.ErrorMessage}");
        Assert.True(run.Verified, run.VerifyMessage);
        Assert.Equal(("Xong", "Ok", "pg_dump"), (run.StatusLabel, run.Light, run.KindLabel));
        Assert.StartsWith(Path.Combine("dump", "procurement_test_"), run.RelativePath, StringComparison.Ordinal);
        var file = Path.Combine(ProcApiFactory.BackupRoot, run.RelativePath!);
        Assert.True(File.Exists(file));
        Assert.Equal(new FileInfo(file).Length, run.SizeBytes);
        Assert.Equal(await BackupFiles.Sha256Async(file, Ct), run.Sha256);
        Assert.Matches("^[0-9a-f]{64}$", run.Sha256!);
        Assert.False(File.Exists(file + BackupFiles.PartialSuffix));

        Assert.True(run.SecondaryCopied, run.SecondaryMessage);
        Assert.Equal(run.Sha256, await BackupFiles.Sha256Async(Path.Combine(ProcApiFactory.BackupSecondary, run.RelativePath!), Ct));
        Assert.NotNull(run.DurationSeconds);

        var list = await ReadAsync<BackupRunSearchResult>(await head.GetAsync(
            $"{RunsPath}?from={DateTime.UtcNow.AddDays(-1):yyyy-MM-dd}&to={DateTime.UtcNow.AddDays(1):yyyy-MM-dd}", Ct));
        Assert.Contains(list.Items, r => r.Id == run.Id);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProcDbContext>();
        Assert.True(await db.OperationLogs.AnyAsync(l => l.Kind == OperationKinds.BackupFinished && l.Target == "pg_dump", Ct));
        Assert.True(await db.OperationLogs.AnyAsync(l => l.Kind == OperationKinds.BackupQueued, Ct));
    }

    /// <summary>
    /// Đính kèm: lần đầu chép file mới (cả thư mục con) sang nơi chính và nơi thứ hai, đối chiếu đạt; lần sau không có gì mới thì chép 0 file; file
    /// sửa (khác dung lượng) thì chép lại.
    /// </summary>
    [Fact]
    public async Task DinhKem_ChepBoSung_LanSauChiChepFileMoi()
    {
        await ClearRunsAsync();
        var source = _factory.Services.GetRequiredService<IOptions<AttachmentOptions>>().Value.RootPath;
        var sub = Path.Combine(source, "test-sao-lưu", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(sub);
        var fileA = Path.Combine(sub, "hợp đồng 01.pdf");
        await File.WriteAllTextAsync(fileA, "nội dung A", Ct);
        await File.WriteAllTextAsync(Path.Combine(sub, "b.txt"), "B", Ct);
        var head = await HeadAsync();

        var first = await QueueAsync(head, BackupKind.Attachments);
        await ProcessAsync();
        var run1 = await GetAsync(head, first.Id);
        Assert.True(run1.Status == "Succeeded", run1.ErrorMessage);
        Assert.True(run1.Verified, run1.VerifyMessage);
        Assert.True(run1.SecondaryCopied, run1.SecondaryMessage);
        Assert.True(run1.FileCount >= 2);
        var rel = Path.GetRelativePath(source, fileA);
        Assert.Equal("nội dung A", await File.ReadAllTextAsync(Path.Combine(ProcApiFactory.BackupRoot, "attachments", rel), Ct));
        Assert.True(File.Exists(Path.Combine(ProcApiFactory.BackupSecondary, "attachments", rel)));

        var second = await QueueAsync(head, BackupKind.Attachments);
        await ProcessAsync();
        Assert.Equal(0, (await GetAsync(head, second.Id)).FileCount);

        await File.WriteAllTextAsync(fileA, "nội dung A đã sửa dài hơn", Ct);
        var third = await QueueAsync(head, BackupKind.Attachments);
        await ProcessAsync();
        Assert.Equal(1, (await GetAsync(head, third.Id)).FileCount);
        Assert.Equal("nội dung A đã sửa dài hơn", await File.ReadAllTextAsync(Path.Combine(ProcApiFactory.BackupRoot, "attachments", rel), Ct));
    }

    /// <summary>
    /// Full / Diff cần tài khoản bảo trì (REPLICATION): chưa khai ConnectionStrings:Maintenance thì lần sao lưu lỗi kèm câu hướng dẫn, đèn đỏ;
    /// bấm "Sao lưu ngay" hai lần cùng kiểu khi lần trước còn chờ thì báo lỗi, không đặt trùng.
    /// </summary>
    [Fact]
    public async Task Full_ThieuTaiKhoanBaoTri_BaoLoiRo_KhongDatTrung()
    {
        await ClearRunsAsync();
        var head = await HeadAsync();
        var options = _factory.Services.GetRequiredService<IOptions<BackupOptions>>().Value;
        if (!string.IsNullOrWhiteSpace(options.MaintenanceConnectionString))
            Assert.Skip("Máy test có khai ConnectionStrings:Maintenance – trường hợp thiếu tài khoản bảo trì không dựng được.");

        var full = await QueueAsync(head, BackupKind.Full);
        var again = await head.PostAsJsonAsync(RunsPath, new QueueBackupRequest(BackupKind.Full), Ct);
        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
        Assert.Contains("Đang có lần sao lưu Full", await again.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);

        await ProcessAsync();
        var run = await GetAsync(head, full.Id);
        Assert.Equal(("Failed", "Lỗi", "Error"), (run.Status, run.StatusLabel, run.Light));
        Assert.Contains("ConnectionStrings:Maintenance", run.ErrorMessage, StringComparison.Ordinal);
        Assert.Null(run.RelativePath);

        // Lần trước đã kết thúc thì đặt lại được.
        Assert.Equal(HttpStatusCode.OK, (await head.PostAsJsonAsync(RunsPath, new QueueBackupRequest(BackupKind.Full), Ct)).StatusCode);
        await ClearRunsAsync();
    }

    /// <summary>
    /// Chống chạy trùng giữa các tiến trình: tiến trình khác đang giữ khóa advisory thì bộ chạy không làm gì, lần sao lưu vẫn chờ; ứng dụng khởi động
    /// lại khi đang chạy thì lần đó được ghi lỗi.
    /// </summary>
    [Fact]
    public async Task KhoaAdvisory_VaGhiLoiLanDungGiuaChung()
    {
        await ClearRunsAsync();
        var head = await HeadAsync();
        var queued = await QueueAsync(head, BackupKind.Attachments);

        using (var scope = _factory.Services.CreateScope())
        {
            // Không dùng pool: đóng kết nối là nhả khóa (kết nối nằm trong pool vẫn giữ khóa phiên).
            var cs = new NpgsqlConnectionStringBuilder(scope.ServiceProvider.GetRequiredService<ProcDbContext>().Database.GetConnectionString())
                { Pooling = false }.ConnectionString;
            await using var other = new NpgsqlConnection(cs);
            await other.OpenAsync(Ct);
            await using (var cmd = new NpgsqlCommand($"SELECT pg_advisory_lock({BackupExecutor.AdvisoryLockKey})", other))
                await cmd.ExecuteScalarAsync(Ct);
            Assert.Equal(0, await ProcessAsync());
            Assert.Equal(0, await _factory.Services.GetRequiredService<IBackupExecutor>().FailInterruptedAsync(Ct));
        }

        Assert.Equal("Queued", (await GetAsync(head, queued.Id)).Status);
        await ClearRunsAsync();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ProcDbContext>();
            var stuck = BackupRun.Queue(BackupKind.Dump, BackupTrigger.Schedule, new DateOnly(2026, 11, 15), null, DateTimeOffset.UtcNow);
            stuck.Start(DateTimeOffset.UtcNow);
            db.BackupRuns.Add(stuck);
            await db.SaveChangesAsync(Ct);
            Assert.Equal(1, await _factory.Services.GetRequiredService<IBackupExecutor>().FailInterruptedAsync(Ct));
            var run = await GetAsync(head, stuck.Id);
            Assert.Equal("Failed", run.Status);
            Assert.Contains("khởi động lại", run.ErrorMessage, StringComparison.Ordinal);
        }

        await ClearRunsAsync();
    }

    /// <summary>
    /// Quyền: công ty thành viên 403 (kể cả quản trị); chỉ xem sao lưu thì xem lịch sử được, bấm "Sao lưu ngay" 403; có thêm quyền chạy thì đặt được.
    /// </summary>
    [Fact]
    public async Task Quyen_XemVaChayRieng_ChiTongCongTy()
    {
        await ClearRunsAsync();
        var from = DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-dd");
        var to = DateTime.UtcNow.AddDays(1).ToString("yyyy-MM-dd");
        var query = $"{RunsPath}?from={from}&to={to}";

        var member = await _factory.CreateClientInCompanyAsync(ProcApiFactory.AdminUserName, ProcApiFactory.AdminPassword, "BHA");
        Assert.Equal(HttpStatusCode.Forbidden, (await member.GetAsync(query, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.PostAsJsonAsync(RunsPath, new QueueBackupRequest(BackupKind.Dump), Ct)).StatusCode);

        var viewRole = $"B{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        await _factory.CreateRoleAsync(viewRole, Permissions.System.OpsBackupView);
        var viewerName = $"bv.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(viewerName, "TCT", viewRole);
        var viewer = await _factory.CreateClientInCompanyAsync(viewerName, ProcApiFactory.TestPassword, "TCT");
        Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync(query, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsJsonAsync(RunsPath, new QueueBackupRequest(BackupKind.Dump), Ct)).StatusCode);

        var runRole = $"R{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        await _factory.CreateRoleAsync(runRole, Permissions.System.OpsBackupView, Permissions.System.OpsBackupRun);
        var runnerName = $"br.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(runnerName, "TCT", runRole);
        var runner = await _factory.CreateClientInCompanyAsync(runnerName, ProcApiFactory.TestPassword, "TCT");
        var queued = await QueueAsync(runner, BackupKind.Dump);
        Assert.Equal(runnerName, queued.RequestedByName);
        await ClearRunsAsync();
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow => now.ToUniversalTime();
    }

    /// <summary>Bộ lập lịch dựng tay: bật lịch, đồng hồ cố định, thư mục WAL tùy chọn.</summary>
    private static BackupScheduler Scheduler(IServiceScope scope, DateTimeOffset now, string? walPath = null) => new(
        scope.ServiceProvider.GetRequiredService<ProcDbContext>(), new FixedClock(now),
        scope.ServiceProvider.GetRequiredService<IBackupQueueSignal>(), scope.ServiceProvider.GetRequiredService<PgToolRunner>(),
        scope.ServiceProvider.GetRequiredService<IOperationLog>(),
        Options.Create(new BackupOptions
        {
            RootPath = ProcApiFactory.BackupRoot, SecondaryPath = ProcApiFactory.BackupSecondary, ScheduleEnabled = true, WalArchivePath = walPath,
        }),
        NullLogger<BackupScheduler>.Instance);

    /// <summary>
    /// Lịch (giờ mặc định 01:00, Full Chủ nhật): Thứ hai 16/11 00:30 chưa tới giờ; 01:30 chưa có Full dùng được → đặt Full + dump + đính kèm theo
    /// đúng thứ tự, gọi lại không đặt trùng; Thứ ba có bản Full dùng được → Diff. Lịch tắt (máy test) thì không đặt.
    /// </summary>
    [Fact]
    public async Task Lich_DatLuotDungGio_KhongTrung_DiffKhiCoFull()
    {
        await ClearRunsAsync();
        var mon0030 = new DateTimeOffset(2026, 11, 16, 0, 30, 0, TimeSpan.FromHours(7));
        using (var scope = _factory.Services.CreateScope())
        {
            Assert.Equal(0, await Scheduler(scope, mon0030).PlanAsync(Ct));
            Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<IBackupScheduler>().PlanAsync(Ct));
            Assert.Equal(3, await Scheduler(scope, mon0030.AddHours(1)).PlanAsync(Ct));
        }

        using (var scope = _factory.Services.CreateScope())
        {
            Assert.Equal(0, await Scheduler(scope, mon0030.AddHours(5)).PlanAsync(Ct));
            var db = scope.ServiceProvider.GetRequiredService<ProcDbContext>();
            var runs = await db.BackupRuns.OrderBy(r => r.QueuedAt).ToListAsync(Ct);
            Assert.Equal([BackupKind.Full, BackupKind.Dump, BackupKind.Attachments], runs.Select(r => r.Kind));
            Assert.All(runs, r => Assert.Equal((BackupTrigger.Schedule, new DateOnly(2026, 11, 16)), (r.Trigger, r.ScheduledDate!.Value)));

            // Giả lập bản Full đạt rồi sang Thứ ba.
            var full = runs[0];
            full.Start(mon0030.ToUniversalTime());
            full.Complete(Path.Combine("full", "gia-lap"), 1, 1, null, "000000010000000000000003", null, mon0030.ToUniversalTime());
            await db.SaveChangesAsync(Ct);
        }

        using (var scope = _factory.Services.CreateScope())
        {
            Assert.Equal(3, await Scheduler(scope, mon0030.AddDays(1).AddHours(1)).PlanAsync(Ct));
            var db = scope.ServiceProvider.GetRequiredService<ProcDbContext>();
            Assert.True(await db.BackupRuns.AnyAsync(r => r.Kind == BackupKind.Diff && r.ScheduledDate == new DateOnly(2026, 11, 17), Ct));
        }

        await ClearRunsAsync();
    }

    /// <summary>
    /// Dọn: bản dump quá 14 ngày bị xóa ở cả nơi chính và nơi thứ hai, ghi lúc dọn; bản mới nhất giữ; bản Full giả lập có file WAL đầu …03 →
    /// pg_archivecleanup xóa WAL …01, …02, giữ từ …03 (khi máy có công cụ PostgreSQL); nhật ký vận hành có dòng dọn.
    /// </summary>
    [Fact]
    public async Task Don_BanQuaHan_VaWalCu()
    {
        await ClearRunsAsync();
        var now = DateTimeOffset.UtcNow;
        var walDir = Path.Combine(Path.GetTempPath(), "proc test sao lưu", "wal", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(walDir);
        for (var i = 1; i <= 4; i++) await File.WriteAllTextAsync(Path.Combine(walDir, $"00000001000000000000000{i}"), "wal", Ct);

        Guid oldId, newId;
        string oldRel;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ProcDbContext>();
            BackupRun Make(BackupKind kind, double daysAgo, string rel, string? wal = null)
            {
                foreach (var root in new[] { ProcApiFactory.BackupRoot, ProcApiFactory.BackupSecondary })
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(root, rel))!);
                    File.WriteAllText(Path.Combine(root, rel), "x");
                }

                var r = BackupRun.Queue(kind, BackupTrigger.Manual, null, null, now.AddDays(-daysAgo));
                r.Start(now.AddDays(-daysAgo));
                r.Complete(rel, 1, 1, null, wal, null, now.AddDays(-daysAgo));
                r.SetVerification(true, null);
                db.BackupRuns.Add(r);
                return r;
            }

            oldRel = Path.Combine("dump", $"cu-{Guid.NewGuid():N}.dump");
            oldId = Make(BackupKind.Dump, 20, oldRel).Id;
            newId = Make(BackupKind.Dump, 1, Path.Combine("dump", $"moi-{Guid.NewGuid():N}.dump")).Id;
            Make(BackupKind.Full, 2, Path.Combine("full", $"gia-lap-{Guid.NewGuid():N}"), "000000010000000000000003");
            await db.SaveChangesAsync(Ct);
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var result = await Scheduler(scope, now, walDir).CleanupAsync(Ct);
            Assert.Equal(1, result.PurgedRuns);
            Assert.False(File.Exists(Path.Combine(ProcApiFactory.BackupRoot, oldRel)));
            Assert.False(File.Exists(Path.Combine(ProcApiFactory.BackupSecondary, oldRel)));
            var db = scope.ServiceProvider.GetRequiredService<ProcDbContext>();
            Assert.NotNull((await db.BackupRuns.SingleAsync(r => r.Id == oldId, Ct)).PurgedAt);
            Assert.Null((await db.BackupRuns.SingleAsync(r => r.Id == newId, Ct)).PurgedAt);

            if (File.Exists(scope.ServiceProvider.GetRequiredService<PgToolRunner>().Resolve("pg_archivecleanup")))
            {
                Assert.Equal("000000010000000000000003", result.OldestNeededWal);
                Assert.Equal(["000000010000000000000003", "000000010000000000000004"],
                    Directory.GetFiles(walDir).Select(f => Path.GetFileName(f)).Order());
            }

            Assert.True(await db.OperationLogs.AnyAsync(l => l.Kind == OperationKinds.BackupCleanup, Ct));
        }

        Directory.Delete(walDir, recursive: true);
        await ClearRunsAsync();
    }

    /// <summary>Sao lưu lỗi thì người có quyền chạy sao lưu ở tổng công ty (admin) nhận thông báo trong phần mềm; SYS có dòng sao lưu và ổ sao lưu.</summary>
    [Fact]
    public async Task SaoLuuLoi_ThongBao_SysCoDong()
    {
        await ClearRunsAsync();
        var head = await HeadAsync();
        var options = _factory.Services.GetRequiredService<IOptions<BackupOptions>>().Value;
        if (!string.IsNullOrWhiteSpace(options.MaintenanceConnectionString))
            Assert.Skip("Máy test có khai ConnectionStrings:Maintenance – không dựng được lần sao lưu Full lỗi.");
        var full = await QueueAsync(head, BackupKind.Full);
        await ProcessAsync();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ProcDbContext>();
            var notes = await db.Notifications.IgnoreQueryFilters()
                .Where(n => n.Type == Proc.Domain.Notifications.NotificationTypes.BackupProblem && n.TargetId == full.Id)
                .ToListAsync(Ct);
            Assert.NotEmpty(notes);
            Assert.All(notes, n => Assert.Equal("BAK", n.ScreenCode));
            Assert.Contains("Sao lưu Full: Lỗi", notes[0].Title, StringComparison.Ordinal);
        }

        var overview = await ReadAsync<SystemOverviewDto>(await head.GetAsync("/api/ops/v1/overview", Ct));
        Assert.Equal("Chưa có", Assert.Single(overview.Rows, r => r.Item == "Sao lưu gần nhất").Value);
        Assert.Contains(overview.Rows, r => r.Item == "Ổ sao lưu");
        await ClearRunsAsync();
    }

    /// <summary>Bốn tham số lịch sao lưu trên màn hình tham số: mặc định, sửa (ngày gõ số, giờ HH:mm), sai thì 400, bỏ trống về mặc định.</summary>
    [Fact]
    public async Task ThamSoLichSaoLuu_SuaVaKiemTra()
    {
        var head = await HeadAsync();
        var list = await ReadAsync<List<SystemParameterDto>>(await head.GetAsync("/api/system-parameters", Ct));
        Assert.Equal("7 – Chủ nhật", Assert.Single(list, p => p.Code == SystemParameterCodes.BackupFullDay).Value);
        Assert.Equal("01:00", Assert.Single(list, p => p.Code == SystemParameterCodes.BackupTime).Value);
        Assert.Equal("4", Assert.Single(list, p => p.Code == SystemParameterCodes.BackupKeepWeeks).Value);
        Assert.Equal("14", Assert.Single(list, p => p.Code == SystemParameterCodes.DumpKeepDays).Value);

        async Task<SystemParameterDto> Put(string code, string value) =>
            await ReadAsync<SystemParameterDto>(await head.PutAsJsonAsync($"/api/system-parameters/{code}", new UpdateSystemParameterRequest(value), Ct));
        async Task<HttpStatusCode> Bad(string code, string value) =>
            (await head.PutAsJsonAsync($"/api/system-parameters/{code}", new UpdateSystemParameterRequest(value), Ct)).StatusCode;

        Assert.Equal("6 – Thứ bảy", (await Put(SystemParameterCodes.BackupFullDay, "6 – Thứ bảy")).Value);
        Assert.Equal("23:30", (await Put(SystemParameterCodes.BackupTime, "23:30")).Value);
        Assert.Equal("02:00", (await Put(SystemParameterCodes.BackupTime, "2")).Value);
        Assert.Equal("8", (await Put(SystemParameterCodes.BackupKeepWeeks, "8")).Value);
        Assert.Equal(HttpStatusCode.BadRequest, await Bad(SystemParameterCodes.BackupFullDay, "8"));
        Assert.Equal(HttpStatusCode.BadRequest, await Bad(SystemParameterCodes.BackupTime, "25:00"));
        Assert.Equal(HttpStatusCode.BadRequest, await Bad(SystemParameterCodes.DumpKeepDays, "0"));

        foreach (var code in new[] { SystemParameterCodes.BackupFullDay, SystemParameterCodes.BackupTime, SystemParameterCodes.BackupKeepWeeks })
            Assert.True((await Put(code, "")).IsDefault);
    }
}
