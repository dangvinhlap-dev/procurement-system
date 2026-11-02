using Proc.Domain.Common;
using Proc.Domain.MasterData;
using Proc.Domain.Purchasing;

namespace Proc.Domain.Tests.Purchasing;

/// <summary>PO theo giá đã duyệt (design.md mục 18.10): chọn bậc giá, lệch giá, cờ ngoài hợp đồng, cấp duyệt ngoại lệ.</summary>
public class PurchaseOrderPriceTests
{
    private static readonly Guid Company = Guid.CreateVersion7();
    private static readonly Uom Cai = Uom.Create("CAI", "Cái");
    private static readonly Uom Thung = Uom.Create("THUNG", "Thùng");

    private static Item NewItem()
    {
        var item = Item.Create("VB02", ItemType.Inventory, ItemCategory.Create("VT", "Vật tư"), Cai,
            new ItemDescription("Vòng bi", null, null, null, null, null, "10", null));
        item.SetConversions([(Thung, 12m)]);
        return item;
    }

    [Fact]
    public void ChonBacGia_CungDvtLayBacLonNhat_KhacDvtQuyTuCoSo()
    {
        PriceTier[] tiers = [new(Cai.Id, 1, 0, 50_000, 5), new(Cai.Id, 1, 100, 48_000, 5)];
        Assert.Equal((48_000m, 5m), PriceTierPicker.Pick(tiers, Cai.Id, 1, 120));
        Assert.Equal((50_000m, 5m), PriceTierPicker.Pick(tiers, Cai.Id, 1, 99));
        // 2 thùng × 12 = 24 cái → bậc 0 cái, giá thùng = 50.000 × 12.
        Assert.Equal((600_000m, 5m), PriceTierPicker.Pick(tiers, Thung.Id, 12, 2));
        // 10 thùng = 120 cái ≥ bậc 100 cái → 48.000 × 12.
        Assert.Equal((576_000m, 5m), PriceTierPicker.Pick(tiers, Thung.Id, 12, 10));
        Assert.Null(PriceTierPicker.Pick([new PriceTier(Cai.Id, 1, 10, 50_000, 0)], Cai.Id, 1, 5));
    }

    [Fact]
    public void LechGia_SoSauChietKhau_BatBuocLyDoKhiGui_HetLechThiXoaLyDo()
    {
        var item = NewItem();
        var po = PurchaseOrder.Create(Company, "PO-1",
            new PurchaseOrderHeader(new DateOnly(2026, 11, 2), Guid.CreateVersion7(), Guid.CreateVersion7(), null, null, null, null, 0, null));
        po.SetLines([
            (new PurchaseOrderLineData(null, item.Id, Cai.Id, 10, 47_500, 0, "10", null, null, null), item),
            (new PurchaseOrderLineData(null, item.Id, Cai.Id, 10, 52_000, 5, "10", null, null, null), item),
            (new PurchaseOrderLineData(null, item.Id, Thung.Id, 1, 700_000, 0, "10", null, null, null), item),
        ]);
        var approved = new ApprovedPrice(PurchasePriceSource.Contract, 50_000, 5, Guid.CreateVersion7(), Guid.CreateVersion7());
        po.ApplyApprovedPrices(l => l.UomId == Cai.Id ? approved : null);

        // 47.500 CK 0% = 50.000 CK 5% → không lệch; 52.000 CK 5% → lệch; thùng không có giá duyệt → ngoài hợp đồng, không lệch.
        Assert.Equal([false, true, false], po.Lines.Select(l => l.IsPriceDeviation));
        Assert.Equal([false, true, true], po.Lines.Select(l => l.IsOutOfContract));
        Assert.Equal([PurchasePriceSource.Contract, PurchasePriceSource.Contract, PurchasePriceSource.None], po.Lines.Select(l => l.PriceSource));
        Assert.True(po.HasPriceException && po.IsOutOfContract);
        var ex = Assert.Throws<BusinessRuleException>(po.EnsurePriceDeviationsExplained);
        Assert.StartsWith("Dòng 2:", ex.Message);

        po.SetLines([
            (new PurchaseOrderLineData(po.Lines[0].Id, item.Id, Cai.Id, 10, 50_000, 5, "10", null, null, null, "Thừa"), item),
            (new PurchaseOrderLineData(po.Lines[1].Id, item.Id, Cai.Id, 10, 52_000, 5, "10", null, null, null, " Tăng giá thép "), item),
        ]);
        po.ApplyApprovedPrices(_ => approved);
        Assert.Equal([null, "Tăng giá thép"], po.Lines.Select(l => l.PriceDeviationReason));
        po.EnsurePriceDeviationsExplained();
        Assert.Equal([false, true], po.Lines.Select(l => l.IsPriceDeviation));
    }

    [Fact]
    public void CapDuyetNgoaiLe_LuonItNhatMotCap()
    {
        var role1 = Guid.CreateVersion7();
        var role2 = Guid.CreateVersion7();
        ApprovalRule[] rules = [ApprovalRule.Create(Company, "POX", 1, 50_000_000, role1), ApprovalRule.Create(Company, "pox", 2, 500_000_000, role2)];
        Assert.Equal([(1, (Guid?)role1)], ApprovalPlan.ExceptionLevels(rules, 10_000_000));
        Assert.Equal([(1, (Guid?)role1), (2, (Guid?)role2)], ApprovalPlan.ExceptionLevels(rules, 600_000_000));
        Assert.Equal([(1, (Guid?)null)], ApprovalPlan.ExceptionLevels([], 1));
    }
}
