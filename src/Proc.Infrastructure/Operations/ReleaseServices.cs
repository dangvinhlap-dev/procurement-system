using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Proc.Application.Common;
using Proc.Application.Operations;
using Proc.Domain.Common;
using Proc.Domain.Operations;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.Operations;

/// <summary>HttpClient lấy gói từ GitHub Release – bọc lại để lớp Infrastructure không phụ thuộc gói IHttpClientFactory (Api tạo và đăng ký).</summary>
/// <param name="Client">HttpClient đã đặt User-Agent, Accept.</param>
public sealed record ReleaseGitHubHttp(HttpClient Client);

/// <summary>
/// Trung tâm phát hành (lát QT8 – design.md mục 21.6, 21.6a, 21.8): nhận gói (tải lên / GitHub Release), mục kiểm thử, lỗi, xác nhận UAT, cài TEST /
/// PROD và quay về bản cũ qua bộ thực thi, đóng, hủy. Phiếu không thuộc công ty (bảng vận hành); API đã chốt tổng công ty + dải IP. Ghi chỉ trên
/// PROD / DEV – máy TEST chỉ xem (T4: làm mới TEST từ PROD sẽ chép đè dữ liệu QT của TEST).
/// </summary>
public sealed class ReleaseService : IReleaseService, IReleaseAgentHooks
{
    /// <summary>Tên HttpClient lấy gói từ GitHub.</summary>
    public const string GitHubClient = "release-github";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private readonly ProcDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IOperationLog _operationLog;
    private readonly IDangerousActionGuard _guard;
    private readonly ReleaseGitHubHttp _http;
    private readonly ReleaseOptions _options;
    private readonly AppEnvironmentOptions _environment;
    private readonly AppVersion _version;

    /// <summary>Tạo dịch vụ.</summary>
    /// <param name="db">CSDL.</param>
    /// <param name="currentUser">Người đang thao tác.</param>
    /// <param name="clock">Đồng hồ.</param>
    /// <param name="operationLog">Nhật ký vận hành.</param>
    /// <param name="guard">Xác nhận việc nguy hiểm Q6.</param>
    /// <param name="http">HttpClient lấy gói GitHub (Api đăng ký qua IHttpClientFactory, tên <see cref="GitHubClient"/>).</param>
    /// <param name="options">Cấu hình trung tâm phát hành.</param>
    /// <param name="environment">Môi trường máy này.</param>
    /// <param name="version">Phiên bản đang chạy.</param>
    public ReleaseService(
        ProcDbContext db, ICurrentUser currentUser, IClock clock, IOperationLog operationLog, IDangerousActionGuard guard, ReleaseGitHubHttp http,
        IOptions<ReleaseOptions> options, IOptions<AppEnvironmentOptions> environment, AppVersion version)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _operationLog = operationLog;
        _guard = guard;
        _http = http;
        _options = options.Value;
        _environment = environment.Value;
        _version = version;
    }

    private long MaxBytes => (long)_options.MaxPackageSizeMb * 1024 * 1024;

    /// <inheritdoc />
    public async Task<ReleaseOverviewDto> GetOverviewAsync(CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var envs = await _db.AgentEnvironments.AsNoTracking().Where(e => e.IsActive).OrderBy(e => e.Code).ToListAsync(cancellationToken);
        var online = TimeSpan.FromMinutes(3);
        return new ReleaseOverviewDto(
            _environment.Name, _version.Version, !_environment.IsTest, ReadOnlyReason(), !HasTestEnvironment(envs),
            !string.IsNullOrWhiteSpace(_options.GitHubRepository), _options.MaxPackageSizeMb,
            envs.Select(e => new ReleaseEnvironmentDto(e.Code, e.Name, e.LastSeenAt is { } seen && now - seen <= online, e.AppVersion, e.LastSeenAt))
                .ToList());
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ReleaseListItemDto>> SearchAsync(CancellationToken cancellationToken)
    {
        var tickets = await _db.ReleaseTickets.AsNoTracking().Include(t => t.Items).Include(t => t.Defects)
            .OrderByDescending(t => t.ReceivedAt).ToListAsync(cancellationToken);
        return tickets.Select(t =>
        {
            var (label, tone) = ReleaseLabels.Status(t.Status);
            return new ReleaseListItemDto(t.Id, t.Version, t.Status, label, tone, ReleaseLabels.Source(t.Source), t.PackageSize, t.ReceivedAt,
                t.ReceivedByName, t.Items.Count(i => i.Result != ReleaseTestResult.NotTested), t.Items.Count,
                t.Defects.Count(d => d.Status == ReleaseDefectStatus.Open), t.ProdInstalledAt);
        }).ToList();
    }

    /// <inheritdoc />
    public async Task<ReleaseTicketDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var ticket = await LoadAsync(id, cancellationToken);
        // Lệnh hết hạn / bị hủy / quá giờ mà bộ thực thi không báo: cập nhật lần cài khi mở phiếu (như RST – lát QT6).
        await SyncAndSaveAsync(ticket, cancellationToken);
        return ToDto(ticket);
    }

    /// <inheritdoc />
    public async Task<ReleaseTicketDto> ReceiveUploadAsync(
        Stream content, string? fileName, string? expectedSha256, string? note, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        EnsureWritable();
        fileName = Path.GetFileName(fileName ?? string.Empty);
        if (!fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) throw new BusinessRuleException("Chọn file gói .zip (procurement-x.y.z.zip).");
        var ticket = await ReceiveAsync(content, fileName, expectedSha256, ReleaseSource.Upload, null, note, cancellationToken);
        return ToDto(ticket);
    }

    /// <inheritdoc />
    public async Task<ReleaseTicketDto> ReceiveFromGitHubAsync(ReceiveFromGitHubRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureWritable();
        if (string.IsNullOrWhiteSpace(_options.GitHubRepository))
            throw new BusinessRuleException("Chưa khai Release:GitHubRepository – nhận gói bằng cách tải lên.");
        var version = request.Version?.Trim().TrimStart('v', 'V') ?? string.Empty;
        if (version.Length == 0) throw new BusinessRuleException("Nhập phiên bản (ví dụ 0.36.0).");
        var client = _http.Client;
        var baseUrl = _options.GitHubApiBaseUrl.TrimEnd('/');
        var pageUrl = $"https://github.com/{_options.GitHubRepository}/releases/tag/v{version}";

        using var releaseResponse = await client.GetAsync($"{baseUrl}/repos/{_options.GitHubRepository}/releases/tags/v{Uri.EscapeDataString(version)}",
            cancellationToken);
        if (releaseResponse.StatusCode == System.Net.HttpStatusCode.NotFound)
            throw new BusinessRuleException($"GitHub chưa có bản phát hành v{version} ({_options.GitHubRepository}).");
        if (!releaseResponse.IsSuccessStatusCode)
            throw new BusinessRuleException($"Không đọc được bản phát hành v{version} trên GitHub (HTTP {(int)releaseResponse.StatusCode}).");
        var release = await releaseResponse.Content.ReadFromJsonAsync<GitHubRelease>(Json, cancellationToken)
            ?? throw new BusinessRuleException("GitHub trả nội dung rỗng.");
        var zipName = $"procurement-{version}.zip";
        var zip = release.Assets.FirstOrDefault(a => string.Equals(a.Name, zipName, StringComparison.OrdinalIgnoreCase))
            ?? throw new BusinessRuleException($"Bản phát hành v{version} không có file {zipName}.");
        var shaAsset = release.Assets.FirstOrDefault(a => string.Equals(a.Name, zipName + ".sha256", StringComparison.OrdinalIgnoreCase))
            ?? throw new BusinessRuleException($"Bản phát hành v{version} không có file {zipName}.sha256 – không kiểm được gói.");
        if (zip.Size > MaxBytes) throw new BusinessRuleException($"Gói lớn quá {_options.MaxPackageSizeMb} MB.");

        var shaText = await client.GetStringAsync(shaAsset.BrowserDownloadUrl, cancellationToken);
        var expected = shaText.Trim().Split([' ', '\t', '*'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        using var zipResponse = await client.GetAsync(zip.BrowserDownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        zipResponse.EnsureSuccessStatusCode();
        await using var stream = await zipResponse.Content.ReadAsStreamAsync(cancellationToken);
        var ticket = await ReceiveAsync(stream, zipName, expected, ReleaseSource.GitHubRelease, release.HtmlUrl ?? pageUrl, request.Note,
            cancellationToken);
        return ToDto(ticket);
    }

    /// <inheritdoc />
    public Task<ReleaseTicketDto> UpdateInfoAsync(Guid id, UpdateReleaseInfoRequest request, CancellationToken cancellationToken) =>
        ChangeAsync(id, t => t.UpdateInfo(request.Risks, request.RollbackPlan, request.Note), null, cancellationToken);

    /// <inheritdoc />
    public Task<ReleaseTicketDto> AppendNoteAsync(Guid id, AppendReleaseNoteRequest request, CancellationToken cancellationToken) =>
        ChangeAsync(id, t => t.AppendNote(request.Text, _currentUser.UserName, _clock.UtcNow), null, cancellationToken);

    /// <inheritdoc />
    public Task<ReleaseTicketDto> RecordTestAsync(Guid id, Guid itemId, RecordReleaseTestRequest request, CancellationToken cancellationToken) =>
        ChangeAsync(id, t => t.RecordTest(itemId, request.Result, request.Comment, _currentUser.UserName, _clock.UtcNow), null, cancellationToken);

    /// <inheritdoc />
    public Task<ReleaseTicketDto> AddDefectAsync(Guid id, AddReleaseDefectRequest request, CancellationToken cancellationToken) =>
        ChangeAsync(id, t => t.AddDefect(request.Title, request.Description, request.Screen, _currentUser.UserName, _clock.UtcNow), null,
            cancellationToken);

    /// <inheritdoc />
    public Task<ReleaseTicketDto> ResolveDefectAsync(Guid id, Guid defectId, ResolveReleaseDefectRequest request, CancellationToken cancellationToken) =>
        ChangeAsync(id, t => t.ResolveDefect(defectId, request.Status, request.Resolution, _currentUser.UserName, _clock.UtcNow), null,
            cancellationToken);

    /// <inheritdoc />
    public async Task<ReleaseTicketDto> SignOffAsync(Guid id, SignOffReleaseRequest request, CancellationToken cancellationToken)
    {
        var single = !HasTestEnvironment(await _db.AgentEnvironments.AsNoTracking().Where(e => e.IsActive).ToListAsync(cancellationToken));
        return await ChangeAsync(id, t => t.SignOffUat(request.Comment, single, _currentUser.UserName, _clock.UtcNow),
            t => ("Xác nhận UAT đạt", t.UatComment), cancellationToken);
    }

    /// <inheritdoc />
    public Task<ReleaseTicketDto> CloseAsync(Guid id, CancellationToken cancellationToken) =>
        ChangeAsync(id, t => t.Close(_currentUser.UserName, _clock.UtcNow), _ => ("Đóng phiếu", null), cancellationToken);

    /// <inheritdoc />
    public Task<ReleaseTicketDto> CancelAsync(Guid id, CancelReleaseRequest request, CancellationToken cancellationToken) =>
        ChangeAsync(id, t => t.Cancel(request.Reason, _currentUser.UserName, _clock.UtcNow), t => ("Hủy phiếu", t.EndReason), cancellationToken);

    /// <inheritdoc />
    public async Task<ReleaseTicketDto> DeployAsync(Guid id, DeployReleaseRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureWritable();
        var ticket = await LoadAsync(id, cancellationToken);
        await SyncAndSaveAsync(ticket, cancellationToken);
        var code = AppEnvironments.Normalize(request.Environment);
        if (code is not (AppEnvironments.Test or AppEnvironments.Prod)) throw new BusinessRuleException("Chọn môi trường TEST hoặc PROD.");
        await _guard.VerifyAsync($"Cài bản {ticket.Version} lên {code}", request.Password, request.Confirmation,
            IReleaseService.DeployConfirmation(ticket.Version), cancellationToken);
        var env = await AgentEnvironmentAsync(code, cancellationToken);
        var now = _clock.UtcNow;
        var parameters = JsonSerializer.Serialize(new
        {
            ticketId = ticket.Id,
            version = ticket.Version,
            sha256 = ticket.Sha256,
            fileName = ticket.PackageFileName,
            size = ticket.PackageSize,
            previousVersion = env.AppVersion,
            packagePath = $"/api/ops/v1/agent/releases/{ticket.Id}/package",
        }, Json);
        var command = AgentCommand.Create(env.Id, AgentCommandKind.DeployRelease, parameters,
            string.IsNullOrWhiteSpace(request.Reason) ? $"Cài bản {ticket.Version} lên {code}" : request.Reason, _currentUser.UserName, now);
        _db.AgentCommands.Add(command);
        var install = ticket.StartDeploy(code, command.Id, env.AppVersion, request.Reason, _currentUser.UserName, now);
        _db.Add(install);
        await _db.SaveChangesAsync(cancellationToken);
        await _operationLog.WriteAsync(new OperationLogEntry(OperationKinds.ReleaseInstallQueued, $"{ticket.Version} → {code}", request.Reason,
            OperationResult.Info, $"Lệnh {command.Id}: cài bản {ticket.Version} (đang chạy {env.AppVersion ?? "chưa rõ"}), SHA256 {ticket.Sha256}."),
            cancellationToken);
        return ToDto(ticket);
    }

    /// <inheritdoc />
    public async Task<ReleaseTicketDto> RollbackAsync(Guid id, RollbackReleaseRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureWritable();
        var ticket = await LoadAsync(id, cancellationToken);
        await SyncAndSaveAsync(ticket, cancellationToken);
        // Lát QT9: quay về được cả trên TEST (UAT không đạt) – trống là PROD như trước.
        var code = string.IsNullOrWhiteSpace(request.Environment) ? AppEnvironments.Prod : AppEnvironments.Normalize(request.Environment);
        if (code is not (AppEnvironments.Test or AppEnvironments.Prod)) throw new BusinessRuleException("Chọn môi trường TEST hoặc PROD.");
        await _guard.VerifyAsync($"Quay về bản trước {ticket.Version} trên {code}", request.Password, request.Confirmation,
            IReleaseService.RollbackConfirmation, cancellationToken);
        // Bản quay về = bản đang chạy trên môi trường đó trước lần cài thành công gần nhất của phiếu.
        var lastDeploy = ticket.Installs
            .Where(i => i.Kind == ReleaseInstallKind.Deploy && i.Environment == code && i.Status == ReleaseInstallStatus.Succeeded)
            .OrderByDescending(i => i.StartedAt).FirstOrDefault();
        var target = lastDeploy?.PreviousVersion;
        var env = await AgentEnvironmentAsync(code, cancellationToken);
        var now = _clock.UtcNow;
        var parameters = JsonSerializer.Serialize(new
        {
            ticketId = ticket.Id,
            version = ticket.Version,
            targetVersion = target,
            level = (int)request.Level,
        }, Json);
        var command = AgentCommand.Create(env.Id, AgentCommandKind.RollbackRelease, parameters, request.Reason, _currentUser.UserName, now);
        var install = ticket.StartRollback(code, request.Level, command.Id, target, request.Reason, _currentUser.UserName, now);
        _db.AgentCommands.Add(command);
        _db.Add(install);
        await _db.SaveChangesAsync(cancellationToken);
        await _operationLog.WriteAsync(new OperationLogEntry(OperationKinds.ReleaseInstallQueued, $"{ticket.Version} → {target} (quay về)",
            request.Reason, OperationResult.Info, $"Lệnh {command.Id}: quay về mức {(int)request.Level} trên {code}."), cancellationToken);
        return ToDto(ticket);
    }

    /// <inheritdoc />
    public async Task OnCommandFinishedAsync(Guid commandId, string? installedSha256, CancellationToken cancellationToken)
    {
        var install = await _db.ReleaseTickets.SelectMany(t => t.Installs).AsNoTracking()
            .FirstOrDefaultAsync(i => i.AgentCommandId == commandId, cancellationToken);
        if (install is null || install.Status != ReleaseInstallStatus.Pending) return;
        var ticket = await LoadAsync(install.TicketId, cancellationToken);
        var cmd = await _db.AgentCommands.AsNoTracking().FirstAsync(c => c.Id == commandId, cancellationToken);
        var running = await _db.AgentEnvironments.AsNoTracking().Where(e => e.Id == cmd.EnvironmentId).Select(e => e.AppVersion)
            .FirstOrDefaultAsync(cancellationToken);
        var entry = Complete(ticket, install.Id, cmd, installedSha256, running);
        await _db.SaveChangesAsync(cancellationToken);
        await _operationLog.WriteAsync(entry, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ReleasePackageFile> OpenPackageAsync(Guid environmentId, Guid ticketId, CancellationToken cancellationToken)
    {
        var commandIds = await _db.ReleaseTickets.Where(t => t.Id == ticketId).SelectMany(t => t.Installs)
            .Where(i => i.Status == ReleaseInstallStatus.Pending && i.Kind == ReleaseInstallKind.Deploy)
            .Select(i => i.AgentCommandId).ToListAsync(cancellationToken);
        // Chỉ tải được khi chính môi trường này đang chạy lệnh cài phiếu đó – khóa bộ thực thi bị lộ không lấy được gói tùy ý.
        var running = await _db.AgentCommands.AnyAsync(c => commandIds.Contains(c.Id) && c.EnvironmentId == environmentId
            && c.Status == AgentCommandStatus.Running, cancellationToken);
        if (!running) throw new ForbiddenAccessException("Môi trường này không có lệnh cài phiếu phát hành này đang chạy.");
        var ticket = await _db.ReleaseTickets.AsNoTracking().FirstAsync(t => t.Id == ticketId, cancellationToken);
        var path = PackagePath(ticket.Version, ticket.PackageFileName);
        if (!File.Exists(path)) throw new NotFoundException("Không còn file gói trên máy chủ – nhận lại gói.");
        return new ReleasePackageFile(path, ticket.PackageFileName, ticket.Sha256);
    }

    /// <inheritdoc />
    public async Task<SmokeResultDto> SmokeAsync(CancellationToken cancellationToken)
    {
        var checks = new List<SmokeCheckDto>();
        async Task Check(string name, Func<Task<string>> run)
        {
            try
            {
                checks.Add(new SmokeCheckDto(name, true, await run()));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                checks.Add(new SmokeCheckDto(name, false, ex.Message));
            }
        }

        // Không có người dùng / công ty → đọc bỏ filter công ty; chỉ đếm, không trả dữ liệu.
        await Check("Cơ sở dữ liệu", async () => $"{await _db.Companies.IgnoreQueryFilters().CountAsync(cancellationToken)} công ty");
        await Check("Đơn mua hàng", async () => $"{await _db.PurchaseOrders.IgnoreQueryFilters().CountAsync(cancellationToken)} phiếu");
        await Check("Nhà cung cấp", async () => $"{await _db.Suppliers.IgnoreQueryFilters().CountAsync(cancellationToken)} NCC");
        await Check("Trung tâm phát hành", async () => $"{await _db.ReleaseTickets.CountAsync(cancellationToken)} phiếu");
        return new SmokeResultDto(_version.Version, checks.All(c => c.Ok), checks);
    }

    /// <summary>Đường dẫn file gói của một bản: <c>&lt;PackageRoot&gt;\&lt;phiên bản&gt;\&lt;tên file&gt;</c>.</summary>
    /// <param name="version">Phiên bản.</param>
    /// <param name="fileName">Tên file.</param>
    /// <returns>Đường dẫn tuyệt đối.</returns>
    public string PackagePath(string version, string fileName) => Path.Combine(_options.PackageRoot, version, Path.GetFileName(fileName));

    /// <summary>Ghi gói ra thư mục tạm (tính SHA256, chặn quá dung lượng) → đọc nội dung → so SHA256 → chặn trùng bản → cất gói → tạo phiếu.</summary>
    private async Task<ReleaseTicket> ReceiveAsync(
        Stream content, string fileName, string? expectedSha256, ReleaseSource source, string? sourceUrl, string? note,
        CancellationToken cancellationToken)
    {
        var incoming = Path.Combine(_options.PackageRoot, "_incoming");
        Directory.CreateDirectory(incoming);
        var temp = Path.Combine(incoming, $"{Guid.NewGuid():N}.zip");
        try
        {
            long size;
            string sha;
            await using (var file = File.Create(temp))
            using (var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            {
                var buffer = new byte[81920];
                size = 0;
                int read;
                while ((read = await content.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    size += read;
                    if (size > MaxBytes) throw new BusinessRuleException($"Gói lớn quá {_options.MaxPackageSizeMb} MB.");
                    hash.AppendData(buffer, 0, read);
                    await file.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                }

                sha = Convert.ToHexStringLower(hash.GetHashAndReset());
            }

            if (size == 0) throw new BusinessRuleException("Gói rỗng.");
            var expected = expectedSha256?.Trim().ToLowerInvariant();
            if (!string.IsNullOrEmpty(expected) && expected != sha)
                throw new BusinessRuleException($"SHA256 của gói ({sha}) khác SHA256 kèm theo ({expected}) – gói hỏng hoặc bị thay, không nhận.");

            var package = ReleasePackageReader.Read(temp);
            if (await _db.ReleaseTickets.AnyAsync(t => t.Version == package.Version && t.Status != ReleaseStatus.Cancelled, cancellationToken))
                throw new BusinessRuleException($"Đã có phiếu phát hành bản {package.Version} – hủy phiếu cũ trước khi nhận lại.");

            var ticket = ReleaseTicket.Receive(
                new ReleasePackageInfo(package.Version, fileName, size, sha, source, sourceUrl, package.Changelog, package.DatabaseChanges,
                    package.MinRollbackLevel, package.RollbackPlan),
                package.TestItems, note, _currentUser.UserName, _clock.UtcNow);
            var target = PackagePath(package.Version, fileName);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Move(temp, target, overwrite: true);
            _db.ReleaseTickets.Add(ticket);
            await _db.SaveChangesAsync(cancellationToken);
            await _operationLog.WriteAsync(new OperationLogEntry(OperationKinds.ReleaseReceived, package.Version, null, OperationResult.Succeeded,
                $"{ReleaseLabels.Source(source)} {fileName} ({size:N0} byte), SHA256 {sha}, {ticket.Items.Count} mục kiểm thử, quay về tối thiểu mức " +
                $"{(int)package.MinRollbackLevel}."), cancellationToken);
            return ticket;
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }

    /// <summary>Đổi phiếu theo một bước domain, lưu, ghi nhật ký vận hành nếu là bước của phiếu.</summary>
    private async Task<ReleaseTicketDto> ChangeAsync(
        Guid id, Action<ReleaseTicket> change, Func<ReleaseTicket, (string Step, string? Reason)>? log, CancellationToken cancellationToken)
    {
        EnsureWritable();
        var ticket = await LoadAsync(id, cancellationToken);
        await SyncAndSaveAsync(ticket, cancellationToken);
        var before = ticket.Defects.Count;
        change(ticket);
        // Lỗi mới thêm vào danh sách của phiếu đang theo dõi – báo EF là dòng mới (Id sinh sẵn nên EF tưởng đã có).
        foreach (var added in ticket.Defects.Skip(before)) _db.Add(added);
        await _db.SaveChangesAsync(cancellationToken);
        if (log?.Invoke(ticket) is { } entry)
            await _operationLog.WriteAsync(new OperationLogEntry(OperationKinds.ReleaseStepChanged, $"{ticket.Version} – {entry.Step}", entry.Reason,
                OperationResult.Succeeded, $"Trạng thái: {ReleaseLabels.Status(ticket.Status).Label}."), cancellationToken);
        return ToDto(ticket);
    }

    /// <summary>
    /// Lần cài còn chờ mà lệnh đã kết thúc không qua báo kết quả (hết hạn, hủy, quá giờ) → ghi lỗi, lưu, ghi nhật ký vận hành. Lệnh báo kết quả bình
    /// thường đi qua <see cref="OnCommandFinishedAsync"/>.
    /// </summary>
    private async Task SyncAndSaveAsync(ReleaseTicket ticket, CancellationToken cancellationToken)
    {
        var pending = ticket.Installs.Where(i => i.Status == ReleaseInstallStatus.Pending).ToList();
        if (pending.Count == 0) return;
        var ids = pending.Select(i => i.AgentCommandId).ToList();
        var commands = await _db.AgentCommands.AsNoTracking().Where(c => ids.Contains(c.Id)).ToListAsync(cancellationToken);
        var entries = new List<OperationLogEntry>();
        foreach (var install in pending)
        {
            var cmd = commands.FirstOrDefault(c => c.Id == install.AgentCommandId);
            if (cmd is null || cmd.Status is AgentCommandStatus.Queued or AgentCommandStatus.Running) continue;
            entries.Add(Complete(ticket, install.Id, cmd, null, null));
        }

        if (entries.Count == 0) return;
        await _db.SaveChangesAsync(cancellationToken);
        foreach (var entry in entries) await _operationLog.WriteAsync(entry, cancellationToken);
    }

    /// <summary>Ghi kết quả lần cài vào phiếu; trả dòng nhật ký vận hành để ghi sau khi lưu.</summary>
    private OperationLogEntry Complete(ReleaseTicket ticket, Guid installId, AgentCommand cmd, string? installedSha256, string? runningVersion)
    {
        var ok = cmd.Status == AgentCommandStatus.Succeeded;
        var message = cmd.ResultMessage ?? AgentLabels.Status(cmd.Status).Label;
        ticket.CompleteInstall(installId, ok, installedSha256, message, "bộ thực thi", _clock.UtcNow, runningVersion);
        var install = ticket.Installs.First(i => i.Id == installId);
        return new OperationLogEntry(OperationKinds.ReleaseInstallFinished,
            $"{ticket.Version} – {(install.Kind == ReleaseInstallKind.Rollback ? "quay về" : "cài")} {install.Environment}", install.Reason,
            install.Status == ReleaseInstallStatus.Succeeded ? OperationResult.Succeeded : OperationResult.Failed,
            $"{install.Message} Phiếu: {ReleaseLabels.Status(ticket.Status).Label}.");
    }

    private async Task<ReleaseTicket> LoadAsync(Guid id, CancellationToken cancellationToken) =>
        await _db.ReleaseTickets.Include(t => t.Items).Include(t => t.Defects).Include(t => t.Installs)
            .AsSplitQuery().FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
        ?? throw new NotFoundException("Không tìm thấy phiếu phát hành.");

    /// <summary>Môi trường có bộ thực thi để giao lệnh: đã khai ở AGT, đang dùng, đã có khóa.</summary>
    private async Task<AgentEnvironment> AgentEnvironmentAsync(string code, CancellationToken cancellationToken)
    {
        var env = await _db.AgentEnvironments.AsNoTracking().FirstOrDefaultAsync(e => e.Code == code && e.IsActive, cancellationToken)
            ?? throw new BusinessRuleException($"Chưa khai môi trường {code} có bộ thực thi (màn hình AGT).");
        if (env.AgentKeyHash is null) throw new BusinessRuleException($"Môi trường {code} chưa có khóa bộ thực thi (AGT → Tạo khóa…).");
        return env;
    }

    private static bool HasTestEnvironment(IEnumerable<AgentEnvironment> envs) =>
        envs.Any(e => e.Code == AppEnvironments.Test && e.AgentKeyHash is not null);

    private string? ReadOnlyReason() => _environment.IsTest
        ? "Máy TEST chỉ xem phiếu phát hành – nhận gói, cài, xác nhận trên máy PROD (làm mới TEST từ PROD chép đè dữ liệu của TEST)."
        : null;

    private void EnsureWritable()
    {
        if (ReadOnlyReason() is { } reason) throw new BusinessRuleException(reason);
    }

    private static ReleaseTicketDto ToDto(ReleaseTicket t)
    {
        var (label, tone) = ReleaseLabels.Status(t.Status);
        return new ReleaseTicketDto(
            t.Id, t.Version, t.Status, label, tone, ReleaseLabels.Source(t.Source), t.SourceUrl, t.PackageFileName, t.PackageSize, t.Sha256,
            t.Changelog, t.DatabaseChanges, t.MinRollbackLevel, t.Risks, t.RollbackPlan, t.Note, t.ReceivedAt, t.ReceivedByName, t.UatSignedAt,
            t.UatSignedByName, t.UatComment, t.UatWithoutTest, t.ProdInstalledAt, t.EndedAt, t.EndedByName, t.EndReason, t.IsLocked, t.OpenIssues(),
            t.Items.OrderBy(i => i.Seq).Select(i => new ReleaseTestItemDto(i.Id, i.Seq, i.Section, i.Content, i.Result, i.TesterName, i.TestedAt, i.Comment))
                .ToList(),
            t.Defects.OrderBy(d => d.ReportedAt).Select(d => new ReleaseDefectDto(d.Id, d.Title, d.Description, d.Screen, d.ReporterName, d.ReportedAt,
                d.Status, d.Resolution, d.ResolvedByName, d.ResolvedAt)).ToList(),
            t.Installs.OrderByDescending(i => i.StartedAt).Select(i => new ReleaseInstallDto(i.Id, i.Environment, i.Kind, i.Status, i.PreviousVersion,
                i.TargetVersion, i.Level, i.InstalledSha256, i.Reason, i.ByName, i.StartedAt, i.FinishedAt, i.Message, i.AgentCommandId)).ToList(),
            t.InstalledOnTest);
    }

    /// <summary>Phần trả về của GitHub API cần dùng.</summary>
    private sealed record GitHubRelease(
        [property: JsonPropertyName("html_url")] string? HtmlUrl,
        [property: JsonPropertyName("assets")] IReadOnlyList<GitHubAsset> Assets);

    /// <summary>Một tài sản của bản phát hành.</summary>
    private sealed record GitHubAsset(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("size")] long Size,
        [property: JsonPropertyName("browser_download_url")] string BrowserDownloadUrl);
}
