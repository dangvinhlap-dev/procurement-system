using Proc.Api.Security;
using Proc.Api.Validation;
using Proc.Application.Purchasing;
using Proc.Domain.Organization;

namespace Proc.Api.Endpoints;

/// <summary>API hợp đồng nguyên tắc (lát B4a – design.md mục 18.7).</summary>
public static class ContractEndpoints
{
    /// <summary>Dung lượng tối đa file nhập Excel (như nhập danh mục).</summary>
    private const long MaxFileBytes = 10 * 1024 * 1024;

    private const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

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

        // Nhập Excel hợp đồng hiện hữu (lát B4c): nhập thành hợp đồng nháp của công ty đang làm việc, quyền như lập hợp đồng.
        contracts.MapGet("/import-template", (IContractImportService s) =>
            {
                var file = s.GetTemplate();
                return Results.File(file.Content, XlsxContentType, file.FileName);
            })
            .RequirePermission(Permissions.Purchase.ContractManage);
        contracts.MapPost("/import", async (bool? commit, IFormFile file, IContractImportService s, CancellationToken ct) =>
            {
                if (file.Length == 0) return Results.Problem(title: "File rỗng.", statusCode: StatusCodes.Status400BadRequest);
                if (file.Length > MaxFileBytes)
                    return Results.Problem(title: "File quá 10 MB. Hãy chia nhỏ file.", statusCode: StatusCodes.Status400BadRequest);
                await using var stream = file.OpenReadStream();
                return Results.Ok(await s.ImportAsync(stream, commit ?? false, ct));
            })
            .RequirePermission(Permissions.Purchase.ContractManage)
            // API dùng JWT trong header, không dùng cookie, nên không có nguy cơ CSRF – tắt kiểm tra antiforgery cho form tải file.
            .DisableAntiforgery();

        // Chiết khấu doanh số/tài trợ thực nhận (lát F4b): xem theo quyền xem hợp đồng; ghi nhận/hủy theo quyền sửa hợp đồng.
        contracts.MapGet("/{id:guid}/rebates", (Guid id, IContractRebateService s, CancellationToken ct) => s.GetTrackingAsync(id, ct))
            .RequirePermission(Permissions.Purchase.ContractView);
        contracts.MapPost("/{id:guid}/rebate-receipts", (Guid id, SaveContractRebateReceiptRequest request, IContractRebateService s, CancellationToken ct) =>
                s.RecordAsync(id, request, ct))
            .RequirePermission(Permissions.Purchase.ContractManage)
            .Validate<SaveContractRebateReceiptRequest>();
        app.MapPost("/api/contract-rebate-receipts/{id:guid}/cancel",
                (Guid id, CancelContractRebateReceiptRequest request, IContractRebateService s, CancellationToken ct) => s.CancelReceiptAsync(id, request, ct))
            .WithTags("Hợp đồng nguyên tắc")
            .RequirePermission(Permissions.Purchase.ContractManage);

        // Phụ lục hợp đồng (lát B4b): lập trên hợp đồng hiệu lực, mỗi phụ lục phải duyệt; quyền như hợp đồng.
        contracts.MapPost("/{id:guid}/amendments", async (Guid id, SaveContractAmendmentRequest request, IContractAmendmentService s, CancellationToken ct) =>
            {
                var created = await s.CreateAmendmentAsync(id, request, ct);
                return Results.Created($"/api/contract-amendments/{created.Id}", created);
            })
            .RequirePermission(Permissions.Purchase.ContractManage)
            .Validate<SaveContractAmendmentRequest>();
        var amendments = app.MapGroup("/api/contract-amendments").WithTags("Hợp đồng nguyên tắc");
        amendments.MapGet("/{id:guid}", (Guid id, IContractAmendmentService s, CancellationToken ct) => s.GetAmendmentAsync(id, ct))
            .RequirePermission(Permissions.Purchase.ContractView);
        amendments.MapPut("/{id:guid}", (Guid id, SaveContractAmendmentRequest request, IContractAmendmentService s, CancellationToken ct) =>
                s.UpdateAmendmentAsync(id, request, ct))
            .RequirePermission(Permissions.Purchase.ContractManage)
            .Validate<SaveContractAmendmentRequest>();
        amendments.MapPost("/{id:guid}/submit", (Guid id, IContractAmendmentService s, CancellationToken ct) => s.SubmitAmendmentAsync(id, ct))
            .RequirePermission(Permissions.Purchase.ContractManage);
        amendments.MapPost("/{id:guid}/recall", (Guid id, IContractAmendmentService s, CancellationToken ct) => s.RecallAmendmentAsync(id, ct))
            .RequirePermission(Permissions.Purchase.ContractManage);
        amendments.MapPost("/{id:guid}/cancel", (Guid id, IContractAmendmentService s, CancellationToken ct) => s.CancelAmendmentAsync(id, ct))
            .RequirePermission(Permissions.Purchase.ContractManage);
        amendments.MapPost("/{id:guid}/approve", (Guid id, ApprovalActionRequest request, IContractAmendmentService s, CancellationToken ct) =>
                s.ApproveAmendmentAsync(id, request, ct))
            .RequirePermission(Permissions.Purchase.ContractApprove);
        amendments.MapPost("/{id:guid}/reject", (Guid id, ApprovalActionRequest request, IContractAmendmentService s, CancellationToken ct) =>
                s.RejectAmendmentAsync(id, request, ct))
            .RequirePermission(Permissions.Purchase.ContractApprove);
        return app;
    }
}
