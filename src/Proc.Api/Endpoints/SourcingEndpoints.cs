using Proc.Api.Security;
using Proc.Api.Validation;
using Proc.Application.Sourcing;
using Proc.Domain.Organization;

namespace Proc.Api.Endpoints;

/// <summary>API tìm nguồn, RFQ, báo giá, chọn thầu (design.md mục 8.3, 8.4).</summary>
public static class SourcingEndpoints
{
    /// <summary>Đăng ký các endpoint.</summary>
    /// <param name="app">Ứng dụng.</param>
    /// <returns>Ứng dụng (để nối tiếp).</returns>
    public static IEndpointRouteBuilder MapSourcingEndpoints(this IEndpointRouteBuilder app)
    {
        var sourcing = app.MapGroup("/api/sourcing").WithTags("Tìm nguồn");
        sourcing.MapGet("/", (ISourcingService s, CancellationToken ct) => s.ListAsync(ct)).RequirePermission(Permissions.Sourcing.RfqView);
        sourcing.MapGet("/{id:guid}", (Guid id, ISourcingService s, CancellationToken ct) => s.GetAsync(id, ct)).RequirePermission(Permissions.Sourcing.RfqView);
        sourcing.MapPost("/", async (SaveSourcingRequest request, ISourcingService s, CancellationToken ct) =>
            {
                var created = await s.CreateAsync(request, ct);
                return Results.Created($"/api/sourcing/{created.Id}", created);
            })
            .RequirePermission(Permissions.Sourcing.RfqManage)
            .Validate<SaveSourcingRequest>();
        sourcing.MapPut("/{id:guid}", (Guid id, SaveSourcingRequest request, ISourcingService s, CancellationToken ct) => s.UpdateAsync(id, request, ct))
            .RequirePermission(Permissions.Sourcing.RfqManage)
            .Validate<SaveSourcingRequest>();
        sourcing.MapPost("/{id:guid}/close", (Guid id, ISourcingService s, CancellationToken ct) => s.CloseAsync(id, ct))
            .RequirePermission(Permissions.Sourcing.RfqManage);
        sourcing.MapPost("/{id:guid}/cancel", (Guid id, ISourcingService s, CancellationToken ct) => s.CancelAsync(id, ct))
            .RequirePermission(Permissions.Sourcing.RfqManage);

        var rfq = app.MapGroup("/api/rfqs").WithTags("Yêu cầu báo giá");
        rfq.MapGet("/", (string? status, IRfqService s, CancellationToken ct) => s.ListAsync(status, ct)).RequirePermission(Permissions.Sourcing.RfqView);
        rfq.MapGet("/{id:guid}", (Guid id, IRfqService s, CancellationToken ct) => s.GetAsync(id, ct)).RequirePermission(Permissions.Sourcing.RfqView);
        rfq.MapPost("/", async (SaveRfqRequest request, IRfqService s, CancellationToken ct) =>
            {
                var created = await s.CreateAsync(request, ct);
                return Results.Created($"/api/rfqs/{created.Id}", created);
            })
            .RequirePermission(Permissions.Sourcing.RfqManage)
            .Validate<SaveRfqRequest>();
        rfq.MapPut("/{id:guid}", (Guid id, SaveRfqRequest request, IRfqService s, CancellationToken ct) => s.UpdateAsync(id, request, ct))
            .RequirePermission(Permissions.Sourcing.RfqManage)
            .Validate<SaveRfqRequest>();
        rfq.MapPost("/{id:guid}/send", (Guid id, IRfqService s, CancellationToken ct) => s.SendAsync(id, ct))
            .RequirePermission(Permissions.Sourcing.RfqManage);
        rfq.MapPut("/{id:guid}/quotations/{supplierId:guid}", (Guid id, Guid supplierId, SaveQuotationRequest request, IRfqService s, CancellationToken ct) =>
                s.SaveQuotationAsync(id, supplierId, request, ct))
            .RequirePermission(Permissions.Sourcing.RfqManage)
            .Validate<SaveQuotationRequest>();
        rfq.MapPost("/{id:guid}/award", (Guid id, AwardRfqRequest request, IRfqService s, CancellationToken ct) => s.AwardAsync(id, request, ct))
            .RequirePermission(Permissions.Sourcing.RfqAward);
        // Sinh PO cần cả quyền lập PO (kiểm tra thêm trong dịch vụ PO qua endpoint này).
        rfq.MapPost("/{id:guid}/create-orders", (Guid id, IRfqService s, CancellationToken ct) => s.CreateOrdersAsync(id, ct))
            .RequirePermission(Permissions.Purchase.PoCreate);
        rfq.MapPost("/{id:guid}/cancel", (Guid id, IRfqService s, CancellationToken ct) => s.CancelAsync(id, ct))
            .RequirePermission(Permissions.Sourcing.RfqManage);
        return app;
    }
}
