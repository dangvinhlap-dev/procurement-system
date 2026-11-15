using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using Proc.Application.Common;
using Proc.Application.Operations;
using Proc.Domain.Common;
using Proc.Domain.Notifications;
using Proc.Domain.Operations;
using Proc.Domain.Organization;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.Operations;

/// <summary>
/// Ghi nhật ký lỗi (lát QT2, design.md mục 21.4 ERR). Singleton, mỗi lần ghi một scope DI riêng (DbContext riêng – thao tác chính vừa lỗi có
/// thể đã làm hỏng DbContext của request). Cách gộp:
/// <list type="number">
/// <item>Tính dấu vết (<see cref="ErrorFingerprint"/>); chưa có nhóm thì tạo nhóm Mới (hai request cùng tạo – trùng khóa – thì quay về bước 2).</item>
/// <item>Đã có nhóm: tăng số lần, lần cuối bằng một lệnh UPDATE nguyên tử; nhóm Đã xử lý thì chuyển về Mới, tăng số lần mở lại.</item>
/// <item>Nhóm mới hoặc mở lại: thông báo trong phần mềm cho người có quyền <c>System.OpsErrorLog.Manage</c> ở tổng công ty – <b>không gửi
/// email</b> (người dùng chốt 15/11/2026).</item>
/// </list>
/// Không bao giờ ném lỗi: hỏng thì ghi log kỹ thuật kèm mã theo dõi, vẫn trả mã cho người dùng.
/// </summary>
public sealed class ErrorRecorder : IErrorRecorder
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IClock _clock;
    private readonly AppVersion _version;
    private readonly AppEnvironmentOptions _environment;
    private readonly ILogger<ErrorRecorder> _logger;

    /// <summary>Tạo bộ ghi.</summary>
    /// <param name="scopes">Tạo scope DI riêng.</param>
    /// <param name="clock">Đồng hồ.</param>
    /// <param name="version">Phiên bản đang chạy.</param>
    /// <param name="environment">Môi trường.</param>
    /// <param name="logger">Log kỹ thuật.</param>
    public ErrorRecorder(
        IServiceScopeFactory scopes, IClock clock, AppVersion version, IOptions<AppEnvironmentOptions> environment, ILogger<ErrorRecorder> logger)
    {
        _scopes = scopes;
        _clock = clock;
        _version = version;
        _environment = environment.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<string> RecordServerAsync(ServerErrorReport report, CancellationToken cancellationToken)
    {
        // Lỗi gói trong lớp ngoài (TargetInvocationException, AggregateException…) thì dấu vết theo lỗi trong cùng – đó mới là chỗ hỏng thật.
        var inner = report.Exception;
        while (inner.InnerException is not null && inner is AggregateException or System.Reflection.TargetInvocationException)
            inner = inner.InnerException;
        var path = ErrorFingerprint.NormalizePath(report.Path);
        var location = ErrorFingerprint.ServerLocation(inner.StackTrace);
        var type = inner.GetType().FullName ?? inner.GetType().Name;
        return RecordAsync(new Pending(ErrorSource.Server, type, inner.Message, location, path, report.Path, report.Method,
            report.Exception.ToString(), report.UserAgent, report.CompanyId), cancellationToken);
    }

    /// <inheritdoc />
    public Task<string> RecordClientAsync(ClientErrorRequest request, string? userAgent, Guid? companyId, CancellationToken cancellationToken)
    {
        var url = request.Url;
        // Trang giao diện: chỉ lấy phần đường dẫn (bỏ http://máy:cổng) để cùng lỗi trên máy khác nhau vẫn một nhóm.
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri)) url = uri.PathAndQuery;
        var path = ErrorFingerprint.NormalizePath(url);
        var type = string.IsNullOrWhiteSpace(request.Name) ? "Error" : request.Name.Trim();
        var location = ErrorFingerprint.ClientLocation(request.Stack, request.Message);
        var detail = new StringBuilder();
        detail.AppendLine($"Loại: {request.Kind ?? "error"}");
        if (!string.IsNullOrWhiteSpace(request.Stack)) detail.AppendLine().AppendLine(request.Stack.Trim());
        if (!string.IsNullOrWhiteSpace(request.ComponentStack)) detail.AppendLine().AppendLine("Cây component:").AppendLine(request.ComponentStack.Trim());
        return RecordAsync(new Pending(ErrorSource.Client, type, request.Message, location, path, url, null, detail.ToString(), userAgent,
            companyId), cancellationToken);
    }

    /// <summary>Thông tin một lần lỗi đã chuẩn hóa.</summary>
    private sealed record Pending(
        ErrorSource Source, string Type, string? Message, string? Location, string? NormalizedPath, string? RawPath, string? Method, string? Detail,
        string? UserAgent, Guid? CompanyId);

    /// <summary>Ghi nhóm + lần xảy ra; không ném lỗi.</summary>
    private async Task<string> RecordAsync(Pending e, CancellationToken cancellationToken)
    {
        var code = ErrorFingerprint.NewTrackingCode();
        try
        {
            var now = _clock.UtcNow;
            var fingerprint = ErrorFingerprint.Compute(e.Source, e.Type, e.Location, e.NormalizedPath);
            await using var scope = _scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ProcDbContext>();
            var user = scope.ServiceProvider.GetRequiredService<ICurrentUser>();

            var (groupId, isNew, reopened) = await UpsertGroupAsync(db, fingerprint, e, now, cancellationToken);
            var companyCode = e.CompanyId is { } cid
                ? await db.Companies.IgnoreQueryFilters().Where(c => c.Id == cid).Select(c => c.Code).FirstOrDefaultAsync(cancellationToken)
                : null;
            db.ErrorOccurrences.Add(ErrorOccurrence.Create(groupId, now, code, e.Source, user.UserId, user.UserName, e.CompanyId, companyCode,
                e.RawPath, e.Method, e.Message, e.Detail, _version.Version, _environment.Name, e.UserAgent));
            if (isNew || reopened) await NotifyManagersAsync(db, groupId, e, code, reopened, now, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Không ghi được nhật ký lỗi {TrackingCode} ({Source} {Path}).", code, e.Source, e.RawPath);
        }

        return code;
    }

    /// <summary>Tạo nhóm hoặc tăng số lần của nhóm sẵn có; trả Id nhóm, có phải nhóm mới, có phải vừa mở lại.</summary>
    private static async Task<(Guid Id, bool IsNew, bool Reopened)> UpsertGroupAsync(
        ProcDbContext db, string fingerprint, Pending e, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var id = await db.ErrorGroups.Where(g => g.Fingerprint == fingerprint).Select(g => (Guid?)g.Id).FirstOrDefaultAsync(cancellationToken);
        if (id is null)
        {
            var group = ErrorGroup.Create(fingerprint, e.Source, e.Type, e.Message, e.Location, e.NormalizedPath, now);
            db.ErrorGroups.Add(group);
            try
            {
                await db.SaveChangesAsync(cancellationToken);
                return (group.Id, true, false);
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                // Request khác vừa tạo cùng nhóm: bỏ nhóm của mình, cộng vào nhóm đã có.
                db.Entry(group).State = EntityState.Detached;
                id = await db.ErrorGroups.Where(g => g.Fingerprint == fingerprint).Select(g => g.Id).FirstAsync(cancellationToken);
            }
        }

        var reopened = await db.ErrorGroups.Where(g => g.Id == id && g.Status == ErrorGroupStatus.Resolved)
            .ExecuteUpdateAsync(s => s
                .SetProperty(g => g.Status, ErrorGroupStatus.New)
                .SetProperty(g => g.ReopenCount, g => g.ReopenCount + 1)
                .SetProperty(g => g.OccurrenceCount, g => g.OccurrenceCount + 1)
                .SetProperty(g => g.LastSeenAt, now), cancellationToken) > 0;
        if (!reopened)
        {
            await db.ErrorGroups.Where(g => g.Id == id)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(g => g.OccurrenceCount, g => g.OccurrenceCount + 1)
                    .SetProperty(g => g.LastSeenAt, now), cancellationToken);
        }

        return (id.Value, false, reopened);
    }

    /// <summary>
    /// Thông báo nhóm lỗi mới / mở lại cho người có quyền xử lý ở tổng công ty – kể cả chính người gặp lỗi nếu họ có quyền (khác thông báo nghiệp
    /// vụ: ở đây không có "người gây ra sự kiện"). Chỉ hiện trong phần mềm: đánh dấu bỏ qua email ngay khi tạo để dịch vụ gửi email không gửi
    /// (người dùng chốt 15/11/2026 – không cần gửi lỗi qua email).
    /// </summary>
    private static async Task NotifyManagersAsync(
        ProcDbContext db, Guid groupId, Pending e, string code, bool reopened, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var headOffice = await db.Companies.IgnoreQueryFilters().Where(c => c.IsHeadOffice && c.IsActive).Select(c => (Guid?)c.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (headOffice is not { } companyId) return;
        // Cùng cách chọn người nhận của NotificationPublisher.UsersWithPermissionAsync (hợp quyền mọi vai trò còn dùng tại công ty).
        var activeRoles = db.UserCompanyRoles.Where(x => x.CompanyId == companyId && x.Company!.IsActive && x.Role!.IsActive);
        var holders = activeRoles
            .Where(x => db.RolePermissions.Any(rp => rp.RoleId == x.RoleId && rp.PermissionCode == Permissions.System.OpsErrorLogManage))
            .Select(x => x.UserId);
        var users = await db.Users.Where(u => u.IsActive && holders.Contains(u.Id)).Select(u => u.Id).ToListAsync(cancellationToken);
        var source = e.Source == ErrorSource.Server ? "máy chủ" : "giao diện";
        var title = reopened ? $"Lỗi {source} lặp lại sau khi đã xử lý" : $"Lỗi {source} mới";
        var message = $"{e.Type}: {e.Message} – mã theo dõi {code}" + (e.RawPath is { } p ? $" – {p}" : string.Empty);
        foreach (var userId in users.Distinct())
        {
            var notification = Notification.Create(companyId, userId, NotificationTypes.ErrorLogNew,
                Cut(title, Notification.TitleMaxLength), Cut(message, Notification.MessageMaxLength), "ERR", groupId, now);
            notification.SkipEmail(now, NoEmailReason);
            db.Notifications.Add(notification);
        }
    }

    /// <summary>Lý do bỏ qua email của thông báo nhật ký lỗi.</summary>
    public const string NoEmailReason = "Thông báo nhật ký lỗi chỉ hiện trong phần mềm, không gửi email.";

    private static string Cut(string value, int max) => value.Length <= max ? value : value[..(max - 1)] + "…";
}

/// <summary>Màn hình ERR: tìm, xem, đổi trạng thái nhóm lỗi; dọn lỗi quá hạn (lát QT2).</summary>
public sealed class ErrorLogService : IErrorLogService
{
    private readonly ProcDbContext _db;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;
    private readonly IOperationLog _operationLog;

    /// <summary>Tạo dịch vụ.</summary>
    /// <param name="db">DbContext.</param>
    /// <param name="user">Người đang thao tác.</param>
    /// <param name="clock">Đồng hồ.</param>
    /// <param name="operationLog">Nhật ký vận hành (đổi trạng thái, dọn lỗi).</param>
    public ErrorLogService(ProcDbContext db, ICurrentUser user, IClock clock, IOperationLog operationLog)
    {
        _db = db;
        _user = user;
        _clock = clock;
        _operationLog = operationLog;
    }

    /// <inheritdoc />
    public async Task<ErrorGroupPage> SearchAsync(
        DateOnly from, DateOnly to, ErrorGroupStatus? status, string? trackingCode, CancellationToken cancellationToken)
    {
        var query = _db.ErrorGroups.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(trackingCode))
        {
            // Người dùng đọc mã qua điện thoại: chấp nhận thiếu "ERR-", chữ thường.
            var code = trackingCode.Trim().ToUpperInvariant();
            if (!code.StartsWith("ERR-", StringComparison.Ordinal)) code = "ERR-" + code;
            query = query.Where(g => _db.ErrorOccurrences.Any(o => o.GroupId == g.Id && o.TrackingCode == code));
        }
        else
        {
            if (to < from) throw new BusinessRuleException("Đến ngày phải sau hoặc bằng Từ ngày.");
            if (to.DayNumber - from.DayNumber >= OperationLogQueries.MaxDays)
                throw new BusinessRuleException($"Mỗi lần chỉ xem được tối đa {OperationLogQueries.MaxDays} ngày nhật ký.");
            var (start, end) = (OperationLogQueries.StartOfDay(from), OperationLogQueries.StartOfDay(to.AddDays(1)));
            query = query.Where(g => g.LastSeenAt >= start && g.LastSeenAt < end);
        }

        if (status is { } s) query = query.Where(g => g.Status == s);
        var rows = await Project(query.OrderByDescending(g => g.LastSeenAt)).Take(IErrorLogService.MaxGroups + 1).ToListAsync(cancellationToken);
        return new ErrorGroupPage(rows.Take(IErrorLogService.MaxGroups).ToList(), rows.Count > IErrorLogService.MaxGroups);
    }

    /// <inheritdoc />
    public async Task<ErrorGroupDetailDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var group = await Project(_db.ErrorGroups.AsNoTracking().Where(g => g.Id == id)).FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy nhóm lỗi.");
        var occurrences = await _db.ErrorOccurrences.AsNoTracking()
            .Where(o => o.GroupId == id)
            .OrderByDescending(o => o.OccurredAt)
            .Take(IErrorLogService.MaxOccurrences)
            .Select(o => new ErrorOccurrenceDto(o.Id, o.OccurredAt, o.TrackingCode, o.UserName, o.CompanyCode, o.Path, o.Method, o.Message,
                o.Detail, o.Version, o.Environment, o.UserAgent))
            .ToListAsync(cancellationToken);
        return new ErrorGroupDetailDto(group, occurrences);
    }

    /// <inheritdoc />
    public async Task<ErrorGroupItemDto> ChangeStatusAsync(Guid id, ChangeErrorGroupStatusRequest request, CancellationToken cancellationToken)
    {
        var group = await _db.ErrorGroups.FirstOrDefaultAsync(g => g.Id == id, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy nhóm lỗi.");
        if (!Enum.TryParse<ErrorGroupStatus>(request.Status, ignoreCase: true, out var status) || !Enum.IsDefined(status)
            || int.TryParse(request.Status, out _))
            throw new BusinessRuleException("Trạng thái nhóm lỗi chỉ là Mới, Đang xử lý hoặc Đã xử lý.");
        var before = group.Status;
        group.ChangeStatus(status, request.Note, _user.UserName, _clock.UtcNow);
        await _db.SaveChangesAsync(cancellationToken);
        await _operationLog.WriteAsync(new OperationLogEntry(OperationKinds.ErrorGroupStatusChanged, $"{group.ErrorType} ({group.Source})",
            request.Note, OperationResult.Succeeded, $"{before} → {group.Status}; {group.Message}"), cancellationToken);
        return await Project(_db.ErrorGroups.AsNoTracking().Where(g => g.Id == id)).FirstAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<(int Occurrences, int Groups)> PurgeAsync(CancellationToken cancellationToken)
    {
        // Lỗi là của cả hệ thống: chỉ tham số của tổng công ty có tác dụng (design.md mục 21.4 ERR).
        var keepDays = await _db.CompanyParameters.IgnoreQueryFilters()
            .Where(p => _db.Companies.IgnoreQueryFilters().Any(c => c.Id == p.CompanyId && c.IsHeadOffice))
            .Select(p => (int?)p.ErrorLogKeepDays)
            .FirstOrDefaultAsync(cancellationToken) ?? CompanyParameter.DefaultErrorLogKeepDays;
        var cutoff = _clock.UtcNow.AddDays(-keepDays);
        var occurrences = await _db.ErrorOccurrences.Where(o => o.OccurredAt < cutoff).ExecuteDeleteAsync(cancellationToken);
        var groups = await _db.ErrorGroups
            .Where(g => g.LastSeenAt < cutoff && !_db.ErrorOccurrences.Any(o => o.GroupId == g.Id))
            .ExecuteDeleteAsync(cancellationToken);
        if (occurrences > 0 || groups > 0)
        {
            await _operationLog.WriteAsync(new OperationLogEntry(OperationKinds.ErrorLogPurged, "Nhật ký lỗi", $"Giữ {keepDays} ngày",
                OperationResult.Succeeded, $"Xóa {occurrences} lần xảy ra, {groups} nhóm lỗi cũ hơn {cutoff:yyyy-MM-dd HH:mm} UTC."), cancellationToken);
        }

        return (occurrences, groups);
    }

    /// <summary>Nhóm lỗi thành DTO, kèm mã theo dõi và phiên bản của lần gần nhất còn giữ.</summary>
    private IQueryable<ErrorGroupItemDto> Project(IQueryable<ErrorGroup> query) =>
        query.Select(g => new ErrorGroupItemDto(
            g.Id, g.Source.ToString(), g.Status.ToString(), g.ErrorType, g.Message, g.Location, g.Path, g.FirstSeenAt, g.LastSeenAt,
            g.OccurrenceCount, g.ReopenCount, g.HandledByName, g.HandledAt, g.Note,
            _db.ErrorOccurrences.Where(o => o.GroupId == g.Id).OrderByDescending(o => o.OccurredAt).Select(o => o.TrackingCode).FirstOrDefault(),
            _db.ErrorOccurrences.Where(o => o.GroupId == g.Id).OrderByDescending(o => o.OccurredAt).Select(o => o.Version).FirstOrDefault()));
}

/// <summary>
/// Dịch vụ nền dọn nhật ký lỗi quá hạn giữ (lát QT2): chạy sau khi khởi động 5 phút rồi mỗi 6 giờ, gọi <see cref="IErrorLogService.PurgeAsync"/>
/// trong một scope mới. Lỗi một lần dọn chỉ ghi log kỹ thuật, lần sau dọn lại.
/// </summary>
public sealed class ErrorLogCleanupBackgroundService : BackgroundService
{
    /// <summary>Tên dịch vụ trên màn hình SYS.</summary>
    public const string ServiceName = "Dọn nhật ký lỗi";

    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);
    private static readonly TimeSpan StartDelay = TimeSpan.FromMinutes(5);

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<ErrorLogCleanupBackgroundService> _logger;
    private readonly IBackgroundHeartbeat _heartbeat;

    /// <summary>Tạo dịch vụ nền.</summary>
    /// <param name="scopes">Tạo scope DI cho mỗi lần dọn.</param>
    /// <param name="logger">Log kỹ thuật.</param>
    /// <param name="heartbeat">Báo nhịp cho màn hình SYS.</param>
    public ErrorLogCleanupBackgroundService(
        IServiceScopeFactory scopes, ILogger<ErrorLogCleanupBackgroundService> logger, IBackgroundHeartbeat heartbeat)
    {
        _scopes = scopes;
        _logger = logger;
        _heartbeat = heartbeat;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Báo nhịp ngay (chu kỳ tính cả 5 phút chờ) để SYS không báo dịch vụ ngừng trước lần dọn đầu.
        _heartbeat.Beat(ServiceName, (int)(StartDelay + Interval).TotalSeconds, "Chờ lần dọn đầu tiên.");
        try
        {
            await Task.Delay(StartDelay, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            string? note = null;
            try
            {
                await using var scope = _scopes.CreateAsyncScope();
                var (occurrences, groups) = await scope.ServiceProvider.GetRequiredService<IErrorLogService>().PurgeAsync(stoppingToken);
                if (occurrences > 0 || groups > 0)
                    _logger.LogInformation("Dọn nhật ký lỗi: {Occurrences} lần xảy ra, {Groups} nhóm.", occurrences, groups);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Lần dọn nhật ký lỗi bị lỗi.");
                note = $"Lần dọn lỗi: {ex.Message}";
            }

            _heartbeat.Beat(ServiceName, (int)Interval.TotalSeconds, note);
            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }
}
