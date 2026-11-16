using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Proc.Application.Common;
using Proc.Application.Operations;
using Proc.Domain.Common;
using Proc.Domain.Documents;
using Proc.Domain.Notifications;
using Proc.Domain.Operations;
using Proc.Infrastructure.Documents;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.Operations;

/// <summary>
/// Câu lệnh đặt lại phần riêng của TEST trên CSDL vừa khôi phục từ PROD (lát QT9 – design.md 21.5): thư chờ gửi của PROD thôi gửi (TEST không gửi lại
/// thư của PROD, kể cả về hộp thư test), mọi phiên đăng nhập hết hiệu lực. Bộ thực thi TEST chạy câu này trên CSDL mới <b>trước</b> khi hoán đổi.
/// Số trạng thái thư lấy từ enum (không viết cứng) để đổi enum là biết.
/// </summary>
public static class TestRefreshSql
{
    /// <summary>Ghi chú trên thư bỏ gửi.</summary>
    public const string SkipNote = "Làm mới TEST từ PROD – không gửi thư chờ của PROD.";

    /// <summary>Câu lệnh (PostgreSQL, chạy trong một giao dịch).</summary>
    public static string Reset
    {
        get
        {
            var pending = ((int)NotificationEmailState.Pending).ToString(CultureInfo.InvariantCulture);
            var skipped = ((int)NotificationEmailState.Skipped).ToString(CultureInfo.InvariantCulture);
            return
                $"UPDATE public.\"Notification\" SET \"EmailState\" = {skipped}, \"EmailedAt\" = now(), \"EmailNote\" = '{SkipNote}' " +
                $"WHERE \"EmailState\" = {pending};\n" +
                $"UPDATE public.\"SupplierPortalEmail\" SET \"State\" = {skipped}, \"SentAt\" = now(), \"Note\" = '{SkipNote}' WHERE \"State\" = {pending};\n" +
                "UPDATE public.\"AccessPolicy\" SET \"SessionsRevokedAt\" = date_trunc('second', now());\n";
        }
    }
}

/// <summary>HttpClient gọi PROD (chuyển báo lỗi từ TEST) – bọc lại như <see cref="ReleaseGitHubHttp"/> (Api tạo qua IHttpClientFactory).</summary>
/// <param name="Client">HttpClient.</param>
public sealed record UpstreamHttp(HttpClient Client);

/// <summary>
/// Làm mới TEST từ PROD – màn hình TST (lát QT9). Chạy trên máy nguồn dữ liệu (PROD; DEV thử); máy TEST chỉ xem. Không bảng riêng: mỗi lần làm mới
/// là một lệnh bộ thực thi <see cref="AgentCommandKind.RefreshTest"/> của môi trường TEST (lịch sử = các lệnh đó).
/// </summary>
public sealed class TestRefreshService : ITestRefreshService, ITestRefreshAgentHooks
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly ProcDbContext _db;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;
    private readonly IOperationLog _operationLog;
    private readonly IDangerousActionGuard _guard;
    private readonly BackupOptions _backup;
    private readonly AttachmentOptions _attachments;
    private readonly AppEnvironmentOptions _environment;
    private readonly AgentOptions _agent;
    private readonly AppVersion _version;

    /// <summary>Tạo dịch vụ.</summary>
    /// <param name="db">CSDL.</param>
    /// <param name="user">Người thao tác.</param>
    /// <param name="clock">Đồng hồ.</param>
    /// <param name="operationLog">Nhật ký vận hành.</param>
    /// <param name="guard">Xác nhận Q6.</param>
    /// <param name="backup">Cấu hình sao lưu (thư mục chứa bản pg_dump).</param>
    /// <param name="attachments">Thư mục đính kèm (đã đổi thành đường dẫn tuyệt đối).</param>
    /// <param name="environment">Môi trường máy này.</param>
    /// <param name="agent">Cấu hình bộ thực thi (số phút coi là đang liên lạc).</param>
    /// <param name="version">Phiên bản máy này.</param>
    public TestRefreshService(
        ProcDbContext db, ICurrentUser user, IClock clock, IOperationLog operationLog, IDangerousActionGuard guard, IOptions<BackupOptions> backup,
        IOptions<AttachmentOptions> attachments, IOptions<AppEnvironmentOptions> environment, IOptions<AgentOptions> agent, AppVersion version)
    {
        _db = db;
        _user = user;
        _clock = clock;
        _operationLog = operationLog;
        _guard = guard;
        _backup = backup.Value;
        _attachments = attachments.Value;
        _environment = environment.Value;
        _agent = agent.Value;
        _version = version;
    }

    /// <inheritdoc />
    public async Task<TestRefreshOverviewDto> GetOverviewAsync(CancellationToken cancellationToken)
    {
        var (overview, _, _) = await EvaluateAsync(cancellationToken);
        return overview;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AgentCommandDto>> SearchAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        if (to < from) throw new BusinessRuleException("Đến ngày phải sau hoặc bằng từ ngày.");
        if (to.DayNumber - from.DayNumber > 366) throw new BusinessRuleException("Khoảng ngày tối đa 366 ngày.");
        var start = DayStartUtc(from);
        var end = DayStartUtc(to.AddDays(1));
        var rows = await _db.AgentCommands.AsNoTracking()
            .Where(c => c.Kind == AgentCommandKind.RefreshTest && c.CreatedAt >= start && c.CreatedAt < end)
            .OrderByDescending(c => c.CreatedAt).Take(500).ToListAsync(cancellationToken);
        var envs = await _db.AgentEnvironments.AsNoTracking().ToDictionaryAsync(e => e.Id, cancellationToken);
        return rows.Select(c => AgentMapping.ToDto(c, envs.GetValueOrDefault(c.EnvironmentId), false)).ToList();
    }

    /// <inheritdoc />
    public async Task<AgentCommandDto> QueueAsync(RunTestRefreshRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var (overview, test, dump) = await EvaluateAsync(cancellationToken);
        if (overview.Blockers.Count > 0) throw new BusinessRuleException(string.Join(" ", overview.Blockers));
        // Q6: dữ liệu TEST (phiếu nhập thử, quyền cấp thêm – E8) bị thay toàn bộ.
        await _guard.VerifyAsync("Làm mới TEST từ PROD", request.Password, request.Confirmation, ITestRefreshService.Confirmation, cancellationToken);

        var parameters = JsonSerializer.Serialize(new
        {
            sourceEnvironment = _environment.Name,
            sourceVersion = _version.Version,
            dumpRunId = dump!.Id,
            dumpFileName = Path.GetFileName(dump.RelativePath),
            dumpFinishedAt = dump.FinishedAt,
            sha256 = dump.Sha256,
            size = dump.SizeBytes,
            dumpPath = "/api/ops/v1/agent/test-refresh/dump",
            attachmentsPath = "/api/ops/v1/agent/test-refresh/attachments",
            keepTables = TestRefreshCarryOver.Tables,
            resetSql = TestRefreshSql.Reset,
        }, Json);
        var command = AgentCommand.Create(test!.Id, AgentCommandKind.RefreshTest, parameters, request.Reason, _user.UserName, _clock.UtcNow);
        _db.AgentCommands.Add(command);
        await _db.SaveChangesAsync(cancellationToken);
        await _operationLog.WriteAsync(new OperationLogEntry(OperationKinds.AgentCommandQueued, $"{test.Code} – {AgentLabels.Kind(command.Kind)}",
            command.Reason, OperationResult.Info,
            $"Lệnh {command.Id}: làm mới TEST bằng {Path.GetFileName(dump.RelativePath)} ({Vn(dump.FinishedAt)}, SHA256 {dump.Sha256}), bản {_version.Version}."),
            cancellationToken);
        return AgentMapping.ToDto(command, test, false);
    }

    /// <inheritdoc />
    public async Task<ReleasePackageFile> OpenDumpAsync(Guid environmentId, CancellationToken cancellationToken)
    {
        var cmd = await RunningCommandAsync(environmentId, cancellationToken);
        var runId = JsonDocument.Parse(cmd.Parameters ?? "{}").RootElement.TryGetProperty("dumpRunId", out var p) && p.TryGetGuid(out var g) ? g : Guid.Empty;
        var run = await _db.BackupRuns.AsNoTracking().FirstOrDefaultAsync(r => r.Id == runId, cancellationToken)
            ?? throw new NotFoundException("Không còn lần sao lưu của lệnh làm mới.");
        var path = DumpPath(run) ?? throw new NotFoundException("Không còn file pg_dump trên máy chủ – sao lưu lại rồi làm mới lại.");
        return new ReleasePackageFile(path, Path.GetFileName(path), run.Sha256 ?? string.Empty);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AttachmentManifestFile>> GetAttachmentManifestAsync(Guid environmentId, CancellationToken cancellationToken)
    {
        await RunningCommandAsync(environmentId, cancellationToken);
        var root = Path.GetFullPath(_attachments.RootPath);
        if (!Directory.Exists(root)) return [];
        return Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            // File tạm đang tải lên (".upload-…") chưa phải đính kèm.
            .Where(f => !Path.GetFileName(f).StartsWith(".upload-", StringComparison.Ordinal))
            .Select(f => new AttachmentManifestFile(Path.GetRelativePath(root, f).Replace(Path.DirectorySeparatorChar, '/'), new FileInfo(f).Length))
            .OrderBy(f => f.Path, StringComparer.Ordinal)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<string> OpenAttachmentAsync(Guid environmentId, string? path, CancellationToken cancellationToken)
    {
        await RunningCommandAsync(environmentId, cancellationToken);
        var root = Path.GetFullPath(_attachments.RootPath);
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path)) throw new BusinessRuleException("Đường dẫn file không hợp lệ.");
        var full = Path.GetFullPath(Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar)));
        // Chặn "..": chỉ trả file nằm trong thư mục đính kèm (so tên theo hệ điều hành – Windows không phân biệt hoa thường).
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!full.StartsWith(Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar, comparison))
            throw new BusinessRuleException("Đường dẫn file không hợp lệ.");
        return File.Exists(full) ? full : throw new NotFoundException("Không có file đính kèm này.");
    }

    /// <summary>Tổng quan + môi trường TEST + bản dump (để giao lệnh dùng lại đúng các giá trị đã kiểm).</summary>
    private async Task<(TestRefreshOverviewDto Overview, AgentEnvironment? Test, BackupRun? Dump)> EvaluateAsync(CancellationToken ct)
    {
        var blockers = new List<string>();
        var warnings = new List<string>();
        string? readOnly = _environment.IsTest
            ? "Máy TEST chỉ xem – làm mới TEST chạy trên máy PROD (nguồn dữ liệu), bộ thực thi TEST nhận lệnh từ đó."
            : null;
        if (readOnly is not null) blockers.Add(readOnly);

        var now = _clock.UtcNow;
        var test = await _db.AgentEnvironments.AsNoTracking().FirstOrDefaultAsync(e => e.Code == AppEnvironments.Test, ct);
        TestRefreshTargetDto? target = null;
        Guid? openRefresh = null;
        if (test is null)
        {
            blockers.Add("Chưa khai môi trường TEST ở màn hình AGT (Bộ thực thi).");
        }
        else
        {
            var (light, label, _) = AgentMapping.Contact(test, now, _agent.OnlineMinutes);
            target = new TestRefreshTargetDto(test.Code, test.Name, light, label, test.AppVersion, test.AgentVersion, test.LastSeenAt,
                test.ServiceKeyHash is not null);
            if (!test.IsActive) blockers.Add("Môi trường TEST đang ngừng dùng (AGT).");
            else if (test.AgentKeyHash is null) blockers.Add("TEST chưa có khóa bộ thực thi (AGT → Tạo khóa…).");
            else if (light == "Warning") warnings.Add("Bộ thực thi TEST đang mất liên lạc – lệnh chờ tối đa 1 giờ rồi hết hạn.");

            // Cấu trúc CSDL đi theo phiên bản: khôi phục bản dump của bản X vào TEST đang chạy bản Y thì phần mềm TEST không chạy đúng.
            if (test.AppVersion is null)
                blockers.Add("Chưa biết phiên bản phần mềm trên TEST – chờ bộ thực thi TEST liên lạc (AGT → Lệnh thử).");
            else if (!string.Equals(test.AppVersion, _version.Version, StringComparison.Ordinal))
                blockers.Add($"TEST đang chạy bản {test.AppVersion}, máy này bản {_version.Version} – CSDL hai bản có thể khác cấu trúc. Đưa TEST về đúng bản " +
                    $"{_version.Version} (cài hoặc quay về ở REL) rồi làm mới.");

            var open = await _db.AgentCommands.AsNoTracking()
                .Where(c => c.EnvironmentId == test.Id && (c.Status == AgentCommandStatus.Queued || c.Status == AgentCommandStatus.Running))
                .OrderBy(c => c.CreatedAt).ToListAsync(ct);
            var live = open.Where(c => !c.IsExpired(now)).ToList();
            if (live.Count > 0)
            {
                blockers.Add($"TEST đang có lệnh \"{AgentLabels.Kind(live[0].Kind)}\" chờ / chạy – chờ xong rồi làm mới.");
                openRefresh = live.FirstOrDefault(c => c.Kind == AgentCommandKind.RefreshTest)?.Id;
            }

            if (test.ServiceKeyHash is null)
                warnings.Add("TEST chưa có khóa dịch vụ – nút \"Báo lỗi / góp ý\" trên TEST chưa gửi về đây được (AGT → Tạo khóa dịch vụ…).");
        }

        BackupRun? dump = null;
        TestRefreshDumpDto? dumpDto = null;
        if (string.IsNullOrWhiteSpace(_backup.RootPath))
        {
            blockers.Add("Máy này chưa khai Backup:RootPath – chưa có bản pg_dump để làm mới.");
        }
        else
        {
            dump = await _db.BackupRuns.AsNoTracking()
                .Where(r => r.Kind == BackupKind.Dump && r.Status == BackupRunStatus.Succeeded && r.Verified != false && r.PurgedAt == null
                    && r.Sha256 != null && r.RelativePath != null)
                .OrderByDescending(r => r.FinishedAt).FirstOrDefaultAsync(ct);
            if (dump is null)
            {
                blockers.Add("Chưa có bản pg_dump dùng được – màn hình BAK → Sao lưu ngay → pg_dump.");
            }
            else
            {
                var age = dump.FinishedAt is { } f ? (now - f).TotalHours : 0;
                var stale = age > ITestRefreshService.StaleHours;
                dumpDto = new TestRefreshDumpDto(dump.Id, dump.FinishedAt, dump.RelativePath, dump.SizeBytes, dump.Sha256, Math.Round(age, 1), stale);
                if (DumpPath(dump) is null) blockers.Add($"Không thấy file {dump.RelativePath} trong thư mục sao lưu.");
                else if (stale)
                    warnings.Add($"Bản pg_dump đã {Math.Floor(age).ToString(CultureInfo.InvariantCulture)} giờ – TEST sẽ có dữ liệu PROD lúc " +
                        $"{Vn(dump.FinishedAt)}. Muốn mới hơn: BAK → Sao lưu ngay → pg_dump rồi làm mới.");
            }
        }

        var overview = new TestRefreshOverviewDto(_environment.Name, _version.Version, readOnly, target, dumpDto, blockers, warnings,
            TestRefreshCarryOver.Tables, openRefresh);
        return (overview, test, dump);
    }

    /// <summary>Lệnh làm mới đang chạy của môi trường (khóa bộ thực thi lộ không lấy được bản dump / file tùy lúc).</summary>
    private async Task<AgentCommand> RunningCommandAsync(Guid environmentId, CancellationToken ct) =>
        await _db.AgentCommands.AsNoTracking().FirstOrDefaultAsync(c => c.EnvironmentId == environmentId && c.Kind == AgentCommandKind.RefreshTest
            && c.Status == AgentCommandStatus.Running, ct)
        ?? throw new ForbiddenAccessException("Môi trường này không có lệnh làm mới TEST đang chạy.");

    private string? DumpPath(BackupRun run)
    {
        if (string.IsNullOrWhiteSpace(_backup.RootPath) || run.RelativePath is null) return null;
        var path = Path.Combine(_backup.RootPath, run.RelativePath);
        return File.Exists(path) ? path : null;
    }

    private static string Vn(DateTimeOffset? utc) => utc is { } u
        ? TimeZoneInfo.ConvertTime(u, VietnamTime.Zone).ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)
        : "?";

    private static DateTimeOffset DayStartUtc(DateOnly day)
    {
        var local = day.ToDateTime(TimeOnly.MinValue);
        return new DateTimeOffset(local, VietnamTime.Zone.GetUtcOffset(local)).ToUniversalTime();
    }
}

/// <summary>
/// Hộp nhận báo lỗi từ TEST trên PROD (lát QT9 – design.md 21.6 "Lỗi phát hiện gửi từ nút Báo lỗi / góp ý trên TEST"): khóa dịch vụ của môi trường
/// TEST → dòng Lỗi phát hiện của phiếu phát hành <b>cùng phiên bản</b> TEST đang chạy (phiếu chưa hủy / đóng / quay về), ảnh đính kèm vào phiếu.
/// </summary>
public sealed class ReleaseDefectInbox : IReleaseDefectInbox
{
    private readonly ProcDbContext _db;
    private readonly IClock _clock;
    private readonly IOperationLog _operationLog;
    private readonly AttachmentService _attachments;
    private readonly AppEnvironmentOptions _environment;

    /// <summary>Tạo dịch vụ.</summary>
    /// <param name="db">CSDL.</param>
    /// <param name="clock">Đồng hồ.</param>
    /// <param name="operationLog">Nhật ký vận hành.</param>
    /// <param name="attachments">Đính kèm (ghi ảnh vào phiếu phát hành).</param>
    /// <param name="environment">Môi trường máy này.</param>
    public ReleaseDefectInbox(
        ProcDbContext db, IClock clock, IOperationLog operationLog, AttachmentService attachments, IOptions<AppEnvironmentOptions> environment)
    {
        _db = db;
        _clock = clock;
        _operationLog = operationLog;
        _attachments = attachments;
        _environment = environment.Value;
    }

    /// <inheritdoc />
    public async Task<string?> AuthenticateAsync(string? key, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(key) || !key.Trim().StartsWith(AgentKeys.ServicePrefix, StringComparison.Ordinal)) return null;
        var hash = AgentKeys.Hash(key);
        var env = await _db.AgentEnvironments.AsNoTracking()
            .Where(e => e.ServiceKeyHash == hash)
            .Select(e => new { e.Code, e.ServiceKeyHash, e.IsActive })
            .FirstOrDefaultAsync(cancellationToken);
        return env is { IsActive: true } && AgentKeys.HashEquals(env.ServiceKeyHash!, hash) ? env.Code : null;
    }

    /// <inheritdoc />
    public async Task<ForwardDefectResult> ReceiveAsync(string environmentCode, ForwardDefectRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (_environment.IsTest) throw new BusinessRuleException("Máy TEST không nhận báo lỗi – báo lỗi gửi về trung tâm phát hành trên PROD.");
        if (environmentCode != AppEnvironments.Test) throw new ForbiddenAccessException("Chỉ máy TEST gửi báo lỗi về trung tâm phát hành.");
        var version = request.Version?.Trim() ?? string.Empty;
        if (version.Length == 0) throw new BusinessRuleException("Thiếu phiên bản của máy TEST.");
        if (string.IsNullOrWhiteSpace(request.Title)) throw new BusinessRuleException("Nhập tiêu đề lỗi.");

        var ticket = await _db.ReleaseTickets.Include(t => t.Defects)
            .Where(t => t.Version == version && t.Status != ReleaseStatus.Cancelled && t.Status != ReleaseStatus.Closed
                && t.Status != ReleaseStatus.RolledBack)
            .OrderByDescending(t => t.ReceivedAt).FirstOrDefaultAsync(cancellationToken)
            ?? throw new BusinessRuleException(
                $"Trung tâm phát hành chưa có phiếu bản {version} đang mở – báo người quản trị phát hành (lỗi chưa được ghi).");

        // Ảnh ghi trước lỗi: ảnh sai (đuôi, dung lượng, nội dung) thì từ chối cả báo lỗi – người dùng sửa rồi gửi lại, không sinh lỗi trùng.
        var description = request.Description?.Trim();
        if (!string.IsNullOrWhiteSpace(request.ImageBase64))
        {
            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(request.ImageBase64);
            }
            catch (FormatException)
            {
                throw new BusinessRuleException("Ảnh gửi kèm bị hỏng.");
            }

            var fileName = Path.GetFileName(request.ImageFileName ?? string.Empty);
            if (fileName.Length == 0) fileName = "anh-bao-loi.png";
            using var content = new MemoryStream(bytes);
            var file = await _attachments.UploadSystemAsync(AttachmentOwnerTypes.ReleaseTicket, ticket.Id, fileName, bytes.LongLength, content,
                $"Ảnh báo lỗi từ TEST: {request.Title.Trim()} – {request.ReporterName}", cancellationToken);
            description = string.IsNullOrEmpty(description) ? $"Ảnh: {file.FileName} (tab Đính kèm)." : $"{description}\nẢnh: {file.FileName} (tab Đính kèm).";
        }

        var before = ticket.Defects.Count;
        ticket.AddDefect(request.Title, description, request.Screen, $"{request.ReporterName} (TEST)", _clock.UtcNow);
        foreach (var added in ticket.Defects.Skip(before)) _db.Add(added);
        await _db.SaveChangesAsync(cancellationToken);
        var open = ticket.Defects.Count(d => d.Status == ReleaseDefectStatus.Open);
        var status = ReleaseLabels.Status(ticket.Status).Label;
        await _operationLog.WriteAsync(new OperationLogEntry(OperationKinds.ReleaseDefectReported, $"{ticket.Version} – lỗi từ TEST", null,
            OperationResult.Info, $"{request.ReporterName}: {request.Title.Trim()} (màn hình {request.Screen ?? "?"})."), cancellationToken);
        return new ForwardDefectResult(ticket.Version, status, open,
            $"Đã ghi lỗi vào phiếu phát hành bản {ticket.Version} ({status}) – phiếu có {open} lỗi chưa xử lý.");
    }
}

/// <summary>
/// Nút "Báo lỗi / góp ý" trên máy TEST (lát QT9): gửi máy chủ – máy chủ về PROD (<see cref="UpstreamOptions"/>, khóa dịch vụ trong appsettings – trình
/// duyệt không cầm khóa). Người báo lấy từ phiên đăng nhập, phiên bản lấy từ máy TEST – không tin dữ liệu client.
/// </summary>
public sealed class TestFeedbackService : ITestFeedbackService
{
    /// <summary>Ảnh base64 dài tối đa (ký tự) – chặn sớm request quá lớn; dung lượng thật PROD kiểm theo giới hạn đính kèm.</summary>
    public const int MaxImageBase64Length = 8_000_000;

    private readonly UpstreamHttp _http;
    private readonly IOptionsMonitor<UpstreamOptions> _upstream;
    private readonly AppEnvironmentOptions _environment;
    private readonly ICurrentUser _user;
    private readonly ProcDbContext _db;
    private readonly AppVersion _version;
    private readonly ILogger<TestFeedbackService> _logger;

    /// <summary>Tạo dịch vụ.</summary>
    /// <param name="http">HttpClient gọi PROD.</param>
    /// <param name="upstream">Địa chỉ, khóa dịch vụ.</param>
    /// <param name="environment">Môi trường máy này.</param>
    /// <param name="user">Người báo.</param>
    /// <param name="db">CSDL (họ tên người báo).</param>
    /// <param name="version">Phiên bản máy này.</param>
    /// <param name="logger">Log kỹ thuật.</param>
    public TestFeedbackService(
        UpstreamHttp http, IOptionsMonitor<UpstreamOptions> upstream, IOptions<AppEnvironmentOptions> environment, ICurrentUser user, ProcDbContext db,
        AppVersion version, ILogger<TestFeedbackService> logger)
    {
        _http = http;
        _upstream = upstream;
        _environment = environment.Value;
        _user = user;
        _db = db;
        _version = version;
        _logger = logger;
    }

    /// <inheritdoc />
    public bool Enabled => _environment.IsTest && _upstream.CurrentValue.IsConfigured;

    /// <inheritdoc />
    public async Task<ForwardDefectResult> SendAsync(SendFeedbackRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!Enabled)
            throw new BusinessRuleException("Báo lỗi / góp ý chỉ dùng trên máy TEST đã khai Operations:Upstream (địa chỉ PROD, khóa dịch vụ).");
        if (string.IsNullOrWhiteSpace(request.Title)) throw new BusinessRuleException("Nhập tiêu đề lỗi.");
        if (request.ImageBase64?.Length > MaxImageBase64Length) throw new BusinessRuleException("Ảnh quá lớn – chụp lại vùng nhỏ hơn.");

        var options = _upstream.CurrentValue;
        var fullName = _user.UserId is { } id
            ? await _db.Users.AsNoTracking().Where(u => u.Id == id).Select(u => u.FullName).FirstOrDefaultAsync(cancellationToken)
            : null;
        var reporter = string.IsNullOrWhiteSpace(fullName) ? _user.UserName ?? "?" : $"{_user.UserName} – {fullName}";
        var body = new ForwardDefectRequest(_version.Version, request.Title.Trim(), request.Description?.Trim(), request.Screen?.Trim(), reporter,
            request.ImageFileName, request.ImageBase64);

        using var message = new HttpRequestMessage(HttpMethod.Post, $"{options.BaseUrl!.TrimEnd('/')}/api/ops/v1/service/defects")
        {
            Content = JsonContent.Create(body),
        };
        message.Headers.Add(ServiceHeaders.Key, options.ServiceKey!.Trim());
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(options.TimeoutSeconds, 5, 300)));
        HttpResponseMessage response;
        try
        {
            response = await _http.Client.SendAsync(message, timeout.Token);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Không gửi được báo lỗi về PROD {Url}", options.BaseUrl);
            throw new BusinessRuleException($"Không liên lạc được máy PROD ({options.BaseUrl}) – thử lại sau hoặc báo quản trị.");
        }

        using (response)
        {
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<ForwardDefectResult>(cancellationToken)
                    ?? throw new BusinessRuleException("PROD trả kết quả rỗng.");
            if (response.StatusCode == HttpStatusCode.Unauthorized)
                throw new BusinessRuleException("PROD từ chối khóa dịch vụ – quản trị tạo lại khóa dịch vụ TEST trên PROD (AGT) và sửa " +
                    "Operations:Upstream:ServiceKey.");
            string? title = null;
            try
            {
                using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
                if (doc.RootElement.TryGetProperty("title", out var t)) title = t.GetString();
            }
            catch (JsonException)
            {
                // Trang lỗi không phải JSON (proxy, IIS) – báo theo mã.
            }

            throw new BusinessRuleException(title is null
                ? $"PROD trả lỗi {(int)response.StatusCode} – lỗi chưa được ghi."
                : $"PROD từ chối: {title}");
        }
    }
}
