namespace Proc.Infrastructure.Logging;

/// <summary>
/// Một dòng nhật ký thao tác dữ liệu (design.md mục 9.3). Chỉ thêm: trigger trong CSDL chặn UPDATE/DELETE.
/// Bảng phân vùng theo tháng của <see cref="OccurredAt"/> nên khóa chính gồm (Id, OccurredAt) – PostgreSQL
/// bắt buộc khóa chính chứa cột phân vùng.
/// </summary>
public sealed class AuditLog
{
    /// <summary>Id (uuid v7).</summary>
    public Guid Id { get; init; } = Guid.CreateVersion7();

    /// <summary>Thời điểm (UTC), cũng là cột phân vùng.</summary>
    public DateTimeOffset OccurredAt { get; init; }

    /// <summary>Người thao tác; null = hệ thống (khởi tạo dữ liệu, job nền).</summary>
    public Guid? UserId { get; init; }

    /// <summary>Tên đăng nhập lúc thao tác.</summary>
    public string? UserName { get; init; }

    /// <summary>Công ty đang làm việc lúc thao tác.</summary>
    public Guid? CompanyId { get; init; }

    /// <summary>IP máy người dùng.</summary>
    public string? IpAddress { get; init; }

    /// <summary>Trình duyệt.</summary>
    public string? UserAgent { get; init; }

    /// <summary>Mã liên kết request.</summary>
    public string? CorrelationId { get; init; }

    /// <summary>Mã loại đối tượng, ví dụ Role.</summary>
    public string EntityType { get; init; } = string.Empty;

    /// <summary>Khóa bản ghi; khóa ghép nối bằng dấu "|".</summary>
    public string EntityId { get; init; } = string.Empty;

    /// <summary>Mô tả bản ghi (mã, tên đăng nhập…).</summary>
    public string? EntityLabel { get; init; }

    /// <summary>Số chứng từ, với entity có số chứng từ.</summary>
    public string? DocumentNumber { get; init; }

    /// <summary>Hành động: Create, Update, Delete (sau này thêm Submit, Approve, Post…).</summary>
    public string Action { get; init; } = string.Empty;

    /// <summary>Giá trị cũ và mới dạng JSON: {"Tên trường": {"old": …, "new": …}} (jsonb).</summary>
    public string Changes { get; init; } = "{}";
}

/// <summary>
/// Một dòng nhật ký truy cập: đăng nhập/đăng xuất, đăng nhập sai, đổi mật khẩu, đổi công ty (design.md mục 9.3).
/// Chỉ thêm như AuditLog.
/// </summary>
public sealed class AccessLog
{
    /// <summary>Id (uuid v7).</summary>
    public Guid Id { get; init; } = Guid.CreateVersion7();

    /// <summary>Thời điểm (UTC).</summary>
    public DateTimeOffset OccurredAt { get; init; }

    /// <summary>Sự kiện (tên của AccessEvent).</summary>
    public string Event { get; init; } = string.Empty;

    /// <summary>Tài khoản; null khi tên đăng nhập không tồn tại.</summary>
    public Guid? UserId { get; init; }

    /// <summary>Tên đăng nhập (với đăng nhập sai là tên đã gõ).</summary>
    public string? UserName { get; init; }

    /// <summary>Công ty liên quan.</summary>
    public Guid? CompanyId { get; init; }

    /// <summary>Ghi chú.</summary>
    public string? Detail { get; init; }

    /// <summary>IP.</summary>
    public string? IpAddress { get; init; }

    /// <summary>Trình duyệt.</summary>
    public string? UserAgent { get; init; }

    /// <summary>Mã liên kết request.</summary>
    public string? CorrelationId { get; init; }
}
