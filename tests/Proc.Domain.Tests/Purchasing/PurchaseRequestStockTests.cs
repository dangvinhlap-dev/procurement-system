using Proc.Domain.Common;
using Proc.Domain.MasterData;
using Proc.Domain.Purchasing;

namespace Proc.Domain.Tests.Purchasing;

/// <summary>
/// Thủ kho nhập tồn trên đề nghị mua (lát Y3b, design.md mục 19.3): Cần mua = Cần sử dụng − Tồn (không âm); chỉ loại A, B, khi phiếu chờ
/// duyệt và có nơi nhận.
/// </summary>
public class PurchaseRequestStockTests
{
    private static readonly Guid Company = Guid.CreateVersion7();
    private static readonly Uom Cai = Uom.Create("CAI", "Cái");
    private static readonly Uom Hop = Uom.Create("HOP", "Hộp");
    private static readonly Guid Warehouse = Guid.CreateVersion7();
    private static readonly DateOnly Day = new(2026, 11, 11);

    private static Item NewItem(string code)
    {
        var item = Item.Create(code, ItemType.Inventory, TestCategories.Leaf("VT", "Vật tư"), Cai,
            new ItemDescription("Vòng bi", null, null, null, null, null, "10", null));
        item.SetConversions([(Hop, 10m)]);
        return item;
    }

    private static PurchaseRequest NewRequest(PurchaseCategory category, Guid? warehouseId, Item a, Item b)
    {
        var pr = PurchaseRequest.Create(Company, "PR-1",
            new PurchaseRequestHeader(Day, Guid.CreateVersion7(), null, null, warehouseId, PurchasePriority.Normal, null, category,
                category == PurchaseCategory.Construction ? Guid.CreateVersion7() : null),
            Guid.CreateVersion7());
        pr.SetLines([
            (new PurchaseRequestLineData(null, a.Id, Hop.Id, 5, 100_000, null, null, null), a),
            (new PurchaseRequestLineData(null, b.Id, Cai.Id, 3, 20_000, null, null, null), b),
        ]);
        return pr;
    }

    [Fact]
    public void ChuaNhapTon_CanMuaBangCanSuDung()
    {
        var pr = NewRequest(PurchaseCategory.Goods, Warehouse, NewItem("VT-A"), NewItem("VT-B"));
        var line = pr.Lines[0];
        Assert.Equal((5m, (decimal?)null, 5m, 50m, 500_000m), (line.RequiredQty, line.StockQty, line.Qty, line.BaseQty, line.Amount));
        Assert.Equal(560_000m, pr.TotalAmount);
    }

    [Fact]
    public void NhapTon_TinhCanMua_KhongAm_TinhLaiTong()
    {
        var pr = NewRequest(PurchaseCategory.Construction, Warehouse, NewItem("VT-A"), NewItem("VT-B"));
        pr.Submit();
        var user = Guid.CreateVersion7();
        var at = DateTimeOffset.UtcNow;
        pr.EnterStock(new Dictionary<Guid, decimal> { [pr.Lines[0].Id] = 2, [pr.Lines[1].Id] = 7.5m }, user, at);

        // Dòng 1: cần 5 hộp, tồn 2 hộp → mua 3 hộp = 30 cái; dòng 2: tồn 7,5 ≥ cần 3 → không mua.
        Assert.Equal((5m, (decimal?)2m, 3m, 30m, 300_000m), (pr.Lines[0].RequiredQty, pr.Lines[0].StockQty, pr.Lines[0].Qty, pr.Lines[0].BaseQty, pr.Lines[0].Amount));
        Assert.Equal((3m, (decimal?)7.5m, 0m, 0m), (pr.Lines[1].RequiredQty, pr.Lines[1].StockQty, pr.Lines[1].Qty, pr.Lines[1].Amount));
        Assert.Equal(300_000m, pr.TotalAmount);
        Assert.Equal(((Guid?)user, (DateTimeOffset?)at), (pr.StockConfirmedBy, pr.StockConfirmedAt));

        // Nhập lại (thủ kho sửa) khi chưa cấp nào duyệt.
        pr.EnterStock(new Dictionary<Guid, decimal> { [pr.Lines[0].Id] = 0, [pr.Lines[1].Id] = 0 }, user, at);
        Assert.Equal(560_000m, pr.TotalAmount);
    }

    [Fact]
    public void NhapTon_ChanDichVu_ChuaGui_ThieuNoiNhan_ThieuDong_SoSai()
    {
        var a = NewItem("VT-A");
        var b = NewItem("VT-B");
        var user = Guid.CreateVersion7();
        var at = DateTimeOffset.UtcNow;

        var service = NewRequest(PurchaseCategory.Service, Warehouse, a, b);
        service.Submit();
        Assert.Contains("dịch vụ", Assert.Throws<BusinessRuleException>(() => service.EnterStock(All(service, 1), user, at)).Message);

        var draft = NewRequest(PurchaseCategory.Goods, Warehouse, a, b);
        Assert.Contains("chờ duyệt", Assert.Throws<BusinessRuleException>(() => draft.EnterStock(All(draft, 1), user, at)).Message);

        var noWarehouse = NewRequest(PurchaseCategory.Goods, null, a, b);
        noWarehouse.Submit();
        Assert.Contains("nơi nhận", Assert.Throws<BusinessRuleException>(() => noWarehouse.EnterStock(All(noWarehouse, 1), user, at)).Message);

        var pr = NewRequest(PurchaseCategory.Goods, Warehouse, a, b);
        pr.Submit();
        Assert.Contains("Dòng 2", Assert.Throws<BusinessRuleException>(() =>
            pr.EnterStock(new Dictionary<Guid, decimal> { [pr.Lines[0].Id] = 1 }, user, at)).Message);
        Assert.Throws<BusinessRuleException>(() => pr.EnterStock(All(pr, -1), user, at));
        Assert.Throws<BusinessRuleException>(() => pr.EnterStock(All(pr, 1.00001m), user, at));
        Assert.Throws<BusinessRuleException>(() =>
            pr.EnterStock(new Dictionary<Guid, decimal> { [pr.Lines[0].Id] = 1, [pr.Lines[1].Id] = 1, [Guid.CreateVersion7()] = 1 }, user, at));
        Assert.Null(pr.StockConfirmedAt);
    }

    [Fact]
    public void VeNhap_SuaDong_GiuTonNeuCungHangCungDvt_DoiThiBoTon()
    {
        var a = NewItem("VT-A");
        var b = NewItem("VT-B");
        var pr = NewRequest(PurchaseCategory.Goods, Warehouse, a, b);
        pr.Submit();
        pr.EnterStock(All(pr, 1), Guid.CreateVersion7(), DateTimeOffset.UtcNow);
        pr.ReturnToDraft("Sửa SL");

        // Dòng 1 giữ hàng + ĐVT, đổi SL cần → giữ tồn, tính lại cần mua; dòng 2 đổi ĐVT → bỏ tồn (thủ kho nhập lại ở lượt sau).
        pr.SetLines([
            (new PurchaseRequestLineData(pr.Lines[0].Id, a.Id, Hop.Id, 8, 100_000, null, null, null), a),
            (new PurchaseRequestLineData(pr.Lines[1].Id, b.Id, Hop.Id, 1, 200_000, null, null, null), b),
        ]);
        Assert.Equal((8m, (decimal?)1m, 7m), (pr.Lines[0].RequiredQty, pr.Lines[0].StockQty, pr.Lines[0].Qty));
        Assert.Equal((1m, (decimal?)null, 1m), (pr.Lines[1].RequiredQty, pr.Lines[1].StockQty, pr.Lines[1].Qty));
    }

    private static Dictionary<Guid, decimal> All(PurchaseRequest pr, decimal stock) => pr.Lines.ToDictionary(l => l.Id, _ => stock);
}
