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
        ops.MapGet("/environment", (IEnvironmentStatus status) => status.Get())
            .AllowAnonymous();
        // Site Portal NCC chỉ mở /api/portal (PortalHostMiddleware – N3: không có chức năng QT trên Portal): cùng nội dung để dải TEST hiện cả
        // trên Portal của máy TEST; nằm dưới /api/portal/auth vì API Portal ẩn danh chỉ được ở nhóm đó (PortalInfrastructureTests).
        app.MapGet("/api/portal/auth/environment", (IEnvironmentStatus status) => status.Get())
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

        ops.MapGet("/backup/runs/{id:guid}", (Guid id, IBackupService s, CancellationToken ct) => s.GetAsync(id, ct))
            .RequirePermission(Permissions.System.OpsBackupView)
            .RequireHeadOffice();

        ops.MapPost("/backup/runs", (QueueBackupRequest request, IBackupService s, CancellationToken ct) => s.QueueAsync(request, ct))
            .RequirePermission(Permissions.System.OpsBackupRun)
            .RequireHeadOffice();

        // Nhật ký lỗi (lát QT2). Gửi lỗi giao diện: mọi người đã đăng nhập ở mọi công ty (lỗi xảy ra ở đâu cũng phải ghi), ≤ 8 KB, giới hạn
        // tần suất ở PortalRateLimiting; xem và xử lý chỉ ở tổng công ty.
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
