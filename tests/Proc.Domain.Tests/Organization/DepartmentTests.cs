using Proc.Domain.Common;
using Proc.Domain.Organization;

namespace Proc.Domain.Tests.Organization;

/// <summary>Quy tắc danh mục bộ phận theo công ty (design.md mục 5.1, 18.5).</summary>
public class DepartmentTests
{
    private static readonly Guid CompanyA = Guid.CreateVersion7();

    [Fact]
    public void TaoBoPhan_ChuanHoaMaVaCatKhoangTrang()
    {
        var department = Department.Create(CompanyA, " px1 ", "  Phân xưởng cán 1 ", " Nguyễn Văn A ");

        Assert.Equal(CompanyA, department.CompanyId);
        Assert.Equal("PX1", department.Code);
        Assert.Equal("Phân xưởng cán 1", department.Name);
        Assert.Equal("Nguyễn Văn A", department.ManagerName);
        Assert.True(department.IsActive);
    }

    [Theory]
    [InlineData("P")]
    [InlineData("PX 1")]
    [InlineData("PX-1")]
    [InlineData("XƯỞNG")]
    public void MaBoPhan_SaiQuyTac_BiChan(string code)
    {
        var ex = Assert.Throws<BusinessRuleException>(() => Department.Create(CompanyA, code, "Bộ phận", null));
        Assert.Contains("Mã bộ phận", ex.Message);
    }

    [Fact]
    public void BoPhan_PhaiCoCongTyVaTen()
    {
        Assert.Throws<BusinessRuleException>(() => Department.Create(Guid.Empty, "PX1", "Bộ phận", null));
        Assert.Throws<BusinessRuleException>(() => Department.Create(CompanyA, "PX1", " ", null));
    }

    [Fact]
    public void SuaBoPhan_DoiTenTruongBoPhan_NgungVaDungLai()
    {
        var department = Department.Create(CompanyA, "PX1", "Phân xưởng 1", null);

        department.Update("Phân xưởng cán", " ", isActive: false);
        Assert.Equal(("PX1", "Phân xưởng cán", null, false), (department.Code, department.Name, department.ManagerName, department.IsActive));

        department.Update("Phân xưởng cán", "Trần B", isActive: true);
        Assert.True(department.IsActive);
        Assert.Throws<BusinessRuleException>(() => department.Update("", null, true));
    }
}
