using Microsoft.EntityFrameworkCore;
using Proc.Application.Common;
using Proc.Application.Purchasing;
using Proc.Domain.Common;
using Proc.Domain.Organization;
using Proc.Domain.Purchasing;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.Purchasing;

/// <summary>
/// Ủy quyền duyệt có thời hạn (lát O2, design.md mục 19.3). Bảng lọc theo công ty đang làm việc; trong công ty, người không có quyền quản lý chỉ
/// thấy ủy quyền mình lập hoặc mình được nhận. Việc duyệt thay nằm ở <see cref="ApprovalEngine"/>.
/// </summary>
public sealed class ApprovalDelegationService : IApprovalDelegationService
{
    private const string AllDocTypesLabel = "Mọi loại phiếu";

    private readonly ProcDbContext _db;
    private readonly ICurrentUserScope _scope;
    private readonly IClock _clock;

    /// <summary>Tạo dịch vụ ủy quyền duyệt.</summary>
    /// <param name="db">DbContext.</param>
    /// <param name="scope">Phạm vi của người thao tác.</param>
    /// <param name="clock">Đồng hồ (ngày Việt Nam xét hiệu lực).</param>
    public ApprovalDelegationService(ProcDbContext db, ICurrentUserScope scope, IClock clock)
    {
        _db = db;
        _scope = scope;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ApprovalDelegationDto>> ListAsync(CancellationToken cancellationToken) =>
        await ToDtosAsync(await Visible().OrderByDescending(d => d.CreatedAt).ToListAsync(cancellationToken), cancellationToken);

    /// <inheritdoc />
    public async Task<ApprovalDelegationDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var delegation = await Visible().SingleOrDefaultAsync(d => d.Id == id, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy ủy quyền duyệt.");
        return (await ToDtosAsync([delegation], cancellationToken))[0];
    }

    /// <inheritdoc />
    public async Task<ApprovalDelegationDto> CreateAsync(CreateApprovalDelegationRequest request, CancellationToken cancellationToken)
    {
        var companyId = _scope.RequireCompany();
        var me = _scope.UserId ?? throw new ForbiddenAccessException("Chưa đăng nhập.");
        var delegator = request.DelegatorUserId ?? me;
        // Tự ủy quyền không cần quyền riêng (người giữ cấp duyệt nào cũng có lúc vắng); lập thay người khác là việc của quản trị.
        if (delegator != me && !_scope.HasPermission(Permissions.Organization.DelegationManage))
            throw new ForbiddenAccessException("Bạn không có quyền lập ủy quyền thay người khác.");

        var docType = string.IsNullOrWhiteSpace(request.DocType) ? null : ApprovalTypes.Get(request.DocType).Code;
        var permissions = docType is null ? ApprovePermissions() : [ApprovalTypes.Get(docType).ApprovePermission];
        var eligible = await EligibleUsersAsync(companyId, permissions, cancellationToken);
        var noun = docType is null ? "duyệt" : $"duyệt {ApprovalTypes.Get(docType).Name.ToLowerInvariant()}";
        if (!eligible.Contains(delegator))
            throw new BusinessRuleException($"Người ủy quyền phải là tài khoản đang hoạt động có quyền {noun} tại công ty.");
        // Người được ủy quyền vẫn phải tự có quyền duyệt (API duyệt kiểm tra quyền chức năng) – ủy quyền chỉ cho mượn vị trí của cấp.
        if (!eligible.Contains(request.DelegateUserId))
            throw new BusinessRuleException($"Người được ủy quyền phải là tài khoản đang hoạt động có quyền {noun} tại công ty – hãy phân quyền trước.");

        var today = _clock.TodayInVietnam();
        var delegation = ApprovalDelegation.Create(
            companyId, delegator, request.DelegateUserId, docType, request.ValidFrom, request.ValidTo, request.Reason, request.Note, today);
        // Trùng: cùng hai người, loại phiếu chồng nhau (mọi loại chồng mọi loại cụ thể), ngày chồng nhau – tránh hai dòng cho cùng một việc.
        var overlap = await _db.ApprovalDelegations.AnyAsync(
            d => d.DelegatorUserId == delegator && d.DelegateUserId == request.DelegateUserId && d.RevokedAt == null
                && (d.DocType == null || docType == null || d.DocType == docType)
                && d.ValidFrom <= delegation.ValidTo && delegation.ValidFrom <= d.ValidTo,
            cancellationToken);
        if (overlap) throw new BusinessRuleException("Đã có ủy quyền còn hiệu lực cho người này trùng loại phiếu và thời gian – thu hồi ủy quyền cũ trước.");

        _db.ApprovalDelegations.Add(delegation);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(delegation.Id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ApprovalDelegationDto> RevokeAsync(Guid id, CancellationToken cancellationToken)
    {
        var me = _scope.UserId ?? throw new ForbiddenAccessException("Chưa đăng nhập.");
        var delegation = await Visible(tracking: true).SingleOrDefaultAsync(d => d.Id == id, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy ủy quyền duyệt.");
        // Người được ủy quyền không tự thu hồi – muốn thôi thì báo người ủy quyền (ủy quyền là quyết định của người giữ cấp).
        if (delegation.DelegatorUserId != me && !_scope.HasPermission(Permissions.Organization.DelegationManage))
            throw new ForbiddenAccessException("Chỉ người ủy quyền hoặc người quản lý ủy quyền được thu hồi.");
        delegation.Revoke(_clock.UtcNow, me, _clock.TodayInVietnam());
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DelegationUserOptionDto>> UserOptionsAsync(CancellationToken cancellationToken)
    {
        var companyId = _scope.RequireCompany();
        var ids = await EligibleUsersAsync(companyId, ApprovePermissions(), cancellationToken);
        return await _db.Users.AsNoTracking().Where(u => ids.Contains(u.Id)).OrderBy(u => u.FullName)
            .Select(u => new DelegationUserOptionDto(u.Id, u.UserName!, u.FullName)).ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public IReadOnlyList<DelegationDocTypeDto> DocTypes() =>
        ApprovalTypes.All.Where(t => !ApprovalTypes.RuleOnlyCodes.Contains(t.Code)).Select(t => new DelegationDocTypeDto(t.Code, t.Name)).ToList();

    /// <summary>Mọi quyền duyệt của các loại phiếu.</summary>
    private static List<string> ApprovePermissions() => ApprovalTypes.All.Select(t => t.ApprovePermission).Distinct().ToList();

    /// <summary>
    /// Người dùng đang hoạt động có ít nhất một trong các quyền tại công ty (qua vai trò còn dùng – cùng cách nạp phạm vi, design.md mục 9.2).
    /// </summary>
    private async Task<HashSet<Guid>> EligibleUsersAsync(Guid companyId, IReadOnlyCollection<string> permissions, CancellationToken cancellationToken)
    {
        var ids = await _db.UserCompanyRoles
            .Where(x => x.CompanyId == companyId && x.Role!.IsActive
                && _db.RolePermissions.Any(rp => rp.RoleId == x.RoleId && permissions.Contains(rp.PermissionCode)))
            .Select(x => x.UserId)
            .Where(u => _db.Users.Any(a => a.Id == u && a.IsActive))
            .Distinct()
            .ToListAsync(cancellationToken);
        return ids.ToHashSet();
    }

    /// <summary>Ủy quyền người dùng được xem tại công ty đang làm việc: toàn bộ khi có quyền quản lý, không thì của mình (lập hoặc nhận).</summary>
    private IQueryable<ApprovalDelegation> Visible(bool tracking = false)
    {
        _scope.RequireCompany();
        var me = _scope.UserId ?? throw new ForbiddenAccessException("Chưa đăng nhập.");
        var all = _scope.HasPermission(Permissions.Organization.DelegationManage);
        var query = tracking ? _db.ApprovalDelegations : _db.ApprovalDelegations.AsNoTracking();
        return query.Where(d => all || d.DelegatorUserId == me || d.DelegateUserId == me);
    }

    /// <summary>Đổi sang DTO kèm tên người, tên loại phiếu, trạng thái theo ngày Việt Nam hôm nay.</summary>
    private async Task<List<ApprovalDelegationDto>> ToDtosAsync(IReadOnlyList<ApprovalDelegation> delegations, CancellationToken cancellationToken)
    {
        var userIds = delegations
            .SelectMany(d => new[] { d.DelegatorUserId, d.DelegateUserId, d.RevokedBy ?? Guid.Empty, d.CreatedBy ?? Guid.Empty })
            .Where(u => u != Guid.Empty).Distinct().ToList();
        var users = await _db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => new { u.FullName, u.UserName }, cancellationToken);
        string Name(Guid id) => users.TryGetValue(id, out var u) ? $"{u.FullName} ({u.UserName})" : string.Empty;
        string? FullName(Guid? id) => id is { } x && users.TryGetValue(x, out var u) ? u.FullName : null;
        var today = _clock.TodayInVietnam();
        var me = _scope.UserId;
        var manage = _scope.HasPermission(Permissions.Organization.DelegationManage);
        return delegations.Select(d =>
        {
            var status = d.RevokedAt is not null ? "Revoked" : d.ValidTo < today ? "Expired" : d.ValidFrom > today ? "Upcoming" : "Active";
            var canRevoke = (status is "Active" or "Upcoming") && (manage || d.DelegatorUserId == me);
            return new ApprovalDelegationDto(
                d.Id, d.DelegatorUserId, Name(d.DelegatorUserId), d.DelegateUserId, Name(d.DelegateUserId), d.DocType,
                d.DocType is null ? AllDocTypesLabel : ApprovalTypes.Find(d.DocType)?.Name ?? d.DocType, d.ValidFrom, d.ValidTo, d.Reason, d.Note,
                status, d.RevokedAt, FullName(d.RevokedBy), d.CreatedAt, FullName(d.CreatedBy), canRevoke);
        }).ToList();
    }
}
