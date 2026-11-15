using Proc.Domain.Common;

namespace Proc.Domain.Operations;

/// <summary>Việc bảo trì CSDL (màn hình DBM – design.md mục 21.4, lát QT4).</summary>
public enum MaintenanceKind
{
    /// <summary><c>VACUUM (ANALYZE)</c> một bảng hoặc cả CSDL: dọn dòng chết, cập nhật thống kê cho bộ lập kế hoạch truy vấn.</summary>
    Vacuum = 0,

    /// <summary><c>REINDEX TABLE CONCURRENTLY</c> một bảng: dựng lại chỉ mục phình to mà không khóa ghi bảng.</summary>
    Reindex = 1,

    /// <summary><c>pg_terminate_backend</c> – ngắt một phiên treo / giữ khóa (việc nguy hiểm, xác nhận Q6). Chạy ngay, không qua hàng chờ.</summary>
    TerminateSession = 2,
}

/// <summary>Ai đặt việc bảo trì.</summary>
public enum MaintenanceTrigger
{
    /// <summary>Người dùng bấm trên màn hình DBM.</summary>
    Manual = 0,

    /// <summary>Lịch bảo trì hằng tuần (lát QT4-2).</summary>
    Schedule = 1,
}

/// <summary>Trạng thái một lần bảo trì.</summary>
public enum MaintenanceRunStatus
{
    /// <summary>Đang chờ – VACUUM / REINDEX chạy lần lượt bằng dịch vụ nền.</summary>
    Queued = 0,

    /// <summary>Đang chạy.</summary>
    Running = 1,

    /// <summary>Xong.</summary>
    Succeeded = 2,

    /// <summary>Lỗi.</summary>
    Failed = 3,
}

/// <summary>
/// Một lần bảo trì CSDL (lát QT4, design.md mục 21.4 DBM): VACUUM (ANALYZE), REINDEX CONCURRENTLY, ngắt phiên. Không thuộc công ty nào – bảo trì
/// cả CSDL, chỉ làm ở tổng công ty (N3). Bảng lịch sử: ghi ai làm, lý do, kết quả; dòng không xóa.
/// </summary>
public sealed class MaintenanceRun : Entity
{
    /// <summary>Độ dài tối đa đối tượng (tên bảng "schema.bảng", mô tả phiên).</summary>
    public const int TargetMaxLength = 300;

    /// <summary>Độ dài tối đa lý do.</summary>
    public const int ReasonMaxLength = 500;

    /// <summary>Độ dài tối thiểu lý do khi bắt buộc (ngắt phiên – Q6): tránh gõ cho có "a", "x".</summary>
    public const int ReasonMinLength = 5;

    /// <summary>Độ dài tối đa thông báo (kết quả, lỗi).</summary>
    public const int MessageMaxLength = 4000;

    private MaintenanceRun()
    {
    }

    /// <summary>Việc bảo trì.</summary>
    public MaintenanceKind Kind { get; private set; }

    /// <summary>Ai đặt.</summary>
    public MaintenanceTrigger Trigger { get; private set; }

    /// <summary>Trạng thái.</summary>
    public MaintenanceRunStatus Status { get; private set; }

    /// <summary>
    /// Đối tượng: tên bảng <c>schema.bảng</c> (VACUUM / REINDEX), null = cả CSDL (chỉ VACUUM); mô tả phiên bị ngắt (ngắt phiên).
    /// </summary>
    public string? Target { get; private set; }

    /// <summary>Số tiến trình (pid) của phiên bị ngắt.</summary>
    public int? SessionPid { get; private set; }

    /// <summary>Lý do (bắt buộc khi ngắt phiên – Q6).</summary>
    public string? Reason { get; private set; }

    /// <summary>Ngày (giờ Việt Nam) của lượt lịch hằng tuần đã sinh ra lần này – chống sinh trùng; null khi chạy tay.</summary>
    public DateOnly? ScheduledDate { get; private set; }

    /// <summary>Tên đăng nhập người bấm; null khi chạy theo lịch.</summary>
    public string? RequestedByName { get; private set; }

    /// <summary>Lúc vào hàng chờ (UTC).</summary>
    public DateTimeOffset QueuedAt { get; private set; }

    /// <summary>Lúc bắt đầu chạy (UTC).</summary>
    public DateTimeOffset? StartedAt { get; private set; }

    /// <summary>Lúc xong / lỗi (UTC).</summary>
    public DateTimeOffset? FinishedAt { get; private set; }

    /// <summary>Dung lượng bảng / CSDL trước khi chạy (byte) – so với sau để thấy hiệu quả.</summary>
    public long? SizeBeforeBytes { get; private set; }

    /// <summary>Dung lượng bảng / CSDL sau khi chạy (byte).</summary>
    public long? SizeAfterBytes { get; private set; }

    /// <summary>Ghi chú kết quả (ví dụ số dòng chết trước / sau).</summary>
    public string? Note { get; private set; }

    /// <summary>Lỗi khi trạng thái <see cref="MaintenanceRunStatus.Failed"/>.</summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>Đặt một lần bảo trì vào hàng chờ (ngắt phiên cũng tạo bằng hàm này rồi chạy ngay).</summary>
    /// <param name="kind">Việc.</param>
    /// <param name="trigger">Ai đặt.</param>
    /// <param name="target">Bảng <c>schema.bảng</c> / mô tả phiên; null = cả CSDL (chỉ VACUUM).</param>
    /// <param name="sessionPid">Pid phiên bị ngắt (bắt buộc khi ngắt phiên).</param>
    /// <param name="reason">Lý do (bắt buộc ≥ <see cref="ReasonMinLength"/> ký tự khi ngắt phiên).</param>
    /// <param name="scheduledDate">Ngày lượt lịch (bắt buộc khi theo lịch).</param>
    /// <param name="requestedByName">Người bấm.</param>
    /// <param name="now">Thời điểm (UTC).</param>
    /// <returns>Lần bảo trì trạng thái Đang chờ.</returns>
    /// <exception cref="BusinessRuleException">
    /// Việc không hợp lệ; REINDEX không có bảng; ngắt phiên thiếu pid / lý do hoặc đặt theo lịch; theo lịch thiếu ngày lượt lịch.
    /// </exception>
    public static MaintenanceRun Queue(
        MaintenanceKind kind, MaintenanceTrigger trigger, string? target, int? sessionPid, string? reason, DateOnly? scheduledDate,
        string? requestedByName, DateTimeOffset now)
    {
        if (!Enum.IsDefined(kind)) throw new BusinessRuleException("Việc bảo trì không hợp lệ.");
        if (!Enum.IsDefined(trigger)) throw new BusinessRuleException("Nguồn đặt bảo trì không hợp lệ.");
        target = string.IsNullOrWhiteSpace(target) ? null : target.Trim();
        reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        // REINDEX cả CSDL khóa từng bảng lần lượt và rất lâu – chỉ cho từng bảng (ke-hoach-qt.md QT4).
        if (kind == MaintenanceKind.Reindex && target is null) throw new BusinessRuleException("REINDEX phải chọn một bảng.");
        if (kind == MaintenanceKind.TerminateSession)
        {
            if (trigger == MaintenanceTrigger.Schedule) throw new BusinessRuleException("Không ngắt phiên theo lịch.");
            if (sessionPid is not > 0) throw new BusinessRuleException("Chưa chọn phiên cần ngắt.");
            // Q6 (design.md mục 21.1): việc nguy hiểm bắt buộc lý do – nhật ký vận hành cho biết vì sao phiên bị ngắt.
            if (reason is null || reason.Length < ReasonMinLength)
                throw new BusinessRuleException($"Nhập lý do ngắt phiên (ít nhất {ReasonMinLength} ký tự).");
        }

        if (trigger == MaintenanceTrigger.Schedule && scheduledDate is null)
            throw new BusinessRuleException("Bảo trì theo lịch phải có ngày của lượt lịch.");
        return new MaintenanceRun
        {
            Kind = kind,
            Trigger = trigger,
            Status = MaintenanceRunStatus.Queued,
            Target = LogText.Cut(target, TargetMaxLength),
            SessionPid = kind == MaintenanceKind.TerminateSession ? sessionPid : null,
            Reason = LogText.Cut(reason, ReasonMaxLength),
            ScheduledDate = trigger == MaintenanceTrigger.Schedule ? scheduledDate : null,
            RequestedByName = LogText.Cut(requestedByName, 256),
            QueuedAt = now,
        };
    }

    /// <summary>Bắt đầu chạy.</summary>
    /// <param name="sizeBeforeBytes">Dung lượng trước khi chạy.</param>
    /// <param name="now">Thời điểm (UTC).</param>
    /// <exception cref="BusinessRuleException">Khi không ở trạng thái Đang chờ.</exception>
    public void Start(long? sizeBeforeBytes, DateTimeOffset now)
    {
        if (Status != MaintenanceRunStatus.Queued) throw new BusinessRuleException("Chỉ lần bảo trì đang chờ mới bắt đầu chạy được.");
        Status = MaintenanceRunStatus.Running;
        SizeBeforeBytes = sizeBeforeBytes;
        StartedAt = now;
    }

    /// <summary>Ghi kết quả chạy xong.</summary>
    /// <param name="sizeAfterBytes">Dung lượng sau khi chạy.</param>
    /// <param name="note">Ghi chú.</param>
    /// <param name="now">Thời điểm (UTC).</param>
    /// <exception cref="BusinessRuleException">Khi không đang chạy.</exception>
    public void Complete(long? sizeAfterBytes, string? note, DateTimeOffset now)
    {
        if (Status != MaintenanceRunStatus.Running) throw new BusinessRuleException("Chỉ lần bảo trì đang chạy mới ghi kết quả được.");
        Status = MaintenanceRunStatus.Succeeded;
        SizeAfterBytes = sizeAfterBytes;
        Note = LogText.Cut(note, MessageMaxLength);
        FinishedAt = now;
    }

    /// <summary>Ghi lỗi – kể cả khi đang chờ (ví dụ ứng dụng khởi động lại giữa chừng, bảng đã bị xóa).</summary>
    /// <param name="message">Lỗi.</param>
    /// <param name="now">Thời điểm (UTC).</param>
    /// <exception cref="BusinessRuleException">Khi lần bảo trì đã kết thúc.</exception>
    public void Fail(string message, DateTimeOffset now)
    {
        if (Status is MaintenanceRunStatus.Succeeded or MaintenanceRunStatus.Failed) throw new BusinessRuleException("Lần bảo trì đã kết thúc.");
        Status = MaintenanceRunStatus.Failed;
        ErrorMessage = LogText.Cut(message, MessageMaxLength) ?? "Lỗi không rõ.";
        StartedAt ??= now;
        FinishedAt = now;
    }
}

/// <summary>Tham số lịch bảo trì CSDL hằng tuần (của tổng công ty – lát QT4-2).</summary>
/// <param name="Day">Ngày chạy: 1 = Thứ hai … 7 = Chủ nhật.</param>
/// <param name="Time">Giờ chạy (giờ Việt Nam).</param>
public sealed record MaintenanceScheduleSettings(int Day, TimeOnly Time);

/// <summary>
/// Lịch bảo trì CSDL hằng tuần (lát QT4-2, hàm thuần): mỗi tuần một lượt <c>VACUUM (ANALYZE)</c> cả CSDL vào ngày <c>DB_MAINT_DAY</c> lúc
/// <c>DB_MAINT_TIME</c> giờ Việt Nam. Ứng dụng tắt đúng giờ thì chạy bù khi bật lại <b>trong cùng ngày</b> (không bù ngày khác – autovacuum của
/// PostgreSQL vẫn chạy hằng ngày, lượt tuần chỉ là lớp bảo hiểm), như lịch sao lưu (<see cref="BackupPlanner"/>).
/// </summary>
public static class MaintenancePlanner
{
    /// <summary>Ngày của lượt lịch đã tới giờ chạy; null khi hôm nay không phải ngày bảo trì hoặc chưa tới giờ.</summary>
    /// <param name="nowVietnam">Giờ hiện tại theo giờ Việt Nam.</param>
    /// <param name="settings">Ngày, giờ chạy.</param>
    /// <returns>Ngày lượt lịch.</returns>
    /// <example>Ngày 7 (Chủ nhật), 03:00: Chủ nhật 22/11/2026 02:59 → null; 03:00 → 22/11/2026; Thứ hai 23/11 → null.</example>
    public static DateOnly? DueSlot(DateTime nowVietnam, MaintenanceScheduleSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var today = DateOnly.FromDateTime(nowVietnam);
        return BackupPlanner.IsoDay(today) == settings.Day && TimeOnly.FromDateTime(nowVietnam) >= settings.Time ? today : null;
    }

    /// <summary>Lượt lịch kế tiếp (giờ Việt Nam) – hiện trên màn hình DBM.</summary>
    /// <param name="nowVietnam">Giờ hiện tại theo giờ Việt Nam.</param>
    /// <param name="settings">Ngày, giờ chạy.</param>
    /// <param name="todayDone">Lượt hôm nay (nếu hôm nay là ngày bảo trì) đã đặt.</param>
    /// <returns>Ngày giờ lượt kế tiếp.</returns>
    /// <example>Ngày 7, 03:00, Thứ tư 18/11/2026 10:00 → Chủ nhật 22/11/2026 03:00.</example>
    public static DateTime NextRun(DateTime nowVietnam, MaintenanceScheduleSettings settings, bool todayDone)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var today = DateOnly.FromDateTime(nowVietnam);
        var offset = (settings.Day - BackupPlanner.IsoDay(today) + 7) % 7;
        // Hôm nay là ngày bảo trì mà lượt đã đặt rồi thì lượt kế tiếp là tuần sau (chưa tới giờ hoặc chưa đặt thì vẫn là hôm nay).
        if (offset == 0 && todayDone) offset = 7;
        return today.AddDays(offset).ToDateTime(settings.Time);
    }
}
