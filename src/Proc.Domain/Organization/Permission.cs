namespace Proc.Domain.Organization;

/// <summary>
/// Một quyền chức năng trong CSDL. Bảng này được đồng bộ từ <see cref="Permissions.All"/> khi khởi động,
/// để RolePermission có khóa ngoại kiểm tra mã quyền hợp lệ.
/// </summary>
public sealed class Permission
{
    private Permission()
    {
    }

    /// <summary>Mã quyền (khóa chính), ví dụ Purchase.PO.Create.</summary>
    public string Code { get; private set; } = string.Empty;

    /// <summary>Tên tiếng Việt.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Module, ví dụ Purchase.</summary>
    public string Module { get; private set; } = string.Empty;

    /// <summary>
    /// Tạo quyền từ khai báo trong code.
    /// </summary>
    /// <param name="definition">Khai báo quyền.</param>
    /// <returns>Entity quyền.</returns>
    public static Permission From(PermissionDefinition definition) =>
        new() { Code = definition.Code, Name = definition.Name, Module = definition.Module };

    /// <summary>
    /// Cập nhật tên/module khi khai báo trong code thay đổi.
    /// </summary>
    /// <param name="definition">Khai báo quyền mới.</param>
    public void UpdateFrom(PermissionDefinition definition)
    {
        Name = definition.Name;
        Module = definition.Module;
    }
}

/// <summary>
/// Quyền thuộc vai trò: vai trò = tập quyền (design.md mục 9.1).
/// </summary>
public sealed class RolePermission
{
    private RolePermission()
    {
    }

    /// <summary>Vai trò.</summary>
    public Guid RoleId { get; private set; }

    /// <summary>Mã quyền.</summary>
    public string PermissionCode { get; private set; } = string.Empty;

    /// <summary>
    /// Cấp một quyền cho vai trò.
    /// </summary>
    /// <param name="roleId">Vai trò.</param>
    /// <param name="permissionCode">Mã quyền (phải có trong <see cref="Permissions.All"/>).</param>
    /// <returns>Dòng gán quyền.</returns>
    public static RolePermission Grant(Guid roleId, string permissionCode) =>
        new() { RoleId = roleId, PermissionCode = permissionCode };
}
