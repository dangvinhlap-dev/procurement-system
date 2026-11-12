using Microsoft.Extensions.Options;
using Proc.Infrastructure.Portal;

namespace Proc.Api.Security;

/// <summary>
/// Tách tên miền Portal NCC khỏi phần nội bộ (lát P1b, design.md 19.5 P1b-2, Q14): Portal có tên miền riêng mở ra internet, phần nội bộ chỉ
/// dùng trong LAN/VPN. Request tới tên miền Portal (<see cref="PortalOptions.Hosts"/>) chỉ được vào trang <c>/ncc</c>, file tĩnh giao diện và
/// API <c>/api/portal</c>; API nội bộ và <c>/health</c> trả 404 (như không tồn tại – không lộ có hệ thống nội bộ), đường dẫn khác chuyển về
/// <c>/ncc</c>. Chưa cấu hình tên miền thì không làm gì (máy dev, chạy thử LAN).
/// Đặt trước file tĩnh và xác thực để chặn sớm, kể cả với request chưa đăng nhập.
/// </summary>
public sealed class PortalHostMiddleware
{
    private const string PortalPage = "/ncc";

    private readonly RequestDelegate _next;
    private readonly IOptionsMonitor<PortalOptions> _options;
    private readonly IWebHostEnvironment _environment;

    /// <summary>Tạo middleware.</summary>
    /// <param name="next">Middleware kế tiếp.</param>
    /// <param name="options">Cấu hình Portal (đọc lại khi appsettings đổi).</param>
    /// <param name="environment">Môi trường – để biết file tĩnh nào có trong wwwroot.</param>
    public PortalHostMiddleware(RequestDelegate next, IOptionsMonitor<PortalOptions> options, IWebHostEnvironment environment)
    {
        _next = next;
        _options = options;
        _environment = environment;
    }

    /// <summary>Xử lý request: cho qua, trả 404 hoặc chuyển về trang Portal.</summary>
    /// <param name="context">Request hiện tại.</param>
    /// <returns>Tác vụ xử lý.</returns>
    public Task InvokeAsync(HttpContext context)
    {
        var hosts = _options.CurrentValue.HostList();
        if (hosts.Count == 0 || !hosts.Contains(context.Request.Host.Host, StringComparer.OrdinalIgnoreCase))
            return _next(context);

        var path = context.Request.Path;
        if (path.StartsWithSegments("/api/portal") || path.StartsWithSegments(PortalPage)) return _next(context);

        if (path.StartsWithSegments("/api") || path.StartsWithSegments("/health") || path.StartsWithSegments("/openapi"))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return Task.CompletedTask;
        }

        // File tĩnh của giao diện (js, css, ảnh) dùng chung cho trang nội bộ và Portal – không chứa dữ liệu.
        if (path.HasValue && path.Value != "/" && _environment.WebRootFileProvider.GetFileInfo(path.Value).Exists) return _next(context);

        context.Response.Redirect(PortalPage);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Thêm tiêu đề bảo mật cho mọi phản hồi (lát P1b): trình duyệt không đoán loại nội dung (<c>nosniff</c> – file đính kèm không bị chạy như
/// HTML/script), không cho trang ngoài nhúng ứng dụng vào khung (chống bấm lừa), không gửi đường dẫn đầy đủ (có thể chứa mã mời) sang trang
/// khác. <c>SAMEORIGIN</c> thay vì <c>DENY</c> để vẫn nhúng được trang / file của chính ứng dụng nếu cần.
/// </summary>
public sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    /// <summary>Tạo middleware.</summary>
    /// <param name="next">Middleware kế tiếp.</param>
    public SecurityHeadersMiddleware(RequestDelegate next) => _next = next;

    /// <summary>Gắn tiêu đề trước khi phản hồi bắt đầu gửi.</summary>
    /// <param name="context">Request hiện tại.</param>
    /// <returns>Tác vụ xử lý.</returns>
    public Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = "SAMEORIGIN";
            headers["Referrer-Policy"] = "same-origin";
            return Task.CompletedTask;
        });
        return _next(context);
    }
}
