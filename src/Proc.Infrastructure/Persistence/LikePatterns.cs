namespace Proc.Infrastructure.Persistence;

/// <summary>
/// Mẫu LIKE cho tìm kiếm theo cột SearchText (đã bỏ dấu): thoát ký tự đặc biệt để từ khóa "50%" hay "m_8" được hiểu đúng
/// là chữ, không phải ký tự đại diện. Dùng kèm <c>EF.Functions.Like(cột, mẫu, "\\")</c>.
/// </summary>
internal static class LikePatterns
{
    /// <summary>Ký tự thoát truyền vào EF.Functions.Like.</summary>
    public const string Escape = "\\";

    /// <summary>
    /// Mẫu "chứa từ khóa". Dấu <c>*</c> là ký tự đại diện kiểu SAP ECC (yêu cầu người dùng 02/11/2026): "*" một mình khớp mọi
    /// dòng (liệt kê toàn bộ danh mục), "b*long" khớp "bu long".
    /// </summary>
    /// <param name="word">Một từ khóa đã chuẩn hóa.</param>
    /// <returns>Ví dụ "m_8" → "%m\_8%"; "b*long" → "%b%long%"; "*" → "%%%".</returns>
    public static string Contains(string word) => "%" + EscapeText(word).Replace("*", "%", StringComparison.Ordinal) + "%";

    /// <summary>Từ khóa có ký tự đại diện <c>*</c> – ô tìm nới giới hạn số dòng trả về (liệt kê toàn bộ).</summary>
    /// <param name="search">Chuỗi người dùng gõ.</param>
    /// <returns>true khi có dấu *.</returns>
    public static bool HasWildcard(string? search) => search is not null && search.Contains('*', StringComparison.Ordinal);

    /// <summary>
    /// Mẫu "bắt đầu bằng" (mã danh mục theo tiền tố).
    /// </summary>
    /// <param name="prefix">Tiền tố.</param>
    /// <returns>Ví dụ "VT_" → "VT\_%".</returns>
    public static string StartsWith(string prefix) => EscapeText(prefix) + "%";

    private static string EscapeText(string text) =>
        text.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
}
