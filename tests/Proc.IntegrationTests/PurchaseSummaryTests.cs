using System.Net.Http.Json;
using Proc.Application.Purchasing;
using Proc.Domain.Organization;

namespace Proc.IntegrationTests;

/// <summary>
/// Lát Y5b (design.md mục 19.3 bước 2): bảng tổng hợp đề xuất mua 2A/2B – chọn dòng đề nghị đã duyệt còn cần mua của một bộ phận (2A: cùng công
/// trình), gán đơn vị mua từng dòng; mỗi dòng đề nghị chỉ ở một bảng chưa hủy; duyệt theo bộ cấp BTH; hủy bảng thì dòng chọn lại được.
/// </summary>
public partial class PurchasingTests
{
    /// <summary>Người duyệt bảng tổng hợp mới (vai trò mới có quyền xem + duyệt BTH) tại công ty fixture.</summary>
    private async Task<(HttpClient Client, Guid RoleId)> SummaryApproverAsync(Fixture f)
    {
        var roleCode = UniqueCode("B");
        var roleId = await _factory.CreateRoleAsync(roleCode, Permissions.Purchase.SummaryView, Permissions.Purchase.SummaryApprove);
        var userName = $"bt.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(userName, f.CompanyCode, roleCode);
        return (await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, f.CompanyCode), roleId);
    }

    /// <summary>Đề nghị mua đã duyệt (một cấp – chưa cấu hình cấp PR thì ai có quyền duyệt PR duyệt được).</summary>
    private static async Task<PurchaseRequestDto> ApprovedPrAsync(Fixture f, HttpClient approver, string? category = null, Guid? projectId = null, decimal qty = 10)
    {
        var pr = await ReadAsync<PurchaseRequestDto>(await CreatePrOfAsync(f, category, projectId, qty));
        pr = await ReadAsync<PurchaseRequestDto>(await f.Buyer.PostAsync($"/api/purchase-requests/{pr.Id}/submit", null, Ct));
        pr = await ReadAsync<PurchaseRequestDto>(await ActAsync(approver, "purchase-requests", pr.Id, "approve"));
        Assert.Equal("Approved", pr.Status);
        return pr;
    }

    private static async Task<PurchasingUnitDto> UnitAsync(Fixture f) =>
        await ReadAsync<PurchasingUnitDto>(await f.Buyer.PostAsJsonAsync("/api/purchasing-units",
            new CreatePurchasingUnitRequest(UniqueCode("DV"), "Phòng CUVT", null), Ct));

    private static Task<HttpResponseMessage> SaveSummaryAsync(
        Fixture f, string form, Guid departmentId, Guid? projectId, IReadOnlyList<SavePurchaseSummaryLineRequest> lines, Guid? id = null)
    {
        var request = new SavePurchaseSummaryRequest(Today, form, departmentId, projectId, "Tổng hợp tháng", lines);
        return id is { } summaryId
            ? f.Buyer.PutAsJsonAsync($"/api/purchase-summaries/{summaryId}", request, Ct)
            : f.Buyer.PostAsJsonAsync("/api/purchase-summaries", request, Ct);
    }

    private static async Task<List<PurchaseSummaryCandidateDto>> CandidatesAsync(Fixture f, string form, Guid? projectId = null) =>
        (await f.Buyer.GetFromJsonAsync<List<PurchaseSummaryCandidateDto>>(
            $"/api/purchase-summaries/candidates?form={form}&departmentId={f.DepartmentId}" + (projectId is { } p ? $"&constructionProjectId={p}" : ""), Ct))!;

    [Fact]
    public async Task BangTongHop2B_DongDeNghiConCanMua_MoiDongMotBang_DuyetQuaBoCapBth_HuyThiChonLaiDuoc()
    {
        var f = await ArrangeAsync();
        var (approver, _) = await ApproverAsync(f);
        var pr = await ApprovedPrAsync(f, approver);
        var line = pr.Lines.Single();
        var unit = await UnitAsync(f);

        // Đề nghị nháp chưa duyệt không là dòng chọn được; đề nghị đã duyệt đã đặt PO 4/10 → còn cần mua 6.
        var draft = await ReadAsync<PurchaseRequestDto>(await CreatePrOfAsync(f, null, null, 3));
        var po = await ReadAsync<PurchaseOrderDto>(await CreatePoAsync(f, 4, line.Id));
        var candidates = await CandidatesAsync(f, "2B");
        Assert.DoesNotContain(candidates, c => c.RequestId == draft.Id);
        Assert.Equal(6m, candidates.Single(c => c.RequestLineId == line.Id).OpenQty);
        Assert.Empty(await CandidatesAsync(f, "2A"));

        // 2B lấy được đề nghị loại B: SL chụp 6, thành tiền = SL × đơn giá tham khảo; thiếu đơn vị mua bị chặn.
        Assert.Contains("đơn vị mua", await TitleAsync(await SaveSummaryAsync(f, "2B", f.DepartmentId, null, [new(line.Id, Guid.Empty, null)])));
        var summary = await ReadAsync<PurchaseSummaryDto>(await SaveSummaryAsync(f, "2B", f.DepartmentId, null, [new(line.Id, unit.Id, "Mua gấp")]));
        Assert.StartsWith("BTH", summary.No);
        Assert.Equal(("Goods", "Draft"), (summary.Form, summary.Status));
        var sl = summary.Lines.Single();
        Assert.Equal((6m, unit.Code, pr.No), (sl.Qty, sl.PurchasingUnitCode, sl.RequestNo));
        Assert.Equal(decimal.Round(6m * sl.EstPrice, 2, MidpointRounding.AwayFromZero), summary.TotalAmount);
        Assert.Equal(summary.TotalAmount, summary.Frame.ProposedAmount);

        // Mỗi dòng đề nghị chỉ ở một bảng chưa hủy: không còn trong danh sách chọn, bảng thứ hai bị chặn.
        Assert.DoesNotContain(await CandidatesAsync(f, "2B"), c => c.RequestLineId == line.Id);
        Assert.Contains(summary.No, await TitleAsync(await SaveSummaryAsync(f, "2B", f.DepartmentId, null, [new(line.Id, unit.Id, null)])));
        // Sai biểu mẫu / bộ phận: 2A chỉ lấy đề nghị loại A; bộ phận khác với bộ phận của đề nghị.
        var project = await ProjectAsync(f, 1_000_000m);
        Assert.Contains("loại A", await TitleAsync(await SaveSummaryAsync(f, "2A", f.DepartmentId, project.Id, [new(line.Id, unit.Id, null)], summary.Id)));
        var other = await TestDepartments.CreateAsync(f.Buyer, "Phòng khác");
        Assert.Contains("bộ phận khác", await TitleAsync(await SaveSummaryAsync(f, "2B", other, null, [new(line.Id, unit.Id, null)], summary.Id)));
        // Đề nghị đang ở bảng tổng hợp chưa hủy: hủy theo thứ tự từ phiếu sau – hủy PO rồi vẫn phải hủy bảng tổng hợp trước.
        await ReadAsync<PurchaseOrderDto>(await f.Buyer.PostAsJsonAsync($"/api/purchase-orders/{po.Id}/cancel", new CancelDocumentRequest("Đặt lại"), Ct));
        Assert.Contains("bảng tổng hợp", await TitleAsync(await f.Buyer.PostAsJsonAsync($"/api/purchase-requests/{pr.Id}/cancel",
            new CancelDocumentRequest("Không mua nữa"), Ct)));

        // Bộ cấp BTH 2 cấp theo vai trò: cấp 1 duyệt trước, cấp 2 chưa đến lượt; phiếu có trong việc chờ duyệt của cấp 1 kèm "2B – bộ phận".
        var (first, firstRole) = await SummaryApproverAsync(f);
        var (second, secondRole) = await SummaryApproverAsync(f);
        await ReadAsync<ApprovalRuleDto>(await RuleAsync(f, "BTH", 1, firstRole));
        await ReadAsync<ApprovalRuleDto>(await RuleAsync(f, "BTH", 2, secondRole));
        summary = await ReadAsync<PurchaseSummaryDto>(await f.Buyer.PostAsync($"/api/purchase-summaries/{summary.Id}/submit", null, Ct));
        Assert.Equal(("Submitted", 2), (summary.Status, summary.Approvals.Count));
        // Gửi duyệt chụp lại SL còn cần mua: PO 4 đã hủy → còn 10.
        Assert.Equal(10m, summary.Lines.Single().Qty);
        var pending = (await first.GetFromJsonAsync<List<PendingApprovalDto>>("/api/approvals/pending", Ct))!.Single(p => p.DocumentId == summary.Id);
        Assert.Equal(("BTH", "Bảng tổng hợp đề xuất mua"), (pending.DocType, pending.DocTypeName));
        Assert.StartsWith("2B – ", pending.Description);
        Assert.Contains("vai trò", await TitleAsync(await ActAsync(second, "purchase-summaries", summary.Id, "approve")));
        await ReadAsync<PurchaseSummaryDto>(await ActAsync(first, "purchase-summaries", summary.Id, "approve", "Đồng ý"));
        summary = await ReadAsync<PurchaseSummaryDto>(await ActAsync(second, "purchase-summaries", summary.Id, "approve"));
        Assert.Equal("Approved", summary.Status);
        Assert.False(summary.Actions.CanEdit);

        // Đã duyệt: hủy chỉ quản trị, bắt buộc lý do; hủy xong dòng đề nghị chọn lại được.
        Assert.Contains("lý do", await TitleAsync(await f.Buyer.PostAsJsonAsync($"/api/purchase-summaries/{summary.Id}/cancel", new CancelDocumentRequest(null), Ct)));
        summary = await ReadAsync<PurchaseSummaryDto>(await f.Buyer.PostAsJsonAsync($"/api/purchase-summaries/{summary.Id}/cancel",
            new CancelDocumentRequest("Lập lại theo đơn vị mua khác"), Ct));
        Assert.Equal("Cancelled", summary.Status);
        Assert.Contains(await CandidatesAsync(f, "2B"), c => c.RequestLineId == line.Id);
    }

    [Fact]
    public async Task BangTongHop2A_CungCongTrinh_KhungDuToan_RutVeNhap()
    {
        var f = await ArrangeAsync();
        var (approver, _) = await ApproverAsync(f);
        var project = await ProjectAsync(f, 1_000_000_000m);
        var otherProject = await ProjectAsync(f, 5_000_000m);
        var pr = await ApprovedPrAsync(f, approver, "Construction", project.Id, 8);
        var unit = await UnitAsync(f);

        // Danh sách chọn 2A lọc theo công trình; dòng của công trình khác không vào được bảng.
        Assert.Single(await CandidatesAsync(f, "2A", project.Id));
        Assert.Empty(await CandidatesAsync(f, "2A", otherProject.Id));
        Assert.Contains("công trình khác", await TitleAsync(
            await SaveSummaryAsync(f, "2A", f.DepartmentId, otherProject.Id, [new(pr.Lines[0].Id, unit.Id, null)])));
        Assert.Contains("công trình", await TitleAsync(await SaveSummaryAsync(f, "2A", f.DepartmentId, null, [new(pr.Lines[0].Id, unit.Id, null)])));

        // Khung dự toán: còn lại = dự toán − lũy kế đã mua − tổng đề xuất (bảng còn nháp / chờ duyệt).
        var summary = await ReadAsync<PurchaseSummaryDto>(await SaveSummaryAsync(f, "2A", f.DepartmentId, project.Id, [new(pr.Lines[0].Id, unit.Id, null)]));
        Assert.Equal(("Construction", 8m), (summary.Form, summary.Lines[0].Qty));
        Assert.Equal(1_000_000_000m, summary.Frame.LimitAmount);
        Assert.Equal(project.Code, summary.Frame.ProjectCode);
        Assert.Equal(summary.Frame.LimitAmount - summary.Frame.AccumulatedAmount - summary.TotalAmount, summary.Frame.RemainingAmount);
        Assert.False(summary.Frame.Exceeded);

        // Có cấp duyệt: người lập rút về nháp được khi chờ duyệt; người khác (người duyệt) không rút được.
        var (checker, checkerRole) = await SummaryApproverAsync(f);
        await ReadAsync<ApprovalRuleDto>(await RuleAsync(f, "BTH", 1, checkerRole));
        summary = await ReadAsync<PurchaseSummaryDto>(await f.Buyer.PostAsync($"/api/purchase-summaries/{summary.Id}/submit", null, Ct));
        Assert.True(summary.Actions.CanRecall);
        Assert.True((await checker.GetFromJsonAsync<PurchaseSummaryDto>($"/api/purchase-summaries/{summary.Id}", Ct))!.Actions.CanApprove);
        summary = await ReadAsync<PurchaseSummaryDto>(await f.Buyer.PostAsync($"/api/purchase-summaries/{summary.Id}/recall", null, Ct));
        Assert.Equal("Draft", summary.Status);
        // Người duyệt từ chối thì phiếu về nháp kèm lý do.
        summary = await ReadAsync<PurchaseSummaryDto>(await f.Buyer.PostAsync($"/api/purchase-summaries/{summary.Id}/submit", null, Ct));
        summary = await ReadAsync<PurchaseSummaryDto>(await ActAsync(checker, "purchase-summaries", summary.Id, "reject", "Thiếu đơn vị mua phù hợp"));
        Assert.Equal(("Draft", "Thiếu đơn vị mua phù hợp"), (summary.Status, summary.RejectReason));
    }
}
