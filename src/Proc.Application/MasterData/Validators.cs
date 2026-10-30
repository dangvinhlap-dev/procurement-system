using FluentValidation;

namespace Proc.Application.MasterData;

/// <summary>Kiểm tra thêm đơn vị tính (định dạng mã do Domain kiểm tra).</summary>
public sealed class CreateUomRequestValidator : AbstractValidator<CreateUomRequest>
{
    /// <summary>Khai báo các quy tắc.</summary>
    public CreateUomRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().WithMessage("Hãy nhập mã đơn vị tính.");
        RuleFor(x => x.Name).NotEmpty().WithMessage("Hãy nhập tên đơn vị tính.").MaximumLength(50);
    }
}

/// <summary>Kiểm tra sửa đơn vị tính.</summary>
public sealed class UpdateUomRequestValidator : AbstractValidator<UpdateUomRequest>
{
    /// <summary>Khai báo các quy tắc.</summary>
    public UpdateUomRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Hãy nhập tên đơn vị tính.").MaximumLength(50);
    }
}

/// <summary>Kiểm tra thêm nhóm hàng.</summary>
public sealed class CreateItemCategoryRequestValidator : AbstractValidator<CreateItemCategoryRequest>
{
    /// <summary>Khai báo các quy tắc.</summary>
    public CreateItemCategoryRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().WithMessage("Hãy nhập mã nhóm hàng.");
        RuleFor(x => x.Name).NotEmpty().WithMessage("Hãy nhập tên nhóm hàng.").MaximumLength(200);
    }
}

/// <summary>Kiểm tra sửa nhóm hàng.</summary>
public sealed class UpdateItemCategoryRequestValidator : AbstractValidator<UpdateItemCategoryRequest>
{
    /// <summary>Khai báo các quy tắc.</summary>
    public UpdateItemCategoryRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Hãy nhập tên nhóm hàng.").MaximumLength(200);
    }
}

/// <summary>Kiểm tra thêm hàng hóa. Độ dài khớp cột (ItemConfiguration); quy tắc nghiệp vụ do Domain kiểm tra.</summary>
public sealed class CreateItemRequestValidator : AbstractValidator<CreateItemRequest>
{
    /// <summary>Khai báo các quy tắc.</summary>
    public CreateItemRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().WithMessage("Hãy nhập mã hàng.");
        RuleFor(x => x.Name).NotEmpty().WithMessage("Hãy nhập tên hàng.").MaximumLength(300);
        RuleFor(x => x.EnglishName).MaximumLength(300);
        RuleFor(x => x.Specification).MaximumLength(500);
        RuleFor(x => x.Brand).MaximumLength(100);
        RuleFor(x => x.Origin).MaximumLength(100);
        RuleFor(x => x.Barcode).MaximumLength(50);
        RuleFor(x => x.TechnicalSpecs).MaximumLength(4000);
        RuleFor(x => x.Conversions).NotNull().WithMessage("Thiếu danh sách quy đổi ĐVT.");
    }
}

/// <summary>Kiểm tra sửa hàng hóa.</summary>
public sealed class UpdateItemRequestValidator : AbstractValidator<UpdateItemRequest>
{
    /// <summary>Khai báo các quy tắc.</summary>
    public UpdateItemRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Hãy nhập tên hàng.").MaximumLength(300);
        RuleFor(x => x.EnglishName).MaximumLength(300);
        RuleFor(x => x.Specification).MaximumLength(500);
        RuleFor(x => x.Brand).MaximumLength(100);
        RuleFor(x => x.Origin).MaximumLength(100);
        RuleFor(x => x.Barcode).MaximumLength(50);
        RuleFor(x => x.TechnicalSpecs).MaximumLength(4000);
        RuleFor(x => x.Conversions).NotNull().WithMessage("Thiếu danh sách quy đổi ĐVT.");
    }
}
