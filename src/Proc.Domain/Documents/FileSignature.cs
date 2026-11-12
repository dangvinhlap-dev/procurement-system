using Proc.Domain.Common;

namespace Proc.Domain.Documents;

/// <summary>
/// Kiểm nội dung file đính kèm theo chữ ký đầu file ("magic number") – lát P1b, design.md 19.5 (quy tắc P1b-3).
/// Đuôi file do người gửi tự đặt nên không đủ tin: Portal mở ra internet, NCC có thể gửi trang HTML hoặc chương trình đổi đuôi thành
/// ".pdf". Áp cho mọi file đính kèm (nội bộ và Portal) để một quy tắc dùng chung.
/// </summary>
public static class FileSignature
{
    /// <summary>
    /// Số byte đầu file cần đọc để kiểm. 1.024 vì chuẩn PDF cho phép "%PDF-" nằm bất kỳ đâu trong 1.024 byte đầu (một số máy quét chèn
    /// byte rác phía trước).
    /// </summary>
    public const int HeaderLength = 1024;

    private static readonly byte[] Zip = [0x50, 0x4B, 0x03, 0x04];
    private static readonly byte[] ZipEmpty = [0x50, 0x4B, 0x05, 0x06];
    private static readonly byte[] ZipSpanned = [0x50, 0x4B, 0x07, 0x08];
    private static readonly byte[] OleCompound = [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1];
    private static readonly byte[] Pdf = "%PDF-"u8.ToArray();

    /// <summary>Chữ ký file chạy được – chặn với mọi đuôi (kể cả đuôi văn bản .txt, .csv vốn không có chữ ký riêng).</summary>
    private static readonly byte[][] Executables =
    [
        "MZ"u8.ToArray(), // Windows .exe/.dll/.com
        [0x7F, 0x45, 0x4C, 0x46], // Linux ELF
        [0xFE, 0xED, 0xFA, 0xCE], [0xFE, 0xED, 0xFA, 0xCF], // macOS Mach-O
        [0xCE, 0xFA, 0xED, 0xFE], [0xCF, 0xFA, 0xED, 0xFE],
        [0xCA, 0xFE, 0xBA, 0xBE], // Mach-O nhiều kiến trúc, Java .class
        "#!"u8.ToArray(), // script Unix
    ];

    /// <summary>
    /// Đuôi → (tên định dạng hiện trong thông báo, các chữ ký hợp lệ ở đầu file). Mảng chữ ký rỗng: định dạng văn bản không có chữ ký
    /// riêng (txt, csv, xml, eml) – chỉ kiểm không phải file chạy được.
    /// </summary>
    private static readonly Dictionary<string, (string Format, byte[][] Signatures)> Rules = new()
    {
        [".pdf"] = ("PDF", [Pdf]),
        [".docx"] = ("Word", [Zip, ZipEmpty, ZipSpanned]),
        [".xlsx"] = ("Excel", [Zip, ZipEmpty, ZipSpanned]),
        [".pptx"] = ("PowerPoint", [Zip, ZipEmpty, ZipSpanned]),
        [".zip"] = ("ZIP", [Zip, ZipEmpty, ZipSpanned]),
        [".doc"] = ("Word 97-2003", [OleCompound]),
        [".xls"] = ("Excel 97-2003", [OleCompound]),
        [".ppt"] = ("PowerPoint 97-2003", [OleCompound]),
        [".msg"] = ("thư Outlook", [OleCompound]),
        [".jpg"] = ("ảnh JPEG", [[0xFF, 0xD8, 0xFF]]),
        [".jpeg"] = ("ảnh JPEG", [[0xFF, 0xD8, 0xFF]]),
        [".png"] = ("ảnh PNG", [[0x89, 0x50, 0x4E, 0x47]]),
        [".gif"] = ("ảnh GIF", ["GIF87a"u8.ToArray(), "GIF89a"u8.ToArray()]),
        [".bmp"] = ("ảnh BMP", ["BM"u8.ToArray()]),
        [".tif"] = ("ảnh TIFF", [[0x49, 0x49, 0x2A, 0x00], [0x4D, 0x4D, 0x00, 0x2A]]),
        [".tiff"] = ("ảnh TIFF", [[0x49, 0x49, 0x2A, 0x00], [0x4D, 0x4D, 0x00, 0x2A]]),
        [".rar"] = ("RAR", [[0x52, 0x61, 0x72, 0x21, 0x1A, 0x07]]),
        [".7z"] = ("7-Zip", [[0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C]]),
        [".txt"] = ("văn bản", []),
        [".csv"] = ("CSV", []),
        [".xml"] = ("XML", []),
        [".eml"] = ("thư email", []),
    };

    /// <summary>Đuôi có quy tắc kiểm hay chưa – test bảo đảm mọi đuôi trong <see cref="Attachment.AllowedExtensions"/> đều có.</summary>
    /// <param name="extension">Đuôi chữ thường có dấu chấm.</param>
    /// <returns>true nếu có quy tắc.</returns>
    public static bool HasRule(string extension) => Rules.ContainsKey(extension);

    /// <summary>
    /// Kiểm các byte đầu file khớp định dạng của đuôi và không phải chương trình chạy được.
    /// </summary>
    /// <param name="extension">Đuôi chữ thường có dấu chấm (đã qua <see cref="Attachment.ValidateFile"/>).</param>
    /// <param name="fileName">Tên file gốc – chỉ để ghi vào thông báo lỗi.</param>
    /// <param name="header">Tối đa <see cref="HeaderLength"/> byte đầu file.</param>
    /// <exception cref="BusinessRuleException">Khi file rỗng, là file chạy được, hoặc nội dung không khớp đuôi.</exception>
    public static void EnsureMatches(string extension, string fileName, ReadOnlySpan<byte> header)
    {
        if (header.IsEmpty) throw new BusinessRuleException("File rỗng.");
        if (!Rules.TryGetValue(extension, out var rule))
            throw new BusinessRuleException($"Không nhận file '{extension}'.");

        foreach (var executable in Executables)
        {
            if (header.StartsWith(executable))
                throw new BusinessRuleException($"File '{fileName}' là chương trình chạy được – không nhận dù đổi đuôi.");
        }

        if (rule.Signatures.Length == 0) return;
        foreach (var signature in rule.Signatures)
        {
            // PDF: tìm "%PDF-" trong 1.024 byte đầu; các định dạng khác: chữ ký phải ở đúng đầu file.
            var matched = ReferenceEquals(signature, Pdf) ? header[..Math.Min(header.Length, HeaderLength)].IndexOf(signature) >= 0
                : header.StartsWith(signature);
            if (matched) return;
        }

        throw new BusinessRuleException(
            $"Nội dung file '{fileName}' không đúng định dạng {rule.Format} (đuôi file có thể đã bị đổi). Hãy mở file bằng phần mềm gốc, " +
            "lưu lại đúng định dạng rồi tải lên.");
    }
}
