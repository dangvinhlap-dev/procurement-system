namespace Proc.Application.Portal;

/// <summary>Tài khoản Portal của một NCC (design.md 19.5, lát P1).</summary>
/// <param name="Id">Id tài khoản.</param>
/// <param name="SupplierId">NCC.</param>
/// <param name="Email">Email – tên đăng nhập.</param>
/// <param name="FullName">Họ tên.</param>
/// <param name="Phone">Điện thoại.</param>
/// <param name="Position">Chức vụ.</param>
/// <param name="IsSupplierAdmin">Quản trị NCC (tự mời, khóa người dùng con).</param>
/// <param name="Status">Invited, Active, Disabled.</param>
/// <param name="InvitedAt">Thời điểm mời (UTC).</param>
/// <param name="ActivatedAt">Thời điểm kích hoạt (UTC).</param>
/// <param name="LastLoginAt">Lần đăng nhập gần nhất (UTC).</param>
public sealed record SupplierPortalUserDto(
    Guid Id,
    Guid SupplierId,
    string Email,
    string FullName,
    string? Phone,
    string? Position,
    bool IsSupplierAdmin,
    string Status,
    DateTimeOffset InvitedAt,
    DateTimeOffset? ActivatedAt,
    DateTimeOffset? LastLoginAt);

/// <summary>Mời người dùng Portal.</summary>
/// <param name="Email">Email (tên đăng nhập).</param>
/// <param name="FullName">Họ tên.</param>
/// <param name="Phone">Điện thoại.</param>
/// <param name="Position">Chức vụ.</param>
public sealed record PortalInviteRequest(string Email, string FullName, string? Phone, string? Position);

/// <summary>Kết quả mời / gửi lại lời mời.</summary>
/// <param name="User">Tài khoản.</param>
/// <param name="EmailSent">Đã gửi email mời; false khi máy chủ chưa bật email hoặc gửi lỗi – người mời tự chuyển link.</param>
/// <param name="InviteUrl">Link kích hoạt (dùng một lần, có hạn) để người mời chuyển cho NCC khi email không gửi được.</param>
/// <param name="ExpiresAt">Link hết hạn lúc (UTC).</param>
public sealed record PortalInviteResult(SupplierPortalUserDto User, bool EmailSent, string InviteUrl, DateTimeOffset ExpiresAt);

/// <summary>Thông tin hiện trên trang kích hoạt.</summary>
/// <param name="Email">Email được mời.</param>
/// <param name="FullName">Họ tên.</param>
/// <param name="SupplierName">Tên NCC.</param>
public sealed record PortalInviteInfo(string Email, string FullName, string SupplierName);

/// <summary>Đặt mật khẩu từ link (kích hoạt hoặc quên mật khẩu).</summary>
/// <param name="Token">Mã trong link.</param>
/// <param name="Password">Mật khẩu mới.</param>
public sealed record PortalSetPasswordRequest(string Token, string Password);

/// <summary>Đăng nhập Portal bước 1.</summary>
/// <param name="Email">Email.</param>
/// <param name="Password">Mật khẩu.</param>
public sealed record PortalLoginRequest(string Email, string Password);

/// <summary>Bước 2 cần nhập OTP đã gửi email.</summary>
/// <param name="ChallengeId">Mã phiên OTP gửi kèm khi xác nhận.</param>
/// <param name="MaskedEmail">Email đã che bớt, ví dụ ba***@ncc.vn.</param>
/// <param name="ExpiresAt">OTP hết hạn lúc (UTC).</param>
/// <param name="DevCode">Mã OTP – chỉ có ở môi trường dev/test khi bật Portal:ExposeCodesForTesting; server thật luôn null.</param>
public sealed record PortalLoginChallenge(Guid ChallengeId, string MaskedEmail, DateTimeOffset ExpiresAt, string? DevCode);

/// <summary>Lý do đăng nhập bước 1 không qua.</summary>
public enum PortalLoginFailure
{
    /// <summary>Email hoặc mật khẩu sai, hoặc tài khoản chưa kích hoạt (báo chung để không lộ tài khoản).</summary>
    InvalidCredentials,

    /// <summary>Khóa tạm do nhập sai nhiều lần.</summary>
    LockedOut,

    /// <summary>Tài khoản hoặc NCC bị khóa.</summary>
    Disabled,
}

/// <summary>Kết quả đăng nhập bước 1.</summary>
/// <param name="Challenge">Phiên OTP khi mật khẩu đúng.</param>
/// <param name="Failure">Lý do không qua.</param>
/// <param name="LockoutEnd">Khóa tạm đến (UTC).</param>
public sealed record PortalLoginOutcome(PortalLoginChallenge? Challenge, PortalLoginFailure? Failure = null, DateTimeOffset? LockoutEnd = null);

/// <summary>Đăng nhập Portal bước 2.</summary>
/// <param name="ChallengeId">Mã phiên OTP.</param>
/// <param name="Code">OTP 6 số.</param>
public sealed record PortalVerifyOtpRequest(Guid ChallengeId, string Code);

/// <summary>NCC của người dùng Portal.</summary>
/// <param name="Id">Id NCC.</param>
/// <param name="Code">Mã NCC.</param>
/// <param name="Name">Tên NCC.</param>
/// <param name="TaxCode">Mã số thuế.</param>
/// <param name="Address">Địa chỉ.</param>
/// <param name="Phone">Điện thoại.</param>
/// <param name="Email">Email.</param>
/// <param name="Status">Trạng thái NCC (Potential, Approved, Suspended, Blacklisted).</param>
public sealed record PortalSupplierDto(
    Guid Id, string Code, string Name, string? TaxCode, string? Address, string? Phone, string? Email, string Status);

/// <summary>Người dùng Portal đang đăng nhập.</summary>
/// <param name="UserId">Id tài khoản.</param>
/// <param name="Email">Email.</param>
/// <param name="FullName">Họ tên.</param>
/// <param name="Phone">Điện thoại.</param>
/// <param name="Position">Chức vụ.</param>
/// <param name="IsSupplierAdmin">Quản trị NCC.</param>
/// <param name="Supplier">NCC.</param>
public sealed record PortalMeDto(
    Guid UserId, string Email, string FullName, string? Phone, string? Position, bool IsSupplierAdmin, PortalSupplierDto Supplier);

/// <summary>Token Portal sau khi qua OTP.</summary>
/// <param name="AccessToken">JWT (audience Portal – không dùng được cho API nội bộ).</param>
/// <param name="ExpiresAt">Hết hạn lúc (UTC).</param>
/// <param name="Me">Người dùng.</param>
public sealed record PortalTokenResult(string AccessToken, DateTimeOffset ExpiresAt, PortalMeDto Me);

/// <summary>Kết quả yêu cầu quên mật khẩu (luôn báo đã gửi để không lộ email nào có tài khoản).</summary>
/// <param name="DevToken">Mã đặt lại – chỉ có ở dev/test khi bật Portal:ExposeCodesForTesting.</param>
public sealed record PortalForgotPasswordResult(string? DevToken);

/// <summary>Đổi mật khẩu khi đã đăng nhập.</summary>
/// <param name="CurrentPassword">Mật khẩu hiện tại.</param>
/// <param name="NewPassword">Mật khẩu mới.</param>
public sealed record PortalChangePasswordRequest(string CurrentPassword, string NewPassword);

/// <summary>Phiên Portal hợp lệ của request hiện tại (đã kiểm tài khoản còn dùng, dấu bảo mật khớp).</summary>
/// <param name="UserId">Id tài khoản Portal.</param>
/// <param name="SupplierId">NCC – mọi dữ liệu Portal lọc theo NCC này.</param>
/// <param name="IsSupplierAdmin">Quản trị NCC.</param>
/// <param name="Email">Email.</param>
public sealed record PortalSession(Guid UserId, Guid SupplierId, bool IsSupplierAdmin, string Email);

/// <summary>
/// Quản lý tài khoản Portal của NCC: Phòng CUVT (API nội bộ, quyền MasterData.Supplier.Portal) và quản trị NCC (trên Portal, chỉ NCC mình).
/// </summary>
public interface ISupplierPortalAccountService
{
    /// <summary>Danh sách tài khoản Portal của NCC.</summary>
    /// <param name="supplierId">NCC.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Tài khoản, quản trị NCC trước.</returns>
    /// <exception cref="Common.NotFoundException">NCC không có.</exception>
    Task<IReadOnlyList<SupplierPortalUserDto>> ListAsync(Guid supplierId, CancellationToken cancellationToken);

    /// <summary>
    /// Mời người dùng: tạo tài khoản Đã mời + link kích hoạt, gửi email nếu máy chủ bật email.
    /// Phòng CUVT mời thì là quản trị NCC; quản trị NCC mời thì là người dùng con.
    /// </summary>
    /// <param name="supplierId">NCC.</param>
    /// <param name="request">Thông tin người được mời.</param>
    /// <param name="bySupplierAdmin">Quản trị NCC mời (null = Phòng CUVT mời).</param>
    /// <param name="fallbackBaseUrl">Địa chỉ giao diện dùng khi chưa cấu hình Portal:BaseUrl (lấy từ request).</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Tài khoản + link kích hoạt.</returns>
    /// <exception cref="Domain.Common.BusinessRuleException">NCC nội bộ / danh sách đen, email sai, email đã có tài khoản.</exception>
    Task<PortalInviteResult> InviteAsync(
        Guid supplierId, PortalInviteRequest request, PortalSession? bySupplierAdmin, string fallbackBaseUrl, CancellationToken cancellationToken);

    /// <summary>Gửi lại lời mời (link cũ mất hiệu lực). Chỉ tài khoản Đã mời.</summary>
    /// <param name="supplierId">NCC.</param>
    /// <param name="portalUserId">Tài khoản.</param>
    /// <param name="bySupplierAdmin">Quản trị NCC thao tác (null = Phòng CUVT).</param>
    /// <param name="fallbackBaseUrl">Địa chỉ giao diện dự phòng.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Tài khoản + link mới.</returns>
    Task<PortalInviteResult> ResendInviteAsync(
        Guid supplierId, Guid portalUserId, PortalSession? bySupplierAdmin, string fallbackBaseUrl, CancellationToken cancellationToken);

    /// <summary>Khóa tài khoản (token đang dùng mất hiệu lực). Quản trị NCC không tự khóa mình.</summary>
    /// <param name="supplierId">NCC.</param>
    /// <param name="portalUserId">Tài khoản.</param>
    /// <param name="bySupplierAdmin">Quản trị NCC thao tác (null = Phòng CUVT).</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Tài khoản sau khi khóa.</returns>
    Task<SupplierPortalUserDto> DisableAsync(
        Guid supplierId, Guid portalUserId, PortalSession? bySupplierAdmin, CancellationToken cancellationToken);

    /// <summary>Mở khóa tài khoản.</summary>
    /// <param name="supplierId">NCC.</param>
    /// <param name="portalUserId">Tài khoản.</param>
    /// <param name="bySupplierAdmin">Quản trị NCC thao tác (null = Phòng CUVT). Quản trị NCC không mở được tài khoản quản trị.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Tài khoản sau khi mở.</returns>
    Task<SupplierPortalUserDto> EnableAsync(
        Guid supplierId, Guid portalUserId, PortalSession? bySupplierAdmin, CancellationToken cancellationToken);
}

/// <summary>Đăng nhập, kích hoạt, mật khẩu của người dùng Portal NCC (endpoint ẩn danh và endpoint Portal).</summary>
public interface IPortalAuthService
{
    /// <summary>Thông tin link mời còn hiệu lực.</summary>
    /// <param name="token">Mã trong link.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Thông tin hiện trên trang kích hoạt.</returns>
    /// <exception cref="Domain.Common.BusinessRuleException">Link sai, đã dùng hoặc hết hạn.</exception>
    Task<PortalInviteInfo> GetInviteAsync(string token, CancellationToken cancellationToken);

    /// <summary>Kích hoạt tài khoản: đặt mật khẩu lần đầu từ link mời.</summary>
    /// <param name="request">Mã + mật khẩu.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Task hoàn tất khi đã lưu.</returns>
    Task ActivateAsync(PortalSetPasswordRequest request, CancellationToken cancellationToken);

    /// <summary>Đăng nhập bước 1: kiểm mật khẩu, gửi OTP qua email.</summary>
    /// <param name="request">Email + mật khẩu.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Phiên OTP hoặc lý do không qua.</returns>
    Task<PortalLoginOutcome> LoginAsync(PortalLoginRequest request, CancellationToken cancellationToken);

    /// <summary>Đăng nhập bước 2: kiểm OTP, cấp token Portal.</summary>
    /// <param name="request">Phiên + mã.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Token, hoặc null khi mã sai / hết hạn / quá số lần.</returns>
    Task<PortalTokenResult?> VerifyOtpAsync(PortalVerifyOtpRequest request, CancellationToken cancellationToken);

    /// <summary>Quên mật khẩu: gửi link đặt lại nếu email có tài khoản đang dùng (không báo email có hay không).</summary>
    /// <param name="email">Email.</param>
    /// <param name="fallbackBaseUrl">Địa chỉ giao diện dự phòng.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Kết quả (mã chỉ có ở dev/test).</returns>
    Task<PortalForgotPasswordResult> ForgotPasswordAsync(string email, string fallbackBaseUrl, CancellationToken cancellationToken);

    /// <summary>Đặt lại mật khẩu từ link quên mật khẩu.</summary>
    /// <param name="request">Mã + mật khẩu mới.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Task hoàn tất khi đã lưu.</returns>
    Task ResetPasswordAsync(PortalSetPasswordRequest request, CancellationToken cancellationToken);

    /// <summary>Kiểm phiên của token Portal: tài khoản còn dùng, NCC không bị chặn, dấu bảo mật khớp.</summary>
    /// <param name="portalUserId">Id trong token.</param>
    /// <param name="securityStamp">Dấu bảo mật trong token.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Phiên, hoặc null khi token không còn hiệu lực.</returns>
    Task<PortalSession?> ValidateSessionAsync(Guid portalUserId, string? securityStamp, CancellationToken cancellationToken);

    /// <summary>Thông tin người dùng đang đăng nhập.</summary>
    /// <param name="session">Phiên.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Người dùng + NCC.</returns>
    Task<PortalMeDto> GetMeAsync(PortalSession session, CancellationToken cancellationToken);

    /// <summary>Đổi mật khẩu; token hiện tại mất hiệu lực, trả token mới.</summary>
    /// <param name="session">Phiên.</param>
    /// <param name="request">Mật khẩu cũ + mới.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Token mới.</returns>
    /// <exception cref="Domain.Common.BusinessRuleException">Mật khẩu hiện tại sai hoặc mật khẩu mới yếu.</exception>
    Task<PortalTokenResult> ChangePasswordAsync(PortalSession session, PortalChangePasswordRequest request, CancellationToken cancellationToken);
}
