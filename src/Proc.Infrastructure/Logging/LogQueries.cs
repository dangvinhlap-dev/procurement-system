using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Proc.Application.Common;
using Proc.Application.Logging;
using Proc.Domain.Common;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.Logging;

/// <summary>
/// Đọc AuditLog và AccessLog cho màn hình nhật ký. Bảng nhật ký không có CompanyId bắt buộc nên không gắn global query
/// filter; phạm vi công ty lọc ngay ở đây, mọi hàm đều đi qua <see cref="ICurrentUserScope"/>.
/// </summary>
public sealed class LogQueries : ILogQueries
{
    /// <summary>Số dòng tối đa mỗi lần tìm; nhiều hơn thì báo người dùng thu hẹp điều kiện lọc.</summary>
    public const int MaxRows = 2000;

    /// <summary>Khoảng ngày tối đa mỗi lần tìm – bảng phân vùng theo tháng, giới hạn để không quét quá nhiều phân vùng.</summary>
    public const int MaxDays = 366;

    private static readonly TimeZoneInfo Vietnam = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");

    private readonly ProcDbContext _db;
    private readonly ICurrentUserScope _scope;

    /// <summary>
    /// Tạo đối tượng truy vấn.
    /// </summary>
    /// <param name="db">DbContext.</param>
    /// <param name="scope">Phạm vi người xem.</param>
    public LogQueries(ProcDbContext db, ICurrentUserScope scope)
    {
        _db = db;
        _scope = scope;
    }

    /// <inheritdoc />
    public async Task<LogPage<AuditLogItemDto>> SearchAuditAsync(AuditLogQuery query, CancellationToken cancellationToken)
    {
        var companyId = _scope.RequireCompany();
        var (from, to) = ToUtcRange(query.From, query.To);

        var logs = _db.AuditLogs.AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.OccurredAt >= from && x.OccurredAt < to);
        if (!string.IsNullOrWhiteSpace(query.UserName)) logs = logs.Where(x => x.UserName == query.UserName.Trim());
        if (!string.IsNullOrWhiteSpace(query.EntityType)) logs = logs.Where(x => x.EntityType == query.EntityType);
        if (!string.IsNullOrWhiteSpace(query.Action)) logs = logs.Where(x => x.Action == query.Action);

        var rows = await logs
            .OrderByDescending(x => x.OccurredAt).ThenByDescending(x => x.Id)
            .Take(MaxRows + 1)
            .ToListAsync(cancellationToken);
        return new LogPage<AuditLogItemDto>(rows.Take(MaxRows).Select(ToItem).ToList(), rows.Count > MaxRows);
    }

    /// <inheritdoc />
    public async Task<AuditLogDetailDto> GetAuditAsync(Guid id, CancellationToken cancellationToken)
    {
        var companyId = _scope.RequireCompany();
        var log = await _db.AuditLogs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.CompanyId == companyId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy dòng nhật ký.");

        var changes = ParseChanges(log.Changes)
            .Select(c => new AuditFieldChangeDto(c.Key, Display(c.Value.Old), Display(c.Value.New)))
            .ToList();
        return new AuditLogDetailDto(ToItem(log), log.UserAgent, log.CorrelationId, changes);
    }

    /// <inheritdoc />
    public async Task<LogPage<AccessLogItemDto>> SearchAccessAsync(AccessLogQuery query, CancellationToken cancellationToken)
    {
        var companyId = _scope.RequireCompany();
        var (from, to) = ToUtcRange(query.From, query.To);

        var logs = _db.AccessLogs.AsNoTracking().Where(x => x.OccurredAt >= from && x.OccurredAt < to);

        // Tổng công ty quản trị toàn tập đoàn nên thấy mọi dòng, kể cả đăng nhập sai bằng tên không tồn tại.
        // Công ty thành viên chỉ thấy người đang làm việc ở công ty mình và sự kiện gắn với công ty mình.
        var isHeadOffice = await _db.Companies.AnyAsync(c => c.Id == companyId && c.IsHeadOffice, cancellationToken);
        if (!isHeadOffice)
        {
            var members = _db.UserCompanyRoles.Where(r => r.CompanyId == companyId).Select(r => (Guid?)r.UserId);
            logs = logs.Where(x => x.CompanyId == companyId || members.Contains(x.UserId));
        }

        if (!string.IsNullOrWhiteSpace(query.UserName)) logs = logs.Where(x => x.UserName == query.UserName.Trim());
        if (query.Event is { } accessEvent) logs = logs.Where(x => x.Event == accessEvent.ToString());

        var rows = await logs
            .OrderByDescending(x => x.OccurredAt).ThenByDescending(x => x.Id)
            .Take(MaxRows + 1)
            .Select(x => new
            {
                x.Id, x.OccurredAt, x.Event, x.UserName, x.Detail, x.IpAddress, x.UserAgent,
                CompanyCode = _db.Companies.Where(c => c.Id == x.CompanyId).Select(c => c.Code).FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        var items = rows.Take(MaxRows)
            .Select(x => new AccessLogItemDto(
                x.Id, x.OccurredAt, Enum.Parse<AccessEvent>(x.Event), x.UserName, x.CompanyCode, x.Detail, x.IpAddress, x.UserAgent))
            .ToList();
        return new LogPage<AccessLogItemDto>(items, rows.Count > MaxRows);
    }

    /// <summary>
    /// Đổi khoảng ngày theo giờ Việt Nam sang khoảng thời điểm UTC [from, to) – CSDL lưu UTC (CLAUDE.md mục 5).
    /// Ví dụ 29/10/2026–29/10/2026 → [28/10/2026 17:00 UTC, 29/10/2026 17:00 UTC).
    /// </summary>
    private static (DateTimeOffset From, DateTimeOffset To) ToUtcRange(DateOnly from, DateOnly to)
    {
        if (to < from) throw new BusinessRuleException("Đến ngày phải sau hoặc bằng Từ ngày.");
        if (to.DayNumber - from.DayNumber >= MaxDays)
            throw new BusinessRuleException($"Mỗi lần chỉ xem được tối đa {MaxDays} ngày nhật ký.");

        static DateTimeOffset StartOfDay(DateOnly day)
        {
            var local = day.ToDateTime(TimeOnly.MinValue);
            return new DateTimeOffset(local, Vietnam.GetUtcOffset(local)).ToUniversalTime();
        }

        return (StartOfDay(from), StartOfDay(to.AddDays(1)));
    }

    private static AuditLogItemDto ToItem(AuditLog x) => new(
        x.Id,
        x.OccurredAt,
        x.UserName,
        x.EntityType,
        AuditRegistry.DisplayName(x.EntityType),
        x.EntityId,
        x.EntityLabel,
        x.DocumentNumber,
        x.Action,
        ParseChanges(x.Changes).Keys.ToList(),
        x.IpAddress);

    private static Dictionary<string, (JsonElement Old, JsonElement New)> ParseChanges(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.EnumerateObject().ToDictionary(
            p => p.Name,
            p => (p.Value.GetProperty("old").Clone(), p.Value.GetProperty("new").Clone()));
    }

    /// <summary>Giá trị JSON thành chuỗi hiển thị: chuỗi bỏ ngoặc kép, null thành null, kiểu khác giữ nguyên dạng JSON.</summary>
    private static string? Display(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Null or JsonValueKind.Undefined => null,
        JsonValueKind.String => value.GetString(),
        _ => value.GetRawText(),
    };
}
