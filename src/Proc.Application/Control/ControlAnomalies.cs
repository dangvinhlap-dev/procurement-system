namespace Proc.Application.Control;

/// <summary>Mã loại dấu hiệu bất thường KSNB (design.md mục 18.19); giao diện đổi sang chữ tiếng Việt.</summary>
public static class AnomalyCodes
{
    /// <summary>Mua lặp lại một NCC với giá cao bất thường so với NCC khác cùng mặt hàng.</summary>
    public const string HighPriceRepeat = "HighPriceRepeat";

    /// <summary>Nhiều PO của cùng người lập × NCC có giá trị sát dưới ngưỡng một cấp duyệt (chia nhỏ đơn để né cấp duyệt – V5).</summary>
    public const string NearThreshold = "NearThreshold";

    /// <summary>Đổi tài khoản ngân hàng NCC (đã duyệt) trong kỳ – N2.</summary>
    public const string BankAccountChange = "BankAccountChange";

    /// <summary>Người lập PO có tỷ lệ mua ngoài giá/hợp đồng đã duyệt cao – P5.</summary>
    public const string OffContract = "OffContract";

    /// <summary>Hóa đơn tính cả hàng nhận thiếu/không đạt – V7, V8.</summary>
    public const string InvoiceShortage = "InvoiceShortage";

    /// <summary>Hàng nhận hoặc hóa đơn có trước PO / trước khi PO được duyệt – dấu hiệu hợp thức hóa (mục 18.11).</summary>
    public const string BackdatedOrder = "BackdatedOrder";

    /// <summary>Người lập hoặc người duyệt PO có kê khai quan hệ lợi ích với NCC còn hiệu lực vào ngày PO – N4.</summary>
    public const string ConflictOfInterest = "ConflictOfInterest";

    /// <summary>Chiết khấu doanh số/tài trợ chưa ghi nhận – P10, chưa đo được (cần chiết khấu trên hợp đồng, lát F4).</summary>
    public const string UnrecordedRebate = "UnrecordedRebate";

    /// <summary>Chiết khấu sản lượng gộp chưa ghi nhận – kỳ của thỏa thuận CKB quá hạn ghi nhận mà thực nhận ít hơn phải nhận (lát G6b).</summary>
    public const string UnrecordedVolumeRebate = "UnrecordedVolumeRebate";
}

/// <summary>Mức độ dấu hiệu: High (cao – đèn đỏ), Medium (trung bình – đèn vàng).</summary>
public static class AnomalySeverities
{
    /// <summary>Cao.</summary>
    public const string High = "High";

    /// <summary>Trung bình.</summary>
    public const string Medium = "Medium";
}

/// <summary>Điều kiện rà dấu hiệu bất thường.</summary>
/// <param name="From">Từ ngày (ngày PO, ngày nhập, ngày duyệt đổi tài khoản).</param>
/// <param name="To">Đến ngày; tối đa 1 năm.</param>
/// <param name="AllCompanies">Toàn tập đoàn – cần quyền <c>Report.Purchase.ViewAllCompanies</c>.</param>
/// <param name="PriceGapPct">Giá BQ của NCC cao hơn NCC rẻ nhất khác từ mức này (%) là bất thường; mặc định 10.</param>
/// <param name="NearThresholdPct">"Sát dưới ngưỡng" = từ (100 − mức này)% ngưỡng đến dưới ngưỡng; mặc định 10.</param>
/// <param name="NearThresholdCount">Số PO sát ngưỡng tối thiểu của cùng người lập × NCC; mặc định 3.</param>
/// <param name="OffContractPct">Tỷ lệ mua ngoài giá đã duyệt của người lập vượt mức này (%) là bất thường; mặc định 5 (KPI ≥ 95%).</param>
public sealed record AnomalyQuery(
    DateOnly From, DateOnly To, bool AllCompanies, decimal PriceGapPct = 10m, decimal NearThresholdPct = 10m, int NearThresholdCount = 3,
    decimal OffContractPct = 5m);

/// <summary>Một dấu hiệu bất thường.</summary>
/// <param name="Code">Loại (<see cref="AnomalyCodes"/>).</param>
/// <param name="Severity">Mức (<see cref="AnomalySeverities"/>).</param>
/// <param name="CompanyId">Công ty phát sinh.</param>
/// <param name="CompanyCode">Mã công ty.</param>
/// <param name="Date">Ngày phát sinh (ngày PO/phiếu nhập gần nhất, ngày duyệt đổi tài khoản).</param>
/// <param name="ScreenCode">Màn hình mở chứng từ liên quan (PO, GRN, SUPAPR, PRCW); null nếu không có.</param>
/// <param name="TargetId">Chứng từ cần mở.</param>
/// <param name="RefNo">Số chứng từ liên quan.</param>
/// <param name="SupplierCode">Mã NCC.</param>
/// <param name="SupplierName">Tên NCC.</param>
/// <param name="UserName">Người liên quan (người lập, người duyệt) – họ tên.</param>
/// <param name="Amount">Tiền liên quan (chênh giá, giá trị PO, giá trị ngoài hợp đồng, chênh hóa đơn), VND.</param>
/// <param name="Detail">Mô tả cụ thể để KSNB kiểm tra.</param>
public sealed record AnomalyDto(
    string Code, string Severity, Guid CompanyId, string CompanyCode, DateOnly Date, string? ScreenCode, Guid? TargetId, string? RefNo,
    string? SupplierCode, string? SupplierName, string? UserName, decimal? Amount, string Detail);

/// <summary>Tổng hợp theo loại dấu hiệu.</summary>
/// <param name="Code">Loại.</param>
/// <param name="Count">Số dấu hiệu.</param>
/// <param name="Measured">false = chưa đo được (thiếu dữ liệu nguồn).</param>
/// <param name="Note">Cách phát hiện hoặc lý do chưa đo.</param>
public sealed record AnomalySummaryDto(string Code, int Count, bool Measured, string Note);

/// <summary>Kết quả rà dấu hiệu bất thường.</summary>
/// <param name="From">Từ ngày.</param>
/// <param name="To">Đến ngày.</param>
/// <param name="AllCompanies">Toàn tập đoàn.</param>
/// <param name="Summary">Tổng hợp theo loại, đủ mọi loại kể cả 0.</param>
/// <param name="Items">Dấu hiệu, mức cao trước, rồi ngày mới trước.</param>
public sealed record AnomalyResult(
    DateOnly From, DateOnly To, bool AllCompanies, IReadOnlyList<AnomalySummaryDto> Summary, IReadOnlyList<AnomalyDto> Items);

/// <summary>
/// Rà dấu hiệu bất thường cho Kiểm soát nội bộ (design.md mục 18.19 – đề xuất Ban CUVT mục KSNB). Chỉ đọc, không đổi dữ liệu; phạm vi công ty
/// đang làm việc hoặc toàn tập đoàn, mặt hàng theo phạm vi nhóm hàng của người xem.
/// </summary>
public interface IControlAnomalyService
{
    /// <summary>
    /// Rà dấu hiệu bất thường trong khoảng ngày.
    /// </summary>
    /// <param name="query">Khoảng ngày, phạm vi, ngưỡng.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Tổng hợp và danh sách dấu hiệu.</returns>
    /// <exception cref="Domain.Common.BusinessRuleException">Khi khoảng ngày hoặc ngưỡng không hợp lệ.</exception>
    /// <exception cref="Common.ForbiddenAccessException">Khi xem toàn tập đoàn mà không có quyền.</exception>
    Task<AnomalyResult> GetAsync(AnomalyQuery query, CancellationToken cancellationToken);
}
