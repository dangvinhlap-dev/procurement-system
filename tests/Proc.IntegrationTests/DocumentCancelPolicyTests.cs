using System.Net;
using System.Net.Http.Json;
using Proc.Application.Administration;
using Proc.Application.Purchasing;
using Proc.Domain.Organization;

namespace Proc.IntegrationTests;

/// <summary>
/// Lát Z4a (design.md mục 20.2a): Hủy phiếu là quyền đặc biệt chỉ của quản trị; phiếu Nháp thì người lập tự hủy; mọi lần hủy bắt buộc lý do.
/// Người lập rút phiếu đang chờ duyệt về Nháp (yêu cầu người dùng 11/11/2026) để sửa hoặc tự hủy.
/// </summary>
public partial class PurchasingTests
{
    /// <summary>Người mua thường (không phải quản trị) lập được PR, PO tại công ty thử.</summary>
    private async Task<(HttpClient Client, Guid RoleId)> PlainBuyerAsync(Fixture f)
    {
        var roleCode = UniqueCode("B");
        var roleId = await _factory.CreateRoleAsync(roleCode,
            Permissions.Purchase.PrView, Permissions.Purchase.PrCreate, Permissions.Purchase.PoView, Permissions.Purchase.PoCreate,
            Permissions.MasterData.ItemView, Permissions.MasterData.SupplierView);
        var userName = $"nm.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(userName, f.CompanyCode, roleCode);
        return (await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, f.CompanyCode), roleId);
    }

    private static Task<HttpResponseMessage> CancelAsync(HttpClient client, string kind, Guid id, string? reason) =>
        client.PostAsJsonAsync($"/api/{kind}/{id}/cancel", new CancelDocumentRequest(reason), Ct);

    [Fact]
    public async Task HuyPhieu_NhapNguoiLapTuHuy_DaGuiChiQuanTri_BatBuocLyDo_RutVeNhap()
    {
        var f = await ArrangeAsync();
        var (buyer, _) = await PlainBuyerAsync(f);
        var (colleague, _) = await PlainBuyerAsync(f);
        var mine = f with { Buyer = buyer };
        // Một cấp duyệt PO do người duyệt riêng – phiếu gửi đi nằm ở Chờ duyệt.
        var (approver, approverRole) = await ApproverAsync(f);
        await ReadAsync<ApprovalRuleDto>(await f.Buyer.PostAsJsonAsync("/api/approval-rules", new SaveApprovalRuleRequest("PO", 1, 0, approverRole), Ct));

        // PR nháp của người mua thường: đồng nghiệp cùng quyền không hủy được; người lập hủy phải có lý do.
        var pr = await ReadAsync<PurchaseRequestDto>(await CreatePrAsync(mine, 2));
        Assert.True(pr.Actions.CanCancel);
        Assert.False((await ReadAsync<PurchaseRequestDto>(await colleague.GetAsync($"/api/purchase-requests/{pr.Id}", Ct))).Actions.CanCancel);
        Assert.Equal(HttpStatusCode.Forbidden, (await CancelAsync(colleague, "purchase-requests", pr.Id, "Không cần")).StatusCode);
        Assert.Contains("lý do hủy", await TitleAsync(await CancelAsync(buyer, "purchase-requests", pr.Id, "  ")));
        pr = await ReadAsync<PurchaseRequestDto>(await CancelAsync(buyer, "purchase-requests", pr.Id, "Lập nhầm"));
        Assert.Equal("Cancelled", pr.Status);

        // PO đã gửi duyệt: người lập không hủy được nữa (chỉ quản trị) nhưng rút về nháp được, rồi tự hủy.
        var po = await ReadAsync<PurchaseOrderDto>(await CreatePoAsync(mine, 3, null));
        po = await ReadAsync<PurchaseOrderDto>(await buyer.PostAsync($"/api/purchase-orders/{po.Id}/submit", null, Ct));
        Assert.Equal("Submitted", po.Status);
        Assert.False(po.Actions.CanCancel);
        Assert.True(po.Actions.CanRecall);
        Assert.Equal(HttpStatusCode.Forbidden, (await CancelAsync(buyer, "purchase-orders", po.Id, "Đổi NCC")).StatusCode);
        // Đồng nghiệp không rút phiếu của người khác.
        Assert.Equal(HttpStatusCode.Forbidden, (await colleague.PostAsync($"/api/purchase-orders/{po.Id}/recall", null, Ct)).StatusCode);
        po = await ReadAsync<PurchaseOrderDto>(await buyer.PostAsync($"/api/purchase-orders/{po.Id}/recall", null, Ct));
        Assert.Equal("Draft", po.Status);
        Assert.All(po.Approvals, s => Assert.NotEqual("Pending", s.Status));
        po = await ReadAsync<PurchaseOrderDto>(await CancelAsync(buyer, "purchase-orders", po.Id, "Đổi NCC"));
        Assert.Equal("Cancelled", po.Status);

        // PO đã duyệt của người mua thường: người lập không hủy; quản trị (quyền Hủy phiếu) hủy được, bắt buộc lý do.
        var approved = await ReadAsync<PurchaseOrderDto>(await CreatePoAsync(mine, 4, null));
        approved = await ReadAsync<PurchaseOrderDto>(await buyer.PostAsync($"/api/purchase-orders/{approved.Id}/submit", null, Ct));
        while (approved.Status == "Submitted")
            approved = await ReadAsync<PurchaseOrderDto>(await ActAsync(approver, "purchase-orders", approved.Id, "approve"));
        Assert.Equal("Approved", approved.Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await CancelAsync(buyer, "purchase-orders", approved.Id, "Thôi")).StatusCode);
        Assert.Contains("lý do hủy", await TitleAsync(await CancelAsync(f.Buyer, "purchase-orders", approved.Id, null)));
        approved = await ReadAsync<PurchaseOrderDto>(await CancelAsync(f.Buyer, "purchase-orders", approved.Id, "NCC ngừng cung cấp"));
        Assert.Equal("Cancelled", approved.Status);
    }

    [Fact]
    public async Task QuyenHuyPhieu_KhongGanDuocChoVaiTroKhac()
    {
        var f = await ArrangeAsync();
        var response = await f.Buyer.PostAsJsonAsync("/api/roles",
            new CreateRoleRequest(UniqueCode("H"), "Thử quyền hủy", [Permissions.Purchase.PoView, Permissions.System.DocumentCancel]), Ct);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Quản trị hệ thống", await TitleAsync(response));
        // Danh mục quyền trên màn hình phân quyền không có quyền Hủy phiếu.
        var catalog = await f.Buyer.GetFromJsonAsync<List<PermissionGroupDto>>("/api/permissions", Ct);
        Assert.DoesNotContain(catalog!.SelectMany(g => g.Permissions), p => p.Code == Permissions.System.DocumentCancel);
    }
}
