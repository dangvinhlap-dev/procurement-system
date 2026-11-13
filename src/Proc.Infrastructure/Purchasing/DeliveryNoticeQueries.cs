using Microsoft.EntityFrameworkCore;
using Proc.Domain.Purchasing;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.Purchasing;

/// <summary>
/// Truy vấn dùng chung của phiếu báo giao NCC (lát P5, design.md 19.5) cho Portal và nội bộ: XNH lập theo phiếu, trạng thái hiển thị, SL còn báo
/// giao được. Bỏ mọi filter: phiên Portal không có công ty / phạm vi; người gọi đã kiểm PO thuộc NCC của phiên hoặc qua filter công ty.
/// </summary>
internal static class DeliveryNoticeQueries
{
    /// <summary>XNH chưa hủy lập theo một phiếu báo giao.</summary>
    /// <param name="NoticeId">Phiếu báo giao.</param>
    /// <param name="ReceiptId">XNH.</param>
    /// <param name="No">Số XNH.</param>
    /// <param name="Status">Trạng thái XNH (Nháp / Đã xác nhận).</param>
    /// <param name="Date">Ngày nhận trên XNH.</param>
    internal sealed record ReceiptLink(Guid NoticeId, Guid ReceiptId, string No, ReceiptConfirmationStatus Status, DateOnly Date);

    /// <summary>XNH chưa hủy theo từng phiếu báo giao (mỗi phiếu tối đa một – chỉ mục duy nhất có điều kiện).</summary>
    /// <param name="db">DbContext.</param>
    /// <param name="noticeIds">Các phiếu báo giao.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Phiếu báo giao → XNH.</returns>
    internal static async Task<Dictionary<Guid, ReceiptLink>> ReceiptsAsync(
        ProcDbContext db, IReadOnlyCollection<Guid> noticeIds, CancellationToken cancellationToken) =>
        await db.GoodsReceiptConfirmations.AsNoTracking().IgnoreQueryFilters()
            .Where(c => c.DeliveryNoticeId != null && noticeIds.Contains(c.DeliveryNoticeId.Value) && c.Status != ReceiptConfirmationStatus.Cancelled)
            .Select(c => new ReceiptLink(c.DeliveryNoticeId!.Value, c.Id, c.No, c.Status, c.Date))
            .ToDictionaryAsync(c => c.NoticeId, cancellationToken);

    /// <summary>Trạng thái hiển thị (P5-2): Cancelled; có XNH đã xác nhận → Received; có XNH nháp → Receiving; còn lại Submitted.</summary>
    /// <param name="notice">Phiếu báo giao.</param>
    /// <param name="receipt">XNH chưa hủy lập theo phiếu (nếu có).</param>
    /// <returns>Mã trạng thái.</returns>
    internal static string StatusOf(PurchaseOrderDeliveryNotice notice, ReceiptLink? receipt) =>
        notice.Status == DeliveryNoticeStatus.Cancelled ? "Cancelled"
        : receipt is null ? "Submitted"
        : receipt.Status == ReceiptConfirmationStatus.Confirmed ? "Received" : "Receiving";

    /// <summary>
    /// SL đã nhận và SL còn báo giao được theo dòng PO, theo ĐVT dòng PO (P5-1): còn báo giao = SL đặt × (1 + dung sai %) − SL thực nhận của
    /// XNH đã xác nhận − SL trên phiếu báo giao chưa hủy mà chưa có XNH đã xác nhận (đang chờ nhận / đang kiểm nhận), không âm. Ví dụ đặt 100,
    /// dung sai 5%, đã nhận 60, đang báo giao 30 → còn báo giao 15.
    /// </summary>
    /// <param name="db">DbContext.</param>
    /// <param name="order">PO (đã nạp dòng).</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Dòng PO → (đã nhận, còn báo giao được).</returns>
    internal static async Task<Dictionary<Guid, (decimal Received, decimal Deliverable)>> QuantitiesAsync(
        ProcDbContext db, PurchaseOrder order, CancellationToken cancellationToken)
    {
        var lineIds = order.Lines.Select(l => l.Id).ToList();
        var received = await (
                from l in db.Set<GoodsReceiptConfirmationLine>().AsNoTracking().IgnoreQueryFilters()
                join c in db.GoodsReceiptConfirmations.IgnoreQueryFilters() on l.ConfirmationId equals c.Id
                where lineIds.Contains(l.PurchaseOrderLineId) && c.Status == ReceiptConfirmationStatus.Confirmed
                group l by l.PurchaseOrderLineId into g
                select new { g.Key, Qty = g.Sum(x => x.ReceivedQty) })
            .ToDictionaryAsync(x => x.Key, x => x.Qty, cancellationToken);
        var pending = await (
                from l in db.Set<PurchaseOrderDeliveryNoticeLine>().AsNoTracking().IgnoreQueryFilters()
                join n in db.PurchaseOrderDeliveryNotices.IgnoreQueryFilters() on l.NoticeId equals n.Id
                where n.PurchaseOrderId == order.Id && n.Status == DeliveryNoticeStatus.Submitted
                      && !db.GoodsReceiptConfirmations.IgnoreQueryFilters()
                          .Any(c => c.DeliveryNoticeId == n.Id && c.Status == ReceiptConfirmationStatus.Confirmed)
                group l by l.PurchaseOrderLineId into g
                select new { g.Key, Qty = g.Sum(x => x.Qty) })
            .ToDictionaryAsync(x => x.Key, x => x.Qty, cancellationToken);
        return order.Lines.ToDictionary(l => l.Id, l =>
        {
            var allowed = decimal.Round(l.Qty * (100m + order.ReceiveTolerancePct) / 100m, 4, MidpointRounding.AwayFromZero);
            var got = received.GetValueOrDefault(l.Id);
            return (got, Math.Max(0, allowed - got - pending.GetValueOrDefault(l.Id)));
        });
    }
}
