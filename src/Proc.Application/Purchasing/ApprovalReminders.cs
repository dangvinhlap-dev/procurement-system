namespace Proc.Application.Purchasing;

/// <summary>Kết quả một lần quét nhắc quá hạn duyệt.</summary>
/// <param name="Steps">Số bước đã nhắc lần này.</param>
/// <param name="Recipients">Tổng số người nhận thông báo nhắc.</param>
public sealed record ApprovalReminderResult(int Steps, int Recipients);

/// <summary>
/// Nhắc người duyệt khi bước duyệt chờ quá 1 ngày làm việc (lát O2c, design.md mục 19.3 – quyết định Q9). Chạy bằng dịch vụ nền, không theo
/// người dùng: quét mọi công ty.
/// </summary>
public interface IApprovalReminderService
{
    /// <summary>
    /// Quét các bước đang đến lượt, gửi thông báo nhắc (loại <c>ApprovalOverdue</c>, kèm email bản sao nếu bật email) cho bước đã quá hạn và
    /// lưu lại lần nhắc. Ngày nghỉ (thứ 7, CN) không nhắc.
    /// </summary>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <param name="companyIds">Chỉ xét bước của các công ty này (test, chạy tay); null = mọi công ty.</param>
    /// <returns>Số bước đã nhắc và số người nhận.</returns>
    Task<ApprovalReminderResult> RemindAsync(CancellationToken cancellationToken, IReadOnlyCollection<Guid>? companyIds = null);
}
