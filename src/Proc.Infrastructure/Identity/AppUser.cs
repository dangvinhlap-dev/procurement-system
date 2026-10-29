using Microsoft.AspNetCore.Identity;

namespace Proc.Infrastructure.Identity;

/// <summary>
/// Tài khoản người dùng (design.md mục 5.1), quản lý bằng ASP.NET Core Identity: băm mật khẩu,
/// khóa tạm khi đăng nhập sai nhiều lần, sẵn chỗ cho 2FA. Lưu ở bảng "User".
/// Vai trò gán theo công ty nằm ở bảng UserCompanyRole, không dùng bảng vai trò của Identity.
/// </summary>
public sealed class AppUser : IdentityUser<Guid>
{
    /// <summary>Tạo tài khoản với Id uuid v7.</summary>
    public AppUser()
    {
        Id = Guid.CreateVersion7();
        SecurityStamp = Guid.NewGuid().ToString();
    }

    /// <summary>Họ tên hiển thị trên thanh menu, thanh trạng thái, nhật ký.</summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>Công ty mặc định khi đăng nhập.</summary>
    public Guid? DefaultCompanyId { get; set; }

    /// <summary>Còn sử dụng. Tài khoản không xóa cứng để giữ nhật ký thao tác.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Thời điểm tạo (UTC).</summary>
    public DateTimeOffset CreatedAt { get; set; }
}
