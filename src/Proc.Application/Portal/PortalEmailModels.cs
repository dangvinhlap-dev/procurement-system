using Proc.Application.Notifications;
using Proc.Domain.MasterData;
using Proc.Domain.Sourcing;

namespace Proc.Application.Portal;

/// <summary>
/// Một thư đã báo NCC về RFQ / PO, hiện trên tab "Email NCC" của RFQ (lát P3b-3) và tab "Xác nhận NCC" của PO (lát P4a). Kind: RfqInvitation,
/// RfqDeadlineExtended, RfqCancelled, RfqAnswer, RfqNotice, PurchaseOrderIssued, PurchaseOrderCancelled, PurchaseOrderReopened,
/// RfqAwarded, RfqNotAwarded (kết quả chọn thầu – lát P4b), PaymentPaid (đã thanh toán – lát P6b). State: Pending (chờ gửi), Sent, Skipped (thôi gửi), Failed (lỗi hết lượt thử).
/// </summary>
/// <param name="Id">Thư.</param>
/// <param name="SupplierId">NCC nhận.</param>
/// <param name="SupplierCode">Mã NCC.</param>
/// <param name="SupplierName">Tên NCC.</param>
/// <param name="ToAddress">Email nhận.</param>
/// <param name="ToName">Tên người nhận.</param>
/// <param name="Kind">Loại thư.</param>
/// <param name="Subject">Tiêu đề thư.</param>
/// <param name="CreatedAt">Lúc soạn (UTC).</param>
/// <param name="State">Trạng thái gửi.</param>
/// <param name="Attempts">Số lần đã thử.</param>
/// <param name="SentAt">Lúc gửi xong / thôi gửi (UTC).</param>
/// <param name="Note">Lỗi gần nhất hoặc lý do thôi gửi.</param>
public sealed record SupplierPortalEmailDto(
    Guid Id, Guid SupplierId, string? SupplierCode, string? SupplierName, string ToAddress, string ToName, string Kind, string Subject,
    DateTimeOffset CreatedAt, string State, int Attempts, DateTimeOffset? SentAt, string? Note);

/// <summary>
/// Thư báo NCC qua Portal (lát P3b-3 – design.md mục 19.5, P3-15…P3-18): soạn thư vào hàng chờ cùng giao dịch với thao tác RFQ, dịch vụ nền
/// email gửi sau; nội bộ xem thư đã báo NCC của từng RFQ.
/// </summary>
public interface IPortalEmailService
{
    /// <summary>
    /// Soạn thư về một RFQ cho mọi tài khoản Portal **đang dùng** của các NCC được mời (hoặc chỉ một NCC) và thêm vào DbContext – **không lưu**:
    /// người gọi lưu cùng thay đổi nghiệp vụ để thao tác và thư cùng thành công hoặc cùng hỏng. NCC trong danh sách đen hoặc chưa có tài khoản
    /// Portal đang dùng thì không có thư (Phòng CUVT báo NCC theo cách cũ).
    /// </summary>
    /// <param name="rfq">RFQ (đã ở trạng thái sau thao tác, ví dụ hạn nộp mới).</param>
    /// <param name="kind">Loại thư.</param>
    /// <param name="onlySupplierId">Chỉ gửi NCC này (trả lời câu hỏi); null = mọi NCC được mời.</param>
    /// <param name="detail">Nội dung thêm (câu hỏi – trả lời, thông báo làm rõ, số dòng trúng thầu); xuống dòng giữ nguyên.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Số thư đã soạn.</returns>
    Task<int> QueueRfqAsync(Rfq rfq, SupplierPortalEmailKind kind, Guid? onlySupplierId, string? detail, CancellationToken cancellationToken);

    /// <summary>
    /// Một lần quét gửi thư chờ (cũ trước). Email đang tắt thì không làm gì; thư quá cũ, tài khoản đã khóa, NCC vào danh sách đen thì thôi gửi;
    /// lỗi thì thử lại đến hết lượt. Dịch vụ nền email gọi định kỳ.
    /// </summary>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Số thư gửi được, bỏ qua, lỗi.</returns>
    Task<EmailDispatchResult> DispatchAsync(CancellationToken cancellationToken);

    /// <summary>Thư đã báo NCC của một RFQ thuộc công ty đang làm việc (mới trước).</summary>
    /// <param name="rfqId">RFQ.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Danh sách thư.</returns>
    /// <exception cref="Common.NotFoundException">RFQ không thuộc công ty đang làm việc.</exception>
    Task<IReadOnlyList<SupplierPortalEmailDto>> ListForRfqAsync(Guid rfqId, CancellationToken cancellationToken);

    /// <summary>
    /// Soạn thư về một PO cho mọi tài khoản Portal đang dùng của NCC của PO (lát P4a – P4-5) và thêm vào DbContext – **không lưu** (người gọi lưu
    /// cùng thao tác). PO mua nội bộ (NCC nội bộ) không có thư.
    /// </summary>
    /// <param name="order">PO.</param>
    /// <param name="kind">PurchaseOrderIssued, PurchaseOrderCancelled, PurchaseOrderReopened hoặc PaymentPaid (lát P6b).</param>
    /// <param name="detail">Nội dung thêm (lý do mở lại; số tiền, hóa đơn của đợt thanh toán – mỗi ý một dòng); null nếu không có.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Số thư đã soạn.</returns>
    Task<int> QueuePurchaseOrderAsync(Proc.Domain.Purchasing.PurchaseOrder order, SupplierPortalEmailKind kind, string? detail, CancellationToken cancellationToken);

    /// <summary>Thư đã báo NCC về một PO thuộc công ty đang làm việc (mới trước).</summary>
    /// <param name="orderId">PO.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Danh sách thư.</returns>
    /// <exception cref="Common.NotFoundException">PO không thuộc công ty đang làm việc.</exception>
    Task<IReadOnlyList<SupplierPortalEmailDto>> ListForPurchaseOrderAsync(Guid orderId, CancellationToken cancellationToken);

    /// <summary>
    /// Soạn thư báo kết quả đề nghị cập nhật hồ sơ (lát P2a – P2-5) cho mọi tài khoản Portal đang dùng của NCC và thêm vào DbContext – **không
    /// lưu** (người gọi lưu cùng thao tác). Thư ghi theo tổng công ty (NCC là danh mục dùng chung).
    /// </summary>
    /// <param name="request">Đề nghị đã chấp nhận / từ chối.</param>
    /// <param name="kind">ProfileRequestAccepted hoặc ProfileRequestRejected.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Số thư đã soạn.</returns>
    Task<int> QueueProfileRequestAsync(SupplierProfileRequest request, SupplierPortalEmailKind kind, CancellationToken cancellationToken);
}
