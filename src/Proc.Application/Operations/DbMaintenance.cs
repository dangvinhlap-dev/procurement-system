using Proc.Domain.Operations;

namespace Proc.Application.Operations;

/// <summary>Cấu hình bảo trì CSDL – mục <c>DbMaintenance</c> trong appsettings (lát QT4, design.md mục 21.4 DBM).</summary>
public sealed class DbMaintenanceOptions
{
    /// <summary>Tên mục cấu hình.</summary>
    public const string Section = "DbMaintenance";

    /// <summary>
    /// Bật bộ chạy nền xử lý hàng chờ VACUUM / REINDEX. Môi trường Testing (test tích hợp) tắt để test tự gọi
    /// <see cref="IDbMaintenanceExecutor.ProcessQueueAsync"/>.
    /// </summary>
    public bool WorkerEnabled { get; set; } = true;

    /// <summary>Bật lịch VACUUM (ANALYZE) hằng tuần (lát QT4-2). Máy DEV, Testing tắt.</summary>
    public bool ScheduleEnabled { get; set; } = true;

    /// <summary>Thời gian tối đa một lệnh VACUUM / REINDEX (phút) – quá thì PostgreSQL hủy lệnh, ghi lỗi.</summary>
    public int CommandTimeoutMinutes { get; set; } = 240;
}

/// <summary>Chữ người dùng phải gõ để xác nhận việc nguy hiểm (Q6 – design.md mục 21.1).</summary>
public static class DangerConfirmations
{
    /// <summary>Ngắt phiên CSDL (màn hình DBM – lát QT4).</summary>
    public const string TerminateSession = "NGẮT PHIÊN";
}

/// <summary>
/// Xác nhận việc nguy hiểm theo Q6 (design.md mục 21.1): nhập lại mật khẩu của chính người đang đăng nhập + lý do + gõ đúng chữ xác nhận. Dùng
/// chung cho ngắt phiên (QT4) và các việc QT sau (phục hồi, cài PROD, quay về bản cũ, làm mới TEST).
/// </summary>
public interface IDangerousActionGuard
{
    /// <summary>Kiểm tra xác nhận; sai mật khẩu ghi nhật ký vận hành (để thấy ai thử nhiều lần).</summary>
    /// <param name="action">Tên việc (ghi nhật ký), ví dụ "Ngắt phiên CSDL".</param>
    /// <param name="password">Mật khẩu nhập lại.</param>
    /// <param name="confirmation">Chữ đã gõ.</param>
    /// <param name="expectedConfirmation">Chữ phải gõ (so không phân biệt hoa thường, bỏ khoảng trắng hai đầu).</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Task.</returns>
    /// <exception cref="Proc.Domain.Common.BusinessRuleException">Thiếu / sai mật khẩu, gõ sai chữ xác nhận.</exception>
    Task VerifyAsync(string action, string? password, string? confirmation, string expectedConfirmation, CancellationToken cancellationToken);
}

/// <summary>Một bảng trên tab "Bảng" của DBM.</summary>
/// <param name="Schema">Schema.</param>
/// <param name="Name">Tên bảng.</param>
/// <param name="TotalBytes">Dung lượng cả bảng (dữ liệu + chỉ mục + TOAST).</param>
/// <param name="TableBytes">Dung lượng dữ liệu (kể cả TOAST).</param>
/// <param name="IndexBytes">Dung lượng chỉ mục.</param>
/// <param name="LiveRows">Số dòng sống (ước tính của PostgreSQL).</param>
/// <param name="DeadRows">Số dòng chết chờ dọn.</param>
/// <param name="DeadPercent">Tỷ lệ dòng chết (%), 1 chữ số lẻ.</param>
/// <param name="LastVacuum">Lần VACUUM tay gần nhất (UTC).</param>
/// <param name="LastAutoVacuum">Lần autovacuum gần nhất (UTC).</param>
/// <param name="LastAnalyze">Lần ANALYZE tay gần nhất (UTC).</param>
/// <param name="LastAutoAnalyze">Lần autoanalyze gần nhất (UTC).</param>
/// <param name="Light">Đèn: Ok, Warning (nhiều dòng chết).</param>
/// <param name="Note">Lý do đèn vàng và cách xử lý.</param>
public sealed record DbTableStatDto(
    string Schema, string Name, long TotalBytes, long TableBytes, long IndexBytes, long LiveRows, long DeadRows, decimal DeadPercent,
    DateTimeOffset? LastVacuum, DateTimeOffset? LastAutoVacuum, DateTimeOffset? LastAnalyze, DateTimeOffset? LastAutoAnalyze, string Light,
    string? Note);

/// <summary>Tab "Bảng" của DBM.</summary>
/// <param name="DatabaseName">Tên CSDL ứng dụng.</param>
/// <param name="DatabaseBytes">Dung lượng cả CSDL.</param>
/// <param name="CheckedAt">Lúc đọc số liệu (UTC).</param>
/// <param name="Tables">Các bảng, lớn nhất trước.</param>
public sealed record DbTableStatsDto(string DatabaseName, long DatabaseBytes, DateTimeOffset CheckedAt, IReadOnlyList<DbTableStatDto> Tables);

/// <summary>Một phiên kết nối CSDL ứng dụng (tab "Phiên và khóa").</summary>
/// <param name="Pid">Số tiến trình phục vụ phiên.</param>
/// <param name="UserName">Tài khoản PostgreSQL.</param>
/// <param name="ApplicationName">Tên ứng dụng khai khi kết nối.</param>
/// <param name="ClientAddress">Địa chỉ máy kết nối (trống = qua socket nội bộ).</param>
/// <param name="State">Trạng thái PostgreSQL: active, idle, idle in transaction…</param>
/// <param name="StateLabel">Chữ tiếng Việt của trạng thái.</param>
/// <param name="BackendStart">Lúc mở kết nối (UTC).</param>
/// <param name="TransactionStart">Lúc bắt đầu giao dịch đang mở (UTC).</param>
/// <param name="QueryStart">Lúc bắt đầu câu lệnh đang / vừa chạy (UTC).</param>
/// <param name="RunningSeconds">Số giây câu lệnh đang chạy (đang chạy) hoặc giao dịch đang mở (chờ trong giao dịch).</param>
/// <param name="WaitEventType">Loại chờ (Lock, IO, Client…).</param>
/// <param name="WaitEvent">Chi tiết chờ.</param>
/// <param name="BlockedBy">Các pid đang giữ khóa mà phiên này chờ.</param>
/// <param name="BlockingCount">Số phiên đang chờ khóa của phiên này.</param>
/// <param name="Query">Câu lệnh đang / vừa chạy (cắt 2.000 ký tự; tài khoản không đủ quyền thì PostgreSQL ẩn).</param>
/// <param name="IsSelf">Chính kết nối đang đọc số liệu – không ngắt được.</param>
/// <param name="Light">Đèn: Ok, Warning (chờ khóa, chặn phiên khác, chạy / mở giao dịch quá lâu).</param>
/// <param name="Note">Lý do đèn vàng.</param>
public sealed record DbSessionDto(
    int Pid, string? UserName, string? ApplicationName, string? ClientAddress, string? State, string StateLabel, DateTimeOffset? BackendStart,
    DateTimeOffset? TransactionStart, DateTimeOffset? QueryStart, long? RunningSeconds, string? WaitEventType, string? WaitEvent,
    IReadOnlyList<int> BlockedBy, int BlockingCount, string? Query, bool IsSelf, string Light, string? Note);

/// <summary>Tab "Phiên và khóa" của DBM.</summary>
/// <param name="CheckedAt">Lúc đọc (UTC).</param>
/// <param name="UsesMaintenanceAccount">Đọc bằng tài khoản bảo trì (xem được câu lệnh của mọi tài khoản).</param>
/// <param name="WaitingCount">Số phiên đang chờ khóa.</param>
/// <param name="Sessions">Các phiên, phiên có cảnh báo trước.</param>
public sealed record DbSessionsDto(DateTimeOffset CheckedAt, bool UsesMaintenanceAccount, int WaitingCount, IReadOnlyList<DbSessionDto> Sessions);

/// <summary>Một lần bảo trì (tab "Lịch sử" của DBM).</summary>
/// <param name="Id">Id.</param>
/// <param name="Kind">Vacuum, Reindex, TerminateSession.</param>
/// <param name="KindLabel">Chữ của việc.</param>
/// <param name="Trigger">Manual / Schedule.</param>
/// <param name="Status">Queued, Running, Succeeded, Failed.</param>
/// <param name="StatusLabel">Chữ tình trạng.</param>
/// <param name="Light">Đèn: Ok, Error, Neutral.</param>
/// <param name="Target">Bảng / phiên; null = cả CSDL.</param>
/// <param name="SessionPid">Pid phiên bị ngắt.</param>
/// <param name="Reason">Lý do.</param>
/// <param name="ScheduledDate">Ngày lượt lịch.</param>
/// <param name="RequestedByName">Người bấm.</param>
/// <param name="QueuedAt">Lúc vào hàng chờ (UTC).</param>
/// <param name="StartedAt">Lúc bắt đầu (UTC).</param>
/// <param name="FinishedAt">Lúc kết thúc (UTC).</param>
/// <param name="DurationSeconds">Thời gian chạy (giây).</param>
/// <param name="SizeBeforeBytes">Dung lượng trước.</param>
/// <param name="SizeAfterBytes">Dung lượng sau.</param>
/// <param name="Note">Ghi chú kết quả.</param>
/// <param name="ErrorMessage">Lỗi.</param>
public sealed record MaintenanceRunDto(
    Guid Id, string Kind, string KindLabel, string Trigger, string Status, string StatusLabel, string Light, string? Target, int? SessionPid,
    string? Reason, DateOnly? ScheduledDate, string? RequestedByName, DateTimeOffset QueuedAt, DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt, long? DurationSeconds, long? SizeBeforeBytes, long? SizeAfterBytes, string? Note, string? ErrorMessage);

/// <summary>Kết quả tìm lịch sử bảo trì.</summary>
/// <param name="Items">Các lần bảo trì, mới nhất trước.</param>
/// <param name="Truncated">Còn dòng chưa trả – thu hẹp khoảng ngày.</param>
/// <param name="ScheduleEnabled">Lịch hằng tuần đang bật trên máy này.</param>
/// <param name="ScheduleLabel">Chữ lịch, ví dụ "Chủ nhật 03:00".</param>
/// <param name="NextScheduledAt">Lượt lịch kế tiếp (giờ Việt Nam, không múi giờ); null khi lịch tắt.</param>
public sealed record MaintenanceRunSearchResult(
    IReadOnlyList<MaintenanceRunDto> Items, bool Truncated, bool ScheduleEnabled, string ScheduleLabel, DateTime? NextScheduledAt);

/// <summary>Yêu cầu VACUUM (ANALYZE) / REINDEX.</summary>
/// <param name="Kind">Vacuum hoặc Reindex.</param>
/// <param name="Schema">Schema của bảng; bỏ trống cùng <paramref name="Table"/> = cả CSDL (chỉ VACUUM).</param>
/// <param name="Table">Tên bảng.</param>
public sealed record QueueMaintenanceRequest(MaintenanceKind Kind, string? Schema, string? Table);

/// <summary>Yêu cầu ngắt phiên (Q6: mật khẩu + lý do + gõ "NGẮT PHIÊN").</summary>
/// <param name="Pid">Pid phiên.</param>
/// <param name="Password">Mật khẩu của người đang đăng nhập.</param>
/// <param name="Reason">Lý do (≥ 5 ký tự).</param>
/// <param name="Confirmation">Chữ xác nhận.</param>
public sealed record TerminateSessionRequest(int Pid, string? Password, string? Reason, string? Confirmation);

/// <summary>Chữ hiển thị và đèn của lần bảo trì.</summary>
public static class MaintenanceLabels
{
    /// <summary>Chữ của việc.</summary>
    /// <param name="kind">Việc.</param>
    /// <returns>Ví dụ "VACUUM ANALYZE", "REINDEX", "Ngắt phiên".</returns>
    public static string Kind(MaintenanceKind kind) => kind switch
    {
        MaintenanceKind.Vacuum => "VACUUM ANALYZE",
        MaintenanceKind.Reindex => "REINDEX",
        MaintenanceKind.TerminateSession => "Ngắt phiên",
        _ => kind.ToString(),
    };

    /// <summary>Tình trạng và đèn: Đang chờ / Đang chạy (trung tính) · Lỗi (đỏ) · Xong (xanh).</summary>
    /// <param name="status">Trạng thái.</param>
    /// <returns>(chữ, đèn).</returns>
    public static (string Label, string Light) Status(MaintenanceRunStatus status) => status switch
    {
        MaintenanceRunStatus.Queued => ("Đang chờ", "Neutral"),
        MaintenanceRunStatus.Running => ("Đang chạy", "Neutral"),
        MaintenanceRunStatus.Failed => ("Lỗi", "Error"),
        _ => ("Xong", "Ok"),
    };
}

/// <summary>
/// Bảo trì CSDL (màn hình DBM – lát QT4, design.md mục 21.4): số liệu bảng, phiên và khóa, đặt VACUUM / REINDEX vào hàng chờ, ngắt phiên (Q6).
/// Chỉ ở tổng công ty; endpoint kiểm tra quyền.
/// </summary>
public interface IDbMaintenanceService
{
    /// <summary>Dung lượng, dòng chết, lần dọn cuối của từng bảng trong CSDL ứng dụng.</summary>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Số liệu.</returns>
    Task<DbTableStatsDto> GetTablesAsync(CancellationToken cancellationToken);

    /// <summary>Các phiên kết nối CSDL ứng dụng, phiên chờ khóa / đang chặn.</summary>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Số liệu.</returns>
    Task<DbSessionsDto> GetSessionsAsync(CancellationToken cancellationToken);

    /// <summary>Lịch sử bảo trì theo khoảng ngày vào hàng chờ (giờ Việt Nam) kèm lịch hằng tuần.</summary>
    /// <param name="from">Từ ngày.</param>
    /// <param name="to">Đến ngày.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Các lần, mới nhất trước.</returns>
    Task<MaintenanceRunSearchResult> SearchRunsAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken);

    /// <summary>Một lần bảo trì.</summary>
    /// <param name="id">Id.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Lần bảo trì.</returns>
    /// <exception cref="Proc.Application.Common.NotFoundException">Khi không có.</exception>
    Task<MaintenanceRunDto> GetRunAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Đặt VACUUM (ANALYZE) một bảng / cả CSDL hoặc REINDEX một bảng vào hàng chờ. Bảng phải có trong CSDL (tên lấy từ danh mục của PostgreSQL,
    /// không ghép chữ của client vào lệnh); đã có lần cùng việc, cùng bảng đang chờ / chạy thì báo lỗi.
    /// </summary>
    /// <param name="request">Việc, bảng.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Lần vừa đặt.</returns>
    /// <exception cref="Proc.Domain.Common.BusinessRuleException">Việc không đặt được qua hàng chờ, bảng không có, đang có lần trùng.</exception>
    Task<MaintenanceRunDto> QueueAsync(QueueMaintenanceRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Ngắt một phiên của CSDL ứng dụng ngay (<c>pg_terminate_backend</c>) sau khi xác nhận Q6; ghi lịch sử + nhật ký vận hành (cả khi lỗi).
    /// </summary>
    /// <param name="request">Pid, mật khẩu, lý do, chữ xác nhận.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Lần ngắt phiên (Xong / Lỗi).</returns>
    /// <exception cref="Proc.Domain.Common.BusinessRuleException">Xác nhận sai, thiếu lý do, phiên không còn / không thuộc CSDL ứng dụng / là chính
    /// kết nối của ứng dụng đang đọc.</exception>
    Task<MaintenanceRunDto> TerminateAsync(TerminateSessionRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Bộ chạy hàng chờ bảo trì (VACUUM / REINDEX lần lượt từng lần). Giữ khóa <c>pg_try_advisory_lock</c> riêng nên nhiều tiến trình ứng dụng không
/// chạy chồng nhau.
/// </summary>
public interface IDbMaintenanceExecutor
{
    /// <summary>Chạy hết hàng chờ.</summary>
    /// <param name="cancellationToken">Hủy thao tác (ứng dụng dừng).</param>
    /// <returns>Số lần đã chạy; 0 khi hàng chờ trống hoặc tiến trình khác đang giữ khóa.</returns>
    Task<int> ProcessQueueAsync(CancellationToken cancellationToken);

    /// <summary>Lúc khởi động: ghi lỗi các lần còn "Đang chạy" của tiến trình cũ đã dừng giữa chừng (khi lấy được khóa).</summary>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Số lần đã ghi lỗi.</returns>
    Task<int> FailInterruptedAsync(CancellationToken cancellationToken);
}

/// <summary>Tín hiệu đánh thức bộ chạy bảo trì khi có lần mới vào hàng chờ (singleton).</summary>
public interface IDbMaintenanceQueueSignal : IBackupQueueSignal;

/// <summary>Lịch bảo trì hằng tuần (lát QT4-2): dịch vụ nền gọi mỗi phút.</summary>
public interface IDbMaintenanceScheduler
{
    /// <summary>Đặt VACUUM (ANALYZE) cả CSDL của lượt lịch tuần này khi đã tới giờ và chưa đặt.</summary>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Số lần vừa đặt (0 hoặc 1).</returns>
    Task<int> PlanAsync(CancellationToken cancellationToken);
}
