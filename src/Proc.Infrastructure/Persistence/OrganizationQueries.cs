using Microsoft.EntityFrameworkCore;
using Proc.Application.Organization;
using Proc.Domain.Organization;

namespace Proc.Infrastructure.Persistence;

/// <summary>
/// Truy vấn danh mục tổ chức qua EF Core. Không tự lọc theo công ty ở đây: global query filter của ProcDbContext
/// đã lọc theo phạm vi, để không có chỗ nào quên lọc.
/// </summary>
public sealed class OrganizationQueries : IOrganizationQueries
{
    private readonly ProcDbContext _db;

    /// <summary>
    /// Tạo đối tượng truy vấn.
    /// </summary>
    /// <param name="db">DbContext.</param>
    public OrganizationQueries(ProcDbContext db) => _db = db;

    /// <inheritdoc />
    public async Task<IReadOnlyList<WarehouseDto>> GetWarehousesAsync(CancellationToken cancellationToken) =>
        await _db.Warehouses
            .AsNoTracking()
            .OrderBy(w => w.Type)
            .ThenBy(w => w.Code)
            .Select(w => new WarehouseDto(
                w.Id, w.Code, w.Name, w.Type == WarehouseType.Main ? "Main" : "Sub", w.ParentWarehouseId, w.IsActive))
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<ItemCategoryDto>> GetItemCategoriesAsync(CancellationToken cancellationToken) =>
        await _db.ItemCategories
            .AsNoTracking()
            .OrderBy(c => c.Code)
            .Select(c => new ItemCategoryDto(c.Id, c.Code, c.Name, c.ParentId, c.IsActive))
            .ToListAsync(cancellationToken);
}
