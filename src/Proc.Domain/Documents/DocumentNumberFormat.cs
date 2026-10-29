using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Proc.Domain.Common;

namespace Proc.Domain.Documents;

/// <summary>
/// Mẫu số chứng từ (design.md mục 5.4), ví dụ <c>PO-{CTY}-{YYMM}-{0000}</c> → PO-BHA-2610-0001.
/// <list type="table">
/// <item><term>{CTY}</term><description>mã công ty</description></item>
/// <item><term>{YYYY} / {YY}</term><description>năm của ngày chứng từ (4 / 2 chữ số)</description></item>
/// <item><term>{MM}</term><description>tháng 2 chữ số</description></item>
/// <item><term>{YYMM}</term><description>năm 2 chữ số + tháng, ví dụ 2610</description></item>
/// <item><term>{000…}</term><description>số thứ tự, đệm số 0 đủ số chữ số (3–10)</description></item>
/// </list>
/// Phần còn lại là chữ cố định (A–Z, 0–9, - / . _). Số thứ tự đếm lại theo <b>kỳ</b> suy từ mẫu:
/// có tháng → mỗi tháng, chỉ có năm → mỗi năm, không có ngày → đếm liên tục.
/// </summary>
public sealed partial class DocumentNumberFormat
{
    /// <summary>Độ dài tối đa của mẫu – số sinh ra phải vừa cột số chứng từ (50 ký tự).</summary>
    public const int MaxPatternLength = 40;

    private const string Company = "CTY";
    private const string Year4 = "YYYY";
    private const string Year2 = "YY";
    private const string Month = "MM";
    private const string YearMonth = "YYMM";

    private readonly IReadOnlyList<Part> _parts;

    private DocumentNumberFormat(string pattern, IReadOnlyList<Part> parts)
    {
        Pattern = pattern;
        _parts = parts;
    }

    /// <summary>Mẫu đã chuẩn hóa (chữ hoa).</summary>
    public string Pattern { get; }

    private bool HasMonth => _parts.Any(p => p.Token is Month or YearMonth);

    private bool HasYear => _parts.Any(p => p.Token is Year4 or Year2 or YearMonth);

    /// <summary>
    /// Mẫu mặc định khi công ty chưa cấu hình: {LOẠI}-{CTY}-{YYMM}-{0000}, ví dụ PO-BHA-2610-0001.
    /// </summary>
    /// <param name="docType">Mã loại chứng từ.</param>
    /// <returns>Mẫu mặc định.</returns>
    public static DocumentNumberFormat Default(string docType) => Parse($"{docType}-{{CTY}}-{{YYMM}}-{{0000}}");

    /// <summary>
    /// Phân tích và kiểm tra mẫu.
    /// </summary>
    /// <param name="pattern">Mẫu người dùng nhập; được chuẩn hóa chữ hoa, bỏ khoảng trắng đầu/cuối.</param>
    /// <returns>Mẫu hợp lệ.</returns>
    /// <exception cref="BusinessRuleException">Khi mẫu trống, quá dài, có ký tự hoặc mã không hợp lệ,
    /// không có đúng một nhóm số, hoặc có tháng mà không có năm.</exception>
    public static DocumentNumberFormat Parse(string? pattern)
    {
        var normalized = (pattern ?? string.Empty).Trim().ToUpperInvariant();
        if (normalized.Length == 0) throw new BusinessRuleException("Mẫu số chứng từ không được để trống.");
        if (normalized.Length > MaxPatternLength)
            throw new BusinessRuleException($"Mẫu số chứng từ dài tối đa {MaxPatternLength} ký tự.");

        var parts = new List<Part>();
        var position = 0;
        foreach (Match match in TokenRegex().Matches(normalized))
        {
            AddLiteral(parts, normalized[position..match.Index]);
            parts.Add(ParseToken(match.Groups[1].Value));
            position = match.Index + match.Length;
        }
        AddLiteral(parts, normalized[position..]);

        var numberCount = parts.Count(p => p.Digits > 0);
        if (numberCount != 1)
            throw new BusinessRuleException("Mẫu số chứng từ phải có đúng một nhóm số thứ tự, ví dụ {0000}.");

        var format = new DocumentNumberFormat(normalized, parts);
        // Có tháng mà không có năm thì tháng 10/2026 và 10/2027 sinh cùng số – trùng số chứng từ.
        if (format.HasMonth && !format.HasYear)
            throw new BusinessRuleException("Mẫu có tháng ({MM}) thì phải có năm ({YYYY} hoặc {YY}) để số không lặp lại giữa các năm.");
        return format;
    }

    /// <summary>
    /// Kỳ đếm số của một ngày chứng từ: "yyyyMM" nếu mẫu có tháng, "yyyy" nếu chỉ có năm, rỗng nếu đếm liên tục.
    /// Ví dụ mẫu PO-{CTY}-{YYMM}-{0000}, ngày 29/10/2026 → "202610".
    /// </summary>
    /// <param name="documentDate">Ngày chứng từ.</param>
    /// <returns>Mã kỳ.</returns>
    public string PeriodKey(DateOnly documentDate) =>
        HasMonth ? documentDate.ToString("yyyyMM", CultureInfo.InvariantCulture)
        : HasYear ? documentDate.ToString("yyyy", CultureInfo.InvariantCulture)
        : string.Empty;

    /// <summary>
    /// Sinh số chứng từ.
    /// </summary>
    /// <param name="companyCode">Mã công ty.</param>
    /// <param name="documentDate">Ngày chứng từ.</param>
    /// <param name="number">Số thứ tự trong kỳ (từ 1).</param>
    /// <returns>Số chứng từ, ví dụ PO-BHA-2610-0001. Số thứ tự vượt số chữ số thì nới rộng (PO-10000), không cắt bớt.</returns>
    public string Render(string companyCode, DateOnly documentDate, long number)
    {
        var result = new StringBuilder();
        foreach (var part in _parts)
        {
            result.Append(part switch
            {
                { Literal: { } literal } => literal,
                { Digits: > 0 } => number.ToString(new string('0', part.Digits), CultureInfo.InvariantCulture),
                { Token: Company } => companyCode,
                { Token: Year4 } => documentDate.ToString("yyyy", CultureInfo.InvariantCulture),
                { Token: Year2 } => documentDate.ToString("yy", CultureInfo.InvariantCulture),
                { Token: Month } => documentDate.ToString("MM", CultureInfo.InvariantCulture),
                _ => documentDate.ToString("yyMM", CultureInfo.InvariantCulture),
            });
        }

        return result.ToString();
    }

    private static void AddLiteral(List<Part> parts, string literal)
    {
        if (literal.Length == 0) return;
        if (!LiteralRegex().IsMatch(literal))
        {
            throw new BusinessRuleException(
                $"Mẫu số chứng từ có ký tự không hợp lệ trong \"{literal}\": chỉ dùng chữ A–Z không dấu, số và - / . _ ngoài các mã {{...}}.");
        }
        parts.Add(new Part(literal, null, 0));
    }

    private static Part ParseToken(string token)
    {
        if (token is Company or Year4 or Year2 or Month or YearMonth) return new Part(null, token, 0);
        if (token.Length > 0 && token.All(c => c == '0'))
        {
            if (token.Length is < 3 or > 10)
                throw new BusinessRuleException("Nhóm số thứ tự phải có 3–10 chữ số 0, ví dụ {0000}.");
            return new Part(null, null, token.Length);
        }

        throw new BusinessRuleException(
            $"Mẫu số chứng từ có mã không hợp lệ {{{token}}}. Chỉ dùng {{CTY}}, {{YYYY}}, {{YY}}, {{MM}}, {{YYMM}}, {{0000}}.");
    }

    /// <summary>Một phần của mẫu: chữ cố định, mã thời gian/công ty, hoặc nhóm số (Digits &gt; 0).</summary>
    private sealed record Part(string? Literal, string? Token, int Digits);

    [GeneratedRegex(@"\{([^{}]*)\}")]
    private static partial Regex TokenRegex();

    [GeneratedRegex(@"^[A-Z0-9\-/._]+$")]
    private static partial Regex LiteralRegex();
}
