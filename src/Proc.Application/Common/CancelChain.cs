namespace Proc.Application.Common;

/// <summary>Một phiếu trong trình tự hủy dây chuyền.</summary>
/// <param name="Order">Thứ tự hủy (1 = hủy trước – phiếu cuối của chuỗi).</param>
/// <param name="DocType">Mã loại phiếu (PR, PO, XNH).</param>
/// <param name="DocTypeName">Tên loại phiếu.</param>
/// <param name="Id">Id phiếu.</param>
/// <param name="No">Số phiếu.</param>
/// <param name="Status">Trạng thái hiện tại (chữ tiếng Việt).</param>
/// <param name="Note">Ghi chú: vì sao phiếu nằm trong chuỗi, ví dụ "Nhận hàng của PO-…".</param>
public sealed record CancelChainItemDto(int Order, string DocType, string DocTypeName, Guid Id, string No, string Status, string? Note);

/// <summary>Trình tự hủy dây chuyền xem trước hoặc đã thực hiện.</summary>
/// <param name="Items">Các phiếu theo thứ tự hủy – từ phiếu cuối về phiếu đầu (phiếu được chọn hủy nằm cuối danh sách).</param>
/// <param name="Warnings">
/// Ảnh hưởng ngoài chuỗi, ví dụ PO gộp dòng từ nhiều PR: hủy PO thì SL của các PR khác trở về "còn lại chưa đặt" (design.md mục 20.2 D3).
/// </param>
/// <param name="BlockedReason">Lý do không hủy được (thiếu quyền, phiếu ngoài phạm vi…); null = hủy được.</param>
public sealed record CancelChainDto(IReadOnlyList<CancelChainItemDto> Items, IReadOnlyList<string> Warnings, string? BlockedReason);

/// <summary>Lệnh hủy dây chuyền.</summary>
/// <param name="Reason">Lý do hủy (bắt buộc) – ghi vào mọi phiếu trong chuỗi.</param>
public sealed record CancelChainRequest(string? Reason);

/// <summary>
/// Hủy dây chuyền (lát Z4b, design.md mục 20.2 D3, 20.2a): hủy một phiếu thì hủy theo trình tự từ phiếu cuối về phiếu đầu của chuỗi chứng từ
/// liên quan (đợt đầu: PR → PO → phiếu xác nhận nhận hàng XNH), trong một giao dịch – một phiếu lỗi thì không hủy phiếu nào. Số liệu (SL đã
/// đặt của PR, SL đã nhận của PO, ngân sách đã dùng) tính từ chứng từ chưa hủy nên tự trả về đúng. Quyền: phiếu Nháp – người lập; còn lại –
/// quyền Hủy phiếu (quản trị); bắt buộc lý do.
/// </summary>
public interface ICancelChainService
{
    /// <summary>Xem trước trình tự sẽ hủy (không đổi dữ liệu).</summary>
    /// <param name="docType">Mã loại phiếu được chọn hủy (PR, PO, XNH).</param>
    /// <param name="id">Id phiếu.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Trình tự, cảnh báo, lý do bị chặn.</returns>
    /// <exception cref="Proc.Domain.Common.BusinessRuleException">Khi loại phiếu chưa hỗ trợ hủy dây chuyền.</exception>
    /// <exception cref="NotFoundException">Khi không thấy phiếu.</exception>
    Task<CancelChainDto> PreviewAsync(string docType, Guid id, CancellationToken cancellationToken);

    /// <summary>Hủy cả chuỗi trong một giao dịch, từ phiếu cuối về phiếu đầu.</summary>
    /// <param name="docType">Mã loại phiếu được chọn hủy.</param>
    /// <param name="id">Id phiếu.</param>
    /// <param name="request">Lý do hủy.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Trình tự đã hủy.</returns>
    /// <exception cref="ForbiddenAccessException">Khi thiếu quyền hủy một phiếu trong chuỗi.</exception>
    /// <exception cref="Proc.Domain.Common.BusinessRuleException">Khi thiếu lý do hoặc một phiếu không hủy được.</exception>
    Task<CancelChainDto> CancelAsync(string docType, Guid id, CancelChainRequest request, CancellationToken cancellationToken);
}
