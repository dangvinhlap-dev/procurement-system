using Proc.Domain.Common;
using Proc.Domain.MasterData;

namespace Proc.Domain.Tests.MasterData;

/// <summary>
/// NCC áp dụng cho công ty và NCC – sản phẩm (design.md mục 5.3, 5.9).
/// </summary>
public class SupplierLinkTests
{
    private static readonly SupplierGroup Group = SupplierGroup.Create("TN", "Trong nước");
    private static readonly Uom Hop = Uom.Create("HOP", "Hộp");
    private static readonly Uom Thung = Uom.Create("THUNG", "Thùng");
    private static readonly Uom Kg = Uom.Create("KG", "Kg");

    private static Supplier NewSupplier() =>
        Supplier.Create("NCC01", Group, new SupplierInfo("Thép Việt", null, false, null, null, null, null, null, null, null, null));

    private static Item NewItem()
    {
        var item = Item.Create("VT01", ItemType.Inventory, ItemCategory.Create("VT", "Vật tư"), Hop,
            new ItemDescription("Bu lông", null, null, null, null, null, null, null));
        item.SetConversions([(Thung, 24m)]);
        return item;
    }

    [Fact]
    public void ApDungCongTy_ChuanHoaMaTaiCongTy()
    {
        var link = SupplierCompany.Apply(NewSupplier(), Guid.CreateVersion7(), " 331-thepviet ", new DateOnly(2026, 11, 1));
        Assert.Equal(("331-THEPVIET", true), (link.LocalCode, link.IsActive));
        Assert.Throws<BusinessRuleException>(() => SupplierCompany.Apply(NewSupplier(), Guid.CreateVersion7(), "MÃ CÓ DẤU", null));
    }

    [Fact]
    public void ApDungCongTy_NccDanhSachDen_KhongBatGiaoDichDuoc()
    {
        var supplier = NewSupplier();
        var link = SupplierCompany.Apply(supplier, Guid.CreateVersion7(), null, null);
        supplier.SetStatus(SupplierStatus.Blacklisted, "Hàng giả");

        Assert.Throws<BusinessRuleException>(() => SupplierCompany.Apply(supplier, Guid.CreateVersion7(), null, null));
        Assert.Throws<BusinessRuleException>(() => link.Update(supplier, null, null, isActive: true));
        link.Update(supplier, null, null, isActive: false);
        Assert.False(link.IsActive);
    }

    [Fact]
    public void NccSanPham_DvtMuaPhaiThuocMatHang()
    {
        var item = NewItem();
        var link = SupplierItem.Create(Guid.CreateVersion7(), item, new SupplierItemData(" BL-M8 ", Thung.Id, 10, 7, true));
        Assert.Equal(("BL-M8", Thung.Id, 10m, 7, true), (link.SupplierItemCode, link.PurchaseUomId, link.Moq, link.LeadTimeDays, link.IsPreferred));

        // Kg không phải ĐVT cơ sở hay quy đổi của mặt hàng: nhập kho sẽ không tính được số tồn.
        Assert.Throws<BusinessRuleException>(() => link.Update(item, new SupplierItemData(null, Kg.Id, null, null, false)));
        link.Update(item, new SupplierItemData(null, Hop.Id, null, null, false));
        Assert.Equal(Hop.Id, link.PurchaseUomId);
    }

    [Theory]
    [InlineData(0.0, null)]
    [InlineData(1.23456, null)]
    [InlineData(null, -1)]
    [InlineData(null, 366)]
    public void NccSanPham_TuChoiMoqVaLeadTimeKhongHopLe(double? moq, int? leadTime)
    {
        var item = NewItem();
        Assert.Throws<BusinessRuleException>(() =>
            SupplierItem.Create(Guid.CreateVersion7(), item, new SupplierItemData(null, Hop.Id, (decimal?)moq, leadTime, false)));
    }

    [Fact]
    public void NccSanPham_HangNgungDung_KhongThemDuoc()
    {
        var item = NewItem();
        item.Deactivate();
        Assert.Throws<BusinessRuleException>(() => SupplierItem.Create(Guid.CreateVersion7(), item, new SupplierItemData(null, Hop.Id, null, null, false)));
    }
}
