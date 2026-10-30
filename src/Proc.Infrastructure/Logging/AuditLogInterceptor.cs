using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Proc.Application.Common;
using Proc.Domain.Common;

namespace Proc.Infrastructure.Logging;

/// <summary>
/// Ghi AuditLog cho mọi thay đổi của entity có [Auditable] (CLAUDE.md mục 4, design.md mục 9.3).
/// Dòng nhật ký được thêm vào chính lần SaveChanges đang chạy, nên lưu cùng một giao dịch với thay đổi:
/// thay đổi thất bại thì không có nhật ký, có thay đổi thì chắc chắn có nhật ký.
/// Đăng ký sau AuditFieldsInterceptor (thứ tự interceptor là thứ tự đăng ký).
/// </summary>
public sealed class AuditLogInterceptor : SaveChangesInterceptor
{
    /// <summary>Giá trị thay cho thuộc tính bí mật (mã băm mật khẩu).</summary>
    public const string RedactedValue = "***";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly ICurrentUser _currentUser;
    private readonly ICurrentUserScope _scope;
    private readonly IRequestContext _request;
    private readonly IClock _clock;

    /// <summary>
    /// Tạo interceptor.
    /// </summary>
    /// <param name="currentUser">Người thao tác.</param>
    /// <param name="scope">Phạm vi (công ty đang làm việc).</param>
    /// <param name="request">IP, trình duyệt, mã liên kết.</param>
    /// <param name="clock">Đồng hồ.</param>
    public AuditLogInterceptor(ICurrentUser currentUser, ICurrentUserScope scope, IRequestContext request, IClock clock)
    {
        _currentUser = currentUser;
        _scope = scope;
        _request = request;
        _clock = clock;
    }

    /// <inheritdoc />
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        AddAuditLogs(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    /// <inheritdoc />
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        AddAuditLogs(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void AddAuditLogs(DbContext? context)
    {
        if (context is null) return;
        var now = _clock.UtcNow;

        // ToList: ChangeTracker.Entries() tự DetectChanges; chép ra danh sách trước khi thêm dòng AuditLog vào context.
        var entries = context.ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Select(e => (Entry: e, Descriptor: AuditRegistry.Find(e.Metadata.ClrType)))
            .Where(x => x.Descriptor is not null)
            .ToList();

        foreach (var (entry, descriptor) in entries)
        {
            var changes = BuildChanges(entry, descriptor!);
            // Chỉ đổi cột bị bỏ qua (ví dụ số lần nhập sai mật khẩu) thì không ghi.
            if (changes.Count == 0) continue;

            context.Add(new AuditLog
            {
                OccurredAt = now,
                UserId = _currentUser.UserId,
                UserName = _currentUser.UserName,
                CompanyId = _scope.CompanyId,
                IpAddress = _request.IpAddress,
                UserAgent = _request.UserAgent,
                CorrelationId = _request.CorrelationId,
                EntityType = descriptor!.EntityType,
                EntityId = KeyOf(entry),
                EntityLabel = descriptor.LabelProperty is { } label ? ValueOf(entry, label)?.ToString() : null,
                DocumentNumber = (entry.Entity as IHasDocumentNumber)?.DocumentNumber,
                Action = entry.State switch
                {
                    EntityState.Added => "Create",
                    EntityState.Deleted => "Delete",
                    _ => "Update",
                },
                Changes = changes.ToJsonString(JsonOptions),
            });
        }
    }

    /// <summary>
    /// Giá trị cũ/mới theo từng trường: tạo mới ghi mọi trường có giá trị (old = null), xóa ghi mọi trường cũ (new = null),
    /// sửa chỉ ghi trường thật sự đổi giá trị. Trường bí mật ghi "***" thay cho giá trị.
    /// Ví dụ đổi tên vai trò: {"Name": {"old": "Thủ kho", "new": "Thủ kho chính"}}.
    /// </summary>
    private static JsonObject BuildChanges(EntityEntry entry, AuditDescriptor descriptor)
    {
        var changes = new JsonObject();
        foreach (var property in entry.Properties)
        {
            var name = property.Metadata.Name;
            if (descriptor.Ignore.Contains(name)) continue;

            object? oldValue = entry.State == EntityState.Added ? null : property.OriginalValue;
            object? newValue = entry.State == EntityState.Deleted ? null : property.CurrentValue;
            if (entry.State == EntityState.Modified && (!property.IsModified || Equals(oldValue, newValue))) continue;
            if (entry.State != EntityState.Modified && oldValue is null && newValue is null) continue;

            if (descriptor.Redact.Contains(name))
            {
                oldValue = oldValue is null ? null : RedactedValue;
                newValue = newValue is null ? null : RedactedValue;
            }

            changes[name] = new JsonObject
            {
                ["old"] = JsonSerializer.SerializeToNode(oldValue, JsonOptions),
                ["new"] = JsonSerializer.SerializeToNode(newValue, JsonOptions),
            };
        }

        return changes;
    }

    private static string KeyOf(EntityEntry entry) =>
        string.Join("|", entry.Metadata.FindPrimaryKey()!.Properties.Select(p => entry.Property(p.Name).CurrentValue));

    private static object? ValueOf(EntityEntry entry, string propertyName)
    {
        // Nhãn có thể là thuộc tính tính toán không lưu CSDL (kỳ kho: "10/2026" ghép từ Month, Year) – đọc thẳng từ đối tượng.
        if (entry.Metadata.FindProperty(propertyName) is null)
            return entry.Entity.GetType().GetProperty(propertyName)?.GetValue(entry.Entity);
        var property = entry.Property(propertyName);
        return entry.State == EntityState.Deleted ? property.OriginalValue : property.CurrentValue;
    }
}
