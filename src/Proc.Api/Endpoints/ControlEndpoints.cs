using Proc.Api.Security;
using Proc.Application.Control;
using Proc.Domain.Organization;

namespace Proc.Api.Endpoints;

/// <summary>
/// Kiểm soát nội bộ – KSNB (design.md mục 18.19): dấu hiệu bất thường. Quyền xem toàn tập đoàn kiểm tra trong dịch vụ.
/// </summary>
public static class ControlEndpoints
{
    /// <summary>
    /// Khai báo các endpoint.
    /// </summary>
    /// <param name="app">Bộ định tuyến endpoint.</param>
    /// <returns>Chính <paramref name="app"/>.</returns>
    public static IEndpointRouteBuilder MapControlEndpoints(this IEndpointRouteBuilder app)
    {
        var control = app.MapGroup("/api/control").WithTags("Kiểm soát nội bộ");
        control.MapGet("/anomalies", (DateOnly from, DateOnly to, bool? allCompanies, decimal? priceGapPct, decimal? nearThresholdPct,
                    int? nearThresholdCount, decimal? offContractPct, IControlAnomalyService s, CancellationToken ct) =>
                s.GetAsync(new AnomalyQuery(from, to, allCompanies ?? false, priceGapPct ?? 10m, nearThresholdPct ?? 10m, nearThresholdCount ?? 3,
                    offContractPct ?? 5m), ct))
            .RequirePermission(Permissions.Control.AnomalyView);
        return app;
    }
}
