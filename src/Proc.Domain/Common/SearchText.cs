using System.Globalization;
using System.Text;

namespace Proc.Domain.Common;

/// <summary>
/// Chuẩn hóa chữ tiếng Việt để tìm kiếm: bỏ dấu, đ → d, chữ thường, gộp khoảng trắng (CLAUDE.md mục 5: gõ có dấu hay không
/// dấu đều tìm được). Danh mục lớn (hàng hóa ~100.000 mã) lưu sẵn chuỗi này vào cột SearchText để tìm ở CSDL.
/// Ví dụ: "Bu lông  M8 – Đài Loan" → "bu long m8 – dai loan". Giống hàm normalizeVi của giao diện (utils/text.ts).
/// </summary>
public static class SearchText
{
    /// <summary>
    /// Chuẩn hóa một chuỗi.
    /// </summary>
    /// <param name="value">Chuỗi gốc; null coi như rỗng.</param>
    /// <returns>Chuỗi không dấu, chữ thường.</returns>
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var decomposed = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        var lastWasSpace = false;
        foreach (var ch in decomposed)
        {
            // Sau khi tách (FormD), dấu thanh và dấu mũ là ký tự NonSpacingMark riêng – bỏ đi. Chữ đ không tách được nên đổi tay.
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
            var c = ch is 'đ' or 'Đ' ? 'd' : char.ToLowerInvariant(ch);
            if (char.IsWhiteSpace(c))
            {
                if (!lastWasSpace && builder.Length > 0) builder.Append(' ');
                lastWasSpace = true;
                continue;
            }

            builder.Append(c);
            lastWasSpace = false;
        }

        return builder.ToString().TrimEnd();
    }

    /// <summary>
    /// Ghép và chuẩn hóa nhiều trường (mã, tên, tên tiếng Anh…) thành một chuỗi tìm kiếm.
    /// </summary>
    /// <param name="parts">Các trường; trường trống bị bỏ qua.</param>
    /// <returns>Chuỗi tìm kiếm.</returns>
    public static string Combine(params string?[] parts) =>
        Normalize(string.Join(' ', parts.Where(p => !string.IsNullOrWhiteSpace(p))));
}
