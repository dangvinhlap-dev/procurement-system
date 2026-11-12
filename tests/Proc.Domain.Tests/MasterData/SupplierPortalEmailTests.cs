using Proc.Domain.Common;
using Proc.Domain.MasterData;
using Proc.Domain.Notifications;

namespace Proc.Domain.Tests.MasterData;

/// <summary>Thư báo NCC qua Portal: soạn, gửi, thôi gửi, thử lại khi lỗi (design.md 19.5, lát P3b-3).</summary>
public class SupplierPortalEmailTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 12, 3, 0, 0, TimeSpan.Zero);

    private static SupplierPortalEmail Make(string to = " ban.hang@ncc.vn ", string subject = "Thư mời báo giá RFQ-1") =>
        SupplierPortalEmail.Queue(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), to, "Người bán", SupplierPortalEmailKind.RfqInvitation, "RFQ",
            Guid.NewGuid(), subject, "<p>x</p>", "x", Now);

    [Fact]
    public void Soan_ChoGui_CatTieuDeDai()
    {
        var m = Make(subject: new string('a', 400));
        Assert.Equal((NotificationEmailState.Pending, "ban.hang@ncc.vn", 0), (m.State, m.ToAddress, m.Attempts));
        Assert.Equal(SupplierPortalEmail.SubjectMaxLength, m.Subject.Length);
        Assert.Throws<BusinessRuleException>(() => Make(to: " "));
        Assert.Throws<BusinessRuleException>(() => Make(subject: ""));
    }

    [Fact]
    public void LoiThuLai_HetLuotThiLoi_GuiDuocThiXoaLoi()
    {
        var m = Make();
        m.MarkFailed(Now, "550 Mailbox unavailable", 2);
        Assert.Equal((NotificationEmailState.Pending, 1, "550 Mailbox unavailable"), (m.State, m.Attempts, m.Note));
        m.MarkFailed(Now, "550", 2);
        Assert.Equal((NotificationEmailState.Failed, 2), (m.State, m.Attempts));

        var ok = Make();
        ok.MarkFailed(Now, "timeout", 3);
        ok.MarkSent(Now.AddMinutes(1));
        Assert.Equal((NotificationEmailState.Sent, 2, null, Now.AddMinutes(1)), (ok.State, ok.Attempts, ok.Note, ok.SentAt));

        var skip = Make();
        skip.Skip(Now, "Tài khoản Portal không còn đang dùng.");
        Assert.Equal((NotificationEmailState.Skipped, "Tài khoản Portal không còn đang dùng."), (skip.State, skip.Note));
    }
}
