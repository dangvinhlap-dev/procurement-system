using System.Net;
using System.Net.Http.Json;
using Proc.Application.Common;
using Proc.Application.Purchasing;
using Proc.Domain.Organization;

namespace Proc.IntegrationTests;

/// <summary>
/// Lát Y8 (design.md mục 19.3 bước 7): đề nghị thanh toán 5A/5B cho một PO – nhiều dòng hóa đơn, mỗi dòng một XNH có hóa đơn; XNH chỉ ở một đề
/// nghị chưa hủy và khóa sửa hóa đơn; giảm trừ; duyệt theo bộ cấp DNT; kế toán ghi Đã chi; đã chi thì không hủy.
/// </summary>
public partial class PurchasingTests
{
    [Fact]
    public async Task DeNghiThanhToan_NhieuHoaDonXnh_KhoaHoaDon_Duyet_DaChi()
    {
        var f = await ArrangeAsync();
        var (approver, _) = await ApproverAsync(f);
        // PO 10 cái × 95.000 (sau CK 5%), VAT 10%.
        var po = await ReadAsync<PurchaseOrderDto>(await CreatePoAsync(f, 10, null));
        await ReadAsync<PurchaseOrderDto>(await f.Buyer.PostAsync($"/api/purchase-orders/{po.Id}/submit", null, Ct));
        po = await ReadAsync<PurchaseOrderDto>(await approver.PostAsJsonAsync($"/api/purchase-orders/{po.Id}/approve", new ApprovalActionRequest(null), Ct));

        // XNH 1: 4 cái → 418.000 gồm VAT; XNH 2: 2 cái → 209.000; XNH 3: 4 cái chưa có hóa đơn.
        var xnh1 = await ConfirmXnhAsync(f, await ReadAsync<ReceiptConfirmationDto>(await ReceiveXnhAsync(f, po, 4)));
        var xnh2 = await ConfirmXnhAsync(f, await ReadAsync<ReceiptConfirmationDto>(await ReceiveXnhAsync(f, po, 2)));
        var xnh3 = await ConfirmXnhAsync(f, await ReadAsync<ReceiptConfirmationDto>(await ReceiveXnhAsync(f, po, 4)));
        await ReadAsync<ReceiptConfirmationDto>(await f.Buyer.PutAsJsonAsync($"/api/receipt-confirmations/{xnh1.Id}/invoice",
            new UpdateReceiptConfirmationInvoiceRequest("101", "1C26TAA", Today, 418_000), Ct));
        await ReadAsync<ReceiptConfirmationDto>(await f.Buyer.PutAsJsonAsync($"/api/receipt-confirmations/{xnh2.Id}/invoice",
            new UpdateReceiptConfirmationInvoiceRequest("102", "1C26TAA", Today, 209_000), Ct));

        var order = Assert.Single(await ReadAsync<List<PaymentOrderOptionDto>>(await f.Buyer.GetAsync("/api/payment-requests/orders", Ct)), o => o.Id == po.Id);
        Assert.Equal((2, 627_000m), (order.OpenReceiptCount, order.OpenAmount));
        var source = await ReadAsync<PaymentRequestSourceDto>(await f.Buyer.GetAsync($"/api/payment-requests/source?purchaseOrderId={po.Id}", Ct));
        Assert.Equal(("5B", 3), (source.Form, source.Receipts.Count));
        Assert.Null(source.Receipts.Single(r => r.Id == xnh3.Id).InvoiceNo);

        SavePaymentRequestRequest Save(string method, decimal deduction, string? reason, params Guid[] receipts) =>
            new(Today, po.Id, method, null, deduction, reason, "Thanh toán đợt 1", receipts.Select(r => new SavePaymentRequestLineRequest(r, null)).ToList());

        Assert.Contains("chưa có hóa đơn", await TitleAsync(await f.Buyer.PostAsJsonAsync("/api/payment-requests", Save("Cash", 0, null, xnh3.Id), Ct)));
        Assert.Contains("tài khoản ngân hàng", await TitleAsync(await f.Buyer.PostAsJsonAsync("/api/payment-requests",
            Save("BankTransfer", 0, null, xnh1.Id), Ct)));
        Assert.Contains("ghi lý do", await TitleAsync(await f.Buyer.PostAsJsonAsync("/api/payment-requests", Save("Cash", 27_000, null, xnh1.Id), Ct)));

        // Hai dòng hóa đơn: 418.000 + 209.000 − giảm trừ 27.000 = 600.000.
        var dnt = await ReadAsync<PaymentRequestDto>(await f.Buyer.PostAsJsonAsync("/api/payment-requests",
            Save("Cash", 27_000, "Trừ tạm ứng", xnh1.Id, xnh2.Id), Ct));
        Assert.StartsWith("DNT", dnt.No, StringComparison.Ordinal);
        Assert.Equal(("5B", "Draft", 627_000m, 600_000m, 2), (dnt.Form, dnt.Status, dnt.TotalAmount, dnt.PayableAmount, dnt.Lines.Count));
        Assert.Equal(("101", "Thanh toán hóa đơn 1C26TAA số 101 theo phiếu nhận hàng " + xnh1.No), (dnt.Lines[0].InvoiceNo, dnt.Lines[0].Description));

        // XNH đã ở đề nghị: không chọn lại, không sửa hóa đơn trên XNH.
        Assert.Contains("đã ở đề nghị thanh toán", await TitleAsync(await f.Buyer.PostAsJsonAsync("/api/payment-requests", Save("Cash", 0, null, xnh1.Id), Ct)));
        Assert.Contains(dnt.No, await TitleAsync(await f.Buyer.PutAsJsonAsync($"/api/receipt-confirmations/{xnh1.Id}/invoice",
            new UpdateReceiptConfirmationInvoiceRequest("101", "1C26TAA", Today, 400_000), Ct)));
        Assert.Equal(0, (await ReadAsync<List<PaymentOrderOptionDto>>(await f.Buyer.GetAsync("/api/payment-requests/orders", Ct))).Count(o => o.Id == po.Id));

        // Gửi duyệt (công ty chưa cấu hình bộ cấp DNT → một cấp theo quyền duyệt), người duyệt có quyền DNT duyệt.
        dnt = await ReadAsync<PaymentRequestDto>(await f.Buyer.PostAsync($"/api/payment-requests/{dnt.Id}/submit", null, Ct));
        Assert.Equal("Submitted", dnt.Status);
        var payRole = UniqueCode("T");
        await _factory.CreateRoleAsync(payRole, Permissions.Purchase.PaymentView, Permissions.Purchase.PaymentApprove);
        var payApproverName = $"tt.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(payApproverName, f.CompanyCode, payRole);
        var payApprover = await _factory.CreateClientInCompanyAsync(payApproverName, ProcApiFactory.TestPassword, f.CompanyCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await approver.PostAsJsonAsync($"/api/payment-requests/{dnt.Id}/approve", new ApprovalActionRequest(null), Ct)).StatusCode);
        dnt = await ReadAsync<PaymentRequestDto>(await payApprover.PostAsJsonAsync(
            $"/api/payment-requests/{dnt.Id}/approve", new ApprovalActionRequest("Đồng ý chi"), Ct));
        Assert.Equal("Approved", dnt.Status);
        Assert.False(dnt.CanPay);

        // Kế toán ghi Đã chi; đã chi thì không hủy.
        var accountantRole = UniqueCode("K");
        await _factory.CreateRoleAsync(accountantRole, Permissions.Purchase.PaymentView, Permissions.Purchase.PaymentPay);
        var accountantName = $"kt.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(accountantName, f.CompanyCode, accountantRole);
        var accountant = await _factory.CreateClientInCompanyAsync(accountantName, ProcApiFactory.TestPassword, f.CompanyCode);
        Assert.True((await ReadAsync<PaymentRequestDto>(await accountant.GetAsync($"/api/payment-requests/{dnt.Id}", Ct))).CanPay);
        Assert.Contains("sau hôm nay", await TitleAsync(await accountant.PostAsJsonAsync($"/api/payment-requests/{dnt.Id}/pay",
            new MarkPaymentPaidRequest(Today.AddDays(1), null), Ct)));
        dnt = await ReadAsync<PaymentRequestDto>(await accountant.PostAsJsonAsync($"/api/payment-requests/{dnt.Id}/pay",
            new MarkPaymentPaidRequest(Today, "UNC 0456"), Ct));
        Assert.Equal((Today, "UNC 0456", false), (dnt.PaidDate, dnt.PaymentNote, dnt.CanPay));
        Assert.Contains("Đã chi", await TitleAsync(await f.Buyer.PostAsJsonAsync($"/api/payment-requests/{dnt.Id}/cancel",
            new CancelDocumentRequest("Nhầm"), Ct)));
        // In 5B (lát Y8): kế toán chỉ có quyền đề nghị thanh toán (không xem PO) vẫn in được.
        var pdf = await accountant.GetAsync($"/api/payment-requests/{dnt.Id}/pdf", Ct);
        Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType?.MediaType);
        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(await pdf.Content.ReadAsByteArrayAsync(Ct), 0, 4), StringComparison.Ordinal);

        // Nội dung mẫu 5B tiền mặt: lũy kế = đã mua trước + đơn (950.000), còn lại = định mức − lũy kế; dòng giảm trừ, số tiền còn lại.
        var form = Proc.Infrastructure.Printing.CuvtPrintForms.PaymentRequest(dnt,
            new Proc.Infrastructure.Printing.PoPrintContext("Công ty thử", null, "Xưởng cán", "Dây curoa", 2_000_000m, 50_000m, 5_000m, null,
                new Dictionary<Guid, string?>(), new Dictionary<Guid, decimal>()), 950_000m);
        Assert.Equal(("5B", "PHIẾU ĐỀ NGHỊ THANH TOÁN TIỀN MẶT", 2), (form.FormNo, form.Title, form.Rows.Count));
        Assert.Contains(form.Info, i => i.Label == "Lũy kế số tiền đã mua" && i.Value == "1.000.000 đ");
        Assert.Contains(form.Info, i => i.Label == "Số tiền còn lại" && i.Value == "1.000.000 đ");
        Assert.Equal(["627.000"], form.TotalCells);
        Assert.Equal("600.000", form.ExtraRows![1].Cells[0].Text);

        var chain = await ReadAsync<CancelChainDto>(await f.Buyer.GetAsync($"/api/cancel-chain/DNT/{dnt.Id}", Ct));
        Assert.Contains("Đã chi", chain.BlockedReason, StringComparison.Ordinal);

        // Đề nghị nháp khác (sau khi giải phóng): hủy dây chuyền chỉ gồm chính nó, XNH chọn lại được.
        await ReadAsync<ReceiptConfirmationDto>(await f.Buyer.PutAsJsonAsync($"/api/receipt-confirmations/{xnh3.Id}/invoice",
            new UpdateReceiptConfirmationInvoiceRequest("103", "1C26TAA", Today, 418_000), Ct));
        var draft = await ReadAsync<PaymentRequestDto>(await f.Buyer.PostAsJsonAsync("/api/payment-requests", Save("Cash", 0, null, xnh3.Id), Ct));
        chain = await ReadAsync<CancelChainDto>(await f.Buyer.GetAsync($"/api/cancel-chain/DNT/{draft.Id}", Ct));
        Assert.Equal(draft.No, Assert.Single(chain.Items).No);
        await ReadAsync<CancelChainDto>(await f.Buyer.PostAsJsonAsync($"/api/cancel-chain/DNT/{draft.Id}", new CancelChainRequest("Lập nhầm"), Ct));
        Assert.Null((await ReadAsync<PaymentRequestSourceDto>(await f.Buyer.GetAsync($"/api/payment-requests/source?purchaseOrderId={po.Id}", Ct)))
            .Receipts.Single(r => r.Id == xnh3.Id).HeldBy);

        var row = Assert.Single(await ReadAsync<List<PaymentRequestListItemDto>>(await accountant.GetAsync("/api/payment-requests", Ct)), r => r.Id == dnt.Id);
        Assert.True(row.IsPaid);
    }
}
