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
    public void MuaKhanCap_BatBuocLyDoVaHanHoSo_QuaHanKhiChuaXacNhan()
    {
        var item = NewItem();
        var date = new DateOnly(2026, 11, 2);
        PurchaseOrderHeader Header(string? reason, DateOnly? due) =>
            new(date, Guid.CreateVersion7(), Guid.CreateVersion7(), null, null, null, null, 0, null, null, PurchaseType.Emergency, reason, due);
        Assert.Contains("lý do khẩn cấp", Assert.Throws<BusinessRuleException>(() => PurchaseOrder.Create(Company, "PO-K", Header(" ", date.AddDays(7)))).Message);
        Assert.Contains("hạn bổ sung hồ sơ", Assert.Throws<BusinessRuleException>(() => PurchaseOrder.Create(Company, "PO-K", Header("Hỏng máy", null))).Message);
        Assert.Throws<BusinessRuleException>(() => PurchaseOrder.Create(Company, "PO-K", Header("Hỏng máy", date.AddDays(31))));

        var po = PurchaseOrder.Create(Company, "PO-K", Header(" Hỏng máy cán số 2 ", date.AddDays(7)));
        Assert.Equal((PurchaseType.Emergency, "Hỏng máy cán số 2"), (po.PurchaseType, po.EmergencyReason));
        po.SetLines([(new PurchaseOrderLineData(null, item.Id, Cai.Id, 1, 100, 0, "10", null, null, null), item)]);
        Assert.Throws<BusinessRuleException>(() => po.CompleteDocs(Guid.CreateVersion7(), DateTimeOffset.UtcNow, "Đã đủ"));
        po.Submit();
        po.MarkApproved(DateTimeOffset.UtcNow);
        Assert.False(po.IsDocsOverdue(date.AddDays(7)));
        Assert.True(po.IsDocsOverdue(date.AddDays(8)));
        Assert.Throws<BusinessRuleException>(() => po.CompleteDocs(Guid.CreateVersion7(), DateTimeOffset.UtcNow, " "));
        po.CompleteDocs(Guid.CreateVersion7(), DateTimeOffset.UtcNow, "Biên bản sự cố, 3 báo giá so sánh");
        Assert.False(po.IsDocsOverdue(date.AddDays(8)));
        Assert.Throws<BusinessRuleException>(() => po.CompleteDocs(Guid.CreateVersion7(), DateTimeOffset.UtcNow, "Lần hai"));

        // Đổi về mua thường thì xóa lý do và hạn.
        var normal = PurchaseOrder.Create(Company, "PO-T", Header("Hỏng máy", date.AddDays(7)) with { PurchaseType = PurchaseType.Normal });
        Assert.Equal(((string?)null, (DateOnly?)null), (normal.EmergencyReason, normal.DocsDueDate));
    }

    [Fact]
    public void HangMoiChuaCoMa_TinhVaoTong_GanMaSinhDongCoMa_KhongDemHaiLan()
    {
        var item = NewItem();
        var kg = Uom.Create("KG", "Kg");
        var pr = PurchaseRequest.Create(Company, "PR-1",
            new PurchaseRequestHeader(new DateOnly(2026, 11, 2), Guid.CreateVersion7(), null, null, null, PurchasePriority.Normal, null), null);
        Assert.Throws<BusinessRuleException>(() => pr.SetLines([]));
        Assert.Contains("mô tả", Assert.Throws<BusinessRuleException>(() => pr.SetNewItems([new(null, " ", null, Cai.Id, 1, 0, null, null)])).Message);
        pr.SetNewItems([new(null, "Vòng bi gốm", "Si3N4", Cai.Id, 4, 250_000, null, null), new(null, "Dây curoa", null, kg.Id, 2, 0, null, null)]);
        pr.SetLines([]);
        Assert.Equal(1_000_000m, pr.TotalAmount);
        Assert.Throws<BusinessRuleException>(() => pr.LinkNewItem(pr.NewItems[0].Id, item));

        pr.Submit();
        pr.MarkApproved(DateTimeOffset.UtcNow);
        // ĐVT kg không thuộc mặt hàng (cái, thùng) thì không gắn được.
        Assert.Contains("ĐVT", Assert.Throws<BusinessRuleException>(() => pr.LinkNewItem(pr.NewItems[1].Id, item)).Message);
        var line = pr.LinkNewItem(pr.NewItems[0].Id, item);
        Assert.Equal((1, 4m, 250_000m, 1_000_000m), (line.LineNo, line.Qty, line.EstPrice, pr.TotalAmount));
        Assert.Equal((item.Id, line.Id), (pr.NewItems[0].ItemId!.Value, pr.NewItems[0].LinkedLineId!.Value));
        Assert.Throws<BusinessRuleException>(() => pr.LinkNewItem(pr.NewItems[0].Id, item));
    }

    [Fact]
    public void HanSla_TinhNgayLamViec_BoQuaThu7ChuNhat()
    {
        var friday = new DateOnly(2026, 11, 6);
        Assert.Equal(friday, PurchaseSla.DueDate(friday, 0));
        Assert.Equal(new DateOnly(2026, 11, 9), PurchaseSla.DueDate(friday, 1));
        Assert.Equal(new DateOnly(2026, 11, 13), PurchaseSla.DueDate(friday, 5));
        Assert.Equal(new DateOnly(2026, 11, 9), PurchaseSla.DueDate(new DateOnly(2026, 11, 7), 0));
        Assert.Equal(new DateOnly(2026, 11, 10), PurchaseSla.DueDate(new DateOnly(2026, 11, 8), 1));

        var parameter = Proc.Domain.Organization.CompanyParameter.Create(Company);
        Assert.Equal((1, 5, 1), (parameter.SlaPricedDays, parameter.SlaSourcingDays, parameter.SlaUrgentDays));
        parameter.ChangeSlaDays(SlaCategory.Sourcing, 3);
        Assert.Equal(3, parameter.SlaSourcingDays);
        Assert.Throws<BusinessRuleException>(() => parameter.ChangeSlaDays(SlaCategory.Priced, 61));
        parameter.ChangeSlaDays(SlaCategory.Sourcing, null);
        Assert.Equal(5, parameter.SlaSourcingDays);
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
