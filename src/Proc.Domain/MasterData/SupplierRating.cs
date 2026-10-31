using Proc.Domain.Common;

namespace Proc.Domain.MasterData;

/// <summary>
/// Một hạng xếp loại nhà cung cấp do người dùng cấu hình (design.md mục 5.3, 11.3), dùng chung toàn tập đoàn. NCC đạt hạng khi tỷ lệ giao
/// đúng hạn ≥ <see cref="MinOnTimePct"/> và tỷ lệ trả hàng ≤ <see cref="MaxReturnPct"/>. Ngưỡng không code cứng – mỗi tập đoàn tự quy định.
/// </summary>
[Auditable("Hạng xếp loại NCC", LabelProperty = nameof(Code))]
public sealed class SupplierRatingRule : AuditableEntity
{
    /// <summary>Độ dài tối đa của mã hạng.</summary>
    public const int CodeMaxLength = 10;

    private SupplierRatingRule()
    {
    }

    /// <summary>Mã hạng, duy nhất, ví dụ A, B, C.</summary>
    public string Code { get; private set; } = string.Empty;

    /// <summary>Tên hạng, ví dụ "Tốt".</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Thứ tự xét: số nhỏ là hạng tốt hơn, xét trước.</summary>
    public int SortOrder { get; private set; }

    /// <summary>Tỷ lệ giao đúng hạn tối thiểu (%), 0–100 – numeric(5,2).</summary>
    public decimal MinOnTimePct { get; private set; }

    /// <summary>Tỷ lệ trả hàng tối đa (%), 0–100 – numeric(5,2).</summary>
    public decimal MaxReturnPct { get; private set; }

    /// <summary>
    /// Tạo hạng.
    /// </summary>
    /// <param name="code">Mã hạng (1–10 chữ không dấu hoặc số).</param>
    /// <param name="name">Tên hạng.</param>
    /// <param name="sortOrder">Thứ tự xét.</param>
    /// <param name="minOnTimePct">% đúng hạn tối thiểu.</param>
    /// <param name="maxReturnPct">% trả hàng tối đa.</param>
    /// <returns>Hạng mới.</returns>
    /// <exception cref="BusinessRuleException">Khi mã, tên hoặc ngưỡng không hợp lệ.</exception>
    public static SupplierRatingRule Create(string code, string name, int sortOrder, decimal minOnTimePct, decimal maxReturnPct)
    {
        var normalized = CodeRules.Normalize(code);
        if (!CodeRules.IsAlphanumeric(normalized, 1, CodeMaxLength))
            throw new BusinessRuleException($"Mã hạng gồm 1–{CodeMaxLength} ký tự chữ không dấu (A–Z) hoặc số.");
        var rule = new SupplierRatingRule { Code = normalized };
        rule.Update(name, sortOrder, minOnTimePct, maxReturnPct);
        return rule;
    }

    /// <summary>
    /// Sửa tên, thứ tự, ngưỡng (mã hạng không đổi).
    /// </summary>
    /// <param name="name">Tên hạng.</param>
    /// <param name="sortOrder">Thứ tự xét.</param>
    /// <param name="minOnTimePct">% đúng hạn tối thiểu.</param>
    /// <param name="maxReturnPct">% trả hàng tối đa.</param>
    /// <exception cref="BusinessRuleException">Khi tên rỗng hoặc ngưỡng ngoài 0–100.</exception>
    public void Update(string name, int sortOrder, decimal minOnTimePct, decimal maxReturnPct)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new BusinessRuleException("Hãy nhập tên hạng.");
        if (minOnTimePct is < 0 or > 100 || maxReturnPct is < 0 or > 100)
            throw new BusinessRuleException("Ngưỡng tỷ lệ phải từ 0 đến 100%.");
        Name = name.Trim();
        SortOrder = sortOrder;
        MinOnTimePct = Math.Round(minOnTimePct, 2, MidpointRounding.AwayFromZero);
        MaxReturnPct = Math.Round(maxReturnPct, 2, MidpointRounding.AwayFromZero);
    }
}

/// <summary>Xếp loại NCC theo các hạng đã cấu hình (design.md mục 11.3).</summary>
public static class SupplierRating
{
    /// <summary>Nhãn khi NCC không đạt hạng nào.</summary>
    public const string Failed = "Không đạt";

    /// <summary>
    /// Kiểm tra cả bộ hạng sau khi thêm/sửa/xóa: mã và thứ tự không trùng; hạng xét sau không được khắt khe hơn hạng xét trước
    /// (đúng hạn tối thiểu không tăng, trả hàng tối đa không giảm) – nếu không, hạng sau không bao giờ đạt được.
    /// Ví dụ A (95%, 1%), B (85%, 3%), C (70%, 5%) hợp lệ; B (98%, 3%) sau A (95%, 1%) bị chặn.
    /// </summary>
    /// <param name="rules">Mọi hạng.</param>
    /// <exception cref="BusinessRuleException">Khi bộ hạng không hợp lệ.</exception>
    public static void ValidateSet(IEnumerable<SupplierRatingRule> rules)
    {
        var list = rules.ToList();
        if (list.GroupBy(r => r.Code).FirstOrDefault(g => g.Count() > 1) is { } code)
            throw new BusinessRuleException($"Mã hạng {code.Key} bị trùng.");
        if (list.GroupBy(r => r.SortOrder).FirstOrDefault(g => g.Count() > 1) is { } order)
            throw new BusinessRuleException($"Thứ tự {order.Key} bị trùng giữa các hạng {string.Join(", ", order.Select(r => r.Code))}.");
        var sorted = list.OrderBy(r => r.SortOrder).ToList();
        for (var i = 1; i < sorted.Count; i++)
        {
            var (better, worse) = (sorted[i - 1], sorted[i]);
            if (worse.MinOnTimePct > better.MinOnTimePct || worse.MaxReturnPct < better.MaxReturnPct)
                throw new BusinessRuleException(
                    $"Hạng {worse.Code} xét sau hạng {better.Code} nên ngưỡng không được khắt khe hơn: đúng hạn tối thiểu ≤ {better.MinOnTimePct}%, " +
                    $"trả hàng tối đa ≥ {better.MaxReturnPct}%.");
        }
    }

    /// <summary>
    /// Xếp loại một NCC: xét từ hạng tốt nhất (thứ tự nhỏ nhất), hạng đầu tiên đạt cả hai ngưỡng là hạng của NCC. Chỉ số không có dữ liệu
    /// (null – ví dụ không có dòng nhập theo PO có hẹn ngày) thì không xét chỉ số đó.
    /// Ví dụ đúng hạn 90%, trả 2%, với A (95%, 1%), B (85%, 3%): không đạt A, đạt B → "B".
    /// </summary>
    /// <param name="rules">Các hạng đã cấu hình.</param>
    /// <param name="onTimePct">% đúng hạn, null nếu không có dữ liệu.</param>
    /// <param name="returnPct">% trả hàng, null nếu không có dữ liệu.</param>
    /// <returns>Mã hạng; <see cref="Failed"/> nếu không đạt hạng nào; null khi chưa cấu hình hạng hoặc cả hai chỉ số đều không có dữ liệu.</returns>
    public static string? Evaluate(IEnumerable<SupplierRatingRule> rules, decimal? onTimePct, decimal? returnPct)
    {
        var sorted = rules.OrderBy(r => r.SortOrder).ToList();
        if (sorted.Count == 0 || (onTimePct is null && returnPct is null)) return null;
        var match = sorted.FirstOrDefault(r =>
            (onTimePct is not { } onTime || onTime >= r.MinOnTimePct) && (returnPct is not { } returned || returned <= r.MaxReturnPct));
        return match?.Code ?? Failed;
    }
}
