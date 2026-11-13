using Proc.Application.Documents;

namespace Proc.Application.Portal;

/// <summary>
/// Một đơn đặt hàng NCC thấy trên Portal (lát P4a – design.md 19.5 P4-1). Status: "Pending" chờ NCC xác nhận, "Confirmed" đã xác nhận,
/// "Rejected" đã từ chối (bên đặt hàng mở lại phản hồi thì về "Pending"), "Received" bên đặt đã nhận đủ hàng, "Cancelled" bên đặt đã hủy đơn.
/// </summary>
/// <param name="Id">PO.</param>
/// <param name="No">Số PO.</param>
/// <param name="CompanyName">Công ty đặt hàng.</param>
/// <param name="Date">Ngày đặt hàng.</param>
/// <param name="ExpectedDate">Ngày giao dự kiến.</param>
/// <param name="Status">Trạng thái hiển thị cho NCC.</param>
/// <param name="LineCount">Số dòng hàng.</param>
/// <param name="TotalPayable">Tổng thanh toán (sau chiết khấu, có VAT).</param>
/// <param name="CurrencyCode">Tiền tệ.</param>
/// <param name="PromisedDate">Ngày giao NCC đã cam kết.</param>
/// <param name="RespondedAt">Lúc NCC phản hồi.</param>
public sealed record PortalOrderListItemDto(
    Guid Id, string No, string CompanyName, DateOnly Date, DateOnly? ExpectedDate, string Status, int LineCount, decimal TotalPayable,
    string CurrencyCode, DateOnly? PromisedDate, DateTimeOffset? RespondedAt);

/// <summary>Dòng hàng của đơn trên Portal – không có giá đã duyệt nội bộ, nguồn giá, lý do lệch giá.</summary>
/// <param name="LineNo">STT.</param>
/// <param name="ItemCode">Mã hàng.</param>
/// <param name="ItemName">Tên hàng.</param>
/// <param name="SupplierItemCode">Mã hàng của chính NCC.</param>
/// <param name="UomCode">ĐVT.</param>
/// <param name="Qty">Số lượng đặt.</param>
/// <param name="UnitPrice">Đơn giá trước chiết khấu, VAT.</param>
/// <param name="DiscountPct">% chiết khấu.</param>
/// <param name="VatCode">Thuế suất.</param>
/// <param name="Amount">Thành tiền sau chiết khấu, trước VAT.</param>
/// <param name="VatAmount">Tiền VAT.</param>
/// <param name="DeliveryDate">Ngày giao của dòng.</param>
/// <param name="Note">Ghi chú dòng.</param>
/// <param name="PurchaseOrderLineId">Id dòng PO – NCC chọn dòng khi báo giao (lát P5).</param>
/// <param name="ReceivedQty">SL bên đặt đã xác nhận nhận (XNH đã xác nhận, kể cả điều chỉnh).</param>
/// <param name="DeliverableQty">SL còn báo giao được = SL đặt × (1 + dung sai) − đã nhận − đang báo giao chưa nhận (P5-1).</param>
public sealed record PortalOrderLineDto(
    int LineNo, string ItemCode, string ItemName, string? SupplierItemCode, string UomCode, decimal Qty, decimal UnitPrice, decimal DiscountPct,
    string? VatCode, decimal Amount, decimal VatAmount, DateOnly? DeliveryDate, string? Note, Guid PurchaseOrderLineId, decimal ReceivedQty,
    decimal DeliverableQty);

/// <summary>Một lần NCC phản hồi đơn – không có tên nhân viên bên đặt hàng (như hỏi đáp P3-14).</summary>
/// <param name="Decision">Confirmed hoặc Rejected.</param>
/// <param name="PromisedDate">Ngày giao cam kết.</param>
/// <param name="Note">Ghi chú / lý do từ chối.</param>
/// <param name="RespondedAt">Lúc phản hồi (UTC).</param>
/// <param name="RespondedByEmail">Tài khoản phản hồi.</param>
/// <param name="ReopenedAt">Lúc bên đặt hàng mở lại (UTC); null = phản hồi đang có hiệu lực.</param>
/// <param name="ReopenReason">Lý do bên đặt hàng mở lại.</param>
public sealed record PortalOrderResponseDto(
    string Decision, DateOnly? PromisedDate, string? Note, DateTimeOffset RespondedAt, string RespondedByEmail, DateTimeOffset? ReopenedAt,
    string? ReopenReason);

/// <summary>Chi tiết đơn đặt hàng trên Portal.</summary>
/// <param name="Id">PO.</param>
/// <param name="No">Số PO.</param>
/// <param name="CompanyName">Công ty đặt hàng.</param>
/// <param name="Date">Ngày đặt hàng.</param>
/// <param name="ApprovedAt">Lúc đơn được duyệt (UTC).</param>
/// <param name="ExpectedDate">Ngày giao dự kiến.</param>
/// <param name="DeliveryPlace">Nơi nhận hàng (tên kho / điểm nhận).</param>
/// <param name="DeliveryAddress">Địa chỉ giao.</param>
/// <param name="PaymentTerms">Điều khoản thanh toán.</param>
/// <param name="DeliveryTerms">Điều khoản giao hàng.</param>
/// <param name="Note">Ghi chú đơn.</param>
/// <param name="CurrencyCode">Tiền tệ.</param>
/// <param name="Status">Trạng thái hiển thị (như danh sách).</param>
/// <param name="CanRespond">Còn xác nhận / từ chối được (P4-2).</param>
/// <param name="GrossAmount">Tiền hàng trước chiết khấu.</param>
/// <param name="TotalDiscount">Chiết khấu.</param>
/// <param name="TotalAmount">Tiền hàng sau chiết khấu, trước VAT.</param>
/// <param name="TotalVat">Tiền VAT.</param>
/// <param name="Lines">Dòng hàng.</param>
/// <param name="Responses">Các lần phản hồi, mới trước (phần tử chưa mở lại là phản hồi đang có hiệu lực).</param>
/// <param name="MaxFileSizeMb">Dung lượng tối đa một file đính kèm.</param>
/// <param name="CanNotifyDelivery">Còn báo giao được (lát P5, P5-1): đơn Đã duyệt, NCC chưa từ chối, còn dòng chưa giao hết.</param>
/// <param name="NotifyDeliveryBlockedReason">Lý do chưa báo giao được (null khi được).</param>
/// <param name="DeliveryNotices">Các phiếu báo giao của đơn, mới trước.</param>
public sealed record PortalOrderDto(
    Guid Id, string No, string CompanyName, DateOnly Date, DateTimeOffset? ApprovedAt, DateOnly? ExpectedDate, string? DeliveryPlace,
    string? DeliveryAddress, string? PaymentTerms, string? DeliveryTerms, string? Note, string CurrencyCode, string Status, bool CanRespond,
    decimal GrossAmount, decimal TotalDiscount, decimal TotalAmount, decimal TotalVat, IReadOnlyList<PortalOrderLineDto> Lines,
    IReadOnlyList<PortalOrderResponseDto> Responses, int MaxFileSizeMb, bool CanNotifyDelivery, string? NotifyDeliveryBlockedReason,
    IReadOnlyList<PortalDeliveryNoticeDto> DeliveryNotices);

/// <summary>Dòng phiếu báo giao NCC thấy trên Portal (lát P5, P5-6).</summary>
/// <param name="LineNo">STT dòng phiếu.</param>
/// <param name="PoLineNo">STT dòng đơn.</param>
/// <param name="ItemCode">Mã hàng.</param>
/// <param name="ItemName">Tên hàng.</param>
/// <param name="UomCode">ĐVT (của dòng đơn).</param>
/// <param name="Qty">SL NCC báo giao.</param>
/// <param name="ReceivedQty">SL bên đặt xác nhận nhận đạt (XNH lập theo phiếu đã xác nhận); null = chưa có.</param>
/// <param name="RejectedQty">SL không đạt theo XNH đã xác nhận; null = chưa có.</param>
/// <param name="Note">Ghi chú dòng.</param>
public sealed record PortalDeliveryNoticeLineDto(
    int LineNo, int PoLineNo, string ItemCode, string ItemName, string UomCode, decimal Qty, decimal? ReceivedQty, decimal? RejectedQty, string? Note);

/// <summary>
/// Phiếu báo giao NCC thấy trên Portal (lát P5). Status: "Submitted" đã gửi – chờ nhận, "Receiving" bên đặt đang kiểm nhận (XNH nháp),
/// "Received" bên đặt đã xác nhận nhận hàng, "Cancelled" NCC đã hủy (P5-2).
/// </summary>
/// <param name="Id">Phiếu báo giao.</param>
/// <param name="No">Số BGH.</param>
/// <param name="ExpectedDate">Ngày giao dự kiến.</param>
/// <param name="DeliveryNoteNo">Số phiếu giao của NCC.</param>
/// <param name="Deliverer">Người giao, phương tiện.</param>
/// <param name="Note">Ghi chú.</param>
/// <param name="Status">Trạng thái hiển thị.</param>
/// <param name="SubmittedAt">Lúc gửi (UTC).</param>
/// <param name="SubmittedByEmail">Tài khoản gửi.</param>
/// <param name="CancelledAt">Lúc hủy (UTC).</param>
/// <param name="CancelReason">Lý do hủy.</param>
/// <param name="ReceiptNo">Số XNH bên đặt lập theo phiếu (khi đang nhận / đã nhận).</param>
/// <param name="ReceivedDate">Ngày nhận hàng trên XNH đã xác nhận.</param>
/// <param name="CanCancel">NCC còn hủy được (chưa có XNH chưa hủy).</param>
/// <param name="Lines">Dòng.</param>
public sealed record PortalDeliveryNoticeDto(
    Guid Id, string No, DateOnly ExpectedDate, string DeliveryNoteNo, string? Deliverer, string? Note, string Status, DateTimeOffset SubmittedAt,
    string SubmittedByEmail, DateTimeOffset? CancelledAt, string? CancelReason, string? ReceiptNo, DateOnly? ReceivedDate, bool CanCancel,
    IReadOnlyList<PortalDeliveryNoticeLineDto> Lines);

/// <summary>Một dòng NCC báo giao.</summary>
/// <param name="PurchaseOrderLineId">Dòng đơn.</param>
/// <param name="Qty">SL giao (theo ĐVT dòng đơn).</param>
/// <param name="Note">Ghi chú dòng (số lô, hạn dùng…).</param>
public sealed record PortalDeliveryNoticeLineRequest(Guid PurchaseOrderLineId, decimal Qty, string? Note);

/// <summary>NCC gửi phiếu báo giao.</summary>
/// <param name="ExpectedDate">Ngày giao dự kiến.</param>
/// <param name="DeliveryNoteNo">Số phiếu giao của NCC (bắt buộc).</param>
/// <param name="Deliverer">Người giao, phương tiện.</param>
/// <param name="Note">Ghi chú.</param>
/// <param name="Lines">Dòng giao (bỏ dòng SL 0 trước khi gửi).</param>
public sealed record PortalDeliveryNoticeRequest(
    DateOnly ExpectedDate, string? DeliveryNoteNo, string? Deliverer, string? Note, IReadOnlyList<PortalDeliveryNoticeLineRequest> Lines);

/// <summary>NCC hủy phiếu báo giao.</summary>
/// <param name="Reason">Lý do (bắt buộc).</param>
public sealed record PortalCancelDeliveryNoticeRequest(string? Reason);

/// <summary>NCC xác nhận hoặc từ chối đơn.</summary>
/// <param name="Decision">"Confirmed" hoặc "Rejected".</param>
/// <param name="PromisedDate">Ngày giao cam kết (bắt buộc khi xác nhận).</param>
/// <param name="Note">Ghi chú; bắt buộc khi từ chối (lý do).</param>
public sealed record PortalOrderRespondRequest(string Decision, DateOnly? PromisedDate, string? Note);

/// <summary>
/// Đơn đặt hàng trên Portal (lát P4a – design.md mục 19.5, P4-1…P4-3). NCC lấy từ phiên, không nhận từ client (P1b-4); đơn của NCC khác,
/// đơn chưa duyệt, đơn mua nội bộ trả 404 như không tồn tại.
/// </summary>
public interface IPortalOrderService
{
    /// <summary>Các đơn của NCC (mới trước).</summary>
    /// <param name="session">Phiên Portal.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Danh sách đơn.</returns>
    Task<IReadOnlyList<PortalOrderListItemDto>> ListAsync(PortalSession session, CancellationToken cancellationToken);

    /// <summary>Chi tiết một đơn.</summary>
    /// <param name="session">Phiên Portal.</param>
    /// <param name="id">PO.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Đơn.</returns>
    /// <exception cref="Common.NotFoundException">Đơn không thuộc NCC hoặc chưa duyệt.</exception>
    Task<PortalOrderDto> GetAsync(PortalSession session, Guid id, CancellationToken cancellationToken);

    /// <summary>Xác nhận hoặc từ chối đơn – không sửa (Phòng CUVT mở lại thì phản hồi lại được); báo người lập PO.</summary>
    /// <param name="session">Phiên Portal.</param>
    /// <param name="id">PO.</param>
    /// <param name="request">Phản hồi.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Đơn sau khi phản hồi.</returns>
    /// <exception cref="Proc.Domain.Common.BusinessRuleException">Đơn không còn chờ xác nhận, đã có phản hồi, thiếu ngày giao / lý do.</exception>
    Task<PortalOrderDto> RespondAsync(PortalSession session, Guid id, PortalOrderRespondRequest request, CancellationToken cancellationToken);

    /// <summary>File NCC gửi kèm đơn.</summary>
    /// <param name="session">Phiên Portal.</param>
    /// <param name="id">PO.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Danh sách file (CanDelete = còn xóa được).</returns>
    Task<IReadOnlyList<AttachmentDto>> ListAttachmentsAsync(PortalSession session, Guid id, CancellationToken cancellationToken);

    /// <summary>Tải file lên – chỉ khi đơn còn chờ xác nhận (P4-3).</summary>
    /// <param name="session">Phiên Portal.</param>
    /// <param name="id">PO.</param>
    /// <param name="fileName">Tên file.</param>
    /// <param name="sizeBytes">Dung lượng.</param>
    /// <param name="content">Nội dung.</param>
    /// <param name="note">Ghi chú file.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>File đã lưu.</returns>
    Task<AttachmentDto> UploadAttachmentAsync(
        PortalSession session, Guid id, string fileName, long sizeBytes, Stream content, string? note, CancellationToken cancellationToken);

    /// <summary>Tải file xuống.</summary>
    /// <param name="session">Phiên Portal.</param>
    /// <param name="id">PO.</param>
    /// <param name="attachmentId">File.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Nội dung file.</returns>
    Task<AttachmentContent> OpenAttachmentAsync(PortalSession session, Guid id, Guid attachmentId, CancellationToken cancellationToken);

    /// <summary>Xóa file – chỉ khi đơn còn chờ xác nhận.</summary>
    /// <param name="session">Phiên Portal.</param>
    /// <param name="id">PO.</param>
    /// <param name="attachmentId">File.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    Task DeleteAttachmentAsync(PortalSession session, Guid id, Guid attachmentId, CancellationToken cancellationToken);
}

/// <summary>
/// NCC báo giao hàng trên Portal (lát P5 – design.md mục 19.5, P5-1…P5-3). Đơn lấy theo NCC của phiên như <see cref="IPortalOrderService"/>
/// (đơn khác NCC / chưa duyệt / mua nội bộ trả 404); phiếu báo giao không thuộc đơn trả 404.
/// </summary>
public interface IPortalDeliveryNoticeService
{
    /// <summary>Gửi phiếu báo giao (P5-1): kiểm SL còn giao được, cấp số BGH, báo thủ kho nơi nhận và người lập PO.</summary>
    /// <param name="session">Phiên Portal.</param>
    /// <param name="orderId">PO.</param>
    /// <param name="request">Phiếu.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Phiếu vừa gửi (để tải file kèm).</returns>
    /// <exception cref="Proc.Domain.Common.BusinessRuleException">Khi đơn không còn nhận hàng, NCC đã từ chối đơn, SL vượt phần còn giao được.</exception>
    Task<PortalDeliveryNoticeDto> CreateAsync(
        PortalSession session, Guid orderId, PortalDeliveryNoticeRequest request, CancellationToken cancellationToken);

    /// <summary>Hủy phiếu báo giao (P5-2) – chỉ khi bên đặt chưa lập phiếu nhận hàng theo phiếu; bắt buộc lý do.</summary>
    /// <param name="session">Phiên Portal.</param>
    /// <param name="orderId">PO.</param>
    /// <param name="noticeId">Phiếu báo giao.</param>
    /// <param name="request">Lý do.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Đơn sau khi hủy.</returns>
    Task<PortalOrderDto> CancelAsync(
        PortalSession session, Guid orderId, Guid noticeId, PortalCancelDeliveryNoticeRequest request, CancellationToken cancellationToken);

    /// <summary>File kèm phiếu báo giao (P5-3).</summary>
    /// <param name="session">Phiên Portal.</param>
    /// <param name="orderId">PO.</param>
    /// <param name="noticeId">Phiếu báo giao.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Danh sách file (CanDelete = còn xóa được).</returns>
    Task<IReadOnlyList<AttachmentDto>> ListAttachmentsAsync(PortalSession session, Guid orderId, Guid noticeId, CancellationToken cancellationToken);

    /// <summary>Tải file lên – chỉ khi phiếu còn chờ nhận (chưa có phiếu nhận hàng, chưa hủy).</summary>
    /// <param name="session">Phiên Portal.</param>
    /// <param name="orderId">PO.</param>
    /// <param name="noticeId">Phiếu báo giao.</param>
    /// <param name="fileName">Tên file.</param>
    /// <param name="sizeBytes">Dung lượng.</param>
    /// <param name="content">Nội dung.</param>
    /// <param name="note">Ghi chú file.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>File đã lưu.</returns>
    Task<AttachmentDto> UploadAttachmentAsync(
        PortalSession session, Guid orderId, Guid noticeId, string fileName, long sizeBytes, Stream content, string? note,
        CancellationToken cancellationToken);

    /// <summary>Tải file xuống.</summary>
    /// <param name="session">Phiên Portal.</param>
    /// <param name="orderId">PO.</param>
    /// <param name="noticeId">Phiếu báo giao.</param>
    /// <param name="attachmentId">File.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Nội dung file.</returns>
    Task<AttachmentContent> OpenAttachmentAsync(
        PortalSession session, Guid orderId, Guid noticeId, Guid attachmentId, CancellationToken cancellationToken);

    /// <summary>Xóa file – chỉ khi phiếu còn chờ nhận.</summary>
    /// <param name="session">Phiên Portal.</param>
    /// <param name="orderId">PO.</param>
    /// <param name="noticeId">Phiếu báo giao.</param>
    /// <param name="attachmentId">File.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    Task DeleteAttachmentAsync(PortalSession session, Guid orderId, Guid noticeId, Guid attachmentId, CancellationToken cancellationToken);
}
