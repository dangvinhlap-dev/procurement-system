namespace Proc.Application.Organization;

/// <summary>Một kho trên danh sách.</summary>
/// <param name="Id">Id kho.</param>
/// <param name="Code">Mã kho.</param>
/// <param name="Name">Tên kho.</param>
/// <param name="Type">"Main" hoặc "Sub".</param>
/// <param name="ParentWarehouseId">Kho cha (kho Sub).</param>
/// <param name="IsActive">Còn sử dụng.</param>
public sealed record WarehouseDto(Guid Id, string Code, string Name, string Type, Guid? ParentWarehouseId, bool IsActive);

/// <summary>Một nhóm hàng trên danh sách.</summary>
/// <param name="Id">Id nhóm.</param>
/// <param name="Code">Mã nhóm.</param>
/// <param name="Name">Tên nhóm.</param>
/// <param name="ParentId">Nhóm cha.</param>
/// <param name="IsActive">Còn sử dụng.</param>
public sealed record ItemCategoryDto(Guid Id, string Code, string Name, Guid? ParentId, bool IsActive);

/// <summary>
/// Truy vấn danh mục tổ chức. Kết quả đã được global query filter lọc theo phạm vi của người dùng
/// (công ty đang làm việc, kho, nhóm hàng được phép).
/// </summary>
public interface IOrganizationQueries
{
    /// <summary>
    /// Danh sách kho của công ty đang làm việc trong phạm vi được phép, kho tổng trước rồi theo mã.
    /// </summary>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Danh sách kho.</returns>
    Task<IReadOnlyList<WarehouseDto>> GetWarehousesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Danh sách nhóm hàng trong phạm vi được phép, theo mã.
    /// </summary>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Danh sách nhóm hàng.</returns>
    Task<IReadOnlyList<ItemCategoryDto>> GetItemCategoriesAsync(CancellationToken cancellationToken);
}
