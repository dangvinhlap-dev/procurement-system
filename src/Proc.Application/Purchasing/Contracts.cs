using FluentValidation;

namespace Proc.Application.Purchasing;

/// <summary>Một dòng danh sách hợp đồng nguyên tắc; ExpiringSoon = hiệu lực còn ≤ 30 ngày.</summary>
public sealed record ContractListItemDto(
    Guid Id, string No, string? ContractNo, string Title, string SupplierCode, string SupplierName, DateOnly ValidFrom, DateOnly ValidTo,
    decimal ExpectedAmount, string Status, bool ExpiringSoon, string? CreatedByName);

/// <summary>Sản lượng dự kiến một mặt hàng; UnitPrice, DiscountPct theo bảng giá gắn với hợp đồng.</summary>
public sealed record ContractLineDto(
    Guid Id, int LineNo, Guid ItemId, string ItemCode, string ItemName, Guid UomId, string UomCode, decimal ExpectedQty, decimal? MinCommitQty,
    decimal UnitPrice, decimal DiscountPct, decimal ExpectedAmount, string? Note);

/// <summary>
/// Thao tác được phép trên hợp đồng. CanAmend: lập phụ lục được (hợp đồng hiệu lực, chưa có phụ lục đang mở, có quyền sửa).
/// </summary>
public sealed record ContractActions(
    bool CanEdit, bool CanSubmit, bool CanApprove, bool CanRecall, bool CanTerminate, bool CanCancel, bool CanAmend, string? ReadOnlyReason);

/// <summary>Một phụ lục trong tab Phụ lục của hợp đồng.</summary>
public sealed record ContractAmendmentListItemDto(
    Guid Id, string No, string? PaperNo, DateOnly? SignDate, string Reason, string ChangeSummary, decimal ExpectedAmount, string Status,
    DateTimeOffset CreatedAt, string? CreatedByName, DateTimeOffset? ApprovedAt);

/// <summary>Chi tiết hợp đồng nguyên tắc (design.md mục 18.7).</summary>
public sealed record ContractDto(
    Guid Id, string No, string CompanyCode, Guid SupplierId, string SupplierCode, string SupplierName, Guid PriceListId, string PriceListNo,
    string PriceListTitle, string PriceListStatus, string? ContractNo, string Title, DateOnly? SignDate, DateOnly ValidFrom, DateOnly ValidTo,
    string? PriceAdjustmentTerms, string? PaymentTerms, decimal? CreditLimit, string? DeliveryTerms, string? DiscountTerms, string? SponsorshipTerms,
    string? PenaltyTerms, string? OtherTerms, string? Note, string Status, decimal ExpectedAmount, IReadOnlyList<Guid> DepartmentIds,
    IReadOnlyList<string> DepartmentNames, IReadOnlyList<ContractLineDto> Lines, DateTimeOffset CreatedAt, string? CreatedByName,
    DateTimeOffset? ActivatedAt, DateOnly? TerminatedOn, string? TerminationReason, string? RejectReason, int DaysToExpiry, bool ExpiringSoon,
    IReadOnlyList<ApprovalStepDto> Approvals, IReadOnlyList<ContractAmendmentListItemDto> Amendments, ContractActions Actions,
    IReadOnlyList<ContractRebateDto> Rebates);

/// <summary>
/// Cam kết chiết khấu doanh số/tài trợ trên hợp đồng (lát F4b – design.md mục 18.21). Kind: SalesVolume, Sponsorship, Other; Method: Percent
/// (Value = %), FixedAmount (Value = số tiền mỗi kỳ); Period: Quarter, Year, WholeContract.
/// </summary>
public sealed record ContractRebateDto(
    Guid Id, int LineNo, string Kind, string Method, decimal Value, decimal? MinPurchaseAmount, string Period, string Description);

/// <summary>Lưu một cam kết chiết khấu/tài trợ (giá trị chữ như <see cref="ContractRebateDto"/>).</summary>
public sealed record SaveContractRebateRequest(
    Guid? Id, string Kind, string Method, decimal Value, decimal? MinPurchaseAmount, string Period, string Description);

/// <summary>
/// Theo dõi một kỳ quyết toán của một cam kết. Status: NotDue (chưa hết kỳ), Waiting (đã hết kỳ, còn trong hạn ghi nhận), Received (đủ),
/// NotReached (chưa đạt doanh số tối thiểu – không phải nhận), Short (quá hạn ghi nhận mà thực nhận ít hơn phải nhận).
/// </summary>
/// <param name="RebateId">Cam kết.</param>
/// <param name="RebateLineNo">STT cam kết.</param>
/// <param name="Kind">Loại cam kết.</param>
/// <param name="Description">Nội dung cam kết.</param>
/// <param name="PeriodFrom">Từ ngày.</param>
/// <param name="PeriodTo">Đến ngày.</param>
/// <param name="DueDate">Hạn ghi nhận = cuối kỳ + số ngày hạn.</param>
/// <param name="PurchaseAmount">Doanh số mua trước VAT trong kỳ (PO Đã duyệt/Đã đóng của công ty × NCC).</param>
/// <param name="ExpectedAmount">Phải nhận.</param>
/// <param name="ReceivedAmount">Đã ghi nhận (không tính bản hủy).</param>
/// <param name="ShortAmount">Còn thiếu = phải nhận − đã nhận (không âm).</param>
/// <param name="Status">Tình trạng.</param>
public sealed record ContractRebatePeriodDto(
    Guid RebateId, int RebateLineNo, string Kind, string Description, DateOnly PeriodFrom, DateOnly PeriodTo, DateOnly DueDate, decimal PurchaseAmount,
    decimal ExpectedAmount, decimal ReceivedAmount, decimal ShortAmount, string Status);

/// <summary>Một lần ghi nhận thực nhận.</summary>
public sealed record ContractRebateReceiptDto(
    Guid Id, Guid RebateId, int RebateLineNo, DateOnly PeriodFrom, DateOnly ReceivedOn, decimal Amount, string Form, string? RefNo, string? Note,
    bool IsCancelled, string? CancelReason, DateTimeOffset CreatedAt, string? CreatedByName);

/// <summary>Theo dõi chiết khấu/tài trợ của hợp đồng; CanRecord: được ghi nhận/hủy thực nhận.</summary>
public sealed record ContractRebateTrackingDto(
    int GraceDays, IReadOnlyList<ContractRebatePeriodDto> Periods, IReadOnlyList<ContractRebateReceiptDto> Receipts, bool CanRecord);

/// <summary>Ghi nhận thực nhận; Form: DebtOffset, Transfer, Goods, Other.</summary>
public sealed record SaveContractRebateReceiptRequest(
    Guid RebateId, DateOnly PeriodFrom, DateOnly ReceivedOn, decimal Amount, string Form, string? RefNo, string? Note);

/// <summary>Hủy ghi nhận thực nhận (bắt buộc lý do).</summary>
public sealed record CancelContractRebateReceiptRequest(string Reason);

/// <summary>
/// Chiết khấu doanh số/tài trợ trên hợp đồng nguyên tắc – theo dõi thực nhận (lát F4b – design.md mục 18.21, đề xuất Ban CUVT P10).
/// </summary>
public interface IContractRebateService
{
    /// <summary>Các kỳ quyết toán (phải nhận, đã nhận, còn thiếu) và các lần ghi nhận của hợp đồng.</summary>
    /// <param name="contractId">Hợp đồng.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    Task<ContractRebateTrackingDto> GetTrackingAsync(Guid contractId, CancellationToken cancellationToken);

    /// <summary>Ghi nhận một khoản thực nhận.</summary>
    /// <param name="contractId">Hợp đồng.</param>
    /// <param name="request">Nội dung.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <exception cref="Common.NotFoundException">Khi không thấy hợp đồng.</exception>
    Task<ContractRebateTrackingDto> RecordAsync(Guid contractId, SaveContractRebateReceiptRequest request, CancellationToken cancellationToken);

    /// <summary>Hủy một lần ghi nhận (giữ lại bản ghi).</summary>
    /// <param name="receiptId">Bản ghi thực nhận.</param>
    /// <param name="request">Lý do.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    Task<ContractRebateTrackingDto> CancelReceiptAsync(Guid receiptId, CancelContractRebateReceiptRequest request, CancellationToken cancellationToken);
}

/// <summary>Điều khoản, hạn mức công nợ (để so sánh trước/sau phụ lục).</summary>
public sealed record ContractTermsDto(
    string? PriceAdjustmentTerms, string? PaymentTerms, decimal? CreditLimit, string? DeliveryTerms, string? DiscountTerms, string? SponsorshipTerms,
    string? PenaltyTerms, string? OtherTerms);

/// <summary>Thao tác được phép trên phụ lục.</summary>
public sealed record ContractAmendmentActions(bool CanEdit, bool CanSubmit, bool CanApprove, bool CanRecall, bool CanCancel, string? ReadOnlyReason);

/// <summary>
/// Chi tiết phụ lục hợp đồng (lát B4b – design.md mục 18.7). Terms là điều khoản sau phụ lục; CurrentTerms, CurrentExpectedAmount là của
/// hợp đồng hiện hành (duyệt xong thì trùng giá trị mới – xem ChangeSummary để biết đã đổi gì).
/// </summary>
public sealed record ContractAmendmentDto(
    Guid Id, string No, Guid ContractId, string ContractNo, string ContractTitle, string SupplierCode, string SupplierName, string? PaperNo,
    string Reason, DateOnly? SignDate, DateOnly ValidFrom, DateOnly OldValidTo, DateOnly NewValidTo, Guid OldPriceListId, string OldPriceListNo,
    Guid NewPriceListId, string NewPriceListNo, string NewPriceListTitle, string NewPriceListStatus, ContractTermsDto Terms,
    ContractTermsDto CurrentTerms, string? Note, decimal CurrentExpectedAmount, decimal ExpectedAmount, string ChangeSummary, string Status,
    string? RejectReason, DateTimeOffset CreatedAt, string? CreatedByName, DateTimeOffset? ApprovedAt, IReadOnlyList<ApprovalStepDto> Approvals,
    ContractAmendmentActions Actions);

/// <summary>Lưu phụ lục nháp: giá trị của hợp đồng sau phụ lục (điền sẵn bằng giá trị hiện hành).</summary>
public sealed record SaveContractAmendmentRequest(
    string? PaperNo, string Reason, DateOnly? SignDate, DateOnly NewValidTo, Guid NewPriceListId, string? PriceAdjustmentTerms, string? PaymentTerms,
    decimal? CreditLimit, string? DeliveryTerms, string? DiscountTerms, string? SponsorshipTerms, string? PenaltyTerms, string? OtherTerms, string? Note);

/// <summary>Lưu hợp đồng nháp.</summary>
public sealed record SaveContractRequest(
    Guid SupplierId, Guid PriceListId, string? ContractNo, string Title, DateOnly? SignDate, DateOnly ValidFrom, DateOnly ValidTo,
    string? PriceAdjustmentTerms, string? PaymentTerms, decimal? CreditLimit, string? DeliveryTerms, string? DiscountTerms, string? SponsorshipTerms,
    string? PenaltyTerms, string? OtherTerms, string? Note, IReadOnlyList<Guid> DepartmentIds, IReadOnlyList<SaveContractLineRequest> Lines,
    IReadOnlyList<SaveContractRebateRequest>? Rebates = null);

/// <summary>Lưu một dòng sản lượng dự kiến.</summary>
public sealed record SaveContractLineRequest(Guid? Id, Guid ItemId, Guid UomId, decimal ExpectedQty, decimal? MinCommitQty, string? Note);

/// <summary>Chấm dứt hợp đồng: ngày và lý do.</summary>
public sealed record TerminateContractRequest(DateOnly Date, string? Reason);

/// <summary>
/// Hợp đồng nguyên tắc phương án 2 (lát B4a – design.md mục 18.7): mỗi công ty một hợp đồng với NCC, dùng chung bảng giá; duyệt qua quy
/// trình loại CTR.
/// </summary>
public interface IContractService
{
    /// <summary>Hợp đồng của công ty đang làm việc, mới nhất trước.</summary>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    Task<IReadOnlyList<ContractListItemDto>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Chi tiết hợp đồng.</summary>
    /// <param name="id">Id.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    Task<ContractDto> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Lập hợp đồng nháp.</summary>
    /// <param name="request">Nội dung.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    Task<ContractDto> CreateAsync(SaveContractRequest request, CancellationToken cancellationToken);

    /// <summary>Sửa hợp đồng nháp.</summary>
    /// <param name="id">Id.</param>
    /// <param name="request">Nội dung.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    Task<ContractDto> UpdateAsync(Guid id, SaveContractRequest request, CancellationToken cancellationToken);

    /// <summary>Gửi duyệt (kiểm tra NCC đã duyệt, không chồng hiệu lực với hợp đồng khác cùng NCC).</summary>
    /// <param name="id">Id.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    Task<ContractDto> SubmitAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Duyệt bước đến lượt; duyệt cấp cuối thì hợp đồng hiệu lực (bảng giá phải đang hiệu lực).</summary>
    /// <param name="id">Id.</param>
    /// <param name="request">Ý kiến.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    Task<ContractDto> ApproveAsync(Guid id, ApprovalActionRequest request, CancellationToken cancellationToken);

    /// <summary>Từ chối (bắt buộc lý do): về nháp.</summary>
    /// <param name="id">Id.</param>
    /// <param name="request">Lý do.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    Task<ContractDto> RejectAsync(Guid id, ApprovalActionRequest request, CancellationToken cancellationToken);

    /// <summary>Rút hợp đồng đang chờ duyệt về nháp.</summary>
    /// <param name="id">Id.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    Task<ContractDto> RecallAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Chấm dứt hợp đồng đang hiệu lực (ngày, lý do).</summary>
    /// <param name="id">Id.</param>
    /// <param name="request">Ngày, lý do.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    Task<ContractDto> TerminateAsync(Guid id, TerminateContractRequest request, CancellationToken cancellationToken);

    /// <summary>Hủy hợp đồng nháp.</summary>
    /// <param name="id">Id.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    Task<ContractDto> CancelAsync(Guid id, CancellationToken cancellationToken);
}

/// <summary>
/// Phụ lục hợp đồng nguyên tắc (lát B4b – design.md mục 18.7): gia hạn, đổi bảng giá, đổi điều khoản của hợp đồng hiệu lực; mỗi phụ lục
/// duyệt qua quy trình loại CTA, duyệt xong áp vào hợp đồng.
/// </summary>
public interface IContractAmendmentService
{
    /// <summary>Chi tiết phụ lục (không thấy hoặc thuộc công ty khác thì báo không tìm thấy).</summary>
    /// <param name="id">Id phụ lục.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    Task<ContractAmendmentDto> GetAmendmentAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Lập phụ lục nháp cho hợp đồng đang hiệu lực (mỗi lúc một phụ lục đang mở).</summary>
    /// <param name="contractId">Hợp đồng.</param>
    /// <param name="request">Nội dung.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    Task<ContractAmendmentDto> CreateAmendmentAsync(Guid contractId, SaveContractAmendmentRequest request, CancellationToken cancellationToken);

    /// <summary>Sửa phụ lục nháp.</summary>
    /// <param name="id">Id phụ lục.</param>
    /// <param name="request">Nội dung.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    Task<ContractAmendmentDto> UpdateAmendmentAsync(Guid id, SaveContractAmendmentRequest request, CancellationToken cancellationToken);

    /// <summary>Gửi duyệt phụ lục; giá trị dưới mọi ngưỡng đã cấu hình thì duyệt và áp vào hợp đồng ngay.</summary>
    /// <param name="id">Id phụ lục.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    Task<ContractAmendmentDto> SubmitAmendmentAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Duyệt bước đến lượt; cấp cuối thì áp phụ lục vào hợp đồng.</summary>
    /// <param name="id">Id phụ lục.</param>
    /// <param name="request">Ý kiến.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    Task<ContractAmendmentDto> ApproveAmendmentAsync(Guid id, ApprovalActionRequest request, CancellationToken cancellationToken);

    /// <summary>Từ chối (bắt buộc lý do): phụ lục về nháp.</summary>
    /// <param name="id">Id phụ lục.</param>
    /// <param name="request">Lý do.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    Task<ContractAmendmentDto> RejectAmendmentAsync(Guid id, ApprovalActionRequest request, CancellationToken cancellationToken);

    /// <summary>Rút phụ lục đang chờ duyệt về nháp.</summary>
    /// <param name="id">Id phụ lục.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    Task<ContractAmendmentDto> RecallAmendmentAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Hủy phụ lục nháp.</summary>
    /// <param name="id">Id phụ lục.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    Task<ContractAmendmentDto> CancelAmendmentAsync(Guid id, CancellationToken cancellationToken);
}

/// <summary>Kết quả nhập Excel hợp đồng hiện hữu.</summary>
/// <param name="TotalRows">Số dòng dữ liệu (bỏ dòng trống).</param>
/// <param name="Contracts">Số hợp đồng sẽ tạo / đã tạo.</param>
/// <param name="Lines">Số dòng sản lượng sẽ tạo / đã tạo.</param>
/// <param name="Errors">Các lỗi (Code = số HĐ giấy); có lỗi thì không hợp đồng nào được lưu.</param>
/// <param name="Committed">true khi đã lưu thật; false khi chỉ kiểm tra thử hoặc có lỗi.</param>
public sealed record ContractImportResult(int TotalRows, int Contracts, int Lines, IReadOnlyList<Excel.ExcelImportError> Errors, bool Committed);

/// <summary>
/// Nhập Excel hợp đồng nguyên tắc hiện hữu (lát B4c – design.md mục 18.7, đề xuất Ban CUVT Bước 8: chuyển giao hợp đồng đang thực hiện).
/// Mỗi dòng file là một dòng sản lượng; các dòng cùng số HĐ giấy là một hợp đồng. Hợp đồng nhập vào công ty đang làm việc ở trạng thái
/// Nháp, qua đúng quy tắc lập tay, rồi gửi duyệt như thường. Cần quyền <c>Purchase.Contract.Manage</c>.
/// </summary>
public interface IContractImportService
{
    /// <summary>File mẫu trống kèm sheet Hướng dẫn.</summary>
    /// <returns>File .xlsx.</returns>
    /// <exception cref="Common.ForbiddenAccessException">Khi không có quyền lập hợp đồng.</exception>
    Excel.ExcelFile GetTemplate();

    /// <summary>Nhập file: kiểm tra thử (không lưu) hoặc nhập thật (chỉ lưu khi mọi dòng đúng).</summary>
    /// <param name="content">Nội dung file .xlsx.</param>
    /// <param name="commit">false = kiểm tra thử; true = lưu nếu không có lỗi.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Số hợp đồng, số dòng và các lỗi theo dòng Excel.</returns>
    /// <exception cref="Common.ForbiddenAccessException">Khi không có quyền lập hợp đồng.</exception>
    Task<ContractImportResult> ImportAsync(Stream content, bool commit, CancellationToken cancellationToken);
}

/// <summary>Kiểm tra dữ liệu lưu phụ lục hợp đồng.</summary>
public sealed class SaveContractAmendmentRequestValidator : AbstractValidator<SaveContractAmendmentRequest>
{
    /// <summary>Khai báo quy tắc.</summary>
    public SaveContractAmendmentRequestValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Phụ lục phải ghi lý do.").MaximumLength(500);
        RuleFor(x => x.NewPriceListId).NotEmpty().WithMessage("Hãy chọn bảng giá dùng chung sau phụ lục.");
        RuleFor(x => x.PaperNo).MaximumLength(100);
        RuleFor(x => x.PriceAdjustmentTerms).MaximumLength(2000);
        RuleFor(x => x.PaymentTerms).MaximumLength(2000);
        RuleFor(x => x.DeliveryTerms).MaximumLength(2000);
        RuleFor(x => x.DiscountTerms).MaximumLength(2000);
        RuleFor(x => x.SponsorshipTerms).MaximumLength(2000);
        RuleFor(x => x.PenaltyTerms).MaximumLength(2000);
        RuleFor(x => x.OtherTerms).MaximumLength(4000);
        RuleFor(x => x.Note).MaximumLength(1000);
    }
}

/// <summary>Kiểm tra dữ liệu lưu hợp đồng.</summary>
public sealed class SaveContractRequestValidator : AbstractValidator<SaveContractRequest>
{
    /// <summary>Khai báo quy tắc.</summary>
    public SaveContractRequestValidator()
    {
        RuleFor(x => x.SupplierId).NotEmpty().WithMessage("Hãy chọn nhà cung cấp.");
        RuleFor(x => x.PriceListId).NotEmpty().WithMessage("Hãy chọn bảng giá NCC dùng chung.");
        RuleFor(x => x.Title).NotEmpty().WithMessage("Hãy nhập tên hợp đồng.").MaximumLength(200);
        RuleFor(x => x.ContractNo).MaximumLength(100);
        RuleFor(x => x.PriceAdjustmentTerms).MaximumLength(2000);
        RuleFor(x => x.PaymentTerms).MaximumLength(2000);
        RuleFor(x => x.DeliveryTerms).MaximumLength(2000);
        RuleFor(x => x.DiscountTerms).MaximumLength(2000);
        RuleFor(x => x.SponsorshipTerms).MaximumLength(2000);
        RuleFor(x => x.PenaltyTerms).MaximumLength(2000);
        RuleFor(x => x.OtherTerms).MaximumLength(4000);
        RuleFor(x => x.Note).MaximumLength(1000);
        RuleFor(x => x.DepartmentIds).NotNull();
        RuleFor(x => x.Lines).NotNull();
        RuleForEach(x => x.Lines).ChildRules(l =>
        {
            l.RuleFor(x => x.ItemId).NotEmpty().WithMessage("Hãy chọn mặt hàng.");
            l.RuleFor(x => x.UomId).NotEmpty().WithMessage("Hãy chọn ĐVT.");
            l.RuleFor(x => x.Note).MaximumLength(500);
        });
        // Rebates null = giữ nguyên cam kết đang có (nhập Excel hợp đồng không có cam kết).
        RuleForEach(x => x.Rebates).ChildRules(r =>
        {
            r.RuleFor(x => x.Description).NotEmpty().WithMessage("Hãy ghi nội dung cam kết chiết khấu/tài trợ.").MaximumLength(500);
        });
    }
}

/// <summary>Kiểm tra dữ liệu ghi nhận thực nhận.</summary>
public sealed class SaveContractRebateReceiptRequestValidator : AbstractValidator<SaveContractRebateReceiptRequest>
{
    /// <summary>Khai báo quy tắc.</summary>
    public SaveContractRebateReceiptRequestValidator()
    {
        RuleFor(x => x.RebateId).NotEmpty().WithMessage("Hãy chọn cam kết chiết khấu/tài trợ.");
        RuleFor(x => x.Amount).GreaterThan(0).WithMessage("Số tiền thực nhận phải lớn hơn 0.");
        RuleFor(x => x.RefNo).MaximumLength(100);
        RuleFor(x => x.Note).MaximumLength(500);
    }
}
