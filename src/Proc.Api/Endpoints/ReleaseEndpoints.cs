using Microsoft.AspNetCore.Mvc;
using Proc.Api.Security;
using Proc.Application.Operations;
using Proc.Domain.Organization;

namespace Proc.Api.Endpoints;

/// <summary>
/// API trung tâm phát hành – màn hình REL (lát QT8, design.md mục 21.6) dưới <c>/api/ops/v1/releases</c>, cùng chốt tổng công ty + dải IP như mọi
/// API QT. Phần bộ thực thi gọi (tải gói, kiểm thử nhanh) dưới <c>/api/ops/v1/agent</c> – xác thực bằng khóa bộ thực thi.
/// </summary>
public static class ReleaseEndpoints
{
    /// <summary>Giới hạn body của API tải gói lên (MB) – trần của <c>Release:MaxPackageSizeMb</c>; web.config nới IIS đúng mức này.</summary>
    public const int MaxUploadMb = 250;

    /// <summary>Khai báo các endpoint.</summary>
    /// <param name="app">Bộ định tuyến endpoint.</param>
    /// <returns>Chính <paramref name="app"/>.</returns>
    public static IEndpointRouteBuilder MapReleaseEndpoints(this IEndpointRouteBuilder app)
    {
        var rel = app.MapGroup("/api/ops/v1/releases").WithTags("Trung tâm phát hành");
        string[] view = [Permissions.System.OpsView, Permissions.System.OpsReleaseManage];

        rel.MapGet("/overview", (IReleaseService s, CancellationToken ct) => s.GetOverviewAsync(ct))
            .RequireAnyPermission(view).RequireHeadOffice();
        rel.MapGet("", (IReleaseService s, CancellationToken ct) => s.SearchAsync(ct))
            .RequireAnyPermission(view).RequireHeadOffice();
        rel.MapGet("/{id:guid}", (Guid id, IReleaseService s, CancellationToken ct) => s.GetAsync(id, ct))
            .RequireAnyPermission(view).RequireHeadOffice();

        // Tải gói lên: thân request là chính file zip (không multipart – không bị giới hạn form 128 MB, không đệm cả file vào bộ nhớ).
        rel.MapPost("/upload", (HttpRequest request, string? fileName, string? sha256, string? note, IReleaseService s, CancellationToken ct) =>
                s.ReceiveUploadAsync(request.Body, fileName, sha256, note, ct))
            .WithMetadata(new RequestSizeLimitAttribute((long)MaxUploadMb * 1024 * 1024))
            .RequirePermission(Permissions.System.OpsReleaseManage).RequireHeadOffice();
        rel.MapPost("/github", (ReceiveFromGitHubRequest request, IReleaseService s, CancellationToken ct) => s.ReceiveFromGitHubAsync(request, ct))
            .RequirePermission(Permissions.System.OpsReleaseManage).RequireHeadOffice();

        rel.MapPut("/{id:guid}/info", (Guid id, UpdateReleaseInfoRequest request, IReleaseService s, CancellationToken ct) =>
                s.UpdateInfoAsync(id, request, ct))
            .RequirePermission(Permissions.System.OpsReleaseManage).RequireHeadOffice();
        rel.MapPost("/{id:guid}/notes", (Guid id, AppendReleaseNoteRequest request, IReleaseService s, CancellationToken ct) =>
                s.AppendNoteAsync(id, request, ct))
            .RequirePermission(Permissions.System.OpsReleaseManage).RequireHeadOffice();
        // Ghi kiểm thử, báo lỗi: người kiểm thử UAT thường chỉ có quyền phát hành hoặc xác nhận UAT.
        rel.MapPut("/{id:guid}/items/{itemId:guid}", (Guid id, Guid itemId, RecordReleaseTestRequest request, IReleaseService s, CancellationToken ct) =>
                s.RecordTestAsync(id, itemId, request, ct))
            .RequireAnyPermission(Permissions.System.OpsReleaseManage, Permissions.System.OpsReleaseUatSignOff).RequireHeadOffice();
        rel.MapPost("/{id:guid}/defects", (Guid id, AddReleaseDefectRequest request, IReleaseService s, CancellationToken ct) =>
                s.AddDefectAsync(id, request, ct))
            .RequireAnyPermission(Permissions.System.OpsReleaseManage, Permissions.System.OpsReleaseUatSignOff).RequireHeadOffice();
        rel.MapPut("/{id:guid}/defects/{defectId:guid}", (Guid id, Guid defectId, ResolveReleaseDefectRequest request, IReleaseService s,
                CancellationToken ct) => s.ResolveDefectAsync(id, defectId, request, ct))
            .RequirePermission(Permissions.System.OpsReleaseManage).RequireHeadOffice();

        rel.MapPost("/{id:guid}/uat-sign-off", (Guid id, SignOffReleaseRequest request, IReleaseService s, CancellationToken ct) =>
                s.SignOffAsync(id, request, ct))
            .RequirePermission(Permissions.System.OpsReleaseUatSignOff).RequireHeadOffice();
        rel.MapPost("/{id:guid}/close", (Guid id, IReleaseService s, CancellationToken ct) => s.CloseAsync(id, ct))
            .RequirePermission(Permissions.System.OpsReleaseManage).RequireHeadOffice();
        rel.MapPost("/{id:guid}/cancel", (Guid id, CancelReleaseRequest request, IReleaseService s, CancellationToken ct) =>
                s.CancelAsync(id, request, ct))
            .RequirePermission(Permissions.System.OpsReleaseManage).RequireHeadOffice();

        // Cài: quyền theo môi trường (TEST – DeployTest, PROD – DeployProd); xác nhận Q6 ở tầng Application.
        rel.MapPost("/{id:guid}/deploy", (Guid id, DeployReleaseRequest request, IReleaseService s, Proc.Application.Common.ICurrentUserScope scope,
                CancellationToken ct) =>
            {
                scope.EnsurePermission(string.Equals(request.Environment, "Prod", StringComparison.OrdinalIgnoreCase)
                    ? Permissions.System.OpsReleaseDeployProd
                    : Permissions.System.OpsReleaseDeployTest);
                return s.DeployAsync(id, request, ct);
            })
            .RequireAnyPermission(Permissions.System.OpsReleaseDeployTest, Permissions.System.OpsReleaseDeployProd).RequireHeadOffice();
        // Quay về: TEST (lát QT9) cần quyền cài TEST, PROD (hoặc không ghi môi trường) cần quyền cài PROD.
        rel.MapPost("/{id:guid}/rollback", (Guid id, RollbackReleaseRequest request, IReleaseService s,
                Proc.Application.Common.ICurrentUserScope scope, CancellationToken ct) =>
            {
                scope.EnsurePermission(string.Equals(request.Environment, "Test", StringComparison.OrdinalIgnoreCase)
                    ? Permissions.System.OpsReleaseDeployTest
                    : Permissions.System.OpsReleaseDeployProd);
                return s.RollbackAsync(id, request, ct);
            })
            .RequireAnyPermission(Permissions.System.OpsReleaseDeployTest, Permissions.System.OpsReleaseDeployProd).RequireHeadOffice();

        return app;
    }
}
