using System.Globalization;
using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Proc.Application.Operations;
using Proc.Infrastructure.Portal;

namespace Proc.Api.Security;

/// <summary>
/// Giới hạn tần suất toàn bộ API (lát P1b cho Portal NCC – design.md 19.5 P1b-1; lát QT7 mở rộng cho phần nội bộ – design.md 21.16). Hai bộ chạy
/// nối tiếp:
/// <list type="number">
/// <item>Số lần gọi, cửa sổ cố định 1 phút: <c>/api/portal/auth/*</c> và API Portal khác theo IP (<see cref="PortalOptions.RateLimit"/>);
/// <c>POST /api/auth/login</c> theo IP (<see cref="AccessPolicySnapshot.LoginPerMinutePerIp"/> – theo tên đăng nhập đã có khóa tạm khi sai mật
/// khẩu); API nội bộ có token theo phiên (<see cref="AccessPolicySnapshot.ApiPerMinutePerUser"/>); <c>POST /api/ops/v1/client-errors</c> theo phiên
/// (lát QT2 – <see cref="OperationsOptions.ClientErrorPermitPerMinute"/>).</item>
/// <item>Việc nặng chạy cùng lúc theo phiên (<see cref="AccessPolicySnapshot.HeavyConcurrentPerUser"/>): nhập / xuất Excel, báo cáo, xuất cho kế
/// toán, in PDF – không xếp hàng, vượt thì 429 ngay.</item>
/// </list>
/// Phần nội bộ không giới hạn request từ chính máy chủ (loopback – Vite dev proxy, bộ thực thi, test tự động) hoặc không rõ IP (test trong bộ nhớ):
/// đó không phải người dò mật khẩu từ mạng. IP lấy từ kết nối: IIS in-process đưa sẵn IP thật; sau reverse proxy cần khai
/// <c>Security:KnownProxies</c> để đọc <c>X-Forwarded-For</c>, nếu không mọi người bị đếm chung theo IP của proxy.
/// </summary>
public static class ApiRateLimiting
{
    /// <summary>Đường dẫn API gửi lỗi giao diện.</summary>
    public const string ClientErrorsPath = "/api/ops/v1/client-errors";

    /// <summary>Đường dẫn đăng nhập nội bộ.</summary>
    public const string LoginPath = "/api/auth/login";

    /// <summary>Đăng ký bộ giới hạn (đọc cấu hình mỗi lần tạo cửa sổ mới).</summary>
    /// <param name="services">Danh sách dịch vụ.</param>
    /// <returns>Chính <paramref name="services"/>.</returns>
    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services) =>
        services.AddRateLimiter(o =>
        {
            o.GlobalLimiter = PartitionedRateLimiter.CreateChained(
                PartitionedRateLimiter.Create<HttpContext, string>(RatePartition),
                PartitionedRateLimiter.Create<HttpContext, string>(HeavyPartition));
            o.OnRejected = OnRejectedAsync;
        });

    /// <summary>
    /// Việc nặng: Excel (nhập, xuất, lưới), báo cáo, xuất cho kế toán, in phiếu PDF. Các việc này giữ CSDL / CPU lâu – một người bấm liên tục
    /// không được chiếm hết máy chủ.
    /// </summary>
    /// <param name="path">Đường dẫn request.</param>
    /// <returns>true khi là việc nặng.</returns>
    public static bool IsHeavy(PathString path) =>
        path.StartsWithSegments("/api/excel")
        || path.StartsWithSegments("/api/reports")
        || path.StartsWithSegments("/api/purchase-accounting-export")
        || (path.StartsWithSegments("/api") && path.Value!.EndsWith("/pdf", StringComparison.OrdinalIgnoreCase));

    /// <summary>Chọn nhóm đếm số lần gọi theo đường dẫn + IP / phiên.</summary>
    private static RateLimitPartition<string> RatePartition(HttpContext context)
    {
        var path = context.Request.Path;
        if (path.StartsWithSegments(ClientErrorsPath))
        {
            var permit = context.RequestServices.GetRequiredService<IOptionsMonitor<OperationsOptions>>().CurrentValue.ClientErrorPermitPerMinute;
            return Window("client-errors", permit, SessionKey(context));
        }

        if (path.StartsWithSegments("/api/portal"))
        {
            var limits = context.RequestServices.GetRequiredService<IOptionsMonitor<PortalOptions>>().CurrentValue.RateLimit;
            var isAuth = path.StartsWithSegments("/api/portal/auth");
            return Window(isAuth ? "portal-auth" : "portal-api", isAuth ? limits.AuthPermitPerMinute : limits.ApiPermitPerMinute, IpKey(context));
        }

        if (!path.StartsWithSegments("/api") || IsServerSelf(context)) return RateLimitPartition.GetNoLimiter(string.Empty);

        var policy = context.RequestServices.GetRequiredService<IAccessPolicyProvider>().Current;
        if (path.StartsWithSegments(LoginPath) && HttpMethods.IsPost(context.Request.Method))
            return Window("login", policy.LoginPerMinutePerIp, IpKey(context));

        // Chưa có token: endpoint ẩn danh (môi trường, bộ thực thi dùng khóa riêng) hoặc sẽ nhận 401 – không đếm theo phiên.
        return HasToken(context)
            ? Window("api", policy.ApiPerMinutePerUser, SessionKey(context))
            : RateLimitPartition.GetNoLimiter(string.Empty);
    }

    /// <summary>Nhóm đếm việc nặng chạy cùng lúc theo phiên; việc khác không giới hạn.</summary>
    private static RateLimitPartition<string> HeavyPartition(HttpContext context)
    {
        if (!IsHeavy(context.Request.Path) || IsServerSelf(context) || !HasToken(context)) return RateLimitPartition.GetNoLimiter(string.Empty);
        var permit = Math.Max(1, context.RequestServices.GetRequiredService<IAccessPolicyProvider>().Current.HeavyConcurrentPerUser);
        // Khóa nhóm gồm cả số việc cho phép: đổi cấu hình thì nhóm mới dùng ngay giá trị mới.
        return RateLimitPartition.GetConcurrencyLimiter(
            $"heavy|{permit}|{SessionKey(context)}",
            _ => new ConcurrencyLimiterOptions { PermitLimit = permit, QueueLimit = 0 });
    }

    /// <summary>Cửa sổ cố định 1 phút; khóa nhóm gồm cả số lượt cho phép để đổi cấu hình thì nhóm mới dùng ngay giá trị mới.</summary>
    private static RateLimitPartition<string> Window(string kind, int permit, string who) =>
        RateLimitPartition.GetFixedWindowLimiter(
            $"{kind}|{permit}|{who}",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = Math.Max(1, permit),
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            });

    private static bool HasToken(HttpContext context) => !string.IsNullOrEmpty(context.Request.Headers.Authorization.ToString());

    /// <summary>Request từ chính máy chủ (loopback) hoặc không rõ IP (test chạy trong bộ nhớ).</summary>
    private static bool IsServerSelf(HttpContext context) =>
        context.Connection.RemoteIpAddress is not { } ip || IPAddress.IsLoopback(ip);

    private static string IpKey(HttpContext context) => context.Connection.RemoteIpAddress?.ToString() ?? "khong-ro";

    /// <summary>
    /// Khóa theo phiên đăng nhập: băm chuỗi Authorization (bộ giới hạn chạy trước bước xác thực nên chưa có Id người dùng; mỗi phiên là một người),
    /// không có thì theo IP. Token giả không qua được xác thực nên không lách giới hạn để làm việc gì được.
    /// </summary>
    private static string SessionKey(HttpContext context)
    {
        var who = context.Request.Headers.Authorization.ToString();
        if (string.IsNullOrEmpty(who)) return IpKey(context);
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(who)));
    }

    /// <summary>Trả 429 + Retry-After + ProblemDetails tiếng Việt, ghi log cảnh báo (IP, đường dẫn) để theo dõi dò mật khẩu.</summary>
    private static async ValueTask OnRejectedAsync(OnRejectedContext rejected, CancellationToken cancellationToken)
    {
        var context = rejected.HttpContext;
        var hasRetryAfter = rejected.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter);
        var seconds = hasRetryAfter ? Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds)) : 60;
        context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(ApiRateLimiting))
            .LogWarning("Vượt giới hạn tần suất từ IP {Ip} vào {Path}", context.Connection.RemoteIpAddress, context.Request.Path.Value);

        // Bộ đếm việc cùng lúc không có Retry-After: báo chờ việc trước xong, gợi ý thử lại sau vài giây.
        var heavy = !hasRetryAfter && IsHeavy(context.Request.Path);
        if (heavy) seconds = 5;
        var title = heavy
            ? "Bạn đang chạy nhiều việc nặng cùng lúc (Excel, báo cáo, in phiếu). Chờ việc trước xong rồi thử lại."
            : context.Request.Path.StartsWithSegments(LoginPath)
                ? $"Đăng nhập quá nhiều lần từ máy của bạn. Vui lòng thử lại sau {seconds} giây."
                : $"Quá nhiều yêu cầu từ địa chỉ của bạn. Vui lòng thử lại sau {seconds} giây.";

        context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
        await context.Response.WriteAsJsonAsync(
            new ProblemDetails { Status = StatusCodes.Status429TooManyRequests, Title = title },
            options: null,
            contentType: "application/problem+json",
            cancellationToken);
    }
}
