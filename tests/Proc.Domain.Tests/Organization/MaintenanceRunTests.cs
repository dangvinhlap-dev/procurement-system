using Proc.Domain.Common;
using Proc.Domain.Operations;

namespace Proc.Domain.Tests.Organization;

/// <summary>Lát QT4 – vòng đời lần bảo trì CSDL, ràng buộc ngắt phiên (Q6), lịch hằng tuần (design.md mục 21.4 DBM).</summary>
public class MaintenanceRunTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 15, 18, 0, 0, TimeSpan.Zero);

    /// <summary>VACUUM cả CSDL (không bảng) được; chờ → chạy → xong; xong rồi không ghi lỗi, không chạy lại.</summary>
    [Fact]
    public void VongDoi_ChoChayXong()
    {
        var run = MaintenanceRun.Queue(MaintenanceKind.Vacuum, MaintenanceTrigger.Manual, "  ", 99, "  ", new DateOnly(2026, 11, 15), "admin", Now);
        Assert.Null(run.Target);
        Assert.Null(run.SessionPid);
        Assert.Null(run.Reason);
        Assert.Null(run.ScheduledDate);
        Assert.Throws<BusinessRuleException>(() => run.Complete(1, null, Now));

        run.Start(1000, Now);
        Assert.Throws<BusinessRuleException>(() => run.Start(1000, Now));
        run.Complete(800, "Xong", Now.AddSeconds(5));
        Assert.Equal((MaintenanceRunStatus.Succeeded, 1000L, 800L), (run.Status, run.SizeBeforeBytes, run.SizeAfterBytes));
        Assert.Throws<BusinessRuleException>(() => run.Fail("x", Now));
    }

    /// <summary>REINDEX phải có bảng; theo lịch phải có ngày lượt lịch; lỗi khi đang chờ ghi giờ bắt đầu = giờ lỗi.</summary>
    [Fact]
    public void Reindex_CanBang_LichCanNgay()
    {
        Assert.Throws<BusinessRuleException>(() =>
            MaintenanceRun.Queue(MaintenanceKind.Reindex, MaintenanceTrigger.Manual, null, null, null, null, "admin", Now));
        Assert.Throws<BusinessRuleException>(() =>
            MaintenanceRun.Queue(MaintenanceKind.Vacuum, MaintenanceTrigger.Schedule, null, null, null, null, null, Now));

        var run = MaintenanceRun.Queue(MaintenanceKind.Reindex, MaintenanceTrigger.Manual, "public.Item", null, null, null, "admin", Now);
        run.Fail(new string('x', 5000), Now.AddMinutes(1));
        Assert.Equal(MaintenanceRunStatus.Failed, run.Status);
        Assert.Equal(Now.AddMinutes(1), run.StartedAt);
        Assert.Equal(MaintenanceRun.MessageMaxLength, run.ErrorMessage!.Length);
    }

    /// <summary>Ngắt phiên (Q6): bắt buộc pid, lý do ≥ 5 ký tự; không đặt theo lịch.</summary>
    [Fact]
    public void NgatPhien_CanPidVaLyDo()
    {
        Assert.Throws<BusinessRuleException>(() =>
            MaintenanceRun.Queue(MaintenanceKind.TerminateSession, MaintenanceTrigger.Manual, "pid 1", null, "Phiên treo", null, "admin", Now));
        Assert.Throws<BusinessRuleException>(() =>
            MaintenanceRun.Queue(MaintenanceKind.TerminateSession, MaintenanceTrigger.Manual, "pid 1", 1, "treo", null, "admin", Now));
        Assert.Throws<BusinessRuleException>(() => MaintenanceRun.Queue(
            MaintenanceKind.TerminateSession, MaintenanceTrigger.Schedule, "pid 1", 1, "Phiên treo", new DateOnly(2026, 11, 15), null, Now));

        var run = MaintenanceRun.Queue(MaintenanceKind.TerminateSession, MaintenanceTrigger.Manual, "pid 1234", 1234, " Phiên treo giữ khóa ", null,
            "admin", Now);
        Assert.Equal((1234, "Phiên treo giữ khóa"), (run.SessionPid, run.Reason));
    }

    /// <summary>
    /// Lịch tuần (mặc định Chủ nhật 03:00): Chủ nhật 22/11/2026 02:59 chưa tới giờ, 03:00 tới; Thứ hai không phải ngày bảo trì. Lượt kế tiếp:
    /// Thứ tư → Chủ nhật cùng tuần; Chủ nhật đã đặt → Chủ nhật tuần sau; Chủ nhật chưa đặt → chính hôm đó.
    /// </summary>
    [Fact]
    public void LichTuan_DenGio_LuotKeTiep()
    {
        var settings = new MaintenanceScheduleSettings(7, new TimeOnly(3, 0));
        var sunday = new DateTime(2026, 11, 22);
        Assert.Null(MaintenancePlanner.DueSlot(sunday.AddHours(2).AddMinutes(59), settings));
        Assert.Equal(new DateOnly(2026, 11, 22), MaintenancePlanner.DueSlot(sunday.AddHours(3), settings));
        Assert.Null(MaintenancePlanner.DueSlot(sunday.AddDays(1).AddHours(10), settings));

        Assert.Equal(sunday.AddHours(3), MaintenancePlanner.NextRun(new DateTime(2026, 11, 18, 10, 0, 0), settings, false));
        Assert.Equal(sunday.AddDays(7).AddHours(3), MaintenancePlanner.NextRun(sunday.AddHours(4), settings, true));
        Assert.Equal(sunday.AddHours(3), MaintenancePlanner.NextRun(sunday.AddHours(1), settings, false));
    }
}
