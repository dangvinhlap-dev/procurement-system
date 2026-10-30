using Microsoft.EntityFrameworkCore;
using Proc.Application.Common;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.Organization;

/// <summary>
/// Danh mục dùng chung toàn tập đoàn (công ty, nhóm hàng, hàng hóa, đơn vị tính, nhà cung cấp) do tổng công ty quản lý
/// (design.md mục 5, 5.6, 5.7): chỉ thêm, sửa khi đang làm việc ở tổng công ty.
/// </summary>
internal static class HeadOffice
{
    /// <summary>
    /// Công ty đang làm việc có phải tổng công ty không.
    /// </summary>
    /// <param name="db">DbContext.</param>
    /// <param name="scope">Phạm vi của người thao tác.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>true nếu đang làm việc ở tổng công ty.</returns>
    /// <exception cref="ForbiddenAccessException">Khi chưa chọn công ty.</exception>
    public static async Task<bool> IsWorkingAtAsync(ProcDbContext db, ICurrentUserScope scope, CancellationToken cancellationToken)
    {
        var companyId = scope.RequireCompany();
        return await db.Companies.AnyAsync(c => c.Id == companyId && c.IsHeadOffice, cancellationToken);
    }

    /// <summary>
    /// Bắt buộc đang làm việc ở tổng công ty.
    /// </summary>
    /// <param name="db">DbContext.</param>
    /// <param name="scope">Phạm vi của người thao tác.</param>
    /// <param name="what">Danh mục, ví dụ "công ty", "hàng hóa" – để thông báo rõ.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Id tổng công ty.</returns>
    /// <exception cref="ForbiddenAccessException">Khi không làm việc ở tổng công ty.</exception>
    public static async Task<Guid> RequireAsync(ProcDbContext db, ICurrentUserScope scope, string what, CancellationToken cancellationToken)
    {
        if (!await IsWorkingAtAsync(db, scope, cancellationToken))
            throw new ForbiddenAccessException($"Chỉ thêm, sửa {what} khi đang làm việc ở tổng công ty.");
        return scope.RequireCompany();
    }
}
