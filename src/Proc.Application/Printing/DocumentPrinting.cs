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

    /// <summary>In đề nghị mua hàng.</summary>
    /// <param name="id">PR.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>File PDF.</returns>
    /// <exception cref="Common.NotFoundException">Khi không có phiếu hoặc phiếu ngoài phạm vi.</exception>
    Task<PdfFile> PurchaseRequestAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>In chứng từ kho (nhập mua, trả NCC, chuyển kho, xuất bán / nhập mua nội bộ).</summary>
    /// <param name="id">Chứng từ kho.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>File PDF.</returns>
    /// <exception cref="Common.NotFoundException">Khi không có phiếu hoặc phiếu ngoài phạm vi.</exception>
    Task<PdfFile> InventoryDocumentAsync(Guid id, CancellationToken cancellationToken);
}
