using Proc.Domain.Common;

namespace Proc.Domain.MasterData;

/// <summary>Trạng thái phiếu đánh giá NCC định kỳ.</summary>
public enum SupplierReviewStatus
{
    /// <summary>Đang đánh giá – còn sửa được.</summary>
    Draft = 1,

    /// <summary>Đã hoàn thành – khóa; tính vào KPI "Hoàn thành đánh giá NCC".</summary>
    Completed = 2,

    /// <summary>Đã hủy (lập nhầm) – không tính.</summary>
    Cancelled = 3,
}

/// <summary>Kết luận sau đánh giá.</summary>
public enum SupplierReviewResult
{
    /// <summary>Tiếp tục hợp tác.</summary>
    Continue = 1,

    /// <summary>Theo dõi, yêu cầu cải thiện.</summary>
    Monitor = 2,

    /// <summary>Đề xuất ngừng hợp tác.</summary>
    Stop = 3,
}

/// <summary>
/// Điểm 6 tiêu chí, thang 1–5 (null = chưa chấm). Tiêu chí theo đề xuất Ban CUVT Bước 10 "không chọn chỉ vì giá thấp nhất".
/// </summary>
/// <param name="Price">Giá, tổng chi phí.</param>
/// <param name="Quality">Chất lượng hàng.</param>
/// <param name="Delivery">Giao hàng (đúng hạn, đủ SL).</param>
/// <param name="Warranty">Bảo hành, hỗ trợ sau bán.</param>
/// <param name="Payment">Công nợ, điều kiện thanh toán.</param>
/// <param name="Compliance">Năng lực tài chính, lịch sử tuân thủ hợp đồng.</param>
public sealed record SupplierReviewScores(int? Price, int? Quality, int? Delivery, int? Warranty, int? Payment, int? Compliance)
{
    /// <summary>Các điểm theo thứ tự tiêu chí.</summary>
    public IReadOnlyList<int?> All => [Price, Quality, Delivery, Warranty, Payment, Compliance];
}

/// <summary>Số liệu tham khảo của NCC trong quý (lấy từ báo cáo đánh giá NCC, kiểm tra chất lượng), chốt lúc hoàn thành.</summary>
/// <param name="ReceiptAmount">Giá trị nhập mua trước VAT.</param>
/// <param name="OnTimePct">Tỷ lệ dòng giao đúng hạn (%).</param>
/// <param name="ReturnPct">Tỷ lệ trả hàng (%).</param>
/// <param name="QualityPct">Tỷ lệ dòng kiểm tra đạt (%).</param>
/// <param name="SystemRating">Xếp loại theo hạng cấu hình (mục 11.3).</param>
public sealed record SupplierReviewMetrics(decimal ReceiptAmount, decimal? OnTimePct, decimal? ReturnPct, decimal? QualityPct, string? SystemRating);

/// <summary>Nội dung người đánh giá nhập.</summary>
/// <param name="Date">Ngày đánh giá.</param>
/// <param name="Scores">Điểm 6 tiêu chí.</param>
/// <param name="Result">Kết luận.</param>
/// <param name="Comment">Nhận xét.</param>
public sealed record SupplierReviewData(DateOnly Date, SupplierReviewScores Scores, SupplierReviewResult? Result, string? Comment);

/// <summary>
/// Phiếu đánh giá NCC định kỳ theo quý của một công ty (design.md mục 18.20, N5): người đánh giá chấm 6 tiêu chí, kết luận; số liệu giao
/// hàng/chất lượng của hệ thống đi kèm để tham khảo. Mỗi công ty × NCC × quý chỉ một phiếu chưa hủy (kiểm tra ở dịch vụ + chỉ mục duy nhất).
/// </summary>
[Auditable("Đánh giá NCC định kỳ", LabelProperty = nameof(No))]
public sealed class SupplierReview : AuditableEntity, ICompanyOwned
{
    /// <summary>Độ dài nhận xét, lý do hủy tối đa.</summary>
    public const int TextMaxLength = 2000;

    /// <summary>Điểm thấp nhất của mỗi tiêu chí.</summary>
    public const int MinScore = 1;

    /// <summary>Điểm cao nhất của mỗi tiêu chí.</summary>
    public const int MaxScore = 5;

    /// <summary>Điểm tiêu chí từ mức này trở xuống là "kém" – bắt buộc nhận xét.</summary>
    public const int LowScore = 2;

    private SupplierReview()
    {
    }

    /// <inheritdoc />
    public Guid CompanyId { get; private set; }

    /// <summary>Số phiếu, ví dụ DGN-BHA-2611-0001.</summary>
    public string No { get; private set; } = string.Empty;

    /// <summary>NCC được đánh giá.</summary>
    public Guid SupplierId { get; private set; }

    /// <summary>Năm của kỳ đánh giá.</summary>
    public int Year { get; private set; }

    /// <summary>Quý (1–4).</summary>
    public int Quarter { get; private set; }

    /// <summary>Ngày đánh giá.</summary>
    public DateOnly Date { get; private set; }

    /// <summary>Điểm giá, tổng chi phí.</summary>
    public int? PriceScore { get; private set; }

    /// <summary>Điểm chất lượng.</summary>
    public int? QualityScore { get; private set; }

    /// <summary>Điểm giao hàng.</summary>
    public int? DeliveryScore { get; private set; }

    /// <summary>Điểm bảo hành, hỗ trợ.</summary>
    public int? WarrantyScore { get; private set; }

    /// <summary>Điểm công nợ, thanh toán.</summary>
    public int? PaymentScore { get; private set; }

    /// <summary>Điểm năng lực tài chính, tuân thủ.</summary>
    public int? ComplianceScore { get; private set; }

    /// <summary>Điểm bình quân 6 tiêu chí (2 số lẻ), tính lúc hoàn thành.</summary>
    public decimal? Score { get; private set; }

    /// <summary>Kết luận.</summary>
    public SupplierReviewResult? Result { get; private set; }

    /// <summary>Nhận xét, yêu cầu cải thiện.</summary>
    public string? Comment { get; private set; }

    /// <summary>Giá trị nhập mua trong quý (trước VAT).</summary>
    public decimal ReceiptAmount { get; private set; }

    /// <summary>Tỷ lệ giao đúng hạn trong quý (%).</summary>
    public decimal? OnTimePct { get; private set; }

    /// <summary>Tỷ lệ trả hàng trong quý (%).</summary>
    public decimal? ReturnPct { get; private set; }

    /// <summary>Tỷ lệ dòng kiểm tra đạt chất lượng trong quý (%).</summary>
    public decimal? QualityPct { get; private set; }

    /// <summary>Xếp loại hệ thống theo hạng cấu hình.</summary>
    public string? SystemRating { get; private set; }

    /// <summary>Trạng thái.</summary>
    public SupplierReviewStatus Status { get; private set; } = SupplierReviewStatus.Draft;

    /// <summary>Thời điểm hoàn thành (UTC).</summary>
    public DateTimeOffset? CompletedAt { get; private set; }

    /// <summary>Người hoàn thành đánh giá.</summary>
    public Guid? CompletedBy { get; private set; }

    /// <summary>Lý do hủy.</summary>
    public string? CancelReason { get; private set; }

    /// <summary>
    /// Ngày đầu và ngày cuối quý.
    /// </summary>
    /// <param name="year">Năm.</param>
    /// <param name="quarter">Quý 1–4.</param>
    /// <returns>Khoảng ngày của quý.</returns>
    /// <exception cref="BusinessRuleException">Khi quý hoặc năm sai.</exception>
    public static (DateOnly Start, DateOnly End) QuarterRange(int year, int quarter)
    {
        if (quarter is < 1 or > 4) throw new BusinessRuleException("Quý phải từ 1 đến 4.");
        if (year is < 2000 or > 2100) throw new BusinessRuleException("Năm đánh giá không hợp lệ.");
        var start = new DateOnly(year, (quarter - 1) * 3 + 1, 1);
        return (start, start.AddMonths(3).AddDays(-1));
    }

    /// <summary>
    /// Lập phiếu đánh giá (Nháp).
    /// </summary>
    /// <param name="companyId">Công ty đánh giá.</param>
    /// <param name="no">Số phiếu đã cấp.</param>
    /// <param name="supplierId">NCC.</param>
    /// <param name="year">Năm.</param>
    /// <param name="quarter">Quý.</param>
    /// <param name="data">Nội dung ban đầu.</param>
    /// <param name="today">Hôm nay (giờ Việt Nam).</param>
    /// <returns>Phiếu nháp.</returns>
    /// <exception cref="BusinessRuleException">Khi quý chưa bắt đầu, ngày hoặc điểm sai.</exception>
    public static SupplierReview Create(Guid companyId, string no, Guid supplierId, int year, int quarter, SupplierReviewData data, DateOnly today)
    {
        if (companyId == Guid.Empty) throw new BusinessRuleException("Phiếu đánh giá phải thuộc một công ty.");
        if (supplierId == Guid.Empty) throw new BusinessRuleException("Hãy chọn nhà cung cấp.");
        if (string.IsNullOrWhiteSpace(no)) throw new BusinessRuleException("Thiếu số phiếu đánh giá.");
        var (start, _) = QuarterRange(year, quarter);
        // Không đánh giá kỳ chưa phát sinh – quý phải đã bắt đầu.
        if (start > today) throw new BusinessRuleException($"Quý {quarter}/{year} chưa bắt đầu.");
        var review = new SupplierReview { CompanyId = companyId, No = no.Trim(), SupplierId = supplierId, Year = year, Quarter = quarter };
        review.Apply(data, today);
        return review;
    }

    /// <summary>
    /// Sửa ngày, điểm, kết luận, nhận xét khi còn Nháp.
    /// </summary>
    /// <param name="data">Nội dung mới.</param>
    /// <param name="today">Hôm nay.</param>
    /// <exception cref="BusinessRuleException">Khi không còn Nháp hoặc dữ liệu sai.</exception>
    public void Update(SupplierReviewData data, DateOnly today)
    {
        EnsureDraft();
        Apply(data, today);
    }

    /// <summary>
    /// Cập nhật số liệu tham khảo của hệ thống (dịch vụ tính lại mỗi lần lưu và lúc hoàn thành; sau đó giữ nguyên để tra cứu).
    /// </summary>
    /// <param name="metrics">Số liệu trong quý.</param>
    /// <exception cref="BusinessRuleException">Khi không còn Nháp.</exception>
    public void SetMetrics(SupplierReviewMetrics metrics)
    {
        EnsureDraft();
        ReceiptAmount = metrics.ReceiptAmount;
        OnTimePct = metrics.OnTimePct;
        ReturnPct = metrics.ReturnPct;
        QualityPct = metrics.QualityPct;
        SystemRating = metrics.SystemRating;
    }

    /// <summary>
    /// Hoàn thành đánh giá: đủ 6 điểm, có kết luận; kết luận khác "Tiếp tục" hoặc có tiêu chí ≤ 2 điểm thì bắt buộc nhận xét. Điểm = bình quân
    /// 6 tiêu chí, làm tròn 2 số lẻ – ví dụ 4, 4, 5, 3, 4, 2 → 22 ÷ 6 = 3,67.
    /// </summary>
    /// <param name="at">Thời điểm (UTC).</param>
    /// <param name="by">Người hoàn thành.</param>
    /// <exception cref="BusinessRuleException">Khi không còn Nháp hoặc thiếu điểm, kết luận, nhận xét.</exception>
    public void Complete(DateTimeOffset at, Guid by)
    {
        EnsureDraft();
        var scores = Scores.All;
        if (scores.Any(s => s is null)) throw new BusinessRuleException("Hãy chấm đủ 6 tiêu chí trước khi hoàn thành.");
        if (Result is null) throw new BusinessRuleException("Hãy chọn kết luận đánh giá.");
        if (string.IsNullOrWhiteSpace(Comment) && (Result != SupplierReviewResult.Continue || scores.Any(s => s <= LowScore)))
            throw new BusinessRuleException($"Kết luận không phải \"Tiếp tục hợp tác\" hoặc có tiêu chí từ {LowScore} điểm trở xuống – hãy nhập nhận xét.");
        Score = Math.Round((decimal)scores.Sum(s => s!.Value) / scores.Count, 2, MidpointRounding.AwayFromZero);
        Status = SupplierReviewStatus.Completed;
        CompletedAt = at;
        CompletedBy = by;
    }

    /// <summary>
    /// Hủy phiếu nháp lập nhầm. Phiếu đã hoàn thành không hủy (giữ kết quả đã dùng cho KPI).
    /// </summary>
    /// <param name="reason">Lý do, bắt buộc.</param>
    /// <exception cref="BusinessRuleException">Khi không còn Nháp hoặc thiếu lý do.</exception>
    public void Cancel(string? reason)
    {
        EnsureDraft();
        if (string.IsNullOrWhiteSpace(reason)) throw new BusinessRuleException("Hãy nhập lý do hủy.");
        if (reason.Trim().Length > TextMaxLength) throw new BusinessRuleException($"Lý do hủy tối đa {TextMaxLength} ký tự.");
        CancelReason = reason.Trim();
        Status = SupplierReviewStatus.Cancelled;
    }

    /// <summary>Điểm hiện tại theo thứ tự tiêu chí.</summary>
    public SupplierReviewScores Scores => new(PriceScore, QualityScore, DeliveryScore, WarrantyScore, PaymentScore, ComplianceScore);

    private void EnsureDraft()
    {
        if (Status != SupplierReviewStatus.Draft) throw new BusinessRuleException("Phiếu đánh giá không còn ở trạng thái Nháp.");
    }

    private void Apply(SupplierReviewData data, DateOnly today)
    {
        var (start, _) = QuarterRange(Year, Quarter);
        if (data.Date < start) throw new BusinessRuleException($"Ngày đánh giá không được trước ngày đầu quý {start:dd/MM/yyyy}.");
        if (data.Date > today) throw new BusinessRuleException("Ngày đánh giá không được sau hôm nay.");
        if (data.Scores.All.Any(s => s is < MinScore or > MaxScore))
            throw new BusinessRuleException($"Điểm mỗi tiêu chí từ {MinScore} đến {MaxScore}.");
        if (data.Result is { } result && !Enum.IsDefined(result)) throw new BusinessRuleException("Kết luận đánh giá không hợp lệ.");
        if (data.Comment?.Trim().Length > TextMaxLength) throw new BusinessRuleException($"Nhận xét tối đa {TextMaxLength} ký tự.");
        Date = data.Date;
        (PriceScore, QualityScore, DeliveryScore, WarrantyScore, PaymentScore, ComplianceScore) =
            (data.Scores.Price, data.Scores.Quality, data.Scores.Delivery, data.Scores.Warranty, data.Scores.Payment, data.Scores.Compliance);
        Result = data.Result;
        Comment = string.IsNullOrWhiteSpace(data.Comment) ? null : data.Comment.Trim();
    }
}
