using Microsoft.EntityFrameworkCore;
using Proc.Application.Common;
using Proc.Application.Notifications;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.Notifications;

/// <summary>
/// Thông báo của người đăng nhập (design.md mục 11.6). Filter công ty giới hạn ở công ty đang làm việc; điều kiện người nhận = chính
/// mình thêm ở mọi truy vấn – thông báo của người khác coi như không tồn tại (404), không lộ là có hay không.
/// </summary>
internal sealed class NotificationService : INotificationService
{
    /// <summary>Số thông báo tối đa trả về một lần – đủ cho màn hình, không tải cả lịch sử nhiều năm.</summary>
    private const int MaxItems = 500;

    private readonly ProcDbContext _db;
    private readonly ICurrentUserScope _scope;
    private readonly IClock _clock;

    /// <summary>Tạo dịch vụ.</summary>
    public NotificationService(ProcDbContext db, ICurrentUserScope scope, IClock clock)
    {
        _db = db;
        _scope = scope;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<NotificationDto>> ListMineAsync(bool unreadOnly, CancellationToken cancellationToken)
    {
        var mine = Mine();
        if (unreadOnly) mine = mine.Where(n => n.ReadAt == null);
        return await mine
            .OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.Id)
            .Take(MaxItems)
            .Select(n => new NotificationDto(n.Id, n.Type, n.Title, n.Message, n.ScreenCode, n.TargetId, n.CreatedAt, n.ReadAt))
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<int> UnreadCountAsync(CancellationToken cancellationToken) =>
        Mine().CountAsync(n => n.ReadAt == null, cancellationToken);

    /// <inheritdoc />
    public async Task MarkReadAsync(Guid id, CancellationToken cancellationToken)
    {
        var notification = await Mine().SingleOrDefaultAsync(n => n.Id == id, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy thông báo.");
        notification.MarkRead(_clock.UtcNow);
        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task MarkAllReadAsync(CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        await Mine().Where(n => n.ReadAt == null).ExecuteUpdateAsync(u => u.SetProperty(n => n.ReadAt, now), cancellationToken);
    }

    /// <summary>Thông báo của tôi ở công ty đang làm việc.</summary>
    private IQueryable<Domain.Notifications.Notification> Mine()
    {
        _scope.RequireCompany();
        var userId = _scope.UserId ?? throw new ForbiddenAccessException("Chưa đăng nhập.");
        return _db.Notifications.Where(n => n.UserId == userId);
    }
}
