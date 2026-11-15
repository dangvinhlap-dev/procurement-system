using Proc.Domain.Operations;

namespace Proc.Domain.Tests.Organization;

/// <summary>Lát QT3b-2 – lịch sao lưu và thời hạn giữ (design.md mục 21.5, B1; quy tắc ở <see cref="BackupPlanner"/>).</summary>
public class BackupPlannerTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 16, 0, 0, 0, TimeSpan.Zero);
    private static readonly BackupScheduleSettings Settings = new(7, new TimeOnly(1, 0), 4, 14);

    /// <summary>Lần sao lưu đã xong, kết thúc cách <paramref name="daysAgo"/> ngày.</summary>
    private static BackupRun Done(BackupKind kind, double daysAgo, string? wal = null, Guid? baseFull = null, bool verified = true)
    {
        var at = Now.AddDays(-daysAgo);
        var run = BackupRun.Queue(kind, BackupTrigger.Manual, null, null, at);
        run.Start(at);
        if (baseFull is { } b) run.SetBaseFull(b);
        run.Complete($"{kind}/{daysAgo}", 1, 1, null, wal, null, at);
        run.SetVerification(verified, null);
        return run;
    }

    /// <summary>Tới giờ mới có lượt; 15/11/2026 là Chủ nhật (thứ 7 theo ISO), 16/11 Thứ hai.</summary>
    [Fact]
    public void LuotLich_VaThuIso()
    {
        Assert.Null(BackupPlanner.DueSlot(new DateTime(2026, 11, 16, 0, 59, 0), new TimeOnly(1, 0)));
        Assert.Equal(new DateOnly(2026, 11, 16), BackupPlanner.DueSlot(new DateTime(2026, 11, 16, 1, 0, 0), new TimeOnly(1, 0)));
        Assert.Equal(new DateOnly(2026, 11, 16), BackupPlanner.DueSlot(new DateTime(2026, 11, 16, 23, 59, 0), new TimeOnly(1, 0)));
        Assert.Equal(7, BackupPlanner.IsoDay(new DateOnly(2026, 11, 15)));
        Assert.Equal(1, BackupPlanner.IsoDay(new DateOnly(2026, 11, 16)));
    }

    /// <summary>Ngày Full → Full; ngày khác → Diff; chưa có Full dùng được → Full ngay.</summary>
    [Fact]
    public void KieuSaoLuu_TheoNgay()
    {
        Assert.Equal(BackupKind.Full, BackupPlanner.BaseKind(new DateOnly(2026, 11, 15), 7, true));
        Assert.Equal(BackupKind.Diff, BackupPlanner.BaseKind(new DateOnly(2026, 11, 16), 7, true));
        Assert.Equal(BackupKind.Full, BackupPlanner.BaseKind(new DateOnly(2026, 11, 16), 7, false));
    }

    /// <summary>
    /// Giữ 4 tuần: Full 35 và 28,1 ngày trước bị dọn kèm Diff của chúng; Full 21 ngày và Full mới giữ cùng Diff; Diff mất gốc dọn; dump quá 14
    /// ngày dọn; WAL giữ từ file đầu của Full cũ nhất còn giữ.
    /// </summary>
    [Fact]
    public void ChonBanCanDon_GiuDungHan()
    {
        var full35 = Done(BackupKind.Full, 35, "000000010000000000000001");
        var full28 = Done(BackupKind.Full, 28.1, "000000010000000000000002");
        var full21 = Done(BackupKind.Full, 21, "000000010000000000000003");
        var full0 = Done(BackupKind.Full, 0.5, "000000010000000000000009");
        var diff30 = Done(BackupKind.Diff, 30, baseFull: full35.Id);
        var diff20 = Done(BackupKind.Diff, 20, baseFull: full21.Id);
        var orphan = Done(BackupKind.Diff, 2, baseFull: Guid.NewGuid());
        var dump15 = Done(BackupKind.Dump, 15);
        var dump13 = Done(BackupKind.Dump, 13);
        var attach = Done(BackupKind.Attachments, 100);
        BackupRun[] all = [full35, full28, full21, full0, diff30, diff20, orphan, dump15, dump13, attach];

        var purge = BackupPlanner.SelectPurge(all, Now, Settings);
        Assert.Equal(new[] { full35.Id, full28.Id, diff30.Id, orphan.Id, dump15.Id }.Order(), purge.Select(r => r.Id).Order());

        foreach (var r in purge) r.MarkPurged(Now);
        Assert.Equal("000000010000000000000003", BackupPlanner.OldestNeededWal(all.Where(r => r.PurgedAt is null)));
    }

    /// <summary>
    /// Sao lưu lỗi lâu ngày: bản Full / dump dùng được mới nhất luôn giữ dù quá hạn (cùng Diff của Full đó); Full không đạt kiểm tra không được tính
    /// là "mới nhất dùng được" và bị dọn khi quá hạn; không có Full ghi file WAL thì không dọn WAL.
    /// </summary>
    [Fact]
    public void LuonGiuBanDungDuocMoiNhat()
    {
        var oldFull = Done(BackupKind.Full, 60);
        var badFull = Done(BackupKind.Full, 40, verified: false);
        var diffOfOld = Done(BackupKind.Diff, 50, baseFull: oldFull.Id);
        var oldDump = Done(BackupKind.Dump, 90);
        BackupRun[] all = [oldFull, badFull, diffOfOld, oldDump];

        var purge = BackupPlanner.SelectPurge(all, Now, Settings);
        Assert.Equal([badFull.Id], purge.Select(r => r.Id));
        Assert.Null(BackupPlanner.OldestNeededWal(all));
    }
}
