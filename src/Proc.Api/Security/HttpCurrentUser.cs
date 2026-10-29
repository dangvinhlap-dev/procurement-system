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

    /// <inheritdoc />
    public string? UserName => _accessor.HttpContext?.User.FindFirst(JwtRegisteredClaimNames.UniqueName)?.Value;
}

/// <summary>
/// IP, trình duyệt, mã liên kết của request HTTP đang xử lý – ghi kèm nhật ký (design.md mục 9.3).
/// Mã liên kết là TraceIdentifier của ASP.NET Core, cũng có trong log kỹ thuật của Serilog.
/// </summary>
public sealed class HttpRequestContext : IRequestContext
{
    private readonly IHttpContextAccessor _accessor;

    /// <summary>
    /// Tạo đối tượng đọc thông tin request.
    /// </summary>
    /// <param name="accessor">Truy cập HTTP context hiện tại.</param>
    public HttpRequestContext(IHttpContextAccessor accessor) => _accessor = accessor;

    /// <inheritdoc />
    public string? IpAddress => _accessor.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? Local();

    /// <inheritdoc />
    public string? UserAgent => Truncate(_accessor.HttpContext?.Request.Headers.UserAgent.ToString(), 512);

    /// <inheritdoc />
    public string? CorrelationId => _accessor.HttpContext?.TraceIdentifier;

    // TestServer (test tích hợp) không có địa chỉ mạng thật.
    private string? Local() => _accessor.HttpContext is null ? null : "local";

    private static string? Truncate(string? value, int max) =>
        string.IsNullOrEmpty(value) ? null : value.Length <= max ? value : value[..max];
}
