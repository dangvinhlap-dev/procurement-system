using Proc.Domain.Common;

namespace Proc.Domain.Analysis;

/// <summary>
/// Khóa nhận diện mặt hàng, NCC "cũ" (theo cách ghi của từng pháp nhân) trên dữ liệu mua lịch sử – dùng chung cho gom nhóm, ánh xạ và
/// tự áp ánh xạ khi nhập (design.md mục 18.2).
/// </summary>
public static class PurchaseHistoryKeys
{
    /// <summary>Tiền tố khóa NCC theo mã số thuế.</summary>
    public const string TaxCodePrefix = "MST:";

    /// <summary>Tiền tố khóa NCC theo tên (dòng không có MST).</summary>
    public const string NamePrefix = "TEN:";

    /// <summary>Độ dài tối đa của khóa NCC: tiền tố + tên NCC (300).</summary>
    public const int SupplierKeyMaxLength = 310;

    /// <summary>
    /// Khóa mã vật tư cũ: viết hoa, bỏ khoảng trắng hai đầu – đúng như mã đã lưu trên dòng mua lịch sử.
    /// </summary>
    /// <param name="itemCode">Mã vật tư theo pháp nhân.</param>
    /// <returns>Khóa; rỗng khi mã trống.</returns>
    public static string Item(string? itemCode) => CodeRules.Normalize(itemCode);

    /// <summary>
    /// Khóa NCC cũ: có MST thì "MST:0101234567" (MST là định danh pháp lý, tên NCC mỗi nơi ghi một kiểu); không có MST thì
    /// "TEN:" + tên không dấu chữ thường, để "Công ty THÉP Việt" và "cong ty thep viet" là một NCC.
    /// </summary>
    /// <param name="taxCode">MST NCC trên dòng.</param>
    /// <param name="name">Tên NCC trên dòng.</param>
    /// <returns>Khóa NCC.</returns>
    public static string Supplier(string? taxCode, string? name) =>
        string.IsNullOrWhiteSpace(taxCode)
            ? NamePrefix + SearchText.Normalize(name)
            : TaxCodePrefix + taxCode.Trim().ToUpperInvariant();

    /// <summary>
    /// MST trong khóa NCC.
    /// </summary>
    /// <param name="supplierKey">Khóa NCC.</param>
    /// <returns>MST; null khi khóa theo tên.</returns>
    public static string? TaxCodeOf(string supplierKey) =>
        supplierKey.StartsWith(TaxCodePrefix, StringComparison.Ordinal) ? supplierKey[TaxCodePrefix.Length..] : null;

    /// <summary>
    /// Khóa NCC có đúng dạng không (bắt đầu bằng MST: hoặc TEN: và có nội dung).
    /// </summary>
    /// <param name="supplierKey">Khóa cần kiểm tra.</param>
    /// <returns>true khi đúng dạng.</returns>
    public static bool IsValidSupplierKey(string? supplierKey) =>
        supplierKey is not null
        && supplierKey.Length <= SupplierKeyMaxLength
        && ((supplierKey.StartsWith(TaxCodePrefix, StringComparison.Ordinal) && supplierKey.Length > TaxCodePrefix.Length)
            || (supplierKey.StartsWith(NamePrefix, StringComparison.Ordinal) && supplierKey.Length > NamePrefix.Length));
}

/// <summary>
/// Điểm giống nhau của hai tên (0–100) để gợi ý mã chung cho mã cũ (design.md mục 18.2). Tách từ trên chữ không dấu, so tập từ theo
/// hệ số Dice: điểm = 2 × số từ trùng / (số từ A + số từ B) × 100, làm tròn.
/// Ví dụ: "Bu lông M8" {bu, long, m8} và "Bu-lông M10" {bu, long, m10} trùng 2 từ → 2×2 / 6 = 66,7 → 67.
/// Dice đơn giản, không phụ thuộc thứ tự từ ("Dầu thủy lực 68" ≈ "Thủy lực dầu 68") – đủ để xếp hạng gợi ý; người dùng luôn chọn lại.
/// </summary>
public static class NameSimilarity
{
    /// <summary>
    /// Từ chỉ loại hình doanh nghiệp – bỏ khi so tên NCC, nếu không mọi "Công ty TNHH …" đều giống nhau.
    /// </summary>
    public static readonly IReadOnlySet<string> CompanyWords = new HashSet<string>(StringComparer.Ordinal)
    {
        "cong", "ty", "tnhh", "cp", "co", "phan", "mtv", "jsc", "ltd", "company", "corp", "chi", "nhanh", "dntn", "htx",
    };

    /// <summary>
    /// Điểm giống nhau của hai tên.
    /// </summary>
    /// <param name="a">Tên thứ nhất (có dấu hay không đều được).</param>
    /// <param name="b">Tên thứ hai.</param>
    /// <param name="ignoredWords">Từ bỏ qua (đã không dấu, chữ thường), ví dụ <see cref="CompanyWords"/>; null = không bỏ từ nào.</param>
    /// <returns>0 (không trùng từ nào) đến 100 (cùng tập từ).</returns>
    public static int Score(string? a, string? b, IReadOnlySet<string>? ignoredWords = null) =>
        Score(Words(a, ignoredWords), Words(b, ignoredWords));

    /// <summary>
    /// Điểm giống nhau của hai tập từ đã tách sẵn – dùng khi so một tên với hàng nghìn mặt hàng, khỏi tách lại tên nguồn mỗi lần.
    /// </summary>
    /// <param name="a">Tập từ thứ nhất (từ <see cref="Words"/>).</param>
    /// <param name="b">Tập từ thứ hai.</param>
    /// <returns>0–100.</returns>
    public static int Score(IReadOnlySet<string> a, IReadOnlySet<string> b)
    {
        if (a.Count == 0 || b.Count == 0) return 0;
        var common = a.Count(b.Contains);
        return (int)Math.Round(200m * common / (a.Count + b.Count), MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Tách tên thành tập từ không dấu, chữ thường; ký tự không phải chữ, số (khoảng trắng, "-", "/", ",") là chỗ ngắt từ.
    /// </summary>
    /// <param name="text">Tên.</param>
    /// <param name="ignoredWords">Từ bỏ qua; null = không bỏ.</param>
    /// <returns>Tập từ.</returns>
    public static IReadOnlySet<string> Words(string? text, IReadOnlySet<string>? ignoredWords = null)
    {
        var words = new HashSet<string>(StringComparer.Ordinal);
        var normalized = SearchText.Normalize(text);
        var start = -1;
        for (var i = 0; i <= normalized.Length; i++)
        {
            var isWordChar = i < normalized.Length && char.IsLetterOrDigit(normalized[i]);
            if (isWordChar && start < 0) start = i;
            if (isWordChar || start < 0) continue;
            var word = normalized[start..i];
            if (ignoredWords is null || !ignoredWords.Contains(word)) words.Add(word);
            start = -1;
        }

        return words;
    }
}

/// <summary>
/// Ánh xạ mã vật tư cũ của một pháp nhân → mã hàng hóa chung (design.md mục 18.2, bước B3 đề xuất Ban Cung ứng: gán một mã cho hàng
/// tương đương). Lưu ánh xạ thì mọi dòng mua lịch sử cùng mã cũ được gán mặt hàng; lần nhập sau tự áp. Ghi nhật ký.
/// </summary>
[Auditable("Ánh xạ mã vật tư cũ", LabelProperty = nameof(SourceCode))]
public sealed class PurchaseHistoryItemMapping : AuditableEntity, ICompanyOwned
{
    private PurchaseHistoryItemMapping()
    {
    }

    /// <summary>Pháp nhân có mã cũ (mỗi pháp nhân tự đặt mã riêng nên cùng mã ở hai nơi có thể là hai hàng khác nhau).</summary>
    public Guid CompanyId { get; private set; }

    /// <summary>Mã vật tư cũ, theo <see cref="PurchaseHistoryKeys.Item"/>.</summary>
    public string SourceCode { get; private set; } = string.Empty;

    /// <summary>Mặt hàng chung.</summary>
    public Guid ItemId { get; private set; }

    /// <summary>
    /// Tạo ánh xạ.
    /// </summary>
    /// <param name="companyId">Pháp nhân.</param>
    /// <param name="sourceCode">Mã vật tư cũ.</param>
    /// <param name="itemId">Mặt hàng chung.</param>
    /// <returns>Ánh xạ mới.</returns>
    /// <exception cref="BusinessRuleException">Khi mã cũ trống, dài quá hoặc chưa chọn mặt hàng.</exception>
    public static PurchaseHistoryItemMapping Create(Guid companyId, string sourceCode, Guid itemId)
    {
        var code = PurchaseHistoryKeys.Item(sourceCode);
        if (code.Length == 0) throw new BusinessRuleException("Thiếu mã vật tư cũ.");
        if (code.Length > PurchaseHistoryRecord.MaxLengths[nameof(PurchaseHistoryRecord.ItemCode)])
            throw new BusinessRuleException("Mã vật tư cũ dài quá.");
        if (itemId == Guid.Empty) throw new BusinessRuleException("Chưa chọn mặt hàng chung.");
        return new PurchaseHistoryItemMapping { CompanyId = companyId, SourceCode = code, ItemId = itemId };
    }

    /// <summary>
    /// Đổi mặt hàng chung (chọn nhầm).
    /// </summary>
    /// <param name="itemId">Mặt hàng mới.</param>
    /// <exception cref="BusinessRuleException">Khi chưa chọn mặt hàng.</exception>
    public void Retarget(Guid itemId)
    {
        if (itemId == Guid.Empty) throw new BusinessRuleException("Chưa chọn mặt hàng chung.");
        ItemId = itemId;
    }
}

/// <summary>
/// Ánh xạ NCC cũ của một pháp nhân (theo MST, hoặc theo tên khi không có MST) → NCC trong danh mục (design.md mục 18.2). Ghi nhật ký.
/// </summary>
[Auditable("Ánh xạ NCC cũ", LabelProperty = nameof(SourceKey))]
public sealed class PurchaseHistorySupplierMapping : AuditableEntity, ICompanyOwned
{
    private PurchaseHistorySupplierMapping()
    {
    }

    /// <summary>Pháp nhân.</summary>
    public Guid CompanyId { get; private set; }

    /// <summary>Khóa NCC cũ, theo <see cref="PurchaseHistoryKeys.Supplier"/> ("MST:…" hoặc "TEN:…").</summary>
    public string SourceKey { get; private set; } = string.Empty;

    /// <summary>NCC trong danh mục.</summary>
    public Guid SupplierId { get; private set; }

    /// <summary>
    /// Tạo ánh xạ.
    /// </summary>
    /// <param name="companyId">Pháp nhân.</param>
    /// <param name="sourceKey">Khóa NCC cũ.</param>
    /// <param name="supplierId">NCC trong danh mục.</param>
    /// <returns>Ánh xạ mới.</returns>
    /// <exception cref="BusinessRuleException">Khi khóa sai dạng hoặc chưa chọn NCC.</exception>
    public static PurchaseHistorySupplierMapping Create(Guid companyId, string sourceKey, Guid supplierId)
    {
        if (!PurchaseHistoryKeys.IsValidSupplierKey(sourceKey)) throw new BusinessRuleException("Khóa NCC cũ không đúng dạng.");
        if (supplierId == Guid.Empty) throw new BusinessRuleException("Chưa chọn NCC trong danh mục.");
        return new PurchaseHistorySupplierMapping { CompanyId = companyId, SourceKey = sourceKey, SupplierId = supplierId };
    }

    /// <summary>
    /// Đổi NCC trong danh mục (chọn nhầm).
    /// </summary>
    /// <param name="supplierId">NCC mới.</param>
    /// <exception cref="BusinessRuleException">Khi chưa chọn NCC.</exception>
    public void Retarget(Guid supplierId)
    {
        if (supplierId == Guid.Empty) throw new BusinessRuleException("Chưa chọn NCC trong danh mục.");
        SupplierId = supplierId;
    }
}
