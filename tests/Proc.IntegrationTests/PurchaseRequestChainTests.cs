using System.Net.Http.Json;
using Proc.Application.Notifications;
using Proc.Application.Organization;
using Proc.Application.Purchasing;
using Proc.Domain.Organization;

namespace Proc.IntegrationTests;

/// <summary>
/// Lát Y3c (design.md mục 19.3 – Q5, Q6): chuỗi duyệt đề nghị mua theo loại mua – bộ cấp riêng PRA (loại A), PRS (dịch vụ), chưa cấu hình thì
/// dùng bộ PR; cấp "Thủ kho nơi nhận" do người có quyền nhập tồn + phạm vi kho nơi nhận duyệt, phải nhập tồn trước; dịch vụ bỏ bước Thủ kho;
/// đề nghị loại A vượt dự toán công trình thêm chặng PRE.
/// </summary>
public partial class PurchasingTests
{
    /// <summary>Thủ kho mới (quyền xem PR + nhập tồn) có phạm vi kho <paramref name="warehouse"/> tại công ty fixture.</summary>
    private async Task<HttpClient> StorekeeperAsync(Fixture f, string roleCode, string warehouse)
    {
        var name = $"tk.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(name, f.CompanyCode, roleCode, [warehouse]);
        return await _factory.CreateClientInCompanyAsync(name, ProcApiFactory.TestPassword, f.CompanyCode);
    }

    private static Task<HttpResponseMessage> RuleAsync(Fixture f, string docType, int level, Guid? roleId, string? kind = null) =>
        f.Buyer.PostAsJsonAsync("/api/approval-rules", new SaveApprovalRuleRequest(docType, level, 0, roleId, kind), Ct);

    [Fact]
    public async Task ChuoiDuyetLoaiMua_CapThuKhoNoiNhan_PhaiNhapTonTruocKhiDuyet_DichVuBoThuKho()
    {
        var f = await ArrangeAsync();
        var (head, headRole) = await ApproverAsync(f);
        var (director, directorRole) = await ApproverAsync(f);

        // Cấp Thủ kho chỉ cấu hình cho PR, PRA (loại có nơi nhận); PRS (dịch vụ), PO không có.
        Assert.Contains("Thủ kho", await TitleAsync(await RuleAsync(f, "PRS", 1, null, "Storekeeper")));
        Assert.Contains("Thủ kho", await TitleAsync(await RuleAsync(f, "PO", 1, null, "Storekeeper")));
        var keeperRule = await ReadAsync<ApprovalRuleDto>(await RuleAsync(f, "PR", 1, null, "Storekeeper"));
        Assert.Equal(("Storekeeper", "Thủ kho nơi nhận"), (keeperRule.ApproverKind, keeperRule.RoleName));
        await ReadAsync<ApprovalRuleDto>(await RuleAsync(f, "PR", 2, headRole));

        await ReadAsync<WarehouseDetailDto>(await f.Buyer.PostAsJsonAsync("/api/warehouses", new CreateWarehouseRequest("KP", "Kho phụ", f.Kt, null, null, false), Ct));
        var keeperRole = UniqueCode("K");
        await _factory.CreateRoleAsync(keeperRole, Permissions.Purchase.PrView, Permissions.Purchase.PrEnterStock);

        // Chưa có thủ kho nào của nơi nhận KT: chặn gửi, phiếu giữ nháp.
        var pr = await ReadAsync<PurchaseRequestDto>(await CreatePrAsync(f, 10));
        Assert.Contains("Thủ kho", await TitleAsync(await f.Buyer.PostAsync($"/api/purchase-requests/{pr.Id}/submit", null, Ct)));
        var keeper = await StorekeeperAsync(f, keeperRole, "KT");
        var other = await StorekeeperAsync(f, keeperRole, "KP");

        // Phiếu B có cấp Thủ kho mà chưa chọn nơi nhận: chặn gửi.
        var noWarehouse = await ReadAsync<PurchaseRequestDto>(await f.Buyer.PostAsJsonAsync("/api/purchase-requests",
            new SavePurchaseRequestRequest(Today, f.DepartmentId, "Không nơi nhận", null, null, "Normal", null, [new(null, f.ItemId, f.Cai, 1, null, null, null)]), Ct));
        Assert.Contains("nơi nhận", await TitleAsync(await f.Buyer.PostAsync($"/api/purchase-requests/{noWarehouse.Id}/submit", null, Ct)));

        pr = await ReadAsync<PurchaseRequestDto>(await f.Buyer.PostAsync($"/api/purchase-requests/{pr.Id}/submit", null, Ct));
        var keeperStep = pr.Approvals.Single(s => s.Level == 1);
        Assert.Equal("Thủ kho KT", keeperStep.RoleName);
        Assert.Single(keeperStep.NextApprovers!);

        // Thủ kho nơi nhận có thông báo "chờ nhập tồn" và thấy phiếu trong việc chờ duyệt; thủ kho kho khác, người duyệt cấp 2 thì không.
        Assert.Contains((await keeper.GetFromJsonAsync<List<NotificationDto>>("/api/notifications", Ct))!,
            n => n.TargetId == pr.Id && n.Title.Contains("nhập tồn"));
        Assert.Contains((await keeper.GetFromJsonAsync<List<PendingApprovalDto>>("/api/approvals/pending", Ct))!, p => p.DocumentId == pr.Id);
        Assert.DoesNotContain((await other.GetFromJsonAsync<List<PendingApprovalDto>>("/api/approvals/pending", Ct))!, p => p.DocumentId == pr.Id);
        Assert.Contains("Thủ kho", await TitleAsync(await ActAsync(head, "purchase-requests", pr.Id, "approve")));
        Assert.Contains("Thủ kho", await TitleAsync(await ActAsync(other, "purchase-requests", pr.Id, "approve")));

        // Chưa nhập tồn thì chưa duyệt được; nhập tồn 4 rồi duyệt → tồn khóa, đến cấp 2.
        Assert.False((await keeper.GetFromJsonAsync<PurchaseRequestDto>($"/api/purchase-requests/{pr.Id}", Ct))!.Actions.CanApprove);
        Assert.Contains("nhập tồn", await TitleAsync(await ActAsync(keeper, "purchase-requests", pr.Id, "approve")));
        pr = await ReadAsync<PurchaseRequestDto>(await EnterStockAsync(keeper, pr, 4));
        Assert.True(pr.Actions.CanApprove);
        pr = await ReadAsync<PurchaseRequestDto>(await ActAsync(keeper, "purchase-requests", pr.Id, "approve"));
        Assert.Equal(6m, pr.Lines[0].Qty);
        Assert.False(pr.CanEnterStock);
        Assert.Contains("tồn đã khóa", await TitleAsync(await EnterStockAsync(keeper, pr, 0)));
        pr = await ReadAsync<PurchaseRequestDto>(await ActAsync(head, "purchase-requests", pr.Id, "approve"));
        Assert.Equal("Approved", pr.Status);

        // Dịch vụ chưa có bộ PRS: dùng bộ PR nhưng tự bỏ cấp Thủ kho – không cần nơi nhận.
        var service = await ReadAsync<PurchaseRequestDto>(await f.Buyer.PostAsJsonAsync("/api/purchase-requests",
            new SavePurchaseRequestRequest(Today, f.DepartmentId, "Sửa máy", null, null, "Normal", null, [new(null, f.ItemId, f.Cai, 1, null, null, null)], null, "Service"), Ct));
        service = await ReadAsync<PurchaseRequestDto>(await f.Buyer.PostAsync($"/api/purchase-requests/{service.Id}/submit", null, Ct));
        Assert.Equal([2], service.Approvals.Select(s => s.Level));
        // Có bộ PRS thì dịch vụ đi theo bộ PRS.
        await ReadAsync<ApprovalRuleDto>(await RuleAsync(f, "PRS", 1, directorRole));
        var service2 = await ReadAsync<PurchaseRequestDto>(await f.Buyer.PostAsJsonAsync("/api/purchase-requests",
            new SavePurchaseRequestRequest(Today, f.DepartmentId, "Thuê xe", null, null, "Normal", null, [new(null, f.ItemId, f.Cai, 1, null, null, null)], null, "Service"), Ct));
        service2 = await ReadAsync<PurchaseRequestDto>(await f.Buyer.PostAsync($"/api/purchase-requests/{service2.Id}/submit", null, Ct));
        Assert.Contains("vai trò", await TitleAsync(await ActAsync(head, "purchase-requests", service2.Id, "approve")));
        service2 = await ReadAsync<PurchaseRequestDto>(await ActAsync(director, "purchase-requests", service2.Id, "approve"));
        Assert.Equal("Approved", service2.Status);
    }

    [Fact]
    public async Task DeNghiLoaiA_BoCapPra_VuotDuToanThemChangPre()
    {
        var f = await ArrangeAsync();
        var (head, headRole) = await ApproverAsync(f);
        var (chief, chiefRole) = await ApproverAsync(f);
        await ReadAsync<ApprovalRuleDto>(await RuleAsync(f, "PR", 1, chiefRole));
        await ReadAsync<ApprovalRuleDto>(await RuleAsync(f, "PRA", 1, headRole));

        // Trong dự toán: chỉ bộ PRA (không qua bộ PR), không có chặng vượt dự toán.
        var big = await ProjectAsync(f, 1_000_000_000m);
        var within = await ReadAsync<PurchaseRequestDto>(await CreatePrOfAsync(f, "Construction", big.Id, 1));
        within = await ReadAsync<PurchaseRequestDto>(await f.Buyer.PostAsync($"/api/purchase-requests/{within.Id}/submit", null, Ct));
        Assert.Equal([(1, 1)], within.Approvals.Select(s => (s.Stage, s.Level)));
        within = await ReadAsync<PurchaseRequestDto>(await ActAsync(head, "purchase-requests", within.Id, "approve"));
        Assert.Equal("Approved", within.Status);

        // Công trình dự toán 1 đ đã có PO đã duyệt 95.000 (lũy kế > dự toán): đề nghị sau vượt dự toán.
        var small = await ProjectAsync(f, 1m);
        var first = await ReadAsync<PurchaseRequestDto>(await CreatePrOfAsync(f, "Construction", small.Id, 1));
        first = await ReadAsync<PurchaseRequestDto>(await f.Buyer.PostAsync($"/api/purchase-requests/{first.Id}/submit", null, Ct));
        Assert.Equal([(1, 1)], first.Approvals.Select(s => (s.Stage, s.Level)));
        await ReadAsync<PurchaseRequestDto>(await ActAsync(head, "purchase-requests", first.Id, "approve"));
        await _factory.ApproveSummaryForRequestAsync(first.Id);
        var po = await ReadAsync<PurchaseOrderDto>(await CreatePoAsync(f, 1, first.Lines[0].Id));
        await ReadAsync<PurchaseOrderDto>(await f.Buyer.PostAsync($"/api/purchase-orders/{po.Id}/submit", null, Ct));
        await ReadAsync<PurchaseOrderDto>(await ActAsync(head, "purchase-orders", po.Id, "approve"));

        // Vượt dự toán: thêm chặng 6 PRE; chưa cấu hình PRE thì một cấp – ai có quyền duyệt PR. Phần vượt được xem trước khi trình lên cấp
        // cao nhất (người dùng yêu cầu 11/11/2026): cấp cao nhất của bộ PRA dời sang chặng 9, duyệt sau cùng.
        var over = await ReadAsync<PurchaseRequestDto>(await CreatePrOfAsync(f, "Construction", small.Id, 1));
        Assert.True(over.Construction!.OverEstimate);
        over = await ReadAsync<PurchaseRequestDto>(await f.Buyer.PostAsync($"/api/purchase-requests/{over.Id}/submit", null, Ct));
        Assert.Equal([(6, 1), (9, 1)], over.Approvals.Select(s => (s.Stage, s.Level)));
        over = await ReadAsync<PurchaseRequestDto>(await ActAsync(chief, "purchase-requests", over.Id, "approve"));
        Assert.Equal("Submitted", over.Status);
        over = await ReadAsync<PurchaseRequestDto>(await ActAsync(head, "purchase-requests", over.Id, "approve"));
        Assert.Equal("Approved", over.Status);

        // Có bộ PRE: chặng vượt dự toán đi theo vai trò của bộ đó – cấp cao nhất của chuỗi thường (head) chưa duyệt được.
        await ReadAsync<ApprovalRuleDto>(await RuleAsync(f, "PRE", 1, chiefRole));
        var over2 = await ReadAsync<PurchaseRequestDto>(await CreatePrOfAsync(f, "Construction", small.Id, 1));
        over2 = await ReadAsync<PurchaseRequestDto>(await f.Buyer.PostAsync($"/api/purchase-requests/{over2.Id}/submit", null, Ct));
        Assert.Contains(over2.Approvals, s => s.Stage == 6 && s.Status == "Pending" && s.RoleName is not null);
        Assert.Contains("vai trò", await TitleAsync(await ActAsync(head, "purchase-requests", over2.Id, "approve")));
        Assert.True((await chief.GetFromJsonAsync<PurchaseRequestDto>($"/api/purchase-requests/{over2.Id}", Ct))!.Actions.CanApprove);
    }
}
