using Proc.Domain.Common;

namespace Proc.Domain.Notifications;

/// <summary>Loại thông báo (design.md mục 11.6).</summary>
public static class NotificationTypes
{
    /// <summary>Có phiếu PR/PO chờ mình duyệt (cấp đang chờ).</summary>
    public const string ApprovalPending = "ApprovalPending";

    /// <summary>Phiếu mình lập đã được duyệt xong.</summary>
    public const string Approved = "Approved";

    /// <summary>Phiếu mình lập bị từ chối (kèm lý do).</summary>
    public const string Rejected = "Rejected";

    /// <summary>Công ty khác xuất bán nội bộ, phiếu nhập mua nội bộ chờ mình xác nhận nhận hàng.</summary>
    public const string IntercompanyReceipt = "IntercompanyReceipt";

    /// <summary>Kỳ kho được mở lại – số liệu đã xuất cho kế toán có thể thay đổi.</summary>
    public const string PeriodReopened = "PeriodReopened";

    /// <summary>KSNB ghi nhận hoặc kết luận vi phạm – gửi thẳng lãnh đạo (design.md mục 18.19).</summary>
    public const string ControlFinding = "ControlFinding";

    /// <summary>Người được ủy quyền vừa duyệt/từ chối thay mình (lát O2, design.md mục 19.3).</summary>
    public const string DelegatedAction = "DelegatedAction";

    /// <summary>Nhắc: phiếu chờ mình duyệt đã quá 1 ngày làm việc (lát O2c, design.md mục 19.3).</summary>
    public const string ApprovalOverdue = "ApprovalOverdue";

    /// <summary>Công ty khác vừa được duyệt đơn mua đặt cho công ty mình (NCC nội bộ) – chờ xác nhận ở "Đơn bán nội bộ" (lát X7).</summary>
    public const string InternalSaleOrder = "InternalSaleOrder";

    /// <summary>Công ty bán đã xác nhận / từ chối đơn mua nội bộ – báo người lập PO (lát X7).</summary>
    public const string InternalSaleDecision = "InternalSaleDecision";

    /// <summary>NCC gửi câu hỏi làm rõ RFQ trên Portal – báo người lập RFQ (lát P3b-2).</summary>
    public const string PortalQuestion = "PortalQuestion";
}

/// <summary>
/// Một thông báo gửi tới một người dùng tại một công ty (design.md mục 11.6). Hệ thống sinh cùng giao dịch với sự kiện nghiệp vụ;
/// người nhận chỉ đánh dấu đã đọc. Không gắn [Auditable]: đây là tin nhắn hệ thống, không phải dữ liệu nghiệp vụ – sự kiện gốc
/// (gửi duyệt, duyệt, ghi sổ, mở lại kỳ) đã có nhật ký riêng.
/// </summary>
public sealed class Notification : Entity, ICompanyOwned
{
    /// <summary>Độ dài tối đa của tiêu đề.</summary>
    public const int TitleMaxLength = 200;

    /// <summary>Độ dài tối đa của nội dung.</summary>
    public const int MessageMaxLength = 1000;

    private Notification()
    {
    }

    /// <inheritdoc />
    public Guid CompanyId { get; private set; }

    /// <summary>Người nhận.</summary>
    public Guid UserId { get; private set; }

    /// <summary>Loại – xem <see cref="NotificationTypes"/>.</summary>
    public string Type { get; private set; } = string.Empty;

    /// <summary>Tiêu đề ngắn.</summary>
    public string Title { get; private set; } = string.Empty;

    /// <summary>Nội dung (số phiếu, lý do…).</summary>
    public string Message { get; private set; } = string.Empty;

    /// <summary>
    /// Mã màn hình để mở khi bấm (PR, PO, ICR, PER…). Lưu mã màn hình thay vì đường dẫn web để đổi đường dẫn giao diện không làm hỏng
    /// thông báo cũ.
    /// </summary>
    public string ScreenCode { get; private set; } = string.Empty;

    /// <summary>Đối tượng cần mở (phiếu, kỳ kho); null nếu chỉ mở màn hình.</summary>
    public Guid? TargetId { get; private set; }

    /// <summary>Thời điểm tạo (UTC).</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Thời điểm người nhận đọc (UTC); null = chưa đọc.</summary>
    public DateTimeOffset? ReadAt { get; private set; }

    /// <summary>
    /// Tạo thông báo. Tiêu đề, nội dung dài quá thì cắt (kèm "…") thay vì báo lỗi – thông báo không được làm hỏng thao tác gốc.
    /// </summary>
    /// <param name="companyId">Công ty của sự kiện.</param>
    /// <param name="userId">Người nhận.</param>
    /// <param name="type">Loại.</param>
    /// <param name="title">Tiêu đề.</param>
    /// <param name="message">Nội dung.</param>
    /// <param name="screenCode">Mã màn hình để mở.</param>
    /// <param name="targetId">Đối tượng cần mở.</param>
    /// <param name="createdAt">Thời điểm tạo (UTC).</param>
    /// <returns>Thông báo mới, chưa đọc.</returns>
    public static Notification Create(
        Guid companyId, Guid userId, string type, string title, string message, string screenCode, Guid? targetId, DateTimeOffset createdAt) =>
        new()
        {
            CompanyId = companyId,
            UserId = userId,
            Type = type,
            Title = Cut(title, TitleMaxLength),
            Message = Cut(message, MessageMaxLength),
            ScreenCode = screenCode,
            TargetId = targetId,
            CreatedAt = createdAt,
        };

    /// <summary>Đánh dấu đã đọc; đọc rồi thì giữ thời điểm đọc lần đầu.</summary>
    /// <param name="at">Thời điểm đọc (UTC).</param>
    public void MarkRead(DateTimeOffset at) => ReadAt ??= at;

    /// <summary>Gửi email bản sao (lát O2b): chờ gửi, đã gửi, bỏ qua, lỗi.</summary>
    public NotificationEmailState EmailState { get; private set; } = NotificationEmailState.Pending;

    /// <summary>Số lần đã thử gửi email.</summary>
    public int EmailAttempts { get; private set; }

    /// <summary>Thời điểm gửi email xong hoặc thôi gửi (UTC).</summary>
    public DateTimeOffset? EmailedAt { get; private set; }

    /// <summary>Lỗi lần gửi gần nhất hoặc lý do bỏ qua (ví dụ "Người nhận chưa khai email").</summary>
    public string? EmailNote { get; private set; }

    /// <summary>Độ dài tối đa của ghi chú gửi email.</summary>
    public const int EmailNoteMaxLength = 500;

    /// <summary>Đã gửi email.</summary>
    /// <param name="at">Thời điểm (UTC).</param>
    public void MarkEmailSent(DateTimeOffset at)
    {
        EmailAttempts++;
        EmailState = NotificationEmailState.Sent;
        EmailedAt = at;
        EmailNote = null;
    }

    /// <summary>Thôi gửi email (người nhận không có email, thông báo quá cũ…).</summary>
    /// <param name="at">Thời điểm (UTC).</param>
    /// <param name="reason">Lý do.</param>
    public void SkipEmail(DateTimeOffset at, string reason)
    {
        EmailState = NotificationEmailState.Skipped;
        EmailedAt = at;
        EmailNote = Cut(reason, EmailNoteMaxLength);
    }

    /// <summary>
    /// Gửi email lỗi: ghi lỗi, còn lượt thì lần quét sau gửi lại; hết <paramref name="maxAttempts"/> lượt thì thôi (trạng thái Lỗi).
    /// </summary>
    /// <param name="at">Thời điểm (UTC).</param>
    /// <param name="error">Thông báo lỗi.</param>
    /// <param name="maxAttempts">Số lần thử tối đa.</param>
    public void MarkEmailFailed(DateTimeOffset at, string error, int maxAttempts)
    {
        EmailAttempts++;
        EmailNote = Cut(error, EmailNoteMaxLength);
        if (EmailAttempts < maxAttempts) return;
        EmailState = NotificationEmailState.Failed;
        EmailedAt = at;
    }

    /// <summary>Cắt chuỗi về tối đa max ký tự, thêm dấu "…" khi bị cắt.</summary>
    private static string Cut(string text, int max) => text.Length <= max ? text : text[..(max - 1)] + "…";
}

/// <summary>Trạng thái gửi email bản sao của thông báo (lát O2b, design.md mục 19.3).</summary>
public enum NotificationEmailState
{
    /// <summary>Chờ dịch vụ nền gửi.</summary>
    Pending = 0,

    /// <summary>Đã gửi.</summary>
    Sent = 1,

    /// <summary>Không gửi: người nhận chưa khai email / ngừng dùng, thông báo quá cũ, hoặc thông báo có trước bản 0.22.0.</summary>
    Skipped = 2,

    /// <summary>Gửi lỗi quá số lần cho phép.</summary>
    Failed = 3,
}
