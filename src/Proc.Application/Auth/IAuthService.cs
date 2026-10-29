namespace Proc.Application.Auth;

/// <summary>
/// Xử lý đăng nhập và lấy thông tin phiên. Cài đặt ở Infrastructure (ASP.NET Core Identity + JWT).
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Kiểm tra tên đăng nhập/mật khẩu; đúng thì cấp JWT, sai thì tăng số lần sai (đủ 5 lần thì khóa tạm).
    /// </summary>
    /// <param name="request">Tên đăng nhập và mật khẩu (đã qua LoginRequestValidator).</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Kết quả đăng nhập.</returns>
    Task<LoginOutcome> LoginAsync(LoginRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Lấy lại thông tin người dùng và danh sách công ty (khi tải lại trang).
    /// </summary>
    /// <param name="userId">Id người dùng trong token.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Thông tin người dùng; null nếu tài khoản không còn, đã ngừng dùng hoặc không còn công ty nào.</returns>
    Task<CurrentUserInfo?> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Đổi mật khẩu của chính người dùng.
    /// </summary>
    /// <param name="userId">Người đang đăng nhập.</param>
    /// <param name="request">Mật khẩu hiện tại và mật khẩu mới (đã qua ChangePasswordRequestValidator).</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Danh sách lỗi tiếng Việt; rỗng khi đổi thành công.</returns>
    Task<IReadOnlyList<string>> ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Ghi nhận đổi công ty làm việc (nhật ký truy cập) sau khi kiểm tra người dùng được làm việc ở công ty đó.
    /// </summary>
    /// <param name="userId">Người dùng.</param>
    /// <param name="companyId">Công ty chọn.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>false nếu người dùng không được làm việc ở công ty này.</returns>
    Task<bool> SwitchCompanyAsync(Guid userId, Guid companyId, CancellationToken cancellationToken);

    /// <summary>
    /// Ghi nhận đăng xuất. JWT không thu hồi được phía server nên đây chỉ là ghi nhật ký; giao diện tự xóa phiên.
    /// </summary>
    /// <param name="userId">Người dùng.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Task hoàn tất khi đã ghi.</returns>
    Task LogoutAsync(Guid userId, CancellationToken cancellationToken);
}
