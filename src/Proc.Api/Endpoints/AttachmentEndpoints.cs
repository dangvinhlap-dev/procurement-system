using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Proc.Application.Documents;
using Proc.Infrastructure.Documents;

namespace Proc.Api.Endpoints;

/// <summary>
/// File đính kèm dùng chung (design.md mục 18.4). Không gắn RequirePermission ở đây vì quyền phụ thuộc loại đối tượng chủ (xem NCC cần
/// quyền NCC, xem PO cần quyền PO…) – dịch vụ kiểm tra; endpoint chỉ cần đăng nhập (chính sách mặc định).
/// </summary>
public static class AttachmentEndpoints
{
    /// <summary>
    /// Khai báo các endpoint.
    /// </summary>
    /// <param name="app">Bộ định tuyến endpoint.</param>
    /// <returns>Chính <paramref name="app"/>.</returns>
    public static IEndpointRouteBuilder MapAttachmentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/attachments").WithTags("Đính kèm");

        // Giới hạn dung lượng cho giao diện kiểm tra trước khi gửi và hiện "Tối đa n MB/file" (design.md mục 18.4) – đổi cấu hình máy chủ
        // là giao diện theo, không phải sửa code.
        group.MapGet("/limits", (IOptions<AttachmentOptions> options) => new AttachmentLimitsDto(options.Value.MaxFileSizeMb));
        group.MapGet("/", (string ownerType, Guid ownerId, IAttachmentService s, CancellationToken ct) => s.ListAsync(ownerType, ownerId, ct));
        group.MapPost("/", async (
                [FromForm] string ownerType, [FromForm] Guid ownerId, [FromForm] string? note, [FromForm] bool? approvalStep, IFormFile file,
                IAttachmentService s, CancellationToken ct) =>
            {
                await using var stream = file.OpenReadStream();
                return Results.Ok(await s.UploadAsync(ownerType, ownerId, file.FileName, file.Length, stream, note, ct, approvalStep == true));
            })
            // API dùng JWT trong header, không dùng cookie, nên không có nguy cơ CSRF – tắt kiểm tra antiforgery cho form tải file.
            .DisableAntiforgery();
        group.MapGet("/{id:guid}/content", async (Guid id, IAttachmentService s, CancellationToken ct) =>
            {
                var file = await s.OpenAsync(id, ct);
                // Luôn tải xuống (Content-Disposition: attachment), không mở thẳng trong trang để file không chạy được trong trình duyệt.
                return Results.File(file.Content, file.ContentType, file.FileName);
            });
        group.MapDelete("/{id:guid}", async (Guid id, IAttachmentService s, CancellationToken ct) =>
            {
                await s.DeleteAsync(id, ct);
                return Results.NoContent();
            });

        return app;
    }
}
