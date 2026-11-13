using Proc.Application.Excel;

namespace Proc.Application.Reports;

/// <summary>Điều kiện xuất chứng từ mua cho kế toán (lát Y9b, design.md mục 19.3 "Lát Y9").</summary>
/// <param name="From">Từ ngày hiệu lực (PO, đề nghị thanh toán: ngày duyệt; XNH: ngày nhận).</param>
/// <param name="To">Đến ngày (tối đa hai năm).</param>
/// <param name="DocTypes">Loại chứng từ (PO, XNH, DNT); rỗng = tất cả.</param>
public sealed record PurchaseAccountingQuery(DateOnly From, DateOnly To, IReadOnlyList<string> DocTypes);

/// <summary>Một chứng từ sẽ xuất cho kế toán (dòng xem trước trên màn hình, sheet "Tổng hợp").</summary>
/// <param name="DocumentId">Chứng từ.</param>
/// <param name="DocType">PO, XNH hoặc DNT.</param>
/// <param name="No">Số chứng từ.</param>
/// <param name="Date">Ngày chứng từ.</param>
/// <param name="EffectiveDate">Ngày hiệu lực dùng để lọc: ngày duyệt (PO, DNT), ngày nhận (XNH).</param>
/// <param name="PurchaseOrderNo">Số PO (XNH, DNT: PO gốc; PO: chính nó).</param>
/// <param name="SupplierCode">Mã NCC.</param>
/// <param name="SupplierLocalCode">Mã NCC tại công ty – khớp mã đối tượng trong phần mềm kế toán (design.md mục 5.4).</param>
/// <param name="SupplierName">Tên NCC.</param>
/// <param name="SupplierTaxCode">Mã số thuế NCC.</param>
/// <param name="InvoiceNo">Số hóa đơn (XNH; DNT: các số nối bằng dấu phẩy).</param>
/// <param name="InvoiceSeries">Ký hiệu hóa đơn.</param>
/// <param name="InvoiceDate">Ngày hóa đơn.</param>
/// <param name="LineCount">Số dòng.</param>
/// <param name="Amount">Tiền trước thuế (PO, XNH); DNT: tổng tiền hóa đơn gồm VAT.</param>
/// <param name="VatAmount">Tiền thuế GTGT (PO, XNH); DNT: 0.</param>
/// <param name="TotalAmount">PO, XNH: tiền + thuế; DNT: số tiền đề nghị (tổng − giảm trừ).</param>
/// <param name="Status">Tình trạng bằng chữ (ví dụ "Đã duyệt", "Đã đóng", "Đã chi 13/11/2026").</param>
public sealed record PurchaseAccountingDocumentDto(
    Guid DocumentId, string DocType, string No, DateOnly Date, DateOnly EffectiveDate, string? PurchaseOrderNo, string SupplierCode,
    string? SupplierLocalCode, string SupplierName, string? SupplierTaxCode, string? InvoiceNo, string? InvoiceSeries, DateOnly? InvoiceDate, int LineCount,
    decimal Amount, decimal VatAmount, decimal TotalAmount, string Status);

/// <summary>
/// Xuất chứng từ mua ra Excel cho phần mềm kế toán (lát Y9b – thay ACCX từ khi bỏ phân hệ kho, design.md mục 19.4): PO đã duyệt, phiếu xác nhận
/// nhận hàng (kèm hóa đơn), đề nghị thanh toán đã duyệt. Hệ thống không làm sổ cái – kế toán nhập file và tự định khoản. Chỉ công ty đang làm
/// việc, qua global query filter.
/// </summary>
public interface IPurchaseAccountingExportService
{
    /// <summary>Danh sách chứng từ sẽ xuất (xem trước).</summary>
    /// <param name="query">Khoảng ngày, loại chứng từ.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Chứng từ theo loại (PO, XNH, DNT), ngày hiệu lực, số.</returns>
    /// <exception cref="Domain.Common.BusinessRuleException">Khi khoảng ngày hoặc loại chứng từ không hợp lệ.</exception>
    Task<IReadOnlyList<PurchaseAccountingDocumentDto>> ListAsync(PurchaseAccountingQuery query, CancellationToken cancellationToken);

    /// <summary>File Excel: sheet "Tổng hợp", "Đơn mua hàng", "Nhận hàng – hóa đơn", "Đề nghị thanh toán" (mỗi dòng chi tiết một dòng), "Hướng dẫn".</summary>
    /// <param name="query">Khoảng ngày, loại chứng từ.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>File để tải.</returns>
    /// <exception cref="Domain.Common.BusinessRuleException">Khi điều kiện không hợp lệ hoặc quá nhiều dòng.</exception>
    Task<ExcelFile> ExportAsync(PurchaseAccountingQuery query, CancellationToken cancellationToken);
}
