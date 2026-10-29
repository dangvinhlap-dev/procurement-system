namespace Proc.Application.Auth;

/// <summary>Yêu cầu đăng nhập.</summary>
/// <param name="UserName">Tên đăng nhập (không phân biệt hoa/thường).</param>
/// <param name="Password">Mật khẩu.</param>
public sealed record LoginRequest(string UserName, string Password);

/// <summary>Kết quả đăng nhập thành công.</summary>
/// <param name="AccessToken">JWT gửi kèm mỗi request trong header Authorization: Bearer.</param>
/// <param name="ExpiresAt">Thời điểm token hết hạn (UTC).</param>
/// <param name="User">Thông tin người dùng và các công ty được làm việc.</param>
public sealed record LoginResult(string AccessToken, DateTimeOffset ExpiresAt, CurrentUserInfo User);

/// <summary>Thông tin người dùng đang đăng nhập.</summary>
/// <param name="UserId">Id người dùng.</param>
/// <param name="UserName">Tên đăng nhập.</param>
/// <param name="FullName">Họ tên hiển thị.</param>
/// <param name="DefaultCompanyId">Công ty mặc định khi vừa đăng nhập; luôn thuộc danh sách Companies.</param>
/// <param name="Companies">Các công ty người dùng được làm việc (có ít nhất một vai trò).</param>
public sealed record CurrentUserInfo(
    Guid UserId,
    string UserName,
    string FullName,
    Guid DefaultCompanyId,
    IReadOnlyList<UserCompanyInfo> Companies);

/// <summary>Một công ty người dùng được làm việc.</summary>
/// <param name="Id">Id công ty – gửi lên API qua header X-Company-Id.</param>
/// <param name="Code">Mã công ty.</param>
/// <param name="Name">Tên công ty.</param>
/// <param name="ShortName">Tên viết tắt.</param>
/// <param name="IsHeadOffice">Là tổng công ty.</param>
/// <param name="RoleCodes">Mã các vai trò của người dùng tại công ty này.</param>
/// <param name="Permissions">Mã các quyền chức năng tại công ty này (hợp của quyền mọi vai trò) –
/// giao diện dùng để ẩn menu, nút; API vẫn kiểm tra lại ở mỗi request.</param>
public sealed record UserCompanyInfo(
    Guid Id,
    string Code,
    string Name,
    string? ShortName,
    bool IsHeadOffice,
    IReadOnlyList<string> RoleCodes,
    IReadOnlyList<string> Permissions);

/// <summary>Lý do đăng nhập không thành công.</summary>
public enum LoginFailure
{
    /// <summary>Sai tên đăng nhập hoặc mật khẩu (không nói rõ sai phần nào để tránh dò tài khoản).</summary>
    InvalidCredentials,

    /// <summary>Tài khoản đang bị khóa tạm do đăng nhập sai nhiều lần.</summary>
    LockedOut,

    /// <summary>Tài khoản đã ngừng sử dụng.</summary>
    Inactive,

    /// <summary>Tài khoản chưa được gán vào công ty nào đang hoạt động.</summary>
    NoCompany,
}

/// <summary>Kết quả xử lý đăng nhập: thành công kèm LoginResult, hoặc thất bại kèm lý do.</summary>
/// <param name="Result">Kết quả khi thành công.</param>
/// <param name="Failure">Lý do khi thất bại.</param>
/// <param name="LockoutEnd">Thời điểm hết khóa, khi Failure = LockedOut.</param>
public sealed record LoginOutcome(LoginResult? Result, LoginFailure? Failure, DateTimeOffset? LockoutEnd = null)
{
    /// <summary>true khi đăng nhập thành công.</summary>
    public bool Succeeded => Result is not null;
}

/// <summary>Đổi công ty làm việc trên thanh menu – để ghi nhật ký truy cập (design.md mục 9.3).</summary>
/// <param name="CompanyId">Công ty chọn.</param>
public sealed record SwitchCompanyRequest(Guid CompanyId);
