using PdfSharp.Fonts;

namespace Proc.Infrastructure.Printing;

/// <summary>
/// Phông chữ cho phiếu in PDF (design.md mục 10.10). PDFsharp bản đa nền tảng không tự tìm phông hệ thống nên phải chỉ file .ttf; phông
/// phải có đủ dấu tiếng Việt. Tìm lần lượt Arial (Windows), DejaVu Sans, Liberation Sans (Linux: gói fonts-dejavu-core) trong các thư mục
/// cấu hình <c>Printing:FontDirectories</c> rồi các thư mục phông mặc định. Phông được nhúng vào file PDF nên máy mở file không cần cài.
/// Mẫu in biểu mẫu CUVT dùng phông có chân <see cref="SerifFamily"/>: Times New Roman (Windows), Liberation Serif, DejaVu Serif (Linux);
/// không có thì dùng phông không chân ở trên (lát Y10, design.md mục 10.10).
/// </summary>
public static class PdfFonts
{
    /// <summary>Tên họ phông dùng trong mẫu phiếu; bộ chọn phông đổi sang file thật.</summary>
    public const string Family = "Proc Sans";

    /// <summary>Họ phông có chân cho mẫu in biểu mẫu CUVT (biểu mẫu quy định Times New Roman).</summary>
    public const string SerifFamily = "Proc Serif";

    /// <summary>Phông có chân (thường, đậm) có dấu tiếng Việt, thử lần lượt trên Windows và Linux.</summary>
    private static readonly (string Regular, string Bold)[] SerifCandidates =
    [
        ("times.ttf", "timesbd.ttf"),
        ("LiberationSerif-Regular.ttf", "LiberationSerif-Bold.ttf"),
        ("DejaVuSerif.ttf", "DejaVuSerif-Bold.ttf"),
    ];

    /// <summary>Các cặp phông chữ (thường, đậm) có dấu tiếng Việt, thử lần lượt trên Windows và Linux.</summary>
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
                var sans = (File.ReadAllBytes(regularPath), File.ReadAllBytes(Find(directories, bold) ?? regularPath));
                GlobalFontSettings.FontResolver = new Resolver(sans, FindSerif(directories) ?? sans);
                _installed = true;
                return;
            }

            throw new InvalidOperationException(
                "Không tìm thấy phông chữ tiếng Việt để in PDF (Arial, DejaVu Sans hoặc Liberation Sans). Trên Linux cài gói fonts-dejavu-core, " +
                "hoặc khai báo thư mục chứa file .ttf ở Printing:FontDirectories trong appsettings.");
        }
    }

    /// <summary>Phông có chân đầu tiên tìm thấy (thường, đậm); null khi máy không có – mẫu CUVT dùng phông không chân.</summary>
    private static (byte[] Regular, byte[] Bold)? FindSerif(IReadOnlyList<string> directories)
    {
        foreach (var (regular, bold) in SerifCandidates)
        {
            var regularPath = Find(directories, regular);
            if (regularPath is not null) return (File.ReadAllBytes(regularPath), File.ReadAllBytes(Find(directories, bold) ?? regularPath));
        }
        return null;
    }

    /// <summary>Tìm file phông trong các thư mục, so tên không phân biệt hoa thường (Linux phân biệt hoa thường).</summary>
    private static string? Find(IEnumerable<string> directories, string fileName) =>
        directories
            .Where(Directory.Exists)
            .Select(d => Directory.EnumerateFiles(d, "*.ttf", SearchOption.TopDirectoryOnly)
                .FirstOrDefault(f => string.Equals(Path.GetFileName(f), fileName, StringComparison.OrdinalIgnoreCase)))
            .FirstOrDefault(f => f is not null);

    /// <summary>
    /// Đổi họ phông của mẫu phiếu sang dữ liệu file .ttf thường / đậm (nghiêng giả lập bằng PDFsharp): <see cref="SerifFamily"/> → phông có
    /// chân, họ khác → phông không chân.
    /// </summary>
    private sealed class Resolver((byte[] Regular, byte[] Bold) sans, (byte[] Regular, byte[] Bold) serif) : IFontResolver
    {
        public FontResolverInfo? ResolveTypeface(string familyName, bool isBold, bool isItalic)
        {
            var prefix = familyName == SerifFamily ? "proc-serif" : "proc";
            return new(isBold ? prefix + "-bold" : prefix + "-regular", false, isItalic);
        }

        public byte[]? GetFont(string faceName) => faceName switch
        {
            "proc-bold" => sans.Bold,
            "proc-serif-regular" => serif.Regular,
            "proc-serif-bold" => serif.Bold,
            _ => sans.Regular,
        };
    }
}
