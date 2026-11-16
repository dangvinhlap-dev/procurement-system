using Proc.Domain.Common;

namespace Proc.Domain.Organization;

/// <summary>
/// Kho của một công ty – từ khi bỏ phân hệ kho chỉ còn là <b>nơi nhận hàng</b> (design.md mục 19.4): danh sách phẳng, không kho cha, không tồn
/// kho. Mã duy nhất trong công ty.
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

    /// <summary>Địa chỉ.</summary>
    public string? Address { get; private set; }

    /// <summary>Thủ kho.</summary>
    public string? Keeper { get; private set; }

    /// <summary>Còn sử dụng.</summary>
    public bool IsActive { get; private set; } = true;

    /// <summary>
    /// Tạo kho (nơi nhận hàng) của công ty.
    /// </summary>
    /// <param name="companyId">Công ty.</param>
    /// <param name="code">Mã kho: 2–20 ký tự chữ không dấu hoặc số, tự viết hoa.</param>
    /// <param name="name">Tên kho.</param>
    /// <returns>Kho mới, đang dùng.</returns>
    /// <exception cref="BusinessRuleException">Khi thiếu công ty, mã hoặc tên không hợp lệ.</exception>
    public static Warehouse Create(Guid companyId, string code, string name)
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
        };
    }

    /// <summary>
    /// Sửa thông tin kho. Mã không đổi: mã nằm trên chứng từ (đề nghị mua, PO, XNH) đã lập.
    /// </summary>
    /// <param name="name">Tên kho, bắt buộc.</param>
    /// <param name="address">Địa chỉ.</param>
    /// <param name="keeper">Thủ kho.</param>
    /// <exception cref="BusinessRuleException">Khi tên rỗng.</exception>
    public void Update(string name, string? address, string? keeper)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new BusinessRuleException("Tên kho không được để trống.");
        Name = name.Trim();
        Address = string.IsNullOrWhiteSpace(address) ? null : address.Trim();
        Keeper = string.IsNullOrWhiteSpace(keeper) ? null : keeper.Trim();
    }

    /// <summary>Ngừng dùng kho – kho nào cũng ngừng được (không còn kho tổng bắt buộc từ khi bỏ giao dịch liên công ty ICS/ICR).</summary>
    public void Deactivate() => IsActive = false;

    /// <summary>Dùng lại kho đã ngừng.</summary>
    public void Activate() => IsActive = true;
}
