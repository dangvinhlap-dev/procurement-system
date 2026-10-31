using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Proc.Application.Notifications;
using Proc.Domain.Notifications;
using Proc.Infrastructure.Persistence;

namespace Proc.IntegrationTests;

/// <summary>Gọi API thông báo trong test (dùng chung cho các lớp test luồng nghiệp vụ).</summary>
internal static class NotificationApi
{
    /// <summary>Thông báo của người đăng nhập tại công ty đang làm việc, mới nhất trước.</summary>
    public static async Task<List<NotificationDto>> MineAsync(HttpClient client, CancellationToken ct, bool unreadOnly = false) =>
        (await client.GetFromJsonAsync<List<NotificationDto>>($"/api/notifications{(unreadOnly ? "?unreadOnly=true" : "")}", ct))!;
}

/// <summary>
/// Thông báo (design.md mục 11.6): chỉ người nhận xem và đánh dấu đã đọc; theo công ty đang làm việc; số chưa đọc trên trang chủ.
/// </summary>
[Collection(ApiCollection.Name)]
public class NotificationTests
{
    private readonly ProcApiFactory _factory;

    public NotificationTests(ProcApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Thêm thông báo thẳng vào CSDL cho người dùng tại công ty (mô phỏng sự kiện nghiệp vụ).</summary>
    private async Task<Guid> AddAsync(Guid userId, string companyCode, string title)
    {
        var companyId = await _factory.GetCompanyIdAsync(companyCode);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProcDbContext>();
        var n = Notification.Create(companyId, userId, NotificationTypes.ApprovalPending, title, $"Nội dung {title}", "PR", Guid.CreateVersion7(), DateTimeOffset.UtcNow);
        db.Notifications.Add(n);
        await db.SaveChangesAsync(Ct);
        return n.Id;
    }

    [Fact]
    public async Task ChiNguoiNhanXemVaDanhDauDaDoc_TheoCongTyDangLamViec()
    {
        var me = $"tb.{Guid.NewGuid():N}"[..16];
        var other = $"tb.{Guid.NewGuid():N}"[..16];
        var meId = await _factory.CreateUserAsync(me, ProcApiFactory.TestPassword, ["BHA", "NMI"]);
        var otherId = await _factory.CreateUserAsync(other, ProcApiFactory.TestPassword, ["BHA"]);
        var first = await AddAsync(meId, "BHA", "Thông báo 1");
        var second = await AddAsync(meId, "BHA", "Thông báo 2");
        var atNmi = await AddAsync(meId, "NMI", "Thông báo NMI");
        var others = await AddAsync(otherId, "BHA", "Của người khác");

        var bha = await _factory.CreateClientInCompanyAsync(me, ProcApiFactory.TestPassword, "BHA");
        var list = await NotificationApi.MineAsync(bha, Ct);
        // Chỉ của mình, chỉ công ty đang làm việc, mới nhất trước.
        Assert.Equal([second, first], list.Select(n => n.Id));
        Assert.All(list, n => Assert.Null(n.ReadAt));
        var dashboard = await bha.GetFromJsonAsync<Proc.Application.Reports.DashboardDto>("/api/dashboard", Ct);
        Assert.Equal((2, true), (dashboard!.NavCounts["NOTI"].Count, dashboard.NavCounts["NOTI"].Attention));

        Assert.Equal(HttpStatusCode.NoContent, (await bha.PostAsync($"/api/notifications/{first}/read", null, Ct)).StatusCode);
        Assert.Equal([second], (await NotificationApi.MineAsync(bha, Ct, unreadOnly: true)).Select(n => n.Id));
        // Thông báo của người khác, hoặc ở công ty khác: như không tồn tại.
        Assert.Equal(HttpStatusCode.NotFound, (await bha.PostAsync($"/api/notifications/{others}/read", null, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await bha.PostAsync($"/api/notifications/{atNmi}/read", null, Ct)).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await bha.PostAsync("/api/notifications/read-all", null, Ct)).StatusCode);
        Assert.Empty(await NotificationApi.MineAsync(bha, Ct, unreadOnly: true));
        Assert.False((await bha.GetFromJsonAsync<Proc.Application.Reports.DashboardDto>("/api/dashboard", Ct))!.NavCounts.ContainsKey("NOTI"));
        // Đọc hết ở BHA không đụng thông báo ở NMI.
        var nmi = await _factory.CreateClientInCompanyAsync(me, ProcApiFactory.TestPassword, "NMI");
        Assert.Equal([atNmi], (await NotificationApi.MineAsync(nmi, Ct, unreadOnly: true)).Select(n => n.Id));
        var otherClient = await _factory.CreateClientInCompanyAsync(other, ProcApiFactory.TestPassword, "BHA");
        Assert.Equal([others], (await NotificationApi.MineAsync(otherClient, Ct, unreadOnly: true)).Select(n => n.Id));
    }
}
