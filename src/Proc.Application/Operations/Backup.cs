namespace Proc.Application.Operations;

/// <summary>
/// Cấu hình sao lưu – mục <c>Backup</c> trong appsettings (design.md mục 21.5, lát QT3a). Lát QT3b thêm thư mục sao lưu Full / Diff / pg_dump,
/// nơi lưu thứ hai, đường dẫn công cụ PostgreSQL.
/// </summary>
public sealed class BackupOptions
{
    /// <summary>Tên mục cấu hình.</summary>
    public const string Section = "Backup";

    /// <summary>
    /// Thư mục PostgreSQL chép file WAL vào (đúng thư mục đã truyền cho <c>deploy/windows/qt-enable-wal.ps1</c>), ví dụ
    /// <c>E:\ProcurementBackup\wal</c> – nên ở ổ khác ổ dữ liệu CSDL. Ứng dụng chỉ đọc để báo chỗ trống ổ; bỏ trống thì màn hình BAK báo chưa khai.
    /// </summary>
    public string? WalArchivePath { get; set; }

    /// <summary>
    /// Chuỗi kết nối tài khoản bảo trì <c>proc_maint</c> (REPLICATION, CREATEDB, pg_monitor, pg_read_all_settings, pg_read_all_data – tạo bằng
    /// <c>qt-enable-wal.ps1</c>). Không khai trong mục <c>Backup</c>: Program.cs gán từ <c>ConnectionStrings:Maintenance</c> (biến môi trường
    /// <c>ConnectionStrings__Maintenance</c>) cho cùng chỗ với chuỗi kết nối chính. Bỏ trống thì đọc tình trạng bằng tài khoản ứng dụng (không xem
    /// được <c>archive_command</c>); lát QT3b cần tài khoản này để chạy <c>pg_basebackup</c>, <c>pg_dump</c>.
    /// </summary>
    public string? MaintenanceConnectionString { get; set; }

    /// <summary>
    /// Thư mục gốc chứa bản sao lưu (lát QT3b): các thư mục con <c>full</c>, <c>diff</c>, <c>dump</c>, <c>attachments</c> – nên ở ổ khác ổ dữ liệu
    /// CSDL (B4). Đường dẫn tương đối tính từ ContentRoot (Program.cs). Bỏ trống = chưa khai: không chạy được sao lưu, màn hình BAK báo.
    /// </summary>
    public string? RootPath { get; set; }

    /// <summary>
    /// Nơi lưu thứ hai (NAS / thư mục mạng) – sau mỗi lần sao lưu xong, chép nguyên bản sang đây cùng đường dẫn tương đối. Không bắt buộc (B4).
    /// </summary>
    public string? SecondaryPath { get; set; }

    /// <summary>
    /// Thư mục công cụ PostgreSQL (<c>pg_basebackup</c>, <c>pg_dump</c>…), ví dụ thư mục <c>bin</c> của PostgreSQL 18. Bỏ trống thì tìm theo PATH,
    /// rồi (Windows) thư mục PostgreSQL bản cao nhất trong Program Files.
    /// </summary>
    public string? PgBinPath { get; set; }

    /// <summary>Thời gian tối đa một lệnh công cụ PostgreSQL được chạy (phút) – quá thì dừng lệnh, ghi lỗi.</summary>
    public int ToolTimeoutMinutes { get; set; } = 360;

    /// <summary>
    /// Bật bộ chạy nền xử lý hàng chờ sao lưu. Môi trường Testing tắt để test tự gọi <see cref="IBackupExecutor.ProcessQueueAsync"/> (không chạy
    /// đua với dịch vụ nền).
    /// </summary>
    public bool WorkerEnabled { get; set; } = true;
}

/// <summary>Trạng thái lưu WAL (mã tiếng Anh dùng chung giữa máy chủ và giao diện).</summary>
public static class WalArchiveStates
{
    /// <summary>Chưa bật: <c>archive_mode</c> tắt hoặc <c>wal_level = minimal</c> – chưa chạy <c>qt-enable-wal.ps1</c>.</summary>
    public const string NotEnabled = "NotEnabled";

    /// <summary>Đã đổi cấu hình nhưng chưa khởi động lại PostgreSQL (<c>pending_restart</c>).</summary>
    public const string PendingRestart = "PendingRestart";

    /// <summary>Đang chạy: không có file WAL đầy nào chờ lưu quá 15 phút, lần lưu gần nhất không lỗi.</summary>
    public const string Running = "Running";

    /// <summary>Trễ: có file WAL đã đầy chưa lưu và lần lưu thành công gần nhất cách đây quá 15 phút.</summary>
    public const string Lagging = "Lagging";

    /// <summary>Có lỗi: lần chép lỗi gần nhất sau lần chép thành công gần nhất, hoặc bật lưu mà <c>archive_command</c> trống.</summary>
    public const string Failing = "Failing";
}

/// <summary>Tình trạng lưu WAL của máy chủ CSDL (tab "Lưu WAL" màn hình BAK – lát QT3a).</summary>
/// <param name="State">Trạng thái – một giá trị của <see cref="WalArchiveStates"/>.</param>
/// <param name="StateLabel">Chữ hiển thị của trạng thái, ví dụ "Đang chạy".</param>
/// <param name="Status">Đèn: Ok, Warning, Error, Neutral (máy DEV không bắt buộc lưu WAL).</param>
/// <param name="Summary">Một câu tóm tắt (lần lưu cuối, độ trễ, việc cần làm).</param>
/// <param name="CheckedAt">Thời điểm lấy số liệu (UTC).</param>
/// <param name="LastArchivedAt">Lần chép file WAL thành công gần nhất (UTC); null = chưa chép file nào.</param>
/// <param name="LagSeconds">
/// Độ trễ (giây) tính từ lần chép thành công gần nhất khi còn file WAL đầy chờ chép; 0 = không có file chờ; null = không tính được (chưa bật,
/// máy đang ở chế độ phục hồi, chưa chép file nào).
/// </param>
/// <param name="Rows">Các dòng chi tiết: tham số PostgreSQL, thống kê chép, thư mục WAL, tài khoản bảo trì.</param>
public sealed record WalArchiveStatusDto(
    string State, string StateLabel, string Status, string Summary, DateTimeOffset CheckedAt, DateTimeOffset? LastArchivedAt, long? LagSeconds,
    IReadOnlyList<SystemOverviewRowDto> Rows);

/// <summary>
/// Đọc tình trạng lưu WAL (design.md mục 21.5): tham số trong <c>pg_settings</c>, thống kê <c>pg_stat_archiver</c>, file WAL hiện tại. Dùng chuỗi
/// kết nối <c>ConnectionStrings:Maintenance</c> (tài khoản <c>proc_maint</c> – xem được <c>archive_command</c>) khi có, không thì chuỗi chính.
/// </summary>
public interface IWalArchiveStatus
{
    /// <summary>Lấy tình trạng.</summary>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Tình trạng và các dòng chi tiết.</returns>
    Task<WalArchiveStatusDto> GetAsync(CancellationToken cancellationToken);
}
