namespace Proc.Application.Portal;

/// <summary>
/// Tình hình thanh toán một đơn NCC thấy trên Portal (lát P6b – design.md 19.5, P6b-1, P6b-4). Tiền VND gồm VAT. Status: "NoInvoice" chưa
/// có hóa đơn, "Unpaid" chưa thanh toán, "PartlyPaid" thanh toán một phần, "Paid" đã thanh toán đủ hóa đơn (như đèn TTPO – Y9-3).
/// </summary>
/// <param name="OrderId">PO.</param>
/// <param name="OrderNo">Số PO.</param>
/// <param name="CompanyName">Công ty đặt hàng.</param>
/// <param name="OrderDate">Ngày đặt hàng.</param>
/// <param name="OrderAmount">Giá trị đơn (sau chiết khấu, có VAT).</param>
/// <param name="ReceivedAmount">Giá trị hàng bên đặt đã xác nhận nhận (XNH đã xác nhận).</param>
/// <param name="ReceiptCount">Số phiếu nhận hàng đã xác nhận.</param>
/// <param name="AwaitingInvoiceCount">Số phiếu nhận hàng chưa có hóa đơn.</param>
/// <param name="InvoiceAmount">Tổng tiền hóa đơn bên đặt đã ghi nhận.</param>
/// <param name="ProcessingAmount">Hóa đơn đang trong đề nghị thanh toán chưa duyệt xong (đang lập, chờ duyệt).</param>
/// <param name="ApprovedUnpaidAmount">Số tiền các đợt thanh toán đã duyệt, chờ chi.</param>
/// <param name="PaidAmount">Đã thanh toán.</param>
/// <param name="DeductionAmount">Giảm trừ đã cấn trừ (của đợt đã thanh toán).</param>
/// <param name="OutstandingAmount">Còn phải trả = tiền hóa đơn − đã thanh toán − giảm trừ đã cấn trừ.</param>
/// <param name="LastPaidDate">Ngày thanh toán gần nhất.</param>
/// <param name="Status">Trạng thái thanh toán.</param>
public sealed record PortalPaymentOrderDto(
    Guid OrderId, string OrderNo, string CompanyName, DateOnly OrderDate, decimal OrderAmount, decimal ReceivedAmount, int ReceiptCount,
    int AwaitingInvoiceCount, decimal InvoiceAmount, decimal ProcessingAmount, decimal ApprovedUnpaidAmount, decimal PaidAmount,
    decimal DeductionAmount, decimal OutstandingAmount, DateOnly? LastPaidDate, string Status);

/// <summary>
/// Một phiếu nhận hàng đã xác nhận và hóa đơn của nó (P6b-2). Status: "AwaitingInvoice" chờ hóa đơn, "Invoiced" đã nhận hóa đơn – chưa lập
/// đề nghị, "Processing" đang xử lý thanh toán (đề nghị đang lập / chờ duyệt), "Approved" đã duyệt – chờ chi, "Paid" đã thanh toán.
/// </summary>
/// <param name="ReceiptNo">Số XNH.</param>
/// <param name="ReceivedDate">Ngày nhận hàng.</param>
/// <param name="ReceivedAmount">Giá trị hàng nhận (có VAT).</param>
/// <param name="DeliveryNoteNo">Số phiếu giao của NCC.</param>
/// <param name="InvoiceNo">Số hóa đơn.</param>
/// <param name="InvoiceSeries">Ký hiệu hóa đơn.</param>
/// <param name="InvoiceDate">Ngày hóa đơn.</param>
/// <param name="InvoiceAmount">Tiền hóa đơn (có VAT).</param>
/// <param name="Status">Trạng thái thanh toán của hóa đơn.</param>
/// <param name="PaymentNo">Số đợt thanh toán (đề nghị) đã duyệt chứa hóa đơn; null khi chưa duyệt.</param>
/// <param name="PaidDate">Ngày thanh toán.</param>
public sealed record PortalPaymentInvoiceDto(
    string ReceiptNo, DateOnly ReceivedDate, decimal ReceivedAmount, string? DeliveryNoteNo, string? InvoiceNo, string? InvoiceSeries,
    DateOnly? InvoiceDate, decimal? InvoiceAmount, string Status, string? PaymentNo, DateOnly? PaidDate);

/// <summary>
/// Một đợt thanh toán = đề nghị thanh toán đã duyệt (P6b-3). Đề nghị đang lập / chờ duyệt không hiện (số tiền còn đổi). Status: "Approved"
/// đã duyệt – chờ chi, "Paid" đã thanh toán. Không có người lập / người duyệt, ý kiến duyệt, ghi chú chi của kế toán.
/// </summary>
/// <param name="No">Số đề nghị thanh toán.</param>
/// <param name="ApprovedDate">Ngày duyệt.</param>
/// <param name="Method">"Cash" tiền mặt hoặc "BankTransfer" chuyển khoản.</param>
/// <param name="BankName">Ngân hàng nhận (chuyển khoản).</param>
/// <param name="BankAccountNumber">Số tài khoản nhận.</param>
/// <param name="BankAccountName">Chủ tài khoản.</param>
/// <param name="InvoiceNos">Số các hóa đơn trong đợt, cách dấu phẩy.</param>
/// <param name="TotalAmount">Tổng tiền hóa đơn của đợt.</param>
/// <param name="DeductionAmount">Giảm trừ.</param>
/// <param name="DeductionReason">Lý do giảm trừ.</param>
/// <param name="PayableAmount">Số tiền thanh toán = tổng − giảm trừ.</param>
/// <param name="Status">Trạng thái.</param>
/// <param name="PaidDate">Ngày thanh toán.</param>
public sealed record PortalPaymentRoundDto(
    string No, DateOnly ApprovedDate, string Method, string? BankName, string? BankAccountNumber, string? BankAccountName, string InvoiceNos,
    decimal TotalAmount, decimal DeductionAmount, string? DeductionReason, decimal PayableAmount, string Status, DateOnly? PaidDate);

/// <summary>Chi tiết thanh toán một đơn trên Portal (P6b-2, P6b-3).</summary>
/// <param name="Summary">Tổng hợp của đơn.</param>
/// <param name="Invoices">Phiếu nhận hàng – hóa đơn (mới trước).</param>
/// <param name="Rounds">Đợt thanh toán đã duyệt (mới trước).</param>
public sealed record PortalPaymentDetailDto(
    PortalPaymentOrderDto Summary, IReadOnlyList<PortalPaymentInvoiceDto> Invoices, IReadOnlyList<PortalPaymentRoundDto> Rounds);

/// <summary>
/// Trạng thái thanh toán trên Portal (lát P6b – design.md 19.5). Chỉ đọc; NCC lấy từ phiên (P1b-4); chỉ đơn NCC được thấy (P4-1), không gồm
/// đề nghị 1C tạp vụ (không gắn NCC).
/// </summary>
public interface IPortalPaymentService
{
    /// <summary>Các đơn đã có phiếu nhận hàng xác nhận hoặc đề nghị thanh toán (mới trước).</summary>
    /// <param name="session">Phiên Portal.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Tổng hợp từng đơn.</returns>
    Task<IReadOnlyList<PortalPaymentOrderDto>> ListAsync(PortalSession session, CancellationToken cancellationToken);

    /// <summary>Chi tiết thanh toán một đơn.</summary>
    /// <param name="session">Phiên Portal.</param>
    /// <param name="orderId">PO.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Chi tiết.</returns>
    /// <exception cref="Common.NotFoundException">Đơn không thuộc NCC hoặc NCC không được thấy.</exception>
    Task<PortalPaymentDetailDto> GetAsync(PortalSession session, Guid orderId, CancellationToken cancellationToken);
}
