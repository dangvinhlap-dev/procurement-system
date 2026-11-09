using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Proc.Application.Common;
using Proc.Application.MasterData;
using Proc.Application.Organization;
using Proc.Application.Pricing;
using Proc.Application.Purchasing;
using Proc.Domain.Organization;

namespace Proc.IntegrationTests;

/// <summary>
/// Đặt hàng gộp (lát G7, design.md mục 18.25): Phòng cung ứng chung lập phiếu DHG ở tổng công ty theo phiếu GTH đã duyệt, phân bổ SL cho
/// các công ty trong biên độ so với kế hoạch nhu cầu, "Tạo PO" sinh mỗi công ty một PO nháp giá GTH – một công ty lỗi thì không PO nào.
/// </summary>
[Collection(ApiCollection.Name)]
public class PooledOrderTests
{
    private const string Url = "/api/pooled-orders";
    private const string GthUrl = "/api/monthly-supplier-approvals";
    private readonly ProcApiFactory _factory;

    public PooledOrderTests(ProcApiFactory factory) => _factory = factory;

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

    [Fact]
    public async Task DatGop_PhanBoTrongBienDo_SinhMoiCongTyMotPo_LoiMotCongTyThiKhongPoNao()
    {
        var userName = $"g7.{Guid.NewGuid():N}"[..16];
        await _factory.CreateUserAsync(userName, ProcApiFactory.TestPassword, ["TCT"]);
        var head = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, "TCT");
        var month = new DateOnly(Today.Year, Today.Month, 1);
        async Task<CompanyDetailDto> CompanyAsync(string name) => await ReadAsync<CompanyDetailDto>(await head.PostAsJsonAsync("/api/companies",
            new CreateCompanyRequest(UniqueCode("G"), name, null, null, null, null, month, "KT", "Kho tổng"), Ct));
        var companyA = await CompanyAsync("Công ty A đặt gộp");
        var companyB = await CompanyAsync("Công ty B đặt gộp");
        var clientA = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, companyA.Code);
        var clientB = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, companyB.Code);
        var cai = (await head.GetFromJsonAsync<List<UomDto>>("/api/uoms", Ct))!.Single(u => u.Code == "CAI").Id;
        var category = await ReadAsync<ItemCategoryDetailDto>(await head.PostAsJsonAsync("/api/item-categories",
            new CreateItemCategoryRequest(null, $"Nhóm thử đặt gộp {Guid.NewGuid():N}", null, null), Ct));
        var item = (await ReadAsync<ItemDetailDto>(await head.PostAsJsonAsync("/api/items",
            new CreateItemRequest(null, "Bột giặt G7", null, "Inventory", category.Id, cai, null, null, null, null, "10", null, false, false, null, null, []), Ct))).Id;
        var groupId = (await head.GetFromJsonAsync<List<SupplierGroupDto>>("/api/supplier-groups", Ct))!.Single(g => g.Code == "TN").Id;
        async Task<Guid> SupplierAsync(string name)
        {
            var supplier = await ReadAsync<SupplierDetailDto>(await head.PostAsJsonAsync("/api/suppliers",
                new SaveSupplierRequest(null, groupId, name, null, false, null, null, null, null, null, null, null, null, "Approved", null, null, [], []), Ct));
            await _factory.FinishSupplierApprovalAsync(supplier.Id);
            foreach (var c in new[] { clientA, clientB })
                await ReadAsync<SupplierCompanyDto>(await c.PutAsJsonAsync($"/api/suppliers/{supplier.Id}/company", new SaveSupplierCompanyRequest(null, null, true), Ct));
            return supplier.Id;
        }

        var main = await SupplierAsync("NCC chính G7");
        var backup = await SupplierAsync("NCC dự phòng G7");
        var warehouseA = (await clientA.GetFromJsonAsync<List<WarehouseDetailDto>>("/api/warehouses", Ct))!.Single().Id;
        var warehouseB = (await clientB.GetFromJsonAsync<List<WarehouseDetailDto>>("/api/warehouses", Ct))!.Single().Id;

        // Kế hoạch nhu cầu tháng này: A 60 cái, B 40 cái (đã chốt) – tỷ lệ phân bổ 60/40.
        async Task PlanAsync(HttpClient client, decimal qty)
        {
            var months = Enumerable.Repeat(0m, 12).ToArray();
            months[month.Month - 1] = qty;
            var plan = await ReadAsync<DemandPlanDto>(await client.PostAsJsonAsync("/api/demand-plans",
                new SaveDemandPlanRequest(month.Year, category.Id, null, [new(null, item, cai, months, null)]), Ct));
            await ReadAsync<DemandPlanDto>(await client.PostAsync($"/api/demand-plans/{plan.Id}/confirm", null, Ct));
        }

        await PlanAsync(clientA, 60m);
        await PlanAsync(clientB, 40m);

        // Phiếu GTH tháng cho A, B: NCC chính 12.000, dự phòng 12.500; Ban (người khác) duyệt.
        var gth = await ReadAsync<MonthlyApprovalDto>(await head.PostAsJsonAsync(GthUrl, new SaveMonthlyApprovalRequest(
            month, category.Id, [companyA.Id, companyB.Id], null, [new(null, item, cai, main, 12_000m, backup, 12_500m, null, null)]), Ct));
        await ReadAsync<MonthlyApprovalDto>(await head.PostAsync($"{GthUrl}/{gth.Id}/submit", null, Ct));
        var boardRole = UniqueCode("B");
        await _factory.CreateRoleAsync(boardRole, Permissions.Pricing.MonthlyApprovalView, Permissions.Pricing.MonthlyApprovalApprove);
        var boardUser = $"gb.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(boardUser, "TCT", boardRole);
        var board = await _factory.CreateClientInCompanyAsync(boardUser, ProcApiFactory.TestPassword, "TCT");
        await ReadAsync<MonthlyApprovalDto>(await board.PostAsJsonAsync($"{GthUrl}/{gth.Id}/approve", new ApprovalActionRequest(null), Ct));

        // Chọn phiếu GTH của tháng và NCC; ngữ cảnh: giá dự phòng cho NCC dự phòng, kế hoạch A 60 / B 40, kho Main đứng đầu.
        var approvals = await ReadAsync<List<PooledApprovalOptionDto>>(await head.GetAsync($"{Url}/approvals?date={Today:yyyy-MM-dd}", Ct));
        var option = approvals.Single(a => a.Id == gth.Id);
        Assert.Equal(new[] { backup, main }.Order(), option.Suppliers.Select(s => s.SupplierId).Order());
        var backupContext = await ReadAsync<PooledOrderContextDto>(await head.GetAsync($"{Url}/context?approvalId={gth.Id}&supplierId={backup}", Ct));
        var source = backupContext.Lines.Single();
        Assert.Equal((12_500m, true, 10m), (source.UnitPrice, source.IsBackup, backupContext.MarginPercent));
        Assert.Equal(60m, source.Plans.Single(p => p.CompanyId == companyA.Id).PlanQty);
        Assert.True(backupContext.Companies.Single(c => c.CompanyId == companyA.Id).Warehouses[0].IsMain);

        // Công ty thành viên không lập phiếu đặt gộp.
        SavePooledOrderRequest Request(decimal a, decimal b, Guid? lineId = null) => new(Today, gth.Id, main, Today.AddDays(2), "Giao một lần",
            [new(companyA.Id, warehouseA, null), new(companyB.Id, warehouseB, null)],
            [new(lineId, source.MonthlyApprovalLineId, [new(companyA.Id, a), new(companyB.Id, b)], null, "Giao buổi sáng")]);
        Assert.Equal(HttpStatusCode.Forbidden, (await clientA.PostAsJsonAsync(Url, Request(60, 40), Ct)).StatusCode);
        // Kho phải thuộc đúng công ty.
        Assert.Contains("kho nhận không thuộc công ty", await TitleAsync(await head.PostAsJsonAsync(Url,
            Request(60, 40) with { Companies = [new(companyA.Id, warehouseB, null), new(companyB.Id, warehouseB, null)] }, Ct)));

        // A 75, B 25: đề xuất 60/40 ± 10 → A vượt biên độ: lưu nháp được, cảnh báo trên phiếu, không tạo PO được.
        var order = await ReadAsync<PooledOrderDto>(await head.PostAsJsonAsync(Url, Request(75, 25), Ct));
        var allocationA = order.Lines[0].Allocations.Single(x => x.CompanyId == companyA.Id);
        Assert.Equal(("Draft", 12_000m, (decimal?)60m, true, 10m), (order.Status, order.Lines[0].UnitPrice, allocationA.ExpectedQty, allocationA.IsOutOfMargin, order.Lines[0].Tolerance));
        Assert.Equal(1_200_000m, order.TotalAmount);
        var blocked = await TitleAsync(await head.PostAsync($"{Url}/{order.Id}/create-orders", null, Ct));
        Assert.Contains("vượt biên độ 10%", blocked);
        Assert.Contains($"{companyA.Code}: phân bổ 75, đề xuất theo kế hoạch 60 ± 10", blocked);

        // Không có quyền đặc biệt "Tạo PO cho công ty thành viên": có quyền lập phiếu và tạo PO ở tổng công ty vẫn không tạo được PO cho A, B
        // – nút khóa kèm lý do, API từ chối trước khi tạo PO nào.
        order = await ReadAsync<PooledOrderDto>(await head.PutAsJsonAsync($"{Url}/{order.Id}", Request(68, 32, order.Lines[0].Id), Ct));
        var plainRole = UniqueCode("D");
        await _factory.CreateRoleAsync(plainRole, Permissions.Purchase.PooledOrderView, Permissions.Purchase.PooledOrderManage,
            Permissions.Purchase.PoView, Permissions.Purchase.PoCreate);
        var plainUser = $"g7p.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(plainUser, "TCT", plainRole);
        var plain = await _factory.CreateClientInCompanyAsync(plainUser, ProcApiFactory.TestPassword, "TCT");
        var plainActions = (await ReadAsync<PooledOrderDto>(await plain.GetAsync($"{Url}/{order.Id}", Ct))).Actions;
        Assert.False(plainActions.CanCreateOrders);
        Assert.Contains("quyền đặc biệt", plainActions.CreateOrdersBlockedReason);
        var poCountA = (await ReadAsync<PurchaseDocumentListResult>(await clientA.GetAsync("/api/purchase-orders", Ct))).Items.Count;
        var denied = await plain.PostAsync($"{Url}/{order.Id}/create-orders", null, Ct);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        var deniedTitle = await TitleAsync(denied);
        Assert.Contains("quyền đặc biệt", deniedTitle);
        Assert.Contains(companyA.Code, deniedTitle);
        Assert.Contains(companyB.Code, deniedTitle);

        // Có quyền đặc biệt thì không cần vai trò ở A, B (PO lập thay mặt tổng công ty, kiểm tra theo công ty nhận). B ngừng áp dụng NCC →
        // lỗi ở B, không PO nào được tạo (kể cả của A), phiếu vẫn nháp.
        var specialRole = UniqueCode("E");
        await _factory.CreateRoleAsync(specialRole, Permissions.Purchase.PooledOrderView, Permissions.Purchase.PooledOrderManage,
            Permissions.Purchase.PooledOrderCreateMemberPo);
        var specialUser = $"g7s.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(specialUser, "TCT", specialRole);
        var special = await _factory.CreateClientInCompanyAsync(specialUser, ProcApiFactory.TestPassword, "TCT");
        var specialActions = (await ReadAsync<PooledOrderDto>(await special.GetAsync($"{Url}/{order.Id}", Ct))).Actions;
        Assert.Equal((true, (string?)null), (specialActions.CanCreateOrders, specialActions.CreateOrdersBlockedReason));
        await ReadAsync<SupplierCompanyDto>(await clientB.PutAsJsonAsync($"/api/suppliers/{main}/company", new SaveSupplierCompanyRequest(null, null, false), Ct));
        var failed = await TitleAsync(await special.PostAsync($"{Url}/{order.Id}/create-orders", null, Ct));
        Assert.Contains($"Công ty {companyB.Code}:", failed);
        Assert.Contains("chưa được áp dụng", failed);
        Assert.Equal(poCountA, (await ReadAsync<PurchaseDocumentListResult>(await clientA.GetAsync("/api/purchase-orders", Ct))).Items.Count);
        Assert.Equal("Draft", (await ReadAsync<PooledOrderDto>(await head.GetAsync($"{Url}/{order.Id}", Ct))).Status);
        await ReadAsync<SupplierCompanyDto>(await clientB.PutAsJsonAsync($"/api/suppliers/{main}/company", new SaveSupplierCompanyRequest(null, null, true), Ct));

        // A 68, B 32 trong biên độ, người có quyền đặc biệt tạo PO: mỗi công ty một PO nháp giá GTH, ghi chú dẫn số phiếu đặt gộp; phiếu khóa,
        // không hủy.
        order = await ReadAsync<PooledOrderDto>(await special.PostAsync($"{Url}/{order.Id}/create-orders", null, Ct));
        Assert.Equal(("Ordered", 10m, false), (order.Status, order.MarginPercent, order.Actions.CanEdit));
        var rowA = order.Companies.Single(c => c.CompanyId == companyA.Id);
        Assert.Equal(("Draft", 816_000m), (rowA.PurchaseOrderStatus, rowA.Amount));
        var poA = await ReadAsync<PurchaseOrderDto>(await clientA.GetAsync($"/api/purchase-orders/{rowA.PurchaseOrderId}", Ct));
        Assert.Equal((68m, 12_000m, "MonthlyApproval", main, warehouseA), (poA.Lines[0].Qty, poA.Lines[0].UnitPrice, poA.Lines[0].PriceSource, poA.SupplierId, poA.WarehouseId));
        Assert.Contains(order.No, poA.Note);
        Assert.Equal("Giao buổi sáng", poA.Lines[0].Note);
        var poB = await ReadAsync<PurchaseOrderDto>(await clientB.GetAsync($"/api/purchase-orders/{order.Companies.Single(c => c.CompanyId == companyB.Id).PurchaseOrderId}", Ct));
        Assert.Equal(32m, poB.Lines[0].Qty);
        Assert.Contains("đã tạo PO", await TitleAsync(await head.PostAsJsonAsync($"{Url}/{order.Id}/cancel", new CancelPooledOrderRequest("Sai"), Ct)));

        // Phiếu nháp hủy được, bắt buộc lý do; tra cứu trong danh sách.
        var draft = await ReadAsync<PooledOrderDto>(await head.PostAsJsonAsync(Url, Request(60, 40), Ct));
        Assert.Contains("lý do", await TitleAsync(await head.PostAsJsonAsync($"{Url}/{draft.Id}/cancel", new CancelPooledOrderRequest(" "), Ct)));
        Assert.Equal("Cancelled", (await ReadAsync<PooledOrderDto>(await head.PostAsJsonAsync($"{Url}/{draft.Id}/cancel", new CancelPooledOrderRequest("Lập trùng"), Ct))).Status);
        var list = await ReadAsync<List<PooledOrderListItemDto>>(await head.GetAsync(Url, Ct));
        Assert.Equal((2, 1_200_000m), (list.Single(x => x.Id == order.Id).OrderCount, list.Single(x => x.Id == order.Id).TotalAmount));
    }
}
