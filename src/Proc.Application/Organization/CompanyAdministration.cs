namespace Proc.Application.Organization;

/// <summary>Một dòng trên danh sách công ty.</summary>
/// <param name="Id">Id công ty.</param>
/// <param name="Code">Mã công ty.</param>
/// <param name="Name">Tên đầy đủ.</param>
/// <param name="ShortName">Tên viết tắt.</param>
/// <param name="TaxCode">Mã số thuế.</param>
/// <param name="IsHeadOffice">Là tổng công ty.</param>
/// <param name="IsActive">Còn sử dụng.</param>
/// <param name="UserCount">Số người dùng được gán vào công ty.</param>
public sealed record CompanyListItemDto(
    Guid Id, string Code, string Name, string? ShortName, string? TaxCode, bool IsHeadOffice, bool IsActive, int UserCount);

/// <summary>Chi tiết công ty.</summary>
/// <param name="Id">Id công ty.</param>
/// <param name="Code">Mã công ty (không đổi được).</param>
/// <param name="Name">Tên đầy đủ.</param>
/// <param name="ShortName">Tên viết tắt.</param>
/// <param name="TaxCode">Mã số thuế.</param>
/// <param name="Address">Địa chỉ.</param>
/// <param name="Representative">Người đại diện theo pháp luật.</param>
/// <param name="IsHeadOffice">Là tổng công ty.</param>
/// <param name="AccountingCurrency">Tiền tệ hạch toán.</param>
/// <param name="StartDate">Ngày bắt đầu dùng hệ thống.</param>
/// <param name="IsActive">Còn sử dụng.</param>
/// <param name="CanEdit">Người đang xem sửa được (có quyền quản lý và đang làm việc ở tổng công ty).</param>
public sealed record CompanyDetailDto(
    Guid Id, string Code, string Name, string? ShortName, string? TaxCode, string? Address, string? Representative,
    bool IsHeadOffice, string AccountingCurrency, DateOnly? StartDate, bool IsActive, bool CanEdit);

/// <summary>Thêm công ty thành viên, kèm kho tổng của công ty.</summary>
/// <param name="Code">Mã công ty (2–10 chữ không dấu hoặc số), không đổi được về sau.</param>
/// <param name="Name">Tên đầy đủ.</param>
/// <param name="ShortName">Tên viết tắt.</param>
/// <param name="TaxCode">Mã số thuế.</param>
/// <param name="Address">Địa chỉ.</param>
/// <param name="Representative">Người đại diện.</param>
/// <param name="StartDate">Ngày bắt đầu dùng hệ thống.</param>
/// <param name="MainWarehouseCode">Mã kho tổng.</param>
/// <param name="MainWarehouseName">Tên kho tổng.</param>
public sealed record CreateCompanyRequest(
    string Code, string Name, string? ShortName, string? TaxCode, string? Address, string? Representative,
    DateOnly? StartDate, string MainWarehouseCode, string MainWarehouseName);

/// <summary>Sửa công ty.</summary>
/// <param name="Name">Tên đầy đủ.</param>
/// <param name="ShortName">Tên viết tắt.</param>
/// <param name="TaxCode">Mã số thuế.</param>
/// <param name="Address">Địa chỉ.</param>
/// <param name="Representative">Người đại diện.</param>
/// <param name="StartDate">Ngày bắt đầu dùng hệ thống.</param>
/// <param name="IsActive">Còn sử dụng.</param>
public sealed record UpdateCompanyRequest(
    string Name, string? ShortName, string? TaxCode, string? Address, string? Representative, DateOnly? StartDate, bool IsActive);

/// <summary>
/// Quản lý danh mục công ty (design.md mục 5.1, 5.6). Danh mục tổ chức do tổng công ty quản lý: chỉ thêm, sửa khi đang
/// làm việc ở tổng công ty; ở công ty thành viên chỉ xem được công ty mình.
/// </summary>
public interface ICompanyAdministration
{
    /// <summary>
    /// Danh sách công ty: ở tổng công ty là mọi công ty, ở công ty thành viên chỉ công ty đang làm việc.
    /// </summary>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Tổng công ty đầu tiên, sau đó theo mã.</returns>
    Task<IReadOnlyList<CompanyListItemDto>> ListAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Chi tiết công ty.
    /// </summary>
    /// <param name="id">Id công ty.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Chi tiết.</returns>
    /// <exception cref="Common.NotFoundException">Khi không có hoặc ngoài phạm vi xem.</exception>
    Task<CompanyDetailDto> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Thêm công ty thành viên cùng kho tổng; người thêm được gán các vai trò mình đang có ở tổng công ty tại công ty mới
    /// để vào làm việc được ngay (khai báo kho phụ, gán người dùng).
    /// </summary>
    /// <param name="request">Thông tin công ty.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Chi tiết công ty vừa thêm.</returns>
    /// <exception cref="Domain.Common.BusinessRuleException">Khi trùng mã/mã số thuế hoặc dữ liệu sai.</exception>
    /// <exception cref="Common.ForbiddenAccessException">Khi không làm việc ở tổng công ty.</exception>
    Task<CompanyDetailDto> CreateAsync(CreateCompanyRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Sửa thông tin, ngừng dùng hoặc dùng lại công ty.
    /// </summary>
    /// <param name="id">Id công ty.</param>
    /// <param name="request">Thông tin mới.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Chi tiết sau khi sửa.</returns>
    /// <exception cref="Domain.Common.BusinessRuleException">Khi trùng mã số thuế, dữ liệu sai, hoặc ngừng dùng tổng công ty.</exception>
    /// <exception cref="Common.ForbiddenAccessException">Khi không làm việc ở tổng công ty.</exception>
    /// <exception cref="Common.NotFoundException">Khi không có công ty.</exception>
    Task<CompanyDetailDto> UpdateAsync(Guid id, UpdateCompanyRequest request, CancellationToken cancellationToken);
}
