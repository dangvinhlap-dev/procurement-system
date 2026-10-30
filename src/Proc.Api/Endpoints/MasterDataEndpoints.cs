using Proc.Api.Security;
using Proc.Api.Validation;
using Proc.Application.MasterData;
using Proc.Domain.Organization;

namespace Proc.Api.Endpoints;

/// <summary>
/// Endpoint danh mục dùng chung: đơn vị tính, nhóm hàng, hàng hóa, nhóm NCC, nhà cung cấp (design.md mục 5.2, 5.3, 5.7, 5.8).
/// Đơn vị tính dùng chung quyền của hàng hóa (là một phần của danh mục hàng). Quy tắc "chỉ ở tổng công ty" nằm ở dịch vụ.
/// </summary>
public static class MasterDataEndpoints
{
    /// <summary>
    /// Khai báo các endpoint.
    /// </summary>
    /// <param name="app">Bộ định tuyến endpoint.</param>
    /// <returns>Chính <paramref name="app"/>.</returns>
    public static IEndpointRouteBuilder MapMasterDataEndpoints(this IEndpointRouteBuilder app)
    {
        var uoms = app.MapGroup("/api/uoms").WithTags("Danh mục hàng");
        uoms.MapGet("/", (IUomAdministration s, CancellationToken ct) => s.ListAsync(ct))
            .RequirePermission(Permissions.MasterData.ItemView);
        uoms.MapPost("/", async (CreateUomRequest request, IUomAdministration s, CancellationToken ct) =>
            {
                var created = await s.CreateAsync(request, ct);
                return Results.Created($"/api/uoms/{created.Id}", created);
            })
            .RequirePermission(Permissions.MasterData.ItemManage)
            .Validate<CreateUomRequest>();
        uoms.MapPut("/{id:guid}", (Guid id, UpdateUomRequest request, IUomAdministration s, CancellationToken ct) =>
                s.UpdateAsync(id, request, ct))
            .RequirePermission(Permissions.MasterData.ItemManage)
            .Validate<UpdateUomRequest>();

        var categories = app.MapGroup("/api/item-categories").WithTags("Danh mục hàng");
        categories.MapGet("/", (IItemCategoryAdministration s, CancellationToken ct) => s.ListAsync(ct))
            .RequirePermission(Permissions.MasterData.ItemCategoryView);
        categories.MapPost("/", async (CreateItemCategoryRequest request, IItemCategoryAdministration s, CancellationToken ct) =>
            {
                var created = await s.CreateAsync(request, ct);
                return Results.Created($"/api/item-categories/{created.Id}", created);
            })
            .RequirePermission(Permissions.MasterData.ItemCategoryManage)
            .Validate<CreateItemCategoryRequest>();
        categories.MapPut("/{id:guid}", (Guid id, UpdateItemCategoryRequest request, IItemCategoryAdministration s, CancellationToken ct) =>
                s.UpdateAsync(id, request, ct))
            .RequirePermission(Permissions.MasterData.ItemCategoryManage)
            .Validate<UpdateItemCategoryRequest>();

        var items = app.MapGroup("/api/items").WithTags("Danh mục hàng");
        items.MapGet("/", (string? search, Guid? categoryId, string? type, bool? activeOnly, IItemAdministration s, CancellationToken ct) =>
                s.SearchAsync(new ItemQuery(search, categoryId, type, activeOnly ?? false), ct))
            .RequirePermission(Permissions.MasterData.ItemView);
        items.MapGet("/{id:guid}", (Guid id, IItemAdministration s, CancellationToken ct) => s.GetAsync(id, ct))
            .RequirePermission(Permissions.MasterData.ItemView);
        items.MapPost("/", async (CreateItemRequest request, IItemAdministration s, CancellationToken ct) =>
            {
                var created = await s.CreateAsync(request, ct);
                return Results.Created($"/api/items/{created.Id}", created);
            })
            .RequirePermission(Permissions.MasterData.ItemManage)
            .Validate<CreateItemRequest>();
        items.MapPut("/{id:guid}", (Guid id, UpdateItemRequest request, IItemAdministration s, CancellationToken ct) =>
                s.UpdateAsync(id, request, ct))
            .RequirePermission(Permissions.MasterData.ItemManage)
            .Validate<UpdateItemRequest>();

        var groups = app.MapGroup("/api/supplier-groups").WithTags("Nhà cung cấp");
        groups.MapGet("/", (ISupplierGroupAdministration s, CancellationToken ct) => s.ListAsync(ct))
            .RequirePermission(Permissions.MasterData.SupplierView);
        groups.MapPost("/", async (CreateSupplierGroupRequest request, ISupplierGroupAdministration s, CancellationToken ct) =>
            {
                var created = await s.CreateAsync(request, ct);
                return Results.Created($"/api/supplier-groups/{created.Id}", created);
            })
            .RequirePermission(Permissions.MasterData.SupplierManage)
            .Validate<CreateSupplierGroupRequest>();
        groups.MapPut("/{id:guid}", (Guid id, UpdateSupplierGroupRequest request, ISupplierGroupAdministration s, CancellationToken ct) =>
                s.UpdateAsync(id, request, ct))
            .RequirePermission(Permissions.MasterData.SupplierManage)
            .Validate<UpdateSupplierGroupRequest>();

        var suppliers = app.MapGroup("/api/suppliers").WithTags("Nhà cung cấp");
        suppliers.MapGet("/", (string? search, Guid? groupId, string? status, ISupplierAdministration s, CancellationToken ct) =>
                s.SearchAsync(new SupplierQuery(search, groupId, status), ct))
            .RequirePermission(Permissions.MasterData.SupplierView);
        suppliers.MapGet("/{id:guid}", (Guid id, ISupplierAdministration s, CancellationToken ct) => s.GetAsync(id, ct))
            .RequirePermission(Permissions.MasterData.SupplierView);
        suppliers.MapPost("/", async (SaveSupplierRequest request, ISupplierAdministration s, CancellationToken ct) =>
            {
                var created = await s.CreateAsync(request, ct);
                return Results.Created($"/api/suppliers/{created.Id}", created);
            })
            .RequirePermission(Permissions.MasterData.SupplierManage)
            .Validate<SaveSupplierRequest>();
        suppliers.MapPut("/{id:guid}", (Guid id, SaveSupplierRequest request, ISupplierAdministration s, CancellationToken ct) =>
                s.UpdateAsync(id, request, ct))
            .RequirePermission(Permissions.MasterData.SupplierManage)
            .Validate<SaveSupplierRequest>();

        return app;
    }
}
