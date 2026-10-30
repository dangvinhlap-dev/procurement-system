using Proc.Domain.Common;
using Proc.Domain.Inventory;

namespace Proc.Domain.Tests.Inventory;

/// <summary>Công thức giá vốn (design.md mục 7.3) và vòng đời đóng/mở lại kỳ (mục 7.1, 7.4).</summary>
public class PeriodCostingTests
{
    [Fact]
    public void GiaVon_ViDuTrongThietKe()
    {
        // Tồn đầu 100 × 10.000; nhập 50 × 13.000; trả NCC 10 × 13.000 → 1.520.000 ÷ 140.
        var cost = PeriodCosting.UnitCost(100, 1_000_000, 50 - 10, 650_000 - 130_000, 0);

        Assert.Equal(10_857.142857m, cost);
    }

    [Fact]
    public void GiaVon_KhongTonKhongNhap_DungGiaKyTruoc()
    {
        Assert.Equal(12_000m, PeriodCosting.UnitCost(0, 0, 0, 0, 12_000));
        Assert.Equal(0m, PeriodCosting.UnitCost(10, 100_000, -10, -100_000, 0));
    }

    [Fact]
    public void SoDuKy_TonCuoi_BangDauCongNhapTruXuat()
    {
        var b = StockPeriodBalance.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, 100, 1_000_000, 50, 650_000, 120, 1_302_857.14m);

        Assert.Equal((30m, 347_142.86m), (b.CloseQty, b.CloseAmount));
    }

    [Fact]
    public void Ky_DongRoiMoLai_CanLyDo()
    {
        var period = InventoryPeriod.CreateYear(Guid.NewGuid(), 2026)[10];
        period.Open();

        period.StartCosting();
        Assert.Throws<BusinessRuleException>(() => period.StartCosting());
        period.Close(Guid.NewGuid(), DateTimeOffset.UtcNow);
        Assert.Equal(PeriodStatus.Closed, period.Status);

        Assert.Throws<BusinessRuleException>(() => period.Reopen("  "));
        period.Reopen("Sửa phiếu nhập sai giá");
        Assert.Equal((PeriodStatus.Open, "Sửa phiếu nhập sai giá", null), (period.Status, period.ReopenReason, period.ClosedAt));
    }

    [Fact]
    public void Ky_TinhGiaLoi_TraVeDangMo()
    {
        var period = InventoryPeriod.CreateYear(Guid.NewGuid(), 2026)[3];
        period.Open();
        period.StartCosting();

        period.CancelCosting();

        Assert.Equal(PeriodStatus.Open, period.Status);
    }
}
