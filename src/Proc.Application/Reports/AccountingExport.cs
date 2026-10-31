using Proc.Application.Excel;

namespace Proc.Application.Reports;

/// <summary>Điều kiện xuất chứng từ cho kế toán.</summary>
/// <param name="From">Từ ngày chứng từ.</param>
/// <param name="To">Đến ngày (tối đa hai năm).</param>
/// <param name="DocTypes">Loại chứng từ kho (GRN, RTV, TRF, ICS, ICR); rỗng = tất cả.</param>
public sealed record AccountingExportQuery(DateOnly From, DateOnly To, IReadOnlyList<string> DocTypes);

/// <summary>Một chứng từ đã ghi sổ sẽ xuất cho kế toán (dòng xem trước trên màn hình, sheet "Tổng hợp").</summary>
/// <param name="DocumentId">Chứng từ.</param>
/// <param name="DocType">Loại chứng từ.</param>
/// <param name="No">Số chứng từ.</param>
/// <param name="Date">Ngày chứng từ.</param>
/// <param name="PartnerCode">Mã đối tượng: mã NCC, hoặc mã công ty đối ứng (mua bán nội bộ); null với chuyển kho.</param>
/// <param name="PartnerLocalCode">Mã NCC tại công ty – để khớp mã đối tượng trong phần mềm kế toán (design.md mục 5.4).</param>
/// <param name="PartnerName">Tên đối tượng.</param>
/// <param name="PartnerTaxCode">Mã số thuế đối tượng.</param>
/// <param name="InvoiceNo">Số hóa đơn.</param>
/// <param name="InvoiceSeries">Ký hiệu hóa đơn.</param>
/// <param name="InvoiceDate">Ngày hóa đơn.</param>
/// <param name="LineCount">Số dòng hàng.</param>
/// <param name="Amount">Tiền hàng trước thuế.</param>
/// <param name="VatAmount">Tiền thuế GTGT.</param>
/// <param name="CostAmount">Tổng giá vốn của phần xuất (trả NCC, chuyển kho, xuất bán nội bộ); với phiếu nhập bằng tiền hàng.</param>
/// <param name="IsCostFinal">Giá vốn đã chốt (kỳ đã đóng); false = tạm tính.</param>
public sealed record AccountingDocumentDto(
    Guid DocumentId, string DocType, string No, DateOnly Date, string? PartnerCode, string? PartnerLocalCode, string? PartnerName, string? PartnerTaxCode,
    string? InvoiceNo, string? InvoiceSeries, DateOnly? InvoiceDate, int LineCount, decimal Amount, decimal VatAmount, decimal CostAmount, bool IsCostFinal);

/// <summary>
/// Xuất chứng từ kho đã ghi sổ ra Excel cho phần mềm kế toán (design.md mục 1 – quyết định 6, mục 11.5): hệ thống này không làm sổ cái, kế
/// toán nhập file vào phần mềm kế toán và tự định khoản. Chỉ công ty đang làm việc, trong phạm vi kho của người xem.
/// </summary>
public interface IAccountingExportService
{
    /// <summary>Danh sách chứng từ sẽ xuất (xem trước).</summary>
    /// <param name="query">Khoảng ngày, loại chứng từ.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Chứng từ theo ngày, số.</returns>
    /// <exception cref="Domain.Common.BusinessRuleException">Khi khoảng ngày hoặc loại chứng từ không hợp lệ.</exception>
    Task<IReadOnlyList<AccountingDocumentDto>> ListAsync(AccountingExportQuery query, CancellationToken cancellationToken);

    /// <summary>File Excel: sheet "Chứng từ" (mỗi dòng hàng một dòng), "Tổng hợp" (mỗi chứng từ một dòng), "Hướng dẫn".</summary>
    /// <param name="query">Khoảng ngày, loại chứng từ.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>File để tải.</returns>
    /// <exception cref="Domain.Common.BusinessRuleException">Khi điều kiện không hợp lệ hoặc quá nhiều dòng.</exception>
    Task<ExcelFile> ExportAsync(AccountingExportQuery query, CancellationToken cancellationToken);
}
