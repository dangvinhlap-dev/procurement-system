using System.Net;
using System.Net.Http.Json;
using Proc.Application.Common;
using Proc.Application.Inventory;
using Proc.Application.MasterData;
using Proc.Application.Organization;
using Proc.Application.Purchasing;
using Proc.Application.Reports;
using Proc.Domain.Organization;

namespace Proc.IntegrationTests;

/// <summary>
/// Gợi ý đặt hàng (lát D1, design.md mục 18.16): khả dụng = tồn mọi kho + đang đặt + đề nghị còn mở; dưới điểm đặt hàng lại (chưa có thì
/// tồn tối thiểu) thì gợi ý SL lên tồn tối đa; tồn dưới tồn an toàn là khẩn; lọc trọng yếu, theo phạm vi nhóm hàng.
/// </summary>
[Collection(ApiCollection.Name)]
public class ReorderSuggestionTests
{
    private readonly ProcApiFactory _factory;

    public ReorderSuggestionTests(ProcApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string UniqueCode(string prefix) => $"{prefix}{Guid.NewGuid():N}".ToUpperInvariant()[..10];

    private static DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTime.UtcNow, VietnamTime.Zone));

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(Ct)}");
        return (await response.Content.ReadFromJsonAsync<T>(Ct))!;
    }

    [Fact]
    public async Task GoiYDatHang_TonCongDangDatCongDeNghi_DuoiDiemDatLai_KhanKhiDuoiTonAnToan()
    {
        var userName = $"dh.{Guid.NewGuid():N}"[..16];
        await _factory.CreateUserAsync(userName, ProcApiFactory.TestPassword, ["TCT"]);
        var head = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, "TCT");
        var company = await ReadAsync<CompanyDetailDto>(await head.PostAsJsonAsync("/api/companies",
            new CreateCompanyRequest(UniqueCode("R"), "Công ty thử đặt hàng lại", null, null, null, null, new DateOnly(Today.Year, Today.Month, 1), "KT", "Kho tổng"), Ct));
        var buyer = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, company.Code);
        var calendar = await ReadAsync<InventoryCalendarDto>(await buyer.PostAsJsonAsync("/api/inventory-periods/years", new CreateInventoryYearRequest(Today.Year), Ct));
        await ReadAsync<InventoryPeriodDto>(await buyer.PostAsync($"/api/inventory-periods/{calendar.Periods.Single(p => p.Month == Today.Month).Id}/open", null, Ct));
        var kt = (await buyer.GetFromJsonAsync<List<WarehouseDetailDto>>("/api/warehouses", Ct))!.Single().Id;
        var cai = (await head.GetFromJsonAsync<List<UomDto>>("/api/uoms", Ct))!.Single(u => u.Code == "CAI").Id;
        var category = await ReadAsync<ItemCategoryDetailDto>(await head.PostAsJsonAsync("/api/item-categories",
            new CreateItemCategoryRequest(TestCodes.Category(), $"Nhóm đặt hàng lại {Guid.NewGuid():N}", null, null), Ct));

        CreateItemRequest NewItem(string name, decimal? min, decimal? max, decimal? safety, decimal? reorder, bool critical) =>
            new(null, name, null, "Inventory", category.Id, cai, null, null, null, null, "10", null, false, false, min, max, [], safety, reorder, critical);

        // Tồn an toàn > điểm đặt hàng lại bị từ chối.
        var invalid = await head.PostAsJsonAsync("/api/items", NewItem("Sai thứ tự", null, null, 60, 50, false), Ct);
        Assert.False(invalid.IsSuccessStatusCode);

        // A: trọng yếu, an toàn 20, đặt lại 50, tối đa 200. B: chỉ có tồn tối thiểu 10. C: đặt lại 5, đã có PR 10 → đủ.
        var a = await ReadAsync<ItemDetailDto>(await head.PostAsJsonAsync("/api/items", NewItem("Vòng bi trọng yếu", null, 200, 20, 50, true), Ct));
        Assert.Equal((20m, 50m, true), (a.SafetyStock!.Value, a.ReorderPoint!.Value, a.IsCritical));
        var b = await ReadAsync<ItemDetailDto>(await head.PostAsJsonAsync("/api/items", NewItem("Găng tay", 10, null, null, null, false), Ct));
        var c = await ReadAsync<ItemDetailDto>(await head.PostAsJsonAsync("/api/items", NewItem("Dây đai", null, null, null, 5, false), Ct));

        var groupId = (await head.GetFromJsonAsync<List<SupplierGroupDto>>("/api/supplier-groups", Ct))!.Single(g => g.Code == "TN").Id;
        var supplier = await ReadAsync<SupplierDetailDto>(await head.PostAsJsonAsync("/api/suppliers",
            new SaveSupplierRequest(null, groupId, "NCC đặt hàng lại", null, false, null, null, null, null, null, null, null, null, "Approved", null, null, [], []), Ct));
        await _factory.FinishSupplierApprovalAsync(supplier.Id);
        await ReadAsync<SupplierCompanyDto>(await buyer.PutAsJsonAsync($"/api/suppliers/{supplier.Id}/company", new SaveSupplierCompanyRequest(null, null, true), Ct));
        var approverRole = UniqueCode("A");
        await _factory.CreateRoleAsync(approverRole, Permissions.Purchase.PoView, Permissions.Purchase.PoApprove);
        var approverName = $"dy.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(approverName, company.Code, approverRole);
        var approver = await _factory.CreateClientInCompanyAsync(approverName, ProcApiFactory.TestPassword, company.Code);

        // A: tồn 15 (phiếu nhập đã ghi sổ), PO đã duyệt 10 chưa nhận, PR 5 chờ duyệt. C: PR nháp 10.
        var receipt = await ReadAsync<InventoryDocumentDto>(await buyer.PostAsJsonAsync("/api/goods-receipts",
            new SaveReceiptRequest(Today, kt, supplier.Id, null, null, null, null, null, null, [new(null, a.Id, cai, 15, 50_000, "10", null, null, null)]), Ct));
        await ReadAsync<InventoryDocumentDto>(await buyer.PostAsync($"/api/inventory-documents/{receipt.Id}/post", null, Ct));
        var po = await ReadAsync<PurchaseOrderDto>(await buyer.PostAsJsonAsync("/api/purchase-orders",
            new SavePurchaseOrderRequest(Today, supplier.Id, kt, Today.AddDays(5), null, null, null, 0, null,
                [new(null, a.Id, cai, 10, 50_000, 0, "10", null, null, null)]), Ct));
        await ReadAsync<PurchaseOrderDto>(await buyer.PostAsync($"/api/purchase-orders/{po.Id}/submit", null, Ct));
        await ReadAsync<PurchaseOrderDto>(await approver.PostAsJsonAsync($"/api/purchase-orders/{po.Id}/approve", new ApprovalActionRequest(null), Ct));
        var department = await TestDepartments.CreateAsync(buyer);
        var pr = await ReadAsync<PurchaseRequestDto>(await buyer.PostAsJsonAsync("/api/purchase-requests",
            new SavePurchaseRequestRequest(Today, department, null, Today.AddDays(7), kt, "Normal", null, [new(null, a.Id, cai, 5, null, null, null)]), Ct));
        await ReadAsync<PurchaseRequestDto>(await buyer.PostAsync($"/api/purchase-requests/{pr.Id}/submit", null, Ct));
        await ReadAsync<PurchaseRequestDto>(await buyer.PostAsJsonAsync("/api/purchase-requests",
            new SavePurchaseRequestRequest(Today, department, null, Today.AddDays(7), kt, "Normal", null, [new(null, c.Id, cai, 10, null, null, null)]), Ct));

        // A: khả dụng 15 + 10 + 5 = 30 < 50 → gợi ý 200 − 30 = 170; tồn 15 < an toàn 20 → khẩn. B: 0 < 10 → gợi ý 10 (lên ngưỡng).
        var result = await ReadAsync<ReorderSuggestionResult>(await buyer.GetAsync($"/api/reports/reorder-suggestions?categoryId={category.Id}", Ct));
        Assert.Equal([a.Code, b.Code], result.Items.Select(r => r.ItemCode));
        var rowA = result.Items[0];
        Assert.Equal((15m, 10m, 5m, 30m, 170m, "Urgent", true), (rowA.OnHandQty, rowA.OnOrderQty, rowA.OpenRequestQty, rowA.AvailableQty, rowA.SuggestedQty, rowA.Level, rowA.IsCritical));
        var rowB = result.Items[1];
        Assert.Equal((10m, 10m, "Reorder"), (rowB.ReorderPoint, rowB.SuggestedQty, rowB.Level));

        var critical = await ReadAsync<ReorderSuggestionResult>(await buyer.GetAsync($"/api/reports/reorder-suggestions?categoryId={category.Id}&criticalOnly=true", Ct));
        Assert.Equal([a.Code], critical.Items.Select(r => r.ItemCode));
        var all = await ReadAsync<ReorderSuggestionResult>(await buyer.GetAsync($"/api/reports/reorder-suggestions?categoryId={category.Id}&includeAll=true", Ct));
        Assert.Equal((c.Code, 10m, 0m, "Ok"), all.Items.Where(r => r.ItemCode == c.Code).Select(r => (r.ItemCode, r.OpenRequestQty, r.SuggestedQty, r.Level)).Single());

        // Người chỉ được nhóm hàng khác không thấy các mặt hàng này; thiếu quyền báo cáo kho thì bị chặn.
        var otherCategory = await ReadAsync<ItemCategoryDetailDto>(await head.PostAsJsonAsync("/api/item-categories",
            new CreateItemCategoryRequest(TestCodes.Category(), $"Nhóm khác đặt hàng lại {Guid.NewGuid():N}", null, null), Ct));
        var viewerRole = UniqueCode("V");
        await _factory.CreateRoleAsync(viewerRole, Permissions.Report.StockView);
        var viewerName = $"xv.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(viewerName, company.Code, viewerRole, categoryCodes: [otherCategory.Code]);
        var viewer = await _factory.CreateClientInCompanyAsync(viewerName, ProcApiFactory.TestPassword, company.Code);
        var none = await ReadAsync<ReorderSuggestionResult>(await viewer.GetAsync("/api/reports/reorder-suggestions?includeAll=true", Ct));
        Assert.Empty(none.Items);
        Assert.Equal(HttpStatusCode.Forbidden, (await approver.GetAsync("/api/reports/reorder-suggestions", Ct)).StatusCode);
    }
}
