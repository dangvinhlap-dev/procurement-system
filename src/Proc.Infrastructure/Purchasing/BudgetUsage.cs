using Microsoft.EntityFrameworkCore;
using Proc.Domain.Purchasing;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.Purchasing;

/// <summary>
/// Tính ngân sách đã dùng theo bộ phận × tháng của công ty đang làm việc (design.md mục 18.24). Mọi truy vấn qua global query filter
/// công ty nên chỉ thấy phiếu của công ty đang làm việc.
/// <para>
/// Đã dùng = Σ tiền hàng chưa VAT của <b>PO</b> Chờ duyệt/Đã duyệt/Đã đóng (theo tháng ngày PO, bộ phận đặt hàng của PO)
/// + Σ phần <b>PR</b> Chờ duyệt/Đã duyệt <b>chưa lên PO</b> (theo tháng ngày PR). Phiếu đã đóng phần còn lại (lát R6): PO chỉ tính phần đã
/// nhận thuần, PR không tính phần chưa lên PO. Phần PR đã lên một PO được tính thì không tính ở PR
/// nữa – tránh đếm hai lần. PO nháp, bị từ chối (về nháp), đã hủy không tính.
/// </para>
/// <para>
/// Phần PR chưa lên PO của một dòng = thành tiền dòng × (SL cơ sở − SL cơ sở đã lên PO được tính) / SL cơ sở, không âm, làm tròn 2 số.
/// Ví dụ dòng 10 cái × 1.000.000 = 10.000.000, PO đã đặt 4 cái → còn 6.000.000. Hàng mới chưa có mã (mục 18.12) chưa gắn mã: tính đủ
/// thành tiền; đã gắn mã thì đã thành dòng PR có mã nên tính theo dòng đó.
/// </para>
/// </summary>
internal sealed class BudgetUsage
{
    private static readonly ApprovalDocumentStatus[] Counted =
        [ApprovalDocumentStatus.Submitted, ApprovalDocumentStatus.Approved, ApprovalDocumentStatus.Closed];

    private readonly ProcDbContext _db;

    /// <summary>Tạo bộ tính.</summary>
    public BudgetUsage(ProcDbContext db) => _db = db;

    /// <summary>
    /// Kiểm tra ngân sách cho một phiếu PR/PO (lúc lưu, gửi duyệt). Phiếu đang xét không tính vào "đã dùng"; với PO, phần PR mà chính
    /// PO này đặt coi như đã lên PO (không đếm cả ở PR lẫn ở PO).
    /// </summary>
    /// <param name="document">PR hoặc PO đang xét (dòng đã nạp).</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Ngân sách và đã dùng; null khi phiếu không có bộ phận hoặc tháng đó bộ phận không có ngân sách.</returns>
    public async Task<BudgetCheck?> CheckAsync(ApprovableDocument document, CancellationToken cancellationToken)
    {
        if (document.BudgetDepartmentId is not { } departmentId) return null;
        var (year, month) = (document.Date.Year, document.Date.Month);
        var budget = await _db.DepartmentBudgets.AsNoTracking()
            .Where(b => b.DepartmentId == departmentId && b.Year == year && b.Month == month)
            .Select(b => (decimal?)b.Amount)
            .SingleOrDefaultAsync(cancellationToken);
        if (budget is null) return null;

        var order = document as PurchaseOrder;
        var extraOrdered = order?.Lines.Where(l => l.SourcePrLineId is not null)
            .GroupBy(l => l.SourcePrLineId!.Value).ToDictionary(g => g.Key, g => g.Sum(l => l.BaseQty));
        var used = await UsedAsync(year, departmentId, document.Id, extraOrdered, cancellationToken);
        return new BudgetCheck(budget.Value, used.GetValueOrDefault((departmentId, month)));
    }

    /// <summary>
    /// Đã dùng theo bộ phận × tháng của một năm.
    /// </summary>
    /// <param name="year">Năm.</param>
    /// <param name="departmentId">Chỉ một bộ phận; null = mọi bộ phận.</param>
    /// <param name="excludeDocumentId">Phiếu PR/PO không tính (phiếu đang kiểm tra).</param>
    /// <param name="extraOrdered">SL cơ sở theo dòng PR mà phiếu đang kiểm tra đặt thêm (PO đang xét).</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Khóa (bộ phận, tháng) → số tiền đã dùng.</returns>
    public async Task<Dictionary<(Guid DepartmentId, int Month), decimal>> UsedAsync(
        int year, Guid? departmentId, Guid? excludeDocumentId, IReadOnlyDictionary<Guid, decimal>? extraOrdered, CancellationToken cancellationToken)
    {
        var from = new DateOnly(year, 1, 1);
        var to = new DateOnly(year, 12, 31);
        var result = new Dictionary<(Guid, int), decimal>();

        var orders = await _db.PurchaseOrders.AsNoTracking()
            .Where(o => o.DepartmentId != null && o.Date >= from && o.Date <= to && Counted.Contains(o.Status) && o.Id != excludeDocumentId)
            .Where(o => departmentId == null || o.DepartmentId == departmentId)
            .Where(o => o.ClosedAt == null)
            .GroupBy(o => new { DepartmentId = o.DepartmentId!.Value, o.Date.Month })
            .Select(g => new { g.Key.DepartmentId, g.Key.Month, Amount = g.Sum(o => o.TotalAmount) })
            .ToListAsync(cancellationToken);
        foreach (var o in orders) Add(result, (o.DepartmentId, o.Month), o.Amount);

        // PO đã đóng phần còn lại (lát R6): chỉ tính phần đã nhận thuần – phần NCC không giao nữa trả lại ngân sách. Mỗi dòng = thành tiền ×
        // min(1, SL đã nhận ÷ SL đặt), ví dụ đặt 10 × 100.000 = 1.000.000, nhận 6 → 600.000.
        var (start, end) = (from, to);
        var closedLines = await (
                from l in _db.Set<PurchaseOrderLine>().AsNoTracking()
                join o in _db.PurchaseOrders on l.OrderId equals o.Id
                where o.DepartmentId != null && o.Date >= start && o.Date <= end && o.ClosedAt != null && o.Id != excludeDocumentId
                      && (departmentId == null || o.DepartmentId == departmentId)
                select new { l.Id, DepartmentId = o.DepartmentId!.Value, o.Date.Month, l.Amount, l.BaseQty })
            .ToListAsync(cancellationToken);
        if (closedLines.Count > 0)
        {
            var received = await PurchaseOrderReceipts.ReceivedBaseByLineAsync(_db, closedLines.Select(l => l.Id).ToList(), null, cancellationToken);
            foreach (var l in closedLines.Where(l => l.BaseQty > 0))
            {
                var share = Math.Clamp(received.GetValueOrDefault(l.Id) / l.BaseQty, 0m, 1m);
                Add(result, (l.DepartmentId, l.Month), decimal.Round(l.Amount * share, 2, MidpointRounding.AwayFromZero));
            }
        }

        var requests = _db.PurchaseRequests.AsNoTracking()
            .Where(r => r.DepartmentId != null && r.Date >= from && r.Date <= to && Counted.Contains(r.Status) && r.Id != excludeDocumentId)
            .Where(r => departmentId == null || r.DepartmentId == departmentId)
            // PR đã đóng phần còn lại (lát R6): phần chưa lên PO không đặt nữa – không giữ ngân sách.
            .Where(r => r.ClosedAt == null);
        var lines = await (
                from l in _db.Set<PurchaseRequestLine>().AsNoTracking()
                join r in requests on l.RequestId equals r.Id
                select new { l.Id, DepartmentId = r.DepartmentId!.Value, r.Date.Month, l.Amount, l.BaseQty })
            .ToListAsync(cancellationToken);
        var lineIds = lines.Select(l => l.Id).ToList();
        // SL đã lên PO được tính (Chờ duyệt/Đã duyệt/Đã đóng), trừ PO đang xét – phần của PO đang xét lấy từ extraOrdered.
        var ordered = await (
                from ol in _db.Set<PurchaseOrderLine>().AsNoTracking()
                join o in _db.PurchaseOrders on ol.OrderId equals o.Id
                where ol.SourcePrLineId != null && lineIds.Contains(ol.SourcePrLineId.Value) && Counted.Contains(o.Status) && o.Id != excludeDocumentId
                group ol by ol.SourcePrLineId!.Value into g
                select new { LineId = g.Key, Base = g.Sum(x => x.BaseQty) })
            .ToDictionaryAsync(x => x.LineId, x => x.Base, cancellationToken);
        foreach (var l in lines)
        {
            if (l.BaseQty <= 0) continue;
            var done = ordered.GetValueOrDefault(l.Id) + (extraOrdered?.GetValueOrDefault(l.Id) ?? 0m);
            var remaining = Math.Max(0m, l.BaseQty - done);
            Add(result, (l.DepartmentId, l.Month), decimal.Round(l.Amount * remaining / l.BaseQty, 2, MidpointRounding.AwayFromZero));
        }

        var newItems = await (
                from n in _db.Set<PurchaseRequestNewItem>().AsNoTracking()
                join r in requests on n.RequestId equals r.Id
                where n.LinkedLineId == null
                group n by new { DepartmentId = r.DepartmentId!.Value, r.Date.Month } into g
                select new { g.Key.DepartmentId, g.Key.Month, Amount = g.Sum(n => n.Amount) })
            .ToListAsync(cancellationToken);
        foreach (var n in newItems) Add(result, (n.DepartmentId, n.Month), n.Amount);
        return result;
    }

    private static void Add(Dictionary<(Guid, int), decimal> result, (Guid, int) key, decimal amount) =>
        result[key] = result.GetValueOrDefault(key) + amount;
}
