using Proc.Domain.Common;
using Proc.Domain.MasterData;
using Proc.Domain.Purchasing;

namespace Proc.Domain.Tests.Purchasing;

/// <summary>NCC xác nhận / từ chối PO trên Portal, Phòng CUVT mở lại phản hồi (lát P4a – design.md mục 19.5, P4-2).</summary>
public class PurchaseOrderSupplierResponseTests
{
    private static readonly Uom Cai = Uom.Create("CAI", "Cái");
    private static readonly DateTimeOffset Now = new(2026, 11, 12, 3, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly OrderDate = new(2026, 11, 2);

    private static PurchaseOrder Order(Guid supplierId, bool approve = true)
    {
        var item = Item.Create("VB01", ItemType.Inventory, TestCategories.Leaf("VT", "Vật tư"), Cai,
            new ItemDescription("Vòng bi", null, null, null, null, null, "10", null));
        var order = PurchaseOrder.Create(Guid.CreateVersion7(), "PO-1",
            new PurchaseOrderHeader(OrderDate, supplierId, Guid.CreateVersion7(), null, null, null, null, 0, null));
        order.SetLines([(new PurchaseOrderLineData(null, item.Id, Cai.Id, 10, 50_000, 0, "10", null, null, null), item)]);
        order.Submit();
        if (approve) order.MarkApproved(Now);
        return order;
    }

    private static PurchaseOrderSupplierResponse Respond(
        PurchaseOrder order, Guid supplierId, SupplierOrderDecision decision, DateOnly? promised, string? note) =>
        PurchaseOrderSupplierResponse.Create(order, supplierId, Guid.CreateVersion7(), "ban.hang@ncc.vn", decision, promised, note, Now);

    [Fact]
    public void XacNhan_CanNgayGiaoKhongTruocNgayDat()
    {
        var supplier = Guid.CreateVersion7();
        var order = Order(supplier);
        Assert.Contains("ngày giao", Assert.Throws<BusinessRuleException>(() =>
            Respond(order, supplier, SupplierOrderDecision.Confirmed, null, null)).Message);
        Assert.Throws<BusinessRuleException>(() => Respond(order, supplier, SupplierOrderDecision.Confirmed, OrderDate.AddDays(-1), null));

        var response = Respond(order, supplier, SupplierOrderDecision.Confirmed, OrderDate.AddDays(10), "  Giao một lần ");
        Assert.Equal((order.CompanyId, OrderDate.AddDays(10), "Giao một lần", true),
            (response.CompanyId, response.PromisedDate, response.Note, response.IsCurrent));
    }

    [Fact]
    public void TuChoi_BatBuocLyDo_BoNgayGiao()
    {
        var supplier = Guid.CreateVersion7();
        var order = Order(supplier);
        Assert.Contains("lý do", Assert.Throws<BusinessRuleException>(() =>
            Respond(order, supplier, SupplierOrderDecision.Rejected, null, " ")).Message);
        var response = Respond(order, supplier, SupplierOrderDecision.Rejected, OrderDate.AddDays(5), "Hết hàng");
        Assert.Null(response.PromisedDate);
    }

    [Fact]
    public void ChiPoDaDuyetCuaDungNcc()
    {
        var supplier = Guid.CreateVersion7();
        Assert.Throws<BusinessRuleException>(() => Respond(Order(supplier, approve: false), supplier, SupplierOrderDecision.Rejected, null, "x"));
        Assert.Throws<BusinessRuleException>(() => Respond(Order(supplier), Guid.CreateVersion7(), SupplierOrderDecision.Rejected, null, "x"));
        var cancelled = Order(supplier);
        cancelled.Cancel("Đổi NCC");
        Assert.Throws<BusinessRuleException>(() => Respond(cancelled, supplier, SupplierOrderDecision.Rejected, null, "x"));
    }

    [Fact]
    public void MoLai_BatBuocLyDo_MotLan_PoConDaDuyet()
    {
        var supplier = Guid.CreateVersion7();
        var order = Order(supplier);
        var response = Respond(order, supplier, SupplierOrderDecision.Rejected, null, "Hết hàng");
        var user = Guid.CreateVersion7();

        Assert.Throws<BusinessRuleException>(() => response.Reopen(order, user, "Nguyễn Văn A", "  ", Now));
        response.Reopen(order, user, "Nguyễn Văn A", " Chấp nhận giao chậm ", Now.AddHours(1));
        Assert.Equal((false, "Chấp nhận giao chậm", user), (response.IsCurrent, response.ReopenReason, response.ReopenedBy));
        Assert.Contains("đã được mở lại", Assert.Throws<BusinessRuleException>(() =>
            response.Reopen(order, user, "Nguyễn Văn A", "Lần hai", Now)).Message);

        var other = Respond(order, supplier, SupplierOrderDecision.Confirmed, OrderDate.AddDays(20), null);
        order.Cancel("Đổi NCC");
        Assert.Throws<BusinessRuleException>(() => other.Reopen(order, user, "Nguyễn Văn A", "Lý do", Now));
    }
}
