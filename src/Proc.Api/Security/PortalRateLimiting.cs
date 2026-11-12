using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Proc.Infrastructure.Portal;

namespace Proc.Api.Security;

/// <summary>
/// Giới hạn tần suất API Portal NCC theo địa chỉ IP (lát P1b, design.md 19.5 P1b-1). Cửa sổ cố định 1 phút, đếm riêng:
/// <c>/api/portal/auth/*</c> (đăng nhập, OTP, kích hoạt, quên mật khẩu – chống dò mật khẩu / OTP, gửi tràn email) và các API Portal khác.
/// API nội bộ (chỉ trong LAN/VPN) không giới hạn.
/// IP lấy từ kết nối: IIS in-process đưa sẵn IP thật; sau reverse proxy (Docker + Caddy) cần bật forwarded headers
/// (<c>ASPNETCORE_FORWARDEDHEADERS_ENABLED=true</c> – docker-compose đã đặt) để không bị đếm chung theo IP của proxy.
/// </summary>
public static class PortalRateLimiting
{
    /// <summary>Đăng ký bộ giới hạn (đọc <see cref="PortalOptions.RateLimit"/> mỗi lần tạo cửa sổ mới).</summary>
    /// <param name="services">Danh sách dịch vụ.</param>
    /// <returns>Chính <paramref name="services"/>.</returns>
    public static IServiceCollection AddPortalRateLimiting(this IServiceCollection services) =>
        services.AddRateLimiter(o =>
        {
            o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(Partition);
            o.OnRejected = OnRejectedAsync;
        });

    /// <summary>Chọn nhóm đếm theo đường dẫn + IP; request ngoài Portal không giới hạn.</summary>
    private static RateLimitPartition<string> Partition(HttpContext context)
    {
        var path = context.Request.Path;
        if (!path.StartsWithSegments("/api/portal")) return RateLimitPartition.GetNoLimiter(string.Empty);

        var limits = context.RequestServices.GetRequiredService<IOptionsMonitor<PortalOptions>>().CurrentValue.RateLimit;
        var isAuth = path.StartsWithSegments("/api/portal/auth");
        var permit = isAuth ? limits.AuthPermitPerMinute : limits.ApiPermitPerMinute;
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "khong-ro";
        // Khóa nhóm gồm cả số lượt cho phép: đổi cấu hình thì nhóm mới dùng ngay giá trị mới.
        return RateLimitPartition.GetFixedWindowLimiter(
            $"{(isAuth ? "auth" : "api")}|{permit}|{ip}",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = Math.Max(1, permit),
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            });
    }

    /// <summary>Trả 429 + Retry-After + ProblemDetails tiếng Việt, ghi log cảnh báo (IP, đường dẫn) để theo dõi dò mật khẩu.</summary>
    private static async ValueTask OnRejectedAsync(OnRejectedContext rejected, CancellationToken cancellationToken)
    {
        var context = rejected.HttpContext;
        var seconds = rejected.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
            ? Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds))
            : 60;
        context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(PortalRateLimiting))
            .LogWarning("Portal: vượt giới hạn tần suất từ IP {Ip} vào {Path}", context.Connection.RemoteIpAddress, context.Request.Path.Value);

        context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
        await context.Response.WriteAsJsonAsync(
            new ProblemDetails
            {
                Status = StatusCodes.Status429TooManyRequests,
                Title = $"Quá nhiều yêu cầu từ địa chỉ của bạn. Vui lòng thử lại sau {seconds} giây.",
            },
            options: null,
            contentType: "application/problem+json",
            cancellationToken);
    }
}
