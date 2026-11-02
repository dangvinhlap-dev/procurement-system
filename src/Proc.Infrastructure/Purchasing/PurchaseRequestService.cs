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
/// Đề nghị mua hàng (design.md mục 6.2, 10.2). PR có CompanyId nên mọi truy vấn tự lọc theo công ty đang làm việc; mặt hàng đọc qua
/// filter nhóm hàng (hàng ngoài phạm vi như không tồn tại).
/// </summary>
public sealed class PurchaseRequestService : IPurchaseRequestService
{
    /// <summary>Số phiếu tối đa mỗi lần tìm.</summary>
    public const int SearchLimit = 2000;

    private readonly ProcDbContext _db;
    private readonly ICurrentUserScope _scope;
    private readonly IDocumentNumberService _numbers;
    private readonly IClock _clock;
    private readonly ApprovalEngine _approvals;

    /// <summary>Tạo dịch vụ.</summary>
    public PurchaseRequestService(ProcDbContext db, ICurrentUserScope scope, IDocumentNumberService numbers, IClock clock)
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
        var requests = _db.PurchaseRequests.AsNoTracking().Where(r => r.Date >= from && r.Date <= to);
        if (!string.IsNullOrWhiteSpace(query.Status)) requests = requests.Where(r => r.Status == ParseStatus(query.Status));
        var rows = await (
                from r in requests
                join u in _db.Users on r.CreatedBy equals u.Id into users
                from u in users.DefaultIfEmpty()
                orderby r.Date descending, r.No descending
                select new
                {
                    r.Id, r.No, r.Date, r.Status, r.Purpose, r.NeededDate, r.TotalAmount, LineCount = r.Lines.Count,
                    // Tên bộ phận theo danh mục; PR cũ chưa có danh mục thì hiện chữ ghi trước đây (design.md mục 18.5).
                    Department = _db.Departments.Where(d => d.Id == r.DepartmentId).Select(d => d.Name).FirstOrDefault() ?? r.LegacyDepartment,
                    CreatedBy = u == null ? null : u.FullName, r.Priority,
                    Base = r.Lines.Sum(l => l.BaseQty),
                })
            .Take(SearchLimit + 1)
            .ToListAsync(cancellationToken);
        var ordered = await OrderedBaseByRequestAsync(rows.Select(r => r.Id).ToList(), cancellationToken);
        var items = rows.Take(SearchLimit).Select(r => new PurchaseDocumentListItemDto(
                r.Id, r.No, r.Date, r.Status.ToString(), r.Department, r.Purpose, r.NeededDate, r.TotalAmount, r.LineCount, r.CreatedBy,
                r.Base > 0 ? Math.Min(100m, Math.Round(ordered.GetValueOrDefault(r.Id) * 100m / r.Base, 1)) : 0m, r.Priority.ToString()))
            .ToList();
        return new PurchaseDocumentListResult(items, rows.Count > SearchLimit, SearchLimit);
    }

    /// <inheritdoc />
    public async Task<PurchaseRequestDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        var request = await _db.PurchaseRequests.AsNoTracking().Include(r => r.Lines).Include(r => r.NewItems).AsSplitQuery().SingleOrDefaultAsync(r => r.Id == id, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy đề nghị mua hàng.");
        return await ToDtoAsync(request, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PurchaseRequestDto> CreateAsync(SavePurchaseRequestRequest request, CancellationToken cancellationToken)
    {
        var companyId = _scope.RequireCompany();
        await CheckWarehouseAsync(request.WarehouseId, cancellationToken);
        await CheckDepartmentAsync(request.DepartmentId, null, cancellationToken);
        var lines = await ResolveLinesAsync(request.Lines, cancellationToken);
        var newItems = await ResolveNewItemsAsync(request.NewItems, cancellationToken);
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var no = await _numbers.NextAsync(DocumentTypes.PurchaseRequest, request.Date, cancellationToken);
        var pr = PurchaseRequest.Create(companyId, no, ToHeader(request), _scope.UserId);
        pr.SetNewItems(newItems);
        pr.SetLines(lines);
        _db.PurchaseRequests.Add(pr);
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetAsync(pr.Id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PurchaseRequestDto> UpdateAsync(Guid id, SavePurchaseRequestRequest request, CancellationToken cancellationToken)
    {
        var pr = await LoadAsync(id, cancellationToken);
        await EnsureItemsVisibleAsync(pr, cancellationToken);
        await CheckWarehouseAsync(request.WarehouseId, cancellationToken);
        await CheckDepartmentAsync(request.DepartmentId, pr.DepartmentId, cancellationToken);
        var lines = await ResolveLinesAsync(request.Lines, cancellationToken);
        var newItems = await ResolveNewItemsAsync(request.NewItems, cancellationToken);
        pr.Update(ToHeader(request));
        pr.SetNewItems(newItems);
        pr.SetLines(lines);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PurchaseRequestDto> SubmitAsync(Guid id, CancellationToken cancellationToken)
    {
        var pr = await LoadAsync(id, cancellationToken);
        await EnsureItemsVisibleAsync(pr, cancellationToken);
        await _approvals.SubmitAsync(pr, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PurchaseRequestDto> ApproveAsync(Guid id, ApprovalActionRequest request, CancellationToken cancellationToken)
    {
        var pr = await LoadAsync(id, cancellationToken);
        await _approvals.ApproveAsync(pr, request.Comment, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PurchaseRequestDto> RejectAsync(Guid id, ApprovalActionRequest request, CancellationToken cancellationToken)
    {
        var pr = await LoadAsync(id, cancellationToken);
        await _approvals.RejectAsync(pr, request.Comment, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PurchaseRequestDto> CancelAsync(Guid id, CancellationToken cancellationToken)
    {
        var pr = await LoadAsync(id, cancellationToken);
        // PR đã lên PO (chưa hủy) thì hủy PO trước – không để PO trỏ vào PR đã hủy.
        var lineIds = pr.Lines.Select(l => l.Id).ToList();
        var orders = await (
                from ol in _db.Set<PurchaseOrderLine>()
                join o in _db.PurchaseOrders on ol.OrderId equals o.Id
                where ol.SourcePrLineId != null && lineIds.Contains(ol.SourcePrLineId.Value) && o.Status != ApprovalDocumentStatus.Cancelled
                select o.No)
            .Distinct().ToListAsync(cancellationToken);
        if (orders.Count > 0) throw new BusinessRuleException($"PR {pr.No} đã lên đơn mua hàng {string.Join(", ", orders)} – hủy đơn mua hàng trước.");
        pr.Cancel();
        await _approvals.SkipPendingAsync(pr, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<OpenRequestLineDto>> OpenLinesAsync(CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        var lines = await (
                from l in _db.Set<PurchaseRequestLine>().AsNoTracking()
                join r in _db.PurchaseRequests on l.RequestId equals r.Id
                join i in _db.Items on l.ItemId equals i.Id
                join u in _db.Uoms on l.UomId equals u.Id
                where r.Status == ApprovalDocumentStatus.Approved
                orderby r.Date, r.No, l.LineNo
                select new
                {
                    l.Id, RequestId = r.Id, r.No, NeededDate = l.NeededDate ?? r.NeededDate, l.ItemId, ItemCode = i.Code,
                    Department = _db.Departments.Where(d => d.Id == r.DepartmentId).Select(d => d.Name).FirstOrDefault() ?? r.LegacyDepartment,
                    ItemName = i.Name, l.UomId, UomCode = u.Code, l.Qty, l.UomFactor, l.BaseQty, l.EstPrice, i.VatCode, l.SuggestedSupplierId,
                })
            .ToListAsync(cancellationToken);
        var ordered = await OrderedBaseByLineAsync(lines.Select(l => l.Id).ToList(), null, cancellationToken);
        return lines
            .Select(l => new { l, Remaining = l.BaseQty - ordered.GetValueOrDefault(l.Id) })
            .Where(x => x.Remaining > 0)
            .Select(x => new OpenRequestLineDto(
                x.l.Id, x.l.RequestId, x.l.No, x.l.NeededDate, x.l.Department, x.l.ItemId, x.l.ItemCode, x.l.ItemName, x.l.UomId, x.l.UomCode,
                Math.Round(x.Remaining / x.l.UomFactor, 4, MidpointRounding.AwayFromZero), x.l.EstPrice, x.l.VatCode, x.l.SuggestedSupplierId))
            .ToList();
    }

    /// <summary>
    /// SL đã lên PO (ĐVT cơ sở) theo dòng PR, chỉ PO chưa hủy. <paramref name="excludeOrderId"/>: PO đang sửa (không tính chính nó).
    /// </summary>
    internal async Task<Dictionary<Guid, decimal>> OrderedBaseByLineAsync(IReadOnlyList<Guid> prLineIds, Guid? excludeOrderId, CancellationToken cancellationToken) =>
        await (
                from ol in _db.Set<PurchaseOrderLine>().AsNoTracking()
                join o in _db.PurchaseOrders on ol.OrderId equals o.Id
                where ol.SourcePrLineId != null && prLineIds.Contains(ol.SourcePrLineId.Value) && o.Status != ApprovalDocumentStatus.Cancelled
                      && o.Id != excludeOrderId
                group ol by ol.SourcePrLineId!.Value into g
                select new { g.Key, Qty = g.Sum(x => x.BaseQty) })
            .ToDictionaryAsync(x => x.Key, x => x.Qty, cancellationToken);

    private async Task<Dictionary<Guid, decimal>> OrderedBaseByRequestAsync(IReadOnlyList<Guid> requestIds, CancellationToken cancellationToken) =>
        await (
                from ol in _db.Set<PurchaseOrderLine>().AsNoTracking()
                join o in _db.PurchaseOrders on ol.OrderId equals o.Id
                join l in _db.Set<PurchaseRequestLine>() on ol.SourcePrLineId equals l.Id
                where requestIds.Contains(l.RequestId) && o.Status != ApprovalDocumentStatus.Cancelled
                group ol by l.RequestId into g
                select new { g.Key, Qty = g.Sum(x => x.BaseQty) })
            .ToDictionaryAsync(x => x.Key, x => x.Qty, cancellationToken);

    /// <summary>
    /// Dòng PR: mặt hàng qua filter nhóm hàng; đơn giá tham khảo = giá nhập gần nhất của công ty (phiếu nhập mua đã ghi sổ) theo ĐVT cơ
    /// sở × hệ số ĐVT – không nhận từ client (design.md mục 10.2: chỉ đọc). Chưa từng nhập thì 0.
    /// </summary>
    private async Task<List<(PurchaseRequestLineData, Item)>> ResolveLinesAsync(IReadOnlyList<SavePurchaseRequestLineRequest> lines, CancellationToken cancellationToken)
    {
        var ids = lines.Select(l => l.ItemId).Distinct().ToList();
        var items = await _db.Items.AsNoTracking().Include(i => i.Conversions).Where(i => ids.Contains(i.Id)).ToDictionaryAsync(i => i.Id, cancellationToken);
        var prices = await LastPurchasePricesAsync(ids, cancellationToken);
        return lines.Select((l, index) =>
        {
            var item = items.GetValueOrDefault(l.ItemId)
                ?? throw new NotFoundException($"Dòng {index + 1}: không tìm thấy mặt hàng (hoặc mặt hàng ngoài phạm vi nhóm hàng của bạn).");
            var factor = l.UomId == item.BaseUomId ? 1m : item.Conversions.FirstOrDefault(c => c.UomId == l.UomId)?.Factor ?? 1m;
            var price = Math.Round(prices.GetValueOrDefault(item.Id) * factor, 6, MidpointRounding.AwayFromZero);
            return (new PurchaseRequestLineData(l.Id, l.ItemId, l.UomId, l.Qty, price, l.SuggestedSupplierId, l.NeededDate, l.Note), item);
        }).ToList();
    }

    /// <summary>
    /// Giá nhập gần nhất theo ĐVT cơ sở (đơn giá dòng ÷ hệ số ĐVT) của các mặt hàng, từ phiếu nhập mua đã ghi sổ của công ty đang
    /// làm việc (mọi kho – giá không phụ thuộc phạm vi kho).
    /// </summary>
    internal async Task<Dictionary<Guid, decimal>> LastPurchasePricesAsync(IReadOnlyList<Guid> itemIds, CancellationToken cancellationToken)
    {
        // IgnoreQueryFilters tắt filter công ty cho cả câu truy vấn (cả bảng phiếu) nên phải lọc công ty tường minh – mỗi công ty
        // là pháp nhân độc lập, không lấy giá nhập của công ty khác (CLAUDE.md mục 4).
        var companyId = _scope.RequireCompany();
        var rows = await (
                from l in _db.InventoryDocumentLines.IgnoreQueryFilters([ProcDbContext.CompanyFilter]).AsNoTracking()
                join d in _db.InventoryDocuments.IgnoreQueryFilters([ProcDbContext.WarehouseFilter]) on l.DocumentId equals d.Id
                where d.CompanyId == companyId && itemIds.Contains(l.ItemId) && d.DocType == DocumentTypes.GoodsReceipt && d.Status == DocumentStatus.Posted
                orderby d.Date descending, d.PostedAt descending
                select new { l.ItemId, l.UnitPrice, l.UomFactor })
            .ToListAsync(cancellationToken);
        return rows.GroupBy(r => r.ItemId).ToDictionary(g => g.Key, g => Math.Round(g.First().UnitPrice / g.First().UomFactor, 6, MidpointRounding.AwayFromZero));
    }

    private async Task CheckWarehouseAsync(Guid? warehouseId, CancellationToken cancellationToken)
    {
        if (warehouseId is { } id && !await _db.Warehouses.AnyAsync(w => w.Id == id && w.IsActive, cancellationToken))
            throw new NotFoundException("Không tìm thấy kho nhận (hoặc kho ngoài phạm vi, đã ngừng dùng).");
    }

    /// <summary>
    /// Bộ phận phải thuộc công ty đang làm việc (global query filter) và còn dùng; phiếu đã chọn bộ phận nay ngừng dùng thì
    /// lưu lại vẫn được, để sửa dòng hàng không buộc đổi bộ phận (design.md mục 18.5).
    /// </summary>
    private async Task CheckDepartmentAsync(Guid? departmentId, Guid? currentId, CancellationToken cancellationToken)
    {
        if (departmentId is not { } id) return;
        var active = await _db.Departments.Where(d => d.Id == id).Select(d => (bool?)d.IsActive).SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy bộ phận đề nghị (hoặc bộ phận thuộc công ty khác).");
        if (!active && id != currentId) throw new BusinessRuleException("Bộ phận đề nghị đã ngừng dùng.");
    }

    /// <summary>Tải PR kèm dòng có mã và hàng mới chưa có mã (dùng chung cho dịch vụ xử lý hàng mới).</summary>
    /// <exception cref="NotFoundException">Khi không có hoặc ngoài phạm vi công ty.</exception>
    internal async Task<PurchaseRequest> LoadAsync(Guid id, CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        return await _db.PurchaseRequests.Include(r => r.Lines).Include(r => r.NewItems).AsSplitQuery().SingleOrDefaultAsync(r => r.Id == id, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy đề nghị mua hàng.");
    }

    /// <summary>
    /// SL đang đặt (ĐVT cơ sở) theo mặt hàng tại công ty – design.md mục 18.13 (V2: kiểm tra hàng đang đặt trước khi đề nghị): tổng trên
    /// các PO đã duyệt còn chờ nhận (chưa nhận đủ) của SL đặt − SL đã nhận theo phiếu nhập đã ghi sổ, mỗi dòng không âm.
    /// Ví dụ PO 100 cái đã nhận 30 → đang đặt 70.
    /// </summary>
    private async Task<Dictionary<Guid, decimal>> OnOrderBaseByItemAsync(IReadOnlyList<Guid> itemIds, CancellationToken cancellationToken)
    {
        if (itemIds.Count == 0) return [];
        var lines = await (
                from l in _db.Set<PurchaseOrderLine>().AsNoTracking()
                join o in _db.PurchaseOrders on l.OrderId equals o.Id
                where o.Status == ApprovalDocumentStatus.Approved && itemIds.Contains(l.ItemId)
                select new { l.Id, l.ItemId, l.BaseQty })
            .ToListAsync(cancellationToken);
        var received = await PurchaseOrderReceipts.ReceivedBaseByLineAsync(_db, lines.Select(l => l.Id).ToList(), null, cancellationToken);
        return lines.GroupBy(l => l.ItemId)
            .ToDictionary(g => g.Key, g => g.Sum(l => Math.Max(0, l.BaseQty - received.GetValueOrDefault(l.Id))));
    }

    /// <summary>Hàng mới chưa có mã gửi lên: ĐVT phải có trong danh mục ĐVT.</summary>
    /// <exception cref="NotFoundException">Khi ĐVT không có.</exception>
    private async Task<List<PurchaseRequestNewItemData>> ResolveNewItemsAsync(
        IReadOnlyList<SavePurchaseRequestNewItemRequest>? items, CancellationToken cancellationToken)
    {
        if (items is null || items.Count == 0) return [];
        var uomIds = items.Select(i => i.UomId).Distinct().ToList();
        var known = await _db.Uoms.AsNoTracking().Where(u => uomIds.Contains(u.Id)).Select(u => u.Id).ToListAsync(cancellationToken);
        return items.Select((i, index) => known.Contains(i.UomId)
                ? new PurchaseRequestNewItemData(i.Id, i.Description, i.Specification, i.UomId, i.Qty, i.EstPrice, i.NeededDate, i.Note)
                : throw new NotFoundException($"Hàng mới dòng {index + 1}: hãy chọn ĐVT trong danh mục."))
            .ToList();
    }

    /// <summary>Sửa là thay toàn bộ dòng – chặn nếu phiếu có hàng người sửa không được xem (lưu sẽ xóa mất dòng đó).</summary>
    private async Task EnsureItemsVisibleAsync(PurchaseRequest pr, CancellationToken cancellationToken)
    {
        var ids = pr.Lines.Select(l => l.ItemId).Distinct().ToList();
        if (await _db.Items.CountAsync(i => ids.Contains(i.Id), cancellationToken) < ids.Count)
            throw new ForbiddenAccessException("Phiếu có mặt hàng ngoài phạm vi nhóm hàng của bạn nên bạn không thao tác được.");
    }

    /// <summary>Đổi PR sang DTO (dùng chung cho dịch vụ xử lý hàng mới).</summary>
    internal async Task<PurchaseRequestDto> ToDtoAsync(PurchaseRequest pr, CancellationToken cancellationToken)
    {
        var itemIds = pr.Lines.Select(l => l.ItemId).Distinct().ToList();
        var items = await _db.Items.AsNoTracking().Where(i => itemIds.Contains(i.Id))
            .Select(i => new { i.Id, i.Code, i.Name, i.BaseUomId }).ToDictionaryAsync(i => i.Id, cancellationToken);
        var uomIds = pr.Lines.Select(l => l.UomId).Concat(pr.NewItems.Select(n => n.UomId)).Concat(items.Values.Select(i => i.BaseUomId)).Distinct().ToList();
        var uoms = await _db.Uoms.AsNoTracking().Where(u => uomIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Code, cancellationToken);
        var supplierIds = pr.Lines.Select(l => l.SuggestedSupplierId).OfType<Guid>().Distinct().ToList();
        var suppliers = await _db.Suppliers.AsNoTracking().Where(s => supplierIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Code, cancellationToken);
        var onHand = await _db.StockOnHand.AsNoTracking().Where(s => itemIds.Contains(s.ItemId))
            .GroupBy(s => s.ItemId).Select(g => new { g.Key, Qty = g.Sum(s => s.Qty) }).ToDictionaryAsync(x => x.Key, x => x.Qty, cancellationToken);
        var ordered = await OrderedBaseByLineAsync(pr.Lines.Select(l => l.Id).ToList(), null, cancellationToken);
        var onOrder = await OnOrderBaseByItemAsync(itemIds, cancellationToken);
        var department = pr.DepartmentId is { } departmentId
            ? await _db.Departments.Where(d => d.Id == departmentId).Select(d => d.Name).SingleOrDefaultAsync(cancellationToken)
            : null;
        var userIds = new[] { pr.CreatedBy, pr.RequesterId }.OfType<Guid>().Distinct().ToList();
        var users = await _db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken);

        var lines = pr.Lines.OrderBy(l => l.LineNo).Select(l =>
        {
            var item = items.GetValueOrDefault(l.ItemId);
            return new PurchaseRequestLineDto(
                l.Id, l.LineNo, l.ItemId, item?.Code ?? "(ngoài phạm vi)", item?.Name ?? "", l.UomId, uoms.GetValueOrDefault(l.UomId) ?? "",
                l.Qty, l.UomFactor, l.BaseQty, item is null ? "" : uoms.GetValueOrDefault(item.BaseUomId) ?? "", l.EstPrice, l.Amount,
                l.SuggestedSupplierId, l.SuggestedSupplierId is { } s ? suppliers.GetValueOrDefault(s) : null, l.NeededDate, l.Note,
                Math.Round(ordered.GetValueOrDefault(l.Id) / l.UomFactor, 4, MidpointRounding.AwayFromZero), onHand.GetValueOrDefault(l.ItemId),
                onOrder.GetValueOrDefault(l.ItemId));
        }).ToList();

        var sourcingIds = pr.NewItems.Select(n => n.SourcingRequestId).OfType<Guid>().ToList();
        var sourcingNos = await _db.SourcingRequests.AsNoTracking().Where(s => sourcingIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.No, cancellationToken);
        var linkedItemIds = pr.NewItems.Select(n => n.ItemId).OfType<Guid>().ToList();
        var linkedCodes = await _db.Items.AsNoTracking().Where(i => linkedItemIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, i => i.Code, cancellationToken);
        var newItems = pr.NewItems.OrderBy(n => n.LineNo).Select(n => new PurchaseRequestNewItemDto(
                n.Id, n.LineNo, n.Description, n.Specification, n.UomId, uoms.GetValueOrDefault(n.UomId) ?? "", n.Qty, n.EstPrice, n.Amount, n.NeededDate,
                n.Note, n.SourcingRequestId, n.SourcingRequestId is { } sr ? sourcingNos.GetValueOrDefault(sr) : null, n.ItemId,
                n.ItemId is { } li ? linkedCodes.GetValueOrDefault(li) ?? "(ngoài phạm vi)" : null))
            .ToList();
        var pendingNew = pr.Status == ApprovalDocumentStatus.Approved && pr.NewItems.Any(n => n.LinkedLineId is null);

        var itemsVisible = items.Count == itemIds.Count;
        var step = pr.Status == ApprovalDocumentStatus.Submitted ? await _approvals.CurrentStepAsync(pr, cancellationToken) : null;
        var canApprove = step is not null && itemsVisible && await _approvals.ApproveBlockerAsync(pr, step, cancellationToken) is null;
        var canCreate = _scope.HasPermission(Permissions.Purchase.PrCreate);
        string? readOnly = !itemsVisible ? "Phiếu có mặt hàng ngoài phạm vi nhóm hàng của bạn nên chỉ xem được."
            : !canCreate ? "Bạn không có quyền lập, sửa đề nghị mua hàng." : null;
        var actions = new PurchaseDocumentActions(
            pr.Status == ApprovalDocumentStatus.Draft && readOnly is null,
            pr.Status == ApprovalDocumentStatus.Draft && readOnly is null,
            canApprove,
            pr.Status is ApprovalDocumentStatus.Draft or ApprovalDocumentStatus.Submitted or ApprovalDocumentStatus.Approved && readOnly is null,
            readOnly);

        return new PurchaseRequestDto(
            pr.Id, pr.No, pr.Date, pr.Status.ToString(), pr.DepartmentId, department ?? pr.LegacyDepartment, pr.RequesterId is { } r ? users.GetValueOrDefault(r) : null, pr.Purpose,
            pr.NeededDate, pr.WarehouseId, pr.Priority.ToString(), pr.Note, pr.TotalAmount, pr.RejectReason, pr.CreatedAt,
            pr.CreatedBy is { } c ? users.GetValueOrDefault(c) : null, pr.ApprovedAt, lines,
            await _approvals.HistoryAsync(DocumentTypes.PurchaseRequest, pr.Id, cancellationToken, pr), actions, newItems,
            pendingNew && _scope.HasPermission(Permissions.Sourcing.RfqManage),
            pendingNew && (_scope.HasPermission(Permissions.Purchase.PoCreate) || canCreate));
    }

    /// <summary>Đọc trạng thái từ chuỗi.</summary>
    /// <exception cref="BusinessRuleException">Khi trạng thái không hợp lệ.</exception>
    internal static ApprovalDocumentStatus ParseStatus(string status) =>
        Enum.TryParse<ApprovalDocumentStatus>(status, ignoreCase: true, out var s) && Enum.IsDefined(s)
            ? s
            : throw new BusinessRuleException("Trạng thái phải là Draft, Submitted, Approved, Closed hoặc Cancelled.");

    private static PurchaseRequestHeader ToHeader(SavePurchaseRequestRequest r) =>
        new(r.Date, r.DepartmentId, r.Purpose, r.NeededDate, r.WarehouseId,
            Enum.TryParse<PurchasePriority>(r.Priority, ignoreCase: true, out var p) && Enum.IsDefined(p)
                ? p
                : throw new BusinessRuleException("Mức ưu tiên phải là Low, Normal hoặc Urgent."),
            r.Note);
}
