namespace Proc.Application.Reports;

/// <summary>Số đếm bên phải một mục trên cây điều hướng.</summary>
/// <param name="Count">Số đếm.</param>
/// <param name="Attention">Cần chú ý (badge đỏ – ui-guide.md).</param>
public sealed record NavCountDto(int Count, bool Attention);

/// <summary>Số liệu mua hàng trên trang chủ.</summary>
/// <param name="PendingApprovals">Số phiếu PR, PO chờ người dùng hiện tại duyệt.</param>
/// <param name="PendingRequests">Số PR chờ duyệt hoặc đã duyệt chưa đặt đủ.</param>
/// <param name="OverdueRequests">Trong đó số PR có dòng quá ngày cần.</param>
/// <param name="OpenOrders">Số PO đã duyệt còn chờ giao.</param>
/// <param name="LateOrders">Trong đó số PO có dòng trễ hạn giao.</param>
/// <param name="OpenRfqs">Số RFQ đang nhận báo giá.</param>
public sealed record DashboardPurchaseDto(int PendingApprovals, int? PendingRequests, int? OverdueRequests, int? OpenOrders, int? LateOrders, int? OpenRfqs);

/// <summary>Số liệu kho trên trang chủ.</summary>
/// <param name="DraftDocuments">Chứng từ kho còn nháp theo loại (GRN, RTV, TRF, ICS, ICR).</param>
/// <param name="BelowMinItems">Số mặt hàng có tồn tối thiểu mà tổng tồn của công ty dưới mức (gồm cả hàng chưa có tồn).</param>
/// <param name="ExpiredLots">Số lô còn tồn đã hết hạn.</param>
/// <param name="ExpiringLots">Số lô còn tồn sẽ hết hạn trong 30 ngày tới.</param>
/// <param name="GoodsInTransit">Số phiếu hàng đi đường nội bộ (null khi không có quyền báo cáo kho).</param>
/// <param name="PeriodLabel">Kỳ kho tháng hiện tại, ví dụ "10/2026" (null khi chưa tạo lịch kỳ).</param>
/// <param name="PeriodStatus">Trạng thái kỳ: NotOpened, Open, Costing, Closed.</param>
public sealed record DashboardStockDto(
    IReadOnlyDictionary<string, int> DraftDocuments, int? BelowMinItems, int? ExpiredLots, int? ExpiringLots, int? GoodsInTransit,
    string? PeriodLabel, string? PeriodStatus);

/// <summary>Một NCC trong top mua hàng tháng.</summary>
/// <param name="Code">Mã NCC.</param>
/// <param name="Name">Tên NCC.</param>
/// <param name="NetAmount">Mua thuần trong tháng.</param>
public sealed record DashboardSupplierDto(string Code, string Name, decimal NetAmount);

/// <summary>Mua hàng thuần (trước VAT) tháng này và tháng trước của công ty đang làm việc.</summary>
/// <param name="ThisMonth">Tháng này (từ ngày 1 đến hôm nay).</param>
/// <param name="LastMonth">Cả tháng trước.</param>
/// <param name="TopSuppliers">5 NCC mua nhiều nhất tháng này.</param>
public sealed record DashboardSpendDto(decimal ThisMonth, decimal LastMonth, IReadOnlyList<DashboardSupplierDto> TopSuppliers);

/// <summary>
/// Trang chủ (design.md mục 11.4): số liệu tổng hợp của công ty đang làm việc và số đếm cây điều hướng. Mỗi phần chỉ có khi người dùng có
/// quyền xem màn hình tương ứng (null = không có quyền).
/// </summary>
/// <param name="Today">Hôm nay (giờ Việt Nam).</param>
/// <param name="Purchase">Mua hàng.</param>
/// <param name="Stock">Kho (null khi không có quyền xem chứng từ kho lẫn tồn kho).</param>
/// <param name="Spend">Chi tiêu mua hàng (cần quyền báo cáo mua hàng).</param>
/// <param name="NavCounts">Mã màn hình → số đếm.</param>
public sealed record DashboardDto(
    DateOnly Today, DashboardPurchaseDto Purchase, DashboardStockDto? Stock, DashboardSpendDto? Spend, IReadOnlyDictionary<string, NavCountDto> NavCounts);

/// <summary>Số liệu trang chủ và số đếm cây điều hướng.</summary>
public interface IDashboardService
{
    /// <summary>Số liệu của công ty đang làm việc, theo quyền của người xem.</summary>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Số liệu trang chủ.</returns>
    Task<DashboardDto> GetAsync(CancellationToken cancellationToken);
}
