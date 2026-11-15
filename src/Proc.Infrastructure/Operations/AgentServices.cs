using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Proc.Application.Common;
using Proc.Application.Operations;
using Proc.Domain.Common;
using Proc.Domain.Operations;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.Operations;

/// <summary>
/// Khóa bộ thực thi (lát QT5): chuỗi <c>qta_</c> + 32 byte ngẫu nhiên (base64url, 43 ký tự) – đủ mạnh để chỉ cần băm SHA256 một lần (không như mật
/// khẩu người gõ phải băm chậm). CSDL lưu băm hex chữ thường; so bằng <see cref="CryptographicOperations.FixedTimeEquals"/> để không lộ qua thời gian.
/// </summary>
public static class AgentKeys
{
    /// <summary>Tiền tố khóa – nhìn là biết khóa bộ thực thi (dò khóa lộ trong log, repo).</summary>
    public const string Prefix = "qta_";

    /// <summary>Tạo khóa mới.</summary>
    /// <returns>Khóa.</returns>
    public static string Generate() =>
        Prefix + Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>Băm SHA256 (hex chữ thường) của khóa.</summary>
    /// <param name="key">Khóa.</param>
    /// <returns>64 ký tự hex.</returns>
    public static string Hash(string key) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(key.Trim())));

    /// <summary>So hai băm hex thời gian cố định.</summary>
    /// <param name="a">Băm thứ nhất.</param>
    /// <param name="b">Băm thứ hai.</param>
    /// <returns>Trùng nhau.</returns>
    public static bool HashEquals(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(a), Encoding.ASCII.GetBytes(b));
}

/// <summary>Đổi entity bộ thực thi sang DTO, tình trạng liên lạc.</summary>
public static class AgentMapping
{
    /// <summary>DTO lệnh.</summary>
    /// <param name="c">Lệnh.</param>
    /// <param name="env">Môi trường của lệnh.</param>
    /// <param name="withJournal">Kèm nhật ký chi tiết.</param>
    /// <returns>DTO.</returns>
    public static AgentCommandDto ToDto(AgentCommand c, AgentEnvironment? env, bool withJournal)
    {
        ArgumentNullException.ThrowIfNull(c);
        var (label, light) = AgentLabels.Status(c.Status);
        long? duration = c.StartedAt is { } s && c.FinishedAt is { } f ? (long)(f - s).TotalSeconds : null;
        return new AgentCommandDto(c.Id, c.EnvironmentId, env?.Code ?? "?", env?.Name ?? "(đã xóa)", c.Kind.ToString(), AgentLabels.Kind(c.Kind),
            c.Status.ToString(), label, light, c.Reason, c.CreatedByName, c.CreatedAt, c.ExpiresAt, c.StartedAt, c.FinishedAt, duration,
            c.ResultMessage, c.CancelledByName, withJournal ? c.Journal : null);
    }

    /// <summary>
    /// Tình trạng liên lạc: ngừng dùng / chưa có khóa (đỏ) · chưa liên lạc lần nào (trung tính) · liên lạc trong <paramref name="onlineMinutes"/>
    /// phút (xanh) · quá thì vàng "Mất liên lạc" (tác vụ chạy mỗi phút – máy tắt, tác vụ hỏng, sai khóa).
    /// </summary>
    /// <param name="e">Môi trường.</param>
    /// <param name="now">Thời điểm (UTC).</param>
    /// <param name="onlineMinutes">Số phút coi là đang liên lạc.</param>
    /// <returns>(đèn, chữ, ghi chú).</returns>
    public static (string Light, string Label, string? Note) Contact(AgentEnvironment e, DateTimeOffset now, int onlineMinutes)
    {
        ArgumentNullException.ThrowIfNull(e);
        if (!e.IsActive) return ("Error", "Ngừng dùng", "Khóa bộ thực thi bị từ chối khi môi trường ngừng dùng.");
        if (e.AgentKeyHash is null) return ("Error", "Chưa có khóa", "Bấm \"Tạo khóa…\" rồi dán khóa vào cấu hình bộ thực thi trên máy đó.");
        if (e.LastSeenAt is not { } seen)
            return ("Neutral", "Chưa liên lạc", "Cài bộ thực thi trên máy đó (deploy-windows.md mục 4d), tác vụ chạy mỗi phút.");
        var minutes = (now - seen).TotalMinutes;
        if (minutes <= onlineMinutes) return ("Ok", "Đang liên lạc", null);
        return ("Warning", "Mất liên lạc",
            $"Bộ thực thi không liên lạc {Math.Floor(minutes).ToString(CultureInfo.InvariantCulture)} phút – kiểm tra máy, tác vụ Task Scheduler " +
            "\"Procurement QT agent\", file agent-journal.log, khóa trong cấu hình.");
    }
}

/// <summary>Quản trị bộ thực thi – màn hình AGT (lát QT5).</summary>
public sealed class AgentAdminService : IAgentAdminService
{
    /// <summary>Số lệnh tối đa mỗi lần tìm.</summary>
    public const int MaxRows = 2000;

    private readonly ProcDbContext _db;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;
    private readonly IOperationLog _operationLog;
    private readonly IDangerousActionGuard _guard;
    private readonly AgentOptions _options;
    private readonly AppEnvironmentOptions _environment;

    /// <summary>Tạo dịch vụ.</summary>
    /// <param name="db">CSDL.</param>
    /// <param name="user">Người đang đăng nhập.</param>
    /// <param name="clock">Đồng hồ.</param>
    /// <param name="operationLog">Nhật ký vận hành.</param>
    /// <param name="guard">Xác nhận việc nguy hiểm Q6.</param>
    /// <param name="options">Cấu hình bộ thực thi.</param>
    /// <param name="environment">Môi trường máy đang chạy.</param>
    public AgentAdminService(
        ProcDbContext db, ICurrentUser user, IClock clock, IOperationLog operationLog, IDangerousActionGuard guard, IOptions<AgentOptions> options,
        IOptions<AppEnvironmentOptions> environment)
    {
        _db = db;
        _user = user;
        _clock = clock;
        _operationLog = operationLog;
        _guard = guard;
        _options = options.Value;
        _environment = environment.Value;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AgentEnvironmentDto>> GetEnvironmentsAsync(CancellationToken cancellationToken)
    {
        var envs = await _db.AgentEnvironments.AsNoTracking().ToListAsync(cancellationToken);
        var open = await _db.AgentCommands.AsNoTracking()
            .Where(c => c.Status == AgentCommandStatus.Queued || c.Status == AgentCommandStatus.Running)
            .Select(c => new { c.Id, c.EnvironmentId, c.Status })
            .ToListAsync(cancellationToken);
        var now = _clock.UtcNow;
        return envs
            .OrderBy(e => AppEnvironments.All.ToList().IndexOf(e.Code))
            .Select(e => ToDto(e, open.Count(c => c.EnvironmentId == e.Id && c.Status == AgentCommandStatus.Queued),
                open.FirstOrDefault(c => c.EnvironmentId == e.Id && c.Status == AgentCommandStatus.Running)?.Id, now))
            .ToList();
    }

    /// <inheritdoc />
    public async Task<AgentEnvironmentDto> CreateEnvironmentAsync(SaveAgentEnvironmentRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var env = AgentEnvironment.Register(request.Code, request.Name, request.Address);
        if (!request.IsActive) env.Update(request.Name, request.Address, false);
        if (await _db.AgentEnvironments.AnyAsync(e => e.Code == env.Code, cancellationToken))
            throw new BusinessRuleException($"Môi trường {env.Code} đã khai.");
        _db.AgentEnvironments.Add(env);
        await _db.SaveChangesAsync(cancellationToken);
        await LogAsync(OperationKinds.AgentEnvironmentSaved, env.Code, null, OperationResult.Succeeded,
            $"Khai môi trường {env.Code} – {env.Name}{(env.Address is null ? "" : $" ({env.Address})")}.", cancellationToken);
        return ToDto(env, 0, null, _clock.UtcNow);
    }

    /// <inheritdoc />
    public async Task<AgentEnvironmentDto> UpdateEnvironmentAsync(Guid id, SaveAgentEnvironmentRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var env = await FindAsync(id, cancellationToken);
        env.Update(request.Name, request.Address, request.IsActive);
        await _db.SaveChangesAsync(cancellationToken);
        await LogAsync(OperationKinds.AgentEnvironmentSaved, env.Code, null, OperationResult.Succeeded,
            $"Sửa môi trường {env.Code} – {env.Name}, {(env.IsActive ? "đang dùng" : "ngừng dùng")}.", cancellationToken);
        var queued = await _db.AgentCommands.CountAsync(c => c.EnvironmentId == id && c.Status == AgentCommandStatus.Queued, cancellationToken);
        var running = await _db.AgentCommands.Where(c => c.EnvironmentId == id && c.Status == AgentCommandStatus.Running)
            .Select(c => (Guid?)c.Id).FirstOrDefaultAsync(cancellationToken);
        return ToDto(env, queued, running, _clock.UtcNow);
    }

    /// <inheritdoc />
    public async Task<AgentKeyCreatedDto> CreateKeyAsync(Guid id, CreateAgentKeyRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var env = await FindAsync(id, cancellationToken);
        // Khóa cho phép ra lệnh quyền cao trên máy chủ (cài bản, khôi phục) – việc nguy hiểm Q6: nhập lại mật khẩu + gõ "TẠO KHÓA".
        await _guard.VerifyAsync($"Tạo khóa bộ thực thi {env.Code}", request.Password, request.Confirmation, AgentConfirmations.CreateKey,
            cancellationToken);
        var key = AgentKeys.Generate();
        var hint = key[^4..];
        env.SetKey(AgentKeys.Hash(key), hint, _user.UserName, _clock.UtcNow);
        await _db.SaveChangesAsync(cancellationToken);
        await LogAsync(OperationKinds.AgentKeyCreated, env.Code, null, OperationResult.Succeeded,
            $"Tạo khóa bộ thực thi mới (…{hint}) – khóa cũ hết hiệu lực.", cancellationToken);
        return new AgentKeyCreatedDto(env.Id, key, hint);
    }

    /// <inheritdoc />
    public async Task<AgentCommandSearchResult> SearchCommandsAsync(
        DateOnly from, DateOnly to, Guid? environmentId, CancellationToken cancellationToken)
    {
        if (to < from) throw new BusinessRuleException("Đến ngày phải sau hoặc bằng từ ngày.");
        if (to.DayNumber - from.DayNumber > 366) throw new BusinessRuleException("Khoảng ngày tối đa 366 ngày.");
        var start = VietnamDayStartUtc(from);
        var end = VietnamDayStartUtc(to.AddDays(1));
        var query = _db.AgentCommands.AsNoTracking().Where(c => c.CreatedAt >= start && c.CreatedAt < end);
        if (environmentId is { } envId) query = query.Where(c => c.EnvironmentId == envId);
        var rows = await query.OrderByDescending(c => c.CreatedAt).Take(MaxRows + 1).ToListAsync(cancellationToken);
        var envs = await _db.AgentEnvironments.AsNoTracking().ToDictionaryAsync(e => e.Id, cancellationToken);
        return new AgentCommandSearchResult(
            rows.Take(MaxRows).Select(c => AgentMapping.ToDto(c, envs.GetValueOrDefault(c.EnvironmentId), false)).ToList(), rows.Count > MaxRows);
    }

    /// <inheritdoc />
    public async Task<AgentCommandDto> GetCommandAsync(Guid id, CancellationToken cancellationToken)
    {
        var cmd = await _db.AgentCommands.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy lệnh bộ thực thi.");
        var env = await _db.AgentEnvironments.AsNoTracking().FirstOrDefaultAsync(e => e.Id == cmd.EnvironmentId, cancellationToken);
        return AgentMapping.ToDto(cmd, env, true);
    }

    /// <inheritdoc />
    public async Task<AgentCommandDto> QueueAsync(QueueAgentCommandRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var env = await FindAsync(request.EnvironmentId, cancellationToken);
        if (!env.IsActive) throw new BusinessRuleException($"Môi trường {env.Code} đang ngừng dùng.");
        if (env.AgentKeyHash is null) throw new BusinessRuleException($"Môi trường {env.Code} chưa có khóa bộ thực thi – tạo khóa trước.");
        // Lệnh khôi phục / hoán đổi (QT6) chỉ ra từ màn hình RST – ở đó có kiểm tra kế hoạch, xác nhận Q6, tham số đúng của lần phục hồi.
        if (request.Kind != AgentCommandKind.Ping)
            throw new BusinessRuleException("Màn hình AGT chỉ ra được Lệnh thử; khôi phục / hoán đổi làm ở màn hình Phục hồi (RST).");
        var cmd = AgentCommand.Create(env.Id, request.Kind, null, request.Reason, _user.UserName, _clock.UtcNow);
        _db.AgentCommands.Add(cmd);
        await _db.SaveChangesAsync(cancellationToken);
        await LogAsync(OperationKinds.AgentCommandQueued, $"{env.Code} – {AgentLabels.Kind(cmd.Kind)}", cmd.Reason, OperationResult.Info,
            $"Lệnh {cmd.Id} chờ bộ thực thi lấy đến {Vn(cmd.ExpiresAt)}.", cancellationToken);
        return AgentMapping.ToDto(cmd, env, false);
    }

    /// <inheritdoc />
    public async Task<AgentCommandDto> CancelAsync(Guid id, CancellationToken cancellationToken)
    {
        var cmd = await _db.AgentCommands.FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy lệnh bộ thực thi.");
        cmd.Cancel(_user.UserName, _clock.UtcNow);
        await _db.SaveChangesAsync(cancellationToken);
        var env = await _db.AgentEnvironments.AsNoTracking().FirstOrDefaultAsync(e => e.Id == cmd.EnvironmentId, cancellationToken);
        await LogAsync(OperationKinds.AgentCommandCancelled, $"{env?.Code} – {AgentLabels.Kind(cmd.Kind)}", cmd.Reason, OperationResult.Info,
            $"Hủy lệnh {cmd.Id}.", cancellationToken);
        return AgentMapping.ToDto(cmd, env, false);
    }

    private AgentEnvironmentDto ToDto(AgentEnvironment e, int queued, Guid? running, DateTimeOffset now)
    {
        var (light, label, note) = AgentMapping.Contact(e, now, _options.OnlineMinutes);
        return new AgentEnvironmentDto(e.Id, e.Code, e.Name, e.Address, e.IsActive, e.Code == _environment.Name, e.AgentKeyHash is not null,
            e.AgentKeyHint, e.KeyCreatedAt, e.KeyCreatedByName, e.LastSeenAt, e.AgentVersion, e.AppVersion, e.HostName, light, label, note, queued,
            running);
    }

    private async Task<AgentEnvironment> FindAsync(Guid id, CancellationToken cancellationToken) =>
        await _db.AgentEnvironments.FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
        ?? throw new NotFoundException("Không tìm thấy môi trường bộ thực thi.");

    private Task LogAsync(string kind, string? target, string? reason, OperationResult result, string detail, CancellationToken ct) =>
        _operationLog.WriteAsync(new OperationLogEntry(kind, target, reason, result, detail), ct);

    private static string Vn(DateTimeOffset utc) =>
        TimeZoneInfo.ConvertTime(utc, VietnamTime.Zone).ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);

    private static DateTimeOffset VietnamDayStartUtc(DateOnly day)
    {
        var local = day.ToDateTime(TimeOnly.MinValue);
        return new DateTimeOffset(local, VietnamTime.Zone.GetUtcOffset(local)).ToUniversalTime();
    }
}

/// <summary>
/// Cổng bộ thực thi (lát QT5). Giao lệnh trong giao dịch có khóa dòng môi trường (<c>FOR UPDATE</c>) nên hai lần hỏi cùng lúc của một môi trường
/// (tác vụ chạy chồng, hai máy dùng nhầm một khóa) không cùng nhận lệnh; chỉ mục duy nhất "một lệnh Đang chạy mỗi môi trường" chặn thêm ở CSDL.
/// </summary>
public sealed class AgentGateway : IAgentGateway
{
    private readonly ProcDbContext _db;
    private readonly IClock _clock;
    private readonly IOperationLog _operationLog;
    private readonly AgentOptions _options;
    private readonly IRestoreAgentHooks _restore;

    /// <summary>Tạo cổng.</summary>
    /// <param name="db">CSDL.</param>
    /// <param name="clock">Đồng hồ.</param>
    /// <param name="operationLog">Nhật ký vận hành.</param>
    /// <param name="options">Cấu hình bộ thực thi.</param>
    /// <param name="restore">Cập nhật lần phục hồi theo lệnh bộ thực thi (lát QT6).</param>
    public AgentGateway(ProcDbContext db, IClock clock, IOperationLog operationLog, IOptions<AgentOptions> options, IRestoreAgentHooks restore)
    {
        _restore = restore;
        _db = db;
        _clock = clock;
        _operationLog = operationLog;
        _options = options.Value;
    }

    /// <inheritdoc />
    public async Task<Guid?> AuthenticateAsync(string? key, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(key) || !key.Trim().StartsWith(AgentKeys.Prefix, StringComparison.Ordinal)) return null;
        var hash = AgentKeys.Hash(key);
        // Tìm theo băm (chỉ mục duy nhất) rồi so lại thời gian cố định – băm không lộ khóa nên tìm bằng = an toàn.
        var env = await _db.AgentEnvironments.AsNoTracking()
            .Where(e => e.AgentKeyHash == hash)
            .Select(e => new { e.Id, e.AgentKeyHash, e.IsActive })
            .FirstOrDefaultAsync(cancellationToken);
        return env is { IsActive: true } && AgentKeys.HashEquals(env.AgentKeyHash!, hash) ? env.Id : null;
    }

    /// <inheritdoc />
    public async Task<AgentWorkDto?> PollAsync(Guid environmentId, AgentPollInfo info, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(info);
        var now = _clock.UtcNow;
        var finished = new List<(AgentCommand Command, string Detail)>();
        AgentWorkDto? work = null;
        string? envCode;

        await using (var tx = await _db.Database.BeginTransactionAsync(cancellationToken))
        {
            var env = await _db.AgentEnvironments
                .FromSql($"SELECT * FROM \"AgentEnvironment\" WHERE \"Id\" = {environmentId} FOR UPDATE")
                .FirstAsync(cancellationToken);
            envCode = env.Code;
            env.RecordContact(info.AgentVersion, info.AppVersion, info.Host, now);

            var open = await _db.AgentCommands
                .Where(c => c.EnvironmentId == environmentId
                    && (c.Status == AgentCommandStatus.Queued || c.Status == AgentCommandStatus.Running))
                .OrderBy(c => c.CreatedAt)
                .ToListAsync(cancellationToken);

            if (!info.Peek)
            {
                // Lệnh chờ quá hạn: không chạy nữa (máy bật lại sau nhiều giờ không được tự cài bản cũ, khôi phục cũ).
                foreach (var c in open.Where(c => c.IsExpired(now)))
                {
                    c.Expire(now);
                    finished.Add((c, "Hết hạn – bộ thực thi không lấy lệnh trong 1 giờ."));
                }

                // Lệnh chạy quá giờ không báo kết quả: bộ thực thi chết giữa chừng – ghi lỗi để nhận lệnh mới.
                var timeout = TimeSpan.FromHours(Math.Max(1, _options.RunningTimeoutHours));
                foreach (var c in open.Where(c => c.Status == AgentCommandStatus.Running && now - c.StartedAt >= timeout))
                {
                    c.FailStale($"Bộ thực thi không báo kết quả sau {_options.RunningTimeoutHours} giờ – kiểm tra agent-journal.log trên máy.", now);
                    finished.Add((c, c.ResultMessage!));
                }
            }

            var running = open.FirstOrDefault(c => c.Status == AgentCommandStatus.Running);
            var next = open.FirstOrDefault(c => c.Status == AgentCommandStatus.Queued && !c.IsExpired(now));
            // Mỗi môi trường chỉ một lệnh chạy: đang có lệnh chạy thì chưa giao lệnh mới (bộ thực thi giữ khóa file nên bình thường không xảy ra).
            if (running is null && next is not null)
            {
                if (!info.Peek) next.Claim(now);
                work = new AgentWorkDto(next.Id, next.Kind.ToString(), next.Parameters, next.Reason, next.CreatedByName, next.ExpiresAt, !info.Peek);
            }

            await _db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
        }

        foreach (var (command, detail) in finished)
        {
            await LogFinishedAsync(envCode, command, detail, cancellationToken);
            await _restore.OnCommandFinishedAsync(command.Id, cancellationToken);
        }

        return work;
    }

    /// <inheritdoc />
    public async Task ReportAsync(Guid environmentId, Guid commandId, AgentResultRequest result, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(result);
        var cmd = await _db.AgentCommands.FirstOrDefaultAsync(c => c.Id == commandId && c.EnvironmentId == environmentId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy lệnh của môi trường này.");
        var now = _clock.UtcNow;
        cmd.Report(result.Succeeded, result.Message, result.Journal, now);
        var env = await _db.AgentEnvironments.FirstAsync(e => e.Id == environmentId, cancellationToken);
        env.RecordContact(null, null, null, now);
        await _db.SaveChangesAsync(cancellationToken);
        await LogFinishedAsync(env.Code, cmd, cmd.ResultMessage ?? "", cancellationToken);
        // Lệnh của lần phục hồi (QT6): cập nhật lần phục hồi theo kết quả (sẵn sàng kiểm tra, đã hoán đổi, lỗi…).
        await _restore.OnCommandFinishedAsync(cmd.Id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task PrepareSwapAsync(Guid environmentId, Guid commandId, CancellationToken cancellationToken)
    {
        var cmd = await _db.AgentCommands.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == commandId && c.EnvironmentId == environmentId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy lệnh của môi trường này.");
        if (cmd.Status != AgentCommandStatus.Running || cmd.Kind is not (AgentCommandKind.SwapDatabase or AgentCommandKind.SwapCluster))
            throw new BusinessRuleException("Chỉ lệnh hoán đổi đang chạy mới xin chuẩn bị hoán đổi được.");
        var env = await _db.AgentEnvironments.AsNoTracking().FirstAsync(e => e.Id == environmentId, cancellationToken);
        await _restore.PrepareSwapAsync(cmd.Id, env.Code, cancellationToken);
    }

    private Task LogFinishedAsync(string? envCode, AgentCommand c, string detail, CancellationToken ct) =>
        _operationLog.WriteAsync(new OperationLogEntry(OperationKinds.AgentCommandFinished, $"{envCode} – {AgentLabels.Kind(c.Kind)}", c.Reason,
            c.Status == AgentCommandStatus.Succeeded ? OperationResult.Succeeded : OperationResult.Failed,
            $"Lệnh {c.Id}: {AgentLabels.Status(c.Status).Label}. {detail}"), ct);
}

/// <summary>
/// Chế độ bảo trì (lát QT5). Singleton: đệm trạng thái <see cref="IMaintenanceMode.CacheSeconds"/> giây (middleware hỏi ở mọi request – không đọc
/// CSDL mỗi lần); mỗi lần đọc / ghi mở scope CSDL riêng. Đọc CSDL lỗi (CSDL đang dừng) thì coi như <b>tắt</b> – không khóa người dùng vì lỗi đọc;
/// lỗi CSDL tự hiện ở chính request.
/// </summary>
public sealed class MaintenanceModeService : IMaintenanceMode
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IClock _clock;
    private readonly SemaphoreSlim _refresh = new(1, 1);
    private (MaintenanceModeDto State, DateTimeOffset LoadedAt)? _cache;

    /// <summary>Tạo dịch vụ.</summary>
    /// <param name="scopes">Tạo scope CSDL.</param>
    /// <param name="clock">Đồng hồ.</param>
    public MaintenanceModeService(IServiceScopeFactory scopes, IClock clock)
    {
        _scopes = scopes;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task<MaintenanceModeDto> GetAsync(CancellationToken cancellationToken)
    {
        if (Fresh() is { } cached) return cached;
        await _refresh.WaitAsync(cancellationToken);
        try
        {
            if (Fresh() is { } again) return again;
            MaintenanceModeDto state;
            try
            {
                await using var scope = _scopes.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<ProcDbContext>();
                var row = await db.MaintenanceModes.AsNoTracking().FirstOrDefaultAsync(m => m.Id == MaintenanceModeState.SingletonId, cancellationToken);
                state = row is null ? Off : ToDto(row);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                state = Off;
            }

            _cache = (state, _clock.UtcNow);
            return state;
        }
        finally
        {
            _refresh.Release();
        }
    }

    /// <inheritdoc />
    public async Task<MaintenanceModeDto> SetAsync(SetMaintenanceModeRequest request, string? changedByName, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await using var scope = _scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ProcDbContext>();
        var row = await db.MaintenanceModes.FirstOrDefaultAsync(m => m.Id == MaintenanceModeState.SingletonId, cancellationToken);
        if (row is null)
        {
            row = MaintenanceModeState.CreateDefault();
            db.MaintenanceModes.Add(row);
        }

        var now = _clock.UtcNow;
        if (request.IsOn) row.TurnOn(request.Message, request.Reason, changedByName, now);
        else row.TurnOff(request.Reason, changedByName, now);
        await db.SaveChangesAsync(cancellationToken);

        var state = ToDto(row);
        _cache = (state, now);
        await scope.ServiceProvider.GetRequiredService<IOperationLog>().WriteAsync(new OperationLogEntry(OperationKinds.MaintenanceModeChanged,
            request.IsOn ? "Bật chế độ bảo trì" : "Tắt chế độ bảo trì", row.Reason, OperationResult.Succeeded,
            request.IsOn ? $"Câu thông báo: {row.Message}" : "Người dùng vào lại bình thường."), cancellationToken);
        return state;
    }

    private static readonly MaintenanceModeDto Off = new(false, null, null, null, null, null);

    private MaintenanceModeDto? Fresh() =>
        _cache is { } c && _clock.UtcNow - c.LoadedAt < TimeSpan.FromSeconds(IMaintenanceMode.CacheSeconds) ? c.State : null;

    private static MaintenanceModeDto ToDto(MaintenanceModeState m) =>
        new(m.IsOn, m.Message, m.Reason, m.StartedAt, m.ChangedByName, m.ChangedAt);
}
