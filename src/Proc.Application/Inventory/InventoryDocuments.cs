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
/// <param name="FromWarehouseCode">Mã kho xuất (phiếu chuyển kho; phiếu trả NCC thì trùng kho ở cột Kho).</param>
/// <param name="ToWarehouseCode">Mã kho nhập (phiếu chuyển kho).</param>
/// <param name="CounterCompanyCode">Mã công ty đối ứng (phiếu bán/nhập nội bộ).</param>
/// <param name="ReceiveStatus">Trạng thái nhận hàng bán nội bộ (Pending, Full, Short); hiện chưa tính ở danh sách.</param>
public sealed record InventoryDocumentListItemDto(
    Guid Id, string DocType, string No, DateOnly Date, string Status, string? WarehouseCode, string? WarehouseName,
    string? SupplierCode, string? SupplierName, string? InvoiceNo, decimal TotalAmount, decimal TotalVat, int LineCount,
    string? FromWarehouseCode = null, string? ToWarehouseCode = null, string? CounterCompanyCode = null, string? ReceiveStatus = null);

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
/// <param name="SourceLineId">Dòng chứng từ gốc (phiếu trả: dòng phiếu nhập).</param>
/// <param name="SentQty">SL gửi theo dòng phiếu xuất bán (chỉ phiếu nhập mua nội bộ).</param>
public sealed record InventoryLineDto(
    Guid Id, int LineNo, Guid ItemId, string ItemCode, string ItemName, Guid UomId, string UomCode, decimal Qty, decimal UomFactor,
    decimal BaseQty, string BaseUomCode, decimal UnitPrice, decimal Amount, string? VatCode, decimal VatAmount, string? LotNo,
    DateOnly? ExpiryDate, string? Note, Guid? SourceLineId = null, decimal? SentQty = null);

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
/// <param name="CanPost">Người xem ghi sổ được (phiếu nháp, có quyền ghi sổ, thấy hết mặt hàng).</param>
/// <param name="CanUnpost">Người xem bỏ ghi sổ được (đã ghi sổ, có quyền bỏ ghi sổ; kỳ còn mở kiểm tra khi bấm).</param>
/// <param name="Receiver">Người nhận (phiếu chuyển kho).</param>
/// <param name="LinkedDocumentId">Chứng từ liên kết (phiếu trả: phiếu nhập gốc).</param>
/// <param name="LinkedDocumentNo">Số chứng từ liên kết.</param>
/// <param name="CounterCompanyId">Công ty đối ứng (ICS: công ty mua; ICR: công ty bán).</param>
/// <param name="CounterCompanyCode">Mã công ty đối ứng.</param>
/// <param name="CounterCompanyName">Tên công ty đối ứng.</param>
/// <param name="ReceiveStatus">Trạng thái nhận hàng bán nội bộ: Pending (chờ nhận), Full (đã nhận đủ), Short (nhận thiếu).</param>
public sealed record InventoryDocumentDto(
    Guid Id, string DocType, string No, DateOnly Date, string Status, Guid? FromWarehouseId, Guid? ToWarehouseId, Guid? SupplierId,
    string? SupplierCode, string? SupplierName, string? InvoiceNo, string? InvoiceSeries, DateOnly? InvoiceDate, string? DeliveryNoteNo,
    string? Deliverer, string? Note, decimal TotalAmount, decimal TotalVat, DateTimeOffset CreatedAt, string? CreatedByName,
    DateTimeOffset? PostedAt, IReadOnlyList<InventoryLineDto> Lines, bool CanEdit, string? ReadOnlyReason, bool CanPost, bool CanUnpost,
    string? Receiver = null, Guid? LinkedDocumentId = null, string? LinkedDocumentNo = null,
    Guid? CounterCompanyId = null, string? CounterCompanyCode = null, string? CounterCompanyName = null, string? ReceiveStatus = null);

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

/// <summary>Một dòng trả NCC gửi lên khi lưu: chỉ chọn dòng nhập gốc và số lượng trả; hàng, ĐVT, lô, giá lấy theo dòng gốc.</summary>
/// <param name="Id">Id dòng đã có.</param>
/// <param name="SourceLineId">Dòng của phiếu nhập gốc.</param>
/// <param name="Qty">Số lượng trả theo ĐVT của dòng gốc.</param>
/// <param name="Note">Ghi chú dòng.</param>
public sealed record SaveReturnLineRequest(Guid? Id, Guid SourceLineId, decimal Qty, string? Note);

/// <summary>Lưu phiếu xuất trả NCC (nháp) – design.md mục 10.5.</summary>
/// <param name="Date">Ngày xuất trả.</param>
/// <param name="SourceDocumentId">Phiếu nhập gốc (đã ghi sổ).</param>
/// <param name="InvoiceNo">Số hóa đơn điều chỉnh.</param>
/// <param name="Note">Lý do trả.</param>
/// <param name="Lines">Toàn bộ dòng.</param>
public sealed record SaveReturnRequest(DateOnly Date, Guid SourceDocumentId, string? InvoiceNo, string? Note, IReadOnlyList<SaveReturnLineRequest> Lines);

/// <summary>Một dòng chuyển kho gửi lên khi lưu (không có đơn giá – design.md mục 10.6).</summary>
/// <param name="Id">Id dòng đã có.</param>
/// <param name="ItemId">Mặt hàng.</param>
/// <param name="UomId">ĐVT.</param>
/// <param name="Qty">Số lượng.</param>
/// <param name="LotNo">Số lô.</param>
/// <param name="Note">Ghi chú dòng.</param>
public sealed record SaveTransferLineRequest(Guid? Id, Guid ItemId, Guid UomId, decimal Qty, string? LotNo, string? Note);

/// <summary>Lưu phiếu chuyển kho nội bộ (nháp).</summary>
/// <param name="Date">Ngày chuyển.</param>
/// <param name="FromWarehouseId">Kho xuất.</param>
/// <param name="ToWarehouseId">Kho nhập.</param>
/// <param name="Deliverer">Người giao.</param>
/// <param name="Receiver">Người nhận.</param>
/// <param name="Note">Lý do chuyển.</param>
/// <param name="Lines">Toàn bộ dòng.</param>
public sealed record SaveTransferRequest(
    DateOnly Date, Guid FromWarehouseId, Guid ToWarehouseId, string? Deliverer, string? Receiver, string? Note,
    IReadOnlyList<SaveTransferLineRequest> Lines);

/// <summary>Một dòng phiếu nhập còn trả được (để lập phiếu trả NCC).</summary>
/// <param name="SourceLineId">Dòng phiếu nhập.</param>
/// <param name="LineNo">Số thứ tự dòng trên phiếu nhập.</param>
/// <param name="ItemId">Mặt hàng.</param>
/// <param name="ItemCode">Mã hàng.</param>
/// <param name="ItemName">Tên hàng.</param>
/// <param name="UomId">ĐVT của dòng nhập.</param>
/// <param name="UomCode">Mã ĐVT.</param>
/// <param name="LotNo">Số lô.</param>
/// <param name="UnitPrice">Đơn giá nhập gốc.</param>
/// <param name="VatCode">Thuế suất.</param>
/// <param name="ReceivedQty">Số đã nhập.</param>
/// <param name="ReturnedQty">Số đã trả (các phiếu trả đã ghi sổ, không tính phiếu đang sửa).</param>
/// <param name="RemainingQty">Còn trả được = đã nhập − đã trả.</param>
public sealed record ReturnableLineDto(
    Guid SourceLineId, int LineNo, Guid ItemId, string ItemCode, string ItemName, Guid UomId, string UomCode, string? LotNo,
    decimal UnitPrice, string? VatCode, decimal ReceivedQty, decimal ReturnedQty, decimal RemainingQty);

/// <summary>Tồn tức thời một mặt hàng (một lô) tại một kho.</summary>
/// <param name="ItemId">Mặt hàng.</param>
/// <param name="LotNo">Số lô.</param>
/// <param name="Qty">Số tồn theo ĐVT cơ sở.</param>
public sealed record StockAvailabilityDto(Guid ItemId, string? LotNo, decimal Qty);

/// <summary>Một dòng phiếu xuất bán nội bộ gửi lên: giá, thuế suất lấy từ bảng giá nội bộ ở server, không nhận từ client.</summary>
public sealed record SaveSaleLineRequest(Guid? Id, Guid ItemId, Guid UomId, decimal Qty, string? LotNo, string? Note);

/// <summary>Nội dung phiếu xuất bán nội bộ (design.md mục 10.7).</summary>
public sealed record SaveSaleRequest(
    DateOnly Date, Guid FromWarehouseId, Guid BuyerCompanyId, string? InvoiceNo, DateOnly? InvoiceDate, string? Deliverer, string? Note,
    IReadOnlyList<SaveSaleLineRequest> Lines);

/// <summary>SL thực nhận một dòng phiếu nhập mua nội bộ.</summary>
public sealed record ConfirmReceiptLineRequest(Guid LineId, decimal ReceivedQty, string? LotNo, string? Note);

/// <summary>Bên nhận lưu xác nhận phiếu nhập mua nội bộ (design.md mục 10.8) – ghi sổ bằng lệnh ghi sổ chung.</summary>
public sealed record ConfirmIntercompanyReceiptRequest(DateOnly Date, string? Note, IReadOnlyList<ConfirmReceiptLineRequest> Lines);

/// <summary>Một phiếu bán nội bộ đang là hàng đi đường (đã xuất, bên mua chưa xác nhận).</summary>
/// <param name="Direction">Out = công ty đang làm việc bán ra; In = công ty khác bán cho công ty đang làm việc.</param>
/// <param name="SaleId">Phiếu xuất bán.</param>
/// <param name="SaleNo">Số phiếu xuất bán.</param>
/// <param name="SaleDate">Ngày xuất.</param>
/// <param name="SellerCode">Mã công ty bán.</param>
/// <param name="BuyerCode">Mã công ty mua.</param>
/// <param name="ReceiptId">Phiếu nhập mua nội bộ chờ nhận (nếu có).</param>
/// <param name="ReceiptNo">Số phiếu nhập chờ nhận.</param>
/// <param name="TotalAmount">Tiền hàng trước VAT.</param>
/// <param name="TotalVat">Tiền VAT.</param>
public sealed record GoodsInTransitDto(
    string Direction, Guid SaleId, string SaleNo, DateOnly SaleDate, string SellerCode, string BuyerCode, Guid? ReceiptId, string? ReceiptNo,
    decimal TotalAmount, decimal TotalVat);

/// <summary>
/// Đối chiếu mua bán nội bộ với một công ty (design.md mục 11 – nhóm Nội bộ): bên này bán – bên kia đã nhận; bên kia bán – bên này
/// đã nhận. Chênh lệch = hàng đi đường + nhận thiếu.
/// </summary>
public sealed record IntercompanyReconciliationDto(
    Guid CounterCompanyId, string CounterCompanyCode, string CounterCompanyName,
    decimal SoldAmount, decimal ReceivedByCounterAmount, decimal SoldByCounterAmount, decimal PurchasedAmount);

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

    /// <summary>
    /// Ghi sổ phiếu nháp trong một giao dịch (design.md mục 12.2): kiểm tra kỳ mở và lại các điều kiện của phiếu → ghi sổ kho →
    /// cập nhật tồn (khóa dòng tồn) → đổi trạng thái → nhật ký.
    /// </summary>
    /// <param name="id">Id phiếu.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Phiếu sau khi ghi sổ.</returns>
    /// <exception cref="Domain.Common.BusinessRuleException">Khi kỳ không mở, phiếu không còn nháp, điều kiện phiếu không còn đúng, tồn âm.</exception>
    /// <exception cref="Common.ForbiddenAccessException">Khi phiếu có mặt hàng ngoài phạm vi nhóm hàng của người ghi sổ.</exception>
    /// <exception cref="Common.NotFoundException">Khi không có phiếu.</exception>
    Task<InventoryDocumentDto> PostAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Bỏ ghi sổ khi kỳ của phiếu còn mở (CLAUDE.md mục 4): xóa dòng sổ kho của phiếu, trả lại tồn, phiếu về nháp.
    /// Không cho bỏ ghi sổ nếu làm tồn kho âm (trừ kho cho phép tồn âm) – hàng đã xuất dùng rồi.
    /// </summary>
    /// <param name="id">Id phiếu.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Phiếu sau khi bỏ ghi sổ.</returns>
    /// <exception cref="Domain.Common.BusinessRuleException">Khi phiếu chưa ghi sổ, kỳ đã đóng hoặc tồn sẽ âm.</exception>
    /// <exception cref="Common.ForbiddenAccessException">Khi phiếu có mặt hàng ngoài phạm vi nhóm hàng.</exception>
    /// <exception cref="Common.NotFoundException">Khi không có phiếu.</exception>
    Task<InventoryDocumentDto> UnpostAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Các dòng của phiếu nhập đã ghi sổ kèm số còn trả được.</summary>
    /// <param name="receiptId">Phiếu nhập.</param>
    /// <param name="excludeDocumentId">Phiếu trả đang sửa (không tính vào số đã trả).</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Theo số thứ tự dòng.</returns>
    /// <exception cref="Common.NotFoundException">Khi không có phiếu nhập hoặc ngoài phạm vi.</exception>
    Task<IReadOnlyList<ReturnableLineDto>> GetReturnableLinesAsync(Guid receiptId, Guid? excludeDocumentId, CancellationToken cancellationToken);

    /// <summary>Lập phiếu xuất trả NCC (nháp).</summary>
    /// <param name="request">Nội dung phiếu.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Phiếu vừa lập.</returns>
    /// <exception cref="Domain.Common.BusinessRuleException">Khi phiếu gốc chưa ghi sổ, số trả vượt số còn trả được.</exception>
    /// <exception cref="Common.NotFoundException">Khi phiếu gốc, dòng gốc không có hoặc ngoài phạm vi.</exception>
    Task<InventoryDocumentDto> CreateReturnAsync(SaveReturnRequest request, CancellationToken cancellationToken);

    /// <summary>Sửa phiếu xuất trả NCC còn nháp.</summary>
    /// <param name="id">Id phiếu.</param>
    /// <param name="request">Nội dung mới.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Phiếu sau khi sửa.</returns>
    /// <exception cref="Domain.Common.BusinessRuleException">Khi phiếu không còn nháp hoặc dữ liệu sai.</exception>
    /// <exception cref="Common.NotFoundException">Khi không có phiếu.</exception>
    Task<InventoryDocumentDto> UpdateReturnAsync(Guid id, SaveReturnRequest request, CancellationToken cancellationToken);

    /// <summary>Lập phiếu chuyển kho nội bộ (nháp).</summary>
    /// <param name="request">Nội dung phiếu.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Phiếu vừa lập.</returns>
    /// <exception cref="Domain.Common.BusinessRuleException">Khi kho trùng nhau, kho ngừng dùng, dòng sai.</exception>
    /// <exception cref="Common.NotFoundException">Khi kho, mặt hàng không có hoặc ngoài phạm vi.</exception>
    Task<InventoryDocumentDto> CreateTransferAsync(SaveTransferRequest request, CancellationToken cancellationToken);

    /// <summary>Sửa phiếu chuyển kho còn nháp.</summary>
    /// <param name="id">Id phiếu.</param>
    /// <param name="request">Nội dung mới.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Phiếu sau khi sửa.</returns>
    /// <exception cref="Domain.Common.BusinessRuleException">Khi phiếu không còn nháp hoặc dữ liệu sai.</exception>
    /// <exception cref="Common.NotFoundException">Khi không có phiếu.</exception>
    Task<InventoryDocumentDto> UpdateTransferAsync(Guid id, SaveTransferRequest request, CancellationToken cancellationToken);

    /// <summary>Lập phiếu xuất bán nội bộ nháp; giá theo bảng giá nội bộ của công ty bán.</summary>
    /// <param name="request">Nội dung.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Phiếu đã lưu.</returns>
    /// <exception cref="Domain.Common.BusinessRuleException">Khi kho không phải kho tổng, chưa có giá nội bộ…</exception>
    Task<InventoryDocumentDto> CreateSaleAsync(SaveSaleRequest request, CancellationToken cancellationToken);

    /// <summary>Sửa phiếu xuất bán nội bộ nháp.</summary>
    /// <param name="id">Id phiếu.</param>
    /// <param name="request">Nội dung.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Phiếu sau khi sửa.</returns>
    Task<InventoryDocumentDto> UpdateSaleAsync(Guid id, SaveSaleRequest request, CancellationToken cancellationToken);

    /// <summary>Bên nhận lưu ngày nhận, SL thực nhận, lô, ghi chú của phiếu nhập mua nội bộ chờ nhận.</summary>
    /// <param name="id">Id phiếu nhập mua nội bộ.</param>
    /// <param name="request">Nội dung xác nhận.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Phiếu sau khi lưu.</returns>
    Task<InventoryDocumentDto> ConfirmIntercompanyReceiptAsync(Guid id, ConfirmIntercompanyReceiptRequest request, CancellationToken cancellationToken);

    /// <summary>Hàng đi đường của công ty đang làm việc: phiếu bán ra chưa được nhận và phiếu mua vào đang chờ nhận.</summary>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Danh sách theo ngày xuất.</returns>
    Task<IReadOnlyList<GoodsInTransitDto>> GetGoodsInTransitAsync(CancellationToken cancellationToken);

    /// <summary>Đối chiếu mua bán nội bộ hai chiều với từng công ty trong khoảng ngày (theo ngày phiếu xuất bán).</summary>
    /// <param name="from">Từ ngày.</param>
    /// <param name="to">Đến ngày.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Một dòng mỗi công ty có giao dịch.</returns>
    Task<IReadOnlyList<IntercompanyReconciliationDto>> GetReconciliationAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken);

    /// <summary>Tồn tức thời của các mặt hàng tại một kho (hiện "tồn khả dụng" trên dòng phiếu xuất/chuyển).</summary>
    /// <param name="warehouseId">Kho.</param>
    /// <param name="itemIds">Mặt hàng.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Theo mặt hàng, lô.</returns>
    Task<IReadOnlyList<StockAvailabilityDto>> GetAvailableStockAsync(Guid warehouseId, IReadOnlyList<Guid> itemIds, CancellationToken cancellationToken);
}
