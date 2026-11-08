using Proc.Application.Common;

namespace Proc.Infrastructure.Security;

/// <summary>
/// Phạm vi của người dùng trong một request (scoped). Mặc định là "không được gì": chưa nạp thì global query filter
/// không trả dòng nào – an toàn khi quên nạp, thay vì lộ dữ liệu. UserScopeLoader nạp đầy đủ ở đầu mỗi request.
/// </summary>
public sealed class CurrentUserScope : ICurrentUserScope
{
    private static readonly IReadOnlySet<string> NoPermissions = new HashSet<string>();

    /// <inheritdoc />
    public Guid? UserId { get; private set; }

    /// <inheritdoc />
    public Guid? CompanyId { get; private set; }

    /// <inheritdoc />
    public IReadOnlySet<string> Permissions { get; private set; } = NoPermissions;

    /// <inheritdoc />
    public bool AllWarehouses { get; private set; }

    /// <inheritdoc />
    public IReadOnlyList<Guid> WarehouseIds { get; private set; } = [];

    /// <inheritdoc />
    public bool AllItemCategories { get; private set; }

    /// <inheritdoc />
    public IReadOnlyList<Guid> ItemCategoryIds { get; private set; } = [];

    /// <summary>
    /// Gán phạm vi đã nạp từ CSDL.
    /// </summary>
    /// <param name="userId">Người dùng.</param>
    /// <param name="companyId">Công ty đang làm việc; null nếu chưa chọn.</param>
    /// <param name="permissions">Quyền tại công ty.</param>
    /// <param name="allWarehouses">Được mọi kho của công ty.</param>
    /// <param name="warehouseIds">Kho được phép khi không được mọi kho.</param>
    /// <param name="allItemCategories">Được mọi nhóm hàng.</param>
    /// <param name="itemCategoryIds">Nhóm hàng được phép (đã gồm nhóm con).</param>
    public void Initialize(
        Guid userId,
        Guid? companyId,
        IEnumerable<string> permissions,
        bool allWarehouses,
        IReadOnlyList<Guid> warehouseIds,
        bool allItemCategories,
        IReadOnlyList<Guid> itemCategoryIds)
    {
        UserId = userId;
        CompanyId = companyId;
        Permissions = new HashSet<string>(permissions, StringComparer.Ordinal);
        AllWarehouses = allWarehouses;
        WarehouseIds = warehouseIds;
        AllItemCategories = allItemCategories;
        ItemCategoryIds = itemCategoryIds;
    }

    /// <summary>Chụp phạm vi hiện tại để trả lại sau khi chạy tạm ở công ty khác (<see cref="CompanyScopeRunner"/>).</summary>
    /// <returns>Bản chụp.</returns>
    internal ScopeSnapshot Capture() => new(UserId, CompanyId, Permissions, AllWarehouses, WarehouseIds, AllItemCategories, ItemCategoryIds);

    /// <summary>Trả lại phạm vi đã chụp.</summary>
    /// <param name="snapshot">Bản chụp từ <see cref="Capture"/>.</param>
    internal void Restore(ScopeSnapshot snapshot)
    {
        UserId = snapshot.UserId;
        CompanyId = snapshot.CompanyId;
        Permissions = snapshot.Permissions;
        AllWarehouses = snapshot.AllWarehouses;
        WarehouseIds = snapshot.WarehouseIds;
        AllItemCategories = snapshot.AllItemCategories;
        ItemCategoryIds = snapshot.ItemCategoryIds;
    }

    /// <inheritdoc />
    public bool HasPermission(string permission) => CompanyId is not null && Permissions.Contains(permission);

    /// <inheritdoc />
    public Guid RequireCompany() =>
        CompanyId ?? throw new ForbiddenAccessException("Chưa chọn công ty làm việc.");

    /// <inheritdoc />
    public void EnsurePermission(string permission)
    {
        RequireCompany();
        if (!Permissions.Contains(permission))
            throw new ForbiddenAccessException($"Bạn không có quyền thực hiện chức năng này ({permission}).");
    }

    /// <inheritdoc />
    public void EnsureCompany(Guid companyId)
    {
        // Mỗi công ty là pháp nhân độc lập: chỉ ghi vào công ty đang làm việc – CLAUDE.md mục 4.
        if (RequireCompany() != companyId)
            throw new ForbiddenAccessException("Dữ liệu thuộc công ty khác với công ty đang làm việc.");
    }

    /// <inheritdoc />
    public void EnsureWarehouse(Guid warehouseId)
    {
        RequireCompany();
        if (!AllWarehouses && !WarehouseIds.Contains(warehouseId))
            throw new ForbiddenAccessException("Bạn không được làm việc với kho này.");
    }
}

/// <summary>Bản chụp phạm vi của <see cref="CurrentUserScope"/>.</summary>
internal sealed record ScopeSnapshot(
    Guid? UserId, Guid? CompanyId, IReadOnlySet<string> Permissions, bool AllWarehouses, IReadOnlyList<Guid> WarehouseIds,
    bool AllItemCategories, IReadOnlyList<Guid> ItemCategoryIds);
