using Microsoft.AspNetCore.Identity;
using Proc.Domain.Common;

namespace Proc.Infrastructure.Identity;

/// <summary>
/// Tài khoản người dùng (design.md mục 5.1), quản lý bằng ASP.NET Core Identity: băm mật khẩu,
/// khóa tạm khi đăng nhập sai nhiều lần, sẵn chỗ cho 2FA. Lưu ở bảng "User".
/// Vai trò gán theo công ty nằm ở bảng UserCompanyRole, không dùng bảng vai trò của Identity.
/// Nhật ký: mã băm mật khẩu chỉ ghi là "đã đổi"; bỏ qua số lần nhập sai, các mã bảo mật và cột chuẩn hóa
/// vì đổi liên tục hoặc lặp lại cột khác, không có ý nghĩa nghiệp vụ (design.md mục 9.3).
/// </summary>
[Auditable(
    "Tài khoản",
    EntityType = "User",
    LabelProperty = nameof(UserName),
    Ignore = [nameof(AccessFailedCount), nameof(ConcurrencyStamp), nameof(SecurityStamp), nameof(NormalizedUserName), nameof(NormalizedEmail)],
    Redact = [nameof(PasswordHash)])]
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
