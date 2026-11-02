using Proc.Application.Documents;

namespace Proc.Api.Endpoints;

/// <summary>
/// Endpoint Tra cứu chứng từ (design.md mục 18.22). Ai đăng nhập cũng gọi được; dịch vụ chỉ trả các loại chứng từ người dùng có quyền xem,
/// trong phạm vi công ty/kho của họ.
/// </summary>
public static class DocumentRegistryEndpoints
{
    /// <summary>
    /// Khai báo các endpoint.
    /// </summary>
    /// <param name="app">Bộ định tuyến endpoint.</param>
    /// <returns>Chính <paramref name="app"/>.</returns>
    public static IEndpointRouteBuilder MapDocumentRegistryEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/document-registry",
                (DateOnly from, DateOnly to, string[]? docTypes, DocumentProgress? progress, string? mine, IDocumentRegistryService s,
                    CancellationToken ct) =>
                    s.SearchAsync(new DocumentRegistryQuery(from, to, docTypes ?? [], progress, mine), ct))
            .WithTags("Tra cứu chứng từ")
            .RequireAuthorization();
        return app;
    }
}
