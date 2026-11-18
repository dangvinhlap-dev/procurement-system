using Microsoft.AspNetCore.Mvc;
using Proc.Api.Security;
using Proc.Application.Documents;
using Proc.Application.Printing;
using Proc.Domain.Common;
using Proc.Domain.Documents;
using Proc.Domain.Organization;

namespace Proc.Api.Endpoints;

/// <summary>
/// Chữ ký người dùng, logo công ty, In / Tải PDF đề nghị mua có nhật ký in (lát IN1 – design.md mục 20.4). Chữ ký của mình: chỉ cần đăng nhập
/// (chính sách mặc định); chữ ký người khác: quyền <c>System.User.Signature</c>; logo: quyền xem / sửa công ty.
/// </summary>
public static class SignatureEndpoints
{
    /// <summary>Khai báo các endpoint.</summary>
    /// <param name="app">Bộ định tuyến endpoint.</param>
    /// <returns>Chính <paramref name="app"/>.</returns>
    public static IEndpointRouteBuilder MapSignatureEndpoints(this IEndpointRouteBuilder app)
    {
        var mine = app.MapGroup("/api/me/signature").WithTags("Chữ ký");
        mine.MapGet("/", (IUserSignatureService s, CancellationToken ct) => s.GetAsync(null, ct));
        mine.MapGet("/image", async (int? version, IUserSignatureService s, CancellationToken ct) =>
            Png(await s.GetImageAsync(null, version, ct)));
        mine.MapPost("/", async ([FromForm] string? reason, IFormFile file, IUserSignatureService s, CancellationToken ct) =>
                await s.UploadAsync(null, await ReadAsync(file, UserSignature.MaxImageBytes, "Ảnh chữ ký", ct), reason, ct))
            // API dùng JWT trong header, không dùng cookie – không có nguy cơ CSRF (như đính kèm).
            .DisableAntiforgery();
        mine.MapPost("/retire", (SignatureReasonRequest request, IUserSignatureService s, CancellationToken ct) => s.RetireAsync(null, request.Reason, ct));

        var others = app.MapGroup("/api/users/{id:guid}/signature").WithTags("Chữ ký")
            .RequirePermission(Permissions.System.UserSignature);
        others.MapGet("/", (Guid id, IUserSignatureService s, CancellationToken ct) => s.GetAsync(id, ct));
        others.MapGet("/image", async (Guid id, int? version, IUserSignatureService s, CancellationToken ct) =>
            Png(await s.GetImageAsync(id, version, ct)));
        others.MapPost("/", async (Guid id, [FromForm] string? reason, IFormFile file, IUserSignatureService s, CancellationToken ct) =>
                await s.UploadAsync(id, await ReadAsync(file, UserSignature.MaxImageBytes, "Ảnh chữ ký", ct), reason, ct))
            .DisableAntiforgery();
        others.MapPost("/retire", (Guid id, SignatureReasonRequest request, IUserSignatureService s, CancellationToken ct) =>
            s.RetireAsync(id, request.Reason, ct));

        var logos = app.MapGroup("/api/companies/{id:guid}/logo").WithTags("Danh mục");
        logos.MapGet("/", (Guid id, ICompanyLogoService s, CancellationToken ct) => s.GetAsync(id, ct))
            .RequirePermission(Permissions.System.CompanyView);
        logos.MapGet("/image", async (Guid id, ICompanyLogoService s, CancellationToken ct) =>
            {
                var (content, type) = await s.GetImageAsync(id, ct);
                return Results.File(content, type);
            })
            .RequirePermission(Permissions.System.CompanyView);
        logos.MapPost("/", async (Guid id, IFormFile file, ICompanyLogoService s, CancellationToken ct) =>
                await s.UploadAsync(id, await ReadAsync(file, CompanyLogo.MaxImageBytes, "Logo", ct), ct))
            .RequirePermission(Permissions.System.CompanyManage)
            .DisableAntiforgery();
        logos.MapDelete("/", async (Guid id, ICompanyLogoService s, CancellationToken ct) =>
            {
                await s.DeleteAsync(id, ct);
                return Results.NoContent();
            })
            .RequirePermission(Permissions.System.CompanyManage);

        // In / Tải PDF đề nghị mua: POST vì ghi nhật ký in (lần in thứ n); xem trước dùng GET /pdf sẵn có (không ghi).
        app.MapPost("/api/purchase-requests/{id:guid}/print", async (Guid id, string? kind, IDocumentPdfService s, CancellationToken ct) =>
            {
                var file = await s.PurchaseRequestPrintAsync(id, kind == "download" ? PrintKind.Download : PrintKind.Print, ct);
                return Results.File(file.Content, "application/pdf", file.FileName);
            })
            .WithTags("In phiếu")
            .RequirePermission(Permissions.Purchase.PrView);
        app.MapGet("/api/purchase-requests/{id:guid}/prints", (Guid id, IDocumentPdfService s, CancellationToken ct) => s.PurchaseRequestPrintsAsync(id, ct))
            .WithTags("In phiếu")
            .RequirePermission(Permissions.Purchase.PrView);

        // Lát IN2: bảng theo dõi mua hàng 4A/4B giữ nguyên sheet – cùng quyền với xem PO; xem trước dùng GET /pdf sẵn có (không ghi).
        app.MapPost("/api/purchase-orders/{id:guid}/print", async (Guid id, string? kind, IDocumentPdfService s, CancellationToken ct) =>
            {
                var file = await s.DocumentPrintAsync(DocumentTypes.PurchaseOrder, id, kind == "download" ? PrintKind.Download : PrintKind.Print, null, ct);
                return Results.File(file.Content, "application/pdf", file.FileName);
            })
            .WithTags("In phiếu")
            .RequirePermission(Permissions.Purchase.PoView);
        app.MapGet("/api/purchase-orders/{id:guid}/prints",
                (Guid id, IDocumentPdfService s, CancellationToken ct) => s.DocumentPrintsAsync(DocumentTypes.PurchaseOrder, id, ct))
            .WithTags("In phiếu")
            .RequirePermission(Permissions.Purchase.PoView);

        // Lát IN2b: bảng tổng hợp đề xuất mua 2A/2B giữ nguyên sheet – cùng quyền với xem bảng tổng hợp.
        app.MapPost("/api/purchase-summaries/{id:guid}/print", async (Guid id, string? kind, IDocumentPdfService s, CancellationToken ct) =>
            {
                var file = await s.DocumentPrintAsync(DocumentTypes.PurchaseSummary, id, kind == "download" ? PrintKind.Download : PrintKind.Print, null, ct);
                return Results.File(file.Content, "application/pdf", file.FileName);
            })
            .WithTags("In phiếu")
            .RequirePermission(Permissions.Purchase.SummaryView);
        app.MapGet("/api/purchase-summaries/{id:guid}/prints",
                (Guid id, IDocumentPdfService s, CancellationToken ct) => s.DocumentPrintsAsync(DocumentTypes.PurchaseSummary, id, ct))
            .WithTags("In phiếu")
            .RequirePermission(Permissions.Purchase.SummaryView);

        // Lát IN2c: kết quả hỏi giá 3a/3A/3B giữ nguyên sheet (form=3A in bảng so sánh) – cùng quyền với xem RFQ.
        app.MapPost("/api/rfqs/{id:guid}/print", async (Guid id, string? kind, string? form, IDocumentPdfService s, CancellationToken ct) =>
            {
                var file = await s.DocumentPrintAsync(
                    DocumentTypes.RequestForQuotation, id, kind == "download" ? PrintKind.Download : PrintKind.Print, form, ct);
                return Results.File(file.Content, "application/pdf", file.FileName);
            })
            .WithTags("In phiếu")
            .RequirePermission(Permissions.Sourcing.RfqView);
        app.MapGet("/api/rfqs/{id:guid}/prints",
                (Guid id, IDocumentPdfService s, CancellationToken ct) => s.DocumentPrintsAsync(DocumentTypes.RequestForQuotation, id, ct))
            .WithTags("In phiếu")
            .RequirePermission(Permissions.Sourcing.RfqView);
        return app;
    }

    // Ảnh chữ ký trả nguyên PNG để giao diện xem (fetch kèm JWT rồi tạo blob) – không để trình duyệt lưu đệm vì chữ ký là dữ liệu nhạy cảm.
    private static IResult Png(byte[] content) => new NoStoreFile(content);

    /// <summary>Đọc file tải lên, chặn trước khi đọc nếu vượt dung lượng.</summary>
    private static async Task<byte[]> ReadAsync(IFormFile file, int maxBytes, string what, CancellationToken cancellationToken)
    {
        if (file.Length > maxBytes) throw new BusinessRuleException($"{what}: dung lượng {file.Length / 1024} KB vượt mức tối đa {maxBytes / 1024} KB.");
        using var memory = new MemoryStream((int)file.Length);
        await file.CopyToAsync(memory, cancellationToken);
        return memory.ToArray();
    }

    /// <summary>Trả PNG kèm Cache-Control: no-store.</summary>
    private sealed class NoStoreFile(byte[] content) : IResult
    {
        public Task ExecuteAsync(HttpContext httpContext)
        {
            httpContext.Response.ContentType = PrintImages.Png;
            httpContext.Response.Headers.CacheControl = "no-store";
            return httpContext.Response.Body.WriteAsync(content).AsTask();
        }
    }
}
