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
/// Đơn đặt hàng trên Portal (lát P4a – design.md mục 19.5, P4-1…P4-3). Phiên Portal không có công ty / người dùng nội bộ nên mọi truy vấn bỏ
/// bộ lọc toàn cục và **tự lọc theo NCC của phiên** (P1b-4): chỉ PO đặt cho NCC này, đã duyệt (kể cả đã nhận đủ, hủy sau khi duyệt), không phải
/// mua nội bộ. Không trả giá đã duyệt nội bộ, nguồn giá, lý do lệch giá, ngân sách.
/// </summary>
public sealed class PortalOrderService : IPortalOrderService
{
    private const string LockedAfterResponse = "File đã gửi kèm phản hồi – đã khóa.";

    private readonly ProcDbContext _db;
    private readonly IClock _clock;
    private readonly AttachmentService _attachments;
    private readonly ICurrentUserScope _scope;

    /// <summary>Tạo dịch vụ.</summary>
    /// <param name="db">DbContext.</param>
    /// <param name="clock">Đồng hồ.</param>
    /// <param name="attachments">Kho file đính kèm.</param>
    /// <param name="scope">Phạm vi người dùng nội bộ – phiên Portal không có; chỉ dùng cho bộ sinh thông báo.</param>
    public PortalOrderService(ProcDbContext db, IClock clock, AttachmentService attachments, ICurrentUserScope scope)
    {
        _db = db;
        _clock = clock;
        _attachments = attachments;
        _scope = scope;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PortalOrderListItemDto>> ListAsync(PortalSession session, CancellationToken cancellationToken)
    {
        var orders = await VisibleOrders(session.SupplierId)
            .OrderByDescending(o => o.Date).ThenByDescending(o => o.No)
            .Select(o => new
            {
                o.Id, o.No, o.CompanyId, o.Date, o.ExpectedDate, o.Status, o.TotalAmount, o.TotalVat, o.CurrencyCode, LineCount = o.Lines.Count,
            })
            .ToListAsync(cancellationToken);
        var ids = orders.Select(o => o.Id).ToList();
        var current = await Responses(session.SupplierId).Where(r => ids.Contains(r.PurchaseOrderId) && r.ReopenedAt == null)
            .ToDictionaryAsync(r => r.PurchaseOrderId, cancellationToken);
        var companies = await CompanyNamesAsync(orders.Select(o => o.CompanyId), cancellationToken);
        return orders.Select(o =>
            {
                var response = current.GetValueOrDefault(o.Id);
                return new PortalOrderListItemDto(o.Id, o.No, companies.GetValueOrDefault(o.CompanyId) ?? "", o.Date, o.ExpectedDate,
                    StatusOf(o.Status, response), o.LineCount, o.TotalAmount + o.TotalVat, o.CurrencyCode, response?.PromisedDate, response?.RespondedAt);
            })
            .ToList();
    }

    /// <inheritdoc />
    public async Task<PortalOrderDto> GetAsync(PortalSession session, Guid id, CancellationToken cancellationToken) =>
        await ToDtoAsync(session.SupplierId, await LoadAsync(session.SupplierId, id, cancellationToken), cancellationToken);

    /// <inheritdoc />
    public async Task<PortalOrderDto> RespondAsync(
        PortalSession session, Guid id, PortalOrderRespondRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var order = await LoadAsync(session.SupplierId, id, cancellationToken);
        if (!Enum.TryParse<SupplierOrderDecision>(request.Decision, ignoreCase: true, out var decision) || !Enum.IsDefined(decision))
            throw new BusinessRuleException("Phản hồi phải là Xác nhận hoặc Từ chối.");
        if (await Responses(session.SupplierId).AnyAsync(r => r.PurchaseOrderId == id && r.ReopenedAt == null, cancellationToken))
            throw new BusinessRuleException($"Đơn {order.No} đã được phản hồi – liên hệ Phòng Cung ứng vật tư nếu cần thay đổi.");
        var response = PurchaseOrderSupplierResponse.Create(
            order, session.SupplierId, session.UserId, session.Email, decision, request.PromisedDate, request.Note, _clock.UtcNow);
        _db.PurchaseOrderSupplierResponses.Add(response);
        await NotifyBuyerAsync(order, response, cancellationToken);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: Npgsql.PostgresErrorCodes.UniqueViolation })
        {
            // Hai người cùng NCC phản hồi cùng lúc: chỉ mục duy nhất (PO, phản hồi chưa mở lại) chặn bản thứ hai.
            throw new BusinessRuleException($"Có người khác của công ty bạn vừa phản hồi đơn {order.No} – tải lại trang để xem.");
        }

        return await GetAsync(session, id, cancellationToken);
    }

    /// <summary>
    /// Báo người lập PO (người theo dõi đơn); PO không rõ người lập / người lập đã nghỉ thì báo mọi người có quyền lập PO của công ty đặt hàng.
    /// </summary>
    private async Task NotifyBuyerAsync(PurchaseOrder order, PurchaseOrderSupplierResponse response, CancellationToken cancellationToken)
    {
        var publisher = new NotificationPublisher(_db, _scope, _clock);
        List<Guid> receivers = order.CreatedBy is { } creator && await _db.Users.AnyAsync(u => u.Id == creator && u.IsActive, cancellationToken)
            ? [creator]
            : await publisher.UsersWithPermissionAsync(order.CompanyId, Permissions.Purchase.PoCreate, null, null, cancellationToken);
        var supplier = await _db.Suppliers.AsNoTracking().IgnoreQueryFilters().Where(s => s.Id == order.SupplierId)
            .Select(s => s.Code + " – " + s.Name).SingleAsync(cancellationToken);
        var confirmed = response.Decision == SupplierOrderDecision.Confirmed;
        var message = confirmed
            ? $"{supplier} xác nhận đơn {order.No}, giao ngày {response.PromisedDate:dd/MM/yyyy}." + (response.Note is null ? "" : $" Ghi chú: {response.Note}")
            : $"{supplier} từ chối đơn {order.No}. Lý do: {response.Note} – hủy đơn, hoàn thành PO hoặc mở lại phản hồi.";
        publisher.Add(order.CompanyId, receivers, NotificationTypes.PortalOrderResponse,
            $"Đơn {order.No} {(confirmed ? "được NCC xác nhận" : "bị NCC từ chối")}", message, DocumentTypes.PurchaseOrder, order.Id);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AttachmentDto>> ListAttachmentsAsync(PortalSession session, Guid id, CancellationToken cancellationToken)
    {
        var order = await LoadAsync(session.SupplierId, id, cancellationToken);
        var (openReason, lastResponseAt) = await FileStateAsync(session.SupplierId, order, cancellationToken);
        var files = await _attachments.ListForPortalAsync(AttachmentOwnerTypes.PoSupplierFile, order.Id, openReason, cancellationToken);
        // File gửi trước một lần phản hồi (kể cả phản hồi đã mở lại) là chứng cứ của lần đó – khóa vĩnh viễn; chỉ file thêm sau mới xóa được.
        return files.Select(f => f.CanDelete && lastResponseAt is { } last && f.UploadedAt <= last
                ? f with { CanDelete = false, LockReason = LockedAfterResponse }
                : f)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<AttachmentDto> UploadAttachmentAsync(
        PortalSession session, Guid id, string fileName, long sizeBytes, Stream content, string? note, CancellationToken cancellationToken)
    {
        var order = await LoadAsync(session.SupplierId, id, cancellationToken);
        var (openReason, _) = await FileStateAsync(session.SupplierId, order, cancellationToken);
        if (openReason is not null) throw new BusinessRuleException(openReason);
        return await _attachments.UploadForPortalAsync(
            AttachmentOwnerTypes.PoSupplierFile, order.Id, order.CompanyId, session.UserId, fileName, sizeBytes, content, note, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<AttachmentContent> OpenAttachmentAsync(PortalSession session, Guid id, Guid attachmentId, CancellationToken cancellationToken)
    {
        var order = await LoadAsync(session.SupplierId, id, cancellationToken);
        return await _attachments.OpenForPortalAsync(AttachmentOwnerTypes.PoSupplierFile, attachmentId, order.Id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteAttachmentAsync(PortalSession session, Guid id, Guid attachmentId, CancellationToken cancellationToken)
    {
        var files = await ListAttachmentsAsync(session, id, cancellationToken);
        var file = files.FirstOrDefault(f => f.Id == attachmentId) ?? throw new NotFoundException("Không tìm thấy file đính kèm.");
        if (!file.CanDelete) throw new BusinessRuleException(file.LockReason ?? LockedAfterResponse);
        await _attachments.DeleteForPortalAsync(AttachmentOwnerTypes.PoSupplierFile, attachmentId, id, session.UserId, cancellationToken);
    }

    /// <summary>
    /// File thêm, xóa được khi đơn còn chờ phản hồi (P4-3): PO Đã duyệt và chưa có phản hồi đang hiệu lực. Trả lý do khóa (null = mở) và lúc
    /// phản hồi gần nhất (kể cả đã mở lại) – file trước mốc đó luôn khóa.
    /// </summary>
    private async Task<(string? LockReason, DateTimeOffset? LastResponseAt)> FileStateAsync(
        Guid supplierId, PurchaseOrder order, CancellationToken cancellationToken)
    {
        var responses = await Responses(supplierId).Where(r => r.PurchaseOrderId == order.Id)
            .Select(r => new { r.RespondedAt, r.ReopenedAt }).ToListAsync(cancellationToken);
        var last = responses.Count == 0 ? (DateTimeOffset?)null : responses.Max(r => r.RespondedAt);
        string? reason = order.Status != ApprovalDocumentStatus.Approved
            ? "Đơn đã hủy hoặc đã nhận đủ hàng – file đã khóa."
            : responses.Any(r => r.ReopenedAt is null) ? "Đơn đã được phản hồi – file đã khóa." : null;
        return (reason, last);
    }

    /// <summary>PO NCC được thấy: đặt cho NCC này, đã duyệt / đã nhận đủ / hủy sau khi duyệt, không phải mua nội bộ (P4-1).</summary>
    private IQueryable<PurchaseOrder> VisibleOrders(Guid supplierId) => VisibleOrders(_db, supplierId);

    /// <summary>PO NCC được thấy (P4-1) – dùng chung với báo giao Portal (lát P5).</summary>
    /// <param name="db">DbContext.</param>
    /// <param name="supplierId">NCC của phiên.</param>
    /// <returns>Truy vấn PO (không theo dõi thay đổi, bỏ filter).</returns>
    internal static IQueryable<PurchaseOrder> VisibleOrders(ProcDbContext db, Guid supplierId) =>
        db.PurchaseOrders.AsNoTracking().IgnoreQueryFilters()
            .Where(o => o.SupplierId == supplierId && o.InternalSellerCompanyId == null
                        && (o.Status == ApprovalDocumentStatus.Approved || o.Status == ApprovalDocumentStatus.Closed
                            || (o.Status == ApprovalDocumentStatus.Cancelled && o.ApprovedAt != null)));

    private IQueryable<PurchaseOrderSupplierResponse> Responses(Guid supplierId) =>
        _db.PurchaseOrderSupplierResponses.AsNoTracking().IgnoreQueryFilters().Where(r => r.SupplierId == supplierId);

    /// <summary>Nạp PO kèm dòng; PO không thuộc NCC / chưa duyệt / mua nội bộ trả 404 như không tồn tại.</summary>
    private async Task<PurchaseOrder> LoadAsync(Guid supplierId, Guid id, CancellationToken cancellationToken)
    {
        return await VisibleOrders(supplierId).Include(o => o.Lines).SingleOrDefaultAsync(o => o.Id == id, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy đơn đặt hàng.");
    }

    private async Task<Dictionary<Guid, string>> CompanyNamesAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken)
    {
        var list = ids.Distinct().ToList();
        return await _db.Companies.AsNoTracking().IgnoreQueryFilters().Where(c => list.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);
    }

    /// <summary>
    /// Trạng thái NCC thấy: Cancelled (bên đặt hủy), Received (đã nhận đủ / hoàn thành), Confirmed / Rejected theo phản hồi đang hiệu lực,
    /// còn lại Pending (chờ phản hồi, kể cả sau khi bên đặt mở lại).
    /// </summary>
    private static string StatusOf(ApprovalDocumentStatus status, PurchaseOrderSupplierResponse? current) => status switch
    {
        ApprovalDocumentStatus.Cancelled => "Cancelled",
        ApprovalDocumentStatus.Closed => "Received",
        _ when current is not null => current.Decision.ToString(),
        _ => "Pending",
    };

    private async Task<PortalOrderDto> ToDtoAsync(Guid supplierId, PurchaseOrder order, CancellationToken cancellationToken)
    {
        var itemIds = order.Lines.Select(l => l.ItemId).Distinct().ToList();
        var items = await _db.Items.AsNoTracking().IgnoreQueryFilters().Where(i => itemIds.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, i => new { i.Code, i.Name }, cancellationToken);
        var uomIds = order.Lines.Select(l => l.UomId).Distinct().ToList();
        var uoms = await _db.Uoms.AsNoTracking().IgnoreQueryFilters().Where(u => uomIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Code, cancellationToken);
        var place = await _db.Warehouses.AsNoTracking().IgnoreQueryFilters().Where(w => w.Id == order.WarehouseId)
            .Select(w => w.Name).SingleOrDefaultAsync(cancellationToken);
        var company = (await CompanyNamesAsync([order.CompanyId], cancellationToken)).GetValueOrDefault(order.CompanyId) ?? "";
        var responses = await Responses(supplierId).Where(r => r.PurchaseOrderId == order.Id)
            .OrderByDescending(r => r.RespondedAt).ToListAsync(cancellationToken);
        var current = responses.FirstOrDefault(r => r.IsCurrent);
        var quantities = await DeliveryNoticeQueries.QuantitiesAsync(_db, order, cancellationToken);
        var lines = order.Lines.OrderBy(l => l.LineNo).Select(l => new PortalOrderLineDto(
                l.LineNo, items.GetValueOrDefault(l.ItemId)?.Code ?? "", items.GetValueOrDefault(l.ItemId)?.Name ?? "", l.SupplierItemCode,
                uoms.GetValueOrDefault(l.UomId) ?? "", l.Qty, l.UnitPrice, l.DiscountPct, l.VatCode, l.Amount, l.VatAmount, l.DeliveryDate, l.Note,
                l.Id, quantities[l.Id].Received, quantities[l.Id].Deliverable))
            .ToList();
        var notices = await NoticesAsync(supplierId, order, items.ToDictionary(i => i.Key, i => (i.Value.Code, i.Value.Name)), uoms, cancellationToken);
        var blocked = DeliveryBlockedReason(order, current, lines);
        return new PortalOrderDto(
            order.Id, order.No, company, order.Date, order.ApprovedAt, order.ExpectedDate, place, order.DeliveryAddress, order.PaymentTerms,
            order.DeliveryTerms, order.Note, order.CurrencyCode, StatusOf(order.Status, current),
            order.Status == ApprovalDocumentStatus.Approved && current is null,
            order.GrossAmount, order.TotalDiscount, order.TotalAmount, order.TotalVat, lines,
            responses.Select(r => new PortalOrderResponseDto(
                r.Decision.ToString(), r.PromisedDate, r.Note, r.RespondedAt, r.RespondedByEmail, r.ReopenedAt, r.ReopenReason)).ToList(),
            _attachments.MaxFileSizeMb, blocked is null, blocked, notices);
    }

    /// <summary>
    /// Lý do NCC chưa báo giao được (P5-1); null = được: đơn Đã duyệt, NCC không đang từ chối đơn, còn dòng chưa báo giao hết.
    /// </summary>
    private static string? DeliveryBlockedReason(PurchaseOrder order, PurchaseOrderSupplierResponse? current, IReadOnlyList<PortalOrderLineDto> lines) =>
        order.Status != ApprovalDocumentStatus.Approved ? "Đơn đã nhận đủ hàng, đã hoàn thành hoặc đã hủy."
        : current?.Decision == SupplierOrderDecision.Rejected ? "Bạn đã từ chối đơn này – liên hệ Phòng Cung ứng vật tư để mở lại phản hồi nếu vẫn giao."
        : lines.All(l => l.DeliverableQty <= 0) ? "Mọi dòng đã được báo giao hoặc đã nhận đủ."
        : null;

    /// <summary>Các phiếu báo giao của đơn (mới trước) kèm XNH và SL bên đặt đã nhận theo từng dòng (P5-6).</summary>
    private async Task<IReadOnlyList<PortalDeliveryNoticeDto>> NoticesAsync(
        Guid supplierId, PurchaseOrder order, Dictionary<Guid, (string Code, string Name)> items, Dictionary<Guid, string> uoms,
        CancellationToken cancellationToken)
    {
        var notices = await _db.PurchaseOrderDeliveryNotices.AsNoTracking().IgnoreQueryFilters().Include(n => n.Lines)
            .Where(n => n.PurchaseOrderId == order.Id && n.SupplierId == supplierId)
            .OrderByDescending(n => n.SubmittedAt).ToListAsync(cancellationToken);
        if (notices.Count == 0) return [];
        var receipts = await DeliveryNoticeQueries.ReceiptsAsync(_db, notices.Select(n => n.Id).ToList(), cancellationToken);
        // SL đạt / không đạt chỉ lấy từ XNH đã xác nhận (XNH nháp còn sửa được – chưa báo NCC).
        var confirmedIds = receipts.Values.Where(r => r.Status == ReceiptConfirmationStatus.Confirmed).Select(r => r.ReceiptId).ToList();
        var receiptLines = await _db.Set<GoodsReceiptConfirmationLine>().AsNoTracking().IgnoreQueryFilters()
            .Where(l => confirmedIds.Contains(l.ConfirmationId))
            .Select(l => new { l.ConfirmationId, l.PurchaseOrderLineId, l.ReceivedQty, l.RejectedQty })
            .ToListAsync(cancellationToken);
        var poLines = order.Lines.ToDictionary(l => l.Id);
        return notices.Select(n =>
            {
                var receipt = receipts.GetValueOrDefault(n.Id);
                var confirmed = receipt?.Status == ReceiptConfirmationStatus.Confirmed;
                var lines = n.Lines.OrderBy(l => l.LineNo).Select(l =>
                    {
                        var poLine = poLines[l.PurchaseOrderLineId];
                        var got = confirmed
                            ? receiptLines.FirstOrDefault(r => r.ConfirmationId == receipt!.ReceiptId && r.PurchaseOrderLineId == l.PurchaseOrderLineId)
                            : null;
                        return new PortalDeliveryNoticeLineDto(
                            l.LineNo, poLine.LineNo, items.GetValueOrDefault(poLine.ItemId).Code ?? "", items.GetValueOrDefault(poLine.ItemId).Name ?? "",
                            uoms.GetValueOrDefault(poLine.UomId) ?? "", l.Qty, confirmed ? got?.ReceivedQty ?? 0 : null,
                            confirmed ? got?.RejectedQty ?? 0 : null, l.Note);
                    })
                    .ToList();
                return new PortalDeliveryNoticeDto(
                    n.Id, n.No, n.ExpectedDate, n.DeliveryNoteNo, n.Deliverer, n.Note, DeliveryNoticeQueries.StatusOf(n, receipt), n.SubmittedAt,
                    n.SubmittedByEmail, n.CancelledAt, n.CancelReason, receipt?.No, confirmed ? receipt!.Date : null,
                    n.Status == DeliveryNoticeStatus.Submitted && receipt is null, lines);
            })
            .ToList();
    }
}
