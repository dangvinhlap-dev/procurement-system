using Proc.Domain.Common;
using Proc.Domain.MasterData;
using Proc.Domain.Purchasing;

namespace Proc.Domain.Tests.Purchasing;

/// <summary>NCC báo giao hàng trên Portal, XNH lập theo phiếu báo giao (lát P5 – design.md mục 19.5, P5-1, P5-2, P5-5).</summary>
public class PurchaseOrderDeliveryNoticeTests
{
    private static readonly Uom Cai = Uom.Create("CAI", "Cái");
    private static readonly DateTimeOffset Now = new(2026, 11, 13, 3, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly OrderDate = new(2026, 11, 2);

    private static PurchaseOrder Order(Guid supplierId, bool approve = true)
    {
        var category = ItemCategory.Create("VT", "Vật tư");
        var bearing = Item.Create("VB01", ItemType.Inventory, category, Cai, new ItemDescription("Vòng bi", null, null, null, null, null, "10", null));
        var bolt = Item.Create("BL01", ItemType.Inventory, category, Cai, new ItemDescription("Bu lông", null, null, null, null, null, "10", null));
        var order = PurchaseOrder.Create(Guid.CreateVersion7(), "PO-1",
            new PurchaseOrderHeader(OrderDate, supplierId, Guid.CreateVersion7(), null, null, null, null, 0, null));
        order.SetLines([
            (new PurchaseOrderLineData(null, bearing.Id, Cai.Id, 10, 50_000, 0, "10", null, null, null), bearing),
            (new PurchaseOrderLineData(null, bolt.Id, Cai.Id, 100, 2_000, 0, "10", null, null, null), bolt),
        ]);
        order.Submit();
        if (approve) order.MarkApproved(Now);
        return order;
    }

    private static PurchaseOrderDeliveryNotice Notice(
        PurchaseOrder order, Guid supplierId, DateOnly date, string? noteNo, params DeliveryNoticeLineData[] lines) =>
        PurchaseOrderDeliveryNotice.Create(order, "BGH-1", supplierId, Guid.CreateVersion7(), "kho@ncc.vn",
            new DeliveryNoticeHeader(date, noteNo, " Xe 51C-123.45 ", null), lines, Now);

    [Fact]
    public void BaoGiao_HopLe_LuuDongTheoDongPo()
    {
        var supplier = Guid.CreateVersion7();
        var order = Order(supplier);
        var first = order.Lines[0];
        var notice = Notice(order, supplier, OrderDate.AddDays(3), " PGH-001 ", new DeliveryNoticeLineData(first.Id, 4.123456m, " Lô 11/2026 "));

        Assert.Equal((order.CompanyId, "PGH-001", "Xe 51C-123.45", DeliveryNoticeStatus.Submitted),
            (notice.CompanyId, notice.DeliveryNoteNo, notice.Deliverer, notice.Status));
        var line = Assert.Single(notice.Lines);
        Assert.Equal((1, first.Id, 4.1235m, "Lô 11/2026"), (line.LineNo, line.PurchaseOrderLineId, line.Qty, line.Note));
    }

    [Fact]
    public void BaoGiao_ChanSaiQuyTac()
    {
        var supplier = Guid.CreateVersion7();
        var order = Order(supplier);
        var line = new DeliveryNoticeLineData(order.Lines[0].Id, 1, null);

        Assert.Contains("trước ngày đặt", Assert.Throws<BusinessRuleException>(() =>
            Notice(order, supplier, OrderDate.AddDays(-1), "P1", line)).Message);
        Assert.Contains("số phiếu giao", Assert.Throws<BusinessRuleException>(() =>
            Notice(order, supplier, OrderDate, "  ", line)).Message);
        Assert.Contains("ít nhất một dòng", Assert.Throws<BusinessRuleException>(() => Notice(order, supplier, OrderDate, "P1")).Message);
        Assert.Contains("lớn hơn 0", Assert.Throws<BusinessRuleException>(() =>
            Notice(order, supplier, OrderDate, "P1", new DeliveryNoticeLineData(order.Lines[0].Id, 0, null))).Message);
        Assert.Contains("nhiều lần", Assert.Throws<BusinessRuleException>(() => Notice(order, supplier, OrderDate, "P1", line, line)).Message);
        Assert.Contains("không thuộc đơn", Assert.Throws<BusinessRuleException>(() =>
            Notice(order, supplier, OrderDate, "P1", new DeliveryNoticeLineData(Guid.CreateVersion7(), 1, null))).Message);
        Assert.Throws<BusinessRuleException>(() => Notice(order, Guid.CreateVersion7(), OrderDate, "P1", line));

        var draft = Order(supplier, approve: false);
        Assert.Throws<BusinessRuleException>(() => Notice(draft, supplier, OrderDate, "P1", new DeliveryNoticeLineData(draft.Lines[0].Id, 1, null)));
    }

    [Fact]
    public void Huy_BatBuocLyDo_MotLan()
    {
        var supplier = Guid.CreateVersion7();
        var order = Order(supplier);
        var notice = Notice(order, supplier, OrderDate, "P1", new DeliveryNoticeLineData(order.Lines[0].Id, 1, null));

        Assert.Contains("lý do", Assert.Throws<BusinessRuleException>(() => notice.Cancel(" ", "kho@ncc.vn", Now)).Message);
        notice.Cancel("Xe hỏng, giao lại", "kho@ncc.vn", Now.AddHours(1));
        Assert.Equal((DeliveryNoticeStatus.Cancelled, "Xe hỏng, giao lại", "kho@ncc.vn"),
            (notice.Status, notice.CancelReason, notice.CancelledByEmail));
        Assert.Throws<BusinessRuleException>(() => notice.Cancel("Lần hai", "kho@ncc.vn", Now));
    }

    [Fact]
    public void Xnh_GanPhieuBaoGiao_CungPo_ChuaHuy()
    {
        var supplier = Guid.CreateVersion7();
        var order = Order(supplier);
        var notice = Notice(order, supplier, OrderDate, "P1", new DeliveryNoticeLineData(order.Lines[0].Id, 1, null));
        var receipt = GoodsReceiptConfirmation.Create(order.CompanyId, "XNH-1", order,
            new ReceiptConfirmationHeader(OrderDate.AddDays(1), "P1", null, null));

        receipt.LinkDeliveryNotice(notice);
        Assert.Equal(notice.Id, receipt.DeliveryNoticeId);

        var otherOrder = Order(supplier);
        var otherNotice = Notice(otherOrder, supplier, OrderDate, "P2", new DeliveryNoticeLineData(otherOrder.Lines[0].Id, 1, null));
        Assert.Contains("không thuộc đơn", Assert.Throws<BusinessRuleException>(() => receipt.LinkDeliveryNotice(otherNotice)).Message);

        var cancelled = Notice(order, supplier, OrderDate, "P3", new DeliveryNoticeLineData(order.Lines[0].Id, 1, null));
        cancelled.Cancel("Giao nhầm", "kho@ncc.vn", Now);
        Assert.Contains("đã hủy", Assert.Throws<BusinessRuleException>(() => receipt.LinkDeliveryNotice(cancelled)).Message);
    }
}
