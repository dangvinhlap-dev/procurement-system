using System.Net.Http.Json;
using Proc.Application.Common;
using Proc.Application.Inventory;
using Proc.Application.MasterData;
using Proc.Application.Organization;
using Proc.Application.Pricing;
using Proc.Application.Purchasing;
using Proc.Application.Reports;
using Proc.Domain.Organization;

namespace Proc.IntegrationTests;

/// <summary>
/// Báo cáo tiết kiệm so giá chuẩn (lát G4c, design.md mục 18.25): dòng PO đã duyệt bằng VND có dòng phiếu duyệt tháng có giá chuẩn;
/// tiết kiệm = giá trị theo giá chuẩn − thành tiền, kèm giá mục tiêu, số dòng vượt trần, tỷ lệ giá trị mua có giá chuẩn.
/// </summary>
[Collection(ApiCollection.Name)]
public class StandardSavingsReportTests
{
    private const string GthUrl = "/api/monthly-supplier-approvals";
    private readonly ProcApiFactory _factory;

    public StandardSavingsReportTests(ProcApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string UniqueCode(string prefix) => $"{prefix}{Guid.NewGuid():N}".ToUpperInvariant()[..10];

    private static DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTime.UtcNow, VietnamTime.Zone));

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(Ct)}");
        return (await response.Content.ReadFromJsonAsync<T>(Ct))!;
    }

    [Fact]
    public async Task TietKiemSoGiaChuan_TinhTheoDongPoDaDuyet_CoGiaMucTieuVaVuotTran()
    {
        var userName = $"g4c.{Guid.NewGuid():N}"[..16];
        await _factory.CreateUserAsync(userName, ProcApiFactory.TestPassword, ["TCT"]);
        var head = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, "TCT");
        var company = await ReadAsync<CompanyDetailDto>(await head.PostAsJsonAsync("/api/companies",
            new CreateCompanyRequest(UniqueCode("S"), "Công ty thử tiết kiệm giá chuẩn", null, null, null, null, new DateOnly(Today.Year, Today.Month, 1), "KT", "Kho tổng"), Ct));
        var client = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, company.Code);
        var cai = (await head.GetFromJsonAsync<List<UomDto>>("/api/uoms", Ct))!.Single(u => u.Code == "CAI").Id;
        var category = await ReadAsync<ItemCategoryDetailDto>(await head.PostAsJsonAsync("/api/item-categories",
            new CreateItemCategoryRequest(null, $"Nhóm thử tiết kiệm giá chuẩn {Guid.NewGuid():N}", null, null), Ct));
        async Task<Guid> ItemAsync(string name) => (await ReadAsync<ItemDetailDto>(await head.PostAsJsonAsync("/api/items",
            new CreateItemRequest(null, name, null, "Inventory", category.Id, cai, null, null, null, null, "10", null, false, false, null, null, []), Ct))).Id;
        var item = await ItemAsync("Dây cáp G4c");
        var other = await ItemAsync("Ống nhựa G4c");
        var groupId = (await head.GetFromJsonAsync<List<SupplierGroupDto>>("/api/supplier-groups", Ct))!.Single(g => g.Code == "TN").Id;
        async Task<Guid> SupplierAsync(string name)
        {
            var supplier = await ReadAsync<SupplierDetailDto>(await head.PostAsJsonAsync("/api/suppliers",
                new SaveSupplierRequest(null, groupId, name, null, false, null, null, null, null, null, null, null, null, "Approved", null, null, [], []), Ct));
            await _factory.FinishSupplierApprovalAsync(supplier.Id);
            await ReadAsync<SupplierCompanyDto>(await client.PutAsJsonAsync($"/api/suppliers/{supplier.Id}/company", new SaveSupplierCompanyRequest(null, null, true), Ct));
            return supplier.Id;
        }

        var main = await SupplierAsync("NCC chính G4c");
        var backup = await SupplierAsync("NCC dự phòng G4c");
        var outsider = await SupplierAsync("NCC ngoài danh sách G4c");
        var warehouse = (await client.GetFromJsonAsync<List<WarehouseDetailDto>>("/api/warehouses", Ct))!.Single().Id;

        // Phiếu tháng: chính 12.000, dự phòng 12.500, giá chuẩn 12.200, trần 12.600, mục tiêu 11.800; Ban duyệt.
        var gth = await ReadAsync<MonthlyApprovalDto>(await head.PostAsJsonAsync(GthUrl, new SaveMonthlyApprovalRequest(
            new DateOnly(Today.Year, Today.Month, 1), category.Id, [company.Id], null,
            [new(null, item, cai, main, 12_000m, backup, 12_500m, null, null, 12_200m, 12_600m, 11_800m)], "Giá trên mục tiêu"), Ct));
        await ReadAsync<MonthlyApprovalDto>(await head.PostAsync($"{GthUrl}/{gth.Id}/submit", null, Ct));
        var boardRole = UniqueCode("B");
        await _factory.CreateRoleAsync(boardRole, Permissions.Pricing.MonthlyApprovalView, Permissions.Pricing.MonthlyApprovalApprove);
        var boardUser = $"sb.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(boardUser, "TCT", boardRole);
        var board = await _factory.CreateClientInCompanyAsync(boardUser, ProcApiFactory.TestPassword, "TCT");
        await ReadAsync<MonthlyApprovalDto>(await board.PostAsJsonAsync($"{GthUrl}/{gth.Id}/approve", new ApprovalActionRequest(null), Ct));
        await _factory.FinishMonthlyApprovalAsync(gth.Id); // chặng ngoại lệ Chủ tịch/TGĐ (lát H2)

        // Người duyệt PO của công ty (người lập không tự duyệt).
        var poRole = UniqueCode("D");
        await _factory.CreateRoleAsync(poRole, Permissions.Purchase.PoView, Permissions.Purchase.PoApprove);
        var approverName = $"sd.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(approverName, company.Code, poRole);
        var approver = await _factory.CreateClientInCompanyAsync(approverName, ProcApiFactory.TestPassword, company.Code);
        // Mỗi cấp/chặng một người duyệt khác nhau (lát R3, design.md mục 18.26): hai người cùng vai trò duyệt luân phiên.
        var approverName2 = $"sd.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(approverName2, company.Code, poRole);
        var approver2 = await _factory.CreateClientInCompanyAsync(approverName2, ProcApiFactory.TestPassword, company.Code);

        async Task<PurchaseOrderDto> DraftAsync(Guid supplier, Guid itemId, decimal qty, decimal price) =>
            await ReadAsync<PurchaseOrderDto>(await client.PostAsJsonAsync("/api/purchase-orders",
                new SavePurchaseOrderRequest(Today, supplier, warehouse, null, null, null, null, 0, null, [new(null, itemId, cai, qty, price, 0, "10", null, null, null)]), Ct));
        async Task ApprovedAsync(Guid supplier, Guid itemId, decimal qty, decimal price)
        {
            var po = await DraftAsync(supplier, itemId, qty, price);
            po = await ReadAsync<PurchaseOrderDto>(await client.PostAsync($"/api/purchase-orders/{po.Id}/submit", null, Ct));
            for (var i = 0; i < 5 && po.Status == "Submitted"; i++)
                po = await ReadAsync<PurchaseOrderDto>(await (i % 2 == 0 ? approver : approver2).PostAsJsonAsync($"/api/purchase-orders/{po.Id}/approve", new ApprovalActionRequest(null), Ct));
            Assert.Equal("Approved", po.Status);
        }

        // Chính 60 × 12.000 (chuẩn 732.000, mục tiêu 708.000); dự phòng 40 × 12.500 (chuẩn 488.000, mục tiêu 472.000); ngoài danh sách
        // 10 × 13.000 vượt trần (chuẩn 122.000, mục tiêu 118.000); mặt hàng không có phiếu tháng 1 × 5.000; PO nháp không tính.
        await ApprovedAsync(main, item, 60, 12_000);
        await ApprovedAsync(backup, item, 40, 12_500);
        await ApprovedAsync(outsider, item, 10, 13_000);
        await ApprovedAsync(main, other, 1, 5_000);
        await DraftAsync(main, item, 5, 12_000);

        var report = await ReadAsync<StandardSavingResult>(await client.GetAsync(
            $"/api/reports/standard-savings?from={Today:yyyy-MM-dd}&to={Today:yyyy-MM-dd}&categoryId={category.Id}", Ct));
        // Thành tiền 1.350.000 so chuẩn 1.342.000 → tiết kiệm −8.000 = −0,60%; có giá chuẩn 1.350.000 ÷ 1.355.000 = 99,63%.
        Assert.Equal((1_355_000m, 1_350_000m, (decimal?)99.63m, 1_342_000m, -8_000m, (decimal?)-0.60m),
            (report.TotalAmount, report.CoveredAmount, report.CoveragePct, report.StandardAmount, report.Saving, report.SavingPct));
        var row = Assert.Single(report.Items);
        Assert.Equal((item, 110m, 12_272.727273m, 12_200m, 3, gth.No), (row.ItemId, row.Qty, row.AvgPrice, row.StandardAvgPrice, row.OrderCount, row.ApprovalNos));
        // Mục tiêu 1.298.000 → còn cách 52.000; một dòng vượt trần 12.600.
        Assert.Equal(((decimal?)1_298_000m, (decimal?)52_000m, 1), (row.TargetAmount, row.TargetGap, row.OverCeilingLines));

        // Kỳ không có PO: không lỗi, không dòng.
        var empty = await ReadAsync<StandardSavingResult>(await client.GetAsync(
            $"/api/reports/standard-savings?from={Today.AddYears(-1):yyyy-MM-dd}&to={Today.AddYears(-1):yyyy-MM-dd}", Ct));
        Assert.Equal((0m, (decimal?)null, 0), (empty.TotalAmount, empty.CoveragePct, empty.Items.Count));
    }
}
