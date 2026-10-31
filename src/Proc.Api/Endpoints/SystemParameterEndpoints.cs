using Proc.Api.Security;
using Proc.Application.Organization;
using Proc.Domain.Organization;

namespace Proc.Api.Endpoints;

/// <summary>
/// Endpoint tham số hệ thống tại công ty đang làm việc (design.md mục 5.4) – màn hình SEC.
/// </summary>
public static class SystemParameterEndpoints
{
    /// <summary>
    /// Khai báo các endpoint.
    /// </summary>
    /// <param name="app">Bộ định tuyến endpoint.</param>
    /// <returns>Chính <paramref name="app"/>.</returns>
    public static IEndpointRouteBuilder MapSystemParameterEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/system-parameters").WithTags("Tham số hệ thống");

        group.MapGet("/", (ISystemParameterService s, CancellationToken ct) => s.ListAsync(ct))
            .RequirePermission(Permissions.System.ParameterView);

        group.MapPut("/{code}", (string code, UpdateSystemParameterRequest request, ISystemParameterService s, CancellationToken ct) =>
                s.UpdateAsync(code, request, ct))
            .RequirePermission(Permissions.System.ParameterManage);

        return app;
    }
}
