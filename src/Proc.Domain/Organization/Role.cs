using Proc.Domain.Common;

namespace Proc.Domain.Organization;

/// <summary>
/// Vai trò = tập quyền chức năng (design.md mục 9.1). Danh sách quyền của vai trò thêm ở lát phân quyền.
/// </summary>
public sealed class Role : AuditableEntity
{
    /// <summary>Mã vai trò Quản trị hệ thống, tạo sẵn khi khởi tạo dữ liệu.</summary>
    public const string AdminCode = "ADMIN";

    private Role()
    {
    }

    /// <summary>Mã vai trò, duy nhất, ví dụ ADMIN, THUKHO.</summary>
    public string Code { get; private set; } = string.Empty;

    /// <summary>Tên hiển thị, ví dụ "Quản trị hệ thống".</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Còn sử dụng.</summary>
    public bool IsActive { get; private set; } = true;

    /// <summary>
    /// Tạo vai trò mới.
    /// </summary>
    /// <param name="code">Mã vai trò; được chuẩn hóa về chữ hoa.</param>
    /// <param name="name">Tên hiển thị.</param>
    /// <returns>Vai trò mới.</returns>
    /// <exception cref="BusinessRuleException">Khi mã hoặc tên không hợp lệ.</exception>
    public static Role Create(string code, string name)
    {
        var normalized = CodeRules.Normalize(code);
        if (!CodeRules.IsAlphanumeric(normalized, 2, 30))
            throw new BusinessRuleException("Mã vai trò phải gồm 2–30 ký tự chữ không dấu (A–Z) hoặc số.");
        if (string.IsNullOrWhiteSpace(name))
            throw new BusinessRuleException("Tên vai trò không được để trống.");
        return new Role { Code = normalized, Name = name.Trim() };
    }
}
