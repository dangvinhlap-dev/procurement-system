using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using Proc.Application.Common;
using Proc.Application.Operations;
using Proc.Domain.Common;
using Proc.Domain.Operations;
using Proc.Domain.Organization;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.Operations;

/// <summary>Kết nối PostgreSQL dùng chung cho màn hình DBM (lát QT4).</summary>
internal static class DbMaintenanceConnections
{
    /// <summary>Chuỗi kết nối tài khoản ứng dụng (chủ các bảng – được VACUUM / REINDEX).</summary>
    /// <param name="db">DbContext.</param>
    /// <returns>Chuỗi kết nối.</returns>
    public static string App(ProcDbContext db) =>
        db.Database.GetConnectionString() ?? throw new InvalidOperationException("Chưa có chuỗi kết nối CSDL.");

    /// <summary>
    /// Chuỗi kết nối tài khoản bảo trì <c>proc_maint</c> trỏ vào <b>đúng CSDL ứng dụng</b> (chuỗi bảo trì có thể khai database khác – ví dụ
    /// <c>postgres</c> cho pg_basebackup); null khi chưa khai.
    /// </summary>
    /// <param name="db">DbContext.</param>
    /// <param name="backup">Cấu hình sao lưu (giữ chuỗi bảo trì).</param>
    /// <returns>Chuỗi kết nối hoặc null.</returns>
    public static string? Maintenance(ProcDbContext db, BackupOptions backup)
    {
        if (string.IsNullOrWhiteSpace(backup.MaintenanceConnectionString)) return null;
        var appDb = new NpgsqlConnectionStringBuilder(App(db)).Database;
        return new NpgsqlConnectionStringBuilder(backup.MaintenanceConnectionString) { Database = appDb }.ConnectionString;
    }

    /// <summary>Đọc cột thời gian có thể trống.</summary>
    public static DateTimeOffset? Time(NpgsqlDataReader r, int i) =>
        r.IsDBNull(i) ? null : new DateTimeOffset(DateTime.SpecifyKind(r.GetDateTime(i), DateTimeKind.Utc));

    /// <summary>Đọc cột chữ có thể trống.</summary>
    public static string? Text(NpgsqlDataReader r, int i) => r.IsDBNull(i) ? null : r.GetString(i);
}

/// <summary>
/// Màn hình DBM – bảo trì CSDL (lát QT4, design.md mục 21.4): số liệu bảng từ <c>pg_stat_user_tables</c>, phiên từ <c>pg_stat_activity</c> +
/// <c>pg_blocking_pids</c>, đặt VACUUM / REINDEX vào hàng chờ, ngắt phiên (Q6).
/// </summary>
public sealed class DbMaintenanceService : IDbMaintenanceService
{
    /// <summary>Số dòng lịch sử tối đa một lần tìm.</summary>
    public const int MaxRows = 1000;

    /// <summary>Ngưỡng cảnh báo dòng chết: từ 1.000 dòng và từ 20 % số dòng (autovacuum mặc định dọn khi vượt 20 % + 50 dòng – vượt mà chưa dọn là
    /// autovacuum không theo kịp).</summary>
    public const long DeadRowsWarn = 1000;

    /// <summary>Tỷ lệ dòng chết cảnh báo (%).</summary>
    public const decimal DeadPercentWarn = 20m;

    /// <summary>Câu lệnh chạy quá số phút này thì đèn vàng.</summary>
    public const int LongQueryMinutes = 5;

    /// <summary>Mở giao dịch mà không làm gì quá số phút này thì đèn vàng (giữ khóa, chặn VACUUM dọn dòng chết).</summary>
    public const int IdleInTransactionMinutes = 10;

    private readonly ProcDbContext _db;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;
    private readonly IOperationLog _operationLog;
    private readonly IDangerousActionGuard _guard;
    private readonly IDbMaintenanceQueueSignal _signal;
    private readonly BackupOptions _backup;
    private readonly DbMaintenanceOptions _options;
    private readonly ILogger<DbMaintenanceService> _logger;

    /// <summary>Tạo dịch vụ.</summary>
    /// <param name="db">CSDL.</param>
    /// <param name="user">Người đang đăng nhập.</param>
    /// <param name="clock">Đồng hồ.</param>
    /// <param name="operationLog">Nhật ký vận hành.</param>
    /// <param name="guard">Xác nhận việc nguy hiểm (Q6).</param>
    /// <param name="signal">Đánh thức bộ chạy nền.</param>
    /// <param name="backup">Cấu hình sao lưu (chuỗi kết nối bảo trì).</param>
    /// <param name="options">Cấu hình bảo trì.</param>
    /// <param name="logger">Log kỹ thuật.</param>
    public DbMaintenanceService(
        ProcDbContext db, ICurrentUser user, IClock clock, IOperationLog operationLog, IDangerousActionGuard guard, IDbMaintenanceQueueSignal signal,
        IOptions<BackupOptions> backup, IOptions<DbMaintenanceOptions> options, ILogger<DbMaintenanceService> logger)
    {
        _db = db;
        _user = user;
        _clock = clock;
        _operationLog = operationLog;
        _guard = guard;
        _signal = signal;
        _backup = backup.Value;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<DbTableStatsDto> GetTablesAsync(CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(DbMaintenanceConnections.App(_db));
        await connection.OpenAsync(cancellationToken);
        string database;
        long databaseBytes;
        await using (var cmd = new NpgsqlCommand("SELECT current_database(), pg_database_size(current_database())", connection))
        await using (var r = await cmd.ExecuteReaderAsync(cancellationToken))
        {
            await r.ReadAsync(cancellationToken);
            database = r.GetString(0);
            databaseBytes = r.GetInt64(1);
        }

        var tables = new List<DbTableStatDto>();
        const string sql = """
            SELECT s.schemaname, s.relname, pg_total_relation_size(s.relid), pg_table_size(s.relid), pg_indexes_size(s.relid),
                   s.n_live_tup, s.n_dead_tup, s.last_vacuum, s.last_autovacuum, s.last_analyze, s.last_autoanalyze
            FROM pg_stat_user_tables s
            ORDER BY pg_total_relation_size(s.relid) DESC, s.schemaname, s.relname
            """;
        await using (var cmd = new NpgsqlCommand(sql, connection))
        await using (var r = await cmd.ExecuteReaderAsync(cancellationToken))
        {
            while (await r.ReadAsync(cancellationToken))
            {
                var live = r.GetInt64(5);
                var dead = r.GetInt64(6);
                var percent = live + dead == 0 ? 0m : Math.Round(dead * 100m / (live + dead), 1);
                var lastVacuum = DbMaintenanceConnections.Time(r, 7);
                var lastAutoVacuum = DbMaintenanceConnections.Time(r, 8);
                var lastAnalyze = DbMaintenanceConnections.Time(r, 9);
                var lastAutoAnalyze = DbMaintenanceConnections.Time(r, 10);
                var (light, note) = TableLight(live, dead, percent, lastAnalyze ?? lastAutoAnalyze);
                tables.Add(new DbTableStatDto(r.GetString(0), r.GetString(1), r.GetInt64(2), r.GetInt64(3), r.GetInt64(4), live, dead, percent,
                    lastVacuum, lastAutoVacuum, lastAnalyze, lastAutoAnalyze, light, note));
            }
        }

        return new DbTableStatsDto(database, databaseBytes, _clock.UtcNow, tables);
    }

    /// <summary>
    /// Đèn của một bảng: vàng khi dòng chết ≥ <see cref="DeadRowsWarn"/> và ≥ <see cref="DeadPercentWarn"/> %, hoặc bảng có trên 1.000 dòng mà
    /// chưa ANALYZE lần nào (bộ lập kế hoạch truy vấn đoán sai số dòng).
    /// </summary>
    /// <example>8.000 dòng sống, 2.500 dòng chết → 23,8 % → vàng.</example>
    public static (string Light, string? Note) TableLight(long live, long dead, decimal deadPercent, DateTimeOffset? lastAnalyze)
    {
        if (dead >= DeadRowsWarn && deadPercent >= DeadPercentWarn)
            return ("Warning", $"Nhiều dòng chết ({deadPercent.ToString("0.#", CultureInfo.GetCultureInfo("vi-VN"))} %) – chạy VACUUM ANALYZE; " +
                "lặp lại thường xuyên thì xem phiên mở giao dịch lâu ở tab Phiên và khóa.");
        if (live > 1000 && lastAnalyze is null)
            return ("Warning", "Chưa ANALYZE lần nào – chạy VACUUM ANALYZE để cập nhật thống kê.");
        return ("Ok", null);
    }

    /// <inheritdoc />
    public async Task<DbSessionsDto> GetSessionsAsync(CancellationToken cancellationToken)
    {
        // Tài khoản bảo trì (pg_monitor) xem được câu lệnh của mọi tài khoản; lỗi thì đọc bằng tài khoản ứng dụng (câu lệnh của tài khoản khác bị
        // PostgreSQL ẩn) – màn hình vẫn có số liệu.
        var maintenance = DbMaintenanceConnections.Maintenance(_db, _backup);
        if (maintenance is not null)
        {
            try
            {
                return await ReadSessionsAsync(maintenance, true, cancellationToken);
            }
            catch (NpgsqlException ex)
            {
                _logger.LogWarning(ex, "Không đọc được phiên bằng tài khoản bảo trì – đọc bằng tài khoản ứng dụng.");
            }
        }

        return await ReadSessionsAsync(DbMaintenanceConnections.App(_db), false, cancellationToken);
    }

    private async Task<DbSessionsDto> ReadSessionsAsync(string connectionString, bool maintenance, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        // Chỉ phiên của CSDL ứng dụng, loại "client backend" (bỏ tiến trình nền của PostgreSQL: autovacuum, walsender của pg_basebackup…).
        const string sql = """
            SELECT a.pid, a.usename, a.application_name, host(a.client_addr), a.state, a.backend_start, a.xact_start, a.query_start,
                   a.wait_event_type, a.wait_event, pg_blocking_pids(a.pid), left(a.query, 2000), a.pid = pg_backend_pid(), now()
            FROM pg_stat_activity a
            WHERE a.datname = current_database() AND a.backend_type = 'client backend'
            ORDER BY a.backend_start
            """;
        var raw = new List<(int Pid, string? User, string? App, string? Client, string? State, DateTimeOffset? Start, DateTimeOffset? Xact,
            DateTimeOffset? Query, string? WaitType, string? Wait, int[] BlockedBy, string? Text, bool Self, DateTimeOffset Now)>();
        await using (var cmd = new NpgsqlCommand(sql, connection))
        await using (var r = await cmd.ExecuteReaderAsync(cancellationToken))
        {
            while (await r.ReadAsync(cancellationToken))
            {
                raw.Add((r.GetInt32(0), DbMaintenanceConnections.Text(r, 1), DbMaintenanceConnections.Text(r, 2), DbMaintenanceConnections.Text(r, 3),
                    DbMaintenanceConnections.Text(r, 4), DbMaintenanceConnections.Time(r, 5), DbMaintenanceConnections.Time(r, 6),
                    DbMaintenanceConnections.Time(r, 7), DbMaintenanceConnections.Text(r, 8), DbMaintenanceConnections.Text(r, 9),
                    r.IsDBNull(10) ? [] : r.GetFieldValue<int[]>(10), DbMaintenanceConnections.Text(r, 11), r.GetBoolean(12),
                    DbMaintenanceConnections.Time(r, 13)!.Value));
            }
        }

        var blocking = raw.SelectMany(s => s.BlockedBy).GroupBy(p => p).ToDictionary(g => g.Key, g => g.Count());
        var sessions = raw.Select(s =>
        {
            var idleInTx = s.State?.StartsWith("idle in transaction", StringComparison.Ordinal) == true;
            DateTimeOffset? since = s.State == "active" ? s.Query : idleInTx ? s.Xact : null;
            long? seconds = since is { } t ? Math.Max(0, (long)(s.Now - t).TotalSeconds) : null;
            var count = blocking.GetValueOrDefault(s.Pid);
            var notes = new List<string>();
            if (s.BlockedBy.Length > 0)
                notes.Add($"Đang chờ khóa của phiên {string.Join(", ", s.BlockedBy.Select(p => p.ToString(CultureInfo.InvariantCulture)))}");
            if (count > 0) notes.Add($"Đang chặn {count.ToString(CultureInfo.InvariantCulture)} phiên chờ khóa");
            if (idleInTx && seconds >= IdleInTransactionMinutes * 60)
                notes.Add($"Mở giao dịch mà không làm gì {(seconds / 60)?.ToString(CultureInfo.InvariantCulture)} phút – giữ khóa, VACUUM không dọn " +
                    "được dòng chết");
            if (s.State == "active" && seconds >= LongQueryMinutes * 60)
                notes.Add($"Câu lệnh chạy {(seconds / 60)?.ToString(CultureInfo.InvariantCulture)} phút");
            return new DbSessionDto(s.Pid, s.User, s.App, s.Client, s.State, StateLabel(s.State), s.Start, s.Xact, s.Query, seconds, s.WaitType,
                s.Wait, s.BlockedBy, count, s.Text, s.Self, notes.Count > 0 ? "Warning" : "Ok", notes.Count > 0 ? string.Join("; ", notes) + "." : null);
        })
            .OrderBy(s => s.Light == "Warning" ? 0 : 1)
            .ThenBy(s => s.BackendStart)
            .ToList();
        return new DbSessionsDto(_clock.UtcNow, maintenance, sessions.Count(s => s.BlockedBy.Count > 0), sessions);
    }

    /// <summary>Chữ tiếng Việt của trạng thái phiên PostgreSQL.</summary>
    /// <param name="state">Trạng thái (<c>pg_stat_activity.state</c>).</param>
    /// <returns>Chữ hiển thị.</returns>
    public static string StateLabel(string? state) => state switch
    {
        "active" => "Đang chạy lệnh",
        "idle" => "Rảnh",
        "idle in transaction" => "Chờ trong giao dịch",
        "idle in transaction (aborted)" => "Chờ trong giao dịch lỗi",
        "fastpath function call" => "Gọi hàm",
        "disabled" => "Không theo dõi",
        null => "Không rõ (thiếu quyền xem)",
        _ => state,
    };

    /// <inheritdoc />
    public async Task<MaintenanceRunSearchResult> SearchRunsAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        if (to < from) throw new BusinessRuleException("Đến ngày phải sau hoặc bằng từ ngày.");
        var start = VietnamDayStartUtc(from);
        var end = VietnamDayStartUtc(to.AddDays(1));
        var rows = await _db.MaintenanceRuns.AsNoTracking()
            .Where(r => r.QueuedAt >= start && r.QueuedAt < end)
            .OrderByDescending(r => r.QueuedAt)
            .Take(MaxRows + 1)
            .ToListAsync(cancellationToken);

        var settings = await DbMaintenanceScheduler.LoadSettingsAsync(_db, cancellationToken);
        var nowVn = TimeZoneInfo.ConvertTime(_clock.UtcNow, VietnamTime.Zone).DateTime;
        var today = DateOnly.FromDateTime(nowVn);
        var todayDone = await _db.MaintenanceRuns.AnyAsync(r => r.ScheduledDate == today, cancellationToken);
        DateTime? next = _options.ScheduleEnabled ? MaintenancePlanner.NextRun(nowVn, settings, todayDone) : null;
        var label = $"{IsoDayName(settings.Day)} {settings.Time.ToString("HH:mm", CultureInfo.InvariantCulture)}";
        return new MaintenanceRunSearchResult(rows.Take(MaxRows).Select(ToDto).ToList(), rows.Count > MaxRows, _options.ScheduleEnabled, label, next);
    }

    /// <summary>Tên thứ theo ISO.</summary>
    /// <param name="day">1 = Thứ hai … 7 = Chủ nhật.</param>
    /// <returns>Tên thứ.</returns>
    public static string IsoDayName(int day) =>
        day switch { 1 => "Thứ hai", 2 => "Thứ ba", 3 => "Thứ tư", 4 => "Thứ năm", 5 => "Thứ sáu", 6 => "Thứ bảy", _ => "Chủ nhật" };

    /// <inheritdoc />
    public async Task<MaintenanceRunDto> GetRunAsync(Guid id, CancellationToken cancellationToken)
    {
        var run = await _db.MaintenanceRuns.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy lần bảo trì.");
        return ToDto(run);
    }

    /// <inheritdoc />
    public async Task<MaintenanceRunDto> QueueAsync(QueueMaintenanceRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Kind is not (MaintenanceKind.Vacuum or MaintenanceKind.Reindex))
            throw new BusinessRuleException("Chỉ VACUUM ANALYZE, REINDEX đặt vào hàng chờ được.");

        string? target = null;
        if (!string.IsNullOrWhiteSpace(request.Table))
        {
            // Bảng phải có thật trong CSDL; tên ghi lại đúng như danh mục PostgreSQL (không tin chữ client).
            target = await FindTableAsync(DbMaintenanceConnections.App(_db), request.Schema ?? "public", request.Table.Trim(), cancellationToken)
                ?? throw new BusinessRuleException($"Không có bảng {request.Schema ?? "public"}.{request.Table.Trim()} trong CSDL.");
        }

        if (await _db.MaintenanceRuns.AnyAsync(r => r.Kind == request.Kind && r.Target == target
                && (r.Status == MaintenanceRunStatus.Queued || r.Status == MaintenanceRunStatus.Running), cancellationToken))
            throw new BusinessRuleException(
                $"Đang có lần {MaintenanceLabels.Kind(request.Kind)} {target ?? "cả CSDL"} chờ hoặc đang chạy – đợi lần đó xong.");

        var run = MaintenanceRun.Queue(request.Kind, MaintenanceTrigger.Manual, target, null, null, null, _user.UserName, _clock.UtcNow);
        _db.MaintenanceRuns.Add(run);
        await _db.SaveChangesAsync(cancellationToken);
        await _operationLog.WriteAsync(new OperationLogEntry(OperationKinds.MaintenanceQueued,
            $"{MaintenanceLabels.Kind(run.Kind)} {run.Target ?? "cả CSDL"}", null, OperationResult.Info, "Đặt trên màn hình DBM."), cancellationToken);
        _signal.Notify();
        return ToDto(run);
    }

    /// <summary>Tên "schema.bảng" theo danh mục PostgreSQL; null khi không có.</summary>
    /// <param name="connectionString">Chuỗi kết nối.</param>
    /// <param name="schema">Schema.</param>
    /// <param name="table">Tên bảng.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Tên đầy đủ hoặc null.</returns>
    internal static async Task<string?> FindTableAsync(string connectionString, string schema, string table, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(
            "SELECT schemaname || '.' || relname FROM pg_stat_user_tables WHERE schemaname = @s AND relname = @t", connection);
        cmd.Parameters.AddWithValue("s", schema);
        cmd.Parameters.AddWithValue("t", table);
        return await cmd.ExecuteScalarAsync(cancellationToken) as string;
    }

    /// <inheritdoc />
    public async Task<MaintenanceRunDto> TerminateAsync(TerminateSessionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        // Kiểm tra pid + lý do trước (domain), rồi mới hỏi mật khẩu – lỗi nhập thiếu không bị ghi là "sai mật khẩu".
        var run = MaintenanceRun.Queue(MaintenanceKind.TerminateSession, MaintenanceTrigger.Manual, $"pid {request.Pid}", request.Pid,
            request.Reason, null, _user.UserName, _clock.UtcNow);
        await _guard.VerifyAsync("Ngắt phiên CSDL", request.Password, request.Confirmation, DangerConfirmations.TerminateSession, cancellationToken);

        var sessions = await GetSessionsAsync(cancellationToken);
        var session = sessions.Sessions.FirstOrDefault(s => s.Pid == request.Pid)
            ?? throw new BusinessRuleException($"Không còn phiên {request.Pid} trong CSDL ứng dụng – làm mới tab Phiên và khóa.");
        if (session.IsSelf) throw new BusinessRuleException("Không ngắt được chính kết nối đang đọc số liệu.");

        var described = $"pid {session.Pid} – {session.UserName}@{session.ClientAddress ?? "nội bộ"} ({session.ApplicationName})";
        run = MaintenanceRun.Queue(MaintenanceKind.TerminateSession, MaintenanceTrigger.Manual, described, request.Pid, request.Reason, null,
            _user.UserName, _clock.UtcNow);
        run.Start(null, _clock.UtcNow);
        try
        {
            var note = await TerminateBackendAsync(request.Pid, cancellationToken);
            run.Complete(null, note, _clock.UtcNow);
        }
        catch (BusinessRuleException ex)
        {
            run.Fail(ex.Message, _clock.UtcNow);
        }

        _db.MaintenanceRuns.Add(run);
        await _db.SaveChangesAsync(cancellationToken);
        await _operationLog.WriteAsync(new OperationLogEntry(OperationKinds.SessionTerminated, described, run.Reason,
            run.Status == MaintenanceRunStatus.Succeeded ? OperationResult.Succeeded : OperationResult.Failed,
            run.ErrorMessage ?? $"{run.Note} Câu lệnh: {session.Query}"), cancellationToken);
        return ToDto(run);
    }

    /// <summary>
    /// <c>pg_terminate_backend(pid, 5000)</c>: chờ tối đa 5 giây cho phiên dừng hẳn. Ưu tiên tài khoản bảo trì (cần vai trò <c>pg_signal_backend</c>
    /// để ngắt phiên của tài khoản khác); không có thì tài khoản ứng dụng (chỉ ngắt được phiên của chính nó).
    /// </summary>
    private async Task<string> TerminateBackendAsync(int pid, CancellationToken cancellationToken)
    {
        var maintenance = DbMaintenanceConnections.Maintenance(_db, _backup);
        var connectionString = maintenance ?? DbMaintenanceConnections.App(_db);
        var account = new NpgsqlConnectionStringBuilder(connectionString).Username;
        try
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var cmd = new NpgsqlCommand("SELECT pg_terminate_backend(@p, 5000)", connection);
            cmd.Parameters.AddWithValue("p", pid);
            return await cmd.ExecuteScalarAsync(cancellationToken) is true
                ? $"Đã ngắt phiên {pid} (bằng tài khoản {account})."
                : throw new BusinessRuleException($"Phiên {pid} không dừng sau 5 giây (hoặc đã đóng trước đó).");
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.InsufficientPrivilege)
        {
            throw new BusinessRuleException(
                $"Tài khoản {account} không được ngắt phiên này: {ex.MessageText}. Khai ConnectionStrings:Maintenance (proc_maint có vai trò " +
                "pg_signal_backend – qt-enable-wal.ps1) để ngắt phiên của tài khoản khác.");
        }
    }

    /// <summary>Đổi lần bảo trì sang DTO.</summary>
    /// <param name="r">Lần bảo trì.</param>
    /// <returns>DTO.</returns>
    public static MaintenanceRunDto ToDto(MaintenanceRun r)
    {
        ArgumentNullException.ThrowIfNull(r);
        var (label, light) = MaintenanceLabels.Status(r.Status);
        long? duration = r.StartedAt is { } s && r.FinishedAt is { } f ? (long)(f - s).TotalSeconds : null;
        return new MaintenanceRunDto(r.Id, r.Kind.ToString(), MaintenanceLabels.Kind(r.Kind), r.Trigger.ToString(), r.Status.ToString(), label, light,
            r.Target, r.SessionPid, r.Reason, r.ScheduledDate, r.RequestedByName, r.QueuedAt, r.StartedAt, r.FinishedAt, duration, r.SizeBeforeBytes,
            r.SizeAfterBytes, r.Note, r.ErrorMessage);
    }

    private static DateTimeOffset VietnamDayStartUtc(DateOnly day)
    {
        var local = day.ToDateTime(TimeOnly.MinValue);
        return new DateTimeOffset(local, VietnamTime.Zone.GetUtcOffset(local)).ToUniversalTime();
    }
}

/// <summary>
/// Bộ chạy hàng chờ VACUUM / REINDEX (lát QT4). Chạy bằng tài khoản ứng dụng – chủ các bảng (VACUUM, REINDEX cần là chủ bảng hoặc quyền
/// MAINTAIN). Lệnh chạy ngoài giao dịch (Npgsql tự commit từng lệnh – REINDEX CONCURRENTLY không chạy được trong giao dịch).
/// </summary>
public sealed class DbMaintenanceExecutor : IDbMaintenanceExecutor
{
    /// <summary>Khóa <c>pg_advisory_lock</c> riêng của bộ chạy bảo trì (chữ "PROCDBM" dạng số) – khác khóa sao lưu, hai việc chạy song song được.</summary>
    public const long AdvisoryLockKey = 0x50524F4344424D;

    private readonly IServiceScopeFactory _scopes;
    private readonly IClock _clock;
    private readonly DbMaintenanceOptions _options;
    private readonly ILogger<DbMaintenanceExecutor> _logger;

    /// <summary>Tạo bộ chạy.</summary>
    /// <param name="scopes">Tạo scope DI cho từng lần.</param>
    /// <param name="clock">Đồng hồ.</param>
    /// <param name="options">Cấu hình bảo trì.</param>
    /// <param name="logger">Log kỹ thuật.</param>
    public DbMaintenanceExecutor(
        IServiceScopeFactory scopes, IClock clock, IOptions<DbMaintenanceOptions> options, ILogger<DbMaintenanceExecutor> logger)
    {
        _scopes = scopes;
        _clock = clock;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<int> ProcessQueueAsync(CancellationToken cancellationToken)
    {
        await using var lockScope = _scopes.CreateAsyncScope();
        await using var lockConnection = await TryLockAsync(lockScope.ServiceProvider.GetRequiredService<ProcDbContext>(), cancellationToken);
        if (lockConnection is null) return 0;

        var count = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            await using var scope = _scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ProcDbContext>();
            var run = await db.MaintenanceRuns.Where(r => r.Status == MaintenanceRunStatus.Queued).OrderBy(r => r.QueuedAt)
                .FirstOrDefaultAsync(cancellationToken);
            if (run is null) break;

            var connectionString = DbMaintenanceConnections.App(db);
            try
            {
                await ExecuteAsync(run, db, connectionString, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                run.Fail("Ứng dụng dừng khi đang bảo trì – chạy lại.", _clock.UtcNow);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Bảo trì {Kind} {Target} lỗi.", run.Kind, run.Target);
                run.Fail(ex switch
                {
                    PostgresException pg => pg.MessageText,
                    BusinessRuleException or NpgsqlException or TimeoutException => ex.Message,
                    _ => $"{ex.GetType().Name}: {ex.Message}",
                }, _clock.UtcNow);
            }

            await db.SaveChangesAsync(CancellationToken.None);
            await WriteLogAsync(scope.ServiceProvider.GetRequiredService<IOperationLog>(), run);
            count++;
        }

        return count;
    }

    /// <inheritdoc />
    public async Task<int> FailInterruptedAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ProcDbContext>();
        await using var lockConnection = await TryLockAsync(db, cancellationToken);
        if (lockConnection is null) return 0;
        var stuck = await db.MaintenanceRuns
            .Where(r => r.Status == MaintenanceRunStatus.Running && r.Kind != MaintenanceKind.TerminateSession)
            .ToListAsync(cancellationToken);
        foreach (var run in stuck) run.Fail("Ứng dụng khởi động lại khi đang bảo trì – chạy lại.", _clock.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        return stuck.Count;
    }

    /// <summary>Khóa theo phiên trên kết nối <b>không dùng pool</b> (lý do như bộ chạy sao lưu – <see cref="BackupExecutor"/>).</summary>
    private static async Task<NpgsqlConnection?> TryLockAsync(ProcDbContext db, CancellationToken cancellationToken)
    {
        var cs = new NpgsqlConnectionStringBuilder(DbMaintenanceConnections.App(db)) { Pooling = false }.ConnectionString;
        var connection = new NpgsqlConnection(cs);
        await connection.OpenAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand("SELECT pg_try_advisory_lock(@k)", connection);
        cmd.Parameters.AddWithValue("k", AdvisoryLockKey);
        if (await cmd.ExecuteScalarAsync(cancellationToken) is true) return connection;
        await connection.DisposeAsync();
        return null;
    }

    private async Task ExecuteAsync(MaintenanceRun run, ProcDbContext db, string connectionString, CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        // Cảnh báo của PostgreSQL (ví dụ "skipping … only table or database owner can vacuum it") – đếm để ghi chú.
        var skipped = 0;
        connection.Notice += (_, e) =>
        {
            if (e.Notice.Severity is "WARNING") Interlocked.Increment(ref skipped);
        };

        string? quoted = null;
        long? deadBefore = null;
        if (run.Target is { } target)
        {
            var dot = target.IndexOf('.', StringComparison.Ordinal);
            await using var find = new NpgsqlCommand(
                "SELECT quote_ident(schemaname) || '.' || quote_ident(relname), n_dead_tup FROM pg_stat_user_tables WHERE schemaname = @s AND relname = @t",
                connection);
            find.Parameters.AddWithValue("s", dot > 0 ? target[..dot] : "public");
            find.Parameters.AddWithValue("t", dot > 0 ? target[(dot + 1)..] : target);
            await using (var r = await find.ExecuteReaderAsync(ct))
            {
                if (await r.ReadAsync(ct))
                {
                    quoted = r.GetString(0);
                    deadBefore = r.GetInt64(1);
                }
            }

            if (quoted is null) throw new BusinessRuleException($"Bảng {target} không còn trong CSDL.");
        }

        var sizeSql = quoted is null ? "SELECT pg_database_size(current_database())" : "SELECT pg_total_relation_size(@r::regclass)";
        run.Start(await ScalarLongAsync(connection, sizeSql, quoted, ct), _clock.UtcNow);
        await db.SaveChangesAsync(ct);

        // Tên bảng lấy từ quote_ident của PostgreSQL (đã kiểm tra có trong danh mục) – không ghép chữ client vào lệnh.
        var sql = run.Kind switch
        {
            MaintenanceKind.Vacuum => quoted is null ? "VACUUM (ANALYZE)" : $"VACUUM (ANALYZE) {quoted}",
            MaintenanceKind.Reindex => $"REINDEX TABLE CONCURRENTLY {quoted}",
            _ => throw new BusinessRuleException("Ngắt phiên không chạy qua hàng chờ."),
        };
        await using (var cmd = new NpgsqlCommand(sql, connection) { CommandTimeout = _options.CommandTimeoutMinutes * 60 })
            await cmd.ExecuteNonQueryAsync(ct);

        var after = await ScalarLongAsync(connection, sizeSql, quoted, ct);
        var notes = new List<string>();
        if (deadBefore is { } d) notes.Add($"Dòng chết trước khi chạy: {d.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"))}.");
        if (skipped > 0)
            notes.Add($"PostgreSQL bỏ qua {skipped.ToString(CultureInfo.InvariantCulture)} bảng tài khoản ứng dụng không phải chủ (bảng hệ thống – " +
                "autovacuum vẫn dọn).");
        run.Complete(after, notes.Count > 0 ? string.Join(" ", notes) : null, _clock.UtcNow);
    }

    private static async Task<long?> ScalarLongAsync(NpgsqlConnection connection, string sql, string? relation, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand(sql, connection);
        if (relation is not null) cmd.Parameters.AddWithValue("r", relation);
        return await cmd.ExecuteScalarAsync(ct) is long v ? v : null;
    }

    private async Task WriteLogAsync(IOperationLog log, MaintenanceRun run)
    {
        try
        {
            var (label, _) = MaintenanceLabels.Status(run.Status);
            await log.WriteAsync(new OperationLogEntry(OperationKinds.MaintenanceFinished, $"{MaintenanceLabels.Kind(run.Kind)} {run.Target ?? "cả CSDL"}",
                run.Trigger == MaintenanceTrigger.Schedule ? "Theo lịch" : $"Chạy tay – {run.RequestedByName}",
                run.Status == MaintenanceRunStatus.Succeeded ? OperationResult.Succeeded : OperationResult.Failed,
                run.ErrorMessage ?? $"{label} · {run.SizeBeforeBytes?.ToString(CultureInfo.InvariantCulture)} → " +
                $"{run.SizeAfterBytes?.ToString(CultureInfo.InvariantCulture)} byte · {run.Note}"), CancellationToken.None);
        }
        catch (Exception ex)
        {
            // Nhật ký vận hành đã tự ghi ra file khi CSDL lỗi; lỗi ở đây không làm hỏng kết quả bảo trì.
            _logger.LogWarning(ex, "Không ghi được nhật ký vận hành cho lần bảo trì {Id}.", run.Id);
        }
    }
}

/// <summary>Tín hiệu đánh thức bộ chạy bảo trì (singleton).</summary>
public sealed class DbMaintenanceQueueSignal : IDbMaintenanceQueueSignal
{
    private readonly SemaphoreSlim _semaphore = new(0, 1);

    /// <inheritdoc />
    public void Notify()
    {
        try
        {
            _semaphore.Release();
        }
        catch (SemaphoreFullException)
        {
            // Đã có tín hiệu chờ – bộ chạy luôn chạy hết hàng chờ mỗi lần thức.
        }
    }

    /// <inheritdoc />
    public Task WaitAsync(TimeSpan timeout, CancellationToken cancellationToken) => _semaphore.WaitAsync(timeout, cancellationToken);
}

/// <summary>Lịch VACUUM (ANALYZE) cả CSDL hằng tuần (lát QT4-2, tham số DB_MAINT_DAY / DB_MAINT_TIME của tổng công ty).</summary>
public sealed class DbMaintenanceScheduler : IDbMaintenanceScheduler
{
    private readonly ProcDbContext _db;
    private readonly IClock _clock;
    private readonly IDbMaintenanceQueueSignal _signal;
    private readonly DbMaintenanceOptions _options;

    /// <summary>Tạo bộ lập lịch.</summary>
    /// <param name="db">CSDL.</param>
    /// <param name="clock">Đồng hồ.</param>
    /// <param name="signal">Đánh thức bộ chạy.</param>
    /// <param name="options">Cấu hình bảo trì.</param>
    public DbMaintenanceScheduler(ProcDbContext db, IClock clock, IDbMaintenanceQueueSignal signal, IOptions<DbMaintenanceOptions> options)
    {
        _db = db;
        _clock = clock;
        _signal = signal;
        _options = options.Value;
    }

    /// <summary>Đọc tham số lịch của tổng công ty (chưa có dòng tham số thì mặc định Chủ nhật 03:00).</summary>
    /// <param name="db">CSDL.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Tham số.</returns>
    public static async Task<MaintenanceScheduleSettings> LoadSettingsAsync(ProcDbContext db, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);
        var p = await db.CompanyParameters.IgnoreQueryFilters()
            .Where(x => db.Companies.IgnoreQueryFilters().Any(c => c.Id == x.CompanyId && c.IsHeadOffice))
            .Select(x => new MaintenanceScheduleSettings(x.DbMaintenanceDay, x.DbMaintenanceTime))
            .FirstOrDefaultAsync(cancellationToken);
        return p ?? new MaintenanceScheduleSettings(CompanyParameter.DefaultDbMaintenanceDay, CompanyParameter.DefaultDbMaintenanceTime);
    }

    /// <inheritdoc />
    public async Task<int> PlanAsync(CancellationToken cancellationToken)
    {
        if (!_options.ScheduleEnabled) return 0;
        var settings = await LoadSettingsAsync(_db, cancellationToken);
        if (MaintenancePlanner.DueSlot(TimeZoneInfo.ConvertTime(_clock.UtcNow, VietnamTime.Zone).DateTime, settings) is not { } slot) return 0;
        if (await _db.MaintenanceRuns.AnyAsync(r => r.ScheduledDate == slot, cancellationToken)) return 0;

        _db.MaintenanceRuns.Add(MaintenanceRun.Queue(MaintenanceKind.Vacuum, MaintenanceTrigger.Schedule, null, null, null, slot, null, _clock.UtcNow));
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Tiến trình khác vừa đặt cùng lượt (khóa duy nhất Kind + ScheduledDate).
            _db.ChangeTracker.Clear();
            return 0;
        }

        _signal.Notify();
        return 1;
    }
}

/// <summary>
/// Dịch vụ nền bảo trì CSDL (lát QT4): khởi động thì ghi lỗi các lần "Đang chạy" của tiến trình cũ; mỗi vòng (mỗi phút hoặc khi có tín hiệu):
/// đặt lượt lịch tuần khi tới giờ → chạy hàng chờ.
/// </summary>
public sealed class DbMaintenanceBackgroundService : BackgroundService
{
    /// <summary>Tên dịch vụ trên màn hình SYS.</summary>
    public const string ServiceName = "Bảo trì CSDL";

    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    private readonly IDbMaintenanceExecutor _executor;
    private readonly IServiceScopeFactory _scopes;
    private readonly IDbMaintenanceQueueSignal _signal;
    private readonly IBackgroundHeartbeat _heartbeat;
    private readonly DbMaintenanceOptions _options;
    private readonly ILogger<DbMaintenanceBackgroundService> _logger;

    /// <summary>Tạo dịch vụ nền.</summary>
    /// <param name="executor">Bộ chạy.</param>
    /// <param name="scopes">Tạo scope DI cho lịch.</param>
    /// <param name="signal">Tín hiệu có việc mới.</param>
    /// <param name="heartbeat">Báo nhịp cho SYS.</param>
    /// <param name="options">Cấu hình bảo trì.</param>
    /// <param name="logger">Log kỹ thuật.</param>
    public DbMaintenanceBackgroundService(
        IDbMaintenanceExecutor executor, IServiceScopeFactory scopes, IDbMaintenanceQueueSignal signal, IBackgroundHeartbeat heartbeat,
        IOptions<DbMaintenanceOptions> options, ILogger<DbMaintenanceBackgroundService> logger)
    {
        _executor = executor;
        _scopes = scopes;
        _signal = signal;
        _heartbeat = heartbeat;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.WorkerEnabled) return;
        // Nhịp theo thời gian tối đa một lệnh: VACUUM cả CSDL lớn chạy lâu – SYS không báo dịch vụ ngừng trong lúc đó.
        var beatSeconds = Math.Max(60, _options.CommandTimeoutMinutes * 60);
        _heartbeat.Beat(ServiceName, beatSeconds, "Khởi động.");
        try
        {
            var stuck = await _executor.FailInterruptedAsync(stoppingToken);
            if (stuck > 0) _logger.LogWarning("Ghi lỗi {Count} lần bảo trì dừng giữa chừng do ứng dụng khởi động lại.", stuck);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Không kiểm tra được lần bảo trì dừng giữa chừng.");
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            string? note = null;
            try
            {
                await using (var scope = _scopes.CreateAsyncScope())
                    await scope.ServiceProvider.GetRequiredService<IDbMaintenanceScheduler>().PlanAsync(stoppingToken);
                var done = await _executor.ProcessQueueAsync(stoppingToken);
                if (done > 0) note = $"Vừa chạy {done.ToString(CultureInfo.InvariantCulture)} lần bảo trì.";
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Bộ chạy bảo trì CSDL lỗi.");
                note = $"Lỗi: {ex.Message}";
            }

            _heartbeat.Beat(ServiceName, beatSeconds, note);
            try
            {
                await _signal.WaitAsync(Interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }
}
