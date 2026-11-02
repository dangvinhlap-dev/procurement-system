using Proc.Api.Security;
using Proc.Api.Validation;
using Proc.Application.Control;
using Proc.Domain.Organization;

namespace Proc.Api.Endpoints;

/// <summary>
/// Kiểm soát nội bộ – KSNB (design.md mục 18.19): dấu hiệu bất thường, sổ vi phạm. Quyền xem toàn tập đoàn kiểm tra trong dịch vụ.
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

        // Sổ vi phạm: KSNB hoặc lãnh đạo xem; chỉ KSNB ghi nhận, sửa, kết luận.
        var findings = app.MapGroup("/api/control/findings").WithTags("Kiểm soát nội bộ");
        findings.MapGet("/", (IControlFindingService s, CancellationToken ct) => s.ListAsync(ct))
            .RequireAnyPermission(Permissions.Control.AnomalyView, Permissions.Control.FindingReceive);
        findings.MapGet("/{id:guid}", (Guid id, IControlFindingService s, CancellationToken ct) => s.GetAsync(id, ct))
            .RequireAnyPermission(Permissions.Control.AnomalyView, Permissions.Control.FindingReceive);
        findings.MapPost("/", async (SaveControlFindingRequest request, IControlFindingService s, CancellationToken ct) =>
            {
                var created = await s.CreateAsync(request, ct);
                return Results.Created($"/api/control/findings/{created.Id}", created);
            })
            .RequirePermission(Permissions.Control.FindingManage)
            .Validate<SaveControlFindingRequest>();
        findings.MapPut("/{id:guid}", (Guid id, SaveControlFindingRequest request, IControlFindingService s, CancellationToken ct) =>
                s.UpdateAsync(id, request, ct))
            .RequirePermission(Permissions.Control.FindingManage)
            .Validate<SaveControlFindingRequest>();
        findings.MapPost("/{id:guid}/conclude", (Guid id, ConcludeControlFindingRequest request, IControlFindingService s, CancellationToken ct) =>
                s.ConcludeAsync(id, request, ct))
            .RequirePermission(Permissions.Control.FindingManage)
            .Validate<ConcludeControlFindingRequest>();
        return app;
    }
}
