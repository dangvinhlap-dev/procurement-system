using FluentValidation;

namespace Proc.Application.Administration;

/// <summary>
/// Kiểm tra tạo tài khoản. Độ mạnh mật khẩu (design.md mục 9.4) do Identity kiểm tra lần cuối.
/// </summary>
public sealed class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    /// <summary>Khai báo các quy tắc.</summary>
    public CreateUserRequestValidator()
    {
        // Tên đăng nhập chữ thường không dấu để gõ nhanh, không lẫn hoa/thường, giống tài khoản mẫu (thukho.bha).
        RuleFor(x => x.UserName)
            .NotEmpty().WithMessage("Hãy nhập tên đăng nhập.")
            .Length(3, 50).WithMessage("Tên đăng nhập dài 3–50 ký tự.")
            .Matches("^[a-z0-9][a-z0-9._-]*$")
            .WithMessage("Tên đăng nhập chỉ gồm chữ thường không dấu, số, dấu chấm, gạch ngang, gạch dưới.");
        RuleFor(x => x.FullName).NotEmpty().WithMessage("Hãy nhập họ tên.").MaximumLength(200);
        RuleFor(x => x.Email).EmailAddress().WithMessage("Email không hợp lệ.").MaximumLength(256)
            .When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Hãy nhập mật khẩu ban đầu.")
            .MinimumLength(8).WithMessage("Mật khẩu phải có ít nhất 8 ký tự.")
            .MaximumLength(200);
        RuleFor(x => x.Access).NotNull().WithMessage("Thiếu thông tin phân quyền.");
    }
}

/// <summary>Kiểm tra sửa tài khoản.</summary>
public sealed class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
{
    /// <summary>Khai báo các quy tắc.</summary>
    public UpdateUserRequestValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().WithMessage("Hãy nhập họ tên.").MaximumLength(200);
        RuleFor(x => x.Email).EmailAddress().WithMessage("Email không hợp lệ.").MaximumLength(256)
            .When(x => !string.IsNullOrWhiteSpace(x.Email));
    }
}

/// <summary>Kiểm tra đặt lại mật khẩu.</summary>
public sealed class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    /// <summary>Khai báo các quy tắc.</summary>
    public ResetPasswordRequestValidator()
    {
        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("Hãy nhập mật khẩu mới.")
            .MinimumLength(8).WithMessage("Mật khẩu mới phải có ít nhất 8 ký tự.")
            .MaximumLength(200);
    }
}

/// <summary>Kiểm tra gán quyền: phải gửi đủ ba danh sách (rỗng cũng được, nhưng không được thiếu).</summary>
public sealed class UserAccessValidator : AbstractValidator<UserAccessDto>
{
    /// <summary>Khai báo các quy tắc.</summary>
    public UserAccessValidator()
    {
        RuleFor(x => x.RoleIds).NotNull().WithMessage("Thiếu danh sách vai trò.");
        RuleFor(x => x.WarehouseIds).NotNull().WithMessage("Thiếu danh sách kho.");
        RuleFor(x => x.ItemCategoryIds).NotNull().WithMessage("Thiếu danh sách nhóm hàng.");
    }
}

/// <summary>Kiểm tra tạo vai trò. Định dạng mã do Role.Create kiểm tra.</summary>
public sealed class CreateRoleRequestValidator : AbstractValidator<CreateRoleRequest>
{
    /// <summary>Khai báo các quy tắc.</summary>
    public CreateRoleRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().WithMessage("Hãy nhập mã vai trò.");
        RuleFor(x => x.Name).NotEmpty().WithMessage("Hãy nhập tên vai trò.").MaximumLength(200);
        RuleFor(x => x.PermissionCodes).NotNull().WithMessage("Thiếu danh sách quyền.");
    }
}

/// <summary>Kiểm tra sửa vai trò.</summary>
public sealed class UpdateRoleRequestValidator : AbstractValidator<UpdateRoleRequest>
{
    /// <summary>Khai báo các quy tắc.</summary>
    public UpdateRoleRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Hãy nhập tên vai trò.").MaximumLength(200);
        RuleFor(x => x.PermissionCodes).NotNull().WithMessage("Thiếu danh sách quyền.");
    }
}
