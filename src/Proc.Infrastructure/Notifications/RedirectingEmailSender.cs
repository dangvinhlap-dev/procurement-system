using Microsoft.Extensions.Options;
using Proc.Application.Notifications;
using Proc.Application.Operations;
using Proc.Domain.Operations;

namespace Proc.Infrastructure.Notifications;

/// <summary>
/// Bọc bộ gửi thư thật để mọi thư (thông báo nội bộ, thư mời / OTP Portal NCC, nhắc duyệt) đi qua một chốt (design.md mục 21.3, quyết định
/// E5 – lát QT1): khai <see cref="EmailOptions.RedirectAllTo"/> thì đổi người nhận thành hộp thư đó và thêm "[TEST] gửi cho: …" vào đầu tiêu
/// đề; máy <c>App:Environment=Test</c> mà chưa khai thì <b>không gửi</b> (báo lỗi để thư nằm ở hàng chờ) – TEST giữ dữ liệu thật nên không bao
/// giờ được gửi tới người dùng, NCC thật. Máy chưa khai <c>App:Environment</c> cũng không gửi thư nào (người dùng chốt 14/11/2026).
/// </summary>
public sealed class RedirectingEmailSender : IEmailSender
{
    private readonly IEmailSender _inner;
    private readonly IOptionsMonitor<EmailOptions> _options;
    private readonly AppEnvironmentOptions _environment;

    /// <summary>Tạo bộ gửi.</summary>
    /// <param name="inner">Bộ gửi thật (SMTP).</param>
    /// <param name="options">Cấu hình email (đọc lại mỗi lần gửi).</param>
    /// <param name="environment">Môi trường đang chạy.</param>
    public RedirectingEmailSender(IEmailSender inner, IOptionsMonitor<EmailOptions> options, IOptions<AppEnvironmentOptions> environment)
    {
        _inner = inner;
        _options = options;
        _environment = environment.Value;
    }

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">Khi máy chưa khai <c>App:Environment</c>, hoặc máy TEST chưa khai <c>Email:RedirectAllTo</c>.</exception>
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken) =>
        _inner.SendAsync(Apply(message, _options.CurrentValue.RedirectAllTo, _environment.Name), cancellationToken);

    /// <summary>
    /// Đổi thư theo cấu hình chuyển hướng. Ví dụ thư gửi "an@congty.vn", tiêu đề "Phiếu chờ duyệt" với RedirectAllTo = "test@congty.vn" →
    /// gửi "test@congty.vn", tiêu đề "[TEST] gửi cho: an@congty.vn – Phiếu chờ duyệt".
    /// </summary>
    /// <param name="message">Thư gốc.</param>
    /// <param name="redirectAllTo">Hộp thư nhận thay; trống = không chuyển.</param>
    /// <param name="environment">Môi trường đang chạy (Dev / Test / Prod / None).</param>
    /// <returns>Thư sẽ gửi.</returns>
    /// <exception cref="InvalidOperationException">Khi máy chưa khai môi trường, hoặc máy TEST chưa khai hộp thư nhận thay.</exception>
    public static EmailMessage Apply(EmailMessage message, string? redirectAllTo, string environment)
    {
        if (environment == AppEnvironments.None)
            throw new InvalidOperationException(
                "Máy chủ chưa khai App:Environment (Dev / Test / Prod) – không gửi email cho tới khi khai (design.md mục 21.3).");
        var redirect = redirectAllTo?.Trim();
        if (string.IsNullOrEmpty(redirect))
        {
            if (environment == AppEnvironments.Test)
                throw new InvalidOperationException(
                    "Máy TEST chưa khai Email:RedirectAllTo – không gửi thư tới người dùng thật (design.md mục 21.3, E5).");
            return message;
        }

        return message with
        {
            ToAddress = redirect,
            ToName = $"[TEST] {message.ToName}",
            Subject = $"[TEST] gửi cho: {message.ToAddress} – {message.Subject}",
        };
    }
}
