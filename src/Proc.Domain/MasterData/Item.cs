using System.Text.RegularExpressions;
using Proc.Domain.Common;

namespace Proc.Domain.MasterData;

/// <summary>Loại hàng hóa – dịch vụ (design.md mục 5.2).</summary>
public enum ItemType
{
    /// <summary>Hàng tồn kho: loại duy nhất đi qua kho (nhập, xuất, tồn, giá vốn).</summary>
    Inventory = 1,

    /// <summary>Dịch vụ: mua theo PO nhưng không nhập kho.</summary>
    Service = 2,

    /// <summary>Tài sản: theo dõi ở phần mềm tài sản/kế toán, không nhập kho.</summary>
    Asset = 3,

    /// <summary>Vật tư tiêu hao: dùng ngay khi mua, không nhập kho.</summary>
    Consumable = 4,
}

/// <summary>Thuế suất GTGT mặc định của mặt hàng (design.md mục 1.3, câu 5; mục 5.4).</summary>
public static class VatRates
{
    /// <summary>Không chịu thuế.</summary>
    public const string NotSubject = "KCT";

    /// <summary>Các mã thuế suất hợp lệ: KCT, 0%, 5%, 8%, 10%. Danh mục thuế suất đầy đủ làm sau, giữ đúng các mã này.</summary>
    public static readonly IReadOnlyList<string> All = [NotSubject, "0", "5", "8", "10"];
}

/// <summary>Các trường mô tả của mặt hàng, gom lại để hàm sửa không có quá nhiều tham số.</summary>
/// <param name="Name">Tên hàng, bắt buộc.</param>
/// <param name="EnglishName">Tên tiếng Anh.</param>
/// <param name="Specification">Quy cách.</param>
/// <param name="Brand">Thương hiệu.</param>
/// <param name="Origin">Xuất xứ.</param>
/// <param name="Barcode">Mã vạch.</param>
/// <param name="VatCode">Thuế suất mặc định (<see cref="VatRates.All"/>); trống = chưa khai báo.</param>
/// <param name="TechnicalSpecs">Thông số kỹ thuật.</param>
public sealed record ItemDescription(
    string Name, string? EnglishName, string? Specification, string? Brand, string? Origin, string? Barcode,
    string? VatCode, string? TechnicalSpecs);

/// <summary>
/// Hàng hóa – dịch vụ dùng chung toàn tập đoàn (design.md mục 5.2): một mã hàng cho mọi công ty để so sánh giá được.
/// Tồn kho luôn lưu theo ĐVT cơ sở; ĐVT khác quy đổi qua <see cref="ItemUomConversion"/>.
/// </summary>
// SearchText suy ra từ mã, tên… nên không ghi vào nhật ký (trùng thông tin).
[Auditable("Hàng hóa", LabelProperty = nameof(Code), Ignore = [nameof(SearchText)])]
public sealed partial class Item : AuditableEntity
{
    /// <summary>Độ dài tối đa của mã hàng.</summary>
    public const int CodeMaxLength = 30;

    private readonly List<ItemUomConversion> _conversions = [];

    private Item()
    {
    }

    /// <summary>Mã hàng, duy nhất toàn tập đoàn, không đổi sau khi tạo.</summary>
    public string Code { get; private set; } = string.Empty;

    /// <summary>Tên hàng.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Tên tiếng Anh.</summary>
    public string? EnglishName { get; private set; }

    /// <summary>Loại hàng.</summary>
    public ItemType Type { get; private set; }

    /// <summary>Nhóm hàng – quyết định ai thấy mặt hàng (phạm vi nhóm hàng, design.md mục 9.2).</summary>
    public Guid CategoryId { get; private set; }

    /// <summary>ĐVT cơ sở: đơn vị lưu tồn kho và tính giá vốn.</summary>
    public Guid BaseUomId { get; private set; }

    /// <summary>Quy cách.</summary>
    public string? Specification { get; private set; }

    /// <summary>Thương hiệu.</summary>
    public string? Brand { get; private set; }

    /// <summary>Xuất xứ.</summary>
    public string? Origin { get; private set; }

    /// <summary>Mã vạch.</summary>
    public string? Barcode { get; private set; }

    /// <summary>Thuế suất GTGT mặc định (KCT, 0, 5, 8, 10).</summary>
    public string? VatCode { get; private set; }

    /// <summary>Thông số kỹ thuật.</summary>
    public string? TechnicalSpecs { get; private set; }

    /// <summary>Quản lý theo lô (chỉ hàng tồn kho).</summary>
    public bool TrackLot { get; private set; }

    /// <summary>Quản lý hạn dùng (chỉ hàng tồn kho).</summary>
    public bool TrackExpiry { get; private set; }

    /// <summary>Tồn tối thiểu theo ĐVT cơ sở – numeric(18,4) như mọi số lượng.</summary>
    public decimal? MinStock { get; private set; }

    /// <summary>Tồn tối đa theo ĐVT cơ sở.</summary>
    public decimal? MaxStock { get; private set; }

    /// <summary>Còn sử dụng.</summary>
    public bool IsActive { get; private set; } = true;

    /// <summary>Chuỗi tìm kiếm không dấu từ mã, tên, tên tiếng Anh, mã vạch – tự cập nhật khi sửa.</summary>
    public string SearchText { get; private set; } = string.Empty;

    /// <summary>Các ĐVT quy đổi.</summary>
    public IReadOnlyList<ItemUomConversion> Conversions => _conversions;

    /// <summary>
    /// Tạo mặt hàng.
    /// </summary>
    /// <param name="code">Mã hàng: 2–30 ký tự chữ không dấu, số và - . _ /; được chuẩn hóa chữ hoa.</param>
    /// <param name="type">Loại hàng.</param>
    /// <param name="category">Nhóm hàng (đang dùng).</param>
    /// <param name="baseUom">ĐVT cơ sở (đang dùng).</param>
    /// <param name="description">Tên và các trường mô tả.</param>
    /// <returns>Mặt hàng mới.</returns>
    /// <exception cref="BusinessRuleException">Khi dữ liệu không hợp lệ.</exception>
    public static Item Create(string code, ItemType type, ItemCategory category, Uom baseUom, ItemDescription description)
    {
        var normalized = CodeRules.Normalize(code);
        // Mã hàng thường có dấu phân cách (VT-0001, BL.M8.50) nên rộng hơn mã công ty, nhưng không có khoảng trắng để gõ nhanh.
        if (normalized.Length is < 2 or > CodeMaxLength || !ItemCodeRegex().IsMatch(normalized))
            throw new BusinessRuleException($"Mã hàng gồm 2–{CodeMaxLength} ký tự chữ không dấu (A–Z), số hoặc - . _ /.");
        if (!Enum.IsDefined(type)) throw new BusinessRuleException("Loại hàng không hợp lệ.");

        var item = new Item { Code = normalized, Type = type };
        item.ChangeCategory(category);
        item.ChangeBaseUom(baseUom);
        item.Describe(description);
        return item;
    }

    /// <summary>
    /// Sửa tên và các trường mô tả.
    /// </summary>
    /// <param name="description">Thông tin mới.</param>
    /// <exception cref="BusinessRuleException">Khi tên rỗng hoặc thuế suất không hợp lệ.</exception>
    public void Describe(ItemDescription description)
    {
        ArgumentNullException.ThrowIfNull(description);
        if (string.IsNullOrWhiteSpace(description.Name)) throw new BusinessRuleException("Tên hàng không được để trống.");
        var vat = Clean(description.VatCode)?.ToUpperInvariant();
        if (vat is not null && !VatRates.All.Contains(vat))
            throw new BusinessRuleException($"Thuế suất phải là một trong: {string.Join(", ", VatRates.All)}.");

        Name = description.Name.Trim();
        EnglishName = Clean(description.EnglishName);
        Specification = Clean(description.Specification);
        Brand = Clean(description.Brand);
        Origin = Clean(description.Origin);
        Barcode = Clean(description.Barcode);
        VatCode = vat;
        TechnicalSpecs = Clean(description.TechnicalSpecs);
        SearchText = Common.SearchText.Combine(Code, Name, EnglishName, Barcode);
    }

    /// <summary>
    /// Đổi nhóm hàng.
    /// </summary>
    /// <param name="category">Nhóm mới, đang dùng.</param>
    /// <exception cref="BusinessRuleException">Khi nhóm đã ngừng dùng.</exception>
    public void ChangeCategory(ItemCategory category)
    {
        ArgumentNullException.ThrowIfNull(category);
        if (!category.IsActive && category.Id != CategoryId)
            throw new BusinessRuleException($"Nhóm hàng {category.Code} đã ngừng dùng.");
        CategoryId = category.Id;
    }

    /// <summary>
    /// Đổi ĐVT cơ sở. (Khi đã có giao dịch kho sẽ khóa – đổi ĐVT cơ sở làm sai số tồn đã ghi.)
    /// </summary>
    /// <param name="uom">ĐVT mới, đang dùng, không trùng ĐVT quy đổi.</param>
    /// <exception cref="BusinessRuleException">Khi ĐVT đã ngừng dùng hoặc đang là ĐVT quy đổi.</exception>
    public void ChangeBaseUom(Uom uom)
    {
        ArgumentNullException.ThrowIfNull(uom);
        if (!uom.IsActive && uom.Id != BaseUomId) throw new BusinessRuleException($"Đơn vị tính {uom.Code} đã ngừng dùng.");
        if (_conversions.Any(c => c.UomId == uom.Id))
            throw new BusinessRuleException($"{uom.Code} đang là ĐVT quy đổi, không làm ĐVT cơ sở được.");
        BaseUomId = uom.Id;
    }

    /// <summary>
    /// Đặt loại hàng và các thông số quản lý kho. Chỉ hàng tồn kho mới đi qua kho (design.md mục 5.2) nên các loại khác
    /// không được bật lô/hạn dùng hay đặt tồn tối thiểu/tối đa.
    /// </summary>
    /// <param name="type">Loại hàng.</param>
    /// <param name="trackLot">Quản lý lô.</param>
    /// <param name="trackExpiry">Quản lý hạn dùng.</param>
    /// <param name="minStock">Tồn tối thiểu (≥ 0, tối đa 4 chữ số thập phân).</param>
    /// <param name="maxStock">Tồn tối đa (≥ tồn tối thiểu).</param>
    /// <exception cref="BusinessRuleException">Khi thông số không hợp lệ với loại hàng.</exception>
    public void SetStockControl(ItemType type, bool trackLot, bool trackExpiry, decimal? minStock, decimal? maxStock)
    {
        if (!Enum.IsDefined(type)) throw new BusinessRuleException("Loại hàng không hợp lệ.");
        if (type != ItemType.Inventory && (trackLot || trackExpiry || minStock is not null || maxStock is not null))
            throw new BusinessRuleException("Chỉ hàng tồn kho mới quản lý lô, hạn dùng và tồn tối thiểu/tối đa.");
        CheckQuantity(minStock, "Tồn tối thiểu");
        CheckQuantity(maxStock, "Tồn tối đa");
        if (minStock is { } min && maxStock is { } max && min > max)
            throw new BusinessRuleException("Tồn tối thiểu không được lớn hơn tồn tối đa.");

        Type = type;
        TrackLot = trackLot;
        TrackExpiry = trackExpiry;
        MinStock = minStock;
        MaxStock = maxStock;
    }

    /// <summary>
    /// Thay toàn bộ ĐVT quy đổi. Ví dụ ĐVT cơ sở là Hộp, thêm (Thùng, 24) nghĩa là 1 thùng = 24 hộp:
    /// nhập 2 thùng thì tồn tăng 2 × 24 = 48 hộp.
    /// </summary>
    /// <param name="conversions">ĐVT và hệ số về ĐVT cơ sở (&gt; 0, tối đa 6 chữ số thập phân để quy đổi được g → kg = 0,001).</param>
    /// <exception cref="BusinessRuleException">Khi trùng ĐVT, trùng ĐVT cơ sở, hệ số không hợp lệ hoặc ĐVT đã ngừng dùng.</exception>
    public void SetConversions(IEnumerable<(Uom Uom, decimal Factor)> conversions)
    {
        var list = conversions.ToList();
        var duplicate = list.GroupBy(c => c.Uom.Id).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null) throw new BusinessRuleException($"ĐVT {duplicate.First().Uom.Code} bị khai báo hai lần.");

        foreach (var (uom, factor) in list)
        {
            if (uom.Id == BaseUomId) throw new BusinessRuleException($"{uom.Code} là ĐVT cơ sở, không cần quy đổi.");
            if (factor <= 0 || decimal.Round(factor, 6) != factor || factor >= 1_000_000_000_000m)
                throw new BusinessRuleException($"Hệ số quy đổi của {uom.Code} phải lớn hơn 0, tối đa 6 chữ số thập phân.");
            var existing = _conversions.FirstOrDefault(c => c.UomId == uom.Id);
            if (existing is null && !uom.IsActive) throw new BusinessRuleException($"Đơn vị tính {uom.Code} đã ngừng dùng.");
            if (existing is null) _conversions.Add(new ItemUomConversion(Id, uom.Id, factor));
            else existing.ChangeFactor(factor);
        }

        _conversions.RemoveAll(c => list.All(x => x.Uom.Id != c.UomId));
    }

    /// <summary>Ngừng dùng: không chọn được trên chứng từ mới; chứng từ cũ vẫn giữ.</summary>
    public void Deactivate() => IsActive = false;

    /// <summary>Dùng lại.</summary>
    public void Activate() => IsActive = true;

    private static void CheckQuantity(decimal? value, string label)
    {
        // Số lượng lưu numeric(18,4) (CLAUDE.md mục 4): tối đa 14 chữ số phần nguyên, 4 chữ số thập phân.
        if (value is { } v && (v < 0 || decimal.Round(v, 4) != v || v >= 100_000_000_000_000m))
            throw new BusinessRuleException($"{label} phải ≥ 0 và tối đa 4 chữ số thập phân.");
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    [GeneratedRegex(@"^[A-Z0-9][A-Z0-9._/\-]*$")]
    private static partial Regex ItemCodeRegex();
}

/// <summary>
/// Quy đổi ĐVT của một mặt hàng: 1 <see cref="UomId"/> = <see cref="Factor"/> ĐVT cơ sở (design.md mục 5.2).
/// </summary>
[Auditable("Quy đổi ĐVT")]
public sealed class ItemUomConversion : Entity
{
    private ItemUomConversion()
    {
    }

    internal ItemUomConversion(Guid itemId, Guid uomId, decimal factor)
    {
        ItemId = itemId;
        UomId = uomId;
        Factor = factor;
    }

    /// <summary>Mặt hàng.</summary>
    public Guid ItemId { get; private set; }

    /// <summary>ĐVT quy đổi.</summary>
    public Guid UomId { get; private set; }

    /// <summary>Số ĐVT cơ sở trong 1 ĐVT này – numeric(18,6).</summary>
    public decimal Factor { get; private set; }

    internal void ChangeFactor(decimal factor) => Factor = factor;
}
