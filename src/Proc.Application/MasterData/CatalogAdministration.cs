namespace Proc.Application.MasterData;

/// <summary>Đơn vị tính trên danh sách.</summary>
/// <param name="Id">Id.</param>
/// <param name="Code">Mã (không đổi được).</param>
/// <param name="Name">Tên.</param>
/// <param name="IsActive">Còn sử dụng.</param>
/// <param name="ItemCount">Số mặt hàng (trong phạm vi người xem) dùng làm ĐVT cơ sở.</param>
public sealed record UomDto(Guid Id, string Code, string Name, bool IsActive, int ItemCount);

/// <summary>Thêm đơn vị tính.</summary>
/// <param name="Code">Mã (1–10 chữ không dấu hoặc số).</param>
/// <param name="Name">Tên.</param>
public sealed record CreateUomRequest(string Code, string Name);

/// <summary>Sửa đơn vị tính.</summary>
/// <param name="Name">Tên.</param>
/// <param name="IsActive">Còn sử dụng.</param>
public sealed record UpdateUomRequest(string Name, bool IsActive);

/// <summary>Nhóm hàng trên danh sách và trong ngăn kéo chi tiết.</summary>
/// <param name="Id">Id.</param>
/// <param name="Code">Mã (không đổi được).</param>
/// <param name="Name">Tên.</param>
/// <param name="ParentId">Nhóm cha.</param>
/// <param name="ParentCode">Mã nhóm cha (null nếu là nhóm gốc hoặc nhóm cha ngoài phạm vi người xem).</param>
/// <param name="Path">Đường dẫn từ nhóm gốc, ví dụ "VT / VTC / VTCK" – để danh sách đọc như cây.</param>
/// <param name="Level">Cấp trong cây, nhóm gốc = 1.</param>
/// <param name="InventoryAccount">Tài khoản kho mặc định.</param>
/// <param name="IsActive">Còn sử dụng.</param>
/// <param name="ItemCount">Số mặt hàng trực tiếp trong nhóm.</param>
public sealed record ItemCategoryDetailDto(
    Guid Id, string Code, string Name, Guid? ParentId, string? ParentCode, string Path, int Level,
    string? InventoryAccount, bool IsActive, int ItemCount);

/// <summary>Thêm nhóm hàng.</summary>
/// <param name="Code">Mã (2–20 chữ không dấu hoặc số), duy nhất toàn tập đoàn.</param>
/// <param name="Name">Tên.</param>
/// <param name="ParentId">Nhóm cha; null = nhóm gốc.</param>
/// <param name="InventoryAccount">Tài khoản kho mặc định.</param>
public sealed record CreateItemCategoryRequest(string Code, string Name, Guid? ParentId, string? InventoryAccount);

/// <summary>Sửa nhóm hàng.</summary>
/// <param name="Name">Tên.</param>
/// <param name="ParentId">Nhóm cha; null = nhóm gốc.</param>
/// <param name="InventoryAccount">Tài khoản kho mặc định.</param>
/// <param name="IsActive">Còn sử dụng.</param>
public sealed record UpdateItemCategoryRequest(string Name, Guid? ParentId, string? InventoryAccount, bool IsActive);

/// <summary>
/// Quản lý đơn vị tính (design.md mục 5.2, 5.7). Danh mục dùng chung: chỉ thêm, sửa khi làm việc ở tổng công ty
/// và được mọi nhóm hàng (ngừng dùng ĐVT phải biết mọi mặt hàng đang dùng nó).
/// </summary>
public interface IUomAdministration
{
    /// <summary>Danh sách đơn vị tính theo mã.</summary>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Danh sách.</returns>
    Task<IReadOnlyList<UomDto>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Thêm đơn vị tính.</summary>
    /// <param name="request">Thông tin.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Đơn vị tính vừa thêm.</returns>
    /// <exception cref="Domain.Common.BusinessRuleException">Khi trùng mã hoặc dữ liệu sai.</exception>
    /// <exception cref="Common.ForbiddenAccessException">Khi không ở tổng công ty hoặc bị giới hạn nhóm hàng.</exception>
    Task<UomDto> CreateAsync(CreateUomRequest request, CancellationToken cancellationToken);

    /// <summary>Sửa tên, ngừng dùng hoặc dùng lại.</summary>
    /// <param name="id">Id.</param>
    /// <param name="request">Thông tin mới.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Đơn vị tính sau khi sửa.</returns>
    /// <exception cref="Domain.Common.BusinessRuleException">Khi ngừng dùng ĐVT đang là ĐVT cơ sở của hàng đang dùng.</exception>
    /// <exception cref="Common.ForbiddenAccessException">Khi không ở tổng công ty hoặc bị giới hạn nhóm hàng.</exception>
    /// <exception cref="Common.NotFoundException">Khi không có.</exception>
    Task<UomDto> UpdateAsync(Guid id, UpdateUomRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Quản lý nhóm hàng dạng cây (design.md mục 5.2, 5.7). Chỉ thấy nhóm trong phạm vi nhóm hàng; thêm, sửa ở tổng công ty.
/// Thêm nhóm gốc hoặc đổi nhóm cha cần phạm vi mọi nhóm hàng (người bị giới hạn chỉ thấy một phần cây).
/// </summary>
public interface IItemCategoryAdministration
{
    /// <summary>Danh sách nhóm trong phạm vi, xếp theo đường dẫn để đọc như cây.</summary>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Danh sách.</returns>
    Task<IReadOnlyList<ItemCategoryDetailDto>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Thêm nhóm hàng.</summary>
    /// <param name="request">Thông tin.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Nhóm vừa thêm.</returns>
    /// <exception cref="Domain.Common.BusinessRuleException">Khi trùng mã, dữ liệu sai hoặc nhóm cha đã ngừng dùng.</exception>
    /// <exception cref="Common.ForbiddenAccessException">Khi không ở tổng công ty, hoặc thêm nhóm gốc khi bị giới hạn nhóm hàng.</exception>
    /// <exception cref="Common.NotFoundException">Khi nhóm cha không có hoặc ngoài phạm vi.</exception>
    Task<ItemCategoryDetailDto> CreateAsync(CreateItemCategoryRequest request, CancellationToken cancellationToken);

    /// <summary>Sửa, đổi nhóm cha, ngừng dùng hoặc dùng lại.</summary>
    /// <param name="id">Id.</param>
    /// <param name="request">Thông tin mới.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Nhóm sau khi sửa.</returns>
    /// <exception cref="Domain.Common.BusinessRuleException">Khi tạo vòng, hoặc ngừng dùng nhóm còn nhóm con/hàng đang dùng.</exception>
    /// <exception cref="Common.ForbiddenAccessException">Khi không ở tổng công ty, hoặc đổi nhóm cha khi bị giới hạn nhóm hàng.</exception>
    /// <exception cref="Common.NotFoundException">Khi không có hoặc ngoài phạm vi.</exception>
    Task<ItemCategoryDetailDto> UpdateAsync(Guid id, UpdateItemCategoryRequest request, CancellationToken cancellationToken);
}
