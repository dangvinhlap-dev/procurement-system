using Proc.Domain.Common;

namespace Proc.Domain.MasterData;

/// <summary>
/// Nhóm hàng dạng cây nhiều cấp, dùng chung toàn tập đoàn (design.md mục 5.2).
/// Dùng để phân quyền theo nhóm hàng: được phép một nhóm thì được phép mọi nhóm con (design.md mục 9.2).
/// </summary>
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
}
