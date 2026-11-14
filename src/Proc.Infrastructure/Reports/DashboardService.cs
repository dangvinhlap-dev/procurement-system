using Microsoft.EntityFrameworkCore;
using Proc.Application.Common;
using Proc.Application.Purchasing;
using Proc.Application.Reports;
using Proc.Domain.Documents;
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
    private readonly ProcDbContext _db;
    private readonly ICurrentUserScope _scope;
    private readonly IClock _clock;
    private readonly IApprovalRuleAdministration _approvals;
    private readonly IPurchaseReportService _purchaseReports;
    private readonly IPurchaseOrderService _orders;
    private readonly Proc.Application.Notifications.INotificationService _notifications;

    /// <summary>Tạo dịch vụ.</summary>
    /// <param name="db">DbContext (đã gắn phạm vi người dùng).</param>
    /// <param name="scope">Phạm vi người dùng.</param>
    /// <param name="clock">Đồng hồ.</param>
    /// <param name="approvals">Phiếu chờ duyệt.</param>
    /// <param name="purchaseReports">Báo cáo mua hàng.</param>
    /// <param name="orders">Đơn mua hàng (theo dõi giao hàng).</param>
    /// <param name="notifications">Thông báo (số chưa đọc).</param>
    public DashboardService(
        ProcDbContext db, ICurrentUserScope scope, IClock clock, IApprovalRuleAdministration approvals, IPurchaseReportService purchaseReports,
        IPurchaseOrderService orders,
        Proc.Application.Notifications.INotificationService notifications)
    {
        _db = db;
        _scope = scope;
        _clock = clock;
        _approvals = approvals;
        _purchaseReports = purchaseReports;
        _orders = orders;
        _notifications = notifications;
    }

    /// <inheritdoc />
    public async Task<DashboardDto> GetAsync(CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        var today = _clock.TodayInVietnam();
        var purchase = await PurchaseAsync(cancellationToken);
        var spend = _scope.HasPermission(Permissions.Report.PurchaseView) ? await SpendAsync(today, cancellationToken) : null;
        var counts = NavCounts(purchase);
        // Thông báo chưa đọc: cùng API trang chủ để chuông trên Shellbar và cây điều hướng tự cập nhật mỗi phút (design.md mục 11.4, 11.6).
        var unread = await _notifications.UnreadCountAsync(cancellationToken);
        if (unread > 0) counts["NOTI"] = new NavCountDto(unread, Attention: true);
        return new DashboardDto(today, purchase, spend, counts);
    }

    /// <summary>Số liệu mua hàng cho trang chủ: phiếu chờ tôi duyệt, PR/PO đang mở.</summary>
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

    /// <summary>Mua thuần tháng này (đến hôm nay), cả tháng trước, top 5 NCC tháng này – cùng cách tính báo cáo Mua hàng theo NCC.</summary>
    private async Task<DashboardSpendDto> SpendAsync(DateOnly today, CancellationToken cancellationToken)
    {
        var firstOfMonth = new DateOnly(today.Year, today.Month, 1);
        var thisMonth = await _purchaseReports.GetPurchaseSpendAsync(
            new PurchaseReportPeriod(firstOfMonth, today, false), PurchaseSpendGroups.Supplier, null, cancellationToken);
        var lastMonth = await _purchaseReports.GetPurchaseSpendAsync(
            new PurchaseReportPeriod(firstOfMonth.AddMonths(-1), firstOfMonth.AddDays(-1), false), PurchaseSpendGroups.Supplier, null, cancellationToken);
        var top = thisMonth.Items.Where(r => r.NetAmount > 0).Take(5).Select(r => new DashboardSupplierDto(r.Code, r.Name, r.NetAmount)).ToList();
        return new DashboardSpendDto(thisMonth.TotalNet, lastMonth.TotalNet, top);
    }

    /// <summary>Số đếm cây điều hướng theo mã màn hình (screens.tsx); chỉ mục có số &gt; 0.</summary>
    private static Dictionary<string, NavCountDto> NavCounts(DashboardPurchaseDto purchase)
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
        return counts;
    }
}
