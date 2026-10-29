using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Proc.Application.Auth;
using Proc.Application.Logging;
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
    private readonly IAccessLog _accessLog;

    /// <summary>
    /// Tạo dịch vụ đăng nhập.
    /// </summary>
    /// <param name="userManager">Quản lý tài khoản của Identity.</param>
    /// <param name="db">DbContext.</param>
    /// <param name="tokens">Dịch vụ cấp JWT.</param>
    /// <param name="accessLog">Nhật ký truy cập (design.md mục 9.3).</param>
    public AuthService(UserManager<AppUser> userManager, ProcDbContext db, JwtTokenService tokens, IAccessLog accessLog)
    {
        _userManager = userManager;
        _db = db;
        _tokens = tokens;
        _accessLog = accessLog;
    }

    /// <inheritdoc />
    public async Task<LoginOutcome> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var typedName = request.UserName.Trim();
        var user = await _userManager.FindByNameAsync(typedName);
        if (user is null)
        {
            // Nhật ký ghi rõ lý do để quản trị phát hiện dò tài khoản; người dùng chỉ thấy câu báo chung.
            await LogAsync(AccessEvent.LoginFailed, null, typedName, "Tên đăng nhập không tồn tại.", cancellationToken);
            return new LoginOutcome(null, LoginFailure.InvalidCredentials);
        }

        // Kiểm tra khóa trước mật khẩu: đang bị khóa thì mật khẩu đúng cũng không vào được.
        if (await _userManager.IsLockedOutAsync(user))
        {
            await LogAsync(AccessEvent.LockedOut, user.Id, user.UserName, "Đăng nhập khi tài khoản đang bị khóa tạm.", cancellationToken);
            return new LoginOutcome(null, LoginFailure.LockedOut, user.LockoutEnd);
        }

        if (!await _userManager.CheckPasswordAsync(user, request.Password))
        {
            // Tăng số lần sai; đủ MaxFailedAccessAttempts (5) thì Identity tự khóa tạm (cấu hình ở DependencyInjection).
            await WithConcurrencyRetryAsync(user, () => _userManager.AccessFailedAsync(user));
            if (await _userManager.IsLockedOutAsync(user))
            {
                await LogAsync(AccessEvent.LockedOut, user.Id, user.UserName,
                    $"Nhập sai mật khẩu {DependencyInjection.MaxFailedLoginAttempts} lần liên tiếp, khóa tạm.", cancellationToken);
                return new LoginOutcome(null, LoginFailure.LockedOut, user.LockoutEnd);
            }

            await LogAsync(AccessEvent.LoginFailed, user.Id, user.UserName, "Sai mật khẩu.", cancellationToken);
            return new LoginOutcome(null, LoginFailure.InvalidCredentials);
        }

        await WithConcurrencyRetryAsync(user, () => _userManager.ResetAccessFailedCountAsync(user));

        // Báo "ngừng sử dụng" chỉ sau khi mật khẩu đúng, để người ngoài không dò được tài khoản nào tồn tại.
        if (!user.IsActive)
        {
            await LogAsync(AccessEvent.LoginFailed, user.Id, user.UserName, "Tài khoản đã ngừng sử dụng.", cancellationToken);
            return new LoginOutcome(null, LoginFailure.Inactive);
        }

        var info = await BuildUserInfoAsync(user, cancellationToken);
        if (info is null)
        {
            await LogAsync(AccessEvent.LoginFailed, user.Id, user.UserName, "Chưa được gán vào công ty nào.", cancellationToken);
            return new LoginOutcome(null, LoginFailure.NoCompany);
        }

        var (token, expiresAt) = _tokens.CreateToken(user);
        await LogAsync(AccessEvent.Login, user.Id, user.UserName, null, cancellationToken);
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
        List<string> errors = result.Succeeded ? [] : result.Errors.Select(e => e.Description).ToList();
        await LogAsync(
            errors.Count == 0 ? AccessEvent.PasswordChanged : AccessEvent.PasswordChangeFailed,
            user.Id, user.UserName, errors.Count == 0 ? null : string.Join(" ", errors), cancellationToken);
        return errors;
    }

    /// <inheritdoc />
    public async Task<bool> SwitchCompanyAsync(Guid userId, Guid companyId, CancellationToken cancellationToken)
    {
        // Cùng điều kiện với danh sách công ty khi đăng nhập: công ty và vai trò đều đang dùng (design.md mục 9.4).
        var allowed = await _db.UserCompanyRoles.AnyAsync(
            x => x.UserId == userId && x.CompanyId == companyId && x.Company!.IsActive && x.Role!.IsActive,
            cancellationToken);
        if (!allowed) return false;

        var userName = await _db.Users.Where(u => u.Id == userId).Select(u => u.UserName).SingleOrDefaultAsync(cancellationToken);
        await _accessLog.WriteAsync(AccessEvent.CompanySwitched, userId, userName, companyId, null, cancellationToken);
        return true;
    }

    /// <inheritdoc />
    public async Task LogoutAsync(Guid userId, CancellationToken cancellationToken)
    {
        var userName = await _db.Users.Where(u => u.Id == userId).Select(u => u.UserName).SingleOrDefaultAsync(cancellationToken);
        await LogAsync(AccessEvent.Logout, userId, userName, null, cancellationToken);
    }

    /// <summary>
    /// Cập nhật bộ đếm sai mật khẩu, thử lại khi xung đột ghi: cùng tài khoản đăng nhập đồng thời (nhiều tab, nhiều máy)
    /// thì Identity báo ConcurrencyFailure vì ConcurrencyStamp đã đổi. Đọc lại tài khoản rồi làm lại, tối đa 3 lần;
    /// vẫn xung đột thì bỏ qua – bộ đếm lệch một lần không đáng làm hỏng việc đăng nhập.
    /// </summary>
    private async Task WithConcurrencyRetryAsync(AppUser user, Func<Task<IdentityResult>> update)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var result = await update();
            if (result.Succeeded || result.Errors.All(e => e.Code != nameof(IdentityErrorDescriber.ConcurrencyFailure))) return;
            // Đọc lại để có ConcurrencyStamp mới và bỏ trạng thái "đã sửa" còn treo trên DbContext.
            await _db.Entry(user).ReloadAsync();
        }
    }

    private Task LogAsync(AccessEvent accessEvent, Guid? userId, string? userName, string? detail, CancellationToken cancellationToken) =>
        _accessLog.WriteAsync(accessEvent, userId, userName, null, detail, cancellationToken);

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
