using FluentValidation;

namespace Proc.Application.MasterData;

/// <summary>Một dải đánh số danh mục.</summary>
/// <param name="Id">Id.</param>
/// <param name="Catalog">Danh mục: supplier, supplier-group, item-category, item.</param>
/// <param name="CatalogName">Tên danh mục.</param>
/// <param name="CategoryId">Nhóm hàng của dải mã hàng; null = mặc định.</param>
/// <param name="CategoryCode">Mã nhóm hàng.</param>
/// <param name="CategoryName">Tên nhóm hàng.</param>
/// <param name="Prefix">Tiền tố (có thể chứa {NHOM}).</param>
/// <param name="Digits">Số chữ số.</param>
/// <param name="FromNumber">Số đầu dải.</param>
/// <param name="ToNumber">Số cuối dải.</param>
/// <param name="Sample">Ví dụ mã đầu dải (tiền tố {NHOM} thay bằng mã nhóm hoặc "VT").</param>
/// <param name="NextCode">Mã sẽ cấp kế tiếp; null với dòng mặc định của hàng hóa (mỗi nhóm một bộ đếm) hoặc khi hết dải.</param>
public sealed record CatalogNumberingDto(
    Guid Id, string Catalog, string CatalogName, Guid? CategoryId, string? CategoryCode, string? CategoryName, string Prefix, int Digits,
    long FromNumber, long ToNumber, string Sample, string? NextCode);

/// <summary>Thêm hoặc sửa một dải đánh số.</summary>
/// <param name="Catalog">Danh mục (chỉ dùng khi thêm).</param>
/// <param name="CategoryId">Nhóm hàng của dải mã hàng (chỉ dùng khi thêm; null = mặc định).</param>
/// <param name="Prefix">Tiền tố.</param>
/// <param name="Digits">Số chữ số.</param>
/// <param name="FromNumber">Số đầu dải.</param>
/// <param name="ToNumber">Số cuối dải.</param>
public sealed record SaveCatalogNumberingRequest(string Catalog, Guid? CategoryId, string? Prefix, int Digits, long FromNumber, long ToNumber);

/// <summary>
/// Cấu hình đánh số danh mục (design.md mục 5.11) – dùng chung toàn tập đoàn, chỉ sửa khi làm việc ở tổng công ty, quyền tham số hệ thống.
/// </summary>
public interface ICatalogNumberingAdministration
{
    /// <summary>Các dải theo danh mục, nhóm hàng.</summary>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Danh sách dải.</returns>
    Task<IReadOnlyList<CatalogNumberingDto>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Thêm dải.</summary>
    /// <param name="request">Nội dung.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Dải vừa thêm.</returns>
    /// <exception cref="Domain.Common.BusinessRuleException">Khi cấu hình không hợp lệ hoặc danh mục/nhóm hàng đã có dải.</exception>
    /// <exception cref="Common.ForbiddenAccessException">Khi không làm việc ở tổng công ty.</exception>
    Task<CatalogNumberingDto> CreateAsync(SaveCatalogNumberingRequest request, CancellationToken cancellationToken);

    /// <summary>Sửa dải (danh mục, nhóm hàng không đổi).</summary>
    /// <param name="id">Dải.</param>
    /// <param name="request">Nội dung.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Dải sau khi sửa.</returns>
    /// <exception cref="Common.NotFoundException">Khi không có dải.</exception>
    /// <exception cref="Domain.Common.BusinessRuleException">Khi cấu hình không hợp lệ.</exception>
    /// <exception cref="Common.ForbiddenAccessException">Khi không làm việc ở tổng công ty.</exception>
    Task<CatalogNumberingDto> UpdateAsync(Guid id, SaveCatalogNumberingRequest request, CancellationToken cancellationToken);

    /// <summary>Xóa dải – danh mục (nhóm hàng) đó không thêm mới được cho tới khi cấu hình lại; mã đã cấp không đổi.</summary>
    /// <param name="id">Dải.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <exception cref="Common.NotFoundException">Khi không có dải.</exception>
    /// <exception cref="Common.ForbiddenAccessException">Khi không làm việc ở tổng công ty.</exception>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}

/// <summary>Kiểm tra nội dung dải gửi lên (quy tắc chi tiết ở domain CatalogNumbering).</summary>
public sealed class SaveCatalogNumberingRequestValidator : AbstractValidator<SaveCatalogNumberingRequest>
{
    /// <summary>Khai báo quy tắc.</summary>
    public SaveCatalogNumberingRequestValidator()
    {
        RuleFor(x => x.Catalog).NotEmpty().WithMessage("Hãy chọn danh mục.");
        RuleFor(x => x.Prefix).MaximumLength(20);
        RuleFor(x => x.Digits).InclusiveBetween(1, 10).WithMessage("Số chữ số từ 1 đến 10.");
        RuleFor(x => x.FromNumber).GreaterThanOrEqualTo(1).WithMessage("Từ số phải từ 1.");
        RuleFor(x => x.ToNumber).GreaterThanOrEqualTo(x => x.FromNumber).WithMessage("Đến số không nhỏ hơn từ số.");
    }
}
