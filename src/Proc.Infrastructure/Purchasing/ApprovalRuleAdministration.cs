using Microsoft.EntityFrameworkCore;
using Proc.Application.Common;
using Proc.Application.Purchasing;
using Proc.Domain.Common;
using Proc.Domain.Documents;
using Proc.Domain.Purchasing;
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
    public async Task<IReadOnlyList<PendingApprovalDto>> PendingForMeAsync(CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        if (_scope.UserId is null) return [];
        var requests = await _db.PurchaseRequests.Where(r => r.Status == ApprovalDocumentStatus.Submitted).ToListAsync(cancellationToken);
        var orders = await _db.PurchaseOrders.Where(o => o.Status == ApprovalDocumentStatus.Submitted).ToListAsync(cancellationToken);
        var supplierIds = orders.Select(o => o.SupplierId).Distinct().ToList();
        var suppliers = await _db.Suppliers.AsNoTracking().Where(s => supplierIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => $"{s.Code} – {s.Name}", cancellationToken);
        var creatorIds = requests.Select(r => r.CreatedBy).Concat(orders.Select(o => o.CreatedBy)).OfType<Guid>().Distinct().ToList();
        var users = await _db.Users.AsNoTracking().Where(u => creatorIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken);

        var result = new List<PendingApprovalDto>();
        foreach (var (document, description) in requests.Select(r => ((ApprovableDocument)r, r.Department))
                     .Concat(orders.Select(o => ((ApprovableDocument)o, (string?)suppliers.GetValueOrDefault(o.SupplierId)))))
        {
            var step = await _approvals.CurrentStepAsync(document, cancellationToken);
            if (await _approvals.ApproveBlockerAsync(document, step, cancellationToken) is not null) continue;
            result.Add(new PendingApprovalDto(
                document.DocType, document.Id, document.No, document.Date, description, document.ApprovalAmount, step!.Level,
                document.CreatedBy is { } c ? users.GetValueOrDefault(c) : null));
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
