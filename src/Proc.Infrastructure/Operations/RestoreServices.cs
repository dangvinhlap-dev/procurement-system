using System.Globalization;
using System.Text.Json;
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
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.Operations;

/// <summary>
/// Phục hồi – màn hình RST (lát QT6, design.md mục 21.4, N1, N2). Khôi phục <b>không ghi đè</b> CSDL đang chạy: pg_dump vào CSDL mới (ứng dụng
/// tự làm ở <see cref="RestoreExecutor"/>), mốc thời gian ra máy chủ CSDL tạm (bộ thực thi); kiểm tra bằng bảng so sánh số dòng; hoán đổi giao bộ
/// thực thi của chính máy này (ứng dụng không tự dừng mình, không tự dừng PostgreSQL – N1). Mỗi lúc chỉ một lần phục hồi chưa kết thúc.
/// </summary>
public sealed class RestoreService : IRestoreService
{
    /// <summary>Số dòng tối đa một lần tìm.</summary>
    public const int MaxRows = 500;

    private static readonly RestoreRunStatus[] OpenStatuses =
        [RestoreRunStatus.Queued, RestoreRunStatus.Restoring, RestoreRunStatus.Ready, RestoreRunStatus.Swapping];

    private readonly ProcDbContext _db;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;
    private readonly IOperationLog _operationLog;
    private readonly IRestoreQueueSignal _signal;
    private readonly IDangerousActionGuard _guard;
    private readonly BackupOptions _backup;
    private readonly RestoreOptions _options;
    private readonly AppEnvironmentOptions _environment;

    /// <summary>Tạo dịch vụ.</summary>
    /// <param name="db">CSDL.</param>
    /// <param name="user">Người đang đăng nhập.</param>
    /// <param name="clock">Đồng hồ.</param>
    /// <param name="operationLog">Nhật ký vận hành.</param>
    /// <param name="signal">Đánh thức bộ chạy khôi phục pg_dump.</param>
    /// <param name="guard">Xác nhận Q6 khi hoán đổi.</param>
    /// <param name="backup">Cấu hình sao lưu (thư mục, tài khoản bảo trì, thư mục WAL).</param>
    /// <param name="options">Cấu hình phục hồi.</param>
    /// <param name="environment">Môi trường máy này (chọn bộ thực thi).</param>
    public RestoreService(
        ProcDbContext db, ICurrentUser user, IClock clock, IOperationLog operationLog, IRestoreQueueSignal signal, IDangerousActionGuard guard,
        IOptions<BackupOptions> backup, IOptions<RestoreOptions> options, IOptions<AppEnvironmentOptions> environment)
    {
        _db = db;
        _user = user;
        _clock = clock;
        _operationLog = operationLog;
        _signal = signal;
        _guard = guard;
        _backup = backup.Value;
        _options = options.Value;
        _environment = environment.Value;
    }

    private string MainConnectionString => _db.Database.GetConnectionString()!;

    private string LiveDatabase => new NpgsqlConnectionStringBuilder(MainConnectionString).Database
        ?? throw new BusinessRuleException("Chuỗi kết nối chính không có tên database.");

    /// <inheritdoc />
    public async Task<RestoreOverviewDto> GetOverviewAsync(CancellationToken cancellationToken)
    {
        var dumps = await _db.BackupRuns.AsNoTracking()
            .Where(r => r.Kind == BackupKind.Dump && r.Status == BackupRunStatus.Succeeded && r.Verified != false && r.PurgedAt == null)
            .OrderByDescending(r => r.FinishedAt)
            .Take(100)
            .ToListAsync(cancellationToken);
        var root = _backup.RootPath;
        var sources = dumps.Select(d => new RestoreDumpSourceDto(d.Id, d.FinishedAt, d.RelativePath, d.SizeBytes,
            root is not null && d.RelativePath is not null && File.Exists(Path.Combine(root, d.RelativePath)))).ToList();
        var (ready, note) = await AgentStateAsync(cancellationToken);
        IReadOnlyList<string> others;
        try
        {
            others = await RestoreDatabases.OtherDatabasesAsync(MainConnectionString, LiveDatabase, cancellationToken);
        }
        catch (NpgsqlException)
        {
            others = [];
        }

        return new RestoreOverviewDto(LiveDatabase, sources, !string.IsNullOrWhiteSpace(_backup.MaintenanceConnectionString),
            !string.IsNullOrWhiteSpace(_backup.WalArchivePath), _options.TempPort, _environment.Name, ready, note, others);
    }

    /// <inheritdoc />
    public async Task<RestoreRunSearchResult> SearchAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        if (to < from) throw new BusinessRuleException("Đến ngày phải sau hoặc bằng từ ngày.");
        await SyncWithAgentAsync(cancellationToken);
        var start = VietnamDayStartUtc(from);
        var end = VietnamDayStartUtc(to.AddDays(1));
        var rows = await _db.RestoreRuns.AsNoTracking()
            .Where(r => r.QueuedAt >= start && r.QueuedAt < end)
            .OrderByDescending(r => r.QueuedAt)
            .Take(MaxRows + 1)
            .ToListAsync(cancellationToken);
        return new RestoreRunSearchResult(rows.Take(MaxRows).Select(r => ToDto(r, false)).ToList(), rows.Count > MaxRows);
    }

    /// <inheritdoc />
    public async Task<RestoreRunDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        await SyncWithAgentAsync(cancellationToken);
        return ToDto(await FindAsync(id, cancellationToken), true);
    }

    /// <inheritdoc />
    public async Task<RestorePlan> PlanAsync(DateTimeOffset targetTime, CancellationToken cancellationToken)
    {
        var backups = await _db.BackupRuns.AsNoTracking()
            .Where(r => (r.Kind == BackupKind.Full || r.Kind == BackupKind.Diff)
                && r.Status == BackupRunStatus.Succeeded && r.Verified != false && r.PurgedAt == null)
            .ToListAsync(cancellationToken);
        var candidates = backups.Where(b => b.FinishedAt is not null)
            .Select(b => new RestoreBackupCandidate(b.Id, b.Kind, b.FinishedAt!.Value, b.StartWalFile, b.BaseFullRunId, b.RelativePath));

        var wal = new List<WalArchiveFile>();
        var problems = new List<string>();
        if (string.IsNullOrWhiteSpace(_backup.WalArchivePath))
            problems.Add("Chưa khai thư mục WAL (Backup:WalArchivePath) – không khôi phục được về mốc thời gian.");
        else if (!Directory.Exists(_backup.WalArchivePath))
            problems.Add($"Không đọc được thư mục WAL {_backup.WalArchivePath}.");
        else
            wal.AddRange(new DirectoryInfo(_backup.WalArchivePath).EnumerateFiles()
                .Select(f => new WalArchiveFile(f.Name, new DateTimeOffset(f.LastWriteTimeUtc, TimeSpan.Zero))));

        var plan = RestorePlanner.Plan(targetTime.ToUniversalTime(), _clock.UtcNow, candidates, wal, VietnamTime.Zone);
        var warnings = plan.Warnings.ToList();
        // Bản Full là cả thư mục dữ liệu PostgreSQL: các CSDL khác trên cùng máy chủ cũng quay về mốc khi hoán đổi.
        try
        {
            var others = await RestoreDatabases.OtherDatabasesAsync(MainConnectionString, LiveDatabase, cancellationToken);
            if (others.Count > 0)
                warnings.Add($"Máy chủ PostgreSQL này còn {others.Count.ToString(CultureInfo.InvariantCulture)} CSDL khác ({string.Join(", ", others)}) – " +
                    "hoán đổi về mốc thời gian đưa CẢ các CSDL đó về mốc (mất dữ liệu nhập sau mốc của các phần mềm khác). Chỉ cần lấy lại dữ liệu " +
                    "phần mềm này thì dùng cách khôi phục từ pg_dump.");
        }
        catch (NpgsqlException)
        {
            // Không đọc được danh sách CSDL – không chặn kế hoạch.
        }

        return plan with
        {
            Feasible = plan.Feasible && problems.Count == 0,
            Problems = problems.Concat(plan.Problems).ToList(),
            Warnings = warnings,
        };
    }

    /// <inheritdoc />
    public async Task<RestoreRunDto> QueueAsync(QueueRestoreRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await SyncWithAgentAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(_backup.RootPath))
            throw new BusinessRuleException("Chưa khai thư mục sao lưu (Backup:RootPath) trên máy chủ – xem docs/deploy-windows.md mục 4b.");
        if (await _db.RestoreRuns.AnyAsync(r => OpenStatuses.Contains(r.Status), cancellationToken))
            throw new BusinessRuleException("Đang có lần phục hồi chưa kết thúc – hoán đổi hoặc bỏ bản khôi phục đó trước.");
        var live = LiveDatabase;
        var now = _clock.UtcNow;
        var vnNow = TimeZoneInfo.ConvertTime(now, VietnamTime.Zone).DateTime;

        RestoreRun run;
        string detail;
        if (request.Kind == RestoreKind.Dump)
        {
            var dump = await _db.BackupRuns.AsNoTracking().FirstOrDefaultAsync(r => r.Id == request.DumpRunId, cancellationToken);
            if (dump is not { Kind: BackupKind.Dump, IsUsable: true } || dump.RelativePath is null)
                throw new BusinessRuleException("Chọn một bản pg_dump đã xong, kiểm tra đạt, chưa bị dọn.");
            if (!File.Exists(Path.Combine(_backup.RootPath, dump.RelativePath)))
                throw new BusinessRuleException($"Không thấy file {dump.RelativePath} trong thư mục sao lưu.");
            var name = await FreeNameAsync(live, vnNow, cancellationToken);
            run = RestoreRun.QueueDump(dump.Id, live, name, request.Reason, _user.UserName, now);
            _db.RestoreRuns.Add(run);
            detail = $"Khôi phục {dump.RelativePath} vào CSDL mới {name}.";
        }
        else if (request.Kind == RestoreKind.PointInTime)
        {
            if (request.TargetTime is not { } target) throw new BusinessRuleException("Chọn mốc thời gian cần về.");
            var env = await AgentEnvironmentAsync(cancellationToken);
            var plan = await PlanAsync(target, cancellationToken);
            var stamp = vnNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            var rel = Path.Combine("restore", stamp);
            run = RestoreRun.QueuePointInTime(plan, JsonSerializer.Serialize(plan, RestoreDatabases.Json), live, rel, _options.TempPort,
                request.Reason, _user.UserName, now);
            var parameters = new
            {
                restoreRunId = run.Id,
                fullPath = Path.Combine(_backup.RootPath, plan.FullPath!),
                diffPath = plan.DiffPath is null ? null : Path.Combine(_backup.RootPath, plan.DiffPath),
                walArchivePath = _backup.WalArchivePath,
                targetTime = plan.TargetTime.ToString("yyyy-MM-dd HH:mm:ss+00", CultureInfo.InvariantCulture),
                workDir = Path.Combine(_backup.RootPath, rel),
                port = _options.TempPort,
            };
            var cmd = AgentCommand.Create(env.Id, AgentCommandKind.RestorePointInTime, JsonSerializer.Serialize(parameters, RestoreDatabases.Json),
                run.Reason, _user.UserName, now);
            run.SetAgentCommand(cmd.Id);
            _db.RestoreRuns.Add(run);
            _db.AgentCommands.Add(cmd);
            detail = $"Về mốc {Vn(plan.TargetTime)}: Full {plan.FullPath}{(plan.DiffPath is null ? "" : " + Diff " + plan.DiffPath)}, WAL {plan.FirstWal} → " +
                $"{plan.LastWal}; máy chủ tạm cổng {_options.TempPort.ToString(CultureInfo.InvariantCulture)}; lệnh {cmd.Id} giao bộ thực thi {env.Code}.";
        }
        else
        {
            throw new BusinessRuleException("Cách phục hồi không hợp lệ.");
        }

        await _db.SaveChangesAsync(cancellationToken);
        await _operationLog.WriteAsync(new OperationLogEntry(OperationKinds.RestoreQueued, RestoreLabels.Kind(run.Kind), run.Reason,
            OperationResult.Info, detail), cancellationToken);
        if (run.Kind == RestoreKind.Dump) _signal.Notify();
        return ToDto(run, true);
    }

    /// <inheritdoc />
    public async Task<RestoreRunDto> CompareAsync(Guid id, CancellationToken cancellationToken)
    {
        var run = await FindAsync(id, cancellationToken);
        if (run.Status != RestoreRunStatus.Ready) throw new BusinessRuleException("Chỉ so sánh lại khi bản khôi phục đang chờ kiểm tra.");
        var (cmp, canSwap, note) = await RestoreDatabases.CompareAsync(RestoreDatabases.ForDatabase(MainConnectionString, run.LiveDatabase),
            RestoreConnectionString(MainConnectionString, run), _clock.UtcNow, cancellationToken);
        run.UpdateComparison(JsonSerializer.Serialize(cmp, RestoreDatabases.Json), canSwap, note);
        await _db.SaveChangesAsync(cancellationToken);
        return ToDto(run, true);
    }

    /// <inheritdoc />
    public async Task<RestoreRunDto> DiscardAsync(Guid id, CancellationToken cancellationToken)
    {
        await SyncWithAgentAsync(cancellationToken);
        var run = await FindAsync(id, cancellationToken);
        if (run.Status is not (RestoreRunStatus.Ready or RestoreRunStatus.Failed))
            throw new BusinessRuleException("Chỉ bỏ được bản khôi phục đang chờ kiểm tra hoặc lần phục hồi lỗi.");
        var now = _clock.UtcNow;
        string detail;
        if (run.Kind == RestoreKind.Dump)
        {
            var dropped = await RestoreDatabases.DropAsync(AdminConnectionString, run.RestoreDatabase, run.LiveDatabase, cancellationToken);
            run.Discard(_user.UserName, now);
            detail = dropped ? $"Đã xóa CSDL {run.RestoreDatabase}." : $"CSDL {run.RestoreDatabase} không còn.";
        }
        else if (run.Status == RestoreRunStatus.Failed && run.AgentCommandId is { } cid
                 && await _db.AgentCommands.AnyAsync(c => c.Id == cid && c.Status != AgentCommandStatus.Succeeded && c.Status != AgentCommandStatus.Failed,
                     cancellationToken))
        {
            // Lệnh khôi phục chưa từng chạy (hết hạn / hủy) – trên máy không có gì để dọn.
            run.Discard(_user.UserName, now);
            detail = "Lệnh khôi phục chưa chạy – không có gì để dọn.";
        }
        else
        {
            // Mốc thời gian: bộ thực thi dừng máy chủ tạm, xóa thư mục; lần phục hồi chuyển Đã bỏ khi lệnh xong (OnCommandFinishedAsync).
            var env = await AgentEnvironmentAsync(cancellationToken);
            var parameters = new { restoreRunId = run.Id, workDir = Path.Combine(_backup.RootPath ?? "", run.RelativePath ?? ""), port = run.TempPort };
            var cmd = AgentCommand.Create(env.Id, AgentCommandKind.DiscardPointInTime, JsonSerializer.Serialize(parameters, RestoreDatabases.Json),
                "Bỏ bản khôi phục theo mốc thời gian", _user.UserName, now);
            _db.AgentCommands.Add(cmd);
            run.SetAgentCommand(cmd.Id);
            detail = $"Giao bộ thực thi {env.Code} dừng máy chủ tạm, xóa {run.RelativePath} (lệnh {cmd.Id}).";
        }

        await _db.SaveChangesAsync(cancellationToken);
        await _operationLog.WriteAsync(new OperationLogEntry(OperationKinds.RestoreDiscarded, RestoreLabels.Kind(run.Kind), run.Reason,
            OperationResult.Info, detail), cancellationToken);
        return ToDto(run, true);
    }

    /// <inheritdoc />
    public async Task<RestoreRunDto> SwapAsync(Guid id, SwapRestoreRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await SyncWithAgentAsync(cancellationToken);
        var run = await FindAsync(id, cancellationToken);
        if (run.Status != RestoreRunStatus.Ready) throw new BusinessRuleException("Chỉ hoán đổi được bản khôi phục đang chờ kiểm tra.");
        if (!string.Equals(run.LiveDatabase, LiveDatabase, StringComparison.Ordinal))
            throw new BusinessRuleException("CSDL đang chạy đã khác lúc khôi phục – bỏ bản khôi phục này, làm lại.");
        await _guard.VerifyAsync("Hoán đổi bản khôi phục", request.Password, request.Confirmation, DangerConfirmations.Restore, cancellationToken);
        var env = await AgentEnvironmentAsync(cancellationToken);
        var now = _clock.UtcNow;
        run.RequestSwap(request.Reason, _user.UserName, now);
        var vnNow = TimeZoneInfo.ConvertTime(now, VietnamTime.Zone).DateTime;
        AgentCommand cmd;
        if (run.Kind == RestoreKind.Dump)
        {
            var parameters = new
            {
                restoreRunId = run.Id,
                liveDatabase = run.LiveDatabase,
                restoreDatabase = run.RestoreDatabase,
                oldDatabase = RestoreNames.Make(run.LiveDatabase, "old", vnNow),
            };
            cmd = AgentCommand.Create(env.Id, AgentCommandKind.SwapDatabase, JsonSerializer.Serialize(parameters, RestoreDatabases.Json),
                run.SwapReason, _user.UserName, now);
        }
        else
        {
            var parameters = new
            {
                restoreRunId = run.Id,
                workDir = Path.Combine(_backup.RootPath ?? "", run.RelativePath ?? ""),
                port = run.TempPort,
                oldSuffix = string.Create(CultureInfo.InvariantCulture, $"old_{vnNow:yyyyMMdd_HHmm}"),
                walArchivePath = _backup.WalArchivePath,
            };
            cmd = AgentCommand.Create(env.Id, AgentCommandKind.SwapCluster, JsonSerializer.Serialize(parameters, RestoreDatabases.Json),
                run.SwapReason, _user.UserName, now);
        }

        _db.AgentCommands.Add(cmd);
        run.SetAgentCommand(cmd.Id);
        await _db.SaveChangesAsync(cancellationToken);
        await _operationLog.WriteAsync(new OperationLogEntry(OperationKinds.RestoreSwapRequested, RestoreLabels.Kind(run.Kind), run.SwapReason,
            OperationResult.Info, $"Giao bộ thực thi {env.Code} hoán đổi {run.RestoreDatabase} (lệnh {cmd.Id}). Bộ thực thi sẽ bật chế độ bảo trì, " +
            "dừng site; CSDL cũ giữ lại."), cancellationToken);
        return ToDto(run, true);
    }

    /// <summary>
    /// Lệnh bộ thực thi của lần phục hồi đã hết hạn / bị hủy / quá giờ mà lần phục hồi vẫn chờ kết quả (bộ thực thi không báo – không qua
    /// <see cref="RestoreAgentHooks.OnCommandFinishedAsync"/>) → cập nhật theo lệnh.
    /// </summary>
    private async Task SyncWithAgentAsync(CancellationToken ct)
    {
        var waiting = await _db.RestoreRuns
            .Where(r => (r.Status == RestoreRunStatus.Restoring || r.Status == RestoreRunStatus.Swapping
                || ((r.Status == RestoreRunStatus.Ready || r.Status == RestoreRunStatus.Failed) && r.Kind == RestoreKind.PointInTime))
                && r.AgentCommandId != null)
            .ToListAsync(ct);
        if (waiting.Count == 0) return;
        var ids = waiting.Select(r => r.AgentCommandId!.Value).ToList();
        var commands = await _db.AgentCommands.AsNoTracking().Where(c => ids.Contains(c.Id)).ToDictionaryAsync(c => c.Id, ct);
        var changed = false;
        foreach (var run in waiting)
        {
            if (!commands.TryGetValue(run.AgentCommandId!.Value, out var cmd)) continue;
            changed |= RestoreAgentHooks.Apply(run, cmd, _clock.UtcNow) is not null;
        }

        if (changed) await _db.SaveChangesAsync(ct);
    }

    private async Task<(bool Ready, string? Note)> AgentStateAsync(CancellationToken ct)
    {
        var code = _environment.Name;
        var env = await _db.AgentEnvironments.AsNoTracking().FirstOrDefaultAsync(e => e.Code == code, ct);
        if (env is null) return (false, $"Chưa khai môi trường {code} ở màn hình AGT – khôi phục theo mốc và hoán đổi cần bộ thực thi của máy này.");
        if (!env.IsActive) return (false, $"Môi trường {code} đang ngừng dùng ở màn hình AGT.");
        if (env.AgentKeyHash is null) return (false, $"Môi trường {code} chưa có khóa bộ thực thi (AGT → Tạo khóa…).");
        return (true, null);
    }

    private async Task<AgentEnvironment> AgentEnvironmentAsync(CancellationToken ct)
    {
        var (ready, note) = await AgentStateAsync(ct);
        if (!ready) throw new BusinessRuleException(note!);
        return await _db.AgentEnvironments.AsNoTracking().FirstAsync(e => e.Code == _environment.Name, ct);
    }

    private string AdminConnectionString =>
        string.IsNullOrWhiteSpace(_backup.MaintenanceConnectionString) ? MainConnectionString : _backup.MaintenanceConnectionString;

    /// <summary>Tên CSDL khôi phục chưa có trên máy chủ: <c>…_restore_yyyyMMdd_HHmm</c>, trùng thì thêm số.</summary>
    private async Task<string> FreeNameAsync(string live, DateTime vnNow, CancellationToken ct)
    {
        var baseName = RestoreNames.Make(live, "restore", vnNow);
        for (var i = 1; i <= 9; i++)
        {
            var name = i == 1 ? baseName : RestoreNames.Make(live, "restore", vnNow)[..Math.Min(baseName.Length, RestoreRun.DatabaseNameMaxLength - 2)]
                + "_" + i.ToString(CultureInfo.InvariantCulture);
            if (!await RestoreDatabases.ExistsAsync(MainConnectionString, name, ct)) return name;
        }

        throw new BusinessRuleException("Đã có nhiều CSDL khôi phục cùng phút này – thử lại sau một phút.");
    }

    private async Task<RestoreRun> FindAsync(Guid id, CancellationToken ct) =>
        await _db.RestoreRuns.FirstOrDefaultAsync(r => r.Id == id, ct) ?? throw new NotFoundException("Không tìm thấy lần phục hồi.");

    /// <summary>Kết nối tới bản khôi phục: CSDL mới cùng máy chủ (pg_dump) hoặc máy chủ tạm cổng riêng (mốc thời gian).</summary>
    /// <param name="mainConnectionString">Chuỗi kết nối chính.</param>
    /// <param name="run">Lần phục hồi.</param>
    /// <returns>Chuỗi kết nối.</returns>
    public static string RestoreConnectionString(string mainConnectionString, RestoreRun run)
    {
        ArgumentNullException.ThrowIfNull(run);
        return RestoreDatabases.ForDatabase(mainConnectionString, run.RestoreDatabase, run.Kind == RestoreKind.PointInTime ? run.TempPort : null);
    }

    /// <summary>Đổi lần phục hồi sang DTO.</summary>
    /// <param name="r">Lần phục hồi.</param>
    /// <param name="withDetail">Kèm bảng so sánh, kế hoạch.</param>
    /// <returns>DTO.</returns>
    public static RestoreRunDto ToDto(RestoreRun r, bool withDetail)
    {
        ArgumentNullException.ThrowIfNull(r);
        var (label, light) = RestoreLabels.Status(r.Status);
        var source = r.Kind == RestoreKind.Dump
            ? "Bản pg_dump"
            : $"Mốc {(r.TargetTime is { } t ? Vn(t) : "?")} (Full{(r.DiffRunId is null ? "" : " + Diff")} + WAL)";
        RestoreComparisonDto? cmp = null;
        RestorePlan? plan = null;
        if (withDetail)
        {
            cmp = Deserialize<RestoreComparisonDto>(r.ComparisonJson);
            plan = Deserialize<RestorePlan>(r.PlanJson);
        }

        return new RestoreRunDto(r.Id, r.Kind.ToString(), RestoreLabels.Kind(r.Kind), r.Status.ToString(), label, light, source, r.DumpRunId,
            r.FullRunId, r.DiffRunId, r.TargetTime, r.LiveDatabase, r.RestoreDatabase, r.OldDatabase, r.TempPort, r.Reason, r.RequestedByName,
            r.QueuedAt, r.StartedAt, r.ReadyAt, r.SwapReason, r.SwapRequestedByName, r.SwapRequestedAt, r.FinishedAt, r.DiscardedByName,
            r.AgentCommandId, r.CanSwap, r.Note, r.ErrorMessage, cmp, plan);
    }

    private static T? Deserialize<T>(string? json) where T : class
    {
        if (string.IsNullOrEmpty(json)) return null;
        try
        {
            return JsonSerializer.Deserialize<T>(json, RestoreDatabases.Json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Giờ Việt Nam dạng HH:mm dd/MM/yyyy.</summary>
    /// <param name="t">Thời điểm (UTC).</param>
    /// <returns>Chữ.</returns>
    public static string Vn(DateTimeOffset t) =>
        TimeZoneInfo.ConvertTime(t, VietnamTime.Zone).ToString("HH:mm dd/MM/yyyy", CultureInfo.InvariantCulture);

    private static DateTimeOffset VietnamDayStartUtc(DateOnly day)
    {
        var local = day.ToDateTime(TimeOnly.MinValue);
        return new DateTimeOffset(local, VietnamTime.Zone.GetUtcOffset(local)).ToUniversalTime();
    }
}

/// <summary>
/// Nối lệnh bộ thực thi với lần phục hồi (lát QT6): chuẩn bị hoán đổi (bật bảo trì + chép bảng vận hành sang bản khôi phục) và cập nhật lần phục
/// hồi khi lệnh kết thúc. Chạy trong scope của <see cref="AgentGateway"/>.
/// </summary>
public sealed class RestoreAgentHooks : IRestoreAgentHooks
{
    /// <summary>Câu thông báo bảo trì khi hoán đổi.</summary>
    public const string SwapMessage = "Phần mềm đang hoán đổi CSDL sau phục hồi. Vui lòng quay lại sau ít phút.";

    private readonly ProcDbContext _db;
    private readonly IClock _clock;
    private readonly IMaintenanceMode _maintenance;
    private readonly IOperationLog _operationLog;
    private readonly ILogger<RestoreAgentHooks> _logger;

    /// <summary>Tạo.</summary>
    /// <param name="db">CSDL.</param>
    /// <param name="clock">Đồng hồ.</param>
    /// <param name="maintenance">Chế độ bảo trì.</param>
    /// <param name="operationLog">Nhật ký vận hành.</param>
    /// <param name="logger">Log kỹ thuật.</param>
    public RestoreAgentHooks(
        ProcDbContext db, IClock clock, IMaintenanceMode maintenance, IOperationLog operationLog, ILogger<RestoreAgentHooks> logger)
    {
        _db = db;
        _clock = clock;
        _maintenance = maintenance;
        _operationLog = operationLog;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task PrepareSwapAsync(Guid commandId, string environmentCode, CancellationToken cancellationToken)
    {
        var run = await _db.RestoreRuns.AsNoTracking().FirstOrDefaultAsync(r => r.AgentCommandId == commandId, cancellationToken)
            ?? throw new BusinessRuleException("Lệnh hoán đổi không gắn lần phục hồi nào.");
        if (run.Status != RestoreRunStatus.Swapping) throw new BusinessRuleException("Lần phục hồi không ở trạng thái đang hoán đổi.");

        // 1. Chặn người dùng thường trước khi chép (dữ liệu nhập sau lúc này sẽ mất khi hoán đổi – N2).
        await _maintenance.SetAsync(new SetMaintenanceModeRequest(true, SwapMessage, "Hoán đổi bản khôi phục: " + run.SwapReason),
            "agent:" + environmentCode, cancellationToken);
        // 2. Ghi nhật ký TRƯỚC khi chép để dòng này cũng có trong bản khôi phục (sau khi hoán đổi vẫn thấy).
        await _operationLog.WriteAsync(new OperationLogEntry(OperationKinds.RestoreSwapRequested, RestoreLabels.Kind(run.Kind), run.SwapReason,
            OperationResult.Info, $"Bộ thực thi {environmentCode} chuẩn bị hoán đổi: bật chế độ bảo trì, chép bảng vận hành sang " +
            $"{run.RestoreDatabase}."), cancellationToken);
        // 3. Bảng vận hành sang bản khôi phục (kể cả trạng thái bảo trì vừa bật, chính lần phục hồi và lệnh đang chạy).
        var main = _db.Database.GetConnectionString()!;
        var copied = await RestoreCarryOverCopier.CopyAsync(RestoreDatabases.ForDatabase(main, run.LiveDatabase),
            RestoreService.RestoreConnectionString(main, run), cancellationToken);
        _logger.LogInformation("Chuẩn bị hoán đổi {Id}: đã chép bảng vận hành ({Copied}).", run.Id, copied);
    }

    /// <inheritdoc />
    public async Task OnCommandFinishedAsync(Guid commandId, CancellationToken cancellationToken)
    {
        var run = await _db.RestoreRuns.FirstOrDefaultAsync(r => r.AgentCommandId == commandId, cancellationToken);
        if (run is null) return;
        var cmd = await _db.AgentCommands.AsNoTracking().FirstAsync(c => c.Id == commandId, cancellationToken);
        var before = run.Status;
        var now = _clock.UtcNow;
        if (cmd is { Kind: AgentCommandKind.RestorePointInTime, Status: AgentCommandStatus.Succeeded } && run.Status == RestoreRunStatus.Restoring)
        {
            // Máy chủ tạm đã chạy tới mốc: so sánh với CSDL đang chạy (đọc qua cổng máy chủ tạm).
            var main = _db.Database.GetConnectionString()!;
            try
            {
                var (cmp, canSwap, note) = await RestoreDatabases.CompareAsync(RestoreDatabases.ForDatabase(main, run.LiveDatabase),
                    RestoreService.RestoreConnectionString(main, run), now, cancellationToken);
                run.MarkReady(JsonSerializer.Serialize(cmp, RestoreDatabases.Json), canSwap, note, now);
            }
            catch (Exception ex) when (ex is NpgsqlException or InvalidOperationException or TimeoutException)
            {
                _logger.LogWarning(ex, "Không so sánh được bản khôi phục theo mốc {Id}.", run.Id);
                run.MarkReady(null, false, $"Không kết nối được máy chủ CSDL tạm cổng {run.TempPort} để so sánh: {ex.Message}. Bấm \"So sánh lại\".", now);
            }
        }
        else
        {
            Apply(run, cmd, now);
        }

        if (run.Status == before) return;
        await _db.SaveChangesAsync(cancellationToken);
        var kind = run.Status switch
        {
            RestoreRunStatus.Swapped or RestoreRunStatus.Ready when before == RestoreRunStatus.Swapping => OperationKinds.RestoreSwapFinished,
            RestoreRunStatus.Discarded => OperationKinds.RestoreDiscarded,
            _ => OperationKinds.RestoreFinished,
        };
        var ok = run.Status is RestoreRunStatus.Ready or RestoreRunStatus.Swapped or RestoreRunStatus.Discarded && run.ErrorMessage is null;
        await _operationLog.WriteAsync(new OperationLogEntry(kind, RestoreLabels.Kind(run.Kind), run.SwapReason ?? run.Reason,
            ok ? OperationResult.Succeeded : OperationResult.Failed,
            $"{RestoreLabels.Status(run.Status).Label}. {cmd.ResultMessage}"), cancellationToken);
    }

    /// <summary>
    /// Cập nhật lần phục hồi theo lệnh đã kết thúc (trừ khôi phục theo mốc xong – cần so sánh, làm ở <see cref="OnCommandFinishedAsync"/>).
    /// </summary>
    /// <param name="run">Lần phục hồi (đang theo dõi).</param>
    /// <param name="cmd">Lệnh.</param>
    /// <param name="now">Thời điểm (UTC).</param>
    /// <returns>Trạng thái mới khi có đổi; null khi không đổi.</returns>
    public static RestoreRunStatus? Apply(RestoreRun run, AgentCommand cmd, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(cmd);
        if (cmd.Status is AgentCommandStatus.Queued or AgentCommandStatus.Running) return null;
        var ok = cmd.Status == AgentCommandStatus.Succeeded;
        var message = cmd.ResultMessage ?? AgentLabels.Status(cmd.Status).Label;
        switch (cmd.Kind)
        {
            case AgentCommandKind.RestorePointInTime when run.Status == RestoreRunStatus.Restoring && !ok:
                run.Fail("Bộ thực thi: " + message, now);
                return run.Status;
            case AgentCommandKind.SwapDatabase or AgentCommandKind.SwapCluster when run.Status == RestoreRunStatus.Swapping:
                if (ok) run.CompleteSwap(OldName(cmd, run), message, now);
                else run.SwapFailed(message);
                return run.Status;
            case AgentCommandKind.DiscardPointInTime when run.Status is RestoreRunStatus.Ready or RestoreRunStatus.Failed && ok:
                run.Discard(cmd.CreatedByName, now);
                return run.Status;
            default:
                return null;
        }
    }

    /// <summary>Tên CSDL / thư mục cũ lấy từ tham số lệnh hoán đổi.</summary>
    private static string? OldName(AgentCommand cmd, RestoreRun run)
    {
        if (cmd.Parameters is null) return null;
        using var doc = JsonDocument.Parse(cmd.Parameters);
        if (doc.RootElement.TryGetProperty("oldDatabase", out var db)) return db.GetString();
        return doc.RootElement.TryGetProperty("oldSuffix", out var s)
            ? $"Thư mục dữ liệu PostgreSQL cũ đổi tên thêm \"_{s.GetString()}\" (cạnh thư mục dữ liệu)"
            : run.OldDatabase;
    }
}

/// <summary>
/// Chép bảng vận hành (<see cref="RestoreCarryOver.Tables"/>) từ CSDL đang chạy sang bản khôi phục trước khi hoán đổi (lát QT6): trong một giao
/// dịch trên bản khôi phục – <c>COPY … TO STDOUT (FORMAT BINARY)</c> từ CSDL đang chạy vào bảng tạm, rồi xóa nội dung cũ, chèn nội dung mới.
/// <c>OperationLog</c> là bảng chỉ thêm (trigger chặn UPDATE / DELETE) nên chỉ chèn dòng chưa có. Cùng migration (kiểm khi so sánh) nên hai bên
/// cùng cột, cùng kiểu – chép theo các cột cả hai bên đều có cho chắc.
/// </summary>
public static class RestoreCarryOverCopier
{
    private static readonly HashSet<string> AppendOnly = new(StringComparer.Ordinal) { "OperationLog" };

    /// <summary>Chép.</summary>
    /// <param name="liveConnectionString">CSDL đang chạy.</param>
    /// <param name="restoreConnectionString">Bản khôi phục.</param>
    /// <param name="ct">Hủy thao tác.</param>
    /// <returns>Tóm tắt "bảng: số dòng".</returns>
    public static async Task<string> CopyAsync(string liveConnectionString, string restoreConnectionString, CancellationToken ct)
    {
        await using var live = new NpgsqlConnection(liveConnectionString);
        await using var target = new NpgsqlConnection(restoreConnectionString);
        await live.OpenAsync(ct);
        await target.OpenAsync(ct);
        await using var tx = await target.BeginTransactionAsync(ct);
        var parts = new List<string>();
        var tables = RestoreCarryOver.Tables;

        // Xóa nội dung cũ theo thứ tự ngược (bảng con trước – AgentCommand trỏ AgentEnvironment).
        foreach (var t in tables.Reverse().Where(t => !AppendOnly.Contains(t)))
        {
            if (!await TableExistsAsync(target, t, ct)) continue;
            await using var del = new NpgsqlCommand($"DELETE FROM public.{RestoreDatabases.Quote(t)}", target, tx);
            await del.ExecuteNonQueryAsync(ct);
        }

        foreach (var t in tables)
        {
            if (!await TableExistsAsync(live, t, ct) || !await TableExistsAsync(target, t, ct)) continue;
            var liveCols = await ColumnsAsync(live, t, ct);
            var targetCols = (await ColumnsAsync(target, t, ct)).ToHashSet(StringComparer.Ordinal);
            var cols = string.Join(", ", liveCols.Where(targetCols.Contains).Select(RestoreDatabases.Quote));
            var q = "public." + RestoreDatabases.Quote(t);
            await using (var create = new NpgsqlCommand($"CREATE TEMP TABLE \"_carry\" (LIKE {q}) ON COMMIT DROP", target, tx))
                await create.ExecuteNonQueryAsync(ct);
            await using (var reader = await live.BeginRawBinaryCopyAsync($"COPY {q} ({cols}) TO STDOUT (FORMAT BINARY)", ct))
            await using (var writer = await target.BeginRawBinaryCopyAsync($"COPY \"_carry\" ({cols}) FROM STDIN (FORMAT BINARY)", ct))
                await reader.CopyToAsync(writer, ct);
            var insert = AppendOnly.Contains(t)
                ? $"INSERT INTO {q} ({cols}) SELECT {cols} FROM \"_carry\" c WHERE NOT EXISTS (SELECT 1 FROM {q} x WHERE x.\"Id\" = c.\"Id\")"
                : $"INSERT INTO {q} ({cols}) SELECT {cols} FROM \"_carry\"";
            await using (var ins = new NpgsqlCommand(insert, target, tx))
            {
                var n = await ins.ExecuteNonQueryAsync(ct);
                parts.Add($"{t}: {n.ToString(CultureInfo.InvariantCulture)}");
            }

            await using var drop = new NpgsqlCommand("DROP TABLE \"_carry\"", target, tx);
            await drop.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);
        return string.Join(", ", parts);
    }

    private static async Task<bool> TableExistsAsync(NpgsqlConnection conn, string table, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand("SELECT to_regclass(@t) IS NOT NULL", conn);
        cmd.Parameters.AddWithValue("t", "public." + RestoreDatabases.Quote(table));
        return await cmd.ExecuteScalarAsync(ct) is true;
    }

    private static async Task<List<string>> ColumnsAsync(NpgsqlConnection conn, string table, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand(
            "SELECT column_name FROM information_schema.columns WHERE table_schema = 'public' AND table_name = @t ORDER BY ordinal_position", conn);
        cmd.Parameters.AddWithValue("t", table);
        var cols = new List<string>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) cols.Add(reader.GetString(0));
        return cols;
    }
}

/// <summary>
/// Bộ chạy khôi phục pg_dump (lát QT6): tạo CSDL mới → <c>pg_restore</c> → so sánh số dòng → Sẵn sàng kiểm tra. Lỗi thì xóa CSDL dở. Tài khoản
/// bảo trì nếu khai (chủ CSDL mới là tài khoản ứng dụng – <c>--role</c>), không thì tài khoản ứng dụng (cần CREATEDB).
/// </summary>
public sealed class RestoreExecutor : IRestoreExecutor
{
    /// <summary>Khóa <c>pg_advisory_lock</c> riêng của bộ chạy phục hồi (chữ "PROCRST").</summary>
    public const long AdvisoryLockKey = 0x50524F43525354;

    private readonly IServiceScopeFactory _scopes;
    private readonly PgToolRunner _tools;
    private readonly IClock _clock;
    private readonly BackupOptions _options;
    private readonly ILogger<RestoreExecutor> _logger;

    /// <summary>Tạo bộ chạy.</summary>
    /// <param name="scopes">Scope DI.</param>
    /// <param name="tools">Chạy công cụ PostgreSQL.</param>
    /// <param name="clock">Đồng hồ.</param>
    /// <param name="options">Cấu hình sao lưu.</param>
    /// <param name="logger">Log kỹ thuật.</param>
    public RestoreExecutor(IServiceScopeFactory scopes, PgToolRunner tools, IClock clock, IOptions<BackupOptions> options, ILogger<RestoreExecutor> logger)
    {
        _scopes = scopes;
        _tools = tools;
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
            var run = await db.RestoreRuns.Where(r => r.Status == RestoreRunStatus.Queued && r.Kind == RestoreKind.Dump)
                .OrderBy(r => r.QueuedAt).FirstOrDefaultAsync(cancellationToken);
            if (run is null) break;

            run.Start(_clock.UtcNow);
            await db.SaveChangesAsync(cancellationToken);
            var main = db.Database.GetConnectionString()!;
            try
            {
                await RestoreDumpAsync(run, db, main, cancellationToken);
            }
            catch (Exception ex)
            {
                if (ex is not BackupFailedException) _logger.LogError(ex, "Khôi phục pg_dump {Id} lỗi.", run.Id);
                await TryDropAsync(run, main);
                var message = ex is OperationCanceledException && cancellationToken.IsCancellationRequested
                    ? "Ứng dụng dừng khi đang khôi phục – làm lại."
                    : ex is BackupFailedException or NpgsqlException or IOException ? ex.Message : $"{ex.GetType().Name}: {ex.Message}";
                run.Fail(message, _clock.UtcNow);
            }

            await db.SaveChangesAsync(CancellationToken.None);
            await scope.ServiceProvider.GetRequiredService<IOperationLog>().WriteAsync(new OperationLogEntry(OperationKinds.RestoreFinished,
                RestoreLabels.Kind(run.Kind), run.Reason, run.Status == RestoreRunStatus.Ready ? OperationResult.Succeeded : OperationResult.Failed,
                run.Status == RestoreRunStatus.Ready ? $"{run.RestoreDatabase}: {run.Note}" : $"{run.RestoreDatabase}: {run.ErrorMessage}"),
                CancellationToken.None);
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
        var stuck = await db.RestoreRuns.Where(r => r.Status == RestoreRunStatus.Restoring && r.Kind == RestoreKind.Dump).ToListAsync(cancellationToken);
        foreach (var run in stuck)
        {
            await TryDropAsync(run, db.Database.GetConnectionString()!);
            run.Fail("Ứng dụng khởi động lại khi đang khôi phục – làm lại.", _clock.UtcNow);
        }

        await db.SaveChangesAsync(cancellationToken);
        return stuck.Count;
    }

    private async Task RestoreDumpAsync(RestoreRun run, ProcDbContext db, string main, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.RootPath)) throw new BackupFailedException("Chưa khai thư mục sao lưu (Backup:RootPath).");
        var dump = await db.BackupRuns.AsNoTracking().FirstOrDefaultAsync(r => r.Id == run.DumpRunId, ct);
        if (dump?.RelativePath is null) throw new BackupFailedException("Không thấy bản pg_dump nguồn.");
        var file = Path.Combine(_options.RootPath, dump.RelativePath);
        if (!File.Exists(file)) throw new BackupFailedException($"Không thấy file {dump.RelativePath} trong thư mục sao lưu.");

        var mainInfo = PgConnectionInfo.Parse(main);
        var useMaint = !string.IsNullOrWhiteSpace(_options.MaintenanceConnectionString);
        var admin = useMaint ? _options.MaintenanceConnectionString! : main;
        var conn = PgConnectionInfo.Parse(admin);
        // Tài khoản bảo trì tạo / khôi phục thì chủ CSDL và chủ các bảng là tài khoản ứng dụng – sau khi hoán đổi ứng dụng dùng như trước.
        var owner = useMaint ? mainInfo.Username : null;
        await RestoreDatabases.CreateAsync(admin, run.RestoreDatabase, run.LiveDatabase, owner, ct);

        // Công cụ chạy trong thư mục chứa file dump, tham số là tên file không dấu (PgToolRunner.RunAsync).
        var result = await _tools.RunAsync("pg_restore", PgToolArgs.Restore(conn, run.RestoreDatabase, Path.GetFileName(file), owner), conn.Password,
            Path.GetDirectoryName(file)!, ct);
        if (!result.Succeeded) throw new BackupFailedException(result.Describe("pg_restore"));

        var (cmp, canSwap, note) = await RestoreDatabases.CompareAsync(RestoreDatabases.ForDatabase(main, run.LiveDatabase),
            RestoreService.RestoreConnectionString(main, run), _clock.UtcNow, ct);
        run.MarkReady(JsonSerializer.Serialize(cmp, RestoreDatabases.Json), canSwap, note, _clock.UtcNow);
    }

    private async Task TryDropAsync(RestoreRun run, string main)
    {
        try
        {
            var admin = string.IsNullOrWhiteSpace(_options.MaintenanceConnectionString) ? main : _options.MaintenanceConnectionString;
            await RestoreDatabases.DropAsync(admin, run.RestoreDatabase, run.LiveDatabase, CancellationToken.None);
        }
        catch (Exception ex) when (ex is NpgsqlException or BusinessRuleException)
        {
            _logger.LogWarning(ex, "Không xóa được CSDL khôi phục dở {Name}.", run.RestoreDatabase);
        }
    }

    /// <summary>Khóa advisory trên kết nối không dùng pool (xem <see cref="BackupExecutor"/>).</summary>
    private static async Task<NpgsqlConnection?> TryLockAsync(ProcDbContext db, CancellationToken cancellationToken)
    {
        var cs = new NpgsqlConnectionStringBuilder(db.Database.GetConnectionString()) { Pooling = false }.ConnectionString;
        var connection = new NpgsqlConnection(cs);
        await connection.OpenAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand("SELECT pg_try_advisory_lock(@k)", connection);
        cmd.Parameters.AddWithValue("k", AdvisoryLockKey);
        if (await cmd.ExecuteScalarAsync(cancellationToken) is true) return connection;
        await connection.DisposeAsync();
        return null;
    }
}

/// <summary>Tín hiệu đánh thức bộ chạy khôi phục (singleton).</summary>
public sealed class RestoreQueueSignal : IRestoreQueueSignal
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
            // Đã có tín hiệu chờ – bộ chạy chạy hết hàng chờ mỗi lần thức.
        }
    }

    /// <inheritdoc />
    public Task WaitAsync(TimeSpan timeout, CancellationToken cancellationToken) => _semaphore.WaitAsync(timeout, cancellationToken);
}

/// <summary>Dịch vụ nền khôi phục pg_dump (lát QT6): thức khi có lần mới hoặc mỗi phút.</summary>
public sealed class RestoreBackgroundService : BackgroundService
{
    /// <summary>Tên dịch vụ trên màn hình SYS.</summary>
    public const string ServiceName = "Phục hồi";

    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    private readonly IRestoreExecutor _executor;
    private readonly IRestoreQueueSignal _signal;
    private readonly IBackgroundHeartbeat _heartbeat;
    private readonly RestoreOptions _options;
    private readonly BackupOptions _backup;
    private readonly ILogger<RestoreBackgroundService> _logger;

    /// <summary>Tạo dịch vụ nền.</summary>
    /// <param name="executor">Bộ chạy.</param>
    /// <param name="signal">Tín hiệu.</param>
    /// <param name="heartbeat">Báo nhịp cho SYS.</param>
    /// <param name="options">Cấu hình phục hồi.</param>
    /// <param name="backup">Cấu hình sao lưu (thời gian tối đa công cụ).</param>
    /// <param name="logger">Log kỹ thuật.</param>
    public RestoreBackgroundService(
        IRestoreExecutor executor, IRestoreQueueSignal signal, IBackgroundHeartbeat heartbeat, IOptions<RestoreOptions> options,
        IOptions<BackupOptions> backup, ILogger<RestoreBackgroundService> logger)
    {
        _executor = executor;
        _signal = signal;
        _heartbeat = heartbeat;
        _options = options.Value;
        _backup = backup.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.WorkerEnabled) return;
        var beatSeconds = (int)TimeSpan.FromMinutes(Math.Max(1, _backup.ToolTimeoutMinutes)).TotalSeconds;
        _heartbeat.Beat(ServiceName, beatSeconds, "Khởi động.");
        try
        {
            var stuck = await _executor.FailInterruptedAsync(stoppingToken);
            if (stuck > 0) _logger.LogWarning("Ghi lỗi {Count} lần khôi phục dừng giữa chừng.", stuck);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Không kiểm tra được lần khôi phục dừng giữa chừng.");
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            string? note = null;
            try
            {
                var done = await _executor.ProcessQueueAsync(stoppingToken);
                if (done > 0) note = $"Vừa chạy {done.ToString(CultureInfo.InvariantCulture)} lần khôi phục.";
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Bộ chạy phục hồi lỗi.");
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
