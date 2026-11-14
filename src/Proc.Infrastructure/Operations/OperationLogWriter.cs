using System.Collections.Concurrent;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Proc.Application.Common;
using Proc.Application.Operations;
using Proc.Domain.Common;
using Proc.Domain.Operations;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.Operations;

/// <summary>
/// Ghi nhật ký vận hành (design.md mục 21.2 – N3): ghi file ngày trước (<c>operations-yyyyMMdd.log</c>, mỗi dòng một JSON, ngày theo giờ
/// Việt Nam), rồi ghi CSDL bằng scope DI riêng (DbContext riêng – lỗi của thao tác chính không làm mất nhật ký). Ghi CSDL lỗi thì ghi thêm một
/// dòng lỗi vào file và báo lỗi cho bên gọi; file lỗi thì chỉ ghi log kỹ thuật, vẫn ghi CSDL.
/// </summary>
public sealed class OperationLogWriter : IOperationLog
{
    private static readonly JsonSerializerOptions Json = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    // Khóa ghi file trong tiến trình: nhiều request cùng ghi một file ngày không chen dòng lẫn nhau.
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> FileLocks = new(StringComparer.Ordinal);

    private readonly IServiceScopeFactory _scopes;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;
    private readonly AppEnvironmentOptions _environment;
    private readonly string _directory;
    private readonly ILogger<OperationLogWriter> _logger;

    /// <summary>Tạo bộ ghi.</summary>
    /// <param name="scopes">Tạo scope DI riêng để có DbContext riêng.</param>
    /// <param name="user">Người đang thao tác (không có = hệ thống).</param>
    /// <param name="clock">Đồng hồ.</param>
    /// <param name="environment">Môi trường đang chạy.</param>
    /// <param name="options">Thư mục file nhật ký (đã đổi sang đường dẫn tuyệt đối khi khởi động).</param>
    /// <param name="logger">Log kỹ thuật.</param>
    public OperationLogWriter(
        IServiceScopeFactory scopes, ICurrentUser user, IClock clock, IOptions<AppEnvironmentOptions> environment,
        IOptions<OperationsOptions> options, ILogger<OperationLogWriter> logger)
    {
        _scopes = scopes;
        _user = user;
        _clock = clock;
        _environment = environment.Value;
        _directory = options.Value.LogDirectory;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<OperationLogItemDto> WriteAsync(OperationLogEntry entry, CancellationToken cancellationToken)
    {
        var log = OperationLog.Create(_clock.UtcNow, _environment.Name, _user.UserId, _user.UserName, entry.Kind, entry.Target, entry.Reason,
            entry.Result, entry.Detail);
        var item = ToItem(log);
        await AppendFileAsync(item, null, cancellationToken);
        try
        {
            await using var scope = _scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ProcDbContext>();
            db.OperationLogs.Add(log);
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await AppendFileAsync(item, $"Không ghi được vào CSDL: {ex.Message}", cancellationToken);
            throw;
        }

        return item;
    }

    /// <summary>Đường dẫn file nhật ký của một ngày (giờ Việt Nam).</summary>
    /// <param name="directory">Thư mục nhật ký.</param>
    /// <param name="day">Ngày.</param>
    /// <returns>Đường dẫn file.</returns>
    public static string FilePath(string directory, DateOnly day) => Path.Combine(directory, $"operations-{day:yyyyMMdd}.log");

    /// <summary>Thêm một dòng JSON vào file ngày; lỗi chỉ ghi log kỹ thuật.</summary>
    private async Task AppendFileAsync(OperationLogItemDto item, string? writeError, CancellationToken cancellationToken)
    {
        var path = FilePath(_directory, DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(item.OccurredAt, VietnamTime.Zone).DateTime));
        var gate = FileLocks.GetOrAdd(path, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(_directory);
            var line = JsonSerializer.Serialize(new { item, writeError }, Json);
            await File.AppendAllTextAsync(path, line + Environment.NewLine, cancellationToken);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Không ghi được file nhật ký vận hành {Path}.", path);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>Dòng nhật ký thành DTO.</summary>
    /// <param name="x">Dòng nhật ký.</param>
    /// <returns>DTO.</returns>
    internal static OperationLogItemDto ToItem(OperationLog x) =>
        new(x.Id, x.OccurredAt, x.Environment, x.UserName, x.Kind, x.Target, x.Reason, x.Result.ToString(), x.Detail);
}

/// <summary>Đọc nhật ký vận hành trong CSDL (màn hình Nhật ký vận hành).</summary>
public sealed class OperationLogQueries : IOperationLogQueries
{
    /// <summary>Khoảng ngày dài nhất mỗi lần xem.</summary>
    public const int MaxDays = 366;

    private readonly ProcDbContext _db;

    /// <summary>Tạo dịch vụ.</summary>
    /// <param name="db">DbContext.</param>
    public OperationLogQueries(ProcDbContext db) => _db = db;

    /// <inheritdoc />
    public async Task<OperationLogPage> SearchAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        if (to < from) throw new BusinessRuleException("Đến ngày phải sau hoặc bằng Từ ngày.");
        if (to.DayNumber - from.DayNumber >= MaxDays) throw new BusinessRuleException($"Mỗi lần chỉ xem được tối đa {MaxDays} ngày nhật ký.");
        var (start, end) = (StartOfDay(from), StartOfDay(to.AddDays(1)));
        var rows = await _db.OperationLogs.AsNoTracking()
            .Where(x => x.OccurredAt >= start && x.OccurredAt < end)
            .OrderByDescending(x => x.OccurredAt)
            .Take(IOperationLogQueries.MaxRows + 1)
            .ToListAsync(cancellationToken);
        return new OperationLogPage(rows.Take(IOperationLogQueries.MaxRows).Select(OperationLogWriter.ToItem).ToList(),
            rows.Count > IOperationLogQueries.MaxRows);
    }

    /// <summary>Đầu ngày giờ Việt Nam đổi sang UTC, ví dụ 14/11/2026 → 13/11/2026 17:00 UTC.</summary>
    private static DateTimeOffset StartOfDay(DateOnly day)
    {
        var local = day.ToDateTime(TimeOnly.MinValue);
        return new DateTimeOffset(local, VietnamTime.Zone.GetUtcOffset(local)).ToUniversalTime();
    }
}

/// <summary>Nhịp dịch vụ nền lưu trong bộ nhớ (singleton).</summary>
public sealed class BackgroundHeartbeat : IBackgroundHeartbeat
{
    private readonly ConcurrentDictionary<string, BackgroundBeat> _beats = new(StringComparer.Ordinal);
    private readonly IClock _clock;

    /// <summary>Tạo bộ ghi nhịp.</summary>
    /// <param name="clock">Đồng hồ.</param>
    public BackgroundHeartbeat(IClock clock) => _clock = clock;

    /// <inheritdoc />
    public void Beat(string service, int intervalSeconds, string? detail = null) =>
        _beats[service] = new BackgroundBeat(service, _clock.UtcNow, intervalSeconds, detail);

    /// <inheritdoc />
    public IReadOnlyList<BackgroundBeat> Snapshot() => _beats.Values.OrderBy(b => b.Service, StringComparer.Ordinal).ToList();
}
