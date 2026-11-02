using System.Text.Json.Serialization;

namespace Proc.Application.Documents;

/// <summary>Tình trạng chung của một chứng từ trên màn hình Tra cứu chứng từ (design.md mục 18.22).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<DocumentProgress>))]
public enum DocumentProgress
{
    /// <summary>Chưa hoàn thành: nháp, chờ duyệt, đang nhận báo giá, đang mở…</summary>
    Open = 1,

    /// <summary>Hoàn thành: đã ghi sổ, đã duyệt, hiệu lực, đã đóng, đã kết luận…</summary>
    Done = 2,

    /// <summary>Đã hủy hoặc bị từ chối.</summary>
    Cancelled = 3,
}

/// <summary>Bộ lọc màn hình Tra cứu chứng từ.</summary>
/// <param name="From">Từ ngày chứng từ.</param>
/// <param name="To">Đến ngày chứng từ.</param>
/// <param name="DocTypes">Các loại chứng từ (rỗng = mọi loại được xem).</param>
/// <param name="Progress">Tình trạng (null = tất cả).</param>
/// <param name="Mine">"created": tôi lập; "acted": tôi đã duyệt/từ chối; null: mọi người.</param>
public sealed record DocumentRegistryQuery(
    DateOnly From, DateOnly To, IReadOnlyList<string> DocTypes, DocumentProgress? Progress, string? Mine);

/// <summary>Một chứng từ trên màn hình Tra cứu chứng từ.</summary>
/// <param name="DocType">Mã loại (PR, PO, GRN, RTV, TRF, ICS, ICR, ADJ, PRL, SRC, RFQ, CTR, CTA, NCC, NDQ, DGN, VP).</param>
/// <param name="Id">Id chứng từ.</param>
/// <param name="No">Số chứng từ.</param>
/// <param name="Date">Ngày chứng từ (loại không có ngày chứng từ thì lấy ngày lập theo giờ Việt Nam).</param>
/// <param name="Partner">Đối tác: NCC, hoặc kho/công ty đối ứng của phiếu kho; null khi không có.</param>
/// <param name="Description">Nội dung: tiêu đề, ghi chú, mục đích.</param>
/// <param name="Amount">Giá trị (VND) khi loại có; null khi không có.</param>
/// <param name="Status">Mã trạng thái gốc của loại (Draft, Posted, Submitted…).</param>
/// <param name="StatusName">Tên trạng thái tiếng Việt.</param>
/// <param name="Progress">Tình trạng chung.</param>
/// <param name="CreatedByName">Người lập.</param>
/// <param name="CreatedAt">Thời điểm lập.</param>
/// <param name="PendingLevel">Cấp đang chờ duyệt (lượt gửi mới nhất); null khi không chờ duyệt.</param>
public sealed record DocumentRegistryRowDto(
    string DocType, Guid Id, string No, DateOnly Date, string? Partner, string? Description, decimal? Amount, string Status,
    string StatusName, DocumentProgress Progress, string? CreatedByName, DateTimeOffset CreatedAt, int? PendingLevel);

/// <summary>Kết quả tra cứu.</summary>
/// <param name="Rows">Các chứng từ, mới nhất trước.</param>
/// <param name="DocTypes">Các loại người dùng được xem (để hiện ô lọc loại).</param>
/// <param name="Truncated">true khi vượt giới hạn dòng – thu hẹp khoảng ngày.</param>
/// <param name="Limit">Giới hạn dòng.</param>
public sealed record DocumentRegistryResult(
    IReadOnlyList<DocumentRegistryRowDto> Rows, IReadOnlyList<string> DocTypes, bool Truncated, int Limit);

/// <summary>
/// Tra cứu mọi loại chứng từ của công ty đang làm việc (yêu cầu người dùng 02/11/2026 – design.md mục 18.22): đã hoàn thành hoặc chưa,
/// kể cả phiếu kho; mỗi loại chỉ hiện khi người dùng có quyền xem loại đó, theo đúng phạm vi công ty/kho (global query filter).
/// </summary>
public interface IDocumentRegistryService
{
    /// <summary>Tra cứu chứng từ.</summary>
    /// <param name="query">Bộ lọc.</param>
    /// <param name="cancellationToken">Hủy.</param>
    /// <returns>Các chứng từ thỏa bộ lọc.</returns>
    /// <exception cref="Proc.Domain.Common.BusinessRuleException">Khoảng ngày sai hoặc quá 366 ngày.</exception>
    Task<DocumentRegistryResult> SearchAsync(DocumentRegistryQuery query, CancellationToken cancellationToken);
}
