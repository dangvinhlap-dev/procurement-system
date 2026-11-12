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
    /// <summary>
    /// Tên múi giờ thử lần lượt: tên IANA (Linux, Windows có ICU – Windows 10 1903 / Server 2019 trở lên) rồi tên Windows
    /// (Windows Server 2016 trở về trước không đổi được tên IANA). Cả hai đều là UTC+7, không đổi giờ mùa hè.
    /// </summary>
    public static readonly IReadOnlyList<string> ZoneIds = ["Asia/Ho_Chi_Minh", "SE Asia Standard Time"];

    /// <summary>Múi giờ Việt Nam (UTC+7) – mọi chỗ cần giờ Việt Nam dùng chung, không tự tìm theo tên.</summary>
    public static readonly TimeZoneInfo Zone = FindZone(ZoneIds);

    /// <summary>
    /// Tìm múi giờ theo danh sách tên, lấy tên đầu tiên hệ điều hành nhận ra – để cùng một bản chạy được trên Linux container và
    /// Windows Server.
    /// </summary>
    /// <param name="ids">Tên múi giờ theo thứ tự ưu tiên.</param>
    /// <returns>Múi giờ tìm thấy.</returns>
    /// <exception cref="TimeZoneNotFoundException">Khi hệ điều hành không có tên nào trong danh sách.</exception>
    public static TimeZoneInfo FindZone(IEnumerable<string> ids)
    {
        var tried = new List<string>();
        foreach (var id in ids)
        {
            if (TimeZoneInfo.TryFindSystemTimeZoneById(id, out var zone)) return zone;
            tried.Add(id);
        }

        throw new TimeZoneNotFoundException($"Không tìm thấy múi giờ Việt Nam ({string.Join(", ", tried)}) trên máy chủ.");
    }

    /// <summary>
    /// Hôm nay theo giờ Việt Nam – 0h–7h sáng ở Việt Nam vẫn là "hôm qua" theo UTC, nên không lấy ngày UTC.
    /// </summary>
    /// <param name="clock">Đồng hồ.</param>
    /// <returns>Ngày hôm nay ở Việt Nam.</returns>
    public static DateOnly TodayInVietnam(this IClock clock) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.UtcNow, Zone).DateTime);

    /// <summary>
    /// Giờ đồng hồ hiện tại ở Việt Nam (không kèm múi giờ) – so với các mốc ghi theo giờ Việt Nam như hạn nộp báo giá RFQ (lát P3a).
    /// </summary>
    /// <param name="clock">Đồng hồ.</param>
    /// <returns>Ví dụ 12/11/2026 16:59 khi UTC là 09:59.</returns>
    public static DateTime NowInVietnam(this IClock clock) => TimeZoneInfo.ConvertTime(clock.UtcNow, Zone).DateTime;

    /// <summary>Đổi một thời điểm ghi theo giờ Việt Nam (ngày + giờ) sang UTC.</summary>
    /// <param name="local">Giờ Việt Nam.</param>
    /// <returns>Thời điểm UTC tương ứng.</returns>
    public static DateTimeOffset VietnamToUtc(DateTime local) =>
        new(TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), Zone), TimeSpan.Zero);
}
