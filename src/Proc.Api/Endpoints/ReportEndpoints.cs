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

        reports.MapGet("/pending-requests", (IPurchaseReportService s, CancellationToken ct) => s.GetPendingRequestsAsync(ct))
            .RequirePermission(Permissions.Report.PurchaseView);
        reports.MapGet("/purchase-spend", (DateOnly from, DateOnly to, string? groupBy, bool? allCompanies, IPurchaseReportService s, CancellationToken ct) =>
                s.GetPurchaseSpendAsync(new PurchaseReportPeriod(from, to, allCompanies ?? false), groupBy ?? PurchaseSpendGroups.Supplier, ct))
            .RequirePermission(Permissions.Report.PurchaseView);
        reports.MapGet("/supplier-evaluation", (DateOnly from, DateOnly to, bool? allCompanies, IPurchaseReportService s, CancellationToken ct) =>
                s.GetSupplierEvaluationAsync(new PurchaseReportPeriod(from, to, allCompanies ?? false), ct))
            .RequirePermission(Permissions.Report.PurchaseView);
        reports.MapGet("/sourcing-savings", (DateOnly from, DateOnly to, bool? allCompanies, IPurchaseReportService s, CancellationToken ct) =>
                s.GetSourcingSavingsAsync(new PurchaseReportPeriod(from, to, allCompanies ?? false), ct))
            .RequirePermission(Permissions.Report.PurchaseView);

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
