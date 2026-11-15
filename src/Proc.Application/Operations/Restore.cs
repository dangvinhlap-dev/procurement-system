using Proc.Domain.Operations;

namespace Proc.Application.Operations;

/// <summary>Cấu hình phục hồi – mục <c>Restore</c> trong appsettings (lát QT6, design.md mục 21.4 RST).</summary>
public sealed class RestoreOptions
{
    /// <summary>Tên mục cấu hình.</summary>
    public const string Section = "Restore";

    /// <summary>
    /// Bật bộ chạy nền khôi phục pg_dump sang CSDL mới. Môi trường Testing tắt để test tự gọi <see cref="IRestoreExecutor.ProcessQueueAsync"/>.
    /// </summary>
    public bool WorkerEnabled { get; set; } = true;

    /// <summary>
    /// Cổng máy chủ CSDL tạm khi khôi phục về mốc thời gian (bộ thực thi chạy PostgreSQL thứ hai trên cổng này để kiểm tra trước khi hoán đổi).
    /// Không trùng cổng PostgreSQL chính (5432) và cổng thử nghiệm khác trên máy.
    /// </summary>
    public int TempPort { get; set; } = 5434;
}

/// <summary>
/// Bảng vận hành <b>giữ theo CSDL đang chạy</b> khi hoán đổi (lát QT6): trước khi đổi tên, các bảng này được chép từ CSDL đang chạy sang bản khôi
/// phục (thay hẳn nội dung cũ trong bản khôi phục). Lý do: phục hồi là đưa <b>dữ liệu nghiệp vụ</b> về mốc cũ; còn lịch sử sao lưu (file sao lưu
/// vẫn nằm trên đĩa), khóa bộ thực thi, lệnh đang chạy, nhật ký vận hành, chính lần phục hồi này phải giữ như hiện tại – không thì bộ thực thi
/// không xác thực được (khóa tạo sau mốc), dọn sao lưu không biết bản mới, lệnh hoán đổi không báo được kết quả. Thứ tự: bảng cha trước (xóa
/// theo thứ tự ngược).
/// </summary>
public static class RestoreCarryOver
{
    /// <summary>Tên bảng (đúng tên trong CSDL).</summary>
    public static readonly IReadOnlyList<string> Tables =
    [
        "AgentEnvironment", "AgentCommand", "BackupRun", "MaintenanceRun", "MaintenanceModeState", "OperationLog", "RestoreRun",
    ];

    /// <summary>Bảng vận hành (không so sánh như dữ liệu nghiệp vụ).</summary>
    /// <param name="table">Tên bảng.</param>
    /// <returns>true khi thuộc danh sách.</returns>
    public static bool Contains(string table) => Tables.Contains(table, StringComparer.Ordinal);
}

/// <summary>Một dòng so sánh số dòng một bảng giữa CSDL đang chạy và bản khôi phục.</summary>
/// <param name="Schema">Schema.</param>
/// <param name="Table">Tên bảng.</param>
/// <param name="LiveRows">Số dòng CSDL đang chạy (null = bảng không có).</param>
/// <param name="RestoreRows">Số dòng bản khôi phục (null = bảng không có).</param>
/// <param name="Operational">Bảng vận hành – khi hoán đổi giữ theo CSDL đang chạy (<see cref="RestoreCarryOver"/>).</param>
/// <param name="Light">Đèn: Ok (bằng nhau), Warning (khác – dữ liệu nghiệp vụ sẽ về như bản khôi phục), Neutral (bảng vận hành).</param>
public sealed record RestoreCompareRowDto(string Schema, string Table, long? LiveRows, long? RestoreRows, bool Operational, string Light);

/// <summary>Bảng so sánh (lưu JSON trên lần phục hồi).</summary>
/// <param name="ComparedAt">Lúc so sánh (UTC).</param>
/// <param name="LiveMigration">Migration cuối của CSDL đang chạy.</param>
/// <param name="RestoreMigration">Migration cuối của bản khôi phục.</param>
/// <param name="Rows">Các bảng: khác nhau trước, rồi theo tên.</param>
public sealed record RestoreComparisonDto(
    DateTimeOffset ComparedAt, string? LiveMigration, string? RestoreMigration, IReadOnlyList<RestoreCompareRowDto> Rows);

/// <summary>Một lần phục hồi (màn hình RST).</summary>
/// <param name="Id">Id.</param>
/// <param name="Kind">Dump / PointInTime.</param>
/// <param name="KindLabel">Chữ cách phục hồi.</param>
/// <param name="Status">Trạng thái (mã).</param>
/// <param name="StatusLabel">Chữ trạng thái.</param>
/// <param name="Light">Đèn: Neutral (chờ / đang chạy), Warning (chờ kiểm tra), Ok (đã hoán đổi), Error (lỗi), Neutral (đã bỏ).</param>
/// <param name="SourceLabel">Nguồn: bản dump / chuỗi Full + Diff.</param>
/// <param name="DumpRunId">Bản dump nguồn.</param>
/// <param name="FullRunId">Bản Full.</param>
/// <param name="DiffRunId">Bản Diff.</param>
/// <param name="TargetTime">Mốc thời gian (UTC).</param>
/// <param name="LiveDatabase">CSDL đang chạy lúc đặt.</param>
/// <param name="RestoreDatabase">CSDL khôi phục.</param>
/// <param name="OldDatabase">CSDL / thư mục cũ sau hoán đổi.</param>
/// <param name="TempPort">Cổng máy chủ tạm.</param>
/// <param name="Reason">Lý do.</param>
/// <param name="RequestedByName">Người đặt.</param>
/// <param name="QueuedAt">Lúc đặt (UTC).</param>
/// <param name="StartedAt">Lúc bắt đầu (UTC).</param>
/// <param name="ReadyAt">Lúc sẵn sàng kiểm tra (UTC).</param>
/// <param name="SwapReason">Lý do hoán đổi.</param>
/// <param name="SwapRequestedByName">Người hoán đổi.</param>
/// <param name="SwapRequestedAt">Lúc bấm hoán đổi (UTC).</param>
/// <param name="FinishedAt">Lúc kết thúc (UTC).</param>
/// <param name="DiscardedByName">Người bỏ.</param>
/// <param name="AgentCommandId">Lệnh bộ thực thi gần nhất.</param>
/// <param name="CanSwap">Hoán đổi được (cùng phiên bản CSDL).</param>
/// <param name="Note">Ghi chú.</param>
/// <param name="ErrorMessage">Lỗi.</param>
/// <param name="Comparison">Bảng so sánh (chỉ khi xem một lần).</param>
/// <param name="Plan">Kế hoạch chuỗi khôi phục (mốc thời gian, chỉ khi xem một lần).</param>
public sealed record RestoreRunDto(
    Guid Id, string Kind, string KindLabel, string Status, string StatusLabel, string Light, string SourceLabel, Guid? DumpRunId, Guid? FullRunId,
    Guid? DiffRunId, DateTimeOffset? TargetTime, string LiveDatabase, string RestoreDatabase, string? OldDatabase, int? TempPort, string Reason,
    string? RequestedByName, DateTimeOffset QueuedAt, DateTimeOffset? StartedAt, DateTimeOffset? ReadyAt, string? SwapReason,
    string? SwapRequestedByName, DateTimeOffset? SwapRequestedAt, DateTimeOffset? FinishedAt, string? DiscardedByName, Guid? AgentCommandId,
    bool? CanSwap, string? Note, string? ErrorMessage, RestoreComparisonDto? Comparison, RestorePlan? Plan);

/// <summary>Kết quả tìm lịch sử phục hồi.</summary>
/// <param name="Items">Các lần, mới nhất trước.</param>
/// <param name="Truncated">Còn dòng chưa trả – thu hẹp khoảng ngày.</param>
public sealed record RestoreRunSearchResult(IReadOnlyList<RestoreRunDto> Items, bool Truncated);

/// <summary>Bản pg_dump chọn được để khôi phục.</summary>
/// <param name="Id">Id lần sao lưu.</param>
/// <param name="FinishedAt">Lúc xong (UTC).</param>
/// <param name="RelativePath">File (tương đối so với thư mục sao lưu).</param>
/// <param name="SizeBytes">Dung lượng.</param>
/// <param name="FileExists">File còn trên đĩa.</param>
public sealed record RestoreDumpSourceDto(Guid Id, DateTimeOffset? FinishedAt, string? RelativePath, long? SizeBytes, bool FileExists);

/// <summary>Thông tin chung màn hình RST.</summary>
/// <param name="LiveDatabase">CSDL đang chạy.</param>
/// <param name="Dumps">Bản pg_dump dùng được, mới nhất trước.</param>
/// <param name="UsesMaintenanceAccount">Có tài khoản bảo trì (<c>ConnectionStrings:Maintenance</c>) – khôi phục bằng tài khoản đó; không có thì
/// bằng tài khoản ứng dụng (cần quyền CREATEDB).</param>
/// <param name="WalArchiveConfigured">Đã khai thư mục WAL (cần cho khôi phục về mốc thời gian).</param>
/// <param name="TempPort">Cổng máy chủ CSDL tạm.</param>
/// <param name="AgentEnvironment">Mã môi trường bộ thực thi của máy này (Dev / Test / Prod).</param>
/// <param name="AgentReady">Môi trường bộ thực thi của máy này đang dùng và đã có khóa (hoán đổi, khôi phục theo mốc cần bộ thực thi).</param>
/// <param name="AgentNote">Lý do bộ thực thi chưa sẵn sàng.</param>
/// <param name="OtherDatabases">
/// CSDL khác trên cùng máy chủ PostgreSQL (không kể <c>postgres</c>, template, bản khôi phục / bản cũ của phần mềm này): khôi phục <b>về mốc thời
/// gian</b> thay cả thư mục dữ liệu nên các CSDL này cũng quay về mốc đó (mất dữ liệu nhập sau mốc của các phần mềm khác) – màn hình cảnh báo.
/// </param>
public sealed record RestoreOverviewDto(
    string LiveDatabase, IReadOnlyList<RestoreDumpSourceDto> Dumps, bool UsesMaintenanceAccount, bool WalArchiveConfigured, int TempPort,
    string AgentEnvironment, bool AgentReady, string? AgentNote, IReadOnlyList<string> OtherDatabases);

/// <summary>Yêu cầu phục hồi.</summary>
/// <param name="Kind">Dump hoặc PointInTime.</param>
/// <param name="DumpRunId">Bản pg_dump (Dump).</param>
/// <param name="TargetTime">Mốc thời gian (PointInTime).</param>
/// <param name="Reason">Lý do (≥ 5 ký tự).</param>
public sealed record QueueRestoreRequest(RestoreKind Kind, Guid? DumpRunId, DateTimeOffset? TargetTime, string? Reason);

/// <summary>Yêu cầu hoán đổi (Q6: mật khẩu + lý do + gõ "KHÔI PHỤC").</summary>
/// <param name="Password">Mật khẩu của người đang đăng nhập.</param>
/// <param name="Reason">Lý do (≥ 5 ký tự).</param>
/// <param name="Confirmation">Chữ xác nhận.</param>
public sealed record SwapRestoreRequest(string? Password, string? Reason, string? Confirmation);

/// <summary>Chữ hiển thị và đèn của lần phục hồi.</summary>
public static class RestoreLabels
{
    /// <summary>Chữ cách phục hồi.</summary>
    /// <param name="kind">Cách.</param>
    /// <returns>"Từ pg_dump" / "Về mốc thời gian".</returns>
    public static string Kind(RestoreKind kind) => kind switch
    {
        RestoreKind.Dump => "Từ pg_dump",
        RestoreKind.PointInTime => "Về mốc thời gian",
        _ => kind.ToString(),
    };

    /// <summary>Chữ trạng thái và đèn.</summary>
    /// <param name="status">Trạng thái.</param>
    /// <returns>(chữ, đèn).</returns>
    public static (string Label, string Light) Status(RestoreRunStatus status) => status switch
    {
        RestoreRunStatus.Queued => ("Đang chờ", "Neutral"),
        RestoreRunStatus.Restoring => ("Đang khôi phục", "Neutral"),
        RestoreRunStatus.Ready => ("Chờ kiểm tra", "Warning"),
        RestoreRunStatus.Swapping => ("Đang hoán đổi", "Neutral"),
        RestoreRunStatus.Swapped => ("Đã hoán đổi", "Ok"),
        RestoreRunStatus.Failed => ("Lỗi", "Error"),
        _ => ("Đã bỏ", "Neutral"),
    };
}

/// <summary>
/// Phục hồi (màn hình RST – lát QT6, design.md mục 21.4, N2): khôi phục pg_dump sang CSDL mới hoặc về mốc thời gian ra máy chủ CSDL tạm, so sánh
/// số dòng, hoán đổi (bộ thực thi), bỏ bản khôi phục. Chỉ ở tổng công ty; endpoint kiểm tra quyền <c>System.OpsRestore.View</c> / <c>.Run</c>.
/// </summary>
public interface IRestoreService
{
    /// <summary>Thông tin chung: CSDL đang chạy, bản dump chọn được, tình trạng bộ thực thi.</summary>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Thông tin.</returns>
    Task<RestoreOverviewDto> GetOverviewAsync(CancellationToken cancellationToken);

    /// <summary>Lịch sử phục hồi theo ngày đặt (giờ Việt Nam).</summary>
    /// <param name="from">Từ ngày.</param>
    /// <param name="to">Đến ngày.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Các lần, mới nhất trước.</returns>
    Task<RestoreRunSearchResult> SearchAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken);

    /// <summary>Một lần phục hồi kèm bảng so sánh, kế hoạch.</summary>
    /// <param name="id">Id.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Lần phục hồi.</returns>
    /// <exception cref="Proc.Application.Common.NotFoundException">Khi không có.</exception>
    Task<RestoreRunDto> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Lập kế hoạch khôi phục về mốc thời gian (chỉ xem, không đặt gì).</summary>
    /// <param name="targetTime">Mốc (UTC).</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Kế hoạch: chuỗi Full → Diff → WAL hoặc lý do không khôi phục được.</returns>
    Task<RestorePlan> PlanAsync(DateTimeOffset targetTime, CancellationToken cancellationToken);

    /// <summary>
    /// Đặt lần phục hồi: pg_dump vào hàng chờ của bộ chạy nền; mốc thời gian giao bộ thực thi (lát QT6-2). Mỗi lúc chỉ một lần phục hồi chưa kết
    /// thúc (chờ / khôi phục / chờ kiểm tra / hoán đổi) – bỏ hoặc hoán đổi lần trước rồi mới đặt lần mới.
    /// </summary>
    /// <param name="request">Cách, nguồn, lý do.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Lần vừa đặt.</returns>
    /// <exception cref="Proc.Domain.Common.BusinessRuleException">Nguồn không dùng được, thiếu lý do, đang có lần chưa kết thúc.</exception>
    Task<RestoreRunDto> QueueAsync(QueueRestoreRequest request, CancellationToken cancellationToken);

    /// <summary>So sánh lại số dòng bản khôi phục với CSDL đang chạy (đang chờ kiểm tra).</summary>
    /// <param name="id">Id.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Lần phục hồi kèm bảng so sánh mới.</returns>
    Task<RestoreRunDto> CompareAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Bỏ bản khôi phục: xóa CSDL khôi phục (pg_dump – ứng dụng tự xóa); mốc thời gian giao bộ thực thi dừng máy chủ tạm, xóa thư mục.
    /// </summary>
    /// <param name="id">Id.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Lần phục hồi Đã bỏ.</returns>
    Task<RestoreRunDto> DiscardAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Hoán đổi (Q6 "KHÔI PHỤC"): bản khôi phục thành CSDL đang chạy – giao bộ thực thi của máy này (lát QT6-2).
    /// </summary>
    /// <param name="id">Id.</param>
    /// <param name="request">Mật khẩu, lý do, chữ xác nhận.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Lần phục hồi Đang hoán đổi.</returns>
    Task<RestoreRunDto> SwapAsync(Guid id, SwapRestoreRequest request, CancellationToken cancellationToken);
}

/// <summary>Bộ chạy hàng chờ khôi phục pg_dump (khóa <c>pg_try_advisory_lock</c> riêng – nhiều tiến trình không chạy chồng).</summary>
public interface IRestoreExecutor
{
    /// <summary>Chạy hết hàng chờ.</summary>
    /// <param name="cancellationToken">Hủy thao tác (ứng dụng dừng).</param>
    /// <returns>Số lần đã chạy.</returns>
    Task<int> ProcessQueueAsync(CancellationToken cancellationToken);

    /// <summary>Lúc khởi động: ghi lỗi lần khôi phục pg_dump còn "Đang khôi phục" của tiến trình cũ.</summary>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Số lần đã ghi lỗi.</returns>
    Task<int> FailInterruptedAsync(CancellationToken cancellationToken);
}

/// <summary>Tín hiệu đánh thức bộ chạy khôi phục khi có lần mới (singleton).</summary>
public interface IRestoreQueueSignal : IBackupQueueSignal;

/// <summary>
/// Nối lệnh bộ thực thi với lần phục hồi (lát QT6) – <see cref="IAgentGateway"/> gọi khi bộ thực thi xin chuẩn bị hoán đổi và khi báo kết quả.
/// </summary>
public interface IRestoreAgentHooks
{
    /// <summary>
    /// Chuẩn bị hoán đổi: bật chế độ bảo trì, chép bảng vận hành (<see cref="RestoreCarryOver"/>) từ CSDL đang chạy sang bản khôi phục.
    /// </summary>
    /// <param name="commandId">Lệnh hoán đổi đang chạy.</param>
    /// <param name="environmentCode">Mã môi trường (ghi người bật bảo trì "agent:&lt;mã&gt;").</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Task.</returns>
    Task PrepareSwapAsync(Guid commandId, string environmentCode, CancellationToken cancellationToken);

    /// <summary>Lệnh kết thúc: cập nhật lần phục hồi gắn lệnh (nếu có).</summary>
    /// <param name="commandId">Lệnh.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Task.</returns>
    Task OnCommandFinishedAsync(Guid commandId, CancellationToken cancellationToken);
}
