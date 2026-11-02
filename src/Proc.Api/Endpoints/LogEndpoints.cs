using Proc.Api.Security;
using Proc.Application.Logging;
using Proc.Domain.Organization;

namespace Proc.Api.Endpoints;

/// <summary>
/// Endpoint đọc nhật ký thao tác và nhật ký truy cập (design.md mục 9.3). Chỉ đọc – không có API sửa/xóa nhật ký.
/// </summary>
public static class LogEndpoints
{
    /// <summary>
    /// Khai báo các endpoint.
    /// </summary>
    /// <param name="app">Bộ định tuyến endpoint.</param>
    /// <returns>Chính <paramref name="app"/>.</returns>
    public static IEndpointRouteBuilder MapLogEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api").WithTags("Nhật ký");

        group.MapGet("/audit-logs", (
                DateOnly from, DateOnly to, string? userName, string? entityType, string? action,
                ILogQueries logs, CancellationToken ct) =>
                logs.SearchAuditAsync(new AuditLogQuery(from, to, userName, entityType, action), ct))
            .RequirePermission(Permissions.System.LogView);

        group.MapGet("/audit-logs/{id:guid}", (Guid id, ILogQueries logs, CancellationToken ct) => logs.GetAuditAsync(id, ct))
            .RequirePermission(Permissions.System.LogView);

        group.MapGet("/access-logs", (
                DateOnly from, DateOnly to, string? userName, AccessEvent? @event,
                ILogQueries logs, CancellationToken ct) =>
                logs.SearchAccessAsync(new AccessLogQuery(from, to, userName, @event), ct))
            .RequirePermission(Permissions.System.LogView);

        // Lịch sử thay đổi theo ô (F1) và theo bản ghi (design.md mục 9.3a): ai đăng nhập cũng xem được – không cần quyền xem nhật ký;
        // phạm vi công ty/kho/nhóm hàng kiểm tra ở tầng truy vấn qua bản ghi được xem.
        group.MapGet("/change-history/field", (
                string entityType, string entityId, string field, IChangeHistoryQueries history, CancellationToken ct) =>
                history.GetFieldHistoryAsync(entityType, entityId, field, ct))
            .RequireAuthorization();

        group.MapGet("/change-history/record", (
                string entityType, string entityId, IChangeHistoryQueries history, CancellationToken ct) =>
                history.GetRecordHistoryAsync(entityType, entityId, ct))
            .RequireAuthorization();

        return app;
    }
}
