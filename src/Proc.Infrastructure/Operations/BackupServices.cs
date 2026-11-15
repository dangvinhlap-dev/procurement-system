using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using Proc.Application.Common;
using Proc.Application.Operations;
using Proc.Domain.Common;
using Proc.Domain.Operations;
using Proc.Infrastructure.Documents;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.Operations;

/// <summary>Lịch sử sao lưu và "Sao lưu ngay" (lát QT3b, màn hình BAK).</summary>
public sealed class BackupService : IBackupService
{
    /// <summary>Số dòng tối đa một lần tìm.</summary>
    public const int MaxRows = 1000;

    private readonly ProcDbContext _db;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;
    private readonly IOperationLog _operationLog;
    private readonly IBackupQueueSignal _signal;
    private readonly BackupOptions _options;

    /// <summary>Tạo dịch vụ.</summary>
    /// <param name="db">CSDL.</param>
    /// <param name="user">Người đang đăng nhập.</param>
    /// <param name="clock">Đồng hồ.</param>
    /// <param name="operationLog">Nhật ký vận hành.</param>
    /// <param name="signal">Đánh thức bộ chạy nền.</param>
    /// <param name="options">Cấu hình sao lưu.</param>
    public BackupService(
        ProcDbContext db, ICurrentUser user, IClock clock, IOperationLog operationLog, IBackupQueueSignal signal, IOptions<BackupOptions> options)
    {
        _db = db;
        _user = user;
        _clock = clock;
        _operationLog = operationLog;
        _signal = signal;
        _options = options.Value;
    }

    /// <inheritdoc />
    public async Task<BackupRunSearchResult> SearchAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        if (to < from) throw new BusinessRuleException("Đến ngày phải sau hoặc bằng từ ngày.");
        var start = VietnamDayStartUtc(from);
        var end = VietnamDayStartUtc(to.AddDays(1));
        var rows = await _db.BackupRuns.AsNoTracking()
            .Where(r => r.QueuedAt >= start && r.QueuedAt < end)
            .OrderByDescending(r => r.QueuedAt)
            .Take(MaxRows + 1)
            .ToListAsync(cancellationToken);
        return new BackupRunSearchResult(rows.Take(MaxRows).Select(ToDto).ToList(), rows.Count > MaxRows);
    }

    /// <inheritdoc />
    public async Task<BackupRunDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var run = await _db.BackupRuns.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy lần sao lưu.");
        return ToDto(run);
    }

    /// <inheritdoc />
    public async Task<BackupRunDto> QueueAsync(QueueBackupRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(_options.RootPath))
            throw new BusinessRuleException("Chưa khai thư mục sao lưu (Backup:RootPath) trên máy chủ – xem docs/deploy-windows.md mục 4b.");
        if (await _db.BackupRuns.AnyAsync(
                r => r.Kind == request.Kind && (r.Status == BackupRunStatus.Queued || r.Status == BackupRunStatus.Running), cancellationToken))
            throw new BusinessRuleException($"Đang có lần sao lưu {BackupLabels.Kind(request.Kind)} chờ hoặc đang chạy – đợi lần đó xong.");

        var run = BackupRun.Queue(request.Kind, BackupTrigger.Manual, null, _user.UserName, _clock.UtcNow);
        _db.BackupRuns.Add(run);
        await _db.SaveChangesAsync(cancellationToken);
        await _operationLog.WriteAsync(
            new OperationLogEntry(OperationKinds.BackupQueued, BackupLabels.Kind(run.Kind), null, OperationResult.Info, "Sao lưu ngay."),
            cancellationToken);
        _signal.Notify();
        return ToDto(run);
    }

    /// <summary>Đổi lần sao lưu sang DTO.</summary>
    /// <param name="r">Lần sao lưu.</param>
    /// <returns>DTO.</returns>
    public static BackupRunDto ToDto(BackupRun r)
    {
        ArgumentNullException.ThrowIfNull(r);
        var (label, light) = BackupLabels.Status(r);
        long? duration = r.StartedAt is { } s && r.FinishedAt is { } f ? (long)(f - s).TotalSeconds : null;
        return new BackupRunDto(r.Id, r.Kind.ToString(), BackupLabels.Kind(r.Kind), r.Trigger.ToString(), r.Status.ToString(), label, light,
            r.ScheduledDate, r.RequestedByName, r.QueuedAt, r.StartedAt, r.FinishedAt, duration, r.RelativePath, r.SizeBytes, r.FileCount,
            r.Sha256, r.BaseFullRunId, r.StartWalFile, r.Verified, r.VerifyMessage, r.SecondaryCopied, r.SecondaryMessage, r.ErrorMessage, r.Note,
            r.PurgedAt);
    }

    private static DateTimeOffset VietnamDayStartUtc(DateOnly day)
    {
        var local = day.ToDateTime(TimeOnly.MinValue);
        return new DateTimeOffset(local, VietnamTime.Zone.GetUtcOffset(local)).ToUniversalTime();
    }
}

/// <summary>Lỗi sao lưu có câu báo dành cho người quản trị (ghi vào lần sao lưu, không kèm ngăn xếp).</summary>
public sealed class BackupFailedException : Exception
{
    /// <summary>Tạo lỗi.</summary>
    /// <param name="message">Câu báo.</param>
    public BackupFailedException(string message)
        : base(message)
    {
    }
}

/// <summary>
/// Bộ chạy sao lưu (lát QT3b, design.md 21.5): Full / Diff bằng <c>pg_basebackup</c> + <c>pg_verifybackup</c>, <c>pg_dump -Fc</c> +
/// <c>pg_restore --list</c>, chép bổ sung đính kèm; SHA256; chép nơi thứ hai. Cấu trúc thư mục sao lưu:
/// <code>
/// &lt;RootPath&gt;\full\20261115-010000\        (thư mục pg_basebackup, có backup_manifest)
/// &lt;RootPath&gt;\diff\20261116-010000\
/// &lt;RootPath&gt;\dump\procurement_20261116-010500.dump
/// &lt;RootPath&gt;\attachments\…                 (chép bổ sung, giữ cấu trúc thư mục đính kèm)
/// </code>
/// Nơi thứ hai có đúng cấu trúc đó. Tên theo giờ Việt Nam (người quản trị tìm theo ngày).
/// </summary>
public sealed class BackupExecutor : IBackupExecutor
{
    /// <summary>Khóa <c>pg_advisory_lock</c> riêng của bộ chạy sao lưu (chữ "PROCBAK" dạng số).</summary>
    public const long AdvisoryLockKey = 0x50524F4342414B;

    private readonly IServiceScopeFactory _scopes;
    private readonly PgToolRunner _tools;
    private readonly IClock _clock;
    private readonly BackupOptions _options;
    private readonly AttachmentOptions _attachments;
    private readonly ILogger<BackupExecutor> _logger;

    /// <summary>Tạo bộ chạy.</summary>
    /// <param name="scopes">Tạo scope DI cho từng lần sao lưu.</param>
    /// <param name="tools">Chạy công cụ PostgreSQL.</param>
    /// <param name="clock">Đồng hồ.</param>
    /// <param name="options">Cấu hình sao lưu.</param>
    /// <param name="attachments">Cấu hình đính kèm (thư mục nguồn).</param>
    /// <param name="logger">Log kỹ thuật.</param>
    public BackupExecutor(
        IServiceScopeFactory scopes, PgToolRunner tools, IClock clock, IOptions<BackupOptions> options, IOptions<AttachmentOptions> attachments,
        ILogger<BackupExecutor> logger)
    {
        _scopes = scopes;
        _tools = tools;
        _clock = clock;
        _options = options.Value;
        _attachments = attachments.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<int> ProcessQueueAsync(CancellationToken cancellationToken)
    {
        await using var lockScope = _scopes.CreateAsyncScope();
        await using var lockConnection = await TryLockAsync(lockScope.ServiceProvider.GetRequiredService<ProcDbContext>(), cancellationToken);
        if (lockConnection is null) return 0;

        var count = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            await using var scope = _scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ProcDbContext>();
            var run = await db.BackupRuns.Where(r => r.Status == BackupRunStatus.Queued).OrderBy(r => r.QueuedAt).FirstOrDefaultAsync(cancellationToken);
            if (run is null) break;

            run.Start(_clock.UtcNow);
            await db.SaveChangesAsync(cancellationToken);
            try
            {
                await ExecuteAsync(run, db, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                run.Fail("Ứng dụng dừng khi đang sao lưu – lần này không dùng được, chạy lại.", _clock.UtcNow);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Sao lưu {Kind} lỗi.", run.Kind);
                run.Fail(ex is BackupFailedException or IOException or UnauthorizedAccessException ? ex.Message : $"{ex.GetType().Name}: {ex.Message}",
                    _clock.UtcNow);
            }

            await db.SaveChangesAsync(CancellationToken.None);
            await WriteLogAsync(scope.ServiceProvider.GetRequiredService<IOperationLog>(), run);
            count++;
        }

        return count;
    }

    /// <inheritdoc />
    public async Task<int> FailInterruptedAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ProcDbContext>();
        await using var lockConnection = await TryLockAsync(db, cancellationToken);
        if (lockConnection is null) return 0;
        var stuck = await db.BackupRuns.Where(r => r.Status == BackupRunStatus.Running).ToListAsync(cancellationToken);
        foreach (var run in stuck) run.Fail("Ứng dụng khởi động lại khi đang sao lưu – lần này không dùng được, chạy lại.", _clock.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        return stuck.Count;
    }

    /// <summary>
    /// Mở kết nối riêng và lấy khóa <c>pg_try_advisory_lock</c> (khóa theo phiên – mất kết nối là nhả). Không lấy được thì trả null.
    /// Kết nối <b>không dùng pool</b>: kết nối trả về pool vẫn giữ phiên nên vẫn giữ khóa tới khi được lấy ra dùng lại (Npgsql chỉ DISCARD ALL lúc
    /// lấy ra) – lần chạy sau lấy kết nối khác trong pool sẽ thấy khóa đang bị giữ và không sao lưu (gặp khi chạy test 15/11/2026).
    /// </summary>
    private static async Task<NpgsqlConnection?> TryLockAsync(ProcDbContext db, CancellationToken cancellationToken)
    {
        var cs = new NpgsqlConnectionStringBuilder(db.Database.GetConnectionString()) { Pooling = false }.ConnectionString;
        var connection = new NpgsqlConnection(cs);
        await connection.OpenAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand("SELECT pg_try_advisory_lock(@k)", connection);
        cmd.Parameters.AddWithValue("k", AdvisoryLockKey);
        if (await cmd.ExecuteScalarAsync(cancellationToken) is true) return connection;
        await connection.DisposeAsync();
        return null;
    }

    private async Task ExecuteAsync(BackupRun run, ProcDbContext db, CancellationToken ct)
    {
        var root = RequireRoot();
        var stamp = TimeZoneInfo.ConvertTime(_clock.UtcNow, VietnamTime.Zone).ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        switch (run.Kind)
        {
            case BackupKind.Full:
            case BackupKind.Diff:
                await RunBaseBackupAsync(run, db, root, stamp, ct);
                break;
            case BackupKind.Dump:
                await RunDumpAsync(run, db, root, stamp, ct);
                break;
            case BackupKind.Attachments:
                await RunAttachmentsAsync(run, root, ct);
                return;
            default:
                throw new BackupFailedException("Kiểu sao lưu không hợp lệ.");
        }

        await CopyToSecondaryAsync(run, root, ct);
    }

    private string RequireRoot()
    {
        if (string.IsNullOrWhiteSpace(_options.RootPath))
            throw new BackupFailedException("Chưa khai thư mục sao lưu (Backup:RootPath).");
        Directory.CreateDirectory(_options.RootPath);
        return _options.RootPath;
    }

    private async Task RunBaseBackupAsync(BackupRun run, ProcDbContext db, string root, string stamp, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.MaintenanceConnectionString))
            throw new BackupFailedException(
                "Chưa khai ConnectionStrings:Maintenance (tài khoản proc_maint có quyền REPLICATION – tạo bằng qt-enable-wal.ps1) nên không chạy " +
                "được sao lưu Full / Diff.");
        var conn = PgConnectionInfo.Parse(_options.MaintenanceConnectionString);

        string? manifest = null;
        if (run.Kind == BackupKind.Diff)
        {
            // Diff luôn lấy bản Full dùng được gần nhất làm gốc (design.md 21.5) – khôi phục chỉ cần Full + một Diff + WAL.
            var baseFull = await db.BackupRuns.AsNoTracking()
                .Where(r => r.Kind == BackupKind.Full && r.Status == BackupRunStatus.Succeeded && r.Verified != false && r.PurgedAt == null)
                .OrderByDescending(r => r.FinishedAt)
                .FirstOrDefaultAsync(ct)
                ?? throw new BackupFailedException("Chưa có bản Full dùng được làm gốc – chạy sao lưu Full trước.");
            manifest = Path.Combine(root, baseFull.RelativePath!, "backup_manifest");
            if (!File.Exists(manifest))
                throw new BackupFailedException($"Không thấy backup_manifest của bản Full gốc ({baseFull.RelativePath}) – chạy sao lưu Full mới.");
            run.SetBaseFull(baseFull.Id);
        }

        var rel = UniquePath(root, Path.Combine(run.Kind == BackupKind.Full ? "full" : "diff", stamp));
        var dir = Path.Combine(root, rel);
        Directory.CreateDirectory(Path.GetDirectoryName(dir)!);
        var label = $"Procurement {BackupLabels.Kind(run.Kind)} {stamp}";
        // Công cụ chạy trong thư mục cha (full / diff), tham số chỉ là tên thư mục không dấu – xem PgToolRunner.RunAsync.
        var parent = Path.GetDirectoryName(dir)!;
        var relManifest = manifest is null ? null : Path.GetRelativePath(parent, manifest);
        var result = await _tools.RunAsync("pg_basebackup", PgToolArgs.BaseBackup(conn, Path.GetFileName(dir), label, relManifest), conn.Password,
            parent, ct);
        if (!result.Succeeded)
        {
            BackupFiles.DeleteIfExists(dir);
            throw new BackupFailedException(result.Describe("pg_basebackup"));
        }

        var manifestFile = Path.Combine(dir, "backup_manifest");
        var sha = File.Exists(manifestFile) ? await BackupFiles.Sha256Async(manifestFile, ct) : null;
        var (bytes, files) = BackupFiles.DirectoryStats(dir);
        var labelFile = Path.Combine(dir, "backup_label");
        var startWal = PgToolArgs.ParseStartWalFile(File.Exists(labelFile) ? await File.ReadAllTextAsync(labelFile, ct) : null);
        run.Complete(rel, bytes, files, sha, startWal, null, _clock.UtcNow);

        var verify = await _tools.RunAsync("pg_verifybackup", PgToolArgs.VerifyBackup(rel), null, root, ct);
        run.SetVerification(verify.Succeeded, verify.Succeeded ? "pg_verifybackup: đạt." : verify.Describe("pg_verifybackup"));
    }

    private async Task RunDumpAsync(BackupRun run, ProcDbContext db, string root, string stamp, CancellationToken ct)
    {
        // Database của ứng dụng lấy từ chuỗi kết nối chính; tài khoản dùng proc_maint khi có (pg_read_all_data), không thì tài khoản ứng dụng
        // (chủ sở hữu các bảng nên dump được).
        var main = PgConnectionInfo.Parse(db.Database.GetConnectionString()!);
        var conn = string.IsNullOrWhiteSpace(_options.MaintenanceConnectionString) ? main : PgConnectionInfo.Parse(_options.MaintenanceConnectionString);
        var database = main.Database ?? throw new BackupFailedException("Chuỗi kết nối chính không có tên database.");

        // Tên file chỉ giữ chữ / số không dấu (tham số lệnh phải ASCII – PgToolRunner.RunAsync).
        var safeName = new string(database.Select(c => char.IsAsciiLetterOrDigit(c) ? c : '_').ToArray());
        var rel = UniquePath(root, Path.Combine("dump", $"{safeName}_{stamp}.dump"));
        var file = Path.Combine(root, rel);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        var partial = file + BackupFiles.PartialSuffix;
        var dumpDir = Path.GetDirectoryName(file)!;
        var result = await _tools.RunAsync("pg_dump", PgToolArgs.Dump(conn, database, Path.GetFileName(partial)), conn.Password, dumpDir, ct);
        if (!result.Succeeded)
        {
            BackupFiles.DeleteIfExists(partial);
            throw new BackupFailedException(result.Describe("pg_dump"));
        }

        File.Move(partial, file, overwrite: true);
        var sha = await BackupFiles.Sha256Async(file, ct);
        run.Complete(rel, new FileInfo(file).Length, 1, sha, null, null, _clock.UtcNow);

        var list = await _tools.RunAsync("pg_restore", PgToolArgs.RestoreList(Path.GetFileName(file)), null, dumpDir, ct);
        run.SetVerification(list.Succeeded && list.StdOutLines > 0,
            list.Succeeded
                ? $"pg_restore --list: đọc được mục lục ({list.StdOutLines.ToString(CultureInfo.InvariantCulture)} dòng)."
                : list.Describe("pg_restore --list"));
    }

    private async Task RunAttachmentsAsync(BackupRun run, string root, CancellationToken ct)
    {
        // Chép bổ sung từ thư mục đính kèm thật sang cả nơi chính và nơi thứ hai (không chép lại từ nơi chính – nơi chính hỏng không lan sang).
        var source = _attachments.RootPath;
        var rel = "attachments";
        var primary = await BackupFiles.MirrorAsync(source, Path.Combine(root, rel), ct);
        var note = $"Chép {primary.CopiedFiles.ToString(CultureInfo.InvariantCulture)} file mới / đổi; thư mục đính kèm có " +
                   $"{primary.TotalFiles.ToString(CultureInfo.InvariantCulture)} file.";
        run.Complete(rel, primary.CopiedBytes, primary.CopiedFiles, null, null, note, _clock.UtcNow);
        run.SetVerification(primary.MissingAfterCopy == 0,
            primary.MissingAfterCopy == 0
                ? "Đối chiếu: mọi file đính kèm đã có trong bản sao lưu."
                : $"Đối chiếu: còn {primary.MissingAfterCopy.ToString(CultureInfo.InvariantCulture)} file chưa có hoặc khác dung lượng.");

        if (string.IsNullOrWhiteSpace(_options.SecondaryPath)) return;
        try
        {
            var second = await BackupFiles.MirrorAsync(source, Path.Combine(_options.SecondaryPath, rel), ct);
            run.SetSecondary(second.MissingAfterCopy == 0,
                second.MissingAfterCopy == 0
                    ? $"Đã chép bổ sung {second.CopiedFiles.ToString(CultureInfo.InvariantCulture)} file sang nơi thứ hai."
                    : $"Nơi thứ hai còn {second.MissingAfterCopy.ToString(CultureInfo.InvariantCulture)} file chưa có.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            run.SetSecondary(false, $"Không chép được sang nơi thứ hai: {ex.Message}");
        }
    }

    /// <summary>
    /// Chép bản vừa sao lưu sang nơi thứ hai (B4) – chỉ khi bản đạt kiểm tra; đối chiếu dung lượng, số file (và SHA256 với file dump). Lỗi ở đây
    /// không làm hỏng lần sao lưu (bản ở nơi chính vẫn dùng được) – chỉ đèn vàng.
    /// </summary>
    private async Task CopyToSecondaryAsync(BackupRun run, string root, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.SecondaryPath) || run.RelativePath is null) return;
        if (run.Verified == false)
        {
            run.SetSecondary(false, "Không chép sang nơi thứ hai vì bản sao lưu không đạt kiểm tra.");
            return;
        }

        try
        {
            var source = Path.Combine(root, run.RelativePath);
            var target = Path.Combine(_options.SecondaryPath, run.RelativePath);
            var (bytes, files) = await BackupFiles.CopyBackupAsync(source, target, ct);
            var ok = bytes == run.SizeBytes && files == run.FileCount;
            if (ok && run.Kind == BackupKind.Dump) ok = await BackupFiles.Sha256Async(target, ct) == run.Sha256;
            run.SetSecondary(ok, ok ? "Đã chép sang nơi thứ hai, đối chiếu khớp." : "Bản ở nơi thứ hai không khớp dung lượng / mã kiểm tra.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            run.SetSecondary(false, $"Không chép được sang nơi thứ hai: {ex.Message}");
        }
    }

    /// <summary>Thêm hậu tố -2, -3… khi đã có file / thư mục cùng tên (hai lần sao lưu trong cùng một giây).</summary>
    private static string UniquePath(string root, string rel)
    {
        var candidate = rel;
        for (var i = 2; File.Exists(Path.Combine(root, candidate)) || Directory.Exists(Path.Combine(root, candidate)); i++)
        {
            var ext = Path.GetExtension(rel);
            candidate = ext.Length > 0
                ? rel[..^ext.Length] + "-" + i.ToString(CultureInfo.InvariantCulture) + ext
                : rel + "-" + i.ToString(CultureInfo.InvariantCulture);
        }

        return candidate;
    }

    private async Task WriteLogAsync(IOperationLog log, BackupRun run)
    {
        try
        {
            var (label, light) = BackupLabels.Status(run);
            var result = run.Status == BackupRunStatus.Failed || light == "Error" ? OperationResult.Failed : OperationResult.Succeeded;
            var detail = run.Status == BackupRunStatus.Failed
                ? run.ErrorMessage
                : $"{label} · {run.RelativePath} · {run.SizeBytes?.ToString(CultureInfo.InvariantCulture)} byte · {run.VerifyMessage}";
            await log.WriteAsync(new OperationLogEntry(OperationKinds.BackupFinished, BackupLabels.Kind(run.Kind),
                run.Trigger == BackupTrigger.Schedule ? "Theo lịch" : $"Sao lưu ngay – {run.RequestedByName}", result, detail), CancellationToken.None);
        }
        catch (Exception ex)
        {
            // Nhật ký vận hành đã tự ghi ra file khi CSDL lỗi; lỗi ở đây không được làm hỏng kết quả sao lưu.
            _logger.LogWarning(ex, "Không ghi được nhật ký vận hành cho lần sao lưu {Id}.", run.Id);
        }
    }
}

/// <summary>Tín hiệu đánh thức bộ chạy nền (singleton).</summary>
public sealed class BackupQueueSignal : IBackupQueueSignal
{
    private readonly SemaphoreSlim _semaphore = new(0, 1);

    /// <inheritdoc />
    public void Notify()
    {
        // Đã có tín hiệu chờ thì thôi (tối đa 1) – bộ chạy luôn chạy hết hàng chờ mỗi lần thức.
        try
        {
            _semaphore.Release();
        }
        catch (SemaphoreFullException)
        {
        }
    }

    /// <inheritdoc />
    public Task WaitAsync(TimeSpan timeout, CancellationToken cancellationToken) => _semaphore.WaitAsync(timeout, cancellationToken);
}

/// <summary>
/// Dịch vụ nền chạy hàng chờ sao lưu (lát QT3b): khởi động thì ghi lỗi các lần "Đang chạy" của tiến trình cũ, rồi thức khi có lần mới (tín hiệu)
/// hoặc mỗi phút (lần do tiến trình khác đặt). Lát QT3b-2 thêm phần đặt lịch và dọn bản cũ.
/// </summary>
public sealed class BackupBackgroundService : BackgroundService
{
    /// <summary>Tên dịch vụ trên màn hình SYS.</summary>
    public const string ServiceName = "Sao lưu";

    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    private readonly IBackupExecutor _executor;
    private readonly IBackupQueueSignal _signal;
    private readonly IBackgroundHeartbeat _heartbeat;
    private readonly BackupOptions _options;
    private readonly ILogger<BackupBackgroundService> _logger;

    /// <summary>Tạo dịch vụ nền.</summary>
    /// <param name="executor">Bộ chạy.</param>
    /// <param name="signal">Tín hiệu có việc mới.</param>
    /// <param name="heartbeat">Báo nhịp cho SYS.</param>
    /// <param name="options">Cấu hình sao lưu.</param>
    /// <param name="logger">Log kỹ thuật.</param>
    public BackupBackgroundService(
        IBackupExecutor executor, IBackupQueueSignal signal, IBackgroundHeartbeat heartbeat, IOptions<BackupOptions> options,
        ILogger<BackupBackgroundService> logger)
    {
        _executor = executor;
        _signal = signal;
        _heartbeat = heartbeat;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.WorkerEnabled) return;
        // Nhịp ghi theo chu kỳ dài (một lần sao lưu Full có thể chạy hàng giờ – SYS không báo dịch vụ ngừng trong lúc đó).
        var beatSeconds = (int)TimeSpan.FromMinutes(Math.Max(1, _options.ToolTimeoutMinutes)).TotalSeconds;
        _heartbeat.Beat(ServiceName, beatSeconds, "Khởi động.");
        try
        {
            var stuck = await _executor.FailInterruptedAsync(stoppingToken);
            if (stuck > 0) _logger.LogWarning("Ghi lỗi {Count} lần sao lưu dừng giữa chừng do ứng dụng khởi động lại.", stuck);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Không kiểm tra được lần sao lưu dừng giữa chừng.");
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            string? note = null;
            try
            {
                var done = await _executor.ProcessQueueAsync(stoppingToken);
                if (done > 0) note = $"Vừa chạy {done.ToString(CultureInfo.InvariantCulture)} lần sao lưu.";
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Bộ chạy sao lưu lỗi.");
                note = $"Lỗi: {ex.Message}";
            }

            _heartbeat.Beat(ServiceName, beatSeconds, note);
            try
            {
                await _signal.WaitAsync(Interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }
}
