using Microsoft.EntityFrameworkCore;
using Proc.Application.Common;
using Proc.Application.MasterData;
using Proc.Application.Reports;
using Proc.Domain.Common;
using Proc.Domain.Control;
using Proc.Domain.Organization;
using Proc.Domain.Purchasing;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.Reports;

/// <summary>
/// Bảng KPI Ban Cung ứng (design.md mục 18.18): ghép kết quả các báo cáo đã có (PRCW, SLA, QCDEV, đánh giá NCC, tiết kiệm đấu thầu,
/// hàng chậm luân chuyển) để mỗi chỉ số trên bảng KPI khớp đúng con số ở báo cáo chi tiết. Phạm vi: công ty đang làm việc, kho và nhóm
/// hàng của người xem (global query filter qua các báo cáo nguồn).
/// </summary>
public sealed class KpiService : IKpiService
{
    private readonly ProcDbContext _db;
    private readonly ICurrentUserScope _scope;
    private readonly IPurchaseReportService _purchase;
    private readonly IStockReportService _stock;
    private readonly ISupplierReviewService _reviews;

    /// <summary>
    /// Tạo dịch vụ.
    /// </summary>
    /// <param name="db">DbContext.</param>
    /// <param name="scope">Phạm vi người dùng.</param>
    /// <param name="purchase">Báo cáo mua hàng.</param>
    /// <param name="stock">Báo cáo kho.</param>
    /// <param name="reviews">Đánh giá NCC định kỳ.</param>
    public KpiService(
        ProcDbContext db, ICurrentUserScope scope, IPurchaseReportService purchase, IStockReportService stock, ISupplierReviewService reviews)
    {
        _db = db;
        _scope = scope;
        _purchase = purchase;
        _stock = stock;
        _reviews = reviews;
    }

    /// <inheritdoc />
    public async Task<KpiResult> GetAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        if (from > to) throw new BusinessRuleException("Từ ngày phải trước hoặc bằng đến ngày.");
        if (to.DayNumber - from.DayNumber >= PurchaseReportService.MaxDays) throw new BusinessRuleException("Báo cáo xem tối đa hai năm mỗi lần.");

        var parameter = await _db.CompanyParameters.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        decimal Target(KpiCode code) => parameter?.KpiTarget(code) ?? KpiCatalog.Get(code).DefaultTarget;
        var slowDays = parameter?.KpiSlowMovingDays ?? KpiCatalog.DefaultSlowMovingDays;

        var (prevFrom, prevTo) = PurchaseReportService.PreviousPeriod(from, to);
        var current = await PeriodAsync(from, to, cancellationToken);
        var previous = await PeriodAsync(prevFrom, prevTo, cancellationToken);
        var slow = await _stock.GetSlowMovingAsync(new(null, null, slowDays), cancellationToken);
        var slowAmount = slow.Items.Sum(i => i.Amount);
        // Hoàn thành đánh giá NCC tính theo quý kết thúc trong kỳ (design.md mục 18.20) – kỳ không chứa ngày cuối quý thì "Chưa có dữ liệu".
        var review = await _reviews.CompletionAsync(from, to, cancellationToken);
        var reviewBefore = await _reviews.CompletionAsync(prevFrom, prevTo, cancellationToken);

        KpiRowDto Row(KpiCode code, Ratio? now, Ratio? before, string unit, string? note, string? report)
        {
            var k = KpiCatalog.Get(code);
            var target = Target(code);
            return new KpiRowDto(code.ToString(), k.Name, k.Formula, k.HigherIsBetter, k.IsCount, target, true, now?.Pct, before?.Pct,
                now?.Numerator, now?.Denominator, unit, KpiCatalog.IsMet(code, now?.Pct, target), note, report);
        }

        List<KpiRowDto> items =
        [
            Row(KpiCode.Saving, current.Saving, previous.Saving, "Money",
                "Theo RFQ đã chọn thầu trong kỳ; giá cơ sở = giá chào bình quân × SL.", "SAVE"),
            Row(KpiCode.ContractCompliance, current.Contract, previous.Contract, "Money",
                "PO Đã duyệt/Đã đóng theo ngày PO; không tính dòng PO lập trước bản 0.12.0 (không xác định nguồn giá).", "PRCW"),
            Row(KpiCode.PurchaseSla, current.Sla, previous.Sla, "Count", "Dòng PR đủ duyệt trong kỳ; dòng chưa đến hạn chưa tính.", "SLA"),
            Row(KpiCode.OnTimeDelivery, current.OnTime, previous.OnTime, "Count", "Dòng phiếu nhập mua đã ghi sổ trong kỳ có ngày giao hẹn.", "SUPEVAL"),
            Row(KpiCode.Quality, current.Quality, previous.Quality, "Count", "Dòng phiếu nhập mua đã ghi sổ trong kỳ có kết quả kiểm tra.", "QCDEV"),
            Row(KpiCode.Emergency, current.Emergency, previous.Emergency, "Money", "PO Đã duyệt/Đã đóng theo ngày PO, trước VAT.", "PO"),
            Row(KpiCode.SlowMoving, Ratio.Of(slowAmount, slow.StockAmount), null, "Money",
                $"Tính tại hôm nay {slow.AsOf:dd/MM/yyyy}, hàng không xuất từ {slowDays} ngày trở lên (không theo kỳ).", "SLOW"),
            Row(KpiCode.SupplierReview, Ratio.Of(review.CompletedCount, review.DueCount), Ratio.Of(reviewBefore.CompletedCount, reviewBefore.DueCount),
                "Count",
                review.Quarters.Count == 0
                    ? "Tính theo quý kết thúc trong kỳ – kỳ này không chứa ngày cuối quý nào. NCC phải đánh giá: có phiếu nhập mua đã ghi sổ trong quý."
                    : $"Quý {string.Join(", ", review.Quarters)}: NCC có phiếu nhập mua đã ghi sổ trong quý, đã có phiếu đánh giá hoàn thành.",
                "SUPREV"),
            Row(KpiCode.Violation, current.Violation, previous.Violation, "Count",
                "Vi phạm mức Nghiêm trọng đã kết luận, theo ngày phát hiện; mẫu số là mọi vi phạm ghi nhận trong kỳ (sổ vi phạm KSNB).", "VIOL"),
        ];

        return new KpiResult(from, to, prevFrom, prevTo, items.Count(i => i.Value is not null), items.Count(i => i.Met == true), items);
    }

    /// <summary>Tử số / mẫu số và tỷ lệ % (2 số lẻ). Ví dụ 47 ÷ 50 → 94,00%.</summary>
    private sealed record Ratio(decimal Numerator, decimal Denominator, decimal? Pct)
    {
        public static Ratio Of(decimal numerator, decimal denominator) =>
            new(numerator, denominator, denominator > 0 ? Math.Round(numerator * 100m / denominator, 2, MidpointRounding.AwayFromZero) : null);
    }

    /// <summary>Các chỉ số theo kỳ của một khoảng ngày.</summary>
    private sealed record PeriodKpi(Ratio Saving, Ratio Contract, Ratio Sla, Ratio OnTime, Ratio Quality, Ratio Emergency, Ratio Violation);

    private async Task<PeriodKpi> PeriodAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var period = new PurchaseReportPeriod(from, to, false);
        // "from" là từ khóa trong biểu thức truy vấn LINQ nên đặt tên khác khi so sánh ngày.
        var (start, end) = (from, to);

        // Tiết kiệm đã xác minh = tiết kiệm chọn thầu so giá chào bình quân; giá cơ sở = giá chào bình quân × SL.
        // Ví dụ chào 100.000 / 110.000 / 120.000 (BQ 110.000), trúng 100.000 × 10 → tiết kiệm 100.000 ÷ 1.100.000 = 9,09%.
        var savings = await _purchase.GetSourcingSavingsAsync(period, cancellationToken);
        var saving = Ratio.Of(savings.Sum(s => s.SavingVsAvg), savings.Sum(s => Math.Round(s.AvgPrice * s.Qty, 2, MidpointRounding.AwayFromZero)));

        // Cùng công thức báo cáo PRCW (design.md mục 18.17): theo giá đã duyệt ÷ (tổng − không xác định).
        var compliance = await _purchase.GetContractComplianceAsync(period, cancellationToken);
        var known = compliance.Sources.Where(s => s.Source != "Unknown").Sum(s => s.Amount);
        var compliant = compliance.Sources.Where(s => s.Source is "Rfq" or "Contract" or "PriceList").Sum(s => s.Amount);

        var sla = await _purchase.GetPurchaseSlaAsync(from, to, cancellationToken);
        var evaluation = await _purchase.GetSupplierEvaluationAsync(period, cancellationToken);
        var quality = await _purchase.GetReceiptQualityAsync(from, to, cancellationToken);

        // Mua khẩn cấp = giá trị dòng PO khẩn ÷ tổng giá trị dòng PO đã cam kết (Đã duyệt/Đã đóng), trước VAT – như PRCW.
        var amounts = await (
                from o in _db.PurchaseOrders.AsNoTracking()
                where (o.Status == ApprovalDocumentStatus.Approved || o.Status == ApprovalDocumentStatus.Closed) && o.Date >= start && o.Date <= end
                from l in o.Lines
                join i in _db.Items on l.ItemId equals i.Id
                group l.Amount by o.PurchaseType == PurchaseType.Emergency into g
                select new { Emergency = g.Key, Amount = g.Sum() })
            .ToListAsync(cancellationToken);

        // Vi phạm nghiêm trọng = số vụ mức Nghiêm trọng đã kết luận (đã xác minh), theo ngày phát hiện – design.md mục 18.19. Giá trị là số vụ
        // (không phải %), 0 vụ vẫn là kết quả đo được (đạt mục tiêu 0), nên không dùng Ratio.Of (mẫu số 0 = chưa có dữ liệu).
        var findings = await _db.ControlFindings.AsNoTracking().Where(f => f.Date >= start && f.Date <= end)
            .GroupBy(f => f.Severity == FindingSeverity.Serious && f.Status == FindingStatus.Concluded)
            .Select(g => new { Serious = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        var serious = findings.Where(f => f.Serious).Sum(f => f.Count);
        var violation = new Ratio(serious, findings.Sum(f => f.Count), serious);

        return new PeriodKpi(
            saving,
            Ratio.Of(compliant, known),
            Ratio.Of(sla.OnTime, sla.OnTime + sla.Late + sla.Overdue),
            Ratio.Of(evaluation.Sum(e => e.OnTimeLines), evaluation.Sum(e => e.DeliveryLines)),
            Ratio.Of(quality.PassedLines, quality.CheckedLines),
            Ratio.Of(amounts.Where(a => a.Emergency).Sum(a => a.Amount), amounts.Sum(a => a.Amount)),
            violation);
    }
}
