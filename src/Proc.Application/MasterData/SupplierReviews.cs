using System.Linq.Expressions;
using FluentValidation;
using Proc.Domain.MasterData;

namespace Proc.Application.MasterData;

/// <summary>Phiếu đánh giá NCC định kỳ (chi tiết trong ngăn kéo).</summary>
/// <param name="Id">Id.</param>
/// <param name="No">Số phiếu.</param>
/// <param name="SupplierId">NCC.</param>
/// <param name="SupplierCode">Mã NCC.</param>
/// <param name="SupplierName">Tên NCC.</param>
/// <param name="Year">Năm.</param>
/// <param name="Quarter">Quý.</param>
/// <param name="Date">Ngày đánh giá.</param>
/// <param name="PriceScore">Điểm giá, tổng chi phí (1–5).</param>
/// <param name="QualityScore">Điểm chất lượng.</param>
/// <param name="DeliveryScore">Điểm giao hàng.</param>
/// <param name="WarrantyScore">Điểm bảo hành, hỗ trợ.</param>
/// <param name="PaymentScore">Điểm công nợ, thanh toán.</param>
/// <param name="ComplianceScore">Điểm năng lực tài chính, tuân thủ.</param>
/// <param name="Score">Điểm bình quân (khi hoàn thành).</param>
/// <param name="Result">Kết luận: Continue, Monitor, Stop.</param>
/// <param name="Comment">Nhận xét.</param>
/// <param name="ReceiptAmount">Giá trị nhập mua trong quý.</param>
/// <param name="OnTimePct">Tỷ lệ đúng hạn (%).</param>
/// <param name="ReturnPct">Tỷ lệ trả hàng (%).</param>
/// <param name="QualityPct">Tỷ lệ đạt chất lượng (%).</param>
/// <param name="SystemRating">Xếp loại hệ thống.</param>
/// <param name="Status">Trạng thái: Draft, Completed, Cancelled.</param>
/// <param name="CompletedAt">Thời điểm hoàn thành (UTC).</param>
/// <param name="CompletedByName">Người hoàn thành.</param>
/// <param name="CancelReason">Lý do hủy.</param>
/// <param name="CreatedAt">Thời điểm lập (UTC).</param>
/// <param name="CreatedByName">Người lập.</param>
public sealed record SupplierReviewDto(
    Guid Id, string No, Guid SupplierId, string SupplierCode, string SupplierName, int Year, int Quarter, DateOnly Date, int? PriceScore,
    int? QualityScore, int? DeliveryScore, int? WarrantyScore, int? PaymentScore, int? ComplianceScore, decimal? Score, string? Result,
    string? Comment, decimal ReceiptAmount, decimal? OnTimePct, decimal? ReturnPct, decimal? QualityPct, string? SystemRating, string Status,
    DateTimeOffset? CompletedAt, string? CompletedByName, string? CancelReason, DateTimeOffset CreatedAt, string? CreatedByName);

/// <summary>
/// Một NCC trên bảng đánh giá của quý: số liệu hệ thống tính tại lúc xem và phiếu đánh giá (chưa hủy) nếu đã lập.
/// </summary>
/// <param name="SupplierId">NCC.</param>
/// <param name="SupplierCode">Mã NCC.</param>
/// <param name="SupplierName">Tên NCC.</param>
/// <param name="IsDue">Thuộc diện phải đánh giá: có phiếu nhập mua đã ghi sổ trong quý.</param>
/// <param name="ReceiptAmount">Giá trị nhập mua trong quý (trước VAT).</param>
/// <param name="OnTimePct">Tỷ lệ đúng hạn (%).</param>
/// <param name="ReturnPct">Tỷ lệ trả hàng (%).</param>
/// <param name="QualityPct">Tỷ lệ đạt chất lượng (%).</param>
/// <param name="SystemRating">Xếp loại hệ thống.</param>
/// <param name="ReviewId">Phiếu đánh giá.</param>
/// <param name="ReviewNo">Số phiếu.</param>
/// <param name="Status">Trạng thái phiếu; null = chưa đánh giá.</param>
/// <param name="Date">Ngày đánh giá.</param>
/// <param name="Score">Điểm.</param>
/// <param name="Result">Kết luận.</param>
/// <param name="ReviewerName">Người lập phiếu.</param>
public sealed record SupplierReviewBoardRowDto(
    Guid SupplierId, string SupplierCode, string SupplierName, bool IsDue, decimal ReceiptAmount, decimal? OnTimePct, decimal? ReturnPct,
    decimal? QualityPct, string? SystemRating, Guid? ReviewId, string? ReviewNo, string? Status, DateOnly? Date, decimal? Score, string? Result,
    string? ReviewerName);

/// <summary>Bảng đánh giá NCC của một quý tại công ty đang làm việc.</summary>
/// <param name="Year">Năm.</param>
/// <param name="Quarter">Quý.</param>
/// <param name="From">Ngày đầu quý.</param>
/// <param name="To">Ngày cuối quý.</param>
/// <param name="DueCount">Số NCC thuộc diện phải đánh giá.</param>
/// <param name="CompletedCount">Số NCC thuộc diện đã có phiếu hoàn thành.</param>
/// <param name="CompletedPct">Tỷ lệ hoàn thành (%), null khi không NCC nào thuộc diện.</param>
/// <param name="Rows">NCC thuộc diện và NCC đã có phiếu, chưa đánh giá lên đầu.</param>
public sealed record SupplierReviewBoard(
    int Year, int Quarter, DateOnly From, DateOnly To, int DueCount, int CompletedCount, decimal? CompletedPct,
    IReadOnlyList<SupplierReviewBoardRowDto> Rows);

/// <summary>Lập/sửa phiếu đánh giá. Khi sửa, NCC và quý giữ như lúc lập.</summary>
/// <param name="SupplierId">NCC.</param>
/// <param name="Year">Năm.</param>
/// <param name="Quarter">Quý.</param>
/// <param name="Date">Ngày đánh giá.</param>
/// <param name="PriceScore">Điểm giá, tổng chi phí.</param>
/// <param name="QualityScore">Điểm chất lượng.</param>
/// <param name="DeliveryScore">Điểm giao hàng.</param>
/// <param name="WarrantyScore">Điểm bảo hành, hỗ trợ.</param>
/// <param name="PaymentScore">Điểm công nợ, thanh toán.</param>
/// <param name="ComplianceScore">Điểm năng lực tài chính, tuân thủ.</param>
/// <param name="Result">Kết luận: Continue, Monitor, Stop hoặc trống.</param>
/// <param name="Comment">Nhận xét.</param>
public sealed record SaveSupplierReviewRequest(
    Guid SupplierId, int Year, int Quarter, DateOnly Date, int? PriceScore, int? QualityScore, int? DeliveryScore, int? WarrantyScore,
    int? PaymentScore, int? ComplianceScore, string? Result, string? Comment);

/// <summary>Hủy phiếu đánh giá nháp.</summary>
/// <param name="Reason">Lý do.</param>
public sealed record CancelSupplierReviewRequest(string Reason);

/// <summary>Kết quả hoàn thành đánh giá NCC của các quý kết thúc trong một khoảng ngày (KPI, design.md mục 18.18).</summary>
/// <param name="DueCount">Tổng số (NCC × quý) thuộc diện phải đánh giá.</param>
/// <param name="CompletedCount">Số đã có phiếu hoàn thành.</param>
/// <param name="Quarters">Các quý đã tính, ví dụ "3/2026".</param>
public sealed record SupplierReviewCompletion(int DueCount, int CompletedCount, IReadOnlyList<string> Quarters);

/// <summary>
/// Đánh giá NCC định kỳ theo quý (design.md mục 18.20, N5). Phạm vi công ty đang làm việc; NCC thuộc diện tính theo kho của người xem.
/// </summary>
public interface ISupplierReviewService
{
    /// <summary>
    /// Bảng đánh giá của một quý: NCC thuộc diện, số liệu hệ thống, phiếu đã lập.
    /// </summary>
    /// <param name="year">Năm.</param>
    /// <param name="quarter">Quý 1–4.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Bảng đánh giá.</returns>
    /// <exception cref="Domain.Common.BusinessRuleException">Khi quý sai.</exception>
    Task<SupplierReviewBoard> GetBoardAsync(int year, int quarter, CancellationToken cancellationToken);

    /// <summary>
    /// Chi tiết phiếu.
    /// </summary>
    /// <param name="id">Id.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Phiếu.</returns>
    /// <exception cref="Common.NotFoundException">Khi không có hoặc thuộc công ty khác.</exception>
    Task<SupplierReviewDto> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Lập phiếu (Nháp), cấp số loại DGN, chốt số liệu hệ thống của quý.
    /// </summary>
    /// <param name="request">Nội dung.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Phiếu vừa lập.</returns>
    /// <exception cref="Domain.Common.BusinessRuleException">Khi NCC × quý đã có phiếu chưa hủy, hoặc dữ liệu sai.</exception>
    Task<SupplierReviewDto> CreateAsync(SaveSupplierReviewRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Sửa phiếu nháp, tính lại số liệu hệ thống.
    /// </summary>
    /// <param name="id">Id.</param>
    /// <param name="request">Nội dung mới.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Phiếu sau khi sửa.</returns>
    /// <exception cref="Domain.Common.BusinessRuleException">Khi không còn Nháp hoặc dữ liệu sai.</exception>
    Task<SupplierReviewDto> UpdateAsync(Guid id, SaveSupplierReviewRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Lưu nội dung rồi hoàn thành phiếu (khóa) trong một thao tác.
    /// </summary>
    /// <param name="id">Id.</param>
    /// <param name="request">Nội dung cuối.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Phiếu đã hoàn thành.</returns>
    /// <exception cref="Domain.Common.BusinessRuleException">Khi thiếu điểm, kết luận, nhận xét.</exception>
    Task<SupplierReviewDto> CompleteAsync(Guid id, SaveSupplierReviewRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Hủy phiếu nháp lập nhầm.
    /// </summary>
    /// <param name="id">Id.</param>
    /// <param name="request">Lý do.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Phiếu đã hủy.</returns>
    /// <exception cref="Domain.Common.BusinessRuleException">Khi không còn Nháp hoặc thiếu lý do.</exception>
    Task<SupplierReviewDto> CancelAsync(Guid id, CancelSupplierReviewRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Hoàn thành đánh giá cho KPI: các quý có ngày cuối quý nằm trong [from, to]; mỗi quý đếm NCC thuộc diện và NCC thuộc diện có phiếu hoàn
    /// thành. Không quý nào kết thúc trong khoảng → mẫu số 0 (chưa có dữ liệu). Không kiểm quyền đánh giá NCC (bảng KPI đã kiểm quyền báo cáo).
    /// </summary>
    /// <param name="from">Từ ngày.</param>
    /// <param name="to">Đến ngày.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Tử số, mẫu số, các quý.</returns>
    Task<SupplierReviewCompletion> CompletionAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken);
}

/// <summary>Kiểm tra lập/sửa phiếu đánh giá.</summary>
public sealed class SaveSupplierReviewRequestValidator : AbstractValidator<SaveSupplierReviewRequest>
{
    /// <summary>Khai báo các quy tắc.</summary>
    public SaveSupplierReviewRequestValidator()
    {
        RuleFor(x => x.SupplierId).NotEmpty().WithMessage("Hãy chọn nhà cung cấp.");
        RuleFor(x => x.Quarter).InclusiveBetween(1, 4).WithMessage("Quý phải từ 1 đến 4.");
        RuleFor(x => x.Year).InclusiveBetween(2000, 2100).WithMessage("Năm đánh giá không hợp lệ.");
        foreach (var score in new Expression<Func<SaveSupplierReviewRequest, int?>>[]
                 {
                     x => x.PriceScore, x => x.QualityScore, x => x.DeliveryScore, x => x.WarrantyScore, x => x.PaymentScore, x => x.ComplianceScore,
                 })
            RuleFor(score).InclusiveBetween(SupplierReview.MinScore, SupplierReview.MaxScore)
                .WithMessage($"Điểm mỗi tiêu chí từ {SupplierReview.MinScore} đến {SupplierReview.MaxScore}.");
        RuleFor(x => x.Result).Must(r => r is null || (Enum.TryParse<SupplierReviewResult>(r, out var v) && Enum.IsDefined(v)))
            .WithMessage("Kết luận đánh giá không hợp lệ.");
        RuleFor(x => x.Comment).MaximumLength(SupplierReview.TextMaxLength);
    }
}

/// <summary>Kiểm tra hủy phiếu đánh giá.</summary>
public sealed class CancelSupplierReviewRequestValidator : AbstractValidator<CancelSupplierReviewRequest>
{
    /// <summary>Khai báo các quy tắc.</summary>
    public CancelSupplierReviewRequestValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Hãy nhập lý do hủy.").MaximumLength(SupplierReview.TextMaxLength);
    }
}
