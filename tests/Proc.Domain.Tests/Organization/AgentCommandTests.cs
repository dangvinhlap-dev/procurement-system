using Proc.Domain.Common;
using Proc.Domain.Operations;

namespace Proc.Domain.Tests.Organization;

/// <summary>Lát QT5 – lệnh bộ thực thi (hết hạn, lấy lệnh, báo kết quả, hủy), môi trường bộ thực thi, chế độ bảo trì (ke-hoach-qt.md QT5).</summary>
public class AgentCommandTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 15, 18, 0, 0, TimeSpan.Zero);

    /// <summary>Chờ → lấy → xong; hạn lấy 1 giờ; báo kết quả lần hai, hủy khi đang chạy đều bị từ chối; nhật ký dài giữ phần cuối.</summary>
    [Fact]
    public void VongDoi_ChoLayXong()
    {
        var cmd = AgentCommand.Create(Guid.NewGuid(), AgentCommandKind.Ping, " ", "  Lệnh thử sau khi cài  ", "admin", Now);
        Assert.Equal((AgentCommandStatus.Queued, Now.AddHours(1), "Lệnh thử sau khi cài"), (cmd.Status, cmd.ExpiresAt, cmd.Reason));
        Assert.Null(cmd.Parameters);
        Assert.Throws<BusinessRuleException>(() => cmd.Report(true, null, null, Now));

        cmd.Claim(Now.AddMinutes(1));
        Assert.Equal((AgentCommandStatus.Running, Now.AddMinutes(1)), (cmd.Status, cmd.StartedAt));
        Assert.Throws<BusinessRuleException>(() => cmd.Claim(Now.AddMinutes(1)));
        Assert.Throws<BusinessRuleException>(() => cmd.Cancel("admin", Now));

        var journal = new string('a', AgentCommand.JournalMaxLength) + "CUỐI";
        cmd.Report(true, " ", journal, Now.AddMinutes(2));
        Assert.Equal((AgentCommandStatus.Succeeded, "Xong."), (cmd.Status, cmd.ResultMessage));
        Assert.Equal(AgentCommand.JournalMaxLength, cmd.Journal!.Length);
        Assert.StartsWith("…", cmd.Journal, StringComparison.Ordinal);
        Assert.EndsWith("CUỐI", cmd.Journal, StringComparison.Ordinal);
        Assert.Throws<BusinessRuleException>(() => cmd.Report(false, "x", null, Now));
    }

    /// <summary>Thiếu lý do / loại lệnh lạ bị từ chối; quá hạn thì không lấy được, chỉ đánh dấu hết hạn; lệnh chờ hủy được; quá giờ chạy thì lỗi.</summary>
    [Fact]
    public void HetHan_Huy_QuaGio()
    {
        var env = Guid.NewGuid();
        Assert.Throws<BusinessRuleException>(() => AgentCommand.Create(env, AgentCommandKind.Ping, null, "abc", "admin", Now));
        Assert.Throws<BusinessRuleException>(() => AgentCommand.Create(env, (AgentCommandKind)99, null, "Lệnh thử", "admin", Now));
        Assert.Throws<BusinessRuleException>(() =>
            AgentCommand.Create(env, AgentCommandKind.Ping, new string('x', AgentCommand.ParametersMaxLength + 1), "Lệnh thử", "admin", Now));

        var old = AgentCommand.Create(env, AgentCommandKind.Ping, null, "Lệnh thử", "admin", Now);
        Assert.False(old.IsExpired(Now.AddMinutes(59)));
        Assert.Throws<BusinessRuleException>(() => old.Expire(Now.AddMinutes(59)));
        Assert.True(old.IsExpired(Now.AddHours(1)));
        Assert.Throws<BusinessRuleException>(() => old.Claim(Now.AddHours(1)));
        old.Expire(Now.AddHours(1));
        Assert.Equal(AgentCommandStatus.Expired, old.Status);
        Assert.False(old.IsExpired(Now.AddHours(2)));

        var cancel = AgentCommand.Create(env, AgentCommandKind.Ping, null, "Lệnh thử", "admin", Now);
        cancel.Cancel("admin2", Now.AddMinutes(1));
        Assert.Equal((AgentCommandStatus.Cancelled, "admin2"), (cancel.Status, cancel.CancelledByName));
        Assert.Throws<BusinessRuleException>(() => cancel.Claim(Now.AddMinutes(2)));

        var stale = AgentCommand.Create(env, AgentCommandKind.Ping, null, "Lệnh thử", "admin", Now);
        Assert.Throws<BusinessRuleException>(() => stale.FailStale("quá giờ", Now));
        stale.Claim(Now);
        stale.FailStale("Bộ thực thi không báo kết quả", Now.AddHours(7));
        Assert.Equal(AgentCommandStatus.Failed, stale.Status);
    }

    /// <summary>Môi trường: mã chuẩn hóa Dev/Test/Prod, địa chỉ phải http(s), ngừng dùng thì không tạo khóa, liên lạc giữ giá trị cũ khi bỏ trống.</summary>
    [Fact]
    public void MoiTruong_KhaiBao_Khoa_LienLac()
    {
        Assert.Throws<BusinessRuleException>(() => AgentEnvironment.Register("uat", "Máy UAT", null));
        Assert.Throws<BusinessRuleException>(() => AgentEnvironment.Register("Test", " ", null));
        Assert.Throws<BusinessRuleException>(() => AgentEnvironment.Register("Test", "Máy TEST", "ftp://test"));

        var env = AgentEnvironment.Register("test", " Máy TEST ", "https://test.local:5007/");
        Assert.Equal(("Test", "Máy TEST", "https://test.local:5007", true), (env.Code, env.Name, env.Address, env.IsActive));
        Assert.Throws<BusinessRuleException>(() => env.SetKey("abc", "abcd", "admin", Now));
        env.SetKey(new string('f', 64), "wxyz", "admin", Now);
        Assert.Equal(("wxyz", Now), (env.AgentKeyHint, env.KeyCreatedAt));

        env.RecordContact("1.0.0", "0.34.0", "SRV-TEST", Now);
        env.RecordContact(" ", null, null, Now.AddMinutes(1));
        Assert.Equal(("1.0.0", "0.34.0", "SRV-TEST", Now.AddMinutes(1)), (env.AgentVersion, env.AppVersion, env.HostName, env.LastSeenAt));

        env.Update("Máy TEST", null, false);
        Assert.Throws<BusinessRuleException>(() => env.SetKey(new string('e', 64), "abcd", "admin", Now));
    }

    /// <summary>Chế độ bảo trì: bật cần lý do, câu mặc định; bật lại giữ giờ bật; tắt khi đang tắt bị từ chối.</summary>
    [Fact]
    public void CheDoBaoTri_BatTat()
    {
        var mode = MaintenanceModeState.CreateDefault();
        Assert.Equal(MaintenanceModeState.SingletonId, mode.Id);
        Assert.False(mode.IsOn);
        Assert.Throws<BusinessRuleException>(() => mode.TurnOff("Hết bảo trì", "admin", Now));
        Assert.Throws<BusinessRuleException>(() => mode.TurnOn(null, "abc", "admin", Now));

        mode.TurnOn(" ", "Cài bản 0.34.0", "admin", Now);
        Assert.Equal((true, MaintenanceModeState.DefaultMessage, Now), (mode.IsOn, mode.Message, mode.StartedAt));
        mode.TurnOn("Đang khôi phục CSDL", "Đổi câu thông báo", "admin", Now.AddMinutes(5));
        Assert.Equal(("Đang khôi phục CSDL", Now, Now.AddMinutes(5)), (mode.Message, mode.StartedAt, mode.ChangedAt));

        mode.TurnOff("Cài xong", "admin", Now.AddMinutes(10));
        Assert.Equal((false, (DateTimeOffset?)null, "Cài xong"), (mode.IsOn, mode.StartedAt, mode.Reason));
    }
}
