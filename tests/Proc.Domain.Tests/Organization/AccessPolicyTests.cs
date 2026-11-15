using System.Net;
using Proc.Domain.Common;
using Proc.Domain.Operations;

namespace Proc.Domain.Tests.Organization;

/// <summary>Lát QT7 – cấu hình truy cập CFG: khoảng cho phép, dải IP, mật khẩu cấm thêm, mốc đăng xuất tất cả (design.md mục 21.16).</summary>
public class AccessPolicyTests
{
    private static AccessPolicySettings Settings(
        int minLength = 10, string? ranges = null, string? blocked = null, int maxFailed = 5, int session = 480) =>
        new(minLength, true, blocked, maxFailed, 10, session, ranges, 20, 600, 3);

    /// <summary>Mặc định giữ quy tắc trước QT7 (8 ký tự, 5 lần, 10 phút, 480 phút); áp giá trị mới; ngoài khoảng thì báo lỗi kèm tên tham số.</summary>
    [Fact]
    public void MacDinh_ApGiaTri_NgoaiKhoangBaoLoi()
    {
        var policy = AccessPolicy.CreateDefault();
        Assert.Equal(AccessPolicy.SingletonId, policy.Id);
        Assert.Equal((8, 5, 10, 480, true), (policy.MinPasswordLength, policy.MaxFailedLogins, policy.LockoutMinutes, policy.SessionMinutes,
            policy.BlockCommonPasswords));
        Assert.Empty(policy.GetOpsIpRanges());

        policy.Apply(Settings(minLength: 12, session: 60));
        Assert.Equal((12, 60), (policy.MinPasswordLength, policy.SessionMinutes));

        var ex = Assert.Throws<BusinessRuleException>(() => policy.Apply(Settings(minLength: 6)));
        Assert.Contains("Độ dài mật khẩu tối thiểu", ex.Message);
        Assert.Throws<BusinessRuleException>(() => policy.Apply(Settings(maxFailed: 2)));
        Assert.Throws<BusinessRuleException>(() => policy.Apply(Settings(session: 5)));
        // Lỗi thì giữ nguyên giá trị cũ của tham số đó.
        Assert.Equal(12, policy.MinPasswordLength);
    }

    /// <summary>Dải IP: CIDR, địa chỉ lẻ = /32, tách dòng / dấu phẩy, bỏ trùng; sai cú pháp báo lỗi; lưu dạng chuẩn mỗi dòng một dải.</summary>
    [Fact]
    public void DaiIp_DocVaChuanHoa()
    {
        var policy = AccessPolicy.CreateDefault();
        policy.Apply(Settings(ranges: " 10.0.0.0/8, 192.168.1.5\n\n10.0.0.0/8; fd00::/8 "));
        Assert.Equal("10.0.0.0/8\n192.168.1.5/32\nfd00::/8", policy.OpsAllowedIpRanges);

        Assert.Throws<BusinessRuleException>(() => AccessPolicy.ParseIpRanges("10.0.0.300"));
        Assert.Throws<BusinessRuleException>(() => AccessPolicy.ParseIpRanges("10.1.2.3/8"));
        Assert.Throws<BusinessRuleException>(() => AccessPolicy.ParseIpRanges("may-chu"));

        policy.Apply(Settings(ranges: "  "));
        Assert.Null(policy.OpsAllowedIpRanges);
    }

    /// <summary>
    /// IP được dùng chức năng QT: trống = mọi IP; trong dải; IPv4 dạng IPv6 (::ffff:) so như IPv4; loopback và IP không rõ luôn được (sửa cấu hình
    /// ngay trên máy chủ khi lỡ khai sai).
    /// </summary>
    [Fact]
    public void DaiIp_KiemTraDiaChi()
    {
        var ranges = AccessPolicy.ParseIpRanges("10.0.0.0/8\n192.168.1.5");
        Assert.True(AccessPolicy.IsIpAllowed([], IPAddress.Parse("8.8.8.8")));
        Assert.True(AccessPolicy.IsIpAllowed(ranges, IPAddress.Parse("10.20.30.40")));
        Assert.True(AccessPolicy.IsIpAllowed(ranges, IPAddress.Parse("::ffff:192.168.1.5")));
        Assert.False(AccessPolicy.IsIpAllowed(ranges, IPAddress.Parse("192.168.1.6")));
        Assert.False(AccessPolicy.IsIpAllowed(ranges, IPAddress.Parse("fd00::1")));
        Assert.True(AccessPolicy.IsIpAllowed(ranges, IPAddress.Loopback));
        Assert.True(AccessPolicy.IsIpAllowed(ranges, IPAddress.IPv6Loopback));
        Assert.True(AccessPolicy.IsIpAllowed(ranges, null));
    }

    /// <summary>Mật khẩu cấm thêm: mỗi dòng một mục, bỏ trống / trùng không phân biệt hoa thường; quá dài báo lỗi.</summary>
    [Fact]
    public void MatKhauCamThem()
    {
        var policy = AccessPolicy.CreateDefault();
        policy.Apply(Settings(blocked: " CongTy@2026 \r\n\r\ncongty@2026\nTongCongTy1 "));
        Assert.Equal("CongTy@2026\nTongCongTy1", policy.ExtraBlockedPasswords);

        var tooLong = string.Join('\n', Enumerable.Range(0, 500).Select(i => $"matkhau-{i:0000}"));
        Assert.Throws<BusinessRuleException>(() => policy.Apply(Settings(blocked: tooLong)));
    }

    /// <summary>Đăng xuất tất cả: mốc làm tròn xuống giây (giờ cấp token tính bằng giây).</summary>
    [Fact]
    public void DangXuatTatCa_LamTronGiay()
    {
        var policy = AccessPolicy.CreateDefault();
        policy.RevokeAllSessions(new DateTimeOffset(2026, 11, 15, 9, 30, 15, 750, TimeSpan.Zero));
        Assert.Equal(new DateTimeOffset(2026, 11, 15, 9, 30, 15, TimeSpan.Zero), policy.SessionsRevokedAt);
    }
}
