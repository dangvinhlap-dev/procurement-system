using Proc.Application.Notifications;

namespace Proc.Api.Endpoints;

/// <summary>
/// Endpoint thông báo của người đăng nhập (design.md mục 11.6). Ai đăng nhập cũng dùng được (chỉ thấy thông báo của chính mình tại công
/// ty đang làm việc – kiểm trong dịch vụ); không có API tạo thông báo.
/// </summary>
public static class NotificationEndpoints
{
    /// <summary>
    /// Khai báo các endpoint.
    /// </summary>
    /// <param name="app">Bộ định tuyến endpoint.</param>
    /// <returns>Chính <paramref name="app"/>.</returns>
    public static IEndpointRouteBuilder MapNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/notifications").WithTags("Thông báo");

        group.MapGet("/", (bool? unreadOnly, INotificationService s, CancellationToken ct) => s.ListMineAsync(unreadOnly == true, ct));

        group.MapPost("/{id:guid}/read", async (Guid id, INotificationService s, CancellationToken ct) =>
        {
            await s.MarkReadAsync(id, ct);
            return Results.NoContent();
        });

        group.MapPost("/read-all", async (INotificationService s, CancellationToken ct) =>
        {
            await s.MarkAllReadAsync(ct);
            return Results.NoContent();
        });

        return app;
    }
}
