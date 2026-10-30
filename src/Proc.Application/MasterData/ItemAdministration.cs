namespace Proc.Application.MasterData;

/// <summary>Một dòng trên danh sách hàng hóa.</summary>
/// <param name="Id">Id.</param>
/// <param name="Code">Mã hàng.</param>
/// <param name="Name">Tên hàng.</param>
/// <param name="Type">Loại: Inventory, Service, Asset, Consumable.</param>
/// <param name="CategoryId">Nhóm hàng.</param>
/// <param name="CategoryCode">Mã nhóm hàng.</param>
/// <param name="CategoryName">Tên nhóm hàng.</param>
/// <param name="BaseUomCode">Mã ĐVT cơ sở.</param>
/// <param name="Specification">Quy cách.</param>
/// <param name="Brand">Thương hiệu.</param>
/// <param name="Origin">Xuất xứ.</param>
/// <param name="Barcode">Mã vạch.</param>
/// <param name="VatCode">Thuế suất mặc định.</param>
/// <param name="IsActive">Còn sử dụng.</param>
public sealed record ItemListItemDto(
    Guid Id, string Code, string Name, string Type, Guid CategoryId, string CategoryCode, string CategoryName,
    string BaseUomCode, string? Specification, string? Brand, string? Origin, string? Barcode, string? VatCode, bool IsActive);

/// <summary>Kết quả tìm hàng hóa. Danh mục có thể tới ~100.000 mã nên mỗi lần trả tối đa <see cref="Limit"/> dòng.</summary>
/// <param name="Items">Các dòng, theo mã hàng.</param>
/// <param name="Truncated">true khi còn dòng khớp chưa trả – người dùng cần thu hẹp tìm kiếm.</param>
/// <param name="Limit">Số dòng tối đa mỗi lần.</param>
public sealed record ItemListResult(IReadOnlyList<ItemListItemDto> Items, bool Truncated, int Limit);

/// <summary>Điều kiện tìm hàng hóa.</summary>
/// <param name="Search">Từ khóa (mã, tên, tên tiếng Anh, mã vạch), có dấu hay không dấu đều được; nhiều từ thì phải có đủ các từ.</param>
/// <param name="CategoryId">Nhóm hàng – gồm cả nhóm con.</param>
/// <param name="Type">Loại hàng.</param>
/// <param name="ActiveOnly">Chỉ hàng đang dùng (ô chọn hàng trên chứng từ).</param>
public sealed record ItemQuery(string? Search, Guid? CategoryId, string? Type, bool ActiveOnly);

/// <summary>Một ĐVT quy đổi.</summary>
/// <param name="UomId">ĐVT.</param>
/// <param name="UomCode">Mã ĐVT (chỉ đọc).</param>
/// <param name="UomName">Tên ĐVT (chỉ đọc).</param>
/// <param name="Factor">Số ĐVT cơ sở trong 1 ĐVT này.</param>
public sealed record ItemUomConversionDto(Guid UomId, string UomCode, string UomName, decimal Factor);

/// <summary>ĐVT quy đổi gửi lên khi lưu.</summary>
/// <param name="UomId">ĐVT.</param>
/// <param name="Factor">Số ĐVT cơ sở trong 1 ĐVT này.</param>
public sealed record ItemUomConversionInput(Guid UomId, decimal Factor);

/// <summary>Chi tiết hàng hóa.</summary>
/// <param name="Id">Id.</param>
/// <param name="Code">Mã hàng (không đổi được).</param>
/// <param name="Name">Tên.</param>
/// <param name="EnglishName">Tên tiếng Anh.</param>
/// <param name="Type">Loại hàng.</param>
/// <param name="CategoryId">Nhóm hàng.</param>
/// <param name="BaseUomId">ĐVT cơ sở.</param>
/// <param name="Specification">Quy cách.</param>
/// <param name="Brand">Thương hiệu.</param>
/// <param name="Origin">Xuất xứ.</param>
/// <param name="Barcode">Mã vạch.</param>
/// <param name="VatCode">Thuế suất mặc định.</param>
/// <param name="TechnicalSpecs">Thông số kỹ thuật.</param>
/// <param name="TrackLot">Quản lý lô.</param>
/// <param name="TrackExpiry">Quản lý hạn dùng.</param>
/// <param name="MinStock">Tồn tối thiểu.</param>
/// <param name="MaxStock">Tồn tối đa.</param>
/// <param name="IsActive">Còn sử dụng.</param>
/// <param name="Conversions">ĐVT quy đổi.</param>
/// <param name="CanEdit">Người xem sửa được (có quyền và đang làm việc ở tổng công ty).</param>
public sealed record ItemDetailDto(
    Guid Id, string Code, string Name, string? EnglishName, string Type, Guid CategoryId, Guid BaseUomId,
    string? Specification, string? Brand, string? Origin, string? Barcode, string? VatCode, string? TechnicalSpecs,
    bool TrackLot, bool TrackExpiry, decimal? MinStock, decimal? MaxStock, bool IsActive,
    IReadOnlyList<ItemUomConversionDto> Conversions, bool CanEdit);

/// <summary>Thêm hàng hóa.</summary>
/// <param name="Code">Mã hàng: 2–30 ký tự chữ không dấu, số và - . _ /.</param>
/// <param name="Name">Tên.</param>
/// <param name="EnglishName">Tên tiếng Anh.</param>
/// <param name="Type">Loại hàng.</param>
/// <param name="CategoryId">Nhóm hàng.</param>
/// <param name="BaseUomId">ĐVT cơ sở.</param>
/// <param name="Specification">Quy cách.</param>
/// <param name="Brand">Thương hiệu.</param>
/// <param name="Origin">Xuất xứ.</param>
/// <param name="Barcode">Mã vạch.</param>
/// <param name="VatCode">Thuế suất mặc định.</param>
/// <param name="TechnicalSpecs">Thông số kỹ thuật.</param>
/// <param name="TrackLot">Quản lý lô.</param>
/// <param name="TrackExpiry">Quản lý hạn dùng.</param>
/// <param name="MinStock">Tồn tối thiểu.</param>
/// <param name="MaxStock">Tồn tối đa.</param>
/// <param name="Conversions">ĐVT quy đổi.</param>
public sealed record CreateItemRequest(
    string Code, string Name, string? EnglishName, string Type, Guid CategoryId, Guid BaseUomId,
    string? Specification, string? Brand, string? Origin, string? Barcode, string? VatCode, string? TechnicalSpecs,
    bool TrackLot, bool TrackExpiry, decimal? MinStock, decimal? MaxStock, IReadOnlyList<ItemUomConversionInput> Conversions);

/// <summary>Sửa hàng hóa (mọi trường trừ mã).</summary>
/// <param name="Name">Tên.</param>
/// <param name="EnglishName">Tên tiếng Anh.</param>
/// <param name="Type">Loại hàng.</param>
/// <param name="CategoryId">Nhóm hàng.</param>
/// <param name="BaseUomId">ĐVT cơ sở.</param>
/// <param name="Specification">Quy cách.</param>
/// <param name="Brand">Thương hiệu.</param>
/// <param name="Origin">Xuất xứ.</param>
/// <param name="Barcode">Mã vạch.</param>
/// <param name="VatCode">Thuế suất mặc định.</param>
/// <param name="TechnicalSpecs">Thông số kỹ thuật.</param>
/// <param name="TrackLot">Quản lý lô.</param>
/// <param name="TrackExpiry">Quản lý hạn dùng.</param>
/// <param name="MinStock">Tồn tối thiểu.</param>
/// <param name="MaxStock">Tồn tối đa.</param>
/// <param name="Conversions">Toàn bộ ĐVT quy đổi mới.</param>
/// <param name="IsActive">Còn sử dụng.</param>
public sealed record UpdateItemRequest(
    string Name, string? EnglishName, string Type, Guid CategoryId, Guid BaseUomId,
    string? Specification, string? Brand, string? Origin, string? Barcode, string? VatCode, string? TechnicalSpecs,
    bool TrackLot, bool TrackExpiry, decimal? MinStock, decimal? MaxStock, IReadOnlyList<ItemUomConversionInput> Conversions,
    bool IsActive);

/// <summary>
/// Quản lý hàng hóa – dịch vụ dùng chung (design.md mục 5.2, 5.7). Chỉ thấy hàng thuộc nhóm hàng trong phạm vi
/// (global query filter); thêm, sửa khi làm việc ở tổng công ty.
/// </summary>
public interface IItemAdministration
{
    /// <summary>Tìm hàng hóa.</summary>
    /// <param name="query">Điều kiện.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Tối đa <see cref="ItemListResult.Limit"/> dòng theo mã.</returns>
    Task<ItemListResult> SearchAsync(ItemQuery query, CancellationToken cancellationToken);

    /// <summary>Chi tiết hàng hóa.</summary>
    /// <param name="id">Id.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Chi tiết.</returns>
    /// <exception cref="Common.NotFoundException">Khi không có hoặc ngoài phạm vi nhóm hàng.</exception>
    Task<ItemDetailDto> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Thêm hàng hóa.</summary>
    /// <param name="request">Thông tin.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Chi tiết hàng vừa thêm.</returns>
    /// <exception cref="Domain.Common.BusinessRuleException">Khi trùng mã hoặc dữ liệu sai.</exception>
    /// <exception cref="Common.ForbiddenAccessException">Khi không ở tổng công ty.</exception>
    /// <exception cref="Common.NotFoundException">Khi nhóm hàng hoặc ĐVT không có/ngoài phạm vi.</exception>
    Task<ItemDetailDto> CreateAsync(CreateItemRequest request, CancellationToken cancellationToken);

    /// <summary>Sửa hàng hóa.</summary>
    /// <param name="id">Id.</param>
    /// <param name="request">Thông tin mới.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Chi tiết sau khi sửa.</returns>
    /// <exception cref="Domain.Common.BusinessRuleException">Khi dữ liệu sai.</exception>
    /// <exception cref="Common.ForbiddenAccessException">Khi không ở tổng công ty.</exception>
    /// <exception cref="Common.NotFoundException">Khi không có hoặc ngoài phạm vi.</exception>
    Task<ItemDetailDto> UpdateAsync(Guid id, UpdateItemRequest request, CancellationToken cancellationToken);
}
