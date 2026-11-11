namespace Proc.Application.Documents;

/// <summary>Một file đính kèm trong danh sách (design.md mục 18.4).</summary>
/// <param name="Id">Khóa.</param>
/// <param name="FileName">Tên file gốc.</param>
/// <param name="ContentType">Loại nội dung.</param>
/// <param name="SizeBytes">Dung lượng (byte).</param>
/// <param name="Note">Ghi chú.</param>
/// <param name="UploadedAt">Thời điểm tải lên (UTC).</param>
/// <param name="UploadedByName">Họ tên người tải lên.</param>
/// <param name="ApprovalStepLevel">Cấp của bước duyệt mà file thuộc về (người duyệt đính kèm khi duyệt / từ chối – design.md mục 20); null nếu
/// không thuộc bước nào.</param>
/// <param name="AddedAfterApproval">File thêm khi phiếu đã duyệt xong ("Bổ sung sau duyệt").</param>
/// <param name="CanDelete">Người dùng hiện tại xóa được file (design.md mục 20, D1: chỉ người thêm, phiếu còn nháp chưa có bước duyệt xong).</param>
/// <param name="LockReason">Lý do không xóa được (hiện chú thích); null nếu xóa được.</param>
public sealed record AttachmentDto(
    Guid Id, string FileName, string ContentType, long SizeBytes, string? Note, DateTimeOffset UploadedAt, string? UploadedByName,
    int? ApprovalStepLevel = null, bool AddedAfterApproval = false, bool CanDelete = true, string? LockReason = null);

/// <summary>Giới hạn tải lên theo cấu hình máy chủ (design.md mục 18.4).</summary>
/// <param name="MaxFileSizeMb">Dung lượng tối đa một file (MB), mặc định 2.</param>
public sealed record AttachmentLimitsDto(int MaxFileSizeMb);

/// <summary>Nội dung file để tải xuống.</summary>
/// <param name="FileName">Tên file gốc.</param>
/// <param name="ContentType">Loại nội dung (suy từ đuôi).</param>
/// <param name="Content">Luồng đọc file; nơi gọi chịu trách nhiệm đóng.</param>
public sealed record AttachmentContent(string FileName, string ContentType, Stream Content);

/// <summary>
/// Đính kèm file dùng chung cho NCC, bảng giá, RFQ, PR, PO, phiếu kho (design.md mục 18.4). Quyền đi theo đối tượng chủ: xem/tải xuống
/// cần quyền xem chủ, tải lên/xóa cần quyền sửa chủ; chủ đọc qua global query filter nên ngoài phạm vi công ty/kho coi như không có.
/// </summary>
public interface IAttachmentService
{
    /// <summary>File đính kèm (chưa xóa) của một đối tượng, cũ trước mới sau.</summary>
    /// <param name="ownerType">Loại chủ – xem <c>AttachmentOwnerTypes</c>.</param>
    /// <param name="ownerId">Id chủ.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Danh sách.</returns>
    /// <exception cref="Common.ForbiddenAccessException">Không có quyền xem chủ.</exception>
    /// <exception cref="Common.NotFoundException">Chủ không tồn tại hoặc ngoài phạm vi.</exception>
    Task<IReadOnlyList<AttachmentDto>> ListAsync(string ownerType, Guid ownerId, CancellationToken cancellationToken);

    /// <summary>Tải lên một file: ghi đĩa rồi lưu CSDL; lưu CSDL lỗi thì xóa file vừa ghi.</summary>
    /// <param name="ownerType">Loại chủ.</param>
    /// <param name="ownerId">Id chủ.</param>
    /// <param name="fileName">Tên file gốc.</param>
    /// <param name="sizeBytes">Dung lượng client báo (kiểm tra sớm; dung lượng thật đo khi ghi).</param>
    /// <param name="content">Nội dung.</param>
    /// <param name="note">Ghi chú.</param>
    /// <param name="forApprovalStep">File của người duyệt cho bước mình vừa duyệt / từ chối (design.md mục 20): gắn vào bước duyệt gần nhất
    /// người dùng đã xử lý trên phiếu (trong 30 phút); chỉ cần quyền xem phiếu.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>File vừa lưu.</returns>
    /// <exception cref="Domain.Common.BusinessRuleException">Đuôi không được nhận, rỗng, quá dung lượng tối đa (cấu hình <c>Attachments:MaxFileSizeMb</c>, mặc định 2 MB).</exception>
    /// <exception cref="Common.ForbiddenAccessException">Không có quyền sửa chủ.</exception>
    /// <exception cref="Common.NotFoundException">Chủ không tồn tại hoặc ngoài phạm vi.</exception>
    Task<AttachmentDto> UploadAsync(
        string ownerType, Guid ownerId, string fileName, long sizeBytes, Stream content, string? note, CancellationToken cancellationToken,
        bool forApprovalStep = false);

    /// <summary>Mở file để tải xuống.</summary>
    /// <param name="id">File đính kèm.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Tên, loại nội dung, luồng đọc.</returns>
    /// <exception cref="Common.ForbiddenAccessException">Không có quyền xem chủ.</exception>
    /// <exception cref="Common.NotFoundException">File đã xóa, chủ ngoài phạm vi hoặc file mất trên đĩa.</exception>
    Task<AttachmentContent> OpenAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Xóa mềm một file (giữ file trên đĩa, ghi nhật ký).</summary>
    /// <param name="id">File đính kèm.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <exception cref="Common.ForbiddenAccessException">Không có quyền sửa chủ.</exception>
    /// <exception cref="Common.NotFoundException">File đã xóa hoặc chủ ngoài phạm vi.</exception>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}
