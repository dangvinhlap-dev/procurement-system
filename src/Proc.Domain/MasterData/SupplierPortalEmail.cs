using Proc.Domain.Common;
using Proc.Domain.Notifications;

namespace Proc.Domain.MasterData;

/// <summary>Loại thư báo NCC qua Portal (lát P3b-3 – design.md mục 19.5, P3-15…P3-18).</summary>
public enum SupplierPortalEmailKind
{
    /// <summary>Thư mời báo giá – lúc gửi RFQ.</summary>
    RfqInvitation = 1,

    /// <summary>Gia hạn nộp báo giá.</summary>
    RfqDeadlineExtended = 2,

    /// <summary>RFQ đã gửi bị hủy.</summary>
    RfqCancelled = 3,

    /// <summary>Bên mời trả lời câu hỏi của NCC (chỉ NCC hỏi).</summary>
    RfqAnswer = 4,

    /// <summary>Thông báo làm rõ công khai cho mọi NCC được mời.</summary>
    RfqNotice = 5,

    /// <summary>Đơn đặt hàng mới – PO đặt cho NCC vừa duyệt xong (lát P4a).</summary>
    PurchaseOrderIssued = 6,

    /// <summary>Đơn đặt hàng đã duyệt bị hủy (lát P4a) – không ghi lý do.</summary>
    PurchaseOrderCancelled = 7,

    /// <summary>Bên đặt hàng mở lại phản hồi đơn – NCC xác nhận / từ chối lại (kèm lý do mở lại).</summary>
    PurchaseOrderReopened = 8,

    /// <summary>Báo NCC trúng thầu (toàn bộ hoặc một phần dòng đã chào) – khi kết quả chọn thầu duyệt xong (lát P4b).</summary>
    RfqAwarded = 9,

    /// <summary>Báo NCC có báo giá nhưng không trúng thầu – khi kết quả chọn thầu duyệt xong (lát P4b).</summary>
    RfqNotAwarded = 10,

    /// <summary>Bên đặt hàng đã thanh toán một đợt (kế toán ghi Đã chi đề nghị thanh toán của PO – lát P6b, P6b-5).</summary>
    PaymentPaid = 11,

    /// <summary>Phòng CUVT chấp nhận đề nghị cập nhật hồ sơ NCC (lát P2a).</summary>
    ProfileRequestAccepted = 12,

    /// <summary>Phòng CUVT từ chối đề nghị cập nhật hồ sơ NCC (lát P2a).</summary>
    ProfileRequestRejected = 13,
}

/// <summary>
/// Một thư chờ gửi / đã gửi tới một tài khoản Portal NCC (lát P3b-3 – design.md mục 19.5, P3-15…P3-18). Thư được **soạn sẵn và ghi cùng giao
/// dịch** với thao tác gốc (gửi RFQ, gia hạn, hủy, trả lời, thông báo) – thao tác thành công thì chắc chắn có thư trong hàng chờ; dịch vụ nền
/// email gửi sau, lỗi máy chủ thư không làm hỏng thao tác gốc. Không gắn [Auditable]: hệ thống tự sinh, không ai sửa; chính bản ghi là dấu vết
/// "đã báo NCC lúc nào, tới địa chỉ nào". Có <see cref="CompanyId"/> (công ty mời) để danh sách thư trên RFQ đi qua bộ lọc công ty.
/// </summary>
public sealed class SupplierPortalEmail : Entity, ICompanyOwned
{
    /// <summary>Độ dài tối đa tiêu đề thư.</summary>
    public const int SubjectMaxLength = 300;

    /// <summary>Độ dài tối đa ghi chú gửi (lỗi máy chủ thư, lý do bỏ qua).</summary>
    public const int NoteMaxLength = 500;

    private SupplierPortalEmail()
    {
    }

    /// <inheritdoc />
    public Guid CompanyId { get; private set; }

    /// <summary>NCC nhận thư.</summary>
    public Guid SupplierId { get; private set; }

    /// <summary>Tài khoản Portal nhận thư.</summary>
    public Guid PortalUserId { get; private set; }

    /// <summary>Địa chỉ nhận lúc soạn thư (tài khoản đổi email sau đó không làm sai dấu vết).</summary>
    public string ToAddress { get; private set; } = string.Empty;

    /// <summary>Tên người nhận.</summary>
    public string ToName { get; private set; } = string.Empty;

    /// <summary>Loại thư.</summary>
    public SupplierPortalEmailKind Kind { get; private set; }

    /// <summary>Loại chứng từ liên quan: "RFQ" hoặc "PO".</summary>
    public string DocType { get; private set; } = string.Empty;

    /// <summary>Id chứng từ liên quan.</summary>
    public Guid DocId { get; private set; }

    /// <summary>Tiêu đề thư.</summary>
    public string Subject { get; private set; } = string.Empty;

    /// <summary>Nội dung HTML.</summary>
    public string HtmlBody { get; private set; } = string.Empty;

    /// <summary>Nội dung chữ thường.</summary>
    public string TextBody { get; private set; } = string.Empty;

    /// <summary>Lúc soạn (UTC).</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Trạng thái gửi (dùng chung trạng thái với email bản sao thông báo).</summary>
    public NotificationEmailState State { get; private set; } = NotificationEmailState.Pending;

    /// <summary>Số lần đã thử gửi.</summary>
    public int Attempts { get; private set; }

    /// <summary>Lúc gửi xong / thôi gửi (UTC).</summary>
    public DateTimeOffset? SentAt { get; private set; }

    /// <summary>Lỗi lần gửi gần nhất hoặc lý do bỏ qua.</summary>
    public string? Note { get; private set; }

    /// <summary>Soạn một thư chờ gửi.</summary>
    /// <param name="companyId">Công ty mời.</param>
    /// <param name="supplierId">NCC nhận.</param>
    /// <param name="portalUserId">Tài khoản Portal nhận.</param>
    /// <param name="toAddress">Email nhận.</param>
    /// <param name="toName">Tên người nhận.</param>
    /// <param name="kind">Loại thư.</param>
    /// <param name="docType">Loại chứng từ (ví dụ "RFQ").</param>
    /// <param name="docId">Id chứng từ.</param>
    /// <param name="subject">Tiêu đề (dài quá thì cắt).</param>
    /// <param name="htmlBody">Nội dung HTML.</param>
    /// <param name="textBody">Nội dung chữ thường.</param>
    /// <param name="at">Lúc soạn (UTC).</param>
    /// <returns>Thư ở trạng thái chờ gửi.</returns>
    /// <exception cref="BusinessRuleException">Thiếu địa chỉ nhận hoặc tiêu đề.</exception>
    public static SupplierPortalEmail Queue(
        Guid companyId, Guid supplierId, Guid portalUserId, string toAddress, string toName, SupplierPortalEmailKind kind, string docType,
        Guid docId, string subject, string htmlBody, string textBody, DateTimeOffset at)
    {
        if (string.IsNullOrWhiteSpace(toAddress)) throw new BusinessRuleException("Thư Portal thiếu địa chỉ nhận.");
        if (string.IsNullOrWhiteSpace(subject)) throw new BusinessRuleException("Thư Portal thiếu tiêu đề.");
        return new SupplierPortalEmail
        {
            CompanyId = companyId,
            SupplierId = supplierId,
            PortalUserId = portalUserId,
            ToAddress = toAddress.Trim(),
            ToName = toName?.Trim() ?? string.Empty,
            Kind = kind,
            DocType = docType,
            DocId = docId,
            Subject = Cut(subject.Trim(), SubjectMaxLength),
            HtmlBody = htmlBody,
            TextBody = textBody,
            CreatedAt = at,
        };
    }

    /// <summary>Đã gửi.</summary>
    /// <param name="at">Thời điểm (UTC).</param>
    public void MarkSent(DateTimeOffset at)
    {
        Attempts++;
        State = NotificationEmailState.Sent;
        SentAt = at;
        Note = null;
    }

    /// <summary>Thôi gửi (tài khoản đã khóa, NCC vào danh sách đen, thư quá cũ…).</summary>
    /// <param name="at">Thời điểm (UTC).</param>
    /// <param name="reason">Lý do.</param>
    public void Skip(DateTimeOffset at, string reason)
    {
        State = NotificationEmailState.Skipped;
        SentAt = at;
        Note = Cut(reason, NoteMaxLength);
    }

    /// <summary>Gửi lỗi: ghi lỗi; còn lượt thì lần quét sau gửi lại, hết <paramref name="maxAttempts"/> lượt thì thôi (trạng thái Lỗi).</summary>
    /// <param name="at">Thời điểm (UTC).</param>
    /// <param name="error">Thông báo lỗi.</param>
    /// <param name="maxAttempts">Số lần thử tối đa.</param>
    public void MarkFailed(DateTimeOffset at, string error, int maxAttempts)
    {
        Attempts++;
        Note = Cut(error, NoteMaxLength);
        if (Attempts < maxAttempts) return;
        State = NotificationEmailState.Failed;
        SentAt = at;
    }

    private static string Cut(string text, int max) => text.Length <= max ? text : text[..(max - 1)] + "…";
}
