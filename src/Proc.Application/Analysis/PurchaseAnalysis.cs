namespace Proc.Application.Analysis;

/// <summary>
/// Điều kiện phân tích giá hợp nhất.
/// </summary>
/// <param name="From">Ngày mua từ; null = 12 tháng trước <paramref name="To"/>.</param>
/// <param name="To">Ngày mua đến; null = hôm nay.</param>
/// <param name="TargetDiscountPct">% giảm mục tiêu so với giá bình quân thấp nhất; null = 5%.</param>
/// <param name="AllCompanies">true = toàn tập đoàn (cần quyền <c>Analysis.PurchaseHistory.ViewAllCompanies</c>).</param>
public sealed record PurchaseAnalysisQuery(DateOnly? From, DateOnly? To, decimal? TargetDiscountPct, bool AllCompanies);

/// <summary>
/// Số liệu một pháp nhân cho một mặt hàng × ĐVT.
/// </summary>
/// <param name="CompanyId">Pháp nhân.</param>
/// <param name="CompanyCode">Mã công ty.</param>
/// <param name="CompanyName">Tên công ty.</param>
/// <param name="Quantity">Tổng SL.</param>
/// <param name="Amount">Tổng giá trị trước VAT.</param>
/// <param name="AvgPrice">Giá bình quân gia quyền.</param>
/// <param name="MinPrice">Đơn giá thấp nhất.</param>
/// <param name="MaxPrice">Đơn giá cao nhất.</param>
/// <param name="LastPrice">Giá gần nhất (bình quân của ngày mua gần nhất).</param>
/// <param name="LastDate">Ngày mua gần nhất.</param>
/// <param name="PurchaseCount">Số lần mua (số dòng).</param>
/// <param name="SupplierCount">Số NCC.</param>
/// <param name="Savings">Tiết kiệm dự kiến của pháp nhân.</param>
public sealed record PurchaseAnalysisCompanyDto(
    Guid CompanyId, string CompanyCode, string CompanyName, decimal Quantity, decimal Amount, decimal AvgPrice, decimal MinPrice,
    decimal MaxPrice, decimal LastPrice, DateOnly LastDate, int PurchaseCount, int SupplierCount, decimal Savings);

/// <summary>
/// Một dòng phân tích: mặt hàng chung × ĐVT, hợp nhất các pháp nhân.
/// </summary>
/// <param name="ItemId">Mặt hàng chung.</param>
/// <param name="ItemCode">Mã hàng.</param>
/// <param name="ItemName">Tên hàng.</param>
/// <param name="Uom">ĐVT (cách ghi gặp nhiều nhất).</param>
/// <param name="CompanyCount">Số pháp nhân đã mua.</param>
/// <param name="Quantity">Tổng SL.</param>
/// <param name="Amount">Tổng giá trị trước hợp nhất.</param>
/// <param name="AvgPrice">Giá bình quân gia quyền hợp nhất.</param>
/// <param name="MinPrice">Đơn giá thấp nhất.</param>
/// <param name="MaxPrice">Đơn giá cao nhất.</param>
/// <param name="LastDate">Ngày mua gần nhất (mọi pháp nhân).</param>
/// <param name="PurchaseCount">Tổng số lần mua.</param>
/// <param name="SupplierCount">Số NCC khác nhau.</param>
/// <param name="PriceGapPct">Chênh lệch % giá bình quân cao nhất so với thấp nhất giữa các pháp nhân.</param>
/// <param name="TargetPrice">Giá đàm phán mục tiêu.</param>
/// <param name="Savings">Tiết kiệm dự kiến.</param>
/// <param name="SavingsPct">Tiết kiệm / tổng giá trị (%).</param>
/// <param name="BestSupplierName">NCC có giá bình quân thấp nhất trong kỳ (mã – tên khi đã ánh xạ, tên trên file khi chưa).</param>
/// <param name="BestSupplierPrice">Giá bình quân của NCC đó.</param>
/// <param name="Companies">Chi tiết từng pháp nhân, giá thấp trước.</param>
public sealed record PurchaseAnalysisRowDto(
    Guid ItemId, string ItemCode, string ItemName, string Uom, int CompanyCount, decimal Quantity, decimal Amount, decimal AvgPrice,
    decimal MinPrice, decimal MaxPrice, DateOnly LastDate, int PurchaseCount, int SupplierCount, decimal PriceGapPct, decimal TargetPrice,
    decimal Savings, decimal SavingsPct, string BestSupplierName, decimal BestSupplierPrice, IReadOnlyList<PurchaseAnalysisCompanyDto> Companies);

/// <summary>
/// Kết quả phân tích giá hợp nhất.
/// </summary>
/// <param name="From">Ngày mua từ (đã áp mặc định).</param>
/// <param name="To">Ngày mua đến.</param>
/// <param name="TargetDiscountPct">% giảm mục tiêu đã dùng.</param>
/// <param name="AllCompanies">true khi xem toàn tập đoàn.</param>
/// <param name="CompanyCount">Số pháp nhân có dữ liệu trong phân tích.</param>
/// <param name="Amount">Tổng giá trị các dòng đưa vào phân tích.</param>
/// <param name="Savings">Tổng tiết kiệm dự kiến.</param>
/// <param name="SavingsPct">Tiết kiệm / tổng giá trị (%).</param>
/// <param name="UnmappedRecords">Số dòng chưa gán mặt hàng chung (không vào phân tích).</param>
/// <param name="UnmappedAmount">Giá trị các dòng chưa gán (VND).</param>
/// <param name="ForeignCurrencyRecords">Số dòng ngoại tệ (chưa quy đổi nên không vào phân tích).</param>
/// <param name="ZeroPriceRecords">Số dòng đơn giá 0 – hàng tặng (không vào phân tích, nếu không giá mục tiêu thành 0 đ).</param>
/// <param name="Rows">Các dòng, nhiều tiết kiệm trước.</param>
public sealed record PurchaseAnalysisDto(
    DateOnly From, DateOnly To, decimal TargetDiscountPct, bool AllCompanies, int CompanyCount, decimal Amount, decimal Savings,
    decimal SavingsPct, int UnmappedRecords, decimal UnmappedAmount, int ForeignCurrencyRecords, int ZeroPriceRecords,
    IReadOnlyList<PurchaseAnalysisRowDto> Rows);

/// <summary>
/// Phân tích giá hợp nhất và cơ hội tiết kiệm trên dữ liệu mua lịch sử đã ánh xạ (design.md mục 18.3, lát E3). Chỉ đọc.
/// </summary>
public interface IPurchaseAnalysisService
{
    /// <summary>
    /// Phân tích theo mặt hàng chung × ĐVT: số liệu từng pháp nhân, hợp nhất, giá mục tiêu, tiết kiệm dự kiến.
    /// </summary>
    /// <param name="query">Khoảng ngày, % giảm mục tiêu, phạm vi.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Kết quả phân tích.</returns>
    /// <exception cref="Proc.Domain.Common.BusinessRuleException">Khi từ ngày sau đến ngày hoặc % giảm ngoài khoảng 0 đến dưới 100.</exception>
    /// <exception cref="Common.ForbiddenAccessException">Khi không có quyền xem, hoặc xem toàn tập đoàn mà không có quyền.</exception>
    Task<PurchaseAnalysisDto> AnalyzeAsync(PurchaseAnalysisQuery query, CancellationToken cancellationToken);
}
