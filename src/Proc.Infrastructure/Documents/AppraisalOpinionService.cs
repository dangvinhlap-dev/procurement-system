using Microsoft.EntityFrameworkCore;
using Proc.Application.Common;
using Proc.Application.Documents;
using Proc.Domain.Common;
using Proc.Domain.Documents;
using Proc.Domain.MasterData;
using Proc.Domain.Organization;
using Proc.Domain.Pricing;
using Proc.Domain.Purchasing;
using Proc.Domain.Sourcing;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.Documents;

/// <summary>
/// Ý kiến thẩm định của thành viên chuyên môn (lát H1 – design.md mục 18.25). Chủ (GTH, RFQ, hợp đồng, NCC) đọc qua global query filter như
/// đính kèm: ngoài phạm vi công ty thì 404. Xem theo quyền xem chủ; ghi cần thêm <see cref="Permissions.Sourcing.AppraisalGive"/>. Ý kiến
/// không phải cấp duyệt nên không đụng tới luồng duyệt.
/// </summary>
public sealed class AppraisalOpinionService : IAppraisalOpinionService
{
    /// <summary>Quyền xem chủ – cùng quyền xem của màn hình chủ, không thêm mã quyền xem riêng.</summary>
    private static readonly IReadOnlyDictionary<string, string> ViewPermissions = new Dictionary<string, string>
    {
        [AppraisalOwnerTypes.MonthlySupplierApproval] = Permissions.Pricing.MonthlyApprovalView,
        [AppraisalOwnerTypes.Rfq] = Permissions.Sourcing.RfqView,
        [AppraisalOwnerTypes.PurchaseContract] = Permissions.Purchase.ContractView,
        [AppraisalOwnerTypes.Supplier] = Permissions.MasterData.SupplierView,
    };

    private readonly ProcDbContext _db;
    private readonly ICurrentUserScope _scope;
    private readonly IClock _clock;

    /// <summary>Tạo dịch vụ.</summary>
    /// <param name="db">DbContext (đã gắn phạm vi người dùng).</param>
    /// <param name="scope">Phạm vi người dùng.</param>
    /// <param name="clock">Đồng hồ.</param>
    public AppraisalOpinionService(ProcDbContext db, ICurrentUserScope scope, IClock clock)
    {
        _db = db;
        _scope = scope;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task<AppraisalOpinionListDto> ListAsync(string ownerType, Guid ownerId, CancellationToken cancellationToken)
    {
        var owner = await RequireOwnerAsync(ownerType, ownerId, cancellationToken);
        var opinions = await QueryAsync(ownerType, ownerId, null, owner, cancellationToken);
        var active = opinions.Where(o => !o.IsWithdrawn).ToList();
        var blocked = GiveBlockedReason(owner);
        return new AppraisalOpinionListDto(
            opinions, active.Count(o => o.Result == nameof(AppraisalResult.Pass)), active.Count(o => o.Result == nameof(AppraisalResult.Fail)),
            blocked is null, blocked);
    }

    /// <inheritdoc />
    public async Task<AppraisalOpinionDto> GiveAsync(GiveAppraisalOpinionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        _scope.EnsurePermission(Permissions.Sourcing.AppraisalGive);
        var owner = await RequireOwnerAsync(request.OwnerType, request.OwnerId, cancellationToken);
        if (!Enum.TryParse<AppraisalAspect>(request.Aspect, out var aspect)) throw new BusinessRuleException("Khía cạnh thẩm định không hợp lệ.");
        if (!Enum.TryParse<AppraisalResult>(request.Result, out var result))
            throw new BusinessRuleException("Kết luận thẩm định phải là Đạt hoặc Không đạt.");
        var opinion = AppraisalOpinion.Create(
            request.OwnerType, request.OwnerId, owner, _scope.UserId ?? Guid.Empty, aspect, result, request.Content);
        _db.AppraisalOpinions.Add(opinion);
        await _db.SaveChangesAsync(cancellationToken);
        return (await QueryAsync(request.OwnerType, request.OwnerId, opinion.Id, owner, cancellationToken)).Single();
    }

    /// <inheritdoc />
    public async Task<AppraisalOpinionDto> WithdrawAsync(Guid id, WithdrawAppraisalOpinionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var opinion = await FindAsync(id, cancellationToken);
        var owner = await RequireOwnerAsync(opinion.OwnerType, opinion.OwnerId, cancellationToken);
        opinion.Withdraw(owner, _scope.UserId ?? Guid.Empty, request.Reason, _clock.UtcNow);
        await _db.SaveChangesAsync(cancellationToken);
        return (await QueryAsync(opinion.OwnerType, opinion.OwnerId, opinion.Id, owner, cancellationToken)).Single();
    }

    /// <inheritdoc />
    public async Task<Guid?> RequireAttachmentAccessAsync(Guid opinionId, bool manage, CancellationToken cancellationToken)
    {
        var opinion = await FindAsync(opinionId, cancellationToken);
        var owner = await RequireOwnerAsync(opinion.OwnerType, opinion.OwnerId, cancellationToken);
        if (manage)
        {
            // Đính kèm là bằng chứng của chính ý kiến (biên bản, kết quả kiểm nghiệm): chỉ người ghi thêm/xóa, khi ý kiến còn hiệu lực và chủ
            // còn mở – sau khi Hội đồng quyết thì bằng chứng giữ nguyên để đối chiếu.
            _scope.EnsurePermission(Permissions.Sourcing.AppraisalGive);
            if (opinion.CreatedBy != _scope.UserId) throw new BusinessRuleException("Chỉ người ghi ý kiến mới đính kèm, xóa file của ý kiến đó.");
            if (opinion.IsWithdrawn) throw new BusinessRuleException("Ý kiến thẩm định đã rút – không đính kèm thêm được.");
            if (!owner.IsOpen) throw new BusinessRuleException($"{owner.Label} đã có quyết định cuối cùng – không đính kèm thêm được.");
        }

        return owner.CompanyId;
    }

    /// <summary>Tìm ý kiến thẩm định theo Id; không thấy thì báo lỗi 404.</summary>
    private async Task<AppraisalOpinion> FindAsync(Guid id, CancellationToken cancellationToken) =>
        await _db.AppraisalOpinions.FirstOrDefaultAsync(o => o.Id == id, cancellationToken)
        ?? throw new NotFoundException("Không tìm thấy ý kiến thẩm định.");

    /// <summary>Lý do người đang xem không ghi được ý kiến; null khi ghi được.</summary>
    private string? GiveBlockedReason(AppraisalOwnerState owner)
    {
        if (!_scope.HasPermission(Permissions.Sourcing.AppraisalGive)) return "Bạn không có quyền ghi ý kiến thẩm định.";
        if (!owner.IsOpen) return $"{owner.Label} đã có quyết định cuối cùng.";
        if (owner.CreatedBy == _scope.UserId) return "Người lập không tự ghi ý kiến thẩm định cho phiếu của mình.";
        return null;
    }

    /// <summary>
    /// Đọc ý kiến kèm tên, vai trò người ghi. Vai trò lấy ở công ty đang làm việc (bằng công ty của chủ – <see cref="RequireOwnerAsync"/> đã
    /// kiểm tra; với NCC là công ty người xem đang làm việc).
    /// </summary>
    private async Task<List<AppraisalOpinionDto>> QueryAsync(
        string ownerType, Guid ownerId, Guid? onlyId, AppraisalOwnerState owner, CancellationToken cancellationToken)
    {
        var rows = await (
                from o in _db.AppraisalOpinions.AsNoTracking()
                where o.OwnerType == ownerType && o.OwnerId == ownerId && (onlyId == null || o.Id == onlyId)
                join u in _db.Users on o.CreatedBy equals u.Id into users
                from u in users.DefaultIfEmpty()
                orderby o.CreatedAt
                select new { Opinion = o, AuthorName = u == null ? null : u.FullName })
            .ToListAsync(cancellationToken);
        var companyId = _scope.RequireCompany();
        var authorIds = rows.Select(r => r.Opinion.CreatedBy).OfType<Guid>().Distinct().ToList();
        var roles = (await (
                    from ucr in _db.UserCompanyRoles.AsNoTracking()
                    join r in _db.Roles on ucr.RoleId equals r.Id
                    where ucr.CompanyId == companyId && authorIds.Contains(ucr.UserId)
                    select new { ucr.UserId, r.Name })
                .ToListAsync(cancellationToken))
            .GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => string.Join(", ", g.Select(x => x.Name).Order(StringComparer.CurrentCulture)));
        var userId = _scope.UserId;
        return rows.Select(r =>
        {
            var o = r.Opinion;
            var canWithdraw = !o.IsWithdrawn && owner.IsOpen && o.CreatedBy == userId;
            return new AppraisalOpinionDto(
                o.Id, o.Aspect.ToString(), o.Result.ToString(), o.Content, o.CreatedAt, r.AuthorName,
                o.CreatedBy is { } author && roles.TryGetValue(author, out var names) ? names : null,
                o.IsWithdrawn, o.WithdrawReason, o.WithdrawnAt, canWithdraw);
        }).ToList();
    }

    /// <summary>
    /// Kiểm tra quyền xem chủ rồi đọc chủ qua global query filter. "Còn mở" = chưa có quyết định cuối cùng: GTH Nháp/Chờ duyệt; RFQ Nháp/Đã
    /// gửi/Chờ duyệt kết quả; hợp đồng Nháp/Chờ duyệt; NCC chưa vào danh sách đen (hồ sơ NCC được thẩm định lại khi đổi trạng thái, đánh giá).
    /// </summary>
    private async Task<AppraisalOwnerState> RequireOwnerAsync(string ownerType, Guid ownerId, CancellationToken cancellationToken)
    {
        if (!ViewPermissions.TryGetValue(ownerType ?? string.Empty, out var viewPermission))
            throw new BusinessRuleException($"Loại đối tượng thẩm định '{ownerType}' không hợp lệ.");
        _scope.RequireCompany();
        _scope.EnsurePermission(viewPermission);
        var owner = ownerType switch
        {
            AppraisalOwnerTypes.MonthlySupplierApproval => await _db.MonthlySupplierApprovals.Where(x => x.Id == ownerId)
                .Select(x => new AppraisalOwnerState(
                    x.CompanyId, x.Status == MonthlyApprovalStatus.Draft || x.Status == MonthlyApprovalStatus.Submitted, x.CreatedBy, "Phiếu " + x.No))
                .FirstOrDefaultAsync(cancellationToken),
            AppraisalOwnerTypes.Rfq => await _db.Rfqs.Where(x => x.Id == ownerId)
                .Select(x => new AppraisalOwnerState(
                    x.CompanyId, x.Status == RfqStatus.Draft || x.Status == RfqStatus.Sent || x.Status == RfqStatus.AwardPending, x.CreatedBy,
                    "Yêu cầu báo giá " + x.No))
                .FirstOrDefaultAsync(cancellationToken),
            AppraisalOwnerTypes.PurchaseContract => await _db.PurchaseContracts.Where(x => x.Id == ownerId)
                .Select(x => new AppraisalOwnerState(
                    x.CompanyId, x.Status == ContractStatus.Draft || x.Status == ContractStatus.Submitted, x.CreatedBy, "Hợp đồng " + x.No))
                .FirstOrDefaultAsync(cancellationToken),
            _ => await _db.Suppliers.Where(x => x.Id == ownerId)
                .Select(x => new AppraisalOwnerState(null, x.Status != SupplierStatus.Blacklisted, x.CreatedBy, "NCC " + x.Code))
                .FirstOrDefaultAsync(cancellationToken),
        };
        if (owner is null) throw new NotFoundException("Không tìm thấy đối tượng thẩm định hoặc ngoài phạm vi được xem.");
        if (owner.CompanyId is { } companyId) _scope.EnsureCompany(companyId);
        return owner;
    }
}
