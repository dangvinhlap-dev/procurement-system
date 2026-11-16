using System.Text.Json;
using Proc.Api.Security;
using Proc.Application.Common;
using Proc.Application.Operations;
using Proc.Domain.Operations;
using Proc.Domain.Organization;

namespace Proc.Api.Endpoints;

/// <summary>
/// API module Quản trị vận hành (design.md mục 21, lát QT1) dưới <c>/api/ops/v1</c>. Chỉ <b>thêm</b> trường, không sửa / bỏ (máy TEST có thể chạy
/// bản mới hơn PROD – mục 21.8 T3). Mọi API cần quyền <c>System.Ops.*</c> và chỉ dùng ở tổng công ty (N3); riêng <c>environment</c> ẩn danh để
/// trang đăng nhập vẽ dải màu môi trường.
/// </summary>
public static class OperationsEndpoints
{
    /// <summary>Khai báo các endpoint.</summary>
    /// <param name="app">Bộ định tuyến endpoint.</param>
    /// <returns>Chính <paramref name="app"/>.</returns>
    public static IEndpointRouteBuilder MapOperationsEndpoints(this IEndpointRouteBuilder app)
    {
        var ops = app.MapGroup("/api/ops/v1").WithTags("Quản trị vận hành");

        // Ẩn danh: tên môi trường (Dev / Test / Prod / None) và cảnh báo cấu hình để dải màu hiện cả ở trang đăng nhập – không phiên bản, không
        // bí mật.
        ops.MapGet("/environment", GetEnvironmentAsync)
            .AllowAnonymous();
        // Site Portal NCC chỉ mở /api/portal (PortalHostMiddleware – N3: không có chức năng QT trên Portal): cùng nội dung để dải TEST hiện cả
        // trên Portal của máy TEST; nằm dưới /api/portal/auth vì API Portal ẩn danh chỉ được ở nhóm đó (PortalInfrastructureTests).
        app.MapGet("/api/portal/auth/environment", GetEnvironmentAsync)
            .WithTags("Portal NCC")
            .AllowAnonymous();

        ops.MapGet("/overview", (ISystemOverview s, CancellationToken ct) => s.GetAsync(ct))
            .RequirePermission(Permissions.System.OpsView)
            .RequireHeadOffice();

        ops.MapGet("/operation-logs", (DateOnly from, DateOnly to, IOperationLogQueries s, CancellationToken ct) => s.SearchAsync(from, to, ct))
            .RequirePermission(Permissions.System.OpsView)
            .RequireHeadOffice();

        // Tình trạng lưu WAL (màn hình BAK – lát QT3a).
        ops.MapGet("/backup/wal-status", (IWalArchiveStatus s, CancellationToken ct) => s.GetAsync(ct))
            .RequirePermission(Permissions.System.OpsBackupView)
            .RequireHeadOffice();

        // Lịch sử sao lưu, "Sao lưu ngay" (lát QT3b). Không có API tải file sao lưu (Q5 – file chứa toàn bộ dữ liệu, chỉ lấy trên máy chủ).
        ops.MapGet("/backup/runs", (DateOnly from, DateOnly to, IBackupService s, CancellationToken ct) => s.SearchAsync(from, to, ct))
            .RequirePermission(Permissions.System.OpsBackupView)
            .RequireHeadOffice();

        ops.MapGet("/backup/overview", (IBackupService s, CancellationToken ct) => s.GetOverviewAsync(ct))
            .RequirePermission(Permissions.System.OpsBackupView)
            .RequireHeadOffice();

        ops.MapGet("/backup/runs/{id:guid}", (Guid id, IBackupService s, CancellationToken ct) => s.GetAsync(id, ct))
            .RequirePermission(Permissions.System.OpsBackupView)
            .RequireHeadOffice();

        ops.MapPost("/backup/runs", (QueueBackupRequest request, IBackupService s, CancellationToken ct) => s.QueueAsync(request, ct))
            .RequirePermission(Permissions.System.OpsBackupRun)
            .RequireHeadOffice();

        // Bảo trì CSDL – màn hình DBM (lát QT4). Ngắt phiên còn phải xác nhận Q6 (mật khẩu + lý do + gõ "NGẮT PHIÊN") ở tầng Application.
        var dbm = ops.MapGroup("/db-maintenance");
        dbm.MapGet("/tables", (IDbMaintenanceService s, CancellationToken ct) => s.GetTablesAsync(ct))
            .RequirePermission(Permissions.System.OpsMaintenanceView)
            .RequireHeadOffice();
        dbm.MapGet("/sessions", (IDbMaintenanceService s, CancellationToken ct) => s.GetSessionsAsync(ct))
            .RequirePermission(Permissions.System.OpsMaintenanceView)
            .RequireHeadOffice();
        dbm.MapGet("/runs", (DateOnly from, DateOnly to, IDbMaintenanceService s, CancellationToken ct) => s.SearchRunsAsync(from, to, ct))
            .RequirePermission(Permissions.System.OpsMaintenanceView)
            .RequireHeadOffice();
        dbm.MapGet("/runs/{id:guid}", (Guid id, IDbMaintenanceService s, CancellationToken ct) => s.GetRunAsync(id, ct))
            .RequirePermission(Permissions.System.OpsMaintenanceView)
            .RequireHeadOffice();
        dbm.MapPost("/runs", (QueueMaintenanceRequest request, IDbMaintenanceService s, CancellationToken ct) => s.QueueAsync(request, ct))
            .RequirePermission(Permissions.System.OpsMaintenanceRun)
            .RequireHeadOffice();
        dbm.MapPost("/sessions/terminate", (TerminateSessionRequest request, IDbMaintenanceService s, CancellationToken ct) =>
                s.TerminateAsync(request, ct))
            .RequirePermission(Permissions.System.OpsMaintenanceRun)
            .RequireHeadOffice();

        // Chế độ bảo trì (lát QT5): xem – mọi quyền xem vận hành; bật / tắt – quyền riêng.
        ops.MapGet("/maintenance-mode", (IMaintenanceMode m, CancellationToken ct) => m.GetAsync(ct))
            .RequireAnyPermission(Permissions.System.OpsView, Permissions.System.OpsMaintenanceModeManage)
            .RequireHeadOffice();
        ops.MapPut("/maintenance-mode", (SetMaintenanceModeRequest request, IMaintenanceMode m, ICurrentUser user, CancellationToken ct) =>
                m.SetAsync(request, user.UserName, ct))
            .RequirePermission(Permissions.System.OpsMaintenanceModeManage)
            .RequireHeadOffice();

        // Cấu hình truy cập – màn hình CFG (lát QT7): xem – quyền xem vận hành hoặc quản lý CFG; sửa, đăng xuất tất cả (Q6) – quyền quản lý CFG.
        var cfg = ops.MapGroup("/access-config");
        cfg.MapGet("", (HttpContext http, IAccessConfigService s, CancellationToken ct) => s.GetAsync(http.Request.IsHttps, ct))
            .RequireAnyPermission(Permissions.System.OpsView, Permissions.System.OpsConfigManage)
            .RequireHeadOffice();
        cfg.MapPut("", (SaveAccessConfigRequest request, HttpContext http, IAccessConfigService s, CancellationToken ct) =>
                s.SaveAsync(request, http.Request.IsHttps, ct))
            .RequirePermission(Permissions.System.OpsConfigManage)
            .RequireHeadOffice();
        cfg.MapPost("/sign-out-all", (SignOutAllRequest request, IAccessConfigService s, CancellationToken ct) => s.SignOutAllAsync(request, ct))
            .RequirePermission(Permissions.System.OpsConfigManage)
            .RequireHeadOffice();

        // Quản trị bộ thực thi – màn hình AGT (lát QT5).
        var agt = ops.MapGroup("/agent-admin");
        agt.MapGet("/environments", (IAgentAdminService s, CancellationToken ct) => s.GetEnvironmentsAsync(ct))
            .RequirePermission(Permissions.System.OpsAgentView)
            .RequireHeadOffice();
        agt.MapPost("/environments", (SaveAgentEnvironmentRequest request, IAgentAdminService s, CancellationToken ct) =>
                s.CreateEnvironmentAsync(request, ct))
            .RequirePermission(Permissions.System.OpsAgentManage)
            .RequireHeadOffice();
        agt.MapPut("/environments/{id:guid}", (Guid id, SaveAgentEnvironmentRequest request, IAgentAdminService s, CancellationToken ct) =>
                s.UpdateEnvironmentAsync(id, request, ct))
            .RequirePermission(Permissions.System.OpsAgentManage)
            .RequireHeadOffice();
        agt.MapPost("/environments/{id:guid}/key", (Guid id, CreateAgentKeyRequest request, IAgentAdminService s, CancellationToken ct) =>
                s.CreateKeyAsync(id, request, ct))
            .RequirePermission(Permissions.System.OpsAgentManage)
            .RequireHeadOffice();
        // Lát QT9: khóa dịch vụ (máy đó gọi máy này – TEST chuyển báo lỗi về PROD), cùng xác nhận Q6 "TẠO KHÓA".
        agt.MapPost("/environments/{id:guid}/service-key", (Guid id, CreateAgentKeyRequest request, IAgentAdminService s, CancellationToken ct) =>
                s.CreateServiceKeyAsync(id, request, ct))
            .RequirePermission(Permissions.System.OpsAgentManage)
            .RequireHeadOffice();
        agt.MapGet("/commands", (DateOnly from, DateOnly to, Guid? environmentId, IAgentAdminService s, CancellationToken ct) =>
                s.SearchCommandsAsync(from, to, environmentId, ct))
            .RequirePermission(Permissions.System.OpsAgentView)
            .RequireHeadOffice();
        agt.MapGet("/commands/{id:guid}", (Guid id, IAgentAdminService s, CancellationToken ct) => s.GetCommandAsync(id, ct))
            .RequirePermission(Permissions.System.OpsAgentView)
            .RequireHeadOffice();
        agt.MapPost("/commands", (QueueAgentCommandRequest request, IAgentAdminService s, CancellationToken ct) => s.QueueAsync(request, ct))
            .RequirePermission(Permissions.System.OpsAgentManage)
            .RequireHeadOffice();
        agt.MapPost("/commands/{id:guid}/cancel", (Guid id, IAgentAdminService s, CancellationToken ct) => s.CancelAsync(id, ct))
            .RequirePermission(Permissions.System.OpsAgentManage)
            .RequireHeadOffice();

        // Phục hồi (lát QT6) – màn hình RST. Hoán đổi xác nhận Q6 ("KHÔI PHỤC") ở dịch vụ.
        var rst = ops.MapGroup("/restore");
        rst.MapGet("/overview", (IRestoreService s, CancellationToken ct) => s.GetOverviewAsync(ct))
            .RequirePermission(Permissions.System.OpsRestoreView)
            .RequireHeadOffice();
        rst.MapGet("/plan", (DateTimeOffset target, IRestoreService s, CancellationToken ct) => s.PlanAsync(target, ct))
            .RequirePermission(Permissions.System.OpsRestoreView)
            .RequireHeadOffice();
        rst.MapGet("/runs", (DateOnly from, DateOnly to, IRestoreService s, CancellationToken ct) => s.SearchAsync(from, to, ct))
            .RequirePermission(Permissions.System.OpsRestoreView)
            .RequireHeadOffice();
        rst.MapGet("/runs/{id:guid}", (Guid id, IRestoreService s, CancellationToken ct) => s.GetAsync(id, ct))
            .RequirePermission(Permissions.System.OpsRestoreView)
            .RequireHeadOffice();
        rst.MapPost("/runs", (QueueRestoreRequest request, IRestoreService s, CancellationToken ct) => s.QueueAsync(request, ct))
            .RequirePermission(Permissions.System.OpsRestoreRun)
            .RequireHeadOffice();
        rst.MapPost("/runs/{id:guid}/compare", (Guid id, IRestoreService s, CancellationToken ct) => s.CompareAsync(id, ct))
            .RequirePermission(Permissions.System.OpsRestoreRun)
            .RequireHeadOffice();
        rst.MapPost("/runs/{id:guid}/discard", (Guid id, IRestoreService s, CancellationToken ct) => s.DiscardAsync(id, ct))
            .RequirePermission(Permissions.System.OpsRestoreRun)
            .RequireHeadOffice();
        rst.MapPost("/runs/{id:guid}/swap", (Guid id, SwapRestoreRequest request, IRestoreService s, CancellationToken ct) =>
                s.SwapAsync(id, request, ct))
            .RequirePermission(Permissions.System.OpsRestoreRun)
            .RequireHeadOffice();

        // Làm mới TEST từ PROD (lát QT9) – màn hình TST. Giao lệnh xác nhận Q6 ("LÀM MỚI TEST") ở dịch vụ.
        var tst = ops.MapGroup("/test-refresh");
        tst.MapGet("/overview", (ITestRefreshService s, CancellationToken ct) => s.GetOverviewAsync(ct))
            .RequireAnyPermission(Permissions.System.OpsView, Permissions.System.OpsTestRefreshRun)
            .RequireHeadOffice();
        tst.MapGet("/runs", (DateOnly from, DateOnly to, ITestRefreshService s, CancellationToken ct) => s.SearchAsync(from, to, ct))
            .RequireAnyPermission(Permissions.System.OpsView, Permissions.System.OpsTestRefreshRun)
            .RequireHeadOffice();
        tst.MapPost("/runs", (RunTestRefreshRequest request, ITestRefreshService s, CancellationToken ct) => s.QueueAsync(request, ct))
            .RequirePermission(Permissions.System.OpsTestRefreshRun)
            .RequireHeadOffice();

        // Nút "Báo lỗi / góp ý" trên máy TEST (lát QT9): mọi người đã đăng nhập ở mọi công ty (người thử là người dùng thường); máy chủ TEST chuyển
        // về PROD bằng khóa dịch vụ trong appsettings.
        ops.MapPost("/feedback", (SendFeedbackRequest request, ITestFeedbackService s, CancellationToken ct) => s.SendAsync(request, ct));

        // Cổng máy chủ – máy chủ (lát QT9): không JWT – khóa dịch vụ X-Ops-Service-Key (băm lưu CSDL, tách khóa bộ thực thi).
        var service = ops.MapGroup("/service").AllowAnonymous().AddEndpointFilter(AuthenticateServiceAsync);
        service.MapPost("/defects", (ForwardDefectRequest request, HttpContext http, IReleaseDefectInbox inbox, CancellationToken ct) =>
            inbox.ReceiveAsync((string)http.Items[ServiceEnvironmentItem]!, request, ct));

        // Cổng cho bộ thực thi (lát QT5): không JWT – xác thực bằng khóa X-Ops-Agent-Key (băm lưu CSDL); không có phiên người dùng, công ty.
        var agent = ops.MapGroup("/agent").AllowAnonymous().AddEndpointFilter(AuthenticateAgentAsync);
        agent.MapGet("/commands", PollAgentAsync);
        agent.MapPost("/commands/{id:guid}/result", async (Guid id, AgentResultRequest result, HttpContext http, IAgentGateway gateway,
            CancellationToken ct) =>
        {
            await gateway.ReportAsync(AgentEnvironmentId(http), id, result, ct);
            return Results.NoContent();
        });
        // Lát QT6: trước khi dừng site để hoán đổi, bộ thực thi xin máy chủ bật bảo trì + chép bảng vận hành sang bản khôi phục.
        agent.MapPost("/commands/{id:guid}/prepare-swap", async (Guid id, HttpContext http, IAgentGateway gateway, CancellationToken ct) =>
        {
            await gateway.PrepareSwapAsync(AgentEnvironmentId(http), id, ct);
            return Results.NoContent();
        });
        // Lát QT8: tải gói của phiếu phát hành đang cài (chỉ khi môi trường này đang chạy lệnh cài phiếu đó) và kiểm thử nhanh sau khi cài.
        agent.MapGet("/releases/{ticketId:guid}/package", async (Guid ticketId, HttpContext http, IReleaseAgentHooks release, CancellationToken ct) =>
        {
            var file = await release.OpenPackageAsync(AgentEnvironmentId(http), ticketId, ct);
            http.Response.Headers["X-Package-Sha256"] = file.Sha256;
            return Results.File(file.Path, "application/zip", file.FileName, enableRangeProcessing: true);
        });
        agent.MapGet("/smoke", (IReleaseAgentHooks release, CancellationToken ct) => release.SmokeAsync(ct));
        // Lát QT9: bộ thực thi TEST tải bản pg_dump và file đính kèm của máy này – chỉ khi chính môi trường đó đang chạy lệnh làm mới.
        agent.MapGet("/test-refresh/dump", async (HttpContext http, ITestRefreshAgentHooks hooks, CancellationToken ct) =>
        {
            var file = await hooks.OpenDumpAsync(AgentEnvironmentId(http), ct);
            http.Response.Headers["X-Package-Sha256"] = file.Sha256;
            return Results.File(file.Path, "application/octet-stream", file.FileName, enableRangeProcessing: true);
        });
        agent.MapGet("/test-refresh/attachments", (HttpContext http, ITestRefreshAgentHooks hooks, CancellationToken ct) =>
            hooks.GetAttachmentManifestAsync(AgentEnvironmentId(http), ct));
        agent.MapGet("/test-refresh/attachments/file", async (string? path, HttpContext http, ITestRefreshAgentHooks hooks, CancellationToken ct) =>
            Results.File(await hooks.OpenAttachmentAsync(AgentEnvironmentId(http), path, ct), "application/octet-stream"));

        // Nhật ký lỗi (lát QT2). Gửi lỗi giao diện: mọi người đã đăng nhập ở mọi công ty (lỗi xảy ra ở đâu cũng phải ghi), ≤ 8 KB, giới hạn
        // tần suất ở ApiRateLimiting; xem và xử lý chỉ ở tổng công ty.
        ops.MapPost("/client-errors", ReportClientErrorAsync);

        ops.MapGet("/error-groups", (DateOnly from, DateOnly to, ErrorGroupStatus? status, string? trackingCode, IErrorLogService s,
                CancellationToken ct) => s.SearchAsync(from, to, status, trackingCode, ct))
            .RequirePermission(Permissions.System.OpsErrorLogView)
            .RequireHeadOffice();

        ops.MapGet("/error-groups/{id:guid}", (Guid id, IErrorLogService s, CancellationToken ct) => s.GetAsync(id, ct))
            .RequirePermission(Permissions.System.OpsErrorLogView)
            .RequireHeadOffice();

        ops.MapPut("/error-groups/{id:guid}/status", (Guid id, ChangeErrorGroupStatusRequest request, IErrorLogService s, CancellationToken ct) =>
                s.ChangeStatusAsync(id, request, ct))
            .RequirePermission(Permissions.System.OpsErrorLogManage)
            .RequireHeadOffice();

        // Gây lỗi máy chủ thử (nút trên ERR): kiểm tra sau khi cài đặt / nâng cấp rằng lỗi 500 vào được nhật ký và email báo lỗi mới đi được.
        ops.MapPost("/error-groups/test-server-error", ThrowTestServerError)
            .RequirePermission(Permissions.System.OpsErrorLogManage)
            .RequireHeadOffice();

        return app;
    }

    /// <summary>Môi trường + chế độ bảo trì cho dải màu (ẩn danh – chỉ câu thông báo, giờ bật).</summary>
    private static async Task<EnvironmentStatusDto> GetEnvironmentAsync(IEnvironmentStatus status, IMaintenanceMode mode, CancellationToken ct)
    {
        var m = await mode.GetAsync(ct);
        return status.Get() with { Maintenance = m.IsOn ? new MaintenanceNoticeDto(m.Message ?? "", m.StartedAt) : null };
    }

    private const string AgentEnvironmentItem = "ops.agent.environment";

    /// <summary>Bộ lọc nhóm /agent: khóa sai / thiếu / môi trường ngừng dùng → 401 (không nói rõ lý do – không giúp dò khóa).</summary>
    private static async ValueTask<object?> AuthenticateAgentAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        var gateway = http.RequestServices.GetRequiredService<IAgentGateway>();
        var envId = await gateway.AuthenticateAsync(http.Request.Headers[AgentHeaders.Key].ToString(), http.RequestAborted);
        if (envId is null)
            return Results.Problem(title: "Khóa bộ thực thi không hợp lệ.", statusCode: StatusCodes.Status401Unauthorized);
        http.Items[AgentEnvironmentItem] = envId.Value;
        return await next(context);
    }

    private static Guid AgentEnvironmentId(HttpContext http) => (Guid)http.Items[AgentEnvironmentItem]!;

    private const string ServiceEnvironmentItem = "ops.service.environment";

    /// <summary>Bộ lọc nhóm /service (lát QT9): khóa dịch vụ sai / thiếu / môi trường ngừng dùng → 401 cùng một câu.</summary>
    private static async ValueTask<object?> AuthenticateServiceAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        var inbox = http.RequestServices.GetRequiredService<IReleaseDefectInbox>();
        var code = await inbox.AuthenticateAsync(http.Request.Headers[ServiceHeaders.Key].ToString(), http.RequestAborted);
        if (code is null)
            return Results.Problem(title: "Khóa dịch vụ không hợp lệ.", statusCode: StatusCodes.Status401Unauthorized);
        http.Items[ServiceEnvironmentItem] = code;
        return await next(context);
    }

    /// <summary>Bộ thực thi hỏi lệnh: có lệnh trả 200, không có việc trả 204.</summary>
    private static async Task<IResult> PollAgentAsync(
        HttpContext http, IAgentGateway gateway, string? agentVersion, string? appVersion, string? host, bool? peek, CancellationToken ct)
    {
        var work = await gateway.PollAsync(AgentEnvironmentId(http), new AgentPollInfo(agentVersion, appVersion, host, peek == true), ct);
        return work is null ? Results.NoContent() : Results.Ok(work);
    }

    /// <summary>Ném lỗi thử (luôn thành lỗi 500, vào nhật ký lỗi như lỗi thật).</summary>
    private static IResult ThrowTestServerError(ICurrentUser user) =>
        throw new InvalidOperationException($"Lỗi thử do {user.UserName} gây ra từ màn hình Nhật ký lỗi để kiểm tra việc ghi lỗi máy chủ.");

    /// <summary>Kích thước tối đa của một báo lỗi giao diện (byte).</summary>
    public const int ClientErrorMaxBytes = 8 * 1024;

    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Nhận lỗi giao diện. Tự đọc thân request có giới hạn (không để bộ gắn tham số đọc trước cả khối lớn): quá 8 KB trả 413, JSON hỏng trả 400.
    /// Công ty lấy từ phạm vi request đã kiểm tra (X-Company-Id), không tin dữ liệu client.
    /// </summary>
    private static async Task<IResult> ReportClientErrorAsync(
        HttpRequest request, IErrorRecorder recorder, ICurrentUserScope scope, CancellationToken ct)
    {
        if (request.ContentLength > ClientErrorMaxBytes) return TooLarge();
        var buffer = new byte[ClientErrorMaxBytes + 1];
        var read = 0;
        int n;
        while (read < buffer.Length && (n = await request.Body.ReadAsync(buffer.AsMemory(read), ct)) > 0) read += n;
        if (read > ClientErrorMaxBytes) return TooLarge();

        ClientErrorRequest? body;
        try
        {
            body = JsonSerializer.Deserialize<ClientErrorRequest>(buffer.AsSpan(0, read), WebJson);
        }
        catch (JsonException)
        {
            body = null;
        }

        if (body is null || string.IsNullOrWhiteSpace(body.Message))
            return Results.Problem(title: "Báo lỗi giao diện không đúng định dạng.", statusCode: StatusCodes.Status400BadRequest);
        var code = await recorder.RecordClientAsync(body, request.Headers.UserAgent.ToString(), scope.CompanyId, CancellationToken.None);
        return Results.Ok(new ErrorTrackingDto(code));

        static IResult TooLarge() => Results.Problem(
            title: $"Báo lỗi giao diện vượt {ClientErrorMaxBytes / 1024} KB.", statusCode: StatusCodes.Status413PayloadTooLarge);
    }
}
