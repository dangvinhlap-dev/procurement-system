using Proc.Domain.Common;
using Proc.Domain.MasterData;
using Proc.Domain.Purchasing;

namespace Proc.Domain.Tests.Purchasing;

/// <summary>Phiếu xác nhận nhận hàng XNH (lát K2, design.md mục 19.4).</summary>
public sealed class GoodsReceiptConfirmationTests
{
    private static readonly Guid Company = Guid.CreateVersion7();
    private static readonly Uom Cai = Uom.Create("CAI", "Cái");
    private static readonly Uom Thung = Uom.Create("THUNG", "Thùng");
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 3, 0, 0, TimeSpan.Zero);

    private static PurchaseOrder ApprovedOrder()
    {
        var item = Item.Create("VB01", ItemType.Inventory, TestCategories.Leaf("VT", "Vật tư"), Cai,
            new ItemDescription("Vòng bi", null, null, null, null, null, "10", null));
        item.SetConversions([(Thung, 12m)]);
        var order = PurchaseOrder.Create(Company, "PO-1",
            new PurchaseOrderHeader(new DateOnly(2026, 11, 2), Guid.CreateVersion7(), Guid.CreateVersion7(), null, null, null, null, 0, null));
        order.SetLines([
            (new PurchaseOrderLineData(null, item.Id, Cai.Id, 10, 50_000, 10, "10", null, null, null), item),
            (new PurchaseOrderLineData(null, item.Id, Thung.Id, 2, 600_000, 0, "8", null, null, null), item),
        ]);
        order.Submit();
        order.MarkApproved(Now);
        return order;
    }

    private static ReceiptConfirmationHeader Header() => new(new DateOnly(2026, 11, 10), " PG-01 ", null, null);

    private static ReceiptConfirmationLineData Line(PurchaseOrderLine poLine, decimal delivered, decimal accepted, decimal rejected = 0,
        ReceiptCheckResult result = ReceiptCheckResult.Passed, string? checkNote = null) =>
        new(poLine.Id, delivered, accepted, rejected, result, checkNote, null);

    [Fact]
    public void LapTheoPo_GiaThucMuaVaThueTheoDongPo_TinhTienTheoSlThucNhan()
    {
        var order = ApprovedOrder();
        var xnh = GoodsReceiptConfirmation.Create(Company, "XNH-1", order, Header());
        xnh.SetLines(order, [Line(order.Lines[0], 10, 8, 2, ReceiptCheckResult.Failed, "Móp 2 cái"), Line(order.Lines[1], 1, 1)]);

        Assert.Equal(order.SupplierId, xnh.SupplierId);
        Assert.Equal(order.WarehouseId, xnh.WarehouseId);
        Assert.Equal("PG-01", xnh.DeliveryNoteNo);
        // Dòng 1: giá sau chiết khấu 45.000 × 8 = 360.000, VAT 10% = 36.000; dòng 2: 1 thùng × 600.000, VAT 8% = 48.000, cơ sở 12 cái.
        Assert.Equal(45_000m, xnh.Lines[0].UnitPrice);
        Assert.Equal(360_000m, xnh.Lines[0].Amount);
        Assert.Equal(12m, xnh.Lines[1].ReceivedBaseQty);
        Assert.Equal(960_000m, xnh.TotalAmount);
        Assert.Equal(84_000m, xnh.TotalVat);
    }

    [Fact]
    public void QuyTacDong_SlKhongVuotPhieuGiao_KhongDatPhaiCoGhiChu_KhongTrungDongPo()
    {
        var order = ApprovedOrder();
        var xnh = GoodsReceiptConfirmation.Create(Company, "XNH-1", order, Header());
        Assert.Contains("vượt SL theo phiếu giao", Assert.Throws<BusinessRuleException>(() => xnh.SetLines(order, [Line(order.Lines[0], 5, 5, 1)])).Message);
        Assert.Contains("Không đạt", Assert.Throws<BusinessRuleException>(() => xnh.SetLines(order, [Line(order.Lines[0], 5, 4, 1)])).Message);
        Assert.Contains("ghi chú", Assert.Throws<BusinessRuleException>(
            () => xnh.SetLines(order, [Line(order.Lines[0], 5, 4, 1, ReceiptCheckResult.Failed)])).Message);
        Assert.Contains("nhiều lần", Assert.Throws<BusinessRuleException>(
            () => xnh.SetLines(order, [Line(order.Lines[0], 1, 1), Line(order.Lines[0], 1, 1)])).Message);
        Assert.Throws<BusinessRuleException>(() => xnh.SetLines(order, [Line(order.Lines[0], -1, 0)]));
    }

    [Fact]
    public void PoChuaDuyet_KhongLapDuoc()
    {
        var order = ApprovedOrder();
        order.CloseReceived();
        Assert.Contains("không nhận hàng", Assert.Throws<BusinessRuleException>(
            () => GoodsReceiptConfirmation.Create(Company, "XNH-1", order, Header())).Message);
        Assert.Throws<BusinessRuleException>(() => GoodsReceiptConfirmation.Create(Guid.CreateVersion7(), "XNH-1", ApprovedOrder(), Header()));
    }

    [Fact]
    public void XacNhan_BatBuocKiemTraTheoThamSo_SauDoKhongSuaKhongHuy()
    {
        var order = ApprovedOrder();
        var xnh = GoodsReceiptConfirmation.Create(Company, "XNH-1", order, Header());
        xnh.SetLines(order, [Line(order.Lines[0], 0, 0)]);
        Assert.Contains("chưa có dòng", Assert.Throws<BusinessRuleException>(() => xnh.Confirm(Guid.CreateVersion7(), Now, false)).Message);
        xnh.SetLines(order, [Line(order.Lines[0], 5, 5, result: ReceiptCheckResult.NotChecked)]);
        Assert.Contains("kết quả kiểm tra", Assert.Throws<BusinessRuleException>(() => xnh.Confirm(Guid.CreateVersion7(), Now, true)).Message);

        xnh.Confirm(Guid.CreateVersion7(), Now, false);
        Assert.Equal(ReceiptConfirmationStatus.Confirmed, xnh.Status);
        Assert.Throws<BusinessRuleException>(() => xnh.Update(Header()));
        Assert.Throws<BusinessRuleException>(() => xnh.SetLines(order, [Line(order.Lines[0], 1, 1)]));
        Assert.Contains("Điều chỉnh", Assert.Throws<BusinessRuleException>(() => xnh.Cancel("Nhầm")).Message);
        // Hóa đơn về sau hàng – vẫn cập nhật được.
        xnh.UpdateInvoice(new ReceiptConfirmationInvoice("0001234", "1C26TAA", new DateOnly(2026, 11, 11), 247_500.004m));
        Assert.Equal(247_500m, xnh.InvoiceAmount);
    }

    [Fact]
    public void DieuChinh_CoLyDo_KhongAm_GhiLichSu_TinhLaiTien()
    {
        var order = ApprovedOrder();
        var xnh = GoodsReceiptConfirmation.Create(Company, "XNH-1", order, Header());
        xnh.SetLines(order, [Line(order.Lines[0], 10, 10)]);
        var line = xnh.Lines[0];
        Assert.Contains("chưa xác nhận", Assert.Throws<BusinessRuleException>(() => xnh.Adjust(line.Id, -3, "Trả", Guid.CreateVersion7(), Now)).Message);
        xnh.Confirm(Guid.CreateVersion7(), Now, false);

        Assert.Contains("lý do", Assert.Throws<BusinessRuleException>(() => xnh.Adjust(line.Id, -3, " ", Guid.CreateVersion7(), Now)).Message);
        Assert.Throws<BusinessRuleException>(() => xnh.Adjust(line.Id, 0, "Trả", Guid.CreateVersion7(), Now));
        Assert.Contains("không được âm", Assert.Throws<BusinessRuleException>(() => xnh.Adjust(line.Id, -11, "Trả", Guid.CreateVersion7(), Now)).Message);

        xnh.Adjust(line.Id, -3, " Trả NCC 3 cái hỏng ", Guid.CreateVersion7(), Now);
        Assert.Equal(7m, line.ReceivedQty);
        Assert.Equal(-3m, line.AdjustedQty);
        Assert.Equal(315_000m, xnh.TotalAmount);
        Assert.Equal("Trả NCC 3 cái hỏng", Assert.Single(xnh.Adjustments).Reason);
    }

    [Fact]
    public void NghiemThu_SauXacNhan_NguoiNhanKhongTuNghiemThu_KhongDatPhaiCoYKien()
    {
        var order = ApprovedOrder();
        var xnh = GoodsReceiptConfirmation.Create(Company, "XNH-1", order, Header());
        xnh.SetLines(order, [Line(order.Lines[0], 10, 10)]);
        var receiver = Guid.CreateVersion7();
        var user = Guid.CreateVersion7();
        Assert.Throws<BusinessRuleException>(() => xnh.ConfirmAcceptance(true, null, user, Now));
        xnh.Confirm(receiver, Now, false);

        Assert.Contains("không tự nghiệm thu", Assert.Throws<BusinessRuleException>(() => xnh.ConfirmAcceptance(true, null, receiver, Now)).Message);
        Assert.Contains("ý kiến", Assert.Throws<BusinessRuleException>(() => xnh.ConfirmAcceptance(false, " ", user, Now)).Message);
        xnh.ConfirmAcceptance(false, "Thiếu 2 cái", user, Now);
        Assert.Equal(ReceiptConfirmationAcceptance.Disputed, xnh.Acceptance);
        xnh.ConfirmAcceptance(true, null, user, Now);
        Assert.Equal(ReceiptConfirmationAcceptance.Accepted, xnh.Acceptance);
        Assert.Null(xnh.AcceptanceNote);
    }

    [Fact]
    public void HuyPhieuNhap()
    {
        var order = ApprovedOrder();
        var xnh = GoodsReceiptConfirmation.Create(Company, "XNH-1", order, Header());
        xnh.Cancel(" Lập nhầm ");
        Assert.Equal(ReceiptConfirmationStatus.Cancelled, xnh.Status);
        Assert.Equal("Lập nhầm", xnh.CancelReason);
        Assert.Throws<BusinessRuleException>(() => xnh.UpdateInvoice(new ReceiptConfirmationInvoice(null, null, null, null)));
    }
}
