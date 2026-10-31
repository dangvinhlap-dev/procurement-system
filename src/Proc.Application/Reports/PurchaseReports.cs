namespace Proc.Application.Reports;

/// <summary>Một dòng PR chưa xử lý: chờ duyệt, hoặc đã duyệt mà chưa lên PO đủ số lượng.</summary>
/// <param name="RequestId">PR.</param>
/// <param name="RequestNo">Số PR.</param>
/// <param name="Date">Ngày PR.</param>
/// <param name="Department">Bộ phận đề nghị.</param>
/// <param name="Status">Trạng thái PR: Submitted (chờ duyệt) hoặc Approved (đã duyệt, chờ đặt hàng).</param>
/// <param name="Priority">Mức ưu tiên: Low, Normal, Urgent.</param>
/// <param name="LineNo">Số thứ tự dòng.</param>
/// <param name="ItemId">Mặt hàng.</param>
/// <param name="ItemCode">Mã hàng.</param>
/// <param name="ItemName">Tên hàng.</param>
/// <param name="UomCode">ĐVT trên PR.</param>
/// <param name="Qty">SL đề nghị.</param>
/// <param name="OrderedQty">SL đã lên PO chưa hủy (quy về ĐVT của dòng PR).</param>
/// <param name="RemainingQty">SL còn phải đặt.</param>
/// <param name="EstAmount">Giá trị ước tính phần còn lại = SL còn lại × giá tham khảo.</param>
/// <param name="NeededDate">Ngày cần hàng (của dòng, không có thì của PR).</param>
/// <param name="OverdueDays">Số ngày đã quá ngày cần (0 nếu chưa đến hoặc không có ngày cần).</param>
public sealed record PendingRequestLineDto(
    Guid RequestId, string RequestNo, DateOnly Date, string? Department, string Status, string Priority, int LineNo,
    Guid ItemId, string ItemCode, string ItemName, string UomCode, decimal Qty, decimal OrderedQty, decimal RemainingQty, decimal EstAmount,
    DateOnly? NeededDate, int OverdueDays);

/// <summary>Cách nhóm báo cáo mua hàng.</summary>
public static class PurchaseSpendGroups
{
    /// <summary>Theo nhà cung cấp.</summary>
    public const string Supplier = "supplier";

    /// <summary>Theo nhóm hàng.</summary>
    public const string Category = "category";

    /// <summary>Theo mặt hàng.</summary>
    public const string Item = "item";

    /// <summary>Theo công ty.</summary>
    public const string Company = "company";

    /// <summary>Các cách nhóm hợp lệ.</summary>
    public static readonly IReadOnlySet<string> All = new HashSet<string> { Supplier, Category, Item, Company };
}

/// <summary>Điều kiện báo cáo mua hàng, đánh giá NCC, tiết kiệm đấu thầu.</summary>
/// <param name="From">Từ ngày chứng từ.</param>
/// <param name="To">Đến ngày (tối đa hai năm).</param>
/// <param name="AllCompanies">Toàn tập đoàn – cần quyền <c>Report.Purchase.ViewAllCompanies</c>.</param>
public sealed record PurchaseReportPeriod(DateOnly From, DateOnly To, bool AllCompanies);

/// <summary>Một dòng mua hàng theo nhóm: giá trị trước VAT của phiếu nhập mua NCC và phiếu trả NCC đã ghi sổ.</summary>
/// <param name="Key">Id của nhóm (NCC, nhóm hàng, mặt hàng, công ty).</param>
/// <param name="Code">Mã.</param>
/// <param name="Name">Tên.</param>
/// <param name="ReceiptCount">Số phiếu nhập mua.</param>
/// <param name="ReceiptAmount">Giá trị nhập mua.</param>
/// <param name="ReturnAmount">Giá trị trả NCC.</param>
/// <param name="NetAmount">Mua thuần = nhập − trả.</param>
/// <param name="SharePct">Tỷ trọng trong tổng mua thuần (%), 2 số lẻ.</param>
public sealed record PurchaseSpendRowDto(
    Guid Key, string Code, string Name, int ReceiptCount, decimal ReceiptAmount, decimal ReturnAmount, decimal NetAmount, decimal SharePct);

/// <summary>Báo cáo mua hàng theo nhóm.</summary>
/// <param name="GroupBy">Cách nhóm đã dùng.</param>
/// <param name="AllCompanies">Đã xem toàn tập đoàn.</param>
/// <param name="TotalNet">Tổng mua thuần.</param>
/// <param name="Items">Các dòng, mua thuần giảm dần.</param>
public sealed record PurchaseSpendResult(string GroupBy, bool AllCompanies, decimal TotalNet, IReadOnlyList<PurchaseSpendRowDto> Items);

/// <summary>
/// Chỉ số đánh giá một NCC trong khoảng ngày (design.md mục 5.3, 11 – nhóm NCC): giao đúng hạn so với ngày giao trên PO, trả hàng.
/// </summary>
/// <param name="SupplierId">NCC.</param>
/// <param name="SupplierCode">Mã NCC.</param>
/// <param name="SupplierName">Tên NCC.</param>
/// <param name="ReceiptCount">Số phiếu nhập mua.</param>
/// <param name="ReceiptAmount">Giá trị nhập mua (trước VAT).</param>
/// <param name="ReturnAmount">Giá trị trả NCC.</param>
/// <param name="ReturnPct">Tỷ lệ trả = trả ÷ nhập × 100 (null khi không nhập).</param>
/// <param name="DeliveryLines">Số dòng nhập theo PO có ngày giao hẹn.</param>
/// <param name="OnTimeLines">Số dòng giao đúng hoặc trước hạn.</param>
/// <param name="OnTimePct">Tỷ lệ đúng hạn (%), null khi không có dòng nào có ngày hẹn.</param>
/// <param name="AvgLateDays">Số ngày trễ bình quân của các dòng trễ (null nếu không trễ dòng nào).</param>
/// <param name="Rating">Xếp loại theo hạng đã cấu hình: mã hạng, "Không đạt", hoặc null khi chưa cấu hình hạng.</param>
public sealed record SupplierEvaluationRowDto(
    Guid SupplierId, string SupplierCode, string SupplierName, int ReceiptCount, decimal ReceiptAmount, decimal ReturnAmount, decimal? ReturnPct,
    int DeliveryLines, int OnTimeLines, decimal? OnTimePct, decimal? AvgLateDays, string? Rating);

/// <summary>Một dòng RFQ đã chọn thầu và tiền tiết kiệm so với các giá chào (giá sau chiết khấu, trước VAT, theo ĐVT của dòng RFQ).</summary>
/// <param name="RfqId">RFQ.</param>
/// <param name="RfqNo">Số RFQ.</param>
/// <param name="Title">Tiêu đề RFQ.</param>
/// <param name="AwardDate">Ngày chọn thầu.</param>
/// <param name="CompanyCode">Công ty lập RFQ.</param>
/// <param name="ItemCode">Mã hàng.</param>
/// <param name="ItemName">Tên hàng.</param>
/// <param name="UomCode">ĐVT.</param>
/// <param name="Qty">SL.</param>
/// <param name="SupplierCode">NCC trúng thầu.</param>
/// <param name="SupplierName">Tên NCC trúng thầu.</param>
/// <param name="QuoteCount">Số NCC chào giá dòng này.</param>
/// <param name="AwardedPrice">Giá trúng.</param>
/// <param name="MinPrice">Giá chào thấp nhất.</param>
/// <param name="AvgPrice">Giá chào bình quân (số học, 6 số lẻ).</param>
/// <param name="MaxPrice">Giá chào cao nhất.</param>
/// <param name="AwardedAmount">Thành tiền trúng thầu = SL × giá trúng.</param>
/// <param name="SavingVsAvg">Tiết kiệm so với giá chào bình quân = (bình quân − trúng) × SL; âm khi chọn giá cao hơn bình quân.</param>
/// <param name="SavingVsMax">Tiết kiệm so với giá chào cao nhất = (cao nhất − trúng) × SL.</param>
/// <param name="IsLowest">Chọn đúng giá thấp nhất.</param>
/// <param name="AwardReason">Lý do khi không chọn giá thấp nhất.</param>
public sealed record SourcingSavingRowDto(
    Guid RfqId, string RfqNo, string Title, DateOnly AwardDate, string CompanyCode, string ItemCode, string ItemName, string UomCode, decimal Qty,
    string SupplierCode, string SupplierName, int QuoteCount, decimal AwardedPrice, decimal MinPrice, decimal AvgPrice, decimal MaxPrice,
    decimal AwardedAmount, decimal SavingVsAvg, decimal SavingVsMax, bool IsLowest, string? AwardReason);

/// <summary>
/// Báo cáo mua hàng, đánh giá NCC, tiết kiệm từ đấu thầu (design.md mục 11, 11.3). Mặc định chỉ công ty đang làm việc; xem toàn tập đoàn cần
/// quyền <c>Report.Purchase.ViewAllCompanies</c>.
/// </summary>
public interface IPurchaseReportService
{
    /// <summary>Dòng PR chờ duyệt hoặc đã duyệt mà chưa lên PO đủ (công ty đang làm việc).</summary>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Các dòng, ngày cần tăng dần (dòng không có ngày cần ở cuối).</returns>
    Task<IReadOnlyList<PendingRequestLineDto>> GetPendingRequestsAsync(CancellationToken cancellationToken);

    /// <summary>Mua hàng thuần theo NCC, nhóm hàng, mặt hàng hoặc công ty.</summary>
    /// <param name="period">Khoảng ngày, phạm vi.</param>
    /// <param name="groupBy">Cách nhóm, xem <see cref="PurchaseSpendGroups"/>.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Báo cáo.</returns>
    /// <exception cref="Domain.Common.BusinessRuleException">Khi khoảng ngày sai hoặc cách nhóm không hợp lệ.</exception>
    /// <exception cref="Common.ForbiddenAccessException">Khi xem toàn tập đoàn mà thiếu quyền.</exception>
    Task<PurchaseSpendResult> GetPurchaseSpendAsync(PurchaseReportPeriod period, string groupBy, CancellationToken cancellationToken);

    /// <summary>Đánh giá NCC: đúng hạn, trả hàng.</summary>
    /// <param name="period">Khoảng ngày, phạm vi.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Các NCC có nhập hàng trong khoảng, giá trị nhập giảm dần.</returns>
    /// <exception cref="Domain.Common.BusinessRuleException">Khi khoảng ngày sai.</exception>
    /// <exception cref="Common.ForbiddenAccessException">Khi xem toàn tập đoàn mà thiếu quyền.</exception>
    Task<IReadOnlyList<SupplierEvaluationRowDto>> GetSupplierEvaluationAsync(PurchaseReportPeriod period, CancellationToken cancellationToken);

    /// <summary>Tiết kiệm từ đấu thầu: các dòng RFQ chọn thầu trong khoảng ngày.</summary>
    /// <param name="period">Khoảng ngày chọn thầu, phạm vi.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Các dòng, ngày chọn thầu giảm dần.</returns>
    /// <exception cref="Domain.Common.BusinessRuleException">Khi khoảng ngày sai.</exception>
    /// <exception cref="Common.ForbiddenAccessException">Khi xem toàn tập đoàn mà thiếu quyền.</exception>
    Task<IReadOnlyList<SourcingSavingRowDto>> GetSourcingSavingsAsync(PurchaseReportPeriod period, CancellationToken cancellationToken);
}
