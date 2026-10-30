using Proc.Domain.Common;
using Proc.Domain.Inventory;

namespace Proc.Domain.Tests.Inventory;

/// <summary>
/// Kỳ kho (design.md mục 7, 7.1).
/// </summary>
public class InventoryPeriodTests
{
    private static readonly Guid Company = Guid.CreateVersion7();

    [Fact]
    public void TaoLichNam_KyNamDangMo_12KyThangChuaMo_DungNgayDauCuoi()
    {
        var periods = InventoryPeriod.CreateYear(Company, 2028);

        Assert.Equal(13, periods.Count);
        var year = periods[0];
        Assert.Equal((0, PeriodStatus.Open, new DateOnly(2028, 1, 1), new DateOnly(2028, 12, 31), "Năm 2028"),
            (year.Month, year.Status, year.StartDate, year.EndDate, year.Label));
        Assert.All(periods.Skip(1), p => Assert.Equal(PeriodStatus.NotOpened, p.Status));
        // Năm nhuận: tháng 2/2028 có 29 ngày.
        var feb = periods.Single(p => p.Month == 2);
        Assert.Equal((new DateOnly(2028, 2, 1), new DateOnly(2028, 2, 29), "02/2028"), (feb.StartDate, feb.EndDate, feb.Label));
        Assert.Equal(new DateOnly(2028, 12, 31), periods[^1].EndDate);
    }

    [Theory]
    [InlineData(1999)]
    [InlineData(2101)]
    public void TaoLichNam_TuChoiNamNgoaiKhoang(int year)
    {
        Assert.Throws<BusinessRuleException>(() => InventoryPeriod.CreateYear(Company, year));
    }

    [Fact]
    public void MoKy_ChiKyThangChuaMo()
    {
        var periods = InventoryPeriod.CreateYear(Company, 2026);
        var october = periods.Single(p => p.Month == 10);

        october.Open();
        Assert.Equal(PeriodStatus.Open, october.Status);
        Assert.Throws<BusinessRuleException>(october.Open);
        Assert.Throws<BusinessRuleException>(periods[0].Open);
        Assert.True(october.Contains(new DateOnly(2026, 10, 31)));
        Assert.False(october.Contains(new DateOnly(2026, 11, 1)));
    }
}
