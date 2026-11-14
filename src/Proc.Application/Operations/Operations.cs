using Proc.Domain.Operations;

namespace Proc.Application.Operations;

/// <summary>
/// Môi trường của máy chủ đang chạy – mục <c>App</c> trong appsettings (design.md mục 21.3, lát QT1). Mỗi máy chủ đặt
/// <c>App:Environment</c> = Dev / Test / Prod trong <c>appsettings.Production.json</c> hoặc biến môi trường <c>App__Environment</c>.
/// </summary>
public sealed class AppEnvironmentOptions
{
    /// <summary>Tên mục cấu hình.</summary>
    public const string Section = "App";

    /// <summary>
    /// Dev / Test / Prod. <b>Bắt buộc khai</b> trên mọi máy chủ (người dùng chốt 14/11/2026): bỏ trống thì máy coi là "chưa khai" – không gửi
    /// email nào, mọi màn hình hiện dải đỏ cảnh báo (docs/deploy-windows.md). appsettings.Development / Testing đặt Dev. Giá trị khác ba tên trên
    /// thì ứng dụng không khởi động.
    /// </summary>
    public string? Environment { get; set; }

    /// <summary>Đã khai môi trường.</summary>
    public bool IsConfigured => AppEnvironments.Normalize(Environment) is not null;

    /// <summary>Tên đã chuẩn hóa: Dev / Test / Prod, hoặc None khi chưa khai.</summary>
    public string Name => AppEnvironments.Normalize(Environment) ?? AppEnvironments.None;

    /// <summary>Là máy TEST.</summary>
    public bool IsTest => Name == AppEnvironments.Test;
}

/// <summary>Cấu hình module vận hành – mục <c>Operations</c> (design.md mục 21, lát QT1).</summary>
public sealed class OperationsOptions
{
    /// <summary>Tên mục cấu hình.</summary>
    public const string Section = "Operations";

    /// <summary>
    /// Thư mục ghi file nhật ký vận hành (mỗi ngày một file <c>operations-yyyyMMdd.log</c>, mỗi dòng một JSON). Đường dẫn tương đối tính từ
    /// ContentRoot – chỉ hợp cho máy dev; server đặt ra ngoài thư mục ứng dụng, ví dụ <c>D:\ProcurementData\operations-log</c> (N3: khôi phục CSDL
    /// thay luôn nhật ký trong CSDL nên phải có bản ngoài CSDL).
    /// </summary>
    public string LogDirectory { get; set; } = "data/operations-log";
}

/// <summary>Một thao tác vận hành cần ghi nhật ký.</summary>
/// <param name="Kind">Loại việc (mã tiếng Anh, ví dụ AppStarted).</param>
/// <param name="Target">Đối tượng.</param>
/// <param name="Reason">Lý do.</param>
/// <param name="Result">Kết quả.</param>
/// <param name="Detail">Chi tiết.</param>
public sealed record OperationLogEntry(string Kind, string? Target, string? Reason, OperationResult Result, string? Detail);

/// <summary>Mã loại việc vận hành dùng chung giữa máy chủ và giao diện.</summary>
public static class OperationKinds
{
    /// <summary>Ứng dụng khởi động (ghi mỗi lần chạy: phiên bản, môi trường, migration).</summary>
    public const string AppStarted = "AppStarted";
}

/// <summary>
/// Ghi nhật ký vận hành (N3): mỗi dòng vào CSDL (bảng chỉ thêm) và file ngày ngoài CSDL. Ghi bằng DbContext riêng nên lỗi của thao tác chính
/// không làm mất nhật ký; lỗi ghi CSDL vẫn còn bản ghi ở file.
/// </summary>
public interface IOperationLog
{
    /// <summary>Ghi một dòng; người thao tác lấy từ phiên đăng nhập hiện tại (không có = hệ thống).</summary>
    /// <param name="entry">Thao tác.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Dòng đã ghi.</returns>
    Task<OperationLogItemDto> WriteAsync(OperationLogEntry entry, CancellationToken cancellationToken);
}

/// <summary>Một dòng nhật ký vận hành.</summary>
/// <param name="Id">Id.</param>
/// <param name="OccurredAt">Thời điểm (UTC).</param>
/// <param name="Environment">Môi trường.</param>
/// <param name="UserName">Tên đăng nhập; null = hệ thống.</param>
/// <param name="Kind">Loại việc.</param>
/// <param name="Target">Đối tượng.</param>
/// <param name="Reason">Lý do.</param>
/// <param name="Result">Kết quả: Succeeded, Failed, Info.</param>
/// <param name="Detail">Chi tiết.</param>
public sealed record OperationLogItemDto(
    Guid Id, DateTimeOffset OccurredAt, string Environment, string? UserName, string Kind, string? Target, string? Reason, string Result,
    string? Detail);

/// <summary>Kết quả tìm nhật ký vận hành.</summary>
/// <param name="Items">Các dòng, mới nhất trước.</param>
/// <param name="Truncated">Còn dòng chưa trả (vượt giới hạn) – thu hẹp khoảng ngày.</param>
public sealed record OperationLogPage(IReadOnlyList<OperationLogItemDto> Items, bool Truncated);

/// <summary>Đọc nhật ký vận hành.</summary>
public interface IOperationLogQueries
{
    /// <summary>Số dòng tối đa mỗi lần tìm.</summary>
    const int MaxRows = 5000;

    /// <summary>Tìm theo khoảng ngày (giờ Việt Nam).</summary>
    /// <param name="from">Từ ngày.</param>
    /// <param name="to">Đến ngày.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Các dòng, mới nhất trước.</returns>
    /// <exception cref="Domain.Common.BusinessRuleException">Khi khoảng ngày sai hoặc quá 366 ngày.</exception>
    Task<OperationLogPage> SearchAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken);
}

/// <summary>
/// Nhịp chạy của dịch vụ nền (gửi email, nhắc duyệt…): mỗi vòng lặp báo một lần để màn hình SYS biết dịch vụ còn sống. Lưu trong bộ nhớ của
/// tiến trình – khởi động lại thì chờ vòng đầu.
/// </summary>
public interface IBackgroundHeartbeat
{
    /// <summary>Báo dịch vụ vừa chạy một vòng.</summary>
    /// <param name="service">Tên dịch vụ (tiếng Việt, hiện trên SYS).</param>
    /// <param name="intervalSeconds">Chu kỳ dự kiến (giây) – quá 3 chu kỳ không báo thì SYS báo đèn đỏ.</param>
    /// <param name="detail">Ghi chú lần chạy (ví dụ "Email đang tắt").</param>
    void Beat(string service, int intervalSeconds, string? detail = null);

    /// <summary>Lần báo gần nhất của từng dịch vụ.</summary>
    /// <returns>Theo tên dịch vụ.</returns>
    IReadOnlyList<BackgroundBeat> Snapshot();
}

/// <summary>Lần báo gần nhất của một dịch vụ nền.</summary>
/// <param name="Service">Tên dịch vụ.</param>
/// <param name="LastBeatAt">Thời điểm (UTC).</param>
/// <param name="IntervalSeconds">Chu kỳ dự kiến (giây).</param>
/// <param name="Detail">Ghi chú.</param>
public sealed record BackgroundBeat(string Service, DateTimeOffset LastBeatAt, int IntervalSeconds, string? Detail);

/// <summary>Một dòng của bảng tổng quan SYS.</summary>
/// <param name="Group">Nhóm: Ứng dụng, Cơ sở dữ liệu, Ổ đĩa, Dịch vụ nền.</param>
/// <param name="Item">Hạng mục.</param>
/// <param name="Value">Giá trị đã định dạng tiếng Việt.</param>
/// <param name="Status">Đèn: Ok, Warning, Error, Neutral.</param>
/// <param name="Note">Ghi chú (lý do đèn vàng / đỏ, cách xử lý).</param>
public sealed record SystemOverviewRowDto(string Group, string Item, string Value, string Status, string? Note);

/// <summary>Tổng quan hệ thống của môi trường đang chạy (màn hình SYS – design.md mục 21.4, lát QT1).</summary>
/// <param name="Environment">Dev / Test / Prod.</param>
/// <param name="Version">Phiên bản ứng dụng.</param>
/// <param name="CheckedAt">Thời điểm lấy số liệu (UTC).</param>
/// <param name="AppliedMigrations">Số migration đã chạy.</param>
/// <param name="PendingMigrations">Tên các migration chưa chạy (rỗng = CSDL khớp bản cài).</param>
/// <param name="Rows">Các dòng của bảng.</param>
public sealed record SystemOverviewDto(
    string Environment, string Version, DateTimeOffset CheckedAt, int AppliedMigrations, IReadOnlyList<string> PendingMigrations,
    IReadOnlyList<SystemOverviewRowDto> Rows);

/// <summary>Số liệu tổng quan của môi trường đang chạy.</summary>
public interface ISystemOverview
{
    /// <summary>Lấy số liệu: phiên bản, migration, dung lượng CSDL, số kết nối, ổ đĩa, dịch vụ nền, email.</summary>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Tổng quan.</returns>
    Task<SystemOverviewDto> GetAsync(CancellationToken cancellationToken);
}

/// <summary>Phiên bản ứng dụng đang chạy (từ Directory.Build.props, bỏ phần "+mã commit").</summary>
/// <param name="Version">Số phiên bản, ví dụ 0.32.0.</param>
public sealed record AppVersion(string Version);

/// <summary>
/// Chốt "chỉ dùng ở tổng công ty" của mọi chức năng vận hành (design.md mục 21.2 – N3). API dùng qua bộ lọc endpoint <c>RequireHeadOffice</c>.
/// </summary>
public interface IOperationsAccess
{
    /// <summary>Kiểm tra công ty đang làm việc là tổng công ty.</summary>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <exception cref="Common.ForbiddenAccessException">Khi đang làm việc ở công ty thành viên hoặc chưa chọn công ty.</exception>
    Task EnsureHeadOfficeAsync(CancellationToken cancellationToken);
}

/// <summary>Môi trường và cảnh báo cấu hình hiện trên dải màu mọi màn hình (kể cả trang đăng nhập – không chứa bí mật).</summary>
/// <param name="Environment">Dev / Test / Prod; None = máy chưa khai <c>App:Environment</c>.</param>
/// <param name="Warning">Cảnh báo cấu hình (chưa khai môi trường, TEST chưa khai hộp thư test…); null = ổn.</param>
public sealed record EnvironmentStatusDto(string Environment, string? Warning);

/// <summary>Đọc môi trường và cảnh báo cấu hình của máy đang chạy.</summary>
public interface IEnvironmentStatus
{
    /// <summary>Môi trường và cảnh báo (đọc lại cấu hình email mỗi lần).</summary>
    /// <returns>Tình trạng.</returns>
    EnvironmentStatusDto Get();
}
