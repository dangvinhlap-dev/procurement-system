using Proc.Application.Common;
using Proc.Application.MasterData;
using Proc.Application.Pricing;
using Proc.Application.Purchasing;
using Proc.Application.Sourcing;
using Proc.Domain.Common;
using Proc.Domain.Documents;
using Proc.Domain.Organization;
using Proc.Domain.Purchasing;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.Purchasing;

/// <summary>
/// Xem trước chuỗi duyệt (yêu cầu người dùng 03/11/2026): chạy thử lệnh gửi duyệt thật của loại phiếu trong một giao dịch rồi luôn hủy
/// (rollback) – không viết lại logic chọn cấp nên không thể lệch với khi gửi thật. Các dịch vụ gửi duyệt dùng chung DbContext của request
/// nên nằm trong giao dịch này; thông báo chờ duyệt sinh ra trong lúc chạy thử cũng bị hủy cùng.
/// </summary>
public sealed class ApprovalPreviewService : IApprovalPreviewService
{
    private readonly ProcDbContext _db;
    private readonly ICurrentUserScope _scope;
    private readonly IPurchaseRequestService _requests;
    private readonly IPurchaseOrderService _orders;
    private readonly IPriceListService _priceLists;
    private readonly IRfqService _rfqs;
    private readonly IContractService _contracts;
    private readonly IContractAmendmentService _amendments;
    private readonly ISoleSourceService _soleSources;

    /// <summary>Tạo dịch vụ.</summary>
    public ApprovalPreviewService(
        ProcDbContext db, ICurrentUserScope scope, IPurchaseRequestService requests, IPurchaseOrderService orders, IPriceListService priceLists,
        IRfqService rfqs, IContractService contracts, IContractAmendmentService amendments, ISoleSourceService soleSources)
    {
        _db = db;
        _scope = scope;
        _requests = requests;
        _orders = orders;
        _priceLists = priceLists;
        _rfqs = rfqs;
        _contracts = contracts;
        _amendments = amendments;
        _soleSources = soleSources;
    }

    /// <inheritdoc />
    public async Task<ApprovalPreviewDto> PreviewSubmitAsync(ApprovalPreviewRequest request, CancellationToken cancellationToken)
    {
        var docType = (request.DocType ?? string.Empty).Trim().ToUpperInvariant();
        // Quyền như endpoint gửi duyệt của từng loại (endpoint chung không gắn được quyền theo loại).
        _scope.EnsurePermission(docType switch
        {
            DocumentTypes.PurchaseRequest => Permissions.Purchase.PrCreate,
            DocumentTypes.PurchaseOrder => Permissions.Purchase.PoCreate,
            DocumentTypes.PriceList => Permissions.Pricing.PriceListManage,
            DocumentTypes.RequestForQuotation => Permissions.Sourcing.RfqAward,
            DocumentTypes.Contract or ApprovalTypes.ContractAmendment => Permissions.Purchase.ContractManage,
            DocumentTypes.SoleSource => Permissions.MasterData.SoleSourceManage,
            _ => throw new BusinessRuleException($"Loại phiếu {docType} không xem trước chuỗi duyệt được."),
        });

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var (_, steps) = docType switch
            {
                DocumentTypes.PurchaseRequest => Result(await _requests.SubmitAsync(request.DocumentId, cancellationToken) is var pr ? (pr.Status, pr.Approvals) : default),
                DocumentTypes.PurchaseOrder => Result(await _orders.SubmitAsync(request.DocumentId, cancellationToken) is var po ? (po.Status, po.Approvals) : default),
                DocumentTypes.PriceList => Result(await _priceLists.SubmitAsync(request.DocumentId, cancellationToken) is var pl ? (pl.Status, pl.Approvals) : default),
                DocumentTypes.RequestForQuotation => Result(await _rfqs.AwardAsync(request.DocumentId,
                    request.Award ?? throw new BusinessRuleException("Chưa chọn NCC trúng thầu."), cancellationToken) is var rfq ? (rfq.Status, rfq.Approvals) : default),
                DocumentTypes.Contract => Result(await _contracts.SubmitAsync(request.DocumentId, cancellationToken) is var ct ? (ct.Status, ct.Approvals) : default),
                ApprovalTypes.ContractAmendment => Result(await _amendments.SubmitAmendmentAsync(request.DocumentId, cancellationToken) is var am ? (am.Status, am.Approvals) : default),
                _ => Result(await _soleSources.SubmitAsync(request.DocumentId, cancellationToken) is var ss ? (ss.Status, ss.Approvals) : default),
            };
            // Lượt vừa gửi thử là lượt lớn nhất; không có bước nào ở lượt đó = dưới mọi ngưỡng, gửi là duyệt ngay.
            var round = steps.Count == 0 ? 0 : steps.Max(s => s.Round);
            var current = steps.Where(s => s.Round == round && s.Status == "Pending").ToList();
            return new ApprovalPreviewDto(current.Count == 0, current);
        }
        finally
        {
            await transaction.RollbackAsync(cancellationToken);
            _db.ChangeTracker.Clear();
        }
    }

    private static (string Status, IReadOnlyList<ApprovalStepDto> Steps) Result((string Status, IReadOnlyList<ApprovalStepDto> Steps) value) => value;
}
