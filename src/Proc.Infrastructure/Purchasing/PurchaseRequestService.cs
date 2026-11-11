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
    private readonly BudgetUsage _budgets;

    /// <summary>Tạo dịch vụ.</summary>
    public PurchaseRequestService(ProcDbContext db, ICurrentUserScope scope, IDocumentNumberService numbers, IClock clock)
    {
        _db = db;
        _scope = scope;
        _numbers = numbers;
        _clock = clock;
        _approvals = new ApprovalEngine(db, scope, clock);
        _budgets = new BudgetUsage(db);
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
                    r.Id, r.No, r.Date, r.Status, r.Purpose, r.NeededDate, r.TotalAmount, LineCount = r.Lines.Count, r.Category,
                    // Tên bộ phận theo danh mục; PR cũ chưa có danh mục thì hiện chữ ghi trước đây (design.md mục 18.5).
                    Department = _db.Departments.Where(d => d.Id == r.DepartmentId).Select(d => d.Name).FirstOrDefault() ?? r.LegacyDepartment,
                    CreatedBy = u == null ? null : u.FullName, r.Priority,
                    Base = r.Lines.Sum(l => l.BaseQty),
                    ClosedManually = r.ClosedAt != null,
                })
            .Take(SearchLimit + 1)
            .ToListAsync(cancellationToken);
        var ordered = await OrderedBaseByRequestAsync(rows.Select(r => r.Id).ToList(), cancellationToken);
        var items = rows.Take(SearchLimit).Select(r => new PurchaseDocumentListItemDto(
                r.Id, r.No, r.Date, r.Status.ToString(), r.Department, r.Purpose, r.NeededDate, r.TotalAmount, r.LineCount, r.CreatedBy,
                r.Base > 0 ? Math.Min(100m, Math.Round(ordered.GetValueOrDefault(r.Id) * 100m / r.Base, 1)) : 0m, r.Priority.ToString(),
                ClosedManually: r.ClosedManually, Category: r.Category.ToString()))
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
        await CheckConstructionProjectAsync(request.ConstructionProjectId, null, cancellationToken);
        var lines = await ResolveLinesAsync(request.Lines, cancellationToken);
        var newItems = await ResolveNewItemsAsync(request.NewItems, cancellationToken);
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var no = await _numbers.NextAsync(DocumentTypes.PurchaseRequest, request.Date, cancellationToken);
        var pr = PurchaseRequest.Create(companyId, no, ToHeader(request), _scope.UserId);
        pr.SetNewItems(newItems);
        pr.SetLines(lines);
        pr.ApplyBudgetCheck(await _budgets.CheckAsync(pr, cancellationToken));
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
        await CheckConstructionProjectAsync(request.ConstructionProjectId, pr.ConstructionProjectId, cancellationToken);
        var lines = await ResolveLinesAsync(request.Lines, cancellationToken);
        var newItems = await ResolveNewItemsAsync(request.NewItems, cancellationToken);
        pr.Update(ToHeader(request));
        pr.SetNewItems(newItems);
        pr.SetLines(lines);
        pr.ApplyBudgetCheck(await _budgets.CheckAsync(pr, cancellationToken));
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PurchaseRequestDto> SubmitAsync(Guid id, CancellationToken cancellationToken)
    {
        var pr = await LoadAsync(id, cancellationToken);
        await EnsureItemsVisibleAsync(pr, cancellationToken);
        // Công trình đã hoàn thành / tạm ngừng thì không mua thêm (design.md mục 19.3, lát Y3) – kể cả phiếu nháp lập trước khi đổi trạng thái.
        if (pr.ConstructionProjectId is { } projectId)
        {
            var project = await _db.ConstructionProjects.AsNoTracking().Where(p => p.Id == projectId)
                .Select(p => new { p.Code, p.Status }).SingleAsync(cancellationToken);
            if (project.Status != ConstructionProjectStatus.InProgress)
                throw new BusinessRuleException($"Công trình {project.Code} không còn ở trạng thái Đang thực hiện – không gửi duyệt đề nghị mua được.");
        }

        // Ngân sách tính lại lúc gửi (phiếu khác có thể đã dùng ngân sách từ lúc lưu nháp); vượt thì qua thêm chặng duyệt
        // "vượt ngân sách" PRB – không chặn gửi (design.md mục 18.24, quyết định Q5).
        pr.ApplyBudgetCheck(await _budgets.CheckAsync(pr, cancellationToken));
        var stages = new List<(string Type, int Stage)>();
        if (pr.IsOverBudget) stages.Add((ApprovalTypes.PurchaseRequestOverBudget, ApprovalTypes.OverBudgetStage));
        // Loại A vượt dự toán công trình (lũy kế PO đã duyệt + tổng dự kiến của phiếu > dự toán): thêm chặng PRE, không chặn gửi (lát Y3c – Q5).
        if (pr.Category == PurchaseCategory.Construction && (await ConstructionBudgetAsync(pr, cancellationToken))?.OverEstimate == true)
            stages.Add((ApprovalTypes.PurchaseRequestOverEstimate, ApprovalTypes.OverEstimateStage));
        // Chuỗi duyệt theo loại mua (lát Y3c – Q6): loại A theo bộ PRA, dịch vụ theo bộ PRS; chưa cấu hình thì theo bộ PR (dịch vụ tự bỏ cấp Thủ kho).
        var ruleType = pr.Category switch
        {
            PurchaseCategory.Construction => ApprovalTypes.PurchaseRequestConstruction,
            PurchaseCategory.Service => ApprovalTypes.PurchaseRequestService,
            _ => null,
        };
        // Phần vượt được xem trước khi trình lên cấp cao nhất – Chủ tịch (người dùng yêu cầu 11/11/2026).
        await _approvals.SubmitWithStagesAsync(pr, stages, cancellationToken, ruleType, extraBeforeTopLevel: true);
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
    public async Task<PurchaseRequestDto> CancelAsync(Guid id, CancellationToken cancellationToken, string? reason = null)
    {
        var pr = await LoadAsync(id, cancellationToken);
        // Quyền hủy + lý do bắt buộc (design.md mục 20.2a): nháp – người lập; đã gửi duyệt trở đi – chỉ quản trị.
        reason = DocumentCancelPolicy.Authorize(_scope, $"PR {pr.No}", pr.Status == ApprovalDocumentStatus.Draft, pr.CreatedBy, reason);
        // PR đã lên PO (chưa hủy) thì hủy PO trước – không để PO trỏ vào PR đã hủy.
        var lineIds = pr.Lines.Select(l => l.Id).ToList();
        var orders = await (
                from ol in _db.Set<PurchaseOrderLine>()
                join o in _db.PurchaseOrders on ol.OrderId equals o.Id
                where ol.SourcePrLineId != null && lineIds.Contains(ol.SourcePrLineId.Value) && o.Status != ApprovalDocumentStatus.Cancelled
                select o.No)
            .Distinct().ToListAsync(cancellationToken);
        if (orders.Count > 0) throw new BusinessRuleException($"PR {pr.No} đã lên đơn mua hàng {string.Join(", ", orders)} – hủy đơn mua hàng trước.");
        pr.Cancel(reason);
        await _approvals.SkipPendingAsync(pr, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PurchaseRequestDto> RecallAsync(Guid id, CancellationToken cancellationToken)
    {
        var pr = await LoadAsync(id, cancellationToken);
        // Rút về nháp (yêu cầu người dùng 11/11/2026): phiếu đã gửi duyệt chỉ quản trị hủy được (design.md mục 20.2a), nên người lập cần
        // rút về Nháp để sửa hoặc tự hủy. Chỉ người lập (hoặc quản trị) rút – người khác có quyền lập không rút phiếu của người khác.
        if (!CanRecall(pr))
            throw new ForbiddenAccessException($"Chỉ người lập (hoặc quản trị) rút được phiếu {pr.No} về nháp.");
        await _approvals.SkipPendingAsync(pr, cancellationToken);
        pr.ReturnToDraft(null);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PurchaseRequestDto> EnterStockAsync(Guid id, EnterPurchaseRequestStockRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        // Khóa PR: hai thủ kho cùng nhập, hoặc nhập trong lúc rút về nháp, không ghi đè nhau.
        await using var transaction = await RowLocks.BeginOrJoinAsync(_db, cancellationToken);
        await RowLocks.LockAsync<PurchaseRequest>(_db, [id], cancellationToken);
        var pr = await LoadAsync(id, cancellationToken);
        await EnsureItemsVisibleAsync(pr, cancellationToken);
        if (await EnterStockBlockerAsync(pr, cancellationToken) is { } blocker) throw new BusinessRuleException(blocker);
        if (request.Lines.GroupBy(l => l.LineId).Any(g => g.Count() > 1)) throw new BusinessRuleException("Mỗi dòng chỉ nhập tồn một lần.");
        pr.EnterStock(request.Lines.ToDictionary(l => l.LineId, l => l.StockQty),
            _scope.UserId ?? throw new ForbiddenAccessException("Chưa đăng nhập."), _clock.UtcNow);
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <summary>
    /// Lý do thủ kho chưa nhập tồn được trên PR (lát Y3b, design.md mục 19.3); null = nhập được. Phạm vi kho kiểm tra theo nơi nhận của
    /// phiếu (thủ kho của nơi nhận – Q2). Đã có cấp duyệt trong lượt thì khóa (design.md mục 20.1 – đã duyệt thì không sửa).
    /// </summary>
    /// <exception cref="ForbiddenAccessException">Khi không có quyền nhập tồn hoặc nơi nhận ngoài phạm vi kho.</exception>
    private async Task<string?> EnterStockBlockerAsync(PurchaseRequest pr, CancellationToken cancellationToken)
    {
        if (!_scope.HasPermission(Permissions.Purchase.PrEnterStock)) throw new ForbiddenAccessException("Bạn không có quyền nhập tồn trên đề nghị mua.");
        if (pr.Category == PurchaseCategory.Service) return $"Đề nghị dịch vụ {pr.No} không có bước Thủ kho nhập tồn.";
        if (pr.Status != ApprovalDocumentStatus.Submitted) return $"Chỉ nhập tồn khi đề nghị {pr.No} đang chờ duyệt.";
        if (pr.WarehouseId is not { } warehouseId) return $"Đề nghị {pr.No} chưa có nơi nhận – không xác định được thủ kho nhập tồn.";
        if (!_scope.AllWarehouses && !_scope.WarehouseIds.Contains(warehouseId))
            throw new ForbiddenAccessException($"Nơi nhận của đề nghị {pr.No} ngoài phạm vi kho của bạn – chỉ thủ kho nơi nhận nhập tồn.");
        // Lượt có bước Thủ kho (lát Y3c): tồn khóa khi bước Thủ kho đã duyệt; lượt không có bước Thủ kho: khi đã có cấp nào duyệt (lát Y3b).
        var round = await _db.ApprovalSteps.AsNoTracking()
            .Where(s => s.DocType == DocumentTypes.PurchaseRequest && s.DocumentId == pr.Id && s.Round == pr.ApprovalRound)
            .Select(s => new { s.Kind, s.Status })
            .ToListAsync(cancellationToken);
        var keeperSteps = round.Where(s => s.Kind == ApproverKind.Storekeeper).ToList();
        var locked = (keeperSteps.Count > 0 ? keeperSteps : round).Any(s => s.Status == ApprovalStepStatus.Approved);
        return locked ? $"Đề nghị {pr.No} đã có cấp duyệt trong lượt này – tồn đã khóa, không sửa được." : null;
    }

    /// <summary>
    /// Cờ nhập tồn cho DTO: chỉ người có quyền nhập tồn, phiếu loại A/B đang chờ duyệt; trả (nhập được, lý do chặn). Không ném lỗi – nơi nhận
    /// ngoài phạm vi kho thì không hiện nút cũng không hiện lý do (thủ kho kho khác không liên quan).
    /// </summary>
    private async Task<(bool Can, string? Blocker)> EnterStockStateAsync(PurchaseRequest pr, bool itemsVisible, CancellationToken cancellationToken)
    {
        if (!itemsVisible || !_scope.HasPermission(Permissions.Purchase.PrEnterStock) || pr.Category == PurchaseCategory.Service
            || pr.Status != ApprovalDocumentStatus.Submitted)
            return (false, null);
        if (pr.WarehouseId is { } warehouseId && !_scope.AllWarehouses && !_scope.WarehouseIds.Contains(warehouseId)) return (false, null);
        var blocker = await EnterStockBlockerAsync(pr, cancellationToken);
        return (blocker is null, blocker);
    }

    /// <summary>
    /// Gợi ý tồn (design.md mục 19.4 "gợi ý tồn đã xác nhận gần nhất"): theo mặt hàng, tồn thủ kho đã nhập trên đề nghị khác chưa hủy cùng
    /// nơi nhận, lần xác nhận mới nhất; trả tồn theo ĐVT cơ sở để quy về ĐVT của từng dòng.
    /// </summary>
    private async Task<Dictionary<Guid, LastStock>> LastStockAsync(PurchaseRequest pr, IReadOnlyList<Guid> itemIds, CancellationToken cancellationToken)
    {
        var rows = await (
                from l in _db.Set<PurchaseRequestLine>().AsNoTracking()
                join r in _db.PurchaseRequests on l.RequestId equals r.Id
                where r.Id != pr.Id && r.WarehouseId == pr.WarehouseId && r.Status != ApprovalDocumentStatus.Cancelled
                      && r.StockConfirmedAt != null && l.StockQty != null && itemIds.Contains(l.ItemId)
                orderby r.StockConfirmedAt descending
                select new { l.ItemId, Stock = l.StockQty!.Value, l.UomFactor, r.Id, r.No, At = r.StockConfirmedAt!.Value })
            .ToListAsync(cancellationToken);
        return rows.GroupBy(x => x.ItemId)
            .ToDictionary(g => g.Key, g => g.Select(x => new LastStock(x.Stock * x.UomFactor, x.Id, x.No, x.At)).First());
    }

    /// <summary>Tồn gợi ý: SL theo ĐVT cơ sở, đề nghị chứa tồn đó và thời điểm xác nhận.</summary>
    private sealed record LastStock(decimal BaseQty, Guid RequestId, string No, DateTimeOffset At);

    /// <summary>Người dùng hiện tại rút được phiếu về nháp không: phiếu chờ duyệt, là người lập hoặc có quyền Hủy phiếu.</summary>
    private bool CanRecall(ApprovableDocument document) =>
        document.Status == ApprovalDocumentStatus.Submitted
        && (document.CreatedBy == _scope.UserId || _scope.HasPermission(Permissions.System.DocumentCancel));

    /// <inheritdoc />
    public async Task<PurchaseRequestDto> CloseRemainingAsync(Guid id, CloseRemainingRequest request, CancellationToken cancellationToken)
    {
        // Khóa PR: PO lấy dòng của PR đang gửi duyệt cùng lúc (khóa PR nguồn – lát R1) phải xong trước.
        await using var transaction = await RowLocks.BeginOrJoinAsync(_db, cancellationToken);
        await RowLocks.LockAsync<PurchaseRequest>(_db, [id], cancellationToken);
        var pr = await LoadAsync(id, cancellationToken);
        var lineIds = pr.Lines.Select(l => l.Id).ToList();
        var ordered = await (
                from ol in _db.Set<PurchaseOrderLine>()
                join o in _db.PurchaseOrders on ol.OrderId equals o.Id
                where ol.SourcePrLineId != null && lineIds.Contains(ol.SourcePrLineId.Value) && o.Status != ApprovalDocumentStatus.Cancelled
                select o.Id)
            .AnyAsync(cancellationToken);
        // Chưa lên PO thì hủy PR là đúng nghĩa; đóng phần còn lại dành cho PR đã đặt một phần (lát R6, design.md mục 18.26).
        if (!ordered) throw new BusinessRuleException($"PR {pr.No} chưa lên đơn mua hàng nào – dùng lệnh Hủy phiếu thay cho đóng phần còn lại.");
        pr.CloseRemaining(_scope.UserId ?? throw new ForbiddenAccessException("Chưa đăng nhập."), _clock.UtcNow, request.Reason);
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
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

    /// <summary>Tổng số lượng đã đặt (ĐVT cơ sở) theo từng dòng PR, tính trên PO chưa hủy.</summary>
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
    /// Giá nhận hàng gần nhất theo ĐVT cơ sở (giá thực mua ÷ hệ số ĐVT) của các mặt hàng, từ phiếu xác nhận nhận hàng XNH đã xác nhận của
    /// công ty đang làm việc (từ 0.22.0 thay phiếu nhập kho – design.md mục 19.4; mọi nơi nhận).
    /// </summary>
    internal async Task<Dictionary<Guid, decimal>> LastPurchasePricesAsync(IReadOnlyList<Guid> itemIds, CancellationToken cancellationToken)
    {
        // Phiếu đi qua filter công ty – mỗi công ty là pháp nhân độc lập, không lấy giá nhận của công ty khác (CLAUDE.md mục 4).
        _scope.RequireCompany();
        var rows = await (
                from l in _db.Set<GoodsReceiptConfirmationLine>().AsNoTracking()
                join d in _db.GoodsReceiptConfirmations on l.ConfirmationId equals d.Id
                where itemIds.Contains(l.ItemId) && d.Status == ReceiptConfirmationStatus.Confirmed
                orderby d.Date descending, d.ConfirmedAt descending
                select new { l.ItemId, l.UnitPrice, l.UomFactor })
            .ToListAsync(cancellationToken);
        return rows.GroupBy(r => r.ItemId).ToDictionary(g => g.Key, g => Math.Round(g.First().UnitPrice / g.First().UomFactor, 6, MidpointRounding.AwayFromZero));
    }

    /// <summary>Kiểm tra kho nhận còn đang dùng; không thấy thì báo lỗi 404.</summary>
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

    /// <summary>
    /// Công trình của đề nghị loại A phải thuộc công ty đang làm việc (global query filter) và đang thực hiện; phiếu đã gắn công trình nay tạm
    /// ngừng / hoàn thành thì lưu lại vẫn được (giữ nguyên công trình), chặn ở bước gửi duyệt (design.md mục 19.3).
    /// </summary>
    private async Task CheckConstructionProjectAsync(Guid? projectId, Guid? currentId, CancellationToken cancellationToken)
    {
        if (projectId is not { } id) return;
        var status = await _db.ConstructionProjects.Where(p => p.Id == id).Select(p => (ConstructionProjectStatus?)p.Status).SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy công trình (hoặc công trình thuộc công ty khác).");
        if (status != ConstructionProjectStatus.InProgress && id != currentId)
            throw new BusinessRuleException("Công trình không còn ở trạng thái Đang thực hiện – không chọn được trên đề nghị mua mới.");
    }

    /// <summary>
    /// Khung chi phí công trình của đề nghị loại A (biểu mẫu 1A): dự toán, lũy kế theo PO đã duyệt, còn lại; phiếu nháp / chờ duyệt cộng thêm
    /// tổng dự kiến của chính phiếu để báo vượt dự toán (design.md mục 19.3, Q4).
    /// </summary>
    private async Task<ConstructionBudgetDto?> ConstructionBudgetAsync(PurchaseRequest pr, CancellationToken cancellationToken)
    {
        if (pr.ConstructionProjectId is not { } projectId) return null;
        var project = await _db.ConstructionProjects.AsNoTracking().Where(p => p.Id == projectId)
            .Select(p => new { p.Code, p.Name, p.Status, p.EstimateAmount }).SingleOrDefaultAsync(cancellationToken);
        if (project is null) return null;
        var spent = (await ConstructionSpending.ByProjectAsync(_db, [projectId], cancellationToken)).GetValueOrDefault(projectId);
        var pending = pr.Status is ApprovalDocumentStatus.Draft or ApprovalDocumentStatus.Submitted ? pr.TotalAmount : 0m;
        return new ConstructionBudgetDto(
            projectId, project.Code, project.Name, project.Status.ToString(), project.EstimateAmount, spent.Net, spent.Net + spent.Vat,
            project.EstimateAmount - spent.Net, pending, spent.Net + pending > project.EstimateAmount);
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
    /// các PO đã duyệt còn chờ nhận (chưa nhận đủ) của SL đặt − SL thực nhận theo phiếu XNH đã xác nhận, mỗi dòng không âm.
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

    /// <summary>Kết quả kiểm tra định mức sử dụng của một dòng PR (lát G11) – SL theo ĐVT cơ sở.</summary>
    private sealed record UsageNorm(string Status, decimal NormQty, decimal RequestedQty, DateOnly Through);

    /// <summary>
    /// Kiểm tra định mức sử dụng từng dòng PR – lát G11, design.md mục 18.25 (quyết định: Phòng cung ứng chung kiểm tra yêu cầu mua theo
    /// định mức; định mức căn cứ kế hoạch nhu cầu đã chốt của công ty). Mọi SL theo ĐVT cơ sở, lũy kế từ 01/01 đến hết tháng cần hàng T
    /// của dòng (ngày cần dòng → ngày cần đầu phiếu → ngày đề nghị):
    /// định mức = Σ tháng 1..T của dòng kế hoạch Đã chốt (SL tháng × hệ số ĐVT, làm tròn 4 số lẻ từng tháng);
    /// đã đề nghị = Σ dòng PR khác Đã gửi/Đã duyệt (Đã đóng phần còn lại: chỉ phần đã lên PO) có ngày cần trong khoảng
    /// + mọi dòng cùng mặt hàng của chính phiếu có ngày cần trong khoảng.
    /// Ví dụ kế hoạch 10/10/10 cái tháng 1–3, PR khác 15 cái tháng 2, dòng này 8 cái tháng 2 → 23 &gt; 20 → vượt (chỉ cảnh báo).
    /// Chỉ kiểm tra khi nhóm hàng của mặt hàng hoặc một nhóm cha có phiếu kế hoạch Đã chốt của năm đó – nhóm chưa lập kế hoạch thì không
    /// cảnh báo hàng loạt; nhóm có kế hoạch mà mặt hàng không có dòng → "NoPlan" (ngoài kế hoạch).
    /// </summary>
    /// <param name="pr">Phiếu (đã có Id dòng).</param>
    /// <param name="itemCategories">Nhóm hàng của các mặt hàng người xem thấy được (mặt hàng ngoài phạm vi không kiểm tra).</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Kết quả theo Id dòng PR; dòng không kiểm tra thì không có.</returns>
    private async Task<Dictionary<Guid, UsageNorm>> UsageNormsAsync(
        PurchaseRequest pr, IReadOnlyDictionary<Guid, Guid> itemCategories, CancellationToken cancellationToken)
    {
        if (pr.Status == ApprovalDocumentStatus.Cancelled || pr.Lines.Count == 0) return [];
        DateOnly Needed(DateOnly? lineDate) => lineDate ?? pr.NeededDate ?? pr.Date;
        var years = pr.Lines.Select(l => Needed(l.NeededDate).Year).Distinct().ToList();
        // Global query filter: chỉ phiếu kế hoạch của công ty đang làm việc.
        var plans = await _db.DemandPlans.AsNoTracking()
            .Where(p => p.Status == DemandPlanStatus.Confirmed && years.Contains(p.Year))
            .Select(p => new { p.Id, p.Year, p.ItemCategoryId }).ToListAsync(cancellationToken);
        if (plans.Count == 0) return [];

        var itemIds = pr.Lines.Select(l => l.ItemId).Distinct().ToList();
        var planIds = plans.Select(p => p.Id).ToList();
        var planLines = await _db.Set<DemandPlanLine>().AsNoTracking()
            .Where(l => planIds.Contains(l.PlanId) && itemIds.Contains(l.ItemId)).ToListAsync(cancellationToken);
        var parents = await _db.ItemCategories.AsNoTracking().Select(c => new { c.Id, c.ParentId })
            .ToDictionaryAsync(c => c.Id, c => c.ParentId, cancellationToken);
        var plannedCategories = plans.Select(p => (p.Year, p.ItemCategoryId)).ToHashSet();
        bool Covered(Guid categoryId, int year)
        {
            // Đi ngược lên nhóm cha; giới hạn số bước phòng dữ liệu vòng lặp.
            Guid? current = categoryId;
            for (var depth = 0; current is { } id && depth < 50; depth++)
            {
                if (plannedCategories.Contains((year, id))) return true;
                current = parents.GetValueOrDefault(id);
            }
            return false;
        }

        var fromDate = new DateOnly(years.Min(), 1, 1);
        var toDate = new DateOnly(years.Max(), 12, 31);
        var others = await (
                from l in _db.Set<PurchaseRequestLine>().AsNoTracking()
                join r in _db.PurchaseRequests on l.RequestId equals r.Id
                where r.Id != pr.Id && itemIds.Contains(l.ItemId)
                      && (r.Status == ApprovalDocumentStatus.Submitted || r.Status == ApprovalDocumentStatus.Approved
                          || r.Status == ApprovalDocumentStatus.Closed)
                      && (l.NeededDate ?? r.NeededDate ?? r.Date) >= fromDate && (l.NeededDate ?? r.NeededDate ?? r.Date) <= toDate
                select new { l.Id, l.ItemId, l.BaseQty, Needed = l.NeededDate ?? r.NeededDate ?? r.Date, r.Status })
            .ToListAsync(cancellationToken);
        // PR đã đóng phần còn lại (lát R6): phần chưa lên PO không còn là nhu cầu.
        var closedOrdered = await OrderedBaseByLineAsync(
            others.Where(o => o.Status == ApprovalDocumentStatus.Closed).Select(o => o.Id).ToList(), null, cancellationToken);
        var requestedRows = others.Select(o => (o.ItemId, o.Needed,
                Qty: o.Status == ApprovalDocumentStatus.Closed ? closedOrdered.GetValueOrDefault(o.Id) : o.BaseQty))
            .Concat(pr.Lines.Select(l => (l.ItemId, Needed: Needed(l.NeededDate), Qty: l.BaseQty)))
            .ToList();

        var result = new Dictionary<Guid, UsageNorm>();
        foreach (var line in pr.Lines)
        {
            if (!itemCategories.TryGetValue(line.ItemId, out var categoryId)) continue;
            var needed = Needed(line.NeededDate);
            if (!Covered(categoryId, needed.Year)) continue;
            var start = new DateOnly(needed.Year, 1, 1);
            var through = new DateOnly(needed.Year, needed.Month, DateTime.DaysInMonth(needed.Year, needed.Month));
            var yearPlans = plans.Where(p => p.Year == needed.Year).Select(p => p.Id).ToHashSet();
            var rows = planLines.Where(p => p.ItemId == line.ItemId && yearPlans.Contains(p.PlanId)).ToList();
            var norm = rows.Sum(p => p.Months.Take(needed.Month).Sum(q => Math.Round(q * p.UomFactor, 4, MidpointRounding.AwayFromZero)));
            var requested = requestedRows.Where(r => r.ItemId == line.ItemId && r.Needed >= start && r.Needed <= through).Sum(r => r.Qty);
            var status = rows.Count == 0 ? "NoPlan" : requested > norm ? "Over" : "Ok";
            result[line.Id] = new UsageNorm(status, norm, requested, through);
        }
        return result;
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
            .Select(i => new { i.Id, i.Code, i.Name, i.BaseUomId, i.CategoryId }).ToDictionaryAsync(i => i.Id, cancellationToken);
        var norms = await UsageNormsAsync(pr, items.ToDictionary(i => i.Key, i => i.Value.CategoryId), cancellationToken);
        var uomIds = pr.Lines.Select(l => l.UomId).Concat(pr.NewItems.Select(n => n.UomId)).Concat(items.Values.Select(i => i.BaseUomId)).Distinct().ToList();
        var uoms = await _db.Uoms.AsNoTracking().Where(u => uomIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Code, cancellationToken);
        var supplierIds = pr.Lines.Select(l => l.SuggestedSupplierId).OfType<Guid>().Distinct().ToList();
        var suppliers = await _db.Suppliers.AsNoTracking().Where(s => supplierIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Code, cancellationToken);
        // Tồn do thủ kho nơi nhận nhập trên phiếu (lát Y3b) – không đọc tồn kho của phân hệ kho cũ (CLAUDE.md mục 4, từ 0.22.0).
        var itemsVisible = items.Count == itemIds.Count;
        var (canEnterStock, enterStockBlocker) = await EnterStockStateAsync(pr, itemsVisible, cancellationToken);
        var lastStock = canEnterStock ? await LastStockAsync(pr, itemIds, cancellationToken) : [];
        var ordered = await OrderedBaseByLineAsync(pr.Lines.Select(l => l.Id).ToList(), null, cancellationToken);
        var onOrder = await OnOrderBaseByItemAsync(itemIds, cancellationToken);
        var department = pr.DepartmentId is { } departmentId
            ? await _db.Departments.Where(d => d.Id == departmentId).Select(d => d.Name).SingleOrDefaultAsync(cancellationToken)
            : null;
        var userIds = new[] { pr.CreatedBy, pr.RequesterId, pr.StockConfirmedBy }.OfType<Guid>().Distinct().ToList();
        var users = await _db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken);

        var lines = pr.Lines.OrderBy(l => l.LineNo).Select(l =>
        {
            var item = items.GetValueOrDefault(l.ItemId);
            var norm = norms.GetValueOrDefault(l.Id);
            var last = lastStock.GetValueOrDefault(l.ItemId);
            return new PurchaseRequestLineDto(
                l.Id, l.LineNo, l.ItemId, item?.Code ?? "(ngoài phạm vi)", item?.Name ?? "", l.UomId, uoms.GetValueOrDefault(l.UomId) ?? "",
                l.Qty, l.UomFactor, l.BaseQty, item is null ? "" : uoms.GetValueOrDefault(item.BaseUomId) ?? "", l.EstPrice, l.Amount,
                l.SuggestedSupplierId, l.SuggestedSupplierId is { } s ? suppliers.GetValueOrDefault(s) : null, l.NeededDate, l.Note,
                Math.Round(ordered.GetValueOrDefault(l.Id) / l.UomFactor, 4, MidpointRounding.AwayFromZero), l.RequiredQty, l.StockQty,
                onOrder.GetValueOrDefault(l.ItemId), norm?.Status, norm?.NormQty ?? 0, norm?.RequestedQty ?? 0, norm?.Through)
            {
                LastStockQty = last is null ? null : Math.Round(last.BaseQty / l.UomFactor, 4, MidpointRounding.AwayFromZero),
                LastStockRequestNo = last?.No, LastStockRequestId = last?.RequestId, LastStockAt = last?.At,
            };
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

        var step = pr.Status == ApprovalDocumentStatus.Submitted ? await _approvals.CurrentStepAsync(pr, cancellationToken) : null;
        // Bước Thủ kho (lát Y3c): thủ kho thấy nút Duyệt sau khi đã nhập tồn trong lượt này.
        var canApprove = step is not null && itemsVisible && await _approvals.ApproveBlockerAsync(pr, step, cancellationToken) is null
            && pr.ApproveReadyBlocker(step) is null;
        var canCreate = _scope.HasPermission(Permissions.Purchase.PrCreate);
        string? readOnly = !itemsVisible ? "Phiếu có mặt hàng ngoài phạm vi nhóm hàng của bạn nên chỉ xem được."
            : !canCreate ? "Bạn không có quyền lập, sửa đề nghị mua hàng." : null;
        var actions = new PurchaseDocumentActions(
            pr.Status == ApprovalDocumentStatus.Draft && readOnly is null,
            pr.Status == ApprovalDocumentStatus.Draft && readOnly is null,
            canApprove,
            // Hủy: nháp – người lập; đã gửi duyệt trở đi – chỉ quản trị (design.md mục 20.2a).
            // PR đã lên PO / đã đóng: quản trị hủy theo dây chuyền (hủy PO, phiếu nhận hàng trước – lát Z4b).
            pr.Status is ApprovalDocumentStatus.Draft or ApprovalDocumentStatus.Submitted or ApprovalDocumentStatus.Approved or ApprovalDocumentStatus.Closed
                && readOnly is null && DocumentCancelPolicy.CanCancel(_scope, pr.Status == ApprovalDocumentStatus.Draft, pr.CreatedBy),
            readOnly,
            pr.Status == ApprovalDocumentStatus.Approved && readOnly is null && lines.Any(l => l.OrderedQty > 0),
            readOnly is null && CanRecall(pr));

        return new PurchaseRequestDto(
            pr.Id, pr.No, pr.Date, pr.Status.ToString(), pr.DepartmentId, department ?? pr.LegacyDepartment, pr.RequesterId is { } r ? users.GetValueOrDefault(r) : null, pr.Purpose,
            pr.NeededDate, pr.WarehouseId, pr.Priority.ToString(), pr.Note, pr.TotalAmount, pr.RejectReason, pr.CreatedAt,
            pr.CreatedBy is { } c ? users.GetValueOrDefault(c) : null, pr.ApprovedAt, lines,
            await _approvals.HistoryAsync(DocumentTypes.PurchaseRequest, pr.Id, cancellationToken, pr), actions, newItems,
            pendingNew && _scope.HasPermission(Permissions.Sourcing.RfqManage),
            pendingNew && (_scope.HasPermission(Permissions.Purchase.PoCreate) || canCreate), BudgetService.ToDto(pr)) { Version = pr.Version, ClosedReason = pr.ClosedReason, ClosedAt = pr.ClosedAt, CancelReason = pr.CancelReason, ClosedByName = pr.ClosedBy is { } closedBy ? await _db.Users.AsNoTracking().Where(u => u.Id == closedBy).Select(u => u.FullName).SingleOrDefaultAsync(cancellationToken) : null,
            Category = pr.Category.ToString(), Construction = await ConstructionBudgetAsync(pr, cancellationToken),
            WarehouseName = pr.WarehouseId is { } warehouseId
                ? await _db.Warehouses.AsNoTracking().Where(w => w.Id == warehouseId).Select(w => w.Name).SingleOrDefaultAsync(cancellationToken)
                : null,
            StockConfirmedByName = pr.StockConfirmedBy is { } stockBy ? users.GetValueOrDefault(stockBy) : null, StockConfirmedAt = pr.StockConfirmedAt,
            CanEnterStock = canEnterStock, EnterStockBlocker = enterStockBlocker };
    }

    /// <summary>Đọc trạng thái từ chuỗi.</summary>
    /// <exception cref="BusinessRuleException">Khi trạng thái không hợp lệ.</exception>
    internal static ApprovalDocumentStatus ParseStatus(string status) =>
        Enum.TryParse<ApprovalDocumentStatus>(status, ignoreCase: true, out var s) && Enum.IsDefined(s)
            ? s
            : throw new BusinessRuleException("Trạng thái phải là Draft, Submitted, Approved, Closed hoặc Cancelled.");

    /// <summary>Đổi yêu cầu lưu PR thành dữ liệu header cho Domain (mức ưu tiên mặc định Thường).</summary>
    private static PurchaseRequestHeader ToHeader(SavePurchaseRequestRequest r) =>
        new(r.Date, r.DepartmentId, r.Purpose, r.NeededDate, r.WarehouseId,
            Enum.TryParse<PurchasePriority>(r.Priority, ignoreCase: true, out var p) && Enum.IsDefined(p)
                ? p
                : throw new BusinessRuleException("Mức ưu tiên phải là Low, Normal hoặc Urgent."),
            r.Note,
            string.IsNullOrWhiteSpace(r.Category) ? PurchaseCategory.Goods
            : Enum.TryParse<PurchaseCategory>(r.Category, ignoreCase: true, out var c) && Enum.IsDefined(c) ? c
            : throw new BusinessRuleException("Loại mua phải là Construction (A), Goods (B) hoặc Service (DV)."),
            r.ConstructionProjectId);
}
