using Proc.Domain.Common;
using Proc.Domain.MasterData;

namespace Proc.Domain.Tests.MasterData;

/// <summary>
/// Hàng hóa, nhóm hàng, đơn vị tính và quy đổi ĐVT (design.md mục 5.2).
/// </summary>
public class ItemTests
{
    private static readonly ItemCategory Category = TestCategories.Leaf("VT", "Vật tư");
    private static readonly Uom Hop = Uom.Create("hop", "Hộp");
    private static readonly Uom Thung = Uom.Create("THUNG", "Thùng");

    private static ItemDescription Describe(string name = "Bu lông M8", string? vat = "10") =>
        new(name, "Bolt M8", "M8x50", "Đài Loan", "Đài Loan", "8930000000001", vat, null);

    private static Item NewItem(ItemType type = ItemType.Inventory) => Item.Create(" vt-bl.m8/50 ", type, Category, Hop, Describe());

    [Fact]
    public void TaoHang_ChuanHoaMaVaSinhChuoiTimKiemKhongDau()
    {
        var item = NewItem();

        Assert.Equal("VT-BL.M8/50", item.Code);
        Assert.Equal(Category.Id, item.CategoryId);
        Assert.Equal(Hop.Id, item.BaseUomId);
        Assert.Equal("10", item.VatCode);
        // Gõ "bu long" không dấu vẫn tìm được "Bu lông" (CLAUDE.md mục 5).
        Assert.Contains("bu long m8", item.SearchText);
        Assert.Contains("vt-bl.m8/50", item.SearchText);
    }

    [Theory]
    [InlineData("A")]
    [InlineData("VT 01")]
    [InlineData("-VT")]
    [InlineData("ĐVT01")]
    public void TaoHang_TuChoiMaKhongHopLe(string code)
    {
        var ex = Assert.Throws<BusinessRuleException>(() => Item.Create(code, ItemType.Inventory, Category, Hop, Describe()));
        Assert.Contains("Mã hàng", ex.Message);
    }

    [Fact]
    public void TaoHang_TuChoiThueSuatLaVaTenRong()
    {
        Assert.Throws<BusinessRuleException>(() => Item.Create("VT01", ItemType.Inventory, Category, Hop, Describe(vat: "7")));
        Assert.Throws<BusinessRuleException>(() => Item.Create("VT01", ItemType.Inventory, Category, Hop, Describe(name: " ")));
        Assert.Equal("KCT", Item.Create("VT01", ItemType.Service, Category, Hop, Describe(vat: "kct")).VatCode);
    }

    [Fact]
    public void QuanLyKho_ChiHangTonKhoMoiCoLoHanDungTonToiThieu()
    {
        // Chỉ hàng tồn kho đi qua kho – design.md mục 5.2.
        var service = NewItem(ItemType.Service);
        Assert.Throws<BusinessRuleException>(() => service.SetStockControl(ItemType.Service, trackLot: true, false, null, null));
        Assert.Throws<BusinessRuleException>(() => service.SetStockControl(ItemType.Service, false, false, 10, null));

        var item = NewItem();
        item.SetStockControl(ItemType.Inventory, true, true, 10.5m, 100);
        Assert.Equal((true, true, 10.5m, 100m), (item.TrackLot, item.TrackExpiry, item.MinStock, item.MaxStock));

        Assert.Throws<BusinessRuleException>(() => item.SetStockControl(ItemType.Inventory, false, false, 100, 10));
        Assert.Throws<BusinessRuleException>(() => item.SetStockControl(ItemType.Inventory, false, false, -1, null));
        Assert.Throws<BusinessRuleException>(() => item.SetStockControl(ItemType.Inventory, false, false, 1.23456m, null));
    }

    [Fact]
    public void DatHangLai_AnToanNhoHonDiemDatLaiNhoHonToiDa_ChiHangTonKho()
    {
        // Đề xuất Ban CUVT K1 – design.md mục 18.16.
        var service = NewItem(ItemType.Service);
        Assert.Throws<BusinessRuleException>(() => service.SetReorderControl(null, null, isCritical: true));
        Assert.Throws<BusinessRuleException>(() => service.SetReorderControl(null, 5, false));

        var item = NewItem();
        item.SetStockControl(ItemType.Inventory, false, false, 10, 200);
        item.SetReorderControl(20, 50, true);
        Assert.Equal((20m, 50m, true), (item.SafetyStock, item.ReorderPoint, item.IsCritical));

        Assert.Throws<BusinessRuleException>(() => item.SetReorderControl(60, 50, false));
        Assert.Throws<BusinessRuleException>(() => item.SetReorderControl(null, 250, false));
        Assert.Throws<BusinessRuleException>(() => item.SetReorderControl(-1, null, false));
        item.SetReorderControl(null, null, false);
        Assert.Equal(((decimal?)null, (decimal?)null, false), (item.SafetyStock, item.ReorderPoint, item.IsCritical));
    }

    [Fact]
    public void QuyDoi_ThemSuaXoaTheoDanhSachMoi()
    {
        var item = NewItem();
        var kg = Uom.Create("KG", "Kg");

        item.SetConversions([(Thung, 24m), (kg, 0.001m)]);
        Assert.Equal(2, item.Conversions.Count);
        Assert.Equal(24m, item.Conversions.Single(c => c.UomId == Thung.Id).Factor);

        item.SetConversions([(Thung, 12m)]);
        Assert.Equal(12m, Assert.Single(item.Conversions).Factor);
    }

    [Fact]
    public void QuyDoi_TuChoiTrungDvtCoSoHeSoSaiVaTrungDong()
    {
        var item = NewItem();
        Assert.Throws<BusinessRuleException>(() => item.SetConversions([(Hop, 1m)]));
        Assert.Throws<BusinessRuleException>(() => item.SetConversions([(Thung, 0m)]));
        Assert.Throws<BusinessRuleException>(() => item.SetConversions([(Thung, 1.0000001m)]));
        Assert.Throws<BusinessRuleException>(() => item.SetConversions([(Thung, 24m), (Thung, 12m)]));
    }

    [Fact]
    public void DoiDvtCoSo_KhongTrungDvtQuyDoi()
    {
        var item = NewItem();
        item.SetConversions([(Thung, 24m)]);
        Assert.Throws<BusinessRuleException>(() => item.ChangeBaseUom(Thung));
    }

    [Fact]
    public void DvtVaNhomDaNgung_KhongChonDuocChoHangMoi()
    {
        var old = Uom.Create("CAI", "Cái");
        old.Deactivate();
        Assert.Throws<BusinessRuleException>(() => Item.Create("VT02", ItemType.Inventory, Category, old, Describe()));

        var closed = TestCategories.Leaf("CU", "Nhóm cũ");
        closed.Deactivate();
        Assert.Throws<BusinessRuleException>(() => Item.Create("VT02", ItemType.Inventory, closed, Hop, Describe()));
    }

    [Fact]
    public void NhomHang_SuaTaiKhoanKhoVaDoiNhomCha()
    {
        var root = ItemCategory.Create("NVL", "Nguyên vật liệu");
        var child = ItemCategory.Create("NVLT", "Thép", root);

        child.Update(" Thép cuộn ", "1521");
        Assert.Equal(("Thép cuộn", "1521"), (child.Name, child.InventoryAccount));
        Assert.Throws<BusinessRuleException>(() => child.Update("Thép", "15A"));

        child.MoveTo(null);
        Assert.Null(child.ParentId);
        Assert.Throws<BusinessRuleException>(() => child.MoveTo(child));
    }

    [Fact]
    public void DonViTinh_ChuanHoaMa()
    {
        Assert.Equal("HOP", Hop.Code);
        Assert.Throws<BusinessRuleException>(() => Uom.Create("HỘP", "Hộp"));
        Assert.Throws<BusinessRuleException>(() => Uom.Create("ABCDEFGHIJK", "Quá dài"));
    }

    [Theory]
    [InlineData("Bu lông  Đài Loan", "bu long dai loan")]
    [InlineData("ỐNG NHỰA PVC", "ong nhua pvc")]
    [InlineData("  ", "")]
    public void ChuoiTimKiem_BoDauVaGopKhoangTrang(string input, string expected)
    {
        Assert.Equal(expected, SearchText.Normalize(input));
    }
}
