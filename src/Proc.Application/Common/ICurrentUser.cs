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

/// <summary>Ngày giờ theo múi giờ Việt Nam (CLAUDE.md mục 5: lưu UTC, ngày chứng từ theo giờ Việt Nam).</summary>
public static class VietnamTime
{
    /// <summary>Múi giờ Asia/Ho_Chi_Minh (UTC+7, không đổi giờ mùa hè).</summary>
    public static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");

    /// <summary>
    /// Hôm nay theo giờ Việt Nam – 0h–7h sáng ở Việt Nam vẫn là "hôm qua" theo UTC, nên không lấy ngày UTC.
    /// </summary>
    /// <param name="clock">Đồng hồ.</param>
    /// <returns>Ngày hôm nay ở Việt Nam.</returns>
    public static DateOnly TodayInVietnam(this IClock clock) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.UtcNow, Zone).DateTime);
}
