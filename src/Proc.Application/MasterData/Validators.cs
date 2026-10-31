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
        // Mã do hệ thống cấp theo dải đánh số (design.md mục 5.11) – không bắt buộc nhập, nhập thì dịch vụ báo lỗi.
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
        // Mã do hệ thống cấp theo dải đánh số (design.md mục 5.11) – không bắt buộc nhập, nhập thì dịch vụ báo lỗi.
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

/// <summary>Kiểm tra thêm nhóm NCC.</summary>
public sealed class CreateSupplierGroupRequestValidator : AbstractValidator<CreateSupplierGroupRequest>
{
    /// <summary>Khai báo các quy tắc.</summary>
    public CreateSupplierGroupRequestValidator()
    {
        // Mã do hệ thống cấp theo dải đánh số (design.md mục 5.11) – không bắt buộc nhập, nhập thì dịch vụ báo lỗi.
        RuleFor(x => x.Name).NotEmpty().WithMessage("Hãy nhập tên nhóm NCC.").MaximumLength(200);
    }
}

/// <summary>Kiểm tra sửa nhóm NCC.</summary>
public sealed class UpdateSupplierGroupRequestValidator : AbstractValidator<UpdateSupplierGroupRequest>
{
    /// <summary>Khai báo các quy tắc.</summary>
    public UpdateSupplierGroupRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Hãy nhập tên nhóm NCC.").MaximumLength(200);
    }
}

/// <summary>
/// Kiểm tra lưu NCC: độ dài khớp cột (SupplierConfiguration), định dạng email. Quy tắc nghiệp vụ (mã số thuế, trạng thái,
/// liên hệ chính, tài khoản mặc định) do Domain kiểm tra.
/// </summary>
public sealed class SaveSupplierRequestValidator : AbstractValidator<SaveSupplierRequest>
{
    /// <summary>Khai báo các quy tắc.</summary>
    public SaveSupplierRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Hãy nhập tên NCC.").MaximumLength(300);
        RuleFor(x => x.ShortName).MaximumLength(100);
        RuleFor(x => x.TaxCode).MaximumLength(20);
        RuleFor(x => x.Address).MaximumLength(500);
        RuleFor(x => x.Phone).MaximumLength(50);
        RuleFor(x => x.Email).EmailAddress().WithMessage("Email NCC không hợp lệ.").MaximumLength(256)
            .When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Website).MaximumLength(200);
        RuleFor(x => x.PaymentTerms).MaximumLength(200);
        RuleFor(x => x.SupplyScope).MaximumLength(500);
        RuleFor(x => x.StatusNote).MaximumLength(500);
        RuleFor(x => x.Contacts).NotNull().WithMessage("Thiếu danh sách người liên hệ.");
        RuleFor(x => x.BankAccounts).NotNull().WithMessage("Thiếu danh sách tài khoản ngân hàng.");
        RuleForEach(x => x.Contacts).ChildRules(c =>
        {
            c.RuleFor(x => x.Name).MaximumLength(200);
            c.RuleFor(x => x.Position).MaximumLength(100);
            c.RuleFor(x => x.Phone).MaximumLength(50);
            c.RuleFor(x => x.Email).EmailAddress().WithMessage("Email người liên hệ không hợp lệ.").MaximumLength(256)
                .When(x => !string.IsNullOrWhiteSpace(x.Email));
        });
        RuleForEach(x => x.BankAccounts).ChildRules(b =>
        {
            b.RuleFor(x => x.BankName).MaximumLength(200);
            b.RuleFor(x => x.Branch).MaximumLength(200);
            b.RuleFor(x => x.AccountName).MaximumLength(200);
        });
    }
}

/// <summary>Kiểm tra lưu NCC áp dụng tại công ty.</summary>
public sealed class SaveSupplierCompanyRequestValidator : AbstractValidator<SaveSupplierCompanyRequest>
{
    /// <summary>Khai báo các quy tắc.</summary>
    public SaveSupplierCompanyRequestValidator()
    {
        RuleFor(x => x.LocalCode).MaximumLength(30);
    }
}

/// <summary>Kiểm tra lưu NCC – sản phẩm.</summary>
public sealed class SaveSupplierItemRequestValidator : AbstractValidator<SaveSupplierItemRequest>
{
    /// <summary>Khai báo các quy tắc.</summary>
    public SaveSupplierItemRequestValidator()
    {
        RuleFor(x => x.ItemId).NotEmpty().WithMessage("Hãy chọn mặt hàng.");
        RuleFor(x => x.PurchaseUomId).NotEmpty().WithMessage("Hãy chọn ĐVT mua.");
        RuleFor(x => x.SupplierItemCode).MaximumLength(50);
    }
}
