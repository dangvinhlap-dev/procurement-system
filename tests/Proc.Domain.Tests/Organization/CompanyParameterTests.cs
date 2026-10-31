using Proc.Domain.Common;
using Proc.Domain.Organization;

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
