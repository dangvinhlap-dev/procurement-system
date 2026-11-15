using Proc.Domain.Operations;

namespace Proc.Application.Operations;

/// <summary>Cấu hình trung tâm phát hành – mục <c>Release</c> (lát QT8, design.md mục 21.6).</summary>
public sealed class ReleaseOptions
{
    /// <summary>Tên mục cấu hình.</summary>
    public const string Section = "Release";

    /// <summary>
    /// Thư mục cất gói đã nhận (mỗi bản một thư mục con). Tương đối thì tính từ ContentRoot – chỉ hợp cho máy dev; server đặt ngoài thư mục ứng dụng,
    /// ví dụ <c>D:\ProcurementData\packages</c>.
    /// </summary>
    public string PackageRoot { get; set; } = "data/packages";

    /// <summary>Dung lượng gói tối đa (MB) khi tải lên hoặc lấy từ GitHub.</summary>
    public int MaxPackageSizeMb { get; set; } = 200;

    /// <summary>Kho GitHub chứa các bản phát hành (<c>chủ/tên</c>) – để trống thì không nhận gói từ GitHub Release.</summary>
    public string? GitHubRepository { get; set; }

    /// <summary>Địa chỉ API GitHub (đổi được cho máy chủ GitHub riêng / test).</summary>
    public string GitHubApiBaseUrl { get; set; } = "https://api.github.com";
}

/// <summary>Một dòng danh sách phiếu phát hành.</summary>
/// <param name="Id">Id phiếu.</param>
/// <param name="Version">Phiên bản.</param>
/// <param name="Status">Trạng thái.</param>
/// <param name="StatusLabel">Chữ trạng thái.</param>
/// <param name="Tone">Đèn: success / warning / error / neutral.</param>
/// <param name="Source">Nguồn gói (chữ).</param>
/// <param name="PackageSize">Dung lượng gói (byte).</param>
/// <param name="ReceivedAt">Lúc nhận (UTC).</param>
/// <param name="ReceivedByName">Người nhận.</param>
/// <param name="TestedItems">Số mục đã kiểm thử.</param>
/// <param name="TotalItems">Tổng số mục kiểm thử.</param>
/// <param name="OpenDefects">Lỗi chưa xử lý.</param>
/// <param name="ProdInstalledAt">Lúc cài PROD (UTC).</param>
public sealed record ReleaseListItemDto(
    Guid Id, string Version, ReleaseStatus Status, string StatusLabel, string Tone, string Source, long PackageSize, DateTimeOffset ReceivedAt,
    string? ReceivedByName, int TestedItems, int TotalItems, int OpenDefects, DateTimeOffset? ProdInstalledAt);

/// <summary>Mục kiểm thử trên form phiếu.</summary>
/// <param name="Id">Id.</param>
/// <param name="Seq">Thứ tự.</param>
/// <param name="Section">Nhóm CHANGELOG.</param>
/// <param name="Content">Nội dung.</param>
/// <param name="Result">Kết quả.</param>
/// <param name="TesterName">Người kiểm thử.</param>
/// <param name="TestedAt">Lúc kiểm thử (UTC).</param>
/// <param name="Comment">Ý kiến.</param>
public sealed record ReleaseTestItemDto(
    Guid Id, int Seq, string Section, string Content, ReleaseTestResult Result, string? TesterName, DateTimeOffset? TestedAt, string? Comment);

/// <summary>Lỗi phát hiện trên form phiếu.</summary>
/// <param name="Id">Id.</param>
/// <param name="Title">Tiêu đề.</param>
/// <param name="Description">Mô tả.</param>
/// <param name="Screen">Màn hình.</param>
/// <param name="ReporterName">Người báo.</param>
/// <param name="ReportedAt">Lúc báo (UTC).</param>
/// <param name="Status">Trạng thái.</param>
/// <param name="Resolution">Cách xử lý.</param>
/// <param name="ResolvedByName">Người xử lý.</param>
/// <param name="ResolvedAt">Lúc xử lý (UTC).</param>
public sealed record ReleaseDefectDto(
    Guid Id, string Title, string? Description, string? Screen, string? ReporterName, DateTimeOffset ReportedAt, ReleaseDefectStatus Status,
    string? Resolution, string? ResolvedByName, DateTimeOffset? ResolvedAt);

/// <summary>Một lần cài / quay về.</summary>
/// <param name="Id">Id.</param>
/// <param name="Environment">Test / Prod.</param>
/// <param name="Kind">Cài / quay về.</param>
/// <param name="Status">Trạng thái.</param>
/// <param name="PreviousVersion">Bản trước.</param>
/// <param name="TargetVersion">Bản quay về.</param>
/// <param name="Level">Mức quay về.</param>
/// <param name="InstalledSha256">SHA256 đã cài.</param>
/// <param name="Reason">Lý do.</param>
/// <param name="ByName">Người giao.</param>
/// <param name="StartedAt">Lúc giao (UTC).</param>
/// <param name="FinishedAt">Lúc xong (UTC).</param>
/// <param name="Message">Kết quả.</param>
/// <param name="AgentCommandId">Lệnh bộ thực thi (mở nhật ký ở AGT).</param>
public sealed record ReleaseInstallDto(
    Guid Id, string Environment, ReleaseInstallKind Kind, ReleaseInstallStatus Status, string? PreviousVersion, string? TargetVersion,
    RollbackLevel? Level, string? InstalledSha256, string? Reason, string? ByName, DateTimeOffset StartedAt, DateTimeOffset? FinishedAt,
    string? Message, Guid AgentCommandId);

/// <summary>Phiếu phát hành đầy đủ (form REL).</summary>
/// <param name="Id">Id.</param>
/// <param name="Version">Phiên bản.</param>
/// <param name="Status">Trạng thái.</param>
/// <param name="StatusLabel">Chữ trạng thái.</param>
/// <param name="Tone">Đèn.</param>
/// <param name="Source">Nguồn (chữ).</param>
/// <param name="SourceUrl">Địa chỉ nguồn.</param>
/// <param name="PackageFileName">Tên file gói.</param>
/// <param name="PackageSize">Dung lượng (byte).</param>
/// <param name="Sha256">SHA256.</param>
/// <param name="Changelog">CHANGELOG của bản.</param>
/// <param name="DatabaseChanges">Thay đổi CSDL.</param>
/// <param name="MinRollbackLevel">Mức quay về tối thiểu.</param>
/// <param name="Risks">Rủi ro.</param>
/// <param name="RollbackPlan">Cách quay lại.</param>
/// <param name="Note">Ghi chú.</param>
/// <param name="ReceivedAt">Lúc nhận (UTC).</param>
/// <param name="ReceivedByName">Người nhận.</param>
/// <param name="UatSignedAt">Lúc xác nhận UAT (UTC).</param>
/// <param name="UatSignedByName">Người xác nhận UAT.</param>
/// <param name="UatComment">Ý kiến UAT.</param>
/// <param name="UatWithoutTest">Xác nhận UAT không qua máy TEST.</param>
/// <param name="ProdInstalledAt">Lúc cài PROD (UTC).</param>
/// <param name="EndedAt">Lúc đóng / hủy / quay về (UTC).</param>
/// <param name="EndedByName">Người đóng / hủy / quay về.</param>
/// <param name="EndReason">Lý do hủy / quay về.</param>
/// <param name="IsLocked">Phiếu đã khóa.</param>
/// <param name="OpenIssues">Điều còn vướng trước khi xác nhận UAT.</param>
/// <param name="Items">Mục kiểm thử.</param>
/// <param name="Defects">Lỗi phát hiện.</param>
/// <param name="Installs">Lịch sử cài.</param>
public sealed record ReleaseTicketDto(
    Guid Id, string Version, ReleaseStatus Status, string StatusLabel, string Tone, string Source, string? SourceUrl, string PackageFileName,
    long PackageSize, string Sha256, string? Changelog, string? DatabaseChanges, RollbackLevel MinRollbackLevel, string? Risks, string? RollbackPlan,
    string? Note, DateTimeOffset ReceivedAt, string? ReceivedByName, DateTimeOffset? UatSignedAt, string? UatSignedByName, string? UatComment,
    bool UatWithoutTest, DateTimeOffset? ProdInstalledAt, DateTimeOffset? EndedAt, string? EndedByName, string? EndReason, bool IsLocked,
    IReadOnlyList<string> OpenIssues, IReadOnlyList<ReleaseTestItemDto> Items, IReadOnlyList<ReleaseDefectDto> Defects,
    IReadOnlyList<ReleaseInstallDto> Installs);

/// <summary>Một môi trường trên bảng đầu REL (bộ thực thi đã khai ở AGT).</summary>
/// <param name="Code">Dev / Test / Prod.</param>
/// <param name="Name">Tên.</param>
/// <param name="AgentOnline">Bộ thực thi đang liên lạc.</param>
/// <param name="AppVersion">Phiên bản phần mềm bộ thực thi báo gần nhất.</param>
/// <param name="LastSeenAt">Lần liên lạc gần nhất (UTC).</param>
public sealed record ReleaseEnvironmentDto(string Code, string Name, bool AgentOnline, string? AppVersion, DateTimeOffset? LastSeenAt);

/// <summary>Thông tin đầu màn hình REL.</summary>
/// <param name="ThisEnvironment">Môi trường của máy đang mở.</param>
/// <param name="ThisVersion">Phiên bản đang chạy trên máy này.</param>
/// <param name="CanWrite">Máy này ghi được phiếu (PROD / DEV); TEST chỉ xem.</param>
/// <param name="ReadOnlyReason">Lý do chỉ xem.</param>
/// <param name="SingleEnvironment">Chưa khai môi trường TEST – xác nhận UAT không cần cài TEST.</param>
/// <param name="GitHubEnabled">Đã khai kho GitHub – nhận gói từ GitHub Release được.</param>
/// <param name="MaxPackageSizeMb">Dung lượng gói tối đa.</param>
/// <param name="Environments">Các môi trường có bộ thực thi.</param>
public sealed record ReleaseOverviewDto(
    string ThisEnvironment, string ThisVersion, bool CanWrite, string? ReadOnlyReason, bool SingleEnvironment, bool GitHubEnabled,
    int MaxPackageSizeMb, IReadOnlyList<ReleaseEnvironmentDto> Environments);

/// <summary>Nhận gói từ GitHub Release.</summary>
/// <param name="Version">Phiên bản (x.y.z) – tag <c>v</c>x.y.z.</param>
/// <param name="Note">Ghi chú.</param>
public sealed record ReceiveFromGitHubRequest(string? Version, string? Note);

/// <summary>Sửa rủi ro, cách quay lại, ghi chú (trước khi khóa).</summary>
/// <param name="Risks">Rủi ro.</param>
/// <param name="RollbackPlan">Cách quay lại.</param>
/// <param name="Note">Ghi chú.</param>
public sealed record UpdateReleaseInfoRequest(string? Risks, string? RollbackPlan, string? Note);

/// <summary>Thêm ghi chú (cả khi đã khóa).</summary>
/// <param name="Text">Nội dung.</param>
public sealed record AppendReleaseNoteRequest(string? Text);

/// <summary>Ghi kết quả một mục kiểm thử.</summary>
/// <param name="Result">Kết quả.</param>
/// <param name="Comment">Ý kiến.</param>
public sealed record RecordReleaseTestRequest(ReleaseTestResult Result, string? Comment);

/// <summary>Thêm lỗi phát hiện.</summary>
/// <param name="Title">Tiêu đề.</param>
/// <param name="Description">Mô tả.</param>
/// <param name="Screen">Màn hình.</param>
public sealed record AddReleaseDefectRequest(string? Title, string? Description, string? Screen);

/// <summary>Đổi trạng thái lỗi.</summary>
/// <param name="Status">Trạng thái.</param>
/// <param name="Resolution">Cách xử lý.</param>
public sealed record ResolveReleaseDefectRequest(ReleaseDefectStatus Status, string? Resolution);

/// <summary>Xác nhận UAT đạt.</summary>
/// <param name="Comment">Ý kiến (bắt buộc khi còn vướng).</param>
public sealed record SignOffReleaseRequest(string? Comment);

/// <summary>Hủy phiếu.</summary>
/// <param name="Reason">Lý do.</param>
public sealed record CancelReleaseRequest(string? Reason);

/// <summary>Cài lên một môi trường (Q6).</summary>
/// <param name="Environment">Test / Prod.</param>
/// <param name="Reason">Lý do.</param>
/// <param name="Password">Mật khẩu người giao.</param>
/// <param name="Confirmation">Chữ xác nhận "CÀI &lt;phiên bản&gt;".</param>
public sealed record DeployReleaseRequest(string? Environment, string? Reason, string? Password, string? Confirmation);

/// <summary>Quay về bản cũ trên PROD (Q6).</summary>
/// <param name="Level">Mức.</param>
/// <param name="Reason">Lý do.</param>
/// <param name="Password">Mật khẩu.</param>
/// <param name="Confirmation">Chữ xác nhận "QUAY VỀ".</param>
public sealed record RollbackReleaseRequest(RollbackLevel Level, string? Reason, string? Password, string? Confirmation);

/// <summary>Trung tâm phát hành – màn hình REL (lát QT8, design.md mục 21.6). Chỉ tổng công ty; ghi chỉ trên PROD / DEV (TEST chỉ xem – T4).</summary>
public interface IReleaseService
{
    /// <summary>Chữ xác nhận quay về bản cũ (Q6).</summary>
    const string RollbackConfirmation = "QUAY VỀ";

    /// <summary>Chữ xác nhận cài – "CÀI " + phiên bản (gõ đúng bản đang cài, tránh nhầm phiếu).</summary>
    /// <param name="version">Phiên bản.</param>
    /// <returns>Ví dụ "CÀI 0.36.0".</returns>
    static string DeployConfirmation(string version) => $"CÀI {version}";

    /// <summary>Bảng đầu REL: môi trường, phiên bản, máy này ghi được không.</summary>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Thông tin.</returns>
    Task<ReleaseOverviewDto> GetOverviewAsync(CancellationToken cancellationToken);

    /// <summary>Danh sách phiếu, mới nhận trước.</summary>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Danh sách.</returns>
    Task<IReadOnlyList<ReleaseListItemDto>> SearchAsync(CancellationToken cancellationToken);

    /// <summary>Đọc một phiếu.</summary>
    /// <param name="id">Id.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Phiếu.</returns>
    Task<ReleaseTicketDto> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Nhận gói tải lên: kiểm dung lượng, SHA256 (nếu người tải gửi kèm), đọc CHANGELOG / rollback.json, cất gói, tạo phiếu.</summary>
    /// <param name="content">Nội dung file zip.</param>
    /// <param name="fileName">Tên file.</param>
    /// <param name="expectedSha256">SHA256 mong đợi (từ file .sha256) – null thì không so.</param>
    /// <param name="note">Ghi chú.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Phiếu mới.</returns>
    Task<ReleaseTicketDto> ReceiveUploadAsync(Stream content, string? fileName, string? expectedSha256, string? note, CancellationToken cancellationToken);

    /// <summary>Nhận gói từ GitHub Release (tài sản <c>procurement-x.y.z.zip</c> + <c>.sha256</c> của tag <c>vx.y.z</c>).</summary>
    /// <param name="request">Phiên bản, ghi chú.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Phiếu mới.</returns>
    Task<ReleaseTicketDto> ReceiveFromGitHubAsync(ReceiveFromGitHubRequest request, CancellationToken cancellationToken);

    /// <summary>Sửa rủi ro, cách quay lại, ghi chú.</summary>
    /// <param name="id">Phiếu.</param>
    /// <param name="request">Nội dung.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Phiếu.</returns>
    Task<ReleaseTicketDto> UpdateInfoAsync(Guid id, UpdateReleaseInfoRequest request, CancellationToken cancellationToken);

    /// <summary>Thêm ghi chú.</summary>
    /// <param name="id">Phiếu.</param>
    /// <param name="request">Nội dung.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Phiếu.</returns>
    Task<ReleaseTicketDto> AppendNoteAsync(Guid id, AppendReleaseNoteRequest request, CancellationToken cancellationToken);

    /// <summary>Ghi kết quả mục kiểm thử.</summary>
    /// <param name="id">Phiếu.</param>
    /// <param name="itemId">Mục.</param>
    /// <param name="request">Kết quả.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Phiếu.</returns>
    Task<ReleaseTicketDto> RecordTestAsync(Guid id, Guid itemId, RecordReleaseTestRequest request, CancellationToken cancellationToken);

    /// <summary>Thêm lỗi phát hiện.</summary>
    /// <param name="id">Phiếu.</param>
    /// <param name="request">Lỗi.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Phiếu.</returns>
    Task<ReleaseTicketDto> AddDefectAsync(Guid id, AddReleaseDefectRequest request, CancellationToken cancellationToken);

    /// <summary>Đổi trạng thái lỗi.</summary>
    /// <param name="id">Phiếu.</param>
    /// <param name="defectId">Lỗi.</param>
    /// <param name="request">Trạng thái, cách xử lý.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Phiếu.</returns>
    Task<ReleaseTicketDto> ResolveDefectAsync(Guid id, Guid defectId, ResolveReleaseDefectRequest request, CancellationToken cancellationToken);

    /// <summary>Xác nhận UAT đạt (bước 3).</summary>
    /// <param name="id">Phiếu.</param>
    /// <param name="request">Ý kiến.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Phiếu.</returns>
    Task<ReleaseTicketDto> SignOffAsync(Guid id, SignOffReleaseRequest request, CancellationToken cancellationToken);

    /// <summary>Đóng phiếu (bước 5).</summary>
    /// <param name="id">Phiếu.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Phiếu.</returns>
    Task<ReleaseTicketDto> CloseAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Hủy phiếu.</summary>
    /// <param name="id">Phiếu.</param>
    /// <param name="request">Lý do.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Phiếu.</returns>
    Task<ReleaseTicketDto> CancelAsync(Guid id, CancelReleaseRequest request, CancellationToken cancellationToken);

    /// <summary>Cài lên TEST / PROD qua bộ thực thi (bước 2, 4 – Q6).</summary>
    /// <param name="id">Phiếu.</param>
    /// <param name="request">Môi trường, lý do, xác nhận.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Phiếu.</returns>
    Task<ReleaseTicketDto> DeployAsync(Guid id, DeployReleaseRequest request, CancellationToken cancellationToken);

    /// <summary>Quay về bản cũ trên PROD (Q6).</summary>
    /// <param name="id">Phiếu.</param>
    /// <param name="request">Mức, lý do, xác nhận.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Phiếu.</returns>
    Task<ReleaseTicketDto> RollbackAsync(Guid id, RollbackReleaseRequest request, CancellationToken cancellationToken);
}

/// <summary>Một hạng mục kiểm thử nhanh sau khi cài.</summary>
/// <param name="Name">Hạng mục.</param>
/// <param name="Ok">Đạt.</param>
/// <param name="Detail">Chi tiết.</param>
public sealed record SmokeCheckDto(string Name, bool Ok, string? Detail);

/// <summary>Kết quả kiểm thử nhanh (<c>GET /api/ops/v1/agent/smoke</c> – bộ thực thi gọi sau khi cài).</summary>
/// <param name="Version">Phiên bản đang chạy.</param>
/// <param name="Ok">Mọi hạng mục đạt.</param>
/// <param name="Checks">Các hạng mục.</param>
public sealed record SmokeResultDto(string Version, bool Ok, IReadOnlyList<SmokeCheckDto> Checks);

/// <summary>Gói bộ thực thi tải về.</summary>
/// <param name="Path">Đường dẫn file trên máy chủ.</param>
/// <param name="FileName">Tên file.</param>
/// <param name="Sha256">SHA256 mong đợi.</param>
public sealed record ReleasePackageFile(string Path, string FileName, string Sha256);

/// <summary>Phần trung tâm phát hành bộ thực thi gọi tới (khóa bộ thực thi, không JWT).</summary>
public interface IReleaseAgentHooks
{
    /// <summary>Lệnh kết thúc: cập nhật lần cài gắn lệnh (nếu có) và chuyển bước phiếu.</summary>
    /// <param name="commandId">Lệnh.</param>
    /// <param name="installedSha256">SHA256 gói đã cài (bộ thực thi báo).</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Task.</returns>
    Task OnCommandFinishedAsync(Guid commandId, string? installedSha256, CancellationToken cancellationToken);

    /// <summary>Gói của phiếu cho bộ thực thi tải – chỉ khi môi trường đang chạy lệnh cài phiếu đó.</summary>
    /// <param name="environmentId">Môi trường đã xác thực.</param>
    /// <param name="ticketId">Phiếu.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>File gói.</returns>
    /// <exception cref="Common.ForbiddenAccessException">Không có lệnh cài phiếu này đang chạy.</exception>
    Task<ReleasePackageFile> OpenPackageAsync(Guid environmentId, Guid ticketId, CancellationToken cancellationToken);

    /// <summary>Kiểm thử nhanh sau khi cài: truy vấn CSDL, danh sách chính, trung tâm phát hành đọc được (design.md 21.8 T2).</summary>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Kết quả.</returns>
    Task<SmokeResultDto> SmokeAsync(CancellationToken cancellationToken);
}

/// <summary>Chữ, đèn của trạng thái phiếu phát hành – dùng chung API danh sách và form.</summary>
public static class ReleaseLabels
{
    /// <summary>Chữ + đèn của trạng thái (màu không là thông tin duy nhất).</summary>
    /// <param name="status">Trạng thái.</param>
    /// <returns>(Chữ, đèn).</returns>
    public static (string Label, string Tone) Status(ReleaseStatus status) => status switch
    {
        ReleaseStatus.Received => ("Đã nhận", "neutral"),
        ReleaseStatus.UatInProgress => ("Đang UAT", "warning"),
        ReleaseStatus.UatPassed => ("UAT đạt", "success"),
        ReleaseStatus.ProdInstalled => ("Đã cài PROD", "success"),
        ReleaseStatus.Closed => ("Đã đóng", "neutral"),
        ReleaseStatus.Cancelled => ("Đã hủy", "neutral"),
        ReleaseStatus.RolledBack => ("Đã quay về", "error"),
        _ => (status.ToString(), "neutral"),
    };

    /// <summary>Chữ nguồn gói.</summary>
    /// <param name="source">Nguồn.</param>
    /// <returns>Chữ.</returns>
    public static string Source(ReleaseSource source) => source == ReleaseSource.GitHubRelease ? "GitHub Release" : "Tải lên";
}
