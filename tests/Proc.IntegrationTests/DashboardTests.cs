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
/// Trang chủ và số đếm cây điều hướng (design.md mục 11.4): số liệu của công ty đang làm việc; phần không có quyền là null; số đếm theo
/// đúng phạm vi nhóm hàng của người xem.
/// </summary>
[Collection(ApiCollection.Name)]
public class DashboardTests
{
    private readonly ProcApiFactory _factory;

    public DashboardTests(ProcApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string UniqueCode(string prefix) => $"{prefix}{Guid.NewGuid():N}".ToUpperInvariant()[..10];

    private static DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTime.UtcNow, VietnamTime.Zone));

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(Ct)}");
        return (await response.Content.ReadFromJsonAsync<T>(Ct))!;
    }

    [Fact]
    public async Task TrangChu_SoLieuTheoQuyenVaPhamVi()
    {
        var userName = $"tc.{Guid.NewGuid():N}"[..16];
        await _factory.CreateUserAsync(userName, ProcApiFactory.TestPassword, ["TCT"]);
        var head = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, "TCT");
        var company = await ReadAsync<CompanyDetailDto>(await head.PostAsJsonAsync("/api/companies",
            new CreateCompanyRequest(UniqueCode("H"), "Công ty thử trang chủ", null, null, null, null, new DateOnly(Today.Year, Today.Month, 1), "KT", "Kho tổng"), Ct));
        var buyer = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, company.Code);
        var calendar = await ReadAsync<InventoryCalendarDto>(await buyer.PostAsJsonAsync("/api/inventory-periods/years", new CreateInventoryYearRequest(Today.Year), Ct));
        await ReadAsync<InventoryPeriodDto>(await buyer.PostAsync($"/api/inventory-periods/{calendar.Periods.Single(p => p.Month == Today.Month).Id}/open", null, Ct));
        var kt = (await buyer.GetFromJsonAsync<List<WarehouseDetailDto>>("/api/warehouses", Ct))!.Single().Id;
        var cai = (await head.GetFromJsonAsync<List<UomDto>>("/api/uoms", Ct))!.Single(u => u.Code == "CAI").Id;
        var category = await ReadAsync<ItemCategoryDetailDto>(await head.PostAsJsonAsync("/api/item-categories",
            new CreateItemCategoryRequest(null, "Nhóm trang chủ", null, null), Ct));
        // Tồn tối thiểu 100, công ty chưa có tồn → dưới mức.
        var item = await ReadAsync<ItemDetailDto>(await head.PostAsJsonAsync("/api/items",
            new CreateItemRequest(null, "Dầu thủy lực", null, "Inventory", category.Id, cai, null, null, null, null, "10", null, false, false, 100, null, []), Ct));
        var groupId = (await head.GetFromJsonAsync<List<SupplierGroupDto>>("/api/supplier-groups", Ct))!.Single(g => g.Code == "TN").Id;
        var supplier = await ReadAsync<SupplierDetailDto>(await head.PostAsJsonAsync("/api/suppliers",
            new SaveSupplierRequest(null, groupId, "NCC trang chủ", null, false, null, null, null, null, null, null, null, null, "Approved", null, null, [], []), Ct));
        await ReadAsync<SupplierCompanyDto>(await buyer.PutAsJsonAsync($"/api/suppliers/{supplier.Id}/company", new SaveSupplierCompanyRequest(null, null, true), Ct));
        var approverRole = UniqueCode("A");
        await _factory.CreateRoleAsync(approverRole, Permissions.Purchase.PrView, Permissions.Purchase.PrApprove, Permissions.Purchase.PoView, Permissions.Purchase.PoApprove);
        var approverName = $"dy.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(approverName, company.Code, approverRole);
        var approver = await _factory.CreateClientInCompanyAsync(approverName, ProcApiFactory.TestPassword, company.Code);

        // PR chờ duyệt; PO đã duyệt hẹn giao 2 ngày trước (trễ); phiếu nhập nháp; phiếu nhập đã ghi sổ 2 × 50.000 trong tháng.
        var pr = await ReadAsync<PurchaseRequestDto>(await buyer.PostAsJsonAsync("/api/purchase-requests",
            new SavePurchaseRequestRequest(Today, null, null, Today.AddDays(7), kt, "Normal", null, [new(null, item.Id, cai, 5, null, null, null)]), Ct));
        await ReadAsync<PurchaseRequestDto>(await buyer.PostAsync($"/api/purchase-requests/{pr.Id}/submit", null, Ct));
        var po = await ReadAsync<PurchaseOrderDto>(await buyer.PostAsJsonAsync("/api/purchase-orders",
            new SavePurchaseOrderRequest(Today.AddDays(-10), supplier.Id, kt, Today.AddDays(-2), null, null, null, 0, null,
                [new(null, item.Id, cai, 10, 50_000, 0, "10", null, null, null)]), Ct));
        await ReadAsync<PurchaseOrderDto>(await buyer.PostAsync($"/api/purchase-orders/{po.Id}/submit", null, Ct));
        await ReadAsync<PurchaseOrderDto>(await approver.PostAsJsonAsync($"/api/purchase-orders/{po.Id}/approve", new ApprovalActionRequest(null), Ct));
        await ReadAsync<InventoryDocumentDto>(await buyer.PostAsJsonAsync("/api/goods-receipts",
            new SaveReceiptRequest(Today, kt, supplier.Id, null, null, null, null, null, null, [new(null, item.Id, cai, 1, 50_000, "10", null, null, null)]), Ct));
        var posted = await ReadAsync<InventoryDocumentDto>(await buyer.PostAsJsonAsync("/api/goods-receipts",
            new SaveReceiptRequest(Today, kt, supplier.Id, null, null, null, null, null, null, [new(null, item.Id, cai, 2, 50_000, "10", null, null, null)]), Ct));
        await ReadAsync<InventoryDocumentDto>(await buyer.PostAsync($"/api/inventory-documents/{posted.Id}/post", null, Ct));

        // Người lập (quản trị): đủ mọi phần.
        var dashboard = await ReadAsync<DashboardDto>(await buyer.GetAsync("/api/dashboard", Ct));
        Assert.Equal(Today, dashboard.Today);
        Assert.Equal((1, 0, 1, 1), (dashboard.Purchase.PendingRequests!.Value, dashboard.Purchase.OverdueRequests!.Value,
            dashboard.Purchase.OpenOrders!.Value, dashboard.Purchase.LateOrders!.Value));
        Assert.Equal(1, dashboard.Stock!.DraftDocuments["GRN"]);
        Assert.Equal(("Open", $"{Today.Month:00}/{Today.Year}"), (dashboard.Stock.PeriodStatus, dashboard.Stock.PeriodLabel));
        Assert.Equal(100_000m, dashboard.Spend!.ThisMonth);
        Assert.Equal([(supplier.Code, 100_000m)], dashboard.Spend.TopSuppliers.Select(s => (s.Code, s.NetAmount)));
        Assert.Equal(new NavCountDto(1, true), dashboard.NavCounts["POTRK"]);
        Assert.Equal(new NavCountDto(1, false), dashboard.NavCounts["GRN"]);
        Assert.Equal(new NavCountDto(1, false), dashboard.NavCounts["PR"]);
        Assert.False(dashboard.NavCounts.ContainsKey("RTV"));

        // Người duyệt: có PR chờ mình duyệt; không có quyền kho, báo cáo → các phần đó null.
        var forApprover = await ReadAsync<DashboardDto>(await approver.GetAsync("/api/dashboard", Ct));
        Assert.Equal(1, forApprover.Purchase.PendingApprovals);
        Assert.Equal(new NavCountDto(1, true), forApprover.NavCounts["TODO"]);
        Assert.Null(forApprover.Stock);
        Assert.Null(forApprover.Spend);

        // Thủ kho chỉ xem tồn, chỉ nhóm hàng mới: đúng 1 mặt hàng dưới mức; không thấy phần mua hàng, chứng từ kho.
        var keeperRole = UniqueCode("K");
        await _factory.CreateRoleAsync(keeperRole, Permissions.Inventory.StockView);
        var keeperName = $"tk.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(keeperName, company.Code, keeperRole, categoryCodes: [category.Code]);
        var keeper = await _factory.CreateClientInCompanyAsync(keeperName, ProcApiFactory.TestPassword, company.Code);
        var forKeeper = await ReadAsync<DashboardDto>(await keeper.GetAsync("/api/dashboard", Ct));
        Assert.Equal((0, (int?)null, (int?)null), (forKeeper.Purchase.PendingApprovals, forKeeper.Purchase.PendingRequests, forKeeper.Purchase.OpenOrders));
        Assert.Equal(1, forKeeper.Stock!.BelowMinItems);
        Assert.Empty(forKeeper.Stock.DraftDocuments);
        Assert.Null(forKeeper.Stock.ExpiredLots);
        Assert.Equal(new NavCountDto(1, true), forKeeper.NavCounts["STK"]);
    }
}
