using Microsoft.EntityFrameworkCore;
using Proc.Domain.Documents;
using Proc.Domain.Inventory;
using Proc.Domain.Purchasing;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.Purchasing;

/// <summary>SL đã nhận của đơn mua hàng, dùng chung cho dịch vụ PO, ngân sách, phiếu XNH (design.md mục 6.2 bước 4, 19.4).</summary>
internal static class PurchaseOrderReceipts
{
    /// <summary>
    /// SL đã nhận (ĐVT cơ sở) theo dòng PO từ bản 0.22.0 = tổng SL thực nhận (gồm điều chỉnh) của các phiếu xác nhận nhận hàng XNH đã xác nhận
    /// (lát K2, design.md mục 19.4). Phiếu nhập kho cũ không còn tính – phân hệ kho đã tắt. <paramref name="excludeConfirmationId"/>: phiếu
    /// XNH đang sửa/xác nhận (không tính). Bỏ filter công ty ở phiếu vì poLineIds lấy từ PO đã qua filter công ty (báo cáo toàn tập đoàn).
    /// </summary>
    /// <param name="db">DbContext.</param>
    /// <param name="poLineIds">Các dòng PO.</param>
    /// <param name="excludeConfirmationId">Phiếu XNH không tính.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>SL đã nhận theo Id dòng PO (dòng chưa nhận không có trong từ điển).</returns>
    public static async Task<Dictionary<Guid, decimal>> ReceivedBaseByLineAsync(
        ProcDbContext db, IReadOnlyList<Guid> poLineIds, Guid? excludeConfirmationId, CancellationToken cancellationToken) =>
        await (
                from l in db.Set<GoodsReceiptConfirmationLine>().AsNoTracking()
                join c in db.GoodsReceiptConfirmations.IgnoreQueryFilters([ProcDbContext.CompanyFilter]) on l.ConfirmationId equals c.Id
                where poLineIds.Contains(l.PurchaseOrderLineId) && c.Status == ReceiptConfirmationStatus.Confirmed && c.Id != excludeConfirmationId
                group l by l.PurchaseOrderLineId into g
                select new { g.Key, Qty = g.Sum(x => x.ReceivedBaseQty) })
            .ToDictionaryAsync(x => x.Key, x => x.Qty, cancellationToken);

    /// <summary>
    /// (Code kho cũ – chỉ còn đến khi gỡ ở 0.28.0, design.md mục 19.4.)
    /// SL đã nhận <b>thuần</b> (ĐVT cơ sở) theo dòng PO = SL của phiếu nhập mua đã ghi sổ − SL đã trả NCC (phiếu RTV đã ghi sổ của các
    /// phiếu nhập đó). Hàng trả lại không còn tính là đã nhận: PO mở lại để NCC giao bù, hoặc đóng phần còn lại nếu không mua nữa (lát R5,
    /// design.md mục 18.26). Ví dụ đặt 10, nhận 10, trả 3 → đã nhận 7, còn nhận 3. <paramref name="excludeDocumentId"/>: phiếu đang sửa/ghi
    /// sổ (không tính). SL đã nhận của đơn là số của cả công ty, không phụ thuộc kho người xem: bỏ filter kho; filter của dòng phiếu đi
    /// qua phiếu (kèm filter kho) nên bỏ ở dòng, công ty vẫn lọc qua phép nối với phiếu.
    /// </summary>
    public static async Task<Dictionary<Guid, decimal>> LegacyGrnReceivedBaseByLineAsync(
        ProcDbContext db, IReadOnlyList<Guid> poLineIds, Guid? excludeDocumentId, CancellationToken cancellationToken)
    {
        // IgnoreQueryFilters tắt filter công ty cho cả câu truy vấn; an toàn vì poLineIds lấy từ PO đã qua filter công ty.
        var received = await (
                from l in db.InventoryDocumentLines.IgnoreQueryFilters([ProcDbContext.CompanyFilter]).AsNoTracking()
                join d in db.InventoryDocuments.IgnoreQueryFilters([ProcDbContext.WarehouseFilter]) on l.DocumentId equals d.Id
                where l.PurchaseOrderLineId != null && poLineIds.Contains(l.PurchaseOrderLineId.Value)
                      && d.Status == DocumentStatus.Posted && d.Id != excludeDocumentId
                group l by l.PurchaseOrderLineId!.Value into g
                select new { g.Key, Qty = g.Sum(x => x.BaseQty) })
            .ToDictionaryAsync(x => x.Key, x => x.Qty, cancellationToken);
        // Dòng trả trỏ về dòng phiếu nhập gốc (SourceLineId); dòng nhập gốc mang dòng PO.
        var returned = await (
                from r in db.InventoryDocumentLines.IgnoreQueryFilters([ProcDbContext.CompanyFilter]).AsNoTracking()
                join rd in db.InventoryDocuments.IgnoreQueryFilters([ProcDbContext.WarehouseFilter]) on r.DocumentId equals rd.Id
                join g in db.InventoryDocumentLines.IgnoreQueryFilters([ProcDbContext.CompanyFilter]) on r.SourceLineId equals g.Id
                where rd.DocType == DocumentTypes.ReturnToVendor && rd.Status == DocumentStatus.Posted && rd.Id != excludeDocumentId
                      && g.PurchaseOrderLineId != null && poLineIds.Contains(g.PurchaseOrderLineId.Value)
                group r by g.PurchaseOrderLineId!.Value into grp
                select new { grp.Key, Qty = grp.Sum(x => x.BaseQty) })
            .ToDictionaryAsync(x => x.Key, x => x.Qty, cancellationToken);
        foreach (var (lineId, qty) in returned) received[lineId] = received.GetValueOrDefault(lineId) - qty;
        return received;
    }
}
