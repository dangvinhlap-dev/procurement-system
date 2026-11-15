using Proc.Domain.Common;

namespace Proc.Domain.Operations;

/// <summary>Cách phục hồi (lát QT6, design.md mục 21.4 RST).</summary>
public enum RestoreKind
{
    /// <summary>Từ bản <c>pg_dump</c>: khôi phục sang <b>CSDL mới</b> cùng máy chủ (ứng dụng tự làm – không đụng CSDL đang chạy).</summary>
    Dump = 0,

    /// <summary>Về một <b>mốc thời gian</b>: bản Full (+ Diff) + WAL, bộ thực thi dựng máy chủ CSDL tạm rồi phát lại WAL tới mốc.</summary>
    PointInTime = 1,
}

/// <summary>Trạng thái một lần phục hồi.</summary>
public enum RestoreRunStatus
{
    /// <summary>Đang chờ bộ chạy (pg_dump) / bộ thực thi (mốc thời gian).</summary>
    Queued = 0,

    /// <summary>Đang khôi phục ra CSDL / máy chủ tạm.</summary>
    Restoring = 1,

    /// <summary>Đã có bản khôi phục, chờ người dùng kiểm tra (xem bảng so sánh số dòng) rồi hoán đổi hoặc bỏ.</summary>
    Ready = 2,

    /// <summary>Bộ thực thi đang hoán đổi (chế độ bảo trì, dừng site, đổi tên…).</summary>
    Swapping = 3,

    /// <summary>Đã hoán đổi – bản khôi phục là CSDL đang chạy, bản cũ giữ lại tới khi người quản trị xóa tay (N2).</summary>
    Swapped = 4,

    /// <summary>Lỗi khi khôi phục – không có bản khôi phục dùng được.</summary>
    Failed = 5,

    /// <summary>Đã bỏ: xóa CSDL / thư mục khôi phục tạm, không hoán đổi.</summary>
    Discarded = 6,
}

/// <summary>
/// Một lần phục hồi (lát QT6 – màn hình RST, design.md mục 21.4, nguyên tắc N2): <b>không ghi đè thẳng</b> CSDL đang dùng – khôi phục ra CSDL /
/// máy chủ CSDL tạm → người dùng kiểm tra → hoán đổi (bật bảo trì, đổi tên, giữ bản cũ). Không thuộc công ty nào, chỉ ở tổng công ty; dòng không
/// xóa (lịch sử ai khôi phục gì, khi nào). Khi hoán đổi, bảng này cùng các bảng vận hành khác được chép từ CSDL đang chạy sang bản khôi phục
/// (<c>RestoreCarryOver</c>) nên lịch sử không bị "quay về" theo dữ liệu.
/// </summary>
public sealed class RestoreRun : Entity
{
    /// <summary>Độ dài tối đa tên CSDL PostgreSQL (NAMEDATALEN − 1).</summary>
    public const int DatabaseNameMaxLength = 63;

    /// <summary>Độ dài tối đa đường dẫn / lý do.</summary>
    public const int TextMaxLength = 500;

    /// <summary>Độ dài tối đa thông báo (lỗi, ghi chú).</summary>
    public const int MessageMaxLength = 4000;

    /// <summary>Độ dài tối đa bảng so sánh / kế hoạch (JSON).</summary>
    public const int JsonMaxLength = 200_000;

    /// <summary>Độ dài tối thiểu lý do (như các việc QT khác).</summary>
    public const int ReasonMinLength = 5;

    private RestoreRun()
    {
    }

    /// <summary>Cách phục hồi.</summary>
    public RestoreKind Kind { get; private set; }

    /// <summary>Trạng thái.</summary>
    public RestoreRunStatus Status { get; private set; }

    /// <summary>Bản pg_dump nguồn (<see cref="RestoreKind.Dump"/>).</summary>
    public Guid? DumpRunId { get; private set; }

    /// <summary>Bản Full gốc (<see cref="RestoreKind.PointInTime"/>).</summary>
    public Guid? FullRunId { get; private set; }

    /// <summary>Bản Diff dùng kèm Full (null = chỉ Full + WAL).</summary>
    public Guid? DiffRunId { get; private set; }

    /// <summary>Mốc thời gian cần về (UTC) – chỉ khi khôi phục theo mốc.</summary>
    public DateTimeOffset? TargetTime { get; private set; }

    /// <summary>Tên CSDL đang chạy lúc đặt lần phục hồi (lấy từ chuỗi kết nối chính).</summary>
    public string LiveDatabase { get; private set; } = string.Empty;

    /// <summary>Tên CSDL khôi phục, ví dụ <c>procurement_restore_20261115_1030</c> (cả hai cách: với mốc thời gian là tên trên máy chủ tạm).</summary>
    public string RestoreDatabase { get; private set; } = string.Empty;

    /// <summary>Tên CSDL cũ sau khi hoán đổi (pg_dump), ví dụ <c>procurement_old_20261115_1100</c>; mốc thời gian: thư mục dữ liệu cũ.</summary>
    public string? OldDatabase { get; private set; }

    /// <summary>Thư mục khôi phục tạm (mốc thời gian) tương đối so với <c>Backup:RootPath</c>.</summary>
    public string? RelativePath { get; private set; }

    /// <summary>Cổng máy chủ CSDL tạm (mốc thời gian, mặc định 5434).</summary>
    public int? TempPort { get; private set; }

    /// <summary>Lý do phục hồi.</summary>
    public string Reason { get; private set; } = string.Empty;

    /// <summary>Người đặt.</summary>
    public string? RequestedByName { get; private set; }

    /// <summary>Lúc đặt (UTC).</summary>
    public DateTimeOffset QueuedAt { get; private set; }

    /// <summary>Lúc bắt đầu khôi phục (UTC).</summary>
    public DateTimeOffset? StartedAt { get; private set; }

    /// <summary>Lúc bản khôi phục sẵn sàng kiểm tra (UTC).</summary>
    public DateTimeOffset? ReadyAt { get; private set; }

    /// <summary>Lý do hoán đổi.</summary>
    public string? SwapReason { get; private set; }

    /// <summary>Người bấm hoán đổi.</summary>
    public string? SwapRequestedByName { get; private set; }

    /// <summary>Lúc bấm hoán đổi (UTC).</summary>
    public DateTimeOffset? SwapRequestedAt { get; private set; }

    /// <summary>Lúc kết thúc: hoán đổi xong, lỗi, bỏ (UTC).</summary>
    public DateTimeOffset? FinishedAt { get; private set; }

    /// <summary>Người bỏ bản khôi phục.</summary>
    public string? DiscardedByName { get; private set; }

    /// <summary>Lệnh bộ thực thi gần nhất của lần này (khôi phục theo mốc, hoán đổi).</summary>
    public Guid? AgentCommandId { get; private set; }

    /// <summary>Kế hoạch chuỗi Full → Diff → WAL (JSON) – mốc thời gian.</summary>
    public string? PlanJson { get; private set; }

    /// <summary>Bảng so sánh số dòng CSDL đang chạy với bản khôi phục (JSON).</summary>
    public string? ComparisonJson { get; private set; }

    /// <summary>Hoán đổi được: bản khôi phục cùng phiên bản CSDL (migration cuối) với CSDL đang chạy.</summary>
    public bool? CanSwap { get; private set; }

    /// <summary>Ghi chú (tóm tắt so sánh, lý do không hoán đổi được, kết quả hoán đổi).</summary>
    public string? Note { get; private set; }

    /// <summary>Lỗi gần nhất (khôi phục lỗi; hoán đổi lỗi – bản khôi phục vẫn giữ, trạng thái về Sẵn sàng).</summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>Đặt lần khôi phục từ bản pg_dump vào hàng chờ.</summary>
    /// <param name="dumpRunId">Bản dump dùng được.</param>
    /// <param name="liveDatabase">CSDL đang chạy.</param>
    /// <param name="restoreDatabase">Tên CSDL khôi phục (do phần mềm đặt – xem <see cref="RestoreNames"/>).</param>
    /// <param name="reason">Lý do (≥ 5 ký tự).</param>
    /// <param name="requestedByName">Người đặt.</param>
    /// <param name="now">Thời điểm (UTC).</param>
    /// <returns>Lần phục hồi Đang chờ.</returns>
    /// <exception cref="BusinessRuleException">Thiếu lý do, tên CSDL không hợp lệ.</exception>
    public static RestoreRun QueueDump(
        Guid dumpRunId, string liveDatabase, string restoreDatabase, string? reason, string? requestedByName, DateTimeOffset now)
    {
        var run = Create(RestoreKind.Dump, liveDatabase, restoreDatabase, reason, requestedByName, now);
        run.DumpRunId = dumpRunId;
        return run;
    }

    /// <summary>
    /// Đặt lần khôi phục về mốc thời gian: kế hoạch đã kiểm tra đủ chuỗi (<see cref="RestorePlanner"/>); bộ thực thi nhận lệnh nên trạng thái là
    /// Đang khôi phục ngay.
    /// </summary>
    /// <param name="plan">Kế hoạch dùng được.</param>
    /// <param name="planJson">Kế hoạch dạng JSON (lưu để xem lại).</param>
    /// <param name="liveDatabase">CSDL đang chạy.</param>
    /// <param name="relativePath">Thư mục khôi phục tạm (tương đối so với thư mục sao lưu).</param>
    /// <param name="tempPort">Cổng máy chủ CSDL tạm.</param>
    /// <param name="reason">Lý do.</param>
    /// <param name="requestedByName">Người đặt.</param>
    /// <param name="now">Thời điểm (UTC).</param>
    /// <returns>Lần phục hồi Đang khôi phục.</returns>
    /// <exception cref="BusinessRuleException">Kế hoạch không dùng được, thiếu lý do, cổng sai.</exception>
    public static RestoreRun QueuePointInTime(
        RestorePlan plan, string? planJson, string liveDatabase, string relativePath, int tempPort, string? reason, string? requestedByName,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (!plan.Feasible || plan.FullRunId is null)
            throw new BusinessRuleException("Kế hoạch khôi phục chưa đủ chuỗi Full → Diff → WAL: " + string.Join(" ", plan.Problems));
        if (tempPort is < 1024 or > 65535) throw new BusinessRuleException("Cổng máy chủ CSDL tạm không hợp lệ.");
        if (string.IsNullOrWhiteSpace(relativePath)) throw new BusinessRuleException("Thiếu thư mục khôi phục tạm.");
        // Máy chủ tạm là bản chụp cả máy chủ CSDL nên database trên đó trùng tên CSDL đang chạy.
        var run = Create(RestoreKind.PointInTime, liveDatabase, liveDatabase, reason, requestedByName, now);
        run.FullRunId = plan.FullRunId;
        run.DiffRunId = plan.DiffRunId;
        run.TargetTime = plan.TargetTime;
        run.PlanJson = Cut(planJson, JsonMaxLength);
        run.RelativePath = LogText.Cut(relativePath, TextMaxLength);
        run.TempPort = tempPort;
        run.Status = RestoreRunStatus.Restoring;
        run.StartedAt = now;
        return run;
    }

    private static RestoreRun Create(
        RestoreKind kind, string liveDatabase, string restoreDatabase, string? reason, string? requestedByName, DateTimeOffset now)
    {
        reason = reason?.Trim();
        if (reason is null || reason.Length < ReasonMinLength)
            throw new BusinessRuleException($"Nhập lý do phục hồi (ít nhất {ReasonMinLength} ký tự).");
        if (!RestoreNames.IsValidDatabaseName(liveDatabase) || !RestoreNames.IsValidDatabaseName(restoreDatabase))
            throw new BusinessRuleException("Tên CSDL không hợp lệ (chỉ chữ thường không dấu, số, dấu gạch dưới; tối đa 63 ký tự).");
        return new RestoreRun
        {
            Kind = kind,
            Status = RestoreRunStatus.Queued,
            LiveDatabase = liveDatabase,
            RestoreDatabase = restoreDatabase,
            Reason = LogText.Cut(reason, TextMaxLength)!,
            RequestedByName = LogText.Cut(requestedByName, 256),
            QueuedAt = now,
        };
    }

    /// <summary>Gắn lệnh bộ thực thi (khôi phục theo mốc, hoán đổi).</summary>
    /// <param name="commandId">Id lệnh.</param>
    public void SetAgentCommand(Guid commandId) => AgentCommandId = commandId;

    /// <summary>Bộ chạy bắt đầu khôi phục pg_dump: Đang chờ → Đang khôi phục.</summary>
    /// <param name="now">Thời điểm (UTC).</param>
    /// <exception cref="BusinessRuleException">Không ở trạng thái Đang chờ.</exception>
    public void Start(DateTimeOffset now)
    {
        if (Status != RestoreRunStatus.Queued) throw new BusinessRuleException("Chỉ lần phục hồi đang chờ mới bắt đầu được.");
        Status = RestoreRunStatus.Restoring;
        StartedAt = now;
    }

    /// <summary>Khôi phục xong: ghi bảng so sánh, Đang khôi phục → Sẵn sàng kiểm tra.</summary>
    /// <param name="comparisonJson">Bảng so sánh số dòng (JSON).</param>
    /// <param name="canSwap">Cùng phiên bản CSDL với CSDL đang chạy.</param>
    /// <param name="note">Tóm tắt so sánh / lý do không hoán đổi được.</param>
    /// <param name="now">Thời điểm (UTC).</param>
    /// <exception cref="BusinessRuleException">Không đang khôi phục.</exception>
    public void MarkReady(string? comparisonJson, bool canSwap, string? note, DateTimeOffset now)
    {
        if (Status != RestoreRunStatus.Restoring) throw new BusinessRuleException("Chỉ lần phục hồi đang khôi phục mới ghi xong được.");
        Status = RestoreRunStatus.Ready;
        ComparisonJson = Cut(comparisonJson, JsonMaxLength);
        CanSwap = canSwap;
        Note = LogText.Cut(note, MessageMaxLength);
        ReadyAt = now;
    }

    /// <summary>Ghi lại bảng so sánh (người dùng bấm "So sánh lại" khi đang chờ kiểm tra).</summary>
    /// <param name="comparisonJson">Bảng so sánh.</param>
    /// <param name="canSwap">Hoán đổi được.</param>
    /// <param name="note">Tóm tắt.</param>
    /// <exception cref="BusinessRuleException">Không ở trạng thái Sẵn sàng.</exception>
    public void UpdateComparison(string? comparisonJson, bool canSwap, string? note)
    {
        if (Status != RestoreRunStatus.Ready) throw new BusinessRuleException("Chỉ so sánh lại khi bản khôi phục đang chờ kiểm tra.");
        ComparisonJson = Cut(comparisonJson, JsonMaxLength);
        CanSwap = canSwap;
        Note = LogText.Cut(note, MessageMaxLength);
    }

    /// <summary>Khôi phục lỗi (kể cả khi còn chờ – ví dụ cấu hình thiếu, ứng dụng khởi động lại giữa chừng).</summary>
    /// <param name="message">Lỗi.</param>
    /// <param name="now">Thời điểm (UTC).</param>
    /// <exception cref="BusinessRuleException">Không ở trạng thái Đang chờ / Đang khôi phục.</exception>
    public void Fail(string message, DateTimeOffset now)
    {
        if (Status is not (RestoreRunStatus.Queued or RestoreRunStatus.Restoring))
            throw new BusinessRuleException("Chỉ lần phục hồi đang chờ / đang khôi phục mới ghi lỗi khôi phục.");
        Status = RestoreRunStatus.Failed;
        ErrorMessage = LogText.Cut(message, MessageMaxLength) ?? "Lỗi không rõ.";
        StartedAt ??= now;
        FinishedAt = now;
    }

    /// <summary>
    /// Bấm hoán đổi (đã xác nhận Q6 ở tầng Application): Sẵn sàng → Đang hoán đổi. Chỉ khi bản khôi phục cùng phiên bản CSDL.
    /// </summary>
    /// <param name="reason">Lý do (≥ 5 ký tự).</param>
    /// <param name="byName">Người bấm.</param>
    /// <param name="now">Thời điểm (UTC).</param>
    /// <exception cref="BusinessRuleException">Không ở trạng thái Sẵn sàng, khác phiên bản CSDL, thiếu lý do.</exception>
    public void RequestSwap(string? reason, string? byName, DateTimeOffset now)
    {
        if (Status != RestoreRunStatus.Ready) throw new BusinessRuleException("Chỉ hoán đổi được bản khôi phục đang chờ kiểm tra.");
        if (CanSwap != true)
            throw new BusinessRuleException(
                "Bản khôi phục khác phiên bản CSDL với CSDL đang chạy – không hoán đổi được (chỉ dùng để tra cứu, lấy lại dữ liệu).");
        reason = reason?.Trim();
        if (reason is null || reason.Length < ReasonMinLength)
            throw new BusinessRuleException($"Nhập lý do hoán đổi (ít nhất {ReasonMinLength} ký tự).");
        Status = RestoreRunStatus.Swapping;
        SwapReason = LogText.Cut(reason, TextMaxLength);
        SwapRequestedByName = LogText.Cut(byName, 256);
        SwapRequestedAt = now;
        ErrorMessage = null;
    }

    /// <summary>Hoán đổi xong: Đang hoán đổi → Đã hoán đổi.</summary>
    /// <param name="oldDatabase">Tên CSDL cũ (pg_dump) / thư mục dữ liệu cũ (mốc thời gian) giữ lại.</param>
    /// <param name="note">Ghi chú.</param>
    /// <param name="now">Thời điểm (UTC).</param>
    /// <exception cref="BusinessRuleException">Không đang hoán đổi.</exception>
    public void CompleteSwap(string? oldDatabase, string? note, DateTimeOffset now)
    {
        if (Status != RestoreRunStatus.Swapping) throw new BusinessRuleException("Chỉ lần phục hồi đang hoán đổi mới ghi xong hoán đổi.");
        Status = RestoreRunStatus.Swapped;
        OldDatabase = LogText.Cut(oldDatabase, TextMaxLength);
        Note = LogText.Cut(note, MessageMaxLength) ?? Note;
        FinishedAt = now;
    }

    /// <summary>
    /// Hoán đổi lỗi: bộ thực thi đã trả CSDL cũ về như trước (đổi tên ngược) nên bản khôi phục vẫn còn – Đang hoán đổi → Sẵn sàng, ghi lỗi để
    /// người dùng xem rồi thử lại hoặc bỏ.
    /// </summary>
    /// <param name="message">Lỗi.</param>
    /// <exception cref="BusinessRuleException">Không đang hoán đổi.</exception>
    public void SwapFailed(string message)
    {
        if (Status != RestoreRunStatus.Swapping) throw new BusinessRuleException("Chỉ lần phục hồi đang hoán đổi mới ghi lỗi hoán đổi.");
        Status = RestoreRunStatus.Ready;
        ErrorMessage = LogText.Cut("Hoán đổi lỗi: " + message, MessageMaxLength);
    }

    /// <summary>Bỏ bản khôi phục (đã xóa CSDL / thư mục tạm): Sẵn sàng / Lỗi → Đã bỏ.</summary>
    /// <param name="byName">Người bỏ.</param>
    /// <param name="now">Thời điểm (UTC).</param>
    /// <exception cref="BusinessRuleException">Đang khôi phục / hoán đổi, đã hoán đổi, đã bỏ.</exception>
    public void Discard(string? byName, DateTimeOffset now)
    {
        if (Status is not (RestoreRunStatus.Ready or RestoreRunStatus.Failed))
            throw new BusinessRuleException("Chỉ bỏ được bản khôi phục đang chờ kiểm tra hoặc lần phục hồi lỗi.");
        Status = RestoreRunStatus.Discarded;
        DiscardedByName = LogText.Cut(byName, 256);
        FinishedAt ??= now;
        if (FinishedAt < now) FinishedAt = now;
    }

    private static string? Cut(string? value, int max) =>
        string.IsNullOrEmpty(value) ? null : value.Length <= max ? value : value[..max];
}

/// <summary>Đặt tên CSDL khôi phục / CSDL cũ (lát QT6) – chỉ chữ thường không dấu, số, gạch dưới để dùng thẳng trong tham số công cụ PostgreSQL.</summary>
public static class RestoreNames
{
    /// <summary>
    /// Tên CSDL hợp lệ cho phần mềm tự đặt: bắt đầu bằng chữ thường / gạch dưới, chỉ <c>a-z 0-9 _</c>, tối đa 63 ký tự (PostgreSQL cắt tên dài
    /// hơn – hai tên khác nhau có thể thành một).
    /// </summary>
    /// <param name="name">Tên.</param>
    /// <returns>true khi hợp lệ.</returns>
    public static bool IsValidDatabaseName(string? name) =>
        name is { Length: > 0 and <= RestoreRun.DatabaseNameMaxLength }
        && (char.IsAsciiLetterLower(name[0]) || name[0] == '_')
        && name.All(c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c == '_');

    /// <summary>
    /// Tên có hậu tố, ví dụ <c>procurement</c> + <c>restore</c> lúc 10:30 ngày 15/11/2026 → <c>procurement_restore_20261115_1030</c>. Tên CSDL đang
    /// chạy dài thì cắt bớt phần đầu cho vừa 63 ký tự.
    /// </summary>
    /// <param name="liveDatabase">CSDL đang chạy.</param>
    /// <param name="suffix">Hậu tố: <c>restore</c> hoặc <c>old</c>.</param>
    /// <param name="vietnamTime">Giờ Việt Nam.</param>
    /// <returns>Tên.</returns>
    public static string Make(string liveDatabase, string suffix, DateTime vietnamTime)
    {
        var tail = string.Create(System.Globalization.CultureInfo.InvariantCulture, $"_{suffix}_{vietnamTime:yyyyMMdd_HHmm}");
        var head = liveDatabase.Length + tail.Length > RestoreRun.DatabaseNameMaxLength
            ? liveDatabase[..(RestoreRun.DatabaseNameMaxLength - tail.Length)]
            : liveDatabase;
        return head + tail;
    }
}
