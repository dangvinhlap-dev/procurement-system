namespace Proc.Application.Reports;

/// <summary>Điều kiện báo cáo hàng chậm luân chuyển.</summary>
/// <param name="WarehouseId">Một kho; null = mọi kho được phép.</param>
/// <param name="CategoryId">Nhóm hàng (gồm nhóm con); null = mọi nhóm được phép.</param>
/// <param name="Days">Số ngày không xuất tối thiểu để coi là chậm luân chuyển (1–3.650).</param>
public sealed record SlowMovingQuery(Guid? WarehouseId, Guid? CategoryId, int Days);

/// <summary>
/// Một dòng hàng chậm luân chuyển: kho × mặt hàng (cộng mọi lô) còn tồn mà lâu không xuất. SL theo ĐVT cơ sở.
/// </summary>
/// <param name="WarehouseId">Kho.</param>
/// <param name="WarehouseCode">Mã kho.</param>
/// <param name="WarehouseName">Tên kho.</param>
/// <param name="ItemId">Mặt hàng.</param>
/// <param name="ItemCode">Mã hàng.</param>
/// <param name="ItemName">Tên hàng.</param>
/// <param name="CategoryCode">Mã nhóm hàng.</param>
/// <param name="CategoryName">Tên nhóm hàng.</param>
/// <param name="UomCode">ĐVT cơ sở.</param>
/// <param name="Qty">Tồn hiện tại.</param>
/// <param name="Amount">Giá trị tồn theo sổ kho (giá vốn tạm tính khi kỳ còn mở).</param>
/// <param name="FirstInDate">Ngày nhập đầu tiên vào kho này.</param>
/// <param name="LastInDate">Ngày nhập gần nhất.</param>
/// <param name="LastOutDate">Ngày xuất gần nhất (null = chưa xuất lần nào).</param>
/// <param name="IdleDays">Số ngày không xuất, tính đến hôm nay, từ ngày xuất gần nhất hoặc (chưa xuất bao giờ) từ ngày nhập đầu tiên.</param>
public sealed record SlowMovingRowDto(
    Guid WarehouseId, string WarehouseCode, string WarehouseName, Guid ItemId, string ItemCode, string ItemName,
    string CategoryCode, string CategoryName, string UomCode, decimal Qty, decimal Amount,
    DateOnly? FirstInDate, DateOnly? LastInDate, DateOnly? LastOutDate, int IdleDays);

/// <summary>Báo cáo hàng chậm luân chuyển.</summary>
/// <param name="AsOf">Ngày tính (hôm nay theo giờ Việt Nam).</param>
/// <param name="Days">Ngưỡng số ngày đã dùng.</param>
/// <param name="Items">Các dòng, số ngày không xuất giảm dần.</param>
public sealed record SlowMovingResult(DateOnly AsOf, int Days, IReadOnlyList<SlowMovingRowDto> Items);

/// <summary>Điều kiện báo cáo hàng sắp hết hạn.</summary>
/// <param name="WarehouseId">Một kho; null = mọi kho được phép.</param>
/// <param name="CategoryId">Nhóm hàng (gồm nhóm con); null = mọi nhóm được phép.</param>
/// <param name="Days">Hết hạn trong vòng bấy nhiêu ngày tới (0–3.650); lô đã hết hạn luôn có mặt.</param>
public sealed record ExpiringQuery(Guid? WarehouseId, Guid? CategoryId, int Days);

/// <summary>Một lô còn tồn sắp (hoặc đã) hết hạn dùng.</summary>
/// <param name="WarehouseId">Kho.</param>
/// <param name="WarehouseCode">Mã kho.</param>
/// <param name="WarehouseName">Tên kho.</param>
/// <param name="ItemId">Mặt hàng.</param>
/// <param name="ItemCode">Mã hàng.</param>
/// <param name="ItemName">Tên hàng.</param>
/// <param name="CategoryCode">Mã nhóm hàng.</param>
/// <param name="UomCode">ĐVT cơ sở.</param>
/// <param name="LotNo">Số lô.</param>
/// <param name="ExpiryDate">Hạn dùng.</param>
/// <param name="DaysLeft">Số ngày còn lại (âm = đã hết hạn bấy nhiêu ngày).</param>
/// <param name="Qty">Tồn của lô tại kho.</param>
/// <param name="Amount">Giá trị tồn theo sổ kho.</param>
public sealed record ExpiringRowDto(
    Guid WarehouseId, string WarehouseCode, string WarehouseName, Guid ItemId, string ItemCode, string ItemName,
    string CategoryCode, string UomCode, string LotNo, DateOnly ExpiryDate, int DaysLeft, decimal Qty, decimal Amount);

/// <summary>Báo cáo hàng sắp hết hạn.</summary>
/// <param name="AsOf">Ngày tính.</param>
/// <param name="Days">Số ngày tới đã dùng.</param>
/// <param name="Items">Các lô, hạn dùng tăng dần.</param>
public sealed record ExpiringResult(DateOnly AsOf, int Days, IReadOnlyList<ExpiringRowDto> Items);

/// <summary>
/// Tồn một mặt hàng tại một công ty trong tập đoàn – chỉ số lượng, không có giá trị (giá vốn là số liệu riêng của từng pháp nhân).
/// </summary>
/// <param name="CompanyId">Công ty.</param>
/// <param name="CompanyCode">Mã công ty.</param>
/// <param name="CompanyName">Tên công ty.</param>
/// <param name="IsCurrent">Là công ty đang làm việc.</param>
/// <param name="MainQty">Tồn kho Main.</param>
/// <param name="MainAvailable">Tồn kho Main chưa giữ chỗ – phần có thể bán nội bộ (liên công ty chỉ đi kho Main → kho Main).</param>
/// <param name="SubQty">Tồn các kho Sub (chỉ chuyển trong công ty).</param>
/// <param name="TotalQty">Tổng tồn.</param>
public sealed record GroupStockRowDto(
    Guid CompanyId, string CompanyCode, string CompanyName, bool IsCurrent, decimal MainQty, decimal MainAvailable, decimal SubQty, decimal TotalQty);

/// <summary>Tồn tập đoàn của một mặt hàng.</summary>
/// <param name="ItemId">Mặt hàng.</param>
/// <param name="ItemCode">Mã hàng.</param>
/// <param name="ItemName">Tên hàng.</param>
/// <param name="UomCode">ĐVT cơ sở.</param>
/// <param name="MinStock">Tồn tối thiểu của mặt hàng.</param>
/// <param name="Items">Các công ty còn tồn (khác 0), theo mã công ty.</param>
public sealed record GroupStockResult(Guid ItemId, string ItemCode, string ItemName, string UomCode, decimal? MinStock, IReadOnlyList<GroupStockRowDto> Items);

/// <summary>
/// Báo cáo kho còn lại của design.md mục 11 (nhóm Kho, Tồn tập đoàn) – chi tiết quy tắc ở mục 11.2.
/// </summary>
public interface IStockReportService
{
    /// <summary>Hàng còn tồn mà không xuất trong <see cref="SlowMovingQuery.Days"/> ngày, trong phạm vi kho và nhóm hàng của người xem.</summary>
    /// <param name="query">Điều kiện.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Báo cáo.</returns>
    /// <exception cref="Domain.Common.BusinessRuleException">Khi số ngày ngoài khoảng cho phép.</exception>
    Task<SlowMovingResult> GetSlowMovingAsync(SlowMovingQuery query, CancellationToken cancellationToken);

    /// <summary>Lô còn tồn có hạn dùng trong <see cref="ExpiringQuery.Days"/> ngày tới hoặc đã hết hạn.</summary>
    /// <param name="query">Điều kiện.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Báo cáo.</returns>
    /// <exception cref="Domain.Common.BusinessRuleException">Khi số ngày ngoài khoảng cho phép.</exception>
    Task<ExpiringResult> GetExpiringAsync(ExpiringQuery query, CancellationToken cancellationToken);

    /// <summary>Tồn một mặt hàng ở mọi công ty – để gợi ý mua nội bộ thay vì mua mới. Cần quyền <c>Report.Stock.ViewAllCompanies</c>.</summary>
    /// <param name="itemId">Mặt hàng.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Báo cáo.</returns>
    /// <exception cref="Common.ForbiddenAccessException">Khi thiếu quyền xem toàn tập đoàn.</exception>
    /// <exception cref="Common.NotFoundException">Khi mặt hàng không có hoặc ngoài phạm vi nhóm hàng.</exception>
    Task<GroupStockResult> GetGroupStockAsync(Guid itemId, CancellationToken cancellationToken);
}
