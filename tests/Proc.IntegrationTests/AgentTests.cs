using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Proc.Application.Operations;
using Proc.Domain.Operations;
using Proc.Domain.Organization;
using Proc.Infrastructure.Persistence;

namespace Proc.IntegrationTests;

/// <summary>
/// Lát QT5 – bộ thực thi (ke-hoach-qt.md QT5): khai môi trường, tạo khóa (Q6), khóa sai / cũ / môi trường ngừng dùng bị từ chối, giao lệnh (xem thử
/// không nhận lệnh, mỗi môi trường một lệnh chạy), báo kết quả, hết hạn, quá giờ, hủy, quyền.
/// </summary>
[Collection(ApiCollection.Name)]
public class AgentTests
{
    private const string AdminPath = "/api/ops/v1/agent-admin";
    private const string AgentPath = "/api/ops/v1/agent/commands";

    private readonly ProcApiFactory _factory;

    public AgentTests(ProcApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(Ct)}");
        return (await response.Content.ReadFromJsonAsync<T>(Ct))!;
    }

    private Task<HttpClient> HeadAsync() =>
        _factory.CreateClientInCompanyAsync(ProcApiFactory.AdminUserName, ProcApiFactory.AdminPassword, "TCT");

    /// <summary>Xóa khai báo bộ thực thi của lần chạy trước (bảng chỉ có dữ liệu test).</summary>
    private async Task ClearAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProcDbContext>();
        await db.AgentCommands.ExecuteDeleteAsync(Ct);
        await db.AgentEnvironments.ExecuteDeleteAsync(Ct);
    }

    private HttpClient AgentClient(string? key)
    {
        var client = _factory.CreateClient();
        if (key is not null) client.DefaultRequestHeaders.Add(AgentHeaders.Key, key);
        return client;
    }

    private async Task<(AgentEnvironmentDto Env, string Key)> SetupAsync(HttpClient head)
    {
        var env = await ReadAsync<AgentEnvironmentDto>(await head.PostAsJsonAsync($"{AdminPath}/environments",
            new SaveAgentEnvironmentRequest("test", "Máy TEST", "https://test.local:5007"), Ct));
        var key = await ReadAsync<AgentKeyCreatedDto>(await head.PostAsJsonAsync($"{AdminPath}/environments/{env.Id}/key",
            new CreateAgentKeyRequest(ProcApiFactory.AdminPassword, "tạo khóa"), Ct));
        return (env, key.Key);
    }

    private static async Task<AgentCommandDto> QueueAsync(HttpClient head, Guid envId) =>
        await ReadAsync<AgentCommandDto>(await head.PostAsJsonAsync($"{AdminPath}/commands",
            new QueueAgentCommandRequest(envId, AgentCommandKind.Ping, "Lệnh thử sau khi cài"), Ct));

    /// <summary>
    /// Khai môi trường (trùng mã → 400), chưa có khóa thì không ra lệnh được; tạo khóa sai chữ xác nhận → 400. Bộ thực thi: thiếu / sai khóa → 401;
    /// đúng khóa, chưa có lệnh → 204 và ghi lần liên lạc, phiên bản. Xem thử (peek) không nhận lệnh; lấy lệnh → Đang chạy; lệnh thứ hai chờ tới khi
    /// lệnh đầu báo kết quả. Báo kết quả lệnh lạ → 404, báo lần hai → 400; xem lệnh có nhật ký. Tạo khóa mới thì khóa cũ bị từ chối; ngừng dùng thì
    /// khóa mới cũng bị từ chối. Nhật ký vận hành có tạo khóa, lệnh kết thúc.
    /// </summary>
    [Fact]
    public async Task GiaoLenh_KhoaVaKetQua()
    {
        var startedAt = DateTimeOffset.UtcNow;
        await ClearAsync();
        var head = await HeadAsync();
        var env = await ReadAsync<AgentEnvironmentDto>(await head.PostAsJsonAsync($"{AdminPath}/environments",
            new SaveAgentEnvironmentRequest("test", "Máy TEST", null), Ct));
        Assert.Equal(("Test", false, "Error", "Chưa có khóa"), (env.Code, env.HasKey, env.Light, env.StatusLabel));
        Assert.Equal(HttpStatusCode.BadRequest, (await head.PostAsJsonAsync($"{AdminPath}/environments",
            new SaveAgentEnvironmentRequest("Test", "Trùng", null), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await head.PostAsJsonAsync($"{AdminPath}/commands",
            new QueueAgentCommandRequest(env.Id, AgentCommandKind.Ping, "Lệnh thử sau khi cài"), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await head.PostAsJsonAsync($"{AdminPath}/environments/{env.Id}/key",
            new CreateAgentKeyRequest(ProcApiFactory.AdminPassword, "TAO KHOA"), Ct)).StatusCode);

        var created = await ReadAsync<AgentKeyCreatedDto>(await head.PostAsJsonAsync($"{AdminPath}/environments/{env.Id}/key",
            new CreateAgentKeyRequest(ProcApiFactory.AdminPassword, "TẠO KHÓA"), Ct));
        Assert.StartsWith("qta_", created.Key, StringComparison.Ordinal);
        Assert.Equal(created.Key[^4..], created.Hint);

        Assert.Equal(HttpStatusCode.Unauthorized, (await AgentClient(null).GetAsync(AgentPath, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await AgentClient("qta_khongdung").GetAsync(AgentPath, Ct)).StatusCode);
        var agent = AgentClient(created.Key);
        Assert.Equal(HttpStatusCode.NoContent, (await agent.GetAsync($"{AgentPath}?agentVersion=1.0.0&appVersion=0.34.0&host=SRV-TEST", Ct)).StatusCode);
        var seen = Assert.Single(await ReadAsync<List<AgentEnvironmentDto>>(await head.GetAsync($"{AdminPath}/environments", Ct)));
        Assert.Equal(("Ok", "1.0.0", "0.34.0", "SRV-TEST", "Đang liên lạc"),
            (seen.Light, seen.AgentVersion, seen.AppVersion, seen.HostName, seen.StatusLabel));

        var first = await QueueAsync(head, env.Id);
        Assert.Equal(("Queued", "Lệnh thử (Ping)", ProcApiFactory.AdminUserName), (first.Status, first.KindLabel, first.CreatedByName));
        var peek = await ReadAsync<AgentWorkDto>(await agent.GetAsync($"{AgentPath}?peek=true", Ct));
        Assert.Equal((first.Id, false, "Ping"), (peek.Id, peek.Claimed, peek.Kind));
        var work = await ReadAsync<AgentWorkDto>(await agent.GetAsync(AgentPath, Ct));
        Assert.Equal((first.Id, true, "Lệnh thử sau khi cài"), (work.Id, work.Claimed, work.Reason));
        var second = await QueueAsync(head, env.Id);
        Assert.Equal(HttpStatusCode.NoContent, (await agent.GetAsync(AgentPath, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await head.PostAsync($"{AdminPath}/commands/{first.Id}/cancel", null, Ct)).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await agent.PostAsJsonAsync($"{AgentPath}/{Guid.NewGuid()}/result",
            new AgentResultRequest(true, "x", null), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await agent.PostAsJsonAsync($"{AgentPath}/{first.Id}/result",
            new AgentResultRequest(true, "Bộ thực thi 1.0.0 trên SRV-TEST", "dòng 1\ndòng 2"), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await agent.PostAsJsonAsync($"{AgentPath}/{first.Id}/result",
            new AgentResultRequest(false, "lần hai", null), Ct)).StatusCode);
        var done = await ReadAsync<AgentCommandDto>(await head.GetAsync($"{AdminPath}/commands/{first.Id}", Ct));
        Assert.Equal(("Succeeded", "Xong", "dòng 1\ndòng 2"), (done.Status, done.StatusLabel, done.Journal));
        Assert.Equal(second.Id, (await ReadAsync<AgentWorkDto>(await agent.GetAsync(AgentPath, Ct))).Id);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var list = await ReadAsync<AgentCommandSearchResult>(await head.GetAsync(
            $"{AdminPath}/commands?from={today.AddDays(-1):yyyy-MM-dd}&to={today.AddDays(1):yyyy-MM-dd}&environmentId={env.Id}", Ct));
        Assert.Equal(2, list.Items.Count);
        Assert.All(list.Items, i => Assert.Null(i.Journal));

        var renewed = await ReadAsync<AgentKeyCreatedDto>(await head.PostAsJsonAsync($"{AdminPath}/environments/{env.Id}/key",
            new CreateAgentKeyRequest(ProcApiFactory.AdminPassword, "TẠO KHÓA"), Ct));
        Assert.Equal(HttpStatusCode.Unauthorized, (await agent.GetAsync(AgentPath, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await AgentClient(renewed.Key).GetAsync(AgentPath, Ct)).StatusCode);
        await ReadAsync<AgentEnvironmentDto>(await head.PutAsJsonAsync($"{AdminPath}/environments/{env.Id}",
            new SaveAgentEnvironmentRequest(null, "Máy TEST", null, false), Ct));
        Assert.Equal(HttpStatusCode.Unauthorized, (await AgentClient(renewed.Key).GetAsync(AgentPath, Ct)).StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProcDbContext>();
        Assert.Equal(2, await db.OperationLogs.CountAsync(l => l.Kind == OperationKinds.AgentKeyCreated && l.Target == "Test" && l.OccurredAt >= startedAt, Ct));
        Assert.True(await db.OperationLogs.AnyAsync(l => l.Kind == OperationKinds.AgentCommandFinished
            && l.Detail!.Contains(first.Id.ToString()), Ct));
        Assert.DoesNotContain(created.Key, string.Join("|", await db.OperationLogs.Select(l => l.Detail).ToListAsync(Ct)), StringComparison.Ordinal);
    }

    /// <summary>
    /// Lệnh chờ quá 1 giờ → Hết hạn, không giao; lệnh chờ hủy được (hủy lần hai → 400); lệnh chạy quá Agent:RunningTimeoutHours không báo kết quả →
    /// Lỗi, rồi lệnh chờ kế tiếp được giao.
    /// </summary>
    [Fact]
    public async Task HetHan_Huy_QuaGio()
    {
        await ClearAsync();
        var head = await HeadAsync();
        var (env, key) = await SetupAsync(head);
        var agent = AgentClient(key);

        var old = await QueueAsync(head, env.Id);
        await SetAsync(old.Id, c => c.SetProperty(x => x.ExpiresAt, DateTimeOffset.UtcNow.AddMinutes(-1)));
        Assert.Equal(HttpStatusCode.NoContent, (await agent.GetAsync(AgentPath, Ct)).StatusCode);
        Assert.Equal("Expired", (await ReadAsync<AgentCommandDto>(await head.GetAsync($"{AdminPath}/commands/{old.Id}", Ct))).Status);

        var cancel = await QueueAsync(head, env.Id);
        var cancelled = await ReadAsync<AgentCommandDto>(await head.PostAsync($"{AdminPath}/commands/{cancel.Id}/cancel", null, Ct));
        Assert.Equal(("Cancelled", ProcApiFactory.AdminUserName), (cancelled.Status, cancelled.CancelledByName));
        Assert.Equal(HttpStatusCode.BadRequest, (await head.PostAsync($"{AdminPath}/commands/{cancel.Id}/cancel", null, Ct)).StatusCode);

        var stuck = await QueueAsync(head, env.Id);
        Assert.Equal(stuck.Id, (await ReadAsync<AgentWorkDto>(await agent.GetAsync(AgentPath, Ct))).Id);
        var next = await QueueAsync(head, env.Id);
        await SetAsync(stuck.Id, c => c.SetProperty(x => x.StartedAt, DateTimeOffset.UtcNow.AddHours(-7)));
        Assert.Equal(next.Id, (await ReadAsync<AgentWorkDto>(await agent.GetAsync(AgentPath, Ct))).Id);
        var failed = await ReadAsync<AgentCommandDto>(await head.GetAsync($"{AdminPath}/commands/{stuck.Id}", Ct));
        Assert.Equal("Failed", failed.Status);
        Assert.Contains("không báo kết quả", failed.ResultMessage, StringComparison.Ordinal);
    }

    /// <summary>Công ty thành viên → 403; quyền xem đọc được, không ra lệnh / tạo khóa được.</summary>
    [Fact]
    public async Task Quyen_ChiTongCongTy()
    {
        await ClearAsync();
        var member = await _factory.CreateClientInCompanyAsync(ProcApiFactory.AdminUserName, ProcApiFactory.AdminPassword, "BHA");
        Assert.Equal(HttpStatusCode.Forbidden, (await member.GetAsync($"{AdminPath}/environments", Ct)).StatusCode);

        var head = await HeadAsync();
        var (env, _) = await SetupAsync(head);
        var role = $"A{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        await _factory.CreateRoleAsync(role, Permissions.System.OpsAgentView);
        var name = $"av.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(name, "TCT", role);
        var viewer = await _factory.CreateClientInCompanyAsync(name, ProcApiFactory.TestPassword, "TCT");
        Assert.Single(await ReadAsync<List<AgentEnvironmentDto>>(await viewer.GetAsync($"{AdminPath}/environments", Ct)));
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsJsonAsync($"{AdminPath}/commands",
            new QueueAgentCommandRequest(env.Id, AgentCommandKind.Ping, "Lệnh thử sau khi cài"), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsJsonAsync($"{AdminPath}/environments/{env.Id}/key",
            new CreateAgentKeyRequest(ProcApiFactory.TestPassword, "TẠO KHÓA"), Ct)).StatusCode);
    }

    private async Task SetAsync(Guid id, Action<Microsoft.EntityFrameworkCore.Query.UpdateSettersBuilder<AgentCommand>> setters)
    {
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ProcDbContext>().AgentCommands.Where(c => c.Id == id).ExecuteUpdateAsync(setters, Ct);
    }
}
