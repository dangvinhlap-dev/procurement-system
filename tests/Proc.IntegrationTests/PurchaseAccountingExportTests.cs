using System.Net;
using System.Net.Http.Json;
using ClosedXML.Excel;
using Proc.Application.Common;
using Proc.Application.Purchasing;
using Proc.Application.Reports;
using Proc.Domain.Organization;

namespace Proc.IntegrationTests;

/// <summary>
/// Lát Y9b (design.md mục 19.3 "Lát Y9", thay ACCX – mục 19.4): xuất Excel chứng từ mua cho kế toán – PO theo ngày duyệt, XNH theo ngày nhận
/// (kèm hóa đơn), đề nghị thanh toán đã duyệt theo ngày duyệt; quyền riêng.
/// </summary>
public partial class PurchasingTests
{
    [Fact]
    public async Task XuatChungTuMuaKeToan_Po_Xnh_DeNghiThanhToan()
    {
        var f = await ArrangeAsync();
        var (approver, _) = await ApproverAsync(f);
        // PO 10 cái × 95.000 (sau CK 5%) = 950.000, VAT 95.000.
        var po = await ReadAsync<PurchaseOrderDto>(await CreatePoAsync(f, 10, null));
        await ReadAsync<PurchaseOrderDto>(await f.Buyer.PostAsync($"/api/purchase-orders/{po.Id}/submit", null, Ct));
        po = await ReadAsync<PurchaseOrderDto>(await approver.PostAsJsonAsync($"/api/purchase-orders/{po.Id}/approve", new ApprovalActionRequest(null), Ct));
        var xnh = await ConfirmXnhAsync(f, await ReadAsync<ReceiptConfirmationDto>(await ReceiveXnhAsync(f, po, 4)));
        await ReadAsync<ReceiptConfirmationDto>(await f.Buyer.PutAsJsonAsync($"/api/receipt-confirmations/{xnh.Id}/invoice",
            new UpdateReceiptConfirmationInvoiceRequest("301", "1C26TAA", Today, 418_000), Ct));
        var dnt = await ReadAsync<PaymentRequestDto>(await f.Buyer.PostAsJsonAsync("/api/payment-requests",
            new SavePaymentRequestRequest(Today, po.Id, "Cash", null, 18_000, "Trừ tạm ứng", null, [new SavePaymentRequestLineRequest(xnh.Id, null)]), Ct));
        // Đề nghị nháp chưa duyệt: chưa xuất.
        var query = $"from={Today.AddDays(-1):yyyy-MM-dd}&to={Today:yyyy-MM-dd}";

        var role = UniqueCode("K");
        await _factory.CreateRoleAsync(role, Permissions.Report.PurchaseAccountingExport, Permissions.Purchase.PaymentView,
            Permissions.Purchase.PaymentApprove, Permissions.Purchase.PaymentPay);
        var name = $"kt.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(name, f.CompanyCode, role);
        var accountant = await _factory.CreateClientInCompanyAsync(name, ProcApiFactory.TestPassword, f.CompanyCode);
        // Chỉ có quyền xem đề nghị thanh toán (không có quyền xuất) → 403.
        var viewRole = UniqueCode("V");
        await _factory.CreateRoleAsync(viewRole, Permissions.Purchase.PaymentView);
        var viewName = $"xv.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(viewName, f.CompanyCode, viewRole);
        var viewer = await _factory.CreateClientInCompanyAsync(viewName, ProcApiFactory.TestPassword, f.CompanyCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.GetAsync($"/api/purchase-accounting-export/documents?{query}", Ct)).StatusCode);

        var docs = await ReadAsync<List<PurchaseAccountingDocumentDto>>(await accountant.GetAsync($"/api/purchase-accounting-export/documents?{query}", Ct));
        Assert.DoesNotContain(docs, d => d.DocType == "DNT");
        var poDoc = Assert.Single(docs, d => d.DocumentId == po.Id);
        Assert.Equal(("PO", 950_000m, 95_000m, 1_045_000m), (poDoc.DocType, poDoc.Amount, poDoc.VatAmount, poDoc.TotalAmount));
        var xnhDoc = Assert.Single(docs, d => d.DocumentId == xnh.Id);
        Assert.Equal(("XNH", "301", "1C26TAA", 380_000m, 38_000m), (xnhDoc.DocType, xnhDoc.InvoiceNo, xnhDoc.InvoiceSeries, xnhDoc.Amount, xnhDoc.VatAmount));

        // Duyệt + chi → đề nghị xuất theo ngày duyệt: tổng 400.000 (hóa đơn 418.000 − giảm trừ 18.000), ghi ngày chi.
        await ReadAsync<PaymentRequestDto>(await f.Buyer.PostAsync($"/api/payment-requests/{dnt.Id}/submit", null, Ct));
        await ReadAsync<PaymentRequestDto>(await accountant.PostAsJsonAsync($"/api/payment-requests/{dnt.Id}/approve", new ApprovalActionRequest(null), Ct));
        await ReadAsync<PaymentRequestDto>(await accountant.PostAsJsonAsync($"/api/payment-requests/{dnt.Id}/pay", new MarkPaymentPaidRequest(Today, "UNC 9"), Ct));
        docs = await ReadAsync<List<PurchaseAccountingDocumentDto>>(await accountant.GetAsync(
            $"/api/purchase-accounting-export/documents?{query}&docTypes=DNT", Ct));
        var dntDoc = Assert.Single(docs);
        Assert.Equal((dnt.Id, 400_000m, "301"), (dntDoc.DocumentId, dntDoc.TotalAmount, dntDoc.InvoiceNo));
        Assert.Contains("Đã chi", dntDoc.Status, StringComparison.Ordinal);
        Assert.Contains("Loại chứng từ", await TitleAsync(await accountant.GetAsync($"/api/purchase-accounting-export/documents?{query}&docTypes=GRN", Ct)));

        var file = await accountant.GetAsync($"/api/purchase-accounting-export/file?{query}", Ct);
        Assert.Equal(HttpStatusCode.OK, file.StatusCode);
        using var workbook = new XLWorkbook(await file.Content.ReadAsStreamAsync(Ct));
        Assert.Equal(["Tổng hợp", "Đơn mua hàng", "Nhận hàng – hóa đơn", "Đề nghị thanh toán", "Hướng dẫn"], workbook.Worksheets.Select(w => w.Name));
        // Dữ liệu từ dòng 5 (dòng 4 tiêu đề cột): đề nghị có số hóa đơn, số tiền dòng, số tiền đề nghị.
        var payments = workbook.Worksheet("Đề nghị thanh toán");
        Assert.Equal(dnt.No, payments.Cell(5, 2).GetString());
        Assert.Contains(payments.Row(5).CellsUsed(), c => c.GetString() == "301");
        Assert.Contains(payments.Row(5).CellsUsed(), c => c.DataType == XLDataType.Number && c.GetValue<decimal>() == 400_000m);
        Assert.Equal(po.No, workbook.Worksheet("Đơn mua hàng").Cell(5, 2).GetString());
    }
}
