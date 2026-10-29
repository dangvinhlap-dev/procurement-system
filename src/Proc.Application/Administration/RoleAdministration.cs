namespace Proc.Application.Administration;

/// <summary>Một dòng trên danh sách vai trò.</summary>
/// <param name="Id">Id vai trò.</param>
/// <param name="Code">Mã.</param>
/// <param name="Name">Tên.</param>
/// <param name="IsActive">Còn sử dụng.</param>
/// <param name="IsSystem">Vai trò hệ thống (ADMIN), không sửa được.</param>
/// <param name="PermissionCount">Số quyền.</param>
/// <param name="UserCount">Số người dùng đang giữ vai trò (ở mọi công ty).</param>
public sealed record RoleListItemDto(
    Guid Id, string Code, string Name, bool IsActive, bool IsSystem, int PermissionCount, int UserCount);

/// <summary>Chi tiết vai trò.</summary>
/// <param name="Id">Id vai trò.</param>
/// <param name="Code">Mã.</param>
/// <param name="Name">Tên.</param>
/// <param name="IsActive">Còn sử dụng.</param>
/// <param name="IsSystem">Vai trò hệ thống (ADMIN), không sửa được.</param>
/// <param name="CanEdit">Người đang xem sửa được vai trò này (có quyền quản lý và có đủ mọi quyền của vai trò).</param>
/// <param name="PermissionCodes">Mã quyền của vai trò.</param>
public sealed record RoleDetailDto(
    Guid Id, string Code, string Name, bool IsActive, bool IsSystem, bool CanEdit, IReadOnlyList<string> PermissionCodes);

/// <summary>Một quyền trên màn hình phân quyền.</summary>
/// <param name="Code">Mã quyền.</param>
/// <param name="Name">Tên tiếng Việt.</param>
public sealed record PermissionDto(string Code, string Name);

/// <summary>Quyền gom theo module.</summary>
/// <param name="Module">Mã module, ví dụ Purchase.</param>
/// <param name="Name">Tên module tiếng Việt.</param>
/// <param name="Permissions">Các quyền của module.</param>
public sealed record PermissionGroupDto(string Module, string Name, IReadOnlyList<PermissionDto> Permissions);

/// <summary>Tạo vai trò.</summary>
/// <param name="Code">Mã (2–30 ký tự chữ không dấu hoặc số), không đổi được về sau.</param>
/// <param name="Name">Tên.</param>
/// <param name="PermissionCodes">Mã quyền.</param>
public sealed record CreateRoleRequest(string Code, string Name, IReadOnlyList<string> PermissionCodes);

/// <summary>Sửa vai trò.</summary>
/// <param name="Name">Tên.</param>
/// <param name="IsActive">Còn sử dụng.</param>
/// <param name="PermissionCodes">Toàn bộ mã quyền mới.</param>
public sealed record UpdateRoleRequest(string Name, bool IsActive, IReadOnlyList<string> PermissionCodes);

/// <summary>
/// Quản lý vai trò và quyền của vai trò (design.md mục 9.1, 9.5). Vai trò dùng chung cho mọi công ty.
/// Chống tự nâng quyền: chỉ cấp được quyền mà người thao tác đang có tại công ty làm việc, và chỉ sửa được vai trò
/// mà mọi quyền của nó nằm trong quyền của người thao tác. Vai trò ADMIN cố định, không sửa.
/// </summary>
public interface IRoleAdministration
{
    /// <summary>
    /// Danh mục quyền gom theo module.
    /// </summary>
    /// <returns>Các nhóm quyền theo thứ tự hiển thị.</returns>
    IReadOnlyList<PermissionGroupDto> GetPermissionCatalog();

    /// <summary>
    /// Danh sách vai trò.
    /// </summary>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Danh sách theo mã, ADMIN đầu tiên.</returns>
    Task<IReadOnlyList<RoleListItemDto>> ListAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Chi tiết vai trò.
    /// </summary>
    /// <param name="id">Id vai trò.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Chi tiết.</returns>
    Task<RoleDetailDto> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Tạo vai trò.
    /// </summary>
    /// <param name="request">Thông tin vai trò.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Chi tiết vai trò vừa tạo.</returns>
    Task<RoleDetailDto> CreateAsync(CreateRoleRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Sửa tên, trạng thái và toàn bộ quyền của vai trò.
    /// </summary>
    /// <param name="id">Id vai trò.</param>
    /// <param name="request">Thông tin mới.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Chi tiết sau khi sửa.</returns>
    Task<RoleDetailDto> UpdateAsync(Guid id, UpdateRoleRequest request, CancellationToken cancellationToken);
}
