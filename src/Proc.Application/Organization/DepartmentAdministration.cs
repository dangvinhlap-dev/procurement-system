using FluentValidation;
using Proc.Domain.Organization;

namespace Proc.Application.Organization;

/// <summary>Bộ phận trên danh sách và trong ngăn kéo chi tiết.</summary>
/// <param name="Id">Id bộ phận.</param>
/// <param name="Code">Mã bộ phận (không đổi được).</param>
/// <param name="Name">Tên bộ phận.</param>
/// <param name="ManagerName">Trưởng bộ phận.</param>
/// <param name="IsActive">Còn sử dụng.</param>
/// <param name="RequestCount">Số PR đã ghi bộ phận này.</param>
/// <param name="DivisionId">Khối (lát O1).</param>
/// <param name="DivisionName">"Mã – tên" khối.</param>
/// <param name="ManagerUserId">Trưởng phòng ban (tài khoản) – người duyệt cấp "Trưởng phòng ban của phiếu".</param>
/// <param name="ManagerUserName">Họ tên Trưởng phòng ban.</param>
/// <param name="DirectorName">Họ tên Giám đốc khối của khối đã gán.</param>
/// <param name="MemberCount">Số người dùng thuộc bộ phận.</param>
public sealed record DepartmentDto(
    Guid Id, string Code, string Name, string? ManagerName, bool IsActive, int RequestCount, Guid? DivisionId = null, string? DivisionName = null,
    Guid? ManagerUserId = null, string? ManagerUserName = null, string? DirectorName = null, int MemberCount = 0);

/// <summary>Thêm bộ phận tại công ty đang làm việc.</summary>
/// <param name="Code">Mã (2–20 chữ không dấu hoặc số), duy nhất trong công ty, không đổi được.</param>
/// <param name="Name">Tên bộ phận.</param>
/// <param name="ManagerName">Trưởng bộ phận (họ tên in).</param>
/// <param name="DivisionId">Khối (lát O1).</param>
/// <param name="ManagerUserId">Trưởng phòng ban (tài khoản có quyền tại công ty).</param>
public sealed record CreateDepartmentRequest(string Code, string Name, string? ManagerName, Guid? DivisionId = null, Guid? ManagerUserId = null);

/// <summary>Sửa bộ phận.</summary>
/// <param name="Name">Tên bộ phận.</param>
/// <param name="ManagerName">Trưởng bộ phận.</param>
/// <param name="IsActive">Còn sử dụng.</param>
/// <param name="DivisionId">Khối (lát O1); null = bỏ gán.</param>
/// <param name="ManagerUserId">Trưởng phòng ban (tài khoản); null = chưa khai.</param>
public sealed record UpdateDepartmentRequest(string Name, string? ManagerName, bool IsActive, Guid? DivisionId = null, Guid? ManagerUserId = null);

/// <summary>Tài khoản chọn được làm Trưởng phòng ban / Giám đốc khối (có vai trò tại công ty đang làm việc, còn hoạt động).</summary>
/// <param name="Id">Id tài khoản.</param>
/// <param name="UserName">Tên đăng nhập.</param>
/// <param name="FullName">Họ tên.</param>
public sealed record OrganizationUserOptionDto(Guid Id, string UserName, string FullName);

/// <summary>Khối trên danh sách và trong ngăn kéo chi tiết (lát O1).</summary>
/// <param name="Id">Id khối.</param>
/// <param name="Code">Mã khối (không đổi được).</param>
/// <param name="Name">Tên khối.</param>
/// <param name="DirectorUserId">Giám đốc khối (tài khoản).</param>
/// <param name="DirectorName">Họ tên Giám đốc khối.</param>
/// <param name="IsActive">Còn sử dụng.</param>
/// <param name="DepartmentCount">Số bộ phận thuộc khối.</param>
public sealed record DivisionDto(Guid Id, string Code, string Name, Guid? DirectorUserId, string? DirectorName, bool IsActive, int DepartmentCount);

/// <summary>Thêm khối tại công ty đang làm việc.</summary>
/// <param name="Code">Mã (2–20 chữ không dấu hoặc số), duy nhất trong công ty.</param>
/// <param name="Name">Tên khối.</param>
/// <param name="DirectorUserId">Giám đốc khối (tài khoản có quyền tại công ty).</param>
public sealed record CreateDivisionRequest(string Code, string Name, Guid? DirectorUserId);

/// <summary>Sửa khối.</summary>
/// <param name="Name">Tên khối.</param>
/// <param name="DirectorUserId">Giám đốc khối; null = chưa khai.</param>
/// <param name="IsActive">Còn sử dụng.</param>
public sealed record UpdateDivisionRequest(string Name, Guid? DirectorUserId, bool IsActive);

/// <summary>
/// Quản lý danh mục khối của công ty đang làm việc (lát O1, design.md mục 19.3). Khối của công ty khác như không tồn tại (global query
/// filter theo công ty). Không xóa: khối không dùng nữa thì ngừng dùng.
/// </summary>
public interface IDivisionAdministration
{
    /// <summary>Danh sách khối của công ty đang làm việc, theo mã (cả khối ngừng dùng).</summary>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Danh sách khối.</returns>
    Task<IReadOnlyList<DivisionDto>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Chi tiết khối.</summary>
    /// <param name="id">Id khối.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Chi tiết.</returns>
    /// <exception cref="Common.NotFoundException">Khi không có hoặc thuộc công ty khác.</exception>
    Task<DivisionDto> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Thêm khối.</summary>
    /// <param name="request">Thông tin khối.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Khối vừa thêm.</returns>
    /// <exception cref="Domain.Common.BusinessRuleException">Khi trùng mã, Giám đốc khối không có quyền tại công ty hoặc dữ liệu sai.</exception>
    Task<DivisionDto> CreateAsync(CreateDivisionRequest request, CancellationToken cancellationToken);

    /// <summary>Sửa khối, ngừng dùng hoặc dùng lại.</summary>
    /// <param name="id">Id khối.</param>
    /// <param name="request">Thông tin mới.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Khối sau khi sửa.</returns>
    /// <exception cref="Domain.Common.BusinessRuleException">Khi dữ liệu sai.</exception>
    /// <exception cref="Common.NotFoundException">Khi không có hoặc thuộc công ty khác.</exception>
    Task<DivisionDto> UpdateAsync(Guid id, UpdateDivisionRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Quản lý danh mục bộ phận của công ty đang làm việc (design.md mục 5.1, 18.5). Bộ phận của công ty khác như không tồn tại
/// (global query filter theo công ty). Không xóa: bộ phận không dùng nữa thì ngừng dùng, PR cũ vẫn hiện tên.
/// </summary>
public interface IDepartmentAdministration
{
    /// <summary>
    /// Danh sách bộ phận của công ty đang làm việc, theo mã.
    /// </summary>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Danh sách bộ phận (cả bộ phận ngừng dùng).</returns>
    Task<IReadOnlyList<DepartmentDto>> ListAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Chi tiết bộ phận.
    /// </summary>
    /// <param name="id">Id bộ phận.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Chi tiết.</returns>
    /// <exception cref="Common.NotFoundException">Khi không có hoặc thuộc công ty khác.</exception>
    Task<DepartmentDto> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Thêm bộ phận.
    /// </summary>
    /// <param name="request">Thông tin bộ phận.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Bộ phận vừa thêm.</returns>
    /// <exception cref="Domain.Common.BusinessRuleException">Khi trùng mã hoặc dữ liệu sai.</exception>
    Task<DepartmentDto> CreateAsync(CreateDepartmentRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Sửa bộ phận, ngừng dùng hoặc dùng lại.
    /// </summary>
    /// <param name="id">Id bộ phận.</param>
    /// <param name="request">Thông tin mới.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Bộ phận sau khi sửa.</returns>
    /// <exception cref="Domain.Common.BusinessRuleException">Khi dữ liệu sai.</exception>
    /// <exception cref="Common.NotFoundException">Khi không có hoặc thuộc công ty khác.</exception>
    Task<DepartmentDto> UpdateAsync(Guid id, UpdateDepartmentRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Tài khoản chọn được làm Trưởng phòng ban / Giám đốc khối: còn hoạt động, có ít nhất một vai trò tại công ty đang làm việc.
    /// </summary>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Theo họ tên.</returns>
    Task<IReadOnlyList<OrganizationUserOptionDto>> UserOptionsAsync(CancellationToken cancellationToken);

    /// <summary>Bộ phận của người dùng hiện tại tại công ty đang làm việc (điền sẵn khi lập đề nghị mua) – null nếu chưa gán.</summary>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Id bộ phận (còn sử dụng) hoặc null.</returns>
    Task<Guid?> MineAsync(CancellationToken cancellationToken);
}

/// <summary>Kiểm tra thêm bộ phận. Định dạng mã do Domain kiểm tra (<see cref="Department"/>).</summary>
public sealed class CreateDepartmentRequestValidator : AbstractValidator<CreateDepartmentRequest>
{
    /// <summary>Khai báo các quy tắc.</summary>
    public CreateDepartmentRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().WithMessage("Hãy nhập mã bộ phận.");
        RuleFor(x => x.Name).NotEmpty().WithMessage("Hãy nhập tên bộ phận.").MaximumLength(Department.NameMaxLength);
        RuleFor(x => x.ManagerName).MaximumLength(Department.ManagerNameMaxLength);
    }
}

/// <summary>Kiểm tra sửa bộ phận.</summary>
public sealed class UpdateDepartmentRequestValidator : AbstractValidator<UpdateDepartmentRequest>
{
    /// <summary>Khai báo các quy tắc.</summary>
    public UpdateDepartmentRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Hãy nhập tên bộ phận.").MaximumLength(Department.NameMaxLength);
        RuleFor(x => x.ManagerName).MaximumLength(Department.ManagerNameMaxLength);
    }
}

/// <summary>Kiểm tra thêm khối. Định dạng mã do Domain kiểm tra (<see cref="Division"/>).</summary>
public sealed class CreateDivisionRequestValidator : AbstractValidator<CreateDivisionRequest>
{
    /// <summary>Khai báo các quy tắc.</summary>
    public CreateDivisionRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().WithMessage("Hãy nhập mã khối.");
        RuleFor(x => x.Name).NotEmpty().WithMessage("Hãy nhập tên khối.").MaximumLength(Division.NameMaxLength);
    }
}

/// <summary>Kiểm tra sửa khối.</summary>
public sealed class UpdateDivisionRequestValidator : AbstractValidator<UpdateDivisionRequest>
{
    /// <summary>Khai báo các quy tắc.</summary>
    public UpdateDivisionRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Hãy nhập tên khối.").MaximumLength(Division.NameMaxLength);
    }
}
