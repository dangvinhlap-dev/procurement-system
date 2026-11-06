namespace Proc.Domain.Common;

/// <summary>
/// Entity có phiên bản dòng để chống ghi đè khi sửa đồng thời (rà soát 06/11/2026 – design.md mục 18.26, lát R2): Infrastructure ánh
/// xạ <see cref="Version"/> vào cột hệ thống <c>xmin</c> của PostgreSQL làm concurrency token. Lưu mà dòng đã bị giao dịch khác đổi
/// thì báo xung đột thay vì âm thầm ghi đè; client gửi phiên bản đã xem (header If-Match) để phát hiện cả thay đổi giữa hai lần thao tác.
/// </summary>
public interface IVersioned
{
    /// <summary>Id entity.</summary>
    Guid Id { get; }

    /// <summary>Phiên bản dòng hiện tại (đổi sau mỗi lần cập nhật dòng).</summary>
    uint Version { get; }
}
