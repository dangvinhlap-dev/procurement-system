using Proc.Application.Excel;

namespace Proc.Api.Endpoints;

/// <summary>
/// Nhập/xuất danh mục bằng Excel (design.md mục 5.10). Quyền xem/quản lý kiểm tra trong dịch vụ theo từng danh mục
/// (mỗi danh mục một quyền khác nhau nên không khai báo cố định ở endpoint).
/// </summary>
public static class ExcelEndpoints
{
    /// <summary>Dung lượng file nhập tối đa – 5.000 dòng danh mục chỉ vài trăm KB, 10 MB là dư.</summary>
    private const long MaxFileBytes = 10 * 1024 * 1024;

    private const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    /// <summary>
    /// Khai báo các endpoint.
    /// </summary>
    /// <param name="app">Bộ định tuyến endpoint.</param>
    /// <returns>Chính <paramref name="app"/>.</returns>
    public static IEndpointRouteBuilder MapExcelEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/excel").WithTags("Excel");

        group.MapGet("/{catalog}", async (string catalog, bool? template, IExcelTransfer excel, CancellationToken ct) =>
        {
            var file = await excel.ExportAsync(catalog, template ?? false, ct);
            return Results.File(file.Content, XlsxContentType, file.FileName);
        });

        group.MapPost("/{catalog}/import", async (string catalog, bool? commit, IFormFile file, IExcelTransfer excel, CancellationToken ct) =>
            {
                if (file.Length == 0) return Results.Problem(title: "File rỗng.", statusCode: StatusCodes.Status400BadRequest);
                if (file.Length > MaxFileBytes)
                    return Results.Problem(title: "File quá 10 MB. Hãy chia nhỏ file.", statusCode: StatusCodes.Status400BadRequest);
                await using var stream = file.OpenReadStream();
                return Results.Ok(await excel.ImportAsync(catalog, stream, commit ?? false, ct));
            })
            // API dùng JWT trong header, không dùng cookie, nên không có nguy cơ CSRF – tắt kiểm tra antiforgery cho form tải file.
            .DisableAntiforgery();

        return app;
    }
}
