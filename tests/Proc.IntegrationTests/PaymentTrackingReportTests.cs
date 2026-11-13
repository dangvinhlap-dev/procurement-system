using System.Net.Http.Json;
using Proc.Application.Common;
using Proc.Application.Purchasing;
using Proc.Application.Reports;
using Proc.Domain.Organization;

namespace Proc.IntegrationTests;

/// <summary>
/// Lát Y9a (design.md mục 19.3 "Lát Y9"): báo cáo theo dõi thanh toán theo PO – tiền hóa đơn trên XNH, hóa đơn chưa đề nghị, đề nghị đang
/// lập/chờ duyệt, đã duyệt chờ chi, đã chi, giảm trừ, còn phải trả; tổng hợp theo NCC.
/// </summary>
public partial class PurchasingTests
{
    [Fact]
    public async Task TheoDoiThanhToan_TheoPo_HoaDon_DeNghi_DaChi_ConPhaiTra()
    {
        var f = await ArrangeAsync();
        var (approver, _) = await ApproverAsync(f);
        // PO 10 cái × 95.000 (sau CK 5%), VAT 10% → 1.045.000 gồm VAT.
        var po = await ReadAsync<PurchaseOrderDto>(await CreatePoAsync(f, 10, null));
        await ReadAsync<PurchaseOrderDto>(await f.Buyer.PostAsync($"/api/purchase-orders/{po.Id}/submit", null, Ct));
        po = await ReadAsync<PurchaseOrderDto>(await approver.PostAsJsonAsync($"/api/purchase-orders/{po.Id}/approve", new ApprovalActionRequest(null), Ct));

        var xnh1 = await ConfirmXnhAsync(f, await ReadAsync<ReceiptConfirmationDto>(await ReceiveXnhAsync(f, po, 4)));
        var xnh2 = await ConfirmXnhAsync(f, await ReadAsync<ReceiptConfirmationDto>(await ReceiveXnhAsync(f, po, 2)));
        var xnh3 = await ConfirmXnhAsync(f, await ReadAsync<ReceiptConfirmationDto>(await ReceiveXnhAsync(f, po, 4)));
        await ReadAsync<ReceiptConfirmationDto>(await f.Buyer.PutAsJsonAsync($"/api/receipt-confirmations/{xnh1.Id}/invoice",
            new UpdateReceiptConfirmationInvoiceRequest("201", "1C26TAA", Today, 418_000), Ct));
        await ReadAsync<ReceiptConfirmationDto>(await f.Buyer.PutAsJsonAsync($"/api/receipt-confirmations/{xnh2.Id}/invoice",
            new UpdateReceiptConfirmationInvoiceRequest("202", "1C26TAA", Today, 209_000), Ct));

        async Task<PaymentTrackingResult> ReportAsync() => await ReadAsync<PaymentTrackingResult>(await f.Buyer.GetAsync(
            $"/api/reports/payment-tracking?from={Today.AddDays(-1):yyyy-MM-dd}&to={Today:yyyy-MM-dd}", Ct));

        // Chưa đề nghị: hóa đơn 627.000 chưa đề nghị, XNH 3 chưa có hóa đơn, còn phải trả = toàn bộ hóa đơn.
        var row = Assert.Single((await ReportAsync()).Rows, r => r.OrderId == po.Id);
        Assert.Equal((1_045_000m, 627_000m, 3, 1, 627_000m, 0m, 627_000m),
            (row.OrderAmount, row.InvoiceAmount, row.ReceiptCount, row.NoInvoiceReceipts, row.UnrequestedAmount, row.PaidAmount, row.OutstandingAmount));
        Assert.Equal(1_045_000m, row.ReceivedAmount);

        SavePaymentRequestRequest Save(decimal deduction, string? reason, params Guid[] receipts) =>
            new(Today, po.Id, "Cash", null, deduction, reason, null, receipts.Select(r => new SavePaymentRequestLineRequest(r, null)).ToList());

        // Đề nghị 1 (xnh1 + xnh2, giảm trừ 27.000 → 600.000): gửi duyệt → đang chờ duyệt.
        var dnt = await ReadAsync<PaymentRequestDto>(await f.Buyer.PostAsJsonAsync("/api/payment-requests", Save(27_000, "Trừ tạm ứng", xnh1.Id, xnh2.Id), Ct));
        dnt = await ReadAsync<PaymentRequestDto>(await f.Buyer.PostAsync($"/api/payment-requests/{dnt.Id}/submit", null, Ct));
        row = Assert.Single((await ReportAsync()).Rows, r => r.OrderId == po.Id);
        Assert.Equal((0m, 600_000m, 0m, 627_000m), (row.UnrequestedAmount, row.InProgressAmount, row.ApprovedUnpaidAmount, row.OutstandingAmount));

        var payRole = UniqueCode("T");
        await _factory.CreateRoleAsync(payRole, Permissions.Purchase.PaymentView, Permissions.Purchase.PaymentApprove, Permissions.Purchase.PaymentPay);
        var payName = $"tt.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(payName, f.CompanyCode, payRole);
        var payUser = await _factory.CreateClientInCompanyAsync(payName, ProcApiFactory.TestPassword, f.CompanyCode);
        await ReadAsync<PaymentRequestDto>(await payUser.PostAsJsonAsync($"/api/payment-requests/{dnt.Id}/approve", new ApprovalActionRequest(null), Ct));
        row = Assert.Single((await ReportAsync()).Rows, r => r.OrderId == po.Id);
        Assert.Equal((0m, 600_000m, 0m), (row.InProgressAmount, row.ApprovedUnpaidAmount, row.PaidAmount));

        // Đã chi 600.000 + giảm trừ 27.000 → còn phải trả 0; XNH 3 có hóa đơn 418.000, lập đề nghị nháp → đang lập.
        await ReadAsync<PaymentRequestDto>(await payUser.PostAsJsonAsync($"/api/payment-requests/{dnt.Id}/pay", new MarkPaymentPaidRequest(Today, "UNC 1"), Ct));
        await ReadAsync<ReceiptConfirmationDto>(await f.Buyer.PutAsJsonAsync($"/api/receipt-confirmations/{xnh3.Id}/invoice",
            new UpdateReceiptConfirmationInvoiceRequest("203", "1C26TAA", Today, 418_000), Ct));
        await ReadAsync<PaymentRequestDto>(await f.Buyer.PostAsJsonAsync("/api/payment-requests", Save(0, null, xnh3.Id), Ct));

        var report = await ReportAsync();
        row = Assert.Single(report.Rows, r => r.OrderId == po.Id);
        Assert.Equal((1_045_000m, 0, 0m, 418_000m, 0m, 600_000m, 27_000m, 418_000m, (DateOnly?)Today),
            (row.InvoiceAmount, row.NoInvoiceReceipts, row.UnrequestedAmount, row.InProgressAmount, row.ApprovedUnpaidAmount, row.PaidAmount,
                row.PaidDeductionAmount, row.OutstandingAmount, row.LastPaidDate));
        var supplier = Assert.Single(report.Suppliers);
        Assert.Equal((1, 1_045_000m, 600_000m, 418_000m), (supplier.Orders, supplier.InvoiceAmount, supplier.PaidAmount, supplier.OutstandingAmount));
        Assert.Equal(report.Rows.Sum(r => r.OutstandingAmount), report.Total.OutstandingAmount);
    }
}
