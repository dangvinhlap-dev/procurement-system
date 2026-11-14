using System.Net.Http.Json;
using Proc.Application.Common;
using Proc.Application.MasterData;
using Proc.Application.Organization;
using Proc.Application.Purchasing;
using Proc.Domain.Organization;

namespace Proc.IntegrationTests;

/// <summary>
/// Mã hàng theo NCC và ĐVT mua trên chứng từ (design.md mục 5.9): PR/PO/báo giá RFQ tra qua <c>/api/supplier-items/lookup</c>; máy chủ
/// chép mã NCC vào dòng PO lúc lưu/gửi, PO đã gửi giữ nguyên mã khi danh mục NCC – sản phẩm đổi sau đó.
/// </summary>
[Collection(ApiCollection.Name)]
public class PurchaseOrderSupplierItemTests
{
    private readonly ProcApiFactory _factory;

    public PurchaseOrderSupplierItemTests(ProcApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string UniqueCode(string prefix) => $"{prefix}{Guid.NewGuid():N}".ToUpperInvariant()[..10];

    private static DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTime.UtcNow, VietnamTime.Zone));

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(Ct)}");
        return (await response.Content.ReadFromJsonAsync<T>(Ct))!;
    }

    [Fact]
    public async Task PO_LayDvtMua_ChepMaHangNcc_GiuMaKhiDaGui()
    {
        // Công ty mới; vòng bi (cái, thùng = 12 cái); NCC A bán theo thùng, mã của A là "VB6207-A"; NCC B chưa khai báo mặt hàng.
        var userName = $"mn.{Guid.NewGuid():N}"[..16];
        await _factory.CreateUserAsync(userName, ProcApiFactory.TestPassword, ["TCT"]);
        var head = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, "TCT");
        var company = await ReadAsync<CompanyDetailDto>(await head.PostAsJsonAsync("/api/companies",
            new CreateCompanyRequest(UniqueCode("M"), "Công ty thử mã hàng NCC", null, null, null, null, new DateOnly(Today.Year, Today.Month, 1), "KT", "Kho tổng"), Ct));
        var client = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, company.Code);
        var uoms = (await head.GetFromJsonAsync<List<UomDto>>("/api/uoms", Ct))!;
        var (cai, thung) = (uoms.Single(u => u.Code == "CAI").Id, uoms.Single(u => u.Code == "THUNG").Id);
        var category = await ReadAsync<ItemCategoryDetailDto>(await TestCategories.PostLeafAsync(head,
            new CreateItemCategoryRequest(TestCodes.Category(), $"Nhóm mã hàng NCC {Guid.NewGuid():N}", null, null), Ct));
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

        var a = await SupplierAsync("NCC mã hàng A");
        var b = await SupplierAsync("NCC mã hàng B");
        var link = await ReadAsync<SupplierItemDto>(await head.PostAsJsonAsync($"/api/suppliers/{a}/items",
            new SaveSupplierItemRequest(item.Id, " VB6207-A ", thung, null, null, false), Ct));
        var warehouse = (await client.GetFromJsonAsync<List<WarehouseDetailDto>>("/api/warehouses", Ct))!.Single().Id;

        // Người lập phiếu chỉ có quyền PR (không xem danh mục NCC) vẫn tra được ĐVT mua và mã NCC; người bị giới hạn nhóm hàng khác thì không
        // thấy mặt hàng ngoài phạm vi (CLAUDE.md mục 4).
        var prRole = UniqueCode("R");
        await _factory.CreateRoleAsync(prRole, Permissions.Purchase.PrView);
        var prUserName = $"xp.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(prUserName, company.Code, prRole);
        var prUser = await _factory.CreateClientInCompanyAsync(prUserName, ProcApiFactory.TestPassword, company.Code);
        var lookupUrl = $"/api/supplier-items/lookup?supplierIds={a}&supplierIds={b}";
        var found = await ReadAsync<List<SupplierItemLookupDto>>(await prUser.GetAsync(lookupUrl, Ct));
        Assert.Equal(new SupplierItemLookupDto(a, item.Id, "VB6207-A", thung), Assert.Single(found));
        Assert.Single(await ReadAsync<List<SupplierItemLookupDto>>(await prUser.GetAsync($"{lookupUrl}&itemIds={item.Id}", Ct)));
        Assert.Empty(await ReadAsync<List<SupplierItemLookupDto>>(await prUser.GetAsync($"{lookupUrl}&itemIds={Guid.NewGuid()}", Ct)));
        var otherCategory = await ReadAsync<ItemCategoryDetailDto>(await TestCategories.PostLeafAsync(head,
            new CreateItemCategoryRequest(TestCodes.Category(), $"Nhóm ngoài phạm vi {Guid.NewGuid():N}", null, null), Ct));
        var limitedName = $"kp.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(limitedName, company.Code, prRole, categoryCodes: [otherCategory.Code]);
        var limited = await _factory.CreateClientInCompanyAsync(limitedName, ProcApiFactory.TestPassword, company.Code);
        Assert.Empty(await ReadAsync<List<SupplierItemLookupDto>>(await limited.GetAsync(lookupUrl, Ct)));

        // Lưu PO với NCC A: dòng có mã NCC (đã bỏ khoảng trắng); PO với NCC B: không có mã.
        SavePurchaseOrderRequest Po(Guid supplierId) =>
            new(Today, supplierId, warehouse, null, null, null, null, 0, null, [new(null, item.Id, thung, 2, 600_000, 0, "10", null, null, null, "Chưa có bảng giá")]);
        var poA = await ReadAsync<PurchaseOrderDto>(await client.PostAsJsonAsync("/api/purchase-orders", Po(a), Ct));
        Assert.Equal("VB6207-A", Assert.Single(poA.Lines).SupplierItemCode);
        var poB = await ReadAsync<PurchaseOrderDto>(await client.PostAsJsonAsync("/api/purchase-orders", Po(b), Ct));
        Assert.Null(Assert.Single(poB.Lines).SupplierItemCode);

        // Nháp lưu lại sau khi đổi mã ở danh mục → lấy mã mới; gửi duyệt rồi đổi mã tiếp → PO đã gửi giữ mã lúc gửi.
        await ReadAsync<SupplierItemDto>(await head.PutAsJsonAsync($"/api/supplier-items/{link.Id}",
            new SaveSupplierItemRequest(item.Id, "VB6207-B", thung, null, null, false), Ct));
        poA = await ReadAsync<PurchaseOrderDto>(await client.PutAsJsonAsync($"/api/purchase-orders/{poA.Id}", Po(a), Ct));
        Assert.Equal("VB6207-B", poA.Lines.Single().SupplierItemCode);
        await ReadAsync<PurchaseOrderDto>(await client.PostAsync($"/api/purchase-orders/{poA.Id}/submit", null, Ct));
        await ReadAsync<SupplierItemDto>(await head.PutAsJsonAsync($"/api/supplier-items/{link.Id}",
            new SaveSupplierItemRequest(item.Id, "VB6207-C", thung, null, null, false), Ct));
        var submitted = await ReadAsync<PurchaseOrderDto>(await client.GetAsync($"/api/purchase-orders/{poA.Id}", Ct));
        Assert.Equal("Submitted", submitted.Status);
        Assert.Equal("VB6207-B", submitted.Lines.Single().SupplierItemCode);
    }
}
