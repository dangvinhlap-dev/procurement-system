using System.Net;
using System.Net.Http.Json;
using Proc.Application.Common;
using Proc.Application.Inventory;
using Proc.Application.MasterData;
using Proc.Application.Organization;
using Proc.Application.Purchasing;
using Proc.Application.Reports;
using Proc.Application.Sourcing;
using Proc.Domain.Organization;

namespace Proc.IntegrationTests;

/// <summary>
/// Báo cáo PR chưa xử lý, mua hàng theo nhóm, đánh giá NCC, tiết kiệm từ đấu thầu (design.md mục 11.3). Mỗi test một công ty mới nên các
/// báo cáo chỉ thấy chứng từ của test đó.
/// </summary>
[Collection(ApiCollection.Name)]
public class PurchaseReportTests
{
    private readonly ProcApiFactory _factory;

    public PurchaseReportTests(ProcApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string UniqueCode(string prefix) => $"{prefix}{Guid.NewGuid():N}".ToUpperInvariant()[..10];

    private static DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTime.UtcNow, VietnamTime.Zone));

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(Ct)}");
        return (await response.Content.ReadFromJsonAsync<T>(Ct))!;
    }

    private sealed record Fixture(
        HttpClient Buyer, HttpClient Approver, string CompanyCode, Guid Kt, Guid ItemId, Guid Cai, Guid Thung, Guid[] Suppliers, string[] SupplierCodes);

    /// <summary>
    /// Công ty mới (kỳ tháng hiện tại mở), mặt hàng (cái, thùng = 12 cái), 3 NCC đã duyệt áp dụng tại công ty. Người lập: quản trị;
    /// người duyệt: vai trò mới có quyền duyệt PR, PO (người lập không tự duyệt).
    /// </summary>
    private async Task<Fixture> ArrangeAsync()
    {
        var userName = $"bm.{Guid.NewGuid():N}"[..16];
        await _factory.CreateUserAsync(userName, ProcApiFactory.TestPassword, ["TCT"]);
        var head = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, "TCT");
        var company = await ReadAsync<CompanyDetailDto>(await head.PostAsJsonAsync("/api/companies",
            new CreateCompanyRequest(UniqueCode("M"), "Công ty thử báo cáo mua hàng", null, null, null, null, new DateOnly(Today.Year, Today.Month, 1), "KT", "Kho tổng"), Ct));
        var buyer = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, company.Code);
        var calendar = await ReadAsync<InventoryCalendarDto>(await buyer.PostAsJsonAsync("/api/inventory-periods/years", new CreateInventoryYearRequest(Today.Year), Ct));
        await ReadAsync<InventoryPeriodDto>(await buyer.PostAsync($"/api/inventory-periods/{calendar.Periods.Single(p => p.Month == Today.Month).Id}/open", null, Ct));
        var kt = (await buyer.GetFromJsonAsync<List<WarehouseDetailDto>>("/api/warehouses", Ct))!.Single().Id;

        var uoms = (await head.GetFromJsonAsync<List<UomDto>>("/api/uoms", Ct))!;
        var (cai, thung) = (uoms.Single(u => u.Code == "CAI").Id, uoms.Single(u => u.Code == "THUNG").Id);
        var category = await ReadAsync<ItemCategoryDetailDto>(await head.PostAsJsonAsync("/api/item-categories",
            new CreateItemCategoryRequest(UniqueCode("P"), "Nhóm báo cáo mua hàng", null, null), Ct));
        var item = await ReadAsync<ItemDetailDto>(await head.PostAsJsonAsync("/api/items",
            new CreateItemRequest($"{category.Code}-01", "Dây curoa B52", null, "Inventory", category.Id, cai, null, null, null, null, "10", null, false, false,
                null, null, [new(thung, 12)]), Ct));
        var groupId = (await head.GetFromJsonAsync<List<SupplierGroupDto>>("/api/supplier-groups", Ct))!.Single(g => g.Code == "TN").Id;
        var suppliers = new List<SupplierDetailDto>();
        foreach (var name in new[] { "A", "B", "C" })
        {
            var supplier = await ReadAsync<SupplierDetailDto>(await head.PostAsJsonAsync("/api/suppliers",
                new SaveSupplierRequest(UniqueCode("N"), groupId, $"NCC báo cáo {name}", null, false, null, null, null, null, null, null, null, null, "Approved", null, null, [], []), Ct));
            await ReadAsync<SupplierCompanyDto>(await buyer.PutAsJsonAsync($"/api/suppliers/{supplier.Id}/company", new SaveSupplierCompanyRequest(null, null, true), Ct));
            suppliers.Add(supplier);
        }

        var roleCode = UniqueCode("D");
        await _factory.CreateRoleAsync(roleCode, Permissions.Purchase.PrView, Permissions.Purchase.PrApprove, Permissions.Purchase.PoView, Permissions.Purchase.PoApprove);
        var approverName = $"dy.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(approverName, company.Code, roleCode);
        var approver = await _factory.CreateClientInCompanyAsync(approverName, ProcApiFactory.TestPassword, company.Code);
        return new Fixture(buyer, approver, company.Code, kt, item.Id, cai, thung, [.. suppliers.Select(s => s.Id)], [.. suppliers.Select(s => s.Code)]);
    }

    private static async Task<PurchaseRequestDto> SubmitPrAsync(Fixture f, decimal qty, bool approve)
    {
        var pr = await ReadAsync<PurchaseRequestDto>(await f.Buyer.PostAsJsonAsync("/api/purchase-requests",
            new SavePurchaseRequestRequest(Today, "Xưởng cán", "Bảo trì", Today.AddDays(7), f.Kt, "Urgent", null, [new(null, f.ItemId, f.Cai, qty, null, null, null)]), Ct));
        pr = await ReadAsync<PurchaseRequestDto>(await f.Buyer.PostAsync($"/api/purchase-requests/{pr.Id}/submit", null, Ct));
        if (approve) pr = await ReadAsync<PurchaseRequestDto>(await f.Approver.PostAsJsonAsync($"/api/purchase-requests/{pr.Id}/approve", new ApprovalActionRequest(null), Ct));
        return pr;
    }

    /// <summary>PO đã duyệt một dòng (giá trước CK, CK 0%).</summary>
    private static async Task<PurchaseOrderDto> ApprovedPoAsync(Fixture f, Guid supplierId, DateOnly date, DateOnly expected, decimal qty, decimal price)
    {
        var po = await ReadAsync<PurchaseOrderDto>(await f.Buyer.PostAsJsonAsync("/api/purchase-orders",
            new SavePurchaseOrderRequest(date, supplierId, f.Kt, expected, null, null, null, 0, null, [new(null, f.ItemId, f.Cai, qty, price, 0, "10", null, null, null)]), Ct));
        await ReadAsync<PurchaseOrderDto>(await f.Buyer.PostAsync($"/api/purchase-orders/{po.Id}/submit", null, Ct));
        return await ReadAsync<PurchaseOrderDto>(await f.Approver.PostAsJsonAsync($"/api/purchase-orders/{po.Id}/approve", new ApprovalActionRequest(null), Ct));
    }

    /// <summary>Phiếu nhập mua đã ghi sổ hôm nay; có PO thì nhận theo dòng PO (giá theo PO).</summary>
    private static async Task<InventoryDocumentDto> ReceiveAsync(Fixture f, Guid supplierId, decimal qty, decimal price, PurchaseOrderDto? po = null)
    {
        var grn = await ReadAsync<InventoryDocumentDto>(await f.Buyer.PostAsJsonAsync("/api/goods-receipts",
            new SaveReceiptRequest(Today, f.Kt, supplierId, null, null, null, null, null, null,
                [new(null, f.ItemId, f.Cai, qty, price, "10", null, null, null, po?.Lines[0].Id)], po?.Id), Ct));
        return await ReadAsync<InventoryDocumentDto>(await f.Buyer.PostAsync($"/api/inventory-documents/{grn.Id}/post", null, Ct));
    }

    [Fact]
    public async Task PrChuaXuLy_ChoDuyetVaDaDuyetChuaDatDu()
    {
        var f = await ArrangeAsync();
        var submitted = await SubmitPrAsync(f, 10, approve: false);
        var approved = await SubmitPrAsync(f, 20, approve: true);
        // PO nháp lấy 8/20 từ PR đã duyệt: vẫn tính là đã đặt (PO chưa hủy).
        await ReadAsync<PurchaseOrderDto>(await f.Buyer.PostAsJsonAsync("/api/purchase-orders",
            new SavePurchaseOrderRequest(Today, f.Suppliers[0], f.Kt, Today.AddDays(5), null, null, null, 0, null,
                [new(null, f.ItemId, f.Cai, 8, 100_000, 0, "10", null, null, approved.Lines[0].Id)]), Ct));
        var full = await SubmitPrAsync(f, 5, approve: true);
        await ReadAsync<PurchaseOrderDto>(await f.Buyer.PostAsJsonAsync("/api/purchase-orders",
            new SavePurchaseOrderRequest(Today, f.Suppliers[0], f.Kt, Today.AddDays(5), null, null, null, 0, null,
                [new(null, f.ItemId, f.Cai, 5, 100_000, 0, "10", null, null, full.Lines[0].Id)]), Ct));

        var rows = await ReadAsync<List<PendingRequestLineDto>>(await f.Buyer.GetAsync("/api/reports/pending-requests", Ct));
        Assert.Equal(
            [(submitted.No, "Submitted", 10m, 0m, 10m), (approved.No, "Approved", 20m, 8m, 12m)],
            rows.OrderBy(r => r.RequestNo).Select(r => (r.RequestNo, r.Status, r.Qty, r.OrderedQty, r.RemainingQty)));
        Assert.All(rows, r => Assert.Equal((Today.AddDays(7), 0, "Urgent"), (r.NeededDate!.Value, r.OverdueDays, r.Priority)));
    }

    [Fact]
    public async Task MuaHangTheoNcc_DanhGiaNcc_DungHanVaTraHang()
    {
        var f = await ArrangeAsync();
        var (a, b, c) = (f.Suppliers[0], f.Suppliers[1], f.Suppliers[2]);
        // A: hẹn giao sau 5 ngày, nhận hôm nay → đúng hạn. B: hẹn giao 2 ngày trước, nhận hôm nay → trễ 2 ngày, trả 1/5.
        // C: nhập không theo PO → không chấm đúng hạn.
        var poA = await ApprovedPoAsync(f, a, Today.AddDays(-10), Today.AddDays(5), 10, 100_000);
        var poB = await ApprovedPoAsync(f, b, Today.AddDays(-10), Today.AddDays(-2), 5, 200_000);
        await ReceiveAsync(f, a, 10, 1, poA);
        var grnB = await ReceiveAsync(f, b, 5, 1, poB);
        await ReceiveAsync(f, c, 2, 50_000);
        var rtv = await ReadAsync<InventoryDocumentDto>(await f.Buyer.PostAsJsonAsync("/api/goods-returns",
            new SaveReturnRequest(Today, grnB.Id, null, "Hàng lỗi", [new(null, grnB.Lines[0].Id, 1, null)]), Ct));
        await ReadAsync<InventoryDocumentDto>(await f.Buyer.PostAsync($"/api/inventory-documents/{rtv.Id}/post", null, Ct));

        var range = $"from={Today.AddDays(-30):yyyy-MM-dd}&to={Today:yyyy-MM-dd}";
        var spend = await ReadAsync<PurchaseSpendResult>(await f.Buyer.GetAsync($"/api/reports/purchase-spend?{range}&groupBy=supplier", Ct));
        Assert.Equal(1_900_000m, spend.TotalNet);
        // Tỷ trọng: 1.000.000 / 1.900.000 = 52,63%; 800.000 → 42,11%; 100.000 → 5,26%.
        Assert.Equal(
            [(f.SupplierCodes[0], 1, 1_000_000m, 0m, 1_000_000m, 52.63m), (f.SupplierCodes[1], 1, 1_000_000m, 200_000m, 800_000m, 42.11m),
                (f.SupplierCodes[2], 1, 100_000m, 0m, 100_000m, 5.26m)],
            spend.Items.Select(r => (r.Code, r.ReceiptCount, r.ReceiptAmount, r.ReturnAmount, r.NetAmount, r.SharePct)));
        var byCategory = await ReadAsync<PurchaseSpendResult>(await f.Buyer.GetAsync($"/api/reports/purchase-spend?{range}&groupBy=category", Ct));
        Assert.Equal((3, 1_900_000m), (byCategory.Items.Single().ReceiptCount, byCategory.Items.Single().NetAmount));
        Assert.Equal(HttpStatusCode.BadRequest, (await f.Buyer.GetAsync($"/api/reports/purchase-spend?{range}&groupBy=abc", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await f.Buyer.GetAsync($"/api/reports/purchase-spend?from={Today.AddYears(-3):yyyy-MM-dd}&to={Today:yyyy-MM-dd}", Ct)).StatusCode);

        var evaluation = (await ReadAsync<List<SupplierEvaluationRowDto>>(await f.Buyer.GetAsync($"/api/reports/supplier-evaluation?{range}", Ct)))
            .ToDictionary(r => r.SupplierId);
        Assert.Equal((0m, 1, 1, 100m, (decimal?)null), (evaluation[a].ReturnPct!.Value, evaluation[a].DeliveryLines, evaluation[a].OnTimeLines, evaluation[a].OnTimePct!.Value, evaluation[a].AvgLateDays));
        Assert.Equal((20m, 1, 0, 0m, 2m), (evaluation[b].ReturnPct!.Value, evaluation[b].DeliveryLines, evaluation[b].OnTimeLines, evaluation[b].OnTimePct!.Value, evaluation[b].AvgLateDays!.Value));
        Assert.Equal((0, (decimal?)null), (evaluation[c].DeliveryLines, evaluation[c].OnTimePct));

        // Có quyền báo cáo mua hàng nhưng không có quyền toàn tập đoàn.
        var roleCode = UniqueCode("V");
        await _factory.CreateRoleAsync(roleCode, Permissions.Report.PurchaseView);
        var viewerName = $"xm.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(viewerName, f.CompanyCode, roleCode);
        var viewer = await _factory.CreateClientInCompanyAsync(viewerName, ProcApiFactory.TestPassword, f.CompanyCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.GetAsync($"/api/reports/purchase-spend?{range}&allCompanies=true", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.GetAsync($"/api/reports/supplier-evaluation?{range}&allCompanies=true", Ct)).StatusCode);
        Assert.Equal(1_900_000m, (await ReadAsync<PurchaseSpendResult>(await viewer.GetAsync($"/api/reports/purchase-spend?{range}", Ct))).TotalNet);

        // Quản trị xem toàn tập đoàn, nhóm theo công ty: có dòng của công ty này.
        var all = await ReadAsync<PurchaseSpendResult>(await f.Buyer.GetAsync($"/api/reports/purchase-spend?{range}&groupBy=company&allCompanies=true", Ct));
        Assert.True(all.AllCompanies);
        Assert.Equal(1_900_000m, all.Items.Single(r => r.Code == f.CompanyCode).NetAmount);
    }

    [Fact]
    public async Task TietKiemDauThau_SoVoiGiaChaoBinhQuanVaCaoNhat()
    {
        var f = await ArrangeAsync();
        var (a, b, c) = (f.Suppliers[0], f.Suppliers[1], f.Suppliers[2]);
        var rfq = await ReadAsync<RfqDto>(await f.Buyer.PostAsJsonAsync("/api/rfqs",
            new SaveRfqRequest(Today, "Dây curoa quý 4", Today.AddDays(7), null, new RfqWeightsDto(60, 20, 10, 5, 5), null,
                [new(null, f.ItemId, f.Cai, 100, null, null), new(null, f.ItemId, f.Thung, 10, null, null)], [a, b, c]), Ct));
        rfq = await ReadAsync<RfqDto>(await f.Buyer.PostAsync($"/api/rfqs/{rfq.Id}/send", null, Ct));
        var (cai, thung) = (rfq.Lines[0].Id, rfq.Lines[1].Id);
        static SaveQuotationLineRequest Q(Guid lineId, decimal price, decimal discount) => new(lineId, price, discount, null, 7, null);
        SaveQuotationRequest Quote(params List<SaveQuotationLineRequest> lines) => new(Today, Today.AddDays(30), null, 8, 10, 10, 6, null, lines);
        await ReadAsync<RfqDto>(await f.Buyer.PutAsJsonAsync($"/api/rfqs/{rfq.Id}/quotations/{a}", Quote(Q(cai, 50_000, 0), Q(thung, 600_000, 5)), Ct));
        await ReadAsync<RfqDto>(await f.Buyer.PutAsJsonAsync($"/api/rfqs/{rfq.Id}/quotations/{b}", Quote(Q(cai, 48_000, 0)), Ct));
        await ReadAsync<RfqDto>(await f.Buyer.PutAsJsonAsync($"/api/rfqs/{rfq.Id}/quotations/{c}", Quote(Q(cai, 52_000, 0), Q(thung, 590_000, 0)), Ct));
        await ReadAsync<RfqDto>(await f.Buyer.PostAsJsonAsync($"/api/rfqs/{rfq.Id}/award", new AwardRfqRequest([new(cai, b, null), new(thung, a, null)]), Ct));

        var rows = await ReadAsync<List<SourcingSavingRowDto>>(await f.Buyer.GetAsync(
            $"/api/reports/sourcing-savings?from={Today.AddDays(-1):yyyy-MM-dd}&to={Today:yyyy-MM-dd}", Ct));
        var byUom = rows.Where(r => r.RfqId == rfq.Id).ToDictionary(r => r.UomCode);
        // Cái: chào 50.000 / 48.000 / 52.000, chọn 48.000 → bình quân 50.000: tiết kiệm 200.000; so cao nhất: 400.000.
        Assert.Equal((f.SupplierCodes[1], 3, 48_000m, 50_000m, 4_800_000m, 200_000m, 400_000m, true),
            (byUom["CAI"].SupplierCode, byUom["CAI"].QuoteCount, byUom["CAI"].AwardedPrice, byUom["CAI"].AvgPrice, byUom["CAI"].AwardedAmount,
                byUom["CAI"].SavingVsAvg, byUom["CAI"].SavingVsMax, byUom["CAI"].IsLowest));
        // Thùng: A 600.000 − 5% = 570.000, C 590.000 → bình quân 580.000: tiết kiệm 100.000; so cao nhất 200.000.
        Assert.Equal((2, 570_000m, 580_000m, 100_000m, 200_000m), (byUom["THUNG"].QuoteCount, byUom["THUNG"].AwardedPrice, byUom["THUNG"].AvgPrice,
            byUom["THUNG"].SavingVsAvg, byUom["THUNG"].SavingVsMax));
    }
}
