using Microsoft.EntityFrameworkCore;
using Proc.Application.Common;
using Proc.Application.Control;
using Proc.Application.Excel;
using Proc.Application.MasterData;
using Proc.Application.Pricing;
using Proc.Application.Reports;
using Proc.Domain.Common;
using Proc.Domain.Organization;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.Reports;

/// <summary>
/// Gói báo cáo tháng cho Ban cung ứng (lát G12, design.md mục 18.25): mỗi mục gọi đúng dịch vụ của báo cáo chi tiết qua interface lớp
/// Application để số trên báo cáo tháng khớp từng báo cáo (PRCW, KPI, SAVE, giá chuẩn, PRVAR, CKBT, SUPCOV, HSN, ANOM/VIOL). Không đọc bảng
/// nghiệp vụ trực tiếp ngoài tên công ty. Mục thiếu quyền để null kèm ghi chú – như trang chủ – để người chỉ có quyền báo cáo mua vẫn xem được
/// phần của mình.
/// </summary>
public sealed class BoardMonthlyReportService : IBoardMonthlyReportService
{
    private readonly ProcDbContext _db;
    private readonly ICurrentUserScope _scope;
    private readonly IPurchaseReportService _purchase;
    private readonly IKpiService _kpi;
    private readonly IVolumeRebateService _rebates;
    private readonly ISupplierOnboardingService _onboardings;
    private readonly IControlAnomalyService _anomalies;
    private readonly IControlFindingService _findings;
    private readonly IClock _clock;

    /// <summary>Tạo dịch vụ.</summary>
    /// <param name="db">DbContext (chỉ đọc mã công ty).</param>
    /// <param name="scope">Phạm vi người dùng.</param>
    /// <param name="purchase">Báo cáo mua hàng.</param>
    /// <param name="kpi">Bảng KPI.</param>
    /// <param name="rebates">Chiết khấu sản lượng gộp.</param>
    /// <param name="onboardings">Hồ sơ đánh giá NCC mới.</param>
    /// <param name="anomalies">Dấu hiệu bất thường.</param>
    /// <param name="findings">Sổ vi phạm.</param>
    /// <param name="clock">Đồng hồ (giờ xuất file).</param>
    public BoardMonthlyReportService(
        ProcDbContext db, ICurrentUserScope scope, IPurchaseReportService purchase, IKpiService kpi, IVolumeRebateService rebates,
        ISupplierOnboardingService onboardings, IControlAnomalyService anomalies, IControlFindingService findings, IClock clock)
    {
        _db = db;
        _scope = scope;
        _purchase = purchase;
        _kpi = kpi;
        _rebates = rebates;
        _onboardings = onboardings;
        _anomalies = anomalies;
        _findings = findings;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task<BoardMonthlyReportDto> GetAsync(int year, int month, bool allCompanies, CancellationToken cancellationToken)
    {
        var companyId = _scope.RequireCompany();
        if (year is < 2000 or > 2100 || month is < 1 or > 12) throw new BusinessRuleException("Tháng báo cáo không hợp lệ.");
        if (allCompanies) _scope.EnsurePermission(Permissions.Report.PurchaseViewAllCompanies);

        var from = new DateOnly(year, month, 1);
        var to = from.AddMonths(1).AddDays(-1);
        var (prevFrom, prevTo) = PurchaseReportService.PreviousPeriod(from, to);
        var period = new PurchaseReportPeriod(from, to, allCompanies);
        var companyCode = await _db.Companies.AsNoTracking().Where(c => c.Id == companyId).Select(c => c.Code).SingleAsync(cancellationToken);
        var scopeNote = allCompanies ? "Toàn tập đoàn." : $"Công ty {companyCode}.";
        var sections = new List<BoardReportSectionDto>();
        const string noPermission = "Không có quyền xem.";

        // Các mục báo cáo mua hàng: cùng quyền màn hình (Report.Purchase.View – endpoint đã kiểm).
        var compliance = await _purchase.GetContractComplianceAsync(period, cancellationToken);
        var previousCompliance = await _purchase.GetContractComplianceAsync(period with { From = prevFrom, To = prevTo }, cancellationToken);
        var purchase = new BoardPurchaseSummaryDto(
            compliance.OrderCount, compliance.TotalAmount, compliance.CompliancePct,
            compliance.Sources.Where(s => s.Source is "Deviation" or "None").Sum(s => s.Amount),
            previousCompliance.OrderCount, previousCompliance.TotalAmount, previousCompliance.CompliancePct, compliance.Sources);
        sections.Add(new(BoardReportSections.Purchase, "Tổng quan mua hàng", true,
            $"{scopeNote} PO Đã duyệt/Đã đóng theo ngày PO, trước VAT; so tháng trước.", "PRCW"));

        // KPI chỉ tính cho công ty đang làm việc (mục 18.18 – chưa có bảng KPI toàn tập đoàn).
        var kpi = await _kpi.GetAsync(from, to, cancellationToken);
        sections.Add(new(BoardReportSections.Kpi, "KPI cung ứng", true,
            allCompanies ? $"Chỉ công ty {companyCode} – KPI chưa tính toàn tập đoàn." : $"Công ty {companyCode}.", "KPI"));

        var sourcing = await _purchase.GetSourcingSavingsAsync(period, cancellationToken);
        var standard = await _purchase.GetStandardSavingsAsync(period, null, cancellationToken);
        var awarded = sourcing.Sum(r => r.AwardedAmount);
        var sourcingSaving = sourcing.Sum(r => r.SavingVsAvg);
        // Tỷ lệ như KPI "Tiết kiệm mua hàng": tiết kiệm ÷ Σ (giá chào bình quân × SL) = tiết kiệm ÷ (giá trị trúng + tiết kiệm).
        // Ví dụ chào 100.000/110.000/120.000, trúng 100.000 × 10 → 100.000 ÷ 1.100.000 = 9,09%.
        var sourcingBase = awarded + sourcingSaving;
        var savings = new BoardSavingsDto(
            sourcing.Select(r => r.RfqId).Distinct().Count(), awarded, sourcingSaving,
            sourcingBase == 0 ? null : Math.Round(sourcingSaving / sourcingBase * 100, 2, MidpointRounding.AwayFromZero),
            standard.CoveredAmount, standard.CoveragePct, standard.Saving, standard.SavingPct);
        sections.Add(new(BoardReportSections.Savings, "Tiết kiệm", true, $"{scopeNote} Đấu thầu theo ngày chọn thầu; giá chuẩn theo phiếu duyệt tháng.", "SAVE"));

        var outOfContract = compliance.Lines.Take(BoardMonthlyReportLimits.TopRows).ToList();
        sections.Add(new(BoardReportSections.OutOfContract, "Mua ngoài giá đã duyệt", true,
            $"{BoardMonthlyReportLimits.TopRows} dòng tiền chênh lớn nhất trên {compliance.Lines.Count} dòng.", "PRCW"));

        var variance = await _purchase.GetPriceVarianceAsync(period, null, cancellationToken);
        var priceVariance = new BoardPriceVarianceDto(
            variance.ImpactAmount, variance.Items.Count(i => i.ChangePct > 0), variance.Items.Count(i => i.ChangePct < 0),
            variance.Items.Where(i => i.ImpactAmount > 0).OrderByDescending(i => i.ImpactAmount).Take(BoardMonthlyReportLimits.TopRows).ToList());
        sections.Add(new(BoardReportSections.PriceVariance, "Biến động giá", true,
            $"{scopeNote} Giá bình quân tháng so tháng trước; {BoardMonthlyReportLimits.TopRows} mặt hàng tăng giá ảnh hưởng lớn nhất.", "PRVAR"));

        IReadOnlyList<VolumeRebateTrackingRowDto>? rebates = null;
        if (_scope.HasPermission(Permissions.Pricing.VolumeRebateView))
        {
            rebates = await _rebates.TrackingAsync(null, false, cancellationToken);
            sections.Add(new(BoardReportSections.VolumeRebates, "Chiết khấu sản lượng gộp", true,
                "Thỏa thuận hiệu lực, kỳ tính bậc đang chạy tại thời điểm xem (không theo tháng chọn).", "CKBT"));
        }
        else sections.Add(new(BoardReportSections.VolumeRebates, "Chiết khấu sản lượng gộp", false, noPermission, "CKBT"));

        SupplierCoverageResult? coverage = null;
        if (_scope.HasPermission(Permissions.MasterData.SupplierView))
        {
            coverage = await _purchase.GetSupplierCoverageAsync(BoardMonthlyReportLimits.MinSuppliers, null, true, cancellationToken);
            sections.Add(new(BoardReportSections.SupplierCoverage, "NCC đủ điều kiện", true,
                $"Nhóm hàng chưa đủ {BoardMonthlyReportLimits.MinSuppliers} NCC đã duyệt, tại thời điểm xem.", "SUPCOV"));
        }
        else sections.Add(new(BoardReportSections.SupplierCoverage, "NCC đủ điều kiện", false, noPermission, "SUPCOV"));

        BoardOnboardingDto? onboarding = null;
        if (_scope.HasPermission(Permissions.MasterData.SupplierOnboardingView))
        {
            var items = (await _onboardings.ListAsync(null, cancellationToken))
                .Where(o => o.Date >= from && o.Date <= to).OrderBy(o => o.Date).ThenBy(o => o.No).ToList();
            var active = items.Where(o => o.Status != "Cancelled").ToList();
            onboarding = new BoardOnboardingDto(
                active.Count, active.Count(o => o.Status == "Approved"),
                active.Count(o => o.Status is "Draft" or "Evaluated" or "Submitted"),
                active.Count(o => o.Status is "Rejected" or "NotRecommended"), items);
            sections.Add(new(BoardReportSections.Onboarding, "Hồ sơ NCC mới", true, "Hồ sơ có ngày hồ sơ trong tháng, trạng thái hiện tại.", "HSN"));
        }
        else sections.Add(new(BoardReportSections.Onboarding, "Hồ sơ NCC mới", false, noPermission, "HSN"));

        // Kiểm soát nội bộ chỉ hiện với người có quyền KSNB hoặc lãnh đạo nhận báo cáo vi phạm: kết quả KSNB gửi thẳng lãnh đạo, không qua
        // người bị giám sát (design.md mục 18.19) – người không có quyền thì không hiện mục, kể cả dòng "không có quyền".
        BoardControlDto? control = null;
        var canAnomaly = _scope.HasPermission(Permissions.Control.AnomalyView);
        if (canAnomaly || _scope.HasPermission(Permissions.Control.FindingReceive))
        {
            AnomalyResult? anomalies = canAnomaly ? await _anomalies.GetAsync(new AnomalyQuery(from, to, allCompanies), cancellationToken) : null;
            var findings = (await _findings.ListAsync(cancellationToken)).Where(f => f.Date >= from && f.Date <= to).OrderBy(f => f.Date).ToList();
            control = new BoardControlDto(
                anomalies?.Items.Count(a => a.Severity == AnomalySeverities.High), anomalies?.Items.Count(a => a.Severity == AnomalySeverities.Medium),
                anomalies?.Summary ?? [], findings.Count, findings.Count(f => f.Severity == "Serious" && f.Status == "Concluded"), findings);
            sections.Add(new(BoardReportSections.Control, "Kiểm soát nội bộ", true,
                $"{(canAnomaly ? scopeNote : "Chỉ sổ vi phạm.")} Sổ vi phạm của công ty {companyCode} theo ngày phát hiện.", canAnomaly ? "ANOM" : "VIOL"));
        }

        return new BoardMonthlyReportDto(
            year, month, from, to, prevFrom, prevTo, allCompanies, companyCode, sections, purchase, kpi, savings, outOfContract,
            compliance.Lines.Count, priceVariance, rebates, coverage, onboarding, control);
    }

    /// <inheritdoc />
    public async Task<ExcelFile> ExportAsync(int year, int month, bool allCompanies, CancellationToken cancellationToken)
    {
        var report = await GetAsync(year, month, allCompanies, cancellationToken);
        return BoardMonthlyReportExcel.Build(report, _clock.UtcNow);
    }
}
