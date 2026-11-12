using Proc.Domain.Common;
using Proc.Domain.MasterData;

namespace Proc.Domain.Tests.MasterData;

/// <summary>Tài khoản Portal NCC: mời, kích hoạt, khóa tạm, khóa / mở khóa (design.md 19.5, lát P1).</summary>
public class SupplierPortalUserTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 12, 3, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Moi_EmailChuThuong_KichHoat_DoiDauBaoMat()
    {
        var u = SupplierPortalUser.Invite(Guid.NewGuid(), "  Ban.Hang@NCC.vn ", " Nguyễn Văn A ", null, " ", true);
        Assert.Equal("ban.hang@ncc.vn", u.Email);
        Assert.Equal("Nguyễn Văn A", u.FullName);
        Assert.Null(u.Position);
        Assert.Equal(SupplierPortalUserStatus.Invited, u.Status);
        Assert.False(u.CanSignIn);

        var stamp = u.SecurityStamp;
        u.Activate("hash", Now);
        Assert.True(u.CanSignIn);
        Assert.Equal(Now, u.ActivatedAt);
        Assert.NotEqual(stamp, u.SecurityStamp);
        Assert.Contains("đã kích hoạt", Assert.Throws<BusinessRuleException>(() => u.Activate("hash2", Now)).Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("khongcoacong")]
    [InlineData("a@b")]
    [InlineData("a b@ncc.vn")]
    public void EmailSai_BaoLoi(string email) =>
        Assert.Throws<BusinessRuleException>(() => SupplierPortalUser.Invite(Guid.NewGuid(), email, "A", null, null, false));

    [Theory]
    [InlineData("abc123", "tối thiểu")]
    [InlineData("abcdefgh", "chữ số")]
    [InlineData("ABCDEFG1", "chữ thường")]
    public void MatKhauYeu_BaoLoi(string password, string expected) =>
        Assert.Contains(expected, SupplierPortalUser.ValidatePassword(password));

    [Fact]
    public void SaiMatKhauDuSoLan_KhoaTam_DungThiXoaDem()
    {
        var u = SupplierPortalUser.Invite(Guid.NewGuid(), "a@ncc.vn", "A", null, null, false);
        u.Activate("hash", Now);
        for (var i = 0; i < 4; i++) Assert.False(u.RecordFailedLogin(Now, 5, TimeSpan.FromMinutes(15)));
        Assert.True(u.RecordFailedLogin(Now, 5, TimeSpan.FromMinutes(15)));
        Assert.True(u.IsLockedOut(Now.AddMinutes(14)));
        Assert.False(u.IsLockedOut(Now.AddMinutes(15)));

        u.RecordFailedLogin(Now, 5, TimeSpan.FromMinutes(15));
        u.RecordPasswordOk();
        Assert.Equal(0, u.FailedLoginCount);
    }

    [Fact]
    public void Khoa_DoiDauBaoMat_MoKhoaVeTrangThaiCu()
    {
        var invited = SupplierPortalUser.Invite(Guid.NewGuid(), "a@ncc.vn", "A", null, null, false);
        invited.Disable();
        invited.Enable();
        Assert.Equal(SupplierPortalUserStatus.Invited, invited.Status);

        var active = SupplierPortalUser.Invite(Guid.NewGuid(), "b@ncc.vn", "B", null, null, false);
        active.Activate("hash", Now);
        var stamp = active.SecurityStamp;
        active.Disable();
        Assert.NotEqual(stamp, active.SecurityStamp);
        Assert.False(active.CanSignIn);
        Assert.Throws<BusinessRuleException>(() => active.ChangePassword("x"));
        Assert.Throws<BusinessRuleException>(() => active.Disable());
        active.Enable();
        Assert.True(active.CanSignIn);
        Assert.Throws<BusinessRuleException>(() => active.Enable());
    }

    [Fact]
    public void MaDungMotLan_HetHan_SaiQuaSoLan()
    {
        var code = SupplierPortalCode.Issue(Guid.NewGuid(), SupplierPortalCodePurpose.LoginOtp, "h", Now, TimeSpan.FromMinutes(10));
        Assert.True(code.IsUsable(Now.AddMinutes(9), 5));
        Assert.False(code.IsUsable(Now.AddMinutes(10), 5));
        for (var i = 0; i < 5; i++) code.RecordFailedAttempt();
        Assert.False(code.IsUsable(Now, 5));

        var other = SupplierPortalCode.Issue(Guid.NewGuid(), SupplierPortalCodePurpose.Invite, "h", Now, TimeSpan.FromDays(7));
        other.MarkUsed(Now);
        Assert.False(other.IsUsable(Now, 5));
    }
}
