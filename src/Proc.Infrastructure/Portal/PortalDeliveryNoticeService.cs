using Microsoft.EntityFrameworkCore;
using Proc.Application.Common;
using Proc.Application.Documents;
using Proc.Application.Portal;
using Proc.Domain.Common;
using Proc.Domain.Documents;
using Proc.Domain.Notifications;
using Proc.Domain.Organization;
using Proc.Domain.Purchasing;
using Proc.Infrastructure.Documents;
using Proc.Infrastructure.Notifications;
using Proc.Infrastructure.Persistence;
using Proc.Infrastructure.Purchasing;

namespace Proc.Infrastructure.Portal;

/// <summary>
/// NCC báo giao hàng trên Portal (lát P5 – design.md mục 19.5, P5-1…P5-4). Phiên Portal không có công ty / người dùng nội bộ nên mọi truy vấn bỏ
/// filter và tự lọc theo NCC của phiên (P1b-4) qua <see cref="PortalOrderService.VisibleOrders(ProcDbContext, Guid)"/>.
/// </summary>
public sealed class PortalDeliveryNoticeService : IPortalDeliveryNoticeService
{
    private readonly ProcDbContext _db;
    private readonly IClock _clock;
    private readonly AttachmentService _attachments;
    private readonly ICurrentUserScope _scope;
    private readonly IDocumentNumberService _numbers;
    private readonly IPortalOrderService _orders;

    /// <summary>Tạo dịch vụ.</summary>
    /// <param name="db">DbContext.</param>
    /// <param name="clock">Đồng hồ.</param>
    /// <param name="attachments">Kho file đính kèm.</param>
    /// <param name="scope">Phạm vi người dùng nội bộ – phiên Portal không có; chỉ dùng cho bộ sinh thông báo.</param>
    /// <param name="numbers">Đánh số chứng từ (cấp số BGH theo công ty đặt hàng).</param>
    /// <param name="orders">Đơn đặt hàng Portal (trả đơn sau thao tác).</param>
    public PortalDeliveryNoticeService(
        ProcDbContext db, IClock clock, AttachmentService attachments, ICurrentUserScope scope, IDocumentNumberService numbers,
        IPortalOrderService orders)
    {
        _db = db;
        _clock = clock;
        _attachments = attachments;
        _scope = scope;
        _numbers = numbers;
        _orders = orders;
    }

    /// <inheritdoc />
    public async Task<PortalDeliveryNoticeDto> CreateAsync(
        PortalSession session, Guid orderId, PortalDeliveryNoticeRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await using var transaction = await RowLocks.BeginOrJoinAsync(_db, cancellationToken);
        // Khóa PO tới hết giao dịch: hai người cùng NCC báo giao cùng lúc phải lần lượt, phiếu sau thấy SL phiếu trước – không báo vượt SL đặt.
        await RowLocks.LockAsync<PurchaseOrder>(_db, [orderId], cancellationToken);
        var order = await LoadOrderAsync(session.SupplierId, orderId, cancellationToken);
        var rejected = await _db.PurchaseOrderSupplierResponses.AsNoTracking().IgnoreQueryFilters()
            .AnyAsync(r => r.PurchaseOrderId == orderId && r.SupplierId == session.SupplierId && r.ReopenedAt == null
                           && r.Decision == SupplierOrderDecision.Rejected, cancellationToken);
        if (rejected)
            throw new BusinessRuleException($"Bạn đã từ chối đơn {order.No} – liên hệ Phòng Cung ứng vật tư để mở lại phản hồi nếu vẫn giao.");
        var lines = (request.Lines ?? []).Select(l => new DeliveryNoticeLineData(l.PurchaseOrderLineId, l.Qty, l.Note)).ToList();
        var quantities = await DeliveryNoticeQueries.QuantitiesAsync(_db, order, cancellationToken);
        foreach (var line in lines)
        {
            if (!quantities.TryGetValue(line.PurchaseOrderLineId, out var q)) continue; // dòng lạ – domain báo lỗi
            var poLine = order.Lines.Single(l => l.Id == line.PurchaseOrderLineId);
            if (line.Qty > q.Deliverable)
                throw new BusinessRuleException(
                    $"Dòng {poLine.LineNo} của đơn: SL giao {line.Qty:0.####} vượt SL còn giao được {q.Deliverable:0.####} " +
                    $"(đã nhận và đang báo giao đã trừ, dung sai {order.ReceiveTolerancePct:0.##}%).");
        }

        var today = _clock.TodayInVietnam();
        var no = await _numbers.NextForCompanyAsync(order.CompanyId, DocumentTypes.DeliveryNotice, today, cancellationToken);
        var notice = PurchaseOrderDeliveryNotice.Create(order, no, session.SupplierId, session.UserId, session.Email,
            new DeliveryNoticeHeader(request.ExpectedDate, request.DeliveryNoteNo, request.Deliverer, request.Note), lines, _clock.UtcNow);
        _db.PurchaseOrderDeliveryNotices.Add(notice);
        await NotifyAsync(order, notice, cancelled: false, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        var dto = await _orders.GetAsync(session, orderId, cancellationToken);
        return dto.DeliveryNotices.Single(n => n.Id == notice.Id);
    }

    /// <inheritdoc />
    public async Task<PortalOrderDto> CancelAsync(
        PortalSession session, Guid orderId, Guid noticeId, PortalCancelDeliveryNoticeRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await using var transaction = await RowLocks.BeginOrJoinAsync(_db, cancellationToken);
        // Khóa phiếu báo giao: thủ kho lập XNH (cũng khóa phiếu) và NCC hủy cùng lúc thì lần lượt – không có XNH theo phiếu đã hủy.
        await RowLocks.LockAsync<PurchaseOrderDeliveryNotice>(_db, [noticeId], cancellationToken);
        var order = await LoadOrderAsync(session.SupplierId, orderId, cancellationToken);
        var notice = await FindNoticeAsync(session.SupplierId, orderId, noticeId, cancellationToken);
        if (await _db.GoodsReceiptConfirmations.IgnoreQueryFilters()
                .AnyAsync(c => c.DeliveryNoticeId == noticeId && c.Status != ReceiptConfirmationStatus.Cancelled, cancellationToken))
            throw new BusinessRuleException($"Bên đặt hàng đã lập phiếu nhận hàng theo phiếu báo giao {notice.No} – không hủy được, liên hệ nơi nhận.");
        notice.Cancel(request.Reason, session.Email, _clock.UtcNow);
        await NotifyAsync(order, notice, cancelled: true, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await _orders.GetAsync(session, orderId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AttachmentDto>> ListAttachmentsAsync(
        PortalSession session, Guid orderId, Guid noticeId, CancellationToken cancellationToken)
    {
        var (notice, lockReason) = await FileStateAsync(session.SupplierId, orderId, noticeId, cancellationToken);
        return await _attachments.ListForPortalAsync(AttachmentOwnerTypes.DeliveryNoticeFile, notice.Id, lockReason, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<AttachmentDto> UploadAttachmentAsync(
        PortalSession session, Guid orderId, Guid noticeId, string fileName, long sizeBytes, Stream content, string? note,
        CancellationToken cancellationToken)
    {
        var (notice, lockReason) = await FileStateAsync(session.SupplierId, orderId, noticeId, cancellationToken);
        if (lockReason is not null) throw new BusinessRuleException(lockReason);
        return await _attachments.UploadForPortalAsync(AttachmentOwnerTypes.DeliveryNoticeFile, notice.Id, notice.CompanyId, session.UserId,
            fileName, sizeBytes, content, note, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<AttachmentContent> OpenAttachmentAsync(
        PortalSession session, Guid orderId, Guid noticeId, Guid attachmentId, CancellationToken cancellationToken)
    {
        var (notice, _) = await FileStateAsync(session.SupplierId, orderId, noticeId, cancellationToken);
        return await _attachments.OpenForPortalAsync(AttachmentOwnerTypes.DeliveryNoticeFile, attachmentId, notice.Id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteAttachmentAsync(PortalSession session, Guid orderId, Guid noticeId, Guid attachmentId, CancellationToken cancellationToken)
    {
        var (notice, lockReason) = await FileStateAsync(session.SupplierId, orderId, noticeId, cancellationToken);
        if (lockReason is not null) throw new BusinessRuleException(lockReason);
        await _attachments.DeleteForPortalAsync(AttachmentOwnerTypes.DeliveryNoticeFile, attachmentId, notice.Id, session.UserId, cancellationToken);
    }

    /// <summary>
    /// File thêm, xóa được khi phiếu còn chờ nhận (P5-3): đơn Đã duyệt, phiếu chưa hủy, nơi nhận chưa lập XNH theo phiếu. Trả phiếu và lý do khóa
    /// (null = mở).
    /// </summary>
    private async Task<(PurchaseOrderDeliveryNotice Notice, string? LockReason)> FileStateAsync(
        Guid supplierId, Guid orderId, Guid noticeId, CancellationToken cancellationToken)
    {
        var order = await LoadOrderAsync(supplierId, orderId, cancellationToken);
        var notice = await FindNoticeAsync(supplierId, orderId, noticeId, cancellationToken);
        string? reason = notice.Status == DeliveryNoticeStatus.Cancelled ? "Phiếu báo giao đã hủy – file đã khóa."
            : order.Status != ApprovalDocumentStatus.Approved ? "Đơn đã hủy hoặc đã nhận đủ hàng – file đã khóa."
            : await _db.GoodsReceiptConfirmations.IgnoreQueryFilters()
                .AnyAsync(c => c.DeliveryNoticeId == noticeId && c.Status != ReceiptConfirmationStatus.Cancelled, cancellationToken)
                ? "Bên đặt hàng đã lập phiếu nhận hàng theo phiếu báo giao – file đã khóa."
                : null;
        return (notice, reason);
    }

    /// <summary>
    /// Báo thủ kho nơi nhận (quyền lập XNH, kho của PO trong phạm vi) và người lập PO (P5-4) – một người chỉ nhận một thông báo; mở thông báo vào
    /// PO (tab "Giao hàng NCC").
    /// </summary>
    private async Task NotifyAsync(PurchaseOrder order, PurchaseOrderDeliveryNotice notice, bool cancelled, CancellationToken cancellationToken)
    {
        var publisher = new NotificationPublisher(_db, _scope, _clock);
        var receivers = await publisher.UsersWithPermissionAsync(
            order.CompanyId, Permissions.Purchase.ReceiptManage, null, order.WarehouseId, cancellationToken);
        if (order.CreatedBy is { } creator && await _db.Users.AnyAsync(u => u.Id == creator && u.IsActive, cancellationToken))
            receivers.Add(creator);
        receivers = receivers.Distinct().ToList();
        if (receivers.Count == 0) return;
        var supplier = await _db.Suppliers.AsNoTracking().IgnoreQueryFilters().Where(s => s.Id == order.SupplierId)
            .Select(s => s.Code + " – " + s.Name).SingleAsync(cancellationToken);
        var (title, message) = cancelled
            ? ($"NCC hủy báo giao {notice.No} của đơn {order.No}",
                $"{supplier} hủy phiếu báo giao {notice.No} (phiếu giao {notice.DeliveryNoteNo}). Lý do: {notice.CancelReason}")
            : ($"NCC báo giao hàng đơn {order.No} ngày {notice.ExpectedDate:dd/MM/yyyy}",
                $"{supplier} báo giao {notice.Lines.Count} dòng của đơn {order.No} ngày {notice.ExpectedDate:dd/MM/yyyy}, phiếu giao " +
                $"{notice.DeliveryNoteNo} ({notice.No}) – lập phiếu nhận hàng từ tab \"Giao hàng NCC\" của đơn khi hàng đến.");
        publisher.Add(order.CompanyId, receivers, NotificationTypes.PortalDeliveryNotice, title, message, DocumentTypes.PurchaseOrder, order.Id);
    }

    /// <summary>Nạp PO kèm dòng; PO không thuộc NCC / chưa duyệt / mua nội bộ trả 404 như không tồn tại.</summary>
    private async Task<PurchaseOrder> LoadOrderAsync(Guid supplierId, Guid orderId, CancellationToken cancellationToken) =>
        await PortalOrderService.VisibleOrders(_db, supplierId).Include(o => o.Lines).SingleOrDefaultAsync(o => o.Id == orderId, cancellationToken)
        ?? throw new NotFoundException("Không tìm thấy đơn đặt hàng.");

    /// <summary>Phiếu báo giao của đúng đơn và NCC (theo dõi thay đổi để hủy); không thấy trả 404.</summary>
    private async Task<PurchaseOrderDeliveryNotice> FindNoticeAsync(Guid supplierId, Guid orderId, Guid noticeId, CancellationToken cancellationToken) =>
        await _db.PurchaseOrderDeliveryNotices.IgnoreQueryFilters().Include(n => n.Lines)
            .SingleOrDefaultAsync(n => n.Id == noticeId && n.PurchaseOrderId == orderId && n.SupplierId == supplierId, cancellationToken)
        ?? throw new NotFoundException("Không tìm thấy phiếu báo giao.");
}
