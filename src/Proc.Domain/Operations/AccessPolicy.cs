using System.Net;
using Proc.Domain.Common;

namespace Proc.Domain.Operations;

/// <summary>
/// Các giá trị người quản trị nhập trên màn hình CFG (lát QT7, design.md mục 21.16).
/// </summary>
/// <param name="MinPasswordLength">Độ dài mật khẩu tối thiểu.</param>
/// <param name="BlockCommonPasswords">Chặn mật khẩu phổ biến (danh sách nhúng trong phần mềm).</param>
/// <param name="ExtraBlockedPasswords">Mật khẩu cấm thêm, mỗi dòng một mật khẩu.</param>
/// <param name="MaxFailedLogins">Số lần nhập sai mật khẩu liên tiếp thì tạm khóa đăng nhập.</param>
/// <param name="LockoutMinutes">Số phút tạm khóa.</param>
/// <param name="SessionMinutes">Hạn phiên đăng nhập (phút) – hạn của token.</param>
/// <param name="OpsAllowedIpRanges">Dải IP được dùng chức năng quản trị vận hành, mỗi dòng (hoặc dấu phẩy) một dải; trống = mọi IP.</param>
/// <param name="LoginPerMinutePerIp">Số lần gọi đăng nhập mỗi phút từ một IP.</param>
/// <param name="ApiPerMinutePerUser">Số lần gọi API mỗi phút của một phiên đăng nhập.</param>
/// <param name="HeavyConcurrentPerUser">Số việc nặng (nhập / xuất Excel, in PDF, báo cáo) chạy cùng lúc của một phiên.</param>
public sealed record AccessPolicySettings(
    int MinPasswordLength,
    bool BlockCommonPasswords,
    string? ExtraBlockedPasswords,
    int MaxFailedLogins,
    int LockoutMinutes,
    int SessionMinutes,
    string? OpsAllowedIpRanges,
    int LoginPerMinutePerIp,
    int ApiPerMinutePerUser,
    int HeavyConcurrentPerUser);

/// <summary>
/// Cấu hình truy cập của máy chủ (lát QT7 – màn hình CFG, design.md mục 21.4, 21.16): chính sách mật khẩu, khóa khi sai nhiều lần, hạn phiên, dải
/// IP được dùng chức năng quản trị vận hành, giới hạn tần suất; mốc "Đăng xuất tất cả". Bảng chỉ có <b>một dòng</b> (<see cref="SingletonId"/>);
/// chưa có dòng thì dùng <see cref="CreateDefault"/>. Mỗi máy (TEST, PROD) đặt riêng – TEST đặt giống PROD (21.3a).
/// </summary>
[Auditable("Cấu hình truy cập", EntityType = "AccessPolicy")]
public sealed class AccessPolicy : AuditableEntity
{
    /// <summary>Id cố định của dòng duy nhất.</summary>
    public static readonly Guid SingletonId = new("0195f0a0-0000-7000-8000-00000000c7f1");

    /// <summary>Độ dài mật khẩu tối thiểu mặc định – bằng quy tắc trước QT7 (design.md 9.4) để mật khẩu đang dùng / dữ liệu mẫu không bị chặn;
    /// TEST, PROD nâng lên 10 khi cài (deploy-windows.md).</summary>
    public const int DefaultMinPasswordLength = 8;

    /// <summary>Khoảng cho phép của độ dài mật khẩu tối thiểu. Dưới 8 thì yếu hơn quy tắc gốc của Identity (luôn áp 8).</summary>
    public const int MinPasswordLengthFloor = 8, MinPasswordLengthCeiling = 64;

    /// <summary>Số lần sai mặc định (như trước QT7 – lát QT4) và khoảng cho phép.</summary>
    public const int DefaultMaxFailedLogins = 5, MaxFailedLoginsFloor = 3, MaxFailedLoginsCeiling = 20;

    /// <summary>Phút khóa mặc định (như trước QT7) và khoảng cho phép (tối đa 1 ngày).</summary>
    public const int DefaultLockoutMinutes = 10, LockoutMinutesFloor = 1, LockoutMinutesCeiling = 1440;

    /// <summary>Hạn phiên mặc định 480 phút = 8 giờ (một ngày làm việc, như <c>Jwt:LifetimeMinutes</c> cũ) và khoảng cho phép.</summary>
    public const int DefaultSessionMinutes = 480, SessionMinutesFloor = 15, SessionMinutesCeiling = 1440;

    /// <summary>Lần gọi đăng nhập / phút / IP: mặc định 20 (người gõ sai vài lần vẫn thừa), khoảng cho phép.</summary>
    public const int DefaultLoginPerMinutePerIp = 20, LoginPerMinutePerIpFloor = 5, LoginPerMinutePerIpCeiling = 1000;

    /// <summary>Lần gọi API / phút / phiên: mặc định 600 (10 lần / giây – màn hình nặng nhất gọi vài chục API khi mở), khoảng cho phép.</summary>
    public const int DefaultApiPerMinutePerUser = 600, ApiPerMinutePerUserFloor = 60, ApiPerMinutePerUserCeiling = 10000;

    /// <summary>Việc nặng chạy cùng lúc / phiên: mặc định 3, khoảng cho phép.</summary>
    public const int DefaultHeavyConcurrentPerUser = 3, HeavyConcurrentPerUserFloor = 1, HeavyConcurrentPerUserCeiling = 20;

    /// <summary>Độ dài tối đa danh sách mật khẩu cấm thêm.</summary>
    public const int ExtraBlockedPasswordsMaxLength = 4000;

    /// <summary>Độ dài tối đa danh sách dải IP.</summary>
    public const int OpsAllowedIpRangesMaxLength = 2000;

    private AccessPolicy()
    {
    }

    /// <summary>Độ dài mật khẩu tối thiểu.</summary>
    public int MinPasswordLength { get; private set; } = DefaultMinPasswordLength;

    /// <summary>Chặn mật khẩu phổ biến.</summary>
    public bool BlockCommonPasswords { get; private set; } = true;

    /// <summary>Mật khẩu cấm thêm (mỗi dòng một mật khẩu, so không phân biệt hoa thường); null = không có.</summary>
    public string? ExtraBlockedPasswords { get; private set; }

    /// <summary>Số lần sai liên tiếp thì tạm khóa.</summary>
    public int MaxFailedLogins { get; private set; } = DefaultMaxFailedLogins;

    /// <summary>Số phút tạm khóa.</summary>
    public int LockoutMinutes { get; private set; } = DefaultLockoutMinutes;

    /// <summary>Hạn phiên (phút).</summary>
    public int SessionMinutes { get; private set; } = DefaultSessionMinutes;

    /// <summary>Dải IP được dùng chức năng quản trị vận hành (đã chuẩn hóa, mỗi dòng một dải); null = mọi IP.</summary>
    public string? OpsAllowedIpRanges { get; private set; }

    /// <summary>Lần gọi đăng nhập / phút / IP.</summary>
    public int LoginPerMinutePerIp { get; private set; } = DefaultLoginPerMinutePerIp;

    /// <summary>Lần gọi API / phút / phiên.</summary>
    public int ApiPerMinutePerUser { get; private set; } = DefaultApiPerMinutePerUser;

    /// <summary>Việc nặng chạy cùng lúc / phiên.</summary>
    public int HeavyConcurrentPerUser { get; private set; } = DefaultHeavyConcurrentPerUser;

    /// <summary>Mốc "Đăng xuất tất cả" (UTC, đã bỏ phần lẻ giây): token cấp trước mốc này bị từ chối; null = chưa dùng.</summary>
    public DateTimeOffset? SessionsRevokedAt { get; private set; }

    /// <summary>Dòng mặc định – dùng khi chưa có dòng trong CSDL.</summary>
    /// <returns>Chính sách mặc định.</returns>
    public static AccessPolicy CreateDefault() => new() { Id = SingletonId };

    /// <summary>Áp các giá trị mới sau khi kiểm tra khoảng cho phép.</summary>
    /// <param name="settings">Giá trị nhập trên CFG.</param>
    /// <exception cref="BusinessRuleException">Giá trị ngoài khoảng, dải IP sai cú pháp, danh sách quá dài.</exception>
    public void Apply(AccessPolicySettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        // Kiểm tra hết rồi mới gán: một giá trị sai thì không tham số nào bị đổi.
        var minLength = InRange(settings.MinPasswordLength, MinPasswordLengthFloor, MinPasswordLengthCeiling, "Độ dài mật khẩu tối thiểu");
        var maxFailed = InRange(settings.MaxFailedLogins, MaxFailedLoginsFloor, MaxFailedLoginsCeiling, "Số lần sai mật khẩu");
        var lockout = InRange(settings.LockoutMinutes, LockoutMinutesFloor, LockoutMinutesCeiling, "Số phút tạm khóa");
        var session = InRange(settings.SessionMinutes, SessionMinutesFloor, SessionMinutesCeiling, "Hạn phiên đăng nhập (phút)");
        var loginPerIp = InRange(settings.LoginPerMinutePerIp, LoginPerMinutePerIpFloor, LoginPerMinutePerIpCeiling,
            "Số lần đăng nhập mỗi phút từ một IP");
        var apiPerUser = InRange(settings.ApiPerMinutePerUser, ApiPerMinutePerUserFloor, ApiPerMinutePerUserCeiling,
            "Số lần gọi API mỗi phút của một phiên");
        var heavy = InRange(settings.HeavyConcurrentPerUser, HeavyConcurrentPerUserFloor, HeavyConcurrentPerUserCeiling,
            "Số việc nặng chạy cùng lúc");

        var blocked = ParseBlockedPasswords(settings.ExtraBlockedPasswords);
        var blockedText = blocked.Count == 0 ? null : string.Join('\n', blocked);
        if (blockedText is { Length: > ExtraBlockedPasswordsMaxLength })
            throw new BusinessRuleException($"Danh sách mật khẩu cấm thêm dài quá {ExtraBlockedPasswordsMaxLength} ký tự.");

        var ranges = ParseIpRanges(settings.OpsAllowedIpRanges);
        var rangesText = ranges.Count == 0 ? null : string.Join('\n', ranges.Select(r => r.ToString()));
        if (rangesText is { Length: > OpsAllowedIpRangesMaxLength })
            throw new BusinessRuleException($"Danh sách dải IP dài quá {OpsAllowedIpRangesMaxLength} ký tự.");

        (MinPasswordLength, MaxFailedLogins, LockoutMinutes, SessionMinutes) = (minLength, maxFailed, lockout, session);
        (LoginPerMinutePerIp, ApiPerMinutePerUser, HeavyConcurrentPerUser) = (loginPerIp, apiPerUser, heavy);
        BlockCommonPasswords = settings.BlockCommonPasswords;
        ExtraBlockedPasswords = blockedText;
        OpsAllowedIpRanges = rangesText;
    }

    /// <summary>
    /// "Đăng xuất tất cả": mọi token cấp trước <paramref name="now"/> (làm tròn xuống giây – giờ cấp token <c>iat</c> tính bằng giây) bị từ chối.
    /// Token cấp lại cho người bấm ngay sau đó có <c>iat</c> bằng mốc nên vẫn dùng được.
    /// </summary>
    /// <param name="now">Thời điểm (UTC).</param>
    public void RevokeAllSessions(DateTimeOffset now) =>
        SessionsRevokedAt = DateTimeOffset.FromUnixTimeSeconds(now.ToUnixTimeSeconds());

    /// <summary>Danh sách dải IP đã lưu.</summary>
    /// <returns>Các dải; rỗng = mọi IP.</returns>
    public IReadOnlyList<IPNetwork> GetOpsIpRanges() => ParseIpRanges(OpsAllowedIpRanges);

    /// <summary>
    /// Đọc danh sách dải IP: mỗi dòng hoặc dấu phẩy / chấm phẩy một mục, dạng CIDR (<c>10.0.0.0/8</c>, <c>fd00::/8</c>) hoặc một địa chỉ
    /// (<c>10.1.2.3</c> = /32). Bỏ dòng trống, mục trùng.
    /// </summary>
    /// <param name="text">Chữ người nhập.</param>
    /// <returns>Các dải theo thứ tự nhập.</returns>
    /// <exception cref="BusinessRuleException">Có mục sai cú pháp.</exception>
    public static IReadOnlyList<IPNetwork> ParseIpRanges(string? text)
    {
        var result = new List<IPNetwork>();
        foreach (var raw in Split(text))
        {
            IPNetwork network;
            if (raw.Contains('/'))
            {
                // Dải có bit sau tiền tố khác 0 (10.1.2.3/8) thường là gõ nhầm – báo người dùng sửa thành địa chỉ mạng (10.0.0.0/8) thay vì tự cắt.
                if (!IPNetwork.TryParse(raw, out network)
                    || !IPAddress.TryParse(raw[..raw.IndexOf('/')], out var given)
                    || !Normalize(given).Equals(network.BaseAddress))
                    throw new BusinessRuleException($"Dải IP \"{raw}\" không đúng dạng (ví dụ 10.0.0.0/8 hoặc 192.168.1.0/24 – phần sau dấu / là số bit mạng).");
            }
            else if (IPAddress.TryParse(raw, out var address))
            {
                address = Normalize(address);
                network = new IPNetwork(address, address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? 32 : 128);
            }
            else
            {
                throw new BusinessRuleException($"Địa chỉ IP \"{raw}\" không đúng dạng.");
            }

            if (!result.Contains(network)) result.Add(network);
        }

        return result;
    }

    /// <summary>
    /// IP có được dùng chức năng quản trị vận hành không: danh sách rỗng – mọi IP; máy chủ tự gọi chính nó (loopback) luôn được – để người quản trị
    /// đăng nhập ngay trên máy chủ sửa lại khi lỡ khai sai dải; không biết IP (chỉ xảy ra khi chạy test trong bộ nhớ) coi như loopback.
    /// </summary>
    /// <param name="ranges">Danh sách dải.</param>
    /// <param name="address">IP của request.</param>
    /// <returns>true khi được dùng.</returns>
    public static bool IsIpAllowed(IReadOnlyList<IPNetwork> ranges, IPAddress? address)
    {
        if (ranges.Count == 0 || address is null || IPAddress.IsLoopback(address)) return true;
        var ip = Normalize(address);
        return ranges.Any(r => r.BaseAddress.AddressFamily == ip.AddressFamily && r.Contains(ip));
    }

    /// <summary>Đọc danh sách mật khẩu cấm thêm: mỗi dòng một mật khẩu, bỏ khoảng trắng hai đầu, dòng trống, mục trùng (không phân biệt hoa thường).</summary>
    /// <param name="text">Chữ người nhập.</param>
    /// <returns>Các mật khẩu.</returns>
    public static IReadOnlyList<string> ParseBlockedPasswords(string? text) =>
        (text ?? string.Empty).Split('\n')
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>IPv6 dạng ánh xạ IPv4 (::ffff:10.0.0.1 – Kestrel nghe cả hai họ địa chỉ) đổi về IPv4 để so với dải IPv4.</summary>
    private static IPAddress Normalize(IPAddress address) => address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;

    private static IEnumerable<string> Split(string? text) =>
        (text ?? string.Empty).Split(['\n', '\r', ',', ';', ' ', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static int InRange(int value, int floor, int ceiling, string name) =>
        value >= floor && value <= ceiling ? value : throw new BusinessRuleException($"{name} phải từ {floor} đến {ceiling}.");
}
