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
    [Theory]
    [InlineData("0101234567", "0101234567")]
    [InlineData(" 0101234567-001 ", "0101234567-001")]
    [InlineData("", null)]
    public void UpdateInfo_GhiNhanThongTinVaMaSoThueHopLe(string taxCode, string? expected)
    {
        var company = Company.Create("BHA", "Công ty A", isHeadOffice: false);

        company.UpdateInfo(" Công ty Thép Bắc Hà ", "Bắc Hà", taxCode, " Hà Nội ", "Nguyễn Văn A", new DateOnly(2026, 11, 1));

        Assert.Equal("Công ty Thép Bắc Hà", company.Name);
        Assert.Equal("Bắc Hà", company.ShortName);
        Assert.Equal(expected, company.TaxCode);
        Assert.Equal("Hà Nội", company.Address);
        Assert.Equal("Nguyễn Văn A", company.Representative);
        Assert.Equal(new DateOnly(2026, 11, 1), company.StartDate);
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("0101234567-01")]
    [InlineData("01012345AB")]
    public void UpdateInfo_TuChoiMaSoThueSaiDinhDang(string taxCode)
    {
        // MST Việt Nam: 10 chữ số, hoặc 10 chữ số + "-" + 3 chữ số cho chi nhánh.
        var company = Company.Create("BHA", "Công ty A", isHeadOffice: false);
        var ex = Assert.Throws<BusinessRuleException>(() => company.UpdateInfo("Công ty A", null, taxCode, null, null, null));
        Assert.Contains("Mã số thuế", ex.Message);
    }

    [Fact]
    public void TongCongTy_KhongNgungDungDuoc()
    {
        // Tổng công ty quản lý danh mục dùng chung và tài khoản quản trị – ngừng thì không ai vào quản trị được.
        var head = Company.Create("TCT", "Tổng công ty", isHeadOffice: true);
        Assert.Throws<BusinessRuleException>(head.Deactivate);

        var member = Company.Create("BHA", "Công ty A", isHeadOffice: false);
        member.Deactivate();
        Assert.False(member.IsActive);
        member.Activate();
        Assert.True(member.IsActive);
    }
}
