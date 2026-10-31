namespace Proc.Domain.Common;

/// <summary>
/// Đọc số tiền VND bằng chữ cho dòng "Số tiền bằng chữ" trên phiếu in (design.md mục 10.10), theo cách đọc thông dụng trên chứng từ kế toán:
/// nhóm ba chữ số với nghìn / triệu / tỷ; hàng trăm bằng 0 ở nhóm không đứng đầu đọc "không trăm"; hàng chục bằng 0 đọc "lẻ";
/// "mười", "mươi", "mốt" (21, 31…), "lăm" (15, 25…).
/// Ví dụ 1.005.000 → "Một triệu không trăm lẻ năm nghìn đồng"; 2.000.015 → "Hai triệu không trăm mười lăm đồng".
/// </summary>
public static class VietnameseNumberText
{
    private static readonly string[] Digits = ["không", "một", "hai", "ba", "bốn", "năm", "sáu", "bảy", "tám", "chín"];

    /// <summary>
    /// Đọc số tiền VND bằng chữ, làm tròn đến đồng (tiền VND không có số lẻ – CLAUDE.md mục 5), viết hoa chữ đầu.
    /// </summary>
    /// <param name="amount">Số tiền.</param>
    /// <returns>Ví dụ "Một triệu hai trăm năm mươi nghìn đồng".</returns>
    public static string ToVndWords(decimal amount)
    {
        var rounded = Math.Round(amount, 0, MidpointRounding.AwayFromZero);
        var words = rounded == 0 ? "không" : Read((ulong)Math.Abs(rounded));
        if (rounded < 0) words = "âm " + words;
        return char.ToUpperInvariant(words[0]) + words[1..] + " đồng";
    }

    /// <summary>
    /// Đọc số nguyên dương. Phần từ một tỷ trở lên đọc đệ quy rồi thêm "tỷ" (1.000 tỷ = "một nghìn tỷ"); phần dưới tỷ đọc theo nhóm.
    /// </summary>
    private static string Read(ulong number)
    {
        const ulong billion = 1_000_000_000;
        if (number >= billion)
        {
            var head = Read(number / billion) + " tỷ";
            var rest = number % billion;
            return rest == 0 ? head : head + " " + ReadBelowBillion(rest, full: true);
        }

        return ReadBelowBillion(number, full: false);
    }

    /// <summary>Đọc số dưới một tỷ: nhóm triệu, nghìn, đơn vị; nhóm bằng 0 bỏ qua.</summary>
    /// <param name="number">Số (&lt; 1 tỷ).</param>
    /// <param name="full">Đứng sau phần lớn hơn (tỷ) – nhóm đầu cũng đọc đủ "không trăm", "lẻ".</param>
    private static string ReadBelowBillion(ulong number, bool full)
    {
        var groups = new[] { (int)(number / 1_000_000), (int)(number / 1_000 % 1_000), (int)(number % 1_000) };
        var units = new[] { "triệu", "nghìn", "" };
        var parts = new List<string>();
        var started = full;
        for (var i = 0; i < groups.Length; i++)
        {
            if (groups[i] == 0) continue;
            var text = ReadGroup(groups[i], started);
            parts.Add(units[i].Length == 0 ? text : $"{text} {units[i]}");
            started = true;
        }

        return string.Join(' ', parts);
    }

    /// <summary>
    /// Đọc một nhóm ba chữ số (1–999).
    /// </summary>
    /// <param name="group">Giá trị nhóm.</param>
    /// <param name="full">Không phải nhóm đứng đầu: hàng trăm bằng 0 vẫn đọc "không trăm".</param>
    private static string ReadGroup(int group, bool full)
    {
        var (hundreds, tens, ones) = (group / 100, group / 10 % 10, group % 10);
        var words = new List<string>();
        if (full || hundreds > 0) words.Add($"{Digits[hundreds]} trăm");

        if (tens == 0)
        {
            // 105 → "một trăm lẻ năm"; nhóm đầu 5 → "năm" (không có "lẻ").
            if (ones > 0 && words.Count > 0) words.Add("lẻ");
        }
        else
        {
            words.Add(tens == 1 ? "mười" : $"{Digits[tens]} mươi");
        }

        if (ones > 0)
        {
            words.Add(ones switch
            {
                1 when tens > 1 => "mốt",
                5 when tens > 0 => "lăm",
                _ => Digits[ones],
            });
        }

        return string.Join(' ', words);
    }
}
