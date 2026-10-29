using FluentValidation;

namespace Proc.Application.Auth;

/// <summary>Yêu cầu đổi mật khẩu của chính người đang đăng nhập.</summary>
/// <param name="CurrentPassword">Mật khẩu hiện tại.</param>
/// <param name="NewPassword">Mật khẩu mới.</param>
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

/// <summary>
/// Kiểm tra yêu cầu đổi mật khẩu. Quy tắc độ mạnh mật khẩu (design.md mục 9.4) do Identity kiểm tra lần cuối;
/// ở đây báo sớm các lỗi thường gặp bằng tiếng Việt.
/// </summary>
public sealed class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    /// <summary>Khai báo các quy tắc.</summary>
    public ChangePasswordRequestValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty().WithMessage("Hãy nhập mật khẩu hiện tại.");
        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("Hãy nhập mật khẩu mới.")
            .MinimumLength(8).WithMessage("Mật khẩu mới phải có ít nhất 8 ký tự.")
            .MaximumLength(200).WithMessage("Mật khẩu mới tối đa 200 ký tự.")
            .Must((request, value) => value != request.CurrentPassword)
            .WithMessage("Mật khẩu mới phải khác mật khẩu hiện tại.");
    }
}
