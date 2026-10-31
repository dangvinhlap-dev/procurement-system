using FluentValidation;

namespace Proc.Application.MasterData;

/// <summary>Một hạng xếp loại NCC.</summary>
/// <param name="Id">Id.</param>
/// <param name="Code">Mã hạng.</param>
/// <param name="Name">Tên hạng.</param>
/// <param name="SortOrder">Thứ tự xét (nhỏ = tốt hơn).</param>
/// <param name="MinOnTimePct">% giao đúng hạn tối thiểu.</param>
/// <param name="MaxReturnPct">% trả hàng tối đa.</param>
public sealed record SupplierRatingRuleDto(Guid Id, string Code, string Name, int SortOrder, decimal MinOnTimePct, decimal MaxReturnPct);

/// <summary>Thêm hoặc sửa một hạng.</summary>
/// <param name="Code">Mã hạng (chỉ dùng khi thêm; không đổi được sau khi tạo).</param>
/// <param name="Name">Tên hạng.</param>
/// <param name="SortOrder">Thứ tự xét.</param>
/// <param name="MinOnTimePct">% giao đúng hạn tối thiểu (0–100).</param>
/// <param name="MaxReturnPct">% trả hàng tối đa (0–100).</param>
public sealed record SaveSupplierRatingRuleRequest(string Code, string Name, int SortOrder, decimal MinOnTimePct, decimal MaxReturnPct);

/// <summary>
/// Cấu hình hạng xếp loại NCC (design.md mục 11.3) – dùng chung toàn tập đoàn, chỉ sửa khi làm việc ở tổng công ty. Mỗi lần thêm/sửa
/// kiểm tra lại cả bộ hạng.
/// </summary>
public interface ISupplierRatingAdministration
{
    /// <summary>Các hạng theo thứ tự xét.</summary>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Danh sách hạng.</returns>
    Task<IReadOnlyList<SupplierRatingRuleDto>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Thêm hạng.</summary>
    /// <param name="request">Nội dung.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Hạng vừa thêm.</returns>
    /// <exception cref="Domain.Common.BusinessRuleException">Khi hạng hoặc cả bộ hạng không hợp lệ.</exception>
    /// <exception cref="Common.ForbiddenAccessException">Khi không làm việc ở tổng công ty.</exception>
    Task<SupplierRatingRuleDto> CreateAsync(SaveSupplierRatingRuleRequest request, CancellationToken cancellationToken);

    /// <summary>Sửa hạng.</summary>
    /// <param name="id">Hạng.</param>
    /// <param name="request">Nội dung (mã bỏ qua).</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Hạng sau khi sửa.</returns>
    /// <exception cref="Common.NotFoundException">Khi không có hạng.</exception>
    /// <exception cref="Domain.Common.BusinessRuleException">Khi hạng hoặc cả bộ hạng không hợp lệ.</exception>
    /// <exception cref="Common.ForbiddenAccessException">Khi không làm việc ở tổng công ty.</exception>
    Task<SupplierRatingRuleDto> UpdateAsync(Guid id, SaveSupplierRatingRuleRequest request, CancellationToken cancellationToken);

    /// <summary>Xóa hạng (không ảnh hưởng chứng từ: xếp loại tính lại mỗi lần xem báo cáo).</summary>
    /// <param name="id">Hạng.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <exception cref="Common.NotFoundException">Khi không có hạng.</exception>
    /// <exception cref="Common.ForbiddenAccessException">Khi không làm việc ở tổng công ty.</exception>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}

/// <summary>Kiểm tra nội dung hạng gửi lên.</summary>
public sealed class SaveSupplierRatingRuleRequestValidator : AbstractValidator<SaveSupplierRatingRuleRequest>
{
    /// <summary>Khai báo quy tắc.</summary>
    public SaveSupplierRatingRuleRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().WithMessage("Hãy nhập mã hạng.").MaximumLength(10);
        RuleFor(x => x.Name).NotEmpty().WithMessage("Hãy nhập tên hạng.").MaximumLength(50);
        RuleFor(x => x.SortOrder).InclusiveBetween(1, 99).WithMessage("Thứ tự xét từ 1 đến 99.");
        RuleFor(x => x.MinOnTimePct).InclusiveBetween(0, 100).WithMessage("Tỷ lệ đúng hạn từ 0 đến 100%.");
        RuleFor(x => x.MaxReturnPct).InclusiveBetween(0, 100).WithMessage("Tỷ lệ trả hàng từ 0 đến 100%.");
    }
}
