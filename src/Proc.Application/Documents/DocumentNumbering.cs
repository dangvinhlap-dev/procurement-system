namespace Proc.Application.Documents;

/// <summary>Cấu hình đánh số một loại chứng từ tại công ty đang làm việc.</summary>
/// <param name="DocType">Mã loại chứng từ.</param>
/// <param name="Name">Tên loại chứng từ.</param>
/// <param name="Format">Mẫu số đang dùng.</param>
/// <param name="IsDefault">true khi công ty chưa cấu hình riêng, đang dùng mẫu mặc định.</param>
/// <param name="LastNumber">Số chứng từ cấp gần nhất trong kỳ hiện tại; null nếu kỳ này chưa cấp số nào.</param>
/// <param name="NextNumber">Số sẽ cấp cho chứng từ kế tiếp nếu lập hôm nay (chỉ để xem, chưa giữ chỗ).</param>
public sealed record DocumentNumberingDto(
    string DocType, string Name, string Format, bool IsDefault, string? LastNumber, string NextNumber);

/// <summary>Đổi mẫu số; Format trống = quay về mẫu mặc định.</summary>
/// <param name="Format">Mẫu số mới.</param>
public sealed record UpdateDocumentNumberingRequest(string? Format);

/// <summary>
/// Đánh số chứng từ theo công ty đang làm việc (design.md mục 5.4, 12.2).
/// </summary>
public interface IDocumentNumberService
{
    /// <summary>
    /// Cấp số kế tiếp cho một chứng từ. Gọi trong giao dịch lưu chứng từ: giao dịch hủy thì số được trả lại
    /// (không nhảy số), nhiều người lưu cùng lúc không bao giờ trùng số.
    /// </summary>
    /// <param name="docType">Mã loại chứng từ, ví dụ PO.</param>
    /// <param name="documentDate">Ngày chứng từ – quyết định năm/tháng trong số và kỳ đếm.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Số chứng từ, ví dụ PO-BHA-2610-0001.</returns>
    /// <exception cref="Domain.Common.BusinessRuleException">Khi loại chứng từ không có trong danh mục.</exception>
    /// <exception cref="Common.ForbiddenAccessException">Khi chưa chọn công ty làm việc.</exception>
    Task<string> NextAsync(string docType, DateOnly documentDate, CancellationToken cancellationToken);

    /// <summary>
    /// Danh sách mọi loại chứng từ với mẫu số đang dùng tại công ty đang làm việc.
    /// </summary>
    /// <param name="today">Ngày dùng để tính kỳ hiện tại và số kế tiếp.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Theo thứ tự trong danh mục loại chứng từ.</returns>
    Task<IReadOnlyList<DocumentNumberingDto>> ListAsync(DateOnly today, CancellationToken cancellationToken);

    /// <summary>
    /// Đổi mẫu số của một loại chứng từ tại công ty đang làm việc.
    /// </summary>
    /// <param name="docType">Mã loại chứng từ.</param>
    /// <param name="request">Mẫu mới; trống = về mặc định.</param>
    /// <param name="today">Ngày dùng để tính số kế tiếp trả về.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Cấu hình sau khi đổi.</returns>
    /// <exception cref="Domain.Common.BusinessRuleException">Khi mẫu không hợp lệ.</exception>
    /// <exception cref="Common.NotFoundException">Khi loại chứng từ không có trong danh mục.</exception>
    Task<DocumentNumberingDto> UpdateAsync(
        string docType, UpdateDocumentNumberingRequest request, DateOnly today, CancellationToken cancellationToken);
}
