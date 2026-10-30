namespace Proc.Application.MasterData;

/// <summary>NCC áp dụng tại công ty đang làm việc.</summary>
/// <param name="SupplierId">NCC.</param>
/// <param name="CompanyCode">Mã công ty đang làm việc.</param>
/// <param name="IsApplied">Công ty đã khai báo NCC này chưa.</param>
/// <param name="LocalCode">Mã NCC tại công ty.</param>
/// <param name="StartDate">Ngày bắt đầu giao dịch.</param>
/// <param name="IsActive">Đang giao dịch.</param>
/// <param name="CanEdit">Người xem sửa được (có quyền quản lý NCC tại công ty này).</param>
public sealed record SupplierCompanyDto(
    Guid SupplierId, string CompanyCode, bool IsApplied, string? LocalCode, DateOnly? StartDate, bool IsActive, bool CanEdit);

/// <summary>Lưu NCC áp dụng tại công ty đang làm việc.</summary>
/// <param name="LocalCode">Mã NCC tại công ty.</param>
/// <param name="StartDate">Ngày bắt đầu giao dịch.</param>
/// <param name="IsActive">Đang giao dịch.</param>
public sealed record SaveSupplierCompanyRequest(string? LocalCode, DateOnly? StartDate, bool IsActive);

/// <summary>Một dòng NCC – sản phẩm.</summary>
/// <param name="Id">Id dòng.</param>
/// <param name="SupplierId">NCC.</param>
/// <param name="SupplierCode">Mã NCC.</param>
/// <param name="SupplierName">Tên NCC.</param>
/// <param name="SupplierStatus">Trạng thái NCC.</param>
/// <param name="ItemId">Mặt hàng.</param>
/// <param name="ItemCode">Mã hàng.</param>
/// <param name="ItemName">Tên hàng.</param>
/// <param name="BaseUomCode">ĐVT cơ sở của mặt hàng.</param>
/// <param name="SupplierItemCode">Mã hàng của NCC.</param>
/// <param name="PurchaseUomId">ĐVT mua.</param>
/// <param name="PurchaseUomCode">Mã ĐVT mua.</param>
/// <param name="Moq">Số lượng đặt tối thiểu.</param>
/// <param name="LeadTimeDays">Thời gian giao hàng (ngày).</param>
/// <param name="IsPreferred">NCC ưu tiên.</param>
public sealed record SupplierItemDto(
    Guid Id, Guid SupplierId, string SupplierCode, string SupplierName, string SupplierStatus, Guid ItemId, string ItemCode,
    string ItemName, string BaseUomCode, string? SupplierItemCode, Guid PurchaseUomId, string PurchaseUomCode, decimal? Moq,
    int? LeadTimeDays, bool IsPreferred);

/// <summary>Thêm/sửa một dòng NCC – sản phẩm.</summary>
/// <param name="ItemId">Mặt hàng (khi sửa phải trùng mặt hàng cũ).</param>
/// <param name="SupplierItemCode">Mã hàng của NCC.</param>
/// <param name="PurchaseUomId">ĐVT mua.</param>
/// <param name="Moq">Số lượng đặt tối thiểu.</param>
/// <param name="LeadTimeDays">Thời gian giao hàng (ngày).</param>
/// <param name="IsPreferred">NCC ưu tiên (chọn thì NCC ưu tiên cũ của mặt hàng tự bỏ).</param>
public sealed record SaveSupplierItemRequest(
    Guid ItemId, string? SupplierItemCode, Guid PurchaseUomId, decimal? Moq, int? LeadTimeDays, bool IsPreferred);

/// <summary>
/// NCC áp dụng cho công ty và NCC – sản phẩm (design.md mục 5.3, 5.9).
/// Áp dụng cho công ty: mỗi công ty tự quản lý tại công ty đang làm việc. NCC – sản phẩm: dùng chung, sửa ở tổng công ty,
/// từng dòng một (không thay cả danh sách) vì người bị giới hạn nhóm hàng không thấy hết các dòng.
/// </summary>
public interface ISupplierLinkAdministration
{
    /// <summary>NCC áp dụng tại công ty đang làm việc.</summary>
    /// <param name="supplierId">NCC.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Thông tin áp dụng (IsApplied = false nếu chưa khai báo).</returns>
    /// <exception cref="Common.NotFoundException">Khi không có NCC.</exception>
    Task<SupplierCompanyDto> GetCompanyLinkAsync(Guid supplierId, CancellationToken cancellationToken);

    /// <summary>Khai báo hoặc sửa NCC áp dụng tại công ty đang làm việc.</summary>
    /// <param name="supplierId">NCC.</param>
    /// <param name="request">Thông tin.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Thông tin sau khi lưu.</returns>
    /// <exception cref="Domain.Common.BusinessRuleException">Khi NCC ở danh sách đen, trùng mã tại công ty.</exception>
    /// <exception cref="Common.NotFoundException">Khi không có NCC.</exception>
    Task<SupplierCompanyDto> SaveCompanyLinkAsync(Guid supplierId, SaveSupplierCompanyRequest request, CancellationToken cancellationToken);

    /// <summary>Sản phẩm/dịch vụ NCC bán (trong phạm vi nhóm hàng người xem).</summary>
    /// <param name="supplierId">NCC.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Theo mã hàng.</returns>
    Task<IReadOnlyList<SupplierItemDto>> ListItemsOfSupplierAsync(Guid supplierId, CancellationToken cancellationToken);

    /// <summary>Các NCC bán một mặt hàng.</summary>
    /// <param name="itemId">Mặt hàng.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>NCC ưu tiên trước, rồi theo mã NCC.</returns>
    Task<IReadOnlyList<SupplierItemDto>> ListSuppliersOfItemAsync(Guid itemId, CancellationToken cancellationToken);

    /// <summary>Thêm mặt hàng NCC bán.</summary>
    /// <param name="supplierId">NCC.</param>
    /// <param name="request">Thông tin.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Dòng vừa thêm.</returns>
    /// <exception cref="Domain.Common.BusinessRuleException">Khi đã có dòng cho mặt hàng này hoặc dữ liệu sai.</exception>
    /// <exception cref="Common.ForbiddenAccessException">Khi không ở tổng công ty.</exception>
    /// <exception cref="Common.NotFoundException">Khi NCC/mặt hàng không có hoặc ngoài phạm vi.</exception>
    Task<SupplierItemDto> CreateItemAsync(Guid supplierId, SaveSupplierItemRequest request, CancellationToken cancellationToken);

    /// <summary>Sửa một dòng NCC – sản phẩm.</summary>
    /// <param name="id">Id dòng.</param>
    /// <param name="request">Thông tin mới.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Dòng sau khi sửa.</returns>
    /// <exception cref="Domain.Common.BusinessRuleException">Khi dữ liệu sai.</exception>
    /// <exception cref="Common.ForbiddenAccessException">Khi không ở tổng công ty.</exception>
    /// <exception cref="Common.NotFoundException">Khi không có hoặc ngoài phạm vi.</exception>
    Task<SupplierItemDto> UpdateItemAsync(Guid id, SaveSupplierItemRequest request, CancellationToken cancellationToken);

    /// <summary>Xóa một dòng NCC – sản phẩm (NCC thôi bán mặt hàng này; nhật ký thao tác giữ lại giá trị cũ).</summary>
    /// <param name="id">Id dòng.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Task hoàn tất khi xong.</returns>
    /// <exception cref="Common.ForbiddenAccessException">Khi không ở tổng công ty.</exception>
    /// <exception cref="Common.NotFoundException">Khi không có hoặc ngoài phạm vi.</exception>
    Task DeleteItemAsync(Guid id, CancellationToken cancellationToken);
}
