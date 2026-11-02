using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Proc.Application.Common;
using Proc.Application.Logging;
using Proc.Domain.Common;
using Proc.Infrastructure.Identity;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.Logging;

/// <summary>
/// Lịch sử thay đổi theo ô (phím F1) và theo bản ghi ("Lịch sử thay đổi") đọc từ AuditLog – design.md mục 9.3a.
/// Không cần quyền xem nhật ký hệ thống: ai mở được bản ghi thì xem được lịch sử của nó. Vì vậy mọi hàm đều nạp bản ghi qua DbContext
/// có global query filter (công ty, kho, nhóm hàng) và kiểm tra cả bản ghi cha (dòng → đầu phiếu); không thấy thì báo không tìm thấy.
/// Nhật ký của bản ghi thì lấy hết, không lọc theo công ty lúc ghi – danh mục dùng chung có thể do tổng công ty sửa.
/// </summary>
public sealed class ChangeHistoryQueries : IChangeHistoryQueries
{
    /// <summary>Số dòng lịch sử tối đa mỗi lần xem.</summary>
    public const int MaxRows = 2000;

    /// <summary>Độ sâu tối đa khi gom dòng con (phiếu → dòng → dòng báo giá).</summary>
    private const int MaxChildDepth = 2;

    private static readonly MethodInfo FindMethod =
        typeof(ChangeHistoryQueries).GetMethod(nameof(FindVisibleCoreAsync), BindingFlags.NonPublic | BindingFlags.Instance)!;

    private readonly ProcDbContext _db;
    private readonly Dictionary<(Type, string), object?> _cache = [];

    /// <summary>
    /// Tạo đối tượng truy vấn.
    /// </summary>
    /// <param name="db">DbContext (có global query filter theo phạm vi người xem).</param>
    public ChangeHistoryQueries(ProcDbContext db) => _db = db;

    /// <inheritdoc />
    public async Task<FieldHistoryDto> GetFieldHistoryAsync(
        string entityType, string entityId, string field, CancellationToken cancellationToken)
    {
        var (type, descriptor) = ResolveType(entityType);
        var entity = await FindVisibleAsync(type, entityId, checkParents: true, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy bản ghi hoặc bạn không được xem bản ghi này.");

        var property = ResolveProperty(type, field);
        var info = InfoOf(type, descriptor, property);
        var logs = await LogsOfAsync(descriptor.EntityType, [entityId], cancellationToken);
        var record = await StampAsync(entity, descriptor, logs, cancellationToken);
        if (property is null) return new FieldHistoryDto(info, record, [], false);

        var changes = await ToChangesAsync(logs, type, property.Name, null, cancellationToken);
        return new FieldHistoryDto(info, record, changes.Take(MaxRows).ToList(), changes.Count > MaxRows);
    }

    /// <inheritdoc />
    public async Task<RecordHistoryDto> GetRecordHistoryAsync(string entityType, string entityId, CancellationToken cancellationToken)
    {
        var (type, descriptor) = ResolveType(entityType);
        var entity = await FindVisibleAsync(type, entityId, checkParents: true, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy bản ghi hoặc bạn không được xem bản ghi này.");

        var logs = await LogsOfAsync(descriptor.EntityType, [entityId], cancellationToken);
        var record = await StampAsync(entity, descriptor, logs, cancellationToken);
        // Dòng con tạo cùng lúc hoặc sau đầu phiếu: chỉ quét nhật ký từ lúc tạo bản ghi (bảng phân vùng theo tháng – bỏ qua tháng cũ).
        var since = logs.Count > 0 ? logs.Min(l => l.OccurredAt) : (entity as AuditableEntity)?.CreatedAt;

        var changes = await ToChangesAsync(logs, type, null, null, cancellationToken);
        await AddChildrenAsync(type, [entityId], since?.AddMinutes(-1), 1, changes, cancellationToken);
        var ordered = changes.OrderByDescending(c => c.OccurredAt).ThenByDescending(c => c.LogId).ToList();
        return new RecordHistoryDto(
            descriptor.EntityType, descriptor.DisplayName, record, ordered.Take(MaxRows).ToList(), ordered.Count > MaxRows);
    }

    /// <summary>
    /// Gom lịch sử các dòng con (kể cả dòng đã xóa) của các bản ghi cha. Dòng con là entity có [Auditable] nhưng không phải
    /// AuditableEntity (dòng phiếu, người liên hệ, quyền của vai trò…), xóa theo cha (cascade). Dòng đã xóa không còn trong CSDL nên
    /// tìm qua nhật ký: dòng nhật ký tạo/xóa ghi đủ mọi trường, trong đó có khóa trỏ về cha, ví dụ {"OrderId": {"old": null, "new": "…"}}.
    /// </summary>
    private async Task AddChildrenAsync(
        IEntityType parent, string[] parentIds, DateTimeOffset? since, int depth, List<FieldChangeDto> changes, CancellationToken ct)
    {
        foreach (var fk in parent.GetReferencingForeignKeys())
        {
            var child = fk.DeclaringEntityType;
            if (fk.DeleteBehavior != DeleteBehavior.Cascade || fk.Properties.Count != 1 || child == parent) continue;
            if (typeof(AuditableEntity).IsAssignableFrom(child.ClrType) || child.ClrType == typeof(AppUser)) continue;
            if (AuditRegistry.Find(child.ClrType) is not { } childDescriptor) continue;

            var fkName = fk.Properties[0].Name;
            var fromTime = since ?? new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);
            var ids = await _db.Database.SqlQuery<string>($"""
                SELECT DISTINCT "EntityId" AS "Value" FROM "AuditLog"
                WHERE "EntityType" = {childDescriptor.EntityType} AND "OccurredAt" >= {fromTime}
                  AND (("Changes" -> {fkName} ->> 'new') = ANY({parentIds}) OR ("Changes" -> {fkName} ->> 'old') = ANY({parentIds}))
                """).ToArrayAsync(ct);
            if (ids.Length == 0) continue;

            var logs = await LogsOfAsync(childDescriptor.EntityType, ids, ct);
            changes.AddRange(await ToChangesAsync(logs, child, null, LineNumbers(logs), ct));
            if (depth < MaxChildDepth) await AddChildrenAsync(child, ids, since, depth + 1, changes, ct);
        }
    }

    /// <summary>Số dòng của từng dòng con, lấy từ nhật ký (giá trị gần nhất của trường LineNo) – có cả dòng đã xóa.</summary>
    private static Dictionary<string, int> LineNumbers(List<AuditLog> logs)
    {
        var result = new Dictionary<string, int>();
        foreach (var log in logs.OrderBy(l => l.OccurredAt))
        {
            using var json = JsonDocument.Parse(log.Changes);
            if (!json.RootElement.TryGetProperty("LineNo", out var lineNo)) continue;
            var value = lineNo.GetProperty("new").ValueKind == JsonValueKind.Number ? lineNo.GetProperty("new") : lineNo.GetProperty("old");
            if (value.ValueKind == JsonValueKind.Number) result[log.EntityId] = value.GetInt32();
        }

        return result;
    }

    private Task<List<AuditLog>> LogsOfAsync(string entityType, string[] ids, CancellationToken ct) =>
        _db.AuditLogs.AsNoTracking()
            .Where(x => x.EntityType == entityType && ids.Contains(x.EntityId))
            .OrderByDescending(x => x.OccurredAt).ThenByDescending(x => x.Id)
            .ToListAsync(ct);

    /// <summary>
    /// Trải nhật ký thành từng trường thay đổi; đổi khóa tham chiếu sang mã – tên và tên đăng nhập sang họ tên.
    /// </summary>
    /// <param name="logs">Nhật ký (mới nhất trước).</param>
    /// <param name="type">Loại entity của các dòng nhật ký.</param>
    /// <param name="onlyField">Chỉ lấy trường này (F1 trên một ô); null = mọi trường.</param>
    /// <param name="lineNumbers">Số dòng theo khóa bản ghi (với dòng con).</param>
    /// <param name="ct">Hủy thao tác.</param>
    private async Task<List<FieldChangeDto>> ToChangesAsync(
        List<AuditLog> logs, IEntityType type, string? onlyField, Dictionary<string, int>? lineNumbers, CancellationToken ct)
    {
        var descriptor = AuditRegistry.Find(type.ClrType)!;
        var names = await FullNamesAsync(logs, ct);
        var result = new List<FieldChangeDto>();
        foreach (var log in logs)
        {
            using var json = JsonDocument.Parse(log.Changes);
            foreach (var change in json.RootElement.EnumerateObject())
            {
                if (onlyField is not null && change.Name != onlyField) continue;
                var property = type.FindProperty(change.Name);
                var kind = property is null ? FieldValueKinds.Text : KindOf(property);
                var oldValue = Normalize(Raw(change.Value.GetProperty("old")), kind);
                var newValue = Normalize(Raw(change.Value.GetProperty("new")), kind);
                string? oldText = null, newText = null;
                if (kind == FieldValueKinds.Reference && !descriptor.Redact.Contains(change.Name))
                {
                    oldText = await ReferenceLabelAsync(property!, oldValue, ct);
                    newText = await ReferenceLabelAsync(property!, newValue, ct);
                }

                result.Add(new FieldChangeDto(
                    log.Id,
                    log.OccurredAt,
                    log.UserName,
                    log.UserId is { } userId && names.TryGetValue(userId, out var fullName) ? fullName : null,
                    log.Action,
                    descriptor.EntityType,
                    descriptor.DisplayName,
                    log.EntityId,
                    log.EntityLabel,
                    lineNumbers is not null && lineNumbers.TryGetValue(log.EntityId, out var lineNo) ? lineNo : null,
                    change.Name,
                    kind,
                    oldValue,
                    newValue,
                    oldText,
                    newText));
            }
        }

        return result;
    }

    private async Task<Dictionary<Guid, string>> FullNamesAsync(List<AuditLog> logs, CancellationToken ct)
    {
        var ids = logs.Where(l => l.UserId != null).Select(l => l.UserId!.Value).Distinct().ToList();
        if (ids.Count == 0) return [];
        return await _db.Users.AsNoTracking().Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
    }

    /// <summary>
    /// Người, thời điểm tạo và sửa gần nhất: ưu tiên nhật ký; bản ghi tạo trước khi có nhật ký (dữ liệu khởi tạo) thì lấy từ cột
    /// CreatedAt/CreatedBy của bản ghi.
    /// </summary>
    private async Task<RecordStampDto> StampAsync(object entity, AuditDescriptor descriptor, List<AuditLog> logs, CancellationToken ct)
    {
        var created = logs.LastOrDefault(l => l.Action == "Create");
        var updated = logs.FirstOrDefault(l => l.Action != "Create");
        var auditable = entity as AuditableEntity;
        var userIds = new[] { created?.UserId ?? auditable?.CreatedBy, updated?.UserId ?? auditable?.UpdatedBy }
            .OfType<Guid>().Distinct().ToList();
        var users = await _db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => $"{u.UserName} – {u.FullName}", ct);
        string? UserOf(Guid? id, string? fallback) => id is { } v && users.TryGetValue(v, out var name) ? name : fallback;

        return new RecordStampDto(
            LabelOf(entity, descriptor) ?? created?.EntityLabel,
            created?.OccurredAt ?? auditable?.CreatedAt,
            created is not null ? UserOf(created.UserId, created.UserName) : UserOf(auditable?.CreatedBy, null),
            updated?.OccurredAt ?? auditable?.UpdatedAt,
            updated is not null ? UserOf(updated.UserId, updated.UserName) : UserOf(auditable?.UpdatedBy, null));
    }

    private (IEntityType Type, AuditDescriptor Descriptor) ResolveType(string entityType)
    {
        foreach (var type in _db.Model.GetEntityTypes())
        {
            if (AuditRegistry.Find(type.ClrType) is { } descriptor
                && string.Equals(descriptor.EntityType, entityType, StringComparison.OrdinalIgnoreCase))
                return (type, descriptor);
        }

        throw new NotFoundException("Loại dữ liệu này không có nhật ký thay đổi.");
    }

    /// <summary>
    /// Tìm trường theo tên ô ở giao diện: đúng tên (không phân biệt hoa thường), hoặc thêm hậu tố Id
    /// (ô "supplier" hiện mã – tên NCC nhưng lưu ở SupplierId).
    /// </summary>
    private static IProperty? ResolveProperty(IEntityType type, string field)
    {
        var name = field.Trim();
        var properties = type.GetProperties().ToList();
        return properties.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
            ?? properties.FirstOrDefault(p => string.Equals(p.Name, name + "Id", StringComparison.OrdinalIgnoreCase));
    }

    private static FieldInfoDto InfoOf(IEntityType type, AuditDescriptor descriptor, IProperty? property)
    {
        var table = type.GetTableName();
        if (property is null)
            return new FieldInfoDto(descriptor.EntityType, descriptor.DisplayName, null, table, null, null, null, false, FieldValueKinds.Text, null, false);

        var store = StoreObjectIdentifier.Table(table!, type.GetSchema());
        var reference = property.GetContainingForeignKeys().FirstOrDefault()?.PrincipalEntityType;
        return new FieldInfoDto(
            descriptor.EntityType,
            descriptor.DisplayName,
            property.Name,
            table,
            property.GetColumnName(store),
            property.GetColumnType(),
            property.GetMaxLength(),
            !property.IsNullable,
            KindOf(property),
            reference is null ? null : AuditRegistry.Find(reference.ClrType)?.DisplayName ?? reference.ClrType.Name,
            descriptor.Redact.Contains(property.Name));
    }

    private static string KindOf(IProperty property)
    {
        if (property.IsForeignKey()) return FieldValueKinds.Reference;
        var clr = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
        if (clr.IsEnum) return FieldValueKinds.Enum;
        if (clr == typeof(bool)) return FieldValueKinds.Bool;
        if (clr == typeof(DateOnly)) return FieldValueKinds.Date;
        if (clr == typeof(DateTimeOffset) || clr == typeof(DateTime)) return FieldValueKinds.DateTime;
        if (clr == typeof(decimal) || clr == typeof(int) || clr == typeof(long) || clr == typeof(short)) return FieldValueKinds.Number;
        return FieldValueKinds.Text;
    }

    /// <summary>Giá trị JSON thành chuỗi thô: chuỗi bỏ ngoặc kép, số giữ dạng bất biến (1234.5), null là null.</summary>
    private static string? Raw(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Null or JsonValueKind.Undefined => null,
        JsonValueKind.String => value.GetString(),
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        _ => value.GetRawText(),
    };

    /// <summary>
    /// Bỏ số 0 vô nghĩa của số: cột numeric(18,4) ghi 5.0000 sau khi đọc lại từ CSDL nhưng ghi 8 khi vừa nhập – cùng một giá trị.
    /// </summary>
    private static string? Normalize(string? value, string kind) =>
        kind == FieldValueKinds.Number && value is not null && decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
            ? (number / 1.000000000000000000000000000000000m).ToString(CultureInfo.InvariantCulture)
            : value;

    /// <summary>Mã – tên của bản ghi được tham chiếu; bản ghi đã xóa hoặc ngoài phạm vi người xem thì ghi rõ.</summary>
    private async Task<string?> ReferenceLabelAsync(IProperty property, string? value, CancellationToken ct)
    {
        if (value is null) return null;
        var principal = property.GetContainingForeignKeys().First().PrincipalEntityType;
        var target = await FindVisibleAsync(principal, value, checkParents: false, ct);
        return target is null ? "(không xem được)" : LabelOf(target, AuditRegistry.Find(principal.ClrType)) ?? value;
    }

    /// <summary>
    /// Mô tả bản ghi cho người đọc: "Mã – Tên" (NCC, kho, mặt hàng), số phiếu, tên đăng nhập – họ tên, hoặc thuộc tính nhãn của [Auditable].
    /// </summary>
    private static string? LabelOf(object entity, AuditDescriptor? descriptor)
    {
        string? Get(string name) => entity.GetType().GetProperty(name)?.GetValue(entity) is { } v ? Convert.ToString(v, CultureInfo.InvariantCulture) : null;
        var code = Get("Code");
        var name = Get("Name");
        if (code is not null) return name is null ? code : $"{code} – {name}";
        if (Get("UserName") is { } userName) return Get("FullName") is { } fullName ? $"{userName} – {fullName}" : userName;
        if (Get("No") is { } no) return no;
        if (descriptor?.LabelProperty is { } label && Get(label) is { } labelValue) return labelValue;
        return name;
    }

    /// <summary>
    /// Nạp bản ghi theo khóa qua DbContext có global query filter. Với dòng con, kiểm tra luôn bản ghi cha (dòng PO không có
    /// filter riêng – phạm vi đi theo đầu phiếu).
    /// </summary>
    private async Task<object?> FindVisibleAsync(IEntityType type, string entityId, bool checkParents, CancellationToken ct)
    {
        if (_cache.TryGetValue((type.ClrType, entityId), out var cached) && !checkParents) return cached;

        var key = type.FindPrimaryKey()!.Properties;
        var parts = entityId.Split('|');
        if (parts.Length != key.Count) return null;
        var values = new object[key.Count];
        for (var i = 0; i < key.Count; i++)
        {
            if (!TryConvert(parts[i], key[i].ClrType, out var converted)) return null;
            values[i] = converted;
        }

        var task = (Task<object?>)FindMethod.MakeGenericMethod(type.ClrType).Invoke(this, [key, values, ct])!;
        var entity = await task;
        _cache[(type.ClrType, entityId)] = entity;
        if (entity is null || !checkParents) return entity;

        foreach (var fk in type.GetForeignKeys())
        {
            if (fk.DeleteBehavior != DeleteBehavior.Cascade || fk.PrincipalEntityType == type || fk.Properties.Count != 1) continue;
            var parentId = fk.Properties[0].PropertyInfo?.GetValue(entity);
            if (parentId is null) continue;
            if (await FindVisibleAsync(fk.PrincipalEntityType, Convert.ToString(parentId, CultureInfo.InvariantCulture)!, true, ct) is null)
                return null;
        }

        return entity;
    }

    /// <summary>Truy vấn e => EF.Property(e, khóa1) == giá trị1 &amp;&amp; … trên DbSet có global query filter.</summary>
    private async Task<object?> FindVisibleCoreAsync<T>(IReadOnlyList<IProperty> key, object[] values, CancellationToken ct)
        where T : class
    {
        var e = Expression.Parameter(typeof(T), "e");
        Expression? body = null;
        for (var i = 0; i < key.Count; i++)
        {
            var property = Expression.Call(
                typeof(EF), nameof(EF.Property), [key[i].ClrType], e, Expression.Constant(key[i].Name));
            var equal = Expression.Equal(property, Expression.Constant(values[i], key[i].ClrType));
            body = body is null ? equal : Expression.AndAlso(body, equal);
        }

        return await _db.Set<T>().AsNoTracking().Where(Expression.Lambda<Func<T, bool>>(body!, e)).FirstOrDefaultAsync(ct);
    }

    private static bool TryConvert(string text, Type type, out object value)
    {
        var clr = Nullable.GetUnderlyingType(type) ?? type;
        value = text;
        if (clr == typeof(string)) return true;
        if (clr == typeof(Guid) && Guid.TryParse(text, out var guid)) { value = guid; return true; }
        if (clr == typeof(int) && int.TryParse(text, CultureInfo.InvariantCulture, out var number)) { value = number; return true; }
        if (clr == typeof(long) && long.TryParse(text, CultureInfo.InvariantCulture, out var big)) { value = big; return true; }
        return false;
    }
}
