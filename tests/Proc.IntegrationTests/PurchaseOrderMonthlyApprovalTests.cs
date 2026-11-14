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
/// PO kiểm tra theo phiếu duyệt NCC và giá tháng (lát G3, design.md mục 18.25): giá phiếu tháng đứng đầu thứ tự nguồn giá; NCC ngoài
/// NCC chính/dự phòng hoặc dòng không có giá đã duyệt phải qua chặng duyệt "Ngoài danh sách/giá" (POL); vượt hạn mức SL tháng gộp các
/// công ty áp dụng thì chặn gửi duyệt.
/// </summary>
[Collection(ApiCollection.Name)]
public class PurchaseOrderMonthlyApprovalTests
{
    private const string GthUrl = "/api/monthly-supplier-approvals";
    private readonly ProcApiFactory _factory;

    public PurchaseOrderMonthlyApprovalTests(ProcApiFactory factory) => _factory = factory;

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
    public async Task GiaPhieuThang_NgoaiDanhSachThemChangDuyet_VuotHanMucBiChan()
    {
        // Quản trị riêng của test: lập phiếu tháng ở tổng công ty, đặt hàng ở công ty mới.
        var userName = $"g3.{Guid.NewGuid():N}"[..16];
        await _factory.CreateUserAsync(userName, ProcApiFactory.TestPassword, ["TCT"]);
        var head = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, "TCT");
        var company = await ReadAsync<CompanyDetailDto>(await head.PostAsJsonAsync("/api/companies",
            new CreateCompanyRequest(UniqueCode("G"), "Công ty thử phiếu tháng", null, null, null, null, new DateOnly(Today.Year, Today.Month, 1), "KT", "Kho tổng"), Ct));
        var client = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, company.Code);
        var cai = (await head.GetFromJsonAsync<List<UomDto>>("/api/uoms", Ct))!.Single(u => u.Code == "CAI").Id;
        var category = await ReadAsync<ItemCategoryDetailDto>(await head.PostAsJsonAsync("/api/item-categories",
            new CreateItemCategoryRequest(TestCodes.Category(), $"Nhóm thử PO phiếu tháng {Guid.NewGuid():N}", null, null), Ct));
        async Task<Guid> ItemAsync(string name) => (await ReadAsync<ItemDetailDto>(await head.PostAsJsonAsync("/api/items",
            new CreateItemRequest(null, name, null, "Inventory", category.Id, cai, null, null, null, null, "10", null, false, false, null, null, []), Ct))).Id;
        var item = await ItemAsync("Dây cáp G3");
        var other = await ItemAsync("Ống nhựa G3");
        var groupId = (await head.GetFromJsonAsync<List<SupplierGroupDto>>("/api/supplier-groups", Ct))!.Single(g => g.Code == "TN").Id;
        async Task<Guid> SupplierAsync(string name)
        {
            var supplier = await ReadAsync<SupplierDetailDto>(await head.PostAsJsonAsync("/api/suppliers",
                new SaveSupplierRequest(null, groupId, name, null, false, null, null, null, null, null, null, null, null, "Approved", null, null, [], []), Ct));
            await _factory.FinishSupplierApprovalAsync(supplier.Id);
            await ReadAsync<SupplierCompanyDto>(await client.PutAsJsonAsync($"/api/suppliers/{supplier.Id}/company", new SaveSupplierCompanyRequest(null, null, true), Ct));
            return supplier.Id;
        }

        var main = await SupplierAsync("NCC chính G3");
        var backup = await SupplierAsync("NCC dự phòng G3");
        var outsider = await SupplierAsync("NCC ngoài danh sách G3");
        var warehouse = (await client.GetFromJsonAsync<List<WarehouseDetailDto>>("/api/warehouses", Ct))!.Single().Id;

        // Phiếu tháng cho công ty mới: NCC chính 12.000, dự phòng 12.500, hạn mức 100 cái, giá chuẩn 12.200, trần 12.600, mục tiêu 11.800;
        // Ban duyệt. Giá duyệt không được vượt trần (G4b).
        SaveMonthlyApprovalRequest Gth(decimal ceiling) => new(new DateOnly(Today.Year, Today.Month, 1), category.Id, [company.Id], null,
            [new(null, item, cai, main, 12_000m, backup, 12_500m, 100m, null, 12_200m, ceiling, 11_800m)],
            "Giá trên mục tiêu, mới 2 NCC – trình Chủ tịch");
        Assert.Contains("giá NCC dự phòng vượt giá trần", await TitleAsync(await head.PostAsJsonAsync(GthUrl, Gth(12_400m), Ct)));
        var gth = await ReadAsync<MonthlyApprovalDto>(await head.PostAsJsonAsync(GthUrl, Gth(12_600m), Ct));
        Assert.Equal(((decimal?)12_200m, (decimal?)12_600m, (decimal?)11_800m), (gth.Lines[0].StandardPrice, gth.Lines[0].CeilingPrice, gth.Lines[0].TargetPrice));
        await ReadAsync<MonthlyApprovalDto>(await head.PostAsync($"{GthUrl}/{gth.Id}/submit", null, Ct));
        var boardRole = UniqueCode("B");
        await _factory.CreateRoleAsync(boardRole, Permissions.Pricing.MonthlyApprovalView, Permissions.Pricing.MonthlyApprovalApprove);
        var boardUser = $"gb.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(boardUser, "TCT", boardRole);
        var board = await _factory.CreateClientInCompanyAsync(boardUser, ProcApiFactory.TestPassword, "TCT");
        await ReadAsync<MonthlyApprovalDto>(await board.PostAsJsonAsync($"{GthUrl}/{gth.Id}/approve", new ApprovalActionRequest(null), Ct));
        await _factory.FinishMonthlyApprovalAsync(gth.Id); // chặng ngoại lệ Chủ tịch/TGĐ (lát H2)

        // Loại duyệt POL có trong APRL.
        Assert.Contains(await ReadAsync<List<ApprovalTypeDto>>(await client.GetAsync("/api/approval-rules/types", Ct)), t => t.Code == "POL");

        // Giá đã duyệt cho NCC chính lấy từ phiếu tháng (đứng đầu thứ tự nguồn giá).
        var prices = await ReadAsync<List<PoApprovedPriceDto>>(await client.GetAsync(
            $"/api/purchase-orders/approved-prices?supplierId={main}&date={Today:yyyy-MM-dd}&itemIds={item}", Ct));
        Assert.Equal(("MonthlyApproval", 12_000m, gth.No), (prices.Single().Source, prices.Single().UnitPrice, prices.Single().PriceListNo));

        SavePurchaseOrderRequest Po(Guid supplier, Guid itemId, decimal qty, decimal price) =>
            new(Today, supplier, warehouse, null, null, null, null, 0, null, [new(null, itemId, cai, qty, price, 0, "10", null, null, null)]);
        async Task<PurchaseOrderDto> SubmitAsync(Guid id) =>
            await ReadAsync<PurchaseOrderDto>(await client.PostAsync($"/api/purchase-orders/{id}/submit", null, Ct));

        // NCC ngoài danh sách tháng: dòng gắn cờ, gửi duyệt qua thêm chặng 4 "Ngoài danh sách/giá". Giá 13.000 vượt trần 12.600 của mặt
        // hàng (áp cả NCC ngoài danh sách): thêm chặng 5 "Vượt giá trần" (Q3, lát H3).
        var offList = await ReadAsync<PurchaseOrderDto>(await client.PostAsJsonAsync("/api/purchase-orders", Po(outsider, item, 1, 13_000), Ct));
        Assert.Equal(("None", true, gth.No, true), (offList.Lines[0].PriceSource, offList.Lines[0].IsOffMonthlyList, offList.Lines[0].MonthlyApprovalNo, offList.IsOffApprovedList));
        Assert.Equal(((decimal?)12_600m, true, true), (offList.Lines[0].CeilingPrice, offList.Lines[0].IsOverCeiling, offList.IsOverCeiling));
        offList = await SubmitAsync(offList.Id);
        Assert.Equal([1, 4, 5], offList.Approvals.Select(s => s.Stage));
        Assert.True(offList.IsOverCeiling);

        // Mặt hàng không có phiếu tháng, không có giá đã duyệt: bắt buộc duyệt (thay quy tắc "chỉ gắn cờ" của mục 18.10).
        var noPrice = await SubmitAsync((await ReadAsync<PurchaseOrderDto>(await client.PostAsJsonAsync("/api/purchase-orders", Po(main, other, 1, 5_000), Ct))).Id);
        Assert.Equal((false, true), (noPrice.Lines[0].IsOffMonthlyList, noPrice.IsOffApprovedList));
        Assert.Contains(noPrice.Approvals, s => s.Stage == 4);

        // NCC chính đúng giá: nguồn "Duyệt tháng", không thêm chặng nào.
        var onList = await ReadAsync<PurchaseOrderDto>(await client.PostAsJsonAsync("/api/purchase-orders", Po(main, item, 60, 12_000), Ct));
        Assert.Equal(("MonthlyApproval", 12_000m, false, gth.No), (onList.Lines[0].PriceSource, onList.Lines[0].ApprovedUnitPrice!.Value, onList.IsOffApprovedList, onList.Lines[0].MonthlyApprovalNo));
        Assert.Equal((false, false), (onList.Lines[0].IsOverCeiling, onList.IsOverCeiling));
        onList = await SubmitAsync(onList.Id);
        Assert.Equal([1], onList.Approvals.Select(s => s.Stage));

        // NCC dự phòng theo giá dự phòng; đã đặt 1 + 60, thêm 50 = 111 > hạn mức 100 → chặn gửi; giảm còn 39 thì gửi được.
        var backupPo = await ReadAsync<PurchaseOrderDto>(await client.PostAsJsonAsync("/api/purchase-orders", Po(backup, item, 50, 12_500), Ct));
        Assert.Equal(("MonthlyApproval", 12_500m, false), (backupPo.Lines[0].PriceSource, backupPo.Lines[0].ApprovedUnitPrice!.Value, backupPo.Lines[0].IsPriceDeviation));
        var blocked = await TitleAsync(await client.PostAsync($"/api/purchase-orders/{backupPo.Id}/submit", null, Ct));
        Assert.Contains("vượt hạn mức tháng", blocked);
        Assert.Contains("đã đặt 61", blocked);
        var line = backupPo.Lines[0];
        await ReadAsync<PurchaseOrderDto>(await client.PutAsJsonAsync($"/api/purchase-orders/{backupPo.Id}",
            Po(backup, item, 39, 12_500) with { Lines = [new(line.Id, item, cai, 39, 12_500, 0, "10", null, null, null)] }, Ct));
        Assert.Equal("Submitted", (await SubmitAsync(backupPo.Id)).Status);
    }

    [Fact]
    public async Task PoVuotGiaTran_LenChuTich_KhongLapLaiKhiChuTichDaDuyetPo()
    {
        var userName = $"h3.{Guid.NewGuid():N}"[..16];
        await _factory.CreateUserAsync(userName, ProcApiFactory.TestPassword, ["TCT"]);
        var head = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, "TCT");
        var company = await ReadAsync<CompanyDetailDto>(await head.PostAsJsonAsync("/api/companies",
            new CreateCompanyRequest(UniqueCode("H"), "Công ty thử vượt giá trần", null, null, null, null, new DateOnly(Today.Year, Today.Month, 1), "KT", "Kho tổng"), Ct));
        var client = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, company.Code);
        var cai = (await head.GetFromJsonAsync<List<UomDto>>("/api/uoms", Ct))!.Single(u => u.Code == "CAI").Id;
        var category = await ReadAsync<ItemCategoryDetailDto>(await head.PostAsJsonAsync("/api/item-categories",
            new CreateItemCategoryRequest(TestCodes.Category(), $"Nhóm thử vượt trần {Guid.NewGuid():N}", null, null), Ct));
        var item = (await ReadAsync<ItemDetailDto>(await head.PostAsJsonAsync("/api/items",
            new CreateItemRequest(null, "Van khóa H3", null, "Inventory", category.Id, cai, null, null, null, null, "10", null, false, false, null, null, []), Ct))).Id;
        var groupId = (await head.GetFromJsonAsync<List<SupplierGroupDto>>("/api/supplier-groups", Ct))!.Single(g => g.Code == "TN").Id;
        async Task<Guid> SupplierAsync(string name)
        {
            var supplier = await ReadAsync<SupplierDetailDto>(await head.PostAsJsonAsync("/api/suppliers",
                new SaveSupplierRequest(null, groupId, name, null, false, null, null, null, null, null, null, null, null, "Approved", null, null, [], []), Ct));
            await _factory.FinishSupplierApprovalAsync(supplier.Id);
            await ReadAsync<SupplierCompanyDto>(await client.PutAsJsonAsync($"/api/suppliers/{supplier.Id}/company", new SaveSupplierCompanyRequest(null, null, true), Ct));
            return supplier.Id;
        }

        var main = await SupplierAsync("NCC chính H3");
        var backup = await SupplierAsync("NCC dự phòng H3");
        var outsider = await SupplierAsync("NCC ngoài danh sách H3");
        var warehouse = (await client.GetFromJsonAsync<List<WarehouseDetailDto>>("/api/warehouses", Ct))!.Single().Id;
        // Phiếu tháng: giá chính 10.000, dự phòng 10.200, trần 10.500 (/cái).
        var gth = await ReadAsync<MonthlyApprovalDto>(await head.PostAsJsonAsync(GthUrl, new SaveMonthlyApprovalRequest(
            new DateOnly(Today.Year, Today.Month, 1), category.Id, [company.Id], null,
            [new(null, item, cai, main, 10_000m, backup, 10_200m, null, null, null, 10_500m, null)], "Mới 2 NCC – trình Chủ tịch"), Ct));
        await ReadAsync<MonthlyApprovalDto>(await head.PostAsync($"{GthUrl}/{gth.Id}/submit", null, Ct));
        await _factory.FinishMonthlyApprovalAsync(gth.Id);

        // Loại duyệt POC có trong APRL.
        Assert.Contains(await ReadAsync<List<ApprovalTypeDto>>(await client.GetAsync("/api/approval-rules/types", Ct)), t => t.Code == "POC");

        // Công ty cấu hình: PO cấp 1 Phó ban (mọi giá trị), cấp 2 Chủ tịch từ 50.000.000; POC cấp 1 Chủ tịch.
        var deputy = await _factory.CreateRoleAsync(UniqueCode("P"), Permissions.Purchase.PoApprove);
        var chairman = await _factory.CreateRoleAsync(UniqueCode("C"), Permissions.Purchase.PoApprove);
        await ReadAsync<ApprovalRuleDto>(await client.PostAsJsonAsync("/api/approval-rules", new SaveApprovalRuleRequest("PO", 1, 0, deputy), Ct));
        await ReadAsync<ApprovalRuleDto>(await client.PostAsJsonAsync("/api/approval-rules", new SaveApprovalRuleRequest("PO", 2, 50_000_000, chairman), Ct));
        await ReadAsync<ApprovalRuleDto>(await client.PostAsJsonAsync("/api/approval-rules", new SaveApprovalRuleRequest("POC", 1, 0, chairman), Ct));

        SavePurchaseOrderRequest Po(decimal qty, decimal price) =>
            new(Today, outsider, warehouse, null, null, null, null, 0, null, [new(null, item, cai, qty, price, 0, "10", null, null, null)]);
        async Task<PurchaseOrderDto> SubmitAsync(decimal qty, decimal price) => await ReadAsync<PurchaseOrderDto>(await client.PostAsync(
            $"/api/purchase-orders/{(await ReadAsync<PurchaseOrderDto>(await client.PostAsJsonAsync("/api/purchase-orders", Po(qty, price), Ct))).Id}/submit", null, Ct));

        // PO nhỏ (10 × 11.000 > trần 10.500): chặng PO chỉ Phó ban → thêm chặng 5 Chủ tịch.
        var small = await SubmitAsync(10, 11_000);
        Assert.True(small.IsOverCeiling);
        var chairmanName = small.Approvals.Single(s => s.Stage == 5).RoleName;
        Assert.Equal([(1, 1), (4, 1), (5, 1)], small.Approvals.Select(s => (s.Stage, s.Level)));
        Assert.NotNull(chairmanName);

        // PO lớn (5.000 × 11.000 = 55.000.000 trước VAT): chặng PO đã có Chủ tịch ở cấp 2 → không thêm chặng 5 (mỗi người một cấp – R3).
        var large = await SubmitAsync(5_000, 11_000);
        Assert.Equal([(1, 1), (1, 2), (4, 1)], large.Approvals.Select(s => (s.Stage, s.Level)));
        Assert.Equal(chairmanName, large.Approvals.Single(s => s.Stage == 1 && s.Level == 2).RoleName);

        // Giá không vượt trần (10.400 ≤ 10.500): không có chặng 5.
        var under = await SubmitAsync(10, 10_400);
        Assert.False(under.IsOverCeiling);
        Assert.DoesNotContain(under.Approvals, s => s.Stage == 5);
    }

}
