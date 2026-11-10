using Microsoft.EntityFrameworkCore;
using Proc.Application.Common;
using Proc.Application.Documents;
using Proc.Application.Purchasing;
using Proc.Domain.Common;
using Proc.Domain.Documents;
using Proc.Domain.Organization;
using Proc.Domain.Pricing;
using Proc.Domain.Purchasing;
using Proc.Infrastructure.Organization;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.Purchasing;

/// <summary>
/// Phiếu đặt hàng gộp (design.md mục 18.25, lát G7). Phiếu thuộc tổng công ty: lập, sửa, hủy, tạo PO khi đang làm việc ở tổng công ty.
/// Đọc dữ liệu công ty thành viên bỏ filter công ty, có giới hạn (ghi lý do ở từng chỗ): kho/bộ phận và kế hoạch nhu cầu tháng của các công
/// ty áp dụng phiếu GTH, số và trạng thái PO do chính phiếu sinh ra. Ghi PO cho công ty thành viên cần quyền đặc biệt "Tạo PO cho công ty
/// thành viên" ở tổng công ty (G7c), chạy thay mặt tổng công ty (<see cref="ICompanyScopeRunner"/>) qua chính
/// <see cref="IPurchaseOrderService"/> – không lập PO bằng đường riêng.
/// </summary>
public sealed class PooledOrderService : IPooledOrderService
{
    private readonly ProcDbContext _db;
    private readonly ICurrentUserScope _scope;
    private readonly IDocumentNumberService _numbers;
    private readonly IClock _clock;
    private readonly IPurchaseOrderService _orders;
    private readonly ICompanyScopeRunner _runner;

    /// <summary>Tạo dịch vụ.</summary>
    /// <param name="db">DbContext.</param>
    /// <param name="scope">Phạm vi người dùng.</param>
    /// <param name="numbers">Đánh số chứng từ.</param>
    /// <param name="clock">Đồng hồ.</param>
    /// <param name="orders">Dịch vụ PO (lập PO tại từng công ty).</param>
    /// <param name="runner">Chạy thao tác dưới phạm vi công ty khác.</param>
    public PooledOrderService(
        ProcDbContext db, ICurrentUserScope scope, IDocumentNumberService numbers, IClock clock, IPurchaseOrderService orders, ICompanyScopeRunner runner)
    {
        _db = db;
        _scope = scope;
        _numbers = numbers;
        _clock = clock;
        _orders = orders;
        _runner = runner;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PooledOrderListItemDto>> ListAsync(CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        var rows = await (
                from x in _db.PooledOrders.AsNoTracking()
                join a in _db.MonthlySupplierApprovals on x.MonthlyApprovalId equals a.Id
                join s in _db.Suppliers on x.SupplierId equals s.Id
                orderby x.OrderDate descending, x.No descending
                select new
                {
                    x.Id, x.No, x.OrderDate, ApprovalNo = a.No, SupplierCode = s.Code, SupplierName = s.Name, Lines = x.Lines.Count,
                    Amount = x.Lines.Sum(l => l.Allocations.Sum(q => q.Qty) * l.UnitPrice),
                    Orders = x.Companies.Count(c => c.PurchaseOrderId != null), x.Status, x.CreatedBy,
                })
            .Take(2000)
            .ToListAsync(cancellationToken);
        var users = await UserNamesAsync(rows.Select(r => r.CreatedBy), cancellationToken);
        return rows.Select(r => new PooledOrderListItemDto(
                r.Id, r.No, r.OrderDate, r.ApprovalNo, r.SupplierCode, r.SupplierName, r.Lines, Money(r.Amount), r.Orders, r.Status.ToString(),
                r.CreatedBy is { } u ? users.GetValueOrDefault(u) : null))
            .ToList();
    }

    /// <inheritdoc />
    public async Task<PooledOrderDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        var order = await _db.PooledOrders.AsNoTracking()
                        .Include(x => x.Companies).Include(x => x.Lines).ThenInclude(l => l.Allocations)
                        .SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
                    ?? throw new NotFoundException("Không tìm thấy phiếu đặt hàng gộp.");
        return await ToDtoAsync(order, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PooledApprovalOptionDto>> ApprovalsAsync(DateOnly date, CancellationToken cancellationToken)
    {
        await RequireHeadOfficeAsync(cancellationToken);
        var period = new DateOnly(date.Year, date.Month, 1);
        // Phiếu GTH thuộc tổng công ty (filter công ty, nhóm hàng giữ nguyên) – chỉ phiếu Đã duyệt của tháng là kết quả Ban giao để đặt hàng.
        var approvals = await _db.MonthlySupplierApprovals.AsNoTracking().Include(a => a.Lines).Include(a => a.Companies)
            .Where(a => a.Period == period && a.Status == MonthlyApprovalStatus.Approved)
            .OrderBy(a => a.No)
            .ToListAsync(cancellationToken);
        var categoryIds = approvals.Select(a => a.ItemCategoryId).Distinct().ToList();
        var categories = await _db.ItemCategories.AsNoTracking().Where(c => categoryIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => new { c.Code, c.Name }, cancellationToken);
        var supplierIds = approvals.SelectMany(a => a.RelatedSupplierIds).Distinct().ToList();
        var suppliers = await _db.Suppliers.AsNoTracking().Where(s => supplierIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => new { s.Code, s.Name }, cancellationToken);
        var companies = await CompaniesAsync(approvals.SelectMany(a => a.Companies.Select(c => c.CompanyId)), cancellationToken);
        return approvals.Select(a => new PooledApprovalOptionDto(
                a.Id, a.No, a.Period, categories.GetValueOrDefault(a.ItemCategoryId)?.Code ?? "", categories.GetValueOrDefault(a.ItemCategoryId)?.Name ?? "",
                a.RelatedSupplierIds.Where(suppliers.ContainsKey).Select(s => new PooledSupplierOptionDto(s, suppliers[s].Code, suppliers[s].Name))
                    .OrderBy(s => s.Code).ToList(),
                a.Companies.Select(c => companies.GetValueOrDefault(c.CompanyId)).OfType<CompanyInfo>()
                    .Select(c => new PooledCompanyOptionDto(c.Id, c.Code, c.Name)).OrderBy(c => c.Code).ToList()))
            .ToList();
    }

    /// <inheritdoc />
    public async Task<PooledOrderContextDto> ContextAsync(Guid approvalId, Guid supplierId, CancellationToken cancellationToken)
    {
        await RequireHeadOfficeAsync(cancellationToken);
        var approval = await LoadApprovalAsync(approvalId, cancellationToken);
        var companyIds = approval.Companies.Select(c => c.CompanyId).ToList();
        var lines = approval.Lines.Where(l => PriceFor(l, supplierId) is not null).OrderBy(l => l.LineNo).ToList();
        var itemIds = lines.Select(l => l.ItemId).Distinct().ToList();
        var items = await _db.Items.AsNoTracking().Where(i => itemIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, i => new { i.Code, i.Name }, cancellationToken);
        var uomIds = lines.Select(l => l.UomId).Distinct().ToList();
        var uoms = await _db.Uoms.AsNoTracking().Where(u => uomIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Code, cancellationToken);
        var plans = await PlansAsync(approval.Period, companyIds, itemIds, cancellationToken);
        var sourceLines = lines.Where(l => items.ContainsKey(l.ItemId)).Select(l => new PooledSourceLineDto(
                l.Id, l.ItemId, items[l.ItemId].Code, items[l.ItemId].Name, l.UomId, uoms.GetValueOrDefault(l.UomId) ?? "", PriceFor(l, supplierId)!.Value,
                l.MainSupplierId != supplierId, l.QuantityLimit,
                companyIds.Select(c => new PooledPlanDto(c, PlanIn(plans, c, l.ItemId, l.UomFactor))).ToList()))
            .ToList();
        return new PooledOrderContextDto(await MarginPercentAsync(cancellationToken), await CompanyContextAsync(companyIds, cancellationToken), sourceLines);
    }

    /// <inheritdoc />
    public async Task<PooledOrderDto> CreateAsync(SavePooledOrderRequest request, CancellationToken cancellationToken)
    {
        var companyId = await RequireHeadOfficeAsync(cancellationToken);
        var (header, companies, lines) = await ResolveAsync(request, cancellationToken);
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var no = await _numbers.NextAsync(DocumentTypes.PooledOrder, header.OrderDate, cancellationToken);
        var order = PooledOrder.Create(companyId, no, header);
        order.SetCompanies(companies);
        order.SetLines(lines);
        _db.PooledOrders.Add(order);
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetAsync(order.Id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PooledOrderDto> UpdateAsync(Guid id, SavePooledOrderRequest request, CancellationToken cancellationToken)
    {
        var order = await FindOwnAsync(id, cancellationToken);
        var (header, companies, lines) = await ResolveAsync(request, cancellationToken);
        order.Update(header);
        order.SetCompanies(companies);
        order.SetLines(lines);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PooledOrderDto> CancelAsync(Guid id, CancelPooledOrderRequest request, CancellationToken cancellationToken)
    {
        var order = await FindOwnAsync(id, cancellationToken);
        order.Cancel(_clock.TodayInVietnam(), request.Reason);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PooledOrderDto> CreateOrdersAsync(Guid id, CancellationToken cancellationToken)
    {
        await RequireHeadOfficeAsync(cancellationToken);
        var userId = _scope.UserId ?? throw new ForbiddenAccessException("Chưa đăng nhập.");
        // Một giao dịch cho phiếu và mọi PO: một công ty lỗi thì không PO nào được tạo, phiếu vẫn nháp. Khóa phiếu để hai người bấm "Tạo PO"
        // cùng lúc không sinh hai bộ PO (lỗi L1 – mục 18.26).
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        await RowLocks.LockAsync<PooledOrder>(_db, [id], cancellationToken);
        var order = await FindOwnAsync(id, cancellationToken);
        if (order.Status != PooledOrderStatus.Draft) throw new BusinessRuleException($"Phiếu {order.No} không ở trạng thái nháp.");

        // Phiếu GTH có thể đã được thay bằng phiếu điều chỉnh từ lúc lưu nháp – PO phải theo kết quả đang hiệu lực.
        var approval = await LoadApprovalAsync(order.MonthlyApprovalId, cancellationToken);
        if (approval.Period != new DateOnly(order.OrderDate.Year, order.OrderDate.Month, 1))
            throw new BusinessRuleException("Ngày đặt không thuộc tháng của phiếu duyệt NCC và giá tháng.");
        foreach (var line in order.Lines)
        {
            var source = approval.Lines.FirstOrDefault(l => l.Id == line.MonthlyApprovalLineId);
            if (source is null || PriceFor(source, order.SupplierId) != line.UnitPrice)
                throw new BusinessRuleException($"Dòng {line.LineNo}: giá hoặc NCC trên phiếu duyệt tháng đã khác lúc lập phiếu – mở phiếu, lưu lại rồi tạo PO.");
        }

        var companyIds = order.Companies.Select(c => c.CompanyId).ToList();
        var companies = await CompaniesAsync(companyIds, cancellationToken);
        var plans = await PlansAsync(approval.Period, companyIds, order.Lines.Select(l => l.ItemId).Distinct().ToList(), cancellationToken);
        var margin = await MarginPercentAsync(cancellationToken);
        order.EnsureWithinMargin(
            line => companyIds.Select(c => new CompanyPlan(c, PlanIn(plans, c, line.ItemId, line.UomFactor))).ToList(), margin,
            c => companies.GetValueOrDefault(c)?.Code ?? "?");

        // Kiểm tra quyền trước khi tạo PO nào (design.md mục 18.25, G7c): phần của tổng công ty cần quyền tạo PO ở tổng công ty; phần của công
        // ty thành viên cần quyền đặc biệt "Tạo PO cho công ty thành viên".
        var blocked = CreateOrdersBlockedReason(
            order.Companies.Where(c => order.Lines.Any(l => l.QtyFor(c.CompanyId) > 0)).Select(c => c.CompanyId).ToList(),
            c => companies.GetValueOrDefault(c)?.Code ?? "?");
        if (blocked is not null) throw new ForbiddenAccessException(blocked);

        var itemIds = order.Lines.Select(l => l.ItemId).Distinct().ToList();
        var vats = await _db.Items.AsNoTracking().Where(i => itemIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, i => i.VatCode, cancellationToken);
        var created = new Dictionary<Guid, Guid>();
        foreach (var company in order.Companies.OrderBy(c => companies.GetValueOrDefault(c.CompanyId)?.Code))
        {
            var lines = order.Lines.OrderBy(l => l.LineNo).Where(l => l.QtyFor(company.CompanyId) > 0)
                .Select(l => new SavePurchaseOrderLineRequest(
                    null, l.ItemId, l.UomId, l.QtyFor(company.CompanyId), l.UnitPrice, 0m, vats.GetValueOrDefault(l.ItemId), order.ExpectedDate, l.Note, null))
                .ToList();
            if (lines.Count == 0) continue;
            var request = new SavePurchaseOrderRequest(
                order.OrderDate, order.SupplierId, company.WarehouseId, order.ExpectedDate, null, null, null, 0m,
                order.Note is null ? $"Đặt hàng gộp theo phiếu {order.No}." : $"Đặt hàng gộp theo phiếu {order.No}. {order.Note}",
                lines, company.DepartmentId);
            var code = companies.GetValueOrDefault(company.CompanyId)?.Code ?? "?";
            created[company.CompanyId] = await CreateOrderAtAsync(company.CompanyId, code, request, cancellationToken);
        }

        order.MarkOrdered(_clock.UtcNow, userId, margin, created);
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <summary>
    /// Lý do không tạo PO được theo quyền của người dùng ở tổng công ty (design.md mục 18.25, G7c), null nếu được.
    /// </summary>
    /// <param name="companyIds">Công ty nhận có SL trên phiếu.</param>
    /// <param name="codeOf">Mã công ty để báo lỗi.</param>
    /// <returns>Lý do hoặc null.</returns>
    private string? CreateOrdersBlockedReason(IReadOnlyCollection<Guid> companyIds, Func<Guid, string> codeOf)
    {
        if (companyIds.Any(c => c == _scope.CompanyId) && !_scope.HasPermission(Permissions.Purchase.PoCreate))
            return "Bạn không có quyền tạo đơn mua hàng tại tổng công ty.";
        var members = companyIds.Where(c => c != _scope.CompanyId).Select(codeOf).Order(StringComparer.Ordinal).ToList();
        return members.Count > 0 && !_scope.HasPermission(Permissions.Purchase.PooledOrderCreateMemberPo)
            ? $"Công ty {string.Join(", ", members)}: tạo PO cho công ty thành viên cần quyền đặc biệt \"Tạo PO cho công ty thành viên\" tại tổng công ty – liên hệ quản trị để được cấp."
            : null;
    }

    /// <summary>
    /// Lập PO nháp cho một công ty nhận hàng qua chính <see cref="IPurchaseOrderService"/>: công ty đang làm việc (tổng công ty) dùng phạm vi
    /// hiện tại; công ty thành viên chạy thay mặt tổng công ty (<see cref="ICompanyScopeRunner"/>) – quyền đặc biệt đã kiểm tra ở
    /// <see cref="CreateOrdersAsync"/>. Kho, NCC áp dụng, ngân sách, giá theo phiếu tháng đều kiểm tra theo công ty nhận như lập tay. Lỗi báo
    /// kèm mã công ty.
    /// </summary>
    private async Task<Guid> CreateOrderAtAsync(Guid companyId, string companyCode, SavePurchaseOrderRequest request, CancellationToken cancellationToken)
    {
        try
        {
            if (companyId == _scope.CompanyId) return (await _orders.CreateAsync(request, cancellationToken)).Id;
            return await _runner.RunOnBehalfAsync(companyId, [Permissions.Purchase.PoView, Permissions.Purchase.PoCreate],
                async () => (await _orders.CreateAsync(request, cancellationToken)).Id, cancellationToken);
        }
        catch (BusinessRuleException ex)
        {
            throw new BusinessRuleException($"Công ty {companyCode}: {ex.Message}");
        }
        catch (ForbiddenAccessException ex)
        {
            throw new ForbiddenAccessException($"Công ty {companyCode}: {ex.Message}");
        }
        catch (NotFoundException ex)
        {
            throw new BusinessRuleException($"Công ty {companyCode}: {ex.Message}");
        }
    }

    /// <summary>
    /// Kiểm tra và dựng dữ liệu phiếu: phiếu GTH Đã duyệt của tháng chứa ngày đặt; NCC là NCC chính/dự phòng của mỗi dòng (giá theo NCC đó);
    /// công ty thuộc công ty áp dụng của phiếu GTH; kho, bộ phận thuộc đúng công ty và đang dùng.
    /// </summary>
    private async Task<(PooledOrderHeader, List<PooledOrderCompanyData>, List<(PooledOrderLineData, PooledOrderLineSource)>)> ResolveAsync(
        SavePooledOrderRequest request, CancellationToken cancellationToken)
    {
        var approval = await LoadApprovalAsync(request.MonthlyApprovalId, cancellationToken);
        if (approval.Period != new DateOnly(request.OrderDate.Year, request.OrderDate.Month, 1))
            throw new BusinessRuleException($"Ngày đặt phải thuộc tháng {approval.Period:MM/yyyy} của phiếu {approval.No}.");
        if (!approval.RelatedSupplierIds.Contains(request.SupplierId))
            throw new BusinessRuleException($"NCC không phải NCC chính hay dự phòng trên phiếu {approval.No} – Phòng cung ứng không tự chọn NCC.");

        var allowed = approval.Companies.Select(c => c.CompanyId).ToHashSet();
        if (request.Companies.Any(c => !allowed.Contains(c.CompanyId)))
            throw new BusinessRuleException($"Có công ty không thuộc các công ty áp dụng phiếu {approval.No}.");
        var companyIds = request.Companies.Select(c => c.CompanyId).ToList();
        // Kho, bộ phận của công ty thành viên: bỏ filter công ty và phạm vi kho vì đang ở tổng công ty; chỉ kiểm tra thuộc đúng công ty và
        // đang dùng. Quyền với kho đó kiểm tra lại khi tạo PO dưới phạm vi công ty kia.
        var warehouseIds = request.Companies.Select(c => c.WarehouseId).ToList();
        var warehouses = await _db.Warehouses.IgnoreQueryFilters().AsNoTracking()
            .Where(w => warehouseIds.Contains(w.Id) && w.IsActive).ToDictionaryAsync(w => w.Id, w => w.CompanyId, cancellationToken);
        var departmentIds = request.Companies.Select(c => c.DepartmentId).OfType<Guid>().ToList();
        var departments = await _db.Departments.IgnoreQueryFilters().AsNoTracking()
            .Where(d => departmentIds.Contains(d.Id) && d.IsActive).ToDictionaryAsync(d => d.Id, d => d.CompanyId, cancellationToken);
        var names = await CompaniesAsync(companyIds, cancellationToken);
        foreach (var c in request.Companies)
        {
            var code = names.GetValueOrDefault(c.CompanyId)?.Code ?? "?";
            if (!warehouses.TryGetValue(c.WarehouseId, out var wc) || wc != c.CompanyId)
                throw new BusinessRuleException($"Công ty {code}: kho nhận không thuộc công ty hoặc đã ngừng dùng.");
            if (c.DepartmentId is { } d && (!departments.TryGetValue(d, out var dc) || dc != c.CompanyId))
                throw new BusinessRuleException($"Công ty {code}: bộ phận không thuộc công ty hoặc đã ngừng dùng.");
        }

        var itemIds = approval.Lines.Select(l => l.ItemId).Distinct().ToList();
        var items = await _db.Items.AsNoTracking().Where(i => itemIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, i => i.Code, cancellationToken);
        var lines = request.Lines.Select((l, index) =>
        {
            var source = approval.Lines.FirstOrDefault(x => x.Id == l.MonthlyApprovalLineId)
                         ?? throw new BusinessRuleException($"Dòng {index + 1}: mặt hàng không có trên phiếu {approval.No}.");
            var code = items.GetValueOrDefault(source.ItemId)
                       ?? throw new ForbiddenAccessException($"Dòng {index + 1}: mặt hàng ngoài phạm vi nhóm hàng của bạn.");
            var price = PriceFor(source, request.SupplierId)
                        ?? throw new BusinessRuleException($"Dòng {index + 1} ({code}): NCC không phải NCC chính/dự phòng của mặt hàng trên phiếu {approval.No}.");
            return (new PooledOrderLineData(l.Id, l.MonthlyApprovalLineId, l.Allocations.Select(a => new PooledAllocationData(a.CompanyId, a.Qty)).ToList(),
                    l.AllocationReason, l.Note),
                new PooledOrderLineSource(source.Id, source.ItemId, code, source.UomId, source.UomFactor, price));
        }).ToList();
        var header = new PooledOrderHeader(request.OrderDate, approval.Id, approval.ItemCategoryId, request.SupplierId, request.ExpectedDate, request.Note);
        return (header, request.Companies.Select(c => new PooledOrderCompanyData(c.CompanyId, c.WarehouseId, c.DepartmentId)).ToList(), lines);
    }

    /// <summary>Phiếu GTH Đã duyệt (thuộc tổng công ty – filter công ty và nhóm hàng giữ nguyên).</summary>
    private async Task<MonthlySupplierApproval> LoadApprovalAsync(Guid approvalId, CancellationToken cancellationToken)
    {
        var approval = await _db.MonthlySupplierApprovals.AsNoTracking().Include(a => a.Lines).Include(a => a.Companies)
                           .SingleOrDefaultAsync(a => a.Id == approvalId, cancellationToken)
                       ?? throw new NotFoundException("Không tìm thấy phiếu duyệt NCC và giá tháng (hoặc ngoài phạm vi nhóm hàng của bạn).");
        if (approval.Status != MonthlyApprovalStatus.Approved)
            throw new BusinessRuleException($"Phiếu {approval.No} không ở trạng thái Đã duyệt (có thể đã được thay bằng phiếu điều chỉnh) – chọn phiếu đang hiệu lực.");
        return approval;
    }

    /// <summary>Giá Ban duyệt cho NCC trên dòng GTH: NCC chính → giá chính, NCC dự phòng → giá dự phòng; NCC khác → null.</summary>
    private static decimal? PriceFor(MonthlySupplierApprovalLine line, Guid supplierId) =>
        supplierId == line.MainSupplierId ? line.UnitPrice : supplierId == line.BackupSupplierId ? line.BackupUnitPrice : null;

    /// <summary>
    /// Kế hoạch nhu cầu tháng (SL cơ sở) theo công ty × mặt hàng: phiếu KHN Đã chốt của năm, cột tháng của kỳ, × hệ số ĐVT dòng KHN. Bỏ filter
    /// công ty và nhóm hàng vì đây là căn cứ phân bổ của Phòng cung ứng chung (quy tắc phân bổ của Ban) – chỉ đọc các công ty áp dụng phiếu
    /// GTH và mặt hàng của phiếu, chỉ lấy số tổng. Mỗi mặt hàng × năm × công ty chỉ một phiếu còn hiệu lực (G5a) nên không đếm trùng.
    /// </summary>
    private async Task<Dictionary<(Guid CompanyId, Guid ItemId), decimal>> PlansAsync(
        DateOnly period, IReadOnlyCollection<Guid> companyIds, IReadOnlyCollection<Guid> itemIds, CancellationToken cancellationToken)
    {
        var rows = await (
                from p in _db.DemandPlans.IgnoreQueryFilters([ProcDbContext.CompanyFilter, ProcDbContext.ItemCategoryFilter]).AsNoTracking()
                where p.Year == period.Year && p.Status == DemandPlanStatus.Confirmed && companyIds.Contains(p.CompanyId)
                from l in p.Lines
                where itemIds.Contains(l.ItemId)
                select new { p.CompanyId, Line = l })
            .ToListAsync(cancellationToken);
        return rows.GroupBy(r => (r.CompanyId, r.Line.ItemId))
            .ToDictionary(g => g.Key, g => g.Sum(r => r.Line.Months[period.Month - 1] * r.Line.UomFactor));
    }

    /// <summary>Kế hoạch quy về ĐVT dòng = SL cơ sở ÷ hệ số ĐVT dòng, 4 số lẻ. Ví dụ 120 cái, dòng theo thùng 12 cái → 10 thùng.</summary>
    private static decimal PlanIn(Dictionary<(Guid CompanyId, Guid ItemId), decimal> plans, Guid companyId, Guid itemId, decimal uomFactor) =>
        plans.TryGetValue((companyId, itemId), out var baseQty) ? Math.Round(baseQty / uomFactor, 4, MidpointRounding.AwayFromZero) : 0m;

    /// <summary>% biên độ phân bổ – tham số của tổng công ty (công ty đang làm việc).</summary>
    private async Task<decimal> MarginPercentAsync(CancellationToken cancellationToken) =>
        await _db.CompanyParameters.AsNoTracking().Select(p => (decimal?)p.PooledMarginPercent).FirstOrDefaultAsync(cancellationToken)
        ?? CompanyParameter.DefaultPooledMarginPercent;

    /// <summary>Mã và tên một công ty dùng trong phiếu đặt gộp.</summary>
    private sealed record CompanyInfo(Guid Id, string Code, string Name);

    /// <summary>Lấy mã và tên các công ty theo danh sách Id.</summary>
    private async Task<Dictionary<Guid, CompanyInfo>> CompaniesAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken)
    {
        var list = ids.Distinct().ToList();
        return await _db.Companies.AsNoTracking().Where(c => list.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => new CompanyInfo(c.Id, c.Code, c.Name), cancellationToken);
    }

    /// <summary>Kho đang dùng (kho Main đầu tiên) và bộ phận đang dùng của các công ty áp dụng – xem lý do bỏ filter ở <see cref="ResolveAsync"/>.</summary>
    private async Task<IReadOnlyList<PooledCompanyContextDto>> CompanyContextAsync(IReadOnlyCollection<Guid> companyIds, CancellationToken cancellationToken)
    {
        var companies = await CompaniesAsync(companyIds, cancellationToken);
        var warehouses = await _db.Warehouses.IgnoreQueryFilters().AsNoTracking()
            .Where(w => companyIds.Contains(w.CompanyId) && w.IsActive)
            .Select(w => new { w.CompanyId, w.Id, w.Code, w.Name, w.Type })
            .ToListAsync(cancellationToken);
        var departments = await _db.Departments.IgnoreQueryFilters().AsNoTracking()
            .Where(d => companyIds.Contains(d.CompanyId) && d.IsActive)
            .Select(d => new { d.CompanyId, d.Id, d.Code, d.Name })
            .ToListAsync(cancellationToken);
        return companies.Values.OrderBy(c => c.Code).Select(c => new PooledCompanyContextDto(
                c.Id, c.Code, c.Name,
                warehouses.Where(w => w.CompanyId == c.Id).OrderByDescending(w => w.Type == WarehouseType.Main).ThenBy(w => w.Code)
                    .Select(w => new PooledPlaceOptionDto(w.Id, w.Code, w.Name, w.Type == WarehouseType.Main)).ToList(),
                departments.Where(d => d.CompanyId == c.Id).OrderBy(d => d.Code)
                    .Select(d => new PooledPlaceOptionDto(d.Id, d.Code, d.Name, false)).ToList()))
            .ToList();
    }

    /// <summary>Bắt buộc đang làm việc ở tổng công ty (phòng cung ứng chung đặt hàng cho các công ty).</summary>
    private async Task<Guid> RequireHeadOfficeAsync(CancellationToken cancellationToken)
    {
        // Phòng cung ứng chung đặt hàng cho các công ty – phiếu thuộc tổng công ty (quyết định thành lập Ban, mục 18.25).
        if (!await HeadOffice.IsWorkingAtAsync(_db, _scope, cancellationToken))
            throw new ForbiddenAccessException("Phiếu đặt hàng gộp chỉ lập khi đang làm việc ở tổng công ty.");
        return _scope.RequireCompany();
    }

    /// <summary>Tìm phiếu đặt gộp của tổng công ty kèm công ty nhận và dòng phân bổ; không thấy thì báo lỗi 404.</summary>
    private async Task<PooledOrder> FindOwnAsync(Guid id, CancellationToken cancellationToken)
    {
        await RequireHeadOfficeAsync(cancellationToken);
        return await _db.PooledOrders.Include(x => x.Companies).Include(x => x.Lines).ThenInclude(l => l.Allocations)
                   .SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
               ?? throw new NotFoundException("Không tìm thấy phiếu đặt hàng gộp.");
    }

    /// <summary>Lấy họ tên theo danh sách Id người dùng (bỏ null, bỏ trùng).</summary>
    private async Task<Dictionary<Guid, string>> UserNamesAsync(IEnumerable<Guid?> ids, CancellationToken cancellationToken)
    {
        var list = ids.OfType<Guid>().Distinct().ToList();
        return await _db.Users.AsNoTracking().Where(u => list.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken);
    }

    /// <summary>Tiền VND làm tròn 2 số (numeric(19,2) – CLAUDE.md mục 4).</summary>
    private static decimal Money(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    /// <summary>Đổi phiếu đặt gộp sang DTO kèm phiếu GTH nguồn, công ty nhận, PO đã sinh và người lập.</summary>
    private async Task<PooledOrderDto> ToDtoAsync(PooledOrder x, CancellationToken cancellationToken)
    {
        var approval = await _db.MonthlySupplierApprovals.AsNoTracking().Where(a => a.Id == x.MonthlyApprovalId)
            .Select(a => new { a.No, a.Period }).SingleOrDefaultAsync(cancellationToken);
        var category = await _db.ItemCategories.AsNoTracking().Where(c => c.Id == x.ItemCategoryId).Select(c => new { c.Code, c.Name }).SingleOrDefaultAsync(cancellationToken);
        var supplier = await _db.Suppliers.AsNoTracking().Where(s => s.Id == x.SupplierId).Select(s => new { s.Code, s.Name }).SingleOrDefaultAsync(cancellationToken);
        var itemIds = x.Lines.Select(l => l.ItemId).Distinct().ToList();
        var items = await _db.Items.AsNoTracking().Where(i => itemIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, i => new { i.Code, i.Name }, cancellationToken);
        var uomIds = x.Lines.Select(l => l.UomId).Distinct().ToList();
        var uoms = await _db.Uoms.AsNoTracking().Where(u => uomIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Code, cancellationToken);
        var companyIds = x.Companies.Select(c => c.CompanyId).ToList();
        var companies = await CompaniesAsync(companyIds, cancellationToken);
        var warehouseIds = x.Companies.Select(c => c.WarehouseId).ToList();
        var warehouses = await _db.Warehouses.IgnoreQueryFilters().AsNoTracking().Where(w => warehouseIds.Contains(w.Id))
            .ToDictionaryAsync(w => w.Id, w => w.Code, cancellationToken);
        var departmentIds = x.Companies.Select(c => c.DepartmentId).OfType<Guid>().ToList();
        var departments = await _db.Departments.IgnoreQueryFilters().AsNoTracking().Where(d => departmentIds.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, d => d.Name, cancellationToken);
        // Số và trạng thái PO do chính phiếu sinh ra ở các công ty – bỏ filter công ty, chỉ đọc đúng các PO đã liên kết.
        var orderIds = x.Companies.Select(c => c.PurchaseOrderId).OfType<Guid>().ToList();
        var orders = await _db.PurchaseOrders.IgnoreQueryFilters([ProcDbContext.CompanyFilter]).AsNoTracking().Where(o => orderIds.Contains(o.Id))
            .ToDictionaryAsync(o => o.Id, o => new { o.No, o.Status }, cancellationToken);
        var users = await UserNamesAsync([x.CreatedBy, x.OrderedBy], cancellationToken);

        var draft = x.Status == PooledOrderStatus.Draft;
        var margin = x.MarginPercent ?? await MarginPercentAsync(cancellationToken);
        // Kế hoạch hiện cả khi đã tạo PO (để đối chiếu); cờ vượt biên độ chỉ tính khi còn nháp.
        var plans = approval is not null
            ? await PlansAsync(approval.Period, companyIds, itemIds, cancellationToken)
            : new Dictionary<(Guid CompanyId, Guid ItemId), decimal>();

        var headOffice = await HeadOffice.IsWorkingAtAsync(_db, _scope, cancellationToken);
        string? readOnly = !headOffice ? "Phiếu đặt hàng gộp chỉ sửa khi đang làm việc ở tổng công ty."
            : items.Count < itemIds.Count ? "Phiếu có mặt hàng ngoài phạm vi nhóm hàng của bạn nên chỉ xem được."
            : !_scope.HasPermission(Permissions.Purchase.PooledOrderManage) ? "Bạn không có quyền lập, sửa phiếu đặt hàng gộp." : null;
        var editable = readOnly is null && draft;
        var blocked = editable
            ? CreateOrdersBlockedReason(
                companyIds.Where(c => x.Lines.Any(l => l.QtyFor(c) > 0)).ToList(), c => companies.GetValueOrDefault(c)?.Code ?? "?")
            : null;
        var actions = new PooledOrderActions(editable, editable && blocked is null, editable, readOnly, blocked);

        var lines = x.Lines.OrderBy(l => l.LineNo).Select(l =>
        {
            var item = items.GetValueOrDefault(l.ItemId);
            var linePlans = companyIds.Select(c => new CompanyPlan(c, PlanIn(plans, c, l.ItemId, l.UomFactor))).ToList();
            var hasPlan = linePlans.Sum(p => p.Plan) > 0;
            var total = l.TotalQty;
            var tolerance = PooledAllocation.Tolerance(total, margin);
            var allocations = companyIds.Select(c =>
            {
                var qty = l.QtyFor(c);
                decimal? expected = hasPlan ? Math.Round(PooledAllocation.Expected(total, c, linePlans), 4, MidpointRounding.AwayFromZero) : null;
                return new PooledAllocationDto(c, qty, PlanIn(plans, c, l.ItemId, l.UomFactor), expected,
                    draft && hasPlan && Math.Abs(qty - PooledAllocation.Expected(total, c, linePlans)) > tolerance);
            }).ToList();
            return new PooledOrderLineDto(
                l.Id, l.LineNo, l.MonthlyApprovalLineId, l.ItemId, item?.Code ?? "(ngoài phạm vi)", item?.Name ?? "", l.UomId,
                uoms.GetValueOrDefault(l.UomId) ?? "", l.UnitPrice, total, Money(total * l.UnitPrice), tolerance, hasPlan, l.AllocationReason, l.Note,
                allocations);
        }).ToList();
        var companyRows = x.Companies.OrderBy(c => companies.GetValueOrDefault(c.CompanyId)?.Code).Select(c =>
        {
            var info = companies.GetValueOrDefault(c.CompanyId);
            var po = c.PurchaseOrderId is { } poId ? orders.GetValueOrDefault(poId) : null;
            return new PooledOrderCompanyDto(
                c.CompanyId, info?.Code ?? "", info?.Name ?? "", c.WarehouseId, warehouses.GetValueOrDefault(c.WarehouseId), c.DepartmentId,
                c.DepartmentId is { } d ? departments.GetValueOrDefault(d) : null,
                Money(x.Lines.Sum(l => l.QtyFor(c.CompanyId) * l.UnitPrice)), c.PurchaseOrderId, po?.No, po?.Status.ToString());
        }).ToList();
        return new PooledOrderDto(
            x.Id, x.No, x.OrderDate, x.MonthlyApprovalId, approval?.No ?? "", approval?.Period ?? default, category?.Code ?? "", category?.Name ?? "",
            x.SupplierId, supplier?.Code ?? "", supplier?.Name ?? "", x.ExpectedDate, x.Note, x.Status.ToString(), margin, x.OrderedAt,
            x.OrderedBy is { } ob ? users.GetValueOrDefault(ob) : null, x.CancelledOn, x.CancelReason, x.CreatedAt,
            x.CreatedBy is { } u ? users.GetValueOrDefault(u) : null, Money(x.Lines.Sum(l => l.TotalQty * l.UnitPrice)), companyRows, lines, actions,
            x.Version);
    }
}
