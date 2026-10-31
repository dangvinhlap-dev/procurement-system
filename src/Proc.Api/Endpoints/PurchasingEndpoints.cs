using Proc.Api.Security;
using Proc.Api.Validation;
using Proc.Application.Purchasing;
using Proc.Domain.Organization;

namespace Proc.Api.Endpoints;

/// <summary>API mua hàng (design.md mục 6.2, 10.2, 10.3): quy trình duyệt, đề nghị mua hàng, đơn mua hàng, theo dõi giao hàng.</summary>
public static class PurchasingEndpoints
{
    /// <summary>Đăng ký các endpoint.</summary>
    /// <param name="app">Ứng dụng.</param>
    /// <returns>Ứng dụng (để nối tiếp).</returns>
    public static IEndpointRouteBuilder MapPurchasingEndpoints(this IEndpointRouteBuilder app)
    {
        var rules = app.MapGroup("/api/approval-rules").WithTags("Quy trình duyệt");
        rules.MapGet("/", (IApprovalRuleAdministration s, CancellationToken ct) => s.ListAsync(ct))
            .RequirePermission(Permissions.System.ParameterView);
        rules.MapPost("/", async (SaveApprovalRuleRequest request, IApprovalRuleAdministration s, CancellationToken ct) =>
            {
                var created = await s.CreateAsync(request, ct);
                return Results.Created($"/api/approval-rules/{created.Id}", created);
            })
            .RequirePermission(Permissions.System.ParameterManage)
            .Validate<SaveApprovalRuleRequest>();
        rules.MapPut("/{id:guid}", (Guid id, SaveApprovalRuleRequest request, IApprovalRuleAdministration s, CancellationToken ct) => s.UpdateAsync(id, request, ct))
            .RequirePermission(Permissions.System.ParameterManage)
            .Validate<SaveApprovalRuleRequest>();
        rules.MapDelete("/{id:guid}", async (Guid id, IApprovalRuleAdministration s, CancellationToken ct) =>
            {
                await s.DeleteAsync(id, ct);
                return Results.NoContent();
            })
            .RequirePermission(Permissions.System.ParameterManage);
        // Việc cần làm: ai đăng nhập cũng xem được danh sách phiếu chờ chính mình duyệt (rỗng nếu không có quyền duyệt).
        app.MapGet("/api/approvals/pending", (IApprovalRuleAdministration s, CancellationToken ct) => s.PendingForMeAsync(ct))
            .WithTags("Quy trình duyệt")
            .RequireAuthorization();

        var pr = app.MapGroup("/api/purchase-requests").WithTags("Đề nghị mua hàng");
        pr.MapGet("/", (DateOnly? from, DateOnly? to, string? status, IPurchaseRequestService s, CancellationToken ct) =>
                s.SearchAsync(new PurchaseDocumentQuery(from, to, status), ct))
            .RequirePermission(Permissions.Purchase.PrView);
        pr.MapGet("/{id:guid}", (Guid id, IPurchaseRequestService s, CancellationToken ct) => s.GetAsync(id, ct))
            .RequirePermission(Permissions.Purchase.PrView);
        pr.MapGet("/open-lines", (IPurchaseRequestService s, CancellationToken ct) => s.OpenLinesAsync(ct))
            .RequirePermission(Permissions.Purchase.PoCreate);
        pr.MapPost("/", async (SavePurchaseRequestRequest request, IPurchaseRequestService s, CancellationToken ct) =>
            {
                var created = await s.CreateAsync(request, ct);
                return Results.Created($"/api/purchase-requests/{created.Id}", created);
            })
            .RequirePermission(Permissions.Purchase.PrCreate)
            .Validate<SavePurchaseRequestRequest>();
        pr.MapPut("/{id:guid}", (Guid id, SavePurchaseRequestRequest request, IPurchaseRequestService s, CancellationToken ct) => s.UpdateAsync(id, request, ct))
            .RequirePermission(Permissions.Purchase.PrCreate)
            .Validate<SavePurchaseRequestRequest>();
        pr.MapPost("/{id:guid}/submit", (Guid id, IPurchaseRequestService s, CancellationToken ct) => s.SubmitAsync(id, ct))
            .RequirePermission(Permissions.Purchase.PrCreate);
        pr.MapPost("/{id:guid}/approve", (Guid id, ApprovalActionRequest request, IPurchaseRequestService s, CancellationToken ct) => s.ApproveAsync(id, request, ct))
            .RequirePermission(Permissions.Purchase.PrApprove);
        pr.MapPost("/{id:guid}/reject", (Guid id, ApprovalActionRequest request, IPurchaseRequestService s, CancellationToken ct) => s.RejectAsync(id, request, ct))
            .RequirePermission(Permissions.Purchase.PrApprove);
        pr.MapPost("/{id:guid}/cancel", (Guid id, IPurchaseRequestService s, CancellationToken ct) => s.CancelAsync(id, ct))
            .RequirePermission(Permissions.Purchase.PrCreate);

        var po = app.MapGroup("/api/purchase-orders").WithTags("Đơn mua hàng");
        po.MapGet("/", (DateOnly? from, DateOnly? to, string? status, IPurchaseOrderService s, CancellationToken ct) =>
                s.SearchAsync(new PurchaseDocumentQuery(from, to, status), ct))
            .RequirePermission(Permissions.Purchase.PoView);
        po.MapGet("/tracking", (IPurchaseOrderService s, CancellationToken ct) => s.TrackingAsync(ct))
            .RequirePermission(Permissions.Purchase.PoView);
        // Thủ kho lập phiếu nhập theo PO cần đọc đơn dù không có quyền xem danh sách đơn mua hàng.
        po.MapGet("/{id:guid}", (Guid id, IPurchaseOrderService s, CancellationToken ct) => s.GetAsync(id, ct))
            .RequireAnyPermission(Permissions.Purchase.PoView, Permissions.Inventory.DocumentCreate);
        po.MapGet("/receivable", (IPurchaseOrderService s, CancellationToken ct) =>
                s.SearchAsync(new PurchaseDocumentQuery(DateOnly.MinValue, DateOnly.MaxValue, "Approved"), ct))
            .RequireAnyPermission(Permissions.Purchase.PoView, Permissions.Inventory.DocumentCreate);
        po.MapPost("/", async (SavePurchaseOrderRequest request, IPurchaseOrderService s, CancellationToken ct) =>
            {
                var created = await s.CreateAsync(request, ct);
                return Results.Created($"/api/purchase-orders/{created.Id}", created);
            })
            .RequirePermission(Permissions.Purchase.PoCreate)
            .Validate<SavePurchaseOrderRequest>();
        po.MapPut("/{id:guid}", (Guid id, SavePurchaseOrderRequest request, IPurchaseOrderService s, CancellationToken ct) => s.UpdateAsync(id, request, ct))
            .RequirePermission(Permissions.Purchase.PoCreate)
            .Validate<SavePurchaseOrderRequest>();
        po.MapPost("/{id:guid}/submit", (Guid id, IPurchaseOrderService s, CancellationToken ct) => s.SubmitAsync(id, ct))
            .RequirePermission(Permissions.Purchase.PoCreate);
        po.MapPost("/{id:guid}/approve", (Guid id, ApprovalActionRequest request, IPurchaseOrderService s, CancellationToken ct) => s.ApproveAsync(id, request, ct))
            .RequirePermission(Permissions.Purchase.PoApprove);
        po.MapPost("/{id:guid}/reject", (Guid id, ApprovalActionRequest request, IPurchaseOrderService s, CancellationToken ct) => s.RejectAsync(id, request, ct))
            .RequirePermission(Permissions.Purchase.PoApprove);
        po.MapPost("/{id:guid}/cancel", (Guid id, IPurchaseOrderService s, CancellationToken ct) => s.CancelAsync(id, ct))
            .RequirePermission(Permissions.Purchase.PoCreate);
        return app;
    }
}
