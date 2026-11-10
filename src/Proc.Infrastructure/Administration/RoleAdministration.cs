using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Proc.Application.Administration;
using Proc.Application.Common;
using Proc.Domain.Common;
using Proc.Domain.Organization;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.Administration;

/// <summary>
/// Quản lý vai trò và quyền của vai trò (design.md mục 9.1, 9.5). Quy tắc chống tự nâng quyền xem <see cref="IRoleAdministration"/>.
/// </summary>
public sealed class RoleAdministration : IRoleAdministration
{
    private readonly ProcDbContext _db;
    private readonly ICurrentUserScope _scope;
    private readonly GrantChecker _grants;
    private readonly ModuleOptions _modules;

    /// <summary>
    /// Tạo dịch vụ quản lý vai trò.
    /// </summary>
    /// <param name="db">DbContext.</param>
    /// <param name="scope">Phạm vi của người thao tác.</param>
    /// <param name="modules">Phân hệ bật/tắt – quyền của phân hệ đang tắt không hiện, không cấp, nhưng giữ nguyên trong vai trò đang có.</param>
    public RoleAdministration(ProcDbContext db, ICurrentUserScope scope, IOptions<ModuleOptions> modules)
    {
        _db = db;
        _scope = scope;
        _modules = modules.Value;
        _grants = new GrantChecker(db, scope, _modules);
    }

    /// <inheritdoc />
    public IReadOnlyList<PermissionGroupDto> GetPermissionCatalog() =>
        Permissions.Modules
            .Select(m => new PermissionGroupDto(
                m.Module,
                m.Name,
                Permissions.All.Where(p => p.Module == m.Module && !_modules.IsDisabled(p.Code)).Select(p => new PermissionDto(p.Code, p.Name)).ToList()))
            // Phân hệ kho tắt (0.22.0) thì nhóm Kho không còn quyền nào – bỏ hẳn nhóm khỏi màn hình phân quyền.
            .Where(g => g.Permissions.Count > 0)
            .ToList();

    /// <inheritdoc />
    public async Task<IReadOnlyList<RoleListItemDto>> ListAsync(CancellationToken cancellationToken)
    {
        var disabled = Permissions.InventoryModule.Where(_modules.IsDisabled).ToList();
        var permissionCounts = await _db.RolePermissions.AsNoTracking()
            .Where(rp => !disabled.Contains(rp.PermissionCode))
            .GroupBy(rp => rp.RoleId)
            .Select(g => new { RoleId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.RoleId, x => x.Count, cancellationToken);
        var userCounts = await _db.UserCompanyRoles.AsNoTracking()
            .GroupBy(x => x.RoleId)
            .Select(g => new { RoleId = g.Key, Count = g.Select(x => x.UserId).Distinct().Count() })
            .ToDictionaryAsync(x => x.RoleId, x => x.Count, cancellationToken);

        var roles = await _db.Roles.AsNoTracking()
            .OrderBy(r => r.Code == Role.AdminCode ? 0 : 1)
            .ThenBy(r => r.Code)
            .ToListAsync(cancellationToken);
        return roles
            .Select(r => new RoleListItemDto(
                r.Id, r.Code, r.Name, r.IsActive, r.IsSystem,
                permissionCounts.GetValueOrDefault(r.Id), userCounts.GetValueOrDefault(r.Id)))
            .ToList();
    }

    /// <inheritdoc />
    public async Task<RoleDetailDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var role = await _db.Roles.AsNoTracking().SingleOrDefaultAsync(r => r.Id == id, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy vai trò.");
        var permissions = (await _grants.GetRolePermissionsAsync([id], cancellationToken))[id];

        var canEdit = !role.IsSystem
            && _scope.HasPermission(Permissions.System.RoleManage)
            && _grants.WithinOwnPermissions(permissions);
        return new RoleDetailDto(
            role.Id, role.Code, role.Name, role.IsActive, role.IsSystem, canEdit,
            permissions.Order(StringComparer.Ordinal).ToList());
    }

    /// <inheritdoc />
    public async Task<RoleDetailDto> CreateAsync(CreateRoleRequest request, CancellationToken cancellationToken)
    {
        var role = Role.Create(request.Code, request.Name);
        if (await _db.Roles.AnyAsync(r => r.Code == role.Code, cancellationToken))
            throw new BusinessRuleException($"Mã vai trò {role.Code} đã có.");

        var permissions = CheckPermissions(request.PermissionCodes);
        _db.Roles.Add(role);
        foreach (var code in permissions)
            _db.RolePermissions.Add(RolePermission.Grant(role.Id, code));
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(role.Id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<RoleDetailDto> UpdateAsync(Guid id, UpdateRoleRequest request, CancellationToken cancellationToken)
    {
        var role = await _db.Roles.SingleOrDefaultAsync(r => r.Id == id, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy vai trò.");
        // Quyền của ADMIN do khởi động tự cấp đủ (design.md mục 9.1); sửa ở đây thì lần khởi động sau cũng bị ghi đè.
        if (role.IsSystem)
            throw new BusinessRuleException("Vai trò Quản trị hệ thống luôn có đủ mọi quyền, không sửa được.");

        var current = (await _grants.GetRolePermissionsAsync([id], cancellationToken))[id];
        if (!_grants.WithinOwnPermissions(current))
            throw new ForbiddenAccessException("Vai trò này có quyền mà bạn không có, nên bạn không sửa được.");

        var requested = CheckPermissions(request.PermissionCodes);
        role.Update(request.Name, request.IsActive);

        // Quyền của phân hệ đang tắt không hiện trên màn hình nên không có trong yêu cầu – giữ nguyên, không xóa (design.md mục 19.4).
        var disabled = Permissions.InventoryModule.Where(_modules.IsDisabled).ToList();
        _db.RolePermissions.RemoveRange(await _db.RolePermissions
            .Where(rp => rp.RoleId == id && !requested.Contains(rp.PermissionCode) && !disabled.Contains(rp.PermissionCode))
            .ToListAsync(cancellationToken));
        foreach (var code in requested.Except(current))
            _db.RolePermissions.Add(RolePermission.Grant(id, code));
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RoleTemplateDto>> ListTemplatesAsync(CancellationToken cancellationToken)
    {
        var codes = RoleTemplates.Procurement.Select(t => t.Code).ToList();
        var existing = await _db.Roles.AsNoTracking().Where(r => codes.Contains(r.Code)).Select(r => r.Code).ToListAsync(cancellationToken);
        return RoleTemplates.Procurement
            .Select(t => new RoleTemplateDto(t.Code, t.Name, t.Description, _modules.Filter(t.Permissions).Distinct().ToList(), existing.Contains(t.Code)))
            .ToList();
    }

    /// <inheritdoc />
    public async Task<ApplyRoleTemplatesResult> CreateFromTemplatesAsync(CancellationToken cancellationToken)
    {
        var created = new List<string>();
        var skipped = new List<string>();
        foreach (var template in await ListTemplatesAsync(cancellationToken))
        {
            if (template.Exists)
            {
                skipped.Add($"{template.Code}: đã có, giữ nguyên quyền hiện tại");
                continue;
            }

            if (!_grants.WithinOwnPermissions(template.PermissionCodes))
            {
                skipped.Add($"{template.Code}: có quyền bạn không có");
                continue;
            }

            var role = Role.Create(template.Code, template.Name);
            _db.Roles.Add(role);
            foreach (var code in CheckPermissions(template.PermissionCodes))
                _db.RolePermissions.Add(RolePermission.Grant(role.Id, code));
            created.Add(role.Code);
        }

        // Một lần lưu: lỗi giữa chừng thì không tạo vai trò nào (bộ mẫu không bị thiếu nửa chừng).
        await _db.SaveChangesAsync(cancellationToken);
        return new ApplyRoleTemplatesResult(created, skipped);
    }

    /// <summary>
    /// Mã quyền phải có trong danh mục và nằm trong quyền của người thao tác (không cấp được quyền mình không có).
    /// </summary>
    private HashSet<string> CheckPermissions(IEnumerable<string> codes)
    {
        var requested = codes.ToHashSet(StringComparer.Ordinal);
        var unknown = Permissions.FindUnknown(requested);
        if (unknown.Count > 0)
            throw new BusinessRuleException($"Mã quyền không có trong danh mục: {string.Join(", ", unknown)}.");

        var notOwned = requested.Where(c => !_scope.Permissions.Contains(c)).Order(StringComparer.Ordinal).ToList();
        if (notOwned.Count > 0)
            throw new ForbiddenAccessException($"Bạn không được cấp quyền mà mình không có: {string.Join(", ", notOwned)}.");
        return requested;
    }
}
