using Proc.Application.Common;
using Proc.Domain.Organization;
using Proc.Infrastructure.Organization;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.MasterData;

/// <summary>
/// Quyền sửa thông tin danh mục dùng chung (design.md mục 5.7): thêm mới chỉ ở tổng công ty (tránh tạo trùng từ nhiều công ty); sửa thông tin
/// (trừ mã – mã không bao giờ sửa) được khi làm việc ở tổng công ty với quyền quản lý danh mục đó, hoặc ở bất kỳ công ty nào với quyền
/// <c>MasterData.Catalog.EditShared</c>. Mọi lần sửa vẫn vào nhật ký qua interceptor.
/// </summary>
internal static class SharedCatalog
{
    /// <summary>
    /// Người dùng có sửa được bản ghi của danh mục dùng chung không.
    /// </summary>
    /// <param name="db">DbContext.</param>
    /// <param name="scope">Phạm vi người dùng.</param>
    /// <param name="managePermission">Quyền quản lý của danh mục, ví dụ <c>MasterData.Item.Manage</c>.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>true nếu sửa được.</returns>
    public static async Task<bool> CanEditAsync(ProcDbContext db, ICurrentUserScope scope, string managePermission, CancellationToken cancellationToken) =>
        scope.HasPermission(Permissions.MasterData.CatalogEditShared)
        || (scope.HasPermission(managePermission) && await HeadOffice.IsWorkingAtAsync(db, scope, cancellationToken));

    /// <summary>
    /// Bắt buộc sửa được bản ghi của danh mục dùng chung.
    /// </summary>
    /// <param name="db">DbContext.</param>
    /// <param name="scope">Phạm vi người dùng.</param>
    /// <param name="what">Danh mục, ví dụ "hàng hóa" – để thông báo rõ.</param>
    /// <param name="managePermission">Quyền quản lý của danh mục.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <exception cref="ForbiddenAccessException">Khi không sửa được.</exception>
    public static async Task RequireEditAsync(
        ProcDbContext db, ICurrentUserScope scope, string what, string managePermission, CancellationToken cancellationToken)
    {
        if (!await CanEditAsync(db, scope, managePermission, cancellationToken))
            throw new ForbiddenAccessException(
                $"Chỉ sửa được {what} khi làm việc ở tổng công ty, hoặc khi được cấp quyền \"Sửa thông tin danh mục dùng chung\".");
    }
}
