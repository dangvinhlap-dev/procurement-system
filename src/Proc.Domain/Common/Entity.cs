namespace Proc.Domain.Common;

/// <summary>
/// Lớp gốc của mọi entity: khóa chính uuid v7 sinh ngay khi tạo đối tượng.
/// uuid v7 tăng dần theo thời gian nên chỉ mục B-tree không bị phân mảnh như uuid ngẫu nhiên (design.md mục 12.2).
/// </summary>
public abstract class Entity
{
    /// <summary>Khóa chính.</summary>
    public Guid Id { get; protected set; } = Guid.CreateVersion7();
}

/// <summary>
/// Entity có thông tin người tạo/sửa (design.md mục 5: mọi danh mục có CreatedBy/At, UpdatedBy/At).
/// Các trường này do interceptor của Infrastructure tự điền khi lưu, code nghiệp vụ không gán tay.
/// </summary>
public abstract class AuditableEntity : Entity
{
    /// <summary>Thời điểm tạo (UTC).</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Người tạo; null khi do hệ thống tạo (dữ liệu khởi tạo, job nền).</summary>
    public Guid? CreatedBy { get; set; }

    /// <summary>Thời điểm sửa gần nhất (UTC).</summary>
    public DateTimeOffset? UpdatedAt { get; set; }

    /// <summary>Người sửa gần nhất.</summary>
    public Guid? UpdatedBy { get; set; }
}
