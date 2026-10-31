using Microsoft.EntityFrameworkCore;
using Proc.Application.Common;
using Proc.Application.Pricing;
using Proc.Domain.Documents;
using Proc.Domain.Inventory;
using Proc.Domain.Organization;
using Proc.Domain.Pricing;
using Proc.Domain.Purchasing;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.Pricing;

/// <summary>
/// So sánh giá theo mặt hàng (design.md mục 8.2). Lịch sử giá (mục 8.1 – PriceHistory) không lưu bảng riêng mà tính thẳng từ
/// chứng từ nguồn: bảng giá đang hiệu lực, PO đã duyệt, phiếu nhập mua đã ghi sổ, báo giá trúng thầu – để bỏ ghi sổ, hủy phiếu, ngừng bảng giá không
/// bao giờ làm lịch sử lệch. Mọi giá quy về ĐVT cơ sở, trước VAT, VND (chỉ VND ở giai đoạn này).
/// <para>
/// Xem toàn tập đoàn (quyền <c>Report.Price.ViewAllCompanies</c>, design.md mục 9.2) thì bỏ filter công ty và filter kho trên
/// chứng từ – phạm vi kho của người dùng chỉ khai báo theo từng công ty nên không áp được sang công ty khác; filter nhóm hàng vẫn giữ.
/// </para>
/// </summary>
public sealed class PriceComparisonService : IPriceComparisonService
{
    /// <summary>Số mục lịch sử tối đa trả về.</summary>
    public const int HistoryLimit = 500;

    private readonly ProcDbContext _db;
    private readonly ICurrentUserScope _scope;
    private readonly IClock _clock;

    /// <summary>Tạo dịch vụ.</summary>
    /// <param name="db">DbContext.</param>
    /// <param name="scope">Phạm vi người dùng.</param>
    /// <param name="clock">Đồng hồ.</param>
    public PriceComparisonService(ProcDbContext db, ICurrentUserScope scope, IClock clock)
    {
        _db = db;
        _scope = scope;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task<PriceComparisonDto> CompareAsync(Guid itemId, bool allCompanies, decimal thresholdPct, CancellationToken cancellationToken)
    {
        var companyId = _scope.RequireCompany();
        if (allCompanies) _scope.EnsurePermission(Permissions.Report.PriceViewAllCompanies);
        var item = await (
                from i in _db.Items.AsNoTracking()
                join u in _db.Uoms on i.BaseUomId equals u.Id
                where i.Id == itemId
                select new { i.Id, i.Code, i.Name, Uom = u.Code })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy mặt hàng (hoặc mặt hàng ngoài phạm vi nhóm hàng của bạn).");
        var today = _clock.TodayInVietnam();
        var since = today.AddMonths(-12);

        var priceLists = await PriceListRowsAsync(companyId, allCompanies, itemId, today, cancellationToken);
        var receipts = await ReceiptEntriesAsync(allCompanies, itemId, since, cancellationToken);
        var orders = await OrderEntriesAsync(allCompanies, itemId, since, cancellationToken);
        var quotations = await QuotationEntriesAsync(allCompanies, itemId, since, cancellationToken);

        var entries = receipts.Concat(orders).Concat(quotations).ToList();
        var companyIds = entries.Select(e => e.CompanyId).Concat(priceLists.Select(p => p.CompanyId)).Distinct().ToList();
        var supplierIds = entries.Select(e => e.SupplierId).Concat(priceLists.Select(p => p.SupplierId)).Distinct().ToList();
        var companies = await _db.Companies.AsNoTracking().Where(c => companyIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Code, cancellationToken);
        var suppliers = await _db.Suppliers.AsNoTracking().Where(s => supplierIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => (s.Code, s.Name), cancellationToken);

        var purchases = BuildPurchaseRows(receipts, orders, thresholdPct, companies, suppliers);
        var receiptQty = receipts.Sum(r => r.BaseQty);
        var history = entries.Select(e => new PriceHistoryEntryDto(e.Date, companies[e.CompanyId], suppliers[e.SupplierId].Code, e.Source, e.No, e.BaseQty, e.BasePrice))
            .Concat(priceLists.Select(p => new PriceHistoryEntryDto(
                p.ValidFrom, companies[p.CompanyId], suppliers[p.SupplierId].Code, "PriceList", p.No, p.MinQty * p.UomFactor, p.BaseNetPrice)))
            .OrderByDescending(h => h.Date).ThenBy(h => h.CompanyCode).ThenBy(h => h.DocumentNo)
            .Take(HistoryLimit)
            .ToList();

        return new PriceComparisonDto(
            item.Id, item.Code, item.Name, item.Uom, allCompanies, thresholdPct,
            receipts.Count > 0 ? receipts.Min(r => r.BasePrice) : null,
            receipts.Count > 0 ? receipts.Max(r => r.BasePrice) : null,
            receiptQty > 0 ? Math.Round(receipts.Sum(r => r.Amount) / receiptQty, 6, MidpointRounding.AwayFromZero) : null,
            priceLists.OrderBy(p => p.BaseNetPrice).Select(p => new ComparePriceListRowDto(
                    companies[p.CompanyId], p.IsGroupWide, suppliers[p.SupplierId].Code, suppliers[p.SupplierId].Name, p.No, p.ValidFrom, p.ValidTo,
                    p.UomCode, p.MinQty, p.UnitPrice, p.DiscountPct, p.BaseNetPrice))
                .ToList(),
            purchases,
            history);
    }

    /// <summary>
    /// Mỗi cặp công ty × NCC một dòng. Giá gần nhất = chứng từ mới nhất giữa phiếu nhập và PO (cùng ngày thì phiếu nhập – giá thực
    /// nhận); thấp nhất/cao nhất/bình quân chỉ tính trên phiếu nhập đã ghi sổ (giá thực mua, không đếm hai lần PO và phiếu nhập của
    /// PO đó). Tô màu: giá gần nhất &gt; giá gần nhất thấp nhất trong phạm vi × (1 + X%) – design.md mục 8.2.
    /// Ví dụ thấp nhất 45.000, X = 10%: dòng 50.000 cao hơn 11,11% → tô màu; dòng 49.000 (8,89%) không tô.
    /// </summary>
    private static List<ComparePurchaseRowDto> BuildPurchaseRows(
        List<Entry> receipts, List<Entry> orders, decimal thresholdPct, Dictionary<Guid, string> companies,
        Dictionary<Guid, (string Code, string Name)> suppliers)
    {
        var rows = receipts.Concat(orders)
            .GroupBy(e => (e.CompanyId, e.SupplierId))
            .Select(g =>
            {
                var last = g.OrderByDescending(e => e.Date).ThenByDescending(e => e.Source == "Receipt").ThenByDescending(e => e.No).First();
                var received = g.Where(e => e.Source == "Receipt").ToList();
                var qty = received.Sum(e => e.BaseQty);
                return new
                {
                    g.Key, Last = last,
                    Min = received.Count > 0 ? received.Min(e => e.BasePrice) : (decimal?)null,
                    Max = received.Count > 0 ? received.Max(e => e.BasePrice) : (decimal?)null,
                    Avg = qty > 0 ? Math.Round(received.Sum(e => e.Amount) / qty, 6, MidpointRounding.AwayFromZero) : (decimal?)null,
                    Qty = qty,
                };
            })
            .ToList();
        var groupMin = rows.Count > 0 ? rows.Min(r => r.Last.BasePrice) : 0m;
        return rows
            .Select(r =>
            {
                var pct = groupMin > 0 ? Math.Round((r.Last.BasePrice - groupMin) * 100m / groupMin, 2, MidpointRounding.AwayFromZero) : 0m;
                return new ComparePurchaseRowDto(
                    companies[r.Key.CompanyId], suppliers[r.Key.SupplierId].Code, suppliers[r.Key.SupplierId].Name, r.Last.Date, r.Last.Source, r.Last.No,
                    r.Last.BasePrice, r.Min, r.Max, r.Avg, r.Qty, pct, pct > thresholdPct);
            })
            .OrderBy(r => r.LastPrice).ThenBy(r => r.CompanyCode).ThenBy(r => r.SupplierCode)
            .ToList();
    }

    private async Task<List<PriceListRow>> PriceListRowsAsync(Guid companyId, bool allCompanies, Guid itemId, DateOnly today, CancellationToken cancellationToken)
    {
        // Bỏ filter công ty: xem toàn tập đoàn (đã kiểm quyền) thấy mọi bảng giá; không thì chỉ bảng của mình + bảng toàn tập đoàn.
        var lists = _db.SupplierPriceLists.IgnoreQueryFilters([ProcDbContext.CompanyFilter]).AsNoTracking()
            .Where(p => allCompanies || p.CompanyId == companyId || p.IsGroupWide)
            .Where(p => p.Status == PriceListStatus.Active && p.ValidFrom <= today && (p.ValidTo == null || p.ValidTo >= today));
        var rows = await (
                from p in lists
                from l in p.Lines
                where l.ItemId == itemId
                join u in _db.Uoms on l.UomId equals u.Id
                select new { p.CompanyId, p.IsGroupWide, p.SupplierId, p.No, p.ValidFrom, p.ValidTo, UomCode = u.Code, l.UomFactor, l.MinQty, l.UnitPrice, l.DiscountPct })
            .ToListAsync(cancellationToken);
        return rows.Select(r => new PriceListRow(
                r.CompanyId, r.IsGroupWide, r.SupplierId, r.No, r.ValidFrom, r.ValidTo, r.UomCode, r.UomFactor, r.MinQty, r.UnitPrice, r.DiscountPct,
                PricingMath.BaseNetPrice(r.UnitPrice, r.DiscountPct, r.UomFactor)))
            .ToList();
    }

    private async Task<List<Entry>> ReceiptEntriesAsync(bool allCompanies, Guid itemId, DateOnly since, CancellationToken cancellationToken)
    {
        // IgnoreQueryFilters áp cho cả câu truy vấn (mọi bảng trong đó), nên chỉ bỏ filter khi xem toàn tập đoàn (đã kiểm quyền) –
        // bỏ filter công ty và kho, xem ghi chú lớp. Chế độ công ty mình giữ nguyên mọi filter (dòng lọc qua phiếu, phiếu lọc công ty, kho).
        var lines = allCompanies
            ? _db.InventoryDocumentLines.IgnoreQueryFilters([ProcDbContext.CompanyFilter, ProcDbContext.WarehouseFilter])
            : _db.InventoryDocumentLines;
        var rows = await (
                from l in lines.AsNoTracking()
                join d in _db.InventoryDocuments on l.DocumentId equals d.Id
                where d.DocType == DocumentTypes.GoodsReceipt && d.Status == DocumentStatus.Posted && d.Date >= since && l.ItemId == itemId
                      && d.SupplierId != null && l.BaseQty > 0
                select new { d.CompanyId, SupplierId = d.SupplierId!.Value, d.Date, d.No, l.BaseQty, l.Amount })
            .ToListAsync(cancellationToken);
        // Phiếu nhập không có chiết khấu riêng: đơn giá đã là giá thực mua nên giá cơ sở = thành tiền / SL cơ sở.
        return rows.Select(r => new Entry(
                r.CompanyId, r.SupplierId, r.Date, "Receipt", r.No, r.BaseQty, r.Amount,
                Math.Round(r.Amount / r.BaseQty, 6, MidpointRounding.AwayFromZero)))
            .ToList();
    }

    private async Task<List<Entry>> OrderEntriesAsync(bool allCompanies, Guid itemId, DateOnly since, CancellationToken cancellationToken)
    {
        // Toàn tập đoàn (đã kiểm quyền) – xem ghi chú lớp; IgnoreQueryFilters áp cho cả câu truy vấn.
        var orders = allCompanies ? _db.PurchaseOrders.IgnoreQueryFilters([ProcDbContext.CompanyFilter]) : _db.PurchaseOrders;
        var rows = await (
                from o in orders.AsNoTracking()
                where (o.Status == ApprovalDocumentStatus.Approved || o.Status == ApprovalDocumentStatus.Closed) && o.Date >= since
                from l in o.Lines
                where l.ItemId == itemId
                select new { o.CompanyId, o.SupplierId, o.Date, o.No, l.BaseQty, l.Amount, l.UnitPrice, l.DiscountPct, l.UomFactor })
            .ToListAsync(cancellationToken);
        return rows.Select(r => new Entry(
                r.CompanyId, r.SupplierId, r.Date, "PO", r.No, r.BaseQty, r.Amount, PricingMath.BaseNetPrice(r.UnitPrice, r.DiscountPct, r.UomFactor)))
            .ToList();
    }

    /// <summary>Giá trúng thầu (design.md mục 8.4 bước 5): dòng RFQ đã chọn thầu, ngày = ngày chọn thầu. Chỉ vào lịch sử giá.</summary>
    private async Task<List<Entry>> QuotationEntriesAsync(bool allCompanies, Guid itemId, DateOnly since, CancellationToken cancellationToken)
    {
        // Toàn tập đoàn (đã kiểm quyền) – xem ghi chú lớp; IgnoreQueryFilters áp cho cả câu truy vấn.
        var rfqs = allCompanies ? _db.Rfqs.IgnoreQueryFilters([ProcDbContext.CompanyFilter]) : _db.Rfqs;
        var rows = await (
                from r in rfqs.AsNoTracking()
                where r.Status == Proc.Domain.Sourcing.RfqStatus.Awarded && r.AwardDate >= since
                from l in r.Lines
                where l.ItemId == itemId && l.AwardedSupplierId != null
                from s in r.Suppliers
                where s.SupplierId == l.AwardedSupplierId
                from q in s.QuotationLines
                where q.RfqLineId == l.Id
                select new { r.CompanyId, s.SupplierId, Date = r.AwardDate!.Value, r.No, l.Qty, l.UomFactor, q.UnitPrice, q.DiscountPct })
            .ToListAsync(cancellationToken);
        return rows.Select(r => new Entry(
                r.CompanyId, r.SupplierId, r.Date, "Quotation", r.No, Math.Round(r.Qty * r.UomFactor, 4, MidpointRounding.AwayFromZero),
                Math.Round(r.Qty * r.UnitPrice * (100m - r.DiscountPct) / 100m, 2, MidpointRounding.AwayFromZero),
                PricingMath.BaseNetPrice(r.UnitPrice, r.DiscountPct, r.UomFactor)))
            .ToList();
    }

    private sealed record Entry(Guid CompanyId, Guid SupplierId, DateOnly Date, string Source, string No, decimal BaseQty, decimal Amount, decimal BasePrice);

    private sealed record PriceListRow(
        Guid CompanyId, bool IsGroupWide, Guid SupplierId, string No, DateOnly ValidFrom, DateOnly? ValidTo, string UomCode, decimal UomFactor,
        decimal MinQty, decimal UnitPrice, decimal DiscountPct, decimal BaseNetPrice);
}
