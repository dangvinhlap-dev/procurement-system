using Proc.Domain.Common;
using Proc.Domain.MasterData;

namespace Proc.Domain.Tests.MasterData;

/// <summary>
/// Nhóm NCC và nhà cung cấp (design.md mục 5.3, 5.8).
/// </summary>
public class SupplierTests
{
    private static readonly SupplierGroup Domestic = SupplierGroup.Create("tn", "Trong nước");

    private static SupplierInfo Info(string? taxCode = "0101234567", bool foreign = false, int? rating = 4) =>
        new("Công ty TNHH Thép Việt", "Thép Việt", foreign, taxCode, "Hà Nội", "024 3888 9999", "ban@thepviet.vn", null,
            "Chuyển khoản 30 ngày", "Thép tấm, thép hình", rating);

    private static Supplier NewSupplier() => Supplier.Create(" ncc-hn/001 ", Domestic, Info());

    [Fact]
    public void TaoNcc_ChuanHoaMa_TrangThaiTiemNang_ChuoiTimKiemKhongDau()
    {
        var supplier = NewSupplier();

        Assert.Equal("NCC-HN/001", supplier.Code);
        Assert.Equal("TN", Domestic.Code);
        Assert.Equal(SupplierStatus.Potential, supplier.Status);
        Assert.Contains("thep viet", supplier.SearchText);
        Assert.Contains("0101234567", supplier.SearchText);
    }

    [Theory]
    [InlineData("12345", false)]
    [InlineData("0101234567-1", false)]
    [InlineData("A", true)]
    [InlineData("TAX 123", true)]
    public void MaSoThue_TrongNuocTheoDinhDangVN_NuocNgoaiChiGioiHanKyTu(string taxCode, bool foreign)
    {
        Assert.Throws<BusinessRuleException>(() => Supplier.Create("NCC01", Domestic, Info(taxCode, foreign)));
    }

    [Fact]
    public void MaSoThue_NccNuocNgoaiDungMaThueNuocSoTai()
    {
        var supplier = Supplier.Create("NK01", Domestic, Info("de-123456789", foreign: true));
        Assert.Equal("DE-123456789", supplier.TaxCode);
    }

    [Fact]
    public void XepHang_Tu1Den5()
    {
        Assert.Throws<BusinessRuleException>(() => Supplier.Create("NCC01", Domestic, Info(rating: 6)));
        Assert.Throws<BusinessRuleException>(() => Supplier.Create("NCC01", Domestic, Info(rating: 0)));
        Assert.Null(Supplier.Create("NCC01", Domestic, Info(rating: null)).Rating);
    }

    [Fact]
    public void TrangThai_TamNgungHoacDanhSachDenPhaiCoLyDo()
    {
        var supplier = NewSupplier();
        Assert.Throws<BusinessRuleException>(() => supplier.SetStatus(SupplierStatus.Blacklisted, "  "));

        supplier.SetStatus(SupplierStatus.Suspended, "Giao hàng lỗi 3 lần");
        Assert.Equal((SupplierStatus.Suspended, "Giao hàng lỗi 3 lần"), (supplier.Status, supplier.StatusNote));

        supplier.SetStatus(SupplierStatus.Approved, null);
        Assert.Null(supplier.StatusNote);
    }

    [Fact]
    public void NguoiLienHe_SuaTheoId_ThemMoi_XoaDongKhongCon()
    {
        var supplier = NewSupplier();
        supplier.SetContacts([new(null, "Nguyễn An", "Giám đốc", null, null, true), new(null, "Trần Bình", null, null, null, false)]);
        var an = supplier.Contacts.Single(c => c.Name == "Nguyễn An");

        // Giữ An (đổi chức vụ), bỏ Bình, thêm Cường: An giữ nguyên Id – nhật ký chỉ ghi "sửa", không ghi xóa rồi tạo lại.
        supplier.SetContacts([new(an.Id, "Nguyễn An", "Tổng giám đốc", null, null, true), new(null, "Lê Cường", null, null, null, false)]);

        Assert.Equal(["Nguyễn An", "Lê Cường"], supplier.Contacts.Select(c => c.Name));
        Assert.Same(an, supplier.Contacts[0]);
        Assert.Equal("Tổng giám đốc", an.Position);
    }

    [Fact]
    public void NguoiLienHe_TuChoiThieuTenVaHaiNguoiChinh()
    {
        var supplier = NewSupplier();
        Assert.Throws<BusinessRuleException>(() => supplier.SetContacts([new(null, " ", null, null, null, false)]));
        Assert.Throws<BusinessRuleException>(() =>
            supplier.SetContacts([new(null, "A", null, null, null, true), new(null, "B", null, null, null, true)]));
        Assert.Throws<BusinessRuleException>(() => supplier.SetContacts([new(Guid.NewGuid(), "A", null, null, null, false)]));
    }

    [Fact]
    public void TaiKhoanNganHang_ChuanHoaSoTaiKhoan_ChanTrungVaHaiMacDinh()
    {
        var supplier = NewSupplier();
        supplier.SetBankAccounts([new(null, "Vietcombank", "Hà Nội", "0011 0000 12345", "CTY THEP VIET", true)]);
        Assert.Equal("0011000012345", Assert.Single(supplier.BankAccounts).AccountNumber);

        Assert.Throws<BusinessRuleException>(() => supplier.SetBankAccounts([
            new(null, "Vietcombank", null, "0011000012345", null, false),
            new(null, "vietcombank", null, "0011 000012345", null, false),
        ]));
        Assert.Throws<BusinessRuleException>(() => supplier.SetBankAccounts([
            new(null, "VCB", null, "1111", null, true),
            new(null, "BIDV", null, "2222", null, true),
        ]));
        Assert.Throws<BusinessRuleException>(() => supplier.SetBankAccounts([new(null, "VCB", null, "12", null, false)]));
    }

    [Fact]
    public void NhomNcc_DaNgung_KhongChonDuocChoNccMoi()
    {
        var closed = SupplierGroup.Create("CU", "Nhóm cũ");
        closed.Deactivate();
        Assert.Throws<BusinessRuleException>(() => Supplier.Create("NCC01", closed, Info()));
        Assert.Throws<BusinessRuleException>(() => SupplierGroup.Create("N", "Quá ngắn"));
    }
}
