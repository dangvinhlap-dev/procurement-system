using Proc.Domain.Operations;

namespace Proc.Application.Operations;

/// <summary>Cấu hình bộ thực thi – mục <c>Agent</c> trong appsettings (lát QT5, ke-hoach-qt.md QT5).</summary>
public sealed class AgentOptions
{
    /// <summary>Tên mục cấu hình.</summary>
    public const string Section = "Agent";

    /// <summary>
    /// Lệnh đang chạy quá số giờ này mà bộ thực thi không báo kết quả thì ghi Lỗi (bộ thực thi chết giữa chừng) – để môi trường nhận lệnh mới.
    /// Mặc định 6 giờ (khôi phục CSDL lớn có thể lâu).
    /// </summary>
    public int RunningTimeoutHours { get; set; } = 6;

    /// <summary>Bộ thực thi liên lạc trong số phút này thì coi là "Đang liên lạc" (tác vụ chạy mỗi phút). Mặc định 3.</summary>
    public int OnlineMinutes { get; set; } = 3;
}

/// <summary>Chữ xác nhận việc nguy hiểm của bộ thực thi (Q6).</summary>
public static class AgentConfirmations
{
    /// <summary>Tạo khóa bộ thực thi mới (khóa cho phép ra lệnh quyền cao trên máy chủ; khóa cũ hết hiệu lực).</summary>
    public const string CreateKey = "TẠO KHÓA";
}

/// <summary>Tên header bộ thực thi gửi khóa.</summary>
public static class AgentHeaders
{
    /// <summary>Header mang khóa bộ thực thi (không phải JWT – design.md ke-hoach-qt QT5).</summary>
    public const string Key = "X-Ops-Agent-Key";
}

/// <summary>Một môi trường có bộ thực thi (tab "Môi trường" màn hình AGT).</summary>
/// <param name="Id">Id.</param>
/// <param name="Code">Dev / Test / Prod.</param>
/// <param name="Name">Tên hiển thị.</param>
/// <param name="Address">Địa chỉ web.</param>
/// <param name="IsActive">Đang dùng.</param>
/// <param name="IsCurrent">Là môi trường của chính máy chủ đang trả lời (App:Environment).</param>
/// <param name="HasKey">Đã tạo khóa.</param>
/// <param name="KeyHint">4 ký tự cuối của khóa.</param>
/// <param name="KeyCreatedAt">Lúc tạo khóa (UTC).</param>
/// <param name="KeyCreatedByName">Người tạo khóa.</param>
/// <param name="LastSeenAt">Lần liên lạc cuối (UTC).</param>
/// <param name="AgentVersion">Phiên bản bộ thực thi.</param>
/// <param name="AppVersion">Phiên bản phần mềm trên máy đó.</param>
/// <param name="HostName">Tên máy.</param>
/// <param name="Light">Đèn: Ok (đang liên lạc), Warning (mất liên lạc), Error (ngừng / chưa có khóa), Neutral (chưa liên lạc).</param>
/// <param name="StatusLabel">Chữ tình trạng.</param>
/// <param name="Note">Lý do đèn, cách xử lý.</param>
/// <param name="QueuedCount">Số lệnh đang chờ.</param>
/// <param name="RunningCommandId">Lệnh đang chạy (nếu có).</param>
public sealed record AgentEnvironmentDto(
    Guid Id, string Code, string Name, string? Address, bool IsActive, bool IsCurrent, bool HasKey, string? KeyHint, DateTimeOffset? KeyCreatedAt,
    string? KeyCreatedByName, DateTimeOffset? LastSeenAt, string? AgentVersion, string? AppVersion, string? HostName, string Light,
    string StatusLabel, string? Note, int QueuedCount, Guid? RunningCommandId);

/// <summary>Khai / sửa môi trường.</summary>
/// <param name="Code">Dev / Test / Prod (sửa thì không đổi được).</param>
/// <param name="Name">Tên hiển thị.</param>
/// <param name="Address">Địa chỉ web (không bắt buộc).</param>
/// <param name="IsActive">Đang dùng.</param>
public sealed record SaveAgentEnvironmentRequest(string? Code, string? Name, string? Address, bool IsActive = true);

/// <summary>Tạo khóa bộ thực thi (Q6: mật khẩu + gõ "TẠO KHÓA").</summary>
/// <param name="Password">Mật khẩu của người đang đăng nhập.</param>
/// <param name="Confirmation">Chữ xác nhận.</param>
public sealed record CreateAgentKeyRequest(string? Password, string? Confirmation);

/// <summary>Khóa vừa tạo – <b>chỉ trả một lần</b>, CSDL chỉ giữ băm.</summary>
/// <param name="EnvironmentId">Môi trường.</param>
/// <param name="Key">Khóa (dán vào file cấu hình bộ thực thi trên máy đích).</param>
/// <param name="Hint">4 ký tự cuối.</param>
public sealed record AgentKeyCreatedDto(Guid EnvironmentId, string Key, string Hint);

/// <summary>Một lệnh bộ thực thi (tab "Lệnh" màn hình AGT).</summary>
/// <param name="Id">Id.</param>
/// <param name="EnvironmentId">Môi trường.</param>
/// <param name="EnvironmentCode">Mã môi trường.</param>
/// <param name="EnvironmentName">Tên môi trường.</param>
/// <param name="Kind">Loại lệnh (mã).</param>
/// <param name="KindLabel">Chữ loại lệnh.</param>
/// <param name="Status">Queued, Running, Succeeded, Failed, Expired, Cancelled.</param>
/// <param name="StatusLabel">Chữ tình trạng.</param>
/// <param name="Light">Đèn: Neutral (chờ / chạy), Ok, Error, Warning (hết hạn, đã hủy).</param>
/// <param name="Reason">Lý do.</param>
/// <param name="CreatedByName">Người ra lệnh.</param>
/// <param name="CreatedAt">Lúc ra lệnh (UTC).</param>
/// <param name="ExpiresAt">Hạn lấy lệnh (UTC).</param>
/// <param name="StartedAt">Lúc bộ thực thi lấy (UTC).</param>
/// <param name="FinishedAt">Lúc kết thúc (UTC).</param>
/// <param name="DurationSeconds">Thời gian chạy (giây).</param>
/// <param name="ResultMessage">Kết quả tóm tắt.</param>
/// <param name="CancelledByName">Người hủy.</param>
/// <param name="Journal">Nhật ký chi tiết – chỉ có khi xem một lệnh.</param>
public sealed record AgentCommandDto(
    Guid Id, Guid EnvironmentId, string EnvironmentCode, string EnvironmentName, string Kind, string KindLabel, string Status, string StatusLabel,
    string Light, string Reason, string? CreatedByName, DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt, DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt, long? DurationSeconds, string? ResultMessage, string? CancelledByName, string? Journal);

/// <summary>Kết quả tìm lệnh.</summary>
/// <param name="Items">Các lệnh, mới nhất trước.</param>
/// <param name="Truncated">Còn dòng chưa trả – thu hẹp khoảng ngày.</param>
public sealed record AgentCommandSearchResult(IReadOnlyList<AgentCommandDto> Items, bool Truncated);

/// <summary>Ra lệnh cho bộ thực thi.</summary>
/// <param name="EnvironmentId">Môi trường.</param>
/// <param name="Kind">Loại lệnh (QT5 chỉ có Ping – "Lệnh thử").</param>
/// <param name="Reason">Lý do (≥ 5 ký tự).</param>
public sealed record QueueAgentCommandRequest(Guid EnvironmentId, AgentCommandKind Kind, string? Reason);

/// <summary>Bộ thực thi giới thiệu mình mỗi lần hỏi lệnh (query string).</summary>
/// <param name="AgentVersion">Phiên bản script bộ thực thi.</param>
/// <param name="AppVersion">Phiên bản phần mềm cài trên máy.</param>
/// <param name="Host">Tên máy.</param>
/// <param name="Peek">Chế độ chạy thử <c>-DryRun</c>: chỉ xem lệnh kế tiếp, không nhận lệnh.</param>
public sealed record AgentPollInfo(string? AgentVersion, string? AppVersion, string? Host, bool Peek);

/// <summary>Lệnh giao bộ thực thi.</summary>
/// <param name="Id">Id lệnh (gửi lại khi báo kết quả).</param>
/// <param name="Kind">Loại lệnh.</param>
/// <param name="Parameters">Tham số JSON.</param>
/// <param name="Reason">Lý do (ghi nhật ký trên máy).</param>
/// <param name="CreatedByName">Người ra lệnh.</param>
/// <param name="ExpiresAt">Hạn lấy (UTC).</param>
/// <param name="Claimed">false khi chỉ xem (<see cref="AgentPollInfo.Peek"/>) – lệnh vẫn Chờ.</param>
public sealed record AgentWorkDto(
    Guid Id, string Kind, string? Parameters, string Reason, string? CreatedByName, DateTimeOffset ExpiresAt, bool Claimed);

/// <summary>Bộ thực thi báo kết quả.</summary>
/// <param name="Succeeded">Chạy được.</param>
/// <param name="Message">Kết quả / lỗi tóm tắt.</param>
/// <param name="Journal">Nhật ký chi tiết.</param>
/// <param name="InstalledSha256">Lệnh cài bản phát hành (lát QT8): SHA256 gói đã cài – máy chủ so với phiếu. Trường thêm sau (API v1 chỉ thêm).</param>
public sealed record AgentResultRequest(bool Succeeded, string? Message, string? Journal, string? InstalledSha256 = null);

/// <summary>Chữ hiển thị và đèn của lệnh bộ thực thi.</summary>
public static class AgentLabels
{
    /// <summary>Chữ loại lệnh.</summary>
    /// <param name="kind">Loại.</param>
    /// <returns>Ví dụ "Lệnh thử (Ping)".</returns>
    public static string Kind(AgentCommandKind kind) => kind switch
    {
        AgentCommandKind.Ping => "Lệnh thử (Ping)",
        AgentCommandKind.RestorePointInTime => "Khôi phục về mốc thời gian",
        AgentCommandKind.DiscardPointInTime => "Bỏ bản khôi phục theo mốc",
        AgentCommandKind.SwapDatabase => "Hoán đổi CSDL",
        AgentCommandKind.SwapCluster => "Hoán đổi máy chủ CSDL",
        AgentCommandKind.DeployRelease => "Cài bản phát hành",
        AgentCommandKind.RollbackRelease => "Quay về bản cũ",
        _ => kind.ToString(),
    };

    /// <summary>Tình trạng và đèn.</summary>
    /// <param name="status">Trạng thái.</param>
    /// <returns>(chữ, đèn).</returns>
    public static (string Label, string Light) Status(AgentCommandStatus status) => status switch
    {
        AgentCommandStatus.Queued => ("Đang chờ", "Neutral"),
        AgentCommandStatus.Running => ("Đang chạy", "Neutral"),
        AgentCommandStatus.Succeeded => ("Xong", "Ok"),
        AgentCommandStatus.Failed => ("Lỗi", "Error"),
        AgentCommandStatus.Expired => ("Hết hạn", "Warning"),
        _ => ("Đã hủy", "Warning"),
    };
}

/// <summary>
/// Quản trị bộ thực thi (màn hình AGT – lát QT5): khai môi trường, tạo khóa, ra lệnh, hủy lệnh chờ, xem lịch sử. Chỉ ở tổng công ty; endpoint
/// kiểm tra quyền <c>System.OpsAgent.View</c> / <c>.Manage</c>.
/// </summary>
public interface IAgentAdminService
{
    /// <summary>Các môi trường kèm tình trạng liên lạc.</summary>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Theo mã Dev, Test, Prod.</returns>
    Task<IReadOnlyList<AgentEnvironmentDto>> GetEnvironmentsAsync(CancellationToken cancellationToken);

    /// <summary>Khai môi trường mới (mỗi mã một dòng).</summary>
    /// <param name="request">Mã, tên, địa chỉ.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Môi trường.</returns>
    /// <exception cref="Proc.Domain.Common.BusinessRuleException">Mã sai / đã khai, tên trống, địa chỉ sai.</exception>
    Task<AgentEnvironmentDto> CreateEnvironmentAsync(SaveAgentEnvironmentRequest request, CancellationToken cancellationToken);

    /// <summary>Sửa tên, địa chỉ, ngừng / dùng lại (không đổi mã).</summary>
    /// <param name="id">Môi trường.</param>
    /// <param name="request">Tên, địa chỉ, đang dùng.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Môi trường.</returns>
    /// <exception cref="Proc.Application.Common.NotFoundException">Không có.</exception>
    Task<AgentEnvironmentDto> UpdateEnvironmentAsync(Guid id, SaveAgentEnvironmentRequest request, CancellationToken cancellationToken);

    /// <summary>Tạo khóa mới sau xác nhận Q6 (khóa cũ hết hiệu lực ngay); ghi nhật ký vận hành.</summary>
    /// <param name="id">Môi trường.</param>
    /// <param name="request">Mật khẩu, chữ xác nhận.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Khóa – chỉ trả một lần.</returns>
    Task<AgentKeyCreatedDto> CreateKeyAsync(Guid id, CreateAgentKeyRequest request, CancellationToken cancellationToken);

    /// <summary>Lịch sử lệnh theo khoảng ngày ra lệnh (giờ Việt Nam).</summary>
    /// <param name="from">Từ ngày.</param>
    /// <param name="to">Đến ngày.</param>
    /// <param name="environmentId">Lọc một môi trường.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Lệnh, mới nhất trước (không kèm nhật ký chi tiết).</returns>
    Task<AgentCommandSearchResult> SearchCommandsAsync(DateOnly from, DateOnly to, Guid? environmentId, CancellationToken cancellationToken);

    /// <summary>Một lệnh kèm nhật ký chi tiết.</summary>
    /// <param name="id">Id.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Lệnh.</returns>
    Task<AgentCommandDto> GetCommandAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Ra lệnh (môi trường phải đang dùng và có khóa); ghi nhật ký vận hành.</summary>
    /// <param name="request">Môi trường, loại, lý do.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Lệnh chờ.</returns>
    Task<AgentCommandDto> QueueAsync(QueueAgentCommandRequest request, CancellationToken cancellationToken);

    /// <summary>Hủy lệnh còn chờ; ghi nhật ký vận hành.</summary>
    /// <param name="id">Lệnh.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Lệnh đã hủy.</returns>
    Task<AgentCommandDto> CancelAsync(Guid id, CancellationToken cancellationToken);
}

/// <summary>
/// Cổng cho bộ thực thi (lát QT5): xác thực khóa <c>X-Ops-Agent-Key</c>, giao lệnh, nhận kết quả. Không qua phiên đăng nhập người dùng nên không
/// có công ty / quyền – chỉ khóa. Khóa sai / môi trường ngừng dùng thì từ chối (401).
/// </summary>
public interface IAgentGateway
{
    /// <summary>Tìm môi trường theo khóa (so băm SHA256 thời gian cố định).</summary>
    /// <param name="key">Khóa trong header.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Id môi trường, hoặc null khi khóa sai / môi trường ngừng dùng.</returns>
    Task<Guid?> AuthenticateAsync(string? key, CancellationToken cancellationToken);

    /// <summary>
    /// Ghi lần liên lạc rồi giao lệnh kế tiếp: lệnh chờ cũ nhất còn hạn (lệnh quá hạn đánh dấu Hết hạn; lệnh chạy quá giờ ghi Lỗi). Đang có lệnh chạy
    /// thì không giao lệnh mới (mỗi môi trường một lệnh chạy). Chế độ xem (<see cref="AgentPollInfo.Peek"/>) không nhận lệnh.
    /// </summary>
    /// <param name="environmentId">Môi trường đã xác thực.</param>
    /// <param name="info">Phiên bản, tên máy.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Lệnh, hoặc null khi không có việc.</returns>
    Task<AgentWorkDto?> PollAsync(Guid environmentId, AgentPollInfo info, CancellationToken cancellationToken);

    /// <summary>Nhận kết quả lệnh đang chạy của đúng môi trường; ghi nhật ký vận hành.</summary>
    /// <param name="environmentId">Môi trường đã xác thực.</param>
    /// <param name="commandId">Lệnh.</param>
    /// <param name="result">Kết quả.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Task.</returns>
    /// <exception cref="Proc.Application.Common.NotFoundException">Lệnh không có / thuộc môi trường khác.</exception>
    /// <exception cref="Proc.Domain.Common.BusinessRuleException">Lệnh không đang chạy (đã báo, hết hạn, đã hủy).</exception>
    Task ReportAsync(Guid environmentId, Guid commandId, AgentResultRequest result, CancellationToken cancellationToken);

    /// <summary>
    /// Bộ thực thi xin chuẩn bị hoán đổi (lát QT6, trước khi dừng site): máy chủ bật chế độ bảo trì và chép bảng vận hành sang bản khôi phục.
    /// </summary>
    /// <param name="environmentId">Môi trường (từ khóa).</param>
    /// <param name="commandId">Lệnh hoán đổi đang chạy.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Task.</returns>
    /// <exception cref="Proc.Application.Common.NotFoundException">Lệnh không có / thuộc môi trường khác.</exception>
    /// <exception cref="Proc.Domain.Common.BusinessRuleException">Không phải lệnh hoán đổi đang chạy; lần phục hồi không ở trạng thái hoán đổi.</exception>
    Task PrepareSwapAsync(Guid environmentId, Guid commandId, CancellationToken cancellationToken);
}

/// <summary>Chế độ bảo trì hiện trên dải đỏ và câu báo 503.</summary>
/// <param name="IsOn">Đang bật.</param>
/// <param name="Message">Câu thông báo.</param>
/// <param name="Reason">Lý do lần bật / tắt gần nhất.</param>
/// <param name="StartedAt">Lúc bật (UTC).</param>
/// <param name="ChangedByName">Người bật / tắt gần nhất.</param>
/// <param name="ChangedAt">Lúc bật / tắt gần nhất (UTC).</param>
public sealed record MaintenanceModeDto(
    bool IsOn, string? Message, string? Reason, DateTimeOffset? StartedAt, string? ChangedByName, DateTimeOffset? ChangedAt);

/// <summary>Phần chế độ bảo trì gửi kèm API môi trường ẩn danh (chỉ câu thông báo, giờ bật – không lý do, không người bật).</summary>
/// <param name="Message">Câu thông báo.</param>
/// <param name="StartedAt">Lúc bật (UTC).</param>
public sealed record MaintenanceNoticeDto(string Message, DateTimeOffset? StartedAt);

/// <summary>Bật / tắt chế độ bảo trì.</summary>
/// <param name="IsOn">Bật (true) / tắt (false).</param>
/// <param name="Message">Câu thông báo (bật).</param>
/// <param name="Reason">Lý do (≥ 5 ký tự).</param>
public sealed record SetMaintenanceModeRequest(bool IsOn, string? Message, string? Reason);

/// <summary>
/// Chế độ bảo trì (lát QT5): đọc có đệm ngắn (mỗi request đều hỏi), bật / tắt ghi CSDL + nhật ký vận hành. Nhiều tiến trình (IIS chồng lúc cài)
/// cùng đọc CSDL nên bật ở một tiến trình thì tiến trình khác thấy sau tối đa <see cref="CacheSeconds"/> giây.
/// </summary>
public interface IMaintenanceMode
{
    /// <summary>Thời gian đệm trạng thái (giây).</summary>
    const int CacheSeconds = 5;

    /// <summary>Trạng thái hiện tại (đệm).</summary>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Trạng thái.</returns>
    Task<MaintenanceModeDto> GetAsync(CancellationToken cancellationToken);

    /// <summary>Bật / tắt; ghi nhật ký vận hành.</summary>
    /// <param name="request">Bật / tắt, câu thông báo, lý do.</param>
    /// <param name="changedByName">Người thao tác (tên đăng nhập).</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Trạng thái mới.</returns>
    /// <exception cref="Proc.Domain.Common.BusinessRuleException">Thiếu lý do, tắt khi đang tắt.</exception>
    Task<MaintenanceModeDto> SetAsync(SetMaintenanceModeRequest request, string? changedByName, CancellationToken cancellationToken);
}
