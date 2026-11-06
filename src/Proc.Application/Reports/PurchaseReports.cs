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

/// <summary>Một dòng báo cáo SLA mua hàng (design.md mục 18.13).</summary>
/// <param name="Category">Priced (đã có hợp đồng/bảng giá), Sourcing (chưa có NCC được duyệt), Urgent (PR khẩn).</param>
/// <param name="SlaDays">Số ngày làm việc theo tham số công ty.</param>
/// <param name="DueDate">Hạn = ngày PR duyệt + SlaDays ngày làm việc.</param>
/// <param name="OrderNo">PO đầu tiên được duyệt có dòng lấy từ dòng PR này.</param>
/// <param name="Status">OnTime, Late, InProgress (chưa có PO, còn hạn), Overdue (chưa có PO, quá hạn).</param>
public sealed record PurchaseSlaRowDto(
    Guid RequestId, string RequestNo, DateOnly ApprovedDate, string? Department, int LineNo, string ItemCode, string ItemName, string Category,
    int SlaDays, DateOnly DueDate, Guid? OrderId, string? OrderNo, DateOnly? OrderApprovedDate, string Status);

/// <summary>Báo cáo SLA mua hàng: dòng và tổng hợp.</summary>
/// <param name="OnTimePct">% đúng SLA = đúng hạn / (đúng hạn + trễ + quá hạn), 1 số lẻ; null khi chưa có dòng nào đến hạn hoặc có PO.</param>
public sealed record PurchaseSlaResult(
    IReadOnlyList<PurchaseSlaRowDto> Rows, int OnTime, int Late, int InProgress, int Overdue, decimal? OnTimePct);

/// <summary>Một dòng nhận hàng có sai lệch (design.md mục 18.14).</summary>
/// <param name="DeliveredQty">SL theo chứng từ giao của NCC (null = không ghi).</param>
/// <param name="Qty">SL thực nhận đạt, nhập kho.</param>
/// <param name="RejectedQty">SL không đạt, không nhập kho.</param>
/// <param name="DiscrepancyQty">Thiếu (âm) / thừa (dương) so với chứng từ giao.</param>
/// <param name="QcResult">NotChecked, Passed, Failed.</param>
public sealed record ReceiptQualityRowDto(
    Guid DocumentId, string DocumentNo, DateOnly Date, string SupplierCode, string SupplierName, string? PurchaseOrderNo, int LineNo,
    string ItemCode, string ItemName, string UomCode, decimal? DeliveredQty, decimal Qty, decimal RejectedQty, decimal DiscrepancyQty,
    string QcResult, string? QcNote);

/// <summary>Báo cáo sai lệch nhận hàng: dòng sai lệch và tỷ lệ đạt chất lượng.</summary>
/// <param name="CheckedLines">Số dòng phiếu nhập mua đã ghi sổ trong kỳ có kết quả kiểm tra.</param>
/// <param name="PassedLines">Số dòng đạt.</param>
/// <param name="PassedPct">% đạt = đạt / đã kiểm tra, 1 số lẻ; null khi chưa có dòng nào kiểm tra.</param>
public sealed record ReceiptQualityResult(
    IReadOnlyList<ReceiptQualityRowDto> Rows, int TotalLines, int CheckedLines, int PassedLines, decimal? PassedPct);

/// <summary>Mã cảnh báo trên báo cáo đối chiếu PO – nhận – hóa đơn (design.md mục 18.15); giao diện đổi sang chữ tiếng Việt.</summary>
public static class ThreeWayMatchIssues
{
    /// <summary>Chưa nhập số hóa đơn hoặc tiền hóa đơn.</summary>
    public const string NoInvoice = "NoInvoice";

    /// <summary>Tiền hóa đơn lệch tiền phiếu nhập quá dung sai.</summary>
    public const string AmountMismatch = "AmountMismatch";

    /// <summary>Có dòng nhận ít hơn chứng từ giao (thiếu hoặc không đạt) mà hóa đơn vẫn cao hơn tiền thực nhận – V7, V8.</summary>
    public const string InvoiceIncludesShortage = "InvoiceIncludesShortage";

    /// <summary>Phiếu nhập không theo đơn mua hàng.</summary>
    public const string NoPurchaseOrder = "NoPurchaseOrder";

    /// <summary>Ngày nhập trước ngày đơn mua hàng – dấu hiệu lập PO hợp thức hóa (mục 18.11).</summary>
    public const string ReceivedBeforeOrder = "ReceivedBeforeOrder";

    /// <summary>Ngày nhập trước ngày PO được duyệt (nhưng không trước ngày PO).</summary>
    public const string ReceivedBeforeApproval = "ReceivedBeforeApproval";

    /// <summary>Ngày hóa đơn trước ngày đơn mua hàng.</summary>
    public const string InvoiceBeforeOrder = "InvoiceBeforeOrder";
}

/// <summary>Một phiếu nhập mua trên báo cáo đối chiếu PO – nhận – hóa đơn (design.md mục 18.15).</summary>
/// <param name="DocumentId">Phiếu nhập mua.</param>
/// <param name="DocumentNo">Số phiếu nhập.</param>
/// <param name="Date">Ngày nhập.</param>
/// <param name="SupplierCode">Mã NCC.</param>
/// <param name="SupplierName">Tên NCC.</param>
/// <param name="PurchaseOrderId">Đơn mua hàng nguồn (null = nhập không theo PO).</param>
/// <param name="PurchaseOrderNo">Số PO.</param>
/// <param name="PurchaseOrderDate">Ngày PO.</param>
/// <param name="PurchaseOrderApprovedDate">Ngày PO được duyệt (giờ Việt Nam).</param>
/// <param name="OrderTotal">Tổng thanh toán của PO (gồm VAT) – cả PO, có thể nhận nhiều lần.</param>
/// <param name="ReceiptTotal">Tổng thanh toán của phiếu nhập (tiền hàng + VAT) theo SL thực nhận đạt.</param>
/// <param name="InvoiceNo">Số hóa đơn.</param>
/// <param name="InvoiceSeries">Ký hiệu hóa đơn.</param>
/// <param name="InvoiceDate">Ngày hóa đơn.</param>
/// <param name="InvoiceAmount">Tổng tiền thanh toán trên hóa đơn (null = chưa nhập).</param>
/// <param name="Difference">Hóa đơn − phiếu nhập (null khi chưa có tiền hóa đơn).</param>
/// <param name="Result">Matched (khớp), NoInvoice (chưa có hóa đơn), Mismatch (lệch tiền).</param>
/// <param name="Issues">Các mã cảnh báo <see cref="ThreeWayMatchIssues"/>.</param>
public sealed record ThreeWayMatchRowDto(
    Guid DocumentId, string DocumentNo, DateOnly Date, string SupplierCode, string SupplierName, Guid? PurchaseOrderId, string? PurchaseOrderNo,
    DateOnly? PurchaseOrderDate, DateOnly? PurchaseOrderApprovedDate, decimal? OrderTotal, decimal ReceiptTotal, string? InvoiceNo,
    string? InvoiceSeries, DateOnly? InvoiceDate, decimal? InvoiceAmount, decimal? Difference, string Result, IReadOnlyList<string> Issues);

/// <summary>Báo cáo đối chiếu PO – nhận – hóa đơn: dòng và tổng hợp.</summary>
/// <param name="Rows">Phiếu nhập (đã lọc nếu chỉ lấy phiếu có cảnh báo).</param>
/// <param name="Matched">Số phiếu khớp (trên toàn bộ phiếu trong khoảng ngày, trước khi lọc).</param>
/// <param name="NoInvoice">Số phiếu chưa có hóa đơn.</param>
/// <param name="Mismatch">Số phiếu lệch tiền.</param>
/// <param name="WithIssues">Số phiếu có ít nhất một cảnh báo.</param>
/// <param name="ReceiptTotal">Tổng tiền phiếu nhập.</param>
/// <param name="InvoiceTotal">Tổng tiền hóa đơn đã nhập.</param>
/// <param name="HiddenDocuments">Số phiếu bỏ qua vì có mặt hàng ngoài phạm vi nhóm hàng của người xem (không đối chiếu được tiền cả phiếu).</param>
public sealed record ThreeWayMatchResult(
    IReadOnlyList<ThreeWayMatchRowDto> Rows, int Matched, int NoInvoice, int Mismatch, int WithIssues, decimal ReceiptTotal, decimal InvoiceTotal,
    int HiddenDocuments);

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

/// <summary>Tổng hợp giá trị dòng PO theo một nguồn giá (design.md mục 18.17).</summary>
/// <param name="Source">Rfq, Contract, PriceList (theo giá đã duyệt); Deviation (lệch giá đã duyệt); None (không có giá đã duyệt);
/// Unknown (PO lập trước bản 0.12.0).</param>
/// <param name="LineCount">Số dòng PO.</param>
/// <param name="Amount">Thành tiền sau chiết khấu, trước VAT.</param>
/// <param name="SharePct">Tỷ trọng trong tổng (%), 2 số lẻ.</param>
public sealed record PriceSourceSummaryDto(string Source, int LineCount, decimal Amount, decimal SharePct);

/// <summary>Một dòng PO mua ngoài hợp đồng: không có giá đã duyệt hoặc lệch giá đã duyệt (design.md mục 18.10, 18.17).</summary>
/// <param name="OrderId">PO.</param>
/// <param name="OrderNo">Số PO.</param>
/// <param name="Date">Ngày PO.</param>
/// <param name="CompanyCode">Công ty.</param>
/// <param name="Department">Bộ phận đặt hàng (nếu có).</param>
/// <param name="SupplierCode">Mã NCC.</param>
/// <param name="SupplierName">Tên NCC.</param>
/// <param name="LineNo">Số dòng trên PO.</param>
/// <param name="ItemCode">Mã hàng.</param>
/// <param name="ItemName">Tên hàng.</param>
/// <param name="UomCode">ĐVT dòng.</param>
/// <param name="Qty">SL.</param>
/// <param name="NetUnitPrice">Đơn giá thực sau chiết khấu (theo ĐVT dòng).</param>
/// <param name="ApprovedNetPrice">Giá đã duyệt sau chiết khấu; null khi không có giá đã duyệt.</param>
/// <param name="DeviationPct">% lệch = (giá thực − giá đã duyệt) ÷ giá đã duyệt × 100, 2 số lẻ; null khi không có giá đã duyệt hoặc giá đã duyệt = 0.</param>
/// <param name="Amount">Thành tiền sau chiết khấu, trước VAT.</param>
/// <param name="DeviationAmount">Tiền chênh so với giá đã duyệt = (giá thực − giá đã duyệt) × SL; null khi không có giá đã duyệt.</param>
/// <param name="Kind">Deviation (lệch giá) hoặc None (không có giá đã duyệt).</param>
/// <param name="Reason">Lý do lệch giá.</param>
public sealed record OutOfContractLineDto(
    Guid OrderId, string OrderNo, DateOnly Date, string CompanyCode, string? Department, string SupplierCode, string SupplierName, int LineNo,
    string ItemCode, string ItemName, string UomCode, decimal Qty, decimal NetUnitPrice, decimal? ApprovedNetPrice, decimal? DeviationPct,
    decimal Amount, decimal? DeviationAmount, string Kind, string? Reason);

/// <summary>Báo cáo giá mua & mua ngoài hợp đồng (báo cáo tuần – design.md mục 18.17).</summary>
/// <param name="OrderCount">Số PO đã duyệt trong kỳ.</param>
/// <param name="TotalAmount">Tổng thành tiền dòng PO (trước VAT).</param>
/// <param name="CompliancePct">Tỷ lệ mua theo giá đã duyệt = giá trị theo giá đã duyệt (không lệch) ÷ (tổng − dòng không xác định) × 100,
/// 2 số lẻ; null khi không có dòng xác định được nguồn giá. KPI Ban CUVT ≥ 95%.</param>
/// <param name="Sources">Tổng hợp theo nguồn giá (chỉ nguồn có dòng).</param>
/// <param name="Lines">Các dòng ngoài hợp đồng, tiền chênh/thành tiền giảm dần.</param>
public sealed record ContractComplianceResult(
    int OrderCount, decimal TotalAmount, decimal? CompliancePct, IReadOnlyList<PriceSourceSummaryDto> Sources, IReadOnlyList<OutOfContractLineDto> Lines);

/// <summary>Biến động giá mua một mặt hàng: kỳ này so kỳ trước liền trước (design.md mục 18.17). Giá theo ĐVT cơ sở, sau chiết khấu, trước VAT.</summary>
/// <param name="ItemId">Mặt hàng.</param>
/// <param name="ItemCode">Mã hàng.</param>
/// <param name="ItemName">Tên hàng.</param>
/// <param name="BaseUomCode">ĐVT cơ sở.</param>
/// <param name="PreviousQty">SL kỳ trước (ĐVT cơ sở); 0 khi không mua.</param>
/// <param name="PreviousAvgPrice">Giá bình quân gia quyền kỳ trước; null khi không mua.</param>
/// <param name="CurrentQty">SL kỳ này.</param>
/// <param name="CurrentAvgPrice">Giá bình quân gia quyền kỳ này.</param>
/// <param name="MinPrice">Giá thấp nhất kỳ này.</param>
/// <param name="MaxPrice">Giá cao nhất kỳ này.</param>
/// <param name="ChangePct">% thay đổi = (kỳ này − kỳ trước) ÷ kỳ trước × 100, 2 số lẻ; null khi kỳ trước không mua.</param>
/// <param name="ImpactAmount">Ảnh hưởng = (kỳ này − kỳ trước) × SL kỳ này; dương là mua đắt hơn; null khi kỳ trước không mua.</param>
/// <param name="OrderCount">Số PO kỳ này có mặt hàng.</param>
/// <param name="SupplierCount">Số NCC kỳ này.</param>
public sealed record PriceVarianceRowDto(
    Guid ItemId, string ItemCode, string ItemName, string BaseUomCode, decimal PreviousQty, decimal? PreviousAvgPrice, decimal CurrentQty,
    decimal CurrentAvgPrice, decimal MinPrice, decimal MaxPrice, decimal? ChangePct, decimal? ImpactAmount, int OrderCount, int SupplierCount);

/// <summary>Báo cáo biến động giá mua theo kỳ.</summary>
/// <param name="PreviousFrom">Đầu kỳ trước.</param>
/// <param name="PreviousTo">Cuối kỳ trước (ngày liền trước kỳ này).</param>
/// <param name="ImpactAmount">Tổng ảnh hưởng của các mặt hàng có giá kỳ trước.</param>
/// <param name="Items">Các mặt hàng mua trong kỳ này, |ảnh hưởng| giảm dần.</param>
public sealed record PriceVarianceResult(DateOnly PreviousFrom, DateOnly PreviousTo, decimal ImpactAmount, IReadOnlyList<PriceVarianceRowDto> Items);

/// <summary>
/// Tiết kiệm so giá chuẩn của một mặt hàng (lát G4c, design.md mục 18.25): dòng PO đã duyệt bằng VND có dòng phiếu duyệt tháng (GTH) có giá
/// chuẩn. Giá theo ĐVT cơ sở, sau chiết khấu, trước VAT.
/// </summary>
/// <param name="ItemId">Mặt hàng.</param>
/// <param name="ItemCode">Mã hàng.</param>
/// <param name="ItemName">Tên hàng.</param>
/// <param name="BaseUomCode">ĐVT cơ sở.</param>
/// <param name="Qty">SL mua (ĐVT cơ sở).</param>
/// <param name="AvgPrice">Giá mua bình quân gia quyền = thành tiền ÷ SL cơ sở (6 số lẻ).</param>
/// <param name="StandardAvgPrice">Giá chuẩn bình quân = giá trị theo giá chuẩn ÷ SL cơ sở (6 số lẻ).</param>
/// <param name="Amount">Thành tiền thực (sau CK, trước VAT).</param>
/// <param name="StandardAmount">Giá trị theo giá chuẩn = Σ giá chuẩn quy ĐVT dòng × SL.</param>
/// <param name="Saving">Tiết kiệm = giá trị theo giá chuẩn − thành tiền; âm là mua cao hơn giá chuẩn.</param>
/// <param name="SavingPct">% tiết kiệm = tiết kiệm ÷ giá trị theo giá chuẩn × 100, 2 số lẻ; null khi giá trị chuẩn = 0.</param>
/// <param name="TargetAmount">Giá trị theo giá mục tiêu của các dòng có giá mục tiêu; null khi không dòng nào có.</param>
/// <param name="TargetGap">Còn cách mục tiêu = thành tiền các dòng có giá mục tiêu − giá trị theo giá mục tiêu; ≤ 0 là đạt; null khi không có.</param>
/// <param name="OverCeilingLines">Số dòng PO có giá ròng vượt giá trần của phiếu tháng.</param>
/// <param name="OrderCount">Số PO.</param>
/// <param name="ApprovalNos">Số phiếu GTH, cách nhau "; ".</param>
public sealed record StandardSavingRowDto(
    Guid ItemId, string ItemCode, string ItemName, string BaseUomCode, decimal Qty, decimal AvgPrice, decimal StandardAvgPrice, decimal Amount,
    decimal StandardAmount, decimal Saving, decimal? SavingPct, decimal? TargetAmount, decimal? TargetGap, int OverCeilingLines, int OrderCount,
    string ApprovalNos);

/// <summary>Báo cáo tiết kiệm so giá chuẩn (lát G4c).</summary>
/// <param name="TotalAmount">Tổng thành tiền mọi dòng PO đã duyệt bằng VND trong kỳ (trong nhóm hàng lọc).</param>
/// <param name="CoveredAmount">Thành tiền các dòng có giá chuẩn.</param>
/// <param name="CoveragePct">Tỷ lệ giá trị mua có giá chuẩn = phần có giá chuẩn ÷ tổng × 100, 2 số lẻ; null khi tổng = 0.</param>
/// <param name="StandardAmount">Tổng giá trị theo giá chuẩn.</param>
/// <param name="Saving">Tổng tiết kiệm (âm là mua cao hơn chuẩn).</param>
/// <param name="SavingPct">% tiết kiệm = tổng tiết kiệm ÷ tổng giá trị chuẩn × 100, 2 số lẻ; null khi giá trị chuẩn = 0.</param>
/// <param name="Items">Các mặt hàng, tiết kiệm giảm dần (mua cao hơn chuẩn nhiều nhất cuối danh sách).</param>
public sealed record StandardSavingResult(
    decimal TotalAmount, decimal CoveredAmount, decimal? CoveragePct, decimal StandardAmount, decimal Saving, decimal? SavingPct,
    IReadOnlyList<StandardSavingRowDto> Items);

/// <summary>
/// Một mặt hàng trong báo cáo NCC đủ điều kiện (design.md mục 18.25 – quyết định thành lập Ban cung ứng: mỗi mặt hàng tối thiểu 03 NCC).
/// </summary>
/// <param name="ItemId">Mặt hàng.</param>
/// <param name="ItemCode">Mã hàng.</param>
/// <param name="ItemName">Tên hàng.</param>
/// <param name="CategoryName">Nhóm hàng.</param>
/// <param name="ApprovedCount">Số NCC Đã duyệt có khai báo bán mặt hàng (NCC – sản phẩm).</param>
/// <param name="PotentialCount">Số NCC Tiềm năng (nguồn bổ sung của Văn phòng Ban).</param>
/// <param name="SuspendedCount">Số NCC Tạm ngưng.</param>
/// <param name="Missing">Số NCC Đã duyệt còn thiếu so với mức tối thiểu (0 khi đủ hoặc được miễn).</param>
/// <param name="Status">Ok (đủ), Short (thiếu), SoleSource (miễn – có phiếu NCC độc quyền còn tác dụng hôm nay).</param>
/// <param name="ApprovedSuppliers">Tên NCC Đã duyệt, NCC ưu tiên đứng đầu, cách nhau "; ".</param>
/// <param name="SoleSourceNo">Số phiếu NCC độc quyền khi được miễn.</param>
public sealed record SupplierCoverageRowDto(
    Guid ItemId, string ItemCode, string ItemName, string CategoryName, int ApprovedCount, int PotentialCount, int SuspendedCount, int Missing,
    string Status, string ApprovedSuppliers, string? SoleSourceNo);

/// <summary>Báo cáo NCC đủ điều kiện theo mặt hàng.</summary>
/// <param name="MinSuppliers">Số NCC tối thiểu đang xét.</param>
/// <param name="ItemCount">Số mặt hàng đang dùng trong phạm vi (trước lọc "chỉ thiếu").</param>
/// <param name="ShortCount">Số mặt hàng thiếu NCC.</param>
/// <param name="Items">Các dòng: thiếu nhiều lên đầu, rồi theo mã hàng.</param>
public sealed record SupplierCoverageResult(int MinSuppliers, int ItemCount, int ShortCount, IReadOnlyList<SupplierCoverageRowDto> Items);

/// <summary>Trạng thái dòng báo cáo NCC đủ điều kiện.</summary>
public static class SupplierCoverageStatuses
{
    /// <summary>Đủ số NCC tối thiểu.</summary>
    public const string Ok = "Ok";

    /// <summary>Thiếu NCC.</summary>
    public const string Short = "Short";

    /// <summary>Miễn vì có NCC độc quyền đã duyệt.</summary>
    public const string SoleSource = "SoleSource";
}

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

    /// <summary>
    /// SLA mua hàng (design.md mục 18.13): mỗi dòng PR (cả hàng mới chưa gắn mã) của PR được duyệt trong khoảng ngày tại công ty đang làm
    /// việc – loại yêu cầu, hạn SLA, PO đầu tiên được duyệt, trạng thái đúng hạn/trễ/đang xử lý/quá hạn; tỷ lệ đúng SLA.
    /// </summary>
    /// <param name="from">Từ ngày PR được duyệt.</param>
    /// <param name="to">Đến ngày PR được duyệt.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Dòng và tổng hợp.</returns>
    Task<PurchaseSlaResult> GetPurchaseSlaAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken);

    /// <summary>
    /// Sai lệch nhận hàng (design.md mục 18.14): dòng phiếu nhập mua NCC đã ghi sổ trong khoảng ngày có hàng không đạt hoặc thiếu/thừa so
    /// với chứng từ giao; tỷ lệ dòng đạt chất lượng trên các dòng đã kiểm tra (KPI "lô đạt chất lượng").
    /// </summary>
    Task<ReceiptQualityResult> GetReceiptQualityAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken);

    /// <summary>
    /// Đối chiếu PO – phiếu nhận – hóa đơn (design.md mục 18.15, báo cáo cho kế toán): mỗi phiếu nhập mua NCC đã ghi sổ trong khoảng ngày
    /// tại công ty đang làm việc – so tiền hóa đơn NCC với tiền phiếu nhập, ngày nhận/hóa đơn với ngày PO.
    /// </summary>
    /// <param name="from">Từ ngày nhập.</param>
    /// <param name="to">Đến ngày nhập.</param>
    /// <param name="tolerance">Dung sai tiền (đ) – lệch tuyệt đối không quá mức này coi là khớp (chênh làm tròn VAT); ≥ 0.</param>
    /// <param name="onlyIssues">true = chỉ trả phiếu có cảnh báo.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Dòng và tổng hợp.</returns>
    /// <exception cref="Domain.Common.BusinessRuleException">Khi khoảng ngày sai, quá 1 năm hoặc dung sai âm.</exception>
    Task<ThreeWayMatchResult> GetThreeWayMatchAsync(DateOnly from, DateOnly to, decimal tolerance, bool onlyIssues, CancellationToken cancellationToken);

    /// <summary>
    /// Giá mua & mua ngoài hợp đồng (báo cáo tuần – design.md mục 18.17): dòng PO đã duyệt (Đã duyệt, Đã đóng) có ngày PO trong khoảng,
    /// tổng hợp theo nguồn giá, tỷ lệ mua theo giá đã duyệt và các dòng ngoài hợp đồng.
    /// </summary>
    /// <param name="period">Khoảng ngày PO, phạm vi.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Báo cáo.</returns>
    /// <exception cref="Domain.Common.BusinessRuleException">Khi khoảng ngày sai.</exception>
    /// <exception cref="Common.ForbiddenAccessException">Khi xem toàn tập đoàn mà thiếu quyền.</exception>
    Task<ContractComplianceResult> GetContractComplianceAsync(PurchaseReportPeriod period, CancellationToken cancellationToken);

    /// <summary>
    /// Biến động giá mua (design.md mục 18.17): giá bình quân gia quyền theo ĐVT cơ sở của dòng PO đã duyệt kỳ này so với kỳ trước liền trước (khoảng trọn tháng thì lùi cùng số tháng, còn lại cùng số ngày).
    /// </summary>
    /// <param name="period">Kỳ này (khoảng ngày PO), phạm vi.</param>
    /// <param name="categoryId">Nhóm hàng (gồm nhóm con); null = mọi nhóm.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Báo cáo.</returns>
    /// <exception cref="Domain.Common.BusinessRuleException">Khi khoảng ngày sai.</exception>
    /// <exception cref="Common.ForbiddenAccessException">Khi xem toàn tập đoàn mà thiếu quyền.</exception>
    Task<PriceVarianceResult> GetPriceVarianceAsync(PurchaseReportPeriod period, Guid? categoryId, CancellationToken cancellationToken);

    /// <summary>
    /// Tiết kiệm so giá chuẩn (lát G4c, design.md mục 18.25): dòng PO Đã duyệt/Đã đóng bằng VND có ngày PO trong khoảng, có dòng phiếu
    /// duyệt tháng có giá chuẩn; tiết kiệm = giá trị theo giá chuẩn − thành tiền, gộp theo mặt hàng; kèm giá mục tiêu, số dòng vượt trần.
    /// </summary>
    /// <param name="period">Khoảng ngày PO, phạm vi.</param>
    /// <param name="categoryId">Nhóm hàng (gồm nhóm con); null = mọi nhóm.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Báo cáo.</returns>
    /// <exception cref="Domain.Common.BusinessRuleException">Khi khoảng ngày sai.</exception>
    /// <exception cref="Common.ForbiddenAccessException">Khi xem toàn tập đoàn mà thiếu quyền.</exception>
    Task<StandardSavingResult> GetStandardSavingsAsync(PurchaseReportPeriod period, Guid? categoryId, CancellationToken cancellationToken);

    /// <summary>
    /// NCC đủ điều kiện theo mặt hàng (design.md mục 18.25): mỗi mặt hàng đang dùng đếm NCC Đã duyệt khai báo bán mặt hàng; ít hơn
    /// <paramref name="minSuppliers"/> là thiếu, trừ khi công ty đang làm việc có phiếu NCC độc quyền còn tác dụng hôm nay cho mặt hàng.
    /// </summary>
    /// <param name="minSuppliers">Số NCC tối thiểu (1–20, quyết định Ban cung ứng: 3).</param>
    /// <param name="categoryId">Nhóm hàng (gồm nhóm con); null = mọi nhóm.</param>
    /// <param name="onlyShort">Chỉ trả mặt hàng thiếu.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Báo cáo.</returns>
    /// <exception cref="Domain.Common.BusinessRuleException">Khi số NCC tối thiểu ngoài 1–20.</exception>
    Task<SupplierCoverageResult> GetSupplierCoverageAsync(int minSuppliers, Guid? categoryId, bool onlyShort, CancellationToken cancellationToken);
}
