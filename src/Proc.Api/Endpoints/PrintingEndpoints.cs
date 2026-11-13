using Proc.Api.Security;
using Proc.Application.Printing;
using Proc.Domain.Organization;

namespace Proc.Api.Endpoints;

/// <summary>
/// In phiếu PDF (design.md mục 10.10): cùng quyền với xem phiếu; phạm vi công ty, kho kiểm tra trong dịch vụ xem phiếu.
/// </summary>
public static class PrintingEndpoints
{
    private const string PdfContentType = "application/pdf";

    /// <summary>
    /// Khai báo các endpoint.
    /// </summary>
    /// <param name="app">Bộ định tuyến endpoint.</param>
    /// <returns>Chính <paramref name="app"/>.</returns>
    public static IEndpointRouteBuilder MapPrintingEndpoints(this IEndpointRouteBuilder app)
    {
        // Lát Y7: mặc định bảng theo dõi mua hàng 4A/4B; form=supplier in đơn đặt hàng gửi NCC.
        app.MapGet("/api/purchase-orders/{id:guid}/pdf",
                async (Guid id, string? form, IDocumentPdfService s, CancellationToken ct) => Pdf(await s.PurchaseOrderAsync(id, form, ct)))
            .WithTags("In phiếu")
            .RequirePermission(Permissions.Purchase.PoView);
        app.MapGet("/api/purchase-requests/{id:guid}/pdf", async (Guid id, IDocumentPdfService s, CancellationToken ct) => Pdf(await s.PurchaseRequestAsync(id, ct)))
            .WithTags("In phiếu")
            .RequirePermission(Permissions.Purchase.PrView);
        app.MapGet("/api/purchase-summaries/{id:guid}/pdf", async (Guid id, IDocumentPdfService s, CancellationToken ct) => Pdf(await s.PurchaseSummaryAsync(id, ct)))
            .WithTags("In phiếu")
            .RequirePermission(Permissions.Purchase.SummaryView);
        // Lát Y8: đề nghị thanh toán 5A/5B (tiền mặt / chuyển khoản theo hình thức của phiếu).
        app.MapGet("/api/payment-requests/{id:guid}/pdf", async (Guid id, IDocumentPdfService s, CancellationToken ct) => Pdf(await s.PaymentRequestAsync(id, ct)))
            .WithTags("In phiếu")
            .RequireAnyPermission(Permissions.Purchase.PaymentView, Permissions.Purchase.PaymentCreate, Permissions.Purchase.PaymentApprove,
                Permissions.Purchase.PaymentPay);
        // Lát Y11c: bảng kê chi phí tạp vụ 2C (mẫu giả lập nhóm C – chờ mẫu thật).
        app.MapGet("/api/sundry-expense-sheets/{id:guid}/pdf", async (Guid id, IDocumentPdfService s, CancellationToken ct) => Pdf(await s.SundryExpenseSheetAsync(id, ct)))
            .WithTags("In phiếu")
            .RequireAnyPermission(Permissions.Purchase.SundryView, Permissions.Purchase.SundryCreate, Permissions.Purchase.SundryApprove);
        // Lát Y6: kết quả hỏi giá theo biểu mẫu 3A / 3a / 3B; form=3A in bảng so sánh (RFQ loại A).
        app.MapGet("/api/rfqs/{id:guid}/pdf", async (Guid id, string? form, IDocumentPdfService s, CancellationToken ct) => Pdf(await s.RfqAsync(id, form, ct)))
            .WithTags("In phiếu")
            .RequirePermission(Permissions.Sourcing.RfqView);
        app.MapGet("/api/inventory-documents/{id:guid}/pdf", async (Guid id, IDocumentPdfService s, CancellationToken ct) => Pdf(await s.InventoryDocumentAsync(id, ct)))
            .WithTags("In phiếu")
            .RequirePermission(Permissions.Inventory.DocumentView);
        app.MapGet("/api/goods-receipts/{id:guid}/record-pdf",
                async (Guid id, IDocumentPdfService s, CancellationToken ct) => Pdf(await s.GoodsReceiptRecordAsync(id, ct)))
            .WithTags("In phiếu")
            .RequirePermission(Permissions.Inventory.DocumentView);
        return app;
    }

    // Không đặt tên file tải về (Content-Disposition: inline) để trình duyệt mở xem ngay; tên file đi trong header riêng cho giao diện.
    private static IResult Pdf(PdfFile file) => new PdfResult(file);

    /// <summary>Kết quả HTTP trả file PDF (in phiếu) – đặt Content-Type, tên file tải về rồi ghi nội dung.</summary>
    private sealed class PdfResult(PdfFile file) : IResult
    {
        public Task ExecuteAsync(HttpContext httpContext)
        {
            httpContext.Response.ContentType = PdfContentType;
            httpContext.Response.Headers.ContentDisposition = $"inline; filename*=UTF-8''{Uri.EscapeDataString(file.FileName)}";
            return httpContext.Response.Body.WriteAsync(file.Content).AsTask();
        }
    }
}
