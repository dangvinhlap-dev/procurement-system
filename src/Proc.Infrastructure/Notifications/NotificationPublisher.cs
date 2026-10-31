using Microsoft.EntityFrameworkCore;
using Proc.Application.Common;
using Proc.Domain.Notifications;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.Notifications;

/// <summary>
/// Sinh thông báo từ sự kiện nghiệp vụ (design.md mục 11.6). Chỉ thêm vào DbContext, không tự lưu: dịch vụ gọi lưu cùng thay đổi của
/// chứng từ trong một lần SaveChanges/giao dịch, nên thao tác lỗi thì không có thông báo "ma", thao tác xong thì chắc chắn có thông báo.
/// Người gây ra sự kiện không nhận thông báo của chính mình.
/// </summary>
internal sealed class NotificationPublisher
{
    private readonly ProcDbContext _db;
    private readonly ICurrentUserScope _scope;
    private readonly IClock _clock;

    /// <summary>Tạo bộ sinh thông báo.</summary>
    public NotificationPublisher(ProcDbContext db, ICurrentUserScope scope, IClock clock)
    {
        _db = db;
        _scope = scope;
        _clock = clock;
    }

    /// <summary>
    /// Người dùng đang hoạt động có quyền <paramref name="permission"/> tại công ty (hợp quyền mọi vai trò còn dùng – cùng cách nạp
    /// phạm vi của UserScopeLoader, design.md mục 9.2).
    /// </summary>
    /// <param name="companyId">Công ty – có thể khác công ty đang làm việc (thông báo cho công ty mua nội bộ).</param>
    /// <param name="permission">Mã quyền.</param>
    /// <param name="requiredRoleId">Nếu có: người nhận phải được gán đúng vai trò này tại công ty (cấp duyệt theo vai trò).</param>
    /// <param name="warehouseId">Nếu có: người nhận phải được làm việc với kho này (không giới hạn kho hoặc có kho trong phạm vi).</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Id người nhận.</returns>
    public async Task<List<Guid>> UsersWithPermissionAsync(
        Guid companyId, string permission, Guid? requiredRoleId, Guid? warehouseId, CancellationToken cancellationToken)
    {
        // Các bảng phân quyền không mang filter công ty nên truy vấn được công ty khác công ty đang làm việc.
        var activeRoles = _db.UserCompanyRoles.Where(x => x.CompanyId == companyId && x.Company!.IsActive && x.Role!.IsActive);
        var query = activeRoles
            .Where(x => _db.RolePermissions.Any(rp => rp.RoleId == x.RoleId && rp.PermissionCode == permission))
            .Select(x => x.UserId);
        if (requiredRoleId is { } roleId)
            query = query.Where(u => activeRoles.Any(x => x.UserId == u && x.RoleId == roleId));
        if (warehouseId is { } warehouse)
        {
            // Không có dòng phạm vi kho tại công ty = được mọi kho (design.md mục 9.2).
            var scopes = _db.UserWarehouseScopes.Where(s => s.CompanyId == companyId);
            query = query.Where(u => !scopes.Any(s => s.UserId == u) || scopes.Any(s => s.UserId == u && s.WarehouseId == warehouse));
        }

        return await _db.Users
            .Where(u => u.IsActive && query.Contains(u.Id))
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);
    }

    /// <summary>Thêm cùng một thông báo cho nhiều người nhận (bỏ trùng, bỏ người đang thao tác).</summary>
    /// <param name="companyId">Công ty của sự kiện.</param>
    /// <param name="userIds">Người nhận.</param>
    /// <param name="type">Loại (<see cref="NotificationTypes"/>).</param>
    /// <param name="title">Tiêu đề.</param>
    /// <param name="message">Nội dung.</param>
    /// <param name="screenCode">Mã màn hình để mở.</param>
    /// <param name="targetId">Phiếu/kỳ cần mở.</param>
    public void Add(Guid companyId, IEnumerable<Guid> userIds, string type, string title, string message, string screenCode, Guid? targetId)
    {
        var now = _clock.UtcNow;
        foreach (var userId in userIds.Distinct().Where(u => u != _scope.UserId))
            _db.Notifications.Add(Notification.Create(companyId, userId, type, title, message, screenCode, targetId, now));
    }
}
