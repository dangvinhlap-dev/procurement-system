using Proc.Domain.Common;
using Proc.Domain.MasterData;

namespace Proc.Domain.Tests.MasterData;

/// <summary>Đánh số danh mục tự động theo dải cấu hình (design.md mục 5.11).</summary>
public class CatalogNumberingTests
{
    [Fact]
    public void MaKeTiep_SauSoLonNhatTrongDai_BoQuaMaKhongDungMau()
    {
        var n = CatalogNumbering.Create(NumberedCatalogs.Supplier, null, "ncc", 5, 1, 99999);
        Assert.Equal("NCC", n.Prefix);
        Assert.Equal("NCC00001", n.NextCode("NCC", []));
        // NCC1 (thiếu chữ số), NCCX0001 (khác tiền tố) và mã ngoài dải không tính.
        Assert.Equal("NCC00013", n.NextCode("NCC", ["NCC00007", "NCC00012", "NCC1", "NCCX0001", "NCC123456"]));
    }

    [Fact]
    public void HetDai_BaoLoi_DaiBatDauKhacMot()
    {
        var n = CatalogNumbering.Create(NumberedCatalogs.SupplierGroup, null, "GN", 2, 10, 12);
        Assert.Equal("GN10", n.NextCode("GN", ["GN05"]));
        Assert.Contains("Hết dải mã", Assert.Throws<BusinessRuleException>(() => n.NextCode("GN", ["GN10", "GN11", "GN12"])).Message);
    }

    [Fact]
    public void MaHang_TienToTheoNhom()
    {
        var n = CatalogNumbering.Create(NumberedCatalogs.Item, null, "{nhom}-", 5, 1, 99999);
        Assert.Equal("{NHOM}-", n.Prefix);
        Assert.Equal("VT-", n.ResolvePrefix("VT"));
        Assert.Equal("VT-00004", n.NextCode("VT-", ["VT-00003", "VPP-00009"]));
    }

    [Fact]
    public void CauHinhSai_BiChan()
    {
        // Nhóm hàng chỉ chữ số; {NHOM} chỉ cho hàng hóa; nhóm hàng chỉ cho hàng hóa.
        Assert.Throws<BusinessRuleException>(() => CatalogNumbering.Create(NumberedCatalogs.ItemCategory, null, "NH-", 3, 1, 999));
        Assert.Throws<BusinessRuleException>(() => CatalogNumbering.Create(NumberedCatalogs.Supplier, null, "{NHOM}", 3, 1, 999));
        Assert.Throws<BusinessRuleException>(() => CatalogNumbering.Create(NumberedCatalogs.Supplier, Guid.CreateVersion7(), "NCC", 3, 1, 999));
        // Dải vượt số chữ số; đến số nhỏ hơn từ số.
        Assert.Throws<BusinessRuleException>(() => CatalogNumbering.Create(NumberedCatalogs.Supplier, null, "NCC", 3, 1, 1000));
        Assert.Throws<BusinessRuleException>(() => CatalogNumbering.Create(NumberedCatalogs.Supplier, null, "NCC", 3, 50, 10));
        // Mã có thể vượt độ dài: nhóm hàng tối đa 20 ký tự = tiền tố + số.
        Assert.Contains("vượt độ dài", Assert.Throws<BusinessRuleException>(() =>
            CatalogNumbering.Create(NumberedCatalogs.ItemCategory, null, "NHOMHANGRATDAI", 8, 1, 99)).Message);
        Assert.Throws<BusinessRuleException>(() => CatalogNumbering.Create("uom", null, "DV", 3, 1, 999));
    }
}
