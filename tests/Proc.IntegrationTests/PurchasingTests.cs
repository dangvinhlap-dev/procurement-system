using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Proc.Application.Common;
using Proc.Application.Documents;
using Proc.Application.Inventory;
using Proc.Application.Logging;
using Proc.Application.MasterData;
using Proc.Application.Organization;
using Proc.Application.Pricing;
using Proc.Application.Purchasing;
using Proc.Application.Reports;
using Proc.Domain.Organization;

namespace Proc.IntegrationTests;

/// <summary>
/// Mua hàng (design.md mục 6.2, 10.2, 10.3): PR → duyệt → PO từ PR → duyệt → phiếu nhập theo PO → PO nhận đủ; duyệt nhiều cấp theo
/// ngưỡng và vai trò; từ chối; dưới ngưỡng tự duyệt. Mỗi test một công ty mới để quy trình duyệt không ảnh hưởng test khác.
/// </summary>
[Collection(ApiCollection.Name)]
public partial class PurchasingTests
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

    private sealed record Fixture(HttpClient Buyer, string CompanyCode, Guid Kt, Guid ItemId, Guid Cai, Guid SupplierId, Guid DepartmentId);

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
        await _factory.FinishSupplierApprovalAsync(supplier.Id);
        await ReadAsync<SupplierCompanyDto>(await buyer.PutAsJsonAsync($"/api/suppliers/{supplier.Id}/company", new SaveSupplierCompanyRequest(null, null, true), Ct));
        // Bảng giá đã duyệt 100.000/cái, CK 5% (đúng giá CreatePoAsync): PO thử có giá đã duyệt nên chỉ qua cấp duyệt PO thường – PO không có
        // giá đã duyệt phải qua thêm chặng POL (design.md mục 18.25), có test riêng.
        var list = await ReadAsync<PriceListDto>(await buyer.PostAsJsonAsync("/api/price-lists",
            new SavePriceListRequest(supplier.Id, "Bảng giá mua hàng", false, Today.AddDays(-Today.Day + 1), null, null, null,
                [new(null, item.Id, cai, 0, 100_000, 5, "10", null)]), Ct));
        await ReadAsync<PriceListDto>(await buyer.PostAsync($"/api/price-lists/{list.Id}/submit", null, Ct));
        await _factory.FinishPriceListApprovalAsync(list.Id);
        return new Fixture(buyer, company.Code, kt, item.Id, cai, supplier.Id, await TestDepartments.CreateAsync(buyer, "Xưởng cán"));
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
            new SavePurchaseRequestRequest(Today, f.DepartmentId, "Bảo trì máy", Today.AddDays(7), f.Kt, "Urgent", null, [new(null, f.ItemId, f.Cai, qty, f.SupplierId, null, null)]), Ct);

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

    /// <summary>
    /// Lát R2 (design.md mục 18.26): sửa PO kèm phiên bản đã xem (If-Match) – đúng thì lưu, phiên bản đổi; gửi lại phiên bản cũ (người khác
    /// đã sửa) thì 409, không ghi đè. Hai người duyệt cùng một cấp cùng lúc: chỉ một lần duyệt được ghi.
    /// </summary>
    [Fact]
    public async Task SuaTrenBanCu_Bao409_HaiNguoiDuyetCungLuc_ChiMotNguoi()
    {
        var f = await ArrangeAsync();
        var po = await ReadAsync<PurchaseOrderDto>(await CreatePoAsync(f, 10, null));
        Assert.True(po.Version > 0);

        HttpRequestMessage Update(uint version, decimal qty)
        {
            var request = new HttpRequestMessage(HttpMethod.Put, $"/api/purchase-orders/{po.Id}")
            {
                Content = JsonContent.Create(new SavePurchaseOrderRequest(Today, f.SupplierId, f.Kt, Today.AddDays(5), "30 ngày", null, null, 0, null,
                    [new(po.Lines[0].Id, f.ItemId, f.Cai, qty, 100_000, 5, "10", null, null, null)])),
            };
            request.Headers.IfMatch.Add(new System.Net.Http.Headers.EntityTagHeaderValue($"\"{version}\""));
            return request;
        }

        var saved = await ReadAsync<PurchaseOrderDto>(await f.Buyer.SendAsync(Update(po.Version, 12), Ct));
        Assert.Equal(12m, saved.Lines[0].Qty);
        Assert.NotEqual(po.Version, saved.Version);
        var stale = await f.Buyer.SendAsync(Update(po.Version, 15), Ct);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Contains("người khác thay đổi", await TitleAsync(stale));
        Assert.Equal(12m, (await ReadAsync<PurchaseOrderDto>(await f.Buyer.GetAsync($"/api/purchase-orders/{po.Id}", Ct))).Lines[0].Qty);

        await ReadAsync<PurchaseOrderDto>(await f.Buyer.PostAsync($"/api/purchase-orders/{po.Id}/submit", null, Ct));
        var (first, _) = await ApproverAsync(f);
        var (second, _) = await ApproverAsync(f);
        var approvals = await Task.WhenAll(ActAsync(first, "purchase-orders", po.Id, "approve"), ActAsync(second, "purchase-orders", po.Id, "approve"));

        Assert.Equal(1, approvals.Count(a => a.IsSuccessStatusCode));
        var history = (await ReadAsync<PurchaseOrderDto>(await f.Buyer.GetAsync($"/api/purchase-orders/{po.Id}", Ct))).Approvals;
        Assert.Single(history, s => s.Status == "Approved");
    }

    /// <summary>
    /// Lát R3 (design.md mục 18.26): PO hai cấp cùng một vai trò – người đã duyệt cấp 1 không duyệt tiếp cấp 2 (không có trong "người duyệt
    /// tiếp theo"), người khác duyệt cấp 2; bị từ chối rồi gửi lại là lượt mới, người đó lại duyệt được cấp 1.
    /// </summary>
    [Fact]
    public async Task DuyetNhieuCap_MoiCapMotNguoiKhacNhau_TrongMotLuot()
    {
        var f = await ArrangeAsync();
        var (first, roleId) = await ApproverAsync(f);
        var roleCode = (await f.Buyer.GetFromJsonAsync<List<Proc.Application.Administration.RoleListItemDto>>("/api/roles", Ct))!.Single(r => r.Id == roleId).Code;
        var secondName = $"dy.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(secondName, f.CompanyCode, roleCode);
        var second = await _factory.CreateClientInCompanyAsync(secondName, ProcApiFactory.TestPassword, f.CompanyCode);
        await ReadAsync<ApprovalRuleDto>(await f.Buyer.PostAsJsonAsync("/api/approval-rules", new SaveApprovalRuleRequest("PO", 1, 0, roleId), Ct));
        await ReadAsync<ApprovalRuleDto>(await f.Buyer.PostAsJsonAsync("/api/approval-rules", new SaveApprovalRuleRequest("PO", 2, 0, roleId), Ct));

        var po = await ReadAsync<PurchaseOrderDto>(await CreatePoAsync(f, 10, null));
        await ReadAsync<PurchaseOrderDto>(await f.Buyer.PostAsync($"/api/purchase-orders/{po.Id}/submit", null, Ct));
        po = await ReadAsync<PurchaseOrderDto>(await ActAsync(first, "purchase-orders", po.Id, "approve"));
        Assert.Equal("Submitted", po.Status);
        Assert.Contains("đã duyệt cấp 1", await TitleAsync(await ActAsync(first, "purchase-orders", po.Id, "approve")));
        Assert.Contains("đã duyệt cấp 1", await TitleAsync(await ActAsync(first, "purchase-orders", po.Id, "reject", "Thử từ chối")));
        var waiting = Assert.Single(po.Approvals, s => s.Status == "Pending");
        Assert.DoesNotContain(waiting.NextApprovers!, n => po.Approvals.Any(s => s.Status == "Approved" && s.ActedByName == n));

        // Người khác từ chối cấp 2 → về nháp; gửi lại là lượt mới: người cũ duyệt lại được cấp 1, người khác duyệt cấp 2.
        await ReadAsync<PurchaseOrderDto>(await ActAsync(second, "purchase-orders", po.Id, "reject", "Xem lại giá"));
        await ReadAsync<PurchaseOrderDto>(await f.Buyer.PostAsync($"/api/purchase-orders/{po.Id}/submit", null, Ct));
        await ReadAsync<PurchaseOrderDto>(await ActAsync(first, "purchase-orders", po.Id, "approve"));
        po = await ReadAsync<PurchaseOrderDto>(await ActAsync(second, "purchase-orders", po.Id, "approve"));
        Assert.Equal("Approved", po.Status);
    }

    /// <summary>
    /// Lát R4 (design.md mục 18.26): bật "Nhập mua bắt buộc theo đơn mua hàng" thì không lập, không ghi sổ phiếu nhập thẳng (kể cả phiếu nháp
    /// lập trước khi bật); phiếu nhập theo PO đã duyệt vẫn bình thường. Tắt lại thì nhập thẳng được như cũ.
    /// </summary>
    [Fact]
    public async Task NhapMuaBatBuocTheoPo_ChanPhieuNhapThang()
    {
        var f = await ArrangeAsync();
        var (approver, _) = await ApproverAsync(f);
        SaveReceiptRequest Direct() => new(Today, f.Kt, f.SupplierId, null, null, null, null, null, null,
            [new(null, f.ItemId, f.Cai, 1, 50_000, "10", null, null, null)]);
        var oldDraft = await ReadAsync<InventoryDocumentDto>(await f.Buyer.PostAsJsonAsync("/api/goods-receipts", Direct(), Ct));

        var on = await ReadAsync<SystemParameterDto>(await f.Buyer.PutAsJsonAsync("/api/system-parameters/RECEIPT_PO_REQUIRED", new UpdateSystemParameterRequest("Bật"), Ct));
        Assert.Equal(("Bật", false), (on.Value, on.IsDefault));
        Assert.Contains("bắt buộc nhập mua theo đơn mua hàng", await TitleAsync(await f.Buyer.PostAsJsonAsync("/api/goods-receipts", Direct(), Ct)));
        Assert.Contains("bắt buộc nhập mua theo đơn mua hàng", await TitleAsync(await PostDocAsync(f, oldDraft.Id)));

        var po = await ReadAsync<PurchaseOrderDto>(await CreatePoAsync(f, 5, null));
        await ReadAsync<PurchaseOrderDto>(await f.Buyer.PostAsync($"/api/purchase-orders/{po.Id}/submit", null, Ct));
        po = await ReadAsync<PurchaseOrderDto>(await ActAsync(approver, "purchase-orders", po.Id, "approve"));
        var grn = await ReadAsync<InventoryDocumentDto>(await ReceiveAsync(f, po, 5));
        Assert.Equal("Posted", (await ReadAsync<InventoryDocumentDto>(await PostDocAsync(f, grn.Id))).Status);

        await ReadAsync<SystemParameterDto>(await f.Buyer.PutAsJsonAsync("/api/system-parameters/RECEIPT_PO_REQUIRED", new UpdateSystemParameterRequest(null), Ct));
        Assert.Equal("Posted", (await ReadAsync<InventoryDocumentDto>(await PostDocAsync(f, oldDraft.Id))).Status);
    }

    /// <summary>
    /// Lát R5 (design.md mục 18.26): trả NCC hàng của phiếu nhập theo PO trừ vào SL đã nhận – PO đã nhận đủ mở lại để NCC giao bù; giao bù
    /// đủ thì PO đóng lại. Bỏ ghi sổ phiếu trả thì SL đã nhận cộng lại.
    /// </summary>
    [Fact]
    public async Task TraNcc_TruSlDaNhanCuaPo_MoLaiDeGiaoBu()
    {
        var f = await ArrangeAsync();
        var (approver, _) = await ApproverAsync(f);
        var po = await ReadAsync<PurchaseOrderDto>(await CreatePoAsync(f, 10, null));
        await ReadAsync<PurchaseOrderDto>(await f.Buyer.PostAsync($"/api/purchase-orders/{po.Id}/submit", null, Ct));
        po = await ReadAsync<PurchaseOrderDto>(await ActAsync(approver, "purchase-orders", po.Id, "approve"));
        var grn = await ReadAsync<InventoryDocumentDto>(await ReceiveAsync(f, po, 10));
        grn = await ReadAsync<InventoryDocumentDto>(await PostDocAsync(f, grn.Id));
        Assert.Equal("Closed", (await ReadAsync<PurchaseOrderDto>(await f.Buyer.GetAsync($"/api/purchase-orders/{po.Id}", Ct))).Status);

        var rtv = await ReadAsync<InventoryDocumentDto>(await f.Buyer.PostAsJsonAsync("/api/goods-returns",
            new SaveReturnRequest(Today, grn.Id, null, "Hàng lỗi", [new(null, grn.Lines[0].Id, 3, null)]), Ct));
        await ReadAsync<InventoryDocumentDto>(await PostDocAsync(f, rtv.Id));
        po = await ReadAsync<PurchaseOrderDto>(await f.Buyer.GetAsync($"/api/purchase-orders/{po.Id}", Ct));
        Assert.Equal(("Approved", 7m, 3m), (po.Status, po.Lines[0].ReceivedQty, po.Lines[0].RemainingQty));

        // Giao bù: nhận tối đa 3 (không dung sai).
        Assert.Contains("vượt SL còn nhận được", await TitleAsync(await ReceiveAsync(f, po, 4)));
        var replacement = await ReadAsync<InventoryDocumentDto>(await ReceiveAsync(f, po, 3));
        await ReadAsync<InventoryDocumentDto>(await PostDocAsync(f, replacement.Id));
        Assert.Equal("Closed", (await ReadAsync<PurchaseOrderDto>(await f.Buyer.GetAsync($"/api/purchase-orders/{po.Id}", Ct))).Status);

        // Bỏ ghi sổ phiếu trả: SL đã nhận thành 13 (vẫn đủ) – PO giữ Đã nhận đủ.
        await ReadAsync<InventoryDocumentDto>(await f.Buyer.PostAsync($"/api/inventory-documents/{rtv.Id}/unpost", null, Ct));
        po = await ReadAsync<PurchaseOrderDto>(await f.Buyer.GetAsync($"/api/purchase-orders/{po.Id}", Ct));
        Assert.Equal(("Closed", 13m), (po.Status, po.Lines[0].ReceivedQty));
    }

    [Fact]
    public async Task NhanHang_KiemTraChatLuong_SaiLech_TachNguoiLapPo()
    {
        var f = await ArrangeAsync();
        var (approver, _) = await ApproverAsync(f);
        var po = await ReadAsync<PurchaseOrderDto>(await CreatePoAsync(f, 10, null));
        await ReadAsync<PurchaseOrderDto>(await f.Buyer.PostAsync($"/api/purchase-orders/{po.Id}/submit", null, Ct));
        po = await ReadAsync<PurchaseOrderDto>(await ActAsync(approver, "purchase-orders", po.Id, "approve"));

        // Bật hai tham số nhận hàng (mặc định tắt).
        await ReadAsync<SystemParameterDto>(await f.Buyer.PutAsJsonAsync("/api/system-parameters/RECEIPT_QC_REQUIRED", new UpdateSystemParameterRequest("Bật"), Ct));
        var g4 = await ReadAsync<SystemParameterDto>(await f.Buyer.PutAsJsonAsync("/api/system-parameters/RECEIPT_SEPARATE_PO_CREATOR",
            new UpdateSystemParameterRequest("true"), Ct));
        Assert.Equal(("Bật", false), (g4.Value, g4.IsDefault));

        SaveReceiptRequest Receipt(decimal qty, decimal? delivered, decimal rejected, string? qc, string? note) =>
            new(Today, f.Kt, f.SupplierId, null, null, null, null, null, null,
                [new(null, f.ItemId, f.Cai, qty, 1, null, null, null, null, po.Lines[0].Id, delivered, rejected, qc, note)], po.Id);

        // Chứng từ giao 10, nhận đạt 8, không đạt 1 → thiếu 1, kết quả Không đạt: bắt buộc ghi chú.
        Assert.Contains("ghi chú kiểm tra", await TitleAsync(await f.Buyer.PostAsJsonAsync("/api/goods-receipts", Receipt(8, 10, 1, "Passed", null), Ct)));
        var grn = await ReadAsync<InventoryDocumentDto>(await f.Buyer.PostAsJsonAsync("/api/goods-receipts",
            Receipt(8, 10, 1, "Passed", "1 cái móp, thiếu 1 cái so với phiếu giao"), Ct));
        Assert.Equal(("Failed", 1m, -1m, (decimal?)10m), (grn.Lines[0].QcResult, grn.Lines[0].RejectedQty, grn.Lines[0].DiscrepancyQty, grn.Lines[0].DeliveredQty));

        // Người lập PO không ghi sổ nhận hàng của PO đó; phiếu chưa kiểm tra không ghi sổ được.
        Assert.Contains("người lập đơn mua hàng", await TitleAsync(await PostDocAsync(f, grn.Id)));
        await ReadAsync<SystemParameterDto>(await f.Buyer.PutAsJsonAsync("/api/system-parameters/RECEIPT_SEPARATE_PO_CREATOR", new UpdateSystemParameterRequest(null), Ct));
        var unchecked_ = await ReadAsync<InventoryDocumentDto>(await f.Buyer.PostAsJsonAsync("/api/goods-receipts", Receipt(1, null, 0, null, null), Ct));
        Assert.Contains("chưa có kết quả kiểm tra", await TitleAsync(await PostDocAsync(f, unchecked_.Id)));
        await ReadAsync<InventoryDocumentDto>(await PostDocAsync(f, grn.Id));

        var report = await ReadAsync<ReceiptQualityResult>(await f.Buyer.GetAsync($"/api/reports/receipt-quality?from={Today:yyyy-MM-dd}&to={Today:yyyy-MM-dd}", Ct));
        var row = Assert.Single(report.Rows, r => r.DocumentId == grn.Id);
        Assert.Equal((po.No, 1m, -1m, "Failed"), (row.PurchaseOrderNo, row.RejectedQty, row.DiscrepancyQty, row.QcResult));
        Assert.Equal((1, 0, 0m), (report.CheckedLines, report.PassedLines, report.PassedPct!.Value));
    }

    [Fact]
    public async Task DoiChieuPoNhanHoaDon_LechTien_ChuaCoHoaDon_CapNhatSauGhiSo()
    {
        var f = await ArrangeAsync();
        var (approver, _) = await ApproverAsync(f);
        // PO 10 cái × 100.000, chiết khấu 5%, VAT 10% → giá ròng 95.000/cái, 104.500/cái gồm VAT.
        var po = await ReadAsync<PurchaseOrderDto>(await CreatePoAsync(f, 10, null));
        await ReadAsync<PurchaseOrderDto>(await f.Buyer.PostAsync($"/api/purchase-orders/{po.Id}/submit", null, Ct));
        po = await ReadAsync<PurchaseOrderDto>(await ActAsync(approver, "purchase-orders", po.Id, "approve"));

        SaveReceiptRequest Receipt(decimal qty, decimal? delivered, decimal rejected, string? qc, string? note, decimal? invoiceAmount) =>
            new(Today, f.Kt, f.SupplierId, "0000123", "1C26TAA", Today, null, null, null,
                [new(null, f.ItemId, f.Cai, qty, 1, null, null, null, null, po.Lines[0].Id, delivered, rejected, qc, note)], po.Id, invoiceAmount);

        // Phiếu 1: giao 5, đạt 4, không đạt 1; hóa đơn tính đủ 5 cái = 522.500 trong khi thực nhận 418.000.
        Assert.Contains("không được âm", await TitleAsync(await f.Buyer.PostAsJsonAsync("/api/goods-receipts", Receipt(4, 5, 1, "Failed", "1 cái vỡ", -1), Ct)));
        var short_ = await ReadAsync<InventoryDocumentDto>(await f.Buyer.PostAsJsonAsync("/api/goods-receipts", Receipt(4, 5, 1, "Failed", "1 cái vỡ", 522_500), Ct));
        Assert.Equal((decimal?)522_500m, short_.InvoiceAmount);
        Assert.False(short_.CanUpdateInvoice);
        await ReadAsync<InventoryDocumentDto>(await PostDocAsync(f, short_.Id));

        // Phiếu 2: nhận đủ 6 cái = 627.000; hóa đơn về sau – cập nhật sau khi ghi sổ, lệch 1 đ làm tròn.
        var full = await ReadAsync<InventoryDocumentDto>(await f.Buyer.PostAsJsonAsync("/api/goods-receipts", Receipt(6, 6, 0, "Passed", null, null), Ct));
        full = await ReadAsync<InventoryDocumentDto>(await PostDocAsync(f, full.Id));
        Assert.True(full.CanUpdateInvoice);
        full = await ReadAsync<InventoryDocumentDto>(await f.Buyer.PutAsJsonAsync($"/api/goods-receipts/{full.Id}/invoice",
            new UpdateReceiptInvoiceRequest("0000124", "1C26TAA", Today, 627_001), Ct));
        Assert.Equal(("Posted", "0000124", (decimal?)627_001m), (full.Status, full.InvoiceNo, full.InvoiceAmount));

        // Phiếu 3: nhập thẳng không theo PO, chưa có hóa đơn.
        var direct = await ReadAsync<InventoryDocumentDto>(await f.Buyer.PostAsJsonAsync("/api/goods-receipts",
            new SaveReceiptRequest(Today, f.Kt, f.SupplierId, null, null, null, null, null, null,
                [new(null, f.ItemId, f.Cai, 1, 50_000, "10", null, null, null, null, 1, 0, "Passed", null)]), Ct));
        await ReadAsync<InventoryDocumentDto>(await PostDocAsync(f, direct.Id));

        var url = $"/api/reports/three-way-match?from={Today:yyyy-MM-dd}&to={Today:yyyy-MM-dd}&tolerance=1";
        var report = await ReadAsync<ThreeWayMatchResult>(await f.Buyer.GetAsync(url, Ct));
        var r1 = Assert.Single(report.Rows, r => r.DocumentId == short_.Id);
        Assert.Equal(("Mismatch", 418_000m, (decimal?)104_500m, po.No, (decimal?)1_045_000m), (r1.Result, r1.ReceiptTotal, r1.Difference, r1.PurchaseOrderNo, r1.OrderTotal));
        Assert.Equal([ThreeWayMatchIssues.AmountMismatch, ThreeWayMatchIssues.InvoiceIncludesShortage], r1.Issues);
        var r2 = Assert.Single(report.Rows, r => r.DocumentId == full.Id);
        Assert.Equal(("Matched", 0), (r2.Result, r2.Issues.Count));
        var r3 = Assert.Single(report.Rows, r => r.DocumentId == direct.Id);
        Assert.Equal(("NoInvoice", (decimal?)null), (r3.Result, r3.Difference));
        Assert.Equal([ThreeWayMatchIssues.NoInvoice, ThreeWayMatchIssues.NoPurchaseOrder], r3.Issues);
        Assert.Equal((1, 1, 1, 2, 1_100_000m, 1_149_501m), (report.Matched, report.NoInvoice, report.Mismatch, report.WithIssues, report.ReceiptTotal, report.InvoiceTotal));

        // Không dung sai: lệch 1 đ là lệch; chỉ lấy phiếu có cảnh báo.
        var strict = await ReadAsync<ThreeWayMatchResult>(await f.Buyer.GetAsync(url.Replace("tolerance=1", "tolerance=0") + "&onlyIssues=true", Ct));
        Assert.Equal(3, strict.Rows.Count);
        Assert.Equal((0, 2), (strict.Matched, strict.Mismatch));
        Assert.Contains("Dung sai", await TitleAsync(await f.Buyer.GetAsync(url.Replace("tolerance=1", "tolerance=-1"), Ct)));
    }

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
    public async Task QuyTrinhDuyet_LoaiMoiPrlRfqNccCtr_CauHinhDuoc()
    {
        var f = await ArrangeAsync();
        var (_, role) = await ApproverAsync(f);

        var types = await f.Buyer.GetFromJsonAsync<List<ApprovalTypeDto>>("/api/approval-rules/types", Ct);
        Assert.Equal(["PR", "PO", "POX", "POK", "PRB", "POB", "POL", "PRL", "RFQ", "NCC", "CTR", "CTA", "NDQ", "GTH"], types!.Select(t => t.Code));
        Assert.Null(types.Single(t => t.Code == "NCC").AmountLabel);

        var prl = await ReadAsync<ApprovalRuleDto>(await f.Buyer.PostAsJsonAsync("/api/approval-rules", new SaveApprovalRuleRequest("prl", 1, 0, role), Ct));
        Assert.Equal("PRL", prl.DocType);
        await ReadAsync<ApprovalRuleDto>(await f.Buyer.PostAsJsonAsync("/api/approval-rules", new SaveApprovalRuleRequest("RFQ", 1, 50_000_000, role), Ct));
        await ReadAsync<ApprovalRuleDto>(await f.Buyer.PostAsJsonAsync("/api/approval-rules", new SaveApprovalRuleRequest("CTR", 1, 0, role), Ct));
        Assert.Contains("ngưỡng phải là 0", await TitleAsync(await f.Buyer.PostAsJsonAsync("/api/approval-rules", new SaveApprovalRuleRequest("NCC", 1, 1, role), Ct)));
        Assert.Contains("Loại phiếu duyệt phải là", await TitleAsync(await f.Buyer.PostAsJsonAsync("/api/approval-rules", new SaveApprovalRuleRequest("GRN", 1, 0, role), Ct)));

        var rules = await f.Buyer.GetFromJsonAsync<List<ApprovalRuleDto>>("/api/approval-rules", Ct);
        Assert.Equal(["CTR", "PRL", "RFQ"], rules!.Select(r => r.DocType).Order());
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

    /// <summary>
    /// Người duyệt tiếp theo (TC-4): bước đang chờ nêu tên những người duyệt được – có vai trò của cấp tại công ty, không phải người lập,
    /// không vướng kê khai xung đột lợi ích với NCC; bước chưa tới lượt và phiếu đã duyệt xong thì không có. Tra cứu chứng từ cùng kết quả.
    /// </summary>
    [Fact]
    public async Task NguoiDuyetTiepTheo_TheoCapDangCho_BoNguoiLapVaXungDot()
    {
        var f = await ArrangeAsync();
        async Task<(HttpClient Client, string FullName)> UserAsync(string roleCode)
        {
            var userName = $"nd.{Guid.NewGuid():N}"[..16];
            await _factory.CreateScopedUserAsync(userName, f.CompanyCode, roleCode);
            return (await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, f.CompanyCode), $"Người thử {userName}");
        }

        var headCode = UniqueCode("T");
        var headRole = await _factory.CreateRoleAsync(headCode, Permissions.Purchase.PoView, Permissions.Purchase.PoApprove);
        var directorCode = UniqueCode("G");
        var directorRole = await _factory.CreateRoleAsync(directorCode, Permissions.Purchase.PoView, Permissions.Purchase.PoApprove);
        var (head, headName) = await UserAsync(headCode);
        var (conflicted, _) = await UserAsync(headCode);
        var (director, directorName) = await UserAsync(directorCode);
        // Người có cùng vai trò nhưng đã kê khai quan hệ lợi ích với NCC của PO: không duyệt được nên không phải "người duyệt tiếp theo".
        var declared = await conflicted.PostAsJsonAsync("/api/conflict-declarations",
            new CreateConflictDeclarationRequest(null, f.SupplierId, "Family", "Em trai làm ở NCC", null), Ct);
        Assert.True(declared.IsSuccessStatusCode, await declared.Content.ReadAsStringAsync(Ct));
        await ReadAsync<ApprovalRuleDto>(await f.Buyer.PostAsJsonAsync("/api/approval-rules", new SaveApprovalRuleRequest("PO", 1, 0, headRole), Ct));
        await ReadAsync<ApprovalRuleDto>(await f.Buyer.PostAsJsonAsync("/api/approval-rules", new SaveApprovalRuleRequest("PO", 2, 500_000, directorRole), Ct));

        var po = await ReadAsync<PurchaseOrderDto>(await CreatePoAsync(f, 10, null));
        po = await ReadAsync<PurchaseOrderDto>(await f.Buyer.PostAsync($"/api/purchase-orders/{po.Id}/submit", null, Ct));
        Assert.Equal([headName], po.Approvals.Single(a => a.Level == 1).NextApprovers);
        Assert.Null(po.Approvals.Single(a => a.Level == 2).NextApprovers);
        var range = $"from={Today:yyyy-MM-dd}&to={Today:yyyy-MM-dd}&docTypes=PO";
        async Task<IReadOnlyList<string>?> RegistryAsync() =>
            (await f.Buyer.GetFromJsonAsync<DocumentRegistryResult>($"/api/document-registry?{range}", Ct))!.Rows.Single(r => r.Id == po.Id).NextApprovers;
        Assert.Equal([headName], await RegistryAsync());

        po = await ReadAsync<PurchaseOrderDto>(await ActAsync(head, "purchase-orders", po.Id, "approve"));
        Assert.Null(po.Approvals.Single(a => a.Level == 1).NextApprovers);
        Assert.Equal([directorName], po.Approvals.Single(a => a.Level == 2).NextApprovers);
        Assert.Equal([directorName], await RegistryAsync());

        po = await ReadAsync<PurchaseOrderDto>(await ActAsync(director, "purchase-orders", po.Id, "approve"));
        Assert.Equal("Approved", po.Status);
        Assert.All(po.Approvals, a => Assert.Null(a.NextApprovers));
        Assert.Null(await RegistryAsync());
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

    /// <summary>
    /// Phím F1 – lịch sử theo ô và "Lịch sử thay đổi" cả phiếu (design.md mục 9.3a): người tạo/sửa, giá trị cũ/mới, tham chiếu đổi
    /// sang mã – tên, dòng đã xóa vẫn thấy trong lịch sử phiếu; người ngoài phạm vi công ty không xem được.
    /// </summary>
    [Fact]
    public async Task LichSuThayDoi_TheoO_TheoPhieu_CaDongDaXoa_ChanNgoaiPhamVi()
    {
        var f = await ArrangeAsync();
        var po = await ReadAsync<PurchaseOrderDto>(await f.Buyer.PostAsJsonAsync("/api/purchase-orders",
            new SavePurchaseOrderRequest(Today, f.SupplierId, f.Kt, null, "30 ngày", null, null, 0, null,
                [new(null, f.ItemId, f.Cai, 5, 100_000, 0, "10", null, null, null), new(null, f.ItemId, f.Cai, 2, 50_000, 0, "10", null, null, null)]), Ct));
        var keep = po.Lines.Single(l => l.LineNo == 1);
        var removed = po.Lines.Single(l => l.LineNo == 2);
        // Sửa: đổi điều khoản thanh toán, SL dòng 1 từ 5 thành 8, xóa dòng 2.
        await ReadAsync<PurchaseOrderDto>(await f.Buyer.PutAsJsonAsync($"/api/purchase-orders/{po.Id}",
            new SavePurchaseOrderRequest(Today, f.SupplierId, f.Kt, null, "45 ngày", null, null, 0, null,
                [new(keep.Id, f.ItemId, f.Cai, 8, 100_000, 0, "10", null, null, null)]), Ct));

        var terms = await f.Buyer.GetFromJsonAsync<FieldHistoryDto>(
            $"/api/change-history/field?entityType=PurchaseOrder&entityId={po.Id}&field=paymentTerms", Ct);
        Assert.Equal("PaymentTerms", terms!.Info.Field);
        Assert.Equal("PurchaseOrder", terms.Info.Table);
        Assert.Equal(["Update", "Create"], terms.Changes.Select(c => c.Action));
        Assert.Equal(("30 ngày", "45 ngày"), (terms.Changes[0].OldValue, terms.Changes[0].NewValue));
        Assert.NotNull(terms.Changes[0].UserName);
        Assert.NotNull(terms.Record.CreatedBy);

        // Ô "supplier" lưu ở SupplierId: giá trị đổi sang mã – tên NCC.
        var supplier = await f.Buyer.GetFromJsonAsync<FieldHistoryDto>(
            $"/api/change-history/field?entityType=PurchaseOrder&entityId={po.Id}&field=supplier", Ct);
        Assert.Equal(FieldValueKinds.Reference, supplier!.Info.Kind);
        Assert.Contains("NCC mua hàng", Assert.Single(supplier.Changes).NewText);

        var qty = await f.Buyer.GetFromJsonAsync<FieldHistoryDto>(
            $"/api/change-history/field?entityType=PurchaseOrderLine&entityId={keep.Id}&field=qty", Ct);
        Assert.Equal(FieldValueKinds.Number, qty!.Info.Kind);
        Assert.Equal(("5", "8"), (qty.Changes[0].OldValue, qty.Changes[0].NewValue));

        // Ô không lưu thẳng vào bản ghi: có thông tin ô, không có lịch sử.
        var computed = await f.Buyer.GetFromJsonAsync<FieldHistoryDto>(
            $"/api/change-history/field?entityType=PurchaseOrder&entityId={po.Id}&field=khongCo", Ct);
        Assert.Null(computed!.Info.Field);
        Assert.Empty(computed.Changes);

        var history = await f.Buyer.GetFromJsonAsync<RecordHistoryDto>(
            $"/api/change-history/record?entityType=PurchaseOrder&entityId={po.Id}", Ct);
        Assert.Contains(history!.Changes, c => c.EntityId == removed.Id.ToString() && c.Action == "Delete" && c.LineNo == 2 && c.Field == "Qty");
        Assert.Contains(history.Changes, c => c.EntityId == keep.Id.ToString() && c.Action == "Update" && c.Field == "Qty" && c.LineNo == 1);
        Assert.Contains(history.Changes, c => c.EntityType == "PurchaseOrder" && c.Field == "PaymentTerms" && c.Action == "Update");

        // Người dùng ở công ty khác không xem được lịch sử phiếu, cũng không xem được qua dòng của phiếu.
        var other = await _factory.CreateClientInCompanyAsync(ProcApiFactory.AdminUserName, ProcApiFactory.AdminPassword, "BHA");
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync(
            $"/api/change-history/field?entityType=PurchaseOrder&entityId={po.Id}&field=paymentTerms", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync(
            $"/api/change-history/field?entityType=PurchaseOrderLine&entityId={keep.Id}&field=qty", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync(
            $"/api/change-history/record?entityType=PurchaseOrder&entityId={po.Id}", Ct)).StatusCode);
    }
}
