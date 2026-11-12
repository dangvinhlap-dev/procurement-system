using FluentValidation;
using Proc.Application.Purchasing;

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

    /// <param name="request">Lý do hủy (bắt buộc – design.md mục 20.2a).</param>
    Task<SourcingDto> CancelAsync(Guid id, CancelDocumentRequest request, CancellationToken cancellationToken);
}

/// <summary>Một dòng danh sách RFQ.</summary>
public sealed record RfqListItemDto(
    Guid Id, string No, DateOnly Date, string Title, DateOnly Deadline, int LineCount, int SupplierCount, int QuotedCount, string Status,
    string? CreatedByName);

/// <summary>Dòng RFQ. SourcePrId: Id PR nguồn – mở phiếu gốc từ ô số chứng từ (design.md mục 20.1 R5).</summary>
public sealed record RfqLineDto(
    Guid Id, int LineNo, Guid ItemId, string ItemCode, string ItemName, Guid UomId, string UomCode, decimal UomFactor, decimal Qty, Guid? SourcePrLineId,
    string? SourcePrNo, string? Note, Guid? AwardedSupplierId, string? AwardReason, Guid? SourcePrId = null);

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
    IReadOnlyList<QuotationLineDto> Lines, Guid? PriceListId = null, string? PriceListNo = null, Guid? ContractId = null, string? ContractNo = null);

/// <summary>Một mục hồ sơ trình (lát B3b); IsAutomatic = hệ thống tự tính (bảng so sánh).</summary>
public sealed record RfqDossierItemDto(string Code, string Name, bool IsDone, string? Note, bool IsAutomatic);

/// <summary>Lưu đánh dấu hồ sơ trình (mục không gửi giữ nguyên).</summary>
public sealed record SaveRfqDossierRequest(IReadOnlyList<SaveRfqDossierItemRequest> Items);

/// <summary>Đánh dấu một mục hồ sơ trình.</summary>
public sealed record SaveRfqDossierItemRequest(string Code, bool IsDone, string? Note);

/// <summary>Hợp đồng nguyên tắc nháp vừa sinh từ kết quả chọn thầu (design.md mục 8.6).</summary>
public sealed record CreatedContractDto(Guid SupplierId, string SupplierCode, Guid ContractId, string ContractNo);

/// <summary>Bảng giá NCC nháp vừa sinh từ kết quả chọn thầu (lát B3b).</summary>
public sealed record CreatedPriceListDto(Guid SupplierId, string SupplierCode, Guid PriceListId, string PriceListNo);

/// <summary>
/// Thao tác được phép trên RFQ. CanAward = chọn thầu và gửi duyệt; CanApprove = người dùng duyệt/từ chối được bước đang đến lượt;
/// CanRecall = rút kết quả đang chờ duyệt về để chọn lại; ConflictReason = người dùng có kê khai xung đột lợi ích với NCC được mời nên
/// không nhập báo giá, chọn thầu được (design.md mục 18.8).
/// </summary>
public sealed record RfqActions(
    bool CanEdit, bool CanSend, bool CanQuote, bool CanAward, bool CanCreateOrders, bool CanCancel, string? ReadOnlyReason, bool CanApprove,
    bool CanRecall, bool CanEditDossier = false, bool CanCreatePriceLists = false, string? ConflictReason = null, bool CanCreateContracts = false,
    bool CanExtendDeadline = false);

/// <summary>Trọng số tiêu chí (%), tổng 100.</summary>
public sealed record RfqWeightsDto(int Price, int Quality, int Delivery, int Payment, int Warranty);

/// <summary>
/// Chi tiết RFQ. FewSuppliersReason = giải trình khi mời thiếu NCC; AwardAmount = giá trị trúng thầu xét ngưỡng duyệt; AwardedByName =
/// người chọn thầu; RejectReason = lý do từ chối kết quả gần nhất; Approvals = lịch sử duyệt kết quả chọn thầu (lát B3).
/// </summary>
public sealed record RfqDto(
    Guid Id, string No, DateOnly Date, string Title, DateOnly Deadline, Guid? SourcingId, string? SourcingNo, RfqWeightsDto Weights, string? Note,
    string Status, DateTimeOffset CreatedAt, string? CreatedByName, DateTimeOffset? SentAt, DateTimeOffset? AwardedAt, int MinSuppliers,
    IReadOnlyList<RfqLineDto> Lines, IReadOnlyList<RfqSupplierDto> Suppliers, RfqActions Actions, string? FewSuppliersReason,
    decimal AwardAmount, string? AwardedByName, string? RejectReason, IReadOnlyList<ApprovalStepDto> Approvals,
    IReadOnlyList<RfqDossierItemDto> Dossier, uint Version = 0)
{
    /// <summary>Giải trình ít báo giá lúc chọn thầu (lát R7).</summary>
    public string? FewQuotesReason { get; init; }

    /// <summary>Lý do gia hạn nhận báo giá gần nhất (lát R7).</summary>
    public string? DeadlineExtensionReason { get; init; }

    /// <summary>Số NCC đã chào giá ít nhất một dòng.</summary>
    public int QuotedSupplierCount { get; init; }

    /// <summary>Loại mua (lát Y6): "Construction" (A – công trình) hoặc "Goods" (B – hàng hóa, gồm dịch vụ).</summary>
    public string Category { get; init; } = "Goods";

    /// <summary>Công trình của RFQ loại A.</summary>
    public Guid? ConstructionProjectId { get; init; }

    /// <summary>Mã – tên công trình của RFQ loại A.</summary>
    public string? ConstructionProjectName { get; init; }

    /// <summary>
    /// Biểu mẫu kết quả "3A" / "3a" / "3B": đã chọn thầu thì là biểu mẫu chốt lúc chọn; chưa chọn thì loại B là "3B", loại A null (tùy giá trị
    /// trúng thầu so với <see cref="ComparisonThreshold"/>).
    /// </summary>
    public string? FormCode { get; init; }

    /// <summary>Ngưỡng so sánh 3 NCC loại A của công ty (VND chưa VAT) – trúng thầu từ ngưỡng trở lên là 3A.</summary>
    public decimal ComparisonThreshold { get; init; }

    /// <summary>Lý do chọn NCC (biểu mẫu 3A).</summary>
    public string? SelectionReason { get; init; }
}

/// <summary>Gửi RFQ; Reason = giải trình khi mời ít hơn số NCC tối thiểu (design.md mục 8.6).</summary>
public sealed record SendRfqRequest(string? Reason);

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
/// <param name="Lines">Lựa chọn từng dòng.</param>
/// <param name="FewQuotesReason">Giải trình khi số NCC chào giá ít hơn tối thiểu (lát R7).</param>
/// <param name="SelectionReason">Lý do chọn NCC – bắt buộc khi biểu mẫu là 3A (lát Y6).</param>
public sealed record AwardRfqRequest(IReadOnlyList<AwardRfqLineRequest> Lines, string? FewQuotesReason = null, string? SelectionReason = null);

/// <summary>Gia hạn nhận báo giá (lát R7, design.md mục 18.26).</summary>
/// <param name="Deadline">Hạn mới.</param>
/// <param name="Reason">Lý do, bắt buộc.</param>
public sealed record ExtendRfqDeadlineRequest(DateOnly Deadline, string? Reason);

/// <summary>Chọn thầu một dòng.</summary>
public sealed record AwardRfqLineRequest(Guid RfqLineId, Guid? SupplierId, string? Reason);

/// <summary>PO nháp vừa sinh từ kết quả chọn thầu.</summary>
public sealed record CreatedOrderDto(Guid SupplierId, string SupplierCode, Guid PurchaseOrderId, string PurchaseOrderNo);

/// <summary>RFQ, báo giá, so sánh báo giá và chọn thầu (design.md mục 8.4).</summary>
public interface IRfqService
{
    /// <summary>RFQ của công ty đang làm việc; <paramref name="status"/> lọc theo trạng thái (Draft, Sent, AwardPending, Awarded, Cancelled).</summary>
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

    /// <summary>Gửi RFQ cho NCC; mời thiếu số NCC tối thiểu thì phải có giải trình.</summary>
    /// <param name="id">Id.</param>
    /// <param name="request">Giải trình (nếu mời thiếu).</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>RFQ.</returns>
    Task<RfqDto> SendAsync(Guid id, SendRfqRequest request, CancellationToken cancellationToken);

    /// <summary>Nhập/sửa báo giá của một NCC.</summary>
    /// <param name="id">Id RFQ.</param>
    /// <param name="supplierId">NCC.</param>
    /// <param name="request">Báo giá.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>RFQ.</returns>
    Task<RfqDto> SaveQuotationAsync(Guid id, Guid supplierId, SaveQuotationRequest request, CancellationToken cancellationToken);

    /// <summary>Chọn thầu và gửi hội đồng duyệt kết quả (quy trình loại RFQ của công ty, xét giá trị trúng thầu).</summary>
    /// <param name="id">Id.</param>
    /// <param name="request">Lựa chọn theo dòng.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>RFQ.</returns>
    Task<RfqDto> AwardAsync(Guid id, AwardRfqRequest request, CancellationToken cancellationToken);

    /// <summary>Gia hạn nhận báo giá khi RFQ đang nhận báo giá (lát R7).</summary>
    /// <param name="id">Id.</param>
    /// <param name="request">Hạn mới và lý do.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>RFQ.</returns>
    Task<RfqDto> ExtendDeadlineAsync(Guid id, ExtendRfqDeadlineRequest request, CancellationToken cancellationToken);

    /// <summary>Duyệt kết quả chọn thầu ở bước đến lượt; duyệt đủ thì RFQ Đã chọn thầu.</summary>
    /// <param name="id">Id.</param>
    /// <param name="request">Ý kiến.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>RFQ.</returns>
    /// <exception cref="Domain.Common.BusinessRuleException">Khi người dùng không duyệt được (người lập/người chọn thầu, chưa đến lượt…).</exception>
    Task<RfqDto> ApproveAsync(Guid id, ApprovalActionRequest request, CancellationToken cancellationToken);

    /// <summary>Từ chối kết quả chọn thầu (bắt buộc lý do): RFQ về Đang nhận báo giá.</summary>
    /// <param name="id">Id.</param>
    /// <param name="request">Lý do.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>RFQ.</returns>
    Task<RfqDto> RejectAsync(Guid id, ApprovalActionRequest request, CancellationToken cancellationToken);

    /// <summary>Rút kết quả đang chờ duyệt về Đang nhận báo giá để chọn lại.</summary>
    /// <param name="id">Id.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>RFQ.</returns>
    Task<RfqDto> RecallAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Sinh PO nháp cho mỗi NCC trúng thầu chưa có PO (giá theo báo giá, kho nhận = kho tổng).</summary>
    /// <param name="id">Id.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Các PO vừa sinh.</returns>
    Task<IReadOnlyList<CreatedOrderDto>> CreateOrdersAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Hủy RFQ.</summary>
    /// <param name="id">Id.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>RFQ.</returns>

    /// <param name="request">Lý do hủy (bắt buộc – design.md mục 20.2a).</param>
    Task<RfqDto> CancelAsync(Guid id, CancelDocumentRequest request, CancellationToken cancellationToken);

    /// <summary>Lưu đánh dấu hồ sơ trình (Nháp/Đang nhận báo giá).</summary>
    /// <param name="id">Id.</param>
    /// <param name="request">Các mục.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>RFQ.</returns>
    Task<RfqDto> SaveDossierAsync(Guid id, SaveRfqDossierRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Sinh bảng giá NCC nháp cho mỗi NCC trúng thầu chưa có bảng giá (RFQ đã duyệt kết quả): giá theo báo giá, hiệu lực từ hôm nay; bảng
    /// giá vẫn phải gửi duyệt (design.md mục 8.5).
    /// </summary>
    /// <param name="id">Id.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Các bảng giá vừa sinh.</returns>
    Task<IReadOnlyList<CreatedPriceListDto>> CreatePriceListsAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Sinh hợp đồng nguyên tắc nháp cho mỗi NCC trúng thầu chưa có hợp đồng (hoặc hợp đồng cũ đã hủy) – RFQ đã duyệt kết quả, NCC đã có bảng
    /// giá sinh từ RFQ (design.md mục 8.6, 18.7). Hợp đồng vẫn phải gửi duyệt. Cần quyền <c>Purchase.Contract.Manage</c>.
    /// </summary>
    /// <param name="id">Id RFQ.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Các hợp đồng vừa sinh.</returns>
    /// <exception cref="Domain.Common.BusinessRuleException">Khi RFQ chưa duyệt, NCC chưa có bảng giá từ RFQ hoặc mọi NCC đã có hợp đồng.</exception>
    Task<IReadOnlyList<CreatedContractDto>> CreateContractsAsync(Guid id, CancellationToken cancellationToken);
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

/// <summary>Giá trúng thầu của một dòng RFQ đã chọn thầu (nguồn giá PO – design.md mục 18.10).</summary>
/// <param name="RfqLineId">Dòng RFQ.</param>
/// <param name="RfqNo">Số RFQ.</param>
/// <param name="ItemId">Mặt hàng.</param>
/// <param name="UomId">ĐVT.</param>
/// <param name="UnitPrice">Đơn giá báo giá trúng thầu, trước CK.</param>
/// <param name="DiscountPct">% chiết khấu báo giá.</param>
/// <param name="RfqId">Id RFQ – mở phiếu gốc từ ô số chứng từ (design.md mục 20.1 R5).</param>
public sealed record RfqAwardPriceDto(
    Guid RfqLineId, string RfqNo, Guid ItemId, Guid UomId, decimal UnitPrice, decimal DiscountPct, Guid? RfqId = null);

/// <summary>
/// Đọc giá trúng thầu cho module mua hàng (module chỉ gọi nhau qua interface – CLAUDE.md mục 3). Tách khỏi <see cref="IRfqService"/>
/// vì RfqService đã dùng <see cref="IPurchaseOrderService"/> để lập PO (tránh phụ thuộc vòng).
/// </summary>
public interface IRfqAwardPriceReader
{
    /// <summary>
    /// Giá trúng thầu của các dòng RFQ thuộc công ty đang làm việc, RFQ ở trạng thái Đã chọn thầu (kết quả đã duyệt) và dòng trúng
    /// thầu cho đúng NCC. Dòng không thỏa thì không có trong kết quả.
    /// </summary>
    /// <param name="supplierId">NCC của PO.</param>
    /// <param name="rfqLineIds">Các dòng RFQ.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Giá theo dòng RFQ.</returns>
    Task<IReadOnlyList<RfqAwardPriceDto>> GetAsync(Guid supplierId, IReadOnlyList<Guid> rfqLineIds, CancellationToken cancellationToken);
}
