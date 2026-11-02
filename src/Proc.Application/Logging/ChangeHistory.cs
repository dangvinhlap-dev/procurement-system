using Proc.Application.Common;

namespace Proc.Application.Logging;

/// <summary>
/// Kiểu giá trị của một trường, để giao diện định dạng giá trị cũ/mới đúng quy ước Việt Nam (CLAUDE.md mục 5):
/// text, number, date (dd/MM/yyyy), datetime (dd/MM/yyyy HH:mm giờ Việt Nam), bool (Có/Không), enum (nhãn tiếng Việt ở giao diện),
/// reference (mã nội bộ đã đổi sang mã – tên).
/// </summary>
public static class FieldValueKinds
{
    /// <summary>Chuỗi.</summary>
    public const string Text = "text";

    /// <summary>Số (số lượng, đơn giá, tiền, phần trăm).</summary>
    public const string Number = "number";

    /// <summary>Ngày (kiểu date).</summary>
    public const string Date = "date";

    /// <summary>Thời điểm (UTC).</summary>
    public const string DateTime = "datetime";

    /// <summary>Có/không.</summary>
    public const string Bool = "bool";

    /// <summary>Giá trị liệt kê (trạng thái, loại…).</summary>
    public const string Enum = "enum";

    /// <summary>Tham chiếu tới bản ghi khác (NCC, kho, mặt hàng…).</summary>
    public const string Reference = "reference";
}

/// <summary>
/// Thông tin kỹ thuật của một ô – tab "Thông tin ô" của hộp thoại F1, như "Thông tin kỹ thuật" của ERP (design.md mục 9.3a).
/// </summary>
/// <param name="EntityType">Mã loại đối tượng, ví dụ PurchaseOrder.</param>
/// <param name="EntityName">Tên loại đối tượng tiếng Việt.</param>
/// <param name="Field">Tên trường (thuộc tính) đã chuẩn hóa, ví dụ SupplierId; null khi ô không lưu thẳng vào bản ghi.</param>
/// <param name="Table">Bảng CSDL.</param>
/// <param name="Column">Cột CSDL.</param>
/// <param name="DataType">Kiểu dữ liệu CSDL, ví dụ numeric(18,4).</param>
/// <param name="MaxLength">Độ dài tối đa (chuỗi).</param>
/// <param name="Required">Bắt buộc có giá trị.</param>
/// <param name="Kind">Kiểu giá trị (<see cref="FieldValueKinds"/>).</param>
/// <param name="ReferenceName">Tên loại đối tượng được tham chiếu (với ô tham chiếu), ví dụ "Nhà cung cấp".</param>
/// <param name="Redacted">Trường bí mật: nhật ký chỉ ghi là đã đổi, không ghi giá trị.</param>
public sealed record FieldInfoDto(
    string EntityType,
    string EntityName,
    string? Field,
    string? Table,
    string? Column,
    string? DataType,
    int? MaxLength,
    bool Required,
    string Kind,
    string? ReferenceName,
    bool Redacted);

/// <summary>
/// Người và thời điểm tạo/sửa gần nhất của bản ghi.
/// </summary>
/// <param name="Label">Mô tả bản ghi (mã, số phiếu…).</param>
/// <param name="CreatedAt">Thời điểm tạo (UTC), nếu biết.</param>
/// <param name="CreatedBy">Người tạo (tên đăng nhập – họ tên); null = hệ thống hoặc không rõ.</param>
/// <param name="UpdatedAt">Thời điểm sửa gần nhất (UTC).</param>
/// <param name="UpdatedBy">Người sửa gần nhất.</param>
public sealed record RecordStampDto(string? Label, DateTimeOffset? CreatedAt, string? CreatedBy, DateTimeOffset? UpdatedAt, string? UpdatedBy);

/// <summary>
/// Một lần thay đổi giá trị của một trường.
/// </summary>
/// <param name="LogId">Id dòng nhật ký.</param>
/// <param name="OccurredAt">Thời điểm (UTC).</param>
/// <param name="UserName">Tên đăng nhập; null = hệ thống (khởi tạo dữ liệu, job nền).</param>
/// <param name="UserFullName">Họ tên người thao tác, nếu tìm được.</param>
/// <param name="Action">Create (tạo), Update (sửa), Delete (xóa bản ghi).</param>
/// <param name="EntityType">Mã loại đối tượng của bản ghi đổi (đầu phiếu hoặc dòng).</param>
/// <param name="EntityName">Tên loại đối tượng tiếng Việt.</param>
/// <param name="EntityId">Khóa bản ghi.</param>
/// <param name="EntityLabel">Mô tả bản ghi lúc thay đổi.</param>
/// <param name="LineNo">Số dòng (với dòng chứng từ có số dòng).</param>
/// <param name="Field">Tên trường.</param>
/// <param name="Kind">Kiểu giá trị.</param>
/// <param name="OldValue">Giá trị cũ dạng thô (số, ngày ISO, tên giá trị liệt kê…); null = trống.</param>
/// <param name="NewValue">Giá trị mới dạng thô.</param>
/// <param name="OldText">Với ô tham chiếu: mã – tên của giá trị cũ.</param>
/// <param name="NewText">Với ô tham chiếu: mã – tên của giá trị mới.</param>
public sealed record FieldChangeDto(
    Guid LogId,
    DateTimeOffset OccurredAt,
    string? UserName,
    string? UserFullName,
    string Action,
    string EntityType,
    string EntityName,
    string EntityId,
    string? EntityLabel,
    int? LineNo,
    string Field,
    string Kind,
    string? OldValue,
    string? NewValue,
    string? OldText,
    string? NewText);

/// <summary>
/// Kết quả F1 trên một ô: thông tin ô, người tạo/sửa bản ghi và lịch sử giá trị của ô (mới nhất trước).
/// </summary>
/// <param name="Info">Thông tin kỹ thuật.</param>
/// <param name="Record">Người, thời điểm tạo/sửa bản ghi.</param>
/// <param name="Changes">Các lần đổi giá trị của ô.</param>
/// <param name="Truncated">Có nhiều hơn số dòng tối đa.</param>
public sealed record FieldHistoryDto(FieldInfoDto Info, RecordStampDto Record, IReadOnlyList<FieldChangeDto> Changes, bool Truncated);

/// <summary>
/// Lịch sử thay đổi cả bản ghi kèm các dòng con (cả dòng đã xóa) – "Hiển thị thay đổi" của chứng từ.
/// </summary>
/// <param name="EntityType">Mã loại đối tượng.</param>
/// <param name="EntityName">Tên loại đối tượng.</param>
/// <param name="Record">Người, thời điểm tạo/sửa bản ghi.</param>
/// <param name="Changes">Từng trường thay đổi, mới nhất trước.</param>
/// <param name="Truncated">Có nhiều hơn số dòng tối đa.</param>
public sealed record RecordHistoryDto(
    string EntityType,
    string EntityName,
    RecordStampDto Record,
    IReadOnlyList<FieldChangeDto> Changes,
    bool Truncated);

/// <summary>
/// Tra lịch sử thay đổi theo ô và theo bản ghi (phím F1, "Lịch sử thay đổi" – design.md mục 9.3a). Ai đăng nhập cũng xem được,
/// nhưng chỉ của bản ghi mình xem được (phạm vi công ty, kho, nhóm hàng – CLAUDE.md mục 4).
/// </summary>
public interface IChangeHistoryQueries
{
    /// <summary>
    /// Lịch sử giá trị của một ô.
    /// </summary>
    /// <param name="entityType">Mã loại đối tượng, ví dụ PurchaseOrderLine.</param>
    /// <param name="entityId">Khóa bản ghi.</param>
    /// <param name="field">Tên ô ở giao diện hoặc tên trường, không phân biệt hoa thường (supplier, supplierId, SupplierId).</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Thông tin ô và lịch sử.</returns>
    /// <exception cref="NotFoundException">Loại đối tượng không có nhật ký hoặc bản ghi không tồn tại/ngoài phạm vi.</exception>
    Task<FieldHistoryDto> GetFieldHistoryAsync(string entityType, string entityId, string field, CancellationToken cancellationToken);

    /// <summary>
    /// Lịch sử thay đổi cả bản ghi và các dòng con (kể cả dòng đã xóa).
    /// </summary>
    /// <param name="entityType">Mã loại đối tượng, ví dụ PurchaseOrder.</param>
    /// <param name="entityId">Khóa bản ghi.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Lịch sử.</returns>
    /// <exception cref="NotFoundException">Loại đối tượng không có nhật ký hoặc bản ghi không tồn tại/ngoài phạm vi.</exception>
    Task<RecordHistoryDto> GetRecordHistoryAsync(string entityType, string entityId, CancellationToken cancellationToken);
}
