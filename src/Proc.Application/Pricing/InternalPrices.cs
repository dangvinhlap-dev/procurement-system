using FluentValidation;

namespace Proc.Application.Pricing;

/// <summary>Một dòng bảng giá nội bộ của công ty bán đang làm việc.</summary>
/// <param name="Id">Id.</param>
/// <param name="BuyerCompanyId">Công ty mua; null = mọi công ty.</param>
/// <param name="BuyerCompanyCode">Mã công ty mua (null = mọi công ty).</param>
/// <param name="ItemId">Mặt hàng.</param>
/// <param name="ItemCode">Mã hàng.</param>
/// <param name="ItemName">Tên hàng.</param>
/// <param name="UomCode">ĐVT cơ sở (giá tính theo ĐVT này).</param>
/// <param name="UnitPrice">Giá chưa VAT.</param>
/// <param name="VatCode">Thuế suất (null = theo mặt hàng).</param>
/// <param name="ValidFrom">Hiệu lực từ.</param>
/// <param name="ValidTo">Hiệu lực đến.</param>
/// <param name="Note">Ghi chú.</param>
public sealed record InternalPriceDto(
    Guid Id, Guid? BuyerCompanyId, string? BuyerCompanyCode, Guid ItemId, string ItemCode, string ItemName, string UomCode,
    decimal UnitPrice, string? VatCode, DateOnly ValidFrom, DateOnly? ValidTo, string? Note);

/// <summary>Thêm/sửa giá nội bộ (sửa thì công ty mua, mặt hàng giữ nguyên).</summary>
public sealed record SaveInternalPriceRequest(
    Guid? BuyerCompanyId, Guid ItemId, decimal UnitPrice, string? VatCode, DateOnly ValidFrom, DateOnly? ValidTo, string? Note);

/// <summary>Giá nội bộ áp cho một mặt hàng tại một ngày.</summary>
/// <param name="ItemId">Mặt hàng.</param>
/// <param name="UnitPrice">Giá chưa VAT theo ĐVT cơ sở.</param>
/// <param name="VatCode">Thuế suất đã quy về (giá trống thì lấy của mặt hàng).</param>
public sealed record ResolvedInternalPriceDto(Guid ItemId, decimal UnitPrice, string? VatCode);

/// <summary>Một công ty trong tập đoàn (để chọn công ty mua/bán nội bộ).</summary>
public sealed record GroupCompanyDto(Guid Id, string Code, string Name);

/// <summary>
/// Bảng giá nội bộ của công ty đang làm việc (công ty bán – design.md mục 4, 6.3).
/// </summary>
public interface IInternalPriceAdministration
{
    /// <summary>Mọi dòng giá của công ty bán, theo mã hàng, công ty mua, ngày hiệu lực.</summary>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Danh sách.</returns>
    Task<IReadOnlyList<InternalPriceDto>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Thêm giá.</summary>
    /// <param name="request">Nội dung.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Giá vừa thêm.</returns>
    /// <exception cref="Domain.Common.BusinessRuleException">Khi trùng hiệu lực hoặc sai quy tắc.</exception>
    /// <exception cref="Common.NotFoundException">Khi không có mặt hàng hoặc công ty mua.</exception>
    Task<InternalPriceDto> CreateAsync(SaveInternalPriceRequest request, CancellationToken cancellationToken);

    /// <summary>Sửa giá, thuế suất, hiệu lực, ghi chú.</summary>
    /// <param name="id">Id.</param>
    /// <param name="request">Nội dung.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Giá sau khi sửa.</returns>
    /// <exception cref="Domain.Common.BusinessRuleException">Khi trùng hiệu lực hoặc sai quy tắc.</exception>
    /// <exception cref="Common.NotFoundException">Khi không có.</exception>
    Task<InternalPriceDto> UpdateAsync(Guid id, SaveInternalPriceRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Giá áp dụng khi công ty đang làm việc bán cho <paramref name="buyerCompanyId"/> vào ngày <paramref name="date"/>: giá riêng
    /// cho công ty mua được ưu tiên hơn giá chung. Mặt hàng chưa có giá thì không có trong kết quả.
    /// </summary>
    /// <param name="buyerCompanyId">Công ty mua.</param>
    /// <param name="date">Ngày phiếu.</param>
    /// <param name="itemIds">Mặt hàng.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Giá theo mặt hàng.</returns>
    Task<IReadOnlyList<ResolvedInternalPriceDto>> ResolveAsync(Guid buyerCompanyId, DateOnly date, IReadOnlyList<Guid> itemIds, CancellationToken cancellationToken);

    /// <summary>Các công ty đang hoạt động trong tập đoàn, trừ công ty đang làm việc.</summary>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Danh sách theo mã.</returns>
    Task<IReadOnlyList<GroupCompanyDto>> OtherCompaniesAsync(CancellationToken cancellationToken);
}

/// <summary>Kiểm tra thêm/sửa giá nội bộ.</summary>
public sealed class SaveInternalPriceRequestValidator : AbstractValidator<SaveInternalPriceRequest>
{
    /// <summary>Khai báo các quy tắc.</summary>
    public SaveInternalPriceRequestValidator()
    {
        RuleFor(x => x.ItemId).NotEmpty().WithMessage("Hãy chọn mặt hàng.");
        RuleFor(x => x.UnitPrice).GreaterThanOrEqualTo(0).WithMessage("Giá bán nội bộ không được âm.");
        RuleFor(x => x.Note).MaximumLength(500);
    }
}
