namespace Proc.Application.MasterData;

/// <summary>Nhóm NCC trên danh sách.</summary>
/// <param name="Id">Id.</param>
/// <param name="Code">Mã (không đổi được).</param>
/// <param name="Name">Tên.</param>
/// <param name="IsActive">Còn sử dụng.</param>
/// <param name="SupplierCount">Số NCC trong nhóm.</param>
public sealed record SupplierGroupDto(Guid Id, string Code, string Name, bool IsActive, int SupplierCount);

/// <summary>Thêm nhóm NCC.</summary>
/// <param name="Code">Mã (2–20 chữ không dấu hoặc số).</param>
/// <param name="Name">Tên.</param>
public sealed record CreateSupplierGroupRequest(string? Code, string Name);

/// <summary>Sửa nhóm NCC.</summary>
/// <param name="Name">Tên.</param>
/// <param name="IsActive">Còn sử dụng.</param>
public sealed record UpdateSupplierGroupRequest(string Name, bool IsActive);

/// <summary>Một dòng trên danh sách NCC.</summary>
/// <param name="Id">Id.</param>
/// <param name="Code">Mã NCC.</param>
/// <param name="Name">Tên.</param>
/// <param name="ShortName">Tên viết tắt.</param>
/// <param name="GroupCode">Mã nhóm NCC.</param>
/// <param name="GroupName">Tên nhóm NCC.</param>
/// <param name="IsForeign">NCC nước ngoài.</param>
/// <param name="TaxCode">Mã số thuế.</param>
/// <param name="Phone">Điện thoại.</param>
/// <param name="Email">Email.</param>
/// <param name="Address">Địa chỉ.</param>
/// <param name="PrimaryContact">Người liên hệ chính (họ tên – điện thoại).</param>
/// <param name="Status">Potential, Approved, Suspended, Blacklisted.</param>
/// <param name="Rating">Xếp hạng 1–5.</param>
/// <param name="InternalCompanyCode">Mã công ty thành viên nếu là NCC nội bộ.</param>
/// <param name="AppliedAtCompany">Công ty đang làm việc đang giao dịch với NCC này (SupplierCompany đang dùng).</param>
/// <param name="LocalCode">Mã NCC tại công ty đang làm việc.</param>
public sealed record SupplierListItemDto(
    Guid Id, string Code, string Name, string? ShortName, string GroupCode, string GroupName, bool IsForeign, string? TaxCode,
    string? Phone, string? Email, string? Address, string? PrimaryContact, string Status, int? Rating, string? InternalCompanyCode,
    bool AppliedAtCompany, string? LocalCode);

/// <summary>Kết quả tìm NCC; mỗi lần tối đa <see cref="Limit"/> dòng.</summary>
/// <param name="Items">Các dòng theo mã.</param>
/// <param name="Truncated">true khi còn dòng khớp chưa trả.</param>
/// <param name="Limit">Số dòng tối đa.</param>
public sealed record SupplierListResult(IReadOnlyList<SupplierListItemDto> Items, bool Truncated, int Limit);

/// <summary>Điều kiện tìm NCC.</summary>
/// <param name="Search">Từ khóa (mã, tên, tên viết tắt, mã số thuế, điện thoại), có dấu hay không dấu.</param>
/// <param name="GroupId">Nhóm NCC.</param>
/// <param name="Status">Trạng thái.</param>
/// <param name="AppliedOnly">Chỉ NCC công ty đang làm việc đang giao dịch (ô chọn NCC trên chứng từ mua hàng).</param>
public sealed record SupplierQuery(string? Search, Guid? GroupId, string? Status, bool AppliedOnly = false);

/// <summary>Người liên hệ (đọc ra và gửi lên; Id trống = dòng mới).</summary>
/// <param name="Id">Id dòng.</param>
/// <param name="Name">Họ tên.</param>
/// <param name="Position">Chức vụ.</param>
/// <param name="Phone">Điện thoại.</param>
/// <param name="Email">Email.</param>
/// <param name="IsPrimary">Liên hệ chính.</param>
public sealed record SupplierContactDto(Guid? Id, string Name, string? Position, string? Phone, string? Email, bool IsPrimary);

/// <summary>Tài khoản ngân hàng (đọc ra và gửi lên; Id trống = dòng mới).</summary>
/// <param name="Id">Id dòng.</param>
/// <param name="BankName">Ngân hàng.</param>
/// <param name="Branch">Chi nhánh.</param>
/// <param name="AccountNumber">Số tài khoản.</param>
/// <param name="AccountName">Chủ tài khoản.</param>
/// <param name="IsDefault">Mặc định khi thanh toán.</param>
public sealed record SupplierBankAccountDto(Guid? Id, string BankName, string? Branch, string AccountNumber, string? AccountName, bool IsDefault);

/// <summary>Chi tiết NCC.</summary>
/// <param name="Id">Id.</param>
/// <param name="Code">Mã (không đổi được).</param>
/// <param name="GroupId">Nhóm NCC.</param>
/// <param name="Name">Tên.</param>
/// <param name="ShortName">Tên viết tắt.</param>
/// <param name="IsForeign">NCC nước ngoài.</param>
/// <param name="TaxCode">Mã số thuế.</param>
/// <param name="Address">Địa chỉ.</param>
/// <param name="Phone">Điện thoại.</param>
/// <param name="Email">Email.</param>
/// <param name="Website">Website.</param>
/// <param name="PaymentTerms">Điều khoản thanh toán.</param>
/// <param name="SupplyScope">Ngành hàng cung cấp.</param>
/// <param name="Rating">Xếp hạng.</param>
/// <param name="Status">Trạng thái.</param>
/// <param name="StatusNote">Lý do tạm ngưng/danh sách đen.</param>
/// <param name="InternalCompanyId">Công ty thành viên (NCC nội bộ).</param>
/// <param name="Contacts">Người liên hệ.</param>
/// <param name="BankAccounts">Tài khoản ngân hàng.</param>
/// <param name="CanEdit">Người xem sửa được (có quyền, đang ở tổng công ty).</param>
public sealed record SupplierDetailDto(
    Guid Id, string Code, Guid GroupId, string Name, string? ShortName, bool IsForeign, string? TaxCode, string? Address,
    string? Phone, string? Email, string? Website, string? PaymentTerms, string? SupplyScope, int? Rating, string Status,
    string? StatusNote, Guid? InternalCompanyId, IReadOnlyList<SupplierContactDto> Contacts,
    IReadOnlyList<SupplierBankAccountDto> BankAccounts, bool CanEdit);

/// <summary>Thông tin NCC gửi lên khi thêm (kèm mã) hoặc sửa.</summary>
/// <param name="Code">Mã NCC (chỉ dùng khi thêm; khi sửa bỏ qua).</param>
/// <param name="GroupId">Nhóm NCC.</param>
/// <param name="Name">Tên.</param>
/// <param name="ShortName">Tên viết tắt.</param>
/// <param name="IsForeign">NCC nước ngoài.</param>
/// <param name="TaxCode">Mã số thuế.</param>
/// <param name="Address">Địa chỉ.</param>
/// <param name="Phone">Điện thoại.</param>
/// <param name="Email">Email.</param>
/// <param name="Website">Website.</param>
/// <param name="PaymentTerms">Điều khoản thanh toán.</param>
/// <param name="SupplyScope">Ngành hàng cung cấp.</param>
/// <param name="Rating">Xếp hạng 1–5.</param>
/// <param name="Status">Trạng thái.</param>
/// <param name="StatusNote">Lý do (bắt buộc khi Tạm ngưng, Danh sách đen).</param>
/// <param name="InternalCompanyId">Công ty thành viên nếu là NCC nội bộ.</param>
/// <param name="Contacts">Toàn bộ người liên hệ.</param>
/// <param name="BankAccounts">Toàn bộ tài khoản ngân hàng.</param>
public sealed record SaveSupplierRequest(
    string? Code, Guid GroupId, string Name, string? ShortName, bool IsForeign, string? TaxCode, string? Address,
    string? Phone, string? Email, string? Website, string? PaymentTerms, string? SupplyScope, int? Rating, string Status,
    string? StatusNote, Guid? InternalCompanyId, IReadOnlyList<SupplierContactDto> Contacts,
    IReadOnlyList<SupplierBankAccountDto> BankAccounts);

/// <summary>NCC cho ô chọn NCC trên chứng từ.</summary>
/// <param name="Id">Id.</param>
/// <param name="Code">Mã NCC.</param>
/// <param name="Name">Tên NCC.</param>
/// <param name="TaxCode">Mã số thuế.</param>
public sealed record SupplierLookupDto(Guid Id, string Code, string Name, string? TaxCode);

/// <summary>
/// Quản lý nhóm NCC (design.md mục 5.3, 5.8). Danh mục dùng chung: thêm, sửa ở tổng công ty.
/// </summary>
public interface ISupplierGroupAdministration
{
    /// <summary>Danh sách nhóm NCC theo mã.</summary>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Danh sách.</returns>
    Task<IReadOnlyList<SupplierGroupDto>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Thêm nhóm NCC.</summary>
    /// <param name="request">Thông tin.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Nhóm vừa thêm.</returns>
    /// <exception cref="Domain.Common.BusinessRuleException">Khi trùng mã hoặc dữ liệu sai.</exception>
    /// <exception cref="Common.ForbiddenAccessException">Khi không ở tổng công ty.</exception>
    Task<SupplierGroupDto> CreateAsync(CreateSupplierGroupRequest request, CancellationToken cancellationToken);

    /// <summary>Sửa, ngừng dùng hoặc dùng lại.</summary>
    /// <param name="id">Id.</param>
    /// <param name="request">Thông tin mới.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Nhóm sau khi sửa.</returns>
    /// <exception cref="Domain.Common.BusinessRuleException">Khi ngừng nhóm còn NCC đang giao dịch.</exception>
    /// <exception cref="Common.ForbiddenAccessException">Khi không ở tổng công ty.</exception>
    /// <exception cref="Common.NotFoundException">Khi không có.</exception>
    Task<SupplierGroupDto> UpdateAsync(Guid id, UpdateSupplierGroupRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Quản lý nhà cung cấp dùng chung (design.md mục 5.3, 5.8): thêm, sửa ở tổng công ty; mọi công ty có quyền xem đều xem được.
/// </summary>
public interface ISupplierAdministration
{
    /// <summary>Tìm NCC.</summary>
    /// <param name="query">Điều kiện.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Tối đa <see cref="SupplierListResult.Limit"/> dòng theo mã.</returns>
    Task<SupplierListResult> SearchAsync(SupplierQuery query, CancellationToken cancellationToken);

    /// <summary>
    /// NCC chọn được trên chứng từ mua/nhập của công ty đang làm việc: đã duyệt và đang áp dụng tại công ty (design.md mục 6.1, 5.9).
    /// </summary>
    /// <param name="ids">Thêm các NCC theo Id dù không còn thỏa điều kiện (hiển thị lại phiếu cũ).</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Theo mã NCC.</returns>
    Task<IReadOnlyList<SupplierLookupDto>> LookupForPurchaseAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken);

    /// <summary>Chi tiết NCC.</summary>
    /// <param name="id">Id.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Chi tiết.</returns>
    /// <exception cref="Common.NotFoundException">Khi không có.</exception>
    Task<SupplierDetailDto> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Thêm NCC.</summary>
    /// <param name="request">Thông tin (bắt buộc có mã).</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Chi tiết NCC vừa thêm.</returns>
    /// <exception cref="Domain.Common.BusinessRuleException">Khi trùng mã/mã số thuế/công ty nội bộ hoặc dữ liệu sai.</exception>
    /// <exception cref="Common.ForbiddenAccessException">Khi không ở tổng công ty.</exception>
    Task<SupplierDetailDto> CreateAsync(SaveSupplierRequest request, CancellationToken cancellationToken);

    /// <summary>Sửa NCC (mọi trường trừ mã).</summary>
    /// <param name="id">Id.</param>
    /// <param name="request">Thông tin mới.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Chi tiết sau khi sửa.</returns>
    /// <exception cref="Domain.Common.BusinessRuleException">Khi dữ liệu sai.</exception>
    /// <exception cref="Common.ForbiddenAccessException">Khi không ở tổng công ty.</exception>
    /// <exception cref="Common.NotFoundException">Khi không có.</exception>
    Task<SupplierDetailDto> UpdateAsync(Guid id, SaveSupplierRequest request, CancellationToken cancellationToken);
}
