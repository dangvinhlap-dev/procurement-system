using Proc.Domain.Common;

namespace Proc.Domain.Purchasing;

/// <summary>
/// Một lần nhắc người duyệt khi bước duyệt chờ quá hạn (lát O2c, design.md mục 19.3 – quyết định Q9 "nhắc sau 1 ngày làm việc"). Mỗi bước tối đa
/// <c>ApprovalReminder:MaxReminders</c> lần, số lần đánh 1, 2…; khóa duy nhất (bước, lần) để hai tiến trình cùng quét không nhắc trùng. Không gắn
/// [Auditable]: là dấu vết của tin nhắn hệ thống như <c>Notification</c>; bảng riêng thay vì thêm cột vào <see cref="ApprovalStep"/> để dịch vụ nền
/// không đụng phiên bản dòng (xmin) của bước khi người dùng đang duyệt.
/// </summary>
public sealed class ApprovalReminder : Entity, ICompanyOwned
{
    private ApprovalReminder()
    {
    }

    /// <inheritdoc />
    public Guid CompanyId { get; private set; }

    /// <summary>Bước duyệt được nhắc.</summary>
    public Guid ApprovalStepId { get; private set; }

    /// <summary>Lần nhắc thứ mấy của bước (từ 1).</summary>
    public int Sequence { get; private set; }

    /// <summary>Thời điểm nhắc (UTC).</summary>
    public DateTimeOffset RemindedAt { get; private set; }

    /// <summary>Số người nhận thông báo nhắc (0 = không còn ai duyệt được bước – vẫn ghi để không quét lại liên tục).</summary>
    public int RecipientCount { get; private set; }

    /// <summary>Ghi một lần nhắc.</summary>
    /// <param name="step">Bước đang chờ quá hạn.</param>
    /// <param name="sequence">Lần nhắc (≥ 1).</param>
    /// <param name="remindedAt">Thời điểm nhắc (UTC).</param>
    /// <param name="recipientCount">Số người nhận.</param>
    /// <returns>Lần nhắc mới.</returns>
    /// <exception cref="BusinessRuleException">Khi bước không còn chờ duyệt hoặc số lần nhắc không hợp lệ.</exception>
    public static ApprovalReminder Create(ApprovalStep step, int sequence, DateTimeOffset remindedAt, int recipientCount)
    {
        if (step.Status != ApprovalStepStatus.Pending) throw new BusinessRuleException("Chỉ nhắc bước còn chờ duyệt.");
        if (sequence < 1) throw new BusinessRuleException("Lần nhắc phải từ 1.");
        return new()
        {
            CompanyId = step.CompanyId, ApprovalStepId = step.Id, Sequence = sequence, RemindedAt = remindedAt,
            RecipientCount = Math.Max(0, recipientCount),
        };
    }
}

/// <summary>
/// Hạn duyệt theo ngày làm việc (lát O2c, design.md mục 19.3): bước đến lượt quá <b>1 ngày làm việc</b> thì nhắc. Ngày làm việc = thứ Hai đến thứ
/// Sáu, chưa có lịch nghỉ lễ (giống hạn SLA – <see cref="PurchaseSla"/>).
/// </summary>
public static class ApprovalOverdue
{
    /// <summary>
    /// Cộng một ngày làm việc vào thời điểm cho trước, tính theo giờ địa phương của <paramref name="zone"/>: cùng giờ của ngày làm việc kế tiếp.
    /// Thời điểm rơi vào thứ 7/CN thì bắt đầu tính từ 0 giờ thứ 2 kế tiếp.
    /// Ví dụ (giờ Việt Nam): thứ 2 09/11/2026 10:00 → thứ 3 10/11 10:00; thứ 6 13/11 15:00 → thứ 2 16/11 15:00;
    /// thứ 7 14/11 09:00 → thứ 3 17/11 00:00 (đến lượt cuối tuần thì hết ngày thứ 2 mới quá hạn).
    /// </summary>
    /// <param name="from">Mốc bắt đầu.</param>
    /// <param name="zone">Múi giờ tính ngày làm việc (giờ Việt Nam).</param>
    /// <returns>Thời điểm hết một ngày làm việc, theo giờ địa phương của <paramref name="zone"/>.</returns>
    public static DateTimeOffset AddWorkingDay(DateTimeOffset from, TimeZoneInfo zone)
    {
        var local = TimeZoneInfo.ConvertTime(from, zone);
        if (!IsWorkingDay(DateOnly.FromDateTime(local.DateTime)))
        {
            var day = DateOnly.FromDateTime(local.DateTime);
            while (!IsWorkingDay(day)) day = day.AddDays(1);
            var midnight = day.ToDateTime(TimeOnly.MinValue);
            local = new DateTimeOffset(midnight, zone.GetUtcOffset(midnight));
        }

        var due = local.AddDays(1);
        while (!IsWorkingDay(DateOnly.FromDateTime(due.DateTime))) due = due.AddDays(1);
        return due;
    }

    /// <summary>Ngày làm việc = thứ Hai đến thứ Sáu.</summary>
    /// <param name="day">Ngày.</param>
    /// <returns>true nếu là ngày làm việc.</returns>
    public static bool IsWorkingDay(DateOnly day) => day.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday);

    /// <summary>
    /// Thời điểm bước bắt đầu đến lượt: lúc gửi duyệt, hoặc lúc bước đứng trước nó duyệt xong – cấp thấp hơn cùng công ty cùng chặng, hoặc mọi bước
    /// của chặng trước (chặng sau chỉ bắt đầu khi chặng trước xong – <see cref="ApprovalProgress"/>). Ví dụ gửi 09/11 08:00, cấp 1 duyệt 10/11
    /// 14:00 → cấp 2 đến lượt 10/11 14:00. Bước của công ty khác cùng chặng (duyệt song song) không làm đổi mốc.
    /// </summary>
    /// <param name="step">Bước đang chờ.</param>
    /// <param name="round">Các bước cùng lượt gửi (mọi trạng thái).</param>
    /// <returns>Mốc đến lượt; null khi bước lập trước bản 0.17.0 chưa có thời điểm gửi và chưa cấp nào duyệt.</returns>
    public static DateTimeOffset? WaitingSince(ApprovalStep step, IEnumerable<ApprovalStep> round)
    {
        var marks = round
            .Where(s => s.Status == ApprovalStepStatus.Approved && s.ActedAt is not null && s.Round == step.Round
                && (s.Stage < step.Stage || (s.Stage == step.Stage && s.CompanyId == step.CompanyId && s.Level < step.Level)))
            .Select(s => s.ActedAt!.Value)
            .ToList();
        if (step.SubmittedAt is { } submitted) marks.Add(submitted);
        return marks.Count == 0 ? null : marks.Max();
    }
}
