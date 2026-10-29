using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Proc.Application.Administration;
using Proc.Application.Common;
using Proc.Application.Organization;
using Proc.Domain.Common;
using Proc.Domain.Organization;
using Proc.Infrastructure.Identity;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.Administration;

/// <summary>
/// Quản lý người dùng qua ASP.NET Core Identity và các bảng phân quyền. Quy tắc chống tự nâng quyền xem
/// <see cref="IUserAdministration"/> và design.md mục 9.5. Kho, nhóm hàng đọc qua global query filter nên chỉ cấp được
/// những gì người thao tác thấy (CLAUDE.md mục 4).
/// </summary>
public sealed class UserAdministration : IUserAdministration
{
    private readonly ProcDbContext _db;
    private readonly UserManager<AppUser> _userManager;
    private readonly ICurrentUserScope _scope;
    private readonly IOrganizationQueries _organization;
    private readonly IClock _clock;
    private readonly GrantChecker _grants;

    /// <summary>
    /// Tạo dịch vụ quản lý người dùng.
    /// </summary>
    /// <param name="db">DbContext.</param>
    /// <param name="userManager">Quản lý tài khoản của Identity.</param>
    /// <param name="scope">Phạm vi của người thao tác.</param>
    /// <param name="organization">Truy vấn kho, nhóm hàng (đã lọc theo phạm vi).</param>
    /// <param name="clock">Đồng hồ.</param>
    public UserAdministration(
        ProcDbContext db,
        UserManager<AppUser> userManager,
        ICurrentUserScope scope,
        IOrganizationQueries organization,
        IClock clock)
    {
        _db = db;
        _userManager = userManager;
        _scope = scope;
        _organization = organization;
        _clock = clock;
        _grants = new GrantChecker(db, scope);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<UserListItemDto>> ListAsync(CancellationToken cancellationToken)
    {
        var companyId = _scope.RequireCompany();
        var now = _clock.UtcNow;

        var users = await _db.Users
            .AsNoTracking()
            .OrderBy(u => u.UserName)
            .Select(u => new { u.Id, u.UserName, u.FullName, u.Email, u.IsActive, u.LockoutEnd, u.CreatedAt })
            .ToListAsync(cancellationToken);
        var assignments = (await _db.UserCompanyRoles
                .AsNoTracking()
                .Select(x => new { x.UserId, x.CompanyId, CompanyCode = x.Company!.Code, RoleName = x.Role!.Name })
                .ToListAsync(cancellationToken))
            .ToLookup(x => x.UserId);

        return users
            .Select(u => new UserListItemDto(
                u.Id,
                u.UserName ?? string.Empty,
                u.FullName,
                u.Email,
                u.IsActive,
                u.LockoutEnd > now,
                assignments[u.Id].Where(a => a.CompanyId == companyId).Select(a => a.RoleName).Order().ToList(),
                assignments[u.Id].Select(a => a.CompanyCode).Distinct().Order(StringComparer.Ordinal).ToList(),
                u.CreatedAt))
            .ToList();
    }

    /// <inheritdoc />
    public async Task<UserDetailDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var companyId = _scope.RequireCompany();
        var user = await _db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == id, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy người dùng.");

        var access = await LoadAccessAsync(id, companyId, cancellationToken);
        var companyCodes = await _db.UserCompanyRoles
            .AsNoTracking()
            .Where(x => x.UserId == id)
            .Select(x => x.Company!.Code)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync(cancellationToken);

        return new UserDetailDto(
            user.Id,
            user.UserName ?? string.Empty,
            user.FullName,
            user.Email,
            user.IsActive,
            user.LockoutEnd > _clock.UtcNow ? user.LockoutEnd : null,
            user.CreatedAt,
            await _grants.CanManageAccountAsync(id, cancellationToken),
            id == _scope.UserId,
            access,
            companyCodes);
    }

    /// <inheritdoc />
    public async Task<UserAccessOptionsDto> GetAccessOptionsAsync(CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        var roles = await _db.Roles
            .AsNoTracking()
            .Where(r => r.IsActive)
            .OrderBy(r => r.Code == Role.AdminCode ? 0 : 1)
            .ThenBy(r => r.Code)
            .Select(r => new { r.Id, r.Code, r.Name })
            .ToListAsync(cancellationToken);
        var permissions = await _grants.GetRolePermissionsAsync(roles.Select(r => r.Id).ToList(), cancellationToken);

        return new UserAccessOptionsDto(
            roles.Select(r => new AssignableRoleDto(r.Id, r.Code, r.Name, _grants.WithinOwnPermissions(permissions[r.Id]))).ToList(),
            await _organization.GetWarehousesAsync(cancellationToken),
            await _organization.GetItemCategoriesAsync(cancellationToken),
            MustLimitWarehouses: !_scope.AllWarehouses,
            MustLimitItemCategories: !_scope.AllItemCategories);
    }

    /// <inheritdoc />
    public async Task<UserDetailDto> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken)
    {
        var companyId = _scope.RequireCompany();

        // Tạo tài khoản và gán quyền trong một giao dịch: gán quyền lỗi thì không để lại tài khoản dở dang.
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        var user = new AppUser
        {
            UserName = request.UserName.Trim(),
            FullName = request.FullName.Trim(),
            Email = NormalizeEmail(request.Email),
            CreatedAt = _clock.UtcNow,
            DefaultCompanyId = request.Access.RoleIds.Count > 0 ? companyId : null,
        };
        ThrowIfFailed(await _userManager.CreateAsync(user, request.Password));

        await ApplyAccessAsync(user.Id, companyId, request.Access, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetAsync(user.Id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<UserDetailDto> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        var user = await FindUserAsync(id);

        if (id == _scope.UserId)
        {
            // Tự sửa họ tên, email thì được; tự ngừng dùng thì không, để không tự khóa mình ra ngoài.
            if (!request.IsActive)
                throw new BusinessRuleException("Không được tự ngừng sử dụng tài khoản của mình.");
        }
        else
        {
            await EnsureCanManageAccountAsync(id, cancellationToken);
        }

        user.FullName = request.FullName.Trim();
        user.Email = NormalizeEmail(request.Email);
        user.IsActive = request.IsActive;
        ThrowIfFailed(await _userManager.UpdateAsync(user));
        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task ResetPasswordAsync(Guid id, ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        if (id == _scope.UserId)
            throw new BusinessRuleException("Hãy dùng chức năng Đổi mật khẩu cho tài khoản của mình.");
        var user = await FindUserAsync(id);
        await EnsureCanManageAccountAsync(id, cancellationToken);

        // Kiểm tra quy tắc mật khẩu (design.md mục 9.4) trước khi thay, để mật khẩu cũ còn nguyên nếu mật khẩu mới không đạt.
        var errors = new List<IdentityError>();
        foreach (var validator in _userManager.PasswordValidators)
        {
            var result = await validator.ValidateAsync(_userManager, user, request.NewPassword);
            errors.AddRange(result.Errors);
        }
        if (errors.Count > 0) ThrowIfFailed(IdentityResult.Failed([.. errors]));

        user.PasswordHash = _userManager.PasswordHasher.HashPassword(user, request.NewPassword);
        // Đặt lại mật khẩu thường vì người dùng bị khóa tạm: mở khóa luôn.
        user.LockoutEnd = null;
        user.AccessFailedCount = 0;
        ThrowIfFailed(await _userManager.UpdateSecurityStampAsync(user));
    }

    /// <inheritdoc />
    public async Task<UserDetailDto> SetAccessAsync(Guid id, UserAccessDto access, CancellationToken cancellationToken)
    {
        var companyId = _scope.RequireCompany();
        if (id == _scope.UserId)
            throw new BusinessRuleException("Không được tự sửa quyền của chính mình.");
        await FindUserAsync(id);

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        await ApplyAccessAsync(id, companyId, access, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <summary>
    /// Thay vai trò, kho, nhóm hàng của người dùng tại một công ty sau khi kiểm tra chống tự nâng quyền (design.md mục 9.5).
    /// </summary>
    private async Task ApplyAccessAsync(Guid userId, Guid companyId, UserAccessDto access, CancellationToken cancellationToken)
    {
        var current = await LoadAccessAsync(userId, companyId, cancellationToken);
        var roleIds = access.RoleIds.Distinct().ToList();
        await CheckRolesAsync(current.RoleIds, roleIds, cancellationToken);

        // Không còn vai trò nào = gỡ khỏi công ty, phạm vi kho và nhóm hàng tại đó cũng bỏ theo.
        var hadAccess = current.RoleIds.Count > 0;
        var warehouseIds = roleIds.Count == 0 ? [] : access.WarehouseIds.Distinct().ToList();
        var categoryIds = roleIds.Count == 0 ? [] : access.ItemCategoryIds.Distinct().ToList();

        var visibleWarehouses = await _db.Warehouses.Select(w => w.Id).ToListAsync(cancellationToken);
        CheckScope(
            warehouseIds, current.WarehouseIds, visibleWarehouses, roleIds.Count > 0, _scope.AllWarehouses,
            keepsAll: hadAccess && current.WarehouseIds.Count == 0,
            outsideMessage: "Có kho không thuộc phạm vi của bạn tại công ty đang làm việc.",
            mustLimitMessage: "Bạn chỉ được làm việc với một số kho, nên phải chọn kho cụ thể cho người dùng.");

        var visibleCategories = await _db.ItemCategories.Select(c => c.Id).ToListAsync(cancellationToken);
        CheckScope(
            categoryIds, current.ItemCategoryIds, visibleCategories, roleIds.Count > 0, _scope.AllItemCategories,
            keepsAll: hadAccess && current.ItemCategoryIds.Count == 0,
            outsideMessage: "Có nhóm hàng không thuộc phạm vi của bạn tại công ty đang làm việc.",
            mustLimitMessage: "Bạn chỉ được làm việc với một số nhóm hàng, nên phải chọn nhóm hàng cụ thể cho người dùng.");

        _db.UserCompanyRoles.RemoveRange(await _db.UserCompanyRoles
            .Where(x => x.UserId == userId && x.CompanyId == companyId && !roleIds.Contains(x.RoleId))
            .ToListAsync(cancellationToken));
        foreach (var roleId in roleIds.Except(current.RoleIds))
            _db.UserCompanyRoles.Add(UserCompanyRole.Assign(userId, companyId, roleId));

        _db.UserWarehouseScopes.RemoveRange(await _db.UserWarehouseScopes
            .Where(x => x.UserId == userId && x.CompanyId == companyId && !warehouseIds.Contains(x.WarehouseId))
            .ToListAsync(cancellationToken));
        var newWarehouseIds = warehouseIds.Except(current.WarehouseIds).ToList();
        foreach (var warehouse in await _db.Warehouses.Where(w => newWarehouseIds.Contains(w.Id)).ToListAsync(cancellationToken))
            _db.UserWarehouseScopes.Add(UserWarehouseScope.Allow(userId, warehouse));

        _db.UserItemCategoryScopes.RemoveRange(await _db.UserItemCategoryScopes
            .Where(x => x.UserId == userId && x.CompanyId == companyId && !categoryIds.Contains(x.ItemCategoryId))
            .ToListAsync(cancellationToken));
        foreach (var categoryId in categoryIds.Except(current.ItemCategoryIds))
            _db.UserItemCategoryScopes.Add(UserItemCategoryScope.Allow(userId, companyId, categoryId));

        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Vai trò thêm phải đang dùng; vai trò thêm hoặc gỡ phải có quyền nằm trong quyền của người thao tác.
    /// Gỡ cũng bị chặn để người có ít quyền không hạ quyền được người có nhiều quyền hơn mình.
    /// </summary>
    private async Task CheckRolesAsync(IReadOnlyList<Guid> current, IReadOnlyList<Guid> requested, CancellationToken cancellationToken)
    {
        var changed = requested.Except(current).Concat(current.Except(requested)).ToList();
        if (changed.Count == 0) return;

        var roles = await _db.Roles.AsNoTracking().Where(r => changed.Contains(r.Id)).ToListAsync(cancellationToken);
        var added = requested.Except(current).ToHashSet();
        if (roles.Count != changed.Count) throw new BusinessRuleException("Có vai trò không tồn tại.");
        if (roles.FirstOrDefault(r => added.Contains(r.Id) && !r.IsActive) is { } inactive)
            throw new BusinessRuleException($"Vai trò {inactive.Name} đã ngừng sử dụng.");

        var permissions = await _grants.GetRolePermissionsAsync(changed, cancellationToken);
        if (roles.FirstOrDefault(r => !_grants.WithinOwnPermissions(permissions[r.Id])) is { } tooHigh)
        {
            throw new ForbiddenAccessException(
                $"Bạn không được gán hoặc gỡ vai trò {tooHigh.Name} vì vai trò này có quyền mà bạn không có.");
        }
    }

    /// <summary>
    /// Kiểm tra danh sách kho (hoặc nhóm hàng) cấp cho người dùng:
    /// <list type="bullet">
    /// <item>Mỗi mục phải nằm trong phạm vi người thao tác thấy, hoặc người dùng vốn đã có (giữ nguyên thì không phải nâng quyền).</item>
    /// <item>Danh sách rỗng nghĩa là "tất cả"; người thao tác bị giới hạn thì không được cấp "tất cả",
    /// trừ khi người dùng vốn đã có "tất cả" tại công ty này.</item>
    /// </list>
    /// </summary>
    private static void CheckScope(
        IReadOnlyList<Guid> requested,
        IReadOnlyList<Guid> current,
        IReadOnlyList<Guid> visible,
        bool hasRoles,
        bool callerHasAll,
        bool keepsAll,
        string outsideMessage,
        string mustLimitMessage)
    {
        var allowed = visible.Concat(current).ToHashSet();
        if (requested.Any(id => !allowed.Contains(id))) throw new BusinessRuleException(outsideMessage);
        if (hasRoles && requested.Count == 0 && !callerHasAll && !keepsAll) throw new BusinessRuleException(mustLimitMessage);
    }

    /// <summary>Vai trò, kho, nhóm hàng hiện có của người dùng tại một công ty (đọc thẳng bảng phân quyền).</summary>
    private async Task<UserAccessDto> LoadAccessAsync(Guid userId, Guid companyId, CancellationToken cancellationToken)
    {
        var roleIds = await _db.UserCompanyRoles.AsNoTracking()
            .Where(x => x.UserId == userId && x.CompanyId == companyId)
            .Select(x => x.RoleId)
            .ToListAsync(cancellationToken);
        var warehouseIds = await _db.UserWarehouseScopes.AsNoTracking()
            .Where(x => x.UserId == userId && x.CompanyId == companyId)
            .Select(x => x.WarehouseId)
            .ToListAsync(cancellationToken);
        var categoryIds = await _db.UserItemCategoryScopes.AsNoTracking()
            .Where(x => x.UserId == userId && x.CompanyId == companyId)
            .Select(x => x.ItemCategoryId)
            .ToListAsync(cancellationToken);
        return new UserAccessDto(roleIds, warehouseIds, categoryIds);
    }

    private async Task EnsureCanManageAccountAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!await _grants.CanManageAccountAsync(id, cancellationToken))
        {
            throw new ForbiddenAccessException(
                "Bạn không quản lý được tài khoản này: người này có quyền cao hơn bạn hoặc làm việc ở công ty bạn không quản lý.");
        }
    }

    private async Task<AppUser> FindUserAsync(Guid id) =>
        await _userManager.FindByIdAsync(id.ToString()) ?? throw new NotFoundException("Không tìm thấy người dùng.");

    private static string? NormalizeEmail(string? email) => string.IsNullOrWhiteSpace(email) ? null : email.Trim();

    /// <summary>Lỗi của Identity (đã dịch tiếng Việt ở VietnameseIdentityErrorDescriber) chuyển thành lỗi nghiệp vụ 400.</summary>
    private static void ThrowIfFailed(IdentityResult result)
    {
        if (!result.Succeeded) throw new BusinessRuleException(string.Join(" ", result.Errors.Select(e => e.Description)));
    }
}
