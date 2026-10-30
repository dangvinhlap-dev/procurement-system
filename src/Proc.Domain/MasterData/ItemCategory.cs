using Proc.Domain.Common;

namespace Proc.Domain.MasterData;

/// <summary>
/// Nhóm hàng dạng cây nhiều cấp, dùng chung toàn tập đoàn (design.md mục 5.2).
/// Dùng để phân quyền theo nhóm hàng: được phép một nhóm thì được phép mọi nhóm con (design.md mục 9.2).
/// </summary>
[Auditable("Nhóm hàng", LabelProperty = nameof(Code))]
public sealed class ItemCategory : AuditableEntity
{
    private ItemCategory()
    {
    }

    /// <summary>Mã nhóm, duy nhất toàn tập đoàn.</summary>
    public string Code { get; private set; } = string.Empty;

    /// <summary>Tên nhóm.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Nhóm cha; null là nhóm gốc.</summary>
    public Guid? ParentId { get; private set; }

    /// <summary>Tài khoản kho mặc định (ví dụ 152, 153, 156) – gợi ý khi xuất dữ liệu sang phần mềm kế toán.</summary>
    public string? InventoryAccount { get; private set; }

    /// <summary>Còn sử dụng.</summary>
    public bool IsActive { get; private set; } = true;

    /// <summary>
    /// Tạo nhóm hàng.
    /// </summary>
    /// <param name="code">Mã nhóm.</param>
    /// <param name="name">Tên nhóm.</param>
    /// <param name="parent">Nhóm cha; null nếu là nhóm gốc.</param>
    /// <returns>Nhóm hàng mới.</returns>
    /// <exception cref="BusinessRuleException">Khi mã hoặc tên không hợp lệ.</exception>
    public static ItemCategory Create(string code, string name, ItemCategory? parent = null)
    {
        var normalized = CodeRules.Normalize(code);
        if (!CodeRules.IsAlphanumeric(normalized, 2, 20))
            throw new BusinessRuleException("Mã nhóm hàng phải gồm 2–20 ký tự chữ không dấu (A–Z) hoặc số.");
        if (string.IsNullOrWhiteSpace(name)) throw new BusinessRuleException("Tên nhóm hàng không được để trống.");
        return new ItemCategory { Code = normalized, Name = name.Trim(), ParentId = parent?.Id };
    }

    /// <summary>
    /// Sửa tên và tài khoản kho mặc định. Mã không đổi: đã dùng trong phân quyền và báo cáo.
    /// </summary>
    /// <param name="name">Tên nhóm.</param>
    /// <param name="inventoryAccount">Tài khoản kho mặc định (chỉ số, 3–10 ký tự); trống = không có.</param>
    /// <exception cref="BusinessRuleException">Khi tên rỗng hoặc tài khoản sai định dạng.</exception>
    public void Update(string name, string? inventoryAccount)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new BusinessRuleException("Tên nhóm hàng không được để trống.");
        var account = string.IsNullOrWhiteSpace(inventoryAccount) ? null : inventoryAccount.Trim();
        // Số hiệu tài khoản kế toán Việt Nam là dãy số (152, 1521…), không có chữ.
        if (account is not null && (account.Length is < 3 or > 10 || !account.All(char.IsAsciiDigit)))
            throw new BusinessRuleException("Tài khoản kho gồm 3–10 chữ số, ví dụ 152 hoặc 1521.");
        Name = name.Trim();
        InventoryAccount = account;
    }

    /// <summary>
    /// Đổi nhóm cha. Chặn vòng lặp (nhóm cha là nhóm con, cháu của chính nó) cần cả cây nên tầng Infrastructure kiểm tra trước.
    /// </summary>
    /// <param name="parent">Nhóm cha mới; null = thành nhóm gốc.</param>
    /// <exception cref="BusinessRuleException">Khi nhóm cha là chính nó.</exception>
    public void MoveTo(ItemCategory? parent)
    {
        if (parent?.Id == Id) throw new BusinessRuleException("Nhóm hàng không thể là nhóm cha của chính nó.");
        ParentId = parent?.Id;
    }

    /// <summary>Ngừng dùng (kiểm tra nhóm con, hàng hóa đang dùng do tầng Infrastructure làm).</summary>
    public void Deactivate() => IsActive = false;

    /// <summary>Dùng lại.</summary>
    public void Activate() => IsActive = true;
}
