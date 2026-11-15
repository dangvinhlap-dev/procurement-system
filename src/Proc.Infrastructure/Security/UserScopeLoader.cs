using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Proc.Application.Common;
using Proc.Application.Operations;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.Security;

/// <summary>Kết quả nạp phạm vi.</summary>
public enum ScopeLoadResult
{
    /// <summary>Nạp xong.</summary>
    Ok,

    /// <summary>Tài khoản không còn hoặc đã ngừng dùng.</summary>
    UserInactive,

    /// <summary>Người dùng không có vai trò nào đang dùng tại công ty yêu cầu.</summary>
    CompanyNotAllowed,

    /// <summary>Token đã bị thu hồi: mật khẩu đã đổi / đặt lại (mã bảo mật khác) hoặc quản trị bấm "Đăng xuất tất cả" sau giờ cấp token (lát QT7).</summary>
    SessionRevoked,
}

/// <summary>Thông tin phiên lấy từ token để kiểm tra thu hồi (lát QT7).</summary>
/// <param name="SecurityStamp">Mã bảo mật của tài khoản lúc cấp token (claim <c>sstamp</c>); null = token cấp trước bản 0.35.0.</param>
/// <param name="IssuedAt">Giờ cấp token (claim <c>iat</c>, UTC).</param>
public sealed record SessionClaims(string? SecurityStamp, DateTimeOffset? IssuedAt);

/// <summary>
/// Nạp phạm vi của người dùng tại công ty đang làm việc vào <see cref="CurrentUserScope"/> (design.md mục 9.2).
/// Chỉ đọc các bảng phân quyền (không có global query filter), nên chạy được trước khi phạm vi có giá trị.
/// </summary>
public sealed class UserScopeLoader
{
    private readonly ProcDbContext _db;
    private readonly CurrentUserScope _scope;
    private readonly IAccessPolicyProvider _policy;

    /// <summary>
    /// Tạo bộ nạp phạm vi.
    /// </summary>
    /// <param name="db">DbContext.</param>
    /// <param name="scope">Phạm vi của request hiện tại.</param>
    /// <param name="policy">Cấu hình truy cập – mốc "Đăng xuất tất cả".</param>
    public UserScopeLoader(ProcDbContext db, CurrentUserScope scope, IAccessPolicyProvider policy)
    {
        _db = db;
        _scope = scope;
        _policy = policy;
    }

    /// <summary>
    /// Nạp phạm vi: quyền (hợp quyền của mọi vai trò tại công ty), kho được phép, nhóm hàng được phép kèm nhóm con.
    /// </summary>
    /// <param name="userId">Người dùng trong token.</param>
    /// <param name="companyId">Công ty từ header X-Company-Id; null nếu không gửi.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <param name="session">Thông tin phiên trong token – có thì kiểm tra thu hồi (request thật luôn có; test gọi thẳng bỏ qua được).</param>
    /// <returns>Kết quả nạp.</returns>
    public async Task<ScopeLoadResult> LoadAsync(Guid userId, Guid? companyId, CancellationToken cancellationToken, SessionClaims? session = null)
    {
        // Mã bảo mật đọc cùng truy vấn IsActive đã có – thu hồi phiên không tốn thêm lượt đọc CSDL (lát QT7).
        var user = await _db.Users
            .Where(u => u.Id == userId)
            .Select(u => new { u.IsActive, u.SecurityStamp })
            .SingleOrDefaultAsync(cancellationToken);
        if (user?.IsActive != true) return ScopeLoadResult.UserInactive;

        if (session is not null)
        {
            // Token cấp trước bản 0.35.0 không có mã bảo mật → đăng nhập lại một lần sau khi nâng cấp.
            if (session.SecurityStamp is null || !string.Equals(session.SecurityStamp, user.SecurityStamp, StringComparison.Ordinal))
                return ScopeLoadResult.SessionRevoked;
            var revokedAt = (await _policy.GetAsync(cancellationToken)).SessionsRevokedAt;
            if (revokedAt is { } mark && (session.IssuedAt is not { } issued || issued < mark)) return ScopeLoadResult.SessionRevoked;
        }

        if (companyId is not { } company)
        {
            _scope.Initialize(userId, null, [], false, [], false, []);
            return ScopeLoadResult.Ok;
        }

        var roleIds = await _db.UserCompanyRoles
            .Where(x => x.UserId == userId && x.CompanyId == company && x.Company!.IsActive && x.Role!.IsActive)
            .Select(x => x.RoleId)
            .ToListAsync(cancellationToken);
        if (roleIds.Count == 0) return ScopeLoadResult.CompanyNotAllowed;

        var permissions = await _db.RolePermissions
            .Where(rp => roleIds.Contains(rp.RoleId))
            .Select(rp => rp.PermissionCode)
            .Distinct()
            .ToListAsync(cancellationToken);

        // Không có dòng phạm vi kho nào tại công ty = được mọi kho của công ty (design.md mục 9.2).
        var warehouseIds = await _db.UserWarehouseScopes
            .Where(x => x.UserId == userId && x.CompanyId == company)
            .Select(x => x.WarehouseId)
            .ToListAsync(cancellationToken);

        var categoryRoots = await _db.UserItemCategoryScopes
            .Where(x => x.UserId == userId && x.CompanyId == company)
            .Select(x => x.ItemCategoryId)
            .ToArrayAsync(cancellationToken);
        var categoryIds = categoryRoots.Length == 0 ? [] : await ExpandCategoriesAsync(categoryRoots, cancellationToken);

        _scope.Initialize(
            userId,
            company,
            permissions,
            allWarehouses: warehouseIds.Count == 0,
            warehouseIds,
            allItemCategories: categoryRoots.Length == 0,
            categoryIds);
        return ScopeLoadResult.Ok;
    }

    /// <summary>
    /// Lấy các nhóm được gán kèm toàn bộ nhóm con các cấp: được phép nhóm cha thì được phép nhóm con (design.md mục 9.2).
    /// Dùng CTE đệ quy của PostgreSQL. Ví dụ: gán "VT" (Vật tư) với cây VT → VTD, VTC → VTCK thì trả {VT, VTD, VTC, VTCK}.
    /// </summary>
    private async Task<List<Guid>> ExpandCategoriesAsync(Guid[] roots, CancellationToken cancellationToken) =>
        await _db.Database
            .SqlQuery<Guid>($"""
                WITH RECURSIVE tree AS (
                    SELECT "Id" FROM "ItemCategory" WHERE "Id" = ANY({roots})
                    UNION
                    SELECT c."Id" FROM "ItemCategory" c JOIN tree ON c."ParentId" = tree."Id"
                )
                SELECT "Id" AS "Value" FROM tree
                """)
            .ToListAsync(cancellationToken);
}
