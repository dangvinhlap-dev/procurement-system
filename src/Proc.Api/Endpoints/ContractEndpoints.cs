using Proc.Api.Security;
using Proc.Api.Validation;
using Proc.Application.Purchasing;
using Proc.Domain.Organization;

namespace Proc.Api.Endpoints;

/// <summary>API hợp đồng nguyên tắc (lát B4a – design.md mục 18.7).</summary>
public static class ContractEndpoints
{
    /// <summary>Đăng ký các endpoint.</summary>
    /// <param name="app">Ứng dụng.</param>
    /// <returns>Ứng dụng (để nối tiếp).</returns>
    public static IEndpointRouteBuilder MapContractEndpoints(this IEndpointRouteBuilder app)
    {
        var contracts = app.MapGroup("/api/contracts").WithTags("Hợp đồng nguyên tắc");
        contracts.MapGet("/", (IContractService s, CancellationToken ct) => s.ListAsync(ct)).RequirePermission(Permissions.Purchase.ContractView);
        contracts.MapGet("/{id:guid}", (Guid id, IContractService s, CancellationToken ct) => s.GetAsync(id, ct))
            .RequirePermission(Permissions.Purchase.ContractView);
        contracts.MapPost("/", async (SaveContractRequest request, IContractService s, CancellationToken ct) =>
            {
                var created = await s.CreateAsync(request, ct);
                return Results.Created($"/api/contracts/{created.Id}", created);
            })
            .RequirePermission(Permissions.Purchase.ContractManage)
            .Validate<SaveContractRequest>();
        contracts.MapPut("/{id:guid}", (Guid id, SaveContractRequest request, IContractService s, CancellationToken ct) => s.UpdateAsync(id, request, ct))
            .RequirePermission(Permissions.Purchase.ContractManage)
            .Validate<SaveContractRequest>();
        // Hợp đồng phải duyệt mới hiệu lực (quyết định Q4): gửi/rút/chấm dứt/hủy cần quyền sửa, duyệt/từ chối cần quyền duyệt.
        contracts.MapPost("/{id:guid}/submit", (Guid id, IContractService s, CancellationToken ct) => s.SubmitAsync(id, ct))
            .RequirePermission(Permissions.Purchase.ContractManage);
        contracts.MapPost("/{id:guid}/recall", (Guid id, IContractService s, CancellationToken ct) => s.RecallAsync(id, ct))
            .RequirePermission(Permissions.Purchase.ContractManage);
        contracts.MapPost("/{id:guid}/approve", (Guid id, ApprovalActionRequest request, IContractService s, CancellationToken ct) => s.ApproveAsync(id, request, ct))
            .RequirePermission(Permissions.Purchase.ContractApprove);
        contracts.MapPost("/{id:guid}/reject", (Guid id, ApprovalActionRequest request, IContractService s, CancellationToken ct) => s.RejectAsync(id, request, ct))
            .RequirePermission(Permissions.Purchase.ContractApprove);
        contracts.MapPost("/{id:guid}/terminate", (Guid id, TerminateContractRequest request, IContractService s, CancellationToken ct) =>
                s.TerminateAsync(id, request, ct))
            .RequirePermission(Permissions.Purchase.ContractManage);
        contracts.MapPost("/{id:guid}/cancel", (Guid id, IContractService s, CancellationToken ct) => s.CancelAsync(id, ct))
            .RequirePermission(Permissions.Purchase.ContractManage);
        return app;
    }
}
