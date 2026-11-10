using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using Proc.Application.Notifications;

namespace Proc.Infrastructure.Notifications;

/// <summary>
/// Gửi thư qua SMTP bằng MailKit (MIT – lát O2b). Mỗi thư một kết nối: lượng thư ít (thông báo duyệt), đơn giản hơn giữ kết nối và không bị
/// máy chủ thư cắt kết nối rỗi.
/// </summary>
public sealed class SmtpEmailSender : IEmailSender
{
    private readonly IOptionsMonitor<EmailOptions> _options;

    /// <summary>Tạo bộ gửi thư.</summary>
    /// <param name="options">Cấu hình email (đọc lại mỗi lần gửi để đổi cấu hình không cần khởi động lại).</param>
    public SmtpEmailSender(IOptionsMonitor<EmailOptions> options) => _options = options;

    /// <inheritdoc />
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        var o = _options.CurrentValue;
        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(o.FromName, o.FromAddress));
        mime.To.Add(new MailboxAddress(message.ToName, message.ToAddress));
        mime.Subject = message.Subject;
        mime.Body = new BodyBuilder { HtmlBody = message.HtmlBody, TextBody = message.TextBody }.ToMessageBody();

        using var client = new SmtpClient { Timeout = 30_000 };
        await client.ConnectAsync(o.Host, o.Port, Security(o.Security), cancellationToken);
        if (!string.IsNullOrEmpty(o.UserName)) await client.AuthenticateAsync(o.UserName, o.Password, cancellationToken);
        await client.SendAsync(mime, cancellationToken);
        await client.DisconnectAsync(quit: true, cancellationToken);
    }

    /// <summary>Đổi tên kiểu mã hóa trong cấu hình sang tùy chọn MailKit; tên lạ coi như Auto.</summary>
    private static SecureSocketOptions Security(string value) => value.Trim().ToUpperInvariant() switch
    {
        "STARTTLS" => SecureSocketOptions.StartTls,
        "SSLONCONNECT" or "SSL" => SecureSocketOptions.SslOnConnect,
        "NONE" => SecureSocketOptions.None,
        _ => SecureSocketOptions.Auto,
    };
}
