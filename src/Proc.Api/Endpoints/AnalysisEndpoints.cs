using Proc.Api.Security;
using Proc.Application.Analysis;
using Proc.Domain.Organization;

namespace Proc.Api.Endpoints;

/// <summary>
/// Dữ liệu mua lịch sử (design.md mục 18.1–18.3): nhập Excel, xem, xóa lô, ánh xạ mã cũ → mã chung, phân tích giá hợp nhất. Phạm vi công ty do global query filter và dịch vụ kiểm tra lại.
/// </summary>
public static class AnalysisEndpoints
{
    /// <summary>Dung lượng file nhập tối đa – 50.000 dòng × 25 cột khoảng 5–8 MB, 20 MB là dư (design.md mục 18.1).</summary>
    private const long MaxFileBytes = 20 * 1024 * 1024;

    private const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    /// <summary>
    /// Khai báo các endpoint.
    /// </summary>
    /// <param name="app">Bộ định tuyến endpoint.</param>
    /// <returns>Chính <paramref name="app"/>.</returns>
    public static IEndpointRouteBuilder MapAnalysisEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/purchase-history").WithTags("Dữ liệu mua lịch sử");

        group.MapGet("/", (Guid? batchId, DateOnly? from, DateOnly? to, string? match, string? search, IPurchaseHistoryService s, CancellationToken ct) =>
                s.ListAsync(new PurchaseHistoryQuery(batchId, from, to, match, search), ct))
            .RequirePermission(Permissions.Analysis.PurchaseHistoryView);
        group.MapGet("/batches", (IPurchaseHistoryService s, CancellationToken ct) => s.ListBatchesAsync(ct))
            .RequirePermission(Permissions.Analysis.PurchaseHistoryView);
        group.MapDelete("/batches/{id:guid}", async (Guid id, IPurchaseHistoryService s, CancellationToken ct) =>
            {
                await s.DeleteBatchAsync(id, ct);
                return Results.NoContent();
            })
            .RequirePermission(Permissions.Analysis.PurchaseHistoryImport);
        group.MapGet("/template", (IPurchaseHistoryService s) =>
            {
                var file = s.GetTemplate();
                return Results.File(file.Content, XlsxContentType, file.FileName);
            })
            .RequirePermission(Permissions.Analysis.PurchaseHistoryImport);
        group.MapPost("/import", async (bool? commit, IFormFile file, IPurchaseHistoryService s, CancellationToken ct) =>
            {
                if (file.Length == 0) return Results.Problem(title: "File rỗng.", statusCode: StatusCodes.Status400BadRequest);
                if (file.Length > MaxFileBytes)
                    return Results.Problem(title: "File quá 20 MB. Hãy chia nhỏ file (ví dụ theo năm).", statusCode: StatusCodes.Status400BadRequest);
                await using var stream = file.OpenReadStream();
                return Results.Ok(await s.ImportAsync(file.FileName, stream, commit ?? false, ct));
            })
            .RequirePermission(Permissions.Analysis.PurchaseHistoryImport)
            // API dùng JWT trong header, không dùng cookie, nên không có nguy cơ CSRF – tắt kiểm tra antiforgery cho form tải file.
            .DisableAntiforgery();

        // Ánh xạ mã cũ → mã chung (design.md mục 18.2): xem cần quyền xem, lưu/bỏ ánh xạ cần quyền nhập (cùng người lo dữ liệu này).
        group.MapGet("/sources", (string kind, IPurchaseHistoryMappingService s, CancellationToken ct) => s.ListSourcesAsync(kind, ct))
            .RequirePermission(Permissions.Analysis.PurchaseHistoryView);
        group.MapGet("/candidates", (string kind, string key, string? search, IPurchaseHistoryMappingService s, CancellationToken ct) =>
                s.ListCandidatesAsync(kind, key, search, ct))
            .RequirePermission(Permissions.Analysis.PurchaseHistoryView);
        group.MapPut("/mappings", (SavePurchaseHistoryMappingRequest request, IPurchaseHistoryMappingService s, CancellationToken ct) =>
                s.SaveAsync(request, ct))
            .RequirePermission(Permissions.Analysis.PurchaseHistoryImport);
        group.MapDelete("/mappings", (string kind, string key, IPurchaseHistoryMappingService s, CancellationToken ct) =>
                s.DeleteAsync(kind, key, ct))
            .RequirePermission(Permissions.Analysis.PurchaseHistoryImport);

        // Phân tích giá hợp nhất (design.md mục 18.3): chỉ đọc; xem toàn tập đoàn do dịch vụ kiểm thêm quyền ViewAllCompanies.
        group.MapGet("/analysis", (DateOnly? from, DateOnly? to, decimal? targetDiscountPct, bool? allCompanies, IPurchaseAnalysisService s,
                    CancellationToken ct) =>
                s.AnalyzeAsync(new PurchaseAnalysisQuery(from, to, targetDiscountPct, allCompanies ?? false), ct))
            .RequirePermission(Permissions.Analysis.PurchaseHistoryView);

        return app;
    }
}
