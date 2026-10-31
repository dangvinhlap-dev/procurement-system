using PdfSharp.Fonts;

namespace Proc.Infrastructure.Printing;

/// <summary>
/// Phông chữ cho phiếu in PDF (design.md mục 10.10). PDFsharp bản đa nền tảng không tự tìm phông hệ thống nên phải chỉ file .ttf; phông
/// phải có đủ dấu tiếng Việt. Tìm lần lượt Arial (Windows), DejaVu Sans, Liberation Sans (Linux: gói fonts-dejavu-core) trong các thư mục
/// cấu hình <c>Printing:FontDirectories</c> rồi các thư mục phông mặc định. Phông được nhúng vào file PDF nên máy mở file không cần cài.
/// </summary>
public static class PdfFonts
{
    /// <summary>Tên họ phông dùng trong mẫu phiếu; bộ chọn phông đổi sang file thật.</summary>
    public const string Family = "Proc Sans";

    private static readonly (string Regular, string Bold)[] Candidates =
    [
        ("arial.ttf", "arialbd.ttf"),
        ("DejaVuSans.ttf", "DejaVuSans-Bold.ttf"),
        ("LiberationSans-Regular.ttf", "LiberationSans-Bold.ttf"),
    ];

    private static readonly string[] DefaultDirectories =
    [
        Environment.GetFolderPath(Environment.SpecialFolder.Fonts),
        "/usr/share/fonts/truetype/dejavu",
        "/usr/share/fonts/truetype/liberation",
        "/usr/share/fonts/dejavu",
        "/usr/share/fonts/TTF",
    ];

    private static readonly Lock Gate = new();
    private static bool _installed;

    /// <summary>
    /// Cài bộ chọn phông cho PDFsharp (một lần cho cả tiến trình – PDFsharp chỉ cho đặt trước khi dùng phông lần đầu).
    /// </summary>
    /// <param name="extraDirectories">Thư mục phông cấu hình thêm, xét trước thư mục mặc định.</param>
    /// <exception cref="InvalidOperationException">Khi không tìm thấy phông nào có tiếng Việt – báo rõ cách cài.</exception>
    public static void Install(IEnumerable<string>? extraDirectories = null)
    {
        lock (Gate)
        {
            if (_installed) return;
            var directories = (extraDirectories ?? []).Concat(DefaultDirectories).Where(d => !string.IsNullOrWhiteSpace(d)).ToList();
            foreach (var (regular, bold) in Candidates)
            {
                var regularPath = Find(directories, regular);
                if (regularPath is null) continue;
                GlobalFontSettings.FontResolver = new Resolver(File.ReadAllBytes(regularPath), File.ReadAllBytes(Find(directories, bold) ?? regularPath));
                _installed = true;
                return;
            }

            throw new InvalidOperationException(
                "Không tìm thấy phông chữ tiếng Việt để in PDF (Arial, DejaVu Sans hoặc Liberation Sans). Trên Linux cài gói fonts-dejavu-core, " +
                "hoặc khai báo thư mục chứa file .ttf ở Printing:FontDirectories trong appsettings.");
        }
    }

    private static string? Find(IEnumerable<string> directories, string fileName) =>
        directories
            .Where(Directory.Exists)
            .Select(d => Directory.EnumerateFiles(d, "*.ttf", SearchOption.TopDirectoryOnly)
                .FirstOrDefault(f => string.Equals(Path.GetFileName(f), fileName, StringComparison.OrdinalIgnoreCase)))
            .FirstOrDefault(f => f is not null);

    /// <summary>Đổi họ phông của mẫu phiếu sang dữ liệu file .ttf thường / đậm (nghiêng giả lập bằng PDFsharp).</summary>
    private sealed class Resolver(byte[] regular, byte[] bold) : IFontResolver
    {
        public FontResolverInfo? ResolveTypeface(string familyName, bool isBold, bool isItalic) =>
            new(isBold ? "proc-bold" : "proc-regular", false, isItalic);

        public byte[]? GetFont(string faceName) => faceName == "proc-bold" ? bold : regular;
    }
}
