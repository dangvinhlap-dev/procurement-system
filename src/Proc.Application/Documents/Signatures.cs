namespace Proc.Application.Documents;

/// <summary>Một phiên bản chữ ký (lát IN1 – design.md mục 20.4).</summary>
/// <param name="VersionNo">Số phiên bản.</param>
/// <param name="ValidFrom">Hiệu lực từ (UTC).</param>
/// <param name="ValidTo">Hiệu lực đến (UTC); null = đang dùng.</param>
/// <param name="UploadedByName">Họ tên người tải.</param>
/// <param name="ByAdmin">Người quản trị tải thay.</param>
/// <param name="Reason">Lý do tải (bắt buộc khi tải thay).</param>
/// <param name="RetiredByName">Họ tên người kết thúc hiệu lực.</param>
/// <param name="RetireReason">Lý do ngừng dùng.</param>
/// <param name="Width">Rộng ảnh (điểm ảnh).</param>
/// <param name="Height">Cao ảnh (điểm ảnh).</param>
public sealed record SignatureVersionDto(
    int VersionNo, DateTimeOffset ValidFrom, DateTimeOffset? ValidTo, string? UploadedByName, bool ByAdmin, string? Reason, string? RetiredByName,
    string? RetireReason, int Width, int Height);

/// <summary>Chữ ký của một người dùng: thông tin người, các phiên bản (mới nhất trước).</summary>
/// <param name="UserId">Id người dùng.</param>
/// <param name="UserName">Tên đăng nhập.</param>
/// <param name="FullName">Họ tên.</param>
/// <param name="Honorific">Danh xưng in trước họ tên (ví dụ "AHLĐ.").</param>
/// <param name="IsSelf">Là chính người đang xem.</param>
/// <param name="CurrentVersionNo">Phiên bản đang dùng; null = chưa có chữ ký đang dùng.</param>
/// <param name="Versions">Các phiên bản, mới nhất trước.</param>
public sealed record UserSignatureDto(
    Guid UserId, string UserName, string FullName, string? Honorific, bool IsSelf, int? CurrentVersionNo, IReadOnlyList<SignatureVersionDto> Versions);

/// <summary>Tải chữ ký mới / ngừng dùng chữ ký: lý do bắt buộc khi làm thay người khác.</summary>
/// <param name="Reason">Lý do.</param>
public sealed record SignatureReasonRequest(string? Reason);

/// <summary>
/// Chữ ký người dùng để in vào ô ký (lát IN1 – design.md mục 20.4). <paramref>userId</paramref> null = chính người đang đăng nhập (ai cũng tự làm
/// được); người khác cần quyền <c>System.User.Signature</c> (người dùng chốt 18/11/2026: admin tải thay) – tải thay / ngừng dùng thay thì người
/// được làm thay nhận thông báo. Ảnh chỉ xem được bởi chính chủ và người có quyền đó; bản in lấy ảnh trực tiếp ở máy chủ.
/// </summary>
public interface IUserSignatureService
{
    /// <summary>Thông tin chữ ký và lịch sử phiên bản.</summary>
    /// <param name="userId">Người dùng; null = chính mình.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Chữ ký.</returns>
    /// <exception cref="Common.ForbiddenAccessException">Xem chữ ký người khác mà không có quyền.</exception>
    /// <exception cref="Common.NotFoundException">Không có người dùng.</exception>
    Task<UserSignatureDto> GetAsync(Guid? userId, CancellationToken cancellationToken);

    /// <summary>Ảnh PNG của một phiên bản (mặc định phiên bản đang dùng).</summary>
    /// <param name="userId">Người dùng; null = chính mình.</param>
    /// <param name="versionNo">Phiên bản; null = đang dùng.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Nội dung PNG.</returns>
    /// <exception cref="Common.NotFoundException">Không có phiên bản.</exception>
    Task<byte[]> GetImageAsync(Guid? userId, int? versionNo, CancellationToken cancellationToken);

    /// <summary>Tải chữ ký mới: phiên bản đang dùng (nếu có) kết thúc hiệu lực, phiên bản mới có hiệu lực từ bây giờ.</summary>
    /// <param name="userId">Người dùng; null = chính mình.</param>
    /// <param name="image">Ảnh PNG đã xử lý.</param>
    /// <param name="reason">Lý do – bắt buộc khi tải thay.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Chữ ký sau khi tải.</returns>
    /// <exception cref="Domain.Common.BusinessRuleException">Ảnh không hợp lệ, thiếu lý do.</exception>
    Task<UserSignatureDto> UploadAsync(Guid? userId, byte[] image, string? reason, CancellationToken cancellationToken);

    /// <summary>Ngừng dùng chữ ký đang dùng (phiếu duyệt sau đó in "Đã duyệt điện tử" thay ảnh).</summary>
    /// <param name="userId">Người dùng; null = chính mình.</param>
    /// <param name="reason">Lý do – bắt buộc khi làm thay.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Chữ ký sau khi ngừng dùng.</returns>
    /// <exception cref="Domain.Common.BusinessRuleException">Chưa có chữ ký đang dùng; thiếu lý do.</exception>
    Task<UserSignatureDto> RetireAsync(Guid? userId, string? reason, CancellationToken cancellationToken);
}

/// <summary>Thông tin logo công ty.</summary>
/// <param name="CompanyId">Công ty.</param>
/// <param name="HasLogo">Đã có logo.</param>
/// <param name="ContentType">Kiểu ảnh.</param>
/// <param name="Width">Rộng.</param>
/// <param name="Height">Cao.</param>
/// <param name="UpdatedAt">Lần thay gần nhất (UTC).</param>
public sealed record CompanyLogoDto(Guid CompanyId, bool HasLogo, string? ContentType, int Width, int Height, DateTimeOffset? UpdatedAt);

/// <summary>
/// Logo công ty in đầu phiếu (lát IN1 – design.md mục 20.4): làm chứng từ ở công ty nào in logo công ty đó. Xem cần quyền xem danh mục công ty,
/// tải / xóa cần quyền sửa công ty.
/// </summary>
public interface ICompanyLogoService
{
    /// <summary>Thông tin logo.</summary>
    /// <param name="companyId">Công ty.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Thông tin (HasLogo = false khi chưa có).</returns>
    Task<CompanyLogoDto> GetAsync(Guid companyId, CancellationToken cancellationToken);

    /// <summary>Ảnh logo.</summary>
    /// <param name="companyId">Công ty.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Nội dung và kiểu ảnh.</returns>
    /// <exception cref="Common.NotFoundException">Chưa có logo.</exception>
    Task<(byte[] Content, string ContentType)> GetImageAsync(Guid companyId, CancellationToken cancellationToken);

    /// <summary>Tải / thay logo.</summary>
    /// <param name="companyId">Công ty.</param>
    /// <param name="image">Ảnh PNG/JPEG.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Thông tin logo mới.</returns>
    Task<CompanyLogoDto> UploadAsync(Guid companyId, byte[] image, CancellationToken cancellationToken);

    /// <summary>Xóa logo (phiếu in sau đó để trống góc logo).</summary>
    /// <param name="companyId">Công ty.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Task.</returns>
    Task DeleteAsync(Guid companyId, CancellationToken cancellationToken);
}

/// <summary>Một lần in chứng từ (lát IN1).</summary>
/// <param name="PrintNo">Lần in thứ.</param>
/// <param name="Kind">"Print" (In) hoặc "Download" (Tải PDF).</param>
/// <param name="PrintedAt">Thời điểm (UTC).</param>
/// <param name="PrintedByName">Họ tên người in.</param>
/// <param name="PrintedByUserName">Tên đăng nhập người in.</param>
/// <param name="ApprovalStatus">Tình trạng phê duyệt in trên bản.</param>
public sealed record DocumentPrintDto(int PrintNo, string Kind, DateTimeOffset PrintedAt, string? PrintedByName, string? PrintedByUserName, string ApprovalStatus);
