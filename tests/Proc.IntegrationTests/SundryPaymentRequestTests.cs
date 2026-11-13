using System.Net;
using System.Net.Http.Json;
using Proc.Application.Common;
using Proc.Application.Purchasing;
using Proc.Domain.Organization;

namespace Proc.IntegrationTests;

/// <summary>
/// Lát Y11b (design.md mục 19.2 loại C): đề nghị thanh toán tổng hợp 1C – gộp nhiều bảng kê 2C đã duyệt của một bộ phận, trả người nhận tiền,
/// mỗi bảng kê chỉ ở một đề nghị chưa hủy, duyệt theo bộ cấp DNT, kế toán ghi Đã chi, in PDF; hủy dây chuyền từ bảng kê kéo theo đề nghị.
/// </summary>
public partial class PurchasingTests
{
    [Fact]
    public async Task DeNghiTongHop1C_GopBangKe_NguoiNhan_Duyet_DaChi_HuyDayChuyen()
    {
        var f = await ArrangeAsync();
        var sheetApprover = await ApproverAsync(f, Permissions.Purchase.SundryView, Permissions.Purchase.SundryApprove);
        var payApprover = await ApproverAsync(f, Permissions.Purchase.PaymentView, Permissions.Purchase.PaymentApprove);

        async Task<SundryExpenseSheetDto> SheetAsync(decimal amount, bool approve)
        {
            var sheet = await ReadAsync<SundryExpenseSheetDto>(await f.Buyer.PostAsJsonAsync("/api/sundry-expense-sheets",
                new SaveSundryExpenseSheetRequest(Today, f.DepartmentId, null, [new(Today, "Chi tạp vụ", null, null, amount)]), Ct));
            if (!approve) return sheet;
            await ReadAsync<SundryExpenseSheetDto>(await f.Buyer.PostAsync($"/api/sundry-expense-sheets/{sheet.Id}/submit", null, Ct));
            return await ReadAsync<SundryExpenseSheetDto>(await sheetApprover.PostAsJsonAsync(
                $"/api/sundry-expense-sheets/{sheet.Id}/approve", new ApprovalActionRequest(null), Ct));
        }

        var first = await SheetAsync(550_500, true);
        var second = await SheetAsync(195_500, true);
        var draftSheet = await SheetAsync(10_000, false);
        Assert.Equal(("Approved", "Approved"), (first.Status, second.Status));

        var options = await ReadAsync<List<SundryPaymentSheetOptionDto>>(
            await f.Buyer.GetAsync($"/api/payment-requests/sundry-sheets?departmentId={f.DepartmentId}", Ct));
        Assert.Equal([first.Id, second.Id], options.Select(o => o.Id).OrderBy(x => x != first.Id));

        SavePaymentRequestRequest Save(string method, decimal deduction, string? bankName, params Guid[] sheets) =>
            new(Today, null, method, null, deduction, deduction > 0 ? "Trừ tạm ứng" : null, "Tạp vụ tháng",
                sheets.Select(s => new SavePaymentRequestLineRequest(null, null, s)).ToList(), "1C", f.DepartmentId, "Nguyễn Văn An", bankName, null,
                bankName is null ? null : "0071000123456", bankName is null ? null : "NGUYEN VAN AN");

        Assert.Contains("chưa duyệt", await TitleAsync(await f.Buyer.PostAsJsonAsync("/api/payment-requests", Save("Cash", 0, null, draftSheet.Id), Ct)));
        Assert.Contains("tên ngân hàng", await TitleAsync(await f.Buyer.PostAsJsonAsync("/api/payment-requests",
            Save("BankTransfer", 0, null, first.Id), Ct)));
        Assert.Equal(HttpStatusCode.BadRequest, (await f.Buyer.PostAsJsonAsync("/api/payment-requests",
            Save("Cash", 0, null, first.Id) with { PayeeName = " " }, Ct)).StatusCode);

        // Hai bảng kê: 550.500 + 195.500 − giảm trừ 46.000 = 700.000; chuyển khoản nhập tay tài khoản người nhận.
        var dnt = await ReadAsync<PaymentRequestDto>(await f.Buyer.PostAsJsonAsync("/api/payment-requests",
            Save("BankTransfer", 46_000, "Vietcombank", first.Id, second.Id), Ct));
        Assert.Equal(("1C", "Nguyễn Văn An", 746_000m, 700_000m, 2), (dnt.Form, dnt.PayeeName, dnt.TotalAmount, dnt.PayableAmount, dnt.Lines.Count));
        Assert.Null(dnt.PurchaseOrderId);
        Assert.Equal(("Vietcombank", "0071000123456", (Guid?)null), (dnt.BankName, dnt.BankAccountNumber, dnt.BankAccountId));
        Assert.Equal((first.No, $"Thanh toán chi phí tạp vụ theo bảng kê {first.No} ngày {Today:dd/MM/yyyy}"),
            (dnt.Lines[0].SundryExpenseSheetNo, dnt.Lines[0].Description));

        // Bảng kê đã ở đề nghị: không gộp lại, không hủy riêng; danh sách bảng kê ghi số đề nghị.
        Assert.Contains("đã ở đề nghị thanh toán", await TitleAsync(await f.Buyer.PostAsJsonAsync("/api/payment-requests", Save("Cash", 0, null, first.Id), Ct)));
        Assert.Contains(dnt.No, await TitleAsync(await f.Buyer.PostAsJsonAsync($"/api/sundry-expense-sheets/{first.Id}/cancel",
            new CancelDocumentRequest("Nhầm"), Ct)));
        Assert.Equal(dnt.No, (await ReadAsync<SundryExpenseSheetDto>(await f.Buyer.GetAsync($"/api/sundry-expense-sheets/{first.Id}", Ct))).PaymentRequestNo);
        Assert.Equal(dnt.No, Assert.Single(await ReadAsync<List<SundryPaymentSheetOptionDto>>(
            await f.Buyer.GetAsync($"/api/payment-requests/sundry-sheets?departmentId={f.DepartmentId}", Ct)), o => o.Id == first.Id).HeldBy);

        // Gửi, duyệt (chưa cấu hình DNT → một cấp theo quyền), kế toán ghi Đã chi; in 1C.
        dnt = await ReadAsync<PaymentRequestDto>(await f.Buyer.PostAsync($"/api/payment-requests/{dnt.Id}/submit", null, Ct));
        Assert.Contains(await ReadAsync<List<PendingApprovalDto>>(await payApprover.GetAsync("/api/approvals/pending", Ct)),
            p => p.DocumentId == dnt.Id && p.Description!.StartsWith("1C", StringComparison.Ordinal));
        dnt = await ReadAsync<PaymentRequestDto>(await payApprover.PostAsJsonAsync($"/api/payment-requests/{dnt.Id}/approve", new ApprovalActionRequest(null), Ct));
        Assert.Equal("Approved", dnt.Status);
        dnt = await ReadAsync<PaymentRequestDto>(await f.Buyer.PostAsJsonAsync($"/api/payment-requests/{dnt.Id}/pay",
            new MarkPaymentPaidRequest(Today, "PC 0012"), Ct));
        Assert.Equal(Today, dnt.PaidDate);
        var pdf = await f.Buyer.GetAsync($"/api/payment-requests/{dnt.Id}/pdf", Ct);
        Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);
        var form = Proc.Infrastructure.Printing.CuvtPrintForms.SundryPaymentRequest(dnt, "Công ty thử");
        Assert.Equal(("1C", "PHIẾU ĐỀ NGHỊ THANH TOÁN CHUYỂN KHOẢN", 2), (form.FormNo, form.Title, form.Rows.Count));
        Assert.Contains(form.Info, i => i.Label == "Người nhận tiền" && i.Value == "Nguyễn Văn An");
        Assert.Equal("700.000", form.ExtraRows![1].Cells[0].Text);
        Assert.StartsWith(Proc.Infrastructure.Printing.CuvtPrintForms.SimulatedNote, form.StatusNote, StringComparison.Ordinal);
        // Đã chi: hủy dây chuyền từ bảng kê bị chặn.
        Assert.Contains("Đã chi", (await ReadAsync<CancelChainDto>(await f.Buyer.GetAsync($"/api/cancel-chain/BKT/{first.Id}", Ct))).BlockedReason,
            StringComparison.Ordinal);
        var row = Assert.Single(await ReadAsync<List<PaymentRequestListItemDto>>(await f.Buyer.GetAsync("/api/payment-requests", Ct)), r => r.Id == dnt.Id);
        Assert.Equal(("1C", "Nguyễn Văn An", (string?)null), (row.Form, row.PayeeName, row.PurchaseOrderNo));

        // Bảng kê thứ ba ở đề nghị nháp: hủy dây chuyền từ bảng kê → hủy đề nghị trước rồi đến bảng kê.
        var third = await SheetAsync(80_000, true);
        var draft = await ReadAsync<PaymentRequestDto>(await f.Buyer.PostAsJsonAsync("/api/payment-requests", Save("Cash", 0, null, third.Id), Ct));
        var chain = await ReadAsync<CancelChainDto>(await f.Buyer.GetAsync($"/api/cancel-chain/BKT/{third.Id}", Ct));
        Assert.Equal([draft.No, third.No], chain.Items.Select(i => i.No));
        await ReadAsync<CancelChainDto>(await f.Buyer.PostAsJsonAsync($"/api/cancel-chain/BKT/{third.Id}", new CancelChainRequest("Kê nhầm"), Ct));
        Assert.Equal("Cancelled", (await ReadAsync<PaymentRequestDto>(await f.Buyer.GetAsync($"/api/payment-requests/{draft.Id}", Ct))).Status);
        Assert.Equal("Cancelled", (await ReadAsync<SundryExpenseSheetDto>(await f.Buyer.GetAsync($"/api/sundry-expense-sheets/{third.Id}", Ct))).Status);
    }

    /// <summary>Người dùng có đúng các quyền đưa vào, tại công ty của fixture.</summary>
    private async Task<HttpClient> ApproverAsync(Fixture f, params string[] permissions)
    {
        var role = UniqueCode("Q");
        await _factory.CreateRoleAsync(role, permissions);
        var userName = $"q.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(userName, f.CompanyCode, role);
        return await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, f.CompanyCode);
    }
}
