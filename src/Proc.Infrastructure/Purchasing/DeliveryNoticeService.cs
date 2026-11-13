using Microsoft.EntityFrameworkCore;
using Proc.Application.Common;
using Proc.Application.Purchasing;
using Proc.Domain.Common;
using Proc.Domain.Organization;
using Proc.Domain.Purchasing;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.Purchasing;

/// <summary>
/// Phiếu báo giao NCC ở nội bộ (lát P5 – design.md 19.5, P5-5): tab "Giao hàng NCC" của PO, điền sẵn XNH. Phiếu báo giao có CompanyId nên đi qua
/// filter công ty; quyền xem PO hoặc xem XNH.
/// </summary>
public sealed class DeliveryNoticeService : IDeliveryNoticeService
{
    private readonly ProcDbContext _db;
    private readonly ICurrentUserScope _scope;

    /// <summary>Tạo dịch vụ.</summary>
    /// <param name="db">DbContext đã gắn phạm vi người dùng.</param>
    /// <param name="scope">Phạm vi người dùng.</param>
    public DeliveryNoticeService(ProcDbContext db, ICurrentUserScope scope)
    {
        _db = db;
        _scope = scope;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DeliveryNoticeDto>> ListForOrderAsync(Guid purchaseOrderId, CancellationToken cancellationToken)
    {
        EnsureCanView();
        var order = await _db.PurchaseOrders.AsNoTracking().Include(o => o.Lines).SingleOrDefaultAsync(o => o.Id == purchaseOrderId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy đơn mua hàng.");
        var notices = await _db.PurchaseOrderDeliveryNotices.AsNoTracking().Include(n => n.Lines)
            .Where(n => n.PurchaseOrderId == order.Id).OrderByDescending(n => n.SubmittedAt).ToListAsync(cancellationToken);
        return await ToDtosAsync(order, notices, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<DeliveryNoticeDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        EnsureCanView();
        var notice = await _db.PurchaseOrderDeliveryNotices.AsNoTracking().Include(n => n.Lines).SingleOrDefaultAsync(n => n.Id == id, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy phiếu báo giao.");
        var order = await _db.PurchaseOrders.AsNoTracking().Include(o => o.Lines).SingleAsync(o => o.Id == notice.PurchaseOrderId, cancellationToken);
        return (await ToDtosAsync(order, [notice], cancellationToken)).Single();
    }

    private void EnsureCanView()
    {
        _scope.RequireCompany();
        if (!_scope.HasPermission(Permissions.Purchase.PoView) && !_scope.HasPermission(Permissions.Purchase.ReceiptView))
            throw new ForbiddenAccessException("Bạn không có quyền xem đơn mua hàng hoặc phiếu nhận hàng.");
    }

    private async Task<IReadOnlyList<DeliveryNoticeDto>> ToDtosAsync(
        PurchaseOrder order, IReadOnlyList<PurchaseOrderDeliveryNotice> notices, CancellationToken cancellationToken)
    {
        if (notices.Count == 0) return [];
        var receipts = await DeliveryNoticeQueries.ReceiptsAsync(_db, notices.Select(n => n.Id).ToList(), cancellationToken);
        var itemIds = order.Lines.Select(l => l.ItemId).Distinct().ToList();
        // Tên hàng đi qua filter nhóm hàng như màn hình PO (CLAUDE.md mục 4): mặt hàng ngoài phạm vi để trống tên.
        var items = await _db.Items.AsNoTracking().Where(i => itemIds.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, i => new { i.Code, i.Name }, cancellationToken);
        var uomIds = order.Lines.Select(l => l.UomId).Distinct().ToList();
        var uoms = await _db.Uoms.AsNoTracking().Where(u => uomIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Code, cancellationToken);
        var poLines = order.Lines.ToDictionary(l => l.Id);
        var canCreate = _scope.HasPermission(Permissions.Purchase.ReceiptManage) && order.Status == ApprovalDocumentStatus.Approved;
        return notices.Select(n =>
            {
                var receipt = receipts.GetValueOrDefault(n.Id);
                var lines = n.Lines.OrderBy(l => l.LineNo).Select(l =>
                    {
                        var poLine = poLines[l.PurchaseOrderLineId];
                        var item = items.GetValueOrDefault(poLine.ItemId);
                        return new DeliveryNoticeLineDto(l.LineNo, poLine.Id, poLine.LineNo, item?.Code ?? "", item?.Name ?? "",
                            uoms.GetValueOrDefault(poLine.UomId) ?? "", l.Qty, l.Note);
                    })
                    .ToList();
                return new DeliveryNoticeDto(
                    n.Id, n.No, order.Id, order.No, n.ExpectedDate, n.DeliveryNoteNo, n.Deliverer, n.Note, DeliveryNoticeQueries.StatusOf(n, receipt),
                    n.SubmittedAt, n.SubmittedByEmail, n.CancelledAt, n.CancelReason, receipt?.ReceiptId, receipt?.No,
                    canCreate && n.Status == DeliveryNoticeStatus.Submitted && receipt is null, lines);
            })
            .ToList();
    }
}
