using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Proc.Application.Auth;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.Identity;

/// <summary>
/// Đăng nhập bằng ASP.NET Core Identity và cấp JWT.
/// </summary>
public sealed class AuthService : IAuthService
{
    private readonly UserManager<AppUser> _userManager;
    private readonly ProcDbContext _db;
    private readonly JwtTokenService _tokens;

    /// <summary>
    /// Tạo dịch vụ đăng nhập.
    /// </summary>
    /// <param name="userManager">Quản lý tài khoản của Identity.</param>
    /// <param name="db">DbContext.</param>
    /// <param name="tokens">Dịch vụ cấp JWT.</param>
    public AuthService(UserManager<AppUser> userManager, ProcDbContext db, JwtTokenService tokens)
    {
        _userManager = userManager;
        _db = db;
        _tokens = tokens;
    }

    /// <inheritdoc />
    public async Task<LoginOutcome> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByNameAsync(request.UserName.Trim());
        if (user is null) return new LoginOutcome(null, LoginFailure.InvalidCredentials);

        // Kiểm tra khóa trước mật khẩu: đang bị khóa thì mật khẩu đúng cũng không vào được.
        if (await _userManager.IsLockedOutAsync(user))
            return new LoginOutcome(null, LoginFailure.LockedOut, user.LockoutEnd);

        if (!await _userManager.CheckPasswordAsync(user, request.Password))
        {
            // Tăng số lần sai; đủ MaxFailedAccessAttempts (5) thì Identity tự khóa tạm (cấu hình ở DependencyInjection).
            await _userManager.AccessFailedAsync(user);
            return await _userManager.IsLockedOutAsync(user)
                ? new LoginOutcome(null, LoginFailure.LockedOut, user.LockoutEnd)
                : new LoginOutcome(null, LoginFailure.InvalidCredentials);
        }

        await _userManager.ResetAccessFailedCountAsync(user);

        // Báo "ngừng sử dụng" chỉ sau khi mật khẩu đúng, để người ngoài không dò được tài khoản nào tồn tại.
        if (!user.IsActive) return new LoginOutcome(null, LoginFailure.Inactive);

        var info = await BuildUserInfoAsync(user, cancellationToken);
        if (info is null) return new LoginOutcome(null, LoginFailure.NoCompany);

        var (token, expiresAt) = _tokens.CreateToken(user);
        return new LoginOutcome(new LoginResult(token, expiresAt, info), null);
    }

    /// <inheritdoc />
    public async Task<CurrentUserInfo?> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null || !user.IsActive) return null;
        return await BuildUserInfoAsync(user, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> ChangePasswordAsync(
        Guid userId,
        ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null || !user.IsActive) return ["Tài khoản không còn hiệu lực."];

        // Identity kiểm tra mật khẩu hiện tại và quy tắc mật khẩu mới; thông báo lỗi đã dịch ở VietnameseIdentityErrorDescriber.
        var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        return result.Succeeded ? [] : result.Errors.Select(e => e.Description).ToList();
    }

    /// <summary>
    /// Lấy các công ty người dùng được làm việc: có ít nhất một vai trò đang dùng tại công ty đang hoạt động.
    /// Thứ tự: tổng công ty trước, rồi theo mã. Kèm quyền tại từng công ty (hợp quyền của các vai trò).
    /// </summary>
    private async Task<CurrentUserInfo?> BuildUserInfoAsync(AppUser user, CancellationToken cancellationToken)
    {
        var rows = await _db.UserCompanyRoles
            .AsNoTracking()
            .Where(x => x.UserId == user.Id && x.Company!.IsActive && x.Role!.IsActive)
            .Select(x => new
            {
                x.Company!.Id,
                x.Company.Code,
                x.Company.Name,
                x.Company.ShortName,
                x.Company.IsHeadOffice,
                x.RoleId,
                RoleCode = x.Role!.Code,
            })
            .ToListAsync(cancellationToken);

        if (rows.Count == 0) return null;

        var roleIds = rows.Select(r => r.RoleId).Distinct().ToList();
        var permissionsByRole = (await _db.RolePermissions
                .AsNoTracking()
                .Where(rp => roleIds.Contains(rp.RoleId))
                .Select(rp => new { rp.RoleId, rp.PermissionCode })
                .ToListAsync(cancellationToken))
            .ToLookup(rp => rp.RoleId, rp => rp.PermissionCode);

        var companies = rows
            .GroupBy(r => r.Id)
            .Select(g =>
            {
                var c = g.First();
                return new UserCompanyInfo(c.Id, c.Code, c.Name, c.ShortName, c.IsHeadOffice,
                    g.Select(r => r.RoleCode).Distinct().Order().ToList(),
                    g.SelectMany(r => permissionsByRole[r.RoleId]).Distinct().Order(StringComparer.Ordinal).ToList());
            })
            .OrderByDescending(c => c.IsHeadOffice)
            .ThenBy(c => c.Code, StringComparer.Ordinal)
            .ToList();

        // Công ty mặc định đã bị gỡ quyền hoặc ngừng dùng thì lấy công ty đầu danh sách.
        var defaultCompanyId = companies.Any(c => c.Id == user.DefaultCompanyId)
            ? user.DefaultCompanyId!.Value
            : companies[0].Id;

        return new CurrentUserInfo(user.Id, user.UserName ?? string.Empty, user.FullName, defaultCompanyId, companies);
    }
}
