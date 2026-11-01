using Proc.Domain.Common;

namespace Proc.Domain.Analysis;

/// <summary>
/// Số liệu mua của một pháp nhân cho một mặt hàng chung × ĐVT trong kỳ phân tích, gom từ các dòng dữ liệu mua lịch sử.
/// </summary>
/// <param name="CompanyId">Pháp nhân mua.</param>
/// <param name="Quantity">Tổng SL.</param>
/// <param name="Amount">Tổng thành tiền trước VAT (không gồm chi phí liên quan).</param>
/// <param name="MinPrice">Đơn giá thấp nhất trên các dòng.</param>
/// <param name="MaxPrice">Đơn giá cao nhất trên các dòng.</param>
/// <param name="PurchaseCount">Số lần mua (số dòng).</param>
/// <param name="SupplierCount">Số NCC đã mua.</param>
/// <param name="LastDate">Ngày mua gần nhất.</param>
/// <param name="LastQuantity">Tổng SL mua trong ngày gần nhất.</param>
/// <param name="LastAmount">Tổng thành tiền trong ngày gần nhất.</param>
public sealed record CompanyPurchaseFigures(
    Guid CompanyId, decimal Quantity, decimal Amount, decimal MinPrice, decimal MaxPrice, int PurchaseCount, int SupplierCount,
    DateOnly LastDate, decimal LastQuantity, decimal LastAmount);

/// <summary>
/// Kết quả phân tích của một pháp nhân.
/// </summary>
/// <param name="Figures">Số liệu mua đầu vào.</param>
/// <param name="AvgPrice">Giá bình quân gia quyền trong kỳ.</param>
/// <param name="LastPrice">Giá gần nhất = bình quân gia quyền của ngày mua gần nhất.</param>
/// <param name="Savings">Tiết kiệm dự kiến nếu mua ở giá mục tiêu (không âm).</param>
public sealed record CompanyPriceAnalysis(CompanyPurchaseFigures Figures, decimal AvgPrice, decimal LastPrice, decimal Savings);

/// <summary>
/// Kết quả phân tích hợp nhất một mặt hàng chung × ĐVT giữa các pháp nhân.
/// </summary>
/// <param name="Companies">Từng pháp nhân.</param>
/// <param name="Quantity">Tổng SL các pháp nhân.</param>
/// <param name="Amount">Tổng giá trị trước hợp nhất.</param>
/// <param name="AvgPrice">Giá bình quân gia quyền hợp nhất.</param>
/// <param name="MinPrice">Đơn giá thấp nhất trên mọi dòng.</param>
/// <param name="MaxPrice">Đơn giá cao nhất trên mọi dòng.</param>
/// <param name="PurchaseCount">Tổng số lần mua.</param>
/// <param name="PriceGapPct">Chênh lệch % giữa giá bình quân cao nhất và thấp nhất của các pháp nhân.</param>
/// <param name="TargetPrice">Giá đàm phán mục tiêu.</param>
/// <param name="Savings">Tổng tiết kiệm dự kiến.</param>
/// <param name="SavingsPct">Tiết kiệm / tổng giá trị trước hợp nhất (%).</param>
public sealed record ConsolidatedPriceAnalysis(
    IReadOnlyList<CompanyPriceAnalysis> Companies, decimal Quantity, decimal Amount, decimal AvgPrice, decimal MinPrice, decimal MaxPrice,
    int PurchaseCount, decimal PriceGapPct, decimal TargetPrice, decimal Savings, decimal SavingsPct);

/// <summary>
/// Phân tích giá hợp nhất và cơ hội tiết kiệm (design.md mục 18.3, bước 4 đề xuất Ban Cung ứng).
/// <para>
/// Giá mục tiêu = giá bình quân thấp nhất giữa các pháp nhân × (100 − X) / 100. Tiết kiệm = Σ max(0, giá bình quân pháp nhân − giá mục
/// tiêu) × SL pháp nhân. Ví dụ đề xuất: 12.000 × 100.000 đ và 8.000 × 110.000 đ, X = 5 → giá mục tiêu 95.000 đ, tiết kiệm
/// 5.000 × 12.000 + 15.000 × 8.000 = 180.000.000 đ = 8,65% của 2.080.000.000 đ.
/// </para>
/// Pháp nhân đã mua thấp hơn giá mục tiêu tính 0 chứ không âm: hợp nhất không bắt họ mua đắt hơn.
/// </summary>
public static class PurchaseAnalysis
{
    /// <summary>% giảm mục tiêu mặc định so với giá bình quân thấp nhất – ví dụ của đề xuất (100.000 → 95.000).</summary>
    public const decimal DefaultTargetDiscountPct = 5m;

    /// <summary>
    /// Giá bình quân gia quyền = thành tiền / SL, làm tròn 6 số lẻ (kiểu đơn giá numeric(19,6)).
    /// </summary>
    /// <param name="amount">Tổng thành tiền.</param>
    /// <param name="quantity">Tổng SL.</param>
    /// <returns>Giá bình quân; 0 khi SL = 0.</returns>
    public static decimal AveragePrice(decimal amount, decimal quantity) =>
        quantity == 0 ? 0 : Math.Round(amount / quantity, 6, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Khóa ĐVT để gom dòng: ĐVT trên file cũ là chữ tự do, "Cái", "cai", " CÁI " là một; khác ĐVT (cái/hộp) thì không cộng SL được.
    /// </summary>
    /// <param name="uom">ĐVT trên dòng.</param>
    /// <returns>Chữ không dấu, chữ thường, gộp khoảng trắng.</returns>
    public static string UomKey(string? uom) => SearchText.Normalize(uom);

    /// <summary>
    /// Hợp nhất số liệu các pháp nhân, tính giá mục tiêu và tiết kiệm dự kiến.
    /// </summary>
    /// <param name="companies">Số liệu từng pháp nhân (SL &gt; 0).</param>
    /// <param name="targetDiscountPct">% giảm mục tiêu so với giá bình quân thấp nhất, 0 ≤ X &lt; 100.</param>
    /// <returns>Kết quả hợp nhất; pháp nhân xếp theo giá bình quân tăng dần.</returns>
    /// <exception cref="BusinessRuleException">Khi % giảm ngoài khoảng hoặc không có pháp nhân nào.</exception>
    public static ConsolidatedPriceAnalysis Consolidate(IReadOnlyCollection<CompanyPurchaseFigures> companies, decimal targetDiscountPct)
    {
        EnsureDiscountPct(targetDiscountPct);
        if (companies.Count == 0) throw new BusinessRuleException("Không có số liệu mua để phân tích.");

        var priced = companies
            .Select(c => (Figures: c, Avg: AveragePrice(c.Amount, c.Quantity), Last: AveragePrice(c.LastAmount, c.LastQuantity)))
            .OrderBy(c => c.Avg)
            .ToList();
        var lowestAvg = priced[0].Avg;
        var highestAvg = priced[^1].Avg;
        var target = Math.Round(lowestAvg * (100m - targetDiscountPct) / 100m, 6, MidpointRounding.AwayFromZero);

        var results = priced
            .Select(c => new CompanyPriceAnalysis(c.Figures, c.Avg, c.Last,
                Math.Round(Math.Max(0m, c.Avg - target) * c.Figures.Quantity, 2, MidpointRounding.AwayFromZero)))
            .ToList();
        var quantity = companies.Sum(c => c.Quantity);
        var amount = companies.Sum(c => c.Amount);
        var savings = results.Sum(r => r.Savings);

        return new ConsolidatedPriceAnalysis(
            results, quantity, amount, AveragePrice(amount, quantity), companies.Min(c => c.MinPrice), companies.Max(c => c.MaxPrice),
            companies.Sum(c => c.PurchaseCount), Percent(highestAvg - lowestAvg, lowestAvg), target, savings, Percent(savings, amount));
    }

    /// <summary>
    /// Kiểm tra % giảm mục tiêu: 0 ≤ X &lt; 100 (giảm 100% là giá mục tiêu 0 đ, vô nghĩa).
    /// </summary>
    /// <param name="targetDiscountPct">% giảm.</param>
    /// <exception cref="BusinessRuleException">Khi ngoài khoảng.</exception>
    public static void EnsureDiscountPct(decimal targetDiscountPct)
    {
        if (targetDiscountPct is < 0 or >= 100) throw new BusinessRuleException("% giảm mục tiêu phải từ 0 đến dưới 100.");
    }

    /// <summary>Tỷ lệ phần trăm làm tròn 2 số lẻ; 0 khi mẫu số = 0.</summary>
    private static decimal Percent(decimal part, decimal whole) =>
        whole == 0 ? 0 : Math.Round(part / whole * 100m, 2, MidpointRounding.AwayFromZero);
}
