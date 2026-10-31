using Proc.Api.Security;
using Proc.Api.Validation;
using Proc.Application.Excel;
using Proc.Application.Reports;
using Proc.Domain.Organization;

namespace Proc.Api.Endpoints;

/// <summary>
/// Báo cáo (design.md mục 11, 11.2) và xuất Excel lưới đang xem. Phạm vi công ty, kho, nhóm hàng do global query filter; quyền xem
/// toàn tập đoàn kiểm tra trong dịch vụ.
/// </summary>
public static class ReportEndpoints
{
    private const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    /// <summary>
    /// Khai báo các endpoint.
    /// </summary>
    /// <param name="app">Bộ định tuyến endpoint.</param>
    /// <returns>Chính <paramref name="app"/>.</returns>
    public static IEndpointRouteBuilder MapReportEndpoints(this IEndpointRouteBuilder app)
    {
        var reports = app.MapGroup("/api/reports").WithTags("Báo cáo");
        reports.MapGet("/slow-moving", (Guid? warehouseId, Guid? categoryId, int? days, IStockReportService s, CancellationToken ct) =>
                s.GetSlowMovingAsync(new SlowMovingQuery(warehouseId, categoryId, days ?? 90), ct))
            .RequirePermission(Permissions.Report.StockView);
        reports.MapGet("/expiring", (Guid? warehouseId, Guid? categoryId, int? days, IStockReportService s, CancellationToken ct) =>
                s.GetExpiringAsync(new ExpiringQuery(warehouseId, categoryId, days ?? 30), ct))
            .RequirePermission(Permissions.Report.StockView);
        reports.MapGet("/group-stock", (Guid itemId, IStockReportService s, CancellationToken ct) => s.GetGroupStockAsync(itemId, ct))
            .RequirePermission(Permissions.Report.StockView);

        // Xuất Excel lưới đang xem: chỉ định dạng lại dữ liệu người dùng đã nhận qua các API có kiểm quyền, không đọc CSDL –
        // nên chỉ cần đăng nhập (chính sách mặc định).
        app.MapPost("/api/excel/grid", (GridExportRequest request, IGridExcelExporter exporter) =>
            {
                var file = exporter.Build(request);
                return Results.File(file.Content, XlsxContentType, file.FileName);
            })
            .WithTags("Excel")
            .Validate<GridExportRequest>();

        return app;
    }
}
