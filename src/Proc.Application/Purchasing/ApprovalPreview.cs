using Proc.Application.Sourcing;

namespace Proc.Application.Purchasing;

/// <summary>Yêu cầu xem trước chuỗi duyệt khi gửi duyệt một phiếu.</summary>
/// <param name="DocType">Loại phiếu: PR, PO, PRL, RFQ, CTR, CTA, NDQ.</param>
/// <param name="DocumentId">Id phiếu (đã lưu nháp).</param>
/// <param name="Award">RFQ: lựa chọn NCC trúng thầu sẽ gửi duyệt (giá trị trúng thầu quyết định số cấp).</param>
public sealed record ApprovalPreviewRequest(string DocType, Guid DocumentId, AwardRfqRequest? Award = null);

/// <summary>Chuỗi duyệt dự kiến nếu gửi duyệt ngay bây giờ.</summary>
/// <param name="ApprovedImmediately">Dưới mọi ngưỡng đã cấu hình – gửi là duyệt ngay, không cần ai duyệt.</param>
/// <param name="Steps">Các bước của lượt sẽ gửi (theo chặng, công ty, cấp) kèm người duyệt được từng bước.</param>
public sealed record ApprovalPreviewDto(bool ApprovedImmediately, IReadOnlyList<ApprovalStepDto> Steps);

/// <summary>
/// Xem trước chuỗi duyệt trước khi gửi (yêu cầu người dùng 03/11/2026 – popup "người duyệt tiếp theo là ai" khi gửi duyệt). Chạy thử đúng lệnh
/// gửi duyệt của loại phiếu trong một giao dịch rồi hủy, nên kết quả luôn khớp với khi gửi thật (bộ cấp PO khẩn, chặng ngoại lệ giá, ngưỡng
/// theo giá trị) và báo luôn lỗi khiến phiếu không gửi được. Quyền như lệnh gửi duyệt của loại phiếu.
/// </summary>
public interface IApprovalPreviewService
{
    /// <summary>Chuỗi duyệt dự kiến; không lưu gì.</summary>
    /// <param name="request">Loại phiếu, Id, (RFQ) lựa chọn trúng thầu.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Các bước và người duyệt được.</returns>
    /// <exception cref="Common.ForbiddenAccessException">Khi không có quyền gửi duyệt loại phiếu.</exception>
    /// <exception cref="Domain.Common.BusinessRuleException">Khi phiếu không gửi duyệt được (lỗi như khi gửi thật).</exception>
    Task<ApprovalPreviewDto> PreviewSubmitAsync(ApprovalPreviewRequest request, CancellationToken cancellationToken);
}
