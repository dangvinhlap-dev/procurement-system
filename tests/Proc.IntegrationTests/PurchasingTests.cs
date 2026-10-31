using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Proc.Application.Common;
using Proc.Application.Inventory;
using Proc.Application.MasterData;
using Proc.Application.Organization;
using Proc.Application.Purchasing;
using Proc.Domain.Organization;

namespace Proc.IntegrationTests;

/// <summary>
/// Mua hàng (design.md mục 6.2, 10.2, 10.3): PR → duyệt → PO từ PR → duyệt → phiếu nhập theo PO → PO nhận đủ; duyệt nhiều cấp theo
/// ngưỡng và vai trò; từ chối; dưới ngưỡng tự duyệt. Mỗi test một công ty mới để quy trình duyệt không ảnh hưởng test khác.
/// </summary>
[Collection(ApiCollection.Name)]
public class PurchasingTests
{
    private readonly ProcApiFactory _factory;

    public PurchasingTests(ProcApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string UniqueCode(string prefix) => $"{prefix}{Guid.NewGuid():N}".ToUpperInvariant()[..10];

    private static DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTime.UtcNow, VietnamTime.Zone));

    private static async Task<string?> TitleAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(Ct))?.Title;

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(Ct)}");
        return (await response.Content.ReadFromJsonAsync<T>(Ct))!;
    }

    private sealed record Fixture(HttpClient Buyer, string CompanyCode, Guid Kt, Guid ItemId, Guid Cai, Guid SupplierId);

    /// <summary>Công ty mới (kỳ tháng hiện tại đang mở), một mặt hàng (cái), một NCC đã duyệt áp dụng tại công ty. Người lập: quản trị.</summary>
    private async Task<Fixture> ArrangeAsync()
    {
        var userName = $"mh.{Guid.NewGuid():N}"[..16];
        await _factory.CreateUserAsync(userName, ProcApiFactory.TestPassword, ["TCT"]);
        var head = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, "TCT");
        var company = await ReadAsync<CompanyDetailDto>(await head.PostAsJsonAsync("/api/companies",
            new CreateCompanyRequest(UniqueCode("M"), "Công ty thử mua hàng", null, null, null, null, new DateOnly(Today.Year, Today.Month, 1), "KT", "Kho tổng"), Ct));
        var buyer = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, company.Code);
        var calendar = await ReadAsync<InventoryCalendarDto>(await buyer.PostAsJsonAsync("/api/inventory-periods/years", new CreateInventoryYearRequest(Today.Year), Ct));
        await ReadAsync<InventoryPeriodDto>(await buyer.PostAsync($"/api/inventory-periods/{calendar.Periods.Single(p => p.Month == Today.Month).Id}/open", null, Ct));
        var kt = (await buyer.GetFromJsonAsync<List<WarehouseDetailDto>>("/api/warehouses", Ct))!.Single().Id;

        var cai = (await head.GetFromJsonAsync<List<UomDto>>("/api/uoms", Ct))!.Single(u => u.Code == "CAI").Id;
        var category = await ReadAsync<ItemCategoryDetailDto>(await head.PostAsJsonAsync("/api/item-categories",
            new CreateItemCategoryRequest(null, "Nhóm mua hàng", null, null), Ct));
        var item = await ReadAsync<ItemDetailDto>(await head.PostAsJsonAsync("/api/items",
            new CreateItemRequest(null, "Dây curoa", null, "Inventory", category.Id, cai, null, null, null, null, "10", null, false, false, null, null, []), Ct));
        var groupId = (await head.GetFromJsonAsync<List<SupplierGroupDto>>("/api/supplier-groups", Ct))!.Single(g => g.Code == "TN").Id;
        var supplier = await ReadAsync<SupplierDetailDto>(await head.PostAsJsonAsync("/api/suppliers",
            new SaveSupplierRequest(null, groupId, "NCC mua hàng", null, false, null, null, null, null, null, null, null, null, "Approved", null, null, [], []), Ct));
        await ReadAsync<SupplierCompanyDto>(await buyer.PutAsJsonAsync($"/api/suppliers/{supplier.Id}/company", new SaveSupplierCompanyRequest(null, null, true), Ct));
        return new Fixture(buyer, company.Code, kt, item.Id, cai, supplier.Id);
    }

    /// <summary>Người duyệt mới với một vai trò mới có quyền duyệt PR, PO tại công ty của fixture.</summary>
    private async Task<(HttpClient Client, Guid RoleId)> ApproverAsync(Fixture f)
    {
        var roleCode = UniqueCode("D");
        var roleId = await _factory.CreateRoleAsync(roleCode,
            Permissions.Purchase.PrView, Permissions.Purchase.PrApprove, Permissions.Purchase.PoView, Permissions.Purchase.PoApprove);
        var userName = $"dy.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(userName, f.CompanyCode, roleCode);
        return (await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, f.CompanyCode), roleId);
    }

    private static Task<HttpResponseMessage> CreatePrAsync(Fixture f, decimal qty) =>
        f.Buyer.PostAsJsonAsync("/api/purchase-requests",
            new SavePurchaseRequestRequest(Today, "Xưởng cán", "Bảo trì máy", Today.AddDays(7), f.Kt, "Urgent", null, [new(null, f.ItemId, f.Cai, qty, f.SupplierId, null, null)]), Ct);

    private static Task<HttpResponseMessage> CreatePoAsync(Fixture f, decimal qty, Guid? prLineId, decimal tolerance = 0) =>
        f.Buyer.PostAsJsonAsync("/api/purchase-orders",
            new SavePurchaseOrderRequest(Today, f.SupplierId, f.Kt, Today.AddDays(5), "30 ngày", null, null, tolerance, null,
                [new(null, f.ItemId, f.Cai, qty, 100_000, 5, "10", null, null, prLineId)]), Ct);

    private static Task<HttpResponseMessage> ReceiveAsync(Fixture f, PurchaseOrderDto po, decimal qty) =>
        f.Buyer.PostAsJsonAsync("/api/goods-receipts",
            new SaveReceiptRequest(Today, f.Kt, f.SupplierId, null, null, null, null, null, null,
                [new(null, f.ItemId, f.Cai, qty, 1, null, null, null, null, po.Lines[0].Id)], po.Id), Ct);

    private static Task<HttpResponseMessage> PostDocAsync(Fixture f, Guid id) => f.Buyer.PostAsync($"/api/inventory-documents/{id}/post", null, Ct);

    private static Task<HttpResponseMessage> ActAsync(HttpClient client, string kind, Guid id, string action, string? comment = null) =>
        client.PostAsJsonAsync($"/api/{kind}/{id}/{action}", new ApprovalActionRequest(comment), Ct);

    [Fact]
    public async Task LuongPrPoNhapKho_DuyetMotCap_NhanDuThiDongPo()
    {
        var f = await ArrangeAsync();
        var (approver, _) = await ApproverAsync(f);

        var pr = await ReadAsync<PurchaseRequestDto>(await CreatePrAsync(f, 10));
        Assert.Matches($"^PR-{f.CompanyCode}-{Today:yyMM}-\\d{{4}}$", pr.No);
        pr = await ReadAsync<PurchaseRequestDto>(await f.Buyer.PostAsync($"/api/purchase-requests/{pr.Id}/submit", null, Ct));
        Assert.Equal(("Submitted", false), (pr.Status, pr.Actions.CanApprove));
        Assert.Equal("Người lập không tự duyệt phiếu của mình.", await TitleAsync(await ActAsync(f.Buyer, "purchase-requests", pr.Id, "approve")));
        var pending = await approver.GetFromJsonAsync<List<PendingApprovalDto>>("/api/approvals/pending", Ct);
        Assert.Contains(pending!, p => p.DocumentId == pr.Id && p.DocType == "PR");
        pr = await ReadAsync<PurchaseRequestDto>(await ActAsync(approver, "purchase-requests", pr.Id, "approve", "Đồng ý"));
        Assert.Equal("Approved", pr.Status);
        Assert.Equal(("Approved", "Đồng ý"), (pr.Approvals.Single().Status, pr.Approvals.Single().Comment));

        var open = await f.Buyer.GetFromJsonAsync<List<OpenRequestLineDto>>("/api/purchase-requests/open-lines", Ct);
        Assert.Equal(10m, open!.Single(l => l.RequestId == pr.Id).RemainingQty);

        var po = await ReadAsync<PurchaseOrderDto>(await CreatePoAsync(f, 10, pr.Lines[0].Id));
        Assert.Equal((950_000m, 95_000m, pr.No), (po.TotalAmount, po.TotalVat, po.Lines[0].SourcePrNo));
        Assert.Contains("vượt SL còn chưa đặt", await TitleAsync(await CreatePoAsync(f, 1, pr.Lines[0].Id)));
        Assert.Contains("đã lên đơn mua hàng", await TitleAsync(await f.Buyer.PostAsync($"/api/purchase-requests/{pr.Id}/cancel", null, Ct)));
        Assert.Contains("chưa duyệt", await TitleAsync(await ReceiveAsync(f, po, 1)));
        await ReadAsync<PurchaseOrderDto>(await f.Buyer.PostAsync($"/api/purchase-orders/{po.Id}/submit", null, Ct));
        po = await ReadAsync<PurchaseOrderDto>(await ActAsync(approver, "purchase-orders", po.Id, "approve"));
        Assert.Equal("Approved", po.Status);

        // Nhận lần 1: 6 cái, giá thực mua 95.000 (sau CK 5%), thuế suất theo PO.
        var grn1 = await ReadAsync<InventoryDocumentDto>(await ReceiveAsync(f, po, 6));
        Assert.Equal((95_000m, "10", po.No), (grn1.Lines[0].UnitPrice, grn1.Lines[0].VatCode, grn1.PurchaseOrderNo));
        await ReadAsync<InventoryDocumentDto>(await PostDocAsync(f, grn1.Id));
        po = await ReadAsync<PurchaseOrderDto>(await f.Buyer.GetAsync($"/api/purchase-orders/{po.Id}", Ct));
        Assert.Equal(("Approved", 6m, 4m), (po.Status, po.Lines[0].ReceivedQty, po.Lines[0].RemainingQty));
        var tracking = await f.Buyer.GetFromJsonAsync<List<DeliveryTrackingDto>>("/api/purchase-orders/tracking", Ct);
        Assert.Equal(4m, tracking!.Single(t => t.OrderId == po.Id).RemainingQty);
        Assert.Contains("đã có phiếu nhập", await TitleAsync(await f.Buyer.PostAsync($"/api/purchase-orders/{po.Id}/cancel", null, Ct)));

        // Nhận vượt (dung sai 0%) bị chặn; nhận đủ thì PO đóng; bỏ ghi sổ thì PO mở lại.
        Assert.Contains("vượt SL còn nhận được", await TitleAsync(await ReceiveAsync(f, po, 5)));
        var grn2 = await ReadAsync<InventoryDocumentDto>(await ReceiveAsync(f, po, 4));
        await ReadAsync<InventoryDocumentDto>(await PostDocAsync(f, grn2.Id));
        Assert.Equal("Closed", (await ReadAsync<PurchaseOrderDto>(await f.Buyer.GetAsync($"/api/purchase-orders/{po.Id}", Ct))).Status);
        await ReadAsync<InventoryDocumentDto>(await f.Buyer.PostAsync($"/api/inventory-documents/{grn2.Id}/unpost", null, Ct));
        Assert.Equal("Approved", (await ReadAsync<PurchaseOrderDto>(await f.Buyer.GetAsync($"/api/purchase-orders/{po.Id}", Ct))).Status);
    }

    [Fact]
    public async Task DuyetNhieuCap_TheoNguongVaVaiTro()
    {
        var f = await ArrangeAsync();
        var (head, headRole) = await ApproverAsync(f);
        var (director, directorRole) = await ApproverAsync(f);
        await ReadAsync<ApprovalRuleDto>(await f.Buyer.PostAsJsonAsync("/api/approval-rules", new SaveApprovalRuleRequest("PR", 1, 0, headRole), Ct));
        await ReadAsync<ApprovalRuleDto>(await f.Buyer.PostAsJsonAsync("/api/approval-rules", new SaveApprovalRuleRequest("PR", 2, 1_000_000, directorRole), Ct));
        Assert.Contains("tăng dần", await TitleAsync(await f.Buyer.PostAsJsonAsync("/api/approval-rules", new SaveApprovalRuleRequest("PR", 3, 500_000, directorRole), Ct)));

        // Giá tham khảo = giá nhập gần nhất: nhập thẳng 1 cái × 200.000 trước.
        var direct = await ReadAsync<InventoryDocumentDto>(await f.Buyer.PostAsJsonAsync("/api/goods-receipts",
            new SaveReceiptRequest(Today, f.Kt, f.SupplierId, null, null, null, null, null, null, [new(null, f.ItemId, f.Cai, 1, 200_000, "10", null, null, null)]), Ct));
        await ReadAsync<InventoryDocumentDto>(await PostDocAsync(f, direct.Id));

        var pr = await ReadAsync<PurchaseRequestDto>(await CreatePrAsync(f, 10));
        Assert.Equal((200_000m, 2_000_000m, 1m), (pr.Lines[0].EstPrice, pr.TotalAmount, pr.Lines[0].OnHandQty));
        await ReadAsync<PurchaseRequestDto>(await f.Buyer.PostAsync($"/api/purchase-requests/{pr.Id}/submit", null, Ct));
        // Thông báo chờ duyệt chỉ tới người duyệt cấp đang chờ (design.md mục 11.6); người lập không nhận.
        var pending = Assert.Single(await NotificationApi.MineAsync(head, Ct), n => n.TargetId == pr.Id);
        Assert.Equal(("ApprovalPending", "PR", (DateTimeOffset?)null), (pending.Type, pending.ScreenCode, pending.ReadAt));
        Assert.Contains(pr.No, pending.Message);
        Assert.DoesNotContain(await NotificationApi.MineAsync(director, Ct), n => n.TargetId == pr.Id);
        Assert.DoesNotContain(await NotificationApi.MineAsync(f.Buyer, Ct), n => n.TargetId == pr.Id);

        Assert.Contains("bạn không có vai trò này", await TitleAsync(await ActAsync(director, "purchase-requests", pr.Id, "approve")));
        Assert.DoesNotContain((await director.GetFromJsonAsync<List<PendingApprovalDto>>("/api/approvals/pending", Ct))!, p => p.DocumentId == pr.Id);
        pr = await ReadAsync<PurchaseRequestDto>(await ActAsync(head, "purchase-requests", pr.Id, "approve"));
        Assert.Equal("Submitted", pr.Status);
        Assert.Contains("bạn không có vai trò này", await TitleAsync(await ActAsync(head, "purchase-requests", pr.Id, "approve")));
        Assert.Single((await director.GetFromJsonAsync<List<PendingApprovalDto>>("/api/approvals/pending", Ct))!, p => p.DocumentId == pr.Id && p.Level == 2);
        Assert.Single(await NotificationApi.MineAsync(director, Ct), n => n.TargetId == pr.Id && n.Type == "ApprovalPending");
        pr = await ReadAsync<PurchaseRequestDto>(await ActAsync(director, "purchase-requests", pr.Id, "approve"));
        Assert.Equal(("Approved", 2), (pr.Status, pr.Approvals.Count(a => a.Status == "Approved")));
        // Duyệt xong cấp cuối: người lập nhận "đã duyệt".
        Assert.Equal("Approved", Assert.Single(await NotificationApi.MineAsync(f.Buyer, Ct), n => n.TargetId == pr.Id).Type);

        // PR nhỏ (1 cái = 200.000 < 1 triệu) chỉ qua cấp 1.
        var small = await ReadAsync<PurchaseRequestDto>(await CreatePrAsync(f, 1));
        small = await ReadAsync<PurchaseRequestDto>(await f.Buyer.PostAsync($"/api/purchase-requests/{small.Id}/submit", null, Ct));
        Assert.Single(small.Approvals);
    }

    [Fact]
    public async Task TuChoi_VeNhapCoLyDo_GuiLaiLuotMoi_DuoiNguongTuDuyet()
    {
        var f = await ArrangeAsync();
        var (approver, role) = await ApproverAsync(f);
        var pr = await ReadAsync<PurchaseRequestDto>(await CreatePrAsync(f, 3));
        await ReadAsync<PurchaseRequestDto>(await f.Buyer.PostAsync($"/api/purchase-requests/{pr.Id}/submit", null, Ct));

        Assert.Equal("Phải nhập lý do từ chối.", await TitleAsync(await ActAsync(approver, "purchase-requests", pr.Id, "reject", " ")));
        await ReadAsync<PurchaseRequestDto>(await ActAsync(approver, "purchase-requests", pr.Id, "reject", "Thiếu báo giá"));
        var rejected = Assert.Single(await NotificationApi.MineAsync(f.Buyer, Ct), n => n.TargetId == pr.Id);
        Assert.Equal("Rejected", rejected.Type);
        Assert.Contains("Thiếu báo giá", rejected.Message);
        // Người lập thấy phiếu về nháp, sửa được, kèm lý do từ chối.
        pr = await ReadAsync<PurchaseRequestDto>(await f.Buyer.GetAsync($"/api/purchase-requests/{pr.Id}", Ct));
        Assert.Equal(("Draft", "Thiếu báo giá", true), (pr.Status, pr.RejectReason, pr.Actions.CanEdit));
        pr = await ReadAsync<PurchaseRequestDto>(await f.Buyer.PostAsync($"/api/purchase-requests/{pr.Id}/submit", null, Ct));
        Assert.Equal([(1, "Rejected"), (2, "Pending")], pr.Approvals.Select(a => (a.Round, a.Status)));

        // PO dưới mọi ngưỡng đã cấu hình: gửi là duyệt luôn.
        await ReadAsync<ApprovalRuleDto>(await f.Buyer.PostAsJsonAsync("/api/approval-rules", new SaveApprovalRuleRequest("PO", 1, 10_000_000, role), Ct));
        var po = await ReadAsync<PurchaseOrderDto>(await CreatePoAsync(f, 2, null));
        po = await ReadAsync<PurchaseOrderDto>(await f.Buyer.PostAsync($"/api/purchase-orders/{po.Id}/submit", null, Ct));
        Assert.Equal(("Approved", 0), (po.Status, po.Approvals.Count));
    }

    [Fact]
    public async Task KhongCoQuyenDuyet_BiTuChoi()
    {
        var userName = $"nv.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(userName, "NMI", "NVMUA");
        var buyer = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, "NMI");

        Assert.Equal(HttpStatusCode.Forbidden, (await ActAsync(buyer, "purchase-requests", Guid.NewGuid(), "approve")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await ActAsync(buyer, "purchase-orders", Guid.NewGuid(), "approve")).StatusCode);
    }
}
