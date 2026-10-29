using System.Collections.Concurrent;
using System.Reflection;
using Proc.Domain.Common;

namespace Proc.Infrastructure.Logging;

/// <summary>Cách ghi nhật ký của một loại entity, đọc từ <see cref="AuditableAttribute"/>.</summary>
/// <param name="EntityType">Mã loại đối tượng lưu trong AuditLog.</param>
/// <param name="DisplayName">Tên tiếng Việt.</param>
/// <param name="LabelProperty">Thuộc tính mô tả bản ghi.</param>
/// <param name="Ignore">Thuộc tính bỏ qua.</param>
/// <param name="Redact">Thuộc tính chỉ ghi là đã đổi.</param>
internal sealed record AuditDescriptor(
    string EntityType,
    string DisplayName,
    string? LabelProperty,
    IReadOnlySet<string> Ignore,
    IReadOnlySet<string> Redact);

/// <summary>
/// Danh mục entity có [Auditable]: tra theo kiểu CLR khi ghi, theo mã loại khi hiển thị.
/// </summary>
internal static class AuditRegistry
{
    /// <summary>
    /// Không ghi lại: Id (đã có ở cột EntityId) và cột người tạo/sửa của AuditableEntity
    /// (trùng với người và thời điểm của chính dòng nhật ký).
    /// </summary>
    private static readonly string[] AlwaysIgnored =
    [
        nameof(Entity.Id),
        nameof(AuditableEntity.CreatedAt), nameof(AuditableEntity.CreatedBy),
        nameof(AuditableEntity.UpdatedAt), nameof(AuditableEntity.UpdatedBy),
    ];

    private static readonly ConcurrentDictionary<Type, AuditDescriptor?> ByType = new();

    private static readonly Lazy<IReadOnlyDictionary<string, string>> DisplayNames = new(() =>
        new[] { typeof(Entity).Assembly, typeof(AuditRegistry).Assembly }
            .SelectMany(a => a.GetTypes())
            .Select(Find)
            .OfType<AuditDescriptor>()
            .ToDictionary(d => d.EntityType, d => d.DisplayName));

    /// <summary>
    /// Lấy cách ghi nhật ký của một kiểu entity.
    /// </summary>
    /// <param name="type">Kiểu CLR.</param>
    /// <returns>Mô tả, hoặc null nếu kiểu không có [Auditable].</returns>
    public static AuditDescriptor? Find(Type type) =>
        ByType.GetOrAdd(type, t => t.GetCustomAttribute<AuditableAttribute>() is { } a
            ? new AuditDescriptor(
                a.EntityType ?? t.Name,
                a.DisplayName,
                a.LabelProperty,
                a.Ignore.Concat(AlwaysIgnored).ToHashSet(StringComparer.Ordinal),
                a.Redact.ToHashSet(StringComparer.Ordinal))
            : null);

    /// <summary>
    /// Tên tiếng Việt của loại đối tượng.
    /// </summary>
    /// <param name="entityType">Mã loại lưu trong AuditLog.</param>
    /// <returns>Tên tiếng Việt, hoặc chính mã nếu không tìm thấy (loại đã bỏ ở bản sau).</returns>
    public static string DisplayName(string entityType) =>
        DisplayNames.Value.TryGetValue(entityType, out var name) ? name : entityType;
}
