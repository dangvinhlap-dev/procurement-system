namespace Proc.Application.Inventory;

/// <summary>Một dòng trên danh sách chứng từ kho.</summary>
/// <param name="Id">Id.</param>
/// <param name="DocType">Loại chứng từ.</param>
/// <param name="No">Số phiếu.</param>
/// <param name="Date">Ngày chứng từ.</param>
/// <param name="Status">Draft, Posted, Cancelled.</param>
/// <param name="WarehouseCode">Mã kho (kho nhập với GRN).</param>
/// <param name="WarehouseName">Tên kho.</param>
/// <param name="SupplierCode">Mã NCC.</param>
/// <param name="SupplierName">Tên NCC.</param>
/// <param name="InvoiceNo">Số hóa đơn.</param>
/// <param name="TotalAmount">Tiền hàng trước VAT.</param>
/// <param name="TotalVat">Tiền VAT.</param>
/// <param name="LineCount">Số dòng.</param>
public sealed record InventoryDocumentListItemDto(
    Guid Id, string DocType, string No, DateOnly Date, string Status, string? WarehouseCode, string? WarehouseName,
    string? SupplierCode, string? SupplierName, string? InvoiceNo, decimal TotalAmount, decimal TotalVat, int LineCount);

/// <summary>Kết quả tìm chứng từ; mỗi lần tối đa <see cref="Limit"/> dòng.</summary>
/// <param name="Items">Các phiếu, mới nhất trước.</param>
/// <param name="Truncated">true khi còn phiếu khớp chưa trả – thu hẹp khoảng ngày.</param>
/// <param name="Limit">Số dòng tối đa.</param>
public sealed record InventoryDocumentListResult(IReadOnlyList<InventoryDocumentListItemDto> Items, bool Truncated, int Limit);

/// <summary>Điều kiện tìm chứng từ kho.</summary>
/// <param name="DocType">Loại chứng từ (bắt buộc).</param>
/// <param name="From">Từ ngày (mặc định 90 ngày trước).</param>
/// <param name="To">Đến ngày (mặc định hôm nay).</param>
/// <param name="Status">Trạng thái.</param>
public sealed record InventoryDocumentQuery(string DocType, DateOnly? From, DateOnly? To, string? Status);

/// <summary>Một dòng chứng từ.</summary>
/// <param name="Id">Id dòng.</param>
/// <param name="LineNo">Số thứ tự.</param>
/// <param name="ItemId">Mặt hàng.</param>
/// <param name="ItemCode">Mã hàng ("(ngoài phạm vi)" nếu người xem không được xem nhóm hàng đó).</param>
/// <param name="ItemName">Tên hàng.</param>
/// <param name="UomId">ĐVT trên phiếu.</param>
/// <param name="UomCode">Mã ĐVT trên phiếu.</param>
/// <param name="Qty">Số lượng.</param>
/// <param name="UomFactor">Hệ số về ĐVT cơ sở.</param>
/// <param name="BaseQty">Số lượng theo ĐVT cơ sở.</param>
/// <param name="BaseUomCode">Mã ĐVT cơ sở.</param>
/// <param name="UnitPrice">Đơn giá trước VAT.</param>
/// <param name="Amount">Thành tiền trước VAT.</param>
/// <param name="VatCode">Thuế suất.</param>
/// <param name="VatAmount">Tiền VAT.</param>
/// <param name="LotNo">Số lô.</param>
/// <param name="ExpiryDate">Hạn dùng.</param>
/// <param name="Note">Ghi chú dòng.</param>
public sealed record InventoryLineDto(
    Guid Id, int LineNo, Guid ItemId, string ItemCode, string ItemName, Guid UomId, string UomCode, decimal Qty, decimal UomFactor,
    decimal BaseQty, string BaseUomCode, decimal UnitPrice, decimal Amount, string? VatCode, decimal VatAmount, string? LotNo,
    DateOnly? ExpiryDate, string? Note);

/// <summary>Chi tiết chứng từ kho.</summary>
/// <param name="Id">Id.</param>
/// <param name="DocType">Loại chứng từ.</param>
/// <param name="No">Số phiếu.</param>
/// <param name="Date">Ngày chứng từ.</param>
/// <param name="Status">Draft, Posted, Cancelled.</param>
/// <param name="FromWarehouseId">Kho xuất.</param>
/// <param name="ToWarehouseId">Kho nhập.</param>
/// <param name="SupplierId">NCC.</param>
/// <param name="SupplierCode">Mã NCC.</param>
/// <param name="SupplierName">Tên NCC.</param>
/// <param name="InvoiceNo">Số hóa đơn.</param>
/// <param name="InvoiceSeries">Ký hiệu hóa đơn.</param>
/// <param name="InvoiceDate">Ngày hóa đơn.</param>
/// <param name="DeliveryNoteNo">Số phiếu giao hàng.</param>
/// <param name="Deliverer">Người giao.</param>
/// <param name="Note">Ghi chú.</param>
/// <param name="TotalAmount">Tiền hàng trước VAT.</param>
/// <param name="TotalVat">Tiền VAT.</param>
/// <param name="CreatedAt">Thời điểm lập.</param>
/// <param name="CreatedByName">Người lập.</param>
/// <param name="PostedAt">Thời điểm ghi sổ.</param>
/// <param name="Lines">Các dòng.</param>
/// <param name="CanEdit">Người xem sửa được (nháp, có quyền lập, thấy hết mặt hàng trên phiếu).</param>
/// <param name="ReadOnlyReason">Lý do chỉ xem được (null nếu sửa được hoặc chỉ vì trạng thái).</param>
public sealed record InventoryDocumentDto(
    Guid Id, string DocType, string No, DateOnly Date, string Status, Guid? FromWarehouseId, Guid? ToWarehouseId, Guid? SupplierId,
    string? SupplierCode, string? SupplierName, string? InvoiceNo, string? InvoiceSeries, DateOnly? InvoiceDate, string? DeliveryNoteNo,
    string? Deliverer, string? Note, decimal TotalAmount, decimal TotalVat, DateTimeOffset CreatedAt, string? CreatedByName,
    DateTimeOffset? PostedAt, IReadOnlyList<InventoryLineDto> Lines, bool CanEdit, string? ReadOnlyReason);

/// <summary>Một dòng gửi lên khi lưu; Id trống = dòng mới.</summary>
/// <param name="Id">Id dòng đã có.</param>
/// <param name="ItemId">Mặt hàng.</param>
/// <param name="UomId">ĐVT trên phiếu.</param>
/// <param name="Qty">Số lượng.</param>
/// <param name="UnitPrice">Đơn giá trước VAT.</param>
/// <param name="VatCode">Thuế suất.</param>
/// <param name="LotNo">Số lô.</param>
/// <param name="ExpiryDate">Hạn dùng.</param>
/// <param name="Note">Ghi chú dòng.</param>
public sealed record SaveInventoryLineRequest(
    Guid? Id, Guid ItemId, Guid UomId, decimal Qty, decimal UnitPrice, string? VatCode, string? LotNo, DateOnly? ExpiryDate, string? Note);

/// <summary>Lưu phiếu nhập mua NCC (nháp).</summary>
/// <param name="Date">Ngày nhập.</param>
/// <param name="WarehouseId">Kho nhập.</param>
/// <param name="SupplierId">NCC.</param>
/// <param name="InvoiceNo">Số hóa đơn.</param>
/// <param name="InvoiceSeries">Ký hiệu hóa đơn.</param>
/// <param name="InvoiceDate">Ngày hóa đơn.</param>
/// <param name="DeliveryNoteNo">Số phiếu giao hàng.</param>
/// <param name="Deliverer">Người giao.</param>
/// <param name="Note">Ghi chú.</param>
/// <param name="Lines">Toàn bộ dòng.</param>
public sealed record SaveReceiptRequest(
    DateOnly Date, Guid WarehouseId, Guid SupplierId, string? InvoiceNo, string? InvoiceSeries, DateOnly? InvoiceDate,
    string? DeliveryNoteNo, string? Deliverer, string? Note, IReadOnlyList<SaveInventoryLineRequest> Lines);

/// <summary>
/// Chứng từ kho của công ty đang làm việc (design.md mục 6, 10.1, 10.4). Thấy phiếu của kho được phép (global query filter);
/// lập, sửa phiếu nháp; số phiếu cấp khi lưu lần đầu. Ghi sổ làm ở lát sau.
/// </summary>
public interface IInventoryDocumentService
{
    /// <summary>Tìm chứng từ.</summary>
    /// <param name="query">Điều kiện.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Mới nhất trước, tối đa <see cref="InventoryDocumentListResult.Limit"/> dòng.</returns>
    Task<InventoryDocumentListResult> SearchAsync(InventoryDocumentQuery query, CancellationToken cancellationToken);

    /// <summary>Chi tiết chứng từ.</summary>
    /// <param name="id">Id.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Chi tiết.</returns>
    /// <exception cref="Common.NotFoundException">Khi không có hoặc ngoài phạm vi kho.</exception>
    Task<InventoryDocumentDto> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Lập phiếu nhập mua NCC (nháp), cấp số phiếu.</summary>
    /// <param name="request">Nội dung phiếu.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Phiếu vừa lập.</returns>
    /// <exception cref="Domain.Common.BusinessRuleException">Khi NCC chưa duyệt/chưa áp dụng tại công ty, kho ngừng dùng, dòng sai.</exception>
    /// <exception cref="Common.NotFoundException">Khi kho, NCC, mặt hàng không có hoặc ngoài phạm vi.</exception>
    Task<InventoryDocumentDto> CreateReceiptAsync(SaveReceiptRequest request, CancellationToken cancellationToken);

    /// <summary>Sửa phiếu nhập mua NCC còn nháp.</summary>
    /// <param name="id">Id phiếu.</param>
    /// <param name="request">Nội dung mới.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Phiếu sau khi sửa.</returns>
    /// <exception cref="Domain.Common.BusinessRuleException">Khi phiếu không còn nháp hoặc dữ liệu sai.</exception>
    /// <exception cref="Common.ForbiddenAccessException">Khi phiếu có mặt hàng ngoài phạm vi nhóm hàng của người sửa.</exception>
    /// <exception cref="Common.NotFoundException">Khi không có phiếu.</exception>
    Task<InventoryDocumentDto> UpdateReceiptAsync(Guid id, SaveReceiptRequest request, CancellationToken cancellationToken);

    /// <summary>Hủy phiếu nháp (giữ số phiếu).</summary>
    /// <param name="id">Id phiếu.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Phiếu sau khi hủy.</returns>
    /// <exception cref="Domain.Common.BusinessRuleException">Khi phiếu không còn nháp.</exception>
    /// <exception cref="Common.NotFoundException">Khi không có phiếu.</exception>
    Task<InventoryDocumentDto> CancelAsync(Guid id, CancellationToken cancellationToken);
}
