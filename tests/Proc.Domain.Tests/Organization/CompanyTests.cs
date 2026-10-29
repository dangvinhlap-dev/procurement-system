using Proc.Domain.Common;
using Proc.Domain.Organization;

namespace Proc.Domain.Tests.Organization;

/// <summary>
/// Quy tắc của danh mục công ty (design.md mục 5.1, 5.4).
/// </summary>
public class CompanyTests
{
    [Fact]
    public void Create_ChuanHoaMaVeChuHoaVaBoKhoangTrang()
    {
        var company = Company.Create(" bha ", "Công ty Thép Bắc Hà", isHeadOffice: false);

        Assert.Equal("BHA", company.Code);
        Assert.Equal("Công ty Thép Bắc Hà", company.Name);
        Assert.Equal("VND", company.AccountingCurrency);
        Assert.True(company.IsActive);
        Assert.NotEqual(Guid.Empty, company.Id);
    }

    [Theory]
    [InlineData("")]
    [InlineData("A")]
    [InlineData("CONG TY")]
    [InlineData("CTY-01")]
    [InlineData("ĐNA")]
    [InlineData("ABCDEFGHIJK")]
    public void Create_TuChoiMaKhongHopLe(string code)
    {
        // Mã công ty nằm trong số chứng từ PO-{CTY}-{YYMM}-{0000} nên chỉ cho chữ không dấu và số.
        var ex = Assert.Throws<BusinessRuleException>(() => Company.Create(code, "Công ty A", isHeadOffice: false));
        Assert.Contains("Mã công ty", ex.Message);
    }

    [Fact]
    public void Create_TuChoiTenRong()
    {
        Assert.Throws<BusinessRuleException>(() => Company.Create("BHA", "  ", isHeadOffice: false));
    }

    [Fact]
    public void Id_LaUuidV7()
    {
        // Không so thứ tự hai Id: trong cùng một mili giây, phần sau dấu thời gian của uuid v7 là ngẫu nhiên.
        Assert.Equal(7, Company.Create("AAA", "A", isHeadOffice: false).Id.Version);
    }

    [Fact]
    public void Role_ChuanHoaMaVaTuChoiMaRong()
    {
        Assert.Equal("ADMIN", Role.Create("admin", "Quản trị hệ thống").Code);
        Assert.Throws<BusinessRuleException>(() => Role.Create(" ", "Không tên"));
    }
}
