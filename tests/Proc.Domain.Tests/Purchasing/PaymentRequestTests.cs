using Proc.Domain.Common;
using Proc.Domain.MasterData;
using Proc.Domain.Purchasing;

namespace Proc.Domain.Tests.Purchasing;

/// <summary>Đề nghị thanh toán 5A/5B (lát Y8 – design.md mục 19.3 bước 7): theo một PO, dòng là XNH có hóa đơn, giảm trừ, chuyển khoản, Đã chi.</summary>
public sealed class PaymentRequestTests
{
    private static readonly Uom Cai = Uom.Create("CAI", "Cái");
    private static readonly DateTimeOffset Now = new(2026, 11, 13, 3, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 11, 13);
    private static readonly PaymentBankAccount Bank = new(Guid.CreateVersion7(), "Vietcombank", "Đồng Nai", "0071000123456", "CONG TY VONG BI");

    private static PurchaseOrder Order(bool approve = true)
    {
        var item = Item.Create("VB01", ItemType.Inventory, ItemCategory.Create("VT", "Vật tư"), Cai,
            new ItemDescription("Vòng bi", null, null, null, null, null, "10", null));
        var order = PurchaseOrder.Create(Guid.CreateVersion7(), "PO-1",
            new PurchaseOrderHeader(new DateOnly(2026, 11, 2), Guid.CreateVersion7(), Guid.CreateVersion7(), null, null, null, null, 0, null));
        order.SetLines([(new PurchaseOrderLineData(null, item.Id, Cai.Id, 20, 100_000, 0, "10", null, null, null), item)]);
        order.Submit();
        if (approve) order.MarkApproved(Now);
        return order;
    }

    private static PaymentRequestLineSource Source(
        PurchaseOrder order, string no = "XNH-1", string? invoiceNo = "123", decimal? amount = 1_100_000m,
        ReceiptConfirmationStatus status = ReceiptConfirmationStatus.Confirmed) =>
        new(Guid.CreateVersion7(), no, order.Id, status, invoiceNo, "1C26TAA", Today, amount);

    private static PaymentRequest Create(PurchaseOrder order, PaymentMethod method = PaymentMethod.BankTransfer, decimal deduction = 0, string? reason = null) =>
        PaymentRequest.Create(order.CompanyId, "DNT-1", order, new PaymentRequestHeader(Today, method, deduction, reason, " Đợt 1 "),
            method == PaymentMethod.BankTransfer ? Bank : null);

    [Fact]
    public void Lap_TheoPo_DongTheoHoaDonXnh_GiamTru_ChupTaiKhoan()
    {
        var order = Order();
        var request = Create(order, deduction: 150_000, reason: " Đã tạm ứng ");
        var first = Source(order);
        var second = Source(order, "XNH-2", "124", 550_000m);
        request.SetLines([(new PaymentRequestLineData(first.ReceiptConfirmationId, null), first), (new PaymentRequestLineData(second.ReceiptConfirmationId, " Đợt 2 "), second)]);

        Assert.Equal((order.Id, order.SupplierId, PaymentRequestForm.Goods, "Đợt 1"), (request.PurchaseOrderId, request.SupplierId, request.Form, request.Note));
        Assert.Equal(("Vietcombank", "0071000123456", Bank.Id), (request.BankName, request.BankAccountNumber, request.BankAccountId));
        // 1.100.000 + 550.000 − giảm trừ 150.000 = 1.500.000.
        Assert.Equal((1_650_000m, 150_000m, 1_500_000m, "Đã tạm ứng"), (request.TotalAmount, request.DeductionAmount, request.PayableAmount, request.DeductionReason));
        Assert.Equal("Thanh toán hóa đơn 1C26TAA số 123 theo phiếu nhận hàng XNH-1", request.Lines[0].Description);
        Assert.Equal(("Đợt 2", 550_000m, 2), (request.Lines[1].Description, request.Lines[1].Amount, request.Lines[1].LineNo));
        Assert.Equal(request.PayableAmount, request.ApprovalAmount);

        // Đổi sang tiền mặt: bỏ tài khoản ngân hàng.
        request.Update(new PaymentRequestHeader(Today, PaymentMethod.Cash, 0, "bỏ", null), Bank);
        Assert.Equal((null, null, (string?)null, 1_650_000m), (request.BankAccountId, request.BankName, request.DeductionReason, request.PayableAmount));
    }

    [Fact]
    public void Lap_ChanSaiQuyTac()
    {
        var order = Order();
        Assert.Contains("chưa duyệt", Assert.Throws<BusinessRuleException>(() => Create(Order(approve: false))).Message);
        Assert.Contains("tài khoản ngân hàng", Assert.Throws<BusinessRuleException>(() =>
            PaymentRequest.Create(order.CompanyId, "DNT-1", order, new PaymentRequestHeader(Today, PaymentMethod.BankTransfer, 0, null, null), null)).Message);
        Assert.Contains("ghi lý do", Assert.Throws<BusinessRuleException>(() => Create(order, deduction: 1)).Message);
        Assert.Contains("không được âm", Assert.Throws<BusinessRuleException>(() => Create(order, deduction: -1, reason: "x")).Message);

        var request = Create(order);
        PaymentRequestLineData Data(PaymentRequestLineSource s) => new(s.ReceiptConfirmationId, null);
        var noInvoice = Source(order, invoiceNo: null);
        Assert.Contains("chưa có hóa đơn", Assert.Throws<BusinessRuleException>(() => request.SetLines([(Data(noInvoice), noInvoice)])).Message);
        var zero = Source(order, amount: 0);
        Assert.Contains("chưa có hóa đơn", Assert.Throws<BusinessRuleException>(() => request.SetLines([(Data(zero), zero)])).Message);
        var draft = Source(order, status: ReceiptConfirmationStatus.Draft);
        Assert.Contains("chưa xác nhận", Assert.Throws<BusinessRuleException>(() => request.SetLines([(Data(draft), draft)])).Message);
        var other = Source(Order());
        Assert.Contains("không thuộc đơn", Assert.Throws<BusinessRuleException>(() => request.SetLines([(Data(other), other)])).Message);
        var ok = Source(order);
        Assert.Contains("một lần", Assert.Throws<BusinessRuleException>(() => request.SetLines([(Data(ok), ok), (Data(ok), ok)])).Message);

        // Giảm trừ vượt tổng: lưu được, gửi duyệt bị chặn.
        request.Update(new PaymentRequestHeader(Today, PaymentMethod.Cash, 2_000_000, "Trừ công nợ", null), null);
        request.SetLines([(Data(ok), ok)]);
        Assert.Contains("lớn hơn 0", Assert.Throws<BusinessRuleException>(request.EnsureSubmittable).Message);
    }

    [Fact]
    public void DaChi_ChiSauKhiDuyet_NgayHopLe_MotLan()
    {
        var order = Order();
        var request = Create(order);
        var source = Source(order);
        request.SetLines([(new PaymentRequestLineData(source.ReceiptConfirmationId, null), source)]);
        var user = Guid.CreateVersion7();
        Assert.Contains("chưa duyệt xong", Assert.Throws<BusinessRuleException>(() => request.MarkPaid(Today, null, user, Today, Today, Now)).Message);

        request.Submit();
        request.MarkApproved(Now);
        Assert.Contains("sau hôm nay", Assert.Throws<BusinessRuleException>(() => request.MarkPaid(Today.AddDays(1), null, user, Today, Today, Now)).Message);
        Assert.Contains("trước ngày duyệt", Assert.Throws<BusinessRuleException>(() => request.MarkPaid(Today.AddDays(-1), null, user, Today, Today, Now)).Message);
        request.MarkPaid(Today, " UNC 0456 ", user, Today, Today, Now);
        Assert.Equal((true, Today, "UNC 0456", user), (request.IsPaid, request.PaidDate, request.PaymentNote, request.PaidBy));
        Assert.Contains("đã ghi Đã chi", Assert.Throws<BusinessRuleException>(() => request.MarkPaid(Today, null, user, Today, Today, Now)).Message);
    }
}
