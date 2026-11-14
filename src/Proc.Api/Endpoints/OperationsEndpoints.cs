using Proc.Api.Security;
using Proc.Application.Operations;
using Proc.Domain.Organization;

namespace Proc.Api.Endpoints;

/// <summary>
/// API module Quản trị vận hành (design.md mục 21, lát QT1) dưới <c>/api/ops/v1</c>. Chỉ <b>thêm</b> trường, không sửa / bỏ (máy TEST có thể chạy
/// bản mới hơn PROD – mục 21.8 T3). Mọi API cần quyền <c>System.Ops.*</c> và chỉ dùng ở tổng công ty (N3); riêng <c>environment</c> ẩn danh để
/// trang đăng nhập vẽ dải màu môi trường.
/// </summary>
public static class OperationsEndpoints
{
    /// <summary>Khai báo các endpoint.</summary>
    /// <param name="app">Bộ định tuyến endpoint.</param>
    /// <returns>Chính <paramref name="app"/>.</returns>
    public static IEndpointRouteBuilder MapOperationsEndpoints(this IEndpointRouteBuilder app)
    {
        var ops = app.MapGroup("/api/ops/v1").WithTags("Quản trị vận hành");

        // Ẩn danh: tên môi trường (Dev / Test / Prod / None) và cảnh báo cấu hình để dải màu hiện cả ở trang đăng nhập – không phiên bản, không
        // bí mật.
        ops.MapGet("/environment", (IEnvironmentStatus status) => status.Get())
            .AllowAnonymous();
        // Site Portal NCC chỉ mở /api/portal (PortalHostMiddleware – N3: không có chức năng QT trên Portal): cùng nội dung để dải TEST hiện cả
        // trên Portal của máy TEST; nằm dưới /api/portal/auth vì API Portal ẩn danh chỉ được ở nhóm đó (PortalInfrastructureTests).
        app.MapGet("/api/portal/auth/environment", (IEnvironmentStatus status) => status.Get())
            .WithTags("Portal NCC")
            .AllowAnonymous();

        ops.MapGet("/overview", (ISystemOverview s, CancellationToken ct) => s.GetAsync(ct))
            .RequirePermission(Permissions.System.OpsView)
            .RequireHeadOffice();

        ops.MapGet("/operation-logs", (DateOnly from, DateOnly to, IOperationLogQueries s, CancellationToken ct) => s.SearchAsync(from, to, ct))
            .RequirePermission(Permissions.System.OpsView)
            .RequireHeadOffice();

        return app;
    }
}
