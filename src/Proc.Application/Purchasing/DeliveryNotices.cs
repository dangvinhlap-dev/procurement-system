namespace Proc.Application.Purchasing;

/// <summary>Dòng phiếu báo giao NCC xem ở nội bộ (lát P5).</summary>
/// <param name="LineNo">STT dòng phiếu.</param>
/// <param name="PurchaseOrderLineId">Dòng PO.</param>
/// <param name="PoLineNo">STT dòng PO.</param>
/// <param name="ItemCode">Mã hàng.</param>
/// <param name="ItemName">Tên hàng.</param>
/// <param name="UomCode">ĐVT dòng PO.</param>
/// <param name="Qty">SL NCC báo giao.</param>
/// <param name="Note">Ghi chú dòng.</param>
public sealed record DeliveryNoticeLineDto(
    int LineNo, Guid PurchaseOrderLineId, int PoLineNo, string ItemCode, string ItemName, string UomCode, decimal Qty, string? Note);

/// <summary>
/// Phiếu báo giao NCC gửi trên Portal, xem ở nội bộ (tab "Giao hàng NCC" của PO, lập XNH từ phiếu – design.md 19.5 P5-5). Status: "Submitted"
/// chờ nhận, "Receiving" có XNH nháp, "Received" có XNH đã xác nhận, "Cancelled" NCC đã hủy.
/// </summary>
/// <param name="Id">Phiếu báo giao.</param>
/// <param name="No">Số BGH.</param>
/// <param name="PurchaseOrderId">PO.</param>
/// <param name="PurchaseOrderNo">Số PO.</param>
/// <param name="ExpectedDate">Ngày giao dự kiến.</param>
/// <param name="DeliveryNoteNo">Số phiếu giao của NCC.</param>
/// <param name="Deliverer">Người giao, phương tiện.</param>
/// <param name="Note">Ghi chú của NCC.</param>
/// <param name="Status">Trạng thái hiển thị.</param>
/// <param name="SubmittedAt">Lúc NCC gửi (UTC).</param>
/// <param name="SubmittedByEmail">Tài khoản Portal gửi.</param>
/// <param name="CancelledAt">Lúc NCC hủy (UTC).</param>
/// <param name="CancelReason">Lý do NCC hủy.</param>
/// <param name="ReceiptId">XNH chưa hủy lập theo phiếu.</param>
/// <param name="ReceiptNo">Số XNH.</param>
/// <param name="CanCreateReceipt">Người dùng lập XNH từ phiếu được: phiếu chờ nhận, PO Đã duyệt, có quyền lập XNH.</param>
/// <param name="Lines">Dòng.</param>
public sealed record DeliveryNoticeDto(
    Guid Id, string No, Guid PurchaseOrderId, string PurchaseOrderNo, DateOnly ExpectedDate, string DeliveryNoteNo, string? Deliverer,
    string? Note, string Status, DateTimeOffset SubmittedAt, string SubmittedByEmail, DateTimeOffset? CancelledAt, string? CancelReason,
    Guid? ReceiptId, string? ReceiptNo, bool CanCreateReceipt, IReadOnlyList<DeliveryNoticeLineDto> Lines);

/// <summary>Phiếu báo giao NCC ở nội bộ (lát P5) – chỉ xem; NCC lập / hủy trên Portal, nơi nhận lập XNH từ phiếu.</summary>
public interface IDeliveryNoticeService
{
    /// <summary>Các phiếu báo giao của một PO (mới trước). Cần quyền xem PO hoặc xem XNH; PO công ty khác trả 404.</summary>
    /// <param name="purchaseOrderId">PO.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Danh sách phiếu.</returns>
    Task<IReadOnlyList<DeliveryNoticeDto>> ListForOrderAsync(Guid purchaseOrderId, CancellationToken cancellationToken);

    /// <summary>Một phiếu báo giao (điền sẵn XNH). Cần quyền xem PO hoặc xem XNH.</summary>
    /// <param name="id">Phiếu.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Phiếu.</returns>
    Task<DeliveryNoticeDto> GetAsync(Guid id, CancellationToken cancellationToken);
}
