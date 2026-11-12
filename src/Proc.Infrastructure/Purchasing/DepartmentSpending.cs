using Microsoft.EntityFrameworkCore;
using Proc.Domain.Purchasing;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.Purchasing;

/// <summary>
/// Lũy kế đã mua trong tháng của một bộ phận – dòng "Lũy kế số tiền đã mua trong tháng" của biểu mẫu 1B và "Lũy kế số tiền đã mua" của 2B
/// (người dùng chốt 12/11/2026, lát Y10): <b>chỉ tính PO đã duyệt</b> (cả PO đã đóng), giá chưa VAT, giống lũy kế công trình ở 1A
/// (<see cref="ConstructionSpending"/>) – không tính phần đề nghị chưa lên PO như ngân sách (design.md mục 18.24).
/// Dòng PO lấy từ đề nghị loại B / dịch vụ của bộ phận (đề nghị loại A tính vào lũy kế công trình); tháng theo ngày chứng từ PO.
/// Ví dụ: tháng 11 bộ phận có PO 1 (đã duyệt, 2.000.000 chưa VAT) và PO 2 (chờ duyệt, 500.000) → lũy kế 2.000.000.
/// </summary>
public static class DepartmentSpending
{
    /// <summary>Tổng tiền chưa VAT của các dòng PO đã duyệt/đã đóng trong tháng, lấy từ đề nghị loại B/DV của bộ phận.</summary>
    /// <param name="db">DbContext (giữ bộ lọc phạm vi công ty).</param>
    /// <param name="departmentId">Bộ phận của đề nghị / bảng tổng hợp.</param>
    /// <param name="anyDayInMonth">Một ngày trong tháng cần tính (ngày phiếu).</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Lũy kế đã mua chưa VAT (0 khi chưa có PO).</returns>
    public static async Task<decimal> MonthAsync(ProcDbContext db, Guid departmentId, DateOnly anyDayInMonth, CancellationToken cancellationToken) =>
        (await MonthDetailAsync(db, departmentId, anyDayInMonth, null, cancellationToken)).Net;

    /// <summary>
    /// Như <see cref="MonthAsync"/> nhưng trả cả tiền VAT và bỏ được một PO – bảng theo dõi 4B (lát Y7) tính "đã mua trước đây" không gồm
    /// chính đơn đang in, rồi cộng đơn đó vào sau (ghi chú biểu mẫu: "ghi gộp số tiền đã mua trước đây + số tiền kèm theo đề xuất").
    /// </summary>
    /// <param name="db">DbContext (giữ bộ lọc phạm vi công ty).</param>
    /// <param name="departmentId">Bộ phận.</param>
    /// <param name="anyDayInMonth">Một ngày trong tháng cần tính.</param>
    /// <param name="excludeOrderId">PO bỏ khỏi lũy kế; null = không bỏ.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>(chưa VAT, tiền VAT); 0 khi chưa có PO.</returns>
    public static async Task<(decimal Net, decimal Vat)> MonthDetailAsync(
        ProcDbContext db, Guid departmentId, DateOnly anyDayInMonth, Guid? excludeOrderId, CancellationToken cancellationToken)
    {
        var monthStart = new DateOnly(anyDayInMonth.Year, anyDayInMonth.Month, 1);
        var nextMonth = monthStart.AddMonths(1);
        var rows = await (
                from ol in db.Set<PurchaseOrderLine>().AsNoTracking()
                join o in db.PurchaseOrders on ol.OrderId equals o.Id
                join l in db.Set<PurchaseRequestLine>() on ol.SourcePrLineId equals l.Id
                join r in db.PurchaseRequests on l.RequestId equals r.Id
                where (o.Status == ApprovalDocumentStatus.Approved || o.Status == ApprovalDocumentStatus.Closed)
                      && o.Date >= monthStart && o.Date < nextMonth && o.Id != excludeOrderId
                      && r.DepartmentId == departmentId && r.Category != PurchaseCategory.Construction
                select new { ol.Amount, ol.VatAmount })
            .ToListAsync(cancellationToken);
        return (rows.Sum(x => x.Amount), rows.Sum(x => x.VatAmount));
    }
}
