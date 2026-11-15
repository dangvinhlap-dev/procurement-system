using Proc.Application.Operations;
using Proc.Domain.Common;
using Proc.Domain.Operations;
using Proc.Domain.Organization;

namespace Proc.Domain.Tests.Organization;

/// <summary>Lát QT3b – vòng đời lần sao lưu, chữ / đèn tình trạng, tham số lịch sao lưu (design.md mục 21.5, B1).</summary>
public class BackupRunTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 15, 18, 0, 0, TimeSpan.Zero);

    /// <summary>Theo lịch phải có ngày lượt lịch; chạy tay bỏ ngày; chỉ chờ → chạy → xong; xong rồi không ghi lỗi được.</summary>
    [Fact]
    public void VongDoi_ChoChayXong()
    {
        Assert.Throws<BusinessRuleException>(() => BackupRun.Queue(BackupKind.Full, BackupTrigger.Schedule, null, null, Now));
        var manual = BackupRun.Queue(BackupKind.Dump, BackupTrigger.Manual, new DateOnly(2026, 11, 16), "admin", Now);
        Assert.Null(manual.ScheduledDate);
        Assert.Throws<BusinessRuleException>(() => manual.Complete("x", 1, 1, null, null, null, Now));

        manual.Start(Now);
        Assert.Throws<BusinessRuleException>(() => manual.Start(Now));
        Assert.Throws<BusinessRuleException>(() => manual.SetBaseFull(Guid.NewGuid()));
        manual.Complete("dump/a.dump", 10, 1, "ABCDEF", "000000010000000000000002", null, Now.AddMinutes(1));
        Assert.Equal(("abcdef", "000000010000000000000002"), (manual.Sha256, manual.StartWalFile));
        Assert.True(manual.IsUsable);
        Assert.Throws<BusinessRuleException>(() => manual.Fail("x", Now));

        manual.SetVerification(false, "hỏng");
        Assert.False(manual.IsUsable);
    }

    /// <summary>File WAL không đủ 24 ký tự thì bỏ; đã dọn thì không dùng được; lỗi khi đang chờ vẫn ghi được (giờ bắt đầu = giờ lỗi).</summary>
    [Fact]
    public void WalLa_DaDon_LoiKhiDangCho()
    {
        var full = BackupRun.Queue(BackupKind.Full, BackupTrigger.Schedule, new DateOnly(2026, 11, 15), null, Now);
        full.Start(Now);
        full.Complete("full/x", 1, 1, null, "abc", null, Now);
        Assert.Null(full.StartWalFile);
        full.MarkPurged(Now);
        Assert.False(full.IsUsable);

        var queued = BackupRun.Queue(BackupKind.Diff, BackupTrigger.Manual, null, null, Now);
        queued.Fail("Ứng dụng dừng", Now.AddMinutes(5));
        Assert.Equal((BackupRunStatus.Failed, Now.AddMinutes(5)), (queued.Status, queued.StartedAt!.Value));
    }

    /// <summary>Chữ / đèn: chờ, chạy trung tính; lỗi đỏ; kiểm tra không đạt đỏ; nơi thứ hai lỗi vàng; đạt xanh; đã dọn trung tính.</summary>
    [Fact]
    public void ChuVaDen()
    {
        var run = BackupRun.Queue(BackupKind.Attachments, BackupTrigger.Manual, null, null, Now);
        Assert.Equal(("Đang chờ", "Neutral"), BackupLabels.Status(run));
        run.Start(Now);
        Assert.Equal(("Đang chạy", "Neutral"), BackupLabels.Status(run));
        run.Complete("attachments", 0, 0, null, null, null, Now);
        Assert.Equal(("Xong", "Ok"), BackupLabels.Status(run));
        run.SetSecondary(false, "mạng");
        Assert.Equal(("Xong – chưa chép nơi thứ hai", "Warning"), BackupLabels.Status(run));
        run.SetVerification(false, "thiếu file");
        Assert.Equal("Error", BackupLabels.Status(run).Light);
        run.MarkPurged(Now);
        Assert.Equal(("Đã dọn", "Neutral"), BackupLabels.Status(run));

        var failed = BackupRun.Queue(BackupKind.Full, BackupTrigger.Manual, null, null, Now);
        failed.Fail("x", Now);
        Assert.Equal(("Lỗi", "Error"), BackupLabels.Status(failed));
        Assert.Equal("pg_dump", BackupLabels.Kind(BackupKind.Dump));
    }

    /// <summary>Tham số: mặc định Chủ nhật 01:00, 4 tuần, 14 ngày; ngoài khoảng báo lỗi; null về mặc định; giờ bỏ giây.</summary>
    [Fact]
    public void ThamSoLichSaoLuu()
    {
        var p = CompanyParameter.Create(Guid.NewGuid());
        Assert.Equal((7, new TimeOnly(1, 0), 4, 14), (p.BackupFullDay, p.BackupTime, p.BackupKeepWeeks, p.DumpKeepDays));
        Assert.Throws<BusinessRuleException>(() => p.ChangeBackupFullDay(0));
        Assert.Throws<BusinessRuleException>(() => p.ChangeBackupFullDay(8));
        Assert.Throws<BusinessRuleException>(() => p.ChangeBackupKeepWeeks(0));
        Assert.Throws<BusinessRuleException>(() => p.ChangeBackupKeepWeeks(53));
        Assert.Throws<BusinessRuleException>(() => p.ChangeDumpKeepDays(366));
        p.ChangeBackupFullDay(6);
        p.ChangeBackupTime(new TimeOnly(23, 30, 15));
        p.ChangeBackupKeepWeeks(8);
        p.ChangeDumpKeepDays(30);
        Assert.Equal((6, new TimeOnly(23, 30), 8, 30), (p.BackupFullDay, p.BackupTime, p.BackupKeepWeeks, p.DumpKeepDays));
        p.ChangeBackupFullDay(null);
        p.ChangeBackupTime(null);
        Assert.Equal((7, new TimeOnly(1, 0)), (p.BackupFullDay, p.BackupTime));
    }
}
