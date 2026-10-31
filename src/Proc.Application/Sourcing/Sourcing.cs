using FluentValidation;

namespace Proc.Application.Sourcing;

/// <summary>Một dòng danh sách phiếu tìm nguồn.</summary>
public sealed record SourcingListItemDto(
    Guid Id, string No, DateOnly Date, string Subject, decimal? Qty, DateOnly? Deadline, string? Responsible, int CandidateCount, int PassedCount,
    string Status);

/// <summary>NCC ứng viên.</summary>
public sealed record SourcingCandidateDto(Guid Id, Guid SupplierId, string SupplierCode, string SupplierName, string SupplierStatus, string Status, string? Note);

/// <summary>Chi tiết phiếu tìm nguồn; CanEdit = sửa được (đang mở, có quyền).</summary>
public sealed record SourcingDto(
    Guid Id, string No, DateOnly Date, Guid? ItemId, string? ItemCode, string? ItemName, string? UomCode, string? Description, string? Specification,
    decimal? Qty, DateOnly? Deadline, string? Responsible, string? Note, string Status, DateTimeOffset CreatedAt, string? CreatedByName,
    IReadOnlyList<SourcingCandidateDto> Candidates, bool CanEdit);

/// <summary>Lưu phiếu tìm nguồn.</summary>
public sealed record SaveSourcingRequest(
    DateOnly Date, Guid? ItemId, string? Description, string? Specification, decimal? Qty, DateOnly? Deadline, string? Responsible, string? Note,
    IReadOnlyList<SaveSourcingCandidateRequest> Candidates);

/// <summary>Lưu một NCC ứng viên (Status: Contacted, RfqSent, Quoted, Passed, Failed).</summary>
public sealed record SaveSourcingCandidateRequest(Guid? Id, Guid SupplierId, string Status, string? Note);

/// <summary>Danh sách tìm nguồn (design.md mục 8.3).</summary>
public interface ISourcingService
{
    /// <summary>Phiếu tìm nguồn của công ty đang làm việc, mới nhất trước.</summary>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Danh sách.</returns>
    Task<IReadOnlyList<SourcingListItemDto>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Chi tiết phiếu.</summary>
    /// <param name="id">Id.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Phiếu.</returns>
    Task<SourcingDto> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Lập phiếu.</summary>
    /// <param name="request">Nội dung.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Phiếu vừa lập.</returns>
    Task<SourcingDto> CreateAsync(SaveSourcingRequest request, CancellationToken cancellationToken);

    /// <summary>Sửa phiếu (đang mở).</summary>
    /// <param name="id">Id.</param>
    /// <param name="request">Nội dung.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Phiếu sau khi sửa.</returns>
    Task<SourcingDto> UpdateAsync(Guid id, SaveSourcingRequest request, CancellationToken cancellationToken);

    /// <summary>Đóng phiếu (đã xong).</summary>
    /// <param name="id">Id.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Phiếu.</returns>
    Task<SourcingDto> CloseAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Hủy phiếu.</summary>
    /// <param name="id">Id.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Phiếu.</returns>
    Task<SourcingDto> CancelAsync(Guid id, CancellationToken cancellationToken);
}

/// <summary>Một dòng danh sách RFQ.</summary>
public sealed record RfqListItemDto(
    Guid Id, string No, DateOnly Date, string Title, DateOnly Deadline, int LineCount, int SupplierCount, int QuotedCount, string Status,
    string? CreatedByName);

/// <summary>Dòng RFQ.</summary>
public sealed record RfqLineDto(
    Guid Id, int LineNo, Guid ItemId, string ItemCode, string ItemName, Guid UomId, string UomCode, decimal UomFactor, decimal Qty, Guid? SourcePrLineId,
    string? SourcePrNo, string? Note, Guid? AwardedSupplierId, string? AwardReason);

/// <summary>Giá một NCC chào cho một dòng RFQ; BaseNetPrice = giá quy về ĐVT cơ sở, trước VAT; IsLowest = thấp nhất của dòng.</summary>
public sealed record QuotationLineDto(
    Guid RfqLineId, decimal UnitPrice, decimal DiscountPct, string? VatCode, int? LeadTimeDays, string? Note, decimal BaseNetPrice, bool IsLowest);

/// <summary>
/// NCC được mời kèm báo giá và điểm: PriceScore = điểm giá 0–10 (tự tính), TotalScore = tổng điểm theo trọng số,
/// QuotedLineCount = số dòng có chào giá, AwardedAmount = giá trị trúng thầu (sau CK, trước VAT).
/// </summary>
public sealed record RfqSupplierDto(
    Guid SupplierId, string SupplierCode, string SupplierName, string SupplierStatus, bool HasQuoted, DateOnly? QuoteDate, DateOnly? ValidUntil,
    string? PaymentTerms, decimal QualityScore, decimal DeliveryScore, decimal PaymentScore, decimal WarrantyScore, string? Note,
    decimal PriceScore, decimal TotalScore, int QuotedLineCount, decimal AwardedAmount, Guid? PurchaseOrderId, string? PurchaseOrderNo,
    IReadOnlyList<QuotationLineDto> Lines);

/// <summary>Thao tác được phép trên RFQ.</summary>
public sealed record RfqActions(bool CanEdit, bool CanSend, bool CanQuote, bool CanAward, bool CanCreateOrders, bool CanCancel, string? ReadOnlyReason);

/// <summary>Trọng số tiêu chí (%), tổng 100.</summary>
public sealed record RfqWeightsDto(int Price, int Quality, int Delivery, int Payment, int Warranty);

/// <summary>Chi tiết RFQ.</summary>
public sealed record RfqDto(
    Guid Id, string No, DateOnly Date, string Title, DateOnly Deadline, Guid? SourcingId, string? SourcingNo, RfqWeightsDto Weights, string? Note,
    string Status, DateTimeOffset CreatedAt, string? CreatedByName, DateTimeOffset? SentAt, DateTimeOffset? AwardedAt, int MinSuppliers,
    IReadOnlyList<RfqLineDto> Lines, IReadOnlyList<RfqSupplierDto> Suppliers, RfqActions Actions);

/// <summary>Lưu RFQ (nháp).</summary>
public sealed record SaveRfqRequest(
    DateOnly Date, string Title, DateOnly Deadline, Guid? SourcingId, RfqWeightsDto Weights, string? Note, IReadOnlyList<SaveRfqLineRequest> Lines,
    IReadOnlyList<Guid> SupplierIds);

/// <summary>Lưu một dòng RFQ.</summary>
public sealed record SaveRfqLineRequest(Guid? Id, Guid ItemId, Guid UomId, decimal Qty, Guid? SourcePrLineId, string? Note);

/// <summary>Nhập báo giá của một NCC.</summary>
public sealed record SaveQuotationRequest(
    DateOnly QuoteDate, DateOnly? ValidUntil, string? PaymentTerms, decimal QualityScore, decimal DeliveryScore, decimal PaymentScore,
    decimal WarrantyScore, string? Note, IReadOnlyList<SaveQuotationLineRequest> Lines);

/// <summary>Giá một dòng trong báo giá.</summary>
public sealed record SaveQuotationLineRequest(Guid RfqLineId, decimal UnitPrice, decimal DiscountPct, string? VatCode, int? LeadTimeDays, string? Note);

/// <summary>Chọn thầu: mỗi dòng một NCC (hoặc null = không trao) kèm lý do.</summary>
public sealed record AwardRfqRequest(IReadOnlyList<AwardRfqLineRequest> Lines);

/// <summary>Chọn thầu một dòng.</summary>
public sealed record AwardRfqLineRequest(Guid RfqLineId, Guid? SupplierId, string? Reason);

/// <summary>PO nháp vừa sinh từ kết quả chọn thầu.</summary>
public sealed record CreatedOrderDto(Guid SupplierId, string SupplierCode, Guid PurchaseOrderId, string PurchaseOrderNo);

/// <summary>RFQ, báo giá, so sánh báo giá và chọn thầu (design.md mục 8.4).</summary>
public interface IRfqService
{
    /// <summary>RFQ của công ty đang làm việc; <paramref name="status"/> lọc theo trạng thái (Draft, Sent, Awarded, Cancelled).</summary>
    /// <param name="status">Trạng thái (null = mọi trạng thái).</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Danh sách, mới nhất trước.</returns>
    Task<IReadOnlyList<RfqListItemDto>> ListAsync(string? status, CancellationToken cancellationToken);

    /// <summary>Chi tiết RFQ kèm báo giá, điểm.</summary>
    /// <param name="id">Id.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>RFQ.</returns>
    Task<RfqDto> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Lập RFQ nháp.</summary>
    /// <param name="request">Nội dung.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>RFQ vừa lập.</returns>
    Task<RfqDto> CreateAsync(SaveRfqRequest request, CancellationToken cancellationToken);

    /// <summary>Sửa RFQ nháp.</summary>
    /// <param name="id">Id.</param>
    /// <param name="request">Nội dung.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>RFQ sau khi sửa.</returns>
    Task<RfqDto> UpdateAsync(Guid id, SaveRfqRequest request, CancellationToken cancellationToken);

    /// <summary>Gửi RFQ cho NCC.</summary>
    /// <param name="id">Id.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>RFQ.</returns>
    Task<RfqDto> SendAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Nhập/sửa báo giá của một NCC.</summary>
    /// <param name="id">Id RFQ.</param>
    /// <param name="supplierId">NCC.</param>
    /// <param name="request">Báo giá.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>RFQ.</returns>
    Task<RfqDto> SaveQuotationAsync(Guid id, Guid supplierId, SaveQuotationRequest request, CancellationToken cancellationToken);

    /// <summary>Chọn thầu.</summary>
    /// <param name="id">Id.</param>
    /// <param name="request">Lựa chọn theo dòng.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>RFQ.</returns>
    Task<RfqDto> AwardAsync(Guid id, AwardRfqRequest request, CancellationToken cancellationToken);

    /// <summary>Sinh PO nháp cho mỗi NCC trúng thầu chưa có PO (giá theo báo giá, kho nhận = kho tổng).</summary>
    /// <param name="id">Id.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Các PO vừa sinh.</returns>
    Task<IReadOnlyList<CreatedOrderDto>> CreateOrdersAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Hủy RFQ.</summary>
    /// <param name="id">Id.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>RFQ.</returns>
    Task<RfqDto> CancelAsync(Guid id, CancellationToken cancellationToken);
}

/// <summary>Kiểm tra dữ liệu lưu phiếu tìm nguồn.</summary>
public sealed class SaveSourcingRequestValidator : AbstractValidator<SaveSourcingRequest>
{
    /// <summary>Khai báo quy tắc.</summary>
    public SaveSourcingRequestValidator()
    {
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.Specification).MaximumLength(2000);
        RuleFor(x => x.Responsible).MaximumLength(200);
        RuleFor(x => x.Note).MaximumLength(1000);
        RuleForEach(x => x.Candidates).ChildRules(c =>
        {
            c.RuleFor(x => x.SupplierId).NotEmpty().WithMessage("Hãy chọn NCC.");
            c.RuleFor(x => x.Note).MaximumLength(500);
        });
    }
}

/// <summary>Kiểm tra dữ liệu lưu RFQ.</summary>
public sealed class SaveRfqRequestValidator : AbstractValidator<SaveRfqRequest>
{
    /// <summary>Khai báo quy tắc.</summary>
    public SaveRfqRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().WithMessage("Hãy nhập tiêu đề RFQ.").MaximumLength(200);
        RuleFor(x => x.Note).MaximumLength(1000);
        RuleFor(x => x.Weights).NotNull();
        RuleFor(x => x.Lines).NotEmpty().WithMessage("RFQ phải có ít nhất một dòng hàng.");
        RuleForEach(x => x.Lines).ChildRules(l =>
        {
            l.RuleFor(x => x.ItemId).NotEmpty().WithMessage("Hãy chọn mặt hàng.");
            l.RuleFor(x => x.UomId).NotEmpty().WithMessage("Hãy chọn ĐVT.");
            l.RuleFor(x => x.Note).MaximumLength(500);
        });
    }
}

/// <summary>Kiểm tra dữ liệu báo giá.</summary>
public sealed class SaveQuotationRequestValidator : AbstractValidator<SaveQuotationRequest>
{
    /// <summary>Khai báo quy tắc.</summary>
    public SaveQuotationRequestValidator()
    {
        RuleFor(x => x.PaymentTerms).MaximumLength(200);
        RuleFor(x => x.Note).MaximumLength(1000);
        RuleForEach(x => x.Lines).ChildRules(l => l.RuleFor(x => x.Note).MaximumLength(500));
    }
}
