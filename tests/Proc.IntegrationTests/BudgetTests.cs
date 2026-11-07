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
/// Ngân sách bộ phận × tháng (lát C3 – design.md mục 18.24): khai ngân sách, tính đã dùng (PO + phần PR chưa lên PO, không đếm trùng),
/// PR/PO vượt ngân sách không bị chặn mà qua thêm chặng duyệt "vượt ngân sách" (PRB/POB).
/// </summary>
[Collection(ApiCollection.Name)]
public class BudgetTests
{
    private readonly ProcApiFactory _factory;

    public BudgetTests(ProcApiFactory factory) => _factory = factory;

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

    private sealed record Fixture(HttpClient Client, string CompanyCode, Guid ItemId, Guid Cai, Guid SupplierId, Guid WarehouseId, Guid Px1, Guid Px2);

    /// <summary>
    /// Công ty mới; bộ phận PX1, PX2; một mặt hàng đã nhập kho giá 1.000.000/cái (để PR có đơn giá tham khảo); một NCC đã duyệt, áp
    /// dụng tại công ty. Người thao tác là quản trị của công ty.
    /// </summary>
    private async Task<Fixture> ArrangeAsync()
    {
        var userName = $"ns.{Guid.NewGuid():N}"[..16];
        await _factory.CreateUserAsync(userName, ProcApiFactory.TestPassword, ["TCT"]);
        var head = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, "TCT");
        var company = await ReadAsync<CompanyDetailDto>(await head.PostAsJsonAsync("/api/companies",
            new CreateCompanyRequest(UniqueCode("N"), "Công ty thử ngân sách", null, null, null, null, new DateOnly(Today.Year, Today.Month, 1), "KT", "Kho tổng"), Ct));
        var client = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, company.Code);
        var cai = (await head.GetFromJsonAsync<List<UomDto>>("/api/uoms", Ct))!.Single(u => u.Code == "CAI").Id;
        var category = await ReadAsync<ItemCategoryDetailDto>(await head.PostAsJsonAsync("/api/item-categories",
            new CreateItemCategoryRequest(null, $"Nhóm thử ngân sách {Guid.NewGuid():N}", null, null), Ct));
        var item = await ReadAsync<ItemDetailDto>(await head.PostAsJsonAsync("/api/items",
            new CreateItemRequest(null, "Động cơ 5kW", null, "Inventory", category.Id, cai, null, null, null, null, "10", null, false, false, null, null, []), Ct));
        var groupId = (await head.GetFromJsonAsync<List<SupplierGroupDto>>("/api/supplier-groups", Ct))!.Single(g => g.Code == "TN").Id;
        var supplier = await ReadAsync<SupplierDetailDto>(await head.PostAsJsonAsync("/api/suppliers",
            new SaveSupplierRequest(null, groupId, "NCC thử ngân sách", null, false, null, null, null, null, null, null, null, null, "Approved", null, null, [], []), Ct));
        await _factory.FinishSupplierApprovalAsync(supplier.Id);
        await ReadAsync<SupplierCompanyDto>(await client.PutAsJsonAsync($"/api/suppliers/{supplier.Id}/company", new SaveSupplierCompanyRequest(null, null, true), Ct));
        var warehouse = (await client.GetFromJsonAsync<List<WarehouseDetailDto>>("/api/warehouses", Ct))!.Single().Id;
        var calendar = await ReadAsync<InventoryCalendarDto>(await client.PostAsJsonAsync("/api/inventory-periods/years", new CreateInventoryYearRequest(Today.Year), Ct));
        await ReadAsync<InventoryPeriodDto>(await client.PostAsync($"/api/inventory-periods/{calendar.Periods.Single(p => p.Month == Today.Month).Id}/open", null, Ct));
        var grn = await ReadAsync<InventoryDocumentDto>(await client.PostAsJsonAsync("/api/goods-receipts",
            new SaveReceiptRequest(Today, warehouse, supplier.Id, null, null, null, null, null, null, [new(null, item.Id, cai, 1, 1_000_000, "10", null, null, null)]), Ct));
        await ReadAsync<InventoryDocumentDto>(await client.PostAsync($"/api/inventory-documents/{grn.Id}/post", null, Ct));
        var px1 = await ReadAsync<DepartmentDto>(await client.PostAsJsonAsync("/api/departments", new CreateDepartmentRequest("PX1", "Phân xưởng 1", null), Ct));
        var px2 = await ReadAsync<DepartmentDto>(await client.PostAsJsonAsync("/api/departments", new CreateDepartmentRequest("PX2", "Phân xưởng 2", null), Ct));
        return new Fixture(client, company.Code, item.Id, cai, supplier.Id, warehouse, px1.Id, px2.Id);
    }

    /// <summary>12 tháng, chỉ tháng hiện tại có ngân sách.</summary>
    private static SaveDepartmentBudgetRequest ThisMonth(decimal amount) =>
        new(Enumerable.Range(1, 12).Select(m => m == Today.Month ? amount : (decimal?)null).ToList());

    private static SavePurchaseRequestRequest Pr(Fixture f, Guid departmentId, decimal qty) =>
        new(Today, departmentId, "Bảo trì", null, f.WarehouseId, "Normal", null, [new(null, f.ItemId, f.Cai, qty, null, null, null)]);

    private async Task<HttpClient> ApproverAsync(Fixture f, params string[] permissions)
    {
        var role = UniqueCode("R");
        await _factory.CreateRoleAsync(role, permissions);
        var userName = $"dn.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(userName, f.CompanyCode, role);
        return await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, f.CompanyCode);
    }

    [Fact]
    public async Task NganSach_LuuTheoNam_KiemTraDuLieu_QuyenXem()
    {
        var f = await ArrangeAsync();
        var url = $"/api/budgets/{Today.Year}/{f.Px1}";

        var missing = await f.Client.PutAsJsonAsync(url, new SaveDepartmentBudgetRequest([1, 2, 3]), Ct);
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Equal("Ngân sách phải gửi đủ 12 tháng.", await TitleAsync(missing));
        Assert.Equal(HttpStatusCode.BadRequest, (await f.Client.PutAsJsonAsync(url, ThisMonth(-1), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await f.Client.PutAsJsonAsync($"/api/budgets/{Today.Year}/{Guid.NewGuid()}", ThisMonth(1), Ct)).StatusCode);

        var saved = await ReadAsync<DepartmentBudgetYearDto>(await f.Client.PutAsJsonAsync(url, ThisMonth(10_000_000.555m), Ct));
        Assert.Equal((10_000_000.56m, 0m, 10_000_000.56m), (saved.Months[Today.Month - 1].Budget, saved.Months[Today.Month - 1].Used, saved.TotalBudget));
        Assert.Equal(11, saved.Months.Count(m => m.Budget is null));

        // Bỏ trống tháng = thôi kiểm soát ngân sách tháng đó.
        saved = await ReadAsync<DepartmentBudgetYearDto>(await f.Client.PutAsJsonAsync(url, new SaveDepartmentBudgetRequest(new decimal?[12]), Ct));
        Assert.All(saved.Months, m => Assert.Null(m.Budget));
        await ReadAsync<DepartmentBudgetYearDto>(await f.Client.PutAsJsonAsync(url, ThisMonth(5_000_000), Ct));

        var year = await ReadAsync<List<DepartmentBudgetYearDto>>(await f.Client.GetAsync($"/api/budgets/{Today.Year}", Ct));
        Assert.Equal(["PX1", "PX2"], year.Select(r => r.DepartmentCode));
        Assert.Equal(5_000_000m, year[0].TotalBudget);

        // Chỉ quyền xem: đọc được, không sửa được.
        var viewer = await ApproverAsync(f, Permissions.Purchase.BudgetView);
        Assert.Equal(2, (await ReadAsync<List<DepartmentBudgetYearDto>>(await viewer.GetAsync($"/api/budgets/{Today.Year}", Ct))).Count);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PutAsJsonAsync(url, ThisMonth(1), Ct)).StatusCode);
    }

    [Fact]
    public async Task DaDung_KhongDemTrungPrPo_VuotNganSach_ThemChangDuyet()
    {
        var f = await ArrangeAsync();
        await ReadAsync<DepartmentBudgetYearDto>(await f.Client.PutAsJsonAsync($"/api/budgets/{Today.Year}/{f.Px1}", ThisMonth(10_000_000), Ct));
        // Chặng vượt ngân sách do vai trò riêng duyệt (loại PRB, POB ở APRL).
        var budgetRole = UniqueCode("B");
        await _factory.CreateRoleAsync(budgetRole, Permissions.Purchase.PrView, Permissions.Purchase.PrApprove, Permissions.Purchase.PoView, Permissions.Purchase.PoApprove);
        var budgetRoleId = await _factory.GetRoleIdAsync(budgetRole);
        var types = await ReadAsync<List<ApprovalTypeDto>>(await f.Client.GetAsync("/api/approval-rules/types", Ct));
        Assert.Contains(types, t => t.Code == "PRB");
        Assert.Contains(types, t => t.Code == "POB");
        await ReadAsync<ApprovalRuleDto>(await f.Client.PostAsJsonAsync("/api/approval-rules", new SaveApprovalRuleRequest("PRB", 1, 1_000_000_000, budgetRoleId), Ct));
        await ReadAsync<ApprovalRuleDto>(await f.Client.PostAsJsonAsync("/api/approval-rules", new SaveApprovalRuleRequest("POB", 1, 1_000_000_000, budgetRoleId), Ct));
        var approver = await ApproverAsync(f, Permissions.Purchase.PrView, Permissions.Purchase.PrApprove, Permissions.Purchase.PoView, Permissions.Purchase.PoApprove);

        // PR1: 6 × 1.000.000 = 6.000.000 trong ngân sách 10.000.000 → chỉ chặng thường.
        var pr1 = await ReadAsync<PurchaseRequestDto>(await f.Client.PostAsJsonAsync("/api/purchase-requests", Pr(f, f.Px1, 6), Ct));
        Assert.Equal((10_000_000m, 0m, 6_000_000m, false), (pr1.Budget!.BudgetAmount, pr1.Budget.UsedAmount, pr1.Budget.DocumentAmount, pr1.Budget.IsOver));
        pr1 = await ReadAsync<PurchaseRequestDto>(await f.Client.PostAsync($"/api/purchase-requests/{pr1.Id}/submit", null, Ct));
        Assert.Equal([1], pr1.Approvals.Select(s => s.Stage));
        pr1 = await ReadAsync<PurchaseRequestDto>(await approver.PostAsJsonAsync($"/api/purchase-requests/{pr1.Id}/approve", new ApprovalActionRequest(null), Ct));
        Assert.Equal("Approved", pr1.Status);

        // PO đặt 4/6 cái của PR1: đã dùng (không gồm PO) = phần PR còn lại 2.000.000; PO 4.000.000 → 6.000.000, không vượt.
        var po = await ReadAsync<PurchaseOrderDto>(await f.Client.PostAsJsonAsync("/api/purchase-orders",
            new SavePurchaseOrderRequest(Today, f.SupplierId, f.WarehouseId, null, null, null, null, 0, null,
                [new(null, f.ItemId, f.Cai, 4, 1_000_000, 0, "10", null, null, pr1.Lines[0].Id)]), Ct));
        Assert.Equal(f.Px1, po.DepartmentId);
        Assert.Equal((2_000_000m, 4_000_000m, false), (po.Budget!.UsedAmount, po.Budget.DocumentAmount, po.Budget.IsOver));
        po = await ReadAsync<PurchaseOrderDto>(await f.Client.PostAsync($"/api/purchase-orders/{po.Id}/submit", null, Ct));
        Assert.Equal("Submitted", po.Status);

        // Đã dùng tháng = PO 4.000.000 + PR1 còn 2 cái 2.000.000 = 6.000.000 (phần đã lên PO không đếm hai lần).
        var month = (await ReadAsync<List<DepartmentBudgetYearDto>>(await f.Client.GetAsync($"/api/budgets/{Today.Year}", Ct)))
            .Single(r => r.DepartmentId == f.Px1).Months[Today.Month - 1];
        Assert.Equal((6_000_000m, 4_000_000m), (month.Used, month.Remaining));

        // PR2: 5.000.000 → 6.000.000 + 5.000.000 > 10.000.000: lưu nháp, gửi duyệt được nhưng thêm chặng vượt ngân sách (chặng 3).
        var pr2 = await ReadAsync<PurchaseRequestDto>(await f.Client.PostAsJsonAsync("/api/purchase-requests", Pr(f, f.Px1, 5), Ct));
        Assert.Equal((6_000_000m, true, 1_000_000m), (pr2.Budget!.UsedAmount, pr2.Budget.IsOver, pr2.Budget.OverAmount));
        pr2 = await ReadAsync<PurchaseRequestDto>(await f.Client.PostAsync($"/api/purchase-requests/{pr2.Id}/submit", null, Ct));
        Assert.Equal([(1, false), (3, true)], pr2.Approvals.Select(s => (s.Stage, s.RoleName is not null)));
        pr2 = await ReadAsync<PurchaseRequestDto>(await approver.PostAsJsonAsync($"/api/purchase-requests/{pr2.Id}/approve", new ApprovalActionRequest(null), Ct));
        Assert.Equal("Submitted", pr2.Status);
        Assert.Contains("vai trò", await TitleAsync(await approver.PostAsJsonAsync($"/api/purchase-requests/{pr2.Id}/approve", new ApprovalActionRequest(null), Ct)));
        var budgetUser = $"nb.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(budgetUser, f.CompanyCode, budgetRole);
        var budgetClient = await _factory.CreateClientInCompanyAsync(budgetUser, ProcApiFactory.TestPassword, f.CompanyCode);
        pr2 = await ReadAsync<PurchaseRequestDto>(await budgetClient.PostAsJsonAsync($"/api/purchase-requests/{pr2.Id}/approve", new ApprovalActionRequest("Đồng ý vượt"), Ct));
        Assert.Equal("Approved", pr2.Status);

        // PO thêm 1 cái ngoài PR: đã dùng 11.000.000 + 1.000.000 > ngân sách → chặng vượt ngân sách POB.
        var po2 = await ReadAsync<PurchaseOrderDto>(await f.Client.PostAsJsonAsync("/api/purchase-orders",
            new SavePurchaseOrderRequest(Today, f.SupplierId, f.WarehouseId, null, null, null, null, 0, null,
                [new(null, f.ItemId, f.Cai, 1, 1_000_000, 0, "10", null, null, null)], f.Px1), Ct));
        Assert.Equal((11_000_000m, true), (po2.Budget!.UsedAmount, po2.Budget.IsOver));
        po2 = await ReadAsync<PurchaseOrderDto>(await f.Client.PostAsync($"/api/purchase-orders/{po2.Id}/submit", null, Ct));
        Assert.Contains(po2.Approvals, s => s.Stage == 3);

        // Bộ phận chưa khai ngân sách, PO không chọn bộ phận: không kiểm tra.
        var pr3 = await ReadAsync<PurchaseRequestDto>(await f.Client.PostAsJsonAsync("/api/purchase-requests", Pr(f, f.Px2, 50), Ct));
        Assert.Null(pr3.Budget);
        pr3 = await ReadAsync<PurchaseRequestDto>(await f.Client.PostAsync($"/api/purchase-requests/{pr3.Id}/submit", null, Ct));
        Assert.DoesNotContain(pr3.Approvals, s => s.Stage == 3);
    }
}
