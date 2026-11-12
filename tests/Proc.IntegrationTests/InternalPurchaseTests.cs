using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Proc.Application.Common;
using Proc.Application.MasterData;
using Proc.Application.Organization;
using Proc.Application.Pricing;
using Proc.Application.Purchasing;
using Proc.Application.Reports;
using Proc.Domain.Organization;

namespace Proc.IntegrationTests;

/// <summary>
/// Mua nội bộ qua PO (lát X7, design.md mục 19.3, 19.4): PO đặt cho NCC nội bộ lấy giá bán nội bộ của công ty bán làm gợi ý (nhập tay được
/// – không qua chặng ngoại lệ giá), công ty bán thấy đơn đã duyệt ở "Đơn bán nội bộ", xác nhận / từ chối, hai bên nhận thông báo.
/// </summary>
[Collection(ApiCollection.Name)]
public class InternalPurchaseTests
{
    private readonly ProcApiFactory _factory;

    public InternalPurchaseTests(ProcApiFactory factory) => _factory = factory;

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
    public async Task PoChoNccNoiBo_GiaNoiBoGoiY_NhapTay_BenBanXacNhan_TuChoi()
    {
        // Hai công ty mới: S bán, B mua. Mặt hàng (cái, thùng = 12 cái). NCC nội bộ đại diện S, áp dụng tại B; giá bán nội bộ S → B 15.000/cái.
        var userName = $"mn.{Guid.NewGuid():N}"[..16];
        await _factory.CreateUserAsync(userName, ProcApiFactory.TestPassword, ["TCT"]);
        var head = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, "TCT");
        async Task<CompanyDetailDto> CompanyAsync(string name) => await ReadAsync<CompanyDetailDto>(await head.PostAsJsonAsync("/api/companies",
            new CreateCompanyRequest(UniqueCode("N"), name, null, null, null, null, new DateOnly(Today.Year, Today.Month, 1), "KT", "Kho tổng"), Ct));
        var seller = await CompanyAsync("Công ty bán nội bộ");
        var buyer = await CompanyAsync("Công ty mua nội bộ");
        var s = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, seller.Code);
        var b = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, buyer.Code);
        var uoms = (await head.GetFromJsonAsync<List<UomDto>>("/api/uoms", Ct))!;
        var (cai, thung) = (uoms.Single(u => u.Code == "CAI").Id, uoms.Single(u => u.Code == "THUNG").Id);
        var category = await ReadAsync<ItemCategoryDetailDto>(await head.PostAsJsonAsync("/api/item-categories",
            new CreateItemCategoryRequest(null, $"Nhóm mua nội bộ {Guid.NewGuid():N}", null, null), Ct));
        var item = await ReadAsync<ItemDetailDto>(await head.PostAsJsonAsync("/api/items",
            new CreateItemRequest(null, "Bu lông nội bộ", null, "Inventory", category.Id, cai, null, null, null, null, "10", null, false, false,
                null, null, [new(thung, 12)]), Ct));
        var groupId = (await head.GetFromJsonAsync<List<SupplierGroupDto>>("/api/supplier-groups", Ct))!.Single(g => g.Code == "TN").Id;
        async Task<Guid> SupplierAsync(string name, Guid internalCompanyId)
        {
            var created = await ReadAsync<SupplierDetailDto>(await head.PostAsJsonAsync("/api/suppliers",
                new SaveSupplierRequest(null, groupId, name, null, false, null, null, null, null, null, null, null, null, "Approved", null,
                    internalCompanyId, [], []), Ct));
            await _factory.FinishSupplierApprovalAsync(created.Id);
            await ReadAsync<SupplierCompanyDto>(await b.PutAsJsonAsync($"/api/suppliers/{created.Id}/company", new SaveSupplierCompanyRequest(null, null, true), Ct));
            return created.Id;
        }

        var supplierId = await SupplierAsync("NCC nội bộ – công ty bán", seller.Id);
        var ownSupplierId = await SupplierAsync("NCC nội bộ – chính công ty mua", buyer.Id);
        await ReadAsync<InternalPriceDto>(await s.PostAsJsonAsync("/api/internal-prices",
            new SaveInternalPriceRequest(buyer.Id, item.Id, 15_000, "10", Today.AddDays(-10), null, null), Ct));
        var warehouse = (await b.GetFromJsonAsync<List<WarehouseDetailDto>>("/api/warehouses", Ct))!.Single().Id;
        SavePurchaseOrderRequest Po(Guid supplier, params SavePurchaseOrderLineRequest[] lines) =>
            new(Today, supplier, warehouse, null, null, null, null, 0, null, lines);
        SavePurchaseOrderLineRequest Line(decimal qty, decimal price, Guid? uom = null) => new(null, item.Id, uom ?? cai, qty, price, 0, "10", null, null, null);

        // Giao diện tự điền: bậc giá nội bộ theo ĐVT cơ sở, nguồn Internal.
        var tiers = await ReadAsync<List<PoApprovedPriceDto>>(await b.GetAsync(
            $"/api/purchase-orders/approved-prices?supplierId={supplierId}&date={Today:yyyy-MM-dd}&itemIds={item.Id}", Ct));
        var tier = Assert.Single(tiers);
        Assert.Equal(("Internal", 15_000m, cai, "10", $"Giá nội bộ {seller.Code}"), (tier.Source, tier.UnitPrice, tier.UomId, tier.VatCode, tier.PriceListNo));

        // Không tự đặt mua của chính mình.
        Assert.Contains("chính công ty", await TitleAsync(await b.PostAsJsonAsync("/api/purchase-orders", Po(ownSupplierId, Line(1, 15_000)), Ct)));

        // Nhập tay 16.000/cái (khác giá gợi ý) và thùng theo giá gợi ý quy đổi 180.000: không lệch giá, không ngoài danh sách.
        var po = await ReadAsync<PurchaseOrderDto>(await b.PostAsJsonAsync("/api/purchase-orders", Po(supplierId, Line(10, 16_000), Line(1, 180_000, thung)), Ct));
        Assert.Equal(["Internal", "Internal"], po.Lines.Select(l => l.PriceSource));
        Assert.Equal([15_000m, 180_000m], po.Lines.Select(l => l.ApprovedUnitPrice));
        Assert.False(po.HasPriceException || po.IsOutOfContract || po.IsOffApprovedList);
        Assert.Equal((seller.Code, "NotSent"), (po.InternalSale!.SellerCompanyCode, po.InternalSale.Status));

        // Người quản lý đơn bán ở S; người duyệt PO ở B.
        var managerRole = UniqueCode("R");
        await _factory.CreateRoleAsync(managerRole, Permissions.Purchase.InternalSaleView, Permissions.Purchase.InternalSaleManage);
        var managerName = $"bn.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(managerName, seller.Code, managerRole);
        var manager = await _factory.CreateClientInCompanyAsync(managerName, ProcApiFactory.TestPassword, seller.Code);
        var approverRole = UniqueCode("R");
        await _factory.CreateRoleAsync(approverRole, Permissions.Purchase.PoView, Permissions.Purchase.PoApprove);
        var approverName = $"dn.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(approverName, buyer.Code, approverRole);
        var approver = await _factory.CreateClientInCompanyAsync(approverName, ProcApiFactory.TestPassword, buyer.Code);

        async Task<PurchaseOrderDto> SubmitApproveAsync(Guid id)
        {
            await ReadAsync<PurchaseOrderDto>(await b.PostAsync($"/api/purchase-orders/{id}/submit", null, Ct));
            return await ReadAsync<PurchaseOrderDto>(await approver.PostAsJsonAsync($"/api/purchase-orders/{id}/approve", new ApprovalActionRequest(null), Ct));
        }

        // Chưa duyệt thì bên bán chưa thấy.
        Assert.DoesNotContain((await ReadAsync<InternalSaleListResult>(await manager.GetAsync("/api/internal-sales", Ct))).Items, x => x.OrderId == po.Id);
        po = await SubmitApproveAsync(po.Id);
        Assert.Equal(("Approved", "Pending"), (po.Status, po.InternalSale!.Status));
        var note = Assert.Single(await NotificationApi.MineAsync(manager, Ct), n => n.TargetId == po.Id);
        Assert.Equal(("InternalSaleOrder", "DBN"), (note.Type, note.ScreenCode));

        var row = Assert.Single((await ReadAsync<InternalSaleListResult>(await manager.GetAsync("/api/internal-sales", Ct))).Items, x => x.OrderId == po.Id);
        Assert.Equal((buyer.Code, "Pending", 2, 374_000m), (row.BuyerCompanyCode, row.Status, row.LineCount, row.Amount));
        // Công ty mua không thấy đơn của mình trong "Đơn bán nội bộ" (không phải bên bán), cũng không mở được chi tiết.
        Assert.DoesNotContain((await ReadAsync<InternalSaleListResult>(await b.GetAsync("/api/internal-sales", Ct))).Items, x => x.OrderId == po.Id);
        Assert.Equal(HttpStatusCode.NotFound, (await b.GetAsync($"/api/internal-sales/{po.Id}", Ct)).StatusCode);
        // Người duyệt PO không có quyền đơn bán.
        Assert.Equal(HttpStatusCode.Forbidden, (await approver.GetAsync("/api/internal-sales", Ct)).StatusCode);

        var sale = await ReadAsync<InternalSaleDto>(await manager.GetAsync($"/api/internal-sales/{po.Id}", Ct));
        Assert.True(sale.CanDecide);
        Assert.Equal([16_000m, 180_000m], sale.Lines.Select(l => l.UnitPrice));
        Assert.Contains("lý do", await TitleAsync(await manager.PostAsJsonAsync($"/api/internal-sales/{po.Id}/reject", new InternalSaleDecisionRequest(" "), Ct)));
        sale = await ReadAsync<InternalSaleDto>(await manager.PostAsJsonAsync($"/api/internal-sales/{po.Id}/confirm", new InternalSaleDecisionRequest("Giao trong tuần"), Ct));
        Assert.Equal(("Confirmed", "Giao trong tuần", false), (sale.Status, sale.Note, sale.CanDecide));
        Assert.Contains("đã được xác nhận", await TitleAsync(await manager.PostAsJsonAsync($"/api/internal-sales/{po.Id}/reject", new InternalSaleDecisionRequest("Đổi ý"), Ct)));

        // Công ty mua thấy trạng thái bên bán trên PO và người lập nhận thông báo.
        po = await ReadAsync<PurchaseOrderDto>(await b.GetAsync($"/api/purchase-orders/{po.Id}", Ct));
        Assert.Equal(("Confirmed", "Giao trong tuần", $"Người thử {managerName}"), (po.InternalSale!.Status, po.InternalSale.Note, po.InternalSale.DecidedByName));
        Assert.Contains(await NotificationApi.MineAsync(b, Ct), n => n.TargetId == po.Id && n.Type == "InternalSaleDecision");

        // Đơn thứ hai: công ty bán từ chối, bắt buộc lý do.
        var second = await ReadAsync<PurchaseOrderDto>(await b.PostAsJsonAsync("/api/purchase-orders", Po(supplierId, Line(5, 15_000)), Ct));
        await SubmitApproveAsync(second.Id);
        sale = await ReadAsync<InternalSaleDto>(await manager.PostAsJsonAsync($"/api/internal-sales/{second.Id}/reject", new InternalSaleDecisionRequest("Hết hàng"), Ct));
        Assert.Equal(("Rejected", "Hết hàng"), (sale.Status, sale.Note));
        second = await ReadAsync<PurchaseOrderDto>(await b.GetAsync($"/api/purchase-orders/{second.Id}", Ct));
        Assert.Equal("Rejected", second.InternalSale!.Status);
        var rejected = Assert.Single((await ReadAsync<InternalSaleListResult>(await manager.GetAsync("/api/internal-sales?status=Rejected", Ct))).Items);
        Assert.Equal(second.Id, rejected.OrderId);

        // Báo cáo tỷ lệ mua theo giá đã duyệt (PRCW): dòng mua nội bộ là nguồn "Internal", nhập tay khác giá gợi ý không tính lệch giá.
        var compliance = await ReadAsync<ContractComplianceResult>(await b.GetAsync(
            $"/api/reports/contract-compliance?from={Today:yyyy-MM-dd}&to={Today:yyyy-MM-dd}", Ct));
        Assert.Equal(["Internal"], compliance.Sources.Select(x => x.Source));
        Assert.Equal(100m, compliance.CompliancePct);
        Assert.Empty(compliance.Lines);
    }
}
