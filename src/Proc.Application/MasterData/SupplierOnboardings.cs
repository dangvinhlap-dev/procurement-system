using System.Linq.Expressions;
using FluentValidation;
using Proc.Domain.MasterData;

namespace Proc.Application.MasterData;

/// <summary>Một giấy tờ trong hồ sơ pháp lý NCC mới.</summary>
/// <param name="Code">Mã giấy tờ (LICENSE, QUALITY, QUOTE, SAMPLE, FINANCE).</param>
/// <param name="Name">Tên giấy tờ.</param>
/// <param name="Provided">NCC đã nộp.</param>
/// <param name="ValidTo">Ngày hết hiệu lực.</param>
/// <param name="Note">Ghi chú.</param>
/// <param name="IsRequiredForPass">Bắt buộc khi kết luận Đạt.</param>
public sealed record SupplierOnboardingDocumentDto(string Code, string Name, bool Provided, DateOnly? ValidTo, string? Note, bool IsRequiredForPass);

/// <summary>Nút được bấm trên hồ sơ với người đang xem.</summary>
/// <param name="CanEdit">Sửa, hoàn thành đánh giá (Nháp, quyền lập hồ sơ).</param>
/// <param name="CanReopen">Mở lại đánh giá (Đã đánh giá, quyền lập hồ sơ).</param>
/// <param name="CanPropose">Đề xuất / không đề xuất (Đã đánh giá, quyền đề xuất, không phải người đánh giá).</param>
/// <param name="CanWithdraw">Rút đề xuất (Chờ duyệt, người đề xuất).</param>
/// <param name="CanCancel">Hủy (Nháp/Đã đánh giá, quyền lập hồ sơ).</param>
public sealed record SupplierOnboardingActions(bool CanEdit, bool CanReopen, bool CanPropose, bool CanWithdraw, bool CanCancel);

/// <summary>Hồ sơ đánh giá NCC mới (chi tiết).</summary>
public sealed record SupplierOnboardingDto(
    Guid Id, string No, Guid SupplierId, string SupplierCode, string SupplierName, string SupplierStatus, Guid CategoryId, string CategoryCode,
    string CategoryName, DateOnly Date, string? Source, IReadOnlyList<SupplierOnboardingDocumentDto> Documents, int? PriceScore,
    int? SampleQualityScore, int? CapacityScore, int? DeliveryScore, int? PaymentScore, int? LegalScore, decimal? Score, string? Result,
    string? Comment, string? Note, string Status, DateTimeOffset? EvaluatedAt, string? EvaluatedByName, bool? Recommended,
    string? ProposalComment, DateTimeOffset? ProposedAt, string? ProposedByName, Guid? ChangeRequestId, string? ChangeRequestNo,
    DateTimeOffset? ApprovedAt, string? RejectReason, string? CancelReason, DateTimeOffset CreatedAt, string? CreatedByName,
    SupplierOnboardingActions Actions);

/// <summary>Một dòng danh sách hồ sơ NCC mới.</summary>
public sealed record SupplierOnboardingListItemDto(
    Guid Id, string No, Guid SupplierId, string SupplierCode, string SupplierName, string CategoryName, DateOnly Date, decimal? Score,
    string? Result, bool? Recommended, string Status, string? CreatedByName, DateTimeOffset CreatedAt);

/// <summary>Khai một giấy tờ.</summary>
/// <param name="Code">Mã giấy tờ.</param>
/// <param name="Provided">Đã nộp.</param>
/// <param name="ValidTo">Ngày hết hiệu lực.</param>
/// <param name="Note">Ghi chú.</param>
public sealed record SupplierOnboardingDocumentRequest(string Code, bool Provided, DateOnly? ValidTo, string? Note);

/// <summary>Lập/sửa hồ sơ. Khi sửa, NCC giữ như lúc lập.</summary>
public sealed record SaveSupplierOnboardingRequest(
    Guid SupplierId, Guid CategoryId, DateOnly Date, string? Source, IReadOnlyList<SupplierOnboardingDocumentRequest>? Documents,
    int? PriceScore, int? SampleQualityScore, int? CapacityScore, int? DeliveryScore, int? PaymentScore, int? LegalScore, string? Result,
    string? Comment, string? Note);

/// <summary>Ý kiến của Văn phòng Ban.</summary>
/// <param name="Recommend">true = đề xuất đưa vào danh sách (gửi Hội đồng duyệt); false = không đề xuất (kết thúc hồ sơ).</param>
/// <param name="Comment">Ý kiến; bắt buộc khi không đề xuất.</param>
public sealed record ProposeSupplierOnboardingRequest(bool Recommend, string? Comment);

/// <summary>Hủy hồ sơ.</summary>
/// <param name="Reason">Lý do.</param>
public sealed record CancelSupplierOnboardingRequest(string Reason);

/// <summary>
/// Hồ sơ đánh giá NCC mới (lát G8, design.md mục 18.25): Phòng cung ứng chung lập, đánh giá; Văn phòng Ban đề xuất; Hội đồng duyệt qua yêu
/// cầu duyệt NCC (SUPAPR). Chỉ làm việc ở tổng công ty.
/// </summary>
public interface ISupplierOnboardingService
{
    /// <summary>Danh sách hồ sơ (mới nhất trước, tối đa 2000).</summary>
    /// <param name="status">Lọc trạng thái (tên enum), trống = mọi trạng thái.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Các dòng.</returns>
    Task<IReadOnlyList<SupplierOnboardingListItemDto>> ListAsync(string? status, CancellationToken cancellationToken);

    /// <summary>Chi tiết hồ sơ.</summary>
    /// <param name="id">Id hồ sơ.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Hồ sơ.</returns>
    /// <exception cref="Proc.Application.Common.NotFoundException">Khi không có hoặc ngoài phạm vi.</exception>
    Task<SupplierOnboardingDto> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Lập hồ sơ (Nháp) cho NCC Tiềm năng, cấp số loại HSN.</summary>
    /// <param name="request">Nội dung.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Hồ sơ vừa lập.</returns>
    /// <exception cref="Proc.Domain.Common.BusinessRuleException">Khi NCC không Tiềm năng, đã có hồ sơ mở, nhóm hàng không dùng được.</exception>
    Task<SupplierOnboardingDto> CreateAsync(SaveSupplierOnboardingRequest request, CancellationToken cancellationToken);

    /// <summary>Sửa hồ sơ nháp.</summary>
    /// <param name="id">Id hồ sơ.</param>
    /// <param name="request">Nội dung mới.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Hồ sơ sau khi sửa.</returns>
    Task<SupplierOnboardingDto> UpdateAsync(Guid id, SaveSupplierOnboardingRequest request, CancellationToken cancellationToken);

    /// <summary>Lưu nội dung rồi hoàn thành đánh giá trong một thao tác.</summary>
    /// <param name="id">Id hồ sơ.</param>
    /// <param name="request">Nội dung.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Hồ sơ Đã đánh giá.</returns>
    Task<SupplierOnboardingDto> EvaluateAsync(Guid id, SaveSupplierOnboardingRequest request, CancellationToken cancellationToken);

    /// <summary>Mở lại đánh giá (về Nháp) khi Văn phòng Ban chưa đề xuất.</summary>
    /// <param name="id">Id hồ sơ.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Hồ sơ Nháp.</returns>
    Task<SupplierOnboardingDto> ReopenAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Văn phòng Ban đề xuất (lập yêu cầu duyệt NCC sang Đã duyệt, hồ sơ Chờ duyệt) hoặc không đề xuất (hồ sơ kết thúc).
    /// </summary>
    /// <param name="id">Id hồ sơ.</param>
    /// <param name="request">Ý kiến.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Hồ sơ sau khi ghi ý kiến.</returns>
    Task<SupplierOnboardingDto> ProposeAsync(Guid id, ProposeSupplierOnboardingRequest request, CancellationToken cancellationToken);

    /// <summary>Người đề xuất rút yêu cầu duyệt còn chờ – hồ sơ về Đã đánh giá.</summary>
    /// <param name="id">Id hồ sơ.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Hồ sơ sau khi rút.</returns>
    Task<SupplierOnboardingDto> WithdrawAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Hủy hồ sơ Nháp/Đã đánh giá.</summary>
    /// <param name="id">Id hồ sơ.</param>
    /// <param name="request">Lý do.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Hồ sơ đã hủy.</returns>
    Task<SupplierOnboardingDto> CancelAsync(Guid id, CancelSupplierOnboardingRequest request, CancellationToken cancellationToken);
}

/// <summary>Kiểm tra lập/sửa hồ sơ NCC mới.</summary>
public sealed class SaveSupplierOnboardingRequestValidator : AbstractValidator<SaveSupplierOnboardingRequest>
{
    /// <summary>Khai báo các quy tắc.</summary>
    public SaveSupplierOnboardingRequestValidator()
    {
        RuleFor(x => x.SupplierId).NotEmpty().WithMessage("Hãy chọn nhà cung cấp.");
        RuleFor(x => x.CategoryId).NotEmpty().WithMessage("Hãy chọn nhóm hàng đề xuất NCC cung cấp.");
        RuleFor(x => x.Source).MaximumLength(SupplierOnboarding.ShortTextMaxLength);
        foreach (var score in new Expression<Func<SaveSupplierOnboardingRequest, int?>>[]
                 {
                     x => x.PriceScore, x => x.SampleQualityScore, x => x.CapacityScore, x => x.DeliveryScore, x => x.PaymentScore, x => x.LegalScore,
                 })
            RuleFor(score).InclusiveBetween(SupplierOnboarding.MinScore, SupplierOnboarding.MaxScore)
                .WithMessage($"Điểm mỗi tiêu chí từ {SupplierOnboarding.MinScore} đến {SupplierOnboarding.MaxScore}.");
        RuleFor(x => x.Result).Must(r => r is null || (Enum.TryParse<SupplierOnboardingResult>(r, out var v) && Enum.IsDefined(v)))
            .WithMessage("Kết luận đánh giá không hợp lệ.");
        RuleFor(x => x.Comment).MaximumLength(SupplierOnboarding.TextMaxLength);
        RuleFor(x => x.Note).MaximumLength(SupplierOnboarding.TextMaxLength);
        RuleForEach(x => x.Documents).ChildRules(d =>
        {
            d.RuleFor(x => x.Code).NotEmpty().WithMessage("Thiếu mã giấy tờ.");
            d.RuleFor(x => x.Note).MaximumLength(SupplierOnboarding.ShortTextMaxLength);
        });
    }
}

/// <summary>Kiểm tra ý kiến Văn phòng Ban.</summary>
public sealed class ProposeSupplierOnboardingRequestValidator : AbstractValidator<ProposeSupplierOnboardingRequest>
{
    /// <summary>Khai báo các quy tắc.</summary>
    public ProposeSupplierOnboardingRequestValidator()
    {
        RuleFor(x => x.Comment).NotEmpty().When(x => !x.Recommend).WithMessage("Không đề xuất phải ghi lý do.")
            .MaximumLength(SupplierOnboarding.TextMaxLength);
    }
}

/// <summary>Kiểm tra hủy hồ sơ.</summary>
public sealed class CancelSupplierOnboardingRequestValidator : AbstractValidator<CancelSupplierOnboardingRequest>
{
    /// <summary>Khai báo các quy tắc.</summary>
    public CancelSupplierOnboardingRequestValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Hãy nhập lý do hủy.").MaximumLength(SupplierOnboarding.TextMaxLength);
    }
}
