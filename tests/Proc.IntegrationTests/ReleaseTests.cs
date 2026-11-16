using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Proc.Application.Documents;
using Proc.Application.Operations;
using Proc.Domain.Operations;
using Proc.Infrastructure.Operations;
using Proc.Infrastructure.Persistence;

namespace Proc.IntegrationTests;

/// <summary>
/// Lát QT8 – trung tâm phát hành REL (design.md mục 21.6): nhận gói (tải lên, GitHub Release giả), kiểm SHA256 / rollback.json / CHANGELOG, bước
/// UAT, cài PROD qua bộ thực thi giả (tải gói bằng khóa, báo kết quả kèm SHA256), khóa phiếu, quay về theo mức, kiểm thử nhanh.
/// </summary>
[Collection(ApiCollection.Name)]
public class ReleaseTests
{
    private const string RelPath = "/api/ops/v1/releases";
    private const string AgentPath = "/api/ops/v1/agent";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    private readonly ProcApiFactory _factory;

    public ReleaseTests(ProcApiFactory factory) => _factory = factory;

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

    /// <summary>Xóa phiếu phát hành, bộ thực thi của test trước.</summary>
    private async Task ClearAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProcDbContext>();
        await db.ReleaseTickets.ExecuteDeleteAsync(Ct);
        await db.AgentCommands.ExecuteDeleteAsync(Ct);
        await db.AgentEnvironments.ExecuteDeleteAsync(Ct);
    }

    /// <summary>Gói giả giống gói publish.ps1: app\Proc.Api.dll, rollback.json, CHANGELOG.md có mục của bản.</summary>
    /// <param name="extraItem">Thêm một gạch đầu dòng vào "Thêm mới" (thử mục kiểm thử dài như CHANGELOG thật).</param>
    /// <param name="changelog">CHANGELOG.md thay cho bản giả (thử CHANGELOG thật của repo).</param>
    private static byte[] Package(string version, int minLevel = 1, bool withRollback = true, bool withDll = true, string? extraItem = null,
        string? changelog = null)
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            void Add(string path, string text)
            {
                using var w = new StreamWriter(zip.CreateEntry(path).Open(), new UTF8Encoding(true));
                w.Write(text);
            }

            if (withDll) Add("app/Proc.Api.dll", "MZ giả");
            if (withRollback) Add("rollback.json", $"{{\"version\":\"{version}\",\"minLevel\":{minLevel},\"migrations\":[\"V9_9_9_Thu\"]}}");
            if (changelog is not null) Add("CHANGELOG.md", changelog);
            else Add("CHANGELOG.md", $"""
                # Nhật ký thay đổi

                ## [{version}] - 2026-11-16

                Bản thử.

                ### Thêm mới
                - Màn hình **REL** – trung tâm phát hành: nhận gói,
                  cài TEST / PROD.
                - Màn hình CFG.{(extraItem is null ? "" : "\n- " + extraItem)}

                ### Sửa lỗi
                - Không có.

                ### Thay đổi CSDL
                - Bảng ReleaseTicket.

                ### Cách quay lại bản trước
                - Mức 1: thay lại gói cũ.

                ## [0.1.0] - 2026-01-01

                ### Thêm mới
                - Bản cũ không phải mục kiểm thử.
                """);
        }

        return ms.ToArray();
    }

    private static string Sha(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    private static Task<HttpResponseMessage> UploadAsync(HttpClient client, byte[] zip, string fileName, string? sha = null)
    {
        var content = new ByteArrayContent(zip);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
        var query = $"fileName={Uri.EscapeDataString(fileName)}" + (sha is null ? "" : $"&sha256={sha}");
        return client.PostAsync($"{RelPath}/upload?{query}", content, Ct);
    }

    /// <summary>Môi trường có bộ thực thi + khóa (giả lập bộ thực thi bằng request mang khóa).</summary>
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

    /// <summary>
    /// Nhận gói tải lên: tạo phiếu Đã nhận, mục kiểm thử chỉ của bản đó (gạch đầu dòng nối dòng thụt, bỏ "Không có.", bỏ nhóm CSDL / cách quay lại),
    /// SHA256 tính trên máy chủ, gói cất trong thư mục gói. Từ chối: SHA256 kèm theo sai, không phải zip, thiếu dll / rollback.json, trùng bản.
    /// </summary>
    [Fact]
    public async Task NhanGoi_TaiLen_KiemTraGoi()
    {
        await ClearAsync();
        var head = await HeadAsync();
        var zip = Package("9.1.0");

        Assert.Contains("khác SHA256", await FailAsync(await UploadAsync(head, zip, "procurement-9.1.0.zip", new string('a', 64)), HttpStatusCode.BadRequest));
        Assert.Contains("không phải gói zip", await FailAsync(await UploadAsync(head, "abc"u8.ToArray(), "x.zip"), HttpStatusCode.BadRequest));
        Assert.Contains("Proc.Api.dll", await FailAsync(await UploadAsync(head, Package("9.1.0", withDll: false), "x.zip"), HttpStatusCode.BadRequest));
        Assert.Contains("rollback.json", await FailAsync(await UploadAsync(head, Package("9.1.0", withRollback: false), "x.zip"),
            HttpStatusCode.BadRequest));

        var ticket = await ReadAsync<ReleaseTicketDto>(await UploadAsync(head, zip, "procurement-9.1.0.zip", Sha(zip).ToUpperInvariant()));
        Assert.Equal(("9.1.0", ReleaseStatus.Received, Sha(zip), RollbackLevel.CodeOnly), (ticket.Version, ticket.Status, ticket.Sha256,
            ticket.MinRollbackLevel));
        Assert.Equal(["Màn hình **REL** – trung tâm phát hành: nhận gói, cài TEST / PROD.", "Màn hình CFG."], ticket.Items.Select(i => i.Content));
        Assert.Contains("Bảng ReleaseTicket", ticket.DatabaseChanges);
        Assert.Contains("V9_9_9_Thu", ticket.DatabaseChanges);
        Assert.Contains("Mức 1", ticket.RollbackPlan);
        Assert.DoesNotContain("Bản cũ", ticket.Changelog);
        Assert.True(File.Exists(Path.Combine(ProcApiFactory.PackageRoot, "9.1.0", "procurement-9.1.0.zip")));

        Assert.Contains("Đã có phiếu", await FailAsync(await UploadAsync(head, zip, "procurement-9.1.0.zip"), HttpStatusCode.BadRequest));
        var list = await ReadAsync<List<ReleaseListItemDto>>(await head.GetAsync(RelPath, Ct));
        Assert.Contains(list, r => r.Id == ticket.Id && r.TotalItems == 2 && r.StatusLabel == "Đã nhận");

        // Bản vá 0.36.1: phiếu chưa khóa phải đính kèm được (trước đó "ReleaseTicket" thiếu trong AttachmentOwnerTypes.All).
        using (var form = new MultipartFormDataContent
               {
                   { new StringContent("ReleaseTicket"), "ownerType" }, { new StringContent(ticket.Id.ToString()), "ownerId" },
                   { new ByteArrayContent("bien ban UAT"u8.ToArray()), "file", "bien-ban.txt" },
               })
            Assert.Equal(HttpStatusCode.OK, (await head.PostAsync("/api/attachments", form, Ct)).StatusCode);
        var files = await ReadAsync<List<AttachmentDto>>(await head.GetAsync($"/api/attachments?ownerType=ReleaseTicket&ownerId={ticket.Id}", Ct));
        Assert.Equal("bien-ban.txt", files.Single().FileName);
    }

    /// <summary>
    /// Bản vá 0.36.2: CHANGELOG thật có gạch đầu dòng dài hơn 256 ký tự – nhận gói từng lỗi 500 vì nhãn nhật ký thao tác (AuditLog.EntityLabel
    /// 256 ký tự) lấy nguyên nội dung mục kiểm thử; tiêu đề lỗi phát hiện (tối đa 300) cũng vậy. Nhãn nay được cắt, nội dung mục giữ đủ.
    /// </summary>
    [Fact]
    public async Task NhanGoi_MucKiemThuDai_LoiPhatHienDai()
    {
        await ClearAsync();
        var head = await HeadAsync();
        var longItem = "Màn hình TST – làm mới TEST từ PROD: " + string.Concat(Enumerable.Repeat("điều kiện, điều chặn, giữ lịch sử vận hành; ", 20));
        var zip = Package("9.5.0", extraItem: longItem);
        var ticket = await ReadAsync<ReleaseTicketDto>(await UploadAsync(head, zip, "procurement-9.5.0.zip"));
        Assert.Contains(ticket.Items, i => i.Content == longItem.Trim());

        var title = new string('L', 300);
        ticket = await ReadAsync<ReleaseTicketDto>(await head.PostAsJsonAsync($"{RelPath}/{ticket.Id}/defects",
            new AddReleaseDefectRequest(title, null, null), Ct));
        Assert.Equal(title, ticket.Defects.Single().Title);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProcDbContext>();
        var labels = await db.AuditLogs.Where(a => a.EntityType == "ReleaseTestItem" || a.EntityType == "ReleaseDefect")
            .Select(a => a.EntityLabel).ToListAsync(Ct);
        Assert.Contains(labels, l => l != null && l.Length == 256 && l.EndsWith('…'));
    }

    /// <summary>Nhận gói có CHANGELOG thật của repo (bản trên cùng): mọi mục kiểm thử lưu được, không lỗi 500 (bản vá 0.36.2).</summary>
    [Fact]
    public async Task NhanGoi_ChangelogThatCuaRepo()
    {
        await ClearAsync();
        var head = await HeadAsync();
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "CHANGELOG.md"))) dir = dir.Parent;
        Assert.NotNull(dir);
        var text = await File.ReadAllTextAsync(Path.Combine(dir.FullName, "CHANGELOG.md"), Ct);
        var version = System.Text.RegularExpressions.Regex.Match(text, @"^## \[(?<v>[^\]]+)\]", System.Text.RegularExpressions.RegexOptions.Multiline)
            .Groups["v"].Value;
        var ticket = await ReadAsync<ReleaseTicketDto>(await UploadAsync(head, Package(version, changelog: text), $"procurement-{version}.zip"));
        Assert.Equal(version, ticket.Version);
        Assert.NotEmpty(ticket.Items);
    }

    /// <summary>GitHub Release giả: lấy zip + .sha256 của tag; bản chưa có trên GitHub báo rõ.</summary>
    [Fact]
    public async Task NhanGoi_TuGitHub()
    {
        await ClearAsync();
        var head = await HeadAsync();
        Assert.Contains("chưa có bản phát hành v9.2.0", await FailAsync(await head.PostAsJsonAsync($"{RelPath}/github",
            new ReceiveFromGitHubRequest("9.2.0", null), Ct), HttpStatusCode.BadRequest));

        var zip = Package("9.2.0", minLevel: 2);
        var gh = FakeGitHubHandler.Shared;
        gh.Set("https://api.github.com/repos/demo/procurement/releases/tags/v9.2.0", $$"""
            {"html_url":"https://github.com/demo/procurement/releases/tag/v9.2.0","assets":[
              {"name":"procurement-9.2.0.zip","size":{{zip.Length}},"browser_download_url":"https://dl.test/procurement-9.2.0.zip"},
              {"name":"procurement-9.2.0.zip.sha256","size":64,"browser_download_url":"https://dl.test/procurement-9.2.0.zip.sha256"}]}
            """);
        gh.Set("https://dl.test/procurement-9.2.0.zip", zip);
        gh.Set("https://dl.test/procurement-9.2.0.zip.sha256", $"{Sha(zip)}  procurement-9.2.0.zip\n");

        var ticket = await ReadAsync<ReleaseTicketDto>(await head.PostAsJsonAsync($"{RelPath}/github",
            new ReceiveFromGitHubRequest("v9.2.0", "Nhận từ GitHub"), Ct));
        Assert.Equal(("GitHub Release", RollbackLevel.RemoveSchema, "Nhận từ GitHub"), (ticket.Source, ticket.MinRollbackLevel, ticket.Note));
        Assert.Equal("https://github.com/demo/procurement/releases/tag/v9.2.0", ticket.SourceUrl);
    }

    /// <summary>
    /// Đường đi đủ với một môi trường (chưa khai TEST): ghi kiểm thử, lỗi; xác nhận UAT (còn vướng phải ghi lý do); cài PROD Q6 (sai chữ → 400);
    /// bộ thực thi PROD nhận lệnh DeployRelease, tải gói (môi trường khác không tải được), báo đạt kèm SHA256 → Đã cài PROD, khóa (sửa / đính kèm bị
    /// chặn, ghi chú vẫn thêm được); quay về thấp hơn mức tối thiểu bị chặn; quay về đạt → Đã quay về. Kiểm thử nhanh trả đủ hạng mục.
    /// </summary>
    [Fact]
    public async Task DuongDiDu_MotMoiTruong_CaiProd_QuayVe()
    {
        await ClearAsync();
        var head = await HeadAsync();
        var zip = Package("9.3.0", minLevel: 1);
        var ticket = await ReadAsync<ReleaseTicketDto>(await UploadAsync(head, zip, "procurement-9.3.0.zip"));
        var overview = await ReadAsync<ReleaseOverviewDto>(await head.GetAsync($"{RelPath}/overview", Ct));
        Assert.True(overview.CanWrite && overview.SingleEnvironment);

        ticket = await ReadAsync<ReleaseTicketDto>(await head.PutAsJsonAsync($"{RelPath}/{ticket.Id}/items/{ticket.Items[0].Id}",
            new RecordReleaseTestRequest(ReleaseTestResult.Passed, null), Ct));
        ticket = await ReadAsync<ReleaseTicketDto>(await head.PostAsJsonAsync($"{RelPath}/{ticket.Id}/defects",
            new AddReleaseDefectRequest("Chữ tab lệch", "Ở REL", "REL"), Ct));
        Assert.Contains("1 lỗi chưa xử lý", ticket.OpenIssues);
        Assert.Contains("ghi lý do", await FailAsync(await head.PostAsJsonAsync($"{RelPath}/{ticket.Id}/uat-sign-off",
            new SignOffReleaseRequest(null), Ct), HttpStatusCode.BadRequest));
        ticket = await ReadAsync<ReleaseTicketDto>(await head.PostAsJsonAsync($"{RelPath}/{ticket.Id}/uat-sign-off",
            new SignOffReleaseRequest("Lỗi chữ nhỏ, sửa bản sau"), Ct));
        Assert.Equal((ReleaseStatus.UatPassed, true), (ticket.Status, ticket.UatWithoutTest));

        var deploy = $"{RelPath}/{ticket.Id}/deploy";
        Assert.Contains("AGT", await FailAsync(await head.PostAsJsonAsync(deploy,
            new DeployReleaseRequest("Prod", "Cài tối", ProcApiFactory.AdminPassword, "CÀI 9.3.0"), Ct), HttpStatusCode.BadRequest));
        var (_, prodAgent) = await AgentAsync(_factory, head, "Prod");
        var (_, devAgent) = await AgentAsync(_factory, head, "Dev");
        Assert.Contains("CÀI 9.3.0", await FailAsync(await head.PostAsJsonAsync(deploy,
            new DeployReleaseRequest("Prod", "Cài tối", ProcApiFactory.AdminPassword, "CÀI 9.2.0"), Ct), HttpStatusCode.BadRequest));
        ticket = await ReadAsync<ReleaseTicketDto>(await head.PostAsJsonAsync(deploy,
            new DeployReleaseRequest("prod", "Cài tối", ProcApiFactory.AdminPassword, "cài 9.3.0"), Ct));
        Assert.Equal(ReleaseInstallStatus.Pending, ticket.Installs.Single().Status);

        // Bộ thực thi PROD: nhận lệnh, tải gói; bộ thực thi DEV không tải được.
        var work = await ReadAsync<AgentWorkDto>(await prodAgent.GetAsync($"{AgentPath}/commands?agentVersion=1.2.0&appVersion=9.2.9&host=PROD", Ct));
        Assert.Equal("DeployRelease", work.Kind);
        using (var p = JsonDocument.Parse(work.Parameters!))
        {
            Assert.Equal(Sha(zip), p.RootElement.GetProperty("sha256").GetString());
            Assert.Equal($"/api/ops/v1/agent/releases/{ticket.Id}/package", p.RootElement.GetProperty("packagePath").GetString());
        }

        await FailAsync(await devAgent.GetAsync($"{AgentPath}/releases/{ticket.Id}/package", Ct), HttpStatusCode.Forbidden);
        var download = await prodAgent.GetAsync($"{AgentPath}/releases/{ticket.Id}/package", Ct);
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal(Sha(zip), Sha(await download.Content.ReadAsByteArrayAsync(Ct)));
        Assert.Equal(Sha(zip), download.Headers.GetValues("X-Package-Sha256").Single());

        var smoke = await ReadAsync<SmokeResultDto>(await prodAgent.GetAsync($"{AgentPath}/smoke", Ct));
        Assert.True(smoke.Ok);
        Assert.Contains(smoke.Checks, c => c.Name == "Trung tâm phát hành" && c.Ok);
        await FailAsync(await _factory.CreateClient().GetAsync($"{AgentPath}/smoke", Ct), HttpStatusCode.Unauthorized);

        Assert.Equal(HttpStatusCode.NoContent, (await prodAgent.PostAsJsonAsync($"{AgentPath}/commands/{work.Id}/result",
            new AgentResultRequest(true, "Đã cài 9.3.0, /health đạt", "nhật ký", Sha(zip)), Ct)).StatusCode);
        ticket = await ReadAsync<ReleaseTicketDto>(await head.GetAsync($"{RelPath}/{ticket.Id}", Ct));
        Assert.Equal((ReleaseStatus.ProdInstalled, true), (ticket.Status, ticket.IsLocked));
        Assert.Equal(("9.2.9", ReleaseInstallStatus.Succeeded), (ticket.Installs[0].PreviousVersion, ticket.Installs[0].Status));

        // Khóa: không sửa, không đính kèm; ghi chú vẫn thêm.
        await FailAsync(await head.PutAsJsonAsync($"{RelPath}/{ticket.Id}/info", new UpdateReleaseInfoRequest("x", null, null), Ct),
            HttpStatusCode.BadRequest);
        using (var form = new MultipartFormDataContent
               {
                   { new StringContent("ReleaseTicket"), "ownerType" }, { new StringContent(ticket.Id.ToString()), "ownerId" },
                   { new ByteArrayContent("a"u8.ToArray()), "file", "a.txt" },
               })
            Assert.Contains("đã cài PROD", await FailAsync(await head.PostAsync("/api/attachments", form, Ct), HttpStatusCode.BadRequest));
        ticket = await ReadAsync<ReleaseTicketDto>(await head.PostAsJsonAsync($"{RelPath}/{ticket.Id}/notes",
            new AppendReleaseNoteRequest("Theo dõi ERR"), Ct));
        Assert.Contains("Theo dõi ERR", ticket.Note);

        // Quay về: Q6 "QUAY VỀ", về đúng bản đang chạy trước khi cài.
        var rollback = $"{RelPath}/{ticket.Id}/rollback";
        await FailAsync(await head.PostAsJsonAsync(rollback,
            new RollbackReleaseRequest(RollbackLevel.CodeOnly, "Bản mới chậm", ProcApiFactory.AdminPassword, "QUAY"), Ct), HttpStatusCode.BadRequest);
        ticket = await ReadAsync<ReleaseTicketDto>(await head.PostAsJsonAsync(rollback,
            new RollbackReleaseRequest(RollbackLevel.CodeOnly, "Bản mới chậm", ProcApiFactory.AdminPassword, "quay về"), Ct));
        var back = await ReadAsync<AgentWorkDto>(await prodAgent.GetAsync($"{AgentPath}/commands?agentVersion=1.2.0&appVersion=9.3.0&host=PROD", Ct));
        Assert.Equal("RollbackRelease", back.Kind);
        Assert.Contains("\"targetVersion\":\"9.2.9\"", back.Parameters);
        await prodAgent.PostAsJsonAsync($"{AgentPath}/commands/{back.Id}/result", new AgentResultRequest(true, "Đã về 9.2.9", null), Ct);
        ticket = await ReadAsync<ReleaseTicketDto>(await head.GetAsync($"{RelPath}/{ticket.Id}", Ct));
        Assert.Equal((ReleaseStatus.RolledBack, "Bản mới chậm"), (ticket.Status, ticket.EndReason));

        using var scope = _factory.Services.CreateScope();
        var kinds = await scope.ServiceProvider.GetRequiredService<ProcDbContext>().OperationLogs
            .Where(l => l.Target != null && l.Target.StartsWith("9.3.0")).Select(l => l.Kind).ToListAsync(Ct);
        Assert.Contains(OperationKinds.ReleaseReceived, kinds);
        Assert.Contains(OperationKinds.ReleaseInstallQueued, kinds);
        Assert.Contains(OperationKinds.ReleaseInstallFinished, kinds);
    }

    /// <summary>
    /// Có máy TEST: phải cài TEST trước khi xác nhận UAT; cài TEST lỗi (bộ thực thi báo lỗi) giữ Đã nhận; lệnh hết hạn được ghi lỗi khi mở phiếu;
    /// cài TEST đạt → Đang UAT. Báo đạt nhưng SHA256 khác → lỗi. Không có quyền DeployProd thì không cài PROD.
    /// </summary>
    [Fact]
    public async Task CoMayTest_CaiTestTruoc()
    {
        await ClearAsync();
        var head = await HeadAsync();
        var zip = Package("9.4.0");
        var ticket = await ReadAsync<ReleaseTicketDto>(await UploadAsync(head, zip, "procurement-9.4.0.zip"));
        var (_, testAgent) = await AgentAsync(_factory, head, "Test");
        Assert.Contains("Cài TEST trước", await FailAsync(await head.PostAsJsonAsync($"{RelPath}/{ticket.Id}/uat-sign-off",
            new SignOffReleaseRequest("Đã thử trên DEV"), Ct), HttpStatusCode.BadRequest));

        var deploy = new DeployReleaseRequest("Test", "Cài TEST", ProcApiFactory.AdminPassword, "CÀI 9.4.0");
        await ReadAsync<ReleaseTicketDto>(await head.PostAsJsonAsync($"{RelPath}/{ticket.Id}/deploy", deploy, Ct));
        var w1 = await ReadAsync<AgentWorkDto>(await testAgent.GetAsync($"{AgentPath}/commands?agentVersion=1.2.0&appVersion=9.3.0&host=TEST", Ct));
        await testAgent.PostAsJsonAsync($"{AgentPath}/commands/{w1.Id}/result", new AgentResultRequest(true, "Xong", null, new string('b', 64)), Ct);
        ticket = await ReadAsync<ReleaseTicketDto>(await head.GetAsync($"{RelPath}/{ticket.Id}", Ct));
        Assert.Equal(ReleaseStatus.Received, ticket.Status);
        Assert.Contains("SHA256", ticket.Installs[0].Message);

        // Lệnh hết hạn (máy TEST tắt): mở phiếu thì lần cài ghi lỗi, giao lệnh mới được.
        await ReadAsync<ReleaseTicketDto>(await head.PostAsJsonAsync($"{RelPath}/{ticket.Id}/deploy", deploy, Ct));
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ProcDbContext>();
            await db.AgentCommands.Where(c => c.Status == AgentCommandStatus.Queued)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.Status, AgentCommandStatus.Expired), Ct);
        }

        ticket = await ReadAsync<ReleaseTicketDto>(await head.GetAsync($"{RelPath}/{ticket.Id}", Ct));
        Assert.All(ticket.Installs, i => Assert.Equal(ReleaseInstallStatus.Failed, i.Status));

        await ReadAsync<ReleaseTicketDto>(await head.PostAsJsonAsync($"{RelPath}/{ticket.Id}/deploy", deploy, Ct));
        var w3 = await ReadAsync<AgentWorkDto>(await testAgent.GetAsync($"{AgentPath}/commands?agentVersion=1.2.0&appVersion=9.3.0&host=TEST", Ct));
        await testAgent.PostAsJsonAsync($"{AgentPath}/commands/{w3.Id}/result", new AgentResultRequest(true, "Xong", null, Sha(zip)), Ct);
        ticket = await ReadAsync<ReleaseTicketDto>(await head.GetAsync($"{RelPath}/{ticket.Id}", Ct));
        Assert.Equal(ReleaseStatus.UatInProgress, ticket.Status);

        // Quyền: người chỉ có quyền cài TEST không cài PROD được.
        var role = $"R{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        await _factory.CreateRoleAsync(role, Proc.Domain.Organization.Permissions.System.OpsReleaseDeployTest);
        var name = $"rl.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(name, "TCT", role);
        var tester = await _factory.CreateClientInCompanyAsync(name, ProcApiFactory.TestPassword, "TCT");
        await FailAsync(await tester.PostAsJsonAsync($"{RelPath}/{ticket.Id}/deploy",
            new DeployReleaseRequest("Prod", "x", ProcApiFactory.TestPassword, "CÀI 9.4.0"), Ct), HttpStatusCode.Forbidden);
    }
}
