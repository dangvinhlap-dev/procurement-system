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
        app.MapGet("/api/purchase-orders/{id:guid}/pdf", async (Guid id, IDocumentPdfService s, CancellationToken ct) => Pdf(await s.PurchaseOrderAsync(id, ct)))
            .WithTags("In phiếu")
            .RequirePermission(Permissions.Purchase.PoView);
        app.MapGet("/api/purchase-requests/{id:guid}/pdf", async (Guid id, IDocumentPdfService s, CancellationToken ct) => Pdf(await s.PurchaseRequestAsync(id, ct)))
            .WithTags("In phiếu")
            .RequirePermission(Permissions.Purchase.PrView);
        app.MapGet("/api/inventory-documents/{id:guid}/pdf", async (Guid id, IDocumentPdfService s, CancellationToken ct) => Pdf(await s.InventoryDocumentAsync(id, ct)))
            .WithTags("In phiếu")
            .RequirePermission(Permissions.Inventory.DocumentView);
        return app;
    }

    // Không đặt tên file tải về (Content-Disposition: inline) để trình duyệt mở xem ngay; tên file đi trong header riêng cho giao diện.
    private static IResult Pdf(PdfFile file) => new PdfResult(file);

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
