using Proc.Api.Security;
using Proc.Api.Validation;
using Proc.Application.Inventory;
using Proc.Domain.Organization;

namespace Proc.Api.Endpoints;

/// <summary>
/// Endpoint kho: kỳ kho (design.md mục 7, 7.6). Chứng từ kho, sổ kho, tồn kho thêm ở các lát sau của giai đoạn 2.
/// </summary>
public static class InventoryEndpoints
{
    /// <summary>
    /// Khai báo các endpoint.
    /// </summary>
    /// <param name="app">Bộ định tuyến endpoint.</param>
    /// <returns>Chính <paramref name="app"/>.</returns>
    public static IEndpointRouteBuilder MapInventoryEndpoints(this IEndpointRouteBuilder app)
    {
        var periods = app.MapGroup("/api/inventory-periods").WithTags("Kỳ kho");
        periods.MapGet("/", (int? year, IInventoryPeriodService s, CancellationToken ct) => s.GetCalendarAsync(year, ct))
            .RequirePermission(Permissions.Inventory.PeriodView);
        periods.MapPost("/years", (CreateInventoryYearRequest request, IInventoryPeriodService s, CancellationToken ct) =>
                s.CreateYearAsync(request, ct))
            .RequirePermission(Permissions.Inventory.PeriodOpen)
            .Validate<CreateInventoryYearRequest>();
        periods.MapPost("/{id:guid}/open", (Guid id, IInventoryPeriodService s, CancellationToken ct) => s.OpenAsync(id, ct))
            .RequirePermission(Permissions.Inventory.PeriodOpen);

        return app;
    }
}
