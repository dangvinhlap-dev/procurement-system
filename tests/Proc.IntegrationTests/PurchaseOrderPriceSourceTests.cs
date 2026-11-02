using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Proc.Application.Common;
using Proc.Application.Inventory;
using Proc.Application.MasterData;
using Proc.Application.Organization;
using Proc.Application.Pricing;
using Proc.Application.Purchasing;
using Proc.Domain.Organization;

namespace Proc.IntegrationTests;

/// <summary>
/// PO theo giá đã duyệt (lát C1 – design.md mục 18.10): nguồn giá hợp đồng/bảng giá/ngoài hợp đồng, lệch giá bắt buộc lý do và qua chặng
/// duyệt ngoại lệ (loại POX).
/// </summary>
[Collection(ApiCollection.Name)]
public class PurchaseOrderPriceSourceTests
{
    private readonly ProcApiFactory _factory;

    public PurchaseOrderPriceSourceTests(ProcApiFactory factory) => _factory = factory;

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

    private sealed record Fixture(
        HttpClient Head, HttpClient Client, string CompanyCode, Guid ItemId, Guid Cai, Guid Thung, Guid SupplierId, Guid NoPriceSupplierId,
        Guid PriceListId, string PriceListNo, Guid WarehouseId);

    /// <summary>
    /// Công ty mới; mặt hàng (cái, thùng = 12 cái); NCC A và NCC C đã duyệt, áp dụng tại công ty; bảng giá toàn tập đoàn của A đã hiệu
    /// lực (cái 50.000 CK 5%, bậc 1.000 cái 48.000 CK 5%; thùng 600.000). NCC C không có bảng giá.
    /// </summary>
    private async Task<Fixture> ArrangeAsync()
    {
        var userName = $"ng.{Guid.NewGuid():N}"[..16];
        await _factory.CreateUserAsync(userName, ProcApiFactory.TestPassword, ["TCT"]);
        var head = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, "TCT");
        var company = await ReadAsync<CompanyDetailDto>(await head.PostAsJsonAsync("/api/companies",
            new CreateCompanyRequest(UniqueCode("G"), "Công ty thử nguồn giá", null, null, null, null, new DateOnly(Today.Year, Today.Month, 1), "KT", "Kho tổng"), Ct));
        var client = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, company.Code);
        var uoms = (await head.GetFromJsonAsync<List<UomDto>>("/api/uoms", Ct))!;
        var (cai, thung) = (uoms.Single(u => u.Code == "CAI").Id, uoms.Single(u => u.Code == "THUNG").Id);
        var category = await ReadAsync<ItemCategoryDetailDto>(await head.PostAsJsonAsync("/api/item-categories",
            new CreateItemCategoryRequest(null, "Nhóm nguồn giá", null, null), Ct));
        var item = await ReadAsync<ItemDetailDto>(await head.PostAsJsonAsync("/api/items",
            new CreateItemRequest(null, "Vòng bi 6207", null, "Inventory", category.Id, cai, null, null, null, null, "10", null, false, false,
                null, null, [new(thung, 12)]), Ct));
        var groupId = (await head.GetFromJsonAsync<List<SupplierGroupDto>>("/api/supplier-groups", Ct))!.Single(g => g.Code == "TN").Id;
        async Task<Guid> SupplierAsync(string name)
        {
            var s = await ReadAsync<SupplierDetailDto>(await head.PostAsJsonAsync("/api/suppliers",
                new SaveSupplierRequest(null, groupId, name, null, false, null, null, null, null, null, null, null, null, "Approved", null, null, [], []), Ct));
            await _factory.FinishSupplierApprovalAsync(s.Id);
            await ReadAsync<SupplierCompanyDto>(await client.PutAsJsonAsync($"/api/suppliers/{s.Id}/company", new SaveSupplierCompanyRequest(null, null, true), Ct));
            return s.Id;
        }

        var a = await SupplierAsync("NCC nguồn giá A");
        var c = await SupplierAsync("NCC nguồn giá C");
        var list = await ReadAsync<PriceListDto>(await head.PostAsJsonAsync("/api/price-lists",
            new SavePriceListRequest(a, "Bảng giá vòng bi tập đoàn", true, Today, Today.AddYears(1), null, null,
                [new(null, item.Id, cai, 0, 50_000, 5, null, null), new(null, item.Id, cai, 1_000, 48_000, 5, null, null), new(null, item.Id, thung, 0, 600_000, 0, null, null)]), Ct));
        await ReadAsync<PriceListDto>(await head.PostAsync($"/api/price-lists/{list.Id}/submit", null, Ct));
        await _factory.FinishPriceListApprovalAsync(list.Id);
        var warehouse = (await client.GetFromJsonAsync<List<WarehouseDetailDto>>("/api/warehouses", Ct))!.Single().Id;
        return new Fixture(head, client, company.Code, item.Id, cai, thung, a, c, list.Id, list.No, warehouse);
    }

    private static SavePurchaseOrderRequest Po(Fixture f, Guid supplierId, params SavePurchaseOrderLineRequest[] lines) =>
        new(Today, supplierId, f.WarehouseId, null, null, null, null, 0, null, lines);

    private static SavePurchaseOrderLineRequest Line(Fixture f, decimal qty, decimal price, decimal discount, string? reason = null, Guid? uomId = null) =>
        new(null, f.ItemId, uomId ?? f.Cai, qty, price, discount, "10", null, null, null, reason);

    /// <summary>Người dùng có quyền xem và duyệt PO (và hợp đồng), có thể gán vai trò cho trước.</summary>
    private async Task<HttpClient> ApproverAsync(Fixture f, string roleCode)
    {
        var userName = $"dg.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(userName, f.CompanyCode, roleCode);
        return await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, f.CompanyCode);
    }

    private async Task<string> RoleAsync()
    {
        var code = UniqueCode("R");
        await _factory.CreateRoleAsync(code, Permissions.Purchase.PoView, Permissions.Purchase.PoApprove, Permissions.Purchase.ContractView,
            Permissions.Purchase.ContractApprove);
        return code;
    }

    [Fact]
    public async Task NguonGia_BangGia_HopDong_NgoaiHopDong()
    {
        var f = await ArrangeAsync();
        var prices = await ReadAsync<List<PoApprovedPriceDto>>(await f.Client.GetAsync(
            $"/api/purchase-orders/approved-prices?supplierId={f.SupplierId}&date={Today:yyyy-MM-dd}&itemIds={f.ItemId}", Ct));
        Assert.Equal(3, prices.Count);
        Assert.All(prices, p => Assert.Equal(("PriceList", f.PriceListNo, (string?)null), (p.Source, p.PriceListNo, p.ContractNo)));

        // Bảng giá: đúng giá bậc 0 cái; 1.200 cái lấy bậc 1.000 (48.000); 2 thùng có bậc riêng 600.000. Giá 47.500 CK 0% = 50.000 CK 5%.
        var po = await ReadAsync<PurchaseOrderDto>(await f.Client.PostAsJsonAsync("/api/purchase-orders",
            Po(f, f.SupplierId, Line(f, 10, 50_000, 5), Line(f, 1_200, 48_000, 5), Line(f, 2, 600_000, 0, uomId: f.Thung), Line(f, 5, 47_500, 0, "Không cần")), Ct));
        Assert.All(po.Lines, l => Assert.Equal(("PriceList", f.PriceListNo, false, false), (l.PriceSource, l.PriceListNo, l.IsPriceDeviation, l.IsOutOfContract)));
        Assert.Equal([50_000m, 48_000m, 600_000m, 50_000m], po.Lines.Select(l => l.ApprovedUnitPrice!.Value));
        Assert.Null(po.Lines[3].PriceDeviationReason);
        Assert.Equal((false, false), (po.HasPriceException, po.IsOutOfContract));

        // NCC không có bảng giá: ngoài hợp đồng, không phải ngoại lệ giá (không có giá đã duyệt để lệch).
        var outside = await ReadAsync<PurchaseOrderDto>(await f.Client.PostAsJsonAsync("/api/purchase-orders", Po(f, f.NoPriceSupplierId, Line(f, 1, 70_000, 0)), Ct));
        Assert.Equal(("None", (decimal?)null, true), (outside.Lines[0].PriceSource, outside.Lines[0].ApprovedUnitPrice, outside.Lines[0].IsOutOfContract));
        Assert.Equal((false, true), (outside.HasPriceException, outside.IsOutOfContract));

        // Hợp đồng nguyên tắc hiệu lực dùng bảng giá đó → nguồn chuyển sang Hợp đồng khi lưu lại.
        var role = await RoleAsync();
        var contract = await ReadAsync<ContractDto>(await f.Client.PostAsJsonAsync("/api/contracts", new SaveContractRequest(
            f.SupplierId, f.PriceListId, "HĐNT 02/2026", "Hợp đồng vòng bi", Today, Today, Today.AddMonths(6), null, null, null, null, null, null, null,
            null, null, [], [new SaveContractLineRequest(null, f.ItemId, f.Cai, 1_000, null, null)]), Ct));
        await ReadAsync<ContractDto>(await f.Client.PostAsync($"/api/contracts/{contract.Id}/submit", null, Ct));
        var approver = await ApproverAsync(f, role);
        contract = await ReadAsync<ContractDto>(await approver.PostAsJsonAsync($"/api/contracts/{contract.Id}/approve", new ApprovalActionRequest(null), Ct));
        Assert.Equal("Active", contract.Status);

        prices = await ReadAsync<List<PoApprovedPriceDto>>(await f.Client.GetAsync(
            $"/api/purchase-orders/approved-prices?supplierId={f.SupplierId}&date={Today:yyyy-MM-dd}&itemIds={f.ItemId}", Ct));
        Assert.All(prices, p => Assert.Equal(("Contract", contract.No), (p.Source, p.ContractNo)));
        po = await ReadAsync<PurchaseOrderDto>(await f.Client.PutAsJsonAsync($"/api/purchase-orders/{po.Id}",
            Po(f, f.SupplierId, po.Lines.Select(l => new SavePurchaseOrderLineRequest(l.Id, l.ItemId, l.UomId, l.Qty, l.UnitPrice, l.DiscountPct, l.VatCode,
                null, null, null)).ToArray()), Ct));
        Assert.All(po.Lines, l => Assert.Equal(("Contract", contract.No, f.PriceListNo), (l.PriceSource, l.ContractNo, l.PriceListNo)));

        // Danh sách PO có cờ ngoài hợp đồng / ngoại lệ giá.
        var listResult = await ReadAsync<PurchaseDocumentListResult>(await f.Client.GetAsync("/api/purchase-orders", Ct));
        Assert.Equal((false, false), (listResult.Items.Single(i => i.Id == po.Id).OutOfContract, listResult.Items.Single(i => i.Id == po.Id).PriceException));
        Assert.True(listResult.Items.Single(i => i.Id == outside.Id).OutOfContract);
    }

    [Fact]
    public async Task LechGia_BatBuocLyDo_QuaChangDuyetNgoaiLe()
    {
        var f = await ArrangeAsync();
        // Chặng ngoại lệ do vai trò riêng duyệt (cấu hình loại POX ở APRL).
        var exceptionRole = await RoleAsync();
        var types = await ReadAsync<List<ApprovalTypeDto>>(await f.Client.GetAsync("/api/approval-rules/types", Ct));
        Assert.Contains(types, t => t.Code == "POX");
        await ReadAsync<ApprovalRuleDto>(await f.Client.PostAsJsonAsync("/api/approval-rules",
            new SaveApprovalRuleRequest("POX", 1, 1_000_000_000, await _factory.GetRoleIdAsync(exceptionRole)), Ct));

        // Giá 52.000 CK 5% khác giá bảng giá 50.000 CK 5% → lệch giá, ngoài hợp đồng; lưu nháp được, gửi duyệt thiếu lý do bị chặn.
        var po = await ReadAsync<PurchaseOrderDto>(await f.Client.PostAsJsonAsync("/api/purchase-orders",
            Po(f, f.SupplierId, Line(f, 10, 50_000, 5), Line(f, 10, 52_000, 5)), Ct));
        Assert.Equal([false, true], po.Lines.Select(l => l.IsPriceDeviation));
        Assert.Equal((true, true), (po.HasPriceException, po.IsOutOfContract));
        Assert.Contains("Dòng 2: đơn giá khác giá đã duyệt", await TitleAsync(await f.Client.PostAsync($"/api/purchase-orders/{po.Id}/submit", null, Ct)));

        po = await ReadAsync<PurchaseOrderDto>(await f.Client.PutAsJsonAsync($"/api/purchase-orders/{po.Id}",
            Po(f, f.SupplierId, Line(f, 10, 50_000, 5), Line(f, 10, 52_000, 5, "NCC tăng giá thép, chờ đàm phán lại")), Ct));
        Assert.Equal("NCC tăng giá thép, chờ đàm phán lại", po.Lines[1].PriceDeviationReason);
        po = await ReadAsync<PurchaseOrderDto>(await f.Client.PostAsync($"/api/purchase-orders/{po.Id}/submit", null, Ct));
        // Chưa cấu hình cấp PO thường → một cấp ai có quyền duyệt; chặng 2 ngoại lệ: dưới ngưỡng POX vẫn qua cấp thấp nhất.
        Assert.Equal([(1, 1, (string?)null), (2, 1, "pox")], po.Approvals.Select(s => (s.Stage, s.Level, s.RoleName is null ? null : "pox")));

        var plainRole = UniqueCode("P");
        await _factory.CreateRoleAsync(plainRole, Permissions.Purchase.PoView, Permissions.Purchase.PoApprove);
        var plain = await ApproverAsync(f, plainRole);
        po = await ReadAsync<PurchaseOrderDto>(await plain.PostAsJsonAsync($"/api/purchase-orders/{po.Id}/approve", new ApprovalActionRequest(null), Ct));
        Assert.Equal("Submitted", po.Status);
        Assert.Contains("vai trò", await TitleAsync(await plain.PostAsJsonAsync($"/api/purchase-orders/{po.Id}/approve", new ApprovalActionRequest(null), Ct)));
        var exceptionApprover = await ApproverAsync(f, exceptionRole);
        po = await ReadAsync<PurchaseOrderDto>(await exceptionApprover.PostAsJsonAsync($"/api/purchase-orders/{po.Id}/approve", new ApprovalActionRequest("Chấp nhận tạm"), Ct));
        Assert.Equal("Approved", po.Status);

        // Đúng giá đã duyệt thì lý do cũ bị xóa, không qua chặng ngoại lệ.
        var normal = await ReadAsync<PurchaseOrderDto>(await f.Client.PostAsJsonAsync("/api/purchase-orders",
            Po(f, f.SupplierId, Line(f, 10, 50_000, 5, "Lý do thừa")), Ct));
        Assert.Null(normal.Lines[0].PriceDeviationReason);
        normal = await ReadAsync<PurchaseOrderDto>(await f.Client.PostAsync($"/api/purchase-orders/{normal.Id}/submit", null, Ct));
        Assert.Equal([1], normal.Approvals.Select(s => s.Stage));
    }
}
