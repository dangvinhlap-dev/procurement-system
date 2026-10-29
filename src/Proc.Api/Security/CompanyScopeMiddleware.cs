using Microsoft.IdentityModel.JsonWebTokens;
using Proc.Infrastructure.Security;

namespace Proc.Api.Security;

/// <summary>
/// Đầu mỗi request đã đăng nhập: đọc công ty đang làm việc từ header X-Company-Id và nạp phạm vi người dùng
/// (design.md mục 9.2). Header chỉ tới công ty người dùng không được gán thì từ chối ngay (403),
/// không để request đi tiếp với phạm vi sai.
/// </summary>
public sealed class CompanyScopeMiddleware
{
    /// <summary>Tên header mang công ty đang làm việc.</summary>
    public const string CompanyHeader = "X-Company-Id";

    private readonly RequestDelegate _next;

    /// <summary>
    /// Tạo middleware.
    /// </summary>
    /// <param name="next">Middleware kế tiếp.</param>
    public CompanyScopeMiddleware(RequestDelegate next) => _next = next;

    /// <summary>
    /// Nạp phạm vi rồi chuyển request đi tiếp, hoặc trả 400/401/403.
    /// </summary>
    /// <param name="context">HTTP context.</param>
    /// <param name="loader">Bộ nạp phạm vi (scoped theo request).</param>
    /// <returns>Task hoàn tất khi xử lý xong.</returns>
    public async Task InvokeAsync(HttpContext context, UserScopeLoader loader)
    {
        if (!Guid.TryParse(context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var userId))
        {
            await _next(context);
            return;
        }

        Guid? companyId = null;
        var header = context.Request.Headers[CompanyHeader].ToString();
        if (!string.IsNullOrWhiteSpace(header))
        {
            if (!Guid.TryParse(header, out var parsed))
            {
                await WriteProblemAsync(context, StatusCodes.Status400BadRequest, $"Header {CompanyHeader} không hợp lệ.");
                return;
            }

            companyId = parsed;
        }

        switch (await loader.LoadAsync(userId, companyId, context.RequestAborted))
        {
            case ScopeLoadResult.UserInactive:
                await WriteProblemAsync(context, StatusCodes.Status401Unauthorized, "Phiên đăng nhập không còn hiệu lực. Hãy đăng nhập lại.");
                return;
            case ScopeLoadResult.CompanyNotAllowed:
                // Mỗi công ty là pháp nhân độc lập: không được gán thì không được làm việc – CLAUDE.md mục 4.
                await WriteProblemAsync(context, StatusCodes.Status403Forbidden, "Bạn không được làm việc tại công ty này.");
                return;
            default:
                await _next(context);
                return;
        }
    }

    private static Task WriteProblemAsync(HttpContext context, int status, string title) =>
        Results.Problem(title: title, statusCode: status).ExecuteAsync(context);
}
