using FluentValidation;
using Proc.Domain.Purchasing;

namespace Proc.Application.Purchasing;

/// <summary>Đơn vị mua trên danh sách và trong ngăn kéo chi tiết (lát Y5a, design.md mục 19.3).</summary>
/// <param name="Id">Id.</param>
/// <param name="Code">Mã.</param>
/// <param name="Name">Tên.</param>
/// <param name="Note">Ghi chú.</param>
/// <param name="IsActive">Còn sử dụng.</param>
public sealed record PurchasingUnitDto(Guid Id, string Code, string Name, string? Note, bool IsActive);

/// <summary>Đơn vị mua để chọn trên phiếu (chỉ đơn vị đang dùng).</summary>
/// <param name="Id">Id.</param>
/// <param name="Code">Mã.</param>
/// <param name="Name">Tên.</param>
public sealed record PurchasingUnitOptionDto(Guid Id, string Code, string Name);

/// <summary>Thêm đơn vị mua.</summary>
/// <param name="Code">Mã 2–20 ký tự chữ không dấu hoặc số.</param>
/// <param name="Name">Tên.</param>
/// <param name="Note">Ghi chú.</param>
public sealed record CreatePurchasingUnitRequest(string Code, string Name, string? Note);

/// <summary>Sửa đơn vị mua (mã không đổi).</summary>
/// <param name="Name">Tên.</param>
/// <param name="Note">Ghi chú.</param>
/// <param name="IsActive">Còn sử dụng.</param>
public sealed record UpdatePurchasingUnitRequest(string Name, string? Note, bool IsActive);

/// <summary>Danh mục đơn vị mua của công ty đang làm việc (lát Y5a). Không có lệnh xóa – ngừng dùng thay xóa.</summary>
public interface IPurchasingUnitService
{
    /// <summary>Mọi đơn vị mua (cả ngừng dùng), theo mã.</summary>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Danh sách.</returns>
    Task<IReadOnlyList<PurchasingUnitDto>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Đơn vị mua đang dùng để chọn trên phiếu, theo mã.</summary>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Danh sách chọn.</returns>
    Task<IReadOnlyList<PurchasingUnitOptionDto>> OptionsAsync(CancellationToken cancellationToken);

    /// <summary>Chi tiết một đơn vị mua.</summary>
    /// <param name="id">Id.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Đơn vị mua.</returns>
    /// <exception cref="Common.NotFoundException">Khi không có ở công ty đang làm việc.</exception>
    Task<PurchasingUnitDto> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Thêm đơn vị mua.</summary>
    /// <param name="request">Dữ liệu.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Đơn vị vừa thêm.</returns>
    /// <exception cref="Domain.Common.BusinessRuleException">Khi mã sai quy tắc hoặc đã có ở công ty.</exception>
    Task<PurchasingUnitDto> CreateAsync(CreatePurchasingUnitRequest request, CancellationToken cancellationToken);

    /// <summary>Sửa tên, ghi chú, ngừng dùng / dùng lại.</summary>
    /// <param name="id">Id.</param>
    /// <param name="request">Dữ liệu.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Đơn vị sau khi sửa.</returns>
    /// <exception cref="Common.NotFoundException">Khi không có ở công ty đang làm việc.</exception>
    Task<PurchasingUnitDto> UpdateAsync(Guid id, UpdatePurchasingUnitRequest request, CancellationToken cancellationToken);
}

/// <summary>Kiểm tra thêm đơn vị mua. Định dạng mã do Domain kiểm tra (<see cref="PurchasingUnit"/>).</summary>
public sealed class CreatePurchasingUnitRequestValidator : AbstractValidator<CreatePurchasingUnitRequest>
{
    /// <summary>Khai báo quy tắc.</summary>
    public CreatePurchasingUnitRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().WithMessage("Hãy nhập mã đơn vị mua.").MaximumLength(20);
        RuleFor(x => x.Name).NotEmpty().WithMessage("Hãy nhập tên đơn vị mua.").MaximumLength(PurchasingUnit.NameMaxLength);
        RuleFor(x => x.Note).MaximumLength(PurchasingUnit.NoteMaxLength);
    }
}

/// <summary>Kiểm tra sửa đơn vị mua.</summary>
public sealed class UpdatePurchasingUnitRequestValidator : AbstractValidator<UpdatePurchasingUnitRequest>
{
    /// <summary>Khai báo quy tắc.</summary>
    public UpdatePurchasingUnitRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Hãy nhập tên đơn vị mua.").MaximumLength(PurchasingUnit.NameMaxLength);
        RuleFor(x => x.Note).MaximumLength(PurchasingUnit.NoteMaxLength);
    }
}
