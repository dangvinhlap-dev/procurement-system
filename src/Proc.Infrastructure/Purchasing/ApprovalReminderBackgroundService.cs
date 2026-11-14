using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Proc.Application.Operations;
using Proc.Application.Purchasing;

namespace Proc.Infrastructure.Purchasing;

/// <summary>
/// Dịch vụ nền nhắc quá hạn duyệt (lát O2c, design.md mục 19.3): mỗi <see cref="ApprovalReminderOptions.PollMinutes"/> phút gọi
/// <see cref="IApprovalReminderService.RemindAsync"/> trong một scope mới. Tắt thì chỉ chờ (bật lại bằng cấu hình không cần khởi động lại).
/// Lỗi một lần quét (kể cả trùng lần nhắc do hai tiến trình cùng quét) chỉ ghi nhật ký, lần sau quét lại.
/// </summary>
public sealed class ApprovalReminderBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IOptionsMonitor<ApprovalReminderOptions> _options;
    private readonly ILogger<ApprovalReminderBackgroundService> _logger;
    private readonly IBackgroundHeartbeat _heartbeat;

    /// <summary>Tạo dịch vụ nền.</summary>
    /// <param name="scopes">Tạo scope DI cho mỗi lần quét (DbContext theo scope).</param>
    /// <param name="options">Cấu hình nhắc.</param>
    /// <param name="logger">Nhật ký.</param>
    /// <param name="heartbeat">Báo nhịp cho màn hình SYS (lát QT1).</param>
    public ApprovalReminderBackgroundService(
        IServiceScopeFactory scopes, IOptionsMonitor<ApprovalReminderOptions> options, ILogger<ApprovalReminderBackgroundService> logger,
        IBackgroundHeartbeat heartbeat)
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
                    var result = await scope.ServiceProvider.GetRequiredService<IApprovalReminderService>().RemindAsync(stoppingToken);
                    if (result.Steps > 0)
                        _logger.LogInformation("Nhắc quá hạn duyệt: {Steps} bước, {Recipients} người nhận.", result.Steps, result.Recipients);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Lần quét nhắc quá hạn duyệt bị lỗi.");
                    note = $"Lần quét lỗi: {ex.Message}";
                }
            }
            else
            {
                note = "Đang tắt (ApprovalReminder:Enabled = false).";
            }

            var minutes = Math.Clamp(_options.CurrentValue.PollMinutes, 1, 1440);
            _heartbeat.Beat("Nhắc quá hạn duyệt", minutes * 60, note);

            try
            {
                await Task.Delay(TimeSpan.FromMinutes(minutes), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }
}
