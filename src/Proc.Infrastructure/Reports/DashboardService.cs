using Microsoft.EntityFrameworkCore;
using Proc.Application.Common;
using Proc.Application.Inventory;
using Proc.Application.Purchasing;
using Proc.Application.Reports;
using Proc.Domain.Documents;
using Proc.Domain.Inventory;
using Proc.Domain.MasterData;
using Proc.Domain.Organization;
using Proc.Domain.Sourcing;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.Reports;

/// <summary>
/// Số liệu trang chủ và số đếm cây điều hướng (design.md mục 11.4). Mỗi phần chỉ tính khi người xem có quyền của màn hình tương ứng, và tính
/// bằng chính dịch vụ của màn hình đó (hoặc truy vấn qua global query filter) – nên con số khớp với màn hình khi bấm vào xem.
/// </summary>
public sealed class DashboardService : IDashboardService
{
    /// <summary>Lô sắp hết hạn: trong bấy nhiêu ngày tới (bằng mặc định của báo cáo Hàng sắp hết hạn).</summary>
    public const int ExpiringDays = 30;

    private readonly ProcDbContext _db;
    private readonly ICurrentUserScope _scope;
    private readonly IClock _clock;
    private readonly IApprovalRuleAdministration _approvals;
    private readonly IPurchaseReportService _purchaseReports;
    private readonly IPurchaseOrderService _orders;
    private readonly IStockReportService _stockReports;
    private readonly IInventoryDocumentService _inventory;

    /// <summary>Tạo dịch vụ.</summary>
    /// <param name="db">DbContext (đã gắn phạm vi người dùng).</param>
    /// <param name="scope">Phạm vi người dùng.</param>
    /// <param name="clock">Đồng hồ.</param>
    /// <param name="approvals">Phiếu chờ duyệt.</param>
    /// <param name="purchaseReports">Báo cáo mua hàng.</param>
    /// <param name="orders">Đơn mua hàng (theo dõi giao hàng).</param>
    /// <param name="stockReports">Báo cáo kho.</param>
    /// <param name="inventory">Chứng từ kho (hàng đi đường).</param>
    public DashboardService(
        ProcDbContext db, ICurrentUserScope scope, IClock clock, IApprovalRuleAdministration approvals, IPurchaseReportService purchaseReports,
        IPurchaseOrderService orders, IStockReportService stockReports, IInventoryDocumentService inventory)
    {
        _db = db;
        _scope = scope;
        _clock = clock;
        _approvals = approvals;
        _purchaseReports = purchaseReports;
        _orders = orders;
        _stockReports = stockReports;
        _inventory = inventory;
    }

    /// <inheritdoc />
    public async Task<DashboardDto> GetAsync(CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        var today = _clock.TodayInVietnam();
        var purchase = await PurchaseAsync(cancellationToken);
        var stock = await StockAsync(today, cancellationToken);
        var spend = _scope.HasPermission(Permissions.Report.PurchaseView) ? await SpendAsync(today, cancellationToken) : null;
        return new DashboardDto(today, purchase, stock, spend, NavCounts(purchase, stock));
    }

    private async Task<DashboardPurchaseDto> PurchaseAsync(CancellationToken cancellationToken)
    {
        // Phiếu chờ tôi duyệt: ai đăng nhập cũng xem được danh sách của chính mình (màn hình Việc cần làm).
        var approvals = (await _approvals.PendingForMeAsync(cancellationToken)).Count;

        int? pendingRequests = null, overdueRequests = null;
        if (_scope.HasPermission(Permissions.Purchase.PrView))
        {
            var lines = await _purchaseReports.GetPendingRequestsAsync(cancellationToken);
            pendingRequests = lines.Select(l => l.RequestId).Distinct().Count();
            overdueRequests = lines.Where(l => l.OverdueDays > 0).Select(l => l.RequestId).Distinct().Count();
        }

        int? openOrders = null, lateOrders = null;
        if (_scope.HasPermission(Permissions.Purchase.PoView))
        {
            var tracking = await _orders.TrackingAsync(cancellationToken);
            openOrders = tracking.Select(t => t.OrderId).Distinct().Count();
            lateOrders = tracking.Where(t => t.Overdue).Select(t => t.OrderId).Distinct().Count();
        }

        int? openRfqs = _scope.HasPermission(Permissions.Sourcing.RfqView)
            ? await _db.Rfqs.CountAsync(r => r.Status == RfqStatus.Sent, cancellationToken)
            : null;
        return new DashboardPurchaseDto(approvals, pendingRequests, overdueRequests, openOrders, lateOrders, openRfqs);
    }

    private async Task<DashboardStockDto?> StockAsync(DateOnly today, CancellationToken cancellationToken)
    {
        var canDocuments = _scope.HasPermission(Permissions.Inventory.DocumentView);
        var canStock = _scope.HasPermission(Permissions.Inventory.StockView);
        var canReport = _scope.HasPermission(Permissions.Report.StockView);
        if (!canDocuments && !canStock && !canReport) return null;

        var drafts = canDocuments
            ? await _db.InventoryDocuments.AsNoTracking().Where(d => d.Status == DocumentStatus.Draft)
                .GroupBy(d => d.DocType).Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken)
            : [];

        int? belowMin = canStock ? await BelowMinAsync(cancellationToken) : null;

        int? expired = null, expiring = null, transit = null;
        if (canReport)
        {
            var lots = (await _stockReports.GetExpiringAsync(new ExpiringQuery(null, null, ExpiringDays), cancellationToken)).Items;
            expired = lots.Count(l => l.DaysLeft < 0);
            expiring = lots.Count - expired;
            transit = (await _inventory.GetGoodsInTransitAsync(cancellationToken)).Count;
        }

        string? periodLabel = null, periodStatus = null;
        if (_scope.HasPermission(Permissions.Inventory.PeriodView))
        {
            var period = await _db.InventoryPeriods.AsNoTracking()
                .SingleOrDefaultAsync(p => p.Year == today.Year && p.Month == today.Month, cancellationToken);
            if (period is not null) (periodLabel, periodStatus) = ($"{today.Month:00}/{today.Year}", period.Status.ToString());
        }

        return new DashboardStockDto(drafts, belowMin, expired, expiring, transit, periodLabel, periodStatus);
    }

    /// <summary>
    /// Số mặt hàng tồn kho đang dùng có tồn tối thiểu mà tổng tồn trên mọi kho của công ty dưới mức – như cờ "Dưới mức tối thiểu" của màn
    /// hình tồn kho (design.md mục 11.1), nhưng gồm cả hàng chưa có tồn (0 &lt; tối thiểu: càng cần đặt hàng).
    /// </summary>
    private async Task<int> BelowMinAsync(CancellationToken cancellationToken)
    {
        var items = await _db.Items.AsNoTracking()
            .Where(i => i.IsActive && i.Type == ItemType.Inventory && i.MinStock > 0)
            .Select(i => new { i.Id, MinStock = i.MinStock!.Value })
            .ToListAsync(cancellationToken);
        if (items.Count == 0) return 0;
        var ids = items.Select(i => i.Id).ToList();
        // Bỏ filter kho chỉ ở phép cộng này (cùng lý do mục 11.1: tồn tối thiểu là một số cho cả công ty); chỉ trả về số đếm, không trả số tồn.
        var totals = await _db.StockOnHand.AsNoTracking()
            .IgnoreQueryFilters([ProcDbContext.WarehouseFilter])
            .Where(s => ids.Contains(s.ItemId))
            .GroupBy(s => s.ItemId)
            .Select(g => new { g.Key, Qty = g.Sum(s => s.Qty) })
            .ToDictionaryAsync(x => x.Key, x => x.Qty, cancellationToken);
        return items.Count(i => totals.GetValueOrDefault(i.Id) < i.MinStock);
    }

    /// <summary>Mua thuần tháng này (đến hôm nay), cả tháng trước, top 5 NCC tháng này – cùng cách tính báo cáo Mua hàng theo NCC.</summary>
    private async Task<DashboardSpendDto> SpendAsync(DateOnly today, CancellationToken cancellationToken)
    {
        var firstOfMonth = new DateOnly(today.Year, today.Month, 1);
        var thisMonth = await _purchaseReports.GetPurchaseSpendAsync(
            new PurchaseReportPeriod(firstOfMonth, today, false), PurchaseSpendGroups.Supplier, cancellationToken);
        var lastMonth = await _purchaseReports.GetPurchaseSpendAsync(
            new PurchaseReportPeriod(firstOfMonth.AddMonths(-1), firstOfMonth.AddDays(-1), false), PurchaseSpendGroups.Supplier, cancellationToken);
        var top = thisMonth.Items.Where(r => r.NetAmount > 0).Take(5).Select(r => new DashboardSupplierDto(r.Code, r.Name, r.NetAmount)).ToList();
        return new DashboardSpendDto(thisMonth.TotalNet, lastMonth.TotalNet, top);
    }

    /// <summary>Số đếm cây điều hướng theo mã màn hình (screens.tsx); chỉ mục có số &gt; 0.</summary>
    private static Dictionary<string, NavCountDto> NavCounts(DashboardPurchaseDto purchase, DashboardStockDto? stock)
    {
        var counts = new Dictionary<string, NavCountDto>();
        void Add(string code, int? count, bool attention = false)
        {
            if (count is > 0) counts[code] = new NavCountDto(count.Value, attention);
        }

        Add("TODO", purchase.PendingApprovals, attention: true);
        Add("PR", purchase.PendingRequests, attention: purchase.OverdueRequests > 0);
        Add("PRPEND", purchase.PendingRequests, attention: purchase.OverdueRequests > 0);
        Add("POTRK", purchase.OpenOrders, attention: purchase.LateOrders > 0);
        Add("RFQ", purchase.OpenRfqs);
        Add("QCMP", purchase.OpenRfqs);
        if (stock is null) return counts;
        foreach (var docType in new[] { DocumentTypes.GoodsReceipt, DocumentTypes.ReturnToVendor, DocumentTypes.Transfer, DocumentTypes.IntercompanySale })
            Add(docType, stock.DraftDocuments.GetValueOrDefault(docType));
        // Phiếu nhập mua nội bộ nháp = hàng công ty khác đã xuất, đang chờ mình xác nhận nhận hàng.
        Add(DocumentTypes.IntercompanyReceipt, stock.DraftDocuments.GetValueOrDefault(DocumentTypes.IntercompanyReceipt), attention: true);
        Add("STK", stock.BelowMinItems, attention: true);
        Add("EXP", stock.ExpiredLots + stock.ExpiringLots, attention: stock.ExpiredLots > 0);
        Add("TRANSIT", stock.GoodsInTransit);
        return counts;
    }
}
