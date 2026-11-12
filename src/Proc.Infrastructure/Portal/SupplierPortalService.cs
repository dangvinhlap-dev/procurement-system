using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Proc.Application.Common;
using Proc.Application.Logging;
using Proc.Application.Notifications;
using Proc.Application.Portal;
using Proc.Domain.Common;
using Proc.Domain.MasterData;
using Proc.Infrastructure.Notifications;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.Portal;

/// <summary>
/// Tài khoản, đăng nhập (mật khẩu + OTP email), kích hoạt, quên mật khẩu của người dùng Portal NCC (design.md 19.5, lát P1).
/// Mã trong link và OTP chỉ lưu băm SHA-256; mật khẩu băm bằng PasswordHasher của Identity như người dùng nội bộ.
/// </summary>
public sealed class SupplierPortalService : ISupplierPortalAccountService, IPortalAuthService
{
    private readonly ProcDbContext _db;
    private readonly IPasswordHasher<SupplierPortalUser> _hasher;
    private readonly PortalTokenService _tokens;
    private readonly IEmailSender _email;
    private readonly IOptionsMonitor<EmailOptions> _emailOptions;
    private readonly PortalOptions _options;
    private readonly IAccessLog _accessLog;
    private readonly IClock _clock;
    private readonly ILogger<SupplierPortalService> _logger;

    /// <summary>Tạo dịch vụ.</summary>
    /// <param name="db">DbContext.</param>
    /// <param name="hasher">Băm mật khẩu.</param>
    /// <param name="tokens">Cấp token Portal.</param>
    /// <param name="email">Gửi email.</param>
    /// <param name="emailOptions">Cấu hình email (bật/tắt, địa chỉ phần mềm).</param>
    /// <param name="options">Cấu hình Portal.</param>
    /// <param name="accessLog">Nhật ký truy cập.</param>
    /// <param name="clock">Đồng hồ.</param>
    /// <param name="logger">Ghi log.</param>
    public SupplierPortalService(
        ProcDbContext db,
        IPasswordHasher<SupplierPortalUser> hasher,
        PortalTokenService tokens,
        IEmailSender email,
        IOptionsMonitor<EmailOptions> emailOptions,
        IOptions<PortalOptions> options,
        IAccessLog accessLog,
        IClock clock,
        ILogger<SupplierPortalService> logger)
    {
        _db = db;
        _hasher = hasher;
        _tokens = tokens;
        _email = email;
        _emailOptions = emailOptions;
        _options = options.Value;
        _accessLog = accessLog;
        _clock = clock;
        _logger = logger;
    }

    // ───────────────────────── Quản lý tài khoản (Phòng CUVT, quản trị NCC) ─────────────────────────

    /// <inheritdoc />
    public async Task<IReadOnlyList<SupplierPortalUserDto>> ListAsync(Guid supplierId, CancellationToken cancellationToken)
    {
        await RequireSupplierAsync(supplierId, cancellationToken);
        var users = await _db.SupplierPortalUsers.AsNoTracking()
            .Where(x => x.SupplierId == supplierId)
            .OrderByDescending(x => x.IsSupplierAdmin).ThenBy(x => x.Email)
            .ToListAsync(cancellationToken);
        return users.Select(ToDto).ToList();
    }

    /// <inheritdoc />
    public async Task<PortalInviteResult> InviteAsync(
        Guid supplierId, PortalInviteRequest request, PortalSession? bySupplierAdmin, string fallbackBaseUrl, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var supplier = await RequireSupplierAsync(supplierId, cancellationToken);
        RequireAdminOf(bySupplierAdmin, supplierId);
        // NCC nội bộ là công ty thành viên – dùng thẳng phần mềm, không qua Portal (design.md 19.1 Q17).
        if (supplier.InternalCompanyId is not null)
            throw new BusinessRuleException("NCC nội bộ là công ty thành viên, dùng phần mềm trực tiếp – không mời vào Portal.");
        if (supplier.Status == SupplierStatus.Blacklisted)
            throw new BusinessRuleException("NCC đang trong danh sách đen – không mời vào Portal.");

        // Phòng CUVT mời → quản trị NCC; quản trị NCC mời → người dùng con (Q11).
        var user = SupplierPortalUser.Invite(
            supplierId, request.Email, request.FullName, request.Phone, request.Position, isSupplierAdmin: bySupplierAdmin is null);
        if (await _db.SupplierPortalUsers.AnyAsync(x => x.Email == user.Email, cancellationToken))
            throw new BusinessRuleException("Email này đã có tài khoản Portal (một email chỉ thuộc một NCC).");
        _db.SupplierPortalUsers.Add(user);
        return await IssueInviteAsync(user, supplier.Name, fallbackBaseUrl, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PortalInviteResult> ResendInviteAsync(
        Guid supplierId, Guid portalUserId, PortalSession? bySupplierAdmin, string fallbackBaseUrl, CancellationToken cancellationToken)
    {
        var supplier = await RequireSupplierAsync(supplierId, cancellationToken);
        RequireAdminOf(bySupplierAdmin, supplierId);
        var user = await RequireUserAsync(supplierId, portalUserId, cancellationToken);
        if (user.Status != SupplierPortalUserStatus.Invited)
            throw new BusinessRuleException("Chỉ gửi lại lời mời cho tài khoản chưa kích hoạt.");
        await ExpireCodesAsync(user.Id, SupplierPortalCodePurpose.Invite, cancellationToken);
        return await IssueInviteAsync(user, supplier.Name, fallbackBaseUrl, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<SupplierPortalUserDto> DisableAsync(
        Guid supplierId, Guid portalUserId, PortalSession? bySupplierAdmin, CancellationToken cancellationToken)
    {
        await RequireSupplierAsync(supplierId, cancellationToken);
        RequireAdminOf(bySupplierAdmin, supplierId);
        var user = await RequireUserAsync(supplierId, portalUserId, cancellationToken);
        if (bySupplierAdmin is not null && user.Id == bySupplierAdmin.UserId)
            throw new BusinessRuleException("Không tự khóa tài khoản của mình.");
        if (bySupplierAdmin is not null && user.IsSupplierAdmin)
            throw new BusinessRuleException("Tài khoản quản trị NCC chỉ Phòng Cung ứng vật tư khóa được.");
        user.Disable();
        await _db.SaveChangesAsync(cancellationToken);
        return ToDto(user);
    }

    /// <inheritdoc />
    public async Task<SupplierPortalUserDto> EnableAsync(
        Guid supplierId, Guid portalUserId, PortalSession? bySupplierAdmin, CancellationToken cancellationToken)
    {
        await RequireSupplierAsync(supplierId, cancellationToken);
        RequireAdminOf(bySupplierAdmin, supplierId);
        var user = await RequireUserAsync(supplierId, portalUserId, cancellationToken);
        if (bySupplierAdmin is not null && user.IsSupplierAdmin)
            throw new BusinessRuleException("Tài khoản quản trị NCC chỉ Phòng Cung ứng vật tư mở khóa được.");
        user.Enable();
        await _db.SaveChangesAsync(cancellationToken);
        return ToDto(user);
    }

    // ───────────────────────── Kích hoạt, đăng nhập, mật khẩu ─────────────────────────

    /// <inheritdoc />
    public async Task<PortalInviteInfo> GetInviteAsync(string token, CancellationToken cancellationToken)
    {
        var (_, user) = await RequireLinkAsync(token, SupplierPortalCodePurpose.Invite, cancellationToken);
        var supplierName = await _db.Suppliers.AsNoTracking().Where(x => x.Id == user.SupplierId).Select(x => x.Name).FirstAsync(cancellationToken);
        return new PortalInviteInfo(user.Email, user.FullName, supplierName);
    }

    /// <inheritdoc />
    public async Task ActivateAsync(PortalSetPasswordRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var (code, user) = await RequireLinkAsync(request.Token, SupplierPortalCodePurpose.Invite, cancellationToken);
        RequireStrongPassword(request.Password);
        user.Activate(_hasher.HashPassword(user, request.Password), _clock.UtcNow);
        code.MarkUsed(_clock.UtcNow);
        await _db.SaveChangesAsync(cancellationToken);
        await LogAsync(AccessEvent.PasswordChanged, user, "Kích hoạt tài khoản Portal NCC.", cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PortalLoginOutcome> LoginAsync(PortalLoginRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var email = (request.Email ?? string.Empty).Trim().ToLowerInvariant();
        var user = await _db.SupplierPortalUsers.FirstOrDefaultAsync(x => x.Email == email, cancellationToken);
        var now = _clock.UtcNow;
        if (user is null)
        {
            await _accessLog.WriteAsync(AccessEvent.LoginFailed, null, PortalClaims.UserNamePrefix + email, null,
                "Portal NCC: email không có tài khoản.", cancellationToken);
            return new PortalLoginOutcome(null, PortalLoginFailure.InvalidCredentials);
        }

        if (user.IsLockedOut(now))
        {
            await LogAsync(AccessEvent.LockedOut, user, "Portal NCC: đăng nhập khi đang khóa tạm.", cancellationToken);
            return new PortalLoginOutcome(null, PortalLoginFailure.LockedOut, user.LockoutEnd);
        }

        // Chưa kích hoạt thì không có mật khẩu: báo chung như sai mật khẩu để không lộ tài khoản.
        if (user.PasswordHash is null
            || _hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password ?? string.Empty) == PasswordVerificationResult.Failed)
        {
            var locked = user.RecordFailedLogin(now, _options.MaxFailedLogins, TimeSpan.FromMinutes(_options.LockoutMinutes));
            await _db.SaveChangesAsync(cancellationToken);
            await LogAsync(locked ? AccessEvent.LockedOut : AccessEvent.LoginFailed, user,
                locked ? $"Portal NCC: sai mật khẩu {_options.MaxFailedLogins} lần, khóa tạm." : "Portal NCC: sai mật khẩu.", cancellationToken);
            return locked
                ? new PortalLoginOutcome(null, PortalLoginFailure.LockedOut, user.LockoutEnd)
                : new PortalLoginOutcome(null, PortalLoginFailure.InvalidCredentials);
        }

        user.RecordPasswordOk();
        // Báo "bị khóa" chỉ sau khi mật khẩu đúng (như đăng nhập nội bộ) để người ngoài không dò được tài khoản.
        if (!user.CanSignIn || !await SupplierAllowsLoginAsync(user.SupplierId, cancellationToken))
        {
            await _db.SaveChangesAsync(cancellationToken);
            await LogAsync(AccessEvent.LoginFailed, user, "Portal NCC: tài khoản hoặc NCC bị khóa.", cancellationToken);
            return new PortalLoginOutcome(null, PortalLoginFailure.Disabled);
        }

        // Bước 2: OTP 6 số qua email (design.md 19.5 – P1). Mã OTP cũ chưa dùng mất hiệu lực.
        await ExpireCodesAsync(user.Id, SupplierPortalCodePurpose.LoginOtp, cancellationToken);
        var otp = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        var code = SupplierPortalCode.Issue(
            user.Id, SupplierPortalCodePurpose.LoginOtp, Hash(otp), now, TimeSpan.FromMinutes(_options.OtpValidMinutes));
        _db.SupplierPortalCodes.Add(code);
        await _db.SaveChangesAsync(cancellationToken);

        var sent = await TrySendAsync(user, "Mã xác nhận đăng nhập Portal nhà cung cấp",
            $"Mã xác nhận đăng nhập của bạn là: {otp}",
            $"Mã có hiệu lực {_options.OtpValidMinutes} phút. Nếu không phải bạn đăng nhập, hãy đổi mật khẩu ngay.",
            null, cancellationToken);
        if (!sent && !_options.ExposeCodesForTesting)
            throw new BusinessRuleException(
                "Máy chủ chưa gửi được email mã xác nhận. Thử lại sau hoặc liên hệ Phòng Cung ứng vật tư.");

        return new PortalLoginOutcome(new PortalLoginChallenge(
            code.Id, MaskEmail(user.Email), code.ExpiresAt, _options.ExposeCodesForTesting ? otp : null));
    }

    /// <inheritdoc />
    public async Task<PortalTokenResult?> VerifyOtpAsync(PortalVerifyOtpRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var now = _clock.UtcNow;
        var code = await _db.SupplierPortalCodes.FirstOrDefaultAsync(
            x => x.Id == request.ChallengeId && x.Purpose == SupplierPortalCodePurpose.LoginOtp, cancellationToken);
        if (code is null || !code.IsUsable(now, _options.OtpMaxAttempts)) return null;

        var user = await _db.SupplierPortalUsers.FirstAsync(x => x.Id == code.PortalUserId, cancellationToken);
        if (!FixedEquals(code.CodeHash, Hash((request.Code ?? string.Empty).Trim())))
        {
            code.RecordFailedAttempt();
            await _db.SaveChangesAsync(cancellationToken);
            await LogAsync(AccessEvent.LoginFailed, user, "Portal NCC: sai mã OTP.", cancellationToken);
            return null;
        }

        if (!user.CanSignIn || !await SupplierAllowsLoginAsync(user.SupplierId, cancellationToken)) return null;
        code.MarkUsed(now);
        user.RecordLogin(now);
        await _db.SaveChangesAsync(cancellationToken);
        await LogAsync(AccessEvent.Login, user, "Đăng nhập Portal NCC.", cancellationToken);
        return await TokenResultAsync(user, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PortalForgotPasswordResult> ForgotPasswordAsync(string email, string fallbackBaseUrl, CancellationToken cancellationToken)
    {
        var normalized = (email ?? string.Empty).Trim().ToLowerInvariant();
        var user = await _db.SupplierPortalUsers.FirstOrDefaultAsync(x => x.Email == normalized, cancellationToken);
        // Không báo email có tài khoản hay không; tài khoản chưa kích hoạt dùng link mời, bị khóa thì không gửi.
        if (user is null || !user.CanSignIn) return new PortalForgotPasswordResult(null);

        await ExpireCodesAsync(user.Id, SupplierPortalCodePurpose.PasswordReset, cancellationToken);
        var token = NewLinkToken();
        _db.SupplierPortalCodes.Add(SupplierPortalCode.Issue(user.Id, SupplierPortalCodePurpose.PasswordReset, Hash(token),
            _clock.UtcNow, TimeSpan.FromMinutes(_options.PasswordResetValidMinutes)));
        await _db.SaveChangesAsync(cancellationToken);

        var url = BuildUrl(fallbackBaseUrl, "/ncc/dat-lai-mat-khau", token);
        await TrySendAsync(user, "Đặt lại mật khẩu Portal nhà cung cấp",
            "Bạn (hoặc ai đó) vừa yêu cầu đặt lại mật khẩu Portal nhà cung cấp.",
            $"Link có hiệu lực {_options.PasswordResetValidMinutes} phút, dùng một lần. Không yêu cầu thì bỏ qua email này.",
            url, cancellationToken);
        return new PortalForgotPasswordResult(_options.ExposeCodesForTesting ? token : null);
    }

    /// <inheritdoc />
    public async Task ResetPasswordAsync(PortalSetPasswordRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var (code, user) = await RequireLinkAsync(request.Token, SupplierPortalCodePurpose.PasswordReset, cancellationToken);
        RequireStrongPassword(request.Password);
        user.ChangePassword(_hasher.HashPassword(user, request.Password));
        code.MarkUsed(_clock.UtcNow);
        await _db.SaveChangesAsync(cancellationToken);
        await LogAsync(AccessEvent.PasswordChanged, user, "Portal NCC: đặt lại mật khẩu qua email.", cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PortalSession?> ValidateSessionAsync(Guid portalUserId, string? securityStamp, CancellationToken cancellationToken)
    {
        var user = await _db.SupplierPortalUsers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == portalUserId, cancellationToken);
        if (user is null || !user.CanSignIn || user.SecurityStamp != securityStamp) return null;
        if (!await SupplierAllowsLoginAsync(user.SupplierId, cancellationToken)) return null;
        return new PortalSession(user.Id, user.SupplierId, user.IsSupplierAdmin, user.Email);
    }

    /// <inheritdoc />
    public async Task<PortalMeDto> GetMeAsync(PortalSession session, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        var user = await _db.SupplierPortalUsers.AsNoTracking().FirstAsync(x => x.Id == session.UserId, cancellationToken);
        return await MeAsync(user, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PortalTokenResult> ChangePasswordAsync(
        PortalSession session, PortalChangePasswordRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(request);
        var user = await _db.SupplierPortalUsers.FirstAsync(x => x.Id == session.UserId, cancellationToken);
        if (user.PasswordHash is null
            || _hasher.VerifyHashedPassword(user, user.PasswordHash, request.CurrentPassword ?? string.Empty) == PasswordVerificationResult.Failed)
        {
            await LogAsync(AccessEvent.PasswordChangeFailed, user, "Portal NCC: mật khẩu hiện tại không đúng.", cancellationToken);
            throw new BusinessRuleException("Mật khẩu hiện tại không đúng.");
        }

        RequireStrongPassword(request.NewPassword);
        user.ChangePassword(_hasher.HashPassword(user, request.NewPassword));
        await _db.SaveChangesAsync(cancellationToken);
        await LogAsync(AccessEvent.PasswordChanged, user, "Portal NCC: đổi mật khẩu.", cancellationToken);
        // Dấu bảo mật đã đổi nên token cũ hết hiệu lực – trả token mới để người đang thao tác không bị đăng xuất.
        return await TokenResultAsync(user, cancellationToken);
    }

    // ───────────────────────── Hàm phụ ─────────────────────────

    private async Task<PortalInviteResult> IssueInviteAsync(
        SupplierPortalUser user, string supplierName, string fallbackBaseUrl, CancellationToken cancellationToken)
    {
        var token = NewLinkToken();
        var code = SupplierPortalCode.Issue(user.Id, SupplierPortalCodePurpose.Invite, Hash(token), _clock.UtcNow,
            TimeSpan.FromHours(_options.InviteValidHours));
        _db.SupplierPortalCodes.Add(code);
        await _db.SaveChangesAsync(cancellationToken);

        var url = BuildUrl(fallbackBaseUrl, "/ncc/kich-hoat", token);
        var sent = await TrySendAsync(user, "Mời tham gia Portal nhà cung cấp",
            $"{supplierName} được mời tham gia Portal nhà cung cấp. Bấm link dưới đây để đặt mật khẩu và kích hoạt tài khoản {user.Email}.",
            $"Link có hiệu lực đến {TimeZoneInfo.ConvertTime(code.ExpiresAt, VietnamTime.Zone):dd/MM/yyyy HH:mm}, dùng một lần.",
            url, cancellationToken);
        return new PortalInviteResult(ToDto(user), sent, url, code.ExpiresAt);
    }

    private async Task<(SupplierPortalCode Code, SupplierPortalUser User)> RequireLinkAsync(
        string? token, SupplierPortalCodePurpose purpose, CancellationToken cancellationToken)
    {
        var hash = Hash((token ?? string.Empty).Trim());
        var code = await _db.SupplierPortalCodes.FirstOrDefaultAsync(x => x.CodeHash == hash && x.Purpose == purpose, cancellationToken);
        if (code is null || !code.IsUsable(_clock.UtcNow, int.MaxValue))
            throw new BusinessRuleException(purpose == SupplierPortalCodePurpose.Invite
                ? "Link kích hoạt không đúng, đã dùng hoặc đã hết hạn. Đề nghị bên mời gửi lại lời mời."
                : "Link đặt lại mật khẩu không đúng, đã dùng hoặc đã hết hạn. Hãy yêu cầu lại.");
        var user = await _db.SupplierPortalUsers.FirstAsync(x => x.Id == code.PortalUserId, cancellationToken);
        return (code, user);
    }

    private async Task<Supplier> RequireSupplierAsync(Guid supplierId, CancellationToken cancellationToken) =>
        await _db.Suppliers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == supplierId, cancellationToken)
        ?? throw new NotFoundException("Không tìm thấy nhà cung cấp.");

    private async Task<SupplierPortalUser> RequireUserAsync(Guid supplierId, Guid portalUserId, CancellationToken cancellationToken) =>
        await _db.SupplierPortalUsers.FirstOrDefaultAsync(x => x.Id == portalUserId && x.SupplierId == supplierId, cancellationToken)
        ?? throw new NotFoundException("Không tìm thấy tài khoản Portal.");

    // NCC chỉ quản lý tài khoản của chính mình, và chỉ quản trị NCC được làm (design.md 19.5: NCC chỉ thấy dữ liệu của mình).
    private static void RequireAdminOf(PortalSession? session, Guid supplierId)
    {
        if (session is null) return;
        if (session.SupplierId != supplierId || !session.IsSupplierAdmin)
            throw new ForbiddenAccessException("Chỉ quản trị NCC được quản lý người dùng Portal của NCC mình.");
    }

    private async Task<bool> SupplierAllowsLoginAsync(Guid supplierId, CancellationToken cancellationToken) =>
        await _db.Suppliers.AsNoTracking().AnyAsync(x => x.Id == supplierId && x.Status != SupplierStatus.Blacklisted, cancellationToken);

    private async Task ExpireCodesAsync(Guid portalUserId, SupplierPortalCodePurpose purpose, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var open = await _db.SupplierPortalCodes
            .Where(x => x.PortalUserId == portalUserId && x.Purpose == purpose && x.UsedAt == null && x.ExpiresAt > now)
            .ToListAsync(cancellationToken);
        foreach (var c in open) c.MarkUsed(now);
    }

    private static void RequireStrongPassword(string? password)
    {
        if (SupplierPortalUser.ValidatePassword(password) is { } error) throw new BusinessRuleException(error);
    }

    private async Task<PortalTokenResult> TokenResultAsync(SupplierPortalUser user, CancellationToken cancellationToken)
    {
        var (token, expires) = _tokens.CreateToken(user);
        return new PortalTokenResult(token, expires, await MeAsync(user, cancellationToken));
    }

    private async Task<PortalMeDto> MeAsync(SupplierPortalUser user, CancellationToken cancellationToken)
    {
        var s = await _db.Suppliers.AsNoTracking().FirstAsync(x => x.Id == user.SupplierId, cancellationToken);
        return new PortalMeDto(user.Id, user.Email, user.FullName, user.Phone, user.Position, user.IsSupplierAdmin,
            new PortalSupplierDto(s.Id, s.Code, s.Name, s.TaxCode, s.Address, s.Phone, s.Email, s.Status.ToString()));
    }

    // Gửi ngay (không qua hàng đợi thông báo) vì OTP phải tới trong vài giây. Lỗi gửi không làm hỏng thao tác: trả false để người
    // mời tự chuyển link; riêng OTP thì báo lỗi (trừ dev/test).
    private async Task<bool> TrySendAsync(
        SupplierPortalUser user, string subject, string line1, string line2, string? url, CancellationToken cancellationToken)
    {
        if (!_emailOptions.CurrentValue.Enabled) return false;
        var html = new StringBuilder()
            .Append("<p>Kính gửi ").Append(WebUtility.HtmlEncode(user.FullName)).Append(",</p>")
            .Append("<p>").Append(WebUtility.HtmlEncode(line1)).Append("</p>");
        if (url is not null)
            html.Append("<p><a href=\"").Append(WebUtility.HtmlEncode(url)).Append("\">").Append(WebUtility.HtmlEncode(url)).Append("</a></p>");
        html.Append("<p>").Append(WebUtility.HtmlEncode(line2)).Append("</p>");
        var text = $"Kính gửi {user.FullName},\n\n{line1}\n{(url is null ? string.Empty : url + "\n")}\n{line2}\n";
        try
        {
            await _email.SendAsync(new EmailMessage(user.Email, user.FullName, subject, html.ToString(), text), cancellationToken);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Không gửi được email Portal \"{Subject}\" tới {Email}.", subject, user.Email);
            return false;
        }
    }

    private string BuildUrl(string fallbackBaseUrl, string path, string token)
    {
        var baseUrl = new[] { _options.BaseUrl, _emailOptions.CurrentValue.AppBaseUrl, fallbackBaseUrl }
            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? string.Empty;
        return $"{baseUrl.TrimEnd('/')}{path}?token={Uri.EscapeDataString(token)}";
    }

    private Task LogAsync(AccessEvent accessEvent, SupplierPortalUser user, string detail, CancellationToken cancellationToken) =>
        _accessLog.WriteAsync(accessEvent, user.Id, PortalClaims.UserNamePrefix + user.Email, null, detail, cancellationToken);

    /// <summary>Che email: "banhang@ncc.vn" → "ba*****@ncc.vn".</summary>
    private static string MaskEmail(string email)
    {
        var at = email.IndexOf('@');
        if (at <= 2) return email;
        return email[..2] + new string('*', at - 2) + email[at..];
    }

    // 32 byte ngẫu nhiên, base64url – đủ dài để không đoán được; chỉ băm được lưu.
    private static string NewLinkToken() => Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static bool FixedEquals(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(a), Encoding.ASCII.GetBytes(b));

    private static SupplierPortalUserDto ToDto(SupplierPortalUser u) => new(
        u.Id, u.SupplierId, u.Email, u.FullName, u.Phone, u.Position, u.IsSupplierAdmin, u.Status.ToString(),
        u.CreatedAt, u.ActivatedAt, u.LastLoginAt);
}
