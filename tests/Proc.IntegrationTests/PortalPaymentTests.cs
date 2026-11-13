using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Proc.Application.Common;
using Proc.Application.MasterData;
using Proc.Application.Portal;
using Proc.Application.Purchasing;
using Proc.Domain.MasterData;
using Proc.Domain.Organization;
using Proc.Infrastructure.Persistence;

namespace Proc.IntegrationTests;

/// <summary>
/// Lát P6b (design.md mục 19.5, P6b-1…P6b-5): NCC xem trạng thái thanh toán trên Portal – hóa đơn trên XNH đã xác nhận, đề nghị đang xử lý
/// (không lộ số), đợt đã duyệt / đã chi kèm giảm trừ, còn phải trả; thư "đã thanh toán" khi kế toán ghi Đã chi; NCC khác không thấy.
/// </summary>
public partial class PurchasingTests
{
    [Fact]
    public async Task PortalThanhToan_HoaDon_DotDaDuyet_DaChi_ConPhaiTra_ThuBaoNcc_CoLapNcc()
    {
        var f = await ArrangeAsync();
        var (approver, _) = await ApproverAsync(f);
        var ncc = await PortalClientAsync(f.Buyer, f.SupplierId);

        // PO 10 cái × 95.000 (sau CK 5%), VAT 10% → 1.045.000 gồm VAT.
        var po = await ReadAsync<PurchaseOrderDto>(await CreatePoAsync(f, 10, null));
        await ReadAsync<PurchaseOrderDto>(await f.Buyer.PostAsync($"/api/purchase-orders/{po.Id}/submit", null, Ct));
        po = await ReadAsync<PurchaseOrderDto>(await approver.PostAsJsonAsync($"/api/purchase-orders/{po.Id}/approve", new ApprovalActionRequest(null), Ct));
        var url = $"/api/portal/payments/{po.Id}";

        // Chưa nhận hàng: đơn chưa vào danh sách thanh toán (vẫn mở được chi tiết, chưa có gì).
        Assert.DoesNotContain(await ReadAsync<List<PortalPaymentOrderDto>>(await ncc.GetAsync("/api/portal/payments", Ct)), o => o.OrderId == po.Id);
        Assert.Empty((await ReadAsync<PortalPaymentDetailDto>(await ncc.GetAsync(url, Ct))).Invoices);

        var xnh1 = await ConfirmXnhAsync(f, await ReadAsync<ReceiptConfirmationDto>(await ReceiveXnhAsync(f, po, 4)));
        var xnh2 = await ConfirmXnhAsync(f, await ReadAsync<ReceiptConfirmationDto>(await ReceiveXnhAsync(f, po, 2)));
        var xnh3 = await ConfirmXnhAsync(f, await ReadAsync<ReceiptConfirmationDto>(await ReceiveXnhAsync(f, po, 4)));
        await ReadAsync<ReceiptConfirmationDto>(await f.Buyer.PutAsJsonAsync($"/api/receipt-confirmations/{xnh1.Id}/invoice",
            new UpdateReceiptConfirmationInvoiceRequest("201", "1C26TAA", Today, 418_000), Ct));
        await ReadAsync<ReceiptConfirmationDto>(await f.Buyer.PutAsJsonAsync($"/api/receipt-confirmations/{xnh2.Id}/invoice",
            new UpdateReceiptConfirmationInvoiceRequest("202", "1C26TAA", Today, 209_000), Ct));

        // Có hóa đơn 627.000, XNH 3 chờ hóa đơn → chưa thanh toán.
        var summary = Assert.Single(await ReadAsync<List<PortalPaymentOrderDto>>(await ncc.GetAsync("/api/portal/payments", Ct)), o => o.OrderId == po.Id);
        Assert.Equal(("Unpaid", 3, 1, 627_000m, 627_000m, 1_045_000m), (summary.Status, summary.ReceiptCount, summary.AwaitingInvoiceCount,
            summary.InvoiceAmount, summary.OutstandingAmount, summary.ReceivedAmount));
        var detail = await ReadAsync<PortalPaymentDetailDto>(await ncc.GetAsync(url, Ct));
        Assert.Equal("Invoiced", detail.Invoices.Single(i => i.ReceiptNo == xnh1.No).Status);
        Assert.Equal("AwaitingInvoice", detail.Invoices.Single(i => i.ReceiptNo == xnh3.No).Status);

        // Đề nghị (xnh1 + xnh2, giảm trừ 27.000) chờ duyệt: hóa đơn "đang xử lý", không lộ số đề nghị, chưa có đợt thanh toán.
        var dnt = await ReadAsync<PaymentRequestDto>(await f.Buyer.PostAsJsonAsync("/api/payment-requests",
            new SavePaymentRequestRequest(Today, po.Id, "Cash", null, 27_000, "Trừ tạm ứng", null,
                [new SavePaymentRequestLineRequest(xnh1.Id, null), new SavePaymentRequestLineRequest(xnh2.Id, null)]), Ct));
        await ReadAsync<PaymentRequestDto>(await f.Buyer.PostAsync($"/api/payment-requests/{dnt.Id}/submit", null, Ct));
        detail = await ReadAsync<PortalPaymentDetailDto>(await ncc.GetAsync(url, Ct));
        var first = detail.Invoices.Single(i => i.ReceiptNo == xnh1.No);
        Assert.Equal(("Processing", (string?)null), (first.Status, first.PaymentNo));
        Assert.Empty(detail.Rounds);
        Assert.Equal(627_000m, detail.Summary.ProcessingAmount);

        // Duyệt → đợt thanh toán "đã duyệt – chờ chi" có giảm trừ; ghi Đã chi → thư báo NCC, còn phải trả 0.
        var payRole = UniqueCode("T");
        await _factory.CreateRoleAsync(payRole, Permissions.Purchase.PaymentView, Permissions.Purchase.PaymentApprove, Permissions.Purchase.PaymentPay);
        var payName = $"tt.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(payName, f.CompanyCode, payRole);
        var payUser = await _factory.CreateClientInCompanyAsync(payName, ProcApiFactory.TestPassword, f.CompanyCode);
        await ReadAsync<PaymentRequestDto>(await payUser.PostAsJsonAsync($"/api/payment-requests/{dnt.Id}/approve", new ApprovalActionRequest(null), Ct));
        detail = await ReadAsync<PortalPaymentDetailDto>(await ncc.GetAsync(url, Ct));
        var round = Assert.Single(detail.Rounds);
        Assert.Equal((dnt.No, "Approved", 627_000m, 27_000m, "Trừ tạm ứng", 600_000m, "201, 202"),
            (round.No, round.Status, round.TotalAmount, round.DeductionAmount, round.DeductionReason, round.PayableAmount, round.InvoiceNos));
        Assert.Equal(("Approved", dnt.No), (detail.Invoices.Single(i => i.ReceiptNo == xnh2.No).Status, detail.Invoices.Single(i => i.ReceiptNo == xnh2.No).PaymentNo));
        Assert.Equal(600_000m, detail.Summary.ApprovedUnpaidAmount);

        await ReadAsync<PaymentRequestDto>(await payUser.PostAsJsonAsync($"/api/payment-requests/{dnt.Id}/pay", new MarkPaymentPaidRequest(Today, "UNC 1"), Ct));
        detail = await ReadAsync<PortalPaymentDetailDto>(await ncc.GetAsync(url, Ct));
        Assert.Equal(("Paid", (DateOnly?)Today), (detail.Rounds[0].Status, detail.Rounds[0].PaidDate));
        Assert.Equal(("Paid", 600_000m, 27_000m, 0m, (DateOnly?)Today), (detail.Summary.Status, detail.Summary.PaidAmount, detail.Summary.DeductionAmount,
            detail.Summary.OutstandingAmount, detail.Summary.LastPaidDate));
        Assert.Equal("Paid", detail.Invoices.Single(i => i.ReceiptNo == xnh1.No).Status);

        using (var scope = _factory.Services.CreateScope())
        {
            var mail = Assert.Single(await scope.ServiceProvider.GetRequiredService<ProcDbContext>().SupplierPortalEmails.IgnoreQueryFilters()
                .Where(m => m.DocId == po.Id && m.Kind == SupplierPortalEmailKind.PaymentPaid).ToListAsync(Ct));
            Assert.Contains("600.000", mail.TextBody, StringComparison.Ordinal);
            Assert.Contains("27.000", mail.TextBody, StringComparison.Ordinal);
            Assert.DoesNotContain("UNC 1", mail.TextBody, StringComparison.Ordinal);
        }

        // XNH 3 có hóa đơn 418.000 chưa đề nghị → thanh toán một phần, còn 418.000.
        await ReadAsync<ReceiptConfirmationDto>(await f.Buyer.PutAsJsonAsync($"/api/receipt-confirmations/{xnh3.Id}/invoice",
            new UpdateReceiptConfirmationInvoiceRequest("203", "1C26TAA", Today, 418_000), Ct));
        summary = Assert.Single(await ReadAsync<List<PortalPaymentOrderDto>>(await ncc.GetAsync("/api/portal/payments", Ct)), o => o.OrderId == po.Id);
        Assert.Equal(("PartlyPaid", 418_000m), (summary.Status, summary.OutstandingAmount));

        // NCC khác (có tài khoản Portal) không thấy thanh toán của NCC này.
        var headName = $"hd.{Guid.NewGuid():N}"[..16];
        await _factory.CreateUserAsync(headName, ProcApiFactory.TestPassword, ["TCT"]);
        var head = await _factory.CreateClientInCompanyAsync(headName, ProcApiFactory.TestPassword, "TCT");
        var groupId = (await head.GetFromJsonAsync<List<SupplierGroupDto>>("/api/supplier-groups", Ct))!.Single(g => g.Code == "TN").Id;
        var other = await ReadAsync<SupplierDetailDto>(await head.PostAsJsonAsync("/api/suppliers",
            new SaveSupplierRequest(null, groupId, "NCC khác – thanh toán Portal", null, false, null, null, null, null, null, null, null, null,
                "Approved", null, null, [], []), Ct));
        await _factory.FinishSupplierApprovalAsync(other.Id);
        var otherNcc = await PortalClientAsync(head, other.Id);
        Assert.Equal(HttpStatusCode.NotFound, (await otherNcc.GetAsync(url, Ct)).StatusCode);
        Assert.DoesNotContain(await ReadAsync<List<PortalPaymentOrderDto>>(await otherNcc.GetAsync("/api/portal/payments", Ct)), o => o.OrderId == po.Id);
        // Token nội bộ không gọi được API Portal.
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Buyer.GetAsync(url, Ct)).StatusCode);
    }
}
