using Proc.Domain.Common;
using Proc.Domain.Purchasing;

namespace Proc.Domain.Tests.Purchasing;

/// <summary>Hạn duyệt theo ngày làm việc và mốc đến lượt của bước (lát O2c, design.md mục 19.3).</summary>
public class ApprovalOverdueTests
{
    /// <summary>Giờ Việt Nam cố định UTC+7 – không phụ thuộc tên múi giờ của máy chạy test.</summary>
    private static readonly TimeZoneInfo Vn = TimeZoneInfo.CreateCustomTimeZone("VN+7", TimeSpan.FromHours(7), "VN", "VN");

    private static DateTimeOffset At(int day, int hour, int minute = 0) => new(2026, 11, day, hour, minute, 0, TimeSpan.FromHours(7));

    [Theory]
    // Thứ 2 → thứ 3 cùng giờ.
    [InlineData(9, 10, 10, 10)]
    // Thứ 6 → thứ 2 tuần sau cùng giờ (bỏ thứ 7, CN).
    [InlineData(13, 15, 16, 15)]
    // Thứ 7, CN → tính từ 0 giờ thứ 2 nên hết thứ 2 (0 giờ thứ 3) mới quá hạn.
    [InlineData(14, 9, 17, 0)]
    [InlineData(15, 23, 17, 0)]
    public void AddWorkingDay_BoQuaCuoiTuan(int fromDay, int fromHour, int dueDay, int dueHour)
    {
        var due = ApprovalOverdue.AddWorkingDay(At(fromDay, fromHour), Vn);
        Assert.Equal(At(dueDay, dueHour), due);
        // Mốc lưu UTC (CSDL) cho cùng kết quả.
        Assert.Equal(At(dueDay, dueHour), ApprovalOverdue.AddWorkingDay(At(fromDay, fromHour).ToUniversalTime(), Vn));
    }

    [Fact]
    public void WaitingSince_TheoCapTruocCungCongTyVaChangTruoc()
    {
        var company = Guid.CreateVersion7();
        var other = Guid.CreateVersion7();
        var doc = Guid.CreateVersion7();
        var level1 = ApprovalStep.Create(company, "PR", doc, 1, 1, null);
        var level2 = ApprovalStep.Create(company, "PR", doc, 1, 2, null);
        var parallel = ApprovalStep.Create(other, "PR", doc, 1, 1, null);
        var stage2 = ApprovalStep.Create(company, "PR", doc, 1, 1, null, stage: 2);
        var round = new[] { level1, level2, parallel, stage2 };
        foreach (var s in round) s.MarkSubmitted(At(9, 8), null);

        // Mới gửi: mọi bước tính từ lúc gửi.
        Assert.Equal(At(9, 8), ApprovalOverdue.WaitingSince(level1, round));
        Assert.Equal(At(9, 8), ApprovalOverdue.WaitingSince(parallel, round));

        level1.Approve(Guid.CreateVersion7(), At(10, 14), null);
        // Cấp 2 cùng công ty đến lượt khi cấp 1 duyệt; công ty song song không đổi mốc.
        Assert.Equal(At(10, 14), ApprovalOverdue.WaitingSince(level2, round));
        Assert.Equal(At(9, 8), ApprovalOverdue.WaitingSince(parallel, round));

        level2.Approve(Guid.CreateVersion7(), At(11, 9), null);
        parallel.Approve(Guid.CreateVersion7(), At(12, 16), null);
        // Chặng 2 bắt đầu khi bước cuối của chặng 1 xong.
        Assert.Equal(At(12, 16), ApprovalOverdue.WaitingSince(stage2, round));
    }

    [Fact]
    public void WaitingSince_BuocCuChuaCoThoiDiemGui_TraNull()
    {
        var step = ApprovalStep.Create(Guid.CreateVersion7(), "PO", Guid.CreateVersion7(), 1, 1, null);
        Assert.Null(ApprovalOverdue.WaitingSince(step, [step]));
    }

    [Fact]
    public void ApprovalReminder_ChiNhacBuocConCho()
    {
        var step = ApprovalStep.Create(Guid.CreateVersion7(), "PO", Guid.CreateVersion7(), 1, 1, null);
        var reminder = ApprovalReminder.Create(step, 1, At(10, 9), 2);
        Assert.Equal((step.Id, step.CompanyId, 1, 2), (reminder.ApprovalStepId, reminder.CompanyId, reminder.Sequence, reminder.RecipientCount));
        Assert.Throws<BusinessRuleException>(() => ApprovalReminder.Create(step, 0, At(10, 9), 1));
        step.Approve(Guid.CreateVersion7(), At(10, 10), null);
        Assert.Throws<BusinessRuleException>(() => ApprovalReminder.Create(step, 2, At(10, 11), 1));
    }
}
