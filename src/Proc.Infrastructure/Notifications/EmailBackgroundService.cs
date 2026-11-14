using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Proc.Application.Notifications;
using Proc.Application.Operations;
using Proc.Application.Portal;

namespace Proc.Infrastructure.Notifications;

/// <summary>
/// Dịch vụ nền gửi email bản sao thông báo (lát O2b): mỗi <see cref="EmailOptions.PollSeconds"/> giây gọi
/// <see cref="INotificationEmailService.DispatchAsync"/> trong một scope mới. Email tắt thì chỉ chờ (đổi cấu hình bật lại không cần khởi động
/// lại ứng dụng). Lỗi một lần quét chỉ ghi nhật ký, không dừng dịch vụ. Giả định một tiến trình ứng dụng (IIS một worker / một container) –
/// nhiều tiến trình cùng quét có thể gửi trùng thư.
/// </summary>
public sealed class EmailBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IOptionsMonitor<EmailOptions> _options;
    private readonly ILogger<EmailBackgroundService> _logger;
    private readonly IBackgroundHeartbeat _heartbeat;

    /// <summary>Tạo dịch vụ nền.</summary>
    /// <param name="scopes">Tạo scope DI cho mỗi lần quét (DbContext theo scope).</param>
    /// <param name="options">Cấu hình email.</param>
    /// <param name="logger">Nhật ký.</param>
    /// <param name="heartbeat">Báo nhịp cho màn hình SYS (lát QT1).</param>
    public EmailBackgroundService(
        IServiceScopeFactory scopes, IOptionsMonitor<EmailOptions> options, ILogger<EmailBackgroundService> logger, IBackgroundHeartbeat heartbeat)
    {
        _heartbeat = heartbeat;
        _scopes = scopes;
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            string? note = null;
            if (_options.CurrentValue.Enabled)
            {
                try
                {
                    await using var scope = _scopes.CreateAsyncScope();
                    var result = await scope.ServiceProvider.GetRequiredService<INotificationEmailService>().DispatchAsync(stoppingToken);
                    if (result.Sent + result.Failed > 0)
                        _logger.LogInformation("Email thông báo: gửi {Sent}, bỏ qua {Skipped}, lỗi {Failed}.", result.Sent, result.Skipped, result.Failed);
                    // Thư báo NCC qua Portal (lát P3b-3) dùng chung máy chủ thư và chu kỳ quét.
                    var portal = await scope.ServiceProvider.GetRequiredService<IPortalEmailService>().DispatchAsync(stoppingToken);
                    if (portal.Sent + portal.Failed > 0)
                        _logger.LogInformation("Email NCC qua Portal: gửi {Sent}, bỏ qua {Skipped}, lỗi {Failed}.", portal.Sent, portal.Skipped, portal.Failed);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Lần quét gửi email thông báo bị lỗi.");
                    note = $"Lần quét lỗi: {ex.Message}";
                }
            }
            else
            {
                note = "Đang tắt (Email:Enabled = false).";
            }

            var seconds = Math.Clamp(_options.CurrentValue.PollSeconds, 5, 3600);
            _heartbeat.Beat("Gửi email", seconds, note);

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(seconds), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }
}
