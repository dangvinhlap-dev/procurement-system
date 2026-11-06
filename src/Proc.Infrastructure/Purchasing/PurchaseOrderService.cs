using Microsoft.EntityFrameworkCore;
using Proc.Application.Common;
using Proc.Application.Documents;
using Proc.Application.Pricing;
using Proc.Application.Purchasing;
using Proc.Application.Sourcing;
using Proc.Domain.Common;
using Proc.Domain.Documents;
using Proc.Domain.Inventory;
using Proc.Domain.MasterData;
using Proc.Domain.Organization;
using Proc.Domain.Pricing;
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
    private readonly IPriceListService _prices;
    private readonly IRfqAwardPriceReader _awards;
    private readonly BudgetUsage _budgets;

    /// <summary>Tạo dịch vụ.</summary>
    public PurchaseOrderService(
        ProcDbContext db, ICurrentUserScope scope, IDocumentNumberService numbers, IClock clock, IPriceListService prices, IRfqAwardPriceReader awards)
    {
        _db = db;
        _scope = scope;
        _numbers = numbers;
        _clock = clock;
        _prices = prices;
        _awards = awards;
        _approvals = new ApprovalEngine(db, scope, clock);
        _budgets = new BudgetUsage(db);
    }

    /// <inheritdoc />
    public async Task<PurchaseDocumentListResult> SearchAsync(PurchaseDocumentQuery query, CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        var today = _clock.TodayInVietnam();
        var to = query.To ?? today;
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
                    Base = o.Lines.Sum(l => l.BaseQty), o.IsOutOfContract, o.HasPriceException, o.PurchaseType, ClosedManually = o.ClosedAt != null,
                    // Như PurchaseOrder.IsDocsOverdue, viết lại để EF dịch sang SQL.
                    DocsOverdue = o.PurchaseType == PurchaseType.Emergency && o.DocsCompletedAt == null && o.DocsDueDate < today
                                  && (o.Status == ApprovalDocumentStatus.Approved || o.Status == ApprovalDocumentStatus.Closed),
                })
            .Take(PurchaseRequestService.SearchLimit + 1)
            .ToListAsync(cancellationToken);
        var received = await ReceivedBaseByOrderAsync(rows.Select(r => r.Id).ToList(), cancellationToken);
        var items = rows.Take(PurchaseRequestService.SearchLimit).Select(r => new PurchaseDocumentListItemDto(
                r.Id, r.No, r.Date, r.Status.ToString(), r.Supplier, r.Warehouse, r.ExpectedDate, r.Amount, r.LineCount, r.CreatedBy,
                r.Base > 0 ? Math.Min(100m, Math.Round(received.GetValueOrDefault(r.Id) * 100m / r.Base, 1)) : 0m, null, r.IsOutOfContract,
                r.HasPriceException, r.PurchaseType.ToString(), r.DocsOverdue, r.ClosedManually))
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
        var departmentId = await ResolveDepartmentAsync(request.DepartmentId, lines, null, cancellationToken);
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var no = await _numbers.NextAsync(DocumentTypes.PurchaseOrder, request.Date, cancellationToken);
        var order = PurchaseOrder.Create(companyId, no, ToHeader(request, departmentId));
        order.SetLines(lines);
        await ApplyApprovedPricesAsync(order, cancellationToken);
        order.ApplyBudgetCheck(await _budgets.CheckAsync(order, cancellationToken));
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
        var departmentId = await ResolveDepartmentAsync(request.DepartmentId, lines, order.DepartmentId, cancellationToken);
        order.Update(ToHeader(request, departmentId));
        order.SetLines(lines);
        await ApplyApprovedPricesAsync(order, cancellationToken);
        order.ApplyBudgetCheck(await _budgets.CheckAsync(order, cancellationToken));
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PurchaseOrderDto> SubmitAsync(Guid id, CancellationToken cancellationToken)
    {
        // Khóa PO và các PR nguồn tới hết giao dịch: gửi trùng một PO, hoặc hai PO cùng lấy một dòng PR gửi cùng lúc, phải lần lượt –
        // lệnh sau thấy PO đã gửi / SL PR đã đặt của lệnh trước, không đặt quá SL đề nghị (lỗi L1).
        await using var transaction = await RowLocks.BeginOrJoinAsync(_db, cancellationToken);
        var sourceRequests = await (
                from ol in _db.Set<PurchaseOrderLine>().AsNoTracking()
                join rl in _db.Set<PurchaseRequestLine>().AsNoTracking() on ol.SourcePrLineId equals rl.Id
                where ol.OrderId == id
                select rl.RequestId)
            .Distinct().ToListAsync(cancellationToken);
        await RowLocks.LockAsync<PurchaseOrder>(_db, [id], cancellationToken);
        await RowLocks.LockAsync<PurchaseRequest>(_db, sourceRequests, cancellationToken);
        var order = await LoadAsync(id, cancellationToken);
        await EnsureItemsVisibleAsync(order, cancellationToken);
        // Kiểm tra lại lúc gửi: NCC có thể bị tạm ngưng, PR nguồn có thể đã lên PO khác từ lúc lưu nháp.
        await CheckHeaderAsync(order.SupplierId, order.WarehouseId, cancellationToken);
        await CheckRequestQuantitiesAsync(order.Lines.Select(l => (l.SourcePrLineId, l.ItemId, l.BaseQty, l.LineNo)).ToList(), order.Id, cancellationToken);
        // Giá đã duyệt tính lại lúc gửi (bảng giá/hợp đồng có thể đã đổi từ lúc lưu nháp); lệch giá phải có lý do và qua chặng duyệt
        // ngoại lệ – design.md mục 18.10 (quyết định M1).
        await ApplyApprovedPricesAsync(order, cancellationToken);
        order.EnsurePriceDeviationsExplained();
        // Hạn mức SL tháng của phiếu duyệt NCC và giá tháng (gộp các công ty áp dụng) – vượt thì chặn gửi (design.md mục 18.25, G3).
        await CheckMonthlyLimitsAsync(order, cancellationToken);
        // Mua khẩn cấp (design.md mục 18.11 – V4): không lập lùi ngày để hợp thức hóa hàng đã mua; duyệt theo bộ cấp POK nếu đã cấu hình.
        string? ruleType = null;
        if (order.PurchaseType == PurchaseType.Emergency)
        {
            if (order.Date < _clock.TodayInVietnam())
                throw new BusinessRuleException("Đơn mua khẩn cấp không được lập lùi ngày (không dùng mua khẩn để hợp thức hóa hàng đã mua) – sửa ngày đặt hàng là hôm nay.");
            ruleType = ApprovalTypes.PurchaseOrderEmergency;
        }

        // Ngân sách tính lại lúc gửi; vượt thì thêm chặng "vượt ngân sách" POB sau chặng ngoại lệ giá (design.md mục 18.24) – không chặn.
        order.ApplyBudgetCheck(await _budgets.CheckAsync(order, cancellationToken));
        var stages = new List<(string, int)>();
        if (order.HasPriceException) stages.Add((ApprovalTypes.PurchaseOrderPriceException, ApprovalTypes.PriceExceptionStage));
        if (order.IsOverBudget) stages.Add((ApprovalTypes.PurchaseOrderOverBudget, ApprovalTypes.OverBudgetStage));
        // Mua ngoài danh sách tháng hoặc không có giá đã duyệt: bắt buộc duyệt thêm chặng POL (người dùng chốt 06/11/2026 – mục 18.25).
        if (order.IsOffApprovedList) stages.Add((ApprovalTypes.PurchaseOrderOffList, ApprovalTypes.OffListStage));
        await _approvals.SubmitWithStagesAsync(order, stages, cancellationToken, ruleType);
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PurchaseOrderDto> CompleteDocsAsync(Guid id, CompleteDocsRequest request, CancellationToken cancellationToken)
    {
        var order = await LoadAsync(id, cancellationToken);
        await EnsureItemsVisibleAsync(order, cancellationToken);
        order.CompleteDocs(_scope.UserId ?? throw new ForbiddenAccessException("Chưa đăng nhập."), _clock.UtcNow, request.Note);
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
    public async Task<PurchaseOrderDto> CloseRemainingAsync(Guid id, CloseRemainingRequest request, CancellationToken cancellationToken)
    {
        // Khóa PO: phiếu nhập đang ghi sổ cùng lúc phải xong trước, rồi mới xét còn phiếu nhập nháp / đã nhận chưa (lát R1).
        await using var transaction = await RowLocks.BeginOrJoinAsync(_db, cancellationToken);
        await RowLocks.LockAsync<PurchaseOrder>(_db, [id], cancellationToken);
        var order = await LoadAsync(id, cancellationToken);
        await EnsureItemsVisibleAsync(order, cancellationToken);
        // Phiếu nhập nháp theo PO phải ghi sổ hoặc hủy trước – không để phiếu treo trỏ vào PO đã đóng (lát R6, design.md mục 18.26).
        var drafts = await _db.InventoryDocuments.IgnoreQueryFilters([ProcDbContext.WarehouseFilter]).AsNoTracking()
            .Where(d => d.PurchaseOrderId == id && d.Status == DocumentStatus.Draft).Select(d => d.No).ToListAsync(cancellationToken);
        if (drafts.Count > 0)
            throw new BusinessRuleException($"Đơn {order.No} còn phiếu nhập nháp {string.Join(", ", drafts)} – ghi sổ hoặc hủy trước khi đóng phần còn lại.");
        var received = await ReceivedBaseByLineAsync(order.Lines.Select(l => l.Id).ToList(), null, cancellationToken);
        if (!received.Values.Any(q => q > 0))
            throw new BusinessRuleException($"Đơn {order.No} chưa nhận hàng – dùng lệnh Hủy phiếu thay cho đóng phần còn lại.");
        order.CloseRemaining(_scope.UserId ?? throw new ForbiddenAccessException("Chưa đăng nhập."), _clock.UtcNow, request.Reason);
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
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

    // IgnoreQueryFilters tắt filter công ty cho cả câu truy vấn; an toàn vì orderIds đã lấy qua filter công ty.
    /// <inheritdoc />
    public async Task<IReadOnlyList<PoApprovedPriceDto>> ApprovedPricesAsync(
        Guid supplierId, DateOnly date, IReadOnlyList<Guid> itemIds, Guid? departmentId, CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        if (itemIds.Count == 0) return [];
        var companyId = _scope.RequireCompany();
        var contract = await EffectiveContractAsync(supplierId, date, cancellationToken) is { } c && c.Allows(departmentId) ? c : null;
        var tiers = await _prices.ResolveAsync(supplierId, date, itemIds, cancellationToken, contract?.PriceListId);
        // Giá phiếu tháng đứng đầu (G3): mặt hàng có giá tháng cho NCC này thì chỉ trả giá tháng (một bậc từ 0, CK 0 – giá phiếu đã trừ CK);
        // NCC ngoài danh sách thì vẫn trả bậc hợp đồng/bảng giá như server tính (dòng bị gắn cờ ngoài danh sách khi lưu).
        var monthly = (await MonthlyLinesAsync(companyId, date, itemIds, cancellationToken)).Values
            .Select(m => (Line: m, Price: m.PriceFor(supplierId))).Where(x => x.Price is not null).ToList();
        var monthlyItems = monthly.Select(x => x.Line.ItemId).ToHashSet();
        return monthly.Select(x => new PoApprovedPriceDto(
                x.Line.ItemId, x.Line.UomId, x.Line.UomFactor, 0m, x.Price!.Value, 0m, null, PurchasePriceSource.MonthlyApproval.ToString(), x.Line.No, null))
            .Concat(tiers.Where(t => !monthlyItems.Contains(t.ItemId)).Select(t =>
            {
                var fromContract = contract is not null && t.PriceListId == contract.PriceListId;
                return new PoApprovedPriceDto(
                    t.ItemId, t.UomId, t.UomFactor, t.MinQty, t.UnitPrice, t.DiscountPct, t.VatCode,
                    (fromContract ? PurchasePriceSource.Contract : PurchasePriceSource.PriceList).ToString(), t.PriceListNo, fromContract ? contract!.No : null);
            }))
            .ToList();
    }

    /// <summary>
    /// Hợp đồng nguyên tắc của công ty với NCC có hiệu lực vào ngày đặt hàng: Hiệu lực và ngày trong [từ, đến]; hợp đồng đã chấm dứt chỉ
    /// tính tới trước ngày chấm dứt. Mỗi công ty × NCC chỉ một hợp đồng trong một thời kỳ (design.md mục 18.7), lấy cái bắt đầu muộn nhất
    /// cho chắc.
    /// </summary>
    private Task<EffectiveContract?> EffectiveContractAsync(Guid supplierId, DateOnly date, CancellationToken cancellationToken) =>
        _db.PurchaseContracts.AsNoTracking()
            .Where(c => c.SupplierId == supplierId && c.ValidFrom <= date && c.ValidTo >= date
                        && (c.Status == ContractStatus.Active || (c.Status == ContractStatus.Terminated && c.TerminatedOn > date)))
            .OrderByDescending(c => c.ValidFrom)
            .Select(c => new EffectiveContract(c.Id, c.No, c.PriceListId, c.Departments.Select(d => d.DepartmentId).ToList()))
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>
    /// Bộ phận đặt hàng của PO (design.md mục 18.10): dòng lấy từ PR thì mọi PR nguồn phải cùng một bộ phận, trùng bộ phận đã chọn; chưa
    /// chọn thì lấy bộ phận của PR. Bộ phận chọn mới phải thuộc công ty và đang dùng (giữ nguyên bộ phận cũ đã ngừng thì được).
    /// </summary>
    /// <exception cref="BusinessRuleException">Khi trộn PR nhiều bộ phận, PR khác bộ phận của PO hoặc bộ phận ngừng dùng.</exception>
    private async Task<Guid?> ResolveDepartmentAsync(
        Guid? requested, IReadOnlyList<(PurchaseOrderLineData Data, Item Item)> lines, Guid? current, CancellationToken cancellationToken)
    {
        var departmentId = requested == Guid.Empty ? null : requested;
        var prLineIds = lines.Select(l => l.Data.SourcePrLineId).OfType<Guid>().Distinct().ToList();
        var prDepartments = await (
                from l in _db.Set<PurchaseRequestLine>().AsNoTracking()
                join r in _db.PurchaseRequests on l.RequestId equals r.Id
                where prLineIds.Contains(l.Id) && r.DepartmentId != null
                select r.DepartmentId!.Value)
            .Distinct().ToListAsync(cancellationToken);
        var ids = prDepartments.Append(departmentId ?? Guid.Empty).Where(x => x != Guid.Empty).Distinct().ToList();
        var departments = await _db.Departments.AsNoTracking().Where(d => ids.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, d => new { d.Code, d.IsActive }, cancellationToken);
        string Code(Guid id) => departments.GetValueOrDefault(id)?.Code ?? "(không tìm thấy)";
        if (prDepartments.Count > 1)
            throw new BusinessRuleException($"Các dòng lấy từ PR của nhiều bộ phận ({string.Join(", ", prDepartments.Select(Code).Order())}) – mỗi đơn mua hàng một bộ phận đặt hàng.");
        if (prDepartments.Count == 1)
        {
            if (departmentId is { } chosen && chosen != prDepartments[0])
                throw new BusinessRuleException($"Dòng lấy từ PR của bộ phận {Code(prDepartments[0])}, khác bộ phận đặt hàng {Code(chosen)} của đơn.");
            departmentId = prDepartments[0];
        }

        if (departmentId is { } id && id != current)
        {
            var department = departments.GetValueOrDefault(id) ?? throw new NotFoundException("Không tìm thấy bộ phận đặt hàng ở công ty đang làm việc.");
            if (!department.IsActive) throw new BusinessRuleException($"Bộ phận {department.Code} đã ngừng dùng.");
        }

        return departmentId;
    }

    /// <summary>
    /// Gán giá đã duyệt cho các dòng PO theo thứ tự ưu tiên (design.md mục 18.10): giá trúng thầu của dòng RFQ nguồn (cùng mặt hàng, ĐVT;
    /// RFQ Đã chọn thầu cho NCC này) → bảng giá của hợp đồng nguyên tắc hiệu lực → bảng giá NCC hiệu lực (chọn bậc như
    /// <see cref="PriceTierPicker"/>) → không có (ngoài hợp đồng).
    /// </summary>
    private async Task ApplyApprovedPricesAsync(PurchaseOrder order, CancellationToken cancellationToken)
    {
        // Hợp đồng giới hạn bộ phận được phép đặt mà PO của bộ phận khác (hoặc chưa chọn bộ phận) thì không tính giá theo hợp đồng – G5.
        var contract = await EffectiveContractAsync(order.SupplierId, order.Date, cancellationToken) is { } c && c.Allows(order.DepartmentId) ? c : null;
        var itemIds = order.Lines.Select(l => l.ItemId).Distinct().ToList();
        var tiers = (await _prices.ResolveAsync(order.SupplierId, order.Date, itemIds, cancellationToken, contract?.PriceListId))
            .GroupBy(t => t.ItemId).ToDictionary(g => g.Key, g => g.ToList());
        var rfqLineIds = order.Lines.Select(l => l.SourceRfqLineId).OfType<Guid>().Distinct().ToList();
        var awards = (await _awards.GetAsync(order.SupplierId, rfqLineIds, cancellationToken)).ToDictionary(a => a.RfqLineId);
        var monthly = await MonthlyLinesAsync(order.CompanyId, order.Date, itemIds, cancellationToken);
        order.ApplyApprovedPrices(line =>
        {
            // Phiếu duyệt NCC và giá tháng là quyết định của Ban cung ứng – đứng đầu thứ tự (design.md mục 18.25, G3).
            if (monthly.TryGetValue(line.ItemId, out var m) && m.PriceFor(order.SupplierId) is { } monthlyPrice)
                return new ApprovedPrice(PurchasePriceSource.MonthlyApproval, m.PriceIn(monthlyPrice, line.UomFactor), 0m);
            if (line.SourceRfqLineId is { } rfqLineId && awards.TryGetValue(rfqLineId, out var award) && award.ItemId == line.ItemId && award.UomId == line.UomId)
                return new ApprovedPrice(PurchasePriceSource.Rfq, award.UnitPrice, award.DiscountPct, RfqLineId: rfqLineId);
            if (!tiers.TryGetValue(line.ItemId, out var itemTiers)) return null;
            var picked = PriceTierPicker.Pick(
                itemTiers.Select(t => new PriceTier(t.UomId, t.UomFactor, t.MinQty, t.UnitPrice, t.DiscountPct)), line.UomId, line.UomFactor, line.Qty);
            if (picked is not { } price) return null;
            var listId = itemTiers[0].PriceListId;
            var fromContract = contract is not null && listId == contract.PriceListId;
            return new ApprovedPrice(
                fromContract ? PurchasePriceSource.Contract : PurchasePriceSource.PriceList, price.UnitPrice, price.DiscountPct, listId,
                fromContract ? contract!.Id : null);
        }, line => monthly.TryGetValue(line.ItemId, out var m) ? m.Id : null);

        // Ghi nhận mua theo NCC độc quyền: phiếu còn tác dụng vào ngày PO của công ty × NCC × mặt hàng (điều kiện như
        // SoleSourceApproval.IsEffectiveOn, viết lại để EF dịch sang SQL); nhiều phiếu thì lấy phiếu hiệu lực muộn nhất.
        var soleSources = (await _db.SoleSourceApprovals.AsNoTracking()
                .Where(x => x.CompanyId == order.CompanyId && x.SupplierId == order.SupplierId && itemIds.Contains(x.ItemId)
                            && x.Status == SoleSourceStatus.Approved && x.ValidFrom <= order.Date && x.ReviewDate >= order.Date)
                .Select(x => new { x.Id, x.ItemId, x.ValidFrom })
                .ToListAsync(cancellationToken))
            .GroupBy(x => x.ItemId).ToDictionary(g => g.Key, g => g.MaxBy(x => x.ValidFrom)!.Id);
        order.ApplySoleSources(line => soleSources.TryGetValue(line.ItemId, out var id) ? id : null);
    }

    /// <summary>Dòng phiếu duyệt NCC và giá tháng đã duyệt áp cho công ty của PO (design.md mục 18.25).</summary>
    private sealed record MonthlyLine(
        Guid Id, string No, Guid ItemId, Guid UomId, decimal UomFactor, Guid MainSupplierId, decimal UnitPrice, Guid? BackupSupplierId,
        decimal? BackupUnitPrice, decimal? QuantityLimit, Guid ApprovalId)
    {
        /// <summary>Giá theo ĐVT của phiếu cho NCC: NCC chính → giá chính, NCC dự phòng → giá dự phòng; NCC khác → null (ngoài danh sách).</summary>
        public decimal? PriceFor(Guid supplierId) =>
            supplierId == MainSupplierId ? UnitPrice : supplierId == BackupSupplierId ? BackupUnitPrice : null;

        /// <summary>
        /// Quy giá theo ĐVT phiếu sang ĐVT dòng PO: giá ÷ hệ số ĐVT phiếu × hệ số ĐVT dòng, làm tròn 6 số (0,5 lên). Ví dụ phiếu 120.000/thùng
        /// 12 cái, PO đặt theo cái → 10.000/cái.
        /// </summary>
        public decimal PriceIn(decimal price, decimal lineFactor) =>
            UomFactor == lineFactor ? price : Math.Round(price / UomFactor * lineFactor, 6, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Dòng phiếu tháng Đã duyệt (không tính Đã thay, Đã hủy) áp cho công ty × tháng của ngày đặt hàng, theo mặt hàng. Phiếu thuộc tổng
    /// công ty nên bỏ filter công ty, nhưng chỉ lấy phiếu có công ty này trong danh sách áp dụng (đã công bố cho công ty – như màn hình GTH);
    /// bỏ filter nhóm hàng vì mặt hàng của PO đã kiểm tra phạm vi riêng. Mỗi mặt hàng × tháng × công ty chỉ một phiếu còn hiệu lực (G2).
    /// </summary>
    private async Task<Dictionary<Guid, MonthlyLine>> MonthlyLinesAsync(
        Guid companyId, DateOnly date, IReadOnlyCollection<Guid> itemIds, CancellationToken cancellationToken)
    {
        var period = new DateOnly(date.Year, date.Month, 1);
        var lines = await (
                from a in _db.MonthlySupplierApprovals.IgnoreQueryFilters([ProcDbContext.CompanyFilter, ProcDbContext.ItemCategoryFilter]).AsNoTracking()
                where a.Period == period && a.Status == MonthlyApprovalStatus.Approved && a.Companies.Any(c => c.CompanyId == companyId)
                from l in a.Lines
                where itemIds.Contains(l.ItemId)
                select new MonthlyLine(
                    l.Id, a.No, l.ItemId, l.UomId, l.UomFactor, l.MainSupplierId, l.UnitPrice, l.BackupSupplierId, l.BackupUnitPrice,
                    l.QuantityLimit, a.Id))
            .ToListAsync(cancellationToken);
        return lines.GroupBy(l => l.ItemId).ToDictionary(g => g.Key, g => g.First());
    }

    /// <summary>
    /// Chặn gửi duyệt khi vượt hạn mức SL tháng của phiếu duyệt tháng (người dùng chốt 06/11/2026 – hạn mức là số lượng gộp các công ty áp
    /// dụng để đạt bậc chiết khấu; vượt thì chặn). Đã đặt = SL cơ sở mọi PO đã gửi/duyệt/đóng (không tính nháp, đã hủy; mọi NCC) của
    /// các công ty áp dụng phiếu, cùng mặt hàng, ngày đặt trong tháng, trừ PO này; quy về ĐVT phiếu = SL cơ sở ÷ hệ số ĐVT phiếu.
    /// Ví dụ hạn mức 100 cái, đã đặt 61, PO này 50 → 111 > 100: chặn.
    /// </summary>
    /// <exception cref="BusinessRuleException">Khi có mặt hàng vượt hạn mức (báo dòng, phiếu, hạn mức, đã đặt, đơn này).</exception>
    private async Task CheckMonthlyLimitsAsync(PurchaseOrder order, CancellationToken cancellationToken)
    {
        var limitLineIds = order.Lines.Select(l => l.MonthlyApprovalLineId).OfType<Guid>().Distinct().ToList();
        if (limitLineIds.Count == 0) return;
        var monthly = (await MonthlyLinesAsync(order.CompanyId, order.Date, order.Lines.Select(l => l.ItemId).Distinct().ToList(), cancellationToken))
            .Values.Where(m => m.QuantityLimit is not null && limitLineIds.Contains(m.Id)).ToList();
        if (monthly.Count == 0) return;
        var monthStart = new DateOnly(order.Date.Year, order.Date.Month, 1);
        var monthEnd = monthStart.AddMonths(1);
        var approvalIds = monthly.Select(m => m.ApprovalId).Distinct().ToList();
        var companies = await _db.MonthlySupplierApprovalCompanies.AsNoTracking()
            .Where(c => approvalIds.Contains(c.ApprovalId)).Select(c => new { c.ApprovalId, c.CompanyId }).ToListAsync(cancellationToken);
        var itemIds = monthly.Select(m => m.ItemId).ToList();
        var allCompanies = companies.Select(c => c.CompanyId).Distinct().ToList();
        // Gộp các công ty áp dụng: bỏ filter công ty + nhóm hàng, chỉ lấy tổng SL theo công ty × mặt hàng (không trả chi tiết PO công ty khác).
        var ordered = await (
                from o in _db.PurchaseOrders.IgnoreQueryFilters([ProcDbContext.CompanyFilter, ProcDbContext.ItemCategoryFilter]).AsNoTracking()
                where o.Id != order.Id && allCompanies.Contains(o.CompanyId) && o.Date >= monthStart && o.Date < monthEnd
                      && (o.Status == ApprovalDocumentStatus.Submitted || o.Status == ApprovalDocumentStatus.Approved || o.Status == ApprovalDocumentStatus.Closed)
                from l in o.Lines
                where itemIds.Contains(l.ItemId)
                group l by new { o.CompanyId, l.ItemId } into g
                select new { g.Key.CompanyId, g.Key.ItemId, BaseQty = g.Sum(x => x.BaseQty) })
            .ToListAsync(cancellationToken);
        var errors = new List<string>();
        foreach (var m in monthly)
        {
            var applies = companies.Where(c => c.ApprovalId == m.ApprovalId).Select(c => c.CompanyId).ToHashSet();
            var before = ordered.Where(x => x.ItemId == m.ItemId && applies.Contains(x.CompanyId)).Sum(x => x.BaseQty) / m.UomFactor;
            var lines = order.Lines.Where(l => l.ItemId == m.ItemId).ToList();
            var current = lines.Sum(l => l.BaseQty) / m.UomFactor;
            if (before + current <= m.QuantityLimit!.Value) continue;
            errors.Add($"dòng {string.Join(", ", lines.Select(l => l.LineNo))}: hạn mức {Qty(m.QuantityLimit.Value)} theo {m.No}, đã đặt {Qty(before)}, đơn này {Qty(current)}");
        }

        if (errors.Count > 0)
            throw new BusinessRuleException($"Đơn vượt hạn mức tháng của phiếu duyệt NCC và giá tháng – {string.Join("; ", errors)}. Giảm số lượng hoặc đề nghị Ban điều chỉnh phiếu tháng.");
    }

    /// <summary>Số lượng trong câu báo lỗi kiểu Việt Nam, tối đa 4 số lẻ (ví dụ 1.234,5).</summary>
    private static string Qty(decimal value) =>
        Math.Round(value, 4, MidpointRounding.AwayFromZero).ToString("#,##0.####", System.Globalization.CultureInfo.GetCultureInfo("vi-VN"));

    /// <summary>Hợp đồng nguyên tắc dùng làm nguồn giá; DepartmentIds rỗng = mọi bộ phận được đặt.</summary>
    private sealed record EffectiveContract(Guid Id, string No, Guid PriceListId, IReadOnlyList<Guid> DepartmentIds)
    {
        /// <summary>Bộ phận được đặt theo hợp đồng: hợp đồng không giới hạn, hoặc bộ phận có trong danh sách.</summary>
        public bool Allows(Guid? departmentId) => DepartmentIds.Count == 0 || (departmentId is { } d && DepartmentIds.Contains(d));
    }

    /// <summary>
    /// Ghi chú khi hợp đồng hiệu lực với NCC không cho bộ phận của PO đặt hàng (design.md mục 18.10), ví dụ "Hợp đồng CTR-… chỉ cho bộ
    /// phận PX1 đặt hàng – đơn của bộ phận PX2 không tính giá theo hợp đồng."; null nếu không vướng.
    /// </summary>
    private async Task<string?> ContractScopeNoteAsync(PurchaseOrder order, string? departmentCode, CancellationToken cancellationToken)
    {
        if (await EffectiveContractAsync(order.SupplierId, order.Date, cancellationToken) is not { } contract || contract.Allows(order.DepartmentId))
            return null;
        var allowed = await _db.Departments.AsNoTracking().Where(d => contract.DepartmentIds.Contains(d.Id)).Select(d => d.Code)
            .OrderBy(x => x).ToListAsync(cancellationToken);
        var who = departmentCode is null ? "đơn chưa chọn bộ phận đặt hàng" : $"đơn của bộ phận {departmentCode}";
        return $"Hợp đồng {contract.No} chỉ cho bộ phận {string.Join(", ", allowed)} đặt hàng – {who} không tính giá theo hợp đồng.";
    }

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
                new PurchaseOrderLineData(
                    l.Id, l.ItemId, l.UomId, l.Qty, l.UnitPrice, l.DiscountPct, l.VatCode, l.DeliveryDate, l.Note, l.SourcePrLineId,
                    l.PriceDeviationReason, l.SourceRfqLineId),
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
        var docsCompletedBy = order.DocsCompletedBy is { } completedBy
            ? await _db.Users.AsNoTracking().Where(u => u.Id == completedBy).Select(u => u.FullName).SingleOrDefaultAsync(cancellationToken)
            : null;
        // Số bảng giá/hợp đồng/RFQ nguồn để hiện trên dòng. Bảng giá toàn tập đoàn của công ty khác: bỏ filter công ty, chỉ lấy đúng Id
        // đã ghi trên dòng của PO người dùng xem được (CLAUDE.md mục 4).
        var priceListIds = order.Lines.Select(l => l.PriceListId).OfType<Guid>().Distinct().ToList();
        var priceListNos = await _db.SupplierPriceLists.IgnoreQueryFilters([ProcDbContext.CompanyFilter]).AsNoTracking()
            .Where(p => priceListIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.No, cancellationToken);
        var contractIds = order.Lines.Select(l => l.ContractId).OfType<Guid>().Distinct().ToList();
        var contractNos = await _db.PurchaseContracts.AsNoTracking().Where(c => contractIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.No, cancellationToken);
        var rfqLineIds = order.Lines.Select(l => l.SourceRfqLineId).OfType<Guid>().Distinct().ToList();
        var rfqNos = (await _awards.GetAsync(order.SupplierId, rfqLineIds, cancellationToken)).ToDictionary(a => a.RfqLineId, a => a.RfqNo);
        var soleSourceIds = order.Lines.Select(l => l.SoleSourceApprovalId).OfType<Guid>().Distinct().ToList();
        var soleSourceNos = await _db.SoleSourceApprovals.AsNoTracking().Where(x => soleSourceIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.No, cancellationToken);
        // Số phiếu duyệt tháng phủ dòng: phiếu thuộc tổng công ty, chỉ lấy đúng Id đã ghi trên dòng của PO người dùng xem được.
        var monthlyLineIds = order.Lines.Select(l => l.MonthlyApprovalLineId).OfType<Guid>().Distinct().ToList();
        var monthlyLines = await (
                from l in _db.Set<MonthlySupplierApprovalLine>().AsNoTracking()
                join a in _db.MonthlySupplierApprovals.IgnoreQueryFilters([ProcDbContext.CompanyFilter, ProcDbContext.ItemCategoryFilter]) on l.ApprovalId equals a.Id
                where monthlyLineIds.Contains(l.Id)
                select new { l.Id, a.No, l.CeilingPrice, l.UomFactor })
            .ToDictionaryAsync(x => x.Id, cancellationToken);
        var monthlyNos = monthlyLines.ToDictionary(x => x.Key, x => x.Value.No);
        // Giá trần của phiếu tháng quy về ĐVT dòng PO: trần ÷ hệ số ĐVT phiếu × hệ số ĐVT dòng (6 số lẻ), ví dụ trần 126.000/thùng 12 cái → 10.500/cái.
        // Phiếu tháng tính bằng VND nên chỉ so khi PO bằng VND.
        decimal? CeilingOf(PurchaseOrderLine l) =>
            order.CurrencyCode == "VND" && l.MonthlyApprovalLineId is { } id && monthlyLines.GetValueOrDefault(id) is { CeilingPrice: { } ceiling } m
                ? Math.Round(ceiling / m.UomFactor * l.UomFactor, 6, MidpointRounding.AwayFromZero)
                : null;
        // Vượt trần: giá ròng (sau chiết khấu, trước VAT) > trần – chỉ cảnh báo, không chặn gửi duyệt, mọi mặt hàng (người dùng chốt 06/11/2026).
        bool IsOverCeiling(PurchaseOrderLine l) => CeilingOf(l) is { } c && l.NetUnitPrice > c;
        var department = order.DepartmentId is { } departmentId
            ? await _db.Departments.AsNoTracking().Where(d => d.Id == departmentId).Select(d => new { d.Code, d.Name }).SingleOrDefaultAsync(cancellationToken)
            : null;
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
                l.Note, l.SourcePrLineId, l.SourcePrLineId is { } p ? prNos.GetValueOrDefault(p) : null, receivedQty, Math.Max(0, l.Qty - receivedQty),
                l.PriceSource.ToString(), l.ApprovedUnitPrice, l.ApprovedDiscountPct, l.PriceListId is { } pl ? priceListNos.GetValueOrDefault(pl) : null,
                l.ContractId is { } c ? contractNos.GetValueOrDefault(c) : null, l.SourceRfqLineId is { } rl ? rfqNos.GetValueOrDefault(rl) : null,
                l.SourceRfqLineId, l.PriceDeviationReason, l.IsPriceDeviation, l.IsOutOfContract,
                l.SoleSourceApprovalId is { } ss ? soleSourceNos.GetValueOrDefault(ss) : null,
                l.MonthlyApprovalLineId is { } ml ? monthlyNos.GetValueOrDefault(ml) : null, l.IsOffMonthlyList, CeilingOf(l), IsOverCeiling(l));
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
            readOnly,
            order.Status == ApprovalDocumentStatus.Approved && readOnly is null && anyReceived);

        return new PurchaseOrderDto(
            order.Id, order.No, order.Date, order.Status.ToString(), order.SupplierId, supplier.Code, supplier.Name, order.WarehouseId, warehouse,
            order.ExpectedDate, order.PaymentTerms, order.DeliveryTerms, order.DeliveryAddress, order.CurrencyCode, order.ReceiveTolerancePct, order.Note,
            order.GrossAmount, order.TotalDiscount, order.TotalAmount, order.TotalVat, order.RejectReason, order.CreatedAt, createdBy, order.ApprovedAt,
            lines, await _approvals.HistoryAsync(DocumentTypes.PurchaseOrder, order.Id, cancellationToken, order), actions, order.HasPriceException,
            order.IsOutOfContract, order.DepartmentId, department is null ? null : $"{department.Code} – {department.Name}",
            await ContractScopeNoteAsync(order, department?.Code, cancellationToken), order.PurchaseType.ToString(), order.EmergencyReason,
            order.DocsDueDate, order.DocsCompletedAt, docsCompletedBy, order.DocsCompletionNote, order.IsDocsOverdue(_clock.TodayInVietnam()),
            order.PurchaseType == PurchaseType.Emergency && order.DocsCompletedAt is null && readOnly is null
            && order.Status is ApprovalDocumentStatus.Approved or ApprovalDocumentStatus.Closed, BudgetService.ToDto(order), order.IsOffApprovedList,
            order.Lines.Any(IsOverCeiling)) { Version = order.Version, ClosedReason = order.ClosedReason, ClosedAt = order.ClosedAt, ClosedByName = order.ClosedBy is { } closedBy ? await _db.Users.AsNoTracking().Where(u => u.Id == closedBy).Select(u => u.FullName).SingleOrDefaultAsync(cancellationToken) : null };
    }

    private static PurchaseOrderHeader ToHeader(SavePurchaseOrderRequest r, Guid? departmentId) =>
        new(r.Date, r.SupplierId, r.WarehouseId, r.ExpectedDate, r.PaymentTerms, r.DeliveryTerms, r.DeliveryAddress, r.ReceiveTolerancePct, r.Note,
            departmentId, ParsePurchaseType(r.PurchaseType), r.EmergencyReason, r.DocsDueDate);

    /// <summary>Loại mua gửi lên: trống = mua thường.</summary>
    /// <exception cref="BusinessRuleException">Khi không phải Normal/Emergency.</exception>
    private static PurchaseType ParsePurchaseType(string? value) =>
        string.IsNullOrWhiteSpace(value) ? PurchaseType.Normal
        : Enum.TryParse<PurchaseType>(value, true, out var type) && Enum.IsDefined(type) ? type
        : throw new BusinessRuleException("Loại mua phải là Normal (thường) hoặc Emergency (khẩn cấp).");
}
