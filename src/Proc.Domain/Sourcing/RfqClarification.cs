using Proc.Domain.Common;

namespace Proc.Domain.Sourcing;

/// <summary>
/// Một mục hỏi đáp làm rõ của RFQ (lát P3b-2 – design.md mục 19.5, quy tắc P3-11…P3-14): hoặc **câu hỏi** NCC được mời gửi trên Portal
/// (Phòng CUVT trả lời sau), hoặc **thông báo làm rõ** Phòng CUVT chủ động gửi mọi NCC được mời (<see cref="SupplierId"/> = null).
/// Mục hỏi đáp không có giá nên không niêm phong. Không gắn [Auditable]: câu hỏi do tài khoản Portal (không phải người dùng nội bộ) tạo;
/// mọi mục **không sửa, không xóa** và tự ghi người – giờ hỏi, người – giờ trả lời, nên chính bản ghi là dấu vết (như bản nộp báo giá P3a).
/// </summary>
public sealed class RfqClarification : Entity, ICompanyOwned
{
    /// <summary>Độ dài tối đa câu hỏi, câu trả lời, thông báo.</summary>
    public const int TextMaxLength = 2000;

    private RfqClarification()
    {
    }

    /// <inheritdoc />
    public Guid CompanyId { get; private set; }

    /// <summary>RFQ.</summary>
    public Guid RfqId { get; private set; }

    /// <summary>NCC hỏi; null = thông báo làm rõ của Phòng CUVT gửi mọi NCC được mời.</summary>
    public Guid? SupplierId { get; private set; }

    /// <summary>Câu hỏi của NCC (null với thông báo làm rõ).</summary>
    public string? Question { get; private set; }

    /// <summary>Thời điểm hỏi (UTC).</summary>
    public DateTimeOffset? AskedAt { get; private set; }

    /// <summary>Tài khoản Portal hỏi.</summary>
    public Guid? AskedByPortalUserId { get; private set; }

    /// <summary>Email tài khoản Portal lúc hỏi (giữ lại dù tài khoản đổi sau này).</summary>
    public string? AskedByEmail { get; private set; }

    /// <summary>Câu trả lời, hoặc nội dung thông báo làm rõ.</summary>
    public string? Answer { get; private set; }

    /// <summary>Thời điểm trả lời / đăng thông báo (UTC).</summary>
    public DateTimeOffset? AnsweredAt { get; private set; }

    /// <summary>Người dùng nội bộ trả lời / đăng thông báo.</summary>
    public Guid? AnsweredBy { get; private set; }

    /// <summary>Họ tên người trả lời lúc trả lời (giữ lại dù người dùng đổi tên sau này).</summary>
    public string? AnsweredByName { get; private set; }

    /// <summary>
    /// Là thông báo làm rõ (không có câu hỏi) – **công khai** cho mọi NCC được mời. Câu hỏi và câu trả lời thì **riêng** NCC hỏi, bảo mật với NCC
    /// khác (người dùng chốt 12/11/2026).
    /// </summary>
    public bool IsNotice => SupplierId is null;

    /// <summary>Đã có câu trả lời (thông báo làm rõ luôn coi là đã trả lời).</summary>
    public bool IsAnswered => AnsweredAt is not null;

    /// <summary>
    /// NCC gửi câu hỏi (P3-11): RFQ đang nhận báo giá, chưa mở báo giá Portal, chưa qua giờ hạn nộp; NCC được mời. Sau giờ hạn không hỏi
    /// nữa vì NCC không còn nộp lại báo giá được – câu trả lời không còn tác dụng.
    /// </summary>
    /// <param name="rfq">RFQ (đã nạp NCC được mời).</param>
    /// <param name="supplierId">NCC của phiên Portal.</param>
    /// <param name="portalUserId">Tài khoản Portal.</param>
    /// <param name="email">Email tài khoản.</param>
    /// <param name="question">Câu hỏi.</param>
    /// <param name="at">Thời điểm hỏi (UTC).</param>
    /// <param name="now">Bây giờ theo giờ Việt Nam.</param>
    /// <returns>Câu hỏi mới, chưa trả lời.</returns>
    /// <exception cref="BusinessRuleException">Khi RFQ không còn nhận câu hỏi, NCC không được mời hoặc câu hỏi trống / quá dài.</exception>
    public static RfqClarification Ask(
        Rfq rfq, Guid supplierId, Guid portalUserId, string email, string question, DateTimeOffset at, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(rfq);
        if (rfq.Status != RfqStatus.Sent || rfq.PortalQuotesOpenedAt is not null || now > rfq.DeadlineAt)
            throw new BusinessRuleException($"RFQ {rfq.No} không còn nhận câu hỏi (đã hết hạn nộp báo giá {rfq.DeadlineText} hoặc đã mở báo giá).");
        if (rfq.Suppliers.All(s => s.SupplierId != supplierId)) throw new BusinessRuleException("Nhà cung cấp không được mời báo giá RFQ này.");
        return new RfqClarification
        {
            CompanyId = rfq.CompanyId,
            RfqId = rfq.Id,
            SupplierId = supplierId,
            Question = Text(question, "Câu hỏi"),
            AskedAt = at,
            AskedByPortalUserId = portalUserId,
            AskedByEmail = email,
        };
    }

    /// <summary>
    /// Phòng CUVT đăng thông báo làm rõ gửi mọi NCC được mời (P3-13), luôn công khai. Chỉ khi RFQ đang nhận báo giá / xét chọn
    /// (trạng thái Đã gửi).
    /// </summary>
    /// <param name="rfq">RFQ.</param>
    /// <param name="userId">Người đăng.</param>
    /// <param name="userName">Họ tên người đăng.</param>
    /// <param name="text">Nội dung.</param>
    /// <param name="at">Thời điểm (UTC).</param>
    /// <returns>Thông báo mới.</returns>
    /// <exception cref="BusinessRuleException">Khi RFQ không ở trạng thái Đã gửi hoặc nội dung trống / quá dài.</exception>
    public static RfqClarification Announce(Rfq rfq, Guid userId, string userName, string text, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(rfq);
        RequireSent(rfq);
        return new RfqClarification
        {
            CompanyId = rfq.CompanyId,
            RfqId = rfq.Id,
            Answer = Text(text, "Nội dung thông báo"),
            AnsweredAt = at,
            AnsweredBy = userId,
            AnsweredByName = userName,
        };
    }

    /// <summary>
    /// Trả lời câu hỏi (P3-12) – một lần, không sửa (cần đính chính thì đăng thông báo làm rõ mới). Câu trả lời **chỉ NCC hỏi thấy**, bảo mật
    /// với NCC khác (người dùng chốt 12/11/2026); thông tin cần báo mọi NCC thì đăng thông báo làm rõ (không nêu NCC nào hỏi).
    /// </summary>
    /// <param name="rfq">RFQ của câu hỏi.</param>
    /// <param name="userId">Người trả lời.</param>
    /// <param name="userName">Họ tên người trả lời.</param>
    /// <param name="answer">Câu trả lời.</param>
    /// <param name="at">Thời điểm (UTC).</param>
    /// <exception cref="BusinessRuleException">Khi là thông báo, đã trả lời, RFQ không ở trạng thái Đã gửi hoặc câu trả lời trống / quá dài.</exception>
    public void Reply(Rfq rfq, Guid userId, string userName, string answer, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(rfq);
        if (rfq.Id != RfqId) throw new BusinessRuleException("Câu hỏi không thuộc RFQ này.");
        if (IsNotice) throw new BusinessRuleException("Thông báo làm rõ không có câu hỏi để trả lời.");
        if (IsAnswered) throw new BusinessRuleException("Câu hỏi đã được trả lời – cần bổ sung thì đăng thông báo làm rõ.");
        RequireSent(rfq);
        Answer = Text(answer, "Câu trả lời");
        AnsweredAt = at;
        AnsweredBy = userId;
        AnsweredByName = userName;
    }

    /// <summary>RFQ đã chọn thầu, chờ duyệt kết quả hoặc đã hủy thì hỏi đáp không còn tác dụng với việc chào giá.</summary>
    private static void RequireSent(Rfq rfq)
    {
        if (rfq.Status != RfqStatus.Sent)
            throw new BusinessRuleException($"RFQ {rfq.No} không ở trạng thái đang nhận báo giá – không trả lời, đăng thông báo được.");
    }

    private static string Text(string? value, string label)
    {
        var text = value?.Trim();
        if (string.IsNullOrEmpty(text)) throw new BusinessRuleException($"{label} không được để trống.");
        if (text.Length > TextMaxLength) throw new BusinessRuleException($"{label} tối đa {TextMaxLength:N0} ký tự.".Replace(",", ".", StringComparison.Ordinal));
        return text;
    }
}
