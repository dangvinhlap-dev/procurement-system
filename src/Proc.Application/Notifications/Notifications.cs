namespace Proc.Application.Notifications;

/// <summary>Một thông báo của người đăng nhập (design.md mục 11.6).</summary>
/// <param name="Id">Khóa.</param>
/// <param name="Type">Loại: ApprovalPending, Approved, Rejected, IntercompanyReceipt, PeriodReopened.</param>
/// <param name="Title">Tiêu đề.</param>
/// <param name="Message">Nội dung.</param>
/// <param name="ScreenCode">Mã màn hình để mở khi bấm (PR, PO, ICR, PER…).</param>
/// <param name="TargetId">Phiếu/kỳ cần mở; null nếu chỉ mở màn hình.</param>
/// <param name="CreatedAt">Thời điểm tạo (UTC).</param>
/// <param name="ReadAt">Thời điểm đọc (UTC); null = chưa đọc.</param>
public sealed record NotificationDto(
    Guid Id, string Type, string Title, string Message, string ScreenCode, Guid? TargetId, DateTimeOffset CreatedAt, DateTimeOffset? ReadAt);

/// <summary>
/// Thông báo của chính người đăng nhập tại công ty đang làm việc: xem, đánh dấu đã đọc. Không có API tạo – thông báo chỉ do hệ thống
/// sinh từ sự kiện nghiệp vụ.
/// </summary>
public interface INotificationService
{
    /// <summary>Thông báo của tôi, mới nhất trước (tối đa 500).</summary>
    /// <param name="unreadOnly">Chỉ thông báo chưa đọc.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Danh sách.</returns>
    Task<IReadOnlyList<NotificationDto>> ListMineAsync(bool unreadOnly, CancellationToken cancellationToken);

    /// <summary>Số thông báo chưa đọc của tôi.</summary>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Số chưa đọc.</returns>
    Task<int> UnreadCountAsync(CancellationToken cancellationToken);

    /// <summary>Đánh dấu một thông báo của tôi là đã đọc.</summary>
    /// <param name="id">Thông báo.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <exception cref="Common.NotFoundException">Không phải thông báo của tôi ở công ty đang làm việc.</exception>
    Task MarkReadAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Đánh dấu mọi thông báo của tôi ở công ty đang làm việc là đã đọc.</summary>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    Task MarkAllReadAsync(CancellationToken cancellationToken);
}
