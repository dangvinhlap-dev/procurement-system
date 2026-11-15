using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Proc.Application.Common;
using Proc.Application.Auth;
using Proc.Application.Logging;
using Proc.Application.Operations;
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
    private readonly IAccessPolicyProvider _policy;
    private readonly IClock _clock;

    /// <summary>
    /// Tạo dịch vụ đăng nhập.
    /// </summary>
    /// <param name="userManager">Quản lý tài khoản của Identity.</param>
    /// <param name="db">DbContext.</param>
    /// <param name="tokens">Dịch vụ cấp JWT.</param>
    /// <param name="accessLog">Nhật ký truy cập (design.md mục 9.3).</param>
    /// <param name="policy">Cấu hình truy cập CFG: số lần sai, phút khóa, hạn phiên (lát QT7).</param>
    /// <param name="clock">Đồng hồ.</param>
    public AuthService(
        UserManager<AppUser> userManager, ProcDbContext db, JwtTokenService tokens, IAccessLog accessLog, IAccessPolicyProvider policy,
        IClock clock)
    {
        _userManager = userManager;
        _db = db;
        _tokens = tokens;
        _accessLog = accessLog;
        _policy = policy;
        _clock = clock;
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

        var policy = await _policy.GetAsync(cancellationToken);
        if (!await _userManager.CheckPasswordAsync(user, request.Password))
        {
            // Tăng số lần sai; đủ số lần của cấu hình truy cập CFG (mặc định 5) thì khóa tạm số phút của CFG (lát QT7 – thay số cố định của
            // Identity để đổi được trên màn hình).
            await WithConcurrencyRetryAsync(user, () => RegisterFailedAsync(user, policy));
            if (await _userManager.IsLockedOutAsync(user))
            {
                await LogAsync(AccessEvent.LockedOut, user.Id, user.UserName,
                    $"Nhập sai mật khẩu {policy.MaxFailedLogins} lần liên tiếp, tạm ngưng đăng nhập {policy.LockoutMinutes} phút.",
                    cancellationToken);
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

        var (token, expiresAt) = _tokens.CreateToken(user, policy.SessionMinutes);
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
    public async Task<ChangePasswordOutcome> ChangePasswordAsync(
        Guid userId,
        ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null || !user.IsActive) return new ChangePasswordOutcome(["Tài khoản không còn hiệu lực."], null);

        // Identity kiểm tra mật khẩu hiện tại và quy tắc mật khẩu mới (gồm PolicyPasswordValidator – lát QT7); thông báo lỗi đã dịch ở
        // VietnameseIdentityErrorDescriber. Đổi thành công thì Identity đổi luôn mã bảo mật → mọi token cũ của người này bị từ chối.
        var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        List<string> errors = result.Succeeded ? [] : result.Errors.Select(e => e.Description).ToList();
        await LogAsync(
            errors.Count == 0 ? AccessEvent.PasswordChanged : AccessEvent.PasswordChangeFailed,
            user.Id, user.UserName, errors.Count == 0 ? null : string.Join(" ", errors), cancellationToken);
        if (errors.Count > 0) return new ChangePasswordOutcome(errors, null);

        var info = await BuildUserInfoAsync(user, cancellationToken);
        if (info is null) return new ChangePasswordOutcome([], null);
        var policy = await _policy.GetAsync(cancellationToken);
        var (token, expiresAt) = _tokens.CreateToken(user, policy.SessionMinutes);
        return new ChangePasswordOutcome([], new LoginResult(token, expiresAt, info));
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

    /// <summary>
    /// Ghi một lần sai mật khẩu theo cấu hình truy cập: đủ <see cref="AccessPolicySnapshot.MaxFailedLogins"/> lần liên tiếp thì khóa tới
    /// bây giờ + <see cref="AccessPolicySnapshot.LockoutMinutes"/> phút và đếm lại từ 0 (như Identity làm với số cố định). Ví dụ 5 lần, 10 phút:
    /// sai lần thứ 5 lúc 09:00 → khóa tới 09:10.
    /// </summary>
    private async Task<IdentityResult> RegisterFailedAsync(AppUser user, AccessPolicySnapshot policy)
    {
        user.AccessFailedCount++;
        if (user.AccessFailedCount >= policy.MaxFailedLogins)
        {
            user.LockoutEnd = _clock.UtcNow.AddMinutes(policy.LockoutMinutes);
            user.AccessFailedCount = 0;
        }

        return await _userManager.UpdateAsync(user);
    }

    /// <summary>Ghi nhật ký truy cập (đăng nhập, đăng xuất, sai mật khẩu…).</summary>
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
