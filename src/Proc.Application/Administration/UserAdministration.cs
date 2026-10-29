using Proc.Application.Organization;

namespace Proc.Application.Administration;

/// <summary>Một dòng trên danh sách người dùng.</summary>
/// <param name="Id">Id tài khoản.</param>
/// <param name="UserName">Tên đăng nhập.</param>
/// <param name="FullName">Họ tên.</param>
/// <param name="Email">Email.</param>
/// <param name="IsActive">Còn sử dụng.</param>
/// <param name="IsLockedOut">Đang bị khóa tạm do nhập sai mật khẩu.</param>
/// <param name="RoleNames">Tên các vai trò tại công ty đang làm việc (rỗng = không làm việc ở công ty này).</param>
/// <param name="CompanyCodes">Mã mọi công ty người dùng được gán vai trò.</param>
/// <param name="CreatedAt">Thời điểm tạo (UTC).</param>
public sealed record UserListItemDto(
    Guid Id,
    string UserName,
    string FullName,
    string? Email,
    bool IsActive,
    bool IsLockedOut,
    IReadOnlyList<string> RoleNames,
    IReadOnlyList<string> CompanyCodes,
    DateTimeOffset CreatedAt);

/// <summary>Vai trò, kho, nhóm hàng của một người dùng tại một công ty.</summary>
/// <param name="RoleIds">Vai trò được gán; rỗng = không làm việc ở công ty này.</param>
/// <param name="WarehouseIds">Kho được phép; rỗng = mọi kho của công ty (design.md mục 9.2).</param>
/// <param name="ItemCategoryIds">Nhóm hàng được phép (kế thừa xuống nhóm con); rỗng = mọi nhóm hàng.</param>
public sealed record UserAccessDto(
    IReadOnlyList<Guid> RoleIds,
    IReadOnlyList<Guid> WarehouseIds,
    IReadOnlyList<Guid> ItemCategoryIds);

/// <summary>Chi tiết người dùng cho ngăn kéo chi tiết.</summary>
/// <param name="Id">Id tài khoản.</param>
/// <param name="UserName">Tên đăng nhập.</param>
/// <param name="FullName">Họ tên.</param>
/// <param name="Email">Email.</param>
/// <param name="IsActive">Còn sử dụng.</param>
/// <param name="LockoutEnd">Khóa tạm đến lúc nào (UTC); null nếu không bị khóa.</param>
/// <param name="CreatedAt">Thời điểm tạo (UTC).</param>
/// <param name="CanManageAccount">Người đang xem được sửa thông tin, ngừng dùng, đặt lại mật khẩu cho tài khoản này.</param>
/// <param name="IsSelf">Là chính người đang xem (không tự sửa quyền của mình).</param>
/// <param name="Access">Vai trò và phạm vi tại công ty đang làm việc.</param>
/// <param name="CompanyCodes">Mã mọi công ty người dùng được gán vai trò.</param>
public sealed record UserDetailDto(
    Guid Id,
    string UserName,
    string FullName,
    string? Email,
    bool IsActive,
    DateTimeOffset? LockoutEnd,
    DateTimeOffset CreatedAt,
    bool CanManageAccount,
    bool IsSelf,
    UserAccessDto Access,
    IReadOnlyList<string> CompanyCodes);

/// <summary>Vai trò chọn được khi gán cho người dùng.</summary>
/// <param name="Id">Id vai trò.</param>
/// <param name="Code">Mã.</param>
/// <param name="Name">Tên.</param>
/// <param name="CanAssign">Người đang thao tác được gán/gỡ vai trò này (quyền của vai trò nằm trong quyền của họ).</param>
public sealed record AssignableRoleDto(Guid Id, string Code, string Name, bool CanAssign);

/// <summary>Lựa chọn trên tab "Phân quyền" của người dùng, tại công ty đang làm việc.</summary>
/// <param name="Roles">Vai trò đang dùng.</param>
/// <param name="Warehouses">Kho trong phạm vi của người đang thao tác.</param>
/// <param name="ItemCategories">Nhóm hàng trong phạm vi của người đang thao tác.</param>
/// <param name="MustLimitWarehouses">Người đang thao tác bị giới hạn kho, nên phải chọn ít nhất một kho (không cấp "mọi kho").</param>
/// <param name="MustLimitItemCategories">Tương tự cho nhóm hàng.</param>
public sealed record UserAccessOptionsDto(
    IReadOnlyList<AssignableRoleDto> Roles,
    IReadOnlyList<WarehouseDto> Warehouses,
    IReadOnlyList<ItemCategoryDto> ItemCategories,
    bool MustLimitWarehouses,
    bool MustLimitItemCategories);

/// <summary>Tạo tài khoản mới, kèm gán vai trò và phạm vi tại công ty đang làm việc.</summary>
/// <param name="UserName">Tên đăng nhập (chữ thường không dấu, số, dấu chấm, gạch).</param>
/// <param name="FullName">Họ tên.</param>
/// <param name="Email">Email (tùy chọn).</param>
/// <param name="Password">Mật khẩu ban đầu.</param>
/// <param name="Access">Vai trò và phạm vi tại công ty đang làm việc.</param>
public sealed record CreateUserRequest(string UserName, string FullName, string? Email, string Password, UserAccessDto Access);

/// <summary>Sửa thông tin tài khoản.</summary>
/// <param name="FullName">Họ tên.</param>
/// <param name="Email">Email (tùy chọn).</param>
/// <param name="IsActive">Còn sử dụng.</param>
public sealed record UpdateUserRequest(string FullName, string? Email, bool IsActive);

/// <summary>Quản trị đặt lại mật khẩu cho người dùng (quên mật khẩu, bị khóa).</summary>
/// <param name="NewPassword">Mật khẩu mới.</param>
public sealed record ResetPasswordRequest(string NewPassword);

/// <summary>
/// Quản lý người dùng (design.md mục 9.1, 9.2, 9.5). Mọi hàm chạy tại công ty đang làm việc và tự chặn việc tự nâng quyền:
/// <list type="bullet">
/// <item>Chỉ gán/gỡ vai trò mà mọi quyền của nó nằm trong quyền của người thao tác tại công ty đó.</item>
/// <item>Chỉ cấp kho, nhóm hàng trong phạm vi của người thao tác; bị giới hạn thì không cấp "tất cả".</item>
/// <item>Không tự sửa quyền, tự ngừng dùng hay tự đặt lại mật khẩu của chính mình.</item>
/// <item>Sửa thông tin, ngừng dùng, đặt lại mật khẩu (có hiệu lực ở mọi công ty) chỉ khi người thao tác "trùm" được người đó
/// ở mọi công ty họ được gán: có quyền quản lý người dùng và có đủ quyền của các vai trò họ đang giữ.</item>
/// </list>
/// Lỗi quy tắc ném BusinessRuleException (400), không đủ quyền ném ForbiddenAccessException (403),
/// không tìm thấy ném NotFoundException (404).
/// </summary>
public interface IUserAdministration
{
    /// <summary>
    /// Danh sách mọi tài khoản, kèm vai trò tại công ty đang làm việc.
    /// </summary>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Danh sách theo tên đăng nhập.</returns>
    Task<IReadOnlyList<UserListItemDto>> ListAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Chi tiết một tài khoản, kèm vai trò và phạm vi tại công ty đang làm việc.
    /// </summary>
    /// <param name="id">Id tài khoản.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Chi tiết.</returns>
    Task<UserDetailDto> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Các lựa chọn vai trò, kho, nhóm hàng khi phân quyền tại công ty đang làm việc.
    /// </summary>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Lựa chọn.</returns>
    Task<UserAccessOptionsDto> GetAccessOptionsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Tạo tài khoản và gán quyền tại công ty đang làm việc trong cùng một giao dịch.
    /// </summary>
    /// <param name="request">Thông tin tài khoản.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Chi tiết tài khoản vừa tạo.</returns>
    Task<UserDetailDto> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Sửa họ tên, email, trạng thái sử dụng.
    /// </summary>
    /// <param name="id">Id tài khoản.</param>
    /// <param name="request">Thông tin mới.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Chi tiết sau khi sửa.</returns>
    Task<UserDetailDto> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Đặt lại mật khẩu và mở khóa tạm.
    /// </summary>
    /// <param name="id">Id tài khoản.</param>
    /// <param name="request">Mật khẩu mới.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Task hoàn tất khi đã lưu.</returns>
    Task ResetPasswordAsync(Guid id, ResetPasswordRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Thay toàn bộ vai trò, kho, nhóm hàng của người dùng tại công ty đang làm việc.
    /// </summary>
    /// <param name="id">Id tài khoản.</param>
    /// <param name="access">Quyền mới; RoleIds rỗng = gỡ người dùng khỏi công ty (xóa luôn phạm vi tại công ty).</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Chi tiết sau khi sửa.</returns>
    Task<UserDetailDto> SetAccessAsync(Guid id, UserAccessDto access, CancellationToken cancellationToken);
}
