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
