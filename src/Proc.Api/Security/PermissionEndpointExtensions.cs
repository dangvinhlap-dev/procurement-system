using Microsoft.Extensions.Options;
using Proc.Application.Common;

namespace Proc.Api.Security;

/// <summary>
/// Gắn yêu cầu quyền chức năng cho endpoint (design.md mục 9.1): phải chọn công ty làm việc và có quyền tại công ty đó.
/// </summary>
public static class PermissionEndpointExtensions
{
    /// <summary>
    /// Bắt buộc quyền cho endpoint hoặc nhóm endpoint.
    /// </summary>
    /// <typeparam name="TBuilder">RouteHandlerBuilder hoặc RouteGroupBuilder.</typeparam>
    /// <param name="builder">Endpoint cần bảo vệ.</param>
    /// <param name="permission">Mã quyền, ví dụ Organization.Warehouse.View.</param>
    /// <returns>Chính builder để gọi nối tiếp.</returns>
    public static TBuilder RequirePermission<TBuilder>(this TBuilder builder, string permission)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.AddEndpointFilter(async (context, next) =>
        {
            var scope = context.HttpContext.RequestServices.GetRequiredService<ICurrentUserScope>();
            if (scope.CompanyId is null)
            {
                return Results.Problem(
                    title: $"Chưa chọn công ty làm việc (header {CompanyScopeMiddleware.CompanyHeader}).",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            if (!scope.HasPermission(permission))
            {
                if (ModuleDisabled(context, permission)) return ModuleDisabledProblem();
                return Results.Problem(
                    title: $"Bạn không có quyền thực hiện chức năng này ({permission}).",
                    statusCode: StatusCodes.Status403Forbidden);
            }

            return await next(context);
        });
        return builder;
    }

    /// <summary>
    /// Bắt buộc có ít nhất một trong các quyền – dùng khi hai vai trò cùng cần đọc một dữ liệu (ví dụ thủ kho đọc đơn mua hàng để
    /// lập phiếu nhập theo PO dù không có quyền xem danh sách đơn).
    /// </summary>
    /// <typeparam name="TBuilder">RouteHandlerBuilder hoặc RouteGroupBuilder.</typeparam>
    /// <param name="builder">Endpoint cần bảo vệ.</param>
    /// <param name="permissions">Các mã quyền, có một là đủ.</param>
    /// <returns>Chính builder để gọi nối tiếp.</returns>
    public static TBuilder RequireAnyPermission<TBuilder>(this TBuilder builder, params string[] permissions)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.AddEndpointFilter(async (context, next) =>
        {
            var scope = context.HttpContext.RequestServices.GetRequiredService<ICurrentUserScope>();
            if (scope.CompanyId is null)
            {
                return Results.Problem(
                    title: $"Chưa chọn công ty làm việc (header {CompanyScopeMiddleware.CompanyHeader}).",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            if (!permissions.Any(scope.HasPermission))
            {
                if (permissions.All(p => ModuleDisabled(context, p))) return ModuleDisabledProblem();
                return Results.Problem(
                    title: $"Bạn không có quyền thực hiện chức năng này ({string.Join(" hoặc ", permissions)}).",
                    statusCode: StatusCodes.Status403Forbidden);
            }

            return await next(context);
        });
        return builder;
    }

    /// <summary>Thông báo khi gọi API của phân hệ kho đã tắt từ 0.22.0 – design.md mục 19.4.</summary>
    public const string InventoryDisabledMessage =
        "Phân hệ kho đã ngừng dùng từ bản 0.22.0 (công ty dùng phần mềm kho riêng) – nhận hàng bằng phiếu xác nhận nhận hàng theo PO.";

    /// <summary>Quyền thuộc phân hệ đang tắt (cấu hình <c>Modules</c>).</summary>
    private static bool ModuleDisabled(EndpointFilterInvocationContext context, string permission) =>
        context.HttpContext.RequestServices.GetRequiredService<IOptions<ModuleOptions>>().Value.IsDisabled(permission);

    /// <summary>403 kèm lý do phân hệ đã tắt, thay cho câu "không có quyền" dễ hiểu nhầm là thiếu phân quyền.</summary>
    private static IResult ModuleDisabledProblem() =>
        Results.Problem(title: InventoryDisabledMessage, statusCode: StatusCodes.Status403Forbidden);
}
