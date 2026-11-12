using System.Security.Claims;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Proc.Application.Common;
using Proc.Domain.MasterData;
using Proc.Infrastructure.Identity;

namespace Proc.Infrastructure.Portal;

/// <summary>
/// Cấu hình Portal NCC (mục "Portal" trong appsettings; design.md 19.5, Q14). Mọi giá trị có mặc định cho máy dev;
/// server thật đặt <see cref="BaseUrl"/> là tên miền Portal mở ra internet.
/// </summary>
public sealed class PortalOptions
{
    /// <summary>Tên mục cấu hình.</summary>
    public const string Section = "Portal";

    /// <summary>
    /// Địa chỉ gốc giao diện Portal ghi vào link trong email (ví dụ https://ncc.tapdoan.vn). Trống thì lấy Email:AppBaseUrl, vẫn trống thì
    /// lấy địa chỉ trang người mời đang mở (đủ cho chạy thử trong mạng LAN – Q14).
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>Audience của token Portal – khác audience token nội bộ nên token NCC không gọi được API nội bộ.</summary>
    public string Audience { get; set; } = "procurement-portal";

    /// <summary>Thời hạn phiên đăng nhập Portal (phút).</summary>
    public int TokenLifetimeMinutes { get; set; } = 120;

    /// <summary>Thời hạn link mời (giờ).</summary>
    public int InviteValidHours { get; set; } = 168;

    /// <summary>Thời hạn link đặt lại mật khẩu (phút).</summary>
    public int PasswordResetValidMinutes { get; set; } = 60;

    /// <summary>Thời hạn OTP đăng nhập (phút).</summary>
    public int OtpValidMinutes { get; set; } = 10;

    /// <summary>Số lần nhập sai OTP tối đa của một mã.</summary>
    public int OtpMaxAttempts { get; set; } = 5;

    /// <summary>Số lần sai mật khẩu liên tiếp trước khi khóa tạm.</summary>
    public int MaxFailedLogins { get; set; } = 5;

    /// <summary>Thời gian khóa tạm (phút).</summary>
    public int LockoutMinutes { get; set; } = 15;

    /// <summary>
    /// Trả OTP / mã đặt lại trong response để test tự động và máy dev chạy được khi không có máy chủ email. Program.cs ép về false ngoài
    /// môi trường Development/Testing – server thật không bao giờ lộ mã.
    /// </summary>
    public bool ExposeCodesForTesting { get; set; }
}

/// <summary>Tên scheme xác thực và tên claim của token Portal.</summary>
public static class PortalClaims
{
    /// <summary>Scheme JWT của Portal (riêng với scheme mặc định của người dùng nội bộ).</summary>
    public const string Scheme = "Portal";

    /// <summary>Claim NCC.</summary>
    public const string SupplierId = "supplier_id";

    /// <summary>Claim dấu bảo mật – lệch với CSDL thì token mất hiệu lực (đổi mật khẩu, bị khóa).</summary>
    public const string SecurityStamp = "sstamp";

    /// <summary>Tiền tố tên người dùng Portal ghi vào nhật ký (phân biệt với tên đăng nhập nội bộ).</summary>
    public const string UserNamePrefix = "ncc:";
}

/// <summary>Cấp JWT cho người dùng Portal: cùng khóa ký với token nội bộ nhưng audience riêng (<see cref="PortalOptions.Audience"/>).</summary>
public sealed class PortalTokenService
{
    private readonly JwtOptions _jwt;
    private readonly PortalOptions _portal;
    private readonly IClock _clock;

    /// <summary>Tạo dịch vụ.</summary>
    /// <param name="jwt">Cấu hình JWT (khóa ký, issuer).</param>
    /// <param name="portal">Cấu hình Portal (audience, thời hạn).</param>
    /// <param name="clock">Đồng hồ.</param>
    public PortalTokenService(IOptions<JwtOptions> jwt, IOptions<PortalOptions> portal, IClock clock)
    {
        _jwt = jwt.Value;
        _portal = portal.Value;
        _clock = clock;
    }

    /// <summary>
    /// Cấp token cho tài khoản Portal.
    /// </summary>
    /// <param name="user">Tài khoản đã qua OTP.</param>
    /// <returns>Token và thời điểm hết hạn.</returns>
    public (string Token, DateTimeOffset ExpiresAt) CreateToken(SupplierPortalUser user)
    {
        var now = _clock.UtcNow;
        var expires = now.AddMinutes(_portal.TokenLifetimeMinutes);
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _jwt.Issuer,
            Audience = _portal.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expires.UtcDateTime,
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.UniqueName, PortalClaims.UserNamePrefix + user.Email),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(PortalClaims.SupplierId, user.SupplierId.ToString()),
                new Claim(PortalClaims.SecurityStamp, user.SecurityStamp),
            ]),
            SigningCredentials = new SigningCredentials(_jwt.CreateSecurityKey(), SecurityAlgorithms.HmacSha256),
        };
        return (new JsonWebTokenHandler().CreateToken(descriptor), expires);
    }
}
