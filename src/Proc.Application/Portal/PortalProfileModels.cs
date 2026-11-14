using Proc.Application.Documents;

namespace Proc.Application.Portal;

/// <summary>Người liên hệ NCC (hiện tại hoặc đề nghị) – lát P2a.</summary>
/// <param name="Id">Id dòng đã có; null = người mới.</param>
/// <param name="Name">Họ tên.</param>
/// <param name="Position">Chức vụ.</param>
/// <param name="Phone">Điện thoại.</param>
/// <param name="Email">Email.</param>
/// <param name="IsPrimary">Người liên hệ chính.</param>
public sealed record SupplierProfileContactDto(Guid? Id, string Name, string? Position, string? Phone, string? Email, bool IsPrimary);

/// <summary>Tài khoản ngân hàng NCC (hiện tại hoặc đề nghị) – lát P2a.</summary>
/// <param name="Id">Id dòng đã có; null = tài khoản mới.</param>
/// <param name="BankName">Ngân hàng.</param>
/// <param name="Branch">Chi nhánh.</param>
/// <param name="AccountNumber">Số tài khoản.</param>
/// <param name="AccountName">Chủ tài khoản.</param>
/// <param name="IsDefault">Tài khoản mặc định.</param>
public sealed record SupplierProfileBankDto(Guid? Id, string BankName, string? Branch, string AccountNumber, string? AccountName, bool IsDefault);

/// <summary>Một trường thông tin chung NCC đề nghị đổi.</summary>
/// <param name="Field">Tên trường (Name, Address…).</param>
/// <param name="Label">Nhãn tiếng Việt.</param>
/// <param name="Old">Giá trị lúc NCC gửi.</param>
/// <param name="New">Giá trị đề nghị.</param>
/// <param name="Current">Giá trị hiện tại trên NCC (khác <paramref name="Old"/> khi Phòng CUVT đã sửa trong lúc chờ).</param>
public sealed record SupplierProfileFieldDto(string Field, string Label, string? Old, string? New, string? Current);

/// <summary>
/// Đề nghị cập nhật hồ sơ NCC (lát P2a, design.md 19.5 P2-1…P2-6) – dùng cho cả màn hình nội bộ và Portal (Portal không hiện tên người
/// xử lý nội bộ).
/// </summary>
/// <param name="Id">Id.</param>
/// <param name="No">Số đề nghị (NCC0012-HS01).</param>
/// <param name="SupplierId">NCC.</param>
/// <param name="SupplierCode">Mã NCC.</param>
/// <param name="SupplierName">Tên NCC hiện tại.</param>
/// <param name="Status">Submitted / Accepted / Rejected / Withdrawn.</param>
/// <param name="SubmittedAt">Thời điểm gửi (UTC).</param>
/// <param name="SubmittedByEmail">Email người gửi.</param>
/// <param name="SubmittedByName">Họ tên người gửi trên Portal.</param>
/// <param name="Note">Ghi chú của NCC.</param>
/// <param name="Summary">Tóm tắt phần đề nghị đổi, ví dụ "Địa chỉ, Website; người liên hệ; tài khoản ngân hàng".</param>
/// <param name="Fields">Thông tin chung đề nghị đổi.</param>
/// <param name="ProposedContacts">Người liên hệ đề nghị; null = không đổi.</param>
/// <param name="CurrentContacts">Người liên hệ hiện tại.</param>
/// <param name="ProposedBankAccounts">Tài khoản đề nghị; null = không đổi.</param>
/// <param name="CurrentBankAccounts">Tài khoản hiện tại.</param>
/// <param name="ReviewedByName">Người xử lý (chỉ màn hình nội bộ).</param>
/// <param name="ClosedAt">Thời điểm xử lý / rút (UTC).</param>
/// <param name="ReviewComment">Ý kiến, lý do từ chối hoặc lý do rút.</param>
/// <param name="BankChangeRequestNo">Số yêu cầu duyệt NCC (đổi tài khoản) lập khi chấp nhận.</param>
/// <param name="BankChangeRequestId">Id yêu cầu duyệt NCC đó (chỉ màn hình nội bộ).</param>
/// <param name="CanProcess">Người xem chấp nhận / từ chối được (nội bộ).</param>
/// <param name="CanWithdraw">Người xem rút được (quản trị NCC trên Portal).</param>
public sealed record SupplierProfileRequestDto(
    Guid Id, string No, Guid SupplierId, string SupplierCode, string SupplierName, string Status, DateTimeOffset SubmittedAt, string SubmittedByEmail,
    string? SubmittedByName, string? Note, string Summary, IReadOnlyList<SupplierProfileFieldDto> Fields,
    IReadOnlyList<SupplierProfileContactDto>? ProposedContacts, IReadOnlyList<SupplierProfileContactDto> CurrentContacts,
    IReadOnlyList<SupplierProfileBankDto>? ProposedBankAccounts, IReadOnlyList<SupplierProfileBankDto> CurrentBankAccounts,
    string? ReviewedByName, DateTimeOffset? ClosedAt, string? ReviewComment, string? BankChangeRequestNo, Guid? BankChangeRequestId,
    bool CanProcess, bool CanWithdraw);

/// <summary>Hồ sơ NCC hiện tại trên Portal (trang "Hồ sơ công ty").</summary>
/// <param name="Code">Mã NCC.</param>
/// <param name="Name">Tên.</param>
/// <param name="ShortName">Tên viết tắt.</param>
/// <param name="TaxCode">Mã số thuế (chỉ xem – Phòng CUVT sửa trong phần mềm).</param>
/// <param name="IsForeign">NCC nước ngoài (chỉ xem).</param>
/// <param name="Address">Địa chỉ.</param>
/// <param name="Phone">Điện thoại.</param>
/// <param name="Email">Email.</param>
/// <param name="Website">Website.</param>
/// <param name="SupplyScope">Phạm vi cung cấp.</param>
/// <param name="PaymentTerms">Điều khoản thanh toán (chỉ xem – Phòng CUVT quản lý).</param>
/// <param name="Status">Trạng thái NCC.</param>
/// <param name="Contacts">Người liên hệ.</param>
/// <param name="BankAccounts">Tài khoản ngân hàng.</param>
/// <param name="CanSubmit">Người xem gửi được đề nghị (quản trị NCC, chưa có đề nghị đang chờ).</param>
/// <param name="PendingRequestNo">Số đề nghị đang chờ (nếu có).</param>
/// <param name="Requests">Các đề nghị đã gửi, mới nhất trước.</param>
public sealed record PortalProfileDto(
    string Code, string Name, string? ShortName, string? TaxCode, bool IsForeign, string? Address, string? Phone, string? Email, string? Website,
    string? SupplyScope, string? PaymentTerms, string Status, IReadOnlyList<SupplierProfileContactDto> Contacts,
    IReadOnlyList<SupplierProfileBankDto> BankAccounts, bool CanSubmit, string? PendingRequestNo, IReadOnlyList<SupplierProfileRequestDto> Requests);

/// <summary>
/// NCC gửi đề nghị cập nhật hồ sơ: đủ thông tin chung, đủ danh sách người liên hệ, tài khoản (hệ thống tự tìm phần đổi). Không có mã số thuế –
/// chỉ Phòng CUVT sửa trong phần mềm (người dùng chốt 14/11/2026).
/// </summary>
/// <param name="Name">Tên công ty.</param>
/// <param name="ShortName">Tên viết tắt.</param>
/// <param name="Address">Địa chỉ.</param>
/// <param name="Phone">Điện thoại.</param>
/// <param name="Email">Email.</param>
/// <param name="Website">Website.</param>
/// <param name="SupplyScope">Phạm vi cung cấp.</param>
/// <param name="Contacts">Người liên hệ.</param>
/// <param name="BankAccounts">Tài khoản ngân hàng.</param>
/// <param name="Note">Ghi chú.</param>
public sealed record PortalProfileUpdateRequest(
    string Name, string? ShortName, string? Address, string? Phone, string? Email, string? Website, string? SupplyScope,
    IReadOnlyList<SupplierProfileContactDto>? Contacts, IReadOnlyList<SupplierProfileBankDto>? BankAccounts, string? Note);

/// <summary>Lý do rút / từ chối, ý kiến chấp nhận.</summary>
/// <param name="Comment">Nội dung.</param>
public sealed record SupplierProfileCommentRequest(string? Comment);

/// <summary>Hồ sơ NCC trên Portal: xem, gửi đề nghị cập nhật, rút, file kèm (lát P2a). Mọi hàm lấy NCC từ phiên (P1b-4).</summary>
public interface IPortalProfileService
{
    /// <summary>Hồ sơ hiện tại kèm các đề nghị đã gửi.</summary>
    /// <param name="session">Phiên Portal.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Hồ sơ.</returns>
    Task<PortalProfileDto> GetAsync(PortalSession session, CancellationToken cancellationToken);

    /// <summary>Quản trị NCC gửi đề nghị cập nhật hồ sơ.</summary>
    /// <param name="session">Phiên Portal.</param>
    /// <param name="request">Hồ sơ đề nghị.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Đề nghị vừa gửi.</returns>
    /// <exception cref="Proc.Application.Common.ForbiddenAccessException">Khi không phải quản trị NCC.</exception>
    /// <exception cref="Proc.Domain.Common.BusinessRuleException">Khi đang có đề nghị chờ, dữ liệu sai hoặc không có thay đổi.</exception>
    Task<SupplierProfileRequestDto> SubmitAsync(PortalSession session, PortalProfileUpdateRequest request, CancellationToken cancellationToken);

    /// <summary>Quản trị NCC rút đề nghị còn chờ.</summary>
    /// <param name="session">Phiên Portal.</param>
    /// <param name="id">Đề nghị.</param>
    /// <param name="request">Lý do (không bắt buộc).</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Đề nghị sau khi rút.</returns>
    Task<SupplierProfileRequestDto> WithdrawAsync(
        PortalSession session, Guid id, SupplierProfileCommentRequest request, CancellationToken cancellationToken);

    /// <summary>File kèm đề nghị.</summary>
    /// <param name="session">Phiên Portal.</param>
    /// <param name="id">Đề nghị.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Danh sách file.</returns>
    Task<IReadOnlyList<AttachmentDto>> ListAttachmentsAsync(PortalSession session, Guid id, CancellationToken cancellationToken);

    /// <summary>Quản trị NCC tải file kèm đề nghị còn chờ.</summary>
    /// <param name="session">Phiên Portal.</param>
    /// <param name="id">Đề nghị.</param>
    /// <param name="fileName">Tên file.</param>
    /// <param name="sizeBytes">Dung lượng.</param>
    /// <param name="content">Nội dung.</param>
    /// <param name="note">Ghi chú.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>File vừa tải.</returns>
    Task<AttachmentDto> UploadAttachmentAsync(
        PortalSession session, Guid id, string fileName, long sizeBytes, Stream content, string? note, CancellationToken cancellationToken);

    /// <summary>Tải xuống file kèm đề nghị.</summary>
    /// <param name="session">Phiên Portal.</param>
    /// <param name="id">Đề nghị.</param>
    /// <param name="attachmentId">File.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Nội dung file.</returns>
    Task<AttachmentContent> OpenAttachmentAsync(PortalSession session, Guid id, Guid attachmentId, CancellationToken cancellationToken);

    /// <summary>Quản trị NCC xóa file kèm đề nghị còn chờ.</summary>
    /// <param name="session">Phiên Portal.</param>
    /// <param name="id">Đề nghị.</param>
    /// <param name="attachmentId">File.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    Task DeleteAttachmentAsync(PortalSession session, Guid id, Guid attachmentId, CancellationToken cancellationToken);
}
