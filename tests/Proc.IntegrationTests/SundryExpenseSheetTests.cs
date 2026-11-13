using System.Net;
using System.Net.Http.Json;
using Proc.Application.Common;
using Proc.Application.Purchasing;
using Proc.Domain.Organization;
using Proc.Domain.Purchasing;

namespace Proc.IntegrationTests;

/// <summary>
/// Lát Y11a (design.md mục 19.2 loại C): bảng kê chi phí tạp vụ 2C – chỉ kê chi phí đã phát sinh, tính vào định mức tháng của bộ phận, vượt thì thêm
/// chặng TVB không chặn (Q5), duyệt theo quyền bảng kê, hủy dây chuyền chỉ gồm chính bảng kê, có trong Việc cần làm.
/// </summary>
public partial class PurchasingTests
{
    [Fact]
    public async Task BangKeTapVu_DinhMucThang_VuotThemChang_Duyet_Huy()
    {
        var f = await ArrangeAsync();
        // Định mức tháng của bộ phận: 1.000.000.
        await ReadAsync<DepartmentBudgetYearDto>(await f.Buyer.PutAsJsonAsync($"/api/budgets/{Today.Year}/{f.DepartmentId}",
            new SaveDepartmentBudgetRequest(Enumerable.Range(1, 12).Select(m => m == Today.Month ? 1_000_000m : (decimal?)null).ToList()), Ct));

        SaveSundryExpenseSheetRequest Save(string? note, params SaveSundryExpenseLineRequest[] lines) => new(Today, f.DepartmentId, note, lines);

        Assert.Contains("đã phát sinh", await TitleAsync(await f.Buyer.PostAsJsonAsync("/api/sundry-expense-sheets",
            Save(null, new SaveSundryExpenseLineRequest(Today.AddDays(1), "Nước uống", null, null, 50_000)), Ct)));
        Assert.Contains("lớn hơn 0", await TitleAsync(await f.Buyer.PostAsJsonAsync("/api/sundry-expense-sheets",
            Save(null, new SaveSundryExpenseLineRequest(Today, "Nước uống", null, null, 0)), Ct)));
        Assert.Contains("nội dung chi", await TitleAsync(await f.Buyer.PostAsJsonAsync("/api/sundry-expense-sheets",
            Save(null, new SaveSundryExpenseLineRequest(Today, " ", null, null, 10_000)), Ct)));

        // Bảng kê 1: 300.000 + 250.500 = 550.500 – trong định mức.
        var first = await ReadAsync<SundryExpenseSheetDto>(await f.Buyer.PostAsJsonAsync("/api/sundry-expense-sheets", Save("Tạp vụ đầu tháng",
            new SaveSundryExpenseLineRequest(Today, "Nước uống văn phòng", "Cửa hàng A", "HD-01", 300_000),
            new SaveSundryExpenseLineRequest(Today, "Gửi chuyển phát", null, null, 250_500)), Ct));
        Assert.StartsWith("BKT", first.No, StringComparison.Ordinal);
        Assert.Equal(("Draft", 550_500m, 2, 1_000_000m, false), (first.Status, first.TotalAmount, first.Lines.Count, first.Budget!.BudgetAmount, first.Budget.IsOver));

        first = await ReadAsync<SundryExpenseSheetDto>(await f.Buyer.PostAsync($"/api/sundry-expense-sheets/{first.Id}/submit", null, Ct));
        Assert.Equal("Submitted", first.Status);
        Assert.DoesNotContain(first.Approvals, s => s.Stage == ApprovalTypes.OverBudgetStage);
        // Việc cần làm của người có quyền duyệt bảng kê.
        var role = UniqueCode("B");
        await _factory.CreateRoleAsync(role, Permissions.Purchase.SundryView, Permissions.Purchase.SundryApprove);
        var approverName = $"bk.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(approverName, f.CompanyCode, role);
        var approver = await _factory.CreateClientInCompanyAsync(approverName, ProcApiFactory.TestPassword, f.CompanyCode);
        Assert.Contains(await ReadAsync<List<PendingApprovalDto>>(await approver.GetAsync("/api/approvals/pending", Ct)),
            p => p.DocumentId == first.Id && p.DocType == "BKT");
        first = await ReadAsync<SundryExpenseSheetDto>(await approver.PostAsJsonAsync(
            $"/api/sundry-expense-sheets/{first.Id}/approve", new ApprovalActionRequest("Đồng ý"), Ct));
        Assert.Equal("Approved", first.Status);
        Assert.False(first.Actions.CanEdit);
        // In 2C (lát Y11c – mẫu giả lập): người duyệt chỉ có quyền bảng kê in được; nội dung mẫu.
        var pdf = await approver.GetAsync($"/api/sundry-expense-sheets/{first.Id}/pdf", Ct);
        Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType?.MediaType);
        var form = Proc.Infrastructure.Printing.CuvtPrintForms.SundryExpenseSheet(first, "Công ty thử");
        Assert.Equal(("2C", "BẢNG KÊ CHI PHÍ TẠP VỤ", true, 2, 6), (form.FormNo, form.Title, form.Landscape, form.Rows.Count, form.Columns.Count));
        Assert.Contains(form.Info, i => i.Label.StartsWith("Định mức", StringComparison.Ordinal) && i.Value == "1.000.000 đ");
        Assert.Equal(["550.500"], form.TotalCells);
        // Mẫu giả lập (lát Y11d): bản in ghi rõ chỉ dùng thử.
        Assert.Equal(Proc.Infrastructure.Printing.CuvtPrintForms.SimulatedNote, form.StatusNote);

        // Bảng kê 2: 500.000 → đã dùng 550.500 + 500.000 > 1.000.000 → vượt, gửi được nhưng thêm chặng TVB.
        var second = await ReadAsync<SundryExpenseSheetDto>(await f.Buyer.PostAsJsonAsync("/api/sundry-expense-sheets",
            Save(null, new SaveSundryExpenseLineRequest(Today, "Văn phòng phẩm lặt vặt", null, null, 500_000)), Ct));
        Assert.Equal((550_500m, true, 50_500m), (second.Budget!.UsedAmount, second.Budget.IsOver, second.Budget.OverAmount));
        second = await ReadAsync<SundryExpenseSheetDto>(await f.Buyer.PostAsync($"/api/sundry-expense-sheets/{second.Id}/submit", null, Ct));
        Assert.Equal("Submitted", second.Status);
        Assert.Contains(second.Approvals, s => s.Stage == ApprovalTypes.OverBudgetStage);

        // Ngân sách năm: đã dùng gồm cả hai bảng kê.
        var year = await ReadAsync<List<DepartmentBudgetYearDto>>(await f.Buyer.GetAsync($"/api/budgets/{Today.Year}", Ct));
        Assert.Equal(1_050_500m, year.Single(d => d.DepartmentId == f.DepartmentId).Months[Today.Month - 1].Used);

        // Hủy dây chuyền: chuỗi chỉ có chính bảng kê; người lập là quản trị nên hủy được phiếu đã gửi (lý do bắt buộc).
        var chain = await ReadAsync<CancelChainDto>(await f.Buyer.GetAsync($"/api/cancel-chain/BKT/{second.Id}", Ct));
        Assert.Equal(second.No, Assert.Single(chain.Items).No);
        Assert.Contains("lý do", await TitleAsync(await f.Buyer.PostAsJsonAsync($"/api/sundry-expense-sheets/{second.Id}/cancel",
            new CancelDocumentRequest(null), Ct)));
        second = await ReadAsync<SundryExpenseSheetDto>(await f.Buyer.PostAsJsonAsync($"/api/sundry-expense-sheets/{second.Id}/cancel",
            new CancelDocumentRequest("Kê trùng"), Ct));
        Assert.Equal(("Cancelled", "Kê trùng"), (second.Status, second.CancelReason));
        year = await ReadAsync<List<DepartmentBudgetYearDto>>(await f.Buyer.GetAsync($"/api/budgets/{Today.Year}", Ct));
        Assert.Equal(550_500m, year.Single(d => d.DepartmentId == f.DepartmentId).Months[Today.Month - 1].Used);

        // Người chỉ có quyền duyệt không sửa được, không lập được.
        Assert.Equal(HttpStatusCode.Forbidden, (await approver.PostAsJsonAsync("/api/sundry-expense-sheets",
            Save(null, new SaveSundryExpenseLineRequest(Today, "Trà", null, null, 10_000)), Ct)).StatusCode);
        var row = Assert.Single(await ReadAsync<List<SundryExpenseSheetListItemDto>>(await approver.GetAsync("/api/sundry-expense-sheets", Ct)),
            r => r.Id == first.Id);
        Assert.Equal(("Approved", 2, 550_500m), (row.Status, row.LineCount, row.TotalAmount));
    }
}
