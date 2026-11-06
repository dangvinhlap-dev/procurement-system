using System.Globalization;
using Proc.Application.Common;

namespace Proc.Api.Security;

/// <summary>
/// Đọc header <c>If-Match</c> của lệnh ghi (PUT/POST/PATCH/DELETE) có Id trên đường dẫn (<c>{id}</c>): phiên bản chứng từ người dùng đã
/// xem lúc mở. Tầng lưu (<c>VersionCheckInterceptor</c>) so với phiên bản hiện tại – khác thì trả 409, không ghi đè thay đổi của người
/// khác (lát R2, design.md mục 18.26). Không có header thì không so (client cũ, test API) – vẫn còn concurrency token chặn lưu đồng thời.
/// </summary>
public sealed class ExpectedVersionMiddleware
{
    private readonly RequestDelegate _next;

    /// <summary>Tạo middleware.</summary>
    /// <param name="next">Middleware kế tiếp.</param>
    public ExpectedVersionMiddleware(RequestDelegate next) => _next = next;

    /// <summary>Ghi nhận phiên bản mong đợi (nếu có) rồi chuyển request đi tiếp.</summary>
    /// <param name="context">HTTP context.</param>
    /// <param name="expected">Phiên bản mong đợi của request (scoped).</param>
    /// <returns>Task hoàn tất khi xử lý xong.</returns>
    public Task InvokeAsync(HttpContext context, IExpectedVersion expected)
    {
        if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method)
            && TryParse(context.Request.Headers.IfMatch.ToString(), out var version)
            && Guid.TryParse(context.GetRouteValue("id")?.ToString(), out var id))
            expected.Expect(id, version);
        return _next(context);
    }

    /// <summary>Đọc phiên bản từ If-Match dạng <c>"123"</c>, <c>W/"123"</c> hoặc <c>123</c>.</summary>
    private static bool TryParse(string header, out uint version)
    {
        var text = header.Trim();
        if (text.StartsWith("W/", StringComparison.Ordinal)) text = text[2..];
        return uint.TryParse(text.Trim('"'), NumberStyles.None, CultureInfo.InvariantCulture, out version);
    }
}
