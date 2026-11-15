using Proc.Domain.Common;
using Proc.Domain.Operations;

namespace Proc.Domain.Tests.Organization;

/// <summary>Lát QT6 – phục hồi (design.md mục 21.4 RST): chọn chuỗi Full → Diff → WAL, đứt WAL, trạng thái lần phục hồi, tên CSDL.</summary>
public class RestoreTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 15, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeZoneInfo Utc = TimeZoneInfo.Utc;
    private static readonly Guid FullOld = Guid.CreateVersion7();
    private static readonly Guid FullNew = Guid.CreateVersion7();
    private static readonly Guid DiffA = Guid.CreateVersion7();
    private static readonly Guid DiffB = Guid.CreateVersion7();

    /// <summary>Hai bản Full (ngày 8 và 14), hai Diff của Full ngày 14 (ngày 14 18h, ngày 15 01h).</summary>
    private static readonly RestoreBackupCandidate[] Backups =
    [
        new(FullOld, BackupKind.Full, At(8, 1), "000000010000000000000002", null, "full/8"),
        new(FullNew, BackupKind.Full, At(14, 1), "000000010000000000000010", null, "full/14"),
        new(DiffA, BackupKind.Diff, At(14, 18), "000000010000000000000014", FullNew, "diff/14"),
        new(DiffB, BackupKind.Diff, At(15, 1), "000000010000000000000018", FullNew, "diff/15"),
    ];

    private static DateTimeOffset At(int day, int hour, int minute = 0) => new(2026, 11, day, hour, minute, 0, TimeSpan.Zero);

    /// <summary>WAL đoạn <paramref name="from"/>…<paramref name="to"/> (hex cuối), mỗi đoạn ghi cách nhau 1 giờ tính từ <paramref name="firstAt"/>.</summary>
    private static IEnumerable<WalArchiveFile> Wal(long from, long to, DateTimeOffset firstAt, long timeline = 1, params long[] missing) =>
        Enumerable.Range(0, (int)(to - from + 1))
            .Select(i => from + i)
            .Where(n => !missing.Contains(n))
            .Select(n => new WalArchiveFile(RestorePlanner.SegmentName(timeline, n), firstAt.AddHours(n - from)));

    /// <summary>Số thứ tự đoạn: L × 256 + S; sau …FF là log kế tiếp; file .history / .partial bỏ qua.</summary>
    [Fact]
    public void TenFileWal_SoThuTuDoan()
    {
        Assert.Equal((1L, 2 * 256 + 10L), RestorePlanner.Parse("00000001000000020000000A"));
        Assert.Equal("000000010000000300000000", RestorePlanner.SegmentName(1, 3 * 256));
        Assert.Equal("0000000100000002000000FF", RestorePlanner.SegmentName(1, 2 * 256 + 255));
        Assert.Null(RestorePlanner.Parse("00000002.history"));
        Assert.Null(RestorePlanner.Parse("000000010000000000000003.partial"));
        Assert.Null(RestorePlanner.Parse("000000010000000000000100")); // đoạn ≥ 256 không có với WAL 16 MB
    }

    /// <summary>Mốc ngày 15 03h: Full ngày 14 + Diff ngày 15 01h (Diff mới nhất trước mốc), WAL từ đoạn đầu của Diff tới đoạn ghi sau mốc.</summary>
    [Fact]
    public void ChonFullVaDiffMoiNhatTruocMoc()
    {
        var wal = Wal(0x10, 0x30, At(15, 0));
        var plan = RestorePlanner.Plan(At(15, 3), Now, Backups, wal, Utc);

        Assert.True(plan.Feasible, string.Join(" ", plan.Problems));
        Assert.Equal((FullNew, DiffB), (plan.FullRunId!.Value, plan.DiffRunId!.Value));
        Assert.Equal(("full/14", "diff/15"), (plan.FullPath, plan.DiffPath));
        // Đoạn 0x18 ghi lúc 15/11 08h ≥ mốc 03h → chỉ cần một đoạn.
        Assert.Equal(("000000010000000000000018", "000000010000000000000018", 1), (plan.FirstWal, plan.LastWal, plan.WalCount));
        Assert.Empty(plan.Warnings);
    }

    /// <summary>Mốc trước Diff đầu tiên → chỉ Full (cảnh báo phát lại nhiều WAL); mốc trước mọi Full → không khôi phục được.</summary>
    [Fact]
    public void KhongCoDiffTruocMoc_ChiFull_TruocMoiFull_Chan()
    {
        var wal = Wal(0x10, 0x30, At(14, 0));
        var plan = RestorePlanner.Plan(At(14, 5), Now, Backups, wal, Utc);
        Assert.True(plan.Feasible, string.Join(" ", plan.Problems));
        Assert.Equal(FullNew, plan.FullRunId);
        Assert.Null(plan.DiffRunId);
        Assert.Equal("000000010000000000000010", plan.FirstWal);
        Assert.Equal("000000010000000000000015", plan.LastWal); // 0x15 ghi lúc 05h = mốc
        Assert.Single(plan.Warnings);

        var tooOld = RestorePlanner.Plan(At(7, 0), Now, Backups, wal, Utc);
        Assert.False(tooOld.Feasible);
        Assert.Contains("Không có bản Full nào xong trước mốc", tooOld.Problems[0]);

        Assert.Contains("tương lai", RestorePlanner.Plan(Now.AddMinutes(1), Now, Backups, wal, Utc).Problems[0]);
        Assert.Contains("Chưa có bản sao lưu Full", RestorePlanner.Plan(At(15, 3), Now, [], wal, Utc).Problems[0]);
    }

    /// <summary>Thiếu một đoạn giữa chừng → đứt WAL (nêu tên đoạn thiếu); mốc sau đoạn WAL cuối đã lưu → chặn (PostgreSQL sẽ dừng lỗi).</summary>
    [Fact]
    public void DutWal_VaMocSauWalCuoi()
    {
        var broken = Wal(0x10, 0x30, At(14, 0), 1, 0x12);
        var plan = RestorePlanner.Plan(At(14, 5), Now, Backups, broken, Utc);
        Assert.False(plan.Feasible);
        Assert.Contains("Đứt WAL: thiếu file đoạn 000000010000000000000012 sau 000000010000000000000011", plan.Problems[0]);

        var shortWal = Wal(0x18, 0x1A, At(15, 0));
        var late = RestorePlanner.Plan(At(15, 11), Now, Backups, shortWal, Utc);
        Assert.False(late.Feasible);
        Assert.Contains("Mốc sau lần lưu WAL gần nhất", late.Problems[0]);
        Assert.Equal(At(15, 2), late.LastArchivedAt);
    }

    /// <summary>Sau một lần khôi phục (timeline 2): đoạn ở timeline cao hơn vẫn nối được; cùng số đoạn ở hai timeline lấy timeline cao.</summary>
    [Fact]
    public void DoiTimeline_VanLienTuc()
    {
        var wal = Wal(0x18, 0x1A, At(15, 0)).Concat(Wal(0x1A, 0x1D, At(15, 2, 30), 2));
        var plan = RestorePlanner.Plan(At(15, 4), Now, Backups, wal, Utc);
        Assert.True(plan.Feasible, string.Join(" ", plan.Problems));
        Assert.Equal("000000010000000000000018", plan.FirstWal);
        Assert.Equal("00000002000000000000001C", plan.LastWal);
    }

    /// <summary>Trạng thái: chờ → khôi phục → sẵn sàng → hoán đổi → xong; hoán đổi lỗi về Sẵn sàng; khác phiên bản CSDL không hoán đổi; bỏ.</summary>
    [Fact]
    public void TrangThaiLanPhucHoi()
    {
        Assert.Throws<BusinessRuleException>(() => RestoreRun.QueueDump(Guid.NewGuid(), "procurement", "procurement_restore_1", "abc", "admin", Now));
        Assert.Throws<BusinessRuleException>(() => RestoreRun.QueueDump(Guid.NewGuid(), "procurement", "Sai Tên", "Khôi phục thử", "admin", Now));

        var run = RestoreRun.QueueDump(Guid.NewGuid(), "procurement", "procurement_restore_20261115_1900", "Khôi phục thử", "admin", Now);
        Assert.Equal(RestoreRunStatus.Queued, run.Status);
        Assert.Throws<BusinessRuleException>(() => run.RequestSwap("Hoán đổi", "admin", Now));
        run.Start(Now);
        run.MarkReady("[]", canSwap: false, "Khác phiên bản", Now);
        Assert.Throws<BusinessRuleException>(() => run.RequestSwap("Hoán đổi thật", "admin", Now));
        run.UpdateComparison("[]", canSwap: true, "Cùng phiên bản");
        Assert.Throws<BusinessRuleException>(() => run.RequestSwap("ab", "admin", Now));
        run.RequestSwap("Hoán đổi thật", "admin", Now);
        Assert.Equal(RestoreRunStatus.Swapping, run.Status);
        Assert.Throws<BusinessRuleException>(() => run.Discard("admin", Now));
        run.SwapFailed("không đổi tên được");
        Assert.Equal((RestoreRunStatus.Ready, "Hoán đổi lỗi: không đổi tên được"), (run.Status, run.ErrorMessage));
        run.RequestSwap("Hoán đổi lần hai", "admin", Now);
        Assert.Null(run.ErrorMessage);
        run.CompleteSwap("procurement_old_20261115_2000", null, Now);
        Assert.Equal((RestoreRunStatus.Swapped, "procurement_old_20261115_2000"), (run.Status, run.OldDatabase));
        Assert.Throws<BusinessRuleException>(() => run.Discard("admin", Now));

        var failed = RestoreRun.QueueDump(Guid.NewGuid(), "procurement", "procurement_restore_x", "Khôi phục thử", "admin", Now);
        failed.Fail("pg_restore lỗi", Now);
        failed.Discard("admin", Now.AddMinutes(5));
        Assert.Equal((RestoreRunStatus.Discarded, "admin", Now.AddMinutes(5)), (failed.Status, failed.DiscardedByName, failed.FinishedAt));
    }

    /// <summary>Khôi phục theo mốc: kế hoạch không đủ chuỗi thì chặn; đủ thì Đang khôi phục ngay, CSDL trên máy chủ tạm trùng tên CSDL đang chạy.</summary>
    [Fact]
    public void KhoiPhucTheoMoc_CanKeHoachDu()
    {
        var bad = RestorePlanner.Plan(At(7, 0), Now, Backups, [], Utc);
        Assert.Throws<BusinessRuleException>(() =>
            RestoreRun.QueuePointInTime(bad, null, "procurement", "restore/x", 5434, "Về mốc sáng nay", "admin", Now));

        var plan = RestorePlanner.Plan(At(15, 3), Now, Backups, Wal(0x10, 0x30, At(15, 0)), Utc);
        var run = RestoreRun.QueuePointInTime(plan, "{}", "procurement", "restore/x", 5434, "Về mốc sáng nay", "admin", Now);
        Assert.Equal((RestoreRunStatus.Restoring, "procurement", 5434, DiffB), (run.Status, run.RestoreDatabase, run.TempPort!.Value, run.DiffRunId!.Value));
        Assert.Equal(At(15, 3), run.TargetTime);
    }

    /// <summary>Tên CSDL khôi phục: tên đang chạy + hậu tố + giờ; tên dài cắt cho vừa 63 ký tự.</summary>
    [Fact]
    public void TenCsdlKhoiPhuc()
    {
        var t = new DateTime(2026, 11, 15, 10, 30, 0);
        Assert.Equal("procurement_restore_20261115_1030", RestoreNames.Make("procurement", "restore", t));
        var longName = RestoreNames.Make(new string('a', 60), "old", t);
        Assert.Equal(63, longName.Length);
        Assert.EndsWith("_old_20261115_1030", longName, StringComparison.Ordinal);
        Assert.True(RestoreNames.IsValidDatabaseName(longName));
        Assert.False(RestoreNames.IsValidDatabaseName("1abc"));
        Assert.False(RestoreNames.IsValidDatabaseName("abc;drop"));
    }
}
