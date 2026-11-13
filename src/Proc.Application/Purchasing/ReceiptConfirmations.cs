using FluentValidation;
using Proc.Domain.Purchasing;

namespace Proc.Application.Purchasing;

/// <summary>Lọc danh sách XNH.</summary>
/// <param name="From">Từ ngày nhận (mặc định 90 ngày trước <paramref name="To"/>).</param>
/// <param name="To">Đến ngày nhận (mặc định hôm nay).</param>
/// <param name="Status">Trạng thái (Draft, Confirmed, Cancelled); trống = mọi trạng thái.</param>
/// <param name="PurchaseOrderId">Chỉ phiếu của một PO.</param>
public sealed record ReceiptConfirmationQuery(DateOnly? From, DateOnly? To, string? Status, Guid? PurchaseOrderId);

/// <summary>Một dòng danh sách XNH.</summary>
public sealed record ReceiptConfirmationListItemDto(
    Guid Id, string No, DateOnly Date, string Status, Guid PurchaseOrderId, string PurchaseOrderNo, string SupplierCode, string SupplierName,
    string? WarehouseCode, string? DeliveryNoteNo, string? InvoiceNo, decimal TotalWithVat, string Acceptance, string? CreatedByName);

/// <summary>Kết quả danh sách XNH; Truncated = còn phiếu ngoài giới hạn, cần lọc hẹp hơn.</summary>
public sealed record ReceiptConfirmationListResult(IReadOnlyList<ReceiptConfirmationListItemDto> Items, bool Truncated, int Limit);

/// <summary>
/// Dòng XNH. OrderedQty: SL đặt của dòng PO; OtherReceivedQty: SL thực nhận ở các XNH khác đã xác nhận (theo ĐVT dòng);
/// RemainingQty = SL đặt − nhận ở phiếu khác − nhận ở phiếu này (không âm).
/// </summary>
public sealed record ReceiptConfirmationLineDto(
    Guid Id, int LineNo, Guid PurchaseOrderLineId, int PoLineNo, Guid ItemId, string ItemCode, string ItemName, string UomCode,
    decimal OrderedQty, decimal OtherReceivedQty, decimal RemainingQty, decimal DeliveredQty, decimal AcceptedQty, decimal RejectedQty,
    decimal AdjustedQty, decimal ReceivedQty, decimal UnitPrice, string? VatCode, decimal Amount, decimal VatAmount, string CheckResult,
    string? CheckNote, string? Note);

/// <summary>Một lần điều chỉnh SL thực nhận.</summary>
public sealed record ReceiptAdjustmentDto(Guid Id, int LineNo, decimal Qty, string Reason, DateTimeOffset AdjustedAt, string? AdjustedByName);

/// <summary>Thao tác người xem được làm trên XNH (giao diện bật/tắt nút; máy chủ vẫn kiểm tra lại).</summary>
public sealed record ReceiptConfirmationActions(
    bool CanEdit, bool CanConfirm, bool CanCancel, bool CanAdjust, bool CanEditInvoice, bool CanAccept, string? ReadOnlyReason);

/// <summary>Chi tiết XNH. DeliveryNoticeId / DeliveryNoticeNo: phiếu báo giao NCC mà XNH lập theo (lát P5); null = lập thẳng theo PO.</summary>
public sealed record ReceiptConfirmationDto(
    Guid Id, string No, DateOnly Date, string Status, Guid PurchaseOrderId, string PurchaseOrderNo, DateOnly PurchaseOrderDate,
    Guid SupplierId, string SupplierCode, string SupplierName, Guid WarehouseId, string? WarehouseCode, string? WarehouseName,
    string? DepartmentName, string? DeliveryNoteNo, string? Deliverer, string? InvoiceNo, string? InvoiceSeries, DateOnly? InvoiceDate,
    decimal? InvoiceAmount, string? Note, string? CancelReason, decimal TotalAmount, decimal TotalVat, DateTimeOffset CreatedAt,
    string? CreatedByName, DateTimeOffset? ConfirmedAt, string? ConfirmedByName, string Acceptance, string? AcceptanceNote,
    DateTimeOffset? AcceptedAt, string? AcceptedByName, IReadOnlyList<ReceiptConfirmationLineDto> Lines,
    IReadOnlyList<ReceiptAdjustmentDto> Adjustments, ReceiptConfirmationActions Actions, uint Version = 0, Guid? DeliveryNoticeId = null,
    string? DeliveryNoticeNo = null);

/// <summary>Dòng gửi lên khi lưu XNH (SL theo ĐVT dòng PO).</summary>
public sealed record SaveReceiptConfirmationLineRequest(
    Guid PurchaseOrderLineId, decimal DeliveredQty, decimal AcceptedQty, decimal RejectedQty, string? CheckResult, string? CheckNote, string? Note);

/// <summary>
/// Lập/sửa XNH nháp. PO và phiếu báo giao NCC (DeliveryNoticeId – lát P5) chỉ chọn khi lập; sửa thì bỏ qua (phiếu gắn cố định với PO, báo giao).
/// </summary>
public sealed record SaveReceiptConfirmationRequest(
    Guid PurchaseOrderId, DateOnly Date, string? DeliveryNoteNo, string? Deliverer, string? Note, string? InvoiceNo, string? InvoiceSeries,
    DateOnly? InvoiceDate, decimal? InvoiceAmount, IReadOnlyList<SaveReceiptConfirmationLineRequest> Lines, Guid? DeliveryNoticeId = null);

/// <summary>Cập nhật hóa đơn NCC trên XNH (cả khi đã xác nhận).</summary>
public sealed record UpdateReceiptConfirmationInvoiceRequest(string? InvoiceNo, string? InvoiceSeries, DateOnly? InvoiceDate, decimal? InvoiceAmount);

/// <summary>Nghiệm thu của đơn vị đặt hàng.</summary>
public sealed record ReceiptAcceptanceRequest(bool Accepted, string? Note);

/// <summary>Điều chỉnh SL thực nhận một dòng (âm = trả NCC / nhận nhầm).</summary>
public sealed record AdjustReceiptRequest(Guid LineId, decimal Qty, string? Reason);

/// <summary>Hủy XNH nháp.</summary>
public sealed record CancelReceiptConfirmationRequest(string? Reason);

/// <summary>
/// Phiếu xác nhận nhận hàng XNH (lát K2, design.md mục 19.4): thay phiếu nhập kho từ bản 0.22.0. SL thực nhận của XNH đã xác nhận là nguồn
/// "SL đã nhận" của PO; nhận đủ mọi dòng thì PO tự chuyển Đã nhận đủ.
/// </summary>
public interface IReceiptConfirmationService
{
    /// <summary>Danh sách XNH của công ty đang làm việc.</summary>
    /// <param name="query">Bộ lọc.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Các phiếu, mới nhất trước.</returns>
    /// <exception cref="Proc.Domain.Common.BusinessRuleException">Khi trạng thái lọc không hợp lệ.</exception>
    Task<ReceiptConfirmationListResult> SearchAsync(ReceiptConfirmationQuery query, CancellationToken cancellationToken);

    /// <summary>Chi tiết XNH.</summary>
    /// <param name="id">Id phiếu.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Phiếu.</returns>
    /// <exception cref="Proc.Application.Common.NotFoundException">Khi không có hoặc ngoài phạm vi.</exception>
    Task<ReceiptConfirmationDto> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Lập XNH nháp theo PO đã duyệt (nơi nhận trong phạm vi kho của người dùng).</summary>
    /// <param name="request">Nội dung phiếu.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Phiếu đã lưu.</returns>
    /// <exception cref="Proc.Domain.Common.BusinessRuleException">Khi PO không nhận được, vượt SL còn nhận…</exception>
    Task<ReceiptConfirmationDto> CreateAsync(SaveReceiptConfirmationRequest request, CancellationToken cancellationToken);

    /// <summary>Sửa XNH nháp.</summary>
    /// <param name="id">Id phiếu.</param>
    /// <param name="request">Nội dung mới.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Phiếu đã lưu.</returns>
    Task<ReceiptConfirmationDto> UpdateAsync(Guid id, SaveReceiptConfirmationRequest request, CancellationToken cancellationToken);

    /// <summary>Xác nhận nhận hàng: kiểm tra lại SL còn nhận của PO, tính vào SL đã nhận; nhận đủ thì PO Đã nhận đủ.</summary>
    /// <param name="id">Id phiếu.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Phiếu đã xác nhận.</returns>
    Task<ReceiptConfirmationDto> ConfirmAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Hủy XNH nháp.</summary>
    /// <param name="id">Id phiếu.</param>
    /// <param name="request">Lý do.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Phiếu đã hủy.</returns>
    Task<ReceiptConfirmationDto> CancelAsync(Guid id, CancelReceiptConfirmationRequest request, CancellationToken cancellationToken);

    /// <summary>Cập nhật hóa đơn NCC (cả khi đã xác nhận).</summary>
    /// <param name="id">Id phiếu.</param>
    /// <param name="request">Hóa đơn.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Phiếu sau cập nhật.</returns>
    Task<ReceiptConfirmationDto> UpdateInvoiceAsync(Guid id, UpdateReceiptConfirmationInvoiceRequest request, CancellationToken cancellationToken);

    /// <summary>Đơn vị đặt hàng xác nhận nghiệm thu.</summary>
    /// <param name="id">Id phiếu.</param>
    /// <param name="request">Đạt/không đạt, ý kiến.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Phiếu sau nghiệm thu.</returns>
    Task<ReceiptConfirmationDto> ConfirmAcceptanceAsync(Guid id, ReceiptAcceptanceRequest request, CancellationToken cancellationToken);

    /// <summary>Điều chỉnh SL thực nhận một dòng đã xác nhận (có lý do); PO mở lại hoặc đóng theo SL mới.</summary>
    /// <param name="id">Id phiếu.</param>
    /// <param name="request">Dòng, SL, lý do.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Phiếu sau điều chỉnh.</returns>
    Task<ReceiptConfirmationDto> AdjustAsync(Guid id, AdjustReceiptRequest request, CancellationToken cancellationToken);
}

/// <summary>Kiểm tra lập/sửa XNH.</summary>
public sealed class SaveReceiptConfirmationRequestValidator : AbstractValidator<SaveReceiptConfirmationRequest>
{
    /// <summary>Khai báo các quy tắc.</summary>
    public SaveReceiptConfirmationRequestValidator()
    {
        RuleFor(x => x.PurchaseOrderId).NotEmpty().WithMessage("Hãy chọn đơn mua hàng.");
        RuleFor(x => x.DeliveryNoteNo).MaximumLength(100);
        RuleFor(x => x.Deliverer).MaximumLength(200);
        RuleFor(x => x.Note).MaximumLength(1000);
        RuleFor(x => x.InvoiceNo).MaximumLength(50);
        RuleFor(x => x.InvoiceSeries).MaximumLength(50);
        RuleFor(x => x.InvoiceAmount).GreaterThanOrEqualTo(0).WithMessage("Tiền hóa đơn không được âm.");
        RuleFor(x => x.Lines).NotEmpty().WithMessage("Phiếu phải có ít nhất một dòng.");
        RuleForEach(x => x.Lines).ChildRules(l =>
        {
            l.RuleFor(x => x.PurchaseOrderLineId).NotEmpty().WithMessage("Dòng phải theo một dòng của đơn mua hàng.");
            l.RuleFor(x => x.DeliveredQty).GreaterThanOrEqualTo(0).WithMessage("SL không được âm.");
            l.RuleFor(x => x.AcceptedQty).GreaterThanOrEqualTo(0).WithMessage("SL không được âm.");
            l.RuleFor(x => x.RejectedQty).GreaterThanOrEqualTo(0).WithMessage("SL không được âm.");
            l.RuleFor(x => x.CheckResult).Must(r => r is null || (Enum.TryParse<ReceiptCheckResult>(r, out var v) && Enum.IsDefined(v)))
                .WithMessage("Kết quả kiểm tra không hợp lệ.");
            l.RuleFor(x => x.CheckNote).MaximumLength(500);
            l.RuleFor(x => x.Note).MaximumLength(500);
        });
    }
}

/// <summary>Kiểm tra cập nhật hóa đơn.</summary>
public sealed class UpdateReceiptInvoiceRequestValidator : AbstractValidator<UpdateReceiptConfirmationInvoiceRequest>
{
    /// <summary>Khai báo các quy tắc.</summary>
    public UpdateReceiptInvoiceRequestValidator()
    {
        RuleFor(x => x.InvoiceNo).MaximumLength(50);
        RuleFor(x => x.InvoiceSeries).MaximumLength(50);
        RuleFor(x => x.InvoiceAmount).GreaterThanOrEqualTo(0).WithMessage("Tiền hóa đơn không được âm.");
    }
}

/// <summary>Kiểm tra nghiệm thu.</summary>
public sealed class ReceiptAcceptanceRequestValidator : AbstractValidator<ReceiptAcceptanceRequest>
{
    /// <summary>Khai báo các quy tắc.</summary>
    public ReceiptAcceptanceRequestValidator()
    {
        RuleFor(x => x.Note).NotEmpty().When(x => !x.Accepted).WithMessage("Không đạt nghiệm thu phải ghi ý kiến.")
            .MaximumLength(GoodsReceiptConfirmation.MaxReasonLength);
    }
}

/// <summary>Kiểm tra điều chỉnh SL thực nhận.</summary>
public sealed class AdjustReceiptRequestValidator : AbstractValidator<AdjustReceiptRequest>
{
    /// <summary>Khai báo các quy tắc.</summary>
    public AdjustReceiptRequestValidator()
    {
        RuleFor(x => x.LineId).NotEmpty().WithMessage("Hãy chọn dòng cần điều chỉnh.");
        RuleFor(x => x.Qty).NotEqual(0).WithMessage("SL điều chỉnh phải khác 0.");
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Hãy nhập lý do điều chỉnh.").MaximumLength(GoodsReceiptConfirmation.MaxReasonLength);
    }
}

/// <summary>Kiểm tra hủy XNH.</summary>
public sealed class CancelReceiptConfirmationRequestValidator : AbstractValidator<CancelReceiptConfirmationRequest>
{
    /// <summary>Khai báo các quy tắc.</summary>
    public CancelReceiptConfirmationRequestValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Hãy nhập lý do hủy.").MaximumLength(GoodsReceiptConfirmation.MaxReasonLength);
    }
}
