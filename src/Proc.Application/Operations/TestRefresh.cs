namespace Proc.Application.Operations;

/// <summary>
/// Máy chủ "phía trên" mà máy này gọi tới (lát QT9 – design.md 21.18): máy TEST chuyển báo lỗi / góp ý của người dùng về trung tâm phát hành trên PROD.
/// Mục <c>Operations:Upstream</c> trong appsettings của máy TEST; PROD, DEV để trống.
/// </summary>
public sealed class UpstreamOptions
{
    /// <summary>Tên mục cấu hình.</summary>
    public const string Section = "Operations:Upstream";

    /// <summary>Địa chỉ web của PROD (ví dụ <c>https://procurement.congty.vn</c>); trống = không chuyển báo lỗi.</summary>
    public string? BaseUrl { get; set; }

    /// <summary>Khóa dịch vụ của môi trường này – tạo ở màn hình AGT trên PROD ("Tạo khóa dịch vụ…"), bắt đầu bằng <c>qts_</c>.</summary>
    public string? ServiceKey { get; set; }

    /// <summary>Chờ PROD trả lời tối đa (giây).</summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>Đã khai đủ địa chỉ và khóa.</summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(BaseUrl) && !string.IsNullOrWhiteSpace(ServiceKey);
}

/// <summary>Header khóa dịch vụ (máy chủ gọi máy chủ – lát QT9).</summary>
public static class ServiceHeaders
{
    /// <summary>Tên header.</summary>
    public const string Key = "X-Ops-Service-Key";
}

/// <summary>
/// Bảng của chính TEST giữ lại khi làm mới TEST từ PROD (lát QT9 – tự chọn): bảng vận hành như khi hoán đổi phục hồi (<see cref="RestoreCarryOver"/>) cộng
/// nhật ký lỗi. Lý do: lịch sử sao lưu trỏ file trên đĩa của TEST, khóa / lệnh bộ thực thi, chế độ bảo trì, nhật ký vận hành, lỗi là của máy TEST – lấy
/// bản của PROD thì TEST dọn nhầm file sao lưu, mất lịch sử của chính nó. Thứ tự: bảng cha trước bảng con (xóa theo thứ tự ngược).
/// </summary>
public static class TestRefreshCarryOver
{
    /// <summary>Các bảng (schema public).</summary>
    public static readonly IReadOnlyList<string> Tables = [.. RestoreCarryOver.Tables, "ErrorGroup", "ErrorOccurrence"];
}

/// <summary>Môi trường TEST nhìn từ máy chạy màn hình TST.</summary>
/// <param name="Code">Mã (Test).</param>
/// <param name="Name">Tên.</param>
/// <param name="ContactLight">Đèn liên lạc bộ thực thi.</param>
/// <param name="ContactLabel">Chữ liên lạc.</param>
/// <param name="AppVersion">Phiên bản phần mềm trên TEST (bộ thực thi báo).</param>
/// <param name="AgentVersion">Phiên bản bộ thực thi.</param>
/// <param name="LastSeenAt">Lần liên lạc cuối (UTC).</param>
/// <param name="HasServiceKey">Đã tạo khóa dịch vụ (TEST chuyển báo lỗi về được).</param>
public sealed record TestRefreshTargetDto(
    string Code, string Name, string ContactLight, string ContactLabel, string? AppVersion, string? AgentVersion, DateTimeOffset? LastSeenAt,
    bool HasServiceKey);

/// <summary>Bản pg_dump của máy này sẽ dùng để làm mới.</summary>
/// <param name="RunId">Lần sao lưu.</param>
/// <param name="FinishedAt">Lúc xong (UTC).</param>
/// <param name="RelativePath">Đường dẫn trong thư mục sao lưu.</param>
/// <param name="SizeBytes">Dung lượng.</param>
/// <param name="Sha256">SHA256 của file.</param>
/// <param name="AgeHours">Tuổi (giờ).</param>
/// <param name="Stale">Cũ hơn <see cref="ITestRefreshService.StaleHours"/> giờ.</param>
public sealed record TestRefreshDumpDto(
    Guid RunId, DateTimeOffset? FinishedAt, string? RelativePath, long? SizeBytes, string? Sha256, double AgeHours, bool Stale);

/// <summary>Tổng quan màn hình TST.</summary>
/// <param name="MachineEnvironment">Môi trường máy đang mở.</param>
/// <param name="Version">Phiên bản máy đang mở (nguồn dữ liệu).</param>
/// <param name="ReadOnlyReason">Lý do chỉ xem (máy TEST); null = làm mới được nếu không vướng.</param>
/// <param name="Test">Môi trường TEST đã khai ở AGT; null = chưa khai.</param>
/// <param name="Dump">Bản pg_dump mới nhất dùng được; null = chưa có.</param>
/// <param name="Blockers">Điều chặn làm mới (rỗng = làm được).</param>
/// <param name="Warnings">Điều cần lưu ý (vẫn làm được).</param>
/// <param name="KeepTables">Bảng giữ theo TEST.</param>
/// <param name="OpenCommandId">Lệnh làm mới đang chờ / đang chạy.</param>
public sealed record TestRefreshOverviewDto(
    string MachineEnvironment, string Version, string? ReadOnlyReason, TestRefreshTargetDto? Test, TestRefreshDumpDto? Dump,
    IReadOnlyList<string> Blockers, IReadOnlyList<string> Warnings, IReadOnlyList<string> KeepTables, Guid? OpenCommandId);

/// <summary>Làm mới TEST (Q6: lý do + mật khẩu + gõ "LÀM MỚI TEST").</summary>
/// <param name="Reason">Lý do (≥ 5 ký tự).</param>
/// <param name="Password">Mật khẩu người thao tác.</param>
/// <param name="Confirmation">Chữ xác nhận.</param>
public sealed record RunTestRefreshRequest(string? Reason, string? Password, string? Confirmation);

/// <summary>Màn hình TST – làm mới TEST từ PROD (lát QT9, design.md 21.5, 21.18).</summary>
public interface ITestRefreshService
{
    /// <summary>Chữ xác nhận Q6.</summary>
    const string Confirmation = "LÀM MỚI TEST";

    /// <summary>Bản pg_dump cũ hơn số giờ này thì cảnh báo vàng (lịch pg_dump mỗi đêm – mục 21.5).</summary>
    const int StaleHours = 24;

    /// <summary>Tổng quan: TEST, bản dump, điều chặn / lưu ý.</summary>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Tổng quan.</returns>
    Task<TestRefreshOverviewDto> GetOverviewAsync(CancellationToken cancellationToken);

    /// <summary>Lịch sử làm mới (lệnh <c>RefreshTest</c>) trong khoảng ngày (giờ Việt Nam).</summary>
    /// <param name="from">Từ ngày.</param>
    /// <param name="to">Đến ngày.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Các lệnh, mới trước.</returns>
    Task<IReadOnlyList<AgentCommandDto>> SearchAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken);

    /// <summary>Giao bộ thực thi TEST làm mới.</summary>
    /// <param name="request">Lý do + xác nhận Q6.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Lệnh vừa giao.</returns>
    /// <exception cref="Proc.Domain.Common.BusinessRuleException">Máy TEST; còn điều chặn; sai xác nhận.</exception>
    Task<AgentCommandDto> QueueAsync(RunTestRefreshRequest request, CancellationToken cancellationToken);
}

/// <summary>Một file đính kèm trong danh sách gửi bộ thực thi TEST.</summary>
/// <param name="Path">Đường dẫn tương đối trong thư mục đính kèm (dấu <c>/</c>).</param>
/// <param name="Size">Dung lượng (byte).</param>
public sealed record AttachmentManifestFile(string Path, long Size);

/// <summary>Phần máy chủ cho bộ thực thi TEST (lát QT9) – chỉ khi chính môi trường đó đang chạy lệnh làm mới.</summary>
public interface ITestRefreshAgentHooks
{
    /// <summary>File pg_dump của lệnh làm mới đang chạy.</summary>
    /// <param name="environmentId">Môi trường của khóa bộ thực thi.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Đường dẫn, tên file, SHA256.</returns>
    /// <exception cref="Common.ForbiddenAccessException">Môi trường không có lệnh làm mới đang chạy.</exception>
    Task<ReleasePackageFile> OpenDumpAsync(Guid environmentId, CancellationToken cancellationToken);

    /// <summary>Danh sách file trong thư mục đính kèm của máy này.</summary>
    /// <param name="environmentId">Môi trường của khóa bộ thực thi.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Các file.</returns>
    Task<IReadOnlyList<AttachmentManifestFile>> GetAttachmentManifestAsync(Guid environmentId, CancellationToken cancellationToken);

    /// <summary>Đường dẫn tuyệt đối một file đính kèm (chặn thoát khỏi thư mục đính kèm).</summary>
    /// <param name="environmentId">Môi trường của khóa bộ thực thi.</param>
    /// <param name="path">Đường dẫn tương đối như trong danh sách.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Đường dẫn tuyệt đối.</returns>
    Task<string> OpenAttachmentAsync(Guid environmentId, string? path, CancellationToken cancellationToken);
}

/// <summary>Báo lỗi / góp ý máy TEST chuyển về PROD (khóa dịch vụ).</summary>
/// <param name="Version">Phiên bản đang chạy trên TEST.</param>
/// <param name="Title">Tiêu đề.</param>
/// <param name="Description">Mô tả.</param>
/// <param name="Screen">Màn hình đang mở (đường dẫn trang).</param>
/// <param name="ReporterName">Người báo (tên đăng nhập – họ tên).</param>
/// <param name="ImageFileName">Tên file ảnh (png / jpg); null = không có ảnh.</param>
/// <param name="ImageBase64">Nội dung ảnh base64.</param>
public sealed record ForwardDefectRequest(
    string? Version, string? Title, string? Description, string? Screen, string? ReporterName, string? ImageFileName, string? ImageBase64);

/// <summary>Kết quả ghi báo lỗi vào phiếu phát hành.</summary>
/// <param name="Version">Phiên bản của phiếu.</param>
/// <param name="TicketStatus">Trạng thái phiếu (chữ).</param>
/// <param name="OpenDefects">Số lỗi chưa xử lý của phiếu sau khi ghi.</param>
/// <param name="Message">Câu báo cho người dùng.</param>
public sealed record ForwardDefectResult(string Version, string TicketStatus, int OpenDefects, string Message);

/// <summary>Báo lỗi người dùng gửi trên máy TEST (nút "Báo lỗi / góp ý").</summary>
/// <param name="Title">Tiêu đề.</param>
/// <param name="Description">Mô tả.</param>
/// <param name="Screen">Màn hình đang mở.</param>
/// <param name="ImageFileName">Tên file ảnh.</param>
/// <param name="ImageBase64">Ảnh base64.</param>
public sealed record SendFeedbackRequest(string? Title, string? Description, string? Screen, string? ImageFileName, string? ImageBase64);

/// <summary>Hộp nhận báo lỗi trên PROD (lát QT9): xác thực khóa dịch vụ, ghi lỗi phát hiện vào phiếu phát hành cùng phiên bản.</summary>
public interface IReleaseDefectInbox
{
    /// <summary>Xác thực khóa dịch vụ.</summary>
    /// <param name="key">Khóa từ header <see cref="ServiceHeaders.Key"/>.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Mã môi trường của khóa; null = sai / thiếu khóa, môi trường ngừng dùng.</returns>
    Task<string?> AuthenticateAsync(string? key, CancellationToken cancellationToken);

    /// <summary>Ghi báo lỗi vào phiếu phát hành của phiên bản đó.</summary>
    /// <param name="environmentCode">Môi trường gửi (chỉ Test).</param>
    /// <param name="request">Báo lỗi.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Kết quả.</returns>
    /// <exception cref="Proc.Domain.Common.BusinessRuleException">Không có phiếu nhận lỗi; thiếu tiêu đề; ảnh sai.</exception>
    Task<ForwardDefectResult> ReceiveAsync(string environmentCode, ForwardDefectRequest request, CancellationToken cancellationToken);
}

/// <summary>Nút "Báo lỗi / góp ý" trên máy TEST (lát QT9): chuyển về PROD qua <see cref="UpstreamOptions"/>.</summary>
public interface ITestFeedbackService
{
    /// <summary>Máy này là TEST và đã khai <see cref="UpstreamOptions"/>.</summary>
    bool Enabled { get; }

    /// <summary>Gửi báo lỗi về PROD.</summary>
    /// <param name="request">Báo lỗi.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Kết quả PROD trả về.</returns>
    /// <exception cref="Proc.Domain.Common.BusinessRuleException">Không phải TEST / chưa khai; PROD từ chối hoặc không liên lạc được.</exception>
    Task<ForwardDefectResult> SendAsync(SendFeedbackRequest request, CancellationToken cancellationToken);
}
