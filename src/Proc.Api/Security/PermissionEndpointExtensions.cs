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
                return Results.Problem(
                    title: $"Bạn không có quyền thực hiện chức năng này ({string.Join(" hoặc ", permissions)}).",
                    statusCode: StatusCodes.Status403Forbidden);
            }

            return await next(context);
        });
        return builder;
    }
}
