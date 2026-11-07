using Microsoft.EntityFrameworkCore;
using Proc.Application.Common;
using Proc.Application.Documents;
using Proc.Application.Purchasing;
using Proc.Domain.Common;
using Proc.Domain.Documents;
using Proc.Domain.MasterData;
using Proc.Domain.Organization;
using Proc.Domain.Purchasing;
using Proc.Infrastructure.MasterData;
using Proc.Infrastructure.Organization;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.Purchasing;

/// <summary>
/// Phiếu kế hoạch nhu cầu (design.md mục 18.25, lát G5a). Phiếu thuộc công ty đang làm việc – mỗi pháp nhân lập nhu cầu của mình; filter công
/// ty và nhóm hàng giữ nguyên ở mọi truy vấn (trừ lúc dò trùng mặt hàng, xem <see cref="CheckAsync"/>; và tổng hợp nhu cầu ở tổng công ty, xem
/// <see cref="SummaryAsync"/>).
/// </summary>
public sealed class DemandPlanService : IDemandPlanService
{
    private readonly ProcDbContext _db;
    private readonly ICurrentUserScope _scope;
    private readonly IDocumentNumberService _numbers;
    private readonly IClock _clock;

    /// <summary>Tạo dịch vụ.</summary>
    /// <param name="db">DbContext.</param>
    /// <param name="scope">Phạm vi người dùng.</param>
    /// <param name="numbers">Đánh số chứng từ.</param>
    /// <param name="clock">Đồng hồ.</param>
    public DemandPlanService(ProcDbContext db, ICurrentUserScope scope, IDocumentNumberService numbers, IClock clock)
    {
        _db = db;
        _scope = scope;
        _numbers = numbers;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DemandPlanListItemDto>> ListAsync(CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        var rows = await (
                from x in _db.DemandPlans.AsNoTracking()
                join c in _db.ItemCategories on x.ItemCategoryId equals c.Id
                join r in _db.DemandPlans on x.ReplacesId equals r.Id into rj
                from r in rj.DefaultIfEmpty()
                orderby x.Year descending, x.No descending
                select new
                {
                    x.Id, x.No, x.Year, CategoryCode = c.Code, CategoryName = c.Name, Lines = x.Lines.Count, x.Status,
                    ReplacesNo = r == null ? null : r.No, x.CreatedBy,
                })
            .Take(2000)
            .ToListAsync(cancellationToken);
        var users = await UserNamesAsync(rows.Select(r => r.CreatedBy), cancellationToken);
        return rows.Select(r => new DemandPlanListItemDto(
                r.Id, r.No, r.Year, r.CategoryCode, r.CategoryName, r.Lines, r.Status.ToString(), r.ReplacesNo,
                r.CreatedBy is { } u ? users.GetValueOrDefault(u) : null))
            .ToList();
    }

    /// <inheritdoc />
    public async Task<DemandPlanDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        var plan = await _db.DemandPlans.AsNoTracking().Include(x => x.Lines).SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
                   ?? throw new NotFoundException("Không tìm thấy phiếu kế hoạch nhu cầu.");
        return await ToDtoAsync(plan, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<DemandPlanDto> CreateAsync(SaveDemandPlanRequest request, CancellationToken cancellationToken)
    {
        var header = ToHeader(request);
        var lines = await ResolveLinesAsync(request.Lines, cancellationToken);
        var plan = await NewAsync(header, lines, null, cancellationToken);
        return await GetAsync(plan.Id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<DemandPlanDto> UpdateAsync(Guid id, SaveDemandPlanRequest request, CancellationToken cancellationToken)
    {
        var plan = await FindOwnAsync(id, cancellationToken);
        var header = ToHeader(request);
        var lines = await ResolveLinesAsync(request.Lines, cancellationToken);
        await CheckAsync(header, lines, plan.Id, plan.ReplacesId, cancellationToken);
        plan.Update(header, _clock.TodayInVietnam());
        plan.SetLines(lines);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<DemandPlanDto> ConfirmAsync(Guid id, CancellationToken cancellationToken)
    {
        var plan = await FindOwnAsync(id, cancellationToken);
        // Kiểm tra trùng lại lúc chốt: phiếu khác có thể đã thêm cùng mặt hàng sau khi phiếu này lưu nháp.
        var lines = await ResolveLinesAsync(plan.CopyLines().Select(ToRequest).ToList(), cancellationToken);
        await CheckAsync(plan.CopyHeader(), lines, plan.Id, plan.ReplacesId, cancellationToken);
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var today = _clock.TodayInVietnam();
        plan.Confirm(_clock.UtcNow, _scope.UserId ?? throw new ForbiddenAccessException("Chưa đăng nhập."), today);
        if (plan.ReplacesId is { } oldId)
        {
            // Phiếu điều chỉnh chốt xong thì phiếu cũ thôi tính vào tổng hợp – một mặt hàng × năm chỉ một số liệu.
            var old = await _db.DemandPlans.SingleAsync(x => x.Id == oldId, cancellationToken);
            if (old.Status == DemandPlanStatus.Confirmed) old.MarkReplaced();
        }

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<DemandPlanDto> AdjustAsync(Guid id, CancellationToken cancellationToken)
    {
        var old = await FindOwnAsync(id, cancellationToken);
        if (old.Status != DemandPlanStatus.Confirmed) throw new BusinessRuleException("Chỉ điều chỉnh được phiếu đã chốt.");
        var existing = await _db.DemandPlans.AsNoTracking()
            .Where(x => x.ReplacesId == id && x.Status != DemandPlanStatus.Cancelled).Select(x => x.No).FirstOrDefaultAsync(cancellationToken);
        if (existing is not null) throw new BusinessRuleException($"Phiếu {old.No} đã có phiếu điều chỉnh {existing}.");
        var lines = await ResolveLinesAsync(old.CopyLines().Select(ToRequest).ToList(), cancellationToken);
        var plan = await NewAsync(old.CopyHeader(), lines, old.Id, cancellationToken);
        return await GetAsync(plan.Id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<DemandPlanDto> CancelAsync(Guid id, CancelDemandPlanRequest request, CancellationToken cancellationToken)
    {
        var plan = await FindOwnAsync(id, cancellationToken);
        // Phiếu đã chốt đang có phiếu điều chỉnh soạn dở: hủy phiếu điều chỉnh trước, tránh phiếu điều chỉnh mồ côi chốt thay cho phiếu đã hủy.
        var pending = await _db.DemandPlans.AsNoTracking()
            .Where(x => x.ReplacesId == id && x.Status == DemandPlanStatus.Draft).Select(x => x.No).FirstOrDefaultAsync(cancellationToken);
        if (pending is not null) throw new BusinessRuleException($"Phiếu đang có phiếu điều chỉnh {pending} – hủy hoặc chốt phiếu đó trước.");
        plan.Cancel(_clock.TodayInVietnam(), request.Reason);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<DemandSummaryDto> SummaryAsync(int year, Guid? categoryId, CancellationToken cancellationToken)
    {
        var companyId = _scope.RequireCompany();
        if (year is < 2000 or > 2999) throw new BusinessRuleException("Năm kế hoạch không hợp lệ.");

        // Văn phòng Ban (tổng công ty) tổng hợp nhu cầu của các công ty để thương lượng sản lượng gộp (design.md 18.25, lát G5b) – nên ở tổng
        // công ty bỏ filter công ty; công ty thành viên chỉ thấy nhu cầu của mình. Filter nhóm hàng (phiếu, mặt hàng) giữ nguyên.
        var all = await HeadOffice.IsWorkingAtAsync(_db, _scope, cancellationToken);
        var plans = all ? _db.DemandPlans.IgnoreQueryFilters([ProcDbContext.CompanyFilter]) : _db.DemandPlans;
        var lines = await (
                from x in plans.AsNoTracking()
                where x.Year == year && (x.Status == DemandPlanStatus.Draft || x.Status == DemandPlanStatus.Confirmed)
                from l in x.Lines
                join i in _db.Items on l.ItemId equals i.Id
                select new
                {
                    PlanId = x.Id, x.CompanyId, x.No, x.Status, l.ItemId, i.CategoryId, l.UomFactor,
                    l.Qty01, l.Qty02, l.Qty03, l.Qty04, l.Qty05, l.Qty06, l.Qty07, l.Qty08, l.Qty09, l.Qty10, l.Qty11, l.Qty12,
                })
            .ToListAsync(cancellationToken);
        if (categoryId is { } c)
        {
            var parents = await _db.ItemCategories.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.ParentId, cancellationToken);
            lines = lines.Where(l => ItemAdministration.IsUnder(l.CategoryId, c, parents)).ToList();
        }

        // Phiếu nháp chưa tính vào tổng hợp (số liệu còn đang sửa) nhưng đếm để Văn phòng Ban biết công ty nào chưa chốt.
        var confirmed = lines.Where(l => l.Status == DemandPlanStatus.Confirmed).ToList();
        var companies = await _db.Companies.AsNoTracking()
            .Where(x => all ? x.IsActive : x.Id == companyId)
            .OrderByDescending(x => x.IsHeadOffice).ThenBy(x => x.Code)
            .Select(x => new { x.Id, x.Code, x.Name })
            .ToListAsync(cancellationToken);
        var extra = confirmed.Select(l => l.CompanyId).Distinct().Except(companies.Select(x => x.Id)).ToList();
        if (extra.Count > 0)
        {
            // Công ty đã ngừng hoạt động nhưng còn kế hoạch đã chốt của năm: vẫn hiện cột để tổng không lệch.
            companies.AddRange(await _db.Companies.AsNoTracking().Where(x => extra.Contains(x.Id)).OrderBy(x => x.Code)
                .Select(x => new { x.Id, x.Code, x.Name }).ToListAsync(cancellationToken));
        }

        int Count(Guid id, DemandPlanStatus status) => lines.Where(l => l.CompanyId == id && l.Status == status).Select(l => l.PlanId).Distinct().Count();
        var companyDtos = companies
            .Select(x => new DemandSummaryCompanyDto(x.Id, x.Code, x.Name, Count(x.Id, DemandPlanStatus.Confirmed), Count(x.Id, DemandPlanStatus.Draft)))
            .ToList();
        var order = companies.Select((x, index) => (x.Id, index)).ToDictionary(p => p.Id, p => p.index);

        var itemIds = confirmed.Select(l => l.ItemId).Distinct().ToList();
        var items = await (
                from i in _db.Items.AsNoTracking()
                where itemIds.Contains(i.Id)
                join u in _db.Uoms on i.BaseUomId equals u.Id
                join g in _db.ItemCategories on i.CategoryId equals g.Id
                select new { i.Id, i.Code, i.Name, Uom = u.Code, CategoryCode = g.Code, CategoryName = g.Name })
            .ToDictionaryAsync(i => i.Id, cancellationToken);

        // Quy về ĐVT cơ sở từng tháng của từng dòng rồi mới cộng: SL × hệ số, làm tròn 4 số lẻ (numeric(18,4)).
        // Ví dụ 5 thùng × 12 cái/thùng = 60 cái; 2,5 thùng × 12 = 30 cái.
        static decimal[] Add(decimal[] sum, IReadOnlyList<decimal> months)
        {
            for (var m = 0; m < 12; m++) sum[m] += months[m];
            return sum;
        }

        var rows = confirmed
            .Select(l => new
            {
                l.ItemId, l.CompanyId, l.No,
                Months = new[] { l.Qty01, l.Qty02, l.Qty03, l.Qty04, l.Qty05, l.Qty06, l.Qty07, l.Qty08, l.Qty09, l.Qty10, l.Qty11, l.Qty12 }
                    .Select(q => decimal.Round(q * l.UomFactor, 4, MidpointRounding.AwayFromZero)).ToArray(),
            })
            .GroupBy(l => l.ItemId)
            .Select(g =>
            {
                var cells = g.GroupBy(l => l.CompanyId)
                    .OrderBy(cg => order.GetValueOrDefault(cg.Key, int.MaxValue))
                    .Select(cg =>
                    {
                        var months = cg.Aggregate(new decimal[12], (sum, l) => Add(sum, l.Months));
                        return new DemandSummaryCellDto(cg.Key, months, months.Sum(), cg.Select(l => l.No).Distinct().Order().ToList());
                    })
                    .ToList();
                var total = cells.Aggregate(new decimal[12], (sum, cell) => Add(sum, cell.Months));
                var item = items[g.Key];
                return new DemandSummaryRowDto(item.Id, item.Code, item.Name, item.Uom, item.CategoryCode, item.CategoryName, cells, total, total.Sum());
            })
            .OrderBy(r => r.ItemCode, StringComparer.Ordinal)
            .ToList();
        return new DemandSummaryDto(year, all, companyDtos, rows);
    }

    /// <summary>Cấp số và lưu phiếu nháp trong một transaction (số không bị nhảy khi lưu lỗi).</summary>
    private async Task<DemandPlan> NewAsync(
        DemandPlanHeader header, List<(DemandPlanLineData Data, Item Item)> lines, Guid? replacesId, CancellationToken cancellationToken)
    {
        var companyId = _scope.RequireCompany();
        await CheckAsync(header, lines, null, replacesId, cancellationToken);
        var today = _clock.TodayInVietnam();
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var no = await _numbers.NextAsync(DocumentTypes.DemandPlan, today, cancellationToken);
        var plan = DemandPlan.Create(companyId, no, header, today, replacesId);
        plan.SetLines(lines);
        _db.DemandPlans.Add(plan);
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return plan;
    }

    /// <summary>
    /// Quy tắc của phiếu (design.md 18.25, lát G5a): nhóm hàng trong phạm vi; mặt hàng thuộc nhóm hàng (hoặc nhóm con); mỗi mặt hàng × năm của
    /// công ty chỉ thuộc một phiếu còn hiệu lực (Nháp, Đã chốt) – trừ chính phiếu và phiếu mà nó điều chỉnh.
    /// </summary>
    private async Task CheckAsync(
        DemandPlanHeader header, List<(DemandPlanLineData Data, Item Item)> lines, Guid? exceptId, Guid? replacesId, CancellationToken cancellationToken)
    {
        var parents = await _db.ItemCategories.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.ParentId, cancellationToken);
        if (!parents.ContainsKey(header.ItemCategoryId))
            throw new BusinessRuleException("Nhóm hàng không tồn tại hoặc ngoài phạm vi nhóm hàng của bạn.");
        var outside = lines.FirstOrDefault(l => !ItemAdministration.IsUnder(l.Item.CategoryId, header.ItemCategoryId, parents));
        if (outside.Item is not null) throw new BusinessRuleException($"Mặt hàng {outside.Item.Code} không thuộc nhóm hàng của phiếu.");

        // Bỏ filter nhóm hàng khi dò trùng: phiếu nhóm hàng khác (người lập không được xem) vẫn có thể chứa cùng mặt hàng ở nhóm con.
        // Filter công ty giữ nguyên – mỗi pháp nhân có kế hoạch riêng, tổng hợp gộp cộng các công ty (lát G5b).
        var itemIds = lines.Select(l => l.Item.Id).Distinct().ToList();
        var conflict = await _db.DemandPlans.AsNoTracking().IgnoreQueryFilters([ProcDbContext.ItemCategoryFilter])
            .Where(x => x.Id != exceptId && x.Id != replacesId && x.Year == header.Year
                        && (x.Status == DemandPlanStatus.Draft || x.Status == DemandPlanStatus.Confirmed))
            .SelectMany(x => x.Lines.Where(l => itemIds.Contains(l.ItemId)).Select(l => new { x.No, l.ItemId }))
            .FirstOrDefaultAsync(cancellationToken);
        if (conflict is not null)
        {
            var code = lines.First(l => l.Item.Id == conflict.ItemId).Item.Code;
            throw new BusinessRuleException(
                $"Mặt hàng {code} đã có trong phiếu {conflict.No} cùng năm {header.Year} – sửa phiếu đó hoặc lập phiếu điều chỉnh.");
        }
    }

    private async Task<DemandPlan> FindOwnAsync(Guid id, CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        if (!_scope.HasPermission(Permissions.Purchase.DemandPlanManage))
            throw new ForbiddenAccessException("Bạn không có quyền lập, sửa phiếu kế hoạch nhu cầu.");
        var plan = await _db.DemandPlans.Include(x => x.Lines).SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
                   ?? throw new NotFoundException("Không tìm thấy phiếu kế hoạch nhu cầu.");
        var ids = plan.Lines.Select(l => l.ItemId).Distinct().ToList();
        if (await _db.Items.CountAsync(i => ids.Contains(i.Id), cancellationToken) < ids.Count)
            throw new ForbiddenAccessException("Phiếu có mặt hàng ngoài phạm vi nhóm hàng của bạn nên bạn không thao tác được.");
        return plan;
    }

    private async Task<List<(DemandPlanLineData Data, Item Item)>> ResolveLinesAsync(
        IReadOnlyList<SaveDemandPlanLineRequest> lines, CancellationToken cancellationToken)
    {
        var ids = lines.Select(l => l.ItemId).Distinct().ToList();
        var items = await _db.Items.AsNoTracking().Include(i => i.Conversions).Where(i => ids.Contains(i.Id)).ToDictionaryAsync(i => i.Id, cancellationToken);
        return lines.Select((l, index) => (
                new DemandPlanLineData(l.Id, l.ItemId, l.UomId, l.Months, l.Note),
                items.GetValueOrDefault(l.ItemId)
                ?? throw new NotFoundException($"Dòng {index + 1}: không tìm thấy mặt hàng (hoặc mặt hàng ngoài phạm vi nhóm hàng của bạn).")))
            .ToList();
    }

    private async Task<Dictionary<Guid, string>> UserNamesAsync(IEnumerable<Guid?> ids, CancellationToken cancellationToken)
    {
        var list = ids.OfType<Guid>().Distinct().ToList();
        return await _db.Users.AsNoTracking().Where(u => list.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken);
    }

    private async Task<DemandPlanDto> ToDtoAsync(DemandPlan x, CancellationToken cancellationToken)
    {
        var itemIds = x.Lines.Select(l => l.ItemId).Distinct().ToList();
        var items = await _db.Items.AsNoTracking().Where(i => itemIds.Contains(i.Id)).Select(i => new { i.Id, i.Code, i.Name }).ToDictionaryAsync(i => i.Id, cancellationToken);
        var uomIds = x.Lines.Select(l => l.UomId).Distinct().ToList();
        var uoms = await _db.Uoms.AsNoTracking().Where(u => uomIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Code, cancellationToken);
        var category = await _db.ItemCategories.AsNoTracking().Where(c => c.Id == x.ItemCategoryId).Select(c => new { c.Code, c.Name }).SingleOrDefaultAsync(cancellationToken);
        var users = await UserNamesAsync([x.CreatedBy, x.ConfirmedBy], cancellationToken);
        var replacesNo = x.ReplacesId is { } rid
            ? await _db.DemandPlans.AsNoTracking().IgnoreQueryFilters([ProcDbContext.ItemCategoryFilter]).Where(o => o.Id == rid).Select(o => o.No).SingleOrDefaultAsync(cancellationToken)
            : null;
        var replacedBy = await _db.DemandPlans.AsNoTracking().IgnoreQueryFilters([ProcDbContext.ItemCategoryFilter])
            .Where(o => o.ReplacesId == x.Id && o.Status != DemandPlanStatus.Cancelled).Select(o => new { o.Id, o.No }).FirstOrDefaultAsync(cancellationToken);

        string? readOnly = items.Count < itemIds.Count ? "Phiếu có mặt hàng ngoài phạm vi nhóm hàng của bạn nên chỉ xem được."
            : !_scope.HasPermission(Permissions.Purchase.DemandPlanManage) ? "Bạn không có quyền lập, sửa phiếu kế hoạch nhu cầu." : null;
        var actions = new DemandPlanActions(
            readOnly is null && x.Status == DemandPlanStatus.Draft,
            readOnly is null && x.Status is DemandPlanStatus.Draft or DemandPlanStatus.Confirmed && replacedBy is null,
            readOnly is null && x.Status == DemandPlanStatus.Confirmed && replacedBy is null && x.Year >= _clock.TodayInVietnam().Year,
            readOnly);
        var lines = x.Lines.OrderBy(l => l.LineNo).Select(l =>
        {
            var item = items.GetValueOrDefault(l.ItemId);
            return new DemandPlanLineDto(
                l.Id, l.LineNo, l.ItemId, item?.Code ?? "(ngoài phạm vi)", item?.Name ?? "", l.UomId, uoms.GetValueOrDefault(l.UomId) ?? "",
                l.Months, l.Total, l.Note);
        }).ToList();
        return new DemandPlanDto(
            x.Id, x.No, x.Year, x.ItemCategoryId, category?.Code ?? "", category?.Name ?? "", x.Note, x.Status.ToString(), x.ReplacesId, replacesNo,
            replacedBy?.Id, replacedBy?.No, x.ConfirmedAt, x.ConfirmedBy is { } cb ? users.GetValueOrDefault(cb) : null, x.CancelledOn, x.CancelReason,
            x.CreatedAt, x.CreatedBy is { } u ? users.GetValueOrDefault(u) : null, x.Version, lines, actions);
    }

    private static DemandPlanHeader ToHeader(SaveDemandPlanRequest r) => new(r.Year, r.ItemCategoryId, r.Note);

    private static SaveDemandPlanLineRequest ToRequest(DemandPlanLineData d) => new(d.Id, d.ItemId, d.UomId, d.Months, d.Note);
}
