using Microsoft.EntityFrameworkCore;
using Proc.Application.Common;
using Proc.Domain.Organization;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.Administration;

/// <summary>
/// Kiểm tra chống tự nâng quyền khi quản lý người dùng và vai trò (design.md mục 9.5).
/// Quy tắc chung: người thao tác chỉ cấp, gỡ hoặc tác động tới những gì nằm trong quyền chính họ đang có,
/// để người có quyền "quản lý người dùng" không tự cấp cho mình (hay cho tài khoản phụ) quyền cao hơn.
/// </summary>
internal sealed class GrantChecker
{
    private readonly ProcDbContext _db;
    private readonly ICurrentUserScope _scope;

    /// <summary>
    /// Tạo bộ kiểm tra.
    /// </summary>
    /// <param name="db">DbContext.</param>
    /// <param name="scope">Phạm vi của người thao tác.</param>
    public GrantChecker(ProcDbContext db, ICurrentUserScope scope)
    {
        _db = db;
        _scope = scope;
    }

    /// <summary>
    /// Lấy quyền của từng vai trò.
    /// </summary>
    /// <param name="roleIds">Vai trò cần lấy.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Vai trò → tập mã quyền (vai trò không có quyền nào vẫn có mặt với tập rỗng).</returns>
    public async Task<Dictionary<Guid, HashSet<string>>> GetRolePermissionsAsync(
        IReadOnlyCollection<Guid> roleIds,
        CancellationToken cancellationToken)
    {
        var rows = await _db.RolePermissions
            .AsNoTracking()
            .Where(rp => roleIds.Contains(rp.RoleId))
            .Select(rp => new { rp.RoleId, rp.PermissionCode })
            .ToListAsync(cancellationToken);
        var lookup = rows.ToLookup(r => r.RoleId, r => r.PermissionCode);
        return roleIds.Distinct().ToDictionary(id => id, id => lookup[id].ToHashSet(StringComparer.Ordinal));
    }

    /// <summary>
    /// Mọi quyền trong tập đều có trong quyền của người thao tác tại công ty đang làm việc.
    /// </summary>
    /// <param name="permissions">Tập quyền cần so.</param>
    /// <returns>true nếu nằm gọn trong quyền của người thao tác.</returns>
    public bool WithinOwnPermissions(IEnumerable<string> permissions) => permissions.All(_scope.Permissions.Contains);

    /// <summary>
    /// Người thao tác có "trùm" được tài khoản ở mọi công ty tài khoản đó được gán không: tại mỗi công ty phải có quyền
    /// quản lý người dùng và có đủ mọi quyền của các vai trò tài khoản đó đang giữ. Dùng cho thao tác có hiệu lực ở mọi công ty
    /// (sửa thông tin, ngừng dùng, đặt lại mật khẩu) – ví dụ quản lý người dùng của BHA không đặt lại được mật khẩu của
    /// quản trị tổng công ty rồi đăng nhập thay họ. Tài khoản chưa gán công ty nào thì ai có quyền quản lý người dùng cũng sửa được.
    /// </summary>
    /// <param name="targetUserId">Tài khoản bị tác động.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>true nếu được phép.</returns>
    public async Task<bool> CanManageAccountAsync(Guid targetUserId, CancellationToken cancellationToken)
    {
        if (_scope.UserId is not { } callerId || callerId == targetUserId) return false;

        var target = await _db.UserCompanyRoles
            .AsNoTracking()
            .Where(x => x.UserId == targetUserId)
            .Select(x => new { x.CompanyId, x.RoleId })
            .ToListAsync(cancellationToken);
        if (target.Count == 0) return true;

        // Quyền của người thao tác tại từng công ty: chỉ tính công ty và vai trò đang hoạt động, giống lúc nạp phạm vi.
        var caller = await _db.UserCompanyRoles
            .AsNoTracking()
            .Where(x => x.UserId == callerId && x.Company!.IsActive && x.Role!.IsActive)
            .Select(x => new { x.CompanyId, x.RoleId })
            .ToListAsync(cancellationToken);

        var rolePermissions = await GetRolePermissionsAsync(
            target.Select(t => t.RoleId).Concat(caller.Select(c => c.RoleId)).Distinct().ToList(), cancellationToken);
        var callerByCompany = caller
            .GroupBy(c => c.CompanyId)
            .ToDictionary(g => g.Key, g => g.SelectMany(c => rolePermissions[c.RoleId]).ToHashSet(StringComparer.Ordinal));

        return target.All(t =>
            callerByCompany.TryGetValue(t.CompanyId, out var own)
            && own.Contains(Permissions.System.UserManage)
            && rolePermissions[t.RoleId].IsSubsetOf(own));
    }
}
