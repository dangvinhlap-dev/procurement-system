using Microsoft.Extensions.Options;
using Proc.Application.Operations;
using Proc.Domain.Operations;
using Proc.Infrastructure.Notifications;

namespace Proc.Infrastructure.Operations;

/// <summary>
/// Cảnh báo cấu hình của máy đang chạy (lát QT1 – người dùng chốt 14/11/2026: máy thiếu cấu hình thì không gửi email và hiện cảnh báo):
/// chưa khai <c>App:Environment</c>; máy TEST bật email mà chưa khai <c>Email:RedirectAllTo</c>. Cả hai trường hợp chốt gửi thư
/// (<see cref="RedirectingEmailSender"/>) đều chặn thư.
/// </summary>
public sealed class EnvironmentStatus : IEnvironmentStatus
{
    /// <summary>Cảnh báo khi chưa khai môi trường.</summary>
    public const string NotConfiguredWarning =
        "Máy chủ chưa khai App:Environment (Dev / Test / Prod) – email không được gửi. Quản trị khai trong appsettings.Production.json rồi khởi động lại.";

    /// <summary>Cảnh báo khi máy TEST chưa khai hộp thư test.</summary>
    public const string TestWithoutRedirectWarning =
        "Máy TEST chưa khai Email:RedirectAllTo – email không được gửi (TEST không gửi thư tới người dùng, NCC thật).";

    private readonly AppEnvironmentOptions _environment;
    private readonly IOptionsMonitor<EmailOptions> _email;

    /// <summary>Tạo dịch vụ.</summary>
    /// <param name="environment">Môi trường.</param>
    /// <param name="email">Cấu hình email.</param>
    public EnvironmentStatus(IOptions<AppEnvironmentOptions> environment, IOptionsMonitor<EmailOptions> email)
    {
        _environment = environment.Value;
        _email = email;
    }

    /// <inheritdoc />
    public EnvironmentStatusDto Get()
    {
        var email = _email.CurrentValue;
        string? warning = null;
        if (!_environment.IsConfigured) warning = NotConfiguredWarning;
        else if (_environment.IsTest && email.Enabled && string.IsNullOrWhiteSpace(email.RedirectAllTo)) warning = TestWithoutRedirectWarning;
        return new EnvironmentStatusDto(_environment.Name, warning);
    }
}
