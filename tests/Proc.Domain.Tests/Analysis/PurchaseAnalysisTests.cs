using Proc.Domain.Analysis;
using Proc.Domain.Common;

namespace Proc.Domain.Tests.Analysis;

/// <summary>
/// Phân tích giá hợp nhất và tiết kiệm dự kiến (design.md mục 18.3, lát E3).
/// </summary>
public class PurchaseAnalysisTests
{
    private static readonly Guid A = Guid.NewGuid();
    private static readonly Guid B = Guid.NewGuid();
    private static readonly DateOnly D = new(2026, 6, 30);

    private static CompanyPurchaseFigures Figures(Guid company, decimal qty, decimal amount, decimal min, decimal max,
        decimal? lastQty = null, decimal? lastAmount = null, int count = 1, int suppliers = 1) =>
        new(company, qty, amount, min, max, count, suppliers, D, lastQty ?? qty, lastAmount ?? amount);

    [Fact]
    public void ViDuDeXuat_GiaMucTieu95000_TietKiem180Trieu()
    {
        // Ví dụ trang 3 đề xuất Ban Cung ứng: Suối Tiên 12.000 × 100.000 đ, Sơn Tiên 8.000 × 110.000 đ; giảm 5% từ giá thấp nhất.
        var result = PurchaseAnalysis.Consolidate(
            [Figures(A, 12_000m, 1_200_000_000m, 100_000m, 100_000m), Figures(B, 8_000m, 880_000_000m, 110_000m, 110_000m)], 5m);

        Assert.Equal(20_000m, result.Quantity);
        Assert.Equal(2_080_000_000m, result.Amount);
        Assert.Equal(95_000m, result.TargetPrice);
        Assert.Equal(180_000_000m, result.Savings);
        Assert.Equal(8.65m, result.SavingsPct);
        Assert.Equal(1_900_000_000m, result.Amount - result.Savings);
        Assert.Equal(10m, result.PriceGapPct);
        Assert.Equal(104_000m, result.AvgPrice);
        Assert.Equal(60_000_000m, result.Companies.Single(c => c.Figures.CompanyId == A).Savings);
        Assert.Equal(120_000_000m, result.Companies.Single(c => c.Figures.CompanyId == B).Savings);
    }

    [Fact]
    public void PhapNhanDaMuaDuoiGiaMucTieu_KhongTinhTietKiemAm()
    {
        // Giảm 0%: giá mục tiêu = giá thấp nhất (100.000) → pháp nhân A tiết kiệm 0, không âm.
        var result = PurchaseAnalysis.Consolidate(
            [Figures(A, 10m, 1_000_000m, 100_000m, 100_000m), Figures(B, 10m, 1_200_000m, 120_000m, 120_000m)], 0m);

        Assert.Equal(100_000m, result.TargetPrice);
        Assert.Equal(0m, result.Companies.Single(c => c.Figures.CompanyId == A).Savings);
        Assert.Equal(200_000m, result.Savings);
    }

    [Fact]
    public void GiaGanNhat_LaBinhQuanNgayMuaCuoi_ThapCaoNhatTheoDongMua()
    {
        // Cả kỳ 30 cái, 3.150.000 đ; ngày cuối mua 10 cái giá 100.000 + 10 cái giá 110.000 → giá gần nhất 105.000.
        var result = PurchaseAnalysis.Consolidate([Figures(A, 30m, 3_150_000m, 100_000m, 115_000m, 20m, 2_100_000m, count: 3)], 5m);

        var company = result.Companies.Single();
        Assert.Equal(105_000m, company.AvgPrice);
        Assert.Equal(105_000m, company.LastPrice);
        Assert.Equal(100_000m, result.MinPrice);
        Assert.Equal(115_000m, result.MaxPrice);
        Assert.Equal(3, result.PurchaseCount);
        Assert.Equal(0m, result.PriceGapPct);
    }

    [Fact]
    public void GiaBinhQuan_LamTron6SoLe()
    {
        // 10 đ / 3 cái = 3,333333 đ/cái.
        Assert.Equal(3.333333m, PurchaseAnalysis.AveragePrice(10m, 3m));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(100)]
    public void PhanTramGiamNgoaiKhoang_BaoLoi(decimal pct)
    {
        Assert.Throws<BusinessRuleException>(() => PurchaseAnalysis.Consolidate([Figures(A, 1m, 1m, 1m, 1m)], pct));
    }

    [Fact]
    public void KhongCoPhapNhan_BaoLoi()
    {
        Assert.Throws<BusinessRuleException>(() => PurchaseAnalysis.Consolidate([], 5m));
    }

    [Fact]
    public void KhoaDvt_KhongDauKhongPhanBietHoaThuong()
    {
        Assert.Equal(PurchaseAnalysis.UomKey("Cái"), PurchaseAnalysis.UomKey(" CAI "));
        Assert.NotEqual(PurchaseAnalysis.UomKey("Cái"), PurchaseAnalysis.UomKey("Hộp"));
    }
}
