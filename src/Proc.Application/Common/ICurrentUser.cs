namespace Proc.Application.Common;

/// <summary>
/// Người dùng đang gọi hệ thống, lấy từ token đăng nhập. Phạm vi dữ liệu (công ty, kho, nhóm hàng, quyền)
/// nằm ở ICurrentUserScope (design.md mục 9.2).
/// </summary>
public interface ICurrentUser
{
    /// <summary>Id người dùng; null khi chưa đăng nhập hoặc chạy nền (khởi tạo dữ liệu, job).</summary>
    Guid? UserId { get; }

    /// <summary>Tên đăng nhập (ghi vào nhật ký để đọc được ngay cả khi tài khoản đổi họ tên); null khi chạy nền.</summary>
    string? UserName { get; }
}

/// <summary>
/// Thông tin request đang xử lý, ghi kèm nhật ký (design.md mục 9.3). Chạy ngoài HTTP (khởi tạo dữ liệu, job) thì các giá trị null.
/// </summary>
public interface IRequestContext
{
    /// <summary>Địa chỉ IP của máy người dùng.</summary>
    string? IpAddress { get; }

    /// <summary>Trình duyệt (header User-Agent).</summary>
    string? UserAgent { get; }

    /// <summary>Mã liên kết request: cùng mã trong AuditLog, AccessLog và log kỹ thuật để lần theo một thao tác.</summary>
    string? CorrelationId { get; }
}

/// <summary>
/// Đồng hồ hệ thống – tách ra để test giả lập được thời gian (design.md mục 3.1).
/// </summary>
public interface IClock
{
    /// <summary>Thời điểm hiện tại theo UTC (CSDL lưu UTC – CLAUDE.md mục 5).</summary>
    DateTimeOffset UtcNow { get; }
}
