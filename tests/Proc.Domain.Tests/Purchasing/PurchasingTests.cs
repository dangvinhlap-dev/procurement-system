using Proc.Domain.Common;
using Proc.Domain.MasterData;
using Proc.Domain.Purchasing;

namespace Proc.Domain.Tests.Purchasing;

/// <summary>Quy trình duyệt theo ngưỡng (design.md mục 6.2), PR (mục 10.2), PO (mục 10.3).</summary>
public class PurchasingTests
{
    private static readonly Guid Company = Guid.CreateVersion7();
    private static readonly Uom Cai = Uom.Create("CAI", "Cái");
    private static readonly Uom Hop = Uom.Create("HOP", "Hộp");

    private static Item NewItem()
    {
        var item = Item.Create("VB01", ItemType.Inventory, ItemCategory.Create("VT", "Vật tư"), Cai,
            new ItemDescription("Vòng bi", null, null, null, null, null, "10", null));
        item.SetConversions([(Hop, 10m)]);
        return item;
    }

    [Fact]
    public void KeHoachDuyet_TheoNguong_ChuaCauHinhThiMotCap()
    {
        var head = Guid.CreateVersion7();
        var director = Guid.CreateVersion7();
        var rules = new[]
        {
            ApprovalRule.Create(Company, "pr", 1, 0, head),
            ApprovalRule.Create(Company, "PR", 2, 50_000_000, director),
        };

        Assert.Equal([(1, (Guid?)head)], ApprovalPlan.Levels(rules, 10_000_000));
        Assert.Equal([(1, (Guid?)head), (2, (Guid?)director)], ApprovalPlan.Levels(rules, 80_000_000));
        Assert.Equal([(1, (Guid?)null)], ApprovalPlan.Levels([], 1));
        // Có cấu hình nhưng dưới mọi ngưỡng: không cần duyệt.
        Assert.Empty(ApprovalPlan.Levels([ApprovalRule.Create(Company, "PO", 1, 1_000_000, head)], 500_000));

        Assert.Throws<BusinessRuleException>(() => ApprovalRule.Create(Company, "GRN", 1, 0, head));
        Assert.Throws<BusinessRuleException>(() => ApprovalRule.Create(Company, "PR", 10, 0, head));
        Assert.Throws<BusinessRuleException>(() => ApprovalRule.Create(Company, "PR", 1, -1, head));
    }

    [Fact]
    public void BuocDuyet_TuChoiPhaiCoLyDo_KhongDuyetHaiLan()
    {
        var step = ApprovalStep.Create(Company, "PR", Guid.CreateVersion7(), 1, 1, null);

        Assert.Throws<BusinessRuleException>(() => step.Reject(Guid.CreateVersion7(), DateTimeOffset.UtcNow, " "));
        step.Approve(Guid.CreateVersion7(), DateTimeOffset.UtcNow, "Đồng ý");
        Assert.Throws<BusinessRuleException>(() => step.Approve(Guid.CreateVersion7(), DateTimeOffset.UtcNow, null));
        Assert.Equal((ApprovalStepStatus.Approved, "Đồng ý"), (step.Status, step.Comment));
    }

    [Fact]
    public void DeNghiMuaHang_GuiDuyet_TuChoiVeNhap_GuiLaiTangLuot()
    {
        var item = NewItem();
        var pr = PurchaseRequest.Create(Company, "PR-1",
            new PurchaseRequestHeader(new DateOnly(2026, 10, 1), "Xưởng cán", "Bảo trì", new DateOnly(2026, 10, 15), null, PurchasePriority.Urgent, null),
            Guid.CreateVersion7());
        Assert.Throws<BusinessRuleException>(() => pr.Submit());
        pr.SetLines([(new PurchaseRequestLineData(null, item.Id, Hop.Id, 2, 120_000, null, null, null), item)]);
        Assert.Equal((20m, 240_000m), (pr.Lines[0].BaseQty, pr.TotalAmount));

        Assert.Equal(1, pr.Submit());
        Assert.Throws<BusinessRuleException>(() => pr.SetLines([(new PurchaseRequestLineData(null, item.Id, Cai.Id, 1, 1, null, null, null), item)]));
        pr.ReturnToDraft("Thiếu báo giá");
        Assert.Equal((ApprovalDocumentStatus.Draft, "Thiếu báo giá"), (pr.Status, pr.RejectReason));
        Assert.Equal(2, pr.Submit());
        Assert.Null(pr.RejectReason);
        pr.MarkApproved(DateTimeOffset.UtcNow);
        Assert.Equal(ApprovalDocumentStatus.Approved, pr.Status);

        Assert.Throws<BusinessRuleException>(() =>
            PurchaseRequest.Create(Company, "PR-2", new PurchaseRequestHeader(new DateOnly(2026, 10, 10), null, null, new DateOnly(2026, 10, 1), null, PurchasePriority.Normal, null), null));
    }

    [Fact]
    public void DonMuaHang_TinhChietKhauVat_GiaThucMua_DongMoNhanHang()
    {
        var item = NewItem();
        var po = PurchaseOrder.Create(Company, "PO-1",
            new PurchaseOrderHeader(new DateOnly(2026, 10, 1), Guid.CreateVersion7(), Guid.CreateVersion7(), new DateOnly(2026, 10, 10), "30 ngày", null, null, 5, null));
        po.SetLines([(new PurchaseOrderLineData(null, item.Id, Cai.Id, 10, 100_000, 5, "10", null, null, null), item)]);

        var line = po.Lines[0];
        Assert.Equal((1_000_000m, 50_000m, 950_000m, 95_000m, 95_000m), (line.GrossAmount, line.DiscountAmount, line.Amount, line.VatAmount, line.NetUnitPrice));
        Assert.Equal((950_000m, 95_000m, 1_045_000m), (po.TotalAmount, po.TotalVat, po.ApprovalAmount));

        po.Submit();
        po.MarkApproved(DateTimeOffset.UtcNow);
        po.CloseReceived();
        Assert.Equal(ApprovalDocumentStatus.Closed, po.Status);
        Assert.Throws<BusinessRuleException>(po.Cancel);
        po.ReopenReceiving();
        Assert.Equal(ApprovalDocumentStatus.Approved, po.Status);

        Assert.Throws<BusinessRuleException>(() =>
            PurchaseOrder.Create(Company, "PO-2", new PurchaseOrderHeader(new DateOnly(2026, 10, 1), Guid.CreateVersion7(), Guid.CreateVersion7(), null, null, null, null, 25, null)));
    }
}
