using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Proc.Application.Operations;
using Proc.Domain.Operations;
using Proc.Infrastructure.Operations;
using Proc.Infrastructure.Persistence;

namespace Proc.IntegrationTests;

/// <summary>
/// Lát QT9 – làm mới TEST từ PROD (màn hình TST) và báo lỗi từ TEST (design.md 21.5, 21.18): điều kiện chặn (chưa khai TEST, khác phiên bản, chưa có
/// pg_dump, đang có lệnh), giao lệnh Q6, bộ thực thi TEST tải bản dump / file đính kèm (chỉ khi đang chạy lệnh, chặn ".."), câu đặt lại phần riêng TEST
/// chạy được trên CSDL thật; khóa dịch vụ, hộp nhận báo lỗi trên PROD, máy TEST chuyển báo lỗi về PROD.
/// </summary>
[Collection(ApiCollection.Name)]
public class TestRefreshTests
{
    private const string TstPath = "/api/ops/v1/test-refresh";
    private const string AgentPath = "/api/ops/v1/agent";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    /// <summary>PNG 1×1 hợp lệ (đủ chữ ký đầu file để qua kiểm tra nội dung).</summary>
    private static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    private readonly ProcApiFactory _factory;

    public TestRefreshTests(ProcApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync(Ct);
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {body}");
        return JsonSerializer.Deserialize<T>(body, Json)!;
    }

    private static async Task<string> FailAsync(HttpResponseMessage response, HttpStatusCode expected)
    {
        var body = await response.Content.ReadAsStringAsync(Ct);
        Assert.True(response.StatusCode == expected, $"{(int)response.StatusCode}: {body}");
        return body;
    }

    private Task<HttpClient> HeadAsync() =>
        _factory.CreateClientInCompanyAsync(ProcApiFactory.AdminUserName, ProcApiFactory.AdminPassword, "TCT");

    private async Task ClearAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProcDbContext>();
        await db.ReleaseTickets.ExecuteDeleteAsync(Ct);
        await db.AgentCommands.ExecuteDeleteAsync(Ct);
        await db.AgentEnvironments.ExecuteDeleteAsync(Ct);
    }

    private static async Task<(Guid Id, HttpClient Agent)> AgentAsync(ProcApiFactory factory, HttpClient head, string code)
    {
        var env = await ReadAsync<AgentEnvironmentDto>(await head.PostAsJsonAsync("/api/ops/v1/agent-admin/environments",
            new SaveAgentEnvironmentRequest(code, $"Máy {code}", null), Ct));
        var key = (await ReadAsync<AgentKeyCreatedDto>(await head.PostAsJsonAsync($"/api/ops/v1/agent-admin/environments/{env.Id}/key",
            new CreateAgentKeyRequest(ProcApiFactory.AdminPassword, "TẠO KHÓA"), Ct))).Key;
        var agent = factory.CreateClient();
        agent.DefaultRequestHeaders.Add(AgentHeaders.Key, key);
        return (env.Id, agent);
    }

    /// <summary>Bản pg_dump giả của "PROD": file trong thư mục sao lưu + lần sao lưu xong (mới nhất).</summary>
    private async Task<(Guid RunId, byte[] Content)> DumpAsync()
    {
        var content = RandomNumberGenerator.GetBytes(4096);
        var relative = Path.Combine("dump", $"qt9-{Guid.NewGuid():N}.dump");
        var path = Path.Combine(ProcApiFactory.BackupRoot, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, content, Ct);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProcDbContext>();
        var now = DateTimeOffset.UtcNow;
        var run = BackupRun.Queue(BackupKind.Dump, BackupTrigger.Manual, null, "test", now);
        run.Start(now);
        run.Complete(relative, content.Length, 1, Convert.ToHexStringLower(SHA256.HashData(content)), null, null, now);
        db.BackupRuns.Add(run);
        await db.SaveChangesAsync(Ct);
        return (run.Id, content);
    }

    private async Task DeleteRunAsync(Guid id)
    {
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ProcDbContext>().BackupRuns.Where(r => r.Id == id).ExecuteDeleteAsync(Ct);
    }

    /// <summary>
    /// TST: chưa khai TEST → chặn; TEST khác phiên bản → chặn; đủ điều kiện → giao lệnh (sai chữ Q6 bị chặn), lệnh mang tham số bản dump, bảng giữ, câu
    /// đặt lại; đang có lệnh → chặn lần hai. Bộ thực thi TEST nhận lệnh rồi mới tải được bản dump (đúng SHA256) và file đính kèm; bộ thực thi khác bị chặn;
    /// đường dẫn ".." bị chặn.
    /// </summary>
    [Fact]
    public async Task LamMoiTest_DieuKien_GiaoLenh_TaiDumpVaDinhKem()
    {
        await ClearAsync();
        var head = await HeadAsync();
        var overview = await ReadAsync<TestRefreshOverviewDto>(await head.GetAsync($"{TstPath}/overview", Ct));
        Assert.Contains(overview.Blockers, b => b.Contains("Chưa khai môi trường TEST"));
        Assert.Contains("ErrorOccurrence", overview.KeepTables);

        var (_, testAgent) = await AgentAsync(_factory, head, "Test");
        var (_, devAgent) = await AgentAsync(_factory, head, "Dev");
        await testAgent.GetAsync($"{AgentPath}/commands?agentVersion=1.3.0&appVersion=0.0.1&host=TEST&peek=true", Ct);
        overview = await ReadAsync<TestRefreshOverviewDto>(await head.GetAsync($"{TstPath}/overview", Ct));
        Assert.Contains(overview.Blockers, b => b.Contains("TEST đang chạy bản 0.0.1"));
        Assert.Contains(overview.Warnings, w => w.Contains("khóa dịch vụ"));

        var (runId, dump) = await DumpAsync();
        try
        {
            await testAgent.GetAsync($"{AgentPath}/commands?agentVersion=1.3.0&appVersion={overview.Version}&host=TEST&peek=true", Ct);
            overview = await ReadAsync<TestRefreshOverviewDto>(await head.GetAsync($"{TstPath}/overview", Ct));
            Assert.Empty(overview.Blockers);
            Assert.Equal((runId, false), (overview.Dump!.RunId, overview.Dump.Stale));

            Assert.Contains("LÀM MỚI TEST", await FailAsync(await head.PostAsJsonAsync($"{TstPath}/runs",
                new RunTestRefreshRequest("UAT bản 0.37.0", ProcApiFactory.AdminPassword, "LAM MOI"), Ct), HttpStatusCode.BadRequest));
            var cmd = await ReadAsync<AgentCommandDto>(await head.PostAsJsonAsync($"{TstPath}/runs",
                new RunTestRefreshRequest("UAT bản 0.37.0", ProcApiFactory.AdminPassword, "làm mới test"), Ct));
            Assert.Equal(("Test", "RefreshTest"), (cmd.EnvironmentCode, cmd.Kind));
            overview = await ReadAsync<TestRefreshOverviewDto>(await head.GetAsync($"{TstPath}/overview", Ct));
            Assert.Equal(cmd.Id, overview.OpenCommandId);
            Assert.Contains(overview.Blockers, b => b.Contains("đang có lệnh"));
            await FailAsync(await head.PostAsJsonAsync($"{TstPath}/runs",
                new RunTestRefreshRequest("Lần hai", ProcApiFactory.AdminPassword, "LÀM MỚI TEST"), Ct), HttpStatusCode.BadRequest);

            // Chưa nhận lệnh thì chưa tải được.
            await FailAsync(await testAgent.GetAsync($"{AgentPath}/test-refresh/dump", Ct), HttpStatusCode.Forbidden);
            var work = await ReadAsync<AgentWorkDto>(await testAgent.GetAsync(
                $"{AgentPath}/commands?agentVersion=1.3.0&appVersion={overview.Version}&host=TEST", Ct));
            Assert.Equal("RefreshTest", work.Kind);
            using (var p = JsonDocument.Parse(work.Parameters!))
            {
                Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(dump)), p.RootElement.GetProperty("sha256").GetString());
                Assert.Contains("AccessPolicy", p.RootElement.GetProperty("resetSql").GetString());
                Assert.Equal(TestRefreshCarryOver.Tables.Count, p.RootElement.GetProperty("keepTables").GetArrayLength());
            }

            var download = await testAgent.GetAsync($"{AgentPath}/test-refresh/dump", Ct);
            Assert.Equal(HttpStatusCode.OK, download.StatusCode);
            Assert.Equal(dump, await download.Content.ReadAsByteArrayAsync(Ct));
            Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(dump)), download.Headers.GetValues("X-Package-Sha256").Single());
            await FailAsync(await devAgent.GetAsync($"{AgentPath}/test-refresh/dump", Ct), HttpStatusCode.Forbidden);

            // File đính kèm: danh sách + tải từng file; chặn thoát thư mục.
            var root = Path.Combine(Path.GetTempPath(), "proc-test-attachments");
            var marker = Path.Combine(root, "qt9", $"{Guid.NewGuid():N}.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(marker)!);
            await File.WriteAllTextAsync(marker, "đính kèm thử", Ct);
            var relative = Path.GetRelativePath(root, marker).Replace(Path.DirectorySeparatorChar, '/');
            var manifest = await ReadAsync<List<AttachmentManifestFile>>(await testAgent.GetAsync($"{AgentPath}/test-refresh/attachments", Ct));
            Assert.Contains(manifest, f => f.Path == relative && f.Size == new FileInfo(marker).Length);
            var file = await testAgent.GetAsync($"{AgentPath}/test-refresh/attachments/file?path={Uri.EscapeDataString(relative)}", Ct);
            Assert.Equal("đính kèm thử", await file.Content.ReadAsStringAsync(Ct));
            await FailAsync(await testAgent.GetAsync($"{AgentPath}/test-refresh/attachments/file?path=..%2Fsecret.txt", Ct), HttpStatusCode.BadRequest);
            await FailAsync(await devAgent.GetAsync($"{AgentPath}/test-refresh/attachments", Ct), HttpStatusCode.Forbidden);
            File.Delete(marker);

            await testAgent.PostAsJsonAsync($"{AgentPath}/commands/{work.Id}/result", new AgentResultRequest(true, "Đã làm mới TEST", "nhật ký"), Ct);
            var history = await ReadAsync<List<AgentCommandDto>>(await head.GetAsync(
                $"{TstPath}/runs?from={DateTime.Today.AddDays(-1):yyyy-MM-dd}&to={DateTime.Today.AddDays(1):yyyy-MM-dd}", Ct));
            Assert.Contains(history, h => h.Id == cmd.Id && h.Status == "Succeeded");
        }
        finally
        {
            await DeleteRunAsync(runId);
        }
    }

    /// <summary>Câu đặt lại phần riêng TEST chạy được trên CSDL thật (tên bảng, cột, số trạng thái) – chạy trong giao dịch rồi bỏ; bảng giữ đều có thật.</summary>
    [Fact]
    public async Task CauDatLaiTest_ChayDuocTrenCsdl()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProcDbContext>();
        await using var conn = new NpgsqlConnection(db.Database.GetConnectionString());
        await conn.OpenAsync(Ct);
        await using (var tx = await conn.BeginTransactionAsync(Ct))
        {
            await using var cmd = new NpgsqlCommand(TestRefreshSql.Reset, conn, tx);
            await cmd.ExecuteNonQueryAsync(Ct);
            await tx.RollbackAsync(Ct);
        }

        foreach (var table in TestRefreshCarryOver.Tables)
        {
            await using var check = new NpgsqlCommand("SELECT to_regclass(@t) IS NOT NULL", conn);
            check.Parameters.AddWithValue("t", $"public.\"{table}\"");
            Assert.True(await check.ExecuteScalarAsync(Ct) is true, table);
        }
    }

    /// <summary>
    /// Báo lỗi từ TEST: khóa dịch vụ (tạo ở AGT, Q6) – thiếu / sai / khóa bộ thực thi → 401; môi trường không phải TEST → 403; chưa có phiếu bản đó
    /// → 400 (không ghi); có phiếu → dòng Lỗi phát hiện (màn hình, người báo "(TEST)"), ảnh vào tab Đính kèm. Máy TEST (App:Environment=Test, khai
    /// Operations:Upstream) chuyển báo lỗi của người dùng về PROD qua HttpClient – người báo lấy từ phiên đăng nhập, phiên bản từ máy TEST.
    /// </summary>
    [Fact]
    public async Task BaoLoiTuTest_GhiVaoPhieuPhatHanh()
    {
        await ClearAsync();
        var head = await HeadAsync();
        var (testEnvId, testAgent) = await AgentAsync(_factory, head, "Test");
        var (prodEnvId, _) = await AgentAsync(_factory, head, "Prod");
        var serviceKey = (await ReadAsync<AgentKeyCreatedDto>(await head.PostAsJsonAsync(
            $"/api/ops/v1/agent-admin/environments/{testEnvId}/service-key", new CreateAgentKeyRequest(ProcApiFactory.AdminPassword, "TẠO KHÓA"), Ct))).Key;
        Assert.StartsWith(AgentKeys.ServicePrefix, serviceKey);
        var prodKey = (await ReadAsync<AgentKeyCreatedDto>(await head.PostAsJsonAsync(
            $"/api/ops/v1/agent-admin/environments/{prodEnvId}/service-key", new CreateAgentKeyRequest(ProcApiFactory.AdminPassword, "TẠO KHÓA"), Ct))).Key;
        var envs = await ReadAsync<List<AgentEnvironmentDto>>(await head.GetAsync("/api/ops/v1/agent-admin/environments", Ct));
        Assert.Contains(envs, e => e.Id == testEnvId && e.HasServiceKey && e.ServiceKeyHint == serviceKey[^4..]);

        var version = (await ReadAsync<TestRefreshOverviewDto>(await head.GetAsync($"{TstPath}/overview", Ct))).Version;
        var defect = new ForwardDefectRequest(version, "Nút Lưu không chạy", "Bấm Lưu trên PO", "/mua-hang/po", "ut – Người thử",
            "loi.png", Convert.ToBase64String(Png));
        HttpClient Service(string? key)
        {
            var c = _factory.CreateClient();
            if (key is not null) c.DefaultRequestHeaders.Add(ServiceHeaders.Key, key);
            return c;
        }

        const string url = "/api/ops/v1/service/defects";
        await FailAsync(await Service(null).PostAsJsonAsync(url, defect, Ct), HttpStatusCode.Unauthorized);
        await FailAsync(await Service(serviceKey + "x").PostAsJsonAsync(url, defect, Ct), HttpStatusCode.Unauthorized);
        await FailAsync(await testAgent.PostAsJsonAsync(url, defect, Ct), HttpStatusCode.Unauthorized);
        await FailAsync(await Service(prodKey).PostAsJsonAsync(url, defect, Ct), HttpStatusCode.Forbidden);
        Assert.Contains("chưa có phiếu", await FailAsync(await Service(serviceKey).PostAsJsonAsync(url, defect, Ct), HttpStatusCode.BadRequest));

        // Phiếu phát hành của đúng phiên bản TEST đang chạy.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ProcDbContext>();
            db.ReleaseTickets.Add(ReleaseTicket.Receive(
                new ReleasePackageInfo(version, $"procurement-{version}.zip", 10, new string('c', 64), ReleaseSource.Upload, null, null, null,
                    RollbackLevel.CodeOnly, null), [], null, "admin", DateTimeOffset.UtcNow));
            await db.SaveChangesAsync(Ct);
        }

        var result = await ReadAsync<ForwardDefectResult>(await Service(serviceKey).PostAsJsonAsync(url, defect, Ct));
        Assert.Equal((version, 1), (result.Version, result.OpenDefects));
        var ticketId = (await ReadAsync<List<ReleaseListItemDto>>(await head.GetAsync("/api/ops/v1/releases", Ct))).Single().Id;
        var ticket = await ReadAsync<ReleaseTicketDto>(await head.GetAsync($"/api/ops/v1/releases/{ticketId}", Ct));
        var d = ticket.Defects.Single();
        Assert.Equal(("Nút Lưu không chạy", "/mua-hang/po", "ut – Người thử (TEST)"), (d.Title, d.Screen, d.ReporterName));
        Assert.Contains("loi.png", d.Description);
        var files = await ReadAsync<List<Proc.Application.Documents.AttachmentDto>>(await head.GetAsync(
            $"/api/attachments?ownerType=ReleaseTicket&ownerId={ticketId}", Ct));
        Assert.Contains(files, f => f.FileName == "loi.png");
        // Ảnh sai (không phải PNG) → từ chối cả báo lỗi, không sinh lỗi trùng.
        await FailAsync(await Service(serviceKey).PostAsJsonAsync(url, defect with { ImageBase64 = Convert.ToBase64String("abc"u8.ToArray()) }, Ct),
            HttpStatusCode.BadRequest);

        // Máy TEST: nút "Báo lỗi / góp ý" chuyển về "PROD" (chính máy test chính, qua bộ xử lý của TestServer).
        using var testMachine = _factory.WithWebHostBuilder(b =>
        {
            b.UseSetting("App:Environment", "Test");
            b.UseSetting("Operations:Upstream:BaseUrl", "http://prod.local");
            b.UseSetting("Operations:Upstream:ServiceKey", serviceKey);
            b.ConfigureServices(s => s.AddHttpClient(Program.UpstreamClient).ConfigurePrimaryHttpMessageHandler(() => _factory.Server.CreateHandler()));
        });
        var anonymous = testMachine.CreateClient();
        var env = await ReadAsync<EnvironmentStatusDto>(await anonymous.GetAsync("/api/ops/v1/environment", Ct));
        Assert.True(env.FeedbackEnabled);
        var user = testMachine.CreateClient();
        user.DefaultRequestHeaders.Authorization = head.DefaultRequestHeaders.Authorization;
        user.DefaultRequestHeaders.Add("X-Company-Id", head.DefaultRequestHeaders.GetValues("X-Company-Id"));
        var sent = await ReadAsync<ForwardDefectResult>(await user.PostAsJsonAsync("/api/ops/v1/feedback",
            new SendFeedbackRequest("Ô ngày gõ sai", "Gõ 05122026 không nhận", "/mua-hang/pr", null, null), Ct));
        Assert.Equal(2, sent.OpenDefects);
        ticket = await ReadAsync<ReleaseTicketDto>(await head.GetAsync($"/api/ops/v1/releases/{ticketId}", Ct));
        Assert.Contains(ticket.Defects, x => x.Title == "Ô ngày gõ sai" && x.ReporterName!.StartsWith("admin") && x.ReporterName.EndsWith("(TEST)"));

        // Máy không phải TEST thì không gửi.
        Assert.Contains("chỉ dùng trên máy TEST", await FailAsync(await head.PostAsJsonAsync("/api/ops/v1/feedback",
            new SendFeedbackRequest("x", null, null, null, null), Ct), HttpStatusCode.BadRequest));
    }
}
