namespace Proc.Application.Sourcing;

/// <summary>Một mục hỏi đáp làm rõ của RFQ phía nội bộ (lát P3b-2 – design.md mục 19.5, P3-11…P3-14): thấy đủ NCC hỏi, người hỏi.</summary>
/// <param name="Id">Mục hỏi đáp.</param>
/// <param name="IsNotice">Thông báo làm rõ của Phòng CUVT (không có câu hỏi).</param>
/// <param name="SupplierId">NCC hỏi (null với thông báo).</param>
/// <param name="SupplierCode">Mã NCC hỏi.</param>
/// <param name="SupplierName">Tên NCC hỏi.</param>
/// <param name="Question">Câu hỏi.</param>
/// <param name="AskedAt">Thời điểm hỏi.</param>
/// <param name="AskedByEmail">Tài khoản Portal hỏi.</param>
/// <param name="Answer">Câu trả lời / nội dung thông báo (null = chưa trả lời).</param>
/// <param name="AnsweredAt">Thời điểm trả lời / đăng.</param>
/// <param name="AnsweredByName">Người trả lời / đăng.</param>
public sealed record RfqClarificationDto(
    Guid Id, bool IsNotice, Guid? SupplierId, string? SupplierCode, string? SupplierName, string? Question, DateTimeOffset? AskedAt,
    string? AskedByEmail, string? Answer, DateTimeOffset? AnsweredAt, string? AnsweredByName);

/// <summary>Trả lời câu hỏi của NCC – chỉ NCC hỏi thấy.</summary>
/// <param name="Answer">Câu trả lời (tối đa 2.000 ký tự).</param>
public sealed record ReplyClarificationRequest(string Answer);

/// <summary>Đăng thông báo làm rõ gửi mọi NCC được mời.</summary>
/// <param name="Text">Nội dung (tối đa 2.000 ký tự).</param>
public sealed record AnnounceClarificationRequest(string Text);

/// <summary>
/// Hỏi đáp làm rõ RFQ phía nội bộ (lát P3b-2). RFQ lọc theo công ty đang làm việc và phạm vi nhóm hàng như các thao tác RFQ khác.
/// </summary>
public interface IRfqClarificationService
{
    /// <summary>Mọi mục hỏi đáp của RFQ, cũ trước.</summary>
    /// <param name="rfqId">RFQ.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Danh sách.</returns>
    /// <exception cref="Common.NotFoundException">Khi RFQ không thuộc công ty đang làm việc.</exception>
    Task<IReadOnlyList<RfqClarificationDto>> ListAsync(Guid rfqId, CancellationToken cancellationToken);

    /// <summary>Trả lời một câu hỏi (một lần, không sửa).</summary>
    /// <param name="rfqId">RFQ.</param>
    /// <param name="id">Câu hỏi.</param>
    /// <param name="request">Câu trả lời.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Danh sách hỏi đáp sau khi trả lời.</returns>
    /// <exception cref="Common.NotFoundException">Khi RFQ hoặc câu hỏi không tồn tại.</exception>
    /// <exception cref="Domain.Common.BusinessRuleException">Khi đã trả lời, RFQ không còn ở trạng thái Đã gửi, nội dung trống.</exception>
    Task<IReadOnlyList<RfqClarificationDto>> ReplyAsync(Guid rfqId, Guid id, ReplyClarificationRequest request, CancellationToken cancellationToken);

    /// <summary>Đăng thông báo làm rõ cho mọi NCC được mời.</summary>
    /// <param name="rfqId">RFQ.</param>
    /// <param name="request">Nội dung.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Danh sách hỏi đáp sau khi đăng.</returns>
    /// <exception cref="Domain.Common.BusinessRuleException">Khi RFQ không ở trạng thái Đã gửi, nội dung trống.</exception>
    Task<IReadOnlyList<RfqClarificationDto>> AnnounceAsync(Guid rfqId, AnnounceClarificationRequest request, CancellationToken cancellationToken);
}
