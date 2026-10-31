using System.Globalization;
using Proc.Domain.Common;

namespace Proc.Domain.MasterData;

/// <summary>Danh mục được đánh số tự động và quy tắc mã của từng danh mục (design.md mục 5.11).</summary>
public static class NumberedCatalogs
{
    /// <summary>Nhà cung cấp.</summary>
    public const string Supplier = "supplier";

    /// <summary>Nhóm nhà cung cấp.</summary>
    public const string SupplierGroup = "supplier-group";

    /// <summary>Nhóm hàng.</summary>
    public const string ItemCategory = "item-category";

    /// <summary>Hàng hóa (dải theo từng nhóm hàng, hoặc dòng mặc định cho mọi nhóm).</summary>
    public const string Item = "item";

    /// <summary>Ký hiệu trong tiền tố mã hàng, thay bằng mã nhóm hàng, ví dụ "{NHOM}-" → "VT-".</summary>
    public const string CategoryToken = "{NHOM}";

    /// <summary>Quy tắc mã của danh mục: tên hiển thị, chỉ chữ số (không - . _ /), độ dài tối đa – khớp quy tắc mã của entity.</summary>
    /// <param name="Name">Tên danh mục.</param>
    /// <param name="AlphanumericOnly">Mã chỉ gồm chữ không dấu và số.</param>
    /// <param name="MaxLength">Độ dài mã tối đa.</param>
    public sealed record Rule(string Name, bool AlphanumericOnly, int MaxLength);

    /// <summary>Các danh mục đánh số tự động.</summary>
    public static readonly IReadOnlyDictionary<string, Rule> All = new Dictionary<string, Rule>
    {
        [Supplier] = new("Nhà cung cấp", false, MasterData.Supplier.CodeMaxLength),
        [SupplierGroup] = new("Nhóm nhà cung cấp", true, 20),
        [ItemCategory] = new("Nhóm hàng", true, 20),
        [Item] = new("Hàng hóa", false, MasterData.Item.CodeMaxLength),
    };
}

/// <summary>
/// Dải đánh số mã một danh mục (design.md mục 5.11): mã = tiền tố + số thứ tự đủ <see cref="Digits"/> chữ số, trong khoảng
/// <see cref="FromNumber"/>–<see cref="ToNumber"/>. Ví dụ tiền tố NCC, 5 chữ số, 1–99999 → NCC00001, NCC00002…
/// Hàng hóa cấu hình riêng từng nhóm hàng, hoặc một dòng mặc định (<see cref="CategoryId"/> null) dùng ký hiệu {NHOM} trong tiền tố.
/// Dùng chung toàn tập đoàn. Người dùng không tự đặt mã khi thêm mới bản ghi của danh mục đánh số.
/// </summary>
[Auditable("Đánh số danh mục", LabelProperty = nameof(Prefix))]
public sealed class CatalogNumbering : AuditableEntity
{
    /// <summary>Độ dài tối đa của tiền tố (kể cả {NHOM}).</summary>
    public const int PrefixMaxLength = 20;

    /// <summary>Số chữ số tối đa của phần số thứ tự.</summary>
    public const int MaxDigits = 10;

    /// <summary>Độ dài mã nhóm hàng tối đa – dùng để kiểm tra mã hàng sinh ra từ {NHOM} không vượt độ dài cho phép.</summary>
    private const int CategoryCodeMaxLength = 20;

    private CatalogNumbering()
    {
    }

    /// <summary>Danh mục (<see cref="NumberedCatalogs"/>).</summary>
    public string Catalog { get; private set; } = string.Empty;

    /// <summary>Nhóm hàng của dải mã hàng; null = dòng mặc định (hoặc danh mục khác hàng hóa).</summary>
    public Guid? CategoryId { get; private set; }

    /// <summary>Tiền tố, viết hoa; có thể rỗng.</summary>
    public string Prefix { get; private set; } = string.Empty;

    /// <summary>Số chữ số của phần số thứ tự (thêm số 0 bên trái).</summary>
    public int Digits { get; private set; }

    /// <summary>Số đầu dải.</summary>
    public long FromNumber { get; private set; }

    /// <summary>Số cuối dải.</summary>
    public long ToNumber { get; private set; }

    /// <summary>
    /// Tạo dải đánh số.
    /// </summary>
    /// <param name="catalog">Danh mục.</param>
    /// <param name="categoryId">Nhóm hàng (chỉ với hàng hóa; null = mặc định).</param>
    /// <param name="prefix">Tiền tố.</param>
    /// <param name="digits">Số chữ số.</param>
    /// <param name="fromNumber">Số đầu dải.</param>
    /// <param name="toNumber">Số cuối dải.</param>
    /// <returns>Dải mới.</returns>
    /// <exception cref="BusinessRuleException">Khi cấu hình không hợp lệ.</exception>
    public static CatalogNumbering Create(string catalog, Guid? categoryId, string prefix, int digits, long fromNumber, long toNumber)
    {
        if (!NumberedCatalogs.All.ContainsKey(catalog)) throw new BusinessRuleException("Danh mục không đánh số tự động được.");
        if (categoryId is not null && catalog != NumberedCatalogs.Item)
            throw new BusinessRuleException("Chỉ dải mã hàng hóa mới chọn theo nhóm hàng.");
        var numbering = new CatalogNumbering { Catalog = catalog, CategoryId = categoryId };
        numbering.Update(prefix, digits, fromNumber, toNumber);
        return numbering;
    }

    /// <summary>
    /// Sửa tiền tố, số chữ số, dải số. Mã đã cấp không đổi; mã mới theo cấu hình mới.
    /// </summary>
    /// <param name="prefix">Tiền tố.</param>
    /// <param name="digits">Số chữ số.</param>
    /// <param name="fromNumber">Số đầu dải.</param>
    /// <param name="toNumber">Số cuối dải.</param>
    /// <exception cref="BusinessRuleException">Khi cấu hình không hợp lệ.</exception>
    public void Update(string prefix, int digits, long fromNumber, long toNumber)
    {
        var rule = NumberedCatalogs.All[Catalog];
        // Viết hoa toàn bộ – {nhom} gõ chữ thường cũng thành {NHOM}.
        var normalized = CodeRules.Normalize(prefix);
        if (normalized.Length > PrefixMaxLength) throw new BusinessRuleException($"Tiền tố dài tối đa {PrefixMaxLength} ký tự.");
        var hasToken = normalized.Contains(NumberedCatalogs.CategoryToken, StringComparison.Ordinal);
        if (hasToken && Catalog != NumberedCatalogs.Item) throw new BusinessRuleException("Chỉ tiền tố mã hàng hóa mới dùng được {NHOM}.");
        var literal = normalized.Replace(NumberedCatalogs.CategoryToken, string.Empty, StringComparison.Ordinal);
        var allowed = rule.AlphanumericOnly
            ? literal.All(c => c is >= 'A' and <= 'Z' or >= '0' and <= '9')
            : literal.All(c => c is >= 'A' and <= 'Z' or >= '0' and <= '9' or '-' or '.' or '_' or '/');
        if (!allowed)
            throw new BusinessRuleException(rule.AlphanumericOnly
                ? $"Tiền tố mã {rule.Name.ToLowerInvariant()} chỉ gồm chữ không dấu (A–Z) và số."
                : $"Tiền tố mã {rule.Name.ToLowerInvariant()} chỉ gồm chữ không dấu (A–Z), số và - . _ /.");
        if (digits is < 1 or > MaxDigits) throw new BusinessRuleException($"Số chữ số từ 1 đến {MaxDigits}.");
        var capacity = (long)Math.Pow(10, digits) - 1;
        if (fromNumber < 1 || toNumber < fromNumber || toNumber > capacity)
            throw new BusinessRuleException(
                $"Dải số phải từ 1, \"đến số\" không nhỏ hơn \"từ số\" và không vượt {capacity.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"))} ({digits} chữ số).");
        // Mã dài nhất có thể sinh ra (mã nhóm hàng dài nhất khi dùng {NHOM}) phải nằm trong độ dài mã của danh mục.
        var longest = literal.Length + (hasToken ? CategoryCodeMaxLength : 0) + digits;
        if (longest > rule.MaxLength)
            throw new BusinessRuleException($"Mã sinh ra có thể dài {longest} ký tự, vượt độ dài mã {rule.Name.ToLowerInvariant()} ({rule.MaxLength} ký tự) – rút ngắn tiền tố hoặc số chữ số.");
        if (literal.Length + digits < 2) throw new BusinessRuleException("Mã sinh ra phải dài ít nhất 2 ký tự.");
        Prefix = normalized;
        Digits = digits;
        FromNumber = fromNumber;
        ToNumber = toNumber;
    }

    /// <summary>
    /// Tiền tố thật sau khi thay {NHOM} bằng mã nhóm hàng.
    /// </summary>
    /// <param name="categoryCode">Mã nhóm hàng (bắt buộc khi tiền tố có {NHOM}).</param>
    /// <returns>Tiền tố, ví dụ "VT-".</returns>
    public string ResolvePrefix(string? categoryCode) =>
        Prefix.Contains(NumberedCatalogs.CategoryToken, StringComparison.Ordinal)
            ? Prefix.Replace(NumberedCatalogs.CategoryToken, categoryCode ?? throw new BusinessRuleException("Thiếu mã nhóm hàng để sinh mã hàng."), StringComparison.Ordinal)
            : Prefix;

    /// <summary>
    /// Mã kế tiếp: số lớn nhất đang có trong dải (mã khớp đúng tiền tố + <see cref="Digits"/> chữ số) cộng 1; chưa có thì là số đầu dải.
    /// Ví dụ dải NCC 5 chữ số 1–99999, đã có NCC00007 và NCC00012 → NCC00013; mã NCC1 hay NCCX0001 không tính.
    /// </summary>
    /// <param name="resolvedPrefix">Tiền tố thật (<see cref="ResolvePrefix"/>).</param>
    /// <param name="existingCodes">Các mã đang có bắt đầu bằng tiền tố.</param>
    /// <returns>Mã mới.</returns>
    /// <exception cref="BusinessRuleException">Khi hết dải số.</exception>
    public string NextCode(string resolvedPrefix, IEnumerable<string> existingCodes)
    {
        var max = existingCodes
            .Where(c => c.Length == resolvedPrefix.Length + Digits && c.StartsWith(resolvedPrefix, StringComparison.Ordinal))
            .Select(c => long.TryParse(c.AsSpan(resolvedPrefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : -1)
            .Where(n => n >= FromNumber && n <= ToNumber)
            .DefaultIfEmpty(FromNumber - 1)
            .Max();
        var next = max + 1;
        if (next > ToNumber)
            throw new BusinessRuleException(
                $"Hết dải mã {NumberedCatalogs.All[Catalog].Name.ToLowerInvariant()} ({resolvedPrefix}{FromNumber.ToString(new string('0', Digits), CultureInfo.InvariantCulture)}–{resolvedPrefix}{ToNumber.ToString(new string('0', Digits), CultureInfo.InvariantCulture)}) – mở rộng dải ở Hệ thống → Đánh số danh mục.");
        return resolvedPrefix + next.ToString(new string('0', Digits), CultureInfo.InvariantCulture);
    }
}
