namespace Proc.Application.Printing;

/// <summary>File PDF trả về để xem, in.</summary>
/// <param name="FileName">Tên file gợi ý, ví dụ PO-BHA-2610-0001.pdf.</param>
/// <param name="Content">Nội dung file.</param>
public sealed record PdfFile(string FileName, byte[] Content);

/// <summary>
/// In phiếu ra PDF (design.md mục 10.10). Dữ liệu lấy qua đúng dịch vụ xem phiếu của từng module nên phạm vi công ty, kho và quyền xem giữ
/// nguyên như khi mở phiếu trên màn hình.
/// </summary>
public interface IDocumentPdfService
{
    /// <summary>In đơn mua hàng (gửi NCC).</summary>
    /// <param name="id">PO.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>File PDF.</returns>
    /// <exception cref="Common.NotFoundException">Khi không có phiếu hoặc phiếu ngoài phạm vi.</exception>
    Task<PdfFile> PurchaseOrderAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>In đề nghị mua hàng theo biểu mẫu CUVT: loại A → 1A, loại B → 1B, dịch vụ → 1B không cột tồn (lát Y10).</summary>
    /// <param name="id">PR.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>File PDF.</returns>
    /// <exception cref="Common.NotFoundException">Khi không có phiếu hoặc phiếu ngoài phạm vi.</exception>
    Task<PdfFile> PurchaseRequestAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>In bảng tổng hợp đề xuất mua theo biểu mẫu CUVT 2A/2B, khổ ngang (lát Y10).</summary>
    /// <param name="id">Bảng tổng hợp.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>File PDF.</returns>
    /// <exception cref="Common.NotFoundException">Khi không có phiếu hoặc phiếu ngoài phạm vi.</exception>
    Task<PdfFile> PurchaseSummaryAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// In kết quả hỏi giá theo biểu mẫu CUVT (lát Y6): 3A so sánh chọn NCC, hoặc 3a/3B bảng trình giá mỗi NCC trúng thầu một trang; khổ ngang.
    /// </summary>
    /// <param name="id">RFQ.</param>
    /// <param name="form">"3A" = in bảng so sánh (RFQ loại A); null = theo biểu mẫu của phiếu.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>File PDF.</returns>
    /// <exception cref="Common.NotFoundException">Khi không có phiếu hoặc phiếu ngoài phạm vi.</exception>
    /// <exception cref="Proc.Domain.Common.BusinessRuleException">Khi chưa có báo giá, hoặc in 3A cho RFQ không phải loại A.</exception>
    Task<PdfFile> RfqAsync(Guid id, string? form, CancellationToken cancellationToken);

    /// <summary>In chứng từ kho (nhập mua, trả NCC, chuyển kho, xuất bán / nhập mua nội bộ).</summary>
    /// <param name="id">Chứng từ kho.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>File PDF.</returns>
    /// <exception cref="Common.NotFoundException">Khi không có phiếu hoặc phiếu ngoài phạm vi.</exception>
    Task<PdfFile> InventoryDocumentAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// In biên bản giao nhận và kiểm tra hàng hóa của phiếu nhập mua NCC (design.md mục 18.14): SL theo chứng từ giao, SL đạt nhập kho,
    /// SL không đạt, thiếu/thừa, kết quả và ghi chú kiểm tra từng dòng; dùng làm biên bản sai lệch gửi NCC khi có dòng lệch/không đạt.
    /// </summary>
    /// <param name="id">Phiếu nhập mua.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>File PDF.</returns>
    /// <exception cref="Common.NotFoundException">Khi không có phiếu hoặc phiếu ngoài phạm vi.</exception>
    /// <exception cref="Proc.Domain.Common.BusinessRuleException">Khi phiếu không phải phiếu nhập mua NCC.</exception>
    Task<PdfFile> GoodsReceiptRecordAsync(Guid id, CancellationToken cancellationToken);
}
