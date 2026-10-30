namespace Proc.Application.Inventory;

/// <summary>Điều kiện xem tồn kho hiện tại.</summary>
/// <param name="WarehouseId">Một kho; null = mọi kho được phép.</param>
/// <param name="CategoryId">Nhóm hàng (gồm nhóm con); null = mọi nhóm được phép.</param>
/// <param name="IncludeZero">Gồm cả dòng tồn bằng 0 (hàng đã xuất hết).</param>
public sealed record StockOnHandQuery(Guid? WarehouseId, Guid? CategoryId, bool IncludeZero);

/// <summary>Một dòng tồn kho hiện tại: kho × mặt hàng × lô.</summary>
/// <param name="WarehouseId">Kho.</param>
/// <param name="WarehouseCode">Mã kho.</param>
/// <param name="WarehouseName">Tên kho.</param>
/// <param name="ItemId">Mặt hàng.</param>
/// <param name="ItemCode">Mã hàng.</param>
/// <param name="ItemName">Tên hàng.</param>
/// <param name="CategoryCode">Mã nhóm hàng.</param>
/// <param name="CategoryName">Tên nhóm hàng.</param>
/// <param name="UomCode">ĐVT cơ sở.</param>
/// <param name="LotNo">Số lô (null = không theo lô).</param>
/// <param name="Qty">Số lượng tồn theo ĐVT cơ sở.</param>
/// <param name="Amount">Giá trị tồn theo sổ kho (giá vốn tạm tính khi kỳ còn mở).</param>
/// <param name="UnitCost">Đơn giá bình quân = giá trị ÷ số lượng (0 khi tồn ≤ 0).</param>
/// <param name="MinStock">Tồn tối thiểu của mặt hàng (một số cho cả công ty).</param>
/// <param name="BelowMin">Tổng tồn mặt hàng trên mọi kho của công ty (mọi lô) dưới mức tối thiểu – không phụ thuộc kho đang lọc.</param>
public sealed record StockOnHandRowDto(
    Guid WarehouseId, string WarehouseCode, string WarehouseName, Guid ItemId, string ItemCode, string ItemName,
    string CategoryCode, string CategoryName, string UomCode, string? LotNo, decimal Qty, decimal Amount, decimal UnitCost,
    decimal? MinStock, bool BelowMin);

/// <summary>Kết quả xem tồn kho.</summary>
/// <param name="Items">Các dòng, theo mã kho, mã hàng, lô.</param>
/// <param name="Truncated">Còn dòng chưa trả về (vượt giới hạn) – cần lọc hẹp hơn.</param>
/// <param name="Limit">Số dòng tối đa mỗi lần.</param>
public sealed record StockOnHandResult(IReadOnlyList<StockOnHandRowDto> Items, bool Truncated, int Limit);

/// <summary>Điều kiện xem thẻ kho.</summary>
/// <param name="ItemId">Mặt hàng.</param>
/// <param name="WarehouseId">Một kho; null = mọi kho được phép (cộng chung).</param>
/// <param name="From">Từ ngày (ngày chứng từ).</param>
/// <param name="To">Đến ngày.</param>
public sealed record StockCardQuery(Guid ItemId, Guid? WarehouseId, DateOnly From, DateOnly To);

/// <summary>Một phát sinh trên thẻ kho.</summary>
/// <param name="Date">Ngày chứng từ.</param>
/// <param name="DocumentId">Chứng từ (để mở phiếu).</param>
/// <param name="DocumentNo">Số phiếu.</param>
/// <param name="DocType">Loại chứng từ (GRN, RTV, TRF…).</param>
/// <param name="WarehouseCode">Kho phát sinh.</param>
/// <param name="Description">Diễn giải: NCC, hoặc kho đi/đến của phiếu chuyển.</param>
/// <param name="LotNo">Số lô.</param>
/// <param name="InQty">Số lượng nhập (0 nếu là xuất).</param>
/// <param name="OutQty">Số lượng xuất (0 nếu là nhập).</param>
/// <param name="BalanceQty">Tồn lũy kế sau phát sinh.</param>
/// <param name="UnitCost">Đơn giá vốn.</param>
/// <param name="Amount">Giá trị có dấu: + nhập, − xuất.</param>
/// <param name="IsCostFinal">Giá đã chốt (false = tạm tính, chốt khi đóng kỳ).</param>
public sealed record StockCardEntryDto(
    DateOnly Date, Guid DocumentId, string DocumentNo, string DocType, string WarehouseCode, string? Description, string? LotNo,
    decimal InQty, decimal OutQty, decimal BalanceQty, decimal UnitCost, decimal Amount, bool IsCostFinal);

/// <summary>Thẻ kho một mặt hàng trong khoảng ngày.</summary>
/// <param name="ItemCode">Mã hàng.</param>
/// <param name="ItemName">Tên hàng.</param>
/// <param name="UomCode">ĐVT cơ sở.</param>
/// <param name="OpeningQty">Tồn đầu (trước ngày bắt đầu).</param>
/// <param name="OpeningAmount">Giá trị tồn đầu.</param>
/// <param name="Entries">Phát sinh theo ngày, rồi theo thứ tự ghi sổ.</param>
/// <param name="InQty">Tổng nhập trong khoảng.</param>
/// <param name="OutQty">Tổng xuất trong khoảng.</param>
/// <param name="ClosingQty">Tồn cuối.</param>
/// <param name="ClosingAmount">Giá trị tồn cuối.</param>
public sealed record StockCardDto(
    string ItemCode, string ItemName, string UomCode, decimal OpeningQty, decimal OpeningAmount, IReadOnlyList<StockCardEntryDto> Entries,
    decimal InQty, decimal OutQty, decimal ClosingQty, decimal ClosingAmount);

/// <summary>Điều kiện báo cáo nhập – xuất – tồn.</summary>
/// <param name="PeriodId">Kỳ tháng.</param>
/// <param name="WarehouseId">Một kho; null = mọi kho được phép.</param>
/// <param name="CategoryId">Nhóm hàng (gồm nhóm con); null = mọi nhóm được phép.</param>
public sealed record StockMovementQuery(Guid PeriodId, Guid? WarehouseId, Guid? CategoryId);

/// <summary>
/// Một dòng nhập – xuất – tồn theo kho × mặt hàng (cộng mọi lô), SL theo ĐVT cơ sở: tồn đầu; nhập (nhập mua NCC, chuyển kho đến,
/// tổng nhập); xuất (trả NCC, chuyển kho đi, tổng xuất); tồn cuối = đầu + nhập − xuất.
/// </summary>
public sealed record StockMovementRowDto(
    Guid WarehouseId, string WarehouseCode, string WarehouseName, Guid ItemId, string ItemCode, string ItemName,
    string CategoryCode, string CategoryName, string UomCode,
    decimal OpenQty, decimal OpenAmount,
    decimal PurchaseQty, decimal PurchaseAmount, decimal TransferInQty, decimal TransferInAmount, decimal InQty, decimal InAmount,
    decimal ReturnQty, decimal ReturnAmount, decimal TransferOutQty, decimal TransferOutAmount, decimal OutQty, decimal OutAmount,
    decimal CloseQty, decimal CloseAmount);

/// <summary>Báo cáo nhập – xuất – tồn một kỳ.</summary>
/// <param name="PeriodLabel">Nhãn kỳ, ví dụ "10/2026".</param>
/// <param name="PeriodStatus">Trạng thái kỳ.</param>
/// <param name="IsFinal">Kỳ đã đóng: giá trị là giá vốn chính thức; ngược lại là tạm tính.</param>
/// <param name="Items">Các dòng theo mã kho, mã hàng.</param>
public sealed record StockMovementResult(string PeriodLabel, string PeriodStatus, bool IsFinal, IReadOnlyList<StockMovementRowDto> Items);

/// <summary>
/// Xem tồn kho hiện tại và thẻ kho (design.md mục 11, 12) của công ty đang làm việc, trong phạm vi kho và nhóm hàng của người xem.
/// </summary>
public interface IStockQueryService
{
    /// <summary>Tồn kho hiện tại theo kho × mặt hàng × lô.</summary>
    /// <param name="query">Điều kiện lọc.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Các dòng tồn (có giới hạn số dòng).</returns>
    Task<StockOnHandResult> GetOnHandAsync(StockOnHandQuery query, CancellationToken cancellationToken);

    /// <summary>Thẻ kho một mặt hàng: tồn đầu, từng phát sinh với tồn lũy kế, tồn cuối.</summary>
    /// <param name="query">Mặt hàng, kho, khoảng ngày.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Thẻ kho.</returns>
    /// <exception cref="Domain.Common.BusinessRuleException">Khi từ ngày sau đến ngày hoặc khoảng ngày quá dài.</exception>
    /// <exception cref="Common.NotFoundException">Khi mặt hàng hoặc kho không có (hoặc ngoài phạm vi).</exception>
    Task<StockCardDto> GetStockCardAsync(StockCardQuery query, CancellationToken cancellationToken);

    /// <summary>Báo cáo nhập – xuất – tồn một kỳ tháng theo kho × mặt hàng (design.md mục 11).</summary>
    /// <param name="query">Kỳ, kho, nhóm hàng.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Báo cáo.</returns>
    /// <exception cref="Common.NotFoundException">Khi không có kỳ tháng.</exception>
    Task<StockMovementResult> GetMovementAsync(StockMovementQuery query, CancellationToken cancellationToken);
}
