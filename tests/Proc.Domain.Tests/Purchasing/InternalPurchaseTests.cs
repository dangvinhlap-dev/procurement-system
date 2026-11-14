using Proc.Domain.Common;
using Proc.Domain.MasterData;
using Proc.Domain.Purchasing;

namespace Proc.Domain.Tests.Purchasing;

/// <summary>Lát X7 (design.md mục 19.3): mua nội bộ qua PO đặt cho NCC nội bộ, giá bán nội bộ chỉ gợi ý, công ty bán xác nhận đơn.</summary>
public class InternalPurchaseTests
{
    private static readonly Guid Buyer = Guid.CreateVersion7();
    private static readonly Guid Seller = Guid.CreateVersion7();
    private static readonly Uom Cai = Uom.Create("CAI", "Cái");

    private static (PurchaseOrder Order, Item Item) NewOrder()
    {
        var item = Item.Create("VB09", ItemType.Inventory, TestCategories.Leaf("VT", "Vật tư"), Cai,
            new ItemDescription("Vòng bi", null, null, null, null, null, "10", null));
        var order = PurchaseOrder.Create(Buyer, "PO-1",
            new PurchaseOrderHeader(new DateOnly(2026, 11, 12), Guid.CreateVersion7(), Guid.CreateVersion7(), null, null, null, null, 0, null));
        order.SetLines([
            (new PurchaseOrderLineData(null, item.Id, Cai.Id, 10, 16_000, 0, "10", null, null, null), item),
            (new PurchaseOrderLineData(null, item.Id, Cai.Id, 5, 20_000, 0, "10", null, null, null), item),
        ]);
        return (order, item);
    }

    [Fact]
    public void KhongTuMuaCuaMinh_ChiNccNoiBoMoiApGiaNoiBo()
    {
        var (order, _) = NewOrder();
        Assert.Throws<BusinessRuleException>(() => order.SetInternalSeller(Buyer));
        Assert.Throws<BusinessRuleException>(() => order.ApplyInternalPrices(_ => 15_000));
        order.SetInternalSeller(Seller);
        Assert.Equal(Seller, order.InternalSellerCompanyId);
        order.SetInternalSeller(null);
        Assert.Null(order.InternalSellerCompanyId);
    }

    [Fact]
    public void GiaNoiBoChiGoiY_NhapTayKhongTinhLech_KhongQuaChangNgoaiLe()
    {
        var (order, _) = NewOrder();
        order.SetInternalSeller(Seller);
        // Dòng 1 có giá gợi ý 15.000 nhưng người lập gõ 16.000; dòng 2 công ty bán chưa khai giá.
        order.ApplyInternalPrices(l => l.LineNo == 1 ? 15_000m : null);

        Assert.Equal([PurchasePriceSource.Internal, PurchasePriceSource.Internal], order.Lines.Select(l => l.PriceSource));
        Assert.Equal([15_000m, (decimal?)null], order.Lines.Select(l => l.ApprovedUnitPrice));
        Assert.All(order.Lines, l => Assert.False(l.IsPriceDeviation || l.IsOutOfContract || l.NeedsOffListApproval));
        Assert.False(order.HasPriceException || order.IsOutOfContract || order.IsOffApprovedList);
        order.EnsurePriceDeviationsExplained();
    }

    [Fact]
    public void XacNhanDonBan_TuChoiBatBuocLyDo_HaiCongTyKhacNhau()
    {
        var po = Guid.CreateVersion7();
        Assert.Throws<BusinessRuleException>(() =>
            InternalSaleConfirmation.Create(Seller, po, "PO-1", Buyer, InternalSaleDecision.Rejected, "  "));
        Assert.Throws<BusinessRuleException>(() =>
            InternalSaleConfirmation.Create(Seller, po, "PO-1", Seller, InternalSaleDecision.Confirmed, null));
        Assert.Throws<BusinessRuleException>(() =>
            InternalSaleConfirmation.Create(Seller, po, "PO-1", Buyer, InternalSaleDecision.Confirmed, new string('x', 501)));

        var rejected = InternalSaleConfirmation.Create(Seller, po, " PO-1 ", Buyer, InternalSaleDecision.Rejected, " Hết hàng ");
        Assert.Equal((Seller, "PO-1", InternalSaleDecision.Rejected, "Hết hàng"), (rejected.CompanyId, rejected.OrderNo, rejected.Decision, rejected.Note));
        var confirmed = InternalSaleConfirmation.Create(Seller, po, "PO-1", Buyer, InternalSaleDecision.Confirmed, null);
        Assert.Null(confirmed.Note);
    }
}
