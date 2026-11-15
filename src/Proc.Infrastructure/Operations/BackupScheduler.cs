using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Proc.Application.Common;
using Proc.Application.Operations;
using Proc.Domain.Notifications;
using Proc.Domain.Operations;
using Proc.Domain.Organization;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.Operations;

/// <summary>
/// Lịch sao lưu và dọn bản quá hạn (lát QT3b-2, design.md mục 21.5, B1 – quy tắc ở <see cref="BackupPlanner"/>). Tham số lịch lấy của tổng công
/// ty (sao lưu là của cả hệ thống).
/// </summary>
public sealed class BackupScheduler : IBackupScheduler
{
    private readonly ProcDbContext _db;
    private readonly IClock _clock;
    private readonly IBackupQueueSignal _signal;
    private readonly PgToolRunner _tools;
    private readonly IOperationLog _operationLog;
    private readonly BackupOptions _options;
    private readonly ILogger<BackupScheduler> _logger;

    /// <summary>Tạo dịch vụ.</summary>
    /// <param name="db">CSDL.</param>
    /// <param name="clock">Đồng hồ.</param>
    /// <param name="signal">Đánh thức bộ chạy.</param>
    /// <param name="tools">Chạy pg_archivecleanup.</param>
    /// <param name="operationLog">Nhật ký vận hành.</param>
    /// <param name="options">Cấu hình sao lưu.</param>
    /// <param name="logger">Log kỹ thuật.</param>
    public BackupScheduler(
        ProcDbContext db, IClock clock, IBackupQueueSignal signal, PgToolRunner tools, IOperationLog operationLog, IOptions<BackupOptions> options,
        ILogger<BackupScheduler> logger)
    {
        _db = db;
        _clock = clock;
        _signal = signal;
        _tools = tools;
        _operationLog = operationLog;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>Đọc tham số lịch của tổng công ty (chưa có dòng tham số thì mặc định B1).</summary>
    /// <param name="db">CSDL.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Tham số.</returns>
    public static async Task<BackupScheduleSettings> LoadSettingsAsync(ProcDbContext db, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);
        var p = await db.CompanyParameters.IgnoreQueryFilters()
            .Where(x => db.Companies.IgnoreQueryFilters().Any(c => c.Id == x.CompanyId && c.IsHeadOffice))
            .Select(x => new BackupScheduleSettings(x.BackupFullDay, x.BackupTime, x.BackupKeepWeeks, x.DumpKeepDays))
            .FirstOrDefaultAsync(cancellationToken);
        return p ?? new BackupScheduleSettings(CompanyParameter.DefaultBackupFullDay, CompanyParameter.DefaultBackupTime,
            CompanyParameter.DefaultBackupKeepWeeks, CompanyParameter.DefaultDumpKeepDays);
    }

    /// <inheritdoc />
    public async Task<int> PlanAsync(CancellationToken cancellationToken)
    {
        if (!_options.ScheduleEnabled || string.IsNullOrWhiteSpace(_options.RootPath)) return 0;
        var settings = await LoadSettingsAsync(_db, cancellationToken);
        var now = _clock.UtcNow;
        if (BackupPlanner.DueSlot(TimeZoneInfo.ConvertTime(now, VietnamTime.Zone).DateTime, settings.Time) is not { } slot) return 0;

        var existing = await _db.BackupRuns.Where(r => r.ScheduledDate == slot).Select(r => r.Kind).ToListAsync(cancellationToken);
        var kinds = new List<BackupKind>();
        if (!existing.Any(k => k is BackupKind.Full or BackupKind.Diff))
        {
            var hasFull = await _db.BackupRuns.AnyAsync(r => r.Kind == BackupKind.Full && r.Status == BackupRunStatus.Succeeded
                && r.Verified != false && r.PurgedAt == null, cancellationToken);
            kinds.Add(BackupPlanner.BaseKind(slot, settings.FullDay, hasFull));
        }

        if (!existing.Contains(BackupKind.Dump)) kinds.Add(BackupKind.Dump);
        if (!existing.Contains(BackupKind.Attachments)) kinds.Add(BackupKind.Attachments);
        if (kinds.Count == 0) return 0;

        // Cách nhau 1 ms để bộ chạy lấy đúng thứ tự Full / Diff → dump → đính kèm (theo giờ vào hàng chờ).
        for (var i = 0; i < kinds.Count; i++)
            _db.BackupRuns.Add(BackupRun.Queue(kinds[i], BackupTrigger.Schedule, slot, null, now.AddMilliseconds(i)));
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: Npgsql.PostgresErrorCodes.UniqueViolation })
        {
            // Tiến trình khác vừa đặt cùng lượt lịch (khóa duy nhất Kind + ScheduledDate) – để lần kiểm tra sau đặt phần còn thiếu.
            _db.ChangeTracker.Clear();
            return 0;
        }

        _signal.Notify();
        return kinds.Count;
    }

    /// <inheritdoc />
    public async Task<BackupCleanupResult> CleanupAsync(CancellationToken cancellationToken)
    {
        var messages = new List<string>();
        if (string.IsNullOrWhiteSpace(_options.RootPath)) return new BackupCleanupResult(0, null, messages);
        var settings = await LoadSettingsAsync(_db, cancellationToken);
        var now = _clock.UtcNow;

        var live = await _db.BackupRuns
            .Where(r => r.Status == BackupRunStatus.Succeeded && r.PurgedAt == null && r.Kind != BackupKind.Attachments)
            .ToListAsync(cancellationToken);
        var purged = 0;
        foreach (var run in BackupPlanner.SelectPurge(live, now, settings))
        {
            try
            {
                if (run.RelativePath is { } rel)
                {
                    BackupFiles.DeleteIfExists(Path.Combine(_options.RootPath, rel));
                    if (!string.IsNullOrWhiteSpace(_options.SecondaryPath)) BackupFiles.DeleteIfExists(Path.Combine(_options.SecondaryPath, rel));
                }

                run.MarkPurged(now);
                purged++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                messages.Add($"Không xóa được {run.RelativePath}: {ex.Message}");
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        if (purged > 0) messages.Add($"Đã dọn {purged.ToString(CultureInfo.InvariantCulture)} bản quá hạn giữ.");

        var oldestWal = BackupPlanner.OldestNeededWal(live.Where(r => r.PurgedAt is null));
        var walCleaned = await CleanWalAsync(oldestWal, messages, cancellationToken);
        await CheckDiskAsync(messages, now, cancellationToken);

        if (purged > 0 || walCleaned || messages.Count > 0)
        {
            var failed = messages.Any(m => m.StartsWith("Không", StringComparison.Ordinal) || m.Contains("lỗi", StringComparison.Ordinal));
            await _operationLog.WriteAsync(new OperationLogEntry(OperationKinds.BackupCleanup, "Sao lưu", null,
                failed ? OperationResult.Failed : OperationResult.Succeeded, string.Join(" ", messages)), cancellationToken);
        }

        return new BackupCleanupResult(purged, walCleaned ? oldestWal : null, messages);
    }

    /// <summary>
    /// Dọn WAL cũ bằng <c>pg_archivecleanup</c> (chạy trong thư mục WAL, tham số "." – tên thư mục có dấu không vào tham số lệnh). Không có bản Full
    /// nào ghi file WAL đầu, chưa khai / không thấy thư mục WAL thì bỏ qua.
    /// </summary>
    private async Task<bool> CleanWalAsync(string? oldestWal, List<string> messages, CancellationToken cancellationToken)
    {
        if (oldestWal is null || string.IsNullOrWhiteSpace(_options.WalArchivePath) || !Directory.Exists(_options.WalArchivePath)) return false;
        var result = await _tools.RunAsync("pg_archivecleanup", PgToolArgs.ArchiveCleanup(".", oldestWal), null, _options.WalArchivePath,
            cancellationToken);
        if (result.Succeeded)
        {
            messages.Add($"Đã dọn WAL cũ hơn {oldestWal}.");
            return true;
        }

        messages.Add($"Dọn WAL lỗi – {result.Describe("pg_archivecleanup")}");
        return false;
    }

    /// <summary>Ổ sao lưu (nơi chính, nơi thứ hai) còn dưới 15 % thì thông báo – mỗi ngày tối đa một lần.</summary>
    private async Task CheckDiskAsync(List<string> messages, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var low = new List<string>();
        foreach (var (item, path) in new[] { ("Ổ sao lưu", _options.RootPath), ("Nơi lưu thứ hai", _options.SecondaryPath) })
        {
            if (string.IsNullOrWhiteSpace(path)) continue;
            var row = SystemOverviewService.Disk("Sao lưu", item, path);
            if (row.Status is "Warning" or "Error" && row.Value.StartsWith("Trống", StringComparison.Ordinal)) low.Add($"{item}: {row.Value}");
        }

        if (low.Count == 0) return;
        var text = $"Sắp đầy – {string.Join("; ", low)}.";
        messages.Add(text);
        var today = TimeZoneInfo.ConvertTime(now, VietnamTime.Zone).Date;
        var startOfDay = new DateTimeOffset(today, VietnamTime.Zone.GetUtcOffset(today)).ToUniversalTime();
        if (await _db.Notifications.IgnoreQueryFilters()
                .AnyAsync(n => n.Type == NotificationTypes.BackupProblem && n.TargetId == null && n.CreatedAt >= startOfDay, cancellationToken))
            return;
        await BackupNotifier.NotifyAsync(_db, "Ổ sao lưu sắp đầy", $"{text} Dọn bớt hoặc mở rộng ổ – màn hình BAK.", null, now, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogWarning("Ổ sao lưu sắp đầy: {Text}", text);
    }
}

/// <summary>Thông báo trong phần mềm về sao lưu cho người có quyền chạy sao lưu ở tổng công ty (không email – như E10).</summary>
public static class BackupNotifier
{
    /// <summary>Lý do bỏ qua email.</summary>
    public const string NoEmailReason = "Thông báo sao lưu chỉ hiện trong phần mềm, không gửi email.";

    /// <summary>Thêm thông báo (chưa lưu – người gọi SaveChanges).</summary>
    /// <param name="db">CSDL.</param>
    /// <param name="title">Tiêu đề.</param>
    /// <param name="message">Nội dung.</param>
    /// <param name="runId">Lần sao lưu liên quan; null = thông báo chung (ổ đầy).</param>
    /// <param name="now">Thời điểm.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Số người nhận.</returns>
    public static async Task<int> NotifyAsync(
        ProcDbContext db, string title, string message, Guid? runId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);
        var headOffice = await db.Companies.IgnoreQueryFilters().Where(c => c.IsHeadOffice && c.IsActive).Select(c => (Guid?)c.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (headOffice is not { } companyId) return 0;
        // Cùng cách chọn người nhận của thông báo nhật ký lỗi (hợp quyền mọi vai trò còn dùng tại tổng công ty).
        var holders = db.UserCompanyRoles
            .Where(x => x.CompanyId == companyId && x.Company!.IsActive && x.Role!.IsActive)
            .Where(x => db.RolePermissions.Any(rp => rp.RoleId == x.RoleId && rp.PermissionCode == Permissions.System.OpsBackupRun))
            .Select(x => x.UserId);
        var users = await db.Users.Where(u => u.IsActive && holders.Contains(u.Id)).Select(u => u.Id).ToListAsync(cancellationToken);
        foreach (var userId in users.Distinct())
        {
            var n = Notification.Create(companyId, userId, NotificationTypes.BackupProblem, title, message, "BAK", runId, now);
            n.SkipEmail(now, NoEmailReason);
            db.Notifications.Add(n);
        }

        return users.Count;
    }
}
