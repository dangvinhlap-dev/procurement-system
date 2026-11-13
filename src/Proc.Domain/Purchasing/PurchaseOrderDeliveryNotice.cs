using Proc.Domain.Common;

namespace Proc.Domain.Purchasing;

/// <summary>
/// Trạng thái lưu của phiếu báo giao hàng NCC (lát P5). Trạng thái hiển thị "Đang nhận" / "Đã nhận" tính từ XNH lập theo phiếu – không lưu,
/// để không phải đồng bộ hai nơi (design.md mục 19.5, P5-2).
/// </summary>
public enum DeliveryNoticeStatus
{
    /// <summary>NCC đã gửi – chờ nơi nhận lập XNH (hoặc đang nhận / đã nhận theo XNH).</summary>
    Submitted = 1,

    /// <summary>NCC hủy (chỉ khi chưa có XNH chưa hủy lập theo phiếu).</summary>
    Cancelled = 2,
}

/// <summary>Một dòng NCC báo giao: dòng PO và SL theo ĐVT của dòng PO.</summary>
/// <param name="PurchaseOrderLineId">Dòng PO.</param>
/// <param name="Qty">SL giao (&gt; 0, theo ĐVT dòng PO).</param>
/// <param name="Note">Ghi chú dòng (số lô, hạn dùng…).</param>
public sealed record DeliveryNoticeLineData(Guid PurchaseOrderLineId, decimal Qty, string? Note);

/// <summary>Đầu phiếu báo giao NCC nhập trên Portal.</summary>
/// <param name="ExpectedDate">Ngày giao dự kiến (không trước ngày đặt hàng).</param>
/// <param name="DeliveryNoteNo">Số phiếu giao hàng của NCC (bắt buộc – nơi nhận đối chiếu khi hàng đến).</param>
/// <param name="Deliverer">Người giao, phương tiện, biển số xe.</param>
/// <param name="Note">Ghi chú.</param>
public sealed record DeliveryNoticeHeader(DateOnly ExpectedDate, string? DeliveryNoteNo, string? Deliverer, string? Note);

/// <summary>
/// Phiếu báo giao hàng của NCC trên Portal – số chứng từ loại BGH (lát P5, design.md mục 19.5 P5-1…P5-7). NCC báo trước ngày giao, số phiếu
/// giao, SL từng dòng PO; nơi nhận lập XNH từ phiếu (điền sẵn số phiếu giao, SL). Không gắn [Auditable] (như phản hồi PO P4a): phiếu do tài khoản
/// Portal lập, không sửa – chỉ hủy (ghi lý do, giờ, email); chính bản ghi là dấu vết. Không ghi lên PO (PO đã duyệt là khóa – design.md mục 20 R1).
/// Kiểm SL còn giao được (cần SL đã nhận, báo giao khác) làm ở tầng Infrastructure. <see cref="CompanyId"/> là công ty đặt hàng.
/// </summary>
public sealed class PurchaseOrderDeliveryNotice : Entity, ICompanyOwned, IHasDocumentNumber
{
    /// <summary>Độ dài tối đa ghi chú / lý do hủy.</summary>
    public const int NoteMaxLength = 1000;

    private readonly List<PurchaseOrderDeliveryNoticeLine> _lines = [];

    private PurchaseOrderDeliveryNotice()
    {
    }

    /// <inheritdoc />
    public Guid CompanyId { get; private set; }

    /// <summary>Số phiếu BGH (cấp theo công ty đặt hàng).</summary>
    public string No { get; private set; } = string.Empty;

    /// <inheritdoc />
    public string? DocumentNumber => No;

    /// <summary>Đơn đặt hàng.</summary>
    public Guid PurchaseOrderId { get; private set; }

    /// <summary>NCC của PO (NCC của phiên Portal).</summary>
    public Guid SupplierId { get; private set; }

    /// <summary>Ngày giao dự kiến.</summary>
    public DateOnly ExpectedDate { get; private set; }

    /// <summary>Số phiếu giao hàng của NCC.</summary>
    public string DeliveryNoteNo { get; private set; } = string.Empty;

    /// <summary>Người giao, phương tiện.</summary>
    public string? Deliverer { get; private set; }

    /// <summary>Ghi chú.</summary>
    public string? Note { get; private set; }

    /// <summary>Trạng thái lưu.</summary>
    public DeliveryNoticeStatus Status { get; private set; } = DeliveryNoticeStatus.Submitted;

    /// <summary>Thời điểm gửi (UTC).</summary>
    public DateTimeOffset SubmittedAt { get; private set; }

    /// <summary>Tài khoản Portal gửi.</summary>
    public Guid PortalUserId { get; private set; }

    /// <summary>Email tài khoản lúc gửi (giữ lại dù tài khoản đổi sau này).</summary>
    public string SubmittedByEmail { get; private set; } = string.Empty;

    /// <summary>Thời điểm NCC hủy (UTC).</summary>
    public DateTimeOffset? CancelledAt { get; private set; }

    /// <summary>Email tài khoản Portal hủy.</summary>
    public string? CancelledByEmail { get; private set; }

    /// <summary>Lý do hủy.</summary>
    public string? CancelReason { get; private set; }

    /// <summary>Các dòng.</summary>
    public IReadOnlyList<PurchaseOrderDeliveryNoticeLine> Lines => _lines;

    /// <summary>
    /// NCC gửi phiếu báo giao (P5-1): PO phải <b>Đã duyệt</b> (chưa nhận đủ, chưa hủy), đặt cho đúng NCC, không phải mua nội bộ; ngày giao dự
    /// kiến không trước ngày đặt; bắt buộc số phiếu giao; ít nhất một dòng, mỗi dòng PO một lần, SL &gt; 0.
    /// </summary>
    /// <param name="order">PO (đã nạp dòng).</param>
    /// <param name="no">Số phiếu đã cấp.</param>
    /// <param name="supplierId">NCC của phiên Portal.</param>
    /// <param name="portalUserId">Tài khoản Portal.</param>
    /// <param name="email">Email tài khoản.</param>
    /// <param name="header">Đầu phiếu.</param>
    /// <param name="lines">Các dòng.</param>
    /// <param name="at">Thời điểm (UTC).</param>
    /// <returns>Phiếu mới.</returns>
    /// <exception cref="BusinessRuleException">Khi vi phạm một trong các quy tắc trên.</exception>
    public static PurchaseOrderDeliveryNotice Create(
        PurchaseOrder order, string no, Guid supplierId, Guid portalUserId, string email, DeliveryNoticeHeader header,
        IReadOnlyList<DeliveryNoticeLineData> lines, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(header);
        ArgumentNullException.ThrowIfNull(lines);
        if (string.IsNullOrWhiteSpace(no)) throw new BusinessRuleException("Chứng từ phải có số.");
        if (order.SupplierId != supplierId) throw new BusinessRuleException("Đơn đặt hàng không thuộc nhà cung cấp này.");
        if (order.InternalSellerCompanyId is not null)
            throw new BusinessRuleException("Đơn mua nội bộ không báo giao trên Portal – bên bán xử lý ở Đơn bán nội bộ.");
        if (order.Status != ApprovalDocumentStatus.Approved)
            throw new BusinessRuleException($"Đơn {order.No} đã nhận đủ hàng, đã hoàn thành hoặc đã hủy – không báo giao được.");
        if (header.ExpectedDate < order.Date)
            throw new BusinessRuleException($"Ngày giao dự kiến không được trước ngày đặt hàng {order.Date:dd/MM/yyyy}.");
        var noteNo = GoodsReceiptConfirmation.Limit(header.DeliveryNoteNo, 100, "Số phiếu giao")
            ?? throw new BusinessRuleException("Nhập số phiếu giao hàng của công ty bạn để nơi nhận đối chiếu khi hàng đến.");
        if (lines.Count == 0) throw new BusinessRuleException("Phiếu báo giao phải có ít nhất một dòng hàng.");
        var notice = new PurchaseOrderDeliveryNotice
        {
            CompanyId = order.CompanyId,
            No = no,
            PurchaseOrderId = order.Id,
            SupplierId = supplierId,
            ExpectedDate = header.ExpectedDate,
            DeliveryNoteNo = noteNo,
            Deliverer = GoodsReceiptConfirmation.Limit(header.Deliverer, 200, "Người giao"),
            Note = GoodsReceiptConfirmation.Limit(header.Note, NoteMaxLength, "Ghi chú"),
            SubmittedAt = at,
            PortalUserId = portalUserId,
            SubmittedByEmail = email,
        };
        var lineNo = 0;
        foreach (var data in lines)
        {
            var poLine = order.Lines.FirstOrDefault(l => l.Id == data.PurchaseOrderLineId)
                ?? throw new BusinessRuleException($"Dòng báo giao không thuộc đơn {order.No}.");
            if (notice._lines.Any(l => l.PurchaseOrderLineId == poLine.Id))
                throw new BusinessRuleException($"Dòng {poLine.LineNo} của đơn xuất hiện nhiều lần – gộp lại một dòng.");
            if (data.Qty <= 0) throw new BusinessRuleException($"Dòng {poLine.LineNo} của đơn: SL giao phải lớn hơn 0.");
            notice._lines.Add(new PurchaseOrderDeliveryNoticeLine(
                notice.Id, ++lineNo, poLine.Id, decimal.Round(data.Qty, 4, MidpointRounding.AwayFromZero),
                GoodsReceiptConfirmation.Limit(data.Note, 500, $"Ghi chú dòng {poLine.LineNo}")));
        }

        return notice;
    }

    /// <summary>
    /// NCC hủy phiếu báo giao (P5-2): bắt buộc lý do; người gọi đã kiểm chưa có XNH chưa hủy lập theo phiếu.
    /// </summary>
    /// <param name="reason">Lý do (bắt buộc, ≤ 1.000 ký tự).</param>
    /// <param name="email">Email tài khoản Portal hủy.</param>
    /// <param name="at">Thời điểm (UTC).</param>
    /// <exception cref="BusinessRuleException">Khi phiếu đã hủy, thiếu lý do hoặc lý do quá dài.</exception>
    public void Cancel(string? reason, string email, DateTimeOffset at)
    {
        if (Status == DeliveryNoticeStatus.Cancelled) throw new BusinessRuleException($"Phiếu báo giao {No} đã hủy trước đó.");
        CancelReason = GoodsReceiptConfirmation.Limit(reason, NoteMaxLength, "Lý do hủy")
            ?? throw new BusinessRuleException("Nhập lý do hủy phiếu báo giao để nơi nhận biết.");
        Status = DeliveryNoticeStatus.Cancelled;
        CancelledAt = at;
        CancelledByEmail = email;
    }
}

/// <summary>Dòng phiếu báo giao – theo một dòng PO, SL theo ĐVT dòng PO.</summary>
public sealed class PurchaseOrderDeliveryNoticeLine : Entity
{
    private PurchaseOrderDeliveryNoticeLine()
    {
    }

    internal PurchaseOrderDeliveryNoticeLine(Guid noticeId, int lineNo, Guid purchaseOrderLineId, decimal qty, string? note)
    {
        NoticeId = noticeId;
        LineNo = lineNo;
        PurchaseOrderLineId = purchaseOrderLineId;
        Qty = qty;
        Note = note;
    }

    /// <summary>Phiếu báo giao.</summary>
    public Guid NoticeId { get; private set; }

    /// <summary>Số thứ tự dòng.</summary>
    public int LineNo { get; private set; }

    /// <summary>Dòng PO.</summary>
    public Guid PurchaseOrderLineId { get; private set; }

    /// <summary>SL giao theo ĐVT dòng PO.</summary>
    public decimal Qty { get; private set; }

    /// <summary>Ghi chú dòng.</summary>
    public string? Note { get; private set; }
}
