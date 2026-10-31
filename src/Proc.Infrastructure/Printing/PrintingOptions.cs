namespace Proc.Infrastructure.Printing;

/// <summary>Cấu hình in phiếu PDF – mục <c>Printing</c> trong appsettings (design.md mục 10.10).</summary>
public sealed class PrintingOptions
{
    /// <summary>
    /// Thư mục chứa phông .ttf có tiếng Việt, xét trước thư mục phông mặc định của hệ điều hành – dùng khi server không cài phông ở chỗ
    /// mặc định, ví dụ <c>/app/fonts</c>.
    /// </summary>
    public string[] FontDirectories { get; set; } = [];
}
