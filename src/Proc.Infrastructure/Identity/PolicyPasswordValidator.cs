using System.Reflection;
using Microsoft.AspNetCore.Identity;
using Proc.Application.Operations;

namespace Proc.Infrastructure.Identity;

/// <summary>
/// Kiểm mật khẩu theo cấu hình truy cập CFG (lát QT7 – design.md mục 21.16): độ dài tối thiểu, mật khẩu phổ biến (danh sách nhúng
/// <c>CommonPasswords.txt</c>), mật khẩu cấm thêm. Chạy cùng bộ kiểm có sẵn của Identity (chữ thường + chữ số – design.md 9.4) ở mọi chỗ đặt mật
/// khẩu: tạo người dùng, đặt lại, tự đổi. Độ dài của Identity đặt 1 để chỉ bộ này báo độ dài (không hai câu lệch số).
/// </summary>
public sealed class PolicyPasswordValidator : IPasswordValidator<AppUser>
{
    /// <summary>Danh sách mật khẩu phổ biến, đọc một lần từ tài nguyên nhúng.</summary>
    public static IReadOnlySet<string> CommonPasswords { get; } = LoadCommonPasswords();

    private readonly IAccessPolicyProvider _policy;

    /// <summary>Tạo bộ kiểm.</summary>
    /// <param name="policy">Nguồn chính sách truy cập.</param>
    public PolicyPasswordValidator(IAccessPolicyProvider policy) => _policy = policy;

    /// <inheritdoc />
    public async Task<IdentityResult> ValidateAsync(UserManager<AppUser> manager, AppUser user, string? password)
    {
        var policy = await _policy.GetAsync(CancellationToken.None);
        password ??= string.Empty;
        var errors = new List<IdentityError>();
        if (password.Length < policy.MinPasswordLength)
            errors.Add(new IdentityError
            {
                Code = nameof(IdentityErrorDescriber.PasswordTooShort),
                Description = $"Mật khẩu phải có ít nhất {policy.MinPasswordLength} ký tự.",
            });

        // Mật khẩu cấm thêm luôn áp (người quản trị đã khai riêng); danh sách phổ biến theo công tắc.
        if (policy.ExtraBlockedPasswords.Contains(password) || (policy.BlockCommonPasswords && CommonPasswords.Contains(password)))
            errors.Add(new IdentityError
            {
                Code = "PasswordTooCommon",
                Description = "Mật khẩu quá phổ biến, dễ bị đoán – hãy chọn mật khẩu khác.",
            });

        return errors.Count == 0 ? IdentityResult.Success : IdentityResult.Failed([.. errors]);
    }

    private static HashSet<string> LoadCommonPasswords()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Proc.Infrastructure.Identity.CommonPasswords.txt")
            ?? throw new InvalidOperationException("Thiếu tài nguyên nhúng CommonPasswords.txt.");
        using var reader = new StreamReader(stream);
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (reader.ReadLine() is { } line)
        {
            line = line.Trim();
            if (line.Length > 0 && !line.StartsWith('#')) result.Add(line);
        }

        return result;
    }
}
