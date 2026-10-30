using Proc.Domain.Common;
using Proc.Domain.Documents;
using Proc.Domain.MasterData;

namespace Proc.Domain.Inventory;

/// <summary>Trạng thái chứng từ kho ở giai đoạn 2 (design.md mục 10.1; bước duyệt thêm cùng giai đoạn mua hàng).</summary>
public enum DocumentStatus
{
    /// <summary>Nháp: sửa được, chưa ảnh hưởng tồn kho.</summary>
    Draft = 1,

    /// <summary>Đã ghi sổ: đã sinh dòng sổ kho, không sửa trực tiếp.</summary>
    Posted = 2,

    /// <summary>Đã hủy: phiếu nháp bị hủy, giữ lại số phiếu và lịch sử.</summary>
    Cancelled = 3,
}

/// <summary>Thông tin đầu phiếu nhập mua NCC (design.md mục 10.4), gom lại để hàm sửa không có quá nhiều tham số.</summary>
/// <param name="Date">Ngày nhập – quyết định kỳ kho.</param>
/// <param name="WarehouseId">Kho nhập.</param>
/// <param name="SupplierId">Nhà cung cấp.</param>
/// <param name="InvoiceNo">Số hóa đơn.</param>
/// <param name="InvoiceSeries">Ký hiệu hóa đơn.</param>
/// <param name="InvoiceDate">Ngày hóa đơn.</param>
/// <param name="DeliveryNoteNo">Số phiếu giao hàng.</param>
/// <param name="Deliverer">Người giao.</param>
/// <param name="Note">Ghi chú.</param>
public sealed record ReceiptHeader(
    DateOnly Date, Guid WarehouseId, Guid SupplierId, string? InvoiceNo, string? InvoiceSeries, DateOnly? InvoiceDate,
    string? DeliveryNoteNo, string? Deliverer, string? Note);

/// <summary>Đầu phiếu xuất trả NCC (design.md mục 10.5): kho xuất, NCC lấy theo phiếu nhập gốc.</summary>
/// <param name="Date">Ngày xuất trả.</param>
/// <param name="Source">Phiếu nhập gốc (đã ghi sổ).</param>
/// <param name="InvoiceNo">Số hóa đơn điều chỉnh.</param>
/// <param name="Note">Lý do trả.</param>
public sealed record ReturnHeader(DateOnly Date, InventoryDocument Source, string? InvoiceNo, string? Note);

/// <summary>Đầu phiếu chuyển kho nội bộ (design.md mục 10.6).</summary>
/// <param name="Date">Ngày chuyển.</param>
/// <param name="FromWarehouseId">Kho xuất.</param>
/// <param name="ToWarehouseId">Kho nhập (cùng công ty – CSDL chặn kho công ty khác).</param>
/// <param name="Deliverer">Người giao.</param>
/// <param name="Receiver">Người nhận.</param>
/// <param name="Note">Lý do chuyển.</param>
public sealed record TransferHeader(DateOnly Date, Guid FromWarehouseId, Guid ToWarehouseId, string? Deliverer, string? Receiver, string? Note);

/// <summary>Đầu phiếu xuất bán nội bộ (design.md mục 10.7).</summary>
/// <param name="Date">Ngày xuất.</param>
/// <param name="FromWarehouseId">Kho xuất – phải là kho tổng của công ty bán (tầng Infrastructure kiểm tra loại kho).</param>
/// <param name="BuyerCompanyId">Công ty mua.</param>
/// <param name="InvoiceNo">Số hóa đơn.</param>
/// <param name="InvoiceDate">Ngày hóa đơn.</param>
/// <param name="Deliverer">Người giao, phương tiện vận chuyển.</param>
/// <param name="Note">Ghi chú.</param>
public sealed record SaleHeader(
    DateOnly Date, Guid FromWarehouseId, Guid BuyerCompanyId, string? InvoiceNo, DateOnly? InvoiceDate, string? Deliverer, string? Note);

/// <summary>Bên nhận xác nhận một dòng phiếu nhập mua nội bộ.</summary>
/// <param name="LineId">Dòng phiếu nhập mua nội bộ.</param>
/// <param name="ReceivedQty">SL thực nhận theo ĐVT trên phiếu (0 = không nhận được dòng này).</param>
/// <param name="LotNo">Số lô.</param>
/// <param name="Note">Ghi chú chênh lệch.</param>
public sealed record ReceiptConfirmLine(Guid LineId, decimal ReceivedQty, string? LotNo, string? Note);

/// <summary>Một dòng gửi vào khi lưu; Id trống = dòng mới.</summary>
/// <param name="Id">Id dòng đã có.</param>
/// <param name="ItemId">Mặt hàng.</param>
/// <param name="UomId">ĐVT trên phiếu (ĐVT cơ sở hoặc ĐVT quy đổi).</param>
/// <param name="Qty">Số lượng theo ĐVT trên phiếu.</param>
/// <param name="UnitPrice">Đơn giá trước VAT theo ĐVT trên phiếu (phiếu chuyển kho: 0 – giá vốn ghi ở sổ kho).</param>
/// <param name="VatCode">Thuế suất (KCT, 0, 5, 8, 10).</param>
/// <param name="LotNo">Số lô.</param>
/// <param name="ExpiryDate">Hạn dùng.</param>
/// <param name="Note">Ghi chú dòng.</param>
/// <param name="SourceLineId">Dòng chứng từ gốc (phiếu trả NCC: dòng phiếu nhập).</param>
public sealed record InventoryLineData(
    Guid? Id, Guid ItemId, Guid UomId, decimal Qty, decimal UnitPrice, string? VatCode, string? LotNo, DateOnly? ExpiryDate, string? Note,
    Guid? SourceLineId = null);

/// <summary>
/// Chứng từ kho (design.md mục 6.1, 12.1): một bảng cho mọi loại phiếu (GRN, RTV, TRF…), phân biệt bằng DocType.
/// Hiện có phiếu nhập mua NCC (GRN), xuất trả NCC (RTV), chuyển kho nội bộ (TRF).
/// </summary>
[Auditable("Chứng từ kho", LabelProperty = nameof(No))]
public sealed class InventoryDocument : AuditableEntity, ICompanyOwned, IHasDocumentNumber
{
    private readonly List<InventoryDocumentLine> _lines = [];

    private InventoryDocument()
    {
    }

    /// <inheritdoc />
    public Guid CompanyId { get; private set; }

    /// <summary>Loại chứng từ (<see cref="DocumentTypes"/>).</summary>
    public string DocType { get; private set; } = string.Empty;

    /// <summary>Số chứng từ, cấp khi lưu lần đầu (design.md mục 5.5).</summary>
    public string No { get; private set; } = string.Empty;

    /// <inheritdoc />
    public string? DocumentNumber => No;

    /// <summary>Ngày chứng từ – quyết định kỳ kho khi ghi sổ.</summary>
    public DateOnly Date { get; private set; }

    /// <summary>Trạng thái.</summary>
    public DocumentStatus Status { get; private set; } = DocumentStatus.Draft;

    /// <summary>Kho xuất (RTV, TRF).</summary>
    public Guid? FromWarehouseId { get; private set; }

    /// <summary>Kho nhập (GRN, TRF).</summary>
    public Guid? ToWarehouseId { get; private set; }

    /// <summary>Nhà cung cấp (GRN, RTV).</summary>
    public Guid? SupplierId { get; private set; }

    /// <summary>Số hóa đơn.</summary>
    public string? InvoiceNo { get; private set; }

    /// <summary>Ký hiệu hóa đơn.</summary>
    public string? InvoiceSeries { get; private set; }

    /// <summary>Ngày hóa đơn.</summary>
    public DateOnly? InvoiceDate { get; private set; }

    /// <summary>Số phiếu giao hàng của NCC.</summary>
    public string? DeliveryNoteNo { get; private set; }

    /// <summary>Người giao hàng.</summary>
    public string? Deliverer { get; private set; }

    /// <summary>Người nhận hàng (phiếu chuyển kho).</summary>
    public string? Receiver { get; private set; }

    /// <summary>Chứng từ liên kết (phiếu trả NCC: phiếu nhập gốc; ICS ↔ ICR: phiếu bên kia).</summary>
    public Guid? LinkedDocumentId { get; private set; }

    /// <summary>Công ty đối ứng (ICS: công ty mua; ICR: công ty bán).</summary>
    public Guid? CounterCompanyId { get; private set; }

    /// <summary>Ghi chú.</summary>
    public string? Note { get; private set; }

    /// <summary>Kỳ kho đã ghi sổ (gán khi ghi sổ).</summary>
    public Guid? PeriodId { get; private set; }

    /// <summary>Thời điểm ghi sổ (UTC).</summary>
    public DateTimeOffset? PostedAt { get; private set; }

    /// <summary>Người ghi sổ.</summary>
    public Guid? PostedBy { get; private set; }

    /// <summary>Tổng tiền hàng trước VAT – numeric(19,2).</summary>
    public decimal TotalAmount { get; private set; }

    /// <summary>Tổng tiền VAT – numeric(19,2).</summary>
    public decimal TotalVat { get; private set; }

    /// <summary>Các dòng.</summary>
    public IReadOnlyList<InventoryDocumentLine> Lines => _lines;

    /// <summary>
    /// Lập phiếu nhập mua NCC ở trạng thái nháp.
    /// </summary>
    /// <param name="companyId">Công ty.</param>
    /// <param name="no">Số phiếu đã cấp.</param>
    /// <param name="header">Đầu phiếu.</param>
    /// <returns>Phiếu nháp.</returns>
    /// <exception cref="BusinessRuleException">Khi thiếu công ty, số phiếu, kho, NCC.</exception>
    public static InventoryDocument CreateReceipt(Guid companyId, string no, ReceiptHeader header)
    {
        if (companyId == Guid.Empty) throw new BusinessRuleException("Chứng từ kho phải thuộc một công ty.");
        if (string.IsNullOrWhiteSpace(no)) throw new BusinessRuleException("Chứng từ phải có số.");
        var document = new InventoryDocument { CompanyId = companyId, DocType = DocumentTypes.GoodsReceipt, No = no };
        document.UpdateReceipt(header);
        return document;
    }

    /// <summary>
    /// Sửa đầu phiếu nhập mua.
    /// </summary>
    /// <param name="header">Đầu phiếu mới.</param>
    /// <exception cref="BusinessRuleException">Khi phiếu không còn nháp, thiếu kho hoặc NCC.</exception>
    public void UpdateReceipt(ReceiptHeader header)
    {
        ArgumentNullException.ThrowIfNull(header);
        EnsureDraft();
        if (header.WarehouseId == Guid.Empty) throw new BusinessRuleException("Hãy chọn kho nhập.");
        if (header.SupplierId == Guid.Empty) throw new BusinessRuleException("Hãy chọn nhà cung cấp.");
        Date = header.Date;
        ToWarehouseId = header.WarehouseId;
        SupplierId = header.SupplierId;
        InvoiceNo = Clean(header.InvoiceNo);
        InvoiceSeries = Clean(header.InvoiceSeries);
        InvoiceDate = header.InvoiceDate;
        DeliveryNoteNo = Clean(header.DeliveryNoteNo);
        Deliverer = Clean(header.Deliverer);
        Note = Clean(header.Note);
    }

    /// <summary>
    /// Lập phiếu xuất trả NCC ở trạng thái nháp, gắn với phiếu nhập gốc.
    /// </summary>
    /// <param name="companyId">Công ty.</param>
    /// <param name="no">Số phiếu đã cấp.</param>
    /// <param name="header">Đầu phiếu.</param>
    /// <returns>Phiếu nháp.</returns>
    /// <exception cref="BusinessRuleException">Khi phiếu gốc không phải phiếu nhập đã ghi sổ.</exception>
    public static InventoryDocument CreateReturn(Guid companyId, string no, ReturnHeader header)
    {
        if (companyId == Guid.Empty) throw new BusinessRuleException("Chứng từ kho phải thuộc một công ty.");
        if (string.IsNullOrWhiteSpace(no)) throw new BusinessRuleException("Chứng từ phải có số.");
        var document = new InventoryDocument { CompanyId = companyId, DocType = DocumentTypes.ReturnToVendor, No = no };
        document.UpdateReturn(header);
        return document;
    }

    /// <summary>
    /// Sửa đầu phiếu xuất trả NCC: kho xuất = kho đã nhập, NCC = NCC của phiếu nhập gốc (design.md mục 6.1, 10.5).
    /// </summary>
    /// <param name="header">Đầu phiếu mới.</param>
    /// <exception cref="BusinessRuleException">Khi phiếu không còn nháp, phiếu gốc không phải phiếu nhập đã ghi sổ, khác công ty,
    /// hoặc ngày trả trước ngày nhập.</exception>
    public void UpdateReturn(ReturnHeader header)
    {
        ArgumentNullException.ThrowIfNull(header);
        EnsureDraft();
        var source = header.Source;
        if (source.DocType != DocumentTypes.GoodsReceipt || source.Status != DocumentStatus.Posted)
            throw new BusinessRuleException($"Phiếu gốc {source.No} phải là phiếu nhập mua đã ghi sổ.");
        if (source.CompanyId != CompanyId) throw new BusinessRuleException("Phiếu nhập gốc phải cùng công ty.");
        if (header.Date < source.Date)
            throw new BusinessRuleException($"Ngày trả hàng không được trước ngày nhập {source.Date:dd/MM/yyyy} của phiếu {source.No}.");
        Date = header.Date;
        LinkedDocumentId = source.Id;
        FromWarehouseId = source.ToWarehouseId;
        SupplierId = source.SupplierId;
        InvoiceNo = Clean(header.InvoiceNo);
        Note = Clean(header.Note);
    }

    /// <summary>
    /// Lập phiếu chuyển kho nội bộ ở trạng thái nháp.
    /// </summary>
    /// <param name="companyId">Công ty.</param>
    /// <param name="no">Số phiếu đã cấp.</param>
    /// <param name="header">Đầu phiếu.</param>
    /// <returns>Phiếu nháp.</returns>
    /// <exception cref="BusinessRuleException">Khi thiếu kho hoặc kho xuất trùng kho nhập.</exception>
    public static InventoryDocument CreateTransfer(Guid companyId, string no, TransferHeader header)
    {
        if (companyId == Guid.Empty) throw new BusinessRuleException("Chứng từ kho phải thuộc một công ty.");
        if (string.IsNullOrWhiteSpace(no)) throw new BusinessRuleException("Chứng từ phải có số.");
        var document = new InventoryDocument { CompanyId = companyId, DocType = DocumentTypes.Transfer, No = no };
        document.UpdateTransfer(header);
        return document;
    }

    /// <summary>
    /// Sửa đầu phiếu chuyển kho. Hai kho phải khác nhau; cùng công ty do khóa (kho, công ty) trong CSDL bảo đảm – kho Sub không
    /// chuyển sang công ty khác được (CLAUDE.md mục 4).
    /// </summary>
    /// <param name="header">Đầu phiếu mới.</param>
    /// <exception cref="BusinessRuleException">Khi phiếu không còn nháp, thiếu kho hoặc hai kho trùng nhau.</exception>
    public void UpdateTransfer(TransferHeader header)
    {
        ArgumentNullException.ThrowIfNull(header);
        EnsureDraft();
        if (header.FromWarehouseId == Guid.Empty) throw new BusinessRuleException("Hãy chọn kho xuất.");
        if (header.ToWarehouseId == Guid.Empty) throw new BusinessRuleException("Hãy chọn kho nhập.");
        if (header.FromWarehouseId == header.ToWarehouseId) throw new BusinessRuleException("Kho xuất và kho nhập phải khác nhau.");
        Date = header.Date;
        FromWarehouseId = header.FromWarehouseId;
        ToWarehouseId = header.ToWarehouseId;
        Deliverer = Clean(header.Deliverer);
        Receiver = Clean(header.Receiver);
        Note = Clean(header.Note);
    }

    /// <summary>
    /// Lập phiếu xuất bán nội bộ ở trạng thái nháp (design.md mục 6.3, 10.7).
    /// </summary>
    /// <param name="companyId">Công ty bán.</param>
    /// <param name="no">Số phiếu đã cấp.</param>
    /// <param name="header">Đầu phiếu.</param>
    /// <returns>Phiếu nháp.</returns>
    /// <exception cref="BusinessRuleException">Khi thiếu kho, công ty mua hoặc bán cho chính mình.</exception>
    public static InventoryDocument CreateSale(Guid companyId, string no, SaleHeader header)
    {
        if (companyId == Guid.Empty) throw new BusinessRuleException("Chứng từ kho phải thuộc một công ty.");
        if (string.IsNullOrWhiteSpace(no)) throw new BusinessRuleException("Chứng từ phải có số.");
        var document = new InventoryDocument { CompanyId = companyId, DocType = DocumentTypes.IntercompanySale, No = no };
        document.UpdateSale(header);
        return document;
    }

    /// <summary>
    /// Sửa đầu phiếu xuất bán nội bộ. Bán nội bộ là giữa hai pháp nhân khác nhau; trong một công ty dùng phiếu chuyển kho
    /// (design.md mục 4).
    /// </summary>
    /// <param name="header">Đầu phiếu mới.</param>
    /// <exception cref="BusinessRuleException">Khi phiếu không còn nháp, thiếu kho/công ty mua, hoặc công ty mua là chính mình.</exception>
    public void UpdateSale(SaleHeader header)
    {
        ArgumentNullException.ThrowIfNull(header);
        EnsureDraft();
        if (header.FromWarehouseId == Guid.Empty) throw new BusinessRuleException("Hãy chọn kho xuất.");
        if (header.BuyerCompanyId == Guid.Empty) throw new BusinessRuleException("Hãy chọn công ty mua.");
        if (header.BuyerCompanyId == CompanyId) throw new BusinessRuleException("Công ty mua phải khác công ty bán – trong một công ty dùng phiếu chuyển kho.");
        Date = header.Date;
        FromWarehouseId = header.FromWarehouseId;
        CounterCompanyId = header.BuyerCompanyId;
        InvoiceNo = Clean(header.InvoiceNo);
        InvoiceDate = header.InvoiceDate;
        Deliverer = Clean(header.Deliverer);
        Note = Clean(header.Note);
    }

    /// <summary>
    /// Sinh phiếu nhập mua nội bộ "Chờ nhận" tại công ty mua từ phiếu xuất bán đã ghi sổ (design.md mục 6.3 bước 3): kho nhận là kho
    /// tổng công ty mua, dòng chép từ phiếu bán (mặt hàng, ĐVT, SL gửi, giá bán nội bộ, thuế suất, lô, hạn dùng), liên kết hai chiều.
    /// </summary>
    /// <param name="no">Số phiếu cấp theo công ty mua.</param>
    /// <param name="buyerMainWarehouseId">Kho tổng công ty mua.</param>
    /// <returns>Phiếu nhập mua nội bộ ở trạng thái nháp (Chờ nhận).</returns>
    /// <exception cref="BusinessRuleException">Khi phiếu này không phải phiếu bán nội bộ đã ghi sổ.</exception>
    public InventoryDocument CreateIntercompanyReceipt(string no, Guid buyerMainWarehouseId)
    {
        if (DocType != DocumentTypes.IntercompanySale || Status != DocumentStatus.Posted || CounterCompanyId is null)
            throw new BusinessRuleException($"Phiếu {No} phải là phiếu xuất bán nội bộ đã ghi sổ.");
        var receipt = new InventoryDocument
        {
            CompanyId = CounterCompanyId.Value, DocType = DocumentTypes.IntercompanyReceipt, No = no, Date = Date,
            ToWarehouseId = buyerMainWarehouseId, CounterCompanyId = CompanyId, LinkedDocumentId = Id,
            InvoiceNo = InvoiceNo, InvoiceDate = InvoiceDate, Deliverer = Deliverer,
        };
        foreach (var line in _lines.OrderBy(l => l.LineNo)) receipt._lines.Add(line.CopyFor(receipt.Id));
        receipt.Recalculate();
        LinkedDocumentId = receipt.Id;
        return receipt;
    }

    /// <summary>
    /// Bên nhận xác nhận phiếu nhập mua nội bộ (design.md mục 10.8): ngày nhận, SL thực nhận (≤ SL gửi), lô, ghi chú chênh lệch.
    /// Đơn giá = giá bán nội bộ, không sửa. Dòng nhận 0 bị bỏ khỏi phiếu (chênh lệch thể hiện ở SL gửi − SL nhận).
    /// </summary>
    /// <param name="date">Ngày nhận (không trước ngày xuất của bên bán).</param>
    /// <param name="sentDate">Ngày xuất của phiếu bán.</param>
    /// <param name="note">Ghi chú/biên bản chênh lệch.</param>
    /// <param name="lines">SL thực nhận từng dòng.</param>
    /// <param name="sentQty">SL gửi theo dòng phiếu bán (Id dòng phiếu bán → SL).</param>
    /// <exception cref="BusinessRuleException">Khi phiếu không còn chờ nhận, nhận vượt SL gửi hoặc không nhận dòng nào.</exception>
    public void ConfirmIntercompanyReceipt(
        DateOnly date, DateOnly sentDate, string? note, IReadOnlyList<ReceiptConfirmLine> lines, IReadOnlyDictionary<Guid, decimal> sentQty)
    {
        EnsureDraft();
        if (DocType != DocumentTypes.IntercompanyReceipt) throw new BusinessRuleException($"Phiếu {No} không phải phiếu nhập mua nội bộ.");
        if (date < sentDate) throw new BusinessRuleException($"Ngày nhận không được trước ngày xuất {sentDate:dd/MM/yyyy} của bên bán.");
        foreach (var input in lines)
        {
            var line = _lines.FirstOrDefault(l => l.Id == input.LineId)
                ?? throw new BusinessRuleException("Không tìm thấy dòng cần xác nhận – hãy tải lại phiếu.");
            var sent = sentQty.GetValueOrDefault(line.SourceLineId ?? Guid.Empty);
            if (input.ReceivedQty < 0 || input.ReceivedQty > sent)
                throw new BusinessRuleException($"Dòng {line.LineNo}: SL thực nhận phải từ 0 đến SL gửi {sent:0.####}.");
            line.Receive(input.ReceivedQty, Clean(input.LotNo), Clean(input.Note));
        }

        if (_lines.All(l => l.Qty == 0)) throw new BusinessRuleException("Phải nhận ít nhất một dòng hàng.");
        Date = date;
        Note = Clean(note);
        Recalculate();
    }

    /// <summary>Bên bán bỏ ghi sổ phiếu xuất: phiếu nhập chờ nhận bên mua bị hủy (giữ số), gỡ liên kết.</summary>
    /// <param name="receipt">Phiếu nhập mua nội bộ liên kết.</param>
    /// <exception cref="BusinessRuleException">Khi bên mua đã xác nhận (đã ghi sổ) – design.md mục 6.3 bước 6.</exception>
    public void DetachIntercompanyReceipt(InventoryDocument receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        if (receipt.Status == DocumentStatus.Posted)
            throw new BusinessRuleException($"Công ty mua đã xác nhận nhận hàng ở phiếu {receipt.No} – không bỏ ghi sổ phiếu xuất bán được.");
        if (receipt.Status == DocumentStatus.Draft) receipt.Status = DocumentStatus.Cancelled;
        LinkedDocumentId = null;
    }

    /// <summary>
    /// Thay toàn bộ dòng (sửa theo Id để nhật ký chỉ ghi dòng thay đổi), tính lại số lượng cơ sở, thành tiền, VAT, tổng phiếu.
    /// </summary>
    /// <param name="lines">Dòng và mặt hàng tương ứng (đã đọc qua filter phạm vi nhóm hàng).</param>
    /// <exception cref="BusinessRuleException">Khi phiếu không còn nháp, không có dòng, hoặc dòng không hợp lệ.</exception>
    public void SetLines(IReadOnlyList<(InventoryLineData Data, Item Item)> lines)
    {
        EnsureDraft();
        if (lines.Count == 0) throw new BusinessRuleException("Phiếu phải có ít nhất một dòng hàng.");

        var keep = new List<InventoryDocumentLine>();
        var lineNo = 0;
        foreach (var (data, item) in lines)
        {
            lineNo++;
            InventoryDocumentLine line;
            if (data.Id is { } id)
            {
                line = _lines.FirstOrDefault(l => l.Id == id)
                    ?? throw new BusinessRuleException($"Dòng {lineNo}: không tìm thấy dòng cần sửa – hãy tải lại phiếu.");
            }
            else
            {
                line = new InventoryDocumentLine(Id);
                _lines.Add(line);
            }

            line.Set(lineNo, data, item, DocType);
            keep.Add(line);
        }

        _lines.RemoveAll(l => !keep.Contains(l));
        TotalAmount = _lines.Sum(l => l.Amount);
        TotalVat = _lines.Sum(l => l.VatAmount);
    }

    /// <summary>
    /// Đánh dấu đã ghi sổ (dòng sổ kho, tồn kho do tầng Infrastructure ghi trong cùng giao dịch – design.md mục 12.2).
    /// </summary>
    /// <param name="periodId">Kỳ tháng đang mở chứa ngày chứng từ.</param>
    /// <param name="postedAt">Thời điểm ghi sổ (UTC).</param>
    /// <param name="postedBy">Người ghi sổ.</param>
    /// <exception cref="BusinessRuleException">Khi phiếu không còn nháp hoặc không có dòng.</exception>
    public void MarkPosted(Guid periodId, DateTimeOffset postedAt, Guid? postedBy)
    {
        EnsureDraft();
        if (_lines.Count == 0) throw new BusinessRuleException("Phiếu không có dòng hàng nên không ghi sổ được.");
        Status = DocumentStatus.Posted;
        PeriodId = periodId;
        PostedAt = postedAt;
        PostedBy = postedBy;
    }

    /// <summary>
    /// Bỏ ghi sổ: phiếu về nháp để sửa (chỉ khi kỳ còn mở – tầng Infrastructure kiểm tra, xóa dòng sổ kho, trả lại tồn).
    /// </summary>
    /// <exception cref="BusinessRuleException">Khi phiếu chưa ghi sổ.</exception>
    public void MarkUnposted()
    {
        if (Status != DocumentStatus.Posted) throw new BusinessRuleException($"Phiếu {No} chưa ghi sổ.");
        Status = DocumentStatus.Draft;
        PeriodId = null;
        PostedAt = null;
        PostedBy = null;
    }

    /// <summary>Hủy phiếu nháp: giữ số phiếu và lịch sử, không dùng lại được.</summary>
    /// <exception cref="BusinessRuleException">Khi phiếu không còn nháp.</exception>
    public void Cancel()
    {
        EnsureDraft();
        // Phiếu nhập mua nội bộ do bên bán sinh ra; chỉ hủy khi bên bán bỏ ghi sổ phiếu xuất (design.md mục 6.3).
        if (DocType == DocumentTypes.IntercompanyReceipt)
            throw new BusinessRuleException("Phiếu nhập mua nội bộ không hủy được ở bên nhận – báo bên bán bỏ ghi sổ phiếu xuất nếu cần.");
        Status = DocumentStatus.Cancelled;
    }

    private void Recalculate()
    {
        _lines.RemoveAll(l => l.Qty == 0);
        var lineNo = 0;
        foreach (var line in _lines.OrderBy(l => l.LineNo)) line.Renumber(++lineNo);
        TotalAmount = _lines.Sum(l => l.Amount);
        TotalVat = _lines.Sum(l => l.VatAmount);
    }

    private void EnsureDraft()
    {
        // Chứng từ đã ghi sổ không sửa trực tiếp – bỏ ghi sổ khi kỳ còn mở, hoặc lập chứng từ điều chỉnh (CLAUDE.md mục 4).
        if (Status != DocumentStatus.Draft)
            throw new BusinessRuleException($"Phiếu {No} không còn ở trạng thái nháp nên không sửa, hủy được.");
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>
/// Dòng chứng từ kho (design.md mục 12.1). Số lượng lưu cả theo ĐVT trên phiếu (Qty) lẫn ĐVT cơ sở (BaseQty) – tồn kho luôn
/// tính theo ĐVT cơ sở.
/// </summary>
[Auditable("Dòng chứng từ kho")]
public sealed class InventoryDocumentLine : Entity
{
    private InventoryDocumentLine()
    {
    }

    internal InventoryDocumentLine(Guid documentId) => DocumentId = documentId;

    /// <summary>Chứng từ.</summary>
    public Guid DocumentId { get; private set; }

    /// <summary>Số thứ tự dòng (từ 1).</summary>
    public int LineNo { get; private set; }

    /// <summary>Mặt hàng.</summary>
    public Guid ItemId { get; private set; }

    /// <summary>ĐVT trên phiếu.</summary>
    public Guid UomId { get; private set; }

    /// <summary>Số lượng theo ĐVT trên phiếu – numeric(18,4).</summary>
    public decimal Qty { get; private set; }

    /// <summary>Số ĐVT cơ sở trong 1 ĐVT trên phiếu (1 nếu là ĐVT cơ sở) – numeric(18,6), chốt lúc lưu.</summary>
    public decimal UomFactor { get; private set; }

    /// <summary>Số lượng theo ĐVT cơ sở = Qty × UomFactor, làm tròn 4 chữ số – numeric(18,4).</summary>
    public decimal BaseQty { get; private set; }

    /// <summary>Đơn giá trước VAT theo ĐVT trên phiếu – numeric(19,6).</summary>
    public decimal UnitPrice { get; private set; }

    /// <summary>Thành tiền trước VAT = Qty × UnitPrice, làm tròn 2 chữ số – numeric(19,2).</summary>
    public decimal Amount { get; private set; }

    /// <summary>Thuế suất (KCT, 0, 5, 8, 10).</summary>
    public string? VatCode { get; private set; }

    /// <summary>Tiền VAT = Amount × thuế suất %, làm tròn 2 chữ số – numeric(19,2).</summary>
    public decimal VatAmount { get; private set; }

    /// <summary>Số lô.</summary>
    public string? LotNo { get; private set; }

    /// <summary>Hạn dùng.</summary>
    public DateOnly? ExpiryDate { get; private set; }

    /// <summary>Ghi chú dòng.</summary>
    public string? Note { get; private set; }

    /// <summary>Dòng chứng từ gốc (phiếu trả NCC: dòng phiếu nhập) – để tính số còn trả được.</summary>
    public Guid? SourceLineId { get; private set; }

    /// <summary>Chép dòng phiếu bán sang phiếu nhập mua nội bộ (SL nhận ban đầu = SL gửi).</summary>
    internal InventoryDocumentLine CopyFor(Guid documentId) =>
        new(documentId)
        {
            LineNo = LineNo, ItemId = ItemId, UomId = UomId, Qty = Qty, UomFactor = UomFactor, BaseQty = BaseQty, UnitPrice = UnitPrice,
            Amount = Amount, VatCode = VatCode, VatAmount = VatAmount, LotNo = LotNo, ExpiryDate = ExpiryDate, SourceLineId = Id,
        };

    /// <summary>Bên nhận ghi SL thực nhận; thành tiền, VAT tính lại theo giá bán nội bộ giữ nguyên.</summary>
    internal void Receive(decimal qty, string? lotNo, string? note)
    {
        if (decimal.Round(qty, 4) != qty) throw new BusinessRuleException($"Dòng {LineNo}: SL tối đa 4 chữ số thập phân.");
        Qty = qty;
        BaseQty = decimal.Round(qty * UomFactor, 4, MidpointRounding.AwayFromZero);
        Amount = decimal.Round(qty * UnitPrice, 2, MidpointRounding.AwayFromZero);
        VatAmount = VatCode is null or VatRates.NotSubject
            ? 0m
            : decimal.Round(Amount * decimal.Parse(VatCode, System.Globalization.CultureInfo.InvariantCulture) / 100m, 2, MidpointRounding.AwayFromZero);
        LotNo = lotNo ?? LotNo;
        Note = note;
    }

    internal void Renumber(int lineNo) => LineNo = lineNo;

    /// <summary>
    /// Gán dữ liệu dòng và tính số lượng cơ sở, thành tiền, VAT.
    /// Ví dụ: 2 thùng × 24 hộp/thùng, đơn giá 480.000 đ/thùng, VAT 10%: BaseQty = 48 hộp; Amount = 960.000; VAT = 96.000.
    /// </summary>
    internal void Set(int lineNo, InventoryLineData data, Item item, string docType)
    {
        // Phiếu chuyển kho không có đơn giá nhập tay: giá vốn tạm tính ghi ở sổ kho, chốt khi đóng kỳ (design.md mục 10.6).
        if (docType == DocumentTypes.Transfer && (data.UnitPrice != 0 || !string.IsNullOrWhiteSpace(data.VatCode)))
            throw new BusinessRuleException($"Dòng {lineNo}: phiếu chuyển kho không nhập đơn giá, thuế suất.");
        if (docType == DocumentTypes.ReturnToVendor && data.SourceLineId is null)
            throw new BusinessRuleException($"Dòng {lineNo}: phiếu trả NCC phải chọn dòng của phiếu nhập gốc.");
        SourceLineId = data.SourceLineId;
        var prefix = $"Dòng {lineNo} ({item.Code})";
        if (data.ItemId != item.Id) throw new BusinessRuleException($"{prefix}: mặt hàng không khớp.");
        // Chỉ hàng tồn kho mới đi qua kho (design.md mục 5.2).
        if (item.Type != ItemType.Inventory) throw new BusinessRuleException($"{prefix}: chỉ hàng tồn kho mới nhập, xuất kho được.");
        if (!item.IsActive && item.Id != ItemId) throw new BusinessRuleException($"{prefix}: mặt hàng đã ngừng dùng.");

        var factor = data.UomId == item.BaseUomId ? 1m
            : item.Conversions.FirstOrDefault(c => c.UomId == data.UomId)?.Factor
              ?? throw new BusinessRuleException($"{prefix}: ĐVT phải là ĐVT cơ sở hoặc ĐVT quy đổi của mặt hàng.");
        if (data.Qty <= 0 || decimal.Round(data.Qty, 4) != data.Qty || data.Qty >= 100_000_000_000_000m)
            throw new BusinessRuleException($"{prefix}: số lượng phải lớn hơn 0, tối đa 4 chữ số thập phân.");
        if (data.UnitPrice < 0 || decimal.Round(data.UnitPrice, 6) != data.UnitPrice || data.UnitPrice >= 10_000_000_000_000m)
            throw new BusinessRuleException($"{prefix}: đơn giá phải ≥ 0, tối đa 6 chữ số thập phân.");

        var vat = string.IsNullOrWhiteSpace(data.VatCode) ? null : data.VatCode.Trim().ToUpperInvariant();
        if (vat is not null && !VatRates.All.Contains(vat))
            throw new BusinessRuleException($"{prefix}: thuế suất phải là một trong {string.Join(", ", VatRates.All)}.");
        var lot = string.IsNullOrWhiteSpace(data.LotNo) ? null : data.LotNo.Trim();
        if (item.TrackLot && lot is null) throw new BusinessRuleException($"{prefix}: mặt hàng quản lý theo lô – hãy nhập số lô.");
        // Hạn dùng ghi lúc nhập; phiếu xuất/chuyển xác định hàng qua số lô nên không bắt buộc.
        if (item.TrackExpiry && data.ExpiryDate is null && docType == DocumentTypes.GoodsReceipt)
            throw new BusinessRuleException($"{prefix}: mặt hàng quản lý hạn dùng – hãy nhập hạn dùng.");
        if (lot?.Length > 50) throw new BusinessRuleException($"{prefix}: số lô tối đa 50 ký tự.");

        LineNo = lineNo;
        ItemId = item.Id;
        UomId = data.UomId;
        Qty = data.Qty;
        UomFactor = factor;
        // Làm tròn kiểu thông thường (0,5 → lên) theo thói quen kế toán Việt Nam, không dùng làm tròn ngân hàng.
        BaseQty = decimal.Round(data.Qty * factor, 4, MidpointRounding.AwayFromZero);
        UnitPrice = data.UnitPrice;
        Amount = decimal.Round(data.Qty * data.UnitPrice, 2, MidpointRounding.AwayFromZero);
        VatCode = vat;
        VatAmount = vat is null or VatRates.NotSubject
            ? 0m
            : decimal.Round(Amount * decimal.Parse(vat, System.Globalization.CultureInfo.InvariantCulture) / 100m, 2, MidpointRounding.AwayFromZero);
        LotNo = lot;
        ExpiryDate = data.ExpiryDate;
        Note = string.IsNullOrWhiteSpace(data.Note) ? null : data.Note.Trim();
    }
}
