using System.Net;
using Proc.Domain.Operations;

namespace Proc.Application.Operations;

/// <summary>
/// Chính sách truy cập đang hiệu lực (lát QT7 – design.md mục 21.16), dạng đọc nhanh cho mọi request: bộ giới hạn tần suất, kiểm mật khẩu, khóa đăng
/// nhập, hạn token, dải IP quản trị, mốc "Đăng xuất tất cả".
/// </summary>
/// <param name="MinPasswordLength">Độ dài mật khẩu tối thiểu.</param>
/// <param name="BlockCommonPasswords">Chặn mật khẩu phổ biến.</param>
/// <param name="ExtraBlockedPasswords">Mật khẩu cấm thêm (so không phân biệt hoa thường).</param>
/// <param name="MaxFailedLogins">Số lần sai liên tiếp thì tạm khóa.</param>
/// <param name="LockoutMinutes">Số phút tạm khóa.</param>
/// <param name="SessionMinutes">Hạn phiên (phút).</param>
/// <param name="OpsIpRanges">Dải IP được dùng chức năng quản trị vận hành; rỗng = mọi IP.</param>
/// <param name="LoginPerMinutePerIp">Lần gọi đăng nhập / phút / IP.</param>
/// <param name="ApiPerMinutePerUser">Lần gọi API / phút / phiên.</param>
/// <param name="HeavyConcurrentPerUser">Việc nặng chạy cùng lúc / phiên.</param>
/// <param name="SessionsRevokedAt">Mốc "Đăng xuất tất cả" (UTC, tròn giây); null = chưa dùng.</param>
public sealed record AccessPolicySnapshot(
    int MinPasswordLength,
    bool BlockCommonPasswords,
    IReadOnlySet<string> ExtraBlockedPasswords,
    int MaxFailedLogins,
    int LockoutMinutes,
    int SessionMinutes,
    IReadOnlyList<IPNetwork> OpsIpRanges,
    int LoginPerMinutePerIp,
    int ApiPerMinutePerUser,
    int HeavyConcurrentPerUser,
    DateTimeOffset? SessionsRevokedAt)
{
    /// <summary>Chính sách mặc định (chưa có dòng trong CSDL).</summary>
    public static AccessPolicySnapshot Default { get; } = From(AccessPolicy.CreateDefault());

    /// <summary>Đọc từ entity.</summary>
    /// <param name="policy">Dòng cấu hình.</param>
    /// <returns>Bản đọc nhanh.</returns>
    public static AccessPolicySnapshot From(AccessPolicy policy) => new(
        policy.MinPasswordLength,
        policy.BlockCommonPasswords,
        AccessPolicy.ParseBlockedPasswords(policy.ExtraBlockedPasswords).ToHashSet(StringComparer.OrdinalIgnoreCase),
        policy.MaxFailedLogins,
        policy.LockoutMinutes,
        policy.SessionMinutes,
        policy.GetOpsIpRanges(),
        policy.LoginPerMinutePerIp,
        policy.ApiPerMinutePerUser,
        policy.HeavyConcurrentPerUser,
        policy.SessionsRevokedAt);
}

/// <summary>
/// Nguồn chính sách truy cập (singleton): đệm <see cref="CacheSeconds"/> giây vì mọi request đều cần (như chế độ bảo trì). Lưu trên CFG thì cập nhật
/// ngay bản đệm của máy chủ này.
/// </summary>
public interface IAccessPolicyProvider
{
    /// <summary>Số giây đệm.</summary>
    const int CacheSeconds = 5;

    /// <summary>Bản đệm hiện có (không đọc CSDL) – cho chỗ không chờ được như bộ giới hạn tần suất; middleware đầu request đã làm mới trước.</summary>
    AccessPolicySnapshot Current { get; }

    /// <summary>Lấy chính sách, đọc lại CSDL khi bản đệm quá <see cref="CacheSeconds"/> giây.</summary>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Chính sách.</returns>
    Task<AccessPolicySnapshot> GetAsync(CancellationToken cancellationToken);

    /// <summary>Thay bản đệm ngay (sau khi lưu).</summary>
    /// <param name="snapshot">Chính sách mới.</param>
    void Set(AccessPolicySnapshot snapshot);
}

/// <summary>Một dòng thông tin chỉ đọc trên tab "Bảo mật" của CFG (header, giới hạn, proxy) – cùng cột với bảng SYS.</summary>
/// <param name="Group">Nhóm.</param>
/// <param name="Item">Hạng mục.</param>
/// <param name="Value">Giá trị.</param>
/// <param name="Status">Đèn: Ok, Warning, Error, Neutral.</param>
/// <param name="Note">Ghi chú.</param>
public sealed record AccessSecurityRowDto(string Group, string Item, string Value, string Status, string? Note);

/// <summary>Cấu hình truy cập hiện CFG.</summary>
/// <param name="MinPasswordLength">Độ dài mật khẩu tối thiểu.</param>
/// <param name="BlockCommonPasswords">Chặn mật khẩu phổ biến.</param>
/// <param name="ExtraBlockedPasswords">Mật khẩu cấm thêm, mỗi dòng một mật khẩu (người có quyền quản lý mới xem được).</param>
/// <param name="MaxFailedLogins">Số lần sai liên tiếp thì tạm khóa.</param>
/// <param name="LockoutMinutes">Số phút tạm khóa.</param>
/// <param name="SessionMinutes">Hạn phiên (phút).</param>
/// <param name="OpsAllowedIpRanges">Dải IP quản trị, mỗi dòng một dải; trống = mọi IP.</param>
/// <param name="LoginPerMinutePerIp">Lần gọi đăng nhập / phút / IP.</param>
/// <param name="ApiPerMinutePerUser">Lần gọi API / phút / phiên.</param>
/// <param name="HeavyConcurrentPerUser">Việc nặng chạy cùng lúc / phiên.</param>
/// <param name="SessionsRevokedAt">Lần "Đăng xuất tất cả" gần nhất (UTC).</param>
/// <param name="UpdatedAt">Lần lưu gần nhất (UTC); null = đang dùng mặc định.</param>
/// <param name="UpdatedByName">Người lưu gần nhất.</param>
/// <param name="CommonPasswordCount">Số mật khẩu trong danh sách phổ biến nhúng sẵn.</param>
/// <param name="YourIp">IP của người đang xem (để khai dải).</param>
/// <param name="AuditId">Khóa dòng cấu hình (F1 xem lịch sử); null khi chưa lưu lần nào.</param>
/// <param name="Security">Thông tin bảo mật chỉ đọc (header, HTTPS, proxy tin cậy, giới hạn tần suất).</param>
public sealed record AccessConfigDto(
    int MinPasswordLength,
    bool BlockCommonPasswords,
    string? ExtraBlockedPasswords,
    int MaxFailedLogins,
    int LockoutMinutes,
    int SessionMinutes,
    string? OpsAllowedIpRanges,
    int LoginPerMinutePerIp,
    int ApiPerMinutePerUser,
    int HeavyConcurrentPerUser,
    DateTimeOffset? SessionsRevokedAt,
    DateTimeOffset? UpdatedAt,
    string? UpdatedByName,
    int CommonPasswordCount,
    string? YourIp,
    Guid? AuditId,
    IReadOnlyList<AccessSecurityRowDto> Security);

/// <summary>Lưu cấu hình truy cập.</summary>
/// <param name="MinPasswordLength">Độ dài mật khẩu tối thiểu.</param>
/// <param name="BlockCommonPasswords">Chặn mật khẩu phổ biến.</param>
/// <param name="ExtraBlockedPasswords">Mật khẩu cấm thêm.</param>
/// <param name="MaxFailedLogins">Số lần sai.</param>
/// <param name="LockoutMinutes">Phút khóa.</param>
/// <param name="SessionMinutes">Hạn phiên.</param>
/// <param name="OpsAllowedIpRanges">Dải IP quản trị.</param>
/// <param name="LoginPerMinutePerIp">Đăng nhập / phút / IP.</param>
/// <param name="ApiPerMinutePerUser">API / phút / phiên.</param>
/// <param name="HeavyConcurrentPerUser">Việc nặng cùng lúc.</param>
/// <param name="Reason">Lý do đổi (bắt buộc, ≥ 5 ký tự – vào nhật ký vận hành).</param>
public sealed record SaveAccessConfigRequest(
    int MinPasswordLength,
    bool BlockCommonPasswords,
    string? ExtraBlockedPasswords,
    int MaxFailedLogins,
    int LockoutMinutes,
    int SessionMinutes,
    string? OpsAllowedIpRanges,
    int LoginPerMinutePerIp,
    int ApiPerMinutePerUser,
    int HeavyConcurrentPerUser,
    string? Reason);

/// <summary>"Đăng xuất tất cả" – việc nguy hiểm Q6.</summary>
/// <param name="Reason">Lý do (≥ 5 ký tự).</param>
/// <param name="Password">Mật khẩu của người bấm.</param>
/// <param name="Confirmation">Chữ xác nhận "ĐĂNG XUẤT TẤT CẢ".</param>
public sealed record SignOutAllRequest(string? Reason, string? Password, string? Confirmation);

/// <summary>Kết quả "Đăng xuất tất cả": token mới cho người bấm (phiên đang dùng không bị đăng xuất).</summary>
/// <param name="RevokedAt">Mốc thu hồi (UTC).</param>
/// <param name="AccessToken">Token mới.</param>
/// <param name="ExpiresAt">Hạn token mới (UTC).</param>
public sealed record SignOutAllResult(DateTimeOffset RevokedAt, string AccessToken, DateTimeOffset ExpiresAt);

/// <summary>Màn hình CFG – cấu hình truy cập (lát QT7).</summary>
public interface IAccessConfigService
{
    /// <summary>Chữ xác nhận của "Đăng xuất tất cả" (Q6).</summary>
    const string SignOutAllConfirmation = "ĐĂNG XUẤT TẤT CẢ";

    /// <summary>Độ dài tối thiểu lý do.</summary>
    const int ReasonMinLength = 5;

    /// <summary>Đọc cấu hình.</summary>
    /// <param name="isHttps">Request đang dùng HTTPS (dòng HSTS của tab Bảo mật).</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Cấu hình, kèm IP người xem và thông tin bảo mật.</returns>
    Task<AccessConfigDto> GetAsync(bool isHttps, CancellationToken cancellationToken);

    /// <summary>
    /// Lưu cấu hình; dải IP mới phải gồm IP của người đang lưu (không tự khóa mình khỏi CFG). Ghi nhật ký vận hành; bản đệm đổi ngay.
    /// </summary>
    /// <param name="request">Giá trị mới + lý do.</param>
    /// <param name="isHttps">Request đang dùng HTTPS.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Cấu hình sau khi lưu.</returns>
    /// <exception cref="Domain.Common.BusinessRuleException">Thiếu lý do, giá trị ngoài khoảng, dải IP sai hoặc không gồm IP người lưu.</exception>
    Task<AccessConfigDto> SaveAsync(SaveAccessConfigRequest request, bool isHttps, CancellationToken cancellationToken);

    /// <summary>
    /// Đăng xuất tất cả: mọi token cấp trước lúc bấm bị từ chối (kể cả các phiên khác của người bấm); người bấm nhận token mới cho phiên đang dùng.
    /// Xác nhận Q6 (mật khẩu + lý do + gõ <see cref="SignOutAllConfirmation"/>).
    /// </summary>
    /// <param name="request">Lý do, mật khẩu, chữ xác nhận.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Mốc thu hồi + token mới.</returns>
    /// <exception cref="Domain.Common.BusinessRuleException">Xác nhận sai, thiếu lý do.</exception>
    Task<SignOutAllResult> SignOutAllAsync(SignOutAllRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Cấu hình bảo mật theo máy (mục "Security" của appsettings – lát QT7): proxy được tin để đọc IP thật, HSTS. Phần sửa được trên màn hình nằm ở
/// <see cref="AccessPolicy"/>; phần này gắn với cách triển khai máy chủ nên đặt trong file cấu hình.
/// </summary>
public sealed class SecurityOptions
{
    /// <summary>Tên mục cấu hình.</summary>
    public const string Section = "Security";

    /// <summary>
    /// Proxy được tin (IP hoặc dải CIDR) – chỉ request đi qua các địa chỉ này mới được đọc header <c>X-Forwarded-For</c> / <c>-Proto</c>. Trống =
    /// không đọc header (IIS in-process đã đưa sẵn IP thật). Docker + Caddy: khai dải mạng Docker (ví dụ 172.16.0.0/12).
    /// </summary>
    public string[] KnownProxies { get; set; } = [];

    /// <summary>Số ngày trình duyệt nhớ "chỉ dùng HTTPS" (HSTS – chỉ gửi khi request là HTTPS); 0 = không gửi.</summary>
    public int HstsMaxAgeDays { get; set; } = 365;

    /// <summary>
    /// Cách gỡ khi lỡ khóa mình khỏi chức năng quản trị vận hành (người dùng yêu cầu 15/11/2026): đặt <c>true</c> trong appsettings.Production.json
    /// trên máy chủ (hoặc biến môi trường <c>Security__IgnoreOpsIpRanges=true</c>) thì tạm <b>bỏ kiểm tra dải IP</b> của CFG – có hiệu lực ngay, không
    /// cần khởi động lại; tab Bảo mật của CFG báo đỏ tới khi đặt lại <c>false</c>. Cách khác: đăng nhập ngay trên máy chủ (127.0.0.1 luôn được).
    /// </summary>
    public bool IgnoreOpsIpRanges { get; set; }
}

/// <summary>Các header bảo mật gắn vào mọi phản hồi (lát QT7) – một chỗ để middleware và tab "Bảo mật" của CFG dùng chung.</summary>
public static class SecurityHeaderValues
{
    /// <summary>
    /// CSP: chỉ nạp script / kết nối / ảnh từ chính máy chủ. <c>style-src 'unsafe-inline'</c> vì Ant Design sinh CSS lúc chạy; ảnh, font, khung
    /// <c>data:</c> / <c>blob:</c> cho ảnh xem trước, file PDF xem trước in; <c>frame-ancestors 'self'</c> – trang ngoài không nhúng được (chống bấm lừa).
    /// </summary>
    public const string ContentSecurityPolicy =
        "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data: blob:; font-src 'self' data:; " +
        "connect-src 'self'; frame-src 'self' blob:; worker-src 'self' blob:; object-src 'none'; base-uri 'self'; form-action 'self'; " +
        "frame-ancestors 'self'";

    /// <summary>Tắt các API trình duyệt phần mềm không dùng (camera, micro, vị trí, thanh toán, USB).</summary>
    public const string PermissionsPolicy = "camera=(), microphone=(), geolocation=(), payment=(), usb=()";

    /// <summary>Không gửi đường dẫn (có thể chứa mã mời) sang trang khác.</summary>
    public const string ReferrerPolicy = "same-origin";
}
