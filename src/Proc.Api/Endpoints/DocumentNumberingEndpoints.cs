using Proc.Api.Security;
using Proc.Application.Common;
using Proc.Application.Documents;
using Proc.Domain.Organization;

namespace Proc.Api.Endpoints;

/// <summary>
/// Endpoint cấu hình đánh số chứng từ tại công ty đang làm việc (design.md mục 5.4) – thuộc nhóm tham số hệ thống.
/// </summary>
public static class DocumentNumberingEndpoints
{
    /// <summary>
    /// Khai báo các endpoint.
    /// </summary>
    /// <param name="app">Bộ định tuyến endpoint.</param>
    /// <returns>Chính <paramref name="app"/>.</returns>
    public static IEndpointRouteBuilder MapDocumentNumberingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/document-numbering").WithTags("Đánh số chứng từ");

        group.MapGet("/", (IDocumentNumberService s, IClock clock, CancellationToken ct) => s.ListAsync(clock.TodayInVietnam(), ct))
            .RequirePermission(Permissions.System.ParameterView);

        group.MapPut("/{docType}", (string docType, UpdateDocumentNumberingRequest request, IDocumentNumberService s, IClock clock, CancellationToken ct) =>
                s.UpdateAsync(docType, request, clock.TodayInVietnam(), ct))
            .RequirePermission(Permissions.System.ParameterManage);

        return app;
    }
}
