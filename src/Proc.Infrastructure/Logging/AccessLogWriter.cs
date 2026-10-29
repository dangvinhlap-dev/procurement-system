using Microsoft.Extensions.DependencyInjection;
using Proc.Application.Common;
using Proc.Application.Logging;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.Logging;

/// <summary>
/// Ghi AccessLog bằng DbContext riêng (scope DI riêng), không dùng chung DbContext của request:
/// <list type="bullet">
/// <item>Không lưu kèm thay đổi dở dang của request (ví dụ tài khoản đang xung đột ghi) – lỗi ở đó không làm mất nhật ký,
/// nhật ký cũng không làm lỗi thao tác chính.</item>
/// <item>Lưu ngay, độc lập giao dịch của thao tác, để lần đăng nhập sai cũng để lại dấu vết.</item>
/// </list>
/// </summary>
public sealed class AccessLogWriter : IAccessLog
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IRequestContext _request;
    private readonly IClock _clock;

    /// <summary>
    /// Tạo bộ ghi.
    /// </summary>
    /// <param name="scopes">Tạo scope DI riêng để có DbContext riêng.</param>
    /// <param name="request">IP, trình duyệt, mã liên kết của request hiện tại.</param>
    /// <param name="clock">Đồng hồ.</param>
    public AccessLogWriter(IServiceScopeFactory scopes, IRequestContext request, IClock clock)
    {
        _scopes = scopes;
        _request = request;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task WriteAsync(
        AccessEvent accessEvent,
        Guid? userId,
        string? userName,
        Guid? companyId,
        string? detail,
        CancellationToken cancellationToken)
    {
        await using var scope = _scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ProcDbContext>();
        db.AccessLogs.Add(new AccessLog
        {
            OccurredAt = _clock.UtcNow,
            Event = accessEvent.ToString(),
            UserId = userId,
            // Tên gõ ở form đăng nhập có thể rất dài hoặc rác: cắt bớt để không làm hỏng lần ghi.
            UserName = userName?.Trim() is { Length: > 0 } name ? name[..Math.Min(name.Length, 256)] : null,
            CompanyId = companyId,
            Detail = detail?.Length > 500 ? detail[..500] : detail,
            IpAddress = _request.IpAddress,
            UserAgent = _request.UserAgent,
            CorrelationId = _request.CorrelationId,
        });
        await db.SaveChangesAsync(cancellationToken);
    }
}
