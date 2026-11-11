using Proc.Api.Security;
using Proc.Api.Validation;
using Proc.Application.Organization;
using Proc.Application.Purchasing;
using Proc.Domain.Organization;

namespace Proc.Api.Endpoints;

/// <summary>
/// Endpoint danh mục tổ chức: công ty, kho, bộ phận, khối, ủy quyền duyệt (design.md mục 5.1, 5.6, 18.5, 19.3). Dữ liệu đã được lọc theo phạm vi người dùng;
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
            .RequireAnyPermission(Permissions.System.CompanyManage, Permissions.MasterData.CatalogEditShared)
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

        var departments = app.MapGroup("/api/departments").WithTags("Danh mục");
        // Người lập/xem PR, PO (bộ phận đặt hàng – design.md mục 18.10), hợp đồng nguyên tắc cần danh sách bộ phận để chọn và hiển thị,
        // dù không có quyền xem danh mục bộ phận.
        departments.MapGet("/", (IDepartmentAdministration s, CancellationToken ct) => s.ListAsync(ct))
            .RequireAnyPermission(
                Permissions.Organization.DepartmentView, Permissions.Purchase.PrView, Permissions.Purchase.PrCreate, Permissions.Purchase.PoCreate,
                Permissions.Purchase.ContractManage);
        departments.MapGet("/{id:guid}", (Guid id, IDepartmentAdministration s, CancellationToken ct) => s.GetAsync(id, ct))
            .RequirePermission(Permissions.Organization.DepartmentView);
        departments.MapPost("/", async (CreateDepartmentRequest request, IDepartmentAdministration s, CancellationToken ct) =>
            {
                var created = await s.CreateAsync(request, ct);
                return Results.Created($"/api/departments/{created.Id}", created);
            })
            .RequirePermission(Permissions.Organization.DepartmentManage)
            .Validate<CreateDepartmentRequest>();
        departments.MapPut("/{id:guid}", (Guid id, UpdateDepartmentRequest request, IDepartmentAdministration s, CancellationToken ct) =>
                s.UpdateAsync(id, request, ct))
            .RequirePermission(Permissions.Organization.DepartmentManage)
            .Validate<UpdateDepartmentRequest>();
        // Tài khoản chọn làm Trưởng phòng ban / Giám đốc khối (lát O1) – chỉ người sửa danh mục cần.
        departments.MapGet("/user-options", (IDepartmentAdministration s, CancellationToken ct) => s.UserOptionsAsync(ct))
            .RequirePermission(Permissions.Organization.DepartmentManage);
        // Bộ phận của chính người dùng tại công ty đang làm việc – điền sẵn khi lập đề nghị mua; ai đăng nhập cũng hỏi được của mình.
        departments.MapGet("/mine", async (IDepartmentAdministration s, CancellationToken ct) => Results.Ok(new { departmentId = await s.MineAsync(ct) }))
            .RequireAuthorization();

        // Khối (lát O1, design.md mục 19.3): cùng quyền với danh mục bộ phận – khối chỉ là nhóm bộ phận kèm Giám đốc khối.
        var divisions = app.MapGroup("/api/divisions").WithTags("Danh mục");
        divisions.MapGet("/", (IDivisionAdministration s, CancellationToken ct) => s.ListAsync(ct))
            .RequirePermission(Permissions.Organization.DepartmentView);
        divisions.MapGet("/{id:guid}", (Guid id, IDivisionAdministration s, CancellationToken ct) => s.GetAsync(id, ct))
            .RequirePermission(Permissions.Organization.DepartmentView);
        divisions.MapPost("/", async (CreateDivisionRequest request, IDivisionAdministration s, CancellationToken ct) =>
            {
                var created = await s.CreateAsync(request, ct);
                return Results.Created($"/api/divisions/{created.Id}", created);
            })
            .RequirePermission(Permissions.Organization.DepartmentManage)
            .Validate<CreateDivisionRequest>();
        divisions.MapPut("/{id:guid}", (Guid id, UpdateDivisionRequest request, IDivisionAdministration s, CancellationToken ct) =>
                s.UpdateAsync(id, request, ct))
            .RequirePermission(Permissions.Organization.DepartmentManage)
            .Validate<UpdateDivisionRequest>();

        // Ủy quyền duyệt (lát O2, design.md mục 19.3): ai cũng tự ủy quyền được; lập thay, xem toàn bộ cần quyền quản lý (dịch vụ kiểm tra).
        var delegations = app.MapGroup("/api/approval-delegations").WithTags("Ủy quyền duyệt").RequireAuthorization();
        delegations.MapGet("/", (IApprovalDelegationService s, CancellationToken ct) => s.ListAsync(ct));
        delegations.MapGet("/users", (IApprovalDelegationService s, CancellationToken ct) => s.UserOptionsAsync(ct));
        delegations.MapGet("/doc-types", (IApprovalDelegationService s) => s.DocTypes());
        delegations.MapGet("/{id:guid}", (Guid id, IApprovalDelegationService s, CancellationToken ct) => s.GetAsync(id, ct));
        delegations.MapPost("/", async (CreateApprovalDelegationRequest request, IApprovalDelegationService s, CancellationToken ct) =>
            {
                var created = await s.CreateAsync(request, ct);
                return Results.Created($"/api/approval-delegations/{created.Id}", created);
            })
            .Validate<CreateApprovalDelegationRequest>();
        delegations.MapPost("/{id:guid}/revoke", (Guid id, IApprovalDelegationService s, CancellationToken ct) => s.RevokeAsync(id, ct));

        return app;
    }
}
