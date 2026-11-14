using Microsoft.EntityFrameworkCore;
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

}
