namespace Proc.Application.Organization;

/// <summary>Kho trên danh sách và trong ngăn kéo chi tiết.</summary>
/// <param name="Id">Id kho.</param>
/// <param name="Code">Mã kho (không đổi được).</param>
/// <param name="Name">Tên kho.</param>
/// <param name="Type">"Main" hoặc "Sub".</param>
/// <param name="ParentWarehouseId">Kho cha (kho Sub).</param>
/// <param name="ParentCode">Mã kho cha.</param>
/// <param name="Address">Địa chỉ.</param>
/// <param name="Keeper">Thủ kho.</param>
/// <param name="AllowNegativeStock">Cho phép tồn âm.</param>
/// <param name="IsActive">Còn sử dụng.</param>
public sealed record WarehouseDetailDto(
    Guid Id, string Code, string Name, string Type, Guid? ParentWarehouseId, string? ParentCode,
    string? Address, string? Keeper, bool AllowNegativeStock, bool IsActive);

/// <summary>Thêm kho phụ tại công ty đang làm việc (kho tổng tạo cùng công ty).</summary>
/// <param name="Code">Mã kho (2–20 chữ không dấu hoặc số), duy nhất trong công ty, không đổi được.</param>
/// <param name="Name">Tên kho.</param>
/// <param name="ParentWarehouseId">Kho cha cùng công ty.</param>
/// <param name="Address">Địa chỉ.</param>
/// <param name="Keeper">Thủ kho.</param>
/// <param name="AllowNegativeStock">Cho phép tồn âm.</param>
public sealed record CreateWarehouseRequest(
    string Code, string Name, Guid ParentWarehouseId, string? Address, string? Keeper, bool AllowNegativeStock);

/// <summary>Sửa kho.</summary>
/// <param name="Name">Tên kho.</param>
/// <param name="ParentWarehouseId">Kho cha mới (chỉ kho phụ; kho tổng gửi null).</param>
/// <param name="Address">Địa chỉ.</param>
/// <param name="Keeper">Thủ kho.</param>
/// <param name="AllowNegativeStock">Cho phép tồn âm.</param>
/// <param name="IsActive">Còn sử dụng.</param>
public sealed record UpdateWarehouseRequest(
    string Name, Guid? ParentWarehouseId, string? Address, string? Keeper, bool AllowNegativeStock, bool IsActive);

/// <summary>
/// Quản lý kho của công ty đang làm việc (design.md mục 4, 5.1). Chỉ thấy và sửa kho trong phạm vi kho được phép;
/// thêm kho cần phạm vi mọi kho, vì kho mới nằm ngoài danh sách kho của người bị giới hạn.
/// </summary>
public interface IWarehouseAdministration
{
    /// <summary>
    /// Danh sách kho trong phạm vi, kho tổng trước rồi theo mã.
    /// </summary>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Danh sách kho.</returns>
    Task<IReadOnlyList<WarehouseDetailDto>> ListAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Chi tiết kho.
    /// </summary>
    /// <param name="id">Id kho.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Chi tiết.</returns>
    /// <exception cref="Common.NotFoundException">Khi không có hoặc ngoài phạm vi.</exception>
    Task<WarehouseDetailDto> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Thêm kho phụ.
    /// </summary>
    /// <param name="request">Thông tin kho.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Kho vừa thêm.</returns>
    /// <exception cref="Domain.Common.BusinessRuleException">Khi trùng mã, dữ liệu sai hoặc kho cha đã ngừng dùng.</exception>
    /// <exception cref="Common.ForbiddenAccessException">Khi bị giới hạn kho.</exception>
    Task<WarehouseDetailDto> CreateAsync(CreateWarehouseRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Sửa kho, đổi kho cha, ngừng dùng hoặc dùng lại.
    /// </summary>
    /// <param name="id">Id kho.</param>
    /// <param name="request">Thông tin mới.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Kho sau khi sửa.</returns>
    /// <exception cref="Domain.Common.BusinessRuleException">Khi dữ liệu sai, kho cha tạo vòng, ngừng dùng kho tổng hoặc kho còn kho con đang dùng.</exception>
    /// <exception cref="Common.NotFoundException">Khi không có hoặc ngoài phạm vi.</exception>
    Task<WarehouseDetailDto> UpdateAsync(Guid id, UpdateWarehouseRequest request, CancellationToken cancellationToken);
}
