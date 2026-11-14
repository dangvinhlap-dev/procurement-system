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
    /// <summary>
    /// In đơn mua hàng: mặc định bảng theo dõi mua hàng theo biểu mẫu CUVT 4A (loại A) / 4B – biểu mẫu ký trước khi đặt hàng (lát Y7, khổ
    /// ngang); <paramref name="form"/> = "supplier" in đơn đặt hàng gửi NCC (khuôn chung mục 10.10).
    /// </summary>
    /// <param name="id">PO.</param>
    /// <param name="form">"supplier" = đơn gửi NCC; null hoặc "4A"/"4B" = bảng theo dõi theo loại mua của PO.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>File PDF.</returns>
    /// <exception cref="Common.NotFoundException">Khi không có phiếu hoặc phiếu ngoài phạm vi.</exception>
    /// <exception cref="Proc.Domain.Common.BusinessRuleException">Khi <paramref name="form"/> không hợp lệ.</exception>
    Task<PdfFile> PurchaseOrderAsync(Guid id, string? form, CancellationToken cancellationToken);

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

    /// <summary>In đề nghị thanh toán theo biểu mẫu 5A/5B tiền mặt / chuyển khoản (lát Y8); khổ dọc.</summary>
    /// <param name="id">Đề nghị thanh toán.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>File PDF.</returns>
    /// <exception cref="Common.NotFoundException">Khi không có phiếu hoặc phiếu ngoài phạm vi.</exception>
    Task<PdfFile> PaymentRequestAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>In bảng kê chi phí tạp vụ 2C (lát Y11c – mẫu giả lập nhóm C, khổ ngang).</summary>
    /// <param name="id">Bảng kê.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>File PDF.</returns>
    /// <exception cref="Common.NotFoundException">Khi không có phiếu hoặc phiếu ngoài phạm vi.</exception>
    Task<PdfFile> SundryExpenseSheetAsync(Guid id, CancellationToken cancellationToken);

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

    /// <summary>
    /// In phiếu xác nhận nhận hàng XNH thành biên bản giao nhận và kiểm tra hàng hóa (lát K2b, design.md mục 10.10, 19.4): SL theo phiếu
    /// giao, SL đạt, không đạt, thiếu so với phiếu giao, kết quả và ghi chú kiểm tra từng dòng; SL thực nhận sau điều chỉnh (nếu có), hóa
    /// đơn, nghiệm thu của đơn vị đặt hàng, giá trị thực nhận theo giá PO.
    /// </summary>
    /// <param name="id">Phiếu XNH.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>File PDF.</returns>
    /// <exception cref="Common.NotFoundException">Khi không có phiếu hoặc phiếu ngoài phạm vi công ty.</exception>
    Task<PdfFile> ReceiptConfirmationAsync(Guid id, CancellationToken cancellationToken);
}
