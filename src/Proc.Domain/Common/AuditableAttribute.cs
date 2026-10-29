namespace Proc.Domain.Common;

/// <summary>
/// Đánh dấu entity mà mọi thay đổi (tạo, sửa, xóa) phải ghi vào AuditLog kèm giá trị cũ và mới
/// (CLAUDE.md mục 4, design.md mục 9.3). Việc ghi do SaveChangesInterceptor của Infrastructure làm tự động,
/// trong cùng giao dịch với thay đổi, nên code nghiệp vụ không phải gọi gì thêm.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class AuditableAttribute : Attribute
{
    /// <summary>
    /// Đánh dấu entity cần ghi nhật ký.
    /// </summary>
    /// <param name="displayName">Tên loại đối tượng hiển thị trên màn hình Nhật ký, ví dụ "Vai trò".</param>
    public AuditableAttribute(string displayName) => DisplayName = displayName;

    /// <summary>Tên loại đối tượng hiển thị trên màn hình Nhật ký.</summary>
    public string DisplayName { get; }

    /// <summary>
    /// Mã loại đối tượng lưu trong AuditLog; mặc định là tên lớp. Đặt riêng khi tên lớp không nói lên nghiệp vụ
    /// (ví dụ AppUser → "User").
    /// </summary>
    public string? EntityType { get; init; }

    /// <summary>Thuộc tính mô tả bản ghi cho người đọc (ví dụ Code), hiện ở cột "Đối tượng".</summary>
    public string? LabelProperty { get; init; }

    /// <summary>Thuộc tính không ghi vào nhật ký (cột kỹ thuật, thay đổi liên tục, không có ý nghĩa nghiệp vụ).</summary>
    public string[] Ignore { get; init; } = [];

    /// <summary>Thuộc tính bí mật: chỉ ghi là đã đổi ("***"), không ghi giá trị (ví dụ mã băm mật khẩu).</summary>
    public string[] Redact { get; init; } = [];
}

/// <summary>
/// Entity có số chứng từ (PO-{CTY}-{YYMM}-{0000}…): AuditLog ghi kèm số để tìm theo số chứng từ (design.md mục 9.3).
/// </summary>
public interface IHasDocumentNumber
{
    /// <summary>Số chứng từ; null khi chưa cấp số (phiếu nháp).</summary>
    string? DocumentNumber { get; }
}
