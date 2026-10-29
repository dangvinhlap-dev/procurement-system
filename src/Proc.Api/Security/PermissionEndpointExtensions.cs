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
}
