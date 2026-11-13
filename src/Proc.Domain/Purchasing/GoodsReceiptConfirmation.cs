using System.Globalization;
using Proc.Domain.Common;
using Proc.Domain.Documents;
using Proc.Domain.MasterData;

namespace Proc.Domain.Purchasing;

/// <summary>Trạng thái phiếu xác nhận nhận hàng XNH (design.md mục 19.4).</summary>
public enum ReceiptConfirmationStatus
{
    /// <summary>Nháp: sửa được, chưa tính vào SL đã nhận của PO.</summary>
    Draft = 1,

    /// <summary>Đã xác nhận: SL thực nhận tính vào PO; chỉ còn điều chỉnh SL (có lý do), cập nhật hóa đơn, nghiệm thu.</summary>
    Confirmed = 2,

    /// <summary>Đã hủy (chỉ phiếu nháp): giữ số phiếu và lịch sử.</summary>
    Cancelled = 3,
}

/// <summary>Kết quả kiểm tra hàng của một dòng XNH (thay kết quả kiểm tra chất lượng trên phiếu nhập kho cũ – design.md mục 18.14, 19.4).</summary>
public enum ReceiptCheckResult
{
    /// <summary>Chưa kiểm tra.</summary>
    NotChecked = 0,

    /// <summary>Đạt.</summary>
    Passed = 1,

    /// <summary>Không đạt (toàn bộ hoặc một phần – SL không đạt ghi riêng, không tính là đã nhận).</summary>
    Failed = 2,
}

/// <summary>Xác nhận nghiệm thu của đơn vị đặt hàng trên XNH (design.md mục 19.1 – Q8; kế thừa lát H4).</summary>
public enum ReceiptConfirmationAcceptance
{
    /// <summary>Chưa xác nhận.</summary>
    None = 0,

    /// <summary>Đơn vị đặt hàng xác nhận đạt (đúng SL, chất lượng).</summary>
    Accepted = 1,

    /// <summary>Không đồng ý nghiệm thu – có ý kiến, Phòng cung ứng xử lý với NCC (điều chỉnh SL, giao bù bằng PO mới…).</summary>
    Disputed = 2,
}

/// <summary>Đầu phiếu XNH nhập được.</summary>
/// <param name="Date">Ngày nhận hàng.</param>
/// <param name="DeliveryNoteNo">Số phiếu giao hàng của NCC.</param>
/// <param name="Deliverer">Người giao, phương tiện.</param>
/// <param name="Note">Ghi chú (kết quả kiểm tra chung…).</param>
public sealed record ReceiptConfirmationHeader(DateOnly Date, string? DeliveryNoteNo, string? Deliverer, string? Note);

/// <summary>Hóa đơn NCC ghi trên XNH – cập nhật được cả sau khi xác nhận (hóa đơn thường về sau hàng; dùng cho đối chiếu MATCH3).</summary>
/// <param name="InvoiceNo">Số hóa đơn.</param>
/// <param name="InvoiceSeries">Ký hiệu hóa đơn.</param>
/// <param name="InvoiceDate">Ngày hóa đơn.</param>
/// <param name="InvoiceAmount">Tổng tiền thanh toán trên hóa đơn (gồm VAT); null = chưa có.</param>
public sealed record ReceiptConfirmationInvoice(string? InvoiceNo, string? InvoiceSeries, DateOnly? InvoiceDate, decimal? InvoiceAmount);

/// <summary>Một dòng XNH gửi vào khi lưu – theo một dòng PO, SL theo ĐVT của dòng PO.</summary>
/// <param name="PurchaseOrderLineId">Dòng PO.</param>
/// <param name="DeliveredQty">SL theo phiếu giao của NCC.</param>
/// <param name="AcceptedQty">SL đạt – SL thực nhận.</param>
/// <param name="RejectedQty">SL không đạt (trả lại NCC ngay khi giao).</param>
/// <param name="CheckResult">Kết quả kiểm tra.</param>
/// <param name="CheckNote">Ghi chú kiểm tra / sai lệch (bắt buộc khi không đạt).</param>
/// <param name="Note">Ghi chú dòng.</param>
public sealed record ReceiptConfirmationLineData(
    Guid PurchaseOrderLineId, decimal DeliveredQty, decimal AcceptedQty, decimal RejectedQty, ReceiptCheckResult CheckResult,
    string? CheckNote, string? Note);

/// <summary>
/// Phiếu xác nhận nhận hàng theo PO – XNH (lát K2, design.md mục 19.4; Q8). Thay phiếu nhập mua kho (GRN) từ bản 0.22.0: không ghi sổ kho,
/// không tồn kho – chỉ ghi SL NCC giao, SL đạt (thực nhận), SL không đạt, kết quả kiểm tra, số phiếu giao, hóa đơn, đính kèm. SL đã nhận
/// của dòng PO = tổng SL thực nhận của các XNH đã xác nhận (kể cả điều chỉnh). Hàng do thủ kho nơi nhận lập; dịch vụ do phòng ban đề xuất
/// lập (biên bản nghiệm thu); đơn vị đặt hàng xác nhận nghiệm thu.
/// </summary>
[Auditable("Xác nhận nhận hàng", LabelProperty = nameof(No))]
public sealed class GoodsReceiptConfirmation : AuditableEntity, ICompanyOwned, IHasDocumentNumber, IVersioned
{
    /// <summary>Độ dài tối đa của lý do, ý kiến.</summary>
    public const int MaxReasonLength = 500;

    private readonly List<GoodsReceiptConfirmationLine> _lines = [];
    private readonly List<GoodsReceiptAdjustment> _adjustments = [];

    private GoodsReceiptConfirmation()
    {
    }

    /// <summary>Phiên bản dòng (xmin) – chống ghi đè khi hai người cùng sửa.</summary>
    public uint Version { get; private set; }

    /// <inheritdoc />
    public Guid CompanyId { get; private set; }

    /// <summary>Số phiếu, cấp khi lưu lần đầu.</summary>
    public string No { get; private set; } = string.Empty;

    /// <inheritdoc />
    public string? DocumentNumber => No;

    /// <summary>Ngày nhận hàng.</summary>
    public DateOnly Date { get; private set; }

    /// <summary>Trạng thái.</summary>
    public ReceiptConfirmationStatus Status { get; private set; } = ReceiptConfirmationStatus.Draft;

    /// <summary>Đơn mua hàng được nhận.</summary>
    public Guid PurchaseOrderId { get; private set; }

    /// <summary>NCC (theo PO).</summary>
    public Guid SupplierId { get; private set; }

    /// <summary>Nơi nhận (kho nhận của PO – danh mục kho chỉ còn là nơi nhận hàng).</summary>
    public Guid WarehouseId { get; private set; }

    /// <summary>Số phiếu giao hàng của NCC.</summary>
    public string? DeliveryNoteNo { get; private set; }

    /// <summary>Phiếu báo giao NCC gửi trên Portal mà XNH lập theo (lát P5, design.md 19.5 P5-5); null = lập thẳng theo PO.</summary>
    public Guid? DeliveryNoticeId { get; private set; }

    /// <summary>Người giao, phương tiện.</summary>
    public string? Deliverer { get; private set; }

    /// <summary>Số hóa đơn NCC.</summary>
    public string? InvoiceNo { get; private set; }

    /// <summary>Ký hiệu hóa đơn.</summary>
    public string? InvoiceSeries { get; private set; }

    /// <summary>Ngày hóa đơn.</summary>
    public DateOnly? InvoiceDate { get; private set; }

    /// <summary>Tổng tiền thanh toán trên hóa đơn (gồm VAT).</summary>
    public decimal? InvoiceAmount { get; private set; }

    /// <summary>Ghi chú.</summary>
    public string? Note { get; private set; }

    /// <summary>Lý do hủy.</summary>
    public string? CancelReason { get; private set; }

    /// <summary>Thời điểm xác nhận (UTC).</summary>
    public DateTimeOffset? ConfirmedAt { get; private set; }

    /// <summary>Người xác nhận.</summary>
    public Guid? ConfirmedBy { get; private set; }

    /// <summary>Nghiệm thu của đơn vị đặt hàng.</summary>
    public ReceiptConfirmationAcceptance Acceptance { get; private set; }

    /// <summary>Ý kiến nghiệm thu.</summary>
    public string? AcceptanceNote { get; private set; }

    /// <summary>Người nghiệm thu.</summary>
    public Guid? AcceptedBy { get; private set; }

    /// <summary>Thời điểm nghiệm thu (UTC).</summary>
    public DateTimeOffset? AcceptedAt { get; private set; }

    /// <summary>Tiền hàng thực nhận trước VAT = Σ SL thực nhận × giá thực mua của dòng PO.</summary>
    public decimal TotalAmount { get; private set; }

    /// <summary>Tiền VAT của hàng thực nhận.</summary>
    public decimal TotalVat { get; private set; }

    /// <summary>Các dòng.</summary>
    public IReadOnlyList<GoodsReceiptConfirmationLine> Lines => _lines;

    /// <summary>Lịch sử điều chỉnh SL thực nhận.</summary>
    public IReadOnlyList<GoodsReceiptAdjustment> Adjustments => _adjustments;

    /// <summary>
    /// Lập XNH nháp cho một PO đã duyệt, chưa nhận đủ.
    /// </summary>
    /// <param name="companyId">Công ty đang làm việc.</param>
    /// <param name="no">Số phiếu đã cấp.</param>
    /// <param name="order">Đơn mua hàng.</param>
    /// <param name="header">Đầu phiếu.</param>
    /// <returns>Phiếu nháp.</returns>
    /// <exception cref="BusinessRuleException">Khi PO khác công ty, chưa duyệt, đã nhận đủ/đóng hoặc hủy; thiếu số phiếu.</exception>
    public static GoodsReceiptConfirmation Create(Guid companyId, string no, PurchaseOrder order, ReceiptConfirmationHeader header)
    {
        ArgumentNullException.ThrowIfNull(order);
        if (companyId == Guid.Empty || order.CompanyId != companyId) throw new BusinessRuleException("Phiếu nhận hàng phải cùng công ty với đơn mua hàng.");
        if (string.IsNullOrWhiteSpace(no)) throw new BusinessRuleException("Chứng từ phải có số.");
        EnsureReceivable(order);
        var confirmation = new GoodsReceiptConfirmation
        {
            CompanyId = companyId, No = no, PurchaseOrderId = order.Id, SupplierId = order.SupplierId, WarehouseId = order.WarehouseId,
        };
        confirmation.Update(header);
        return confirmation;
    }

    /// <summary>
    /// Gắn phiếu báo giao NCC cho XNH nháp (P5-5): phiếu báo giao phải cùng PO, chưa bị NCC hủy. Mỗi phiếu báo giao tối đa một XNH chưa hủy –
    /// tầng Infrastructure kiểm (chỉ mục duy nhất có điều kiện). XNH vẫn sửa SL được: SL thực tế có thể khác SL NCC báo.
    /// </summary>
    /// <param name="notice">Phiếu báo giao.</param>
    /// <exception cref="BusinessRuleException">Khi phiếu không còn nháp, phiếu báo giao khác PO hoặc đã hủy.</exception>
    public void LinkDeliveryNotice(PurchaseOrderDeliveryNotice notice)
    {
        ArgumentNullException.ThrowIfNull(notice);
        EnsureDraft();
        if (notice.PurchaseOrderId != PurchaseOrderId)
            throw new BusinessRuleException($"Phiếu báo giao {notice.No} không thuộc đơn mua hàng của phiếu nhận hàng.");
        if (notice.Status == DeliveryNoticeStatus.Cancelled)
            throw new BusinessRuleException($"NCC đã hủy phiếu báo giao {notice.No} – lập phiếu nhận hàng thẳng theo đơn.");
        DeliveryNoticeId = notice.Id;
    }

    /// <summary>
    /// PO nhận hàng được: Đã duyệt (chưa nhận đủ, chưa đóng, chưa hủy).
    /// </summary>
    /// <param name="order">Đơn mua hàng.</param>
    /// <exception cref="BusinessRuleException">Khi PO không ở trạng thái Đã duyệt.</exception>
    public static void EnsureReceivable(PurchaseOrder order)
    {
        ArgumentNullException.ThrowIfNull(order);
        if (order.Status != ApprovalDocumentStatus.Approved)
            throw new BusinessRuleException($"Đơn mua hàng {order.No} chưa duyệt, đã nhận đủ/hoàn thành hoặc đã hủy – không nhận hàng theo đơn này được.");
    }

    /// <summary>
    /// Sửa đầu phiếu nháp.
    /// </summary>
    /// <param name="header">Đầu phiếu mới.</param>
    /// <exception cref="BusinessRuleException">Khi phiếu không còn nháp hoặc trường quá dài.</exception>
    public void Update(ReceiptConfirmationHeader header)
    {
        ArgumentNullException.ThrowIfNull(header);
        EnsureDraft();
        Date = header.Date;
        DeliveryNoteNo = Limit(header.DeliveryNoteNo, 100, "Số phiếu giao");
        Deliverer = Limit(header.Deliverer, 200, "Người giao");
        Note = Limit(header.Note, 1000, "Ghi chú");
    }

    /// <summary>
    /// Cập nhật hóa đơn NCC – được làm cả khi đã xác nhận (hóa đơn về sau hàng); mọi thay đổi vào AuditLog.
    /// </summary>
    /// <param name="invoice">Hóa đơn mới.</param>
    /// <exception cref="BusinessRuleException">Khi phiếu đã hủy hoặc tiền âm.</exception>
    public void UpdateInvoice(ReceiptConfirmationInvoice invoice)
    {
        ArgumentNullException.ThrowIfNull(invoice);
        if (Status == ReceiptConfirmationStatus.Cancelled) throw new BusinessRuleException($"Phiếu {No} đã hủy, không cập nhật hóa đơn.");
        if (invoice.InvoiceAmount < 0) throw new BusinessRuleException("Tiền hóa đơn không được âm.");
        InvoiceNo = Limit(invoice.InvoiceNo, 50, "Số hóa đơn");
        InvoiceSeries = Limit(invoice.InvoiceSeries, 50, "Ký hiệu hóa đơn");
        InvoiceDate = invoice.InvoiceDate;
        InvoiceAmount = invoice.InvoiceAmount is { } a ? decimal.Round(a, 2, MidpointRounding.AwayFromZero) : null;
    }

    /// <summary>
    /// Thay toàn bộ dòng của phiếu nháp. Mặt hàng, ĐVT, giá thực mua (sau chiết khấu), thuế suất lấy từ dòng PO – không nhận từ client.
    /// Quy tắc mỗi dòng: SL không âm; SL đạt + SL không đạt ≤ SL theo phiếu giao (phần còn lại là NCC giao thiếu so với phiếu giao);
    /// có SL không đạt thì kết quả "Không đạt" và phải ghi chú; mỗi dòng PO chỉ một dòng trên phiếu.
    /// </summary>
    /// <param name="order">PO của phiếu (đã nạp dòng).</param>
    /// <param name="lines">Dòng mới.</param>
    /// <exception cref="BusinessRuleException">Khi vi phạm quy tắc trên hoặc phiếu không còn nháp.</exception>
    public void SetLines(PurchaseOrder order, IReadOnlyList<ReceiptConfirmationLineData> lines)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(lines);
        EnsureDraft();
        if (order.Id != PurchaseOrderId) throw new BusinessRuleException("Dòng phiếu nhận hàng phải theo đúng đơn mua hàng của phiếu.");
        var duplicate = lines.GroupBy(l => l.PurchaseOrderLineId).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
        {
            var poLineNo = order.Lines.FirstOrDefault(l => l.Id == duplicate.Key)?.LineNo;
            throw new BusinessRuleException($"Dòng {poLineNo} của đơn {order.No} xuất hiện nhiều lần trên phiếu – gộp lại một dòng.");
        }

        _lines.Clear();
        var lineNo = 0;
        foreach (var data in lines)
        {
            lineNo++;
            var poLine = order.Lines.FirstOrDefault(l => l.Id == data.PurchaseOrderLineId)
                ?? throw new BusinessRuleException($"Dòng {lineNo}: phải chọn một dòng của đơn mua hàng {order.No}.");
            var line = new GoodsReceiptConfirmationLine(Id, lineNo, poLine);
            line.Set(data, $"Dòng {lineNo}");
            _lines.Add(line);
        }

        Recalculate();
    }

    /// <summary>
    /// Xác nhận nhận hàng: phiếu có ít nhất một dòng có SL (đạt hoặc không đạt); <paramref name="requireCheck"/> bật (tham số công ty
    /// "bắt buộc kiểm tra chất lượng khi nhận hàng") thì mọi dòng phải có kết quả kiểm tra. SL đạt bắt đầu tính vào SL đã nhận của PO.
    /// </summary>
    /// <param name="userId">Người xác nhận.</param>
    /// <param name="at">Thời điểm (UTC).</param>
    /// <param name="requireCheck">Bắt buộc kết quả kiểm tra mọi dòng.</param>
    /// <exception cref="BusinessRuleException">Khi phiếu không còn nháp, không có SL hoặc thiếu kết quả kiểm tra.</exception>
    public void Confirm(Guid userId, DateTimeOffset at, bool requireCheck)
    {
        EnsureDraft();
        if (!_lines.Any(l => l.AcceptedQty > 0 || l.RejectedQty > 0))
            throw new BusinessRuleException("Phiếu chưa có dòng nào có SL đạt hoặc SL không đạt.");
        if (requireCheck)
        {
            var missing = _lines.Where(l => l.CheckResult == ReceiptCheckResult.NotChecked).Select(l => l.LineNo).ToList();
            if (missing.Count > 0)
                throw new BusinessRuleException($"Dòng {string.Join(", ", missing)}: chưa có kết quả kiểm tra – công ty bắt buộc kiểm tra khi nhận hàng.");
        }

        Status = ReceiptConfirmationStatus.Confirmed;
        ConfirmedBy = userId;
        ConfirmedAt = at;
    }

    /// <summary>
    /// Hủy phiếu nháp. Phiếu đã xác nhận không hủy – sửa SL bằng điều chỉnh có lý do để giữ lịch sử.
    /// </summary>
    /// <param name="reason">Lý do (≤ 500 ký tự).</param>
    /// <exception cref="BusinessRuleException">Khi phiếu không còn nháp hoặc lý do quá dài.</exception>
    public void Cancel(string? reason)
    {
        if (Status != ReceiptConfirmationStatus.Draft)
            throw new BusinessRuleException($"Phiếu {No} đã xác nhận hoặc đã hủy – chỉ hủy được phiếu nháp; sửa SL bằng lệnh Điều chỉnh SL thực nhận.");
        CancelReason = Limit(reason, MaxReasonLength, "Lý do hủy");
        Status = ReceiptConfirmationStatus.Cancelled;
    }

    /// <summary>
    /// Đơn vị đặt hàng xác nhận nghiệm thu phiếu đã xác nhận: đạt, hoặc không đạt kèm ý kiến. Xác nhận lại được (ví dụ sau khi điều chỉnh).
    /// Người lập hoặc người xác nhận nhận hàng không tự nghiệm thu – tách người nhận hàng và người dùng hàng (kiểm soát nội bộ, lát H4).
    /// </summary>
    /// <param name="accepted">true = đạt.</param>
    /// <param name="note">Ý kiến (≤ 500), bắt buộc khi không đạt.</param>
    /// <param name="userId">Người nghiệm thu.</param>
    /// <param name="at">Thời điểm (UTC).</param>
    /// <exception cref="BusinessRuleException">Khi phiếu chưa xác nhận, thiếu/quá dài ý kiến hoặc người nhận hàng tự nghiệm thu.</exception>
    public void ConfirmAcceptance(bool accepted, string? note, Guid userId, DateTimeOffset at)
    {
        if (Status != ReceiptConfirmationStatus.Confirmed)
            throw new BusinessRuleException($"Phiếu {No} chưa xác nhận nhận hàng – xác nhận xong mới nghiệm thu.");
        if (userId == CreatedBy || userId == ConfirmedBy)
            throw new BusinessRuleException("Người lập hoặc xác nhận phiếu nhận hàng không tự nghiệm thu – đơn vị đặt hàng xác nhận.");
        var cleaned = Limit(note, MaxReasonLength, "Ý kiến nghiệm thu");
        if (!accepted && cleaned is null) throw new BusinessRuleException("Không đạt nghiệm thu phải ghi ý kiến (thiếu, hỏng, sai quy cách…).");
        Acceptance = accepted ? ReceiptConfirmationAcceptance.Accepted : ReceiptConfirmationAcceptance.Disputed;
        AcceptanceNote = cleaned;
        AcceptedBy = userId;
        AcceptedAt = at;
    }

    /// <summary>
    /// Điều chỉnh SL thực nhận của một dòng đã xác nhận – thay phiếu trả NCC (RTV) của kho cũ (design.md mục 19.4): âm = trả lại NCC / nhận
    /// nhầm, dương = ghi bổ sung. Bắt buộc lý do; mỗi lần lưu một dòng lịch sử. SL thực nhận sau điều chỉnh không âm; không vượt SL đặt +
    /// dung sai (tầng Infrastructure kiểm tra vì cần SL của các XNH khác). Ví dụ đạt 10, trả 3 → điều chỉnh −3, thực nhận 7.
    /// </summary>
    /// <param name="lineId">Dòng XNH.</param>
    /// <param name="qty">SL điều chỉnh theo ĐVT dòng (khác 0).</param>
    /// <param name="reason">Lý do (bắt buộc, ≤ 500).</param>
    /// <param name="userId">Người điều chỉnh.</param>
    /// <param name="at">Thời điểm (UTC).</param>
    /// <returns>Dòng lịch sử vừa thêm.</returns>
    /// <exception cref="BusinessRuleException">Khi phiếu chưa xác nhận, SL = 0, thiếu lý do hoặc SL thực nhận âm.</exception>
    public GoodsReceiptAdjustment Adjust(Guid lineId, decimal qty, string? reason, Guid userId, DateTimeOffset at)
    {
        if (Status != ReceiptConfirmationStatus.Confirmed)
            throw new BusinessRuleException($"Phiếu {No} chưa xác nhận – phiếu nháp thì sửa trực tiếp SL trên dòng.");
        var line = _lines.FirstOrDefault(l => l.Id == lineId) ?? throw new BusinessRuleException("Không tìm thấy dòng cần điều chỉnh.");
        if (qty == 0) throw new BusinessRuleException("SL điều chỉnh phải khác 0.");
        var cleaned = Limit(reason, MaxReasonLength, "Lý do điều chỉnh")
            ?? throw new BusinessRuleException("Điều chỉnh SL thực nhận phải ghi lý do (trả NCC, nhận nhầm…).");
        line.ApplyAdjustment(qty, $"Dòng {line.LineNo}");
        var adjustment = new GoodsReceiptAdjustment(Id, line.Id, line.LineNo, qty, cleaned, userId, at);
        _adjustments.Add(adjustment);
        Recalculate();
        return adjustment;
    }

    private void Recalculate()
    {
        TotalAmount = _lines.Sum(l => l.Amount);
        TotalVat = _lines.Sum(l => l.VatAmount);
    }

    private void EnsureDraft()
    {
        if (Status != ReceiptConfirmationStatus.Draft)
            throw new BusinessRuleException($"Phiếu {No} đã xác nhận hoặc đã hủy, không sửa được.");
    }

    /// <summary>Bỏ khoảng trắng; rỗng thành null; quá dài thì báo lỗi.</summary>
    internal static string? Limit(string? value, int max, string label)
    {
        var cleaned = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        if (cleaned?.Length > max) throw new BusinessRuleException($"{label} tối đa {max} ký tự.");
        return cleaned;
    }
}

/// <summary>Dòng XNH – theo một dòng PO, SL theo ĐVT dòng PO.</summary>
public sealed class GoodsReceiptConfirmationLine : Entity
{
    private GoodsReceiptConfirmationLine()
    {
    }

    internal GoodsReceiptConfirmationLine(Guid confirmationId, int lineNo, PurchaseOrderLine poLine)
    {
        ConfirmationId = confirmationId;
        LineNo = lineNo;
        PurchaseOrderLineId = poLine.Id;
        ItemId = poLine.ItemId;
        UomId = poLine.UomId;
        UomFactor = poLine.UomFactor;
        UnitPrice = poLine.NetUnitPrice;
        VatCode = poLine.VatCode;
    }

    /// <summary>Phiếu.</summary>
    public Guid ConfirmationId { get; private set; }

    /// <summary>Số thứ tự dòng.</summary>
    public int LineNo { get; private set; }

    /// <summary>Dòng PO.</summary>
    public Guid PurchaseOrderLineId { get; private set; }

    /// <summary>Mặt hàng (theo dòng PO).</summary>
    public Guid ItemId { get; private set; }

    /// <summary>ĐVT (theo dòng PO).</summary>
    public Guid UomId { get; private set; }

    /// <summary>Hệ số quy đổi ĐVT dòng → ĐVT cơ sở (theo dòng PO).</summary>
    public decimal UomFactor { get; private set; }

    /// <summary>SL theo phiếu giao của NCC.</summary>
    public decimal DeliveredQty { get; private set; }

    /// <summary>SL đạt lúc nhận.</summary>
    public decimal AcceptedQty { get; private set; }

    /// <summary>SL không đạt (không nhận).</summary>
    public decimal RejectedQty { get; private set; }

    /// <summary>Tổng SL điều chỉnh sau khi xác nhận (âm = trả lại).</summary>
    public decimal AdjustedQty { get; private set; }

    /// <summary>SL thực nhận = SL đạt + điều chỉnh – số tính vào PO.</summary>
    public decimal ReceivedQty { get; private set; }

    /// <summary>SL thực nhận theo ĐVT cơ sở = SL thực nhận × hệ số, làm tròn 4 số lẻ (cộng dồn so với SL đặt cơ sở của PO).</summary>
    public decimal ReceivedBaseQty { get; private set; }

    /// <summary>Giá thực mua sau chiết khấu của dòng PO.</summary>
    public decimal UnitPrice { get; private set; }

    /// <summary>Thuế suất (theo dòng PO).</summary>
    public string? VatCode { get; private set; }

    /// <summary>Tiền hàng thực nhận trước VAT = SL thực nhận × giá, làm tròn 2 số lẻ.</summary>
    public decimal Amount { get; private set; }

    /// <summary>Tiền VAT = tiền hàng × thuế suất, làm tròn 2 số lẻ.</summary>
    public decimal VatAmount { get; private set; }

    /// <summary>Kết quả kiểm tra.</summary>
    public ReceiptCheckResult CheckResult { get; private set; }

    /// <summary>Ghi chú kiểm tra / sai lệch.</summary>
    public string? CheckNote { get; private set; }

    /// <summary>Ghi chú dòng.</summary>
    public string? Note { get; private set; }

    internal void Set(ReceiptConfirmationLineData data, string prefix)
    {
        if (data.DeliveredQty < 0 || data.AcceptedQty < 0 || data.RejectedQty < 0) throw new BusinessRuleException($"{prefix}: SL không được âm.");
        if (data.AcceptedQty + data.RejectedQty > data.DeliveredQty)
            throw new BusinessRuleException($"{prefix}: SL đạt + SL không đạt vượt SL theo phiếu giao.");
        if (!Enum.IsDefined(data.CheckResult)) throw new BusinessRuleException($"{prefix}: kết quả kiểm tra không hợp lệ.");
        var checkNote = GoodsReceiptConfirmation.Limit(data.CheckNote, 500, $"{prefix}: ghi chú kiểm tra");
        if (data.RejectedQty > 0 && data.CheckResult != ReceiptCheckResult.Failed)
            throw new BusinessRuleException($"{prefix}: có SL không đạt thì kết quả kiểm tra phải là Không đạt.");
        if (data.CheckResult == ReceiptCheckResult.Failed && checkNote is null)
            throw new BusinessRuleException($"{prefix}: kết quả Không đạt phải ghi chú lý do.");
        DeliveredQty = data.DeliveredQty;
        AcceptedQty = data.AcceptedQty;
        RejectedQty = data.RejectedQty;
        CheckResult = data.CheckResult;
        CheckNote = checkNote;
        Note = GoodsReceiptConfirmation.Limit(data.Note, 500, $"{prefix}: ghi chú");
        Recalculate();
    }

    internal void ApplyAdjustment(decimal qty, string prefix)
    {
        if (AcceptedQty + AdjustedQty + qty < 0)
            throw new BusinessRuleException($"{prefix}: SL thực nhận sau điều chỉnh không được âm (đang {AcceptedQty + AdjustedQty:0.####}).");
        AdjustedQty += qty;
        Recalculate();
    }

    /// <summary>
    /// Tính lại SL thực nhận và tiền. Ví dụ đạt 10, điều chỉnh −3, giá 15.000, VAT 8% → thực nhận 7, tiền 105.000, VAT 8.400.
    /// </summary>
    private void Recalculate()
    {
        ReceivedQty = AcceptedQty + AdjustedQty;
        ReceivedBaseQty = decimal.Round(ReceivedQty * UomFactor, 4, MidpointRounding.AwayFromZero);
        Amount = decimal.Round(ReceivedQty * UnitPrice, 2, MidpointRounding.AwayFromZero);
        VatAmount = VatCode is null or VatRates.NotSubject
            ? 0m
            : decimal.Round(Amount * decimal.Parse(VatCode, CultureInfo.InvariantCulture) / 100m, 2, MidpointRounding.AwayFromZero);
    }
}

/// <summary>Một lần điều chỉnh SL thực nhận của dòng XNH (lịch sử – chỉ thêm, không sửa/xóa).</summary>
public sealed class GoodsReceiptAdjustment : Entity
{
    private GoodsReceiptAdjustment()
    {
    }

    internal GoodsReceiptAdjustment(Guid confirmationId, Guid lineId, int lineNo, decimal qty, string reason, Guid userId, DateTimeOffset at)
    {
        ConfirmationId = confirmationId;
        LineId = lineId;
        LineNo = lineNo;
        Qty = qty;
        Reason = reason;
        AdjustedBy = userId;
        AdjustedAt = at;
    }

    /// <summary>Phiếu.</summary>
    public Guid ConfirmationId { get; private set; }

    /// <summary>Dòng phiếu.</summary>
    public Guid LineId { get; private set; }

    /// <summary>Số dòng lúc điều chỉnh (để hiển thị).</summary>
    public int LineNo { get; private set; }

    /// <summary>SL điều chỉnh theo ĐVT dòng (âm = giảm).</summary>
    public decimal Qty { get; private set; }

    /// <summary>Lý do.</summary>
    public string Reason { get; private set; } = string.Empty;

    /// <summary>Người điều chỉnh.</summary>
    public Guid AdjustedBy { get; private set; }

    /// <summary>Thời điểm (UTC).</summary>
    public DateTimeOffset AdjustedAt { get; private set; }
}
