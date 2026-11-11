using Microsoft.EntityFrameworkCore;
using Proc.Application.Common;
using Proc.Domain.Common;
using Proc.Domain.Documents;
using Proc.Domain.Purchasing;
using Proc.Infrastructure.Documents;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.Purchasing;

/// <summary>
/// Hủy dây chuyền PR → PO → phiếu xác nhận nhận hàng (lát Z4b, design.md mục 20.2 D3, 20.2a). Chuỗi dựng từ phiếu được chọn đi xuống các
/// phiếu sau còn hiệu lực: PR → các PO có dòng lấy từ PR (chưa hủy) → các XNH của PO (chưa hủy). Hủy theo thứ tự ngược (XNH → PO → PR) trong
/// một giao dịch, khóa dòng các phiếu để không ai lập phiếu sau xen vào giữa lúc hủy.
/// <para>Người dùng chốt 11/11/2026: chuỗi đã có XNH <b>đã xác nhận</b> (hàng đã về) thì <b>chặn hủy</b> – chỉ điều chỉnh SL thực nhận hoặc
/// đóng phần còn lại; XNH nháp vẫn hủy theo chuỗi.</para>
/// </summary>
internal sealed class CancelChainService : ICancelChainService
{
    private readonly ProcDbContext _db;
    private readonly ICurrentUserScope _scope;
    private readonly ApprovalEngine _approvals;

    /// <summary>Tạo service (ApprovalEngine tự tạo như các service phiếu khác – không đăng ký DI riêng).</summary>
    public CancelChainService(ProcDbContext db, ICurrentUserScope scope, IClock clock)
    {
        _db = db;
        _scope = scope;
        _approvals = new ApprovalEngine(db, scope, clock);
    }

    /// <summary>Chuỗi đã nạp (entity đang theo dõi) theo thứ tự hủy.</summary>
    private sealed record Chain(
        List<GoodsReceiptConfirmation> Receipts, List<PurchaseOrder> Orders, PurchaseRequest? Request, List<string> Warnings);

    /// <inheritdoc />
    public async Task<CancelChainDto> PreviewAsync(string docType, Guid id, CancellationToken cancellationToken)
    {
        var chain = await BuildAsync(docType, id, cancellationToken);
        return ToDto(chain);
    }

    /// <inheritdoc />
    public async Task<CancelChainDto> CancelAsync(string docType, Guid id, CancelChainRequest request, CancellationToken cancellationToken)
    {
        var reason = request.Reason?.Trim();
        if (string.IsNullOrEmpty(reason)) throw new BusinessRuleException("Hãy nhập lý do hủy.");

        // Một giao dịch cho cả chuỗi (D3): một phiếu lỗi thì không hủy phiếu nào. Khóa phiếu gốc trước khi dựng chuỗi để PO/XNH lập cùng lúc
        // (lập PO từ PR, xác nhận nhận hàng) phải chờ – chuỗi dựng ra là đầy đủ.
        await using var transaction = await RowLocks.BeginOrJoinAsync(_db, cancellationToken);
        await LockRootAsync(docType, id, cancellationToken);
        var chain = await BuildAsync(docType, id, cancellationToken);
        await RowLocks.LockAsync<PurchaseOrder>(_db, chain.Orders.Select(o => o.Id), cancellationToken);
        var preview = ToDto(chain);
        if (ConfirmedReceiptsBlock(chain) is { } confirmed) throw new BusinessRuleException(confirmed);
        if (preview.BlockedReason is { } blocked) throw new ForbiddenAccessException(blocked);

        // Chỉ còn XNH nháp trong chuỗi (đã xác nhận thì bị chặn ở trên).
        foreach (var receipt in chain.Receipts) receipt.Cancel(reason);
        foreach (var order in chain.Orders)
        {
            order.Cancel(reason, inChain: true);
            await _approvals.SkipPendingAsync(order, cancellationToken);
        }

        if (chain.Request is { } pr)
        {
            pr.Cancel(reason, inChain: true);
            await _approvals.SkipPendingAsync(pr, cancellationToken);
        }

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return preview;
    }

    /// <summary>Khóa dòng phiếu được chọn (PR/PO/XNH) trong giao dịch đang mở.</summary>
    private Task LockRootAsync(string docType, Guid id, CancellationToken cancellationToken) => Normalize(docType) switch
    {
        DocumentTypes.PurchaseRequest => RowLocks.LockAsync<PurchaseRequest>(_db, [id], cancellationToken),
        DocumentTypes.PurchaseOrder => RowLocks.LockAsync<PurchaseOrder>(_db, [id], cancellationToken),
        _ => Task.CompletedTask,
    };

    /// <summary>Mã loại phiếu viết hoa, bỏ khoảng trắng.</summary>
    private static string Normalize(string docType) => (docType ?? string.Empty).Trim().ToUpperInvariant();

    /// <summary>Dựng chuỗi từ phiếu được chọn đi xuống các phiếu sau còn hiệu lực.</summary>
    private async Task<Chain> BuildAsync(string docType, Guid id, CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        var warnings = new List<string>();
        switch (Normalize(docType))
        {
            case DocumentTypes.PurchaseRequest:
            {
                var pr = await _db.PurchaseRequests.Include(r => r.Lines).SingleOrDefaultAsync(r => r.Id == id, cancellationToken)
                    ?? throw new NotFoundException("Không tìm thấy đề nghị mua hàng.");
                EnsureNotCancelled(pr.Status, pr.No);
                var lineIds = pr.Lines.Select(l => l.Id).ToList();
                var orderIds = await _db.Set<PurchaseOrderLine>()
                    .Where(l => l.SourcePrLineId != null && lineIds.Contains(l.SourcePrLineId.Value)).Select(l => l.OrderId).Distinct()
                    .ToListAsync(cancellationToken);
                var orders = await LoadOrdersAsync(orderIds, cancellationToken);
                await WarnOtherRequestsAsync(orders, pr.Id, warnings, cancellationToken);
                return new Chain(await LoadReceiptsAsync(orders, cancellationToken), orders, pr, warnings);
            }
            case DocumentTypes.PurchaseOrder:
            {
                var orders = await LoadOrdersAsync([id], cancellationToken);
                if (orders.Count == 0)
                {
                    // Không có trong danh sách chưa hủy: hoặc không thấy, hoặc đã hủy.
                    var no = await _db.PurchaseOrders.Where(o => o.Id == id).Select(o => o.No).SingleOrDefaultAsync(cancellationToken)
                             ?? throw new NotFoundException("Không tìm thấy đơn mua hàng.");
                    throw new BusinessRuleException($"Phiếu {no} đã hủy.");
                }

                await WarnOtherRequestsAsync(orders, null, warnings, cancellationToken);
                return new Chain(await LoadReceiptsAsync(orders, cancellationToken), orders, null, warnings);
            }
            case DocumentTypes.GoodsReceiptConfirmation:
            {
                var receipt = await _db.GoodsReceiptConfirmations.SingleOrDefaultAsync(c => c.Id == id, cancellationToken)
                    ?? throw new NotFoundException("Không tìm thấy phiếu xác nhận nhận hàng.");
                if (receipt.Status == ReceiptConfirmationStatus.Cancelled) throw new BusinessRuleException($"Phiếu {receipt.No} đã hủy.");
                return new Chain([receipt], [], null, warnings);
            }
            default:
                throw new BusinessRuleException($"Loại phiếu {docType} chưa hỗ trợ hủy dây chuyền (đợt đầu: PR, PO, phiếu xác nhận nhận hàng).");
        }
    }

    /// <summary>Chặn khi phiếu gốc đã hủy.</summary>
    private static void EnsureNotCancelled(ApprovalDocumentStatus status, string no)
    {
        if (status == ApprovalDocumentStatus.Cancelled) throw new BusinessRuleException($"Phiếu {no} đã hủy.");
    }

    /// <summary>Nạp các PO chưa hủy (kèm dòng) theo Id, thứ tự số phiếu.</summary>
    private async Task<List<PurchaseOrder>> LoadOrdersAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        ids.Count == 0
            ? []
            : await _db.PurchaseOrders.Include(o => o.Lines)
                .Where(o => ids.Contains(o.Id) && o.Status != ApprovalDocumentStatus.Cancelled)
                .OrderByDescending(o => o.No).ToListAsync(cancellationToken);

    /// <summary>Nạp các phiếu xác nhận nhận hàng chưa hủy của các PO, phiếu mới trước (hủy trước).</summary>
    private async Task<List<GoodsReceiptConfirmation>> LoadReceiptsAsync(List<PurchaseOrder> orders, CancellationToken cancellationToken)
    {
        var ids = orders.Select(o => o.Id).ToList();
        return ids.Count == 0
            ? []
            : await _db.GoodsReceiptConfirmations
                .Where(c => ids.Contains(c.PurchaseOrderId) && c.Status != ReceiptConfirmationStatus.Cancelled)
                .OrderByDescending(c => c.No).ToListAsync(cancellationToken);
    }

    /// <summary>
    /// PO gộp dòng từ nhiều PR (D3): hủy PO thì hủy cả PO – các PR khác (ngoài PR đang hủy) không bị hủy nhưng SL của chúng trở về "còn lại
    /// chưa đặt"; hộp thoại nêu rõ để người hủy biết.
    /// </summary>
    private async Task WarnOtherRequestsAsync(List<PurchaseOrder> orders, Guid? exceptRequestId, List<string> warnings, CancellationToken cancellationToken)
    {
        var prLineIds = orders.SelectMany(o => o.Lines).Select(l => l.SourcePrLineId).OfType<Guid>().Distinct().ToList();
        if (prLineIds.Count == 0) return;
        var others = await (
                from l in _db.Set<PurchaseRequestLine>()
                join r in _db.PurchaseRequests on l.RequestId equals r.Id
                where prLineIds.Contains(l.Id) && r.Id != exceptRequestId
                select r.No)
            .Distinct().OrderBy(n => n).ToListAsync(cancellationToken);
        if (others.Count > 0)
            warnings.Add((exceptRequestId is null ? $"PO có dòng lấy từ PR {string.Join(", ", others)}" : $"PO trong chuỗi có dòng lấy từ PR khác: {string.Join(", ", others)}")
                         + " – PR không bị hủy, SL đã đặt trở về \"còn lại chưa đặt\" để lập PO khác.");
    }

    /// <summary>Đổi chuỗi sang DTO kèm kiểm tra quyền từng phiếu (design.md mục 20.2a).</summary>
    private CancelChainDto ToDto(Chain chain)
    {
        var items = new List<CancelChainItemDto>();
        var denied = new List<string>();
        var orderNos = chain.Orders.ToDictionary(o => o.Id, o => o.No);

        void Add(string docType, string typeName, Guid docId, string no, string status, bool isDraft, Guid? createdBy, string? note)
        {
            items.Add(new CancelChainItemDto(items.Count + 1, docType, typeName, docId, no, DocumentRegistryService.StatusName(docType, status), note));
            if (!DocumentCancelPolicy.CanCancel(_scope, isDraft, createdBy)) denied.Add(no);
        }

        foreach (var c in chain.Receipts)
            Add(DocumentTypes.GoodsReceiptConfirmation, "Xác nhận nhận hàng", c.Id, c.No, c.Status.ToString(),
                c.Status == ReceiptConfirmationStatus.Draft, c.CreatedBy, $"Nhận hàng của {orderNos.GetValueOrDefault(c.PurchaseOrderId, "PO")}");
        foreach (var o in chain.Orders)
            Add(DocumentTypes.PurchaseOrder, "Đơn mua hàng", o.Id, o.No, o.Status.ToString(), o.Status == ApprovalDocumentStatus.Draft, o.CreatedBy,
                chain.Request is { } pr ? $"Có dòng lấy từ {pr.No}" : null);
        if (chain.Request is { } request)
            Add(DocumentTypes.PurchaseRequest, "Đề nghị mua hàng", request.Id, request.No, request.Status.ToString(),
                request.Status == ApprovalDocumentStatus.Draft, request.CreatedBy, null);

        var blocked = ConfirmedReceiptsBlock(chain) ?? (denied.Count == 0
            ? null
            : $"Bạn không đủ quyền hủy {string.Join(", ", denied)}: phiếu đã gửi duyệt trở đi chỉ quản trị (quyền Hủy phiếu) hủy được; phiếu nháp " +
              "chỉ người lập (design.md mục 20.2a).");
        return new CancelChainDto(items, chain.Warnings, blocked);
    }

    /// <summary>
    /// Chặn hủy khi chuỗi có XNH đã xác nhận (người dùng chốt 11/11/2026): hàng đã về thì không hủy chứng từ – dùng "Điều chỉnh SL thực nhận"
    /// trên XNH hoặc "Đóng phần còn lại" trên PO.
    /// </summary>
    /// <returns>Câu báo chặn; null khi không có XNH đã xác nhận.</returns>
    private static string? ConfirmedReceiptsBlock(Chain chain)
    {
        var confirmed = chain.Receipts.Where(c => c.Status == ReceiptConfirmationStatus.Confirmed).Select(c => c.No).ToList();
        return confirmed.Count == 0
            ? null
            : $"Chuỗi đã có phiếu nhận hàng đã xác nhận {string.Join(", ", confirmed)} – không hủy được; dùng \"Điều chỉnh SL thực nhận\" trên " +
              "phiếu nhận hàng hoặc \"Đóng phần còn lại\" trên PO.";
    }
}
