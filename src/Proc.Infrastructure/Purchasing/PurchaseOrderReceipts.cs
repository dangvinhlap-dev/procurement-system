using Microsoft.EntityFrameworkCore;
using Proc.Domain.Inventory;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.Purchasing;

/// <summary>SL đã nhận của đơn mua hàng, dùng chung cho dịch vụ PO và phiếu nhập mua theo PO (design.md mục 6.2 bước 4).</summary>
internal static class PurchaseOrderReceipts
{
    /// <summary>
    /// SL đã nhận (ĐVT cơ sở) theo dòng PO từ phiếu nhập mua đã ghi sổ. <paramref name="excludeDocumentId"/>: phiếu nhập đang sửa/ghi sổ.
    /// SL đã nhận của đơn là số của cả công ty, không phụ thuộc kho người xem: bỏ filter kho; filter của dòng phiếu đi qua phiếu (kèm
    /// filter kho) nên bỏ ở dòng, công ty vẫn lọc qua phép nối với phiếu.
    /// </summary>
    public static async Task<Dictionary<Guid, decimal>> ReceivedBaseByLineAsync(
        ProcDbContext db, IReadOnlyList<Guid> poLineIds, Guid? excludeDocumentId, CancellationToken cancellationToken) =>
        await (
                from l in db.InventoryDocumentLines.IgnoreQueryFilters([ProcDbContext.CompanyFilter]).AsNoTracking()
                join d in db.InventoryDocuments.IgnoreQueryFilters([ProcDbContext.WarehouseFilter]) on l.DocumentId equals d.Id
                where l.PurchaseOrderLineId != null && poLineIds.Contains(l.PurchaseOrderLineId.Value)
                      && d.Status == DocumentStatus.Posted && d.Id != excludeDocumentId
                group l by l.PurchaseOrderLineId!.Value into g
                select new { g.Key, Qty = g.Sum(x => x.BaseQty) })
            .ToDictionaryAsync(x => x.Key, x => x.Qty, cancellationToken);
}
