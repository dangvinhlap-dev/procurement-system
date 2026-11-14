using Proc.Application.Portal;

namespace Proc.Application.MasterData;

/// <summary>Lọc danh sách đề nghị cập nhật hồ sơ NCC.</summary>
/// <param name="Status">Trạng thái (Submitted / Accepted / Rejected / Withdrawn); trống = mọi trạng thái.</param>
/// <param name="SupplierId">Chỉ một NCC.</param>
public sealed record SupplierProfileRequestQuery(string? Status, Guid? SupplierId);

/// <summary>
/// Đề nghị cập nhật hồ sơ NCC gửi từ Portal – phía nội bộ (lát P2a, design.md 19.5 P2-4): xem cần quyền xem NCC; chấp nhận / từ chối cần sửa
/// được NCC (quyền "Thêm, sửa nhà cung cấp" khi làm việc ở tổng công ty, hoặc quyền sửa danh mục dùng chung).
/// </summary>
public interface ISupplierProfileRequestService
{
    /// <summary>Danh sách đề nghị, mới nhất trước.</summary>
    /// <param name="query">Bộ lọc.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Danh sách.</returns>
    Task<IReadOnlyList<SupplierProfileRequestDto>> ListAsync(SupplierProfileRequestQuery query, CancellationToken cancellationToken);

    /// <summary>Chi tiết một đề nghị kèm so sánh với hồ sơ hiện tại.</summary>
    /// <param name="id">Đề nghị.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Đề nghị.</returns>
    Task<SupplierProfileRequestDto> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Chấp nhận: ghi thông tin chung, người liên hệ vào NCC; tài khoản ngân hàng đổi thì lập yêu cầu duyệt NCC (Q7); báo NCC qua email.
    /// </summary>
    /// <param name="id">Đề nghị.</param>
    /// <param name="request">Ý kiến (không bắt buộc).</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Đề nghị sau xử lý.</returns>
    Task<SupplierProfileRequestDto> AcceptAsync(Guid id, SupplierProfileCommentRequest request, CancellationToken cancellationToken);

    /// <summary>Từ chối (bắt buộc lý do); báo NCC qua email.</summary>
    /// <param name="id">Đề nghị.</param>
    /// <param name="request">Lý do.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Đề nghị sau xử lý.</returns>
    Task<SupplierProfileRequestDto> RejectAsync(Guid id, SupplierProfileCommentRequest request, CancellationToken cancellationToken);
}
