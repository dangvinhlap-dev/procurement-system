namespace Proc.Application.Portal;

/// <summary>
/// Một thư mời báo giá NCC thấy trên Portal (lát P3a). Status: "Open" đang nhận báo giá, "Closed" đã hết giờ nộp – chờ mở, "Reviewing" đã mở /
/// đang xét chọn, "Awarded" đã có kết quả, "Cancelled" đã hủy.
/// </summary>
/// <param name="Id">RFQ.</param>
/// <param name="No">Số RFQ.</param>
/// <param name="CompanyName">Công ty mời báo giá.</param>
/// <param name="Title">Tiêu đề.</param>
/// <param name="Date">Ngày lập.</param>
/// <param name="DeadlineAt">Hạn nộp (giờ Việt Nam, ngày + giờ).</param>
/// <param name="Status">Trạng thái hiển thị cho NCC.</param>
/// <param name="LineCount">Số dòng hàng.</param>
/// <param name="SubmissionCount">Số lần NCC đã nộp.</param>
/// <param name="LastSubmittedAt">Lần nộp cuối.</param>
public sealed record PortalRfqListItemDto(
    Guid Id, string No, string CompanyName, string Title, DateOnly Date, DateTime DeadlineAt, string Status, int LineCount, int SubmissionCount,
    DateTimeOffset? LastSubmittedAt);

/// <summary>Dòng hàng của RFQ trên Portal (không có giá NCC khác).</summary>
/// <param name="Id">Dòng RFQ.</param>
/// <param name="LineNo">STT.</param>
/// <param name="ItemCode">Mã hàng.</param>
/// <param name="ItemName">Tên hàng.</param>
/// <param name="SupplierItemCode">Mã hàng của chính NCC (nếu đã khai báo).</param>
/// <param name="UomCode">ĐVT.</param>
/// <param name="Qty">Số lượng.</param>
/// <param name="Note">Ghi chú dòng.</param>
public sealed record PortalRfqLineDto(
    Guid Id, int LineNo, string ItemCode, string ItemName, string? SupplierItemCode, string UomCode, decimal Qty, string? Note);

/// <summary>Giá một dòng NCC đã nộp; Amount = SL × đơn giá × (100 − %CK)/100, làm tròn 2 số.</summary>
/// <param name="RfqLineId">Dòng RFQ.</param>
/// <param name="UnitPrice">Đơn giá trước VAT.</param>
/// <param name="DiscountPct">% chiết khấu.</param>
/// <param name="VatCode">Thuế suất.</param>
/// <param name="LeadTimeDays">Thời gian giao (ngày).</param>
/// <param name="Note">Ghi chú.</param>
/// <param name="Amount">Thành tiền.</param>
public sealed record PortalQuoteLineDto(
    Guid RfqLineId, decimal UnitPrice, decimal DiscountPct, string? VatCode, int? LeadTimeDays, string? Note, decimal Amount);

/// <summary>Một lần nộp của NCC (biên nhận).</summary>
/// <param name="Sequence">Lần nộp.</param>
/// <param name="SubmittedAt">Thời điểm nộp.</param>
/// <param name="SubmittedByEmail">Người nộp.</param>
/// <param name="ContentHash">Mã băm SHA-256 (biên nhận).</param>
/// <param name="TotalAmount">Tổng tiền trước VAT.</param>
public sealed record PortalSubmissionDto(int Sequence, DateTimeOffset SubmittedAt, string SubmittedByEmail, string ContentHash, decimal TotalAmount);

/// <summary>Bản nộp cuối của NCC (để xem lại và nộp lại).</summary>
/// <param name="Sequence">Lần nộp.</param>
/// <param name="SubmittedAt">Thời điểm nộp.</param>
/// <param name="ValidUntil">Hiệu lực đến.</param>
/// <param name="PaymentTerms">Điều kiện thanh toán.</param>
/// <param name="Note">Ghi chú.</param>
/// <param name="ContentHash">Mã băm.</param>
/// <param name="Lines">Giá từng dòng.</param>
public sealed record PortalLatestQuoteDto(
    int Sequence, DateTimeOffset SubmittedAt, DateOnly? ValidUntil, string? PaymentTerms, string? Note, string ContentHash,
    IReadOnlyList<PortalQuoteLineDto> Lines);

/// <summary>Chi tiết RFQ cho NCC.</summary>
/// <param name="Id">RFQ.</param>
/// <param name="No">Số.</param>
/// <param name="CompanyName">Công ty mời.</param>
/// <param name="Title">Tiêu đề.</param>
/// <param name="Date">Ngày lập.</param>
/// <param name="DeadlineAt">Hạn nộp (giờ Việt Nam).</param>
/// <param name="Note">Ghi chú của bên mời.</param>
/// <param name="Status">Trạng thái hiển thị (như danh sách).</param>
/// <param name="CanSubmit">Còn nộp / nộp lại được.</param>
/// <param name="ClosedReason">Lý do không nộp được (hết hạn, đã mở, đã hủy…).</param>
/// <param name="VatCodes">Các thuế suất chọn được.</param>
/// <param name="Lines">Dòng hàng.</param>
/// <param name="Latest">Bản nộp cuối (null = chưa nộp).</param>
/// <param name="Submissions">Lịch sử các lần nộp, mới nhất trước.</param>
public sealed record PortalRfqDto(
    Guid Id, string No, string CompanyName, string Title, DateOnly Date, DateTime DeadlineAt, string? Note, string Status, bool CanSubmit,
    string? ClosedReason, IReadOnlyList<string> VatCodes, IReadOnlyList<PortalRfqLineDto> Lines, PortalLatestQuoteDto? Latest,
    IReadOnlyList<PortalSubmissionDto> Submissions);

/// <summary>Giá một dòng NCC nộp.</summary>
/// <param name="RfqLineId">Dòng RFQ.</param>
/// <param name="UnitPrice">Đơn giá trước VAT (theo ĐVT của dòng).</param>
/// <param name="DiscountPct">% chiết khấu.</param>
/// <param name="VatCode">Thuế suất.</param>
/// <param name="LeadTimeDays">Thời gian giao (ngày).</param>
/// <param name="Note">Ghi chú.</param>
public sealed record PortalQuoteLineRequest(Guid RfqLineId, decimal UnitPrice, decimal DiscountPct, string? VatCode, int? LeadTimeDays, string? Note);

/// <summary>NCC nộp báo giá; dòng không gửi = không chào dòng đó.</summary>
/// <param name="ValidUntil">Hiệu lực đến.</param>
/// <param name="PaymentTerms">Điều kiện thanh toán.</param>
/// <param name="Note">Ghi chú.</param>
/// <param name="Lines">Giá từng dòng.</param>
public sealed record PortalSubmitQuoteRequest(DateOnly? ValidUntil, string? PaymentTerms, string? Note, IReadOnlyList<PortalQuoteLineRequest> Lines);

/// <summary>
/// Thư mời và báo giá online của NCC trên Portal (lát P3a – design.md mục 19.5). NCC lấy từ phiên Portal; chỉ thấy RFQ đã gửi có mời mình,
/// chỉ thấy báo giá của mình.
/// </summary>
public interface IPortalRfqService
{
    /// <summary>Thư mời báo giá của NCC, mới nhất trước.</summary>
    /// <param name="session">Phiên Portal.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Danh sách.</returns>
    Task<IReadOnlyList<PortalRfqListItemDto>> ListAsync(PortalSession session, CancellationToken cancellationToken);

    /// <summary>Chi tiết một thư mời.</summary>
    /// <param name="session">Phiên Portal.</param>
    /// <param name="id">RFQ.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Chi tiết.</returns>
    /// <exception cref="Common.NotFoundException">Khi RFQ không mời NCC này (không lộ RFQ có tồn tại hay không).</exception>
    Task<PortalRfqDto> GetAsync(PortalSession session, Guid id, CancellationToken cancellationToken);

    /// <summary>Nộp (hoặc nộp lại) báo giá – bản mới niêm phong đến khi Phòng CUVT mở sau giờ hạn nộp.</summary>
    /// <param name="session">Phiên Portal.</param>
    /// <param name="id">RFQ.</param>
    /// <param name="request">Báo giá.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Chi tiết sau khi nộp (kèm biên nhận).</returns>
    /// <exception cref="Common.NotFoundException">Khi RFQ không mời NCC này.</exception>
    /// <exception cref="Domain.Common.BusinessRuleException">Khi hết hạn, đã mở, dữ liệu giá sai.</exception>
    Task<PortalRfqDto> SubmitAsync(PortalSession session, Guid id, PortalSubmitQuoteRequest request, CancellationToken cancellationToken);
}
