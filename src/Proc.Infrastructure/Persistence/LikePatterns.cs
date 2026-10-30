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
    /// Mẫu "chứa từ khóa".
    /// </summary>
    /// <param name="word">Một từ khóa đã chuẩn hóa.</param>
    /// <returns>Ví dụ "m_8" → "%m\_8%".</returns>
    public static string Contains(string word) =>
        "%" + word.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal) + "%";
}
