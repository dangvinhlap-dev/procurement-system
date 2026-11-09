using Proc.Application.Documents;

namespace Proc.Api.Endpoints;

/// <summary>
/// Ý kiến thẩm định của thành viên chuyên môn (lát H1 – design.md mục 18.25). Không gắn RequirePermission vì quyền xem đi theo loại chủ (GTH,
/// RFQ, hợp đồng, NCC) – dịch vụ kiểm tra; endpoint chỉ cần đăng nhập.
/// </summary>
public static class AppraisalOpinionEndpoints
{
    /// <summary>Khai báo các endpoint.</summary>
    /// <param name="app">Bộ định tuyến endpoint.</param>
    /// <returns>Chính <paramref name="app"/>.</returns>
    public static IEndpointRouteBuilder MapAppraisalOpinionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/appraisal-opinions").WithTags("Ý kiến thẩm định");
        group.MapGet("/", (string ownerType, Guid ownerId, IAppraisalOpinionService s, CancellationToken ct) => s.ListAsync(ownerType, ownerId, ct));
        group.MapPost("/", (GiveAppraisalOpinionRequest request, IAppraisalOpinionService s, CancellationToken ct) => s.GiveAsync(request, ct));
        group.MapPost("/{id:guid}/withdraw", (Guid id, WithdrawAppraisalOpinionRequest request, IAppraisalOpinionService s, CancellationToken ct) =>
            s.WithdrawAsync(id, request, ct));
        return app;
    }
}
