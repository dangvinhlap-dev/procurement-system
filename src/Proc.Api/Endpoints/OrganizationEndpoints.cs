using Proc.Api.Security;
using Proc.Api.Validation;
using Proc.Application.Organization;
using Proc.Domain.Organization;

namespace Proc.Api.Endpoints;

/// <summary>
/// Endpoint danh mục tổ chức: công ty, kho (design.md mục 5.1, 5.6). Dữ liệu đã được lọc theo phạm vi người dùng;
/// quy tắc thêm/sửa (chỉ ở tổng công ty, kho tổng, phạm vi kho) nằm ở dịch vụ.
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
        var companies = app.MapGroup("/api/companies").WithTags("Danh mục");
        companies.MapGet("/", (ICompanyAdministration s, CancellationToken ct) => s.ListAsync(ct))
            .RequirePermission(Permissions.System.CompanyView);
        companies.MapGet("/{id:guid}", (Guid id, ICompanyAdministration s, CancellationToken ct) => s.GetAsync(id, ct))
            .RequirePermission(Permissions.System.CompanyView);
        companies.MapPost("/", async (CreateCompanyRequest request, ICompanyAdministration s, CancellationToken ct) =>
            {
                var created = await s.CreateAsync(request, ct);
                return Results.Created($"/api/companies/{created.Id}", created);
            })
            .RequirePermission(Permissions.System.CompanyManage)
            .Validate<CreateCompanyRequest>();
        companies.MapPut("/{id:guid}", (Guid id, UpdateCompanyRequest request, ICompanyAdministration s, CancellationToken ct) =>
                s.UpdateAsync(id, request, ct))
            .RequirePermission(Permissions.System.CompanyManage)
            .Validate<UpdateCompanyRequest>();

        var warehouses = app.MapGroup("/api/warehouses").WithTags("Danh mục");
        warehouses.MapGet("/", (IWarehouseAdministration s, CancellationToken ct) => s.ListAsync(ct))
            .RequirePermission(Permissions.Organization.WarehouseView);
        warehouses.MapGet("/{id:guid}", (Guid id, IWarehouseAdministration s, CancellationToken ct) => s.GetAsync(id, ct))
            .RequirePermission(Permissions.Organization.WarehouseView);
        warehouses.MapPost("/", async (CreateWarehouseRequest request, IWarehouseAdministration s, CancellationToken ct) =>
            {
                var created = await s.CreateAsync(request, ct);
                return Results.Created($"/api/warehouses/{created.Id}", created);
            })
            .RequirePermission(Permissions.Organization.WarehouseManage)
            .Validate<CreateWarehouseRequest>();
        warehouses.MapPut("/{id:guid}", (Guid id, UpdateWarehouseRequest request, IWarehouseAdministration s, CancellationToken ct) =>
                s.UpdateAsync(id, request, ct))
            .RequirePermission(Permissions.Organization.WarehouseManage)
            .Validate<UpdateWarehouseRequest>();

        return app;
    }
}
