using System.Data.Common;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using Proc.Application.Common;
using Proc.Application.Operations;
using Proc.Domain.Operations;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.Operations;

/// <summary>Một tham số PostgreSQL đọc từ <c>pg_settings</c>.</summary>
/// <param name="Setting">Giá trị đang chạy (theo đơn vị <paramref name="Unit"/>).</param>
/// <param name="Unit">Đơn vị của <c>pg_settings</c> (s, min, B…); null = không đơn vị.</param>
/// <param name="PendingRestart">Đã đổi trong file cấu hình nhưng cần khởi động lại PostgreSQL mới có hiệu lực.</param>
public sealed record WalSetting(string Setting, string? Unit, bool PendingRestart);

/// <summary>Thống kê <c>pg_stat_archiver</c> (tính từ lần đặt lại thống kê).</summary>
/// <param name="ArchivedCount">Số file đã chép thành công.</param>
/// <param name="LastArchivedWal">Tên file chép thành công gần nhất.</param>
/// <param name="LastArchivedAt">Giờ chép thành công gần nhất (UTC).</param>
/// <param name="FailedCount">Số lần chép lỗi.</param>
/// <param name="LastFailedWal">Tên file chép lỗi gần nhất.</param>
/// <param name="LastFailedAt">Giờ chép lỗi gần nhất (UTC).</param>
public sealed record WalArchiverStats(
    long ArchivedCount, string? LastArchivedWal, DateTimeOffset? LastArchivedAt, long FailedCount, string? LastFailedWal, DateTimeOffset? LastFailedAt);

/// <summary>Số liệu thô đọc từ máy chủ CSDL để đánh giá lưu WAL.</summary>
/// <param name="Settings">Tham số theo tên; tham số chỉ quản trị mới xem được (<c>archive_command</c>) vắng khi tài khoản thiếu quyền.</param>
/// <param name="Archiver">Thống kê chép.</param>
/// <param name="CurrentWalFile">File WAL đang ghi; null khi máy đang ở chế độ phục hồi.</param>
public sealed record WalArchiveSnapshot(IReadOnlyDictionary<string, WalSetting> Settings, WalArchiverStats Archiver, string? CurrentWalFile);

/// <summary>Bối cảnh ứng dụng khi đánh giá (không đọc từ CSDL).</summary>
/// <param name="Environment">Dev / Test / Prod / None – máy DEV không bắt buộc lưu WAL.</param>
/// <param name="ArchiveFolderRow">Dòng chỗ trống ổ của thư mục WAL (đã dựng sẵn) hoặc dòng "chưa khai".</param>
/// <param name="UsesMaintenanceAccount">Đọc bằng tài khoản bảo trì <c>ConnectionStrings:Maintenance</c>.</param>
/// <param name="MaintenanceError">Lỗi kết nối tài khoản bảo trì (đã chuyển sang tài khoản ứng dụng); null = không lỗi.</param>
public sealed record WalArchiveContext(string Environment, SystemOverviewRowDto ArchiveFolderRow, bool UsesMaintenanceAccount, string? MaintenanceError);

/// <summary>
/// Đánh giá tình trạng lưu WAL từ số liệu thô (hàm thuần – test không cần PostgreSQL bật lưu WAL). Quy tắc (design.md mục 21.5, ke-hoach-qt.md QT3a):
/// <list type="number">
/// <item>Chưa bật: <c>archive_mode</c> không phải on/always hoặc <c>wal_level = minimal</c>; nếu đã đổi trong file mà chưa khởi động lại thì là
/// "Chờ khởi động lại".</item>
/// <item>Có lỗi: bật mà <c>archive_command</c> và <c>archive_library</c> đều trống (WAL dồn trong pg_wal), hoặc lần chép lỗi gần nhất sau lần chép
/// thành công gần nhất.</item>
/// <item>Trễ: còn file WAL đã đầy chưa chép và lần chép thành công gần nhất cách quá 15 phút (mục tiêu mất tối đa 5 phút dữ liệu –
/// <c>archive_timeout = 300</c>).</item>
/// <item>Còn lại: Đang chạy.</item>
/// </list>
/// </summary>
public static class WalArchiveEvaluator
{
    /// <summary>Trễ quá mức này thì báo "Trễ" (ke-hoach-qt.md QT3a).</summary>
    public static readonly TimeSpan LagLimit = TimeSpan.FromMinutes(15);

    /// <summary><c>archive_timeout</c> tối đa (giây) để mất tối đa 5 phút dữ liệu.</summary>
    public const int MaxArchiveTimeoutSeconds = 300;

    /// <summary>
    /// <c>wal_summary_keep_time</c> tối thiểu (ngày): sao lưu Diff (QT3b) cần tóm tắt WAL từ bản Full gốc – Full mỗi tuần (B1) nên phải giữ hơn
    /// 7 ngày; script đặt 35 ngày cho dư.
    /// </summary>
    public const int MinSummaryKeepDays = 8;

    private const string Group = "Lưu WAL", Parameters = "Tham số PostgreSQL", Stats = "Thống kê chép", Storage = "Nơi lưu";

    /// <summary>Chữ hiển thị của trạng thái.</summary>
    /// <param name="state">Mã trạng thái.</param>
    /// <returns>Chữ tiếng Việt.</returns>
    public static string Label(string state) => state switch
    {
        WalArchiveStates.NotEnabled => "Chưa bật",
        WalArchiveStates.PendingRestart => "Chờ khởi động lại",
        WalArchiveStates.Running => "Đang chạy",
        WalArchiveStates.Lagging => "Trễ > 15 phút",
        WalArchiveStates.Failing => "Có lỗi",
        _ => state,
    };

    /// <summary>
    /// Số thứ tự file WAL (bỏ timeline): tên 24 ký tự hex TTTTTTTT XXXXXXXX YYYYYYYY, số = X × (2^32 / cỡ file) + Y. Ví dụ cỡ 16 MB (256 file mỗi
    /// X): <c>000000010000000200000003</c> → 2 × 256 + 3 = 515. Tên có đuôi (<c>.partial</c>, <c>.backup</c>) lấy 24 ký tự đầu; file
    /// <c>.history</c> hoặc tên lạ trả null.
    /// </summary>
    /// <param name="fileName">Tên file WAL.</param>
    /// <param name="segmentSize">Cỡ file WAL (byte), mặc định 16 MB.</param>
    /// <returns>Số thứ tự hoặc null.</returns>
    public static long? SegmentNumber(string? fileName, long segmentSize)
    {
        if (fileName is null || fileName.Length < 24 || segmentSize <= 0) return null;
        if (fileName.Length > 24 && fileName[24] != '.') return null;
        if (!ulong.TryParse(fileName.AsSpan(8, 8), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var log)
            || !ulong.TryParse(fileName.AsSpan(16, 8), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var seg)
            || !uint.TryParse(fileName.AsSpan(0, 8), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out _))
            return null;
        var perLog = (long)(0x1_0000_0000UL / (ulong)segmentSize);
        return (long)log * perLog + (long)seg;
    }

    /// <summary>
    /// Số file WAL đã đầy chưa chép = số thứ tự file đang ghi − số thứ tự file chép cuối − 1 (file đang ghi chưa đầy nên chưa đến lượt chép).
    /// Ví dụ đang ghi 515, chép cuối 512 → 2 file chờ (513, 514).
    /// </summary>
    /// <param name="currentWalFile">File đang ghi.</param>
    /// <param name="lastArchivedWal">File chép cuối.</param>
    /// <param name="segmentSize">Cỡ file WAL (byte).</param>
    /// <returns>Số file chờ (≥ 0) hoặc null khi không tính được.</returns>
    public static long? PendingSegments(string? currentWalFile, string? lastArchivedWal, long segmentSize)
    {
        var current = SegmentNumber(currentWalFile, segmentSize);
        var last = SegmentNumber(lastArchivedWal, segmentSize);
        return current is null || last is null ? null : Math.Max(0, current.Value - last.Value - 1);
    }

    /// <summary>Đánh giá và dựng kết quả cho màn hình BAK.</summary>
    /// <param name="snapshot">Số liệu thô.</param>
    /// <param name="context">Bối cảnh ứng dụng.</param>
    /// <param name="now">Giờ hiện tại (UTC).</param>
    /// <returns>Tình trạng.</returns>
    public static WalArchiveStatusDto Evaluate(WalArchiveSnapshot snapshot, WalArchiveContext context, DateTimeOffset now)
    {
        var s = snapshot.Settings;
        var a = snapshot.Archiver;
        var level = Value(s, "wal_level");
        var mode = Value(s, "archive_mode");
        var command = Value(s, "archive_command");
        var library = Value(s, "archive_library");
        var commandVisible = s.ContainsKey("archive_command");
        var segmentSize = long.TryParse(Value(s, "wal_segment_size"), CultureInfo.InvariantCulture, out var size) ? size : 16L * 1024 * 1024;
        var dev = context.Environment == AppEnvironments.Dev;

        var levelOk = level is "replica" or "logical";
        var modeOk = mode is "on" or "always";
        var restartPending = Pending(s, "wal_level") || Pending(s, "archive_mode");
        var commandEmpty = commandVisible && string.IsNullOrWhiteSpace(command) && string.IsNullOrWhiteSpace(library);
        var failedLast = a.LastFailedAt is not null && (a.LastArchivedAt is null || a.LastFailedAt > a.LastArchivedAt);
        var pending = snapshot.CurrentWalFile is null ? null : PendingSegments(snapshot.CurrentWalFile, a.LastArchivedWal, segmentSize);
        var sinceLast = a.LastArchivedAt is null ? (TimeSpan?)null : now - a.LastArchivedAt.Value;

        string state;
        if (!levelOk || !modeOk) state = restartPending ? WalArchiveStates.PendingRestart : WalArchiveStates.NotEnabled;
        else if (commandEmpty || failedLast) state = WalArchiveStates.Failing;
        else if (pending > 0 && sinceLast > LagLimit) state = WalArchiveStates.Lagging;
        else state = WalArchiveStates.Running;

        long? lag = state is WalArchiveStates.NotEnabled or WalArchiveStates.PendingRestart || pending is null || sinceLast is null
            ? null
            : pending > 0 ? (long)sinceLast.Value.TotalSeconds : 0;

        var status = state switch
        {
            WalArchiveStates.NotEnabled => dev ? "Neutral" : "Error",
            WalArchiveStates.PendingRestart => "Warning",
            WalArchiveStates.Running => a.LastArchivedAt is null ? "Warning" : "Ok",
            _ => "Error",
        };

        var summary = state switch
        {
            WalArchiveStates.NotEnabled => dev
                ? "Máy DEV không bắt buộc lưu WAL. Máy TEST / PROD: chạy deploy/windows/qt-enable-wal.ps1 rồi khởi động lại PostgreSQL (docs/deploy-windows.md)."
                : "Chưa bật lưu WAL – CSDL hỏng chỉ khôi phục được về bản sao lưu gần nhất. Chạy deploy/windows/qt-enable-wal.ps1 rồi khởi động lại PostgreSQL (docs/deploy-windows.md).",
            WalArchiveStates.PendingRestart => "Đã đổi cấu hình lưu WAL nhưng chưa khởi động lại PostgreSQL – khởi động lại dịch vụ PostgreSQL vào giờ vắng.",
            WalArchiveStates.Failing when commandEmpty =>
                "Đã bật archive_mode nhưng archive_command trống – file WAL dồn trong pg_wal, ổ CSDL sẽ đầy. Chạy lại qt-enable-wal.ps1.",
            WalArchiveStates.Failing =>
                $"Chép file WAL {a.LastFailedWal} lỗi lúc {SystemOverviewService.Stamp(a.LastFailedAt!.Value)} – xem log PostgreSQL, quyền ghi và chỗ trống thư mục WAL.",
            WalArchiveStates.Lagging =>
                $"Trễ {SystemOverviewService.Duration(sinceLast!.Value)}: còn {Number(pending!.Value)} file WAL đầy chưa chép – kiểm tra thư mục WAL, quyền ghi, chỗ trống ổ.",
            _ when a.LastArchivedAt is null =>
                "Đã bật nhưng chưa chép file WAL nào – file được chép khi đầy hoặc sau archive_timeout (5 phút) nếu có ghi dữ liệu. Thử ngay: SELECT pg_switch_wal();",
            _ => $"Lần chép cuối {SystemOverviewService.Stamp(a.LastArchivedAt!.Value)} ({SystemOverviewService.Duration(sinceLast!.Value)} trước)"
                 + (pending is > 0 ? $", còn {Number(pending.Value)} file chờ chép." : ", không có file chờ chép."),
        };

        List<SystemOverviewRowDto> rows =
        [
            new(Group, "Trạng thái", Label(state), status, summary),
            new(Parameters, "wal_level", level ?? "?", levelOk ? Restart(s, "wal_level", "Ok") : "Error",
                PendingNote(s, "wal_level") ?? (levelOk ? null : "Cần replica – qt-enable-wal.ps1 đặt, khởi động lại PostgreSQL.")),
            new(Parameters, "archive_mode", mode ?? "?", modeOk ? Restart(s, "archive_mode", "Ok") : "Error",
                PendingNote(s, "archive_mode") ?? (modeOk ? null : "Cần on – qt-enable-wal.ps1 đặt, khởi động lại PostgreSQL.")),
            CommandRow(commandVisible, command, library, modeOk),
            TimeoutRow(s),
            new(Parameters, "summarize_wal", Value(s, "summarize_wal") ?? "?", Value(s, "summarize_wal") == "on" ? "Ok" : "Warning",
                Value(s, "summarize_wal") == "on" ? "Cho phép sao lưu Diff (pg_basebackup --incremental)." : "Sao lưu Diff (lát QT3b) cần summarize_wal = on."),
            KeepTimeRow(s),
            new(Stats, "Lần chép thành công cuối",
                a.LastArchivedAt is null ? "Chưa chép file nào" : $"{a.LastArchivedWal} · {SystemOverviewService.Stamp(a.LastArchivedAt.Value)}",
                a.LastArchivedAt is null ? (modeOk ? "Warning" : "Neutral") : "Ok",
                sinceLast is null ? null : $"{SystemOverviewService.Duration(sinceLast.Value)} trước."),
            new(Stats, "File WAL chờ chép", pending is null ? "Không tính được" : Number(pending.Value),
                pending is null ? "Neutral" : pending > 0 && sinceLast > LagLimit ? "Error" : pending > 0 ? "Warning" : "Ok",
                snapshot.CurrentWalFile is null
                    ? "Máy CSDL đang ở chế độ phục hồi."
                    : $"File đang ghi: {snapshot.CurrentWalFile} (chưa đầy nên chưa đến lượt chép)."),
            new(Stats, "Lần chép lỗi cuối",
                a.LastFailedAt is null ? "Chưa lỗi lần nào" : $"{a.LastFailedWal} · {SystemOverviewService.Stamp(a.LastFailedAt.Value)}",
                failedLast ? "Error" : a.LastFailedAt is null ? "Ok" : "Neutral",
                failedLast ? "Lỗi sau lần chép thành công cuối – PostgreSQL thử lại mỗi phút; xem log PostgreSQL." : a.LastFailedAt is null ? null : "Đã chép lại được sau lỗi."),
            new(Stats, "Số file đã chép / lỗi", $"{Number(a.ArchivedCount)} / {Number(a.FailedCount)}", "Neutral",
                "Tính từ lần đặt lại thống kê pg_stat_archiver."),
            context.ArchiveFolderRow,
            MaintenanceRow(context, dev),
        ];

        return new WalArchiveStatusDto(state, Label(state), status, summary, now, a.LastArchivedAt, lag, rows);
    }

    /// <summary>Dòng <c>archive_command</c>: trống khi bật là lỗi; tài khoản thiếu quyền thì không xem được.</summary>
    private static SystemOverviewRowDto CommandRow(bool visible, string? command, string? library, bool modeOn)
    {
        if (!visible)
            return new(Parameters, "archive_command", "(không xem được)", "Warning",
                "Tài khoản đang đọc không có quyền pg_read_all_settings – khai ConnectionStrings:Maintenance (tài khoản proc_maint).");
        if (!string.IsNullOrWhiteSpace(library)) return new(Parameters, "archive_library", library, "Ok", null);
        // PostgreSQL hiện "(disabled)" thay cho lệnh khi archive_mode tắt.
        if (!modeOn && command == "(disabled)")
            return new(Parameters, "archive_command", command, "Neutral", "PostgreSQL ẩn lệnh khi archive_mode tắt.");
        if (string.IsNullOrWhiteSpace(command))
            return new(Parameters, "archive_command", "(trống)", modeOn ? "Error" : "Neutral", modeOn ? "Bật lưu mà không có lệnh chép – WAL dồn trong pg_wal." : null);
        return new(Parameters, "archive_command", command, "Ok", "Lệnh không ghi đè file đã có trong thư mục WAL.");
    }

    /// <summary>Dòng <c>archive_timeout</c>: 1–300 giây đạt; 0 (tắt) hoặc lớn hơn thì có thể mất quá 5 phút dữ liệu khi ít ghi.</summary>
    private static SystemOverviewRowDto TimeoutRow(IReadOnlyDictionary<string, WalSetting> s)
    {
        var ok = int.TryParse(Value(s, "archive_timeout"), CultureInfo.InvariantCulture, out var seconds);
        var good = ok && seconds is > 0 and <= MaxArchiveTimeoutSeconds;
        return new(Parameters, "archive_timeout", ok ? $"{Number(seconds)} giây" : "?", good ? "Ok" : "Warning",
            good ? "Ít ghi dữ liệu thì cứ tối đa 5 phút vẫn chép một file." : $"Nên đặt {MaxArchiveTimeoutSeconds} giây (mất tối đa 5 phút dữ liệu).");
    }

    /// <summary>Dòng <c>wal_summary_keep_time</c> (đơn vị phút trong pg_settings; 0 = giữ mãi).</summary>
    private static SystemOverviewRowDto KeepTimeRow(IReadOnlyDictionary<string, WalSetting> s)
    {
        if (!long.TryParse(Value(s, "wal_summary_keep_time"), CultureInfo.InvariantCulture, out var minutes))
            return new(Parameters, "wal_summary_keep_time", "?", "Warning", null);
        // Ví dụ 50.400 phút = 35 ngày.
        var good = minutes == 0 || minutes >= MinSummaryKeepDays * 24L * 60;
        var text = minutes == 0 ? "Giữ mãi" : minutes % 1440 == 0 ? $"{Number(minutes / 1440)} ngày" : $"{Number(minutes)} phút";
        return new(Parameters, "wal_summary_keep_time", text, good ? "Ok" : "Warning",
            good ? null : $"Nên ≥ {MinSummaryKeepDays} ngày (Full mỗi tuần) – script đặt 35 ngày.");
    }

    /// <summary>Dòng tài khoản bảo trì.</summary>
    private static SystemOverviewRowDto MaintenanceRow(WalArchiveContext context, bool dev)
    {
        const string item = "Tài khoản bảo trì (ConnectionStrings:Maintenance)";
        if (context.MaintenanceError is not null)
            return new(Storage, item, "Không kết nối được", "Error", $"{context.MaintenanceError} – đang đọc bằng tài khoản ứng dụng.");
        return context.UsesMaintenanceAccount
            ? new(Storage, item, "Đã khai", "Ok", "Dùng cho sao lưu (lát QT3b) và đọc archive_command.")
            : new(Storage, item, "Chưa khai", dev ? "Neutral" : "Warning", "Sao lưu Full / Diff / pg_dump (lát QT3b) cần tài khoản proc_maint.");
    }

    /// <summary>Đèn của tham số đạt nhưng còn chờ khởi động lại.</summary>
    private static string Restart(IReadOnlyDictionary<string, WalSetting> s, string name, string status) => Pending(s, name) ? "Warning" : status;

    /// <summary>Ghi chú tham số chờ khởi động lại.</summary>
    private static string? PendingNote(IReadOnlyDictionary<string, WalSetting> s, string name) =>
        Pending(s, name) ? "Đã đổi trong file cấu hình – chờ khởi động lại PostgreSQL." : null;

    private static bool Pending(IReadOnlyDictionary<string, WalSetting> s, string name) => s.TryGetValue(name, out var v) && v.PendingRestart;

    private static string? Value(IReadOnlyDictionary<string, WalSetting> s, string name) => s.TryGetValue(name, out var v) ? v.Setting : null;

    private static string Number(long value) => value.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"));
}

/// <summary>
/// Đọc tình trạng lưu WAL của máy chủ CSDL (lát QT3a). Ưu tiên tài khoản bảo trì <c>ConnectionStrings:Maintenance</c> (xem được
/// <c>archive_command</c>); không khai hoặc không kết nối được thì dùng kết nối của ứng dụng (pg_settings ẩn <c>archive_command</c> với tài khoản
/// thường – dòng đó báo "không xem được").
/// </summary>
public sealed class WalArchiveStatusService : IWalArchiveStatus
{
    /// <summary>Các tham số đọc từ pg_settings.</summary>
    private const string SettingsSql = """
        SELECT name, setting, unit, pending_restart FROM pg_settings
        WHERE name IN ('wal_level', 'archive_mode', 'archive_command', 'archive_library', 'archive_timeout', 'summarize_wal',
                       'wal_summary_keep_time', 'wal_segment_size')
        """;

    private const string ArchiverSql = """
        SELECT archived_count, last_archived_wal, last_archived_time, failed_count, last_failed_wal, last_failed_time FROM pg_stat_archiver
        """;

    // pg_current_wal_lsn() lỗi khi máy đang phục hồi (bản sao) – khi đó không tính file chờ chép.
    private const string CurrentWalSql = "SELECT CASE WHEN pg_is_in_recovery() THEN NULL ELSE pg_walfile_name(pg_current_wal_lsn()) END";

    private readonly ProcDbContext _db;
    private readonly IClock _clock;
    private readonly AppEnvironmentOptions _environment;
    private readonly BackupOptions _backup;
    private readonly ILogger<WalArchiveStatusService> _logger;

    /// <summary>Tạo dịch vụ.</summary>
    /// <param name="db">DbContext (kết nối dự phòng).</param>
    /// <param name="clock">Đồng hồ.</param>
    /// <param name="environment">Môi trường đang chạy.</param>
    /// <param name="backup">Cấu hình sao lưu.</param>
    /// <param name="logger">Log kỹ thuật.</param>
    public WalArchiveStatusService(
        ProcDbContext db, IClock clock, IOptions<AppEnvironmentOptions> environment, IOptions<BackupOptions> backup,
        ILogger<WalArchiveStatusService> logger)
    {
        _db = db;
        _clock = clock;
        _environment = environment.Value;
        _backup = backup.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<WalArchiveStatusDto> GetAsync(CancellationToken cancellationToken)
    {
        WalArchiveSnapshot? snapshot = null;
        string? maintenanceError = null;
        var useMaintenance = !string.IsNullOrWhiteSpace(_backup.MaintenanceConnectionString);
        if (useMaintenance)
        {
            try
            {
                await using var connection = new NpgsqlConnection(_backup.MaintenanceConnectionString);
                await connection.OpenAsync(cancellationToken);
                snapshot = await ReadAsync(connection, cancellationToken);
            }
            catch (Exception ex) when (ex is NpgsqlException or ArgumentException or InvalidOperationException)
            {
                // Không lộ chuỗi kết nối: chỉ ghi thông báo lỗi của Npgsql.
                _logger.LogWarning(ex, "Không đọc được tình trạng lưu WAL bằng tài khoản bảo trì – chuyển sang tài khoản ứng dụng");
                maintenanceError = ex.Message;
                useMaintenance = false;
            }
        }

        if (snapshot is null)
        {
            await _db.Database.OpenConnectionAsync(cancellationToken);
            try
            {
                snapshot = await ReadAsync(_db.Database.GetDbConnection(), cancellationToken);
            }
            finally
            {
                await _db.Database.CloseConnectionAsync();
            }
        }

        var folder = string.IsNullOrWhiteSpace(_backup.WalArchivePath)
            ? new SystemOverviewRowDto("Nơi lưu", "Thư mục WAL (Backup:WalArchivePath)", "Chưa khai",
                _environment.Name == AppEnvironments.Dev ? "Neutral" : "Warning",
                "Khai đúng thư mục đã truyền cho qt-enable-wal.ps1 để theo dõi chỗ trống ổ.")
            : SystemOverviewService.Disk("Nơi lưu", "Thư mục WAL (Backup:WalArchivePath)", _backup.WalArchivePath);

        return WalArchiveEvaluator.Evaluate(snapshot, new WalArchiveContext(_environment.Name, folder, useMaintenance, maintenanceError), _clock.UtcNow);
    }

    /// <summary>Đọc tham số, thống kê chép và file WAL đang ghi trên một kết nối đã mở.</summary>
    private static async Task<WalArchiveSnapshot> ReadAsync(DbConnection connection, CancellationToken ct)
    {
        var settings = new Dictionary<string, WalSetting>(StringComparer.Ordinal);
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = SettingsSql;
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                settings[reader.GetString(0)] = new WalSetting(
                    reader.IsDBNull(1) ? "" : reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2), reader.GetBoolean(3));
        }

        WalArchiverStats stats;
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = ArchiverSql;
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            await reader.ReadAsync(ct);
            stats = new WalArchiverStats(
                reader.GetInt64(0), Text(reader, 1), Time(reader, 2), reader.GetInt64(3), Text(reader, 4), Time(reader, 5));
        }

        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = CurrentWalSql;
            var current = await cmd.ExecuteScalarAsync(ct);
            return new WalArchiveSnapshot(settings, stats, current as string);
        }

        static string? Text(DbDataReader r, int i) => r.IsDBNull(i) ? null : r.GetString(i);
        static DateTimeOffset? Time(DbDataReader r, int i) => r.IsDBNull(i) ? null : r.GetFieldValue<DateTimeOffset>(i);
    }
}
