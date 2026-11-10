namespace Proc.Infrastructure.Notifications;

/// <summary>
/// Cấu hình gửi email bản sao thông báo qua SMTP (lát O2b, design.md mục 19.3) – mục <c>Email</c> trong appsettings hoặc biến môi trường
/// <c>Email__Host</c>, <c>Email__Password</c>… Mặc định tắt: máy dev, môi trường test không gửi thư thật. Mật khẩu SMTP không ghi vào file
/// trong kho mã – đặt bằng biến môi trường trên máy chủ.
/// </summary>
public sealed class EmailOptions
{
    /// <summary>Tên mục cấu hình.</summary>
    public const string Section = "Email";

    /// <summary>Bật gửi email. Tắt thì thông báo vẫn có trong phần mềm, không gửi thư.</summary>
    public bool Enabled { get; set; }

    /// <summary>Máy chủ SMTP, ví dụ mail.congty.vn.</summary>
    public string Host { get; set; } = string.Empty;

    /// <summary>Cổng SMTP (587 STARTTLS, 465 SSL, 25 không mã hóa).</summary>
    public int Port { get; set; } = 587;

    /// <summary>Kiểu mã hóa: Auto, StartTls, SslOnConnect, None.</summary>
    public string Security { get; set; } = "Auto";

    /// <summary>Tài khoản đăng nhập SMTP; trống = không đăng nhập (máy chủ nội bộ cho gửi thẳng).</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>Mật khẩu SMTP – đặt bằng biến môi trường <c>Email__Password</c>.</summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>Địa chỉ người gửi, ví dụ muahang@congty.vn.</summary>
    public string FromAddress { get; set; } = string.Empty;

    /// <summary>Tên người gửi hiện trong hộp thư.</summary>
    public string FromName { get; set; } = "Phần mềm mua hàng";

    /// <summary>
    /// Địa chỉ trang web của phần mềm mà người nhận mở được (LAN/VPN), ví dụ http://muahang.congty.local – dùng tạo nút "Mở trên phần mềm".
    /// Trống thì thư không có nút mở.
    /// </summary>
    public string AppBaseUrl { get; set; } = string.Empty;

    /// <summary>Chu kỳ quét thông báo chờ gửi (giây).</summary>
    public int PollSeconds { get; set; } = 30;

    /// <summary>Số thư tối đa mỗi lần quét.</summary>
    public int BatchSize { get; set; } = 50;

    /// <summary>
    /// Thông báo cũ hơn số giờ này thì thôi gửi – tránh dồn thư hàng loạt khi vừa bật email hoặc máy chủ thư hỏng lâu.
    /// </summary>
    public int MaxAgeHours { get; set; } = 24;

    /// <summary>Số lần thử gửi tối đa một thư (mỗi lần quét thử một lần).</summary>
    public int MaxAttempts { get; set; } = 5;
}
