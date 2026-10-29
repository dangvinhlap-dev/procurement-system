namespace Proc.Application.Common;

/// <summary>
/// Người dùng đang gọi hệ thống, lấy từ token đăng nhập. Phạm vi dữ liệu đầy đủ (công ty, kho, nhóm hàng)
/// sẽ nằm ở ICurrentUserScope ở lát phân quyền (design.md mục 9.2).
/// </summary>
public interface ICurrentUser
{
    /// <summary>Id người dùng; null khi chưa đăng nhập hoặc chạy nền (khởi tạo dữ liệu, job).</summary>
    Guid? UserId { get; }
}

/// <summary>
/// Đồng hồ hệ thống – tách ra để test giả lập được thời gian (design.md mục 3.1).
/// </summary>
public interface IClock
{
    /// <summary>Thời điểm hiện tại theo UTC (CSDL lưu UTC – CLAUDE.md mục 5).</summary>
    DateTimeOffset UtcNow { get; }
}
