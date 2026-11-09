using Proc.Application.Control;
using Proc.Application.Excel;
using Proc.Application.MasterData;
using Proc.Application.Pricing;

namespace Proc.Application.Reports;

/// <summary>
/// Trạng thái một mục của báo cáo tháng cho Ban (lát G12, design.md mục 18.25).
/// </summary>
/// <param name="Code">Mã mục (xem <see cref="BoardReportSections"/>).</param>
/// <param name="Title">Tên mục.</param>
/// <param name="Available">true: có số liệu; false: người xem không có quyền hoặc mục không áp dụng.</param>
/// <param name="Note">Ghi chú phạm vi hoặc lý do không có số liệu.</param>
/// <param name="Report">Mã màn hình báo cáo chi tiết để mở xem tiếp (PRCW, KPI…).</param>
public sealed record BoardReportSectionDto(string Code, string Title, bool Available, string? Note, string? Report);

/// <summary>Tổng quan mua hàng trong tháng so tháng trước – cùng số liệu báo cáo PRCW (design.md mục 18.17).</summary>
/// <param name="OrderCount">Số PO Đã duyệt/Đã đóng có ngày PO trong tháng.</param>
/// <param name="TotalAmount">Giá trị dòng PO sau chiết khấu, trước VAT.</param>
/// <param name="CompliancePct">Tỷ lệ mua theo giá đã duyệt (%); null khi không có dòng xác định được nguồn giá.</param>
/// <param name="OutOfContractAmount">Giá trị mua ngoài giá đã duyệt = lệch giá + không có giá đã duyệt.</param>
/// <param name="PreviousOrderCount">Số PO tháng trước.</param>
/// <param name="PreviousTotalAmount">Giá trị tháng trước.</param>
/// <param name="PreviousCompliancePct">Tỷ lệ mua theo giá đã duyệt tháng trước.</param>
/// <param name="Sources">Cơ cấu theo nguồn giá trong tháng.</param>
public sealed record BoardPurchaseSummaryDto(
    int OrderCount, decimal TotalAmount, decimal? CompliancePct, decimal OutOfContractAmount, int PreviousOrderCount, decimal PreviousTotalAmount,
    decimal? PreviousCompliancePct, IReadOnlyList<PriceSourceSummaryDto> Sources);

/// <summary>Tiết kiệm trong tháng: từ đấu thầu (SAVE) và so giá chuẩn của phiếu duyệt tháng (lát G4c).</summary>
/// <param name="RfqCount">Số RFQ chọn thầu trong tháng.</param>
/// <param name="AwardedAmount">Giá trị trúng thầu = Σ giá trúng × SL.</param>
/// <param name="SourcingSaving">Tiết kiệm so giá chào bình quân = Σ (giá chào bình quân − giá trúng) × SL.</param>
/// <param name="SourcingSavingPct">Tiết kiệm ÷ (giá trị trúng + tiết kiệm) × 100 – như KPI "Tiết kiệm mua hàng"; null khi không có thầu.</param>
/// <param name="StandardCoveredAmount">Giá trị mua có giá chuẩn.</param>
/// <param name="StandardCoveragePct">Tỷ lệ giá trị mua có giá chuẩn (%).</param>
/// <param name="StandardSaving">Tiết kiệm so giá chuẩn (âm = mua cao hơn chuẩn).</param>
/// <param name="StandardSavingPct">Tiết kiệm so giá chuẩn (%).</param>
public sealed record BoardSavingsDto(
    int RfqCount, decimal AwardedAmount, decimal SourcingSaving, decimal? SourcingSavingPct, decimal StandardCoveredAmount,
    decimal? StandardCoveragePct, decimal StandardSaving, decimal? StandardSavingPct);

/// <summary>Biến động giá trong tháng so tháng trước (PRVAR): tổng ảnh hưởng và các mặt hàng tăng giá ảnh hưởng lớn nhất.</summary>
/// <param name="ImpactAmount">Tổng ảnh hưởng tiền của thay đổi giá.</param>
/// <param name="IncreasedCount">Số mặt hàng tăng giá.</param>
/// <param name="DecreasedCount">Số mặt hàng giảm giá.</param>
/// <param name="TopIncreases">Tối đa <see cref="BoardMonthlyReportLimits.TopRows"/> mặt hàng tăng giá, ảnh hưởng lớn lên đầu.</param>
public sealed record BoardPriceVarianceDto(decimal ImpactAmount, int IncreasedCount, int DecreasedCount, IReadOnlyList<PriceVarianceRowDto> TopIncreases);

/// <summary>Hồ sơ đánh giá NCC mới (HSN) có ngày hồ sơ trong tháng – tiến độ bổ sung NCC báo cáo Ban.</summary>
/// <param name="Count">Số hồ sơ (không tính hồ sơ đã hủy).</param>
/// <param name="ApprovedCount">Số hồ sơ Hội đồng đã duyệt.</param>
/// <param name="InProgressCount">Số hồ sơ đang lập, đã đánh giá hoặc chờ Hội đồng duyệt.</param>
/// <param name="ClosedCount">Số hồ sơ từ chối hoặc không đề xuất.</param>
/// <param name="Items">Các hồ sơ (kể cả đã hủy để đối chiếu), ngày tăng dần.</param>
public sealed record BoardOnboardingDto(int Count, int ApprovedCount, int InProgressCount, int ClosedCount, IReadOnlyList<SupplierOnboardingListItemDto> Items);

/// <summary>Kiểm soát nội bộ trong tháng – chỉ có khi người xem có quyền KSNB (design.md mục 18.19).</summary>
/// <param name="HighCount">Số dấu hiệu mức Cao (null khi người xem không có quyền xem dấu hiệu).</param>
/// <param name="MediumCount">Số dấu hiệu mức Trung bình.</param>
/// <param name="Summary">Tổng hợp theo loại dấu hiệu.</param>
/// <param name="FindingCount">Số vi phạm ghi nhận trong tháng (theo ngày phát hiện).</param>
/// <param name="SeriousConcludedCount">Số vụ Nghiêm trọng đã kết luận – như KPI "Vi phạm nghiêm trọng".</param>
/// <param name="Findings">Các vi phạm ghi nhận trong tháng.</param>
public sealed record BoardControlDto(
    int? HighCount, int? MediumCount, IReadOnlyList<AnomalySummaryDto> Summary, int FindingCount, int SeriousConcludedCount,
    IReadOnlyList<ControlFindingDto> Findings);

/// <summary>
/// Gói báo cáo tháng cho Ban cung ứng (lát G12, design.md mục 18.25): ghép số liệu các báo cáo đã có, không tính lại. Mục người xem không có
/// quyền là null và có ghi chú trong <paramref name="Sections"/>.
/// </summary>
/// <param name="Year">Năm.</param>
/// <param name="Month">Tháng.</param>
/// <param name="From">Ngày đầu tháng.</param>
/// <param name="To">Ngày cuối tháng.</param>
/// <param name="PreviousFrom">Ngày đầu tháng trước.</param>
/// <param name="PreviousTo">Ngày cuối tháng trước.</param>
/// <param name="AllCompanies">Xem toàn tập đoàn.</param>
/// <param name="CompanyCode">Mã công ty đang làm việc.</param>
/// <param name="Sections">Trạng thái các mục theo thứ tự trình bày.</param>
/// <param name="Purchase">Tổng quan mua hàng (PRCW).</param>
/// <param name="Kpi">Bảng 9 KPI của công ty đang làm việc.</param>
/// <param name="Savings">Tiết kiệm.</param>
/// <param name="OutOfContract">Dòng mua ngoài giá đã duyệt, tiền chênh lớn lên đầu, tối đa <see cref="BoardMonthlyReportLimits.TopRows"/>.</param>
/// <param name="OutOfContractCount">Tổng số dòng mua ngoài giá đã duyệt (trước khi cắt).</param>
/// <param name="PriceVariance">Biến động giá.</param>
/// <param name="VolumeRebates">Theo dõi bậc chiết khấu sản lượng gộp (kỳ đang tính của thỏa thuận hiệu lực).</param>
/// <param name="SupplierCoverage">Nhóm hàng chưa đủ NCC đã duyệt (mức tối thiểu 3).</param>
/// <param name="Onboarding">Hồ sơ đánh giá NCC mới trong tháng.</param>
/// <param name="Control">Kiểm soát nội bộ.</param>
public sealed record BoardMonthlyReportDto(
    int Year, int Month, DateOnly From, DateOnly To, DateOnly PreviousFrom, DateOnly PreviousTo, bool AllCompanies, string CompanyCode,
    IReadOnlyList<BoardReportSectionDto> Sections, BoardPurchaseSummaryDto? Purchase, KpiResult? Kpi, BoardSavingsDto? Savings,
    IReadOnlyList<OutOfContractLineDto>? OutOfContract, int OutOfContractCount, BoardPriceVarianceDto? PriceVariance,
    IReadOnlyList<VolumeRebateTrackingRowDto>? VolumeRebates, SupplierCoverageResult? SupplierCoverage, BoardOnboardingDto? Onboarding,
    BoardControlDto? Control);

/// <summary>Mã các mục của báo cáo tháng cho Ban, theo thứ tự trình bày.</summary>
public static class BoardReportSections
{
    /// <summary>Tổng quan mua hàng, tỷ lệ mua theo giá đã duyệt.</summary>
    public const string Purchase = "Purchase";

    /// <summary>Bảng KPI.</summary>
    public const string Kpi = "Kpi";

    /// <summary>Tiết kiệm.</summary>
    public const string Savings = "Savings";

    /// <summary>Mua ngoài giá đã duyệt.</summary>
    public const string OutOfContract = "OutOfContract";

    /// <summary>Biến động giá.</summary>
    public const string PriceVariance = "PriceVariance";

    /// <summary>Chiết khấu sản lượng gộp.</summary>
    public const string VolumeRebates = "VolumeRebates";

    /// <summary>NCC đủ điều kiện.</summary>
    public const string SupplierCoverage = "SupplierCoverage";

    /// <summary>Hồ sơ NCC mới.</summary>
    public const string Onboarding = "Onboarding";

    /// <summary>Kiểm soát nội bộ.</summary>
    public const string Control = "Control";
}

/// <summary>Giới hạn số dòng của báo cáo tháng.</summary>
public static class BoardMonthlyReportLimits
{
    /// <summary>Số dòng tối đa của các danh sách "lớn nhất" (mua ngoài giá đã duyệt, mặt hàng tăng giá) – Ban xem khoản ảnh hưởng lớn.</summary>
    public const int TopRows = 10;

    /// <summary>Số NCC tối thiểu mỗi nhóm hàng theo quyết định thành lập Ban (design.md mục 18.25).</summary>
    public const int MinSuppliers = 3;
}

/// <summary>Báo cáo tháng cho Ban cung ứng (lát G12, design.md mục 18.25).</summary>
public interface IBoardMonthlyReportService
{
    /// <summary>
    /// Ghép báo cáo tháng: mỗi mục gọi đúng báo cáo chi tiết (PRCW, KPI, SAVE, giá chuẩn, PRVAR, CKBT, SUPCOV, HSN, ANOM/VIOL) cho tháng chọn;
    /// mục người xem thiếu quyền thì để null kèm ghi chú.
    /// </summary>
    /// <param name="year">Năm (2000–2100).</param>
    /// <param name="month">Tháng (1–12).</param>
    /// <param name="allCompanies">Xem toàn tập đoàn (cần quyền <c>Report.Purchase.ViewAllCompanies</c>).</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Báo cáo.</returns>
    /// <exception cref="Domain.Common.BusinessRuleException">Khi tháng/năm sai.</exception>
    /// <exception cref="Common.ForbiddenAccessException">Khi xem toàn tập đoàn mà thiếu quyền.</exception>
    Task<BoardMonthlyReportDto> GetAsync(int year, int month, bool allCompanies, CancellationToken cancellationToken);

    /// <summary>Xuất báo cáo tháng ra Excel: một sheet Tổng quan và một sheet cho mỗi mục có số liệu.</summary>
    /// <param name="year">Năm.</param>
    /// <param name="month">Tháng.</param>
    /// <param name="allCompanies">Xem toàn tập đoàn.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>File Excel.</returns>
    /// <exception cref="Domain.Common.BusinessRuleException">Khi tháng/năm sai.</exception>
    /// <exception cref="Common.ForbiddenAccessException">Khi xem toàn tập đoàn mà thiếu quyền.</exception>
    Task<ExcelFile> ExportAsync(int year, int month, bool allCompanies, CancellationToken cancellationToken);
}
