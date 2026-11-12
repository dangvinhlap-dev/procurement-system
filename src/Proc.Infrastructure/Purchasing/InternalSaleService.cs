using Microsoft.EntityFrameworkCore;
using Proc.Application.Common;
using Proc.Application.Purchasing;
using Proc.Domain.Common;
using Proc.Domain.Documents;
using Proc.Domain.Notifications;
using Proc.Domain.Organization;
using Proc.Domain.Purchasing;
using Proc.Infrastructure.Notifications;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.Purchasing;

/// <summary>
/// "Đơn bán nội bộ" của công ty bán (lát X7, design.md mục 19.3, 19.4). PO thuộc công ty mua nên đọc bằng cách bỏ filter công ty và filter kho
/// <b>chỉ</b> với PO có <see cref="PurchaseOrder.InternalSellerCompanyId"/> là công ty đang làm việc và đã được duyệt (ngoại lệ đọc theo liên
/// kết như cặp ICS–ICR – CLAUDE.md mục 4); filter nhóm hàng vẫn áp: dòng có mặt hàng ngoài phạm vi thì chỉ xem, không quyết định được.
/// Quyết định ghi vào bảng của công ty bán, không sửa PO đã khóa.
/// </summary>
public sealed class InternalSaleService : IInternalSaleService
{
    private static readonly string[] CrossCompany = [ProcDbContext.CompanyFilter, ProcDbContext.WarehouseFilter];

    private readonly ProcDbContext _db;
    private readonly ICurrentUserScope _scope;
    private readonly IClock _clock;

    /// <summary>Tạo dịch vụ.</summary>
    public InternalSaleService(ProcDbContext db, ICurrentUserScope scope, IClock clock)
    {
        _db = db;
        _scope = scope;
        _clock = clock;
    }

    /// <summary>
    /// PO công ty khác đặt cho công ty đang làm việc, đã qua duyệt (đang hiệu lực, đã đóng, hoặc bị hủy sau khi duyệt – để bên bán biết đơn
    /// đã hủy).
    /// </summary>
    private IQueryable<PurchaseOrder> Orders(Guid companyId) =>
        _db.PurchaseOrders.IgnoreQueryFilters(CrossCompany).AsNoTracking()
            .Where(o => o.InternalSellerCompanyId == companyId && o.ApprovedAt != null
                        && (o.Status == ApprovalDocumentStatus.Approved || o.Status == ApprovalDocumentStatus.Closed
                            || o.Status == ApprovalDocumentStatus.Cancelled));

    /// <inheritdoc />
    public async Task<InternalSaleListResult> SearchAsync(InternalSaleQuery query, CancellationToken cancellationToken)
    {
        var companyId = _scope.RequireCompany();
        var toDate = query.To ?? _clock.TodayInVietnam();
        var fromDate = query.From ?? toDate.AddDays(-90);
        var rows = await (
                from o in Orders(companyId).Where(o => o.Date >= fromDate && o.Date <= toDate)
                join c in _db.Companies on o.CompanyId equals c.Id
                join w in _db.Warehouses.IgnoreQueryFilters(CrossCompany) on o.WarehouseId equals w.Id
                join d in _db.InternalSaleConfirmations on o.Id equals d.PurchaseOrderId into decisions
                from d in decisions.DefaultIfEmpty()
                join u in _db.Users on d.CreatedBy equals u.Id into users
                from u in users.DefaultIfEmpty()
                orderby o.Date descending, o.No descending
                select new
                {
                    o.Id, o.No, o.Date, c.Code, c.Name, Warehouse = w.Code + " – " + w.Name, o.ExpectedDate, Amount = o.TotalAmount + o.TotalVat,
                    LineCount = o.Lines.Count, o.Status, Decision = d == null ? (InternalSaleDecision?)null : d.Decision,
                    DecidedBy = u == null ? null : u.FullName, DecidedAt = d == null ? (DateTimeOffset?)null : d.CreatedAt,
                })
            .Take(PurchaseRequestService.SearchLimit + 1)
            .ToListAsync(cancellationToken);
        var items = rows.Take(PurchaseRequestService.SearchLimit)
            .Select(r => new InternalSaleListItemDto(
                r.Id, r.No, r.Date, r.Code, r.Name, r.Warehouse, r.ExpectedDate, r.Amount, r.LineCount, r.Status.ToString(),
                StatusOf(r.Decision, r.Status), r.DecidedBy, r.DecidedAt))
            .Where(r => string.IsNullOrWhiteSpace(query.Status) || string.Equals(r.Status, query.Status.Trim(), StringComparison.OrdinalIgnoreCase))
            .ToList();
        return new InternalSaleListResult(items, rows.Count > PurchaseRequestService.SearchLimit, PurchaseRequestService.SearchLimit);
    }

    /// <inheritdoc />
    public async Task<InternalSaleDto> GetAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var companyId = _scope.RequireCompany();
        var order = await LoadAsync(companyId, orderId, cancellationToken);
        var itemIds = order.Lines.Select(l => l.ItemId).Distinct().ToList();
        var items = await _db.Items.AsNoTracking().Where(i => itemIds.Contains(i.Id)).Select(i => new { i.Id, i.Code, i.Name }).ToDictionaryAsync(i => i.Id, cancellationToken);
        var uomIds = order.Lines.Select(l => l.UomId).Distinct().ToList();
        var uoms = await _db.Uoms.AsNoTracking().Where(u => uomIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Code, cancellationToken);
        var buyer = await _db.Companies.AsNoTracking().Where(c => c.Id == order.CompanyId).Select(c => new { c.Code, c.Name }).SingleAsync(cancellationToken);
        var warehouse = await _db.Warehouses.IgnoreQueryFilters(CrossCompany).AsNoTracking()
            .Where(w => w.Id == order.WarehouseId).Select(w => w.Code + " – " + w.Name).SingleOrDefaultAsync(cancellationToken);
        var orderedBy = order.CreatedBy is { } createdBy
            ? await _db.Users.AsNoTracking().Where(u => u.Id == createdBy).Select(u => u.FullName).SingleOrDefaultAsync(cancellationToken)
            : null;
        var received = await PurchaseOrderReceipts.ReceivedBaseByLineAsync(_db, order.Lines.Select(l => l.Id).ToList(), null, cancellationToken);
        var decision = await (
                from d in _db.InternalSaleConfirmations.AsNoTracking()
                join u in _db.Users on d.CreatedBy equals u.Id into users
                from u in users.DefaultIfEmpty()
                where d.PurchaseOrderId == order.Id
                select new { d.Decision, d.Note, d.CreatedAt, By = u == null ? null : u.FullName })
            .SingleOrDefaultAsync(cancellationToken);

        var lines = order.Lines.OrderBy(l => l.LineNo).Select(l =>
        {
            var item = items.GetValueOrDefault(l.ItemId);
            return new InternalSaleLineDto(
                l.LineNo, l.ItemId, item?.Code ?? "(ngoài phạm vi)", item?.Name ?? "", uoms.GetValueOrDefault(l.UomId) ?? "", l.Qty, l.UnitPrice,
                l.DiscountPct, l.Amount, l.VatCode, l.VatAmount, l.DeliveryDate,
                Math.Round(received.GetValueOrDefault(l.Id) / l.UomFactor, 4, MidpointRounding.AwayFromZero), l.Note);
        }).ToList();

        var readOnly = ReadOnlyReason(order, decision is not null, items.Count == itemIds.Count);
        return new InternalSaleDto(
            order.Id, order.No, order.Date, order.Status.ToString(), buyer.Code, buyer.Name, warehouse, order.DeliveryAddress, order.ExpectedDate,
            order.PaymentTerms, order.DeliveryTerms, order.Note, orderedBy, order.ApprovedAt, order.TotalAmount, order.TotalVat, lines,
            StatusOf(decision?.Decision, order.Status), decision?.Note, decision?.By, decision?.CreatedAt, readOnly is null, readOnly);
    }

    /// <inheritdoc />
    public Task<InternalSaleDto> ConfirmAsync(Guid orderId, InternalSaleDecisionRequest request, CancellationToken cancellationToken) =>
        DecideAsync(orderId, InternalSaleDecision.Confirmed, request.Note, cancellationToken);

    /// <inheritdoc />
    public Task<InternalSaleDto> RejectAsync(Guid orderId, InternalSaleDecisionRequest request, CancellationToken cancellationToken) =>
        DecideAsync(orderId, InternalSaleDecision.Rejected, request.Note, cancellationToken);

    /// <summary>
    /// Ghi quyết định của công ty bán (kiểm tra lại quyền, trạng thái PO, phạm vi mặt hàng ở server – không tin giao diện) và báo người lập PO
    /// ở công ty mua. Hai người bấm cùng lúc: chỉ mục duy nhất theo PO chặn người thứ hai.
    /// </summary>
    private async Task<InternalSaleDto> DecideAsync(Guid orderId, InternalSaleDecision decision, string? note, CancellationToken cancellationToken)
    {
        var companyId = _scope.RequireCompany();
        if (!_scope.HasPermission(Permissions.Purchase.InternalSaleManage))
            throw new ForbiddenAccessException("Bạn không có quyền xác nhận, từ chối đơn bán nội bộ.");
        var order = await LoadAsync(companyId, orderId, cancellationToken);
        var itemIds = order.Lines.Select(l => l.ItemId).Distinct().ToList();
        var visible = await _db.Items.CountAsync(i => itemIds.Contains(i.Id), cancellationToken) == itemIds.Count;
        var decided = await _db.InternalSaleConfirmations.AnyAsync(d => d.PurchaseOrderId == orderId, cancellationToken);
        if (ReadOnlyReason(order, decided, visible) is { } reason) throw new BusinessRuleException(reason);

        _db.InternalSaleConfirmations.Add(InternalSaleConfirmation.Create(companyId, order.Id, order.No, order.CompanyId, decision, note));
        if (order.CreatedBy is { } creator)
        {
            var seller = await _db.Companies.AsNoTracking().Where(c => c.Id == companyId).Select(c => c.Code + " – " + c.Name).SingleAsync(cancellationToken);
            var confirmed = decision == InternalSaleDecision.Confirmed;
            var detail = string.IsNullOrWhiteSpace(note) ? "" : (confirmed ? " Ghi chú: " : " Lý do: ") + note.Trim();
            new NotificationPublisher(_db, _scope, _clock).Add(order.CompanyId, [creator], NotificationTypes.InternalSaleDecision,
                $"Đơn mua nội bộ {order.No} {(confirmed ? "đã được bên bán xác nhận" : "bị bên bán từ chối")}",
                (confirmed ? $"{seller} đã xác nhận nhận đơn {order.No}." : $"{seller} từ chối đơn {order.No} – hủy đơn hoặc đóng phần còn lại.") + detail,
                DocumentTypes.PurchaseOrder, order.Id);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(orderId, cancellationToken);
    }

    /// <summary>Đọc PO là đơn bán nội bộ của công ty, kèm dòng.</summary>
    /// <exception cref="NotFoundException">Khi không phải đơn bán nội bộ đã duyệt của công ty.</exception>
    private async Task<PurchaseOrder> LoadAsync(Guid companyId, Guid orderId, CancellationToken cancellationToken) =>
        await Orders(companyId).Include(o => o.Lines).SingleOrDefaultAsync(o => o.Id == orderId, cancellationToken)
        ?? throw new NotFoundException("Không tìm thấy đơn bán nội bộ.");

    /// <summary>Vì sao không quyết định được: đã quyết định, PO không còn hiệu lực, thiếu quyền, mặt hàng ngoài phạm vi; null = được.</summary>
    private string? ReadOnlyReason(PurchaseOrder order, bool decided, bool itemsVisible) =>
        decided ? "Đơn đã được xác nhận hoặc từ chối – không đổi được."
        : order.Status == ApprovalDocumentStatus.Cancelled ? "Công ty mua đã hủy đơn."
        : order.Status == ApprovalDocumentStatus.Closed ? "Công ty mua đã đóng đơn (nhận đủ hoặc đóng phần còn lại)."
        : !itemsVisible ? "Đơn có mặt hàng ngoài phạm vi nhóm hàng của bạn nên chỉ xem được."
        : !_scope.HasPermission(Permissions.Purchase.InternalSaleManage) ? "Bạn không có quyền xác nhận, từ chối đơn bán nội bộ."
        : null;

    /// <summary>Trạng thái bên bán: theo quyết định nếu có; chưa quyết định mà PO đã hủy thì Cancelled; còn lại Pending.</summary>
    private static string StatusOf(InternalSaleDecision? decision, ApprovalDocumentStatus orderStatus) =>
        decision?.ToString() ?? (orderStatus == ApprovalDocumentStatus.Cancelled ? "Cancelled" : "Pending");
}
