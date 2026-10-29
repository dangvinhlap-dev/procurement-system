using Microsoft.AspNetCore.Identity;

namespace Proc.Infrastructure.Identity;

/// <summary>
/// Thông báo lỗi tài khoản/mật khẩu của Identity bằng tiếng Việt (giao diện hoàn toàn tiếng Việt – CLAUDE.md mục 5).
/// Chỉ ghi đè các lỗi người dùng có thể gặp; lỗi kỹ thuật hiếm gặp giữ mặc định.
/// </summary>
public sealed class VietnameseIdentityErrorDescriber : IdentityErrorDescriber
{
    /// <inheritdoc />
    public override IdentityError PasswordMismatch() =>
        new() { Code = nameof(PasswordMismatch), Description = "Mật khẩu hiện tại không đúng." };

    /// <inheritdoc />
    public override IdentityError PasswordTooShort(int length) =>
        new() { Code = nameof(PasswordTooShort), Description = $"Mật khẩu phải có ít nhất {length} ký tự." };

    /// <inheritdoc />
    public override IdentityError PasswordRequiresDigit() =>
        new() { Code = nameof(PasswordRequiresDigit), Description = "Mật khẩu phải có ít nhất một chữ số (0–9)." };

    /// <inheritdoc />
    public override IdentityError PasswordRequiresLower() =>
        new() { Code = nameof(PasswordRequiresLower), Description = "Mật khẩu phải có ít nhất một chữ thường (a–z)." };

    /// <inheritdoc />
    public override IdentityError PasswordRequiresUpper() =>
        new() { Code = nameof(PasswordRequiresUpper), Description = "Mật khẩu phải có ít nhất một chữ hoa (A–Z)." };

    /// <inheritdoc />
    public override IdentityError PasswordRequiresNonAlphanumeric() =>
        new() { Code = nameof(PasswordRequiresNonAlphanumeric), Description = "Mật khẩu phải có ít nhất một ký tự đặc biệt." };

    /// <inheritdoc />
    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) =>
        new() { Code = nameof(PasswordRequiresUniqueChars), Description = $"Mật khẩu phải có ít nhất {uniqueChars} ký tự khác nhau." };

    /// <inheritdoc />
    public override IdentityError DuplicateUserName(string userName) =>
        new() { Code = nameof(DuplicateUserName), Description = $"Tên đăng nhập '{userName}' đã có người dùng." };

    /// <inheritdoc />
    public override IdentityError InvalidUserName(string? userName) =>
        new() { Code = nameof(InvalidUserName), Description = $"Tên đăng nhập '{userName}' không hợp lệ (chỉ dùng chữ không dấu, số và . _ -)." };

    /// <inheritdoc />
    public override IdentityError UserLockoutNotEnabled() =>
        new() { Code = nameof(UserLockoutNotEnabled), Description = "Tài khoản này không bật khóa tạm." };
}
