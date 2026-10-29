using FluentValidation;
using Proc.Application.Auth;
using Proc.Application.Common;

namespace Proc.Api.Endpoints;

/// <summary>
/// Endpoint đăng nhập và lấy thông tin phiên: /api/auth/login, /api/auth/me.
/// </summary>
public static class AuthEndpoints
{
    /// <summary>Múi giờ hiển thị cho người dùng (CLAUDE.md mục 5).</summary>
    private static readonly TimeZoneInfo VietnamTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");

    /// <summary>
    /// Khai báo các endpoint xác thực.
    /// </summary>
    /// <param name="app">Bộ định tuyến endpoint.</param>
    /// <returns>Nhóm endpoint /api/auth.</returns>
    public static RouteGroupBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Đăng nhập");
        group.MapPost("/login", LoginAsync).AllowAnonymous();
        group.MapGet("/me", GetMeAsync);
        return group;
    }

    /// <summary>
    /// Đăng nhập: trả JWT và danh sách công ty được làm việc; sai thì trả thông báo tiếng Việt.
    /// </summary>
    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        IValidator<LoginRequest> validator,
        IAuthService auth,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
            return Results.ValidationProblem(validation.ToDictionary(), title: "Dữ liệu đăng nhập không hợp lệ.");

        var outcome = await auth.LoginAsync(request, cancellationToken);
        if (outcome.Succeeded) return Results.Ok(outcome.Result);

        return outcome.Failure switch
        {
            LoginFailure.LockedOut => Problem(
                StatusCodes.Status423Locked,
                $"Tài khoản tạm khóa do nhập sai mật khẩu nhiều lần. Thử lại sau {FormatVietnamTime(outcome.LockoutEnd)}."),
            LoginFailure.Inactive => Problem(
                StatusCodes.Status403Forbidden, "Tài khoản đã ngừng sử dụng. Liên hệ quản trị hệ thống."),
            LoginFailure.NoCompany => Problem(
                StatusCodes.Status403Forbidden, "Tài khoản chưa được gán vào công ty nào. Liên hệ quản trị hệ thống."),
            _ => Problem(StatusCodes.Status401Unauthorized, "Tên đăng nhập hoặc mật khẩu không đúng."),
        };
    }

    /// <summary>
    /// Thông tin người dùng đang đăng nhập; giao diện gọi khi tải lại trang để lấy lại danh sách công ty mới nhất.
    /// </summary>
    private static async Task<IResult> GetMeAsync(ICurrentUser currentUser, IAuthService auth, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId) return Results.Unauthorized();
        var info = await auth.GetCurrentUserAsync(userId, cancellationToken);
        return info is null
            ? Problem(StatusCodes.Status401Unauthorized, "Phiên đăng nhập không còn hiệu lực. Hãy đăng nhập lại.")
            : Results.Ok(info);
    }

    private static IResult Problem(int status, string message) => Results.Problem(title: message, statusCode: status);

    private static string FormatVietnamTime(DateTimeOffset? utc) =>
        utc is null ? "ít phút" : TimeZoneInfo.ConvertTime(utc.Value, VietnamTimeZone).ToString("HH:mm");
}
