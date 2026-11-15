using Proc.Application.Common;
using Proc.Application.Operations;

namespace Proc.Api.Security;

/// <summary>
/// Chế độ bảo trì (lát QT5, ke-hoach-qt.md QT5): khi bật, mọi request <c>/api</c> trả <b>503</b> kèm câu thông báo (thuộc tính <c>code</c> =
/// <c>maintenance</c> để giao diện nhận ra), trừ:
/// <list type="bullet">
/// <item>người đã đăng nhập có ít nhất một quyền <c>System.Ops*</c> tại công ty đang làm việc – để quản trị viên cài đặt, kiểm tra, tắt bảo trì;</item>
/// <item>đăng nhập / đổi công ty / thông tin người dùng (<c>/api/auth</c>) – quản trị viên phải đăng nhập được;</item>
/// <item>API môi trường ẩn danh (dải đỏ "ĐANG BẢO TRÌ" hiện cả ở trang đăng nhập, Portal) và API bộ thực thi (bộ thực thi làm việc chính lúc bảo trì).</item>
/// </list>
/// File giao diện, <c>/health</c> không bị chặn (giao diện phải tải được để báo bảo trì; giám sát vẫn đọc được tình trạng).
/// </summary>
public sealed class MaintenanceModeMiddleware
{
    /// <summary>Mã lỗi trong ProblemDetails để giao diện nhận ra 503 do bảo trì.</summary>
    public const string ProblemCode = "maintenance";

    /// <summary>Tiền tố mã quyền quản trị vận hành – có một quyền là được vào khi bảo trì.</summary>
    public const string OpsPermissionPrefix = "System.Ops";

    private static readonly PathString[] Exempt =
    [
        "/api/auth",
        "/api/ops/v1/environment",
        "/api/portal/auth/environment",
        "/api/ops/v1/agent",
    ];

    private readonly RequestDelegate _next;

    /// <summary>Tạo middleware.</summary>
    /// <param name="next">Middleware kế tiếp.</param>
    public MaintenanceModeMiddleware(RequestDelegate next) => _next = next;

    /// <summary>Cho qua hoặc trả 503.</summary>
    /// <param name="context">HTTP context.</param>
    /// <param name="mode">Chế độ bảo trì (đệm vài giây).</param>
    /// <param name="scope">Phạm vi người dùng đã nạp ở <see cref="CompanyScopeMiddleware"/>.</param>
    /// <returns>Task.</returns>
    public async Task InvokeAsync(HttpContext context, IMaintenanceMode mode, ICurrentUserScope scope)
    {
        var path = context.Request.Path;
        if (!path.StartsWithSegments("/api") || Exempt.Any(e => path.StartsWithSegments(e, StringComparison.OrdinalIgnoreCase)))
        {
            await _next(context);
            return;
        }

        var state = await mode.GetAsync(context.RequestAborted);
        if (!state.IsOn || scope.Permissions.Any(p => p.StartsWith(OpsPermissionPrefix, StringComparison.Ordinal)))
        {
            await _next(context);
            return;
        }

        context.Response.Headers.RetryAfter = "60";
        await Results.Problem(
                title: state.Message,
                statusCode: StatusCodes.Status503ServiceUnavailable,
                extensions: new Dictionary<string, object?> { ["code"] = ProblemCode, ["startedAt"] = state.StartedAt })
            .ExecuteAsync(context);
    }
}
