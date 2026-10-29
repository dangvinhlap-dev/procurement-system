using Proc.Api.Security;
using Proc.Application.Organization;
using Proc.Domain.Organization;

namespace Proc.Api.Endpoints;

/// <summary>
/// Endpoint danh mục tổ chức: kho, nhóm hàng. Dữ liệu đã được lọc theo phạm vi người dùng.
/// </summary>
public static class OrganizationEndpoints
{
    /// <summary>
    /// Khai báo các endpoint.
    /// </summary>
    /// <param name="app">Bộ định tuyến endpoint.</param>
    /// <returns>Chính <paramref name="app"/>.</returns>
    public static IEndpointRouteBuilder MapOrganizationEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/warehouses", (IOrganizationQueries queries, CancellationToken ct) => queries.GetWarehousesAsync(ct))
            .WithTags("Danh mục")
            .RequirePermission(Permissions.Organization.WarehouseView);

        app.MapGet("/api/item-categories", (IOrganizationQueries queries, CancellationToken ct) => queries.GetItemCategoriesAsync(ct))
            .WithTags("Danh mục")
            .RequirePermission(Permissions.MasterData.ItemCategoryView);

        return app;
    }
}
