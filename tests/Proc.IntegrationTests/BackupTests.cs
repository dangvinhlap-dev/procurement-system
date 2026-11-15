using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;
using Proc.Application.Operations;
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
}
