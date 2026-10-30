using Proc.Domain.Common;

namespace Proc.Domain.Organization;

/// <summary>Loại kho (design.md mục 4, 5.1).</summary>
public enum WarehouseType
{
    /// <summary>Kho tổng: mỗi công ty đúng một kho; chỉ kho tổng được giao dịch liên công ty (ICS/ICR).</summary>
    Main = 1,

    /// <summary>Kho phụ: thuộc một công ty, chỉ chuyển kho trong cùng công ty (TRF).</summary>
    Sub = 2,
}

/// <summary>
/// Kho của một công ty (design.md mục 5.1). Mỗi công ty có đúng một kho Main (ràng buộc duy nhất trong CSDL);
/// kho Sub thuộc một công ty và có kho cha cùng công ty.
/// </summary>
[Auditable("Kho", LabelProperty = nameof(Code))]
public sealed class Warehouse : AuditableEntity, ICompanyOwned
{
    private Warehouse()
    {
    }

    /// <inheritdoc />
    public Guid CompanyId { get; private set; }

    /// <summary>Mã kho, duy nhất trong công ty.</summary>
    public string Code { get; private set; } = string.Empty;

    /// <summary>Tên kho.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Kho tổng hay kho phụ.</summary>
    public WarehouseType Type { get; private set; }

    /// <summary>Kho cha (chỉ kho Sub có).</summary>
    public Guid? ParentWarehouseId { get; private set; }

    /// <summary>Địa chỉ.</summary>
    public string? Address { get; private set; }

    /// <summary>Thủ kho.</summary>
    public string? Keeper { get; private set; }

    /// <summary>Cho phép tồn âm – mặc định không (CLAUDE.md mục 4).</summary>
    public bool AllowNegativeStock { get; private set; }

    /// <summary>Còn sử dụng.</summary>
    public bool IsActive { get; private set; } = true;

    /// <summary>
    /// Tạo kho tổng của công ty.
    /// </summary>
    /// <param name="companyId">Công ty.</param>
    /// <param name="code">Mã kho.</param>
    /// <param name="name">Tên kho.</param>
    /// <returns>Kho tổng.</returns>
    /// <exception cref="BusinessRuleException">Khi mã hoặc tên không hợp lệ.</exception>
    public static Warehouse CreateMain(Guid companyId, string code, string name) =>
        Create(companyId, code, name, WarehouseType.Main, parentWarehouseId: null);

    /// <summary>
    /// Tạo kho phụ dưới một kho cha cùng công ty.
    /// </summary>
    /// <param name="parent">Kho cha.</param>
    /// <param name="code">Mã kho.</param>
    /// <param name="name">Tên kho.</param>
    /// <returns>Kho phụ cùng công ty với kho cha.</returns>
    /// <exception cref="BusinessRuleException">Khi mã hoặc tên không hợp lệ.</exception>
    public static Warehouse CreateSub(Warehouse parent, string code, string name)
    {
        ArgumentNullException.ThrowIfNull(parent);
        // Kho Sub lấy CompanyId của kho cha, nên không thể tạo kho phụ treo dưới kho của công ty khác – design.md mục 4.
        return Create(parent.CompanyId, code, name, WarehouseType.Sub, parent.Id);
    }

    /// <summary>
    /// Sửa thông tin kho. Mã và loại kho không đổi: mã nằm trên chứng từ đã ghi sổ, loại quyết định đường luân chuyển hợp lệ.
    /// </summary>
    /// <param name="name">Tên kho, bắt buộc.</param>
    /// <param name="address">Địa chỉ.</param>
    /// <param name="keeper">Thủ kho.</param>
    /// <param name="allowNegativeStock">Cho phép tồn âm (CLAUDE.md mục 4: mặc định không).</param>
    /// <exception cref="BusinessRuleException">Khi tên rỗng.</exception>
    public void Update(string name, string? address, string? keeper, bool allowNegativeStock)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new BusinessRuleException("Tên kho không được để trống.");
        Name = name.Trim();
        Address = string.IsNullOrWhiteSpace(address) ? null : address.Trim();
        Keeper = string.IsNullOrWhiteSpace(keeper) ? null : keeper.Trim();
        AllowNegativeStock = allowNegativeStock;
    }

    /// <summary>
    /// Đổi kho cha của kho phụ. Việc chặn vòng lặp (kho cha là kho con của chính nó) cần cả cây kho nên do tầng
    /// Infrastructure kiểm tra trước khi gọi.
    /// </summary>
    /// <param name="parent">Kho cha mới, cùng công ty.</param>
    /// <exception cref="BusinessRuleException">Khi là kho tổng, kho cha khác công ty hoặc là chính nó.</exception>
    public void MoveTo(Warehouse parent)
    {
        ArgumentNullException.ThrowIfNull(parent);
        if (Type == WarehouseType.Main) throw new BusinessRuleException("Kho tổng không có kho cha.");
        // Kho Sub không được treo dưới kho của công ty khác – design.md mục 4.
        if (parent.CompanyId != CompanyId) throw new BusinessRuleException("Kho cha phải cùng công ty.");
        if (parent.Id == Id) throw new BusinessRuleException("Kho không thể là kho cha của chính nó.");
        ParentWarehouseId = parent.Id;
    }

    /// <summary>Ngừng dùng kho.</summary>
    /// <exception cref="BusinessRuleException">Khi là kho tổng.</exception>
    public void Deactivate()
    {
        // Mỗi công ty luôn có một kho tổng đang dùng – kênh duy nhất của giao dịch liên công ty ICS/ICR (design.md mục 4).
        if (Type == WarehouseType.Main) throw new BusinessRuleException("Không ngừng dùng được kho tổng của công ty.");
        IsActive = false;
    }

    /// <summary>Dùng lại kho đã ngừng.</summary>
    public void Activate() => IsActive = true;

    private static Warehouse Create(Guid companyId, string code, string name, WarehouseType type, Guid? parentWarehouseId)
    {
        if (companyId == Guid.Empty) throw new BusinessRuleException("Kho phải thuộc một công ty.");
        var normalized = CodeRules.Normalize(code);
        if (!CodeRules.IsAlphanumeric(normalized, 2, 20))
            throw new BusinessRuleException("Mã kho phải gồm 2–20 ký tự chữ không dấu (A–Z) hoặc số.");
        if (string.IsNullOrWhiteSpace(name)) throw new BusinessRuleException("Tên kho không được để trống.");

        return new Warehouse
        {
            CompanyId = companyId,
            Code = normalized,
            Name = name.Trim(),
            Type = type,
            ParentWarehouseId = parentWarehouseId,
        };
    }
}
