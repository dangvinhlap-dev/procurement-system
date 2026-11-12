using System.Text.RegularExpressions;
using Proc.Domain.Common;

namespace Proc.Domain.MasterData;

/// <summary>Trạng thái tài khoản Portal của NCC (design.md 19.5, lát P1).</summary>
public enum SupplierPortalUserStatus
{
    /// <summary>Đã mời, chưa đặt mật khẩu – chưa đăng nhập được.</summary>
    Invited = 1,

    /// <summary>Đã kích hoạt, đăng nhập được.</summary>
    Active = 2,

    /// <summary>Bị khóa (Phòng CUVT hoặc quản trị NCC khóa) – mọi phiên đang mở mất hiệu lực.</summary>
    Disabled = 3,
}

/// <summary>Mục đích của mã dùng một lần gửi cho NCC.</summary>
public enum SupplierPortalCodePurpose
{
    /// <summary>Link mời kích hoạt tài khoản (đặt mật khẩu lần đầu).</summary>
    Invite = 1,

    /// <summary>Link đặt lại mật khẩu khi quên.</summary>
    PasswordReset = 2,

    /// <summary>Mã OTP 6 số gửi email ở bước 2 khi đăng nhập.</summary>
    LoginOtp = 3,
}

/// <summary>
/// Tài khoản người dùng của NCC trên Portal (design.md 19.5, lát P1; Q11: vào Portal qua lời mời, quản trị NCC tự tạo người dùng con).
/// Tách hẳn khỏi người dùng nội bộ (AppUser): không có công ty, vai trò, quyền nội bộ; token Portal có audience riêng nên không gọi được
/// API nội bộ. Một email chỉ thuộc một NCC (email là tên đăng nhập).
/// </summary>
[Auditable(
    "Tài khoản Portal NCC",
    LabelProperty = nameof(Email),
    Ignore = [nameof(FailedLoginCount), nameof(LastLoginAt), nameof(SecurityStamp), nameof(LockoutEnd)],
    Redact = [nameof(PasswordHash)])]
public sealed partial class SupplierPortalUser : AuditableEntity
{
    /// <summary>Độ dài tối đa của email.</summary>
    public const int EmailMaxLength = 256;

    /// <summary>Độ dài tối thiểu của mật khẩu – cùng chính sách người dùng nội bộ (Identity: 8 ký tự, có chữ số, có chữ thường).</summary>
    public const int PasswordMinLength = 8;

    private SupplierPortalUser()
    {
    }

    /// <summary>NCC sở hữu tài khoản.</summary>
    public Guid SupplierId { get; private set; }

    /// <summary>Email – tên đăng nhập, lưu chữ thường, duy nhất toàn hệ thống.</summary>
    public string Email { get; private set; } = string.Empty;

    /// <summary>Họ tên người dùng.</summary>
    public string FullName { get; private set; } = string.Empty;

    /// <summary>Điện thoại.</summary>
    public string? Phone { get; private set; }

    /// <summary>Chức vụ tại NCC.</summary>
    public string? Position { get; private set; }

    /// <summary>Quản trị NCC: tự mời, khóa người dùng con của NCC mình. Tài khoản do Phòng CUVT mời luôn là quản trị NCC.</summary>
    public bool IsSupplierAdmin { get; private set; }

    /// <summary>Trạng thái.</summary>
    public SupplierPortalUserStatus Status { get; private set; } = SupplierPortalUserStatus.Invited;

    /// <summary>Mật khẩu đã băm (Identity PasswordHasher); null khi chưa kích hoạt.</summary>
    public string? PasswordHash { get; private set; }

    /// <summary>Thời điểm kích hoạt (UTC).</summary>
    public DateTimeOffset? ActivatedAt { get; private set; }

    /// <summary>Lần đăng nhập thành công gần nhất (UTC, sau bước OTP).</summary>
    public DateTimeOffset? LastLoginAt { get; private set; }

    /// <summary>Số lần nhập sai mật khẩu liên tiếp.</summary>
    public int FailedLoginCount { get; private set; }

    /// <summary>Khóa tạm đến thời điểm này (UTC) do nhập sai nhiều lần.</summary>
    public DateTimeOffset? LockoutEnd { get; private set; }

    /// <summary>
    /// Dấu bảo mật ghi vào token; đổi khi đổi mật khẩu hoặc khóa tài khoản để mọi token cũ mất hiệu lực ngay.
    /// </summary>
    public string SecurityStamp { get; private set; } = NewStamp();

    /// <summary>Đăng nhập được (đã kích hoạt, không bị khóa hẳn).</summary>
    public bool CanSignIn => Status == SupplierPortalUserStatus.Active && PasswordHash is not null;

    /// <summary>
    /// Mời người dùng mới cho NCC.
    /// </summary>
    /// <param name="supplierId">NCC.</param>
    /// <param name="email">Email (tên đăng nhập).</param>
    /// <param name="fullName">Họ tên, bắt buộc.</param>
    /// <param name="phone">Điện thoại.</param>
    /// <param name="position">Chức vụ.</param>
    /// <param name="isSupplierAdmin">Quản trị NCC.</param>
    /// <returns>Tài khoản ở trạng thái Đã mời.</returns>
    /// <exception cref="BusinessRuleException">Email sai định dạng hoặc họ tên trống.</exception>
    public static SupplierPortalUser Invite(
        Guid supplierId, string email, string fullName, string? phone, string? position, bool isSupplierAdmin)
    {
        if (string.IsNullOrWhiteSpace(fullName)) throw new BusinessRuleException("Họ tên người dùng Portal không được để trống.");
        return new SupplierPortalUser
        {
            SupplierId = supplierId,
            Email = NormalizeEmail(email),
            FullName = fullName.Trim(),
            Phone = Clean(phone),
            Position = Clean(position),
            IsSupplierAdmin = isSupplierAdmin,
        };
    }

    /// <summary>
    /// Chuẩn hóa email đăng nhập: bỏ khoảng trắng, chữ thường, kiểm định dạng.
    /// </summary>
    /// <param name="email">Email gõ vào.</param>
    /// <returns>Email chữ thường.</returns>
    /// <exception cref="BusinessRuleException">Email trống, quá dài hoặc sai định dạng.</exception>
    public static string NormalizeEmail(string? email)
    {
        var value = (email ?? string.Empty).Trim().ToLowerInvariant();
        if (value.Length == 0 || value.Length > EmailMaxLength || !EmailRegex().IsMatch(value))
            throw new BusinessRuleException("Email không đúng định dạng (ví dụ ten@congty.vn).");
        return value;
    }

    /// <summary>
    /// Kiểm mật khẩu theo chính sách chung: tối thiểu 8 ký tự, có chữ số và chữ thường (như người dùng nội bộ).
    /// </summary>
    /// <param name="password">Mật khẩu.</param>
    /// <returns>Câu báo lỗi tiếng Việt, hoặc null khi đạt.</returns>
    public static string? ValidatePassword(string? password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < PasswordMinLength)
            return $"Mật khẩu tối thiểu {PasswordMinLength} ký tự.";
        if (!password.Any(char.IsDigit)) return "Mật khẩu phải có ít nhất một chữ số.";
        if (!password.Any(char.IsLower)) return "Mật khẩu phải có ít nhất một chữ thường.";
        return null;
    }

    /// <summary>
    /// Kích hoạt từ link mời: đặt mật khẩu lần đầu.
    /// </summary>
    /// <param name="passwordHash">Mật khẩu đã băm.</param>
    /// <param name="now">Thời điểm (UTC).</param>
    /// <exception cref="BusinessRuleException">Tài khoản không ở trạng thái Đã mời.</exception>
    public void Activate(string passwordHash, DateTimeOffset now)
    {
        if (Status != SupplierPortalUserStatus.Invited)
            throw new BusinessRuleException(Status == SupplierPortalUserStatus.Active
                ? "Tài khoản đã kích hoạt. Hãy đăng nhập."
                : "Tài khoản đã bị khóa. Liên hệ Phòng Cung ứng vật tư.");
        SetHash(passwordHash);
        Status = SupplierPortalUserStatus.Active;
        ActivatedAt = now;
    }

    /// <summary>
    /// Đặt mật khẩu mới (đổi mật khẩu, quên mật khẩu). Mọi phiên đang mở mất hiệu lực.
    /// </summary>
    /// <param name="passwordHash">Mật khẩu đã băm.</param>
    /// <exception cref="BusinessRuleException">Tài khoản chưa kích hoạt hoặc đã bị khóa.</exception>
    public void ChangePassword(string passwordHash)
    {
        if (Status != SupplierPortalUserStatus.Active)
            throw new BusinessRuleException("Tài khoản chưa kích hoạt hoặc đã bị khóa.");
        SetHash(passwordHash);
    }

    /// <summary>Đang bị khóa tạm do nhập sai mật khẩu.</summary>
    /// <param name="now">Thời điểm (UTC).</param>
    /// <returns>true khi còn trong thời gian khóa.</returns>
    public bool IsLockedOut(DateTimeOffset now) => LockoutEnd is { } end && end > now;

    /// <summary>
    /// Ghi một lần nhập sai mật khẩu; đủ <paramref name="maxAttempts"/> lần liên tiếp thì khóa tạm.
    /// </summary>
    /// <param name="now">Thời điểm (UTC).</param>
    /// <param name="maxAttempts">Số lần sai tối đa.</param>
    /// <param name="lockout">Thời gian khóa tạm.</param>
    /// <returns>true khi lần sai này làm tài khoản bị khóa tạm.</returns>
    public bool RecordFailedLogin(DateTimeOffset now, int maxAttempts, TimeSpan lockout)
    {
        FailedLoginCount++;
        if (FailedLoginCount < maxAttempts) return false;
        FailedLoginCount = 0;
        LockoutEnd = now.Add(lockout);
        return true;
    }

    /// <summary>Mật khẩu đúng: xóa đếm sai và khóa tạm.</summary>
    public void RecordPasswordOk()
    {
        FailedLoginCount = 0;
        LockoutEnd = null;
    }

    /// <summary>Đăng nhập xong (đã qua OTP).</summary>
    /// <param name="now">Thời điểm (UTC).</param>
    public void RecordLogin(DateTimeOffset now) => LastLoginAt = now;

    /// <summary>
    /// Khóa tài khoản: không đăng nhập được, token đang dùng mất hiệu lực.
    /// </summary>
    /// <exception cref="BusinessRuleException">Đã khóa rồi.</exception>
    public void Disable()
    {
        if (Status == SupplierPortalUserStatus.Disabled) throw new BusinessRuleException("Tài khoản đã bị khóa.");
        Status = SupplierPortalUserStatus.Disabled;
        SecurityStamp = NewStamp();
    }

    /// <summary>
    /// Mở khóa: về Đang dùng nếu đã từng đặt mật khẩu, ngược lại về Đã mời (cần gửi lại lời mời).
    /// </summary>
    /// <exception cref="BusinessRuleException">Tài khoản không bị khóa.</exception>
    public void Enable()
    {
        if (Status != SupplierPortalUserStatus.Disabled) throw new BusinessRuleException("Tài khoản không bị khóa.");
        Status = PasswordHash is null ? SupplierPortalUserStatus.Invited : SupplierPortalUserStatus.Active;
        FailedLoginCount = 0;
        LockoutEnd = null;
    }

    private void SetHash(string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash)) throw new ArgumentException("Thiếu mật khẩu đã băm.", nameof(passwordHash));
        PasswordHash = passwordHash;
        SecurityStamp = NewStamp();
        FailedLoginCount = 0;
        LockoutEnd = null;
    }

    private static string NewStamp() => Guid.NewGuid().ToString("N");

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    // Kiểm định dạng cơ bản (một @, tên miền có dấu chấm); email thật hay không do bước kích hoạt qua link xác nhận.
    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailRegex();
}

/// <summary>
/// Mã dùng một lần gửi qua email cho người dùng Portal: link mời, link đặt lại mật khẩu, OTP đăng nhập. Chỉ lưu băm SHA-256 của mã
/// (lộ CSDL không lộ mã); dùng xong hoặc hết hạn thì thôi. Không ghi AuditLog vì là dữ liệu bí mật tạm thời.
/// </summary>
public sealed class SupplierPortalCode : Entity
{
    private SupplierPortalCode()
    {
    }

    /// <summary>Tài khoản Portal.</summary>
    public Guid PortalUserId { get; private set; }

    /// <summary>Mục đích.</summary>
    public SupplierPortalCodePurpose Purpose { get; private set; }

    /// <summary>Băm SHA-256 (hex) của mã.</summary>
    public string CodeHash { get; private set; } = string.Empty;

    /// <summary>Thời điểm tạo (UTC).</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Hết hạn lúc (UTC).</summary>
    public DateTimeOffset ExpiresAt { get; private set; }

    /// <summary>Đã dùng lúc (UTC); null = chưa dùng.</summary>
    public DateTimeOffset? UsedAt { get; private set; }

    /// <summary>Số lần nhập sai (chỉ OTP).</summary>
    public int FailedAttempts { get; private set; }

    /// <summary>
    /// Tạo mã mới.
    /// </summary>
    /// <param name="portalUserId">Tài khoản.</param>
    /// <param name="purpose">Mục đích.</param>
    /// <param name="codeHash">Băm của mã.</param>
    /// <param name="now">Thời điểm (UTC).</param>
    /// <param name="lifetime">Thời hạn.</param>
    /// <returns>Mã mới.</returns>
    public static SupplierPortalCode Issue(
        Guid portalUserId, SupplierPortalCodePurpose purpose, string codeHash, DateTimeOffset now, TimeSpan lifetime) => new()
    {
        PortalUserId = portalUserId,
        Purpose = purpose,
        CodeHash = codeHash,
        CreatedAt = now,
        ExpiresAt = now.Add(lifetime),
    };

    /// <summary>Còn dùng được: chưa dùng, chưa hết hạn, chưa sai quá số lần.</summary>
    /// <param name="now">Thời điểm (UTC).</param>
    /// <param name="maxFailedAttempts">Số lần sai tối đa.</param>
    /// <returns>true khi còn dùng được.</returns>
    public bool IsUsable(DateTimeOffset now, int maxFailedAttempts) =>
        UsedAt is null && now < ExpiresAt && FailedAttempts < maxFailedAttempts;

    /// <summary>Ghi một lần nhập sai mã.</summary>
    public void RecordFailedAttempt() => FailedAttempts++;

    /// <summary>Đánh dấu đã dùng – không dùng lại được.</summary>
    /// <param name="now">Thời điểm (UTC).</param>
    public void MarkUsed(DateTimeOffset now) => UsedAt = now;
}
