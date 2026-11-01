using Proc.Application.Excel;

namespace Proc.Application.Analysis;

/// <summary>Bộ lọc khớp danh mục của dữ liệu mua lịch sử.</summary>
public static class PurchaseHistoryMatchFilters
{
    /// <summary>Đã khớp cả mặt hàng lẫn NCC.</summary>
    public const string Matched = "matched";

    /// <summary>Còn thiếu mặt hàng hoặc NCC – việc của lát E2.</summary>
    public const string Unmatched = "unmatched";
}

/// <summary>
/// Điều kiện xem dữ liệu mua lịch sử.
/// </summary>
/// <param name="BatchId">Lô nhập; null = mọi lô.</param>
/// <param name="From">Ngày mua từ.</param>
/// <param name="To">Ngày mua đến.</param>
/// <param name="Match">Lọc khớp danh mục (<see cref="PurchaseHistoryMatchFilters"/>); null = tất cả.</param>
/// <param name="Search">Từ khóa (có dấu hoặc không dấu) trên mã, tên vật tư, NCC, MST, bộ phận.</param>
public sealed record PurchaseHistoryQuery(Guid? BatchId, DateOnly? From, DateOnly? To, string? Match, string? Search);

/// <summary>Một dòng dữ liệu mua lịch sử trên danh sách.</summary>
public sealed record PurchaseHistoryRecordDto(
    Guid Id, Guid BatchId, int RowNumber, string? Department, string ItemCode, string ItemName, string? Specification, string? Brand,
    string? Origin, string? Standard, string Uom, DateOnly? RequestDate, DateOnly PurchaseDate, decimal Quantity, decimal UnitPrice,
    decimal? VatPercent, string Currency, decimal Amount, decimal? RelatedCost, string? RelatedCostNote, string SupplierName,
    string? SupplierTaxCode, string? SupplierContact, string? PaymentTerms, string? Discount, string? Sponsorship, bool? OnTime,
    bool? QualityPassed, string? Complaint, Guid? ItemId, string? MatchedItemCode, Guid? SupplierId, string? MatchedSupplierCode);

/// <summary>
/// Kết quả xem dữ liệu mua lịch sử.
/// </summary>
/// <param name="Rows">Các dòng (tối đa <paramref name="Limit"/>), ngày mua mới nhất trước.</param>
/// <param name="Truncated">Còn dòng chưa hiện – thu hẹp bộ lọc.</param>
/// <param name="Limit">Số dòng tối đa mỗi lần xem.</param>
public sealed record PurchaseHistoryListResult(IReadOnlyList<PurchaseHistoryRecordDto> Rows, bool Truncated, int Limit);

/// <summary>Một lô nhập.</summary>
/// <param name="Id">Id lô.</param>
/// <param name="FileName">Tên file.</param>
/// <param name="RecordCount">Số dòng.</param>
/// <param name="FromDate">Ngày mua sớm nhất.</param>
/// <param name="ToDate">Ngày mua muộn nhất.</param>
/// <param name="ImportedAt">Thời điểm nhập (UTC).</param>
/// <param name="ImportedBy">Họ tên người nhập.</param>
public sealed record PurchaseHistoryBatchDto(
    Guid Id, string FileName, int RecordCount, DateOnly FromDate, DateOnly ToDate, DateTimeOffset ImportedAt, string? ImportedBy);

/// <summary>
/// Kết quả nhập file dữ liệu mua lịch sử.
/// </summary>
/// <param name="TotalRows">Số dòng dữ liệu (bỏ dòng trống).</param>
/// <param name="ValidRows">Số dòng hợp lệ (sẽ nhập / đã nhập).</param>
/// <param name="MatchedItems">Số dòng tự khớp được mặt hàng (mã vật tư trùng mã hàng chung).</param>
/// <param name="MatchedSuppliers">Số dòng tự khớp được NCC (MST trùng).</param>
/// <param name="Errors">Các lỗi (tối đa <c>MaxReportedErrors</c> dòng đầu); có lỗi thì không dòng nào được lưu.</param>
/// <param name="Committed">true khi đã lưu thật.</param>
/// <param name="BatchId">Lô vừa tạo khi đã lưu.</param>
public sealed record PurchaseHistoryImportResult(
    int TotalRows, int ValidRows, int MatchedItems, int MatchedSuppliers, IReadOnlyList<ExcelImportError> Errors, bool Committed, Guid? BatchId);

/// <summary>
/// Dữ liệu mua lịch sử 12–24 tháng của từng pháp nhân (design.md mục 18.1, lát E1): nhập Excel, xem, xóa lô nhập nhầm.
/// Dữ liệu chỉ để phân tích – không ghi sổ kho, không ảnh hưởng tồn, giá vốn, lịch sử giá. Mọi thao tác ở công ty đang làm việc.
/// </summary>
public interface IPurchaseHistoryService
{
    /// <summary>
    /// File mẫu: sheet dữ liệu chỉ có tiêu đề, kèm sheet Hướng dẫn.
    /// </summary>
    /// <returns>File .xlsx.</returns>
    /// <exception cref="Common.ForbiddenAccessException">Khi không có quyền nhập.</exception>
    ExcelFile GetTemplate();

    /// <summary>
    /// Nhập file Excel. Kiểm tra mọi dòng trước; có một dòng lỗi thì cả file không lưu. Mã vật tư trùng mã hàng chung, MST trùng NCC thì
    /// tự gán; không khớp vẫn nhập.
    /// </summary>
    /// <param name="fileName">Tên file gốc (ghi vào lô).</param>
    /// <param name="content">Nội dung .xlsx.</param>
    /// <param name="commit">false = kiểm tra thử (không lưu); true = lưu khi không có lỗi.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Số dòng hợp lệ, số dòng khớp danh mục và các lỗi.</returns>
    /// <exception cref="Common.ForbiddenAccessException">Khi không có quyền nhập hoặc chưa chọn công ty.</exception>
    Task<PurchaseHistoryImportResult> ImportAsync(string fileName, Stream content, bool commit, CancellationToken cancellationToken);

    /// <summary>
    /// Danh sách dòng theo bộ lọc, ngày mua mới nhất trước.
    /// </summary>
    /// <param name="query">Bộ lọc.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Các dòng.</returns>
    Task<PurchaseHistoryListResult> ListAsync(PurchaseHistoryQuery query, CancellationToken cancellationToken);

    /// <summary>
    /// Các lô đã nhập của công ty đang làm việc, mới nhất trước.
    /// </summary>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Các lô.</returns>
    Task<IReadOnlyList<PurchaseHistoryBatchDto>> ListBatchesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Xóa cả một lô (nhập nhầm) cùng mọi dòng của lô; ghi nhật ký.
    /// </summary>
    /// <param name="batchId">Lô.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <exception cref="Common.NotFoundException">Khi lô không có ở công ty đang làm việc.</exception>
    /// <exception cref="Common.ForbiddenAccessException">Khi không có quyền nhập.</exception>
    Task DeleteBatchAsync(Guid batchId, CancellationToken cancellationToken);
}
