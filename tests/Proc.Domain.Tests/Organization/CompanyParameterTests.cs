using Proc.Domain.Common;
using Proc.Domain.Organization;
using Proc.Domain.Pricing;

namespace Proc.Domain.Tests.Organization;

/// <summary>Tham số công ty (design.md mục 5.4): mặc định, giới hạn, về mặc định.</summary>
public class CompanyParameterTests
{
    [Fact]
    public void ThamSoMoi_DungGiaTriMacDinh_DoiDuocTrongGioiHan()
    {
        var p = CompanyParameter.Create(Guid.CreateVersion7());
        Assert.Equal(3, p.RfqMinSuppliers);

        p.ChangeRfqMinSuppliers(5);
        Assert.Equal(5, p.RfqMinSuppliers);
        p.ChangeRfqMinSuppliers(null);
        Assert.Equal(CompanyParameter.DefaultRfqMinSuppliers, p.RfqMinSuppliers);

        Assert.Throws<BusinessRuleException>(() => p.ChangeRfqMinSuppliers(0));
        Assert.Throws<BusinessRuleException>(() => p.ChangeRfqMinSuppliers(CompanyParameter.MaxRfqMinSuppliers + 1));
        Assert.Throws<BusinessRuleException>(() => CompanyParameter.Create(Guid.Empty));
    }
}

/// <summary>% markup nội bộ mặc định và giá bán nội bộ gợi ý (design.md mục 1 câu hỏi 2, mục 5.4).</summary>
public class InternalMarkupTests
{
    [Fact]
    public void MarkupMoi_MacDinh0_DoiDuocTrongGioiHan_LamTron4So()
    {
        var p = CompanyParameter.Create(Guid.CreateVersion7());
        Assert.Equal(0m, p.InternalMarkupPercent);

        p.ChangeInternalMarkupPercent(12.5m);
        Assert.Equal(12.5m, p.InternalMarkupPercent);
        p.ChangeInternalMarkupPercent(7.123456m);
        Assert.Equal(7.1235m, p.InternalMarkupPercent);
        p.ChangeInternalMarkupPercent(null);
        Assert.Equal(CompanyParameter.DefaultInternalMarkupPercent, p.InternalMarkupPercent);

        Assert.Throws<BusinessRuleException>(() => p.ChangeInternalMarkupPercent(-0.01m));
        Assert.Throws<BusinessRuleException>(() => p.ChangeInternalMarkupPercent(CompanyParameter.MaxInternalMarkupPercent + 1));
    }

    [Theory]
    [InlineData(10_000, 10, 11_000)]
    // 10.857,142857 × 1,1 = 11.942,857143 → làm tròn đồng: 11.943.
    [InlineData(10_857.142857, 10, 11_943)]
    [InlineData(10_000, 0, 10_000)]
    // 0,5 đồng làm tròn lên (AwayFromZero): 1.000,5 → 1.001.
    [InlineData(1_000.5, 0, 1_001)]
    public void GiaGoiY_BangGiaVonNhanMotCongMarkup_LamTronDong(double cost, double markup, double expected) =>
        Assert.Equal((decimal)expected, InternalPrice.Suggest((decimal)cost, (decimal)markup));
}
