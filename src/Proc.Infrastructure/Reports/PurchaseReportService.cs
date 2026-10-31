using Microsoft.EntityFrameworkCore;
using Proc.Application.Common;
using Proc.Application.Reports;
using Proc.Domain.Common;
using Proc.Domain.Documents;
using Proc.Domain.Inventory;
using Proc.Domain.Organization;
using Proc.Domain.Purchasing;
using Proc.Domain.Sourcing;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.Reports;

/// <summary>
/// Báo cáo PR chưa xử lý, mua hàng theo nhóm, đánh giá NCC, tiết kiệm từ đấu thầu (design.md mục 11, 11.3). Giá trị đều trước VAT, VND.
/// <para>
/// Xem toàn tập đoàn (quyền <c>Report.Purchase.ViewAllCompanies</c>, mục 9.2) thì bỏ filter công ty và filter kho ở truy vấn – phạm vi kho
/// khai báo theo từng công ty nên không áp được sang công ty khác; filter nhóm hàng vẫn giữ (mọi truy vấn đều join mặt hàng).
/// IgnoreQueryFilters áp cho cả câu truy vấn, nên chỉ gọi khi đã kiểm quyền; khi không xem toàn tập đoàn thì không bỏ filter nào.
/// </para>
/// </summary>
public sealed class PurchaseReportService : IPurchaseReportService
{
    /// <summary>Khoảng ngày tối đa mỗi lần xem (hai năm).</summary>
    public const int MaxDays = 731;

    private readonly ProcDbContext _db;
    private readonly ICurrentUserScope _scope;
    private readonly IClock _clock;

    /// <summary>Tạo dịch vụ.</summary>
    /// <param name="db">Ngữ cảnh CSDL (đã gắn phạm vi người dùng).</param>
    /// <param name="scope">Phạm vi người dùng.</param>
    /// <param name="clock">Đồng hồ.</param>
    public PurchaseReportService(ProcDbContext db, ICurrentUserScope scope, IClock clock)
    {
        _db = db;
        _scope = scope;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PendingRequestLineDto>> GetPendingRequestsAsync(CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        var today = _clock.TodayInVietnam();
        var lines = await (
                from l in _db.Set<PurchaseRequestLine>().AsNoTracking()
                join r in _db.PurchaseRequests on l.RequestId equals r.Id
                join i in _db.Items on l.ItemId equals i.Id
                join u in _db.Uoms on l.UomId equals u.Id
                where r.Status == ApprovalDocumentStatus.Submitted || r.Status == ApprovalDocumentStatus.Approved
                select new
                {
                    l.Id, RequestId = r.Id, r.No, r.Date, r.Department, r.Status, r.Priority, l.LineNo, l.ItemId, ItemCode = i.Code, ItemName = i.Name,
                    UomCode = u.Code, l.Qty, l.UomFactor, l.BaseQty, l.EstPrice, NeededDate = l.NeededDate ?? r.NeededDate,
                })
            .ToListAsync(cancellationToken);

        // SL đã lên PO chưa hủy theo dòng PR (ĐVT cơ sở) – cùng cách tính với màn hình lập PO từ PR (mục 6.7).
        var lineIds = lines.Select(l => l.Id).ToList();
        var ordered = await (
                from ol in _db.Set<PurchaseOrderLine>().AsNoTracking()
                join o in _db.PurchaseOrders on ol.OrderId equals o.Id
                where ol.SourcePrLineId != null && lineIds.Contains(ol.SourcePrLineId.Value) && o.Status != ApprovalDocumentStatus.Cancelled
                group ol by ol.SourcePrLineId!.Value into g
                select new { g.Key, Qty = g.Sum(x => x.BaseQty) })
            .ToDictionaryAsync(x => x.Key, x => x.Qty, cancellationToken);

        return lines
            .Select(l =>
            {
                // Quy SL cơ sở về ĐVT của dòng PR, ví dụ đề nghị 5 thùng (60 cái), đã đặt 24 cái → đã đặt 2 thùng, còn 3 thùng.
                var orderedQty = Math.Round(ordered.GetValueOrDefault(l.Id) / l.UomFactor, 4, MidpointRounding.AwayFromZero);
                var remaining = Math.Max(0, l.Qty - orderedQty);
                var overdue = l.NeededDate is { } needed ? Math.Max(0, today.DayNumber - needed.DayNumber) : 0;
                return new PendingRequestLineDto(
                    l.RequestId, l.No, l.Date, l.Department, l.Status.ToString(), l.Priority.ToString(), l.LineNo, l.ItemId, l.ItemCode, l.ItemName,
                    l.UomCode, l.Qty, orderedQty, remaining, Math.Round(remaining * l.EstPrice, 2, MidpointRounding.AwayFromZero), l.NeededDate, overdue);
            })
            .Where(l => l.RemainingQty > 0)
            .OrderBy(l => l.NeededDate is null).ThenBy(l => l.NeededDate).ThenBy(l => l.RequestNo).ThenBy(l => l.LineNo)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<PurchaseSpendResult> GetPurchaseSpendAsync(PurchaseReportPeriod period, string groupBy, CancellationToken cancellationToken)
    {
        if (!PurchaseSpendGroups.All.Contains(groupBy)) throw new BusinessRuleException("Cách nhóm không hợp lệ.");
        Check(period);
        var lines = await SupplierLinesAsync(period, cancellationToken);

        Func<SupplierLine, Guid> key = groupBy switch
        {
            PurchaseSpendGroups.Category => l => l.CategoryId,
            PurchaseSpendGroups.Item => l => l.ItemId,
            PurchaseSpendGroups.Company => l => l.CompanyId,
            _ => l => l.SupplierId,
        };
        var groups = lines.GroupBy(key).Select(g => new
            {
                Key = g.Key,
                ReceiptCount = g.Where(l => l.IsReceipt).Select(l => l.DocumentId).Distinct().Count(),
                Receipt = g.Where(l => l.IsReceipt).Sum(l => l.Amount),
                Return = g.Where(l => !l.IsReceipt).Sum(l => l.Amount),
            })
            .ToList();
        var names = await NamesAsync(groupBy, groups.Select(g => g.Key).ToList(), cancellationToken);
        var total = groups.Sum(g => g.Receipt - g.Return);
        var rows = groups
            .Select(g =>
            {
                var net = g.Receipt - g.Return;
                // Tỷ trọng, ví dụ 25.000.000 / 100.000.000 = 25,00%.
                var share = total == 0 ? 0 : Math.Round(net / total * 100, 2, MidpointRounding.AwayFromZero);
                var (code, name) = names.GetValueOrDefault(g.Key, ("?", "?"));
                return new PurchaseSpendRowDto(g.Key, code, name, g.ReceiptCount, g.Receipt, g.Return, net, share);
            })
            .OrderByDescending(r => r.NetAmount).ThenBy(r => r.Code)
            .ToList();
        return new PurchaseSpendResult(groupBy, period.AllCompanies, total, rows);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SupplierEvaluationRowDto>> GetSupplierEvaluationAsync(PurchaseReportPeriod period, CancellationToken cancellationToken)
    {
        Check(period);
        var lines = await SupplierLinesAsync(period, cancellationToken);

        // Dòng nhập theo PO: hạn giao = ngày giao của dòng PO, không có thì ngày giao dự kiến của PO; dòng không có hạn thì không chấm.
        var docLines = period.AllCompanies
            ? _db.InventoryDocumentLines.IgnoreQueryFilters([ProcDbContext.CompanyFilter, ProcDbContext.WarehouseFilter])
            : _db.InventoryDocumentLines;
        var deliveries = await (
                from l in docLines.AsNoTracking()
                join d in _db.InventoryDocuments on l.DocumentId equals d.Id
                join i in _db.Items on l.ItemId equals i.Id
                join pl in _db.Set<PurchaseOrderLine>() on l.PurchaseOrderLineId equals pl.Id
                join o in _db.PurchaseOrders on pl.OrderId equals o.Id
                where d.Status == DocumentStatus.Posted && d.DocType == DocumentTypes.GoodsReceipt && d.SupplierId != null
                      && d.Date >= period.From && d.Date <= period.To && (pl.DeliveryDate != null || o.ExpectedDate != null)
                select new { SupplierId = d.SupplierId!.Value, d.Date, Due = pl.DeliveryDate ?? o.ExpectedDate })
            .ToListAsync(cancellationToken);
        var delivery = deliveries
            .GroupBy(x => x.SupplierId)
            .ToDictionary(g => g.Key, g =>
            {
                var late = g.Select(x => x.Date.DayNumber - x.Due!.Value.DayNumber).Where(days => days > 0).ToList();
                return (Total: g.Count(), OnTime: g.Count() - late.Count, AvgLate: late.Count == 0 ? (decimal?)null : Math.Round((decimal)late.Average(), 1));
            });

        var names = await NamesAsync(PurchaseSpendGroups.Supplier, lines.Select(l => l.SupplierId).Distinct().ToList(), cancellationToken);
        return lines
            .GroupBy(l => l.SupplierId)
            .Select(g =>
            {
                var receipt = g.Where(l => l.IsReceipt).Sum(l => l.Amount);
                var returned = g.Where(l => !l.IsReceipt).Sum(l => l.Amount);
                var d = delivery.GetValueOrDefault(g.Key);
                var (code, name) = names.GetValueOrDefault(g.Key, ("?", "?"));
                // Tỷ lệ trả, ví dụ nhập 10.000.000, trả 500.000 → 5,00%. Tỷ lệ đúng hạn, ví dụ 9/10 dòng → 90,00%.
                return new SupplierEvaluationRowDto(
                    g.Key, code, name, g.Where(l => l.IsReceipt).Select(l => l.DocumentId).Distinct().Count(), receipt, returned,
                    receipt == 0 ? null : Math.Round(returned / receipt * 100, 2, MidpointRounding.AwayFromZero),
                    d.Total, d.OnTime, d.Total == 0 ? null : Math.Round((decimal)d.OnTime / d.Total * 100, 2, MidpointRounding.AwayFromZero), d.AvgLate);
            })
            .OrderByDescending(r => r.ReceiptAmount).ThenBy(r => r.SupplierCode)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SourcingSavingRowDto>> GetSourcingSavingsAsync(PurchaseReportPeriod period, CancellationToken cancellationToken)
    {
        Check(period);
        var rfqs = period.AllCompanies ? _db.Rfqs.IgnoreQueryFilters([ProcDbContext.CompanyFilter]) : _db.Rfqs;
        var awarded = await (
                from r in rfqs.AsNoTracking()
                where r.Status == RfqStatus.Awarded && r.AwardDate >= period.From && r.AwardDate <= period.To
                from l in r.Lines
                where l.AwardedSupplierId != null
                join i in _db.Items on l.ItemId equals i.Id
                join u in _db.Uoms on l.UomId equals u.Id
                join c in _db.Companies on r.CompanyId equals c.Id
                select new
                {
                    RfqId = r.Id, r.No, r.Title, AwardDate = r.AwardDate!.Value, CompanyCode = c.Code, LineId = l.Id, ItemCode = i.Code,
                    ItemName = i.Name, UomCode = u.Code, l.Qty, SupplierId = l.AwardedSupplierId!.Value, l.AwardReason,
                })
            .ToListAsync(cancellationToken);
        var lineIds = awarded.Select(a => a.LineId).ToList();
        var quotes = (await (
                    from r in rfqs.AsNoTracking()
                    from s in r.Suppliers
                    from q in s.QuotationLines
                    where lineIds.Contains(q.RfqLineId)
                    select new { q.RfqLineId, s.SupplierId, q.UnitPrice, q.DiscountPct })
                .ToListAsync(cancellationToken))
            // Giá sau chiết khấu, ví dụ 600.000 CK 5% → 570.000; làm tròn 6 số lẻ theo numeric(19,6).
            .Select(q => (q.RfqLineId, q.SupplierId, Price: Math.Round(q.UnitPrice * (100m - q.DiscountPct) / 100m, 6, MidpointRounding.AwayFromZero)))
            .ToLookup(q => q.RfqLineId);
        var names = await NamesAsync(PurchaseSpendGroups.Supplier, awarded.Select(a => a.SupplierId).Distinct().ToList(), cancellationToken);

        var rows = new List<SourcingSavingRowDto>();
        foreach (var a in awarded)
        {
            var prices = quotes[a.LineId].ToList();
            var won = prices.Where(p => p.SupplierId == a.SupplierId).Select(p => (decimal?)p.Price).FirstOrDefault();
            if (won is not { } price) continue;
            var (min, max) = (prices.Min(p => p.Price), prices.Max(p => p.Price));
            var avg = Math.Round(prices.Average(p => p.Price), 6, MidpointRounding.AwayFromZero);
            var (code, name) = names.GetValueOrDefault(a.SupplierId, ("?", "?"));
            // Ví dụ 100 cái, chào 50.000 / 48.000 / 52.000, chọn 48.000: bình quân 50.000 → tiết kiệm 200.000; so cao nhất → 400.000.
            rows.Add(new SourcingSavingRowDto(
                a.RfqId, a.No, a.Title, a.AwardDate, a.CompanyCode, a.ItemCode, a.ItemName, a.UomCode, a.Qty, code, name, prices.Count,
                price, min, avg, max, Money(a.Qty * price), Money((avg - price) * a.Qty), Money((max - price) * a.Qty), price == min, a.AwardReason));
        }

        return rows.OrderByDescending(r => r.AwardDate).ThenBy(r => r.RfqNo).ThenBy(r => r.ItemCode).ToList();
    }

    private static decimal Money(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    /// <summary>Kiểm tra khoảng ngày và quyền xem toàn tập đoàn.</summary>
    private void Check(PurchaseReportPeriod period)
    {
        _scope.RequireCompany();
        if (period.From > period.To) throw new BusinessRuleException("Từ ngày phải trước hoặc bằng đến ngày.");
        if (period.To.DayNumber - period.From.DayNumber >= MaxDays) throw new BusinessRuleException("Báo cáo xem tối đa hai năm mỗi lần.");
        if (period.AllCompanies) _scope.EnsurePermission(Permissions.Report.PurchaseViewAllCompanies);
    }

    /// <summary>Một dòng phiếu nhập mua NCC (IsReceipt) hoặc phiếu trả NCC đã ghi sổ, giá trị trước VAT.</summary>
    private sealed record SupplierLine(Guid DocumentId, Guid CompanyId, Guid SupplierId, Guid ItemId, Guid CategoryId, bool IsReceipt, decimal Amount);

    /// <summary>
    /// Dòng phiếu nhập mua NCC và trả NCC đã ghi sổ trong khoảng ngày. Không gồm nhập mua nội bộ (ICR): đó là mua giữa các công ty trong tập
    /// đoàn, không phải chi tiêu với NCC bên ngoài.
    /// </summary>
    private async Task<List<SupplierLine>> SupplierLinesAsync(PurchaseReportPeriod period, CancellationToken cancellationToken)
    {
        // Toàn tập đoàn (đã kiểm quyền ở Check) – xem ghi chú lớp.
        var docLines = period.AllCompanies
            ? _db.InventoryDocumentLines.IgnoreQueryFilters([ProcDbContext.CompanyFilter, ProcDbContext.WarehouseFilter])
            : _db.InventoryDocumentLines;
        return await (
                from l in docLines.AsNoTracking()
                join d in _db.InventoryDocuments on l.DocumentId equals d.Id
                join i in _db.Items on l.ItemId equals i.Id
                where d.Status == DocumentStatus.Posted && d.SupplierId != null
                      && (d.DocType == DocumentTypes.GoodsReceipt || d.DocType == DocumentTypes.ReturnToVendor)
                      && d.Date >= period.From && d.Date <= period.To
                select new SupplierLine(d.Id, d.CompanyId, d.SupplierId!.Value, l.ItemId, i.CategoryId, d.DocType == DocumentTypes.GoodsReceipt, l.Amount))
            .ToListAsync(cancellationToken);
    }

    /// <summary>Mã, tên theo cách nhóm.</summary>
    private async Task<Dictionary<Guid, (string Code, string Name)>> NamesAsync(string groupBy, List<Guid> ids, CancellationToken cancellationToken) =>
        groupBy switch
        {
            PurchaseSpendGroups.Category => await _db.ItemCategories.AsNoTracking().Where(c => ids.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id, c => (c.Code, c.Name), cancellationToken),
            PurchaseSpendGroups.Item => await _db.Items.AsNoTracking().Where(i => ids.Contains(i.Id))
                .ToDictionaryAsync(i => i.Id, i => (i.Code, i.Name), cancellationToken),
            PurchaseSpendGroups.Company => await _db.Companies.AsNoTracking().Where(c => ids.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id, c => (c.Code, c.Name), cancellationToken),
            _ => await _db.Suppliers.AsNoTracking().Where(s => ids.Contains(s.Id))
                .ToDictionaryAsync(s => s.Id, s => (s.Code, s.Name), cancellationToken),
        };
}
