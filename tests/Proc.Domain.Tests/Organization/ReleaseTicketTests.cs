using Proc.Domain.Common;
using Proc.Domain.Operations;

namespace Proc.Domain.Tests.Organization;

/// <summary>Lát QT8 – phiếu phát hành: máy trạng thái, khóa phiếu, mức quay về, SHA256 (design.md mục 21.6, 21.6a).</summary>
public class ReleaseTicketTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 16, 2, 0, 0, TimeSpan.Zero);
    private const string Sha = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    private static ReleaseTicket NewTicket(RollbackLevel minLevel = RollbackLevel.CodeOnly) => ReleaseTicket.Receive(
        new ReleasePackageInfo("0.36.0", "procurement-0.36.0.zip", 1000, Sha.ToUpperInvariant(), ReleaseSource.Upload, null,
            "## [0.36.0]", "V0_36_0_TrungTamPhatHanh", minLevel, "Thay lại gói 0.35.0"),
        [("Thêm mới", "Màn hình REL"), ("Sửa lỗi", "  "), ("Thay đổi", "Hạn phiên theo CFG")], "Bản thử", "admin", Now);

    /// <summary>Nhận gói: SHA256 về chữ thường, mục kiểm thử bỏ dòng trống; phiên bản / SHA sai bị từ chối.</summary>
    [Fact]
    public void NhanGoi_TaoMucKiemThu()
    {
        var t = NewTicket();
        Assert.Equal((ReleaseStatus.Received, Sha), (t.Status, t.Sha256));
        Assert.Equal(["Màn hình REL", "Hạn phiên theo CFG"], t.Items.Select(i => i.Content));
        Assert.Equal([1, 2], t.Items.Select(i => i.Seq));
        Assert.Throws<BusinessRuleException>(() => ReleaseTicket.Receive(
            new ReleasePackageInfo("0.36", "a.zip", 1, Sha, ReleaseSource.Upload, null, null, null, RollbackLevel.CodeOnly, null), [], null, null, Now));
        Assert.Throws<BusinessRuleException>(() => ReleaseTicket.Receive(
            new ReleasePackageInfo("0.36.0", "a.zip", 1, "abc", ReleaseSource.Upload, null, null, null, RollbackLevel.CodeOnly, null), [], null, null, Now));
    }

    /// <summary>
    /// Đường đi đủ: cài TEST (lỗi giữ Đã nhận, đạt → Đang UAT) → xác nhận UAT (còn mục chưa kiểm thử thì phải ghi lý do) → không cài PROD trước UAT →
    /// cài PROD đạt → khóa (không sửa, không ghi kiểm thử, không hủy; vẫn thêm ghi chú, lỗi) → đóng.
    /// </summary>
    [Fact]
    public void DuongDiDu_TestUatProdDong()
    {
        var t = NewTicket();
        Assert.Throws<BusinessRuleException>(() => t.StartDeploy(AppEnvironments.Prod, Guid.NewGuid(), "0.35.0", null, "admin", Now));
        var failed = t.StartDeploy(AppEnvironments.Test, Guid.NewGuid(), "0.35.0", null, "admin", Now);
        Assert.Throws<BusinessRuleException>(() => t.StartDeploy(AppEnvironments.Test, Guid.NewGuid(), "0.35.0", null, "admin", Now));
        t.CompleteInstall(failed.Id, false, null, "health không lên", "agent", Now);
        Assert.Equal(ReleaseStatus.Received, t.Status);

        var test = t.StartDeploy(AppEnvironments.Test, Guid.NewGuid(), "0.35.0", null, "admin", Now);
        t.CompleteInstall(test.Id, true, Sha, "Xong", "agent", Now);
        Assert.Equal(ReleaseStatus.UatInProgress, t.Status);
        Assert.True(t.InstalledOnTest);

        t.RecordTest(t.Items[0].Id, ReleaseTestResult.Passed, null, "tester", Now);
        Assert.Throws<BusinessRuleException>(() => t.RecordTest(t.Items[1].Id, ReleaseTestResult.Failed, " ", "tester", Now));
        var ex = Assert.Throws<BusinessRuleException>(() => t.SignOffUat(null, false, "pm", Now));
        Assert.Contains("1 mục chưa kiểm thử", ex.Message);
        t.SignOffUat("Mục 2 thử sau trên PROD", false, "pm", Now);
        Assert.Equal((ReleaseStatus.UatPassed, false), (t.Status, t.UatWithoutTest));

        var prod = t.StartDeploy(AppEnvironments.Prod, Guid.NewGuid(), "0.35.0", "Cài tối thứ Bảy", "admin", Now);
        t.CompleteInstall(prod.Id, true, Sha, "Xong", "agent", Now.AddMinutes(5));
        Assert.Equal(ReleaseStatus.ProdInstalled, t.Status);
        Assert.True(t.IsLocked);
        Assert.Throws<BusinessRuleException>(() => t.UpdateInfo("x", null, null));
        Assert.Throws<BusinessRuleException>(() => t.RecordTest(t.Items[1].Id, ReleaseTestResult.Passed, null, "tester", Now));
        Assert.Throws<BusinessRuleException>(() => t.Cancel("Không cần nữa", "admin", Now));
        t.AppendNote("Theo dõi ERR 2 ngày", "admin", Now);
        Assert.Contains("Theo dõi ERR 2 ngày", t.Note);
        t.AddDefect("Lệch số trên báo cáo", null, "RPT", "user", Now);

        t.Close("admin", Now.AddDays(2));
        Assert.Equal(ReleaseStatus.Closed, t.Status);
    }

    /// <summary>Cài PROD báo đạt nhưng SHA256 khác gói của phiếu → ghi lỗi, phiếu không sang Đã cài PROD.</summary>
    [Fact]
    public void CaiProd_KhacSha_GhiLoi()
    {
        var t = NewTicket();
        t.SignOffUat("Đã thử trên máy DEV", singleEnvironment: true, "pm", Now);
        Assert.True(t.UatWithoutTest);
        var prod = t.StartDeploy(AppEnvironments.Prod, Guid.NewGuid(), "0.35.0", null, "admin", Now);
        t.CompleteInstall(prod.Id, true, new string('f', 64), "Xong", "agent", Now);
        Assert.Equal(ReleaseStatus.UatPassed, t.Status);
        Assert.Equal(ReleaseInstallStatus.Failed, t.Installs.Single(i => i.Id == prod.Id).Status);
        Assert.Contains("SHA256", t.Installs.Single(i => i.Id == prod.Id).Message);
    }

    /// <summary>Có máy TEST thì không xác nhận UAT khi chưa cài TEST; hủy cần lý do, hủy rồi thì khóa.</summary>
    [Fact]
    public void Uat_CanCaiTest_HuyCanLyDo()
    {
        var t = NewTicket();
        Assert.Throws<BusinessRuleException>(() => t.SignOffUat("ok ok ok", false, "pm", Now));
        Assert.Throws<BusinessRuleException>(() => t.Cancel("abc", "admin", Now));
        t.Cancel("Gói lỗi đóng gói", "admin", Now);
        Assert.Equal(ReleaseStatus.Cancelled, t.Status);
        Assert.Throws<BusinessRuleException>(() => t.AddDefect("x", null, null, null, Now));
    }

    /// <summary>Quay về: chỉ sau khi cài PROD, mức không thấp hơn mức tối thiểu của gói, bắt buộc lý do; đạt → Đã quay về.</summary>
    [Fact]
    public void QuayVe_TheoMucToiThieu()
    {
        var t = NewTicket(RollbackLevel.RemoveSchema);
        Assert.Throws<BusinessRuleException>(() => t.StartRollback(RollbackLevel.RemoveSchema, Guid.NewGuid(), "0.35.0", "Bản mới lỗi", "admin", Now));
        t.SignOffUat("Đã thử trên máy DEV", true, "pm", Now);
        var prod = t.StartDeploy(AppEnvironments.Prod, Guid.NewGuid(), "0.35.0", null, "admin", Now);
        t.CompleteInstall(prod.Id, true, Sha, null, "agent", Now);

        Assert.Throws<BusinessRuleException>(() => t.StartRollback(RollbackLevel.CodeOnly, Guid.NewGuid(), "0.35.0", "Bản mới lỗi", "admin", Now));
        Assert.Throws<BusinessRuleException>(() => t.StartRollback(RollbackLevel.RemoveSchema, Guid.NewGuid(), "0.35.0", "lỗi", "admin", Now));
        var back = t.StartRollback(RollbackLevel.RestoreDatabase, Guid.NewGuid(), "0.35.0", "Bản mới làm sai số liệu", "admin", Now);
        Assert.Equal(("0.36.0", "0.35.0", RollbackLevel.RestoreDatabase), (back.PreviousVersion, back.TargetVersion, back.Level));
        t.CompleteInstall(back.Id, true, null, "Đã về 0.35.0", "agent", Now);
        Assert.Equal((ReleaseStatus.RolledBack, "Bản mới làm sai số liệu"), (t.Status, t.EndReason));
    }

    /// <summary>
    /// Quay về trên TEST (người dùng yêu cầu 16/11/2026 – lát QT9): chỉ khi Đang UAT / UAT đạt và TEST đang chạy bản của phiếu; đạt → về Đã nhận, bỏ
    /// xác nhận UAT; cài TEST lại được và lại Đang UAT.
    /// </summary>
    [Fact]
    public void QuayVeTest_VeDaNhan_BoXacNhanUat()
    {
        var t = NewTicket();
        Assert.Throws<BusinessRuleException>(() =>
            t.StartRollback(AppEnvironments.Test, RollbackLevel.CodeOnly, Guid.NewGuid(), "0.35.0", "UAT không đạt", "admin", Now));
        var deploy = t.StartDeploy(AppEnvironments.Test, Guid.NewGuid(), "0.35.0", null, "admin", Now);
        t.CompleteInstall(deploy.Id, true, Sha, null, "agent", Now);
        t.SignOffUat("Đạt sau khi thử", false, "pm", Now);
        Assert.True(t.InstalledOnTest);
        Assert.Throws<BusinessRuleException>(() =>
            t.StartRollback("Dev", RollbackLevel.CodeOnly, Guid.NewGuid(), "0.35.0", "UAT không đạt", "admin", Now));

        var back = t.StartRollback(AppEnvironments.Test, RollbackLevel.CodeOnly, Guid.NewGuid(), "0.35.0", "UAT không đạt", "admin", Now);
        Assert.Equal((AppEnvironments.Test, "0.35.0"), (back.Environment, back.TargetVersion));
        t.CompleteInstall(back.Id, true, null, "Đã về 0.35.0", "agent", Now);
        Assert.Equal((ReleaseStatus.Received, (DateTimeOffset?)null, false), (t.Status, t.UatSignedAt, t.InstalledOnTest));
        // Không còn bản trên TEST → không quay về lần nữa; PROD không cài được khi chưa UAT lại.
        Assert.Throws<BusinessRuleException>(() =>
            t.StartRollback(AppEnvironments.Test, RollbackLevel.CodeOnly, Guid.NewGuid(), "0.35.0", "UAT không đạt", "admin", Now));
        Assert.Throws<BusinessRuleException>(() => t.StartDeploy(AppEnvironments.Prod, Guid.NewGuid(), "0.35.0", null, "admin", Now));

        var again = t.StartDeploy(AppEnvironments.Test, Guid.NewGuid(), "0.35.0", null, "admin", Now);
        t.CompleteInstall(again.Id, true, Sha, null, "agent", Now);
        Assert.Equal((ReleaseStatus.UatInProgress, true), (t.Status, t.InstalledOnTest));
    }

    /// <summary>Lỗi phát hiện: Đã sửa / Không sửa phải ghi cách xử lý; mở lại xóa người xử lý.</summary>
    [Fact]
    public void LoiPhatHien_XuLy()
    {
        var t = NewTicket();
        var d = t.AddDefect("Nút Lưu không chạy", "Ở PO", "PO", "user", Now);
        Assert.Contains("1 lỗi chưa xử lý", t.OpenIssues());
        Assert.Throws<BusinessRuleException>(() => t.ResolveDefect(d.Id, ReleaseDefectStatus.Fixed, null, "dev", Now));
        t.ResolveDefect(d.Id, ReleaseDefectStatus.Fixed, "Sửa ở 0.36.1", "dev", Now);
        Assert.DoesNotContain(t.OpenIssues(), s => s.Contains("lỗi"));
        t.ResolveDefect(d.Id, ReleaseDefectStatus.Open, null, "dev", Now);
        Assert.Null(t.Defects[0].ResolvedByName);
    }
}
