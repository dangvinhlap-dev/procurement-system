using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;
using Proc.Api.Security;
using Proc.Application.Common;
using Proc.Application.Portal;
using Proc.Domain.Organization;
using Proc.Infrastructure.Portal;

namespace Proc.Api.Endpoints;

/// <summary>
/// Portal nhà cung cấp (design.md 19.5, lát P1): API nội bộ quản lý tài khoản Portal của NCC (/api/suppliers/{id}/portal-users) và API
/// riêng của Portal (/api/portal). API Portal chỉ nhận token scheme <see cref="PortalClaims.Scheme"/> (audience riêng) – token nội bộ không
/// gọi được, token NCC cũng không gọi được API nội bộ (scheme mặc định kiểm audience nội bộ).
/// </summary>
public static class PortalEndpoints
{
    private const string SessionItem = "PortalSession";

    /// <summary>
    /// Khai báo endpoint Portal.
    /// </summary>
    /// <param name="app">Bộ định tuyến endpoint.</param>
    /// <returns>Bộ định tuyến để nối tiếp.</returns>
    public static IEndpointRouteBuilder MapPortalEndpoints(this IEndpointRouteBuilder app)
    {
        // ── Nội bộ: Phòng CUVT quản lý tài khoản Portal của NCC ──
        var admin = app.MapGroup("/api/suppliers/{supplierId:guid}/portal-users").WithTags("Portal NCC – quản lý tài khoản");
        admin.MapGet("/", (Guid supplierId, ISupplierPortalAccountService s, CancellationToken ct) => s.ListAsync(supplierId, ct))
            .RequirePermission(Permissions.MasterData.SupplierView);
        admin.MapPost("/", (Guid supplierId, PortalInviteRequest request, HttpContext http, ISupplierPortalAccountService s, CancellationToken ct) =>
                s.InviteAsync(supplierId, request, null, RequestBaseUrl(http), ct))
            .RequirePermission(Permissions.MasterData.SupplierPortal);
        admin.MapPost("/{userId:guid}/resend-invite", (Guid supplierId, Guid userId, HttpContext http, ISupplierPortalAccountService s, CancellationToken ct) =>
                s.ResendInviteAsync(supplierId, userId, null, RequestBaseUrl(http), ct))
            .RequirePermission(Permissions.MasterData.SupplierPortal);
        admin.MapPost("/{userId:guid}/disable", (Guid supplierId, Guid userId, ISupplierPortalAccountService s, CancellationToken ct) =>
                s.DisableAsync(supplierId, userId, null, ct))
            .RequirePermission(Permissions.MasterData.SupplierPortal);
        admin.MapPost("/{userId:guid}/enable", (Guid supplierId, Guid userId, ISupplierPortalAccountService s, CancellationToken ct) =>
                s.EnableAsync(supplierId, userId, null, ct))
            .RequirePermission(Permissions.MasterData.SupplierPortal);

        // ── Portal: đăng nhập, kích hoạt (ẩn danh) ──
        var auth = app.MapGroup("/api/portal/auth").WithTags("Portal NCC – đăng nhập").AllowAnonymous();
        auth.MapGet("/invite", (string token, IPortalAuthService s, CancellationToken ct) => s.GetInviteAsync(token, ct));
        auth.MapPost("/activate", async (PortalSetPasswordRequest request, IPortalAuthService s, CancellationToken ct) =>
        {
            await s.ActivateAsync(request, ct);
            return Results.NoContent();
        });
        auth.MapPost("/login", LoginAsync);
        auth.MapPost("/verify-otp", async (PortalVerifyOtpRequest request, IPortalAuthService s, CancellationToken ct) =>
            await s.VerifyOtpAsync(request, ct) is { } token
                ? Results.Ok(token)
                : Results.Problem(
                    title: "Mã xác nhận không đúng hoặc đã hết hạn. Nhập lại, hoặc đăng nhập lại để nhận mã mới.",
                    statusCode: StatusCodes.Status401Unauthorized));
        auth.MapPost("/forgot-password", (ForgotPasswordBody body, HttpContext http, IPortalAuthService s, CancellationToken ct) =>
            s.ForgotPasswordAsync(body.Email, RequestBaseUrl(http), ct));
        auth.MapPost("/reset-password", async (PortalSetPasswordRequest request, IPortalAuthService s, CancellationToken ct) =>
        {
            await s.ResetPasswordAsync(request, ct);
            return Results.NoContent();
        });

        // ── Portal: đã đăng nhập (token Portal) ──
        var portal = app.MapGroup("/api/portal").WithTags("Portal NCC");
        portal.RequireAuthorization(new AuthorizationPolicyBuilder(PortalClaims.Scheme)
            .RequireAuthenticatedUser()
            .RequireClaim(PortalClaims.SupplierId)
            .Build());
        portal.AddEndpointFilter(RequireSessionAsync);
        portal.MapGet("/me", (HttpContext http, IPortalAuthService s, CancellationToken ct) => s.GetMeAsync(Session(http), ct));
        portal.MapPost("/change-password", (PortalChangePasswordRequest request, HttpContext http, IPortalAuthService s, CancellationToken ct) =>
            s.ChangePasswordAsync(Session(http), request, ct));

        // Quản trị NCC quản lý người dùng con của NCC mình – NCC lấy từ phiên, không nhận từ client.
        var users = portal.MapGroup("/users");
        users.MapGet("/", (HttpContext http, ISupplierPortalAccountService s, CancellationToken ct) =>
            s.ListAsync(Session(http).SupplierId, ct));
        users.MapPost("/", (PortalInviteRequest request, HttpContext http, ISupplierPortalAccountService s, CancellationToken ct) =>
            s.InviteAsync(Session(http).SupplierId, request, Session(http), RequestBaseUrl(http), ct));
        users.MapPost("/{userId:guid}/resend-invite", (Guid userId, HttpContext http, ISupplierPortalAccountService s, CancellationToken ct) =>
            s.ResendInviteAsync(Session(http).SupplierId, userId, Session(http), RequestBaseUrl(http), ct));
        users.MapPost("/{userId:guid}/disable", (Guid userId, HttpContext http, ISupplierPortalAccountService s, CancellationToken ct) =>
            s.DisableAsync(Session(http).SupplierId, userId, Session(http), ct));
        users.MapPost("/{userId:guid}/enable", (Guid userId, HttpContext http, ISupplierPortalAccountService s, CancellationToken ct) =>
            s.EnableAsync(Session(http).SupplierId, userId, Session(http), ct));

        // Lát P3a: thư mời báo giá và nộp báo giá niêm phong – mọi người dùng Portal của NCC (không riêng quản trị NCC).
        var rfqs = portal.MapGroup("/rfqs");
        rfqs.MapGet("/", (HttpContext http, IPortalRfqService s, CancellationToken ct) => s.ListAsync(Session(http), ct));
        rfqs.MapGet("/{id:guid}", (Guid id, HttpContext http, IPortalRfqService s, CancellationToken ct) => s.GetAsync(Session(http), id, ct));
        rfqs.MapPost("/{id:guid}/quotes", (Guid id, PortalSubmitQuoteRequest request, HttpContext http, IPortalRfqService s, CancellationToken ct) =>
            s.SubmitAsync(Session(http), id, request, ct));
        // Lát P3b-1: file báo giá (2 MB/file, kiểm nội dung như mọi đính kèm); thêm, xóa đến giờ hạn nộp.
        // Lát P3b-2: NCC gửi câu hỏi làm rõ (trả lời riêng NCC hỏi; thông báo làm rõ công khai trả kèm chi tiết thư mời).
        rfqs.MapPost("/{id:guid}/questions", (Guid id, PortalAskRequest request, HttpContext http, IPortalRfqService s, CancellationToken ct) =>
            s.AskAsync(Session(http), id, request, ct));
        rfqs.MapGet("/{id:guid}/attachments", (Guid id, HttpContext http, IPortalRfqService s, CancellationToken ct) =>
            s.ListAttachmentsAsync(Session(http), id, ct));
        rfqs.MapPost("/{id:guid}/attachments", async (
                Guid id, [FromForm] string? note, IFormFile file, HttpContext http, IPortalRfqService s, CancellationToken ct) =>
            {
                await using var stream = file.OpenReadStream();
                return Results.Ok(await s.UploadAttachmentAsync(Session(http), id, file.FileName, file.Length, stream, note, ct));
            })
            // Token Portal đi trong header, không dùng cookie – không có nguy cơ CSRF (như API đính kèm nội bộ).
            .DisableAntiforgery();
        rfqs.MapGet("/{id:guid}/attachments/{attachmentId:guid}/content", async (
                Guid id, Guid attachmentId, HttpContext http, IPortalRfqService s, CancellationToken ct) =>
            {
                var file = await s.OpenAttachmentAsync(Session(http), id, attachmentId, ct);
                return Results.File(file.Content, file.ContentType, file.FileName);
            });
        rfqs.MapDelete("/{id:guid}/attachments/{attachmentId:guid}", async (
                Guid id, Guid attachmentId, HttpContext http, IPortalRfqService s, CancellationToken ct) =>
            {
                await s.DeleteAttachmentAsync(Session(http), id, attachmentId, ct);
                return Results.NoContent();
            });

        // Lát P6b: trạng thái thanh toán – NCC xem hóa đơn bên đặt đã ghi nhận, đợt thanh toán đã duyệt / đã chi (chỉ đọc).
        var payments = portal.MapGroup("/payments");
        payments.MapGet("/", (HttpContext http, IPortalPaymentService s, CancellationToken ct) => s.ListAsync(Session(http), ct));
        payments.MapGet("/{orderId:guid}", (Guid orderId, HttpContext http, IPortalPaymentService s, CancellationToken ct) =>
            s.GetAsync(Session(http), orderId, ct));

        // Lát P7a: bảng giá – NCC xem bảng giá đã duyệt của mình (chỉ đọc).
        var priceLists = portal.MapGroup("/price-lists");
        priceLists.MapGet("/", (HttpContext http, IPortalPriceListService s, CancellationToken ct) => s.ListAsync(Session(http), ct));
        priceLists.MapGet("/{id:guid}", (Guid id, HttpContext http, IPortalPriceListService s, CancellationToken ct) =>
            s.GetAsync(Session(http), id, ct));

        // Lát P4a: đơn đặt hàng – NCC xem PO đã duyệt, xác nhận / từ chối, gửi file kèm phản hồi (mọi người dùng Portal của NCC).
        var orders = portal.MapGroup("/orders");
        orders.MapGet("/", (HttpContext http, IPortalOrderService s, CancellationToken ct) => s.ListAsync(Session(http), ct));
        orders.MapGet("/{id:guid}", (Guid id, HttpContext http, IPortalOrderService s, CancellationToken ct) => s.GetAsync(Session(http), id, ct));
        orders.MapPost("/{id:guid}/respond", (Guid id, PortalOrderRespondRequest request, HttpContext http, IPortalOrderService s, CancellationToken ct) =>
            s.RespondAsync(Session(http), id, request, ct));
        orders.MapGet("/{id:guid}/attachments", (Guid id, HttpContext http, IPortalOrderService s, CancellationToken ct) =>
            s.ListAttachmentsAsync(Session(http), id, ct));
        orders.MapPost("/{id:guid}/attachments", async (
                Guid id, [FromForm] string? note, IFormFile file, HttpContext http, IPortalOrderService s, CancellationToken ct) =>
            {
                await using var stream = file.OpenReadStream();
                return Results.Ok(await s.UploadAttachmentAsync(Session(http), id, file.FileName, file.Length, stream, note, ct));
            })
            // Token Portal đi trong header, không dùng cookie – không có nguy cơ CSRF.
            .DisableAntiforgery();
        orders.MapGet("/{id:guid}/attachments/{attachmentId:guid}/content", async (
                Guid id, Guid attachmentId, HttpContext http, IPortalOrderService s, CancellationToken ct) =>
            {
                var file = await s.OpenAttachmentAsync(Session(http), id, attachmentId, ct);
                return Results.File(file.Content, file.ContentType, file.FileName);
            });
        orders.MapDelete("/{id:guid}/attachments/{attachmentId:guid}", async (
                Guid id, Guid attachmentId, HttpContext http, IPortalOrderService s, CancellationToken ct) =>
            {
                await s.DeleteAttachmentAsync(Session(http), id, attachmentId, ct);
                return Results.NoContent();
            });

        // Lát P2a: hồ sơ công ty – xem (mọi người dùng NCC), gửi / rút đề nghị cập nhật và file kèm (quản trị NCC).
        var profile = portal.MapGroup("/profile");
        profile.MapGet("/", (HttpContext http, IPortalProfileService s, CancellationToken ct) => s.GetAsync(Session(http), ct));
        profile.MapPost("/requests", (PortalProfileUpdateRequest request, HttpContext http, IPortalProfileService s, CancellationToken ct) =>
            s.SubmitAsync(Session(http), request, ct));
        profile.MapPost("/requests/{id:guid}/withdraw", (
                Guid id, SupplierProfileCommentRequest request, HttpContext http, IPortalProfileService s, CancellationToken ct) =>
            s.WithdrawAsync(Session(http), id, request, ct));
        profile.MapGet("/requests/{id:guid}/attachments", (Guid id, HttpContext http, IPortalProfileService s, CancellationToken ct) =>
            s.ListAttachmentsAsync(Session(http), id, ct));
        profile.MapPost("/requests/{id:guid}/attachments", async (
                Guid id, [FromForm] string? note, IFormFile file, HttpContext http, IPortalProfileService s, CancellationToken ct) =>
            {
                await using var stream = file.OpenReadStream();
                return Results.Ok(await s.UploadAttachmentAsync(Session(http), id, file.FileName, file.Length, stream, note, ct));
            })
            // Token Portal đi trong header, không dùng cookie – không có nguy cơ CSRF.
            .DisableAntiforgery();
        profile.MapGet("/requests/{id:guid}/attachments/{attachmentId:guid}/content", async (
                Guid id, Guid attachmentId, HttpContext http, IPortalProfileService s, CancellationToken ct) =>
            {
                var file = await s.OpenAttachmentAsync(Session(http), id, attachmentId, ct);
                return Results.File(file.Content, file.ContentType, file.FileName);
            });
        profile.MapDelete("/requests/{id:guid}/attachments/{attachmentId:guid}", async (
                Guid id, Guid attachmentId, HttpContext http, IPortalProfileService s, CancellationToken ct) =>
            {
                await s.DeleteAttachmentAsync(Session(http), id, attachmentId, ct);
                return Results.NoContent();
            });

        // Lát P5: NCC báo giao hàng theo đơn – gửi, hủy phiếu báo giao, file kèm (phiếu giao, CO/CQ).
        orders.MapPost("/{id:guid}/delivery-notices", (
                Guid id, PortalDeliveryNoticeRequest request, HttpContext http, IPortalDeliveryNoticeService s, CancellationToken ct) =>
            s.CreateAsync(Session(http), id, request, ct));
        orders.MapPost("/{id:guid}/delivery-notices/{noticeId:guid}/cancel", (
                Guid id, Guid noticeId, PortalCancelDeliveryNoticeRequest request, HttpContext http, IPortalDeliveryNoticeService s,
                CancellationToken ct) =>
            s.CancelAsync(Session(http), id, noticeId, request, ct));
        orders.MapGet("/{id:guid}/delivery-notices/{noticeId:guid}/attachments", (
                Guid id, Guid noticeId, HttpContext http, IPortalDeliveryNoticeService s, CancellationToken ct) =>
            s.ListAttachmentsAsync(Session(http), id, noticeId, ct));
        orders.MapPost("/{id:guid}/delivery-notices/{noticeId:guid}/attachments", async (
                Guid id, Guid noticeId, [FromForm] string? note, IFormFile file, HttpContext http, IPortalDeliveryNoticeService s,
                CancellationToken ct) =>
            {
                await using var stream = file.OpenReadStream();
                return Results.Ok(await s.UploadAttachmentAsync(Session(http), id, noticeId, file.FileName, file.Length, stream, note, ct));
            })
            // Token Portal đi trong header, không dùng cookie – không có nguy cơ CSRF.
            .DisableAntiforgery();
        orders.MapGet("/{id:guid}/delivery-notices/{noticeId:guid}/attachments/{attachmentId:guid}/content", async (
                Guid id, Guid noticeId, Guid attachmentId, HttpContext http, IPortalDeliveryNoticeService s, CancellationToken ct) =>
            {
                var file = await s.OpenAttachmentAsync(Session(http), id, noticeId, attachmentId, ct);
                return Results.File(file.Content, file.ContentType, file.FileName);
            });
        orders.MapDelete("/{id:guid}/delivery-notices/{noticeId:guid}/attachments/{attachmentId:guid}", async (
                Guid id, Guid noticeId, Guid attachmentId, HttpContext http, IPortalDeliveryNoticeService s, CancellationToken ct) =>
            {
                await s.DeleteAttachmentAsync(Session(http), id, noticeId, attachmentId, ct);
                return Results.NoContent();
            });
        return app;
    }

    /// <summary>Email gửi khi quên mật khẩu.</summary>
    /// <param name="Email">Email đăng nhập.</param>
    public sealed record ForgotPasswordBody(string Email);

    private static async Task<IResult> LoginAsync(PortalLoginRequest request, IPortalAuthService s, CancellationToken ct)
    {
        var outcome = await s.LoginAsync(request, ct);
        if (outcome.Challenge is { } challenge) return Results.Ok(challenge);
        return outcome.Failure switch
        {
            PortalLoginFailure.LockedOut => Results.Problem(
                title: "Tài khoản tạm khóa do nhập sai mật khẩu nhiều lần. Thử lại sau "
                    + $"{(outcome.LockoutEnd is { } end ? TimeZoneInfo.ConvertTime(end, VietnamTime.Zone).ToString("HH:mm dd/MM/yyyy") : "ít phút")}.",
                statusCode: StatusCodes.Status423Locked),
            PortalLoginFailure.Disabled => Results.Problem(
                title: "Tài khoản đã bị khóa. Liên hệ Phòng Cung ứng vật tư.", statusCode: StatusCodes.Status403Forbidden),
            _ => Results.Problem(title: "Email hoặc mật khẩu không đúng.", statusCode: StatusCodes.Status401Unauthorized),
        };
    }

    /// <summary>
    /// Mỗi request Portal kiểm lại tài khoản trong CSDL: bị khóa, đổi mật khẩu (dấu bảo mật đổi) hoặc NCC vào danh sách đen thì token cũ
    /// hết hiệu lực ngay, không chờ hết hạn.
    /// </summary>
    private static async ValueTask<object?> RequireSessionAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        var unauthorized = Results.Problem(
            title: "Phiên đăng nhập Portal không còn hiệu lực. Hãy đăng nhập lại.", statusCode: StatusCodes.Status401Unauthorized);
        if (!Guid.TryParse(http.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var userId)) return unauthorized;
        var service = http.RequestServices.GetRequiredService<IPortalAuthService>();
        var session = await service.ValidateSessionAsync(userId, http.User.FindFirst(PortalClaims.SecurityStamp)?.Value, http.RequestAborted);
        if (session is null) return unauthorized;
        http.Items[SessionItem] = session;
        return await next(context);
    }

    private static PortalSession Session(HttpContext http) => (PortalSession)http.Items[SessionItem]!;

    /// <summary>
    /// Địa chỉ trang đang mở (header Origin của trình duyệt – đúng cổng giao diện cả khi chạy Vite riêng cổng 5173), dự phòng scheme + host
    /// của request. Chỉ dùng khi chưa cấu hình Portal:BaseUrl / Email:AppBaseUrl.
    /// </summary>
    private static string RequestBaseUrl(HttpContext http)
    {
        var origin = http.Request.Headers.Origin.ToString();
        return Uri.TryCreate(origin, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            ? uri.GetLeftPart(UriPartial.Authority)
            : $"{http.Request.Scheme}://{http.Request.Host}";
    }
}
