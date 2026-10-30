using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Proc.Application.MasterData;
using Proc.Domain.Organization;

namespace Proc.IntegrationTests;

/// <summary>
/// NCC áp dụng cho công ty và NCC – sản phẩm (design.md mục 5.3, 5.9).
/// </summary>
[Collection(ApiCollection.Name)]
public class SupplierLinkTests
{
    private readonly ProcApiFactory _factory;

    public SupplierLinkTests(ProcApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string UniqueCode(string prefix) => $"{prefix}{Guid.NewGuid():N}".ToUpperInvariant()[..10];

    private static async Task<string?> TitleAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(Ct))?.Title;

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(Ct)}");
        return (await response.Content.ReadFromJsonAsync<T>(Ct))!;
    }

    /// <summary>Quản trị mới ở TCT và BHA; trả client ở từng công ty.</summary>
    private async Task<(HttpClient Head, HttpClient Bha)> AdminAsync()
    {
        var userName = $"qt.{Guid.NewGuid():N}"[..16];
        await _factory.CreateUserAsync(userName, ProcApiFactory.TestPassword, ["TCT", "BHA"]);
        return (await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, "TCT"),
            await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, "BHA"));
    }

    private static async Task<SupplierDetailDto> NewSupplierAsync(HttpClient head, string status = "Approved", string? note = null)
    {
        var groupId = (await head.GetFromJsonAsync<List<SupplierGroupDto>>("/api/supplier-groups", Ct))!.Single(g => g.Code == "TN").Id;
        return await ReadAsync<SupplierDetailDto>(await head.PostAsJsonAsync("/api/suppliers",
            new SaveSupplierRequest(UniqueCode("N"), groupId, $"NCC thử {Guid.NewGuid():N}"[..20], null, false, null, null, null, null,
                null, null, null, null, status, note, null, [], []), Ct));
    }

    private static async Task<Dictionary<string, Guid>> UomsAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<List<UomDto>>("/api/uoms", Ct))!.ToDictionary(u => u.Code, u => u.Id);

    /// <summary>Nhóm hàng gốc mới + mặt hàng ĐVT Hộp, quy đổi Thùng = 24 hộp.</summary>
    private static async Task<(ItemCategoryDetailDto Category, ItemDetailDto Item)> NewItemAsync(HttpClient head)
    {
        var uoms = await UomsAsync(head);
        var category = await ReadAsync<ItemCategoryDetailDto>(await head.PostAsJsonAsync("/api/item-categories",
            new CreateItemCategoryRequest(UniqueCode("R"), "Nhóm thử", null, null), Ct));
        var item = await ReadAsync<ItemDetailDto>(await head.PostAsJsonAsync("/api/items",
            new CreateItemRequest($"{category.Code}-01", "Bu lông thử", null, "Inventory", category.Id, uoms["HOP"], null, null, null,
                null, null, null, false, false, null, null, [new(uoms["THUNG"], 24m)]), Ct));
        return (category, item);
    }

    [Fact]
    public async Task ApDungCongTy_MoiCongTyTuKhaiBao_LocNccCongTyDangDung()
    {
        var (head, bha) = await AdminAsync();
        var supplier = await NewSupplierAsync(head);

        var before = await bha.GetFromJsonAsync<SupplierCompanyDto>($"/api/suppliers/{supplier.Id}/company", Ct);
        Assert.Equal(("BHA", false, true), (before!.CompanyCode, before.IsApplied, before.CanEdit));

        var localCode = UniqueCode("331");
        var applied = await ReadAsync<SupplierCompanyDto>(await bha.PutAsJsonAsync($"/api/suppliers/{supplier.Id}/company",
            new SaveSupplierCompanyRequest(localCode.ToLowerInvariant(), new DateOnly(2026, 11, 1), true), Ct));
        Assert.Equal((true, localCode, true), (applied.IsApplied, applied.LocalCode, applied.IsActive));

        // Ở BHA thấy NCC trong danh sách "đang dùng"; ở TCT thì chưa (mỗi công ty là pháp nhân độc lập).
        var atBha = await bha.GetFromJsonAsync<SupplierListResult>($"/api/suppliers?appliedOnly=true&search={supplier.Code}", Ct);
        var row = Assert.Single(atBha!.Items);
        Assert.Equal((true, localCode), (row.AppliedAtCompany, row.LocalCode));
        var atHead = await head.GetFromJsonAsync<SupplierListResult>($"/api/suppliers?appliedOnly=true&search={supplier.Code}", Ct);
        Assert.Empty(atHead!.Items);

        var other = await NewSupplierAsync(head);
        var sameCode = await bha.PutAsJsonAsync($"/api/suppliers/{other.Id}/company", new SaveSupplierCompanyRequest(localCode, null, true), Ct);
        Assert.Equal($"Mã {localCode} đã dùng cho NCC {supplier.Code} tại công ty này.", await TitleAsync(sameCode));
    }

    [Fact]
    public async Task ApDungCongTy_NccDanhSachDen_KhongApDungDuoc()
    {
        var (head, bha) = await AdminAsync();
        var supplier = await NewSupplierAsync(head, "Blacklisted", "Hàng giả");

        var response = await bha.PutAsJsonAsync($"/api/suppliers/{supplier.Id}/company", new SaveSupplierCompanyRequest(null, null, true), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("danh sách đen", await TitleAsync(response));
    }

    [Fact]
    public async Task NccSanPham_ThemSuaXoa_DvtMuaPhaiThuocMatHang()
    {
        var (head, bha) = await AdminAsync();
        var supplier = await NewSupplierAsync(head);
        var (_, item) = await NewItemAsync(head);
        var uoms = await UomsAsync(head);

        var link = await ReadAsync<SupplierItemDto>(await head.PostAsJsonAsync($"/api/suppliers/{supplier.Id}/items",
            new SaveSupplierItemRequest(item.Id, "BL-M8", uoms["THUNG"], 10, 7, false), Ct));
        Assert.Equal((item.Code, "THUNG", "HOP", 10m, 7), (link.ItemCode, link.PurchaseUomCode, link.BaseUomCode, link.Moq, link.LeadTimeDays));

        var badUom = await head.PostAsJsonAsync($"/api/suppliers/{(await NewSupplierAsync(head)).Id}/items",
            new SaveSupplierItemRequest(item.Id, null, uoms["KG"], null, null, false), Ct);
        Assert.Contains("ĐVT mua", await TitleAsync(badUom));
        var duplicate = await head.PostAsJsonAsync($"/api/suppliers/{supplier.Id}/items",
            new SaveSupplierItemRequest(item.Id, null, uoms["HOP"], null, null, false), Ct);
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);

        var edited = await ReadAsync<SupplierItemDto>(await head.PutAsJsonAsync($"/api/supplier-items/{link.Id}",
            new SaveSupplierItemRequest(item.Id, "BL-M8-NEW", uoms["HOP"], null, 14, false), Ct));
        Assert.Equal(("BL-M8-NEW", "HOP", 14), (edited.SupplierItemCode, edited.PurchaseUomCode, edited.LeadTimeDays));

        // Công ty thành viên không sửa được danh mục dùng chung.
        Assert.Equal(HttpStatusCode.Forbidden, (await bha.DeleteAsync($"/api/supplier-items/{link.Id}", Ct)).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await head.DeleteAsync($"/api/supplier-items/{link.Id}", Ct)).StatusCode);
        Assert.Empty((await head.GetFromJsonAsync<List<SupplierItemDto>>($"/api/suppliers/{supplier.Id}/items", Ct))!);
    }

    [Fact]
    public async Task NccSanPham_MoiMatHangMotNccUuTien_ChonMoiThiBoCu()
    {
        var (head, _) = await AdminAsync();
        var (_, item) = await NewItemAsync(head);
        var hop = (await UomsAsync(head))["HOP"];
        var first = await NewSupplierAsync(head);
        var second = await NewSupplierAsync(head);

        await ReadAsync<SupplierItemDto>(await head.PostAsJsonAsync($"/api/suppliers/{first.Id}/items",
            new SaveSupplierItemRequest(item.Id, null, hop, null, null, true), Ct));
        var secondLink = await ReadAsync<SupplierItemDto>(await head.PostAsJsonAsync($"/api/suppliers/{second.Id}/items",
            new SaveSupplierItemRequest(item.Id, null, hop, null, null, false), Ct));

        // Sửa dòng của NCC thứ hai thành ưu tiên: NCC thứ nhất tự bỏ ưu tiên, không lỗi trùng chỉ mục.
        await ReadAsync<SupplierItemDto>(await head.PutAsJsonAsync($"/api/supplier-items/{secondLink.Id}",
            new SaveSupplierItemRequest(item.Id, null, hop, null, null, true), Ct));

        var suppliers = await head.GetFromJsonAsync<List<SupplierItemDto>>($"/api/items/{item.Id}/suppliers", Ct);
        Assert.Equal([(second.Code, true), (first.Code, false)], suppliers!.Select(s => (s.SupplierCode, s.IsPreferred)));
    }

    [Fact]
    public async Task NccSanPham_NguoiBiGioiHanNhomHang_KhongThayHangNhomKhac()
    {
        var (head, _) = await AdminAsync();
        var supplier = await NewSupplierAsync(head);
        var (allowed, visibleItem) = await NewItemAsync(head);
        var (_, hiddenItem) = await NewItemAsync(head);
        var hop = (await UomsAsync(head))["HOP"];
        foreach (var item in new[] { visibleItem, hiddenItem })
        {
            await ReadAsync<SupplierItemDto>(await head.PostAsJsonAsync($"/api/suppliers/{supplier.Id}/items",
                new SaveSupplierItemRequest(item.Id, null, hop, null, null, false), Ct));
        }

        var roleCode = UniqueCode("XNCC");
        await _factory.CreateRoleAsync(roleCode, Permissions.MasterData.SupplierView);
        var userName = $"xn.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(userName, "TCT", roleCode, categoryCodes: [allowed.Code]);
        var limited = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, "TCT");

        var rows = await limited.GetFromJsonAsync<List<SupplierItemDto>>($"/api/suppliers/{supplier.Id}/items", Ct);
        Assert.Equal([visibleItem.Code], rows!.Select(r => r.ItemCode));
    }
}
