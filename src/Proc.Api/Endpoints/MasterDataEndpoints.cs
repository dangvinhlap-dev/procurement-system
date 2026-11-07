using Proc.Api.Security;
using Proc.Api.Validation;
using Proc.Application.MasterData;
using Proc.Application.Purchasing;
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
        // Đánh số danh mục (design.md mục 5.11): xem cần quyền xem tham số; thêm/sửa/xóa cần quyền quản lý tham số, làm việc ở tổng công ty.
        var numberings = app.MapGroup("/api/catalog-numberings").WithTags("Tham số");
        numberings.MapGet("/", (ICatalogNumberingAdministration s, CancellationToken ct) => s.ListAsync(ct))
            .RequirePermission(Permissions.System.ParameterView);
        numberings.MapPost("/", (SaveCatalogNumberingRequest request, ICatalogNumberingAdministration s, CancellationToken ct) => s.CreateAsync(request, ct))
            .RequirePermission(Permissions.System.ParameterManage)
            .Validate<SaveCatalogNumberingRequest>();
        numberings.MapPut("/{id:guid}", (Guid id, SaveCatalogNumberingRequest request, ICatalogNumberingAdministration s, CancellationToken ct) =>
                s.UpdateAsync(id, request, ct))
            .RequirePermission(Permissions.System.ParameterManage)
            .Validate<SaveCatalogNumberingRequest>();
        numberings.MapDelete("/{id:guid}", async (Guid id, ICatalogNumberingAdministration s, CancellationToken ct) =>
            {
                await s.DeleteAsync(id, ct);
                return Results.NoContent();
            })
            .RequirePermission(Permissions.System.ParameterManage);

        var uoms = app.MapGroup("/api/uoms").WithTags("Danh mục hàng");
        // Người lập PR chọn ĐVT cho hàng mới chưa có mã (design.md mục 18.12) dù không có quyền xem danh mục hàng.
        uoms.MapGet("/", (IUomAdministration s, CancellationToken ct) => s.ListAsync(ct))
            .RequireAnyPermission(Permissions.MasterData.ItemView, Permissions.Purchase.PrCreate, Permissions.Purchase.PrView);
        uoms.MapPost("/", async (CreateUomRequest request, IUomAdministration s, CancellationToken ct) =>
            {
                var created = await s.CreateAsync(request, ct);
                return Results.Created($"/api/uoms/{created.Id}", created);
            })
            .RequirePermission(Permissions.MasterData.ItemManage)
            .Validate<CreateUomRequest>();
        uoms.MapPut("/{id:guid}", (Guid id, UpdateUomRequest request, IUomAdministration s, CancellationToken ct) =>
                s.UpdateAsync(id, request, ct))
            .RequireAnyPermission(Permissions.MasterData.ItemManage, Permissions.MasterData.CatalogEditShared)
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
            .RequireAnyPermission(Permissions.MasterData.ItemCategoryManage, Permissions.MasterData.CatalogEditShared)
            .Validate<UpdateItemCategoryRequest>();

        var items = app.MapGroup("/api/items").WithTags("Danh mục hàng");
        items.MapGet("/", (string? search, Guid? categoryId, string? type, bool? activeOnly, IItemAdministration s, CancellationToken ct) =>
                s.SearchAsync(new ItemQuery(search, categoryId, type, activeOnly ?? false), ct))
            .RequirePermission(Permissions.MasterData.ItemView);
        // Ô chọn hàng trên chứng từ: người lập chứng từ kho có thể không có quyền xem danh mục hàng hóa, nên chỉ cần đăng nhập
        // và có công ty làm việc; kết quả vẫn lọc theo phạm vi nhóm hàng.
        items.MapGet("/lookup", (string? search, bool? inventoryOnly, Guid[]? ids, IItemAdministration s, CancellationToken ct) =>
            s.LookupAsync(search, inventoryOnly ?? false, ids ?? [], ct));
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
            .RequireAnyPermission(Permissions.MasterData.ItemManage, Permissions.MasterData.CatalogEditShared)
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
            .RequireAnyPermission(Permissions.MasterData.SupplierManage, Permissions.MasterData.CatalogEditShared)
            .Validate<UpdateSupplierGroupRequest>();

        var suppliers = app.MapGroup("/api/suppliers").WithTags("Nhà cung cấp");
        suppliers.MapGet("/", (string? search, Guid? groupId, string? status, bool? appliedOnly, Guid? categoryId, ISupplierAdministration s,
                    CancellationToken ct) =>
                s.SearchAsync(new SupplierQuery(search, groupId, status, appliedOnly ?? false, categoryId), ct))
            .RequirePermission(Permissions.MasterData.SupplierView);
        // Ô chọn NCC trên chứng từ: thủ kho lập phiếu nhập nhưng có thể không có quyền xem danh mục NCC.
        suppliers.MapGet("/lookup", (Guid[]? ids, ISupplierAdministration s, CancellationToken ct) => s.LookupForPurchaseAsync(ids ?? [], ct));
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
            .RequireAnyPermission(Permissions.MasterData.SupplierManage, Permissions.MasterData.CatalogEditShared)
            .Validate<SaveSupplierRequest>();

        // Yêu cầu duyệt NCC (design.md mục 18.6): ai xem được NCC thì xem được yêu cầu; duyệt cần quyền duyệt NCC tại công ty đang làm việc.
        var changes = app.MapGroup("/api/supplier-change-requests").WithTags("Yêu cầu duyệt NCC");
        changes.MapGet("/", (Guid? supplierId, ISupplierChangeRequestService s, CancellationToken ct) => s.ListAsync(supplierId, ct))
            .RequirePermission(Permissions.MasterData.SupplierView);
        changes.MapGet("/{id:guid}", (Guid id, ISupplierChangeRequestService s, CancellationToken ct) => s.GetAsync(id, ct))
            .RequirePermission(Permissions.MasterData.SupplierView);
        changes.MapPost("/{id:guid}/approve", (Guid id, ApprovalActionRequest request, ISupplierChangeRequestService s, CancellationToken ct) =>
                s.ApproveAsync(id, request, ct))
            .RequirePermission(Permissions.MasterData.SupplierApprove);
        changes.MapPost("/{id:guid}/reject", (Guid id, ApprovalActionRequest request, ISupplierChangeRequestService s, CancellationToken ct) =>
                s.RejectAsync(id, request, ct))
            .RequirePermission(Permissions.MasterData.SupplierApprove);
        changes.MapPost("/{id:guid}/cancel", (Guid id, ISupplierChangeRequestService s, CancellationToken ct) => s.CancelAsync(id, ct))
            .RequirePermission(Permissions.MasterData.SupplierView);

        // Kê khai xung đột lợi ích (design.md mục 18.8): ai đăng nhập cũng tự kê khai, xem kê khai của mình được; dịch vụ tự giới hạn
        // phạm vi xem. Ghi thay, kết thúc kê khai cần quyền quản lý kê khai.
        var conflicts = app.MapGroup("/api/conflict-declarations").WithTags("Kê khai xung đột lợi ích").RequireAuthorization();
        conflicts.MapGet("/", (IConflictDeclarationService s, CancellationToken ct) => s.ListAsync(ct));
        conflicts.MapGet("/users", (IConflictDeclarationService s, CancellationToken ct) => s.UserOptionsAsync(ct))
            .RequirePermission(Permissions.MasterData.ConflictManage);
        conflicts.MapGet("/suppliers", (IConflictDeclarationService s, CancellationToken ct) => s.SupplierOptionsAsync(ct));
        conflicts.MapGet("/{id:guid}", (Guid id, IConflictDeclarationService s, CancellationToken ct) => s.GetAsync(id, ct));
        conflicts.MapPost("/", async (CreateConflictDeclarationRequest request, IConflictDeclarationService s, CancellationToken ct) =>
            {
                var created = await s.CreateAsync(request, ct);
                return Results.Created($"/api/conflict-declarations/{created.Id}", created);
            })
            .Validate<CreateConflictDeclarationRequest>();
        conflicts.MapPost("/{id:guid}/end", (Guid id, EndConflictDeclarationRequest request, IConflictDeclarationService s, CancellationToken ct) =>
                s.EndAsync(id, request, ct))
            .RequirePermission(Permissions.MasterData.ConflictManage)
            .Validate<EndConflictDeclarationRequest>();

        // Phê duyệt NCC độc quyền (design.md mục 18.9): thuộc công ty đang làm việc, duyệt theo quy trình loại NDQ.
        var soleSources = app.MapGroup("/api/sole-sources").WithTags("NCC độc quyền").RequireAuthorization();
        soleSources.MapGet("/", (ISoleSourceService s, CancellationToken ct) => s.ListAsync(ct))
            .RequirePermission(Permissions.MasterData.SoleSourceView);
        soleSources.MapGet("/{id:guid}", (Guid id, ISoleSourceService s, CancellationToken ct) => s.GetAsync(id, ct))
            .RequirePermission(Permissions.MasterData.SoleSourceView);
        soleSources.MapPost("/", async (SaveSoleSourceRequest request, ISoleSourceService s, CancellationToken ct) =>
            {
                var created = await s.CreateAsync(request, ct);
                return Results.Created($"/api/sole-sources/{created.Id}", created);
            })
            .RequirePermission(Permissions.MasterData.SoleSourceManage)
            .Validate<SaveSoleSourceRequest>();
        soleSources.MapPut("/{id:guid}", (Guid id, SaveSoleSourceRequest request, ISoleSourceService s, CancellationToken ct) =>
                s.UpdateAsync(id, request, ct))
            .RequirePermission(Permissions.MasterData.SoleSourceManage)
            .Validate<SaveSoleSourceRequest>();
        soleSources.MapPost("/{id:guid}/submit", (Guid id, ISoleSourceService s, CancellationToken ct) => s.SubmitAsync(id, ct))
            .RequirePermission(Permissions.MasterData.SoleSourceManage);
        soleSources.MapPost("/{id:guid}/recall", (Guid id, ISoleSourceService s, CancellationToken ct) => s.RecallAsync(id, ct))
            .RequirePermission(Permissions.MasterData.SoleSourceManage);
        soleSources.MapPost("/{id:guid}/approve", (Guid id, ApprovalActionRequest request, ISoleSourceService s, CancellationToken ct) =>
                s.ApproveAsync(id, request, ct))
            .RequirePermission(Permissions.MasterData.SoleSourceApprove);
        soleSources.MapPost("/{id:guid}/reject", (Guid id, ApprovalActionRequest request, ISoleSourceService s, CancellationToken ct) =>
                s.RejectAsync(id, request, ct))
            .RequirePermission(Permissions.MasterData.SoleSourceApprove);
        soleSources.MapPost("/{id:guid}/end", (Guid id, EndSoleSourceRequest request, ISoleSourceService s, CancellationToken ct) =>
                s.EndAsync(id, request, ct))
            .RequirePermission(Permissions.MasterData.SoleSourceManage)
            .Validate<EndSoleSourceRequest>();
        soleSources.MapPost("/{id:guid}/renew", async (Guid id, ISoleSourceService s, CancellationToken ct) =>
            {
                var created = await s.RenewAsync(id, ct);
                return Results.Created($"/api/sole-sources/{created.Id}", created);
            })
            .RequirePermission(Permissions.MasterData.SoleSourceManage);

        // Đánh giá NCC định kỳ theo quý (design.md mục 18.20): công ty đang làm việc; người lập phiếu cũng xem được bảng.
        var reviews = app.MapGroup("/api/supplier-reviews").WithTags("Đánh giá NCC định kỳ").RequireAuthorization();
        reviews.MapGet("/", (int year, int quarter, ISupplierReviewService s, CancellationToken ct) => s.GetBoardAsync(year, quarter, ct))
            .RequireAnyPermission(Permissions.MasterData.SupplierReviewView, Permissions.MasterData.SupplierReviewManage);
        reviews.MapGet("/{id:guid}", (Guid id, ISupplierReviewService s, CancellationToken ct) => s.GetAsync(id, ct))
            .RequireAnyPermission(Permissions.MasterData.SupplierReviewView, Permissions.MasterData.SupplierReviewManage);
        reviews.MapPost("/", async (SaveSupplierReviewRequest request, ISupplierReviewService s, CancellationToken ct) =>
            {
                var created = await s.CreateAsync(request, ct);
                return Results.Created($"/api/supplier-reviews/{created.Id}", created);
            })
            .RequirePermission(Permissions.MasterData.SupplierReviewManage)
            .Validate<SaveSupplierReviewRequest>();
        reviews.MapPut("/{id:guid}", (Guid id, SaveSupplierReviewRequest request, ISupplierReviewService s, CancellationToken ct) =>
                s.UpdateAsync(id, request, ct))
            .RequirePermission(Permissions.MasterData.SupplierReviewManage)
            .Validate<SaveSupplierReviewRequest>();
        reviews.MapPost("/{id:guid}/complete", (Guid id, SaveSupplierReviewRequest request, ISupplierReviewService s, CancellationToken ct) =>
                s.CompleteAsync(id, request, ct))
            .RequirePermission(Permissions.MasterData.SupplierReviewManage)
            .Validate<SaveSupplierReviewRequest>();
        reviews.MapPost("/{id:guid}/cancel", (Guid id, CancelSupplierReviewRequest request, ISupplierReviewService s, CancellationToken ct) =>
                s.CancelAsync(id, request, ct))
            .RequirePermission(Permissions.MasterData.SupplierReviewManage)
            .Validate<CancelSupplierReviewRequest>();

        // NCC áp dụng tại công ty đang làm việc: mỗi công ty tự quản lý, nên quyền quản lý NCC tại chính công ty đó là đủ.
        suppliers.MapGet("/{id:guid}/company", (Guid id, ISupplierLinkAdministration s, CancellationToken ct) => s.GetCompanyLinkAsync(id, ct))
            .RequirePermission(Permissions.MasterData.SupplierView);
        suppliers.MapPut("/{id:guid}/company", (Guid id, SaveSupplierCompanyRequest request, ISupplierLinkAdministration s, CancellationToken ct) =>
                s.SaveCompanyLinkAsync(id, request, ct))
            .RequirePermission(Permissions.MasterData.SupplierManage)
            .Validate<SaveSupplierCompanyRequest>();

        suppliers.MapGet("/{id:guid}/items", (Guid id, ISupplierLinkAdministration s, CancellationToken ct) => s.ListItemsOfSupplierAsync(id, ct))
            .RequirePermission(Permissions.MasterData.SupplierView);
        suppliers.MapPost("/{id:guid}/items", async (Guid id, SaveSupplierItemRequest request, ISupplierLinkAdministration s, CancellationToken ct) =>
            {
                var created = await s.CreateItemAsync(id, request, ct);
                return Results.Created($"/api/supplier-items/{created.Id}", created);
            })
            .RequirePermission(Permissions.MasterData.SupplierManage)
            .Validate<SaveSupplierItemRequest>();

        var supplierItems = app.MapGroup("/api/supplier-items").WithTags("Nhà cung cấp");
        // Dòng PR/PO/báo giá RFQ tra mã hàng theo NCC và ĐVT mua (design.md mục 5.9): người lập phiếu có thể không có quyền xem danh mục
        // NCC nên chỉ cần đăng nhập và có công ty làm việc; kết quả vẫn lọc theo phạm vi nhóm hàng.
        supplierItems.MapGet("/lookup", (Guid[]? supplierIds, Guid[]? itemIds, ISupplierLinkAdministration s, CancellationToken ct) =>
            s.LookupItemsAsync(supplierIds ?? [], itemIds ?? [], ct));
        supplierItems.MapPut("/{id:guid}", (Guid id, SaveSupplierItemRequest request, ISupplierLinkAdministration s, CancellationToken ct) =>
                s.UpdateItemAsync(id, request, ct))
            .RequirePermission(Permissions.MasterData.SupplierManage)
            .Validate<SaveSupplierItemRequest>();
        supplierItems.MapDelete("/{id:guid}", async (Guid id, ISupplierLinkAdministration s, CancellationToken ct) =>
            {
                await s.DeleteItemAsync(id, ct);
                return Results.NoContent();
            })
            .RequirePermission(Permissions.MasterData.SupplierManage);

        items.MapGet("/{id:guid}/suppliers", (Guid id, ISupplierLinkAdministration s, CancellationToken ct) => s.ListSuppliersOfItemAsync(id, ct))
            .RequirePermission(Permissions.MasterData.ItemView);

        return app;
    }
}
