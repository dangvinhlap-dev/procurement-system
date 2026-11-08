using Microsoft.EntityFrameworkCore;
using Proc.Application.Common;
using Proc.Application.Purchasing;
using Proc.Domain.Common;
using Proc.Domain.Documents;
using Proc.Domain.MasterData;
using Proc.Domain.Pricing;
using Proc.Domain.Purchasing;
using Proc.Domain.Sourcing;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.Purchasing;

/// <summary>
/// Cấu hình quy trình duyệt của công ty đang làm việc và danh sách phiếu chờ người dùng hiện tại duyệt (design.md mục 6.2).
/// </summary>
public sealed class ApprovalRuleAdministration : IApprovalRuleAdministration
{
    private readonly ProcDbContext _db;
    private readonly ICurrentUserScope _scope;
    private readonly ApprovalEngine _approvals;

    /// <summary>Tạo dịch vụ.</summary>
    public ApprovalRuleAdministration(ProcDbContext db, ICurrentUserScope scope, IClock clock)
    {
        _db = db;
        _scope = scope;
        _approvals = new ApprovalEngine(db, scope, clock);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ApprovalRuleDto>> ListAsync(CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        return await Query().ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ApprovalRuleDto> CreateAsync(SaveApprovalRuleRequest request, CancellationToken cancellationToken)
    {
        var companyId = _scope.RequireCompany();
        await EnsureRoleAsync(request.RoleId, cancellationToken);
        var rule = ApprovalRule.Create(companyId, request.DocType, request.Level, request.MinAmount, request.RoleId);
        await EnsureConsistentAsync(rule, cancellationToken);
        _db.ApprovalRules.Add(rule);
        await _db.SaveChangesAsync(cancellationToken);
        return await Query(rule.Id).SingleAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ApprovalRuleDto> UpdateAsync(Guid id, SaveApprovalRuleRequest request, CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        var rule = await _db.ApprovalRules.SingleOrDefaultAsync(r => r.Id == id, cancellationToken) ?? throw new NotFoundException("Không tìm thấy cấp duyệt.");
        await EnsureRoleAsync(request.RoleId, cancellationToken);
        rule.Update(request.Level, request.MinAmount, request.RoleId);
        await EnsureConsistentAsync(rule, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return await Query(id).SingleAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        var rule = await _db.ApprovalRules.SingleOrDefaultAsync(r => r.Id == id, cancellationToken) ?? throw new NotFoundException("Không tìm thấy cấp duyệt.");
        // Phiếu đang chờ duyệt giữ nguyên các bước đã sinh lúc gửi; cấu hình mới áp cho lần gửi sau.
        _db.ApprovalRules.Remove(rule);
        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public IReadOnlyList<ApprovalTypeDto> Types() => ApprovalTypes.All.Select(t => new ApprovalTypeDto(t.Code, t.Name, t.AmountLabel)).ToList();

    /// <inheritdoc />
    public async Task<IReadOnlyList<PendingApprovalDto>> PendingForMeAsync(CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        if (_scope.UserId is null) return [];
        var requests = await _db.PurchaseRequests.Where(r => r.Status == ApprovalDocumentStatus.Submitted).ToListAsync(cancellationToken);
        var orders = await _db.PurchaseOrders.Where(o => o.Status == ApprovalDocumentStatus.Submitted).ToListAsync(cancellationToken);
        var departmentIds = requests.Select(r => r.DepartmentId).OfType<Guid>().Distinct().ToList();
        var departments = await _db.Departments.AsNoTracking().Where(d => departmentIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id, d => d.Name, cancellationToken);
        var supplierIds = orders.Select(o => o.SupplierId).Distinct().ToList();
        var suppliers = await _db.Suppliers.AsNoTracking().Where(s => supplierIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => $"{s.Code} – {s.Name}", cancellationToken);
        var creatorIds = requests.Select(r => r.CreatedBy).Concat(orders.Select(o => o.CreatedBy)).OfType<Guid>().Distinct().ToList();
        var users = await _db.Users.AsNoTracking().Where(u => creatorIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken);

        var result = new List<PendingApprovalDto>();
        foreach (var (document, description) in requests.Select(r => ((ApprovableDocument)r, (r.DepartmentId is { } d ? departments.GetValueOrDefault(d) : null) ?? r.LegacyDepartment))
                     .Concat(orders.Select(o => ((ApprovableDocument)o, (string?)suppliers.GetValueOrDefault(o.SupplierId)))))
        {
            var step = await _approvals.CurrentStepAsync(document, cancellationToken);
            if (await _approvals.ApproveBlockerAsync(document, step, cancellationToken) is not null) continue;
            result.Add(new PendingApprovalDto(
                document.DocType, ApprovalTypes.Get(document.DocType).Name, document.Id, document.No, document.Date, description, document.ApprovalAmount, step!.Level,
                document.CreatedBy is { } c ? users.GetValueOrDefault(c) : null));
        }

        // Yêu cầu duyệt NCC (design.md mục 18.6): dùng chung toàn tập đoàn, hiện cho người duyệt khi đến lượt công ty đang làm việc.
        var changes = await _db.SupplierChangeRequests.Where(r => r.Status == SupplierChangeRequestStatus.Submitted).ToListAsync(cancellationToken);
        var changeSupplierIds = changes.Select(c => c.SupplierId).Distinct().ToList();
        var changeSuppliers = await _db.Suppliers.AsNoTracking().Where(s => changeSupplierIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => $"{s.Code} – {s.Name}", cancellationToken);
        var changeCreatorIds = changes.Select(c => c.CreatedBy).OfType<Guid>().Except(users.Keys).Distinct().ToList();
        foreach (var u in await _db.Users.AsNoTracking().Where(u => changeCreatorIds.Contains(u.Id)).ToListAsync(cancellationToken)) users[u.Id] = u.FullName;
        foreach (var change in changes)
        {
            var step = await _approvals.CurrentStepAsync(change, cancellationToken);
            if (await _approvals.ApproveBlockerAsync(change, step, cancellationToken) is not null) continue;
            result.Add(new PendingApprovalDto(
                change.DocType, ApprovalTypes.Get(change.DocType).Name, change.Id, change.No,
                DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(change.CreatedAt, VietnamTime.Zone).DateTime),
                $"{changeSuppliers.GetValueOrDefault(change.SupplierId)}: {change.ProposedSummary}", 0, step!.Level,
                change.CreatedBy is { } c ? users.GetValueOrDefault(c) : null));
        }

        // Bảng giá NCC chờ duyệt (lát B1): duyệt tại công ty lập – filter công ty giữ nguyên.
        var priceLists = await _db.SupplierPriceLists.Where(p => p.Status == PriceListStatus.Submitted).ToListAsync(cancellationToken);
        var listSupplierIds = priceLists.Select(p => p.SupplierId).Distinct().ToList();
        var listSuppliers = await _db.Suppliers.AsNoTracking().Where(s => listSupplierIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => $"{s.Code} – {s.Name}", cancellationToken);
        var listCreatorIds = priceLists.Select(p => p.CreatedBy).OfType<Guid>().Except(users.Keys).Distinct().ToList();
        foreach (var u in await _db.Users.AsNoTracking().Where(u => listCreatorIds.Contains(u.Id)).ToListAsync(cancellationToken)) users[u.Id] = u.FullName;
        foreach (var list in priceLists)
        {
            var step = await _approvals.CurrentStepAsync(list, cancellationToken);
            if (await _approvals.ApproveBlockerAsync(list, step, cancellationToken) is not null) continue;
            result.Add(new PendingApprovalDto(
                list.DocType, ApprovalTypes.Get(list.DocType).Name, list.Id, list.No, list.ValidFrom,
                $"{listSuppliers.GetValueOrDefault(list.SupplierId)}: {list.Title}", 0, step!.Level,
                list.CreatedBy is { } c ? users.GetValueOrDefault(c) : null));
        }

        // Kết quả chọn thầu chờ hội đồng duyệt (lát B3): duyệt tại công ty lập RFQ; "Người lập" là người chọn thầu.
        var rfqs = await _db.Rfqs.Include(r => r.Lines).Include(r => r.Suppliers).ThenInclude(s => s.QuotationLines).AsSplitQuery()
            .Where(r => r.Status == RfqStatus.AwardPending).ToListAsync(cancellationToken);
        var awarderIds = rfqs.Select(r => r.AwardedBy).OfType<Guid>().Except(users.Keys).Distinct().ToList();
        foreach (var u in await _db.Users.AsNoTracking().Where(u => awarderIds.Contains(u.Id)).ToListAsync(cancellationToken)) users[u.Id] = u.FullName;
        foreach (var rfq in rfqs)
        {
            var step = await _approvals.CurrentStepAsync(rfq, cancellationToken);
            if (await _approvals.ApproveBlockerAsync(rfq, step, cancellationToken) is not null) continue;
            result.Add(new PendingApprovalDto(
                rfq.DocType, ApprovalTypes.Get(rfq.DocType).Name, rfq.Id, rfq.No, rfq.AwardDate ?? rfq.Date, rfq.Title, rfq.ApprovalAmount, step!.Level,
                rfq.AwardedBy is { } a ? users.GetValueOrDefault(a) : null));
        }

        // Hợp đồng nguyên tắc chờ duyệt (lát B4a): duyệt tại công ty ký – filter công ty giữ nguyên.
        var contracts = await _db.PurchaseContracts.Include(c => c.Lines).Where(c => c.Status == ContractStatus.Submitted).ToListAsync(cancellationToken);
        var contractSupplierIds = contracts.Select(c => c.SupplierId).Distinct().ToList();
        var contractSuppliers = await _db.Suppliers.AsNoTracking().Where(s => contractSupplierIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => $"{s.Code} – {s.Name}", cancellationToken);
        var contractCreatorIds = contracts.Select(c => c.CreatedBy).OfType<Guid>().Except(users.Keys).Distinct().ToList();
        foreach (var u in await _db.Users.AsNoTracking().Where(u => contractCreatorIds.Contains(u.Id)).ToListAsync(cancellationToken)) users[u.Id] = u.FullName;
        foreach (var contract in contracts)
        {
            var step = await _approvals.CurrentStepAsync(contract, cancellationToken);
            if (await _approvals.ApproveBlockerAsync(contract, step, cancellationToken) is not null) continue;
            result.Add(new PendingApprovalDto(
                contract.DocType, ApprovalTypes.Get(contract.DocType).Name, contract.Id, contract.No, contract.ValidFrom,
                $"{contractSuppliers.GetValueOrDefault(contract.SupplierId)}: {contract.Title}", contract.ApprovalAmount, step!.Level,
                contract.CreatedBy is { } c ? users.GetValueOrDefault(c) : null));
        }

        // Phụ lục hợp đồng chờ duyệt (lát B4b): duyệt tại công ty ký – filter công ty giữ nguyên; mô tả kèm tóm tắt thay đổi.
        var amendments = await _db.PurchaseContractAmendments.Where(a => a.Status == ContractAmendmentStatus.Submitted).ToListAsync(cancellationToken);
        var amendedIds = amendments.Select(a => a.ContractId).Distinct().ToList();
        var amended = await (
                from c in _db.PurchaseContracts.AsNoTracking()
                where amendedIds.Contains(c.Id)
                join s in _db.Suppliers on c.SupplierId equals s.Id
                select new { c.Id, Label = s.Code + " – " + s.Name + ": " + c.Title })
            .ToDictionaryAsync(c => c.Id, c => c.Label, cancellationToken);
        var amendmentCreatorIds = amendments.Select(a => a.CreatedBy).OfType<Guid>().Except(users.Keys).Distinct().ToList();
        foreach (var u in await _db.Users.AsNoTracking().Where(u => amendmentCreatorIds.Contains(u.Id)).ToListAsync(cancellationToken)) users[u.Id] = u.FullName;
        foreach (var amendment in amendments)
        {
            var step = await _approvals.CurrentStepAsync(amendment, cancellationToken);
            if (await _approvals.ApproveBlockerAsync(amendment, step, cancellationToken) is not null) continue;
            result.Add(new PendingApprovalDto(
                amendment.DocType, ApprovalTypes.Get(amendment.DocType).Name, amendment.Id, amendment.No,
                DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(amendment.CreatedAt, VietnamTime.Zone).DateTime),
                $"{amended.GetValueOrDefault(amendment.ContractId)} – {amendment.ChangeSummary}", amendment.ApprovalAmount, step!.Level,
                amendment.CreatedBy is { } c ? users.GetValueOrDefault(c) : null));
        }

        // Phê duyệt NCC độc quyền chờ duyệt (lát B5b): duyệt tại công ty lập – filter công ty và nhóm hàng giữ nguyên.
        var soleSources = await _db.SoleSourceApprovals.Where(x => x.Status == SoleSourceStatus.Submitted).ToListAsync(cancellationToken);
        var soleIds = soleSources.Select(x => x.Id).ToList();
        var soleLabels = await (
                from x in _db.SoleSourceApprovals.AsNoTracking()
                where soleIds.Contains(x.Id)
                join s in _db.Suppliers on x.SupplierId equals s.Id
                join i in _db.Items on x.ItemId equals i.Id
                select new { x.Id, Label = s.Code + " – " + s.Name + ": " + i.Code + " " + i.Name })
            .ToDictionaryAsync(x => x.Id, x => x.Label, cancellationToken);
        var soleCreatorIds = soleSources.Select(x => x.CreatedBy).OfType<Guid>().Except(users.Keys).Distinct().ToList();
        foreach (var u in await _db.Users.AsNoTracking().Where(u => soleCreatorIds.Contains(u.Id)).ToListAsync(cancellationToken)) users[u.Id] = u.FullName;
        foreach (var sole in soleSources)
        {
            var step = await _approvals.CurrentStepAsync(sole, cancellationToken);
            if (await _approvals.ApproveBlockerAsync(sole, step, cancellationToken) is not null) continue;
            result.Add(new PendingApprovalDto(
                sole.DocType, ApprovalTypes.Get(sole.DocType).Name, sole.Id, sole.No, sole.ValidFrom, soleLabels.GetValueOrDefault(sole.Id), 0,
                step!.Level, sole.CreatedBy is { } c ? users.GetValueOrDefault(c) : null));
        }

        // Phiếu duyệt NCC và giá tháng chờ Ban duyệt (lát G2): duyệt tại tổng công ty – filter công ty và nhóm hàng giữ nguyên.
        var monthly = await _db.MonthlySupplierApprovals.Include(x => x.Lines).Where(x => x.Status == MonthlyApprovalStatus.Submitted)
            .ToListAsync(cancellationToken);
        var monthlyCategoryIds = monthly.Select(x => x.ItemCategoryId).Distinct().ToList();
        var monthlyCategories = await _db.ItemCategories.AsNoTracking().Where(c => monthlyCategoryIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => $"{c.Code} – {c.Name}", cancellationToken);
        var monthlyCreatorIds = monthly.Select(x => x.CreatedBy).OfType<Guid>().Except(users.Keys).Distinct().ToList();
        foreach (var u in await _db.Users.AsNoTracking().Where(u => monthlyCreatorIds.Contains(u.Id)).ToListAsync(cancellationToken)) users[u.Id] = u.FullName;
        foreach (var sheet in monthly)
        {
            var step = await _approvals.CurrentStepAsync(sheet, cancellationToken);
            if (await _approvals.ApproveBlockerAsync(sheet, step, cancellationToken) is not null) continue;
            result.Add(new PendingApprovalDto(
                sheet.DocType, ApprovalTypes.Get(sheet.DocType).Name, sheet.Id, sheet.No, sheet.Period,
                $"Tháng {sheet.Period:MM/yyyy}: {monthlyCategories.GetValueOrDefault(sheet.ItemCategoryId)} ({sheet.Lines.Count} mặt hàng)", 0,
                step!.Level, sheet.CreatedBy is { } c ? users.GetValueOrDefault(c) : null));
        }

        // Thỏa thuận chiết khấu sản lượng gộp chờ Ban duyệt (lát G6a): duyệt tại tổng công ty – filter công ty và nhóm hàng giữ nguyên.
        var rebates = await _db.VolumeRebateAgreements.Where(x => x.Status == VolumeRebateStatus.Submitted).ToListAsync(cancellationToken);
        var rebateSupplierIds = rebates.Select(x => x.SupplierId).Distinct().ToList();
        var rebateSuppliers = await _db.Suppliers.AsNoTracking().Where(s => rebateSupplierIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => $"{s.Code} – {s.Name}", cancellationToken);
        var rebateCreatorIds = rebates.Select(x => x.CreatedBy).OfType<Guid>().Except(users.Keys).Distinct().ToList();
        foreach (var u in await _db.Users.AsNoTracking().Where(u => rebateCreatorIds.Contains(u.Id)).ToListAsync(cancellationToken)) users[u.Id] = u.FullName;
        foreach (var agreement in rebates)
        {
            var step = await _approvals.CurrentStepAsync(agreement, cancellationToken);
            if (await _approvals.ApproveBlockerAsync(agreement, step, cancellationToken) is not null) continue;
            result.Add(new PendingApprovalDto(
                agreement.DocType, ApprovalTypes.Get(agreement.DocType).Name, agreement.Id, agreement.No, agreement.ValidFrom,
                $"{agreement.Title} – {rebateSuppliers.GetValueOrDefault(agreement.SupplierId)}", 0,
                step!.Level, agreement.CreatedBy is { } c ? users.GetValueOrDefault(c) : null));
        }

        return result.OrderBy(r => r.Date).ThenBy(r => r.No).ToList();
    }

    /// <summary>
    /// Mỗi loại phiếu: cấp cao hơn phải có ngưỡng ≥ cấp thấp hơn – nếu không, phiếu nhỏ phải qua cấp cao mà bỏ cấp thấp, ngược thứ
    /// bậc duyệt. Trùng cấp do chỉ mục duy nhất trong CSDL chặn (thông báo 409).
    /// </summary>
    private async Task EnsureConsistentAsync(ApprovalRule rule, CancellationToken cancellationToken)
    {
        var others = await _db.ApprovalRules.AsNoTracking().Where(r => r.DocType == rule.DocType && r.Id != rule.Id).ToListAsync(cancellationToken);
        if (others.Any(o => o.Level == rule.Level)) throw new BusinessRuleException($"Đã có cấp {rule.Level} cho {rule.DocType}.");
        if (others.Any(o => (o.Level < rule.Level && o.MinAmount > rule.MinAmount) || (o.Level > rule.Level && o.MinAmount < rule.MinAmount)))
            throw new BusinessRuleException("Ngưỡng giá trị phải tăng dần theo cấp duyệt (cấp sau không thấp hơn cấp trước).");
    }

    private async Task EnsureRoleAsync(Guid roleId, CancellationToken cancellationToken)
    {
        if (!await _db.Roles.AnyAsync(r => r.Id == roleId, cancellationToken)) throw new NotFoundException("Không tìm thấy vai trò.");
    }

    private IQueryable<ApprovalRuleDto> Query(Guid? id = null) =>
        from r in _db.ApprovalRules.AsNoTracking()
        where id == null || r.Id == id
        join role in _db.Roles on r.RoleId equals role.Id
        orderby r.DocType, r.Level
        select new ApprovalRuleDto(r.Id, r.DocType, r.Level, r.MinAmount, r.RoleId, role.Code, role.Name);
}
