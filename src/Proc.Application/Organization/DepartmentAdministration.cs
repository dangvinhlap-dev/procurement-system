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
public sealed record DepartmentDto(Guid Id, string Code, string Name, string? ManagerName, bool IsActive, int RequestCount);

/// <summary>Thêm bộ phận tại công ty đang làm việc.</summary>
/// <param name="Code">Mã (2–20 chữ không dấu hoặc số), duy nhất trong công ty, không đổi được.</param>
/// <param name="Name">Tên bộ phận.</param>
/// <param name="ManagerName">Trưởng bộ phận.</param>
public sealed record CreateDepartmentRequest(string Code, string Name, string? ManagerName);

/// <summary>Sửa bộ phận.</summary>
/// <param name="Name">Tên bộ phận.</param>
/// <param name="ManagerName">Trưởng bộ phận.</param>
/// <param name="IsActive">Còn sử dụng.</param>
public sealed record UpdateDepartmentRequest(string Name, string? ManagerName, bool IsActive);

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
