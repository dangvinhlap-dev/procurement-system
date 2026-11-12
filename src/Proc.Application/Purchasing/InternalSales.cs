namespace Proc.Application.Purchasing;

/// <summary>Lọc danh sách đơn bán nội bộ.</summary>
/// <param name="From">Từ ngày PO (mặc định 90 ngày trước ngày đến).</param>
/// <param name="To">Đến ngày PO (mặc định hôm nay).</param>
/// <param name="Status">Pending, Confirmed, Rejected, Cancelled; trống = tất cả.</param>
public sealed record InternalSaleQuery(DateOnly? From, DateOnly? To, string? Status);

/// <summary>Một dòng danh sách "Đơn bán nội bộ" (lát X7).</summary>
/// <param name="OrderId">Id PO của công ty mua.</param>
/// <param name="OrderNo">Số PO.</param>
/// <param name="OrderDate">Ngày PO.</param>
/// <param name="BuyerCompanyCode">Mã công ty mua.</param>
/// <param name="BuyerCompanyName">Tên công ty mua.</param>
/// <param name="WarehouseName">Nơi nhận hàng (kho của công ty mua).</param>
/// <param name="ExpectedDate">Ngày giao dự kiến.</param>
/// <param name="Amount">Tổng tiền gồm VAT.</param>
/// <param name="LineCount">Số dòng.</param>
/// <param name="OrderStatus">Trạng thái PO: Approved, Closed, Cancelled.</param>
/// <param name="Status">Trạng thái bên bán: Pending (chờ xác nhận), Confirmed, Rejected, Cancelled (PO đã hủy trước khi xác nhận).</param>
/// <param name="DecidedByName">Người xác nhận / từ chối.</param>
/// <param name="DecidedAt">Thời điểm xác nhận / từ chối (UTC).</param>
public sealed record InternalSaleListItemDto(
    Guid OrderId, string OrderNo, DateOnly OrderDate, string BuyerCompanyCode, string BuyerCompanyName, string? WarehouseName,
    DateOnly? ExpectedDate, decimal Amount, int LineCount, string OrderStatus, string Status, string? DecidedByName, DateTimeOffset? DecidedAt);

/// <summary>Kết quả danh sách đơn bán nội bộ.</summary>
/// <param name="Items">Các dòng (mới nhất trước).</param>
/// <param name="Truncated">Còn dòng chưa trả do vượt giới hạn.</param>
/// <param name="Limit">Giới hạn số dòng.</param>
public sealed record InternalSaleListResult(IReadOnlyList<InternalSaleListItemDto> Items, bool Truncated, int Limit);

/// <summary>Một dòng hàng của đơn bán nội bộ (chỉ xem – PO thuộc công ty mua).</summary>
/// <param name="LineNo">STT.</param>
/// <param name="ItemId">Mặt hàng.</param>
/// <param name="ItemCode">Mã hàng.</param>
/// <param name="ItemName">Tên hàng.</param>
/// <param name="Uom">ĐVT.</param>
/// <param name="Qty">SL đặt.</param>
/// <param name="UnitPrice">Đơn giá.</param>
/// <param name="DiscountPct">% chiết khấu.</param>
/// <param name="Amount">Thành tiền trước VAT.</param>
/// <param name="VatCode">Thuế suất.</param>
/// <param name="VatAmount">Tiền VAT.</param>
/// <param name="DeliveryDate">Ngày giao của dòng.</param>
/// <param name="ReceivedQty">SL công ty mua đã xác nhận nhận (XNH), theo ĐVT dòng.</param>
/// <param name="Note">Ghi chú dòng.</param>
public sealed record InternalSaleLineDto(
    int LineNo, Guid ItemId, string ItemCode, string ItemName, string Uom, decimal Qty, decimal UnitPrice, decimal DiscountPct, decimal Amount,
    string? VatCode, decimal VatAmount, DateOnly? DeliveryDate, decimal ReceivedQty, string? Note);

/// <summary>Chi tiết đơn bán nội bộ cho công ty bán.</summary>
/// <param name="OrderId">Id PO.</param>
/// <param name="OrderNo">Số PO.</param>
/// <param name="OrderDate">Ngày PO.</param>
/// <param name="OrderStatus">Trạng thái PO.</param>
/// <param name="BuyerCompanyCode">Mã công ty mua.</param>
/// <param name="BuyerCompanyName">Tên công ty mua.</param>
/// <param name="WarehouseName">Nơi nhận hàng.</param>
/// <param name="DeliveryAddress">Địa chỉ giao.</param>
/// <param name="ExpectedDate">Ngày giao dự kiến.</param>
/// <param name="PaymentTerms">Điều khoản thanh toán.</param>
/// <param name="DeliveryTerms">Điều khoản giao hàng.</param>
/// <param name="OrderNote">Ghi chú PO.</param>
/// <param name="OrderedByName">Người lập PO.</param>
/// <param name="ApprovedAt">Thời điểm PO được duyệt (UTC).</param>
/// <param name="TotalAmount">Tiền hàng trước VAT.</param>
/// <param name="TotalVat">Tiền VAT.</param>
/// <param name="Lines">Dòng hàng.</param>
/// <param name="Status">Trạng thái bên bán (như danh sách).</param>
/// <param name="Note">Ghi chú xác nhận / lý do từ chối.</param>
/// <param name="DecidedByName">Người xác nhận / từ chối.</param>
/// <param name="DecidedAt">Thời điểm (UTC).</param>
/// <param name="CanDecide">Người dùng được bấm Xác nhận / Từ chối.</param>
/// <param name="ReadOnlyReason">Lý do chỉ xem (thiếu quyền, đơn đã hủy…), null khi được thao tác.</param>
public sealed record InternalSaleDto(
    Guid OrderId, string OrderNo, DateOnly OrderDate, string OrderStatus, string BuyerCompanyCode, string BuyerCompanyName, string? WarehouseName,
    string? DeliveryAddress, DateOnly? ExpectedDate, string? PaymentTerms, string? DeliveryTerms, string? OrderNote, string? OrderedByName,
    DateTimeOffset? ApprovedAt, decimal TotalAmount, decimal TotalVat, IReadOnlyList<InternalSaleLineDto> Lines, string Status, string? Note,
    string? DecidedByName, DateTimeOffset? DecidedAt, bool CanDecide, string? ReadOnlyReason);

/// <summary>Trạng thái bên bán hiện trên PO mua nội bộ của công ty mua (lát X7).</summary>
/// <param name="SellerCompanyCode">Mã công ty bán.</param>
/// <param name="SellerCompanyName">Tên công ty bán.</param>
/// <param name="Status">NotSent (PO chưa duyệt), Pending, Confirmed, Rejected.</param>
/// <param name="Note">Ghi chú / lý do từ chối của công ty bán.</param>
/// <param name="DecidedByName">Người bên bán xác nhận / từ chối.</param>
/// <param name="DecidedAt">Thời điểm (UTC).</param>
public sealed record InternalSaleStatusDto(
    string SellerCompanyCode, string SellerCompanyName, string Status, string? Note, string? DecidedByName, DateTimeOffset? DecidedAt);

/// <summary>Xác nhận (ghi chú tùy chọn) hoặc từ chối (lý do bắt buộc) đơn bán nội bộ.</summary>
/// <param name="Note">Ghi chú / lý do.</param>
public sealed record InternalSaleDecisionRequest(string? Note);

/// <summary>
/// "Đơn bán nội bộ" của công ty bán (lát X7, design.md mục 19.3, 19.4): PO đã duyệt mà công ty khác đặt cho NCC nội bộ đại diện công ty đang
/// làm việc; công ty bán xác nhận hoặc từ chối. Chỉ đọc PO của công ty khác qua đúng liên kết công ty bán ghi trên PO.
/// </summary>
public interface IInternalSaleService
{
    /// <summary>Danh sách đơn bán nội bộ của công ty đang làm việc.</summary>
    /// <param name="query">Lọc.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Các đơn, mới nhất trước.</returns>
    Task<InternalSaleListResult> SearchAsync(InternalSaleQuery query, CancellationToken cancellationToken);

    /// <summary>Chi tiết một đơn.</summary>
    /// <param name="orderId">Id PO.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Chi tiết.</returns>
    /// <exception cref="Common.NotFoundException">Khi PO không phải đơn bán nội bộ đã duyệt của công ty này.</exception>
    Task<InternalSaleDto> GetAsync(Guid orderId, CancellationToken cancellationToken);

    /// <summary>Xác nhận nhận đơn; báo người lập PO.</summary>
    /// <param name="orderId">Id PO.</param>
    /// <param name="request">Ghi chú tùy chọn.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Chi tiết sau khi xác nhận.</returns>
    /// <exception cref="Domain.Common.BusinessRuleException">Khi đơn đã có quyết định, PO không còn hiệu lực hoặc có mặt hàng ngoài phạm vi.</exception>
    /// <exception cref="Common.NotFoundException">Khi không có đơn.</exception>
    Task<InternalSaleDto> ConfirmAsync(Guid orderId, InternalSaleDecisionRequest request, CancellationToken cancellationToken);

    /// <summary>Từ chối đơn (bắt buộc lý do); báo người lập PO.</summary>
    /// <param name="orderId">Id PO.</param>
    /// <param name="request">Lý do.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Chi tiết sau khi từ chối.</returns>
    /// <exception cref="Domain.Common.BusinessRuleException">Khi thiếu lý do, đơn đã có quyết định hoặc PO không còn hiệu lực.</exception>
    /// <exception cref="Common.NotFoundException">Khi không có đơn.</exception>
    Task<InternalSaleDto> RejectAsync(Guid orderId, InternalSaleDecisionRequest request, CancellationToken cancellationToken);
}

/// <summary>Một lần NCC phản hồi PO trên Portal (lát P4a).</summary>
/// <param name="Decision">Confirmed hoặc Rejected.</param>
/// <param name="PromisedDate">Ngày giao NCC cam kết (khi xác nhận).</param>
/// <param name="Note">Ghi chú / lý do từ chối của NCC.</param>
/// <param name="RespondedAt">Thời điểm phản hồi (UTC).</param>
/// <param name="RespondedByEmail">Email tài khoản Portal phản hồi.</param>
/// <param name="RespondedByName">Họ tên tài khoản Portal phản hồi.</param>
/// <param name="ReopenedAt">Lúc Phòng CUVT mở lại (UTC); null = phản hồi đang có hiệu lực.</param>
/// <param name="ReopenedByName">Người mở lại.</param>
/// <param name="ReopenReason">Lý do mở lại (đã gửi NCC).</param>
public sealed record SupplierOrderResponseItemDto(
    string Decision, DateOnly? PromisedDate, string? Note, DateTimeOffset RespondedAt, string RespondedByEmail, string? RespondedByName,
    DateTimeOffset? ReopenedAt, string? ReopenedByName, string? ReopenReason);

/// <summary>
/// Phản hồi của NCC với PO trên Portal, hiện trên PO của công ty đặt hàng (lát P4a – design.md 19.5 P4-4).
/// </summary>
/// <param name="Status">NotSent (PO chưa duyệt), Pending (chờ NCC xác nhận / đã mở lại), Confirmed, Rejected.</param>
/// <param name="ActivePortalUsers">Số tài khoản Portal đang dùng của NCC – 0 thì NCC không xác nhận trên Portal được, Phòng CUVT báo cách cũ.</param>
/// <param name="CanReopen">Người dùng mở lại được phản hồi đang có hiệu lực (quyền lập PO, PO Đã duyệt).</param>
/// <param name="Responses">Các lần phản hồi, mới trước; phần tử đầu chưa mở lại là phản hồi đang có hiệu lực.</param>
public sealed record SupplierOrderResponseDto(
    string Status, int ActivePortalUsers, bool CanReopen, IReadOnlyList<SupplierOrderResponseItemDto> Responses);

/// <summary>Phòng CUVT mở lại phản hồi của NCC.</summary>
/// <param name="Reason">Lý do (bắt buộc, gửi NCC).</param>
public sealed record ReopenSupplierResponseRequest(string? Reason);
