using System.Net;
using System.Net.Http.Json;
using Proc.Application.Purchasing;
using Proc.Domain.Organization;

namespace Proc.IntegrationTests;

/// <summary>
/// Hiện người duyệt (yêu cầu người dùng 03/11/2026): xem trước chuỗi duyệt trước khi gửi (chạy thử rồi hủy, không lưu gì), người duyệt
/// mọi cấp còn chờ (cấp sau: "sẽ duyệt"), người và thời điểm gửi duyệt của từng lượt.
/// </summary>
public partial class PurchasingTests
{
    [Fact]
    public async Task XemTruocChuoiDuyet_NguoiDuyetMoiCap_ThoiDiemGuiDuyet()
    {
        var f = await ArrangeAsync();
        async Task<(HttpClient Client, string FullName)> UserAsync(string roleCode)
        {
            var userName = $"xt.{Guid.NewGuid():N}"[..16];
            await _factory.CreateScopedUserAsync(userName, f.CompanyCode, roleCode);
            return (await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, f.CompanyCode), $"Người thử {userName}");
        }

        var headCode = UniqueCode("T");
        var headRole = await _factory.CreateRoleAsync(headCode, Permissions.Purchase.PoView, Permissions.Purchase.PoApprove);
        var directorCode = UniqueCode("G");
        var directorRole = await _factory.CreateRoleAsync(directorCode, Permissions.Purchase.PoView, Permissions.Purchase.PoApprove);
        var (head, headName) = await UserAsync(headCode);
        var (_, directorName) = await UserAsync(directorCode);
        await ReadAsync<ApprovalRuleDto>(await f.Buyer.PostAsJsonAsync("/api/approval-rules", new SaveApprovalRuleRequest("PO", 1, 0, headRole), Ct));
        await ReadAsync<ApprovalRuleDto>(await f.Buyer.PostAsJsonAsync("/api/approval-rules", new SaveApprovalRuleRequest("PO", 2, 500_000, directorRole), Ct));

        // PO 10 × 100.000 − 5% + VAT 10% = 1.045.000 ≥ 500.000 → hai cấp. Xem trước: cấp 1 "chờ" trưởng phòng, cấp 2 "sẽ duyệt" giám đốc.
        var po = await ReadAsync<PurchaseOrderDto>(await CreatePoAsync(f, 10, null));
        var preview = await ReadAsync<ApprovalPreviewDto>(await f.Buyer.PostAsJsonAsync("/api/approvals/preview", new ApprovalPreviewRequest("po", po.Id), Ct));
        Assert.False(preview.ApprovedImmediately);
        Assert.Equal([1, 2], preview.Steps.Select(s => s.Level));
        Assert.Equal([headName], preview.Steps[0].NextApprovers);
        Assert.Equal([directorName], preview.Steps[1].UpcomingApprovers);
        // Chạy thử không lưu gì: phiếu vẫn Nháp, chưa có bước duyệt.
        var draft = await ReadAsync<PurchaseOrderDto>(await f.Buyer.GetAsync($"/api/purchase-orders/{po.Id}", Ct));
        Assert.Equal(("Draft", 0), (draft.Status, draft.Approvals.Count));

        // Gửi thật: cùng chuỗi, kèm người và thời điểm gửi.
        po = await ReadAsync<PurchaseOrderDto>(await f.Buyer.PostAsync($"/api/purchase-orders/{po.Id}/submit", null, Ct));
        var (l1, l2) = (po.Approvals.Single(a => a.Level == 1), po.Approvals.Single(a => a.Level == 2));
        Assert.Equal([headName], l1.NextApprovers);
        Assert.Null(l1.UpcomingApprovers);
        Assert.Null(l2.NextApprovers);
        Assert.Equal([directorName], l2.UpcomingApprovers);
        Assert.NotNull(l1.SubmittedAt);
        Assert.False(string.IsNullOrEmpty(l1.SubmittedByName));
        // Đã gửi thì xem trước báo lỗi như gửi thật; người không có quyền lập PO không xem trước được.
        Assert.False((await f.Buyer.PostAsJsonAsync("/api/approvals/preview", new ApprovalPreviewRequest("PO", po.Id), Ct)).IsSuccessStatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await head.PostAsJsonAsync("/api/approvals/preview", new ApprovalPreviewRequest("PO", po.Id), Ct)).StatusCode);

        // Trưởng phòng duyệt cấp 1: thấy người duyệt cấp 2 là người chờ tiếp theo.
        po = await ReadAsync<PurchaseOrderDto>(await ActAsync(head, "purchase-orders", po.Id, "approve"));
        Assert.Equal(headName, po.Approvals.Single(a => a.Level == 1).ActedByName);
        Assert.Equal([directorName], po.Approvals.Single(a => a.Level == 2).NextApprovers);

        // PR: xem trước chạy được cho loại phiếu khác, PR vẫn Nháp, chưa có bước duyệt.
        var pr = await ReadAsync<PurchaseRequestDto>(await CreatePrAsync(f, 1));
        var prPreview = await ReadAsync<ApprovalPreviewDto>(await f.Buyer.PostAsJsonAsync("/api/approvals/preview", new ApprovalPreviewRequest("PR", pr.Id), Ct));
        Assert.Equal(prPreview.Steps.Count == 0, prPreview.ApprovedImmediately);
        var prAfter = await ReadAsync<PurchaseRequestDto>(await f.Buyer.GetAsync($"/api/purchase-requests/{pr.Id}", Ct));
        Assert.Equal(("Draft", 0), (prAfter.Status, prAfter.Approvals.Count));
    }
}
