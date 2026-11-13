using FluentValidation;
using Proc.Domain.Purchasing;

namespace Proc.Application.Purchasing;

/// <summary>Đề nghị thanh toán trên danh sách (lát Y8, design.md mục 19.3 bước 7).</summary>
/// <param name="Id">Id.</param>
/// <param name="No">Số DNT.</param>
/// <param name="Date">Ngày đề nghị.</param>
/// <param name="Form">5A / 5B.</param>
/// <param name="Status">Trạng thái duyệt (Draft, Submitted, Approved, Cancelled).</param>
/// <param name="IsPaid">Kế toán đã ghi Đã chi.</param>
/// <param name="PaidDate">Ngày chi.</param>
/// <param name="PurchaseOrderId">PO.</param>
/// <param name="PurchaseOrderNo">Số PO.</param>
/// <param name="SupplierCode">Mã NCC.</param>
/// <param name="SupplierName">Tên NCC.</param>
/// <param name="Method">Cash / BankTransfer.</param>
/// <param name="TotalAmount">Tổng chứng từ.</param>
/// <param name="DeductionAmount">Giảm trừ.</param>
/// <param name="PayableAmount">Số tiền đề nghị.</param>
/// <param name="CreatedByName">Người lập.</param>
public sealed record PaymentRequestListItemDto(
    Guid Id, string No, DateOnly Date, string Form, string Status, bool IsPaid, DateOnly? PaidDate, Guid PurchaseOrderId, string PurchaseOrderNo,
    string SupplierCode, string SupplierName, string Method, decimal TotalAmount, decimal DeductionAmount, decimal PayableAmount, string? CreatedByName);

/// <summary>Dòng đề nghị thanh toán – một XNH có hóa đơn.</summary>
/// <param name="Id">Id dòng.</param>
/// <param name="LineNo">STT.</param>
/// <param name="ReceiptConfirmationId">XNH.</param>
/// <param name="ReceiptNo">Số XNH.</param>
/// <param name="ReceiptDate">Ngày nhận.</param>
/// <param name="InvoiceNo">Số hóa đơn.</param>
/// <param name="InvoiceSeries">Ký hiệu.</param>
/// <param name="InvoiceDate">Ngày hóa đơn.</param>
/// <param name="Amount">Số tiền (tiền hóa đơn gồm VAT).</param>
/// <param name="Description">Nội dung thanh toán.</param>
public sealed record PaymentRequestLineDto(
    Guid Id, int LineNo, Guid ReceiptConfirmationId, string ReceiptNo, DateOnly? ReceiptDate, string InvoiceNo, string? InvoiceSeries,
    DateOnly? InvoiceDate, decimal Amount, string Description);

/// <summary>Chi tiết đề nghị thanh toán.</summary>
/// <param name="Id">Id.</param>
/// <param name="No">Số DNT.</param>
/// <param name="Date">Ngày đề nghị.</param>
/// <param name="Form">5A / 5B.</param>
/// <param name="Status">Trạng thái duyệt.</param>
/// <param name="PurchaseOrderId">PO.</param>
/// <param name="PurchaseOrderNo">Số PO.</param>
/// <param name="PurchaseOrderDate">Ngày PO.</param>
/// <param name="SupplierId">NCC.</param>
/// <param name="SupplierCode">Mã NCC.</param>
/// <param name="SupplierName">Tên NCC.</param>
/// <param name="SupplierAddress">Địa chỉ NCC.</param>
/// <param name="DepartmentName">Bộ phận đặt hàng của PO.</param>
/// <param name="ConstructionProjectCode">Mã công trình (5A).</param>
/// <param name="ConstructionProjectName">Tên công trình (5A).</param>
/// <param name="Method">Cash / BankTransfer.</param>
/// <param name="BankAccountId">Tài khoản NCC đã chọn.</param>
/// <param name="BankName">Ngân hàng (chụp lúc lưu).</param>
/// <param name="BankBranch">Chi nhánh.</param>
/// <param name="BankAccountNumber">Số tài khoản.</param>
/// <param name="BankAccountName">Chủ tài khoản.</param>
/// <param name="TotalAmount">Tổng chứng từ.</param>
/// <param name="DeductionAmount">Giảm trừ.</param>
/// <param name="DeductionReason">Lý do giảm trừ.</param>
/// <param name="PayableAmount">Số tiền đề nghị.</param>
/// <param name="Note">Ghi chú.</param>
/// <param name="PaidDate">Ngày chi.</param>
/// <param name="PaidByName">Người ghi Đã chi.</param>
/// <param name="PaidRecordedAt">Lúc ghi Đã chi (UTC).</param>
/// <param name="PaymentNote">Ghi chú khi chi.</param>
/// <param name="CanPay">Người dùng ghi Đã chi được (quyền, phiếu đã duyệt, chưa chi).</param>
/// <param name="RejectReason">Lý do từ chối gần nhất.</param>
/// <param name="CancelReason">Lý do hủy.</param>
/// <param name="CreatedAt">Lúc lập (UTC).</param>
/// <param name="CreatedByName">Người lập.</param>
/// <param name="ApprovedAt">Lúc duyệt xong (UTC).</param>
/// <param name="Lines">Dòng.</param>
/// <param name="Approvals">Lịch sử duyệt.</param>
/// <param name="Actions">Lệnh được phép.</param>
/// <param name="Version">Phiên bản dòng (chống ghi đè).</param>
public sealed record PaymentRequestDto(
    Guid Id, string No, DateOnly Date, string Form, string Status, Guid PurchaseOrderId, string PurchaseOrderNo, DateOnly PurchaseOrderDate,
    Guid SupplierId, string SupplierCode, string SupplierName, string? SupplierAddress, string? DepartmentName, string? ConstructionProjectCode,
    string? ConstructionProjectName, string Method, Guid? BankAccountId, string? BankName, string? BankBranch, string? BankAccountNumber,
    string? BankAccountName, decimal TotalAmount, decimal DeductionAmount, string? DeductionReason, decimal PayableAmount, string? Note,
    DateOnly? PaidDate, string? PaidByName, DateTimeOffset? PaidRecordedAt, string? PaymentNote, bool CanPay, string? RejectReason,
    string? CancelReason, DateTimeOffset CreatedAt, string? CreatedByName, DateTimeOffset? ApprovedAt, IReadOnlyList<PaymentRequestLineDto> Lines,
    IReadOnlyList<ApprovalStepDto> Approvals, PurchaseDocumentActions Actions, uint Version);

/// <summary>PO lập được đề nghị thanh toán: đã duyệt / đã đóng, còn XNH có hóa đơn chưa đề nghị.</summary>
/// <param name="Id">PO.</param>
/// <param name="No">Số PO.</param>
/// <param name="Date">Ngày PO.</param>
/// <param name="SupplierCode">Mã NCC.</param>
/// <param name="SupplierName">Tên NCC.</param>
/// <param name="OpenReceiptCount">Số XNH có hóa đơn chưa đề nghị.</param>
/// <param name="OpenAmount">Tổng tiền hóa đơn các XNH đó.</param>
public sealed record PaymentOrderOptionDto(Guid Id, string No, DateOnly Date, string SupplierCode, string SupplierName, int OpenReceiptCount, decimal OpenAmount);

/// <summary>Tài khoản ngân hàng NCC chọn được khi chuyển khoản.</summary>
/// <param name="Id">Id.</param>
/// <param name="BankName">Ngân hàng.</param>
/// <param name="Branch">Chi nhánh.</param>
/// <param name="AccountNumber">Số tài khoản.</param>
/// <param name="AccountName">Chủ tài khoản.</param>
/// <param name="IsDefault">Tài khoản mặc định.</param>
public sealed record PaymentBankAccountDto(Guid Id, string BankName, string? Branch, string AccountNumber, string? AccountName, bool IsDefault);

/// <summary>XNH đã xác nhận của PO – chọn vào đề nghị khi có hóa đơn và chưa thuộc đề nghị khác.</summary>
/// <param name="Id">XNH.</param>
/// <param name="No">Số XNH.</param>
/// <param name="Date">Ngày nhận.</param>
/// <param name="DeliveryNoteNo">Số phiếu giao.</param>
/// <param name="InvoiceNo">Số hóa đơn (trống = chưa cập nhật hóa đơn).</param>
/// <param name="InvoiceSeries">Ký hiệu.</param>
/// <param name="InvoiceDate">Ngày hóa đơn.</param>
/// <param name="InvoiceAmount">Tiền hóa đơn gồm VAT.</param>
/// <param name="ReceivedAmount">Tiền nhận (tiền hàng + VAT theo SL thực nhận) – để đối chiếu.</param>
/// <param name="HeldBy">Số đề nghị thanh toán chưa hủy đang giữ XNH (khác phiếu đang sửa).</param>
public sealed record PaymentReceiptOptionDto(
    Guid Id, string No, DateOnly Date, string? DeliveryNoteNo, string? InvoiceNo, string? InvoiceSeries, DateOnly? InvoiceDate, decimal? InvoiceAmount,
    decimal ReceivedAmount, string? HeldBy);

/// <summary>Dữ liệu chọn khi lập đề nghị cho một PO: biểu mẫu, NCC, tài khoản, XNH.</summary>
/// <param name="PurchaseOrderId">PO.</param>
/// <param name="PurchaseOrderNo">Số PO.</param>
/// <param name="Form">5A / 5B theo loại mua của PO.</param>
/// <param name="SupplierCode">Mã NCC.</param>
/// <param name="SupplierName">Tên NCC.</param>
/// <param name="DepartmentName">Bộ phận đặt hàng.</param>
/// <param name="ConstructionProjectCode">Công trình (5A).</param>
/// <param name="BankAccounts">Tài khoản ngân hàng của NCC.</param>
/// <param name="Receipts">XNH đã xác nhận của PO.</param>
public sealed record PaymentRequestSourceDto(
    Guid PurchaseOrderId, string PurchaseOrderNo, string Form, string SupplierCode, string SupplierName, string? DepartmentName,
    string? ConstructionProjectCode, IReadOnlyList<PaymentBankAccountDto> BankAccounts, IReadOnlyList<PaymentReceiptOptionDto> Receipts);

/// <summary>Dòng gửi lên khi lưu.</summary>
/// <param name="ReceiptConfirmationId">XNH.</param>
/// <param name="Description">Nội dung thanh toán (trống = tự ghi).</param>
public sealed record SavePaymentRequestLineRequest(Guid ReceiptConfirmationId, string? Description);

/// <summary>Lập / sửa đề nghị thanh toán (nháp).</summary>
/// <param name="Date">Ngày đề nghị.</param>
/// <param name="PurchaseOrderId">PO (không đổi được sau khi lập).</param>
/// <param name="Method">Cash / BankTransfer.</param>
/// <param name="BankAccountId">Tài khoản NCC (chuyển khoản).</param>
/// <param name="DeductionAmount">Giảm trừ.</param>
/// <param name="DeductionReason">Lý do giảm trừ.</param>
/// <param name="Note">Ghi chú.</param>
/// <param name="Lines">Dòng.</param>
public sealed record SavePaymentRequestRequest(
    DateOnly Date, Guid PurchaseOrderId, string Method, Guid? BankAccountId, decimal DeductionAmount, string? DeductionReason, string? Note,
    IReadOnlyList<SavePaymentRequestLineRequest> Lines);

/// <summary>Kế toán ghi Đã chi.</summary>
/// <param name="PaidDate">Ngày chi.</param>
/// <param name="Note">Số ủy nhiệm chi, phiếu chi…</param>
public sealed record MarkPaymentPaidRequest(DateOnly PaidDate, string? Note);

/// <summary>Bộ lọc danh sách: khoảng ngày đề nghị.</summary>
/// <param name="From">Từ ngày.</param>
/// <param name="To">Đến ngày.</param>
public sealed record PaymentRequestFilter(DateOnly? From, DateOnly? To);

/// <summary>
/// Đề nghị thanh toán 5A/5B của công ty đang làm việc (lát Y8, design.md mục 19.3 bước 7): lập cho một PO, dòng là XNH có hóa đơn, duyệt theo bộ
/// cấp DNT, kế toán ghi Đã chi. Phạm vi công ty đi qua <see cref="Common.ICurrentUserScope"/>.
/// </summary>
public interface IPaymentRequestService
{
    /// <summary>Danh sách đề nghị, mới nhất trước.</summary>
    /// <param name="filter">Bộ lọc.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Tối đa 2000 phiếu.</returns>
    Task<IReadOnlyList<PaymentRequestListItemDto>> ListAsync(PaymentRequestFilter filter, CancellationToken cancellationToken);

    /// <summary>Chi tiết đề nghị.</summary>
    /// <param name="id">Id.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Phiếu.</returns>
    Task<PaymentRequestDto> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>PO lập được đề nghị (còn XNH có hóa đơn chưa đề nghị).</summary>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Theo ngày PO giảm dần.</returns>
    Task<IReadOnlyList<PaymentOrderOptionDto>> OrdersAsync(CancellationToken cancellationToken);

    /// <summary>Dữ liệu chọn cho một PO (tài khoản NCC, XNH).</summary>
    /// <param name="purchaseOrderId">PO.</param>
    /// <param name="requestId">Đề nghị đang sửa (XNH của chính nó không tính là đang giữ).</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Dữ liệu chọn.</returns>
    Task<PaymentRequestSourceDto> SourceAsync(Guid purchaseOrderId, Guid? requestId, CancellationToken cancellationToken);

    /// <summary>Lập đề nghị nháp.</summary>
    /// <param name="request">Dữ liệu.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Phiếu vừa lập.</returns>
    Task<PaymentRequestDto> CreateAsync(SavePaymentRequestRequest request, CancellationToken cancellationToken);

    /// <summary>Sửa đề nghị nháp.</summary>
    /// <param name="id">Id.</param>
    /// <param name="request">Dữ liệu.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Phiếu sau khi sửa.</returns>
    Task<PaymentRequestDto> UpdateAsync(Guid id, SavePaymentRequestRequest request, CancellationToken cancellationToken);

    /// <summary>Gửi duyệt theo bộ cấp DNT.</summary>
    /// <param name="id">Id.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Phiếu sau khi gửi.</returns>
    Task<PaymentRequestDto> SubmitAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Duyệt cấp đang chờ.</summary>
    /// <param name="id">Id.</param>
    /// <param name="request">Ý kiến.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Phiếu sau khi duyệt.</returns>
    Task<PaymentRequestDto> ApproveAsync(Guid id, ApprovalActionRequest request, CancellationToken cancellationToken);

    /// <summary>Từ chối: về nháp kèm lý do.</summary>
    /// <param name="id">Id.</param>
    /// <param name="request">Ý kiến (bắt buộc).</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Phiếu sau khi từ chối.</returns>
    Task<PaymentRequestDto> RejectAsync(Guid id, ApprovalActionRequest request, CancellationToken cancellationToken);

    /// <summary>Rút về nháp – người lập (hoặc quản trị).</summary>
    /// <param name="id">Id.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Phiếu về nháp.</returns>
    Task<PaymentRequestDto> RecallAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Hủy (design.md mục 20.2a): nháp – người lập; đã gửi trở đi – quản trị; đã chi thì không hủy.</summary>
    /// <param name="id">Id.</param>
    /// <param name="request">Lý do.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Phiếu đã hủy.</returns>
    Task<PaymentRequestDto> CancelAsync(Guid id, CancelDocumentRequest request, CancellationToken cancellationToken);

    /// <summary>Kế toán ghi Đã chi (quyền <c>Purchase.Payment.Pay</c>).</summary>
    /// <param name="id">Id.</param>
    /// <param name="request">Ngày chi, ghi chú.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Phiếu sau khi ghi.</returns>
    Task<PaymentRequestDto> MarkPaidAsync(Guid id, MarkPaymentPaidRequest request, CancellationToken cancellationToken);
}

/// <summary>Kiểm tra lập / sửa đề nghị thanh toán. Quy tắc nghiệp vụ do Domain và dịch vụ kiểm tra.</summary>
public sealed class SavePaymentRequestRequestValidator : AbstractValidator<SavePaymentRequestRequest>
{
    /// <summary>Khai báo quy tắc.</summary>
    public SavePaymentRequestRequestValidator()
    {
        RuleFor(x => x.PurchaseOrderId).NotEmpty().WithMessage("Hãy chọn đơn mua hàng.");
        RuleFor(x => x.Method).NotEmpty().WithMessage("Hãy chọn hình thức thanh toán.");
        RuleFor(x => x.Note).MaximumLength(PaymentRequest.NoteMaxLength);
        RuleFor(x => x.DeductionReason).MaximumLength(PaymentRequest.NoteMaxLength);
        RuleFor(x => x.Lines).NotNull();
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.ReceiptConfirmationId).NotEmpty().WithMessage("Có dòng chưa chọn phiếu nhận hàng.");
            line.RuleFor(l => l.Description).MaximumLength(PaymentRequest.DescriptionMaxLength);
        });
    }
}
