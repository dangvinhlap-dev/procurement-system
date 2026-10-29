using Microsoft.IdentityModel.JsonWebTokens;
using Proc.Application.Common;

namespace Proc.Api.Security;

/// <summary>
/// Người dùng hiện tại lấy từ claim "sub" của JWT trong request đang xử lý.
/// </summary>
public sealed class HttpCurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _accessor;

    /// <summary>
    /// Tạo đối tượng đọc người dùng từ HTTP context.
    /// </summary>
    /// <param name="accessor">Truy cập HTTP context hiện tại.</param>
    public HttpCurrentUser(IHttpContextAccessor accessor) => _accessor = accessor;

    /// <inheritdoc />
    public Guid? UserId =>
        Guid.TryParse(_accessor.HttpContext?.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var id) ? id : null;
}
