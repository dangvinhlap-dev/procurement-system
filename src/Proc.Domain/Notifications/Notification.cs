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

    private static string Cut(string text, int max) => text.Length <= max ? text : text[..(max - 1)] + "…";
}
