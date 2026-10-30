using FluentValidation;

namespace Proc.Application.Organization;

/// <summary>
/// Kiểm tra thêm công ty. Định dạng mã và mã số thuế do Domain kiểm tra (Company), ở đây chỉ chặn trống và quá dài
/// theo độ dài cột (design.md mục 5.1).
/// </summary>
public sealed class CreateCompanyRequestValidator : AbstractValidator<CreateCompanyRequest>
{
    /// <summary>Khai báo các quy tắc.</summary>
    public CreateCompanyRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().WithMessage("Hãy nhập mã công ty.");
        RuleFor(x => x.Name).NotEmpty().WithMessage("Hãy nhập tên công ty.").MaximumLength(300);
        RuleFor(x => x.ShortName).MaximumLength(100);
        RuleFor(x => x.TaxCode).MaximumLength(20);
        RuleFor(x => x.Address).MaximumLength(500);
        RuleFor(x => x.Representative).MaximumLength(200);
        RuleFor(x => x.MainWarehouseCode).NotEmpty().WithMessage("Hãy nhập mã kho tổng.");
        RuleFor(x => x.MainWarehouseName).NotEmpty().WithMessage("Hãy nhập tên kho tổng.").MaximumLength(200);
    }
}

/// <summary>Kiểm tra sửa công ty.</summary>
public sealed class UpdateCompanyRequestValidator : AbstractValidator<UpdateCompanyRequest>
{
    /// <summary>Khai báo các quy tắc.</summary>
    public UpdateCompanyRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Hãy nhập tên công ty.").MaximumLength(300);
        RuleFor(x => x.ShortName).MaximumLength(100);
        RuleFor(x => x.TaxCode).MaximumLength(20);
        RuleFor(x => x.Address).MaximumLength(500);
        RuleFor(x => x.Representative).MaximumLength(200);
    }
}

/// <summary>Kiểm tra thêm kho.</summary>
public sealed class CreateWarehouseRequestValidator : AbstractValidator<CreateWarehouseRequest>
{
    /// <summary>Khai báo các quy tắc.</summary>
    public CreateWarehouseRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().WithMessage("Hãy nhập mã kho.");
        RuleFor(x => x.Name).NotEmpty().WithMessage("Hãy nhập tên kho.").MaximumLength(200);
        RuleFor(x => x.ParentWarehouseId).NotEmpty().WithMessage("Hãy chọn kho cha.");
        RuleFor(x => x.Address).MaximumLength(500);
        RuleFor(x => x.Keeper).MaximumLength(200);
    }
}

/// <summary>Kiểm tra sửa kho.</summary>
public sealed class UpdateWarehouseRequestValidator : AbstractValidator<UpdateWarehouseRequest>
{
    /// <summary>Khai báo các quy tắc.</summary>
    public UpdateWarehouseRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Hãy nhập tên kho.").MaximumLength(200);
        RuleFor(x => x.Address).MaximumLength(500);
        RuleFor(x => x.Keeper).MaximumLength(200);
    }
}
