using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Proc.Application.Common;

namespace Proc.Infrastructure.Identity;

/// <summary>
/// Cấu hình JWT, đọc từ mục "Jwt" của appsettings. Khóa ký trên server thật đặt qua biến môi trường
/// Jwt__SigningKey, không ghi vào file trong repo.
/// </summary>
public sealed class JwtOptions
{
    /// <summary>Độ dài tối thiểu của khóa ký HMAC-SHA256 (256 bit).</summary>
    public const int MinSigningKeyLength = 32;

    /// <summary>Đơn vị phát hành token.</summary>
    public string Issuer { get; set; } = "procurement-system";

    /// <summary>Đối tượng nhận token.</summary>
    public string Audience { get; set; } = "procurement-web";

    /// <summary>Khóa bí mật ký token, tối thiểu 32 ký tự.</summary>
    public string SigningKey { get; set; } = string.Empty;

    /// <summary>Khóa ký dạng đối tượng của thư viện IdentityModel.</summary>
    public SymmetricSecurityKey CreateSecurityKey() => new(Encoding.UTF8.GetBytes(SigningKey));
}

/// <summary>
/// Cấp JWT cho người dùng đã đăng nhập.
/// </summary>
public sealed class JwtTokenService
{
    private readonly JwtOptions _options;
    private readonly IClock _clock;

    /// <summary>
    /// Tạo dịch vụ cấp token.
    /// </summary>
    /// <param name="options">Cấu hình JWT.</param>
    /// <param name="clock">Đồng hồ hệ thống.</param>
    public JwtTokenService(IOptions<JwtOptions> options, IClock clock)
    {
        _options = options.Value;
        _clock = clock;
    }

    /// <summary>Tên claim mang mã bảo mật (SecurityStamp) của tài khoản lúc cấp token (lát QT7).</summary>
    public const string SecurityStampClaim = "sstamp";

    /// <summary>
    /// Tạo token cho người dùng. Token chỉ chứa định danh; công ty và vai trò không đưa vào token
    /// để khi quản trị đổi quyền thì có hiệu lực ngay, không phải chờ token hết hạn.
    /// Lát QT7: kèm mã bảo mật của tài khoản – đổi / đặt lại mật khẩu thì mã đổi, token cũ bị từ chối ở <c>UserScopeLoader</c>.
    /// </summary>
    /// <param name="user">Tài khoản.</param>
    /// <param name="lifetimeMinutes">Hạn phiên (phút) – từ cấu hình truy cập CFG.</param>
    /// <returns>Chuỗi token và thời điểm hết hạn (UTC).</returns>
    public (string Token, DateTimeOffset ExpiresAt) CreateToken(AppUser user, int lifetimeMinutes)
    {
        ArgumentNullException.ThrowIfNull(user);
        var now = _clock.UtcNow;
        var expires = now.AddMinutes(lifetimeMinutes);
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expires.UtcDateTime,
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.UniqueName, user.UserName ?? string.Empty),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(SecurityStampClaim, user.SecurityStamp ?? string.Empty),
            ]),
            SigningCredentials = new SigningCredentials(_options.CreateSecurityKey(), SecurityAlgorithms.HmacSha256),
        };
        return (new JsonWebTokenHandler().CreateToken(descriptor), expires);
    }
}
