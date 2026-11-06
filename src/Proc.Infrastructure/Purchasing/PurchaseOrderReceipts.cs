using Microsoft.EntityFrameworkCore;
using Proc.Domain.Documents;
using Proc.Domain.Inventory;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.Purchasing;

/// <summary>SL đã nhận của đơn mua hàng, dùng chung cho dịch vụ PO và phiếu nhập mua theo PO (design.md mục 6.2 bước 4).</summary>
internal static class PurchaseOrderReceipts
{
    /// <summary>
    /// SL đã nhận <b>thuần</b> (ĐVT cơ sở) theo dòng PO = SL của phiếu nhập mua đã ghi sổ − SL đã trả NCC (phiếu RTV đã ghi sổ của các
    /// phiếu nhập đó). Hàng trả lại không còn tính là đã nhận: PO mở lại để NCC giao bù, hoặc đóng phần còn lại nếu không mua nữa (lát R5,
    /// design.md mục 18.26). Ví dụ đặt 10, nhận 10, trả 3 → đã nhận 7, còn nhận 3. <paramref name="excludeDocumentId"/>: phiếu đang sửa/ghi
    /// sổ (không tính). SL đã nhận của đơn là số của cả công ty, không phụ thuộc kho người xem: bỏ filter kho; filter của dòng phiếu đi
    /// qua phiếu (kèm filter kho) nên bỏ ở dòng, công ty vẫn lọc qua phép nối với phiếu.
    /// </summary>
    public static async Task<Dictionary<Guid, decimal>> ReceivedBaseByLineAsync(
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
