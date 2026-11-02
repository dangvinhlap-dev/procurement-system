using Microsoft.EntityFrameworkCore;
using Proc.Domain.Documents;
using Proc.Domain.Purchasing;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.Purchasing;

/// <summary>
/// Tính phải nhận – đã nhận – còn thiếu theo từng kỳ quyết toán của cam kết chiết khấu/tài trợ (lát F4b – design.md mục 18.21). Dùng chung cho
/// tab Chiết khấu – tài trợ của hợp đồng và dấu hiệu KSNB "Chiết khấu/tài trợ chưa ghi nhận" (mục 18.19) để hai nơi ra cùng số.
/// </summary>
internal static class ContractRebateTracker
{
    /// <summary>Số ngày sau cuối kỳ quyết toán để NCC trả và người mua ghi nhận; quá hạn mà thiếu thì là dấu hiệu KSNB.</summary>
    public const int GraceDays = 30;

    /// <summary>Một kỳ quyết toán đã tính.</summary>
    internal sealed record Row(
        PurchaseContract Contract, PurchaseContractRebate Rebate, DateOnly From, DateOnly To, DateOnly Due, decimal Purchase, decimal Expected,
        decimal Received, string Status)
    {
        /// <summary>Còn thiếu (không âm).</summary>
        public decimal Short => Math.Max(0m, Expected - Received);
    }

    /// <summary>
    /// Các kỳ của mọi cam kết trên các hợp đồng đã hiệu lực (Hiệu lực/Chấm dứt). Doanh số kỳ = Σ thành tiền trước VAT dòng PO Đã duyệt/Đã
    /// đóng của cùng công ty × NCC có ngày PO trong kỳ – mỗi công ty chỉ một hợp đồng với NCC trong một thời kỳ (mục 18.7) nên đó là doanh số
    /// theo hợp đồng. Dòng PO join mặt hàng nên theo phạm vi nhóm hàng của người xem.
    /// </summary>
    /// <param name="db">DbContext đã gắn phạm vi.</param>
    /// <param name="contracts">Hợp đồng đã nạp cam kết.</param>
    /// <param name="allCompanies">Bỏ filter công ty khi đọc PO (người gọi đã kiểm quyền xem toàn tập đoàn).</param>
    /// <param name="today">Hôm nay theo giờ Việt Nam.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    public static async Task<List<Row>> TrackAsync(
        ProcDbContext db, IReadOnlyList<PurchaseContract> contracts, bool allCompanies, DateOnly today, CancellationToken cancellationToken)
    {
        var tracked = contracts.Where(c => (c.Status is ContractStatus.Active or ContractStatus.Terminated) && c.Rebates.Count > 0).ToList();
        if (tracked.Count == 0) return [];
        var supplierIds = tracked.Select(c => c.SupplierId).Distinct().ToList();
        var companyIds = tracked.Select(c => c.CompanyId).Distinct().ToList();
        var minDate = tracked.Min(c => c.ValidFrom);
        var maxDate = tracked.Max(c => c.EndDate);
        var orders = allCompanies ? db.PurchaseOrders.IgnoreQueryFilters([ProcDbContext.CompanyFilter]) : db.PurchaseOrders;
        var purchases = await (
                from o in orders.AsNoTracking()
                where (o.Status == ApprovalDocumentStatus.Approved || o.Status == ApprovalDocumentStatus.Closed)
                      && supplierIds.Contains(o.SupplierId) && companyIds.Contains(o.CompanyId) && o.Date >= minDate && o.Date <= maxDate
                from l in o.Lines
                join i in db.Items on l.ItemId equals i.Id
                group l.Amount by new { o.CompanyId, o.SupplierId, o.Date } into g
                select new { g.Key.CompanyId, g.Key.SupplierId, g.Key.Date, Amount = g.Sum() })
            .ToListAsync(cancellationToken);

        var contractIds = tracked.Select(c => c.Id).ToList();
        var receipts = allCompanies ? db.ContractRebateReceipts.IgnoreQueryFilters([ProcDbContext.CompanyFilter]) : db.ContractRebateReceipts;
        var received = (await receipts.AsNoTracking()
                .Where(r => contractIds.Contains(r.ContractId) && !r.IsCancelled)
                .GroupBy(r => new { r.RebateId, r.PeriodFrom })
                .Select(g => new { g.Key.RebateId, g.Key.PeriodFrom, Amount = g.Sum(r => r.Amount) })
                .ToListAsync(cancellationToken))
            .ToDictionary(r => (r.RebateId, r.PeriodFrom), r => r.Amount);

        var rows = new List<Row>();
        foreach (var contract in tracked)
        {
            foreach (var rebate in contract.Rebates.OrderBy(r => r.LineNo))
            {
                foreach (var (pFrom, pTo) in rebate.PeriodsWithin(contract.ValidFrom, contract.EndDate))
                {
                    var purchase = purchases
                        .Where(p => p.CompanyId == contract.CompanyId && p.SupplierId == contract.SupplierId && p.Date >= pFrom && p.Date <= pTo)
                        .Sum(p => p.Amount);
                    var expected = rebate.ExpectedFor(purchase);
                    var got = received.GetValueOrDefault((rebate.Id, pFrom));
                    var due = pTo.AddDays(GraceDays);
                    rows.Add(new Row(contract, rebate, pFrom, pTo, due, purchase, expected, got, StatusOf(today, pTo, due, expected, got)));
                }
            }
        }

        return rows;
    }

    /// <summary>
    /// Tình trạng kỳ: đủ khi đã nhận ≥ phải nhận (&gt; 0); chưa hết kỳ → NotDue; hết kỳ mà phải nhận 0 (chưa đạt doanh số tối thiểu, không
    /// phát sinh mua) → NotReached; trong hạn ghi nhận → Waiting; quá hạn mà thiếu → Short. Ví dụ kỳ quý 3 kết thúc 30/09, hạn 30/10, hôm nay
    /// 02/11, phải nhận 25.000.000, đã nhận 10.000.000 → Short, thiếu 15.000.000.
    /// </summary>
    private static string StatusOf(DateOnly today, DateOnly to, DateOnly due, decimal expected, decimal received)
    {
        if (expected > 0 && received >= expected) return "Received";
        if (today <= to) return "NotDue";
        if (expected == 0) return "NotReached";
        return today <= due ? "Waiting" : "Short";
    }
}
