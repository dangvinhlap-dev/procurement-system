using System.Diagnostics;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Proc.Application.Common;
using Proc.Application.Operations;
using Proc.Domain.Operations;
using Proc.Infrastructure.Documents;
using Proc.Infrastructure.Notifications;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.Operations;

/// <summary>
/// Số liệu màn hình SYS cho môi trường đang chạy (design.md mục 21.4, lát QT1): phiên bản, migration đã chạy / đang chờ, dung lượng CSDL, số
/// kết nối, chỗ trống ổ chứa đính kèm và nhật ký vận hành, nhịp dịch vụ nền, cách gửi email. Bảng ba môi trường (TEST / PROD) làm ở lát QT8.
/// </summary>
public sealed class SystemOverviewService : ISystemOverview
{
    /// <summary>Ổ còn trống dưới mức này (%) thì đèn vàng (ke-hoach-qt.md mục 6 – WAL, sao lưu có thể làm đầy ổ).</summary>
    public const double DiskWarningPercent = 15;

    /// <summary>Ổ còn trống dưới mức này (%) thì đèn đỏ.</summary>
    public const double DiskErrorPercent = 5;

    /// <summary>Dịch vụ nền không báo nhịp quá số chu kỳ này thì đèn đỏ.</summary>
    public const int MissedBeats = 3;

    private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

    private readonly ProcDbContext _db;
    private readonly IClock _clock;
    private readonly AppVersion _version;
    private readonly AppEnvironmentOptions _environment;
    private readonly IBackgroundHeartbeat _heartbeat;
    private readonly AttachmentOptions _attachments;
    private readonly OperationsOptions _operations;
    private readonly IOptionsMonitor<EmailOptions> _email;
    private readonly IWalArchiveStatus _walArchive;
    private readonly BackupOptions _backup;

    /// <summary>Tạo dịch vụ.</summary>
    /// <param name="db">DbContext.</param>
    /// <param name="clock">Đồng hồ.</param>
    /// <param name="version">Phiên bản ứng dụng.</param>
    /// <param name="environment">Môi trường đang chạy.</param>
    /// <param name="heartbeat">Nhịp dịch vụ nền.</param>
    /// <param name="attachments">Thư mục đính kèm.</param>
    /// <param name="operations">Thư mục nhật ký vận hành.</param>
    /// <param name="email">Cấu hình email.</param>
    /// <param name="walArchive">Tình trạng lưu WAL (lát QT3a).</param>
    /// <param name="backup">Cấu hình sao lưu (lát QT3b – ổ sao lưu).</param>
    public SystemOverviewService(
        ProcDbContext db, IClock clock, AppVersion version, IOptions<AppEnvironmentOptions> environment, IBackgroundHeartbeat heartbeat,
        IOptions<AttachmentOptions> attachments, IOptions<OperationsOptions> operations, IOptionsMonitor<EmailOptions> email,
        IWalArchiveStatus walArchive, IOptions<BackupOptions> backup)
    {
        _walArchive = walArchive;
        _backup = backup.Value;
        _db = db;
        _clock = clock;
        _version = version;
        _environment = environment.Value;
        _heartbeat = heartbeat;
        _attachments = attachments.Value;
        _operations = operations.Value;
        _email = email;
    }

    /// <inheritdoc />
    public async Task<SystemOverviewDto> GetAsync(CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var applied = (await _db.Database.GetAppliedMigrationsAsync(cancellationToken)).ToList();
        var pending = (await _db.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
        var size = await _db.Database.SqlQueryRaw<long>("SELECT pg_database_size(current_database()) AS \"Value\"").SingleAsync(cancellationToken);
        var connections = await _db.Database
            .SqlQueryRaw<int>("SELECT count(*)::int AS \"Value\" FROM pg_stat_activity WHERE datname = current_database()")
            .SingleAsync(cancellationToken);
        var server = await _db.Database.SqlQueryRaw<string>("SELECT current_setting('server_version') AS \"Value\"").SingleAsync(cancellationToken);

        const string app = "Ứng dụng", database = "Cơ sở dữ liệu", disk = "Ổ đĩa", background = "Dịch vụ nền";
        var started = new DateTimeOffset(Process.GetCurrentProcess().StartTime).ToUniversalTime();
        List<SystemOverviewRowDto> rows =
        [
            new(app, "Môi trường", EnvironmentLabel(_environment.Name),
                !_environment.IsConfigured ? "Error" : _environment.Name == AppEnvironments.Prod ? "Ok" : "Neutral",
                !_environment.IsConfigured
                    ? EnvironmentStatus.NotConfiguredWarning
                    : "Đặt bằng App:Environment trong appsettings.Production.json (Dev / Test / Prod)."),
            new(app, "Phiên bản", _version.Version, "Neutral", null),
            new(app, "Khởi động lúc", Stamp(started), "Neutral", $"Đã chạy {Duration(now - started)}."),
            Email(),
            new(database, "Máy chủ PostgreSQL", server, "Neutral", null),
            new(database, "Migration đã chạy", Number(applied.Count), "Ok", applied.Count > 0 ? $"Mới nhất: {applied[^1]}" : null),
            new(database, "Migration đang chờ", Number(pending.Count), pending.Count == 0 ? "Ok" : "Error",
                pending.Count == 0 ? "CSDL khớp bản cài." : $"Chưa chạy: {string.Join(", ", pending)} – chạy script CSDL của bản cài (docs/phat-hanh.md)."),
            new(database, "Dung lượng CSDL", Bytes(size), "Neutral", null),
            new(database, "Số kết nối đang mở", Number(connections), "Neutral", "Đếm trong pg_stat_activity của CSDL này."),
            await WalArchiveAsync(database, cancellationToken),
            await LastBackupAsync(database, now, cancellationToken),
            Disk(disk, "Ổ chứa file đính kèm", _attachments.RootPath),
            Disk(disk, "Ổ chứa nhật ký vận hành", _operations.LogDirectory),
            string.IsNullOrWhiteSpace(_backup.RootPath)
                ? new(disk, "Ổ sao lưu", "Chưa khai", _environment.Name == AppEnvironments.Dev ? "Neutral" : "Error",
                    "Khai Backup:RootPath (ổ khác ổ dữ liệu CSDL) – docs/deploy-windows.md mục 4b.")
                : Disk(disk, "Ổ sao lưu", _backup.RootPath),
        ];

        var beats = _heartbeat.Snapshot();
        if (beats.Count == 0)
            rows.Add(new(background, "Dịch vụ nền", "Chưa báo nhịp", "Warning", "Vừa khởi động – chờ vòng chạy đầu tiên."));
        foreach (var beat in beats)
        {
            var late = now - beat.LastBeatAt > TimeSpan.FromSeconds((double)beat.IntervalSeconds * MissedBeats);
            rows.Add(new(background, beat.Service, $"Lần chạy cuối {Stamp(beat.LastBeatAt)}", late ? "Error" : "Ok",
                late ? $"Quá {MissedBeats} chu kỳ ({Duration(TimeSpan.FromSeconds(beat.IntervalSeconds))}/lần) không chạy – xem log kỹ thuật." : beat.Detail));
        }

        return new SystemOverviewDto(_environment.Name, _version.Version, now, applied.Count, pending, rows);
    }

    /// <summary>
    /// Dòng email: TEST phải chuyển hết về hộp thư test (E5) – chưa khai thì không gửi (đèn đỏ); PROD đang chuyển hướng là cấu hình nhầm (đèn vàng).
    /// </summary>
    private SystemOverviewRowDto Email()
    {
        var o = _email.CurrentValue;
        var redirect = o.RedirectAllTo?.Trim();
        if (!_environment.IsConfigured)
            return new("Ứng dụng", "Gửi email", "Không gửi", o.Enabled ? "Error" : "Neutral", EnvironmentStatus.NotConfiguredWarning);
        if (!o.Enabled) return new("Ứng dụng", "Gửi email", "Tắt", "Neutral", "Email:Enabled = false – thông báo chỉ có trong phần mềm.");
        if (!string.IsNullOrEmpty(redirect))
            return new("Ứng dụng", "Gửi email", $"Chuyển hết về {redirect}", _environment.Name == AppEnvironments.Prod ? "Warning" : "Ok",
                _environment.Name == AppEnvironments.Prod
                    ? "Máy PROD đang chuyển hướng email – người dùng, NCC không nhận thư. Bỏ Email:RedirectAllTo nếu không cố ý."
                    : "Tiêu đề thư có \"[TEST] gửi cho: …\".");
        return _environment.IsTest
            ? new("Ứng dụng", "Gửi email", "Không gửi", "Error", "Máy TEST chưa khai Email:RedirectAllTo – thư bị chặn, không gửi tới người dùng thật.")
            : new("Ứng dụng", "Gửi email", $"Gửi thật qua {o.Host}", "Ok", null);
    }

    /// <summary>
    /// Dòng "Sao lưu gần nhất" (lát QT3b, design.md 21.4 SYS): bản Full / Diff dùng được gần nhất – quá 24 giờ hoặc chưa có thì đèn đỏ trên TEST /
    /// PROD (DEV trung tính – máy lập trình không bắt buộc sao lưu); ghi chú kèm bản pg_dump gần nhất.
    /// </summary>
    private async Task<SystemOverviewRowDto> LastBackupAsync(string group, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var usable = _db.BackupRuns.AsNoTracking()
            .Where(r => r.Status == Proc.Domain.Operations.BackupRunStatus.Succeeded && r.Verified != false && r.PurgedAt == null);
        var last = await usable.Where(r => r.Kind == Proc.Domain.Operations.BackupKind.Full || r.Kind == Proc.Domain.Operations.BackupKind.Diff)
            .OrderByDescending(r => r.FinishedAt).Select(r => new { r.Kind, r.FinishedAt }).FirstOrDefaultAsync(cancellationToken);
        var dump = await usable.Where(r => r.Kind == Proc.Domain.Operations.BackupKind.Dump)
            .OrderByDescending(r => r.FinishedAt).Select(r => r.FinishedAt).FirstOrDefaultAsync(cancellationToken);
        var strict = _environment.Name != AppEnvironments.Dev;
        var dumpNote = dump is { } d ? $"pg_dump gần nhất {Stamp(d)}." : "Chưa có bản pg_dump.";
        if (last?.FinishedAt is not { } at)
            return new(group, "Sao lưu gần nhất", "Chưa có", strict ? "Error" : "Neutral", $"Chưa có bản Full / Diff dùng được. {dumpNote} Chi tiết: màn hình BAK.");
        var late = now - at > TimeSpan.FromHours(24);
        return new(group, "Sao lưu gần nhất", $"{Proc.Application.Operations.BackupLabels.Kind(last.Kind)} {Stamp(at)}",
            late && strict ? "Error" : late ? "Warning" : "Ok",
            $"{(late ? "Quá 24 giờ chưa có bản sao lưu mới – xem lịch sử ở màn hình BAK. " : string.Empty)}{dumpNote}");
    }

    /// <summary>
    /// Dòng "Lưu WAL" (lát QT3a): trạng thái + độ trễ, chi tiết ở màn hình BAK. Lỗi khi đọc chỉ làm đỏ dòng này, không hỏng cả bảng SYS.
    /// </summary>
    private async Task<SystemOverviewRowDto> WalArchiveAsync(string group, CancellationToken cancellationToken)
    {
        try
        {
            var wal = await _walArchive.GetAsync(cancellationToken);
            var value = wal.LagSeconds is > 0 ? $"{wal.StateLabel} · trễ {Duration(TimeSpan.FromSeconds(wal.LagSeconds.Value))}" : wal.StateLabel;
            return new(group, "Lưu WAL", value, wal.Status, $"{wal.Summary} Chi tiết: màn hình BAK.");
        }
        catch (Exception ex) when (ex is Npgsql.NpgsqlException or InvalidOperationException)
        {
            return new(group, "Lưu WAL", "Không đọc được", "Error", ex.Message);
        }
    }

    /// <summary>Dòng chỗ trống của ổ chứa một thư mục.</summary>
    internal static SystemOverviewRowDto Disk(string group, string item, string directory)
    {
        try
        {
            var root = Path.GetPathRoot(Path.GetFullPath(directory));
            if (string.IsNullOrEmpty(root)) return new(group, item, "Không xác định", "Warning", directory);
            var drive = new DriveInfo(root);
            if (!drive.IsReady) return new(group, item, "Ổ chưa sẵn sàng", "Error", directory);
            var free = drive.TotalSize == 0 ? 0 : drive.AvailableFreeSpace * 100.0 / drive.TotalSize;
            var status = free < DiskErrorPercent ? "Error" : free < DiskWarningPercent ? "Warning" : "Ok";
            // Ví dụ còn 120 GB / 500 GB = 24,0 % trống.
            return new(group, item, $"Trống {Bytes(drive.AvailableFreeSpace)} / {Bytes(drive.TotalSize)} ({free.ToString("N1", Vi)} %)", status,
                $"{directory}{(status == "Ok" ? "" : $" – dưới {(status == "Error" ? DiskErrorPercent : DiskWarningPercent)} % trống, dọn hoặc mở rộng ổ.")}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return new(group, item, "Không đọc được", "Warning", $"{directory} – {ex.Message}");
        }
    }

    /// <summary>Tên môi trường hiển thị.</summary>
    private static string EnvironmentLabel(string name) => name switch
    {
        AppEnvironments.Dev => "DEV – máy lập trình",
        AppEnvironments.Test => "TEST – máy thử nghiệm",
        AppEnvironments.Prod => "PROD – máy chạy thật",
        _ => "Chưa khai",
    };

    /// <summary>Số nguyên kiểu Việt Nam, ví dụ 1.234.</summary>
    private static string Number(long value) => value.ToString("N0", Vi);

    /// <summary>Dung lượng dễ đọc, ví dụ 1.536 MB → "1,5 GB".</summary>
    public static string Bytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return $"{value.ToString(unit == 0 ? "N0" : "N1", Vi)} {units[unit]}";
    }

    /// <summary>Giờ ngày theo giờ Việt Nam, dd/MM/yyyy HH:mm.</summary>
    internal static string Stamp(DateTimeOffset utc) => TimeZoneInfo.ConvertTime(utc, VietnamTime.Zone).ToString("dd/MM/yyyy HH:mm", Vi);

    /// <summary>Khoảng thời gian dễ đọc, ví dụ "2 ngày 3 giờ", "15 phút".</summary>
    internal static string Duration(TimeSpan span) =>
        span.TotalDays >= 1 ? $"{(int)span.TotalDays} ngày {span.Hours} giờ"
        : span.TotalHours >= 1 ? $"{(int)span.TotalHours} giờ {span.Minutes} phút"
        : span.TotalMinutes >= 1 ? $"{(int)span.TotalMinutes} phút"
        : $"{Math.Max(0, (int)span.TotalSeconds)} giây";
}
