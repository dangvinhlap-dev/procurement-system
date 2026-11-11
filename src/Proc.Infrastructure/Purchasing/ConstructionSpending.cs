using Microsoft.EntityFrameworkCore;
using Proc.Domain.Purchasing;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.Purchasing;

/// <summary>
/// Lũy kế đã mua của công trình (lát Y3, design.md mục 19.3 – quyết định Q4): tổng các dòng PO <b>đã duyệt hoặc đã đóng</b> lấy từ dòng của đề
/// nghị mua loại A gắn công trình. Người dùng chốt 11/11/2026: mọi khoản mua cho công trình bắt buộc đi qua đề nghị loại A – PO không lấy dòng
/// từ đề nghị loại A không tính vào công trình nào.
/// <para>Ví dụ: dự toán 1.000.000.000; PO-1 (đã duyệt) 2 dòng từ đề nghị A của công trình, thành tiền chưa VAT 300.000.000 + 100.000.000, VAT
/// 32.000.000 → lũy kế 400.000.000 (có VAT 432.000.000), còn lại 600.000.000. PO chờ duyệt / nháp / đã hủy không tính.</para>
/// </summary>
internal static class ConstructionSpending
{
    /// <summary>Lũy kế theo công trình.</summary>
    /// <param name="db">DbContext (đã lọc theo công ty đang làm việc).</param>
    /// <param name="projectIds">Các công trình cần tính.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Công trình → (chưa VAT, tiền VAT); công trình chưa mua gì không có trong từ điển.</returns>
    public static async Task<Dictionary<Guid, (decimal Net, decimal Vat)>> ByProjectAsync(
        ProcDbContext db, IReadOnlyCollection<Guid> projectIds, CancellationToken cancellationToken)
    {
        if (projectIds.Count == 0) return [];
        var rows = await (
                from ol in db.Set<PurchaseOrderLine>().AsNoTracking()
                join o in db.PurchaseOrders on ol.OrderId equals o.Id
                join l in db.Set<PurchaseRequestLine>() on ol.SourcePrLineId equals l.Id
                join r in db.PurchaseRequests on l.RequestId equals r.Id
                where (o.Status == ApprovalDocumentStatus.Approved || o.Status == ApprovalDocumentStatus.Closed)
                      && r.Category == PurchaseCategory.Construction && r.ConstructionProjectId != null
                      && projectIds.Contains(r.ConstructionProjectId.Value)
                group ol by r.ConstructionProjectId!.Value into g
                select new { g.Key, Net = g.Sum(x => x.Amount), Vat = g.Sum(x => x.VatAmount) })
            .ToListAsync(cancellationToken);
        return rows.ToDictionary(x => x.Key, x => (x.Net, x.Vat));
    }
}
