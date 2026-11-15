using System.Globalization;
using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Proc.Application.Common;
using Proc.Application.Operations;
using Proc.Domain.Common;
using Proc.Domain.Operations;
using Proc.Infrastructure.Identity;
using Proc.Infrastructure.Persistence;
using Proc.Infrastructure.Portal;

namespace Proc.Infrastructure.Operations;

/// <summary>
/// Nguồn chính sách truy cập (lát QT7). Singleton đệm <see cref="IAccessPolicyProvider.CacheSeconds"/> giây, mỗi lần đọc mở scope CSDL riêng (như
/// <see cref="MaintenanceModeService"/>). Đọc CSDL lỗi thì giữ bản đệm cũ (chưa có thì mặc định) – lỗi CSDL tự hiện ở chính request.
/// </summary>
public sealed class AccessPolicyProvider : IAccessPolicyProvider
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IClock _clock;
    private readonly SemaphoreSlim _refresh = new(1, 1);
    private volatile Holder? _cache;

    /// <summary>Tạo nguồn.</summary>
    /// <param name="scopes">Tạo scope CSDL.</param>
    /// <param name="clock">Đồng hồ.</param>
    public AccessPolicyProvider(IServiceScopeFactory scopes, IClock clock)
    {
        _scopes = scopes;
        _clock = clock;
    }

    /// <inheritdoc />
    public AccessPolicySnapshot Current => _cache?.Snapshot ?? AccessPolicySnapshot.Default;

    /// <inheritdoc />
    public async Task<AccessPolicySnapshot> GetAsync(CancellationToken cancellationToken)
    {
        if (Fresh() is { } cached) return cached;
        await _refresh.WaitAsync(cancellationToken);
        try
        {
            if (Fresh() is { } again) return again;
            var snapshot = Current;
            try
            {
                await using var scope = _scopes.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<ProcDbContext>();
                var row = await db.AccessPolicies.AsNoTracking().FirstOrDefaultAsync(p => p.Id == AccessPolicy.SingletonId, cancellationToken);
                snapshot = row is null ? AccessPolicySnapshot.Default : AccessPolicySnapshot.From(row);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Giữ bản cũ – CSDL dừng thì mọi request khác cũng lỗi, không cần khóa thêm vì chính sách.
            }

            _cache = new Holder(snapshot, _clock.UtcNow);
            return snapshot;
        }
        finally
        {
            _refresh.Release();
        }
    }

    /// <inheritdoc />
    public void Set(AccessPolicySnapshot snapshot) => _cache = new Holder(snapshot, _clock.UtcNow);

    private AccessPolicySnapshot? Fresh() =>
        _cache is { } c && _clock.UtcNow - c.LoadedAt < TimeSpan.FromSeconds(IAccessPolicyProvider.CacheSeconds) ? c.Snapshot : null;

    /// <summary>Bản đệm + giờ đọc (lớp để gán nguyên khối, luồng khác không thấy nửa cũ nửa mới).</summary>
    private sealed record Holder(AccessPolicySnapshot Snapshot, DateTimeOffset LoadedAt);
}

/// <summary>Màn hình CFG – cấu hình truy cập (lát QT7, design.md mục 21.16).</summary>
public sealed class AccessConfigService : IAccessConfigService
{
    private readonly ProcDbContext _db;
    private readonly IAccessPolicyProvider _provider;
    private readonly IRequestContext _request;
    private readonly ICurrentUser _currentUser;
    private readonly IDangerousActionGuard _guard;
    private readonly IOperationLog _operationLog;
    private readonly IClock _clock;
    private readonly JwtTokenService _tokens;
    private readonly UserManager<AppUser> _users;
    private readonly IOptionsMonitor<SecurityOptions> _security;
    private readonly PortalOptions _portal;
    private readonly OperationsOptions _operations;
    private readonly ICurrentUserScope _scope;

    /// <summary>Tạo dịch vụ.</summary>
    /// <param name="db">CSDL.</param>
    /// <param name="provider">Bản đệm chính sách.</param>
    /// <param name="request">IP người gọi.</param>
    /// <param name="currentUser">Người đang đăng nhập.</param>
    /// <param name="guard">Xác nhận việc nguy hiểm Q6.</param>
    /// <param name="operationLog">Nhật ký vận hành.</param>
    /// <param name="clock">Đồng hồ.</param>
    /// <param name="tokens">Cấp token mới sau "Đăng xuất tất cả".</param>
    /// <param name="users">Tài khoản.</param>
    /// <param name="security">Cấu hình bảo mật theo máy.</param>
    /// <param name="portal">Giới hạn tần suất Portal (hiện trên tab Bảo mật).</param>
    /// <param name="operations">Giới hạn gửi lỗi giao diện.</param>
    /// <param name="scope">Quyền người xem (danh sách mật khẩu cấm chỉ hiện cho người có quyền quản lý).</param>
    public AccessConfigService(
        ProcDbContext db, IAccessPolicyProvider provider, IRequestContext request, ICurrentUser currentUser, IDangerousActionGuard guard,
        IOperationLog operationLog, IClock clock, JwtTokenService tokens, UserManager<AppUser> users, IOptionsMonitor<SecurityOptions> security,
        IOptions<PortalOptions> portal, IOptions<OperationsOptions> operations, ICurrentUserScope scope)
    {
        _scope = scope;
        _db = db;
        _provider = provider;
        _request = request;
        _currentUser = currentUser;
        _guard = guard;
        _operationLog = operationLog;
        _clock = clock;
        _tokens = tokens;
        _users = users;
        _security = security;
        _portal = portal.Value;
        _operations = operations.Value;
    }

    /// <inheritdoc />
    public async Task<AccessConfigDto> GetAsync(bool isHttps, CancellationToken cancellationToken)
    {
        var row = await _db.AccessPolicies.AsNoTracking().FirstOrDefaultAsync(p => p.Id == AccessPolicy.SingletonId, cancellationToken);
        var policy = row ?? AccessPolicy.CreateDefault();
        var updatedBy = row?.UpdatedBy ?? row?.CreatedBy;
        var updatedByName = updatedBy is { } id
            ? await _db.Users.AsNoTracking().Where(u => u.Id == id).Select(u => u.UserName).FirstOrDefaultAsync(cancellationToken)
            : null;
        return new AccessConfigDto(
            policy.MinPasswordLength, policy.BlockCommonPasswords,
            // Mật khẩu cấm thêm thường là mẫu mật khẩu nội bộ hay dùng – người chỉ có quyền xem vận hành không cần thấy.
            _scope.HasPermission(Proc.Domain.Organization.Permissions.System.OpsConfigManage) ? policy.ExtraBlockedPasswords ?? string.Empty : null,
            policy.MaxFailedLogins, policy.LockoutMinutes,
            policy.SessionMinutes, policy.OpsAllowedIpRanges, policy.LoginPerMinutePerIp, policy.ApiPerMinutePerUser, policy.HeavyConcurrentPerUser,
            policy.SessionsRevokedAt, row?.UpdatedAt ?? row?.CreatedAt, updatedByName, PolicyPasswordValidator.CommonPasswords.Count,
            _request.IpAddress, row?.Id, SecurityRows(policy, isHttps));
    }

    /// <inheritdoc />
    public async Task<AccessConfigDto> SaveAsync(SaveAccessConfigRequest request, bool isHttps, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var reason = RequireReason(request.Reason, "đổi cấu hình truy cập");
        var row = await _db.AccessPolicies.FirstOrDefaultAsync(p => p.Id == AccessPolicy.SingletonId, cancellationToken);
        if (row is null)
        {
            row = AccessPolicy.CreateDefault();
            _db.AccessPolicies.Add(row);
        }

        var before = Describe(row);
        row.Apply(new AccessPolicySettings(
            request.MinPasswordLength, request.BlockCommonPasswords, request.ExtraBlockedPasswords, request.MaxFailedLogins, request.LockoutMinutes,
            request.SessionMinutes, request.OpsAllowedIpRanges, request.LoginPerMinutePerIp, request.ApiPerMinutePerUser,
            request.HeavyConcurrentPerUser));

        // Không tự khóa mình: dải mới phải gồm IP người đang lưu (loopback luôn được – sửa lại ngay trên máy chủ nếu cần).
        var ip = IPAddress.TryParse(_request.IpAddress, out var parsed) ? parsed : null;
        if (!AccessPolicy.IsIpAllowed(row.GetOpsIpRanges(), ip))
            throw new BusinessRuleException(
                $"Dải IP mới không gồm địa chỉ của bạn ({_request.IpAddress}) – lưu sẽ khóa chính bạn khỏi màn hình này. Thêm địa chỉ của bạn vào danh sách.");

        await _db.SaveChangesAsync(cancellationToken);
        _provider.Set(AccessPolicySnapshot.From(row));
        await _operationLog.WriteAsync(new OperationLogEntry(OperationKinds.AccessConfigChanged, "Cấu hình truy cập", reason,
            OperationResult.Succeeded, $"Trước: {before}. Sau: {Describe(row)}."), cancellationToken);
        return await GetAsync(isHttps, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<SignOutAllResult> SignOutAllAsync(SignOutAllRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var reason = RequireReason(request.Reason, "đăng xuất tất cả");
        await _guard.VerifyAsync("Đăng xuất tất cả", request.Password, request.Confirmation, IAccessConfigService.SignOutAllConfirmation,
            cancellationToken);
        var user = _currentUser.UserId is { } userId ? await _users.FindByIdAsync(userId.ToString()) : null;
        if (user is null) throw new ForbiddenAccessException("Phiên đăng nhập không còn hiệu lực.");

        var row = await _db.AccessPolicies.FirstOrDefaultAsync(p => p.Id == AccessPolicy.SingletonId, cancellationToken);
        if (row is null)
        {
            row = AccessPolicy.CreateDefault();
            _db.AccessPolicies.Add(row);
        }

        row.RevokeAllSessions(_clock.UtcNow);
        await _db.SaveChangesAsync(cancellationToken);
        var snapshot = AccessPolicySnapshot.From(row);
        _provider.Set(snapshot);

        // Token mới cấp sau mốc (iat = mốc, tròn giây) nên còn hiệu lực – người bấm không bị đăng xuất khỏi phiên đang dùng.
        var (token, expiresAt) = _tokens.CreateToken(user, snapshot.SessionMinutes);
        await _operationLog.WriteAsync(new OperationLogEntry(OperationKinds.SessionsRevoked, "Đăng xuất tất cả", reason, OperationResult.Succeeded,
            $"Mọi phiên đăng nhập cấp trước {TimeZoneInfo.ConvertTime(row.SessionsRevokedAt!.Value, VietnamTime.Zone):dd/MM/yyyy HH:mm:ss} hết hiệu lực."), cancellationToken);
        return new SignOutAllResult(row.SessionsRevokedAt!.Value, token, expiresAt);
    }

    /// <summary>Các dòng tab "Bảo mật": header, HTTPS, proxy tin cậy, giới hạn tần suất (chỉ đọc – đổi qua chính sách hoặc file cấu hình).</summary>
    private List<AccessSecurityRowDto> SecurityRows(AccessPolicy policy, bool isHttps)
    {
        const string Header = "Header bảo mật", Limit = "Giới hạn tần suất", Network = "Mạng";
        var security = _security.CurrentValue;
        var hsts = security.HstsMaxAgeDays > 0;
        var proxies = security.KnownProxies.Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
        List<AccessSecurityRowDto> rows =
        [
            new(Header, "Content-Security-Policy", SecurityHeaderValues.ContentSecurityPolicy, "Ok",
                "Chỉ nạp script, ảnh, kết nối từ chính máy chủ; trang ngoài không nhúng được phần mềm."),
            new(Header, "X-Content-Type-Options", "nosniff", "Ok", "File đính kèm không bị trình duyệt chạy như trang web."),
            new(Header, "X-Frame-Options", "SAMEORIGIN", "Ok", "Chống bấm lừa (trình duyệt cũ không hiểu CSP)."),
            new(Header, "Referrer-Policy", SecurityHeaderValues.ReferrerPolicy, "Ok", "Không gửi đường dẫn sang trang khác."),
            new(Header, "Permissions-Policy", SecurityHeaderValues.PermissionsPolicy, "Ok", "Tắt camera, micro, vị trí, thanh toán, USB."),
            new(Header, "Strict-Transport-Security (HSTS)",
                hsts ? $"max-age = {security.HstsMaxAgeDays} ngày, chỉ gửi khi HTTPS" : "Tắt (Security:HstsMaxAgeDays = 0)",
                !hsts ? "Warning" : isHttps ? "Ok" : "Warning",
                isHttps ? "Đang truy cập bằng HTTPS." : "Đang truy cập bằng HTTP – TEST, PROD phải dùng HTTPS (design.md 21.3a)."),
            new(Network, "Dải IP được dùng chức năng quản trị vận hành",
                policy.OpsAllowedIpRanges?.Replace("\n", ", ", StringComparison.Ordinal) ?? "Mọi IP",
                policy.OpsAllowedIpRanges is null ? "Warning" : "Ok",
                policy.OpsAllowedIpRanges is null
                    ? "Chưa giới hạn – TEST, PROD nên khai dải IP máy quản trị (tab Chính sách)."
                    : "Máy chủ tự gọi chính nó (127.0.0.1) luôn được – để sửa lại ngay trên máy chủ khi lỡ khai sai."),
            new(Network, "Proxy được tin (đọc IP thật)", proxies.Count == 0 ? "Không có – lấy IP từ kết nối" : string.Join(", ", proxies), "Neutral",
                "Security:KnownProxies trong appsettings; IIS in-process không cần, Docker + Caddy khai dải mạng Docker."),
            new(Network, "/health khi chưa đăng nhập", "Chỉ trạng thái + phiên bản", "Ok", "Chi tiết từng kiểm tra chỉ hiện cho người có quyền xem vận hành."),
            new(Limit, "Đăng nhập nội bộ", $"{Fmt(policy.LoginPerMinutePerIp)} lần / phút / IP", "Ok",
                "Theo tên đăng nhập: khóa tạm khi sai mật khẩu liên tiếp (tab Chính sách). Máy chủ tự gọi (127.0.0.1) không giới hạn."),
            new(Limit, "API nội bộ", $"{Fmt(policy.ApiPerMinutePerUser)} lần / phút / phiên", "Ok", "Vượt thì báo 429 kèm số giây chờ."),
            new(Limit, "Việc nặng (Excel, in PDF, báo cáo)", $"{Fmt(policy.HeavyConcurrentPerUser)} việc cùng lúc / phiên", "Ok",
                "Việc thứ tiếp theo báo 429 ngay, không xếp hàng."),
            new(Limit, "Portal NCC – đăng nhập, OTP", $"{Fmt(_portal.RateLimit.AuthPermitPerMinute)} lần / phút / IP", "Neutral",
                "Portal:RateLimit:AuthPermitPerMinute trong appsettings."),
            new(Limit, "Portal NCC – API khác", $"{Fmt(_portal.RateLimit.ApiPermitPerMinute)} lần / phút / IP", "Neutral",
                "Portal:RateLimit:ApiPermitPerMinute trong appsettings."),
            new(Limit, "Gửi lỗi giao diện", $"{Fmt(_operations.ClientErrorPermitPerMinute)} lần / phút / phiên", "Neutral",
                "Operations:ClientErrorPermitPerMinute trong appsettings."),
        ];
        if (security.IgnoreOpsIpRanges)
            rows.Insert(0, new(Network, "Đang bỏ kiểm tra dải IP quản trị", "Security:IgnoreOpsIpRanges = true", "Error",
                "Cách gỡ khi lỡ khóa mình đang bật trong file cấu hình máy chủ – mọi IP vào được chức năng quản trị vận hành. Sửa dải IP trên CFG rồi " +
                "đặt lại false."));
        return rows;
    }

    private static string Fmt(int value) => value.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"));

    /// <summary>Tóm tắt một dòng cho nhật ký vận hành (không ghi danh sách mật khẩu cấm – chỉ số mục).</summary>
    private static string Describe(AccessPolicy p) =>
        $"mật khẩu ≥ {p.MinPasswordLength} ký tự, chặn phổ biến {(p.BlockCommonPasswords ? "bật" : "tắt")}, " +
        $"cấm thêm {AccessPolicy.ParseBlockedPasswords(p.ExtraBlockedPasswords).Count} mục, sai {p.MaxFailedLogins} lần khóa {p.LockoutMinutes} phút, " +
        $"phiên {p.SessionMinutes} phút, IP quản trị {p.OpsAllowedIpRanges?.Replace("\n", ", ", StringComparison.Ordinal) ?? "mọi IP"}, " +
        $"đăng nhập {p.LoginPerMinutePerIp}/phút/IP, API {p.ApiPerMinutePerUser}/phút/phiên, việc nặng {p.HeavyConcurrentPerUser}";

    private static string RequireReason(string? reason, string action)
    {
        reason = reason?.Trim();
        if (reason is null || reason.Length < IAccessConfigService.ReasonMinLength)
            throw new BusinessRuleException($"Nhập lý do {action} (ít nhất {IAccessConfigService.ReasonMinLength} ký tự).");
        return reason.Length > 500 ? reason[..500] : reason;
    }
}
