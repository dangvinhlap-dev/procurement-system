using System.Text.Json.Serialization;

namespace Proc.Application.Logging;

/// <summary>Sự kiện truy cập ghi vào AccessLog (design.md mục 9.3). Lưu và trả về API bằng tên (chuỗi) để đọc thẳng được.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<AccessEvent>))]
public enum AccessEvent
{
    /// <summary>Đăng nhập thành công.</summary>
    Login,

    /// <summary>Đăng nhập sai (sai tên/mật khẩu, tài khoản ngừng dùng, chưa gán công ty).</summary>
    LoginFailed,

    /// <summary>Tài khoản bị khóa tạm do nhập sai nhiều lần, hoặc đăng nhập khi đang bị khóa.</summary>
    LockedOut,

    /// <summary>Đăng xuất.</summary>
    Logout,

    /// <summary>Tự đổi mật khẩu thành công.</summary>
    PasswordChanged,

    /// <summary>Tự đổi mật khẩu không thành công (sai mật khẩu hiện tại, mật khẩu mới không đạt).</summary>
    PasswordChangeFailed,

    /// <summary>Đổi công ty làm việc.</summary>
    CompanySwitched,
}

/// <summary>
/// Ghi nhật ký truy cập. Ghi ngay, không chờ thao tác khác, để lần đăng nhập sai cũng để lại dấu vết.
/// </summary>
public interface IAccessLog
{
    /// <summary>
    /// Ghi một sự kiện truy cập; IP, trình duyệt, mã liên kết lấy từ request hiện tại.
    /// </summary>
    /// <param name="accessEvent">Sự kiện.</param>
    /// <param name="userId">Tài khoản (null khi tên đăng nhập không tồn tại).</param>
    /// <param name="userName">Tên đăng nhập (với đăng nhập sai là tên đã gõ).</param>
    /// <param name="companyId">Công ty liên quan (đổi công ty làm việc), nếu có.</param>
    /// <param name="detail">Ghi chú tiếng Việt, ví dụ lý do đăng nhập sai.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Task hoàn tất khi đã lưu.</returns>
    Task WriteAsync(
        AccessEvent accessEvent,
        Guid? userId,
        string? userName,
        Guid? companyId,
        string? detail,
        CancellationToken cancellationToken);
}

/// <summary>Điều kiện lọc nhật ký thao tác.</summary>
/// <param name="From">Từ ngày (theo giờ Việt Nam).</param>
/// <param name="To">Đến ngày, tính cả ngày này.</param>
/// <param name="UserName">Tên đăng nhập (đúng tên), bỏ trống = mọi người.</param>
/// <param name="EntityType">Mã loại đối tượng, ví dụ Role; bỏ trống = mọi loại.</param>
/// <param name="Action">Hành động, ví dụ Update; bỏ trống = mọi hành động.</param>
public sealed record AuditLogQuery(DateOnly From, DateOnly To, string? UserName, string? EntityType, string? Action);

/// <summary>Một dòng nhật ký thao tác.</summary>
/// <param name="Id">Id dòng nhật ký.</param>
/// <param name="OccurredAt">Thời điểm (UTC).</param>
/// <param name="UserName">Tên đăng nhập người thao tác; null = hệ thống (khởi tạo dữ liệu, job).</param>
/// <param name="EntityType">Mã loại đối tượng.</param>
/// <param name="EntityName">Tên loại đối tượng tiếng Việt.</param>
/// <param name="EntityId">Khóa của bản ghi.</param>
/// <param name="EntityLabel">Mô tả bản ghi (mã, tên đăng nhập…), nếu có.</param>
/// <param name="DocumentNumber">Số chứng từ, nếu có.</param>
/// <param name="Action">Hành động: Create, Update, Delete…</param>
/// <param name="ChangedFields">Tên các trường thay đổi, để xem nhanh trên lưới.</param>
/// <param name="IpAddress">IP máy người dùng.</param>
public sealed record AuditLogItemDto(
    Guid Id,
    DateTimeOffset OccurredAt,
    string? UserName,
    string EntityType,
    string EntityName,
    string EntityId,
    string? EntityLabel,
    string? DocumentNumber,
    string Action,
    IReadOnlyList<string> ChangedFields,
    string? IpAddress);

/// <summary>Giá trị một trường trước và sau thay đổi (chuỗi JSON của giá trị; null = không có).</summary>
/// <param name="Field">Tên trường.</param>
/// <param name="OldValue">Giá trị cũ.</param>
/// <param name="NewValue">Giá trị mới.</param>
public sealed record AuditFieldChangeDto(string Field, string? OldValue, string? NewValue);

/// <summary>Chi tiết một dòng nhật ký thao tác, kèm so sánh trước/sau.</summary>
/// <param name="Item">Thông tin chung.</param>
/// <param name="UserAgent">Trình duyệt.</param>
/// <param name="CorrelationId">Mã liên kết request.</param>
/// <param name="Changes">Các trường thay đổi.</param>
public sealed record AuditLogDetailDto(
    AuditLogItemDto Item,
    string? UserAgent,
    string? CorrelationId,
    IReadOnlyList<AuditFieldChangeDto> Changes);

/// <summary>Kết quả tìm nhật ký: có giới hạn số dòng để lưới không quá nặng.</summary>
/// <typeparam name="T">Kiểu dòng.</typeparam>
/// <param name="Items">Các dòng, mới nhất trước.</param>
/// <param name="Truncated">true khi còn dòng chưa trả – cần thu hẹp điều kiện lọc.</param>
public sealed record LogPage<T>(IReadOnlyList<T> Items, bool Truncated);

/// <summary>Điều kiện lọc nhật ký truy cập.</summary>
/// <param name="From">Từ ngày (theo giờ Việt Nam).</param>
/// <param name="To">Đến ngày, tính cả ngày này.</param>
/// <param name="UserName">Tên đăng nhập, bỏ trống = mọi người.</param>
/// <param name="Event">Sự kiện, bỏ trống = mọi sự kiện.</param>
public sealed record AccessLogQuery(DateOnly From, DateOnly To, string? UserName, AccessEvent? Event);

/// <summary>Một dòng nhật ký truy cập.</summary>
/// <param name="Id">Id dòng.</param>
/// <param name="OccurredAt">Thời điểm (UTC).</param>
/// <param name="Event">Sự kiện.</param>
/// <param name="UserName">Tên đăng nhập (đã gõ, với đăng nhập sai).</param>
/// <param name="CompanyCode">Mã công ty liên quan, nếu có.</param>
/// <param name="Detail">Ghi chú.</param>
/// <param name="IpAddress">IP.</param>
/// <param name="UserAgent">Trình duyệt.</param>
public sealed record AccessLogItemDto(
    Guid Id,
    DateTimeOffset OccurredAt,
    AccessEvent Event,
    string? UserName,
    string? CompanyCode,
    string? Detail,
    string? IpAddress,
    string? UserAgent);

/// <summary>
/// Đọc nhật ký (màn hình Nhật ký thao tác, Nhật ký truy cập – design.md mục 9.3). Chỉ đọc, không có hàm sửa/xóa.
/// Phạm vi: nhật ký thao tác của công ty đang làm việc; nhật ký truy cập của người làm việc ở công ty đó
/// (ở tổng công ty thì thấy toàn bộ, gồm cả đăng nhập sai bằng tên không tồn tại).
/// </summary>
public interface ILogQueries
{
    /// <summary>
    /// Tìm nhật ký thao tác.
    /// </summary>
    /// <param name="query">Điều kiện lọc.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Các dòng mới nhất trước, tối đa một giới hạn.</returns>
    /// <exception cref="Domain.Common.BusinessRuleException">Khi khoảng ngày sai.</exception>
    Task<LogPage<AuditLogItemDto>> SearchAuditAsync(AuditLogQuery query, CancellationToken cancellationToken);

    /// <summary>
    /// Chi tiết một dòng nhật ký thao tác.
    /// </summary>
    /// <param name="id">Id dòng.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Chi tiết kèm so sánh trước/sau.</returns>
    /// <exception cref="Common.NotFoundException">Khi không có hoặc thuộc công ty khác.</exception>
    Task<AuditLogDetailDto> GetAuditAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Tìm nhật ký truy cập.
    /// </summary>
    /// <param name="query">Điều kiện lọc.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Các dòng mới nhất trước, tối đa một giới hạn.</returns>
    Task<LogPage<AccessLogItemDto>> SearchAccessAsync(AccessLogQuery query, CancellationToken cancellationToken);
}
