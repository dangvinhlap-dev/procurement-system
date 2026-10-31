using Microsoft.EntityFrameworkCore;
using Proc.Application.Common;
using Proc.Application.Documents;
using Proc.Application.Purchasing;
using Proc.Domain.Common;
using Proc.Domain.Documents;
using Proc.Domain.Inventory;
using Proc.Domain.MasterData;
using Proc.Domain.Organization;
using Proc.Domain.Purchasing;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.Purchasing;

/// <summary>
/// Đơn mua hàng (design.md mục 6.2, 10.3). SL đã nhận của dòng PO tính từ các phiếu nhập mua đã ghi sổ trỏ về dòng đó – không lưu
/// riêng, nên bỏ ghi sổ phiếu nhập là SL đã nhận tự giảm.
/// </summary>
public sealed class PurchaseOrderService : IPurchaseOrderService
{
    private readonly ProcDbContext _db;
    private readonly ICurrentUserScope _scope;
    private readonly IDocumentNumberService _numbers;
    private readonly IClock _clock;
    private readonly ApprovalEngine _approvals;

    /// <summary>Tạo dịch vụ.</summary>
    public PurchaseOrderService(ProcDbContext db, ICurrentUserScope scope, IDocumentNumberService numbers, IClock clock)
    {
        _db = db;
        _scope = scope;
        _numbers = numbers;
        _clock = clock;
        _approvals = new ApprovalEngine(db, scope, clock);
    }

    /// <inheritdoc />
    public async Task<PurchaseDocumentListResult> SearchAsync(PurchaseDocumentQuery query, CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        var to = query.To ?? _clock.TodayInVietnam();
        var from = query.From ?? to.AddDays(-90);
        var orders = _db.PurchaseOrders.AsNoTracking().Where(o => o.Date >= from && o.Date <= to);
        if (!string.IsNullOrWhiteSpace(query.Status)) orders = orders.Where(o => o.Status == PurchaseRequestService.ParseStatus(query.Status));
        var rows = await (
                from o in orders
                join s in _db.Suppliers on o.SupplierId equals s.Id
                join w in _db.Warehouses.IgnoreQueryFilters([ProcDbContext.WarehouseFilter]) on o.WarehouseId equals w.Id
                join u in _db.Users on o.CreatedBy equals u.Id into users
                from u in users.DefaultIfEmpty()
                orderby o.Date descending, o.No descending
                select new
                {
                    o.Id, o.No, o.Date, o.Status, Supplier = s.Code + " – " + s.Name, Warehouse = w.Code, o.ExpectedDate,
                    Amount = o.TotalAmount + o.TotalVat, LineCount = o.Lines.Count, CreatedBy = u == null ? null : u.FullName,
                    Base = o.Lines.Sum(l => l.BaseQty),
                })
            .Take(PurchaseRequestService.SearchLimit + 1)
            .ToListAsync(cancellationToken);
        var received = await ReceivedBaseByOrderAsync(rows.Select(r => r.Id).ToList(), cancellationToken);
        var items = rows.Take(PurchaseRequestService.SearchLimit).Select(r => new PurchaseDocumentListItemDto(
                r.Id, r.No, r.Date, r.Status.ToString(), r.Supplier, r.Warehouse, r.ExpectedDate, r.Amount, r.LineCount, r.CreatedBy,
                r.Base > 0 ? Math.Min(100m, Math.Round(received.GetValueOrDefault(r.Id) * 100m / r.Base, 1)) : 0m, null))
            .ToList();
        return new PurchaseDocumentListResult(items, rows.Count > PurchaseRequestService.SearchLimit, PurchaseRequestService.SearchLimit);
    }

    /// <inheritdoc />
    public async Task<PurchaseOrderDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        var order = await _db.PurchaseOrders.AsNoTracking().Include(o => o.Lines).SingleOrDefaultAsync(o => o.Id == id, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy đơn mua hàng.");
        return await ToDtoAsync(order, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PurchaseOrderDto> CreateAsync(SavePurchaseOrderRequest request, CancellationToken cancellationToken)
    {
        var companyId = _scope.RequireCompany();
        await CheckHeaderAsync(request.SupplierId, request.WarehouseId, cancellationToken);
        var lines = await ResolveLinesAsync(request.Lines, null, cancellationToken);
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var no = await _numbers.NextAsync(DocumentTypes.PurchaseOrder, request.Date, cancellationToken);
        var order = PurchaseOrder.Create(companyId, no, ToHeader(request));
        order.SetLines(lines);
        _db.PurchaseOrders.Add(order);
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetAsync(order.Id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PurchaseOrderDto> UpdateAsync(Guid id, SavePurchaseOrderRequest request, CancellationToken cancellationToken)
    {
        var order = await LoadAsync(id, cancellationToken);
        await EnsureItemsVisibleAsync(order, cancellationToken);
        await CheckHeaderAsync(request.SupplierId, request.WarehouseId, cancellationToken);
        var lines = await ResolveLinesAsync(request.Lines, id, cancellationToken);
        order.Update(ToHeader(request));
        order.SetLines(lines);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PurchaseOrderDto> SubmitAsync(Guid id, CancellationToken cancellationToken)
    {
        var order = await LoadAsync(id, cancellationToken);
        await EnsureItemsVisibleAsync(order, cancellationToken);
        // Kiểm tra lại lúc gửi: NCC có thể bị tạm ngưng, PR nguồn có thể đã lên PO khác từ lúc lưu nháp.
        await CheckHeaderAsync(order.SupplierId, order.WarehouseId, cancellationToken);
        await CheckRequestQuantitiesAsync(order.Lines.Select(l => (l.SourcePrLineId, l.ItemId, l.BaseQty, l.LineNo)).ToList(), order.Id, cancellationToken);
        await _approvals.SubmitAsync(order, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PurchaseOrderDto> ApproveAsync(Guid id, ApprovalActionRequest request, CancellationToken cancellationToken)
    {
        var order = await LoadAsync(id, cancellationToken);
        await _approvals.ApproveAsync(order, request.Comment, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PurchaseOrderDto> RejectAsync(Guid id, ApprovalActionRequest request, CancellationToken cancellationToken)
    {
        var order = await LoadAsync(id, cancellationToken);
        await _approvals.RejectAsync(order, request.Comment, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PurchaseOrderDto> CancelAsync(Guid id, CancellationToken cancellationToken)
    {
        var order = await LoadAsync(id, cancellationToken);
        // Đã có phiếu nhập (chưa hủy) theo PO thì không hủy – hàng đã/đang về theo đơn này.
        var receipts = await _db.InventoryDocuments.IgnoreQueryFilters([ProcDbContext.WarehouseFilter]).AsNoTracking()
            .Where(d => d.PurchaseOrderId == id && d.Status != DocumentStatus.Cancelled).Select(d => d.No).ToListAsync(cancellationToken);
        if (receipts.Count > 0) throw new BusinessRuleException($"Đơn {order.No} đã có phiếu nhập {string.Join(", ", receipts)} – không hủy được.");
        order.Cancel();
        await _approvals.SkipPendingAsync(order, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DeliveryTrackingDto>> TrackingAsync(CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        var today = _clock.TodayInVietnam();
        var lines = await (
                from l in _db.Set<PurchaseOrderLine>().AsNoTracking()
                join o in _db.PurchaseOrders on l.OrderId equals o.Id
                join s in _db.Suppliers on o.SupplierId equals s.Id
                join i in _db.Items on l.ItemId equals i.Id
                join u in _db.Uoms on l.UomId equals u.Id
                where o.Status == ApprovalDocumentStatus.Approved
                orderby l.DeliveryDate ?? o.ExpectedDate, o.No, l.LineNo
                select new
                {
                    o.Id, o.No, o.Date, SupplierCode = s.Code, SupplierName = s.Name, LineId = l.Id, ItemCode = i.Code, ItemName = i.Name,
                    UomCode = u.Code, l.Qty, l.UomFactor, l.BaseQty, Due = l.DeliveryDate ?? o.ExpectedDate,
                })
            .ToListAsync(cancellationToken);
        var received = await ReceivedBaseByLineAsync(lines.Select(l => l.LineId).ToList(), null, cancellationToken);
        return lines
            .Select(l => new { l, Received = received.GetValueOrDefault(l.LineId) })
            .Where(x => x.Received < x.l.BaseQty)
            .Select(x =>
            {
                var receivedQty = Math.Round(x.Received / x.l.UomFactor, 4, MidpointRounding.AwayFromZero);
                return new DeliveryTrackingDto(
                    x.l.Id, x.l.No, x.l.Date, x.l.SupplierCode, x.l.SupplierName, x.l.LineId, x.l.ItemCode, x.l.ItemName, x.l.UomCode,
                    x.l.Qty, receivedQty, Math.Max(0, x.l.Qty - receivedQty), x.l.Due, x.l.Due is { } due && due < today);
            })
            .ToList();
    }

    /// <summary>SL đã nhận (ĐVT cơ sở) theo dòng PO – xem <see cref="PurchaseOrderReceipts"/>.</summary>
    internal Task<Dictionary<Guid, decimal>> ReceivedBaseByLineAsync(IReadOnlyList<Guid> poLineIds, Guid? excludeDocumentId, CancellationToken cancellationToken) =>
        PurchaseOrderReceipts.ReceivedBaseByLineAsync(_db, poLineIds, excludeDocumentId, cancellationToken);

    private async Task<Dictionary<Guid, decimal>> ReceivedBaseByOrderAsync(IReadOnlyList<Guid> orderIds, CancellationToken cancellationToken) =>
        await (
                from l in _db.InventoryDocumentLines.IgnoreQueryFilters([ProcDbContext.CompanyFilter]).AsNoTracking()
                join d in _db.InventoryDocuments.IgnoreQueryFilters([ProcDbContext.WarehouseFilter]) on l.DocumentId equals d.Id
                where d.PurchaseOrderId != null && orderIds.Contains(d.PurchaseOrderId.Value) && d.Status == DocumentStatus.Posted
                group l by d.PurchaseOrderId!.Value into g
                select new { g.Key, Qty = g.Sum(x => x.BaseQty) })
            .ToDictionaryAsync(x => x.Key, x => x.Qty, cancellationToken);

    /// <summary>
    /// NCC đã duyệt và đang áp dụng tại công ty (design.md mục 10.3, 5.9 – NCC tạm ngưng/danh sách đen không đặt hàng được); kho nhận
    /// trong phạm vi, đang dùng.
    /// </summary>
    private async Task CheckHeaderAsync(Guid supplierId, Guid warehouseId, CancellationToken cancellationToken)
    {
        var supplier = await _db.Suppliers.AsNoTracking().SingleOrDefaultAsync(s => s.Id == supplierId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy nhà cung cấp.");
        if (supplier.Status != SupplierStatus.Approved)
            throw new BusinessRuleException($"NCC {supplier.Code} chưa ở trạng thái Đã duyệt nên chưa đặt hàng được.");
        if (!await _db.SupplierCompanies.AnyAsync(sc => sc.SupplierId == supplierId && sc.IsActive, cancellationToken))
            throw new BusinessRuleException($"NCC {supplier.Code} chưa được áp dụng tại công ty này (Nhà cung cấp → tab Áp dụng tại …).");
        if (!await _db.Warehouses.AnyAsync(w => w.Id == warehouseId && w.IsActive, cancellationToken))
            throw new NotFoundException("Không tìm thấy kho nhận (hoặc kho ngoài phạm vi, đã ngừng dùng).");
    }

    private async Task<List<(PurchaseOrderLineData, Item)>> ResolveLinesAsync(
        IReadOnlyList<SavePurchaseOrderLineRequest> lines, Guid? orderId, CancellationToken cancellationToken)
    {
        var ids = lines.Select(l => l.ItemId).Distinct().ToList();
        var items = await _db.Items.AsNoTracking().Include(i => i.Conversions).Where(i => ids.Contains(i.Id)).ToDictionaryAsync(i => i.Id, cancellationToken);
        var resolved = lines.Select((l, index) => (
                new PurchaseOrderLineData(l.Id, l.ItemId, l.UomId, l.Qty, l.UnitPrice, l.DiscountPct, l.VatCode, l.DeliveryDate, l.Note, l.SourcePrLineId),
                items.GetValueOrDefault(l.ItemId)
                ?? throw new NotFoundException($"Dòng {index + 1}: không tìm thấy mặt hàng (hoặc mặt hàng ngoài phạm vi nhóm hàng của bạn).")))
            .ToList();
        var check = resolved.Select((x, index) =>
        {
            var factor = x.Item1.UomId == x.Item2.BaseUomId ? 1m : x.Item2.Conversions.FirstOrDefault(c => c.UomId == x.Item1.UomId)?.Factor ?? 1m;
            return (x.Item1.SourcePrLineId, x.Item1.ItemId, decimal.Round(x.Item1.Qty * factor, 4, MidpointRounding.AwayFromZero), index + 1);
        }).ToList();
        await CheckRequestQuantitiesAsync(check, orderId, cancellationToken);
        return resolved;
    }

    /// <summary>
    /// Dòng lấy từ PR: PR phải đã duyệt, cùng công ty, cùng mặt hàng; tổng SL đặt (ĐVT cơ sở) trên các PO chưa hủy không vượt SL đề
    /// nghị (design.md mục 6.2 bước 2 – không đặt quá số được duyệt).
    /// </summary>
    private async Task CheckRequestQuantitiesAsync(
        IReadOnlyList<(Guid? PrLineId, Guid ItemId, decimal BaseQty, int LineNo)> lines, Guid? orderId, CancellationToken cancellationToken)
    {
        var prLineIds = lines.Select(l => l.PrLineId).OfType<Guid>().Distinct().ToList();
        if (prLineIds.Count == 0) return;
        var sources = await (
                from l in _db.Set<PurchaseRequestLine>().AsNoTracking()
                join r in _db.PurchaseRequests on l.RequestId equals r.Id
                where prLineIds.Contains(l.Id)
                select new { l.Id, l.ItemId, l.BaseQty, r.No, r.Status, l.LineNo })
            .ToDictionaryAsync(x => x.Id, cancellationToken);
        var ordered = await new PurchaseRequestService(_db, _scope, _numbers, _clock).OrderedBaseByLineAsync(prLineIds, orderId, cancellationToken);
        foreach (var group in lines.Where(l => l.PrLineId is not null).GroupBy(l => l.PrLineId!.Value))
        {
            var first = group.First();
            var source = sources.GetValueOrDefault(group.Key)
                ?? throw new NotFoundException($"Dòng {first.LineNo}: không tìm thấy dòng đề nghị mua hàng nguồn.");
            if (source.Status != ApprovalDocumentStatus.Approved)
                throw new BusinessRuleException($"Dòng {first.LineNo}: đề nghị {source.No} chưa được duyệt (hoặc đã hủy).");
            if (group.Any(l => l.ItemId != source.ItemId))
                throw new BusinessRuleException($"Dòng {first.LineNo}: mặt hàng khác mặt hàng ở dòng {source.LineNo} của đề nghị {source.No}.");
            var remaining = source.BaseQty - ordered.GetValueOrDefault(group.Key);
            if (group.Sum(l => l.BaseQty) > remaining)
                throw new BusinessRuleException($"Dòng {first.LineNo}: vượt SL còn chưa đặt của đề nghị {source.No} dòng {source.LineNo} ({remaining:0.####} theo ĐVT cơ sở).");
        }
    }

    private async Task<PurchaseOrder> LoadAsync(Guid id, CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        return await _db.PurchaseOrders.Include(o => o.Lines).SingleOrDefaultAsync(o => o.Id == id, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy đơn mua hàng.");
    }

    private async Task EnsureItemsVisibleAsync(PurchaseOrder order, CancellationToken cancellationToken)
    {
        var ids = order.Lines.Select(l => l.ItemId).Distinct().ToList();
        if (await _db.Items.CountAsync(i => ids.Contains(i.Id), cancellationToken) < ids.Count)
            throw new ForbiddenAccessException("Phiếu có mặt hàng ngoài phạm vi nhóm hàng của bạn nên bạn không thao tác được.");
    }

    private async Task<PurchaseOrderDto> ToDtoAsync(PurchaseOrder order, CancellationToken cancellationToken)
    {
        var itemIds = order.Lines.Select(l => l.ItemId).Distinct().ToList();
        var items = await _db.Items.AsNoTracking().Where(i => itemIds.Contains(i.Id)).Select(i => new { i.Id, i.Code, i.Name }).ToDictionaryAsync(i => i.Id, cancellationToken);
        var uomIds = order.Lines.Select(l => l.UomId).Distinct().ToList();
        var uoms = await _db.Uoms.AsNoTracking().Where(u => uomIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Code, cancellationToken);
        var supplier = await _db.Suppliers.AsNoTracking().Where(s => s.Id == order.SupplierId).Select(s => new { s.Code, s.Name }).SingleAsync(cancellationToken);
        var warehouse = await _db.Warehouses.IgnoreQueryFilters([ProcDbContext.WarehouseFilter]).AsNoTracking()
            .Where(w => w.Id == order.WarehouseId).Select(w => w.Code).SingleOrDefaultAsync(cancellationToken);
        var prLineIds = order.Lines.Select(l => l.SourcePrLineId).OfType<Guid>().Distinct().ToList();
        var prNos = await (
                from l in _db.Set<PurchaseRequestLine>().AsNoTracking()
                join r in _db.PurchaseRequests on l.RequestId equals r.Id
                where prLineIds.Contains(l.Id)
                select new { l.Id, r.No })
            .ToDictionaryAsync(x => x.Id, x => x.No, cancellationToken);
        var received = await ReceivedBaseByLineAsync(order.Lines.Select(l => l.Id).ToList(), null, cancellationToken);
        var createdBy = order.CreatedBy is { } uid
            ? await _db.Users.AsNoTracking().Where(u => u.Id == uid).Select(u => u.FullName).SingleOrDefaultAsync(cancellationToken)
            : null;

        var lines = order.Lines.OrderBy(l => l.LineNo).Select(l =>
        {
            var item = items.GetValueOrDefault(l.ItemId);
            var receivedQty = Math.Round(received.GetValueOrDefault(l.Id) / l.UomFactor, 4, MidpointRounding.AwayFromZero);
            return new PurchaseOrderLineDto(
                l.Id, l.LineNo, l.ItemId, item?.Code ?? "(ngoài phạm vi)", item?.Name ?? "", l.UomId, uoms.GetValueOrDefault(l.UomId) ?? "", l.Qty,
                l.UomFactor, l.BaseQty, l.UnitPrice, l.DiscountPct, l.GrossAmount, l.DiscountAmount, l.Amount, l.VatCode, l.VatAmount, l.DeliveryDate,
                l.Note, l.SourcePrLineId, l.SourcePrLineId is { } p ? prNos.GetValueOrDefault(p) : null, receivedQty, Math.Max(0, l.Qty - receivedQty));
        }).ToList();

        var itemsVisible = items.Count == itemIds.Count;
        var step = order.Status == ApprovalDocumentStatus.Submitted ? await _approvals.CurrentStepAsync(order, cancellationToken) : null;
        var canApprove = step is not null && itemsVisible && await _approvals.ApproveBlockerAsync(order, step, cancellationToken) is null;
        string? readOnly = !itemsVisible ? "Phiếu có mặt hàng ngoài phạm vi nhóm hàng của bạn nên chỉ xem được."
            : !_scope.HasPermission(Permissions.Purchase.PoCreate) ? "Bạn không có quyền lập, sửa đơn mua hàng." : null;
        var anyReceived = received.Values.Any(q => q > 0);
        var actions = new PurchaseDocumentActions(
            order.Status == ApprovalDocumentStatus.Draft && readOnly is null,
            order.Status == ApprovalDocumentStatus.Draft && readOnly is null,
            canApprove,
            order.Status is ApprovalDocumentStatus.Draft or ApprovalDocumentStatus.Submitted or ApprovalDocumentStatus.Approved && readOnly is null && !anyReceived,
            readOnly);

        return new PurchaseOrderDto(
            order.Id, order.No, order.Date, order.Status.ToString(), order.SupplierId, supplier.Code, supplier.Name, order.WarehouseId, warehouse,
            order.ExpectedDate, order.PaymentTerms, order.DeliveryTerms, order.DeliveryAddress, order.CurrencyCode, order.ReceiveTolerancePct, order.Note,
            order.GrossAmount, order.TotalDiscount, order.TotalAmount, order.TotalVat, order.RejectReason, order.CreatedAt, createdBy, order.ApprovedAt,
            lines, await _approvals.HistoryAsync(DocumentTypes.PurchaseOrder, order.Id, cancellationToken), actions);
    }

    private static PurchaseOrderHeader ToHeader(SavePurchaseOrderRequest r) =>
        new(r.Date, r.SupplierId, r.WarehouseId, r.ExpectedDate, r.PaymentTerms, r.DeliveryTerms, r.DeliveryAddress, r.ReceiveTolerancePct, r.Note);
}
