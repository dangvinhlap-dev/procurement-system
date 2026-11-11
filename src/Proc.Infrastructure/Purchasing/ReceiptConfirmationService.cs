using Microsoft.EntityFrameworkCore;
using Proc.Application.Common;
using Proc.Application.Documents;
using Proc.Application.Purchasing;
using Proc.Domain.Common;
using Proc.Domain.Documents;
using Proc.Domain.Organization;
using Proc.Domain.Purchasing;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.Purchasing;

/// <summary>
/// Phiếu xác nhận nhận hàng XNH (lát K2, design.md mục 19.4; Q8). Thay phiếu nhập kho: không ghi sổ, không tồn kho. Nguồn "SL đã nhận" của
/// PO là SL thực nhận của XNH đã xác nhận (<see cref="PurchaseOrderReceipts"/>). Lập, xác nhận, điều chỉnh, cập nhật hóa đơn cần nơi nhận
/// (kho nhận của PO) trong phạm vi kho của người dùng; xem và nghiệm thu chỉ theo công ty (đơn vị đặt hàng thường không có phạm vi kho).
/// </summary>
public sealed class ReceiptConfirmationService : IReceiptConfirmationService
{
    /// <summary>Số phiếu tối đa mỗi lần xem danh sách.</summary>
    public const int SearchLimit = 500;

    private readonly ProcDbContext _db;
    private readonly ICurrentUserScope _scope;
    private readonly IClock _clock;
    private readonly IDocumentNumberService _numbers;

    /// <summary>Tạo dịch vụ.</summary>
    /// <param name="db">DbContext đã gắn phạm vi người dùng.</param>
    /// <param name="scope">Phạm vi người dùng.</param>
    /// <param name="clock">Đồng hồ.</param>
    /// <param name="numbers">Đánh số chứng từ.</param>
    public ReceiptConfirmationService(ProcDbContext db, ICurrentUserScope scope, IClock clock, IDocumentNumberService numbers)
    {
        _db = db;
        _scope = scope;
        _clock = clock;
        _numbers = numbers;
    }

    /// <inheritdoc />
    public async Task<ReceiptConfirmationListResult> SearchAsync(ReceiptConfirmationQuery query, CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        var to = query.To ?? _clock.TodayInVietnam();
        var from = query.From ?? to.AddDays(-90);
        var set = _db.GoodsReceiptConfirmations.AsNoTracking().Where(c => c.Date >= from && c.Date <= to);
        if (!string.IsNullOrWhiteSpace(query.Status)) set = set.Where(c => c.Status == ParseStatus(query.Status));
        if (query.PurchaseOrderId is { } orderId) set = set.Where(c => c.PurchaseOrderId == orderId);
        var rows = await (
                from c in set
                join o in _db.PurchaseOrders on c.PurchaseOrderId equals o.Id
                join s in _db.Suppliers on c.SupplierId equals s.Id
                join w in _db.Warehouses.IgnoreQueryFilters([ProcDbContext.WarehouseFilter]) on c.WarehouseId equals w.Id
                join u in _db.Users on c.CreatedBy equals u.Id into users
                from u in users.DefaultIfEmpty()
                orderby c.Date descending, c.No descending
                select new ReceiptConfirmationListItemDto(
                    c.Id, c.No, c.Date, c.Status.ToString(), c.PurchaseOrderId, o.No, s.Code, s.Name, w.Code, c.DeliveryNoteNo, c.InvoiceNo,
                    c.TotalAmount + c.TotalVat, c.Acceptance.ToString(), u == null ? null : u.FullName))
            .Take(SearchLimit + 1)
            .ToListAsync(cancellationToken);
        return new ReceiptConfirmationListResult(rows.Take(SearchLimit).ToList(), rows.Count > SearchLimit, SearchLimit);
    }

    /// <inheritdoc />
    public async Task<ReceiptConfirmationDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        var confirmation = await _db.GoodsReceiptConfirmations.AsNoTracking().Include(c => c.Lines).Include(c => c.Adjustments)
                               .AsSplitQuery().SingleOrDefaultAsync(c => c.Id == id, cancellationToken)
                           ?? throw new NotFoundException("Không tìm thấy phiếu xác nhận nhận hàng.");
        await EnsureItemsVisibleAsync(confirmation.Lines.Select(l => l.ItemId), cancellationToken);
        return await ToDtoAsync(confirmation, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ReceiptConfirmationDto> CreateAsync(SaveReceiptConfirmationRequest request, CancellationToken cancellationToken)
    {
        var companyId = _scope.RequireCompany();
        await using var transaction = await RowLocks.BeginOrJoinAsync(_db, cancellationToken);
        var order = await LoadOrderAsync(request.PurchaseOrderId, cancellationToken);
        GoodsReceiptConfirmation.EnsureReceivable(order);
        var no = await _numbers.NextAsync(DocumentTypes.GoodsReceiptConfirmation, request.Date, cancellationToken);
        var confirmation = GoodsReceiptConfirmation.Create(companyId, no, order, ToHeader(request));
        Apply(confirmation, order, request);
        await CheckOrderQuantitiesAsync(order, confirmation, null, cancellationToken);
        _db.GoodsReceiptConfirmations.Add(confirmation);
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetAsync(confirmation.Id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ReceiptConfirmationDto> UpdateAsync(Guid id, SaveReceiptConfirmationRequest request, CancellationToken cancellationToken)
    {
        var confirmation = await LoadAsync(id, cancellationToken);
        var order = await LoadOrderAsync(confirmation.PurchaseOrderId, cancellationToken);
        confirmation.Update(ToHeader(request));
        Apply(confirmation, order, request);
        await CheckOrderQuantitiesAsync(order, confirmation, confirmation.Id, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ReceiptConfirmationDto> ConfirmAsync(Guid id, CancellationToken cancellationToken)
    {
        var userId = _scope.UserId ?? throw new ForbiddenAccessException("Chưa đăng nhập.");
        await using var transaction = await RowLocks.BeginOrJoinAsync(_db, cancellationToken);
        var confirmation = await LoadAsync(id, cancellationToken);
        // Khóa PO tới hết giao dịch: hai XNH của cùng PO xác nhận cùng lúc phải lần lượt, phiếu sau thấy SL phiếu trước – không vượt SL đặt +
        // dung sai (như lỗi L1 của phiếu nhập kho cũ, design.md mục 18.26).
        await RowLocks.LockAsync<PurchaseOrder>(_db, [confirmation.PurchaseOrderId], cancellationToken);
        var order = await LoadOrderAsync(confirmation.PurchaseOrderId, cancellationToken);
        GoodsReceiptConfirmation.EnsureReceivable(order);
        await CheckOrderQuantitiesAsync(order, confirmation, confirmation.Id, cancellationToken);
        var parameter = await _db.CompanyParameters.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        // Tách người đặt hàng và người xác nhận giao nhận (tham số công ty – design.md mục 18.14, G4).
        if (parameter?.SeparatePoCreatorFromReceipt == true && order.CreatedBy == userId)
            throw new BusinessRuleException(
                $"Bạn là người lập đơn mua hàng {order.No} nên không xác nhận nhận hàng của đơn này – người khác phải xác nhận (tham số công ty).");
        confirmation.Confirm(userId, _clock.UtcNow, parameter?.RequireReceiptQc == true);
        await _db.SaveChangesAsync(cancellationToken);
        await SyncOrderStatusAsync(order, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ReceiptConfirmationDto> CancelAsync(Guid id, CancelReceiptConfirmationRequest request, CancellationToken cancellationToken)
    {
        var confirmation = await LoadAsync(id, cancellationToken);
        // Quyền hủy + lý do bắt buộc (design.md mục 20.2a): nháp – người lập; đã gửi duyệt trở đi – chỉ quản trị.
        var reason = DocumentCancelPolicy.Authorize(
            _scope, $"phiếu {confirmation.No}", confirmation.Status == ReceiptConfirmationStatus.Draft, confirmation.CreatedBy, request.Reason);
        confirmation.Cancel(reason);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ReceiptConfirmationDto> UpdateInvoiceAsync(Guid id, UpdateReceiptConfirmationInvoiceRequest request, CancellationToken cancellationToken)
    {
        var confirmation = await LoadAsync(id, cancellationToken);
        confirmation.UpdateInvoice(new ReceiptConfirmationInvoice(request.InvoiceNo, request.InvoiceSeries, request.InvoiceDate, request.InvoiceAmount));
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ReceiptConfirmationDto> ConfirmAcceptanceAsync(Guid id, ReceiptAcceptanceRequest request, CancellationToken cancellationToken)
    {
        // Đơn vị đặt hàng nghiệm thu – không đòi phạm vi kho nơi nhận.
        var confirmation = await LoadAsync(id, cancellationToken, requireWarehouse: false);
        confirmation.ConfirmAcceptance(
            request.Accepted, request.Note, _scope.UserId ?? throw new ForbiddenAccessException("Chưa đăng nhập."), _clock.UtcNow);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ReceiptConfirmationDto> AdjustAsync(Guid id, AdjustReceiptRequest request, CancellationToken cancellationToken)
    {
        var userId = _scope.UserId ?? throw new ForbiddenAccessException("Chưa đăng nhập.");
        await using var transaction = await RowLocks.BeginOrJoinAsync(_db, cancellationToken);
        var confirmation = await LoadAsync(id, cancellationToken);
        await RowLocks.LockAsync<PurchaseOrder>(_db, [confirmation.PurchaseOrderId], cancellationToken);
        var order = await LoadOrderAsync(confirmation.PurchaseOrderId, cancellationToken);
        if (request.Qty > 0)
        {
            // Ghi bổ sung là nhận thêm hàng: PO phải còn nhận được (chưa hủy, chưa hoàn thành) và không vượt SL đặt + dung sai.
            if (order.Status == ApprovalDocumentStatus.Cancelled || order.IsClosedManually)
                throw new BusinessRuleException($"Đơn {order.No} đã hủy hoặc đã hoàn thành – không ghi tăng SL thực nhận; NCC giao bù thì lập PO mới.");
        }

        confirmation.Adjust(request.LineId, request.Qty, request.Reason, userId, _clock.UtcNow);
        if (request.Qty > 0) await CheckOrderQuantitiesAsync(order, confirmation, confirmation.Id, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        await SyncOrderStatusAsync(order, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <summary>Đổi trạng thái lọc từ chuỗi.</summary>
    /// <exception cref="BusinessRuleException">Khi không hợp lệ.</exception>
    internal static ReceiptConfirmationStatus ParseStatus(string status) =>
        Enum.TryParse<ReceiptConfirmationStatus>(status, true, out var parsed) && Enum.IsDefined(parsed)
            ? parsed
            : throw new BusinessRuleException("Trạng thái phiếu nhận hàng không hợp lệ.");

    private static ReceiptConfirmationHeader ToHeader(SaveReceiptConfirmationRequest request) =>
        new(request.Date, request.DeliveryNoteNo, request.Deliverer, request.Note);

    /// <summary>Gán dòng (mặt hàng, giá theo dòng PO) và hóa đơn từ request.</summary>
    private static void Apply(GoodsReceiptConfirmation confirmation, PurchaseOrder order, SaveReceiptConfirmationRequest request)
    {
        confirmation.SetLines(order, request.Lines.Select(l => new ReceiptConfirmationLineData(
                l.PurchaseOrderLineId, l.DeliveredQty, l.AcceptedQty, l.RejectedQty,
                string.IsNullOrWhiteSpace(l.CheckResult) ? ReceiptCheckResult.NotChecked : Enum.Parse<ReceiptCheckResult>(l.CheckResult),
                l.CheckNote, l.Note))
            .ToList());
        confirmation.UpdateInvoice(new ReceiptConfirmationInvoice(request.InvoiceNo, request.InvoiceSeries, request.InvoiceDate, request.InvoiceAmount));
    }

    /// <summary>
    /// Nạp phiếu để ghi (kèm dòng, điều chỉnh). Mặc định đòi nơi nhận trong phạm vi kho và mặt hàng trong phạm vi nhóm hàng của người dùng.
    /// </summary>
    private async Task<GoodsReceiptConfirmation> LoadAsync(Guid id, CancellationToken cancellationToken, bool requireWarehouse = true)
    {
        _scope.RequireCompany();
        var confirmation = await _db.GoodsReceiptConfirmations.Include(c => c.Lines).Include(c => c.Adjustments).AsSplitQuery()
                               .SingleOrDefaultAsync(c => c.Id == id, cancellationToken)
                           ?? throw new NotFoundException("Không tìm thấy phiếu xác nhận nhận hàng.");
        if (requireWarehouse) await EnsureWarehouseAsync(confirmation.WarehouseId, cancellationToken);
        await EnsureItemsVisibleAsync(confirmation.Lines.Select(l => l.ItemId), cancellationToken);
        return confirmation;
    }

    /// <summary>Nạp PO (theo dõi thay đổi để đổi trạng thái nhận đủ), kiểm tra nơi nhận và mặt hàng trong phạm vi người dùng.</summary>
    private async Task<PurchaseOrder> LoadOrderAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var order = await _db.PurchaseOrders.Include(o => o.Lines).SingleOrDefaultAsync(o => o.Id == orderId, cancellationToken)
                    ?? throw new NotFoundException("Không tìm thấy đơn mua hàng.");
        await EnsureWarehouseAsync(order.WarehouseId, cancellationToken);
        await EnsureItemsVisibleAsync(order.Lines.Select(l => l.ItemId), cancellationToken);
        return order;
    }

    /// <summary>Nơi nhận phải trong phạm vi kho của người dùng (filter kho) – thủ kho chỉ xác nhận hàng về kho mình.</summary>
    private async Task EnsureWarehouseAsync(Guid warehouseId, CancellationToken cancellationToken)
    {
        if (!await _db.Warehouses.AnyAsync(w => w.Id == warehouseId, cancellationToken))
            throw new ForbiddenAccessException("Nơi nhận của đơn ngoài phạm vi kho của bạn nên bạn không lập/xác nhận phiếu nhận hàng được.");
    }

    /// <summary>Chặn khi phiếu có mặt hàng ngoài phạm vi nhóm hàng của người dùng.</summary>
    private async Task EnsureItemsVisibleAsync(IEnumerable<Guid> itemIds, CancellationToken cancellationToken)
    {
        var ids = itemIds.Distinct().ToList();
        if (await _db.Items.CountAsync(i => ids.Contains(i.Id), cancellationToken) < ids.Count)
            throw new ForbiddenAccessException("Phiếu có mặt hàng ngoài phạm vi nhóm hàng của bạn nên bạn không thao tác được.");
    }

    /// <summary>
    /// SL thực nhận (XNH đã xác nhận khác + phiếu này) không vượt SL đặt × (1 + dung sai %), theo ĐVT cơ sở – như phiếu nhập kho cũ
    /// (design.md mục 6.1). Ví dụ đặt 100, dung sai 5%, đã nhận 60 → phiếu này nhận tối đa 45.
    /// </summary>
    private async Task CheckOrderQuantitiesAsync(
        PurchaseOrder order, GoodsReceiptConfirmation confirmation, Guid? excludeId, CancellationToken cancellationToken)
    {
        var received = await PurchaseOrderReceipts.ReceivedBaseByLineAsync(_db, order.Lines.Select(l => l.Id).ToList(), excludeId, cancellationToken);
        foreach (var line in confirmation.Lines)
        {
            var poLine = order.Lines.Single(p => p.Id == line.PurchaseOrderLineId);
            var allowed = decimal.Round(poLine.BaseQty * (100m + order.ReceiveTolerancePct) / 100m, 4, MidpointRounding.AwayFromZero);
            var remaining = allowed - received.GetValueOrDefault(poLine.Id);
            if (line.ReceivedBaseQty > remaining)
                throw new BusinessRuleException(
                    $"Dòng {line.LineNo}: vượt SL còn nhận được của đơn {order.No} dòng {poLine.LineNo} " +
                    $"({Math.Max(0, remaining / poLine.UomFactor):0.####}, dung sai {order.ReceiveTolerancePct:0.##}%).");
        }
    }

    /// <summary>
    /// Sau xác nhận/điều chỉnh (đã SaveChanges trong cùng giao dịch): mọi dòng PO nhận đủ thì PO Đã nhận đủ, ngược lại về Đã duyệt (trừ PO đã
    /// hoàn thành bằng tay – giữ nguyên).
    /// </summary>
    private async Task SyncOrderStatusAsync(PurchaseOrder order, CancellationToken cancellationToken)
    {
        var received = await PurchaseOrderReceipts.ReceivedBaseByLineAsync(_db, order.Lines.Select(l => l.Id).ToList(), null, cancellationToken);
        if (order.Lines.All(l => received.GetValueOrDefault(l.Id) >= l.BaseQty)) order.CloseReceived();
        else order.ReopenReceiving();
    }

    private async Task<ReceiptConfirmationDto> ToDtoAsync(GoodsReceiptConfirmation c, CancellationToken cancellationToken)
    {
        var order = await _db.PurchaseOrders.AsNoTracking().Include(o => o.Lines).SingleAsync(o => o.Id == c.PurchaseOrderId, cancellationToken);
        var supplier = await _db.Suppliers.AsNoTracking().Where(s => s.Id == c.SupplierId).Select(s => new { s.Code, s.Name })
            .SingleAsync(cancellationToken);
        var warehouse = await _db.Warehouses.IgnoreQueryFilters([ProcDbContext.WarehouseFilter]).AsNoTracking().Where(w => w.Id == c.WarehouseId)
            .Select(w => new { w.Code, w.Name }).SingleOrDefaultAsync(cancellationToken);
        var department = order.DepartmentId is { } departmentId
            ? await _db.Departments.AsNoTracking().Where(d => d.Id == departmentId).Select(d => d.Name).SingleOrDefaultAsync(cancellationToken)
            : null;
        var itemIds = c.Lines.Select(l => l.ItemId).Distinct().ToList();
        var items = await _db.Items.AsNoTracking().Where(i => itemIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, i => new { i.Code, i.Name }, cancellationToken);
        var uomIds = c.Lines.Select(l => l.UomId).Distinct().ToList();
        var uoms = await _db.Uoms.AsNoTracking().Where(u => uomIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Code, cancellationToken);
        var userIds = new[] { c.CreatedBy, c.ConfirmedBy, c.AcceptedBy }.OfType<Guid>().Concat(c.Adjustments.Select(a => a.AdjustedBy)).Distinct().ToList();
        var users = await _db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken);
        // SL thực nhận ở các XNH khác (đã xác nhận) – để người nhận thấy còn bao nhiêu.
        var otherBase = await PurchaseOrderReceipts.ReceivedBaseByLineAsync(_db, order.Lines.Select(l => l.Id).ToList(), c.Id, cancellationToken);
        string? Name(Guid? id) => id is { } x ? users.GetValueOrDefault(x) : null;

        var lines = c.Lines.OrderBy(l => l.LineNo).Select(l =>
        {
            var poLine = order.Lines.Single(p => p.Id == l.PurchaseOrderLineId);
            var other = poLine.UomFactor == 0 ? 0m : decimal.Round(otherBase.GetValueOrDefault(poLine.Id) / poLine.UomFactor, 4, MidpointRounding.AwayFromZero);
            var thisReceived = c.Status == ReceiptConfirmationStatus.Cancelled ? 0m : l.ReceivedQty;
            var item = items.GetValueOrDefault(l.ItemId);
            return new ReceiptConfirmationLineDto(
                l.Id, l.LineNo, l.PurchaseOrderLineId, poLine.LineNo, l.ItemId, item?.Code ?? string.Empty, item?.Name ?? string.Empty,
                uoms.GetValueOrDefault(l.UomId) ?? string.Empty, poLine.Qty, other, Math.Max(0m, poLine.Qty - other - thisReceived), l.DeliveredQty,
                l.AcceptedQty, l.RejectedQty, l.AdjustedQty, l.ReceivedQty, l.UnitPrice, l.VatCode, l.Amount, l.VatAmount, l.CheckResult.ToString(),
                l.CheckNote, l.Note);
        }).ToList();
        var adjustments = c.Adjustments.OrderBy(a => a.AdjustedAt)
            .Select(a => new ReceiptAdjustmentDto(a.Id, a.LineNo, a.Qty, a.Reason, a.AdjustedAt, Name(a.AdjustedBy)))
            .ToList();
        return new ReceiptConfirmationDto(
            c.Id, c.No, c.Date, c.Status.ToString(), c.PurchaseOrderId, order.No, order.Date, c.SupplierId, supplier.Code, supplier.Name, c.WarehouseId,
            warehouse?.Code, warehouse?.Name, department, c.DeliveryNoteNo, c.Deliverer, c.InvoiceNo, c.InvoiceSeries, c.InvoiceDate, c.InvoiceAmount,
            c.Note, c.CancelReason, c.TotalAmount, c.TotalVat, c.CreatedAt, Name(c.CreatedBy), c.ConfirmedAt, Name(c.ConfirmedBy),
            c.Acceptance.ToString(), c.AcceptanceNote, c.AcceptedAt, Name(c.AcceptedBy), lines, adjustments,
            await ActionsAsync(c, cancellationToken), c.Version);
    }

    /// <summary>Thao tác người xem được làm (giao diện bật nút); máy chủ vẫn kiểm tra lại khi ghi.</summary>
    private async Task<ReceiptConfirmationActions> ActionsAsync(GoodsReceiptConfirmation c, CancellationToken cancellationToken)
    {
        var manage = _scope.HasPermission(Permissions.Purchase.ReceiptManage)
                     && await _db.Warehouses.AnyAsync(w => w.Id == c.WarehouseId, cancellationToken);
        var draft = c.Status == ReceiptConfirmationStatus.Draft;
        var confirmed = c.Status == ReceiptConfirmationStatus.Confirmed;
        var userId = _scope.UserId;
        var canAccept = confirmed && _scope.HasPermission(Permissions.Purchase.ReceiptAccept) && userId != c.CreatedBy && userId != c.ConfirmedBy;
        var reason = c.Status switch
        {
            ReceiptConfirmationStatus.Cancelled => "Phiếu đã hủy.",
            ReceiptConfirmationStatus.Confirmed => "Phiếu đã xác nhận – chỉ còn điều chỉnh SL thực nhận (có lý do), cập nhật hóa đơn, nghiệm thu.",
            _ when !manage => "Bạn không có quyền lập phiếu nhận hàng cho nơi nhận này.",
            _ => null,
        };
        return new ReceiptConfirmationActions(
            draft && manage, draft && manage, draft && manage && DocumentCancelPolicy.CanCancel(_scope, true, c.CreatedBy), confirmed && manage,
            (draft || confirmed) && manage, canAccept, reason);
    }
}
