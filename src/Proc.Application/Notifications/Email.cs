namespace Proc.Application.Notifications;

/// <summary>Một thư cần gửi.</summary>
/// <param name="ToAddress">Địa chỉ người nhận.</param>
/// <param name="ToName">Tên người nhận.</param>
/// <param name="Subject">Tiêu đề.</param>
/// <param name="HtmlBody">Nội dung HTML.</param>
/// <param name="TextBody">Nội dung chữ thường (cho trình đọc thư không hiện HTML).</param>
public sealed record EmailMessage(string ToAddress, string ToName, string Subject, string HtmlBody, string TextBody);

/// <summary>
/// Gửi thư qua máy chủ thư của công ty (lát O2b). Tách interface để test thay bằng bộ gửi giả, không cần máy chủ SMTP.
/// </summary>
public interface IEmailSender
{
    /// <summary>Gửi một thư.</summary>
    /// <param name="message">Thư.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <exception cref="Exception">Lỗi kết nối, đăng nhập hoặc máy chủ thư từ chối.</exception>
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}

/// <summary>Kết quả một lần quét gửi email bản sao thông báo.</summary>
/// <param name="Sent">Số thư đã gửi.</param>
/// <param name="Skipped">Số thông báo thôi gửi (không có email, quá cũ…).</param>
/// <param name="Failed">Số thư gửi lỗi lần này.</param>
public sealed record EmailDispatchResult(int Sent, int Skipped, int Failed);

/// <summary>Tình trạng cấu hình email cho người quản trị.</summary>
/// <param name="Enabled">Đang bật gửi email.</param>
/// <param name="Host">Máy chủ SMTP.</param>
/// <param name="Port">Cổng.</param>
/// <param name="FromAddress">Địa chỉ người gửi.</param>
/// <param name="AppBaseUrl">Địa chỉ phần mềm dùng trong nút mở.</param>
/// <param name="Pending">Số thông báo đang chờ gửi email.</param>
/// <param name="Failed">Số thông báo gửi email lỗi (đã hết lượt thử).</param>
public sealed record EmailStatusDto(bool Enabled, string Host, int Port, string FromAddress, string AppBaseUrl, int Pending, int Failed);

/// <summary>
/// Gửi email bản sao thông báo (lát O2b, design.md mục 19.3): mỗi thông báo hệ thống sinh ra (chờ duyệt, đã duyệt, bị từ chối, duyệt thay,
/// nhắc quá hạn…) được dịch vụ nền gửi thêm một thư tới địa chỉ email của người nhận.
/// </summary>
public interface INotificationEmailService
{
    /// <summary>
    /// Một lần quét: lấy các thông báo chờ gửi (cũ trước), gửi thư, đánh dấu kết quả. Email đang tắt thì không làm gì. Dịch vụ nền gọi định kỳ.
    /// </summary>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Số thư gửi được, bỏ qua, lỗi.</returns>
    Task<EmailDispatchResult> DispatchAsync(CancellationToken cancellationToken);

    /// <summary>Tình trạng cấu hình và hàng chờ (không lộ mật khẩu).</summary>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Tình trạng.</returns>
    Task<EmailStatusDto> StatusAsync(CancellationToken cancellationToken);

    /// <summary>Gửi thư thử tới email của chính người đang đăng nhập (kiểm tra cấu hình SMTP).</summary>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Địa chỉ đã gửi tới.</returns>
    /// <exception cref="Domain.Common.BusinessRuleException">Email đang tắt, người dùng chưa khai email, hoặc máy chủ thư báo lỗi (kèm lỗi).</exception>
    Task<string> SendTestAsync(CancellationToken cancellationToken);
}
