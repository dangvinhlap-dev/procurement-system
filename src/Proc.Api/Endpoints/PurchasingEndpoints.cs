using Proc.Application.Common;
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
        // Xem trước chuỗi duyệt khi gửi duyệt (yêu cầu người dùng 03/11/2026) – quyền theo loại phiếu kiểm tra trong dịch vụ.
        app.MapPost("/api/approvals/preview", (ApprovalPreviewRequest request, IApprovalPreviewService s, CancellationToken ct) => s.PreviewSubmitAsync(request, ct))
            .WithTags("Quy trình duyệt")
            .RequireAuthorization();

        // Hủy dây chuyền (lát Z4b, design.md mục 20.2): xem trước trình tự và hủy cả chuỗi – quyền từng phiếu kiểm tra trong dịch vụ.
        var chain = app.MapGroup("/api/cancel-chain").WithTags("Hủy dây chuyền").RequireAuthorization();
        chain.MapGet("/{docType}/{id:guid}", (string docType, Guid id, ICancelChainService s, CancellationToken ct) => s.PreviewAsync(docType, id, ct));
        chain.MapPost("/{docType}/{id:guid}", (string docType, Guid id, CancelChainRequest request, ICancelChainService s, CancellationToken ct) =>
            s.CancelAsync(docType, id, request, ct));

        var rules = app.MapGroup("/api/approval-rules").WithTags("Quy trình duyệt");
        rules.MapGet("/", (IApprovalRuleAdministration s, CancellationToken ct) => s.ListAsync(ct))
            .RequirePermission(Permissions.System.ParameterView);
        rules.MapGet("/types", (IApprovalRuleAdministration s) => s.Types())
            .RequirePermission(Permissions.System.ParameterView);
        // Cấu hình duyệt mẫu theo sơ đồ phê duyệt mua sắm (lát G10, set=SODO – mặc định) hoặc biểu mẫu CUVT (lát O3, set=CUVT): xem trước,
        // áp cho loại phiếu chưa cấu hình.
        rules.MapGet("/template", (string? set, IApprovalRuleAdministration s, CancellationToken ct) => s.TemplateAsync(set, ct))
            .RequirePermission(Permissions.System.ParameterView);
        rules.MapPost("/template", (string? set, IApprovalRuleAdministration s, CancellationToken ct) => s.ApplyTemplateAsync(set, ct))
            .RequirePermission(Permissions.System.ParameterManage);
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
        // Rút về nháp – người lập (hoặc quản trị); service kiểm tra người lập (yêu cầu người dùng 11/11/2026).
        pr.MapPost("/{id:guid}/recall", (Guid id, IPurchaseRequestService s, CancellationToken ct) => s.RecallAsync(id, ct))
            .RequirePermission(Permissions.Purchase.PrCreate);
        // Thủ kho nơi nhận nhập tồn từng dòng (lát Y3b, design.md mục 19.3); service kiểm tra phạm vi kho, loại mua, khóa sau duyệt.
        pr.MapPost("/{id:guid}/stock", (Guid id, EnterPurchaseRequestStockRequest request, IPurchaseRequestService s, CancellationToken ct) =>
                s.EnterStockAsync(id, request, ct))
            .RequirePermission(Permissions.Purchase.PrEnterStock);
        // Bước Thủ kho nơi nhận do người có quyền nhập tồn duyệt (lát Y3c); cơ chế duyệt kiểm tra đúng quyền của từng bước.
        pr.MapPost("/{id:guid}/approve", (Guid id, ApprovalActionRequest request, IPurchaseRequestService s, CancellationToken ct) => s.ApproveAsync(id, request, ct))
            .RequireAnyPermission(Permissions.Purchase.PrApprove, Permissions.Purchase.PrEnterStock);
        pr.MapPost("/{id:guid}/reject", (Guid id, ApprovalActionRequest request, IPurchaseRequestService s, CancellationToken ct) => s.RejectAsync(id, request, ct))
            .RequireAnyPermission(Permissions.Purchase.PrApprove, Permissions.Purchase.PrEnterStock);
        // PR đã duyệt bắt buộc lý do hủy (lát R7).
        pr.MapPost("/{id:guid}/cancel", async (Guid id, HttpRequest http, IPurchaseRequestService s, CancellationToken ct) =>
            {
                var request = http.HasJsonContentType() ? await http.ReadFromJsonAsync<CancelDocumentRequest>(ct) : null;
                return await s.CancelAsync(id, ct, request?.Reason);
            })
            .RequirePermission(Permissions.Purchase.PrCreate);
        // Đóng phần còn lại chưa lên PO (lát R6, design.md mục 18.26) – cùng quyền với hủy PR.
        pr.MapPost("/{id:guid}/close-remaining", (Guid id, CloseRemainingRequest request, IPurchaseRequestService s, CancellationToken ct) =>
                s.CloseRemainingAsync(id, request, ct))
            .RequirePermission(Permissions.Purchase.PrCreate);
        // Hàng mới chưa có mã của PR đã duyệt (design.md mục 18.12): nhóm Đàm phán lập phiếu tìm nguồn; người mua gắn mã hàng khi đã có mã.
        pr.MapPost("/{id:guid}/new-items/{newItemId:guid}/sourcing", (Guid id, Guid newItemId, IPurchaseRequestNewItemService s, CancellationToken ct) =>
                s.CreateSourcingAsync(id, newItemId, ct))
            .RequirePermission(Permissions.Sourcing.RfqManage);
        pr.MapPost("/{id:guid}/new-items/{newItemId:guid}/link-item",
                (Guid id, Guid newItemId, LinkNewItemRequest request, IPurchaseRequestNewItemService s, CancellationToken ct) =>
                    s.LinkItemAsync(id, newItemId, request, ct))
            .RequireAnyPermission(Permissions.Purchase.PoCreate, Permissions.Purchase.PrCreate);

        var po = app.MapGroup("/api/purchase-orders").WithTags("Đơn mua hàng");
        po.MapGet("/", (DateOnly? from, DateOnly? to, string? status, IPurchaseOrderService s, CancellationToken ct) =>
                s.SearchAsync(new PurchaseDocumentQuery(from, to, status), ct))
            .RequirePermission(Permissions.Purchase.PoView);
        po.MapGet("/tracking", (IPurchaseOrderService s, CancellationToken ct) => s.TrackingAsync(ct))
            .RequirePermission(Permissions.Purchase.PoView);
        // Giá đã duyệt (hợp đồng/bảng giá) cho người lập PO – design.md mục 18.10; không cần quyền xem bảng giá, hợp đồng.
        po.MapGet("/approved-prices", (Guid supplierId, DateOnly date, Guid[]? itemIds, Guid? departmentId, IPurchaseOrderService s, CancellationToken ct) =>
                s.ApprovedPricesAsync(supplierId, date, itemIds ?? [], departmentId, ct))
            .RequireAnyPermission(Permissions.Purchase.PoCreate, Permissions.Purchase.PoView);
        // Giá thấp nhất tập đoàn cho cảnh báo giá cao trên dòng PO (design.md mục 8.5, 10.3) – người lập/xem PO, không cần quyền so sánh giá.
        po.MapGet("/group-min-prices", (Guid[]? itemIds, Guid? excludeOrderId, Proc.Application.Pricing.IPriceComparisonService s, CancellationToken ct) =>
                s.GroupMinPricesAsync(itemIds ?? [], excludeOrderId, ct))
            .RequireAnyPermission(Permissions.Purchase.PoCreate, Permissions.Purchase.PoView);
        // Người lập phiếu nhận hàng XNH (thủ kho, phòng ban đề xuất dịch vụ) cần đọc đơn dù không có quyền xem danh sách đơn mua hàng.
        po.MapGet("/{id:guid}", (Guid id, IPurchaseOrderService s, CancellationToken ct) => s.GetAsync(id, ct))
            .RequireAnyPermission(Permissions.Purchase.PoView, Permissions.Purchase.ReceiptManage, Permissions.Inventory.DocumentCreate);
        po.MapGet("/receivable", (IPurchaseOrderService s, CancellationToken ct) =>
                s.SearchAsync(new PurchaseDocumentQuery(DateOnly.MinValue, DateOnly.MaxValue, "Approved"), ct))
            .RequireAnyPermission(Permissions.Purchase.PoView, Permissions.Purchase.ReceiptManage, Permissions.Inventory.DocumentCreate);
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
        po.MapPost("/{id:guid}/recall", (Guid id, IPurchaseOrderService s, CancellationToken ct) => s.RecallAsync(id, ct))
            .RequirePermission(Permissions.Purchase.PoCreate);
        po.MapPost("/{id:guid}/approve", (Guid id, ApprovalActionRequest request, IPurchaseOrderService s, CancellationToken ct) => s.ApproveAsync(id, request, ct))
            .RequirePermission(Permissions.Purchase.PoApprove);
        po.MapPost("/{id:guid}/reject", (Guid id, ApprovalActionRequest request, IPurchaseOrderService s, CancellationToken ct) => s.RejectAsync(id, request, ct))
            .RequirePermission(Permissions.Purchase.PoApprove);
        // PO đã duyệt bắt buộc lý do hủy (lát R7) – thân request có thể bỏ trống với PO nháp/chờ duyệt.
        po.MapPost("/{id:guid}/cancel", async (Guid id, HttpRequest http, IPurchaseOrderService s, CancellationToken ct) =>
            {
                var request = http.HasJsonContentType() ? await http.ReadFromJsonAsync<CancelDocumentRequest>(ct) : null;
                return await s.CancelAsync(id, ct, request?.Reason);
            })
            .RequirePermission(Permissions.Purchase.PoCreate);
        // Đóng phần NCC chưa giao (lát R6, design.md mục 18.26) – cùng quyền với hủy PO.
        po.MapPost("/{id:guid}/close-remaining", (Guid id, CloseRemainingRequest request, IPurchaseOrderService s, CancellationToken ct) =>
                s.CloseRemainingAsync(id, request, ct))
            .RequirePermission(Permissions.Purchase.PoCreate);
        // Xác nhận đã bổ sung hồ sơ PO khẩn cấp (design.md mục 18.11) – người lập PO.
        po.MapPost("/{id:guid}/complete-docs", (Guid id, CompleteDocsRequest request, IPurchaseOrderService s, CancellationToken ct) =>
                s.CompleteDocsAsync(id, request, ct))
            .RequirePermission(Permissions.Purchase.PoCreate);

        // Phiếu xác nhận nhận hàng XNH (lát K2, design.md mục 19.4): thủ kho / phòng ban đề xuất lập, xác nhận, điều chỉnh; đơn vị đặt
        // hàng nghiệm thu.
        var receipts = app.MapGroup("/api/receipt-confirmations").WithTags("Xác nhận nhận hàng");
        string[] receiptAny = [Permissions.Purchase.ReceiptView, Permissions.Purchase.ReceiptManage, Permissions.Purchase.ReceiptAccept];
        receipts.MapGet("/", (DateOnly? from, DateOnly? to, string? status, Guid? purchaseOrderId, IReceiptConfirmationService s, CancellationToken ct) =>
                s.SearchAsync(new ReceiptConfirmationQuery(from, to, status, purchaseOrderId), ct))
            .RequireAnyPermission(receiptAny);
        receipts.MapGet("/{id:guid}", (Guid id, IReceiptConfirmationService s, CancellationToken ct) => s.GetAsync(id, ct))
            .RequireAnyPermission(receiptAny);
        receipts.MapPost("/", async (SaveReceiptConfirmationRequest request, IReceiptConfirmationService s, CancellationToken ct) =>
            {
                var created = await s.CreateAsync(request, ct);
                return Results.Created($"/api/receipt-confirmations/{created.Id}", created);
            })
            .RequirePermission(Permissions.Purchase.ReceiptManage)
            .Validate<SaveReceiptConfirmationRequest>();
        receipts.MapPut("/{id:guid}", (Guid id, SaveReceiptConfirmationRequest request, IReceiptConfirmationService s, CancellationToken ct) =>
                s.UpdateAsync(id, request, ct))
            .RequirePermission(Permissions.Purchase.ReceiptManage)
            .Validate<SaveReceiptConfirmationRequest>();
        receipts.MapPost("/{id:guid}/confirm", (Guid id, IReceiptConfirmationService s, CancellationToken ct) => s.ConfirmAsync(id, ct))
            .RequirePermission(Permissions.Purchase.ReceiptManage);
        receipts.MapPost("/{id:guid}/cancel", (Guid id, CancelReceiptConfirmationRequest request, IReceiptConfirmationService s, CancellationToken ct) =>
                s.CancelAsync(id, request, ct))
            .RequirePermission(Permissions.Purchase.ReceiptManage)
            .Validate<CancelReceiptConfirmationRequest>();
        receipts.MapPut("/{id:guid}/invoice", (Guid id, UpdateReceiptConfirmationInvoiceRequest request, IReceiptConfirmationService s, CancellationToken ct) =>
                s.UpdateInvoiceAsync(id, request, ct))
            .RequirePermission(Permissions.Purchase.ReceiptManage)
            .Validate<UpdateReceiptConfirmationInvoiceRequest>();
        receipts.MapPost("/{id:guid}/acceptance", (Guid id, ReceiptAcceptanceRequest request, IReceiptConfirmationService s, CancellationToken ct) =>
                s.ConfirmAcceptanceAsync(id, request, ct))
            .RequirePermission(Permissions.Purchase.ReceiptAccept)
            .Validate<ReceiptAcceptanceRequest>();
        receipts.MapPost("/{id:guid}/adjustments", (Guid id, AdjustReceiptRequest request, IReceiptConfirmationService s, CancellationToken ct) =>
                s.AdjustAsync(id, request, ct))
            .RequirePermission(Permissions.Purchase.ReceiptManage)
            .Validate<AdjustReceiptRequest>();

        // Ngân sách bộ phận × tháng (lát C3, design.md mục 18.24).
        var budgets = app.MapGroup("/api/budgets").WithTags("Ngân sách bộ phận");
        budgets.MapGet("/{year:int}", (int year, IBudgetService s, CancellationToken ct) => s.YearAsync(year, ct))
            .RequireAnyPermission(Permissions.Purchase.BudgetView, Permissions.Purchase.BudgetManage);
        budgets.MapPut("/{year:int}/{departmentId:guid}", (int year, Guid departmentId, SaveDepartmentBudgetRequest request, IBudgetService s, CancellationToken ct) =>
                s.SaveAsync(departmentId, year, request, ct))
            .RequirePermission(Permissions.Purchase.BudgetManage);

        // Công trình và dự toán (lát Y2, design.md mục 19.2) – khung chi phí của đề nghị mua loại A.
        var projects = app.MapGroup("/api/construction-projects").WithTags("Công trình");
        projects.MapGet("/", (IConstructionProjectService s, CancellationToken ct) => s.ListAsync(ct))
            .RequireAnyPermission(Permissions.Purchase.ProjectView, Permissions.Purchase.ProjectManage);
        // Người lập / xem đề nghị mua chọn công trình trên phiếu loại A (Y3) – không cần quyền xem danh mục công trình.
        projects.MapGet("/options", (IConstructionProjectService s, CancellationToken ct) => s.OptionsAsync(ct))
            .RequireAnyPermission(
                Permissions.Purchase.ProjectView, Permissions.Purchase.ProjectManage, Permissions.Purchase.PrView, Permissions.Purchase.PrCreate);
        projects.MapGet("/{id:guid}", (Guid id, IConstructionProjectService s, CancellationToken ct) => s.GetAsync(id, ct))
            .RequireAnyPermission(Permissions.Purchase.ProjectView, Permissions.Purchase.ProjectManage);
        projects.MapPost("/", async (CreateConstructionProjectRequest request, IConstructionProjectService s, CancellationToken ct) =>
            {
                var created = await s.CreateAsync(request, ct);
                return Results.Created($"/api/construction-projects/{created.Id}", created);
            })
            .RequirePermission(Permissions.Purchase.ProjectManage)
            .Validate<CreateConstructionProjectRequest>();
        projects.MapPut("/{id:guid}", (Guid id, UpdateConstructionProjectRequest request, IConstructionProjectService s, CancellationToken ct) =>
                s.UpdateAsync(id, request, ct))
            .RequirePermission(Permissions.Purchase.ProjectManage)
            .Validate<UpdateConstructionProjectRequest>();
        projects.MapPost("/{id:guid}/estimate-revisions",
                (Guid id, ReviseConstructionEstimateRequest request, IConstructionProjectService s, CancellationToken ct) =>
                    s.ReviseEstimateAsync(id, request, ct))
            .RequirePermission(Permissions.Purchase.ProjectManage)
            .Validate<ReviseConstructionEstimateRequest>();

        // Phiếu kế hoạch nhu cầu (design.md mục 18.25 – lát G5a): mỗi công ty lập nhu cầu năm × nhóm hàng, chốt, không qua duyệt.
        var plans = app.MapGroup("/api/demand-plans").WithTags("Kế hoạch nhu cầu");
        plans.MapGet("/", (IDemandPlanService s, CancellationToken ct) => s.ListAsync(ct))
            .RequirePermission(Permissions.Purchase.DemandPlanView);
        plans.MapGet("/{id:guid}", (Guid id, IDemandPlanService s, CancellationToken ct) => s.GetAsync(id, ct))
            .RequirePermission(Permissions.Purchase.DemandPlanView);
        // Tổng hợp nhu cầu gộp (lát G5b): toàn tập đoàn cần thêm quyền ViewAllCompanies (kiểm tra trong dịch vụ).
        plans.MapGet("/summary", (int year, Guid? categoryId, bool? allCompanies, IDemandPlanService s, CancellationToken ct) =>
                s.SummaryAsync(year, categoryId, allCompanies ?? false, ct))
            .RequirePermission(Permissions.Purchase.DemandPlanView);
        // SL kế hoạch cho dòng RFQ / hợp đồng nguyên tắc (lát G5c) – chỉ đọc, POST vì gửi danh sách dòng.
        plans.MapPost("/quantities", (PlannedQuantityRequest request, IDemandPlanService s, CancellationToken ct) => s.QuantitiesAsync(request, ct))
            .RequirePermission(Permissions.Purchase.DemandPlanView)
            .Validate<PlannedQuantityRequest>();
        plans.MapPost("/", async (SaveDemandPlanRequest request, IDemandPlanService s, CancellationToken ct) =>
            {
                var created = await s.CreateAsync(request, ct);
                return Results.Created($"/api/demand-plans/{created.Id}", created);
            })
            .RequirePermission(Permissions.Purchase.DemandPlanManage)
            .Validate<SaveDemandPlanRequest>();
        plans.MapPut("/{id:guid}", (Guid id, SaveDemandPlanRequest request, IDemandPlanService s, CancellationToken ct) =>
                s.UpdateAsync(id, request, ct))
            .RequirePermission(Permissions.Purchase.DemandPlanManage)
            .Validate<SaveDemandPlanRequest>();
        plans.MapPost("/{id:guid}/confirm", (Guid id, IDemandPlanService s, CancellationToken ct) => s.ConfirmAsync(id, ct))
            .RequirePermission(Permissions.Purchase.DemandPlanManage);
        plans.MapPost("/{id:guid}/adjust", async (Guid id, IDemandPlanService s, CancellationToken ct) =>
            {
                var created = await s.AdjustAsync(id, ct);
                return Results.Created($"/api/demand-plans/{created.Id}", created);
            })
            .RequirePermission(Permissions.Purchase.DemandPlanManage);
        plans.MapPost("/{id:guid}/cancel", (Guid id, CancelDemandPlanRequest request, IDemandPlanService s, CancellationToken ct) =>
                s.CancelAsync(id, request, ct))
            .RequirePermission(Permissions.Purchase.DemandPlanManage)
            .Validate<CancelDemandPlanRequest>();
        // Phiếu đặt hàng gộp (design.md mục 18.25 – lát G7): Phòng cung ứng chung lập ở tổng công ty, phân bổ cho các công ty, sinh mỗi pháp
        // nhân một PO nháp. Tạo PO còn cần quyền tạo PO tại từng công ty (kiểm tra trong dịch vụ, dưới phạm vi công ty đó).
        var pooled = app.MapGroup("/api/pooled-orders").WithTags("Đặt hàng gộp");
        pooled.MapGet("/", (IPooledOrderService s, CancellationToken ct) => s.ListAsync(ct))
            .RequirePermission(Permissions.Purchase.PooledOrderView);
        pooled.MapGet("/approvals", (DateOnly date, IPooledOrderService s, CancellationToken ct) => s.ApprovalsAsync(date, ct))
            .RequirePermission(Permissions.Purchase.PooledOrderManage);
        pooled.MapGet("/context", (Guid approvalId, Guid supplierId, IPooledOrderService s, CancellationToken ct) =>
                s.ContextAsync(approvalId, supplierId, ct))
            .RequirePermission(Permissions.Purchase.PooledOrderManage);
        pooled.MapGet("/{id:guid}", (Guid id, IPooledOrderService s, CancellationToken ct) => s.GetAsync(id, ct))
            .RequirePermission(Permissions.Purchase.PooledOrderView);
        pooled.MapPost("/", async (SavePooledOrderRequest request, IPooledOrderService s, CancellationToken ct) =>
            {
                var created = await s.CreateAsync(request, ct);
                return Results.Created($"/api/pooled-orders/{created.Id}", created);
            })
            .RequirePermission(Permissions.Purchase.PooledOrderManage)
            .Validate<SavePooledOrderRequest>();
        pooled.MapPut("/{id:guid}", (Guid id, SavePooledOrderRequest request, IPooledOrderService s, CancellationToken ct) =>
                s.UpdateAsync(id, request, ct))
            .RequirePermission(Permissions.Purchase.PooledOrderManage)
            .Validate<SavePooledOrderRequest>();
        pooled.MapPost("/{id:guid}/cancel", (Guid id, CancelPooledOrderRequest request, IPooledOrderService s, CancellationToken ct) =>
                s.CancelAsync(id, request, ct))
            .RequirePermission(Permissions.Purchase.PooledOrderManage);
        pooled.MapPost("/{id:guid}/create-orders", (Guid id, IPooledOrderService s, CancellationToken ct) => s.CreateOrdersAsync(id, ct))
            .RequirePermission(Permissions.Purchase.PooledOrderManage);

        return app;
    }
}
