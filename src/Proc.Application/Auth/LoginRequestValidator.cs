using FluentValidation;

namespace Proc.Application.Auth;

/// <summary>
/// Kiểm tra dữ liệu đăng nhập trước khi chạm tới CSDL. Chỉ kiểm tra có nhập và độ dài,
/// không kiểm tra quy tắc mật khẩu ở đây để không lộ quy tắc cho người dò mật khẩu.
/// </summary>
public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    /// <summary>Khai báo các quy tắc.</summary>
    public LoginRequestValidator()
    {
        RuleFor(x => x.UserName)
            .NotEmpty().WithMessage("Hãy nhập tên đăng nhập.")
            .MaximumLength(100).WithMessage("Tên đăng nhập tối đa 100 ký tự.");
        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Hãy nhập mật khẩu.")
            .MaximumLength(200).WithMessage("Mật khẩu tối đa 200 ký tự.");
    }
}
