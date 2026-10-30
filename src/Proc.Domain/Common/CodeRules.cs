using System.Text.RegularExpressions;

namespace Proc.Domain.Common;

/// <summary>
/// Quy tắc chung cho cột Code của danh mục: bắt buộc, duy nhất, không xóa cứng (design.md mục 5).
/// </summary>
public static partial class CodeRules
{
    /// <summary>
    /// Chuẩn hóa mã: bỏ khoảng trắng đầu/cuối, đổi sang chữ hoa.
    /// </summary>
    /// <param name="code">Mã người dùng nhập.</param>
    /// <returns>Mã đã chuẩn hóa; chuỗi rỗng nếu đầu vào null.</returns>
    public static string Normalize(string? code) => (code ?? string.Empty).Trim().ToUpperInvariant();

    /// <summary>
    /// Kiểm tra mã chỉ gồm chữ A–Z không dấu và số, độ dài trong khoảng cho phép.
    /// </summary>
    /// <param name="code">Mã đã chuẩn hóa.</param>
    /// <param name="minLength">Độ dài tối thiểu.</param>
    /// <param name="maxLength">Độ dài tối đa.</param>
    /// <returns>true nếu hợp lệ.</returns>
    public static bool IsAlphanumeric(string code, int minLength, int maxLength) =>
        code.Length >= minLength && code.Length <= maxLength && AlphanumericRegex().IsMatch(code);

    /// <summary>
    /// Kiểm tra mã dài có dấu phân cách (mã hàng VT-0001, mã NCC NCC.HN/001): chữ A–Z không dấu, số và - . _ /,
    /// bắt đầu bằng chữ hoặc số, không có khoảng trắng để gõ nhanh.
    /// </summary>
    /// <param name="code">Mã đã chuẩn hóa.</param>
    /// <param name="minLength">Độ dài tối thiểu.</param>
    /// <param name="maxLength">Độ dài tối đa.</param>
    /// <returns>true nếu hợp lệ.</returns>
    public static bool IsExtendedCode(string code, int minLength, int maxLength) =>
        code.Length >= minLength && code.Length <= maxLength && ExtendedRegex().IsMatch(code);

    [GeneratedRegex("^[A-Z0-9]+$")]
    private static partial Regex AlphanumericRegex();

    [GeneratedRegex(@"^[A-Z0-9][A-Z0-9._/\-]*$")]
    private static partial Regex ExtendedRegex();
}
