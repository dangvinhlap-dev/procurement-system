using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Proc.Application.MasterData;

namespace Proc.IntegrationTests;

/// <summary>
/// Đánh số danh mục tự động (design.md mục 5.11): dải mặc định có sẵn trong dữ liệu mẫu; dải riêng theo nhóm hàng; sửa dải; chưa cấu hình thì
/// không thêm được; chỉ tổng công ty cấu hình; thêm đồng thời không trùng mã.
/// </summary>
[Collection(ApiCollection.Name)]
public class CatalogNumberingTests
{
    private readonly ProcApiFactory _factory;

    public CatalogNumberingTests(ProcApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(Ct)}");
        return (await response.Content.ReadFromJsonAsync<T>(Ct))!;
    }

    private static async Task<string?> TitleAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(Ct))?.Title;

    private static SaveSupplierRequest NewSupplier(Guid groupId, string name) =>
        new(null, groupId, name, null, false, null, null, null, null, null, null, null, null, "Approved", null, null, [], []);

    [Fact]
    public async Task DaiTheoNhomHang_SuaDai_ChuaCauHinhThiChan()
    {
        var head = await _factory.CreateClientInCompanyAsync(ProcApiFactory.AdminUserName, ProcApiFactory.AdminPassword, "TCT");
        var list = (await ReadAsync<List<CatalogNumberingDto>>(await head.GetAsync("/api/catalog-numberings", Ct))).ToDictionary(n => n.Catalog + (n.CategoryCode ?? ""));
        Assert.Equal(("NCC", 6), (list["supplier"].Prefix, list["supplier"].Digits));
        Assert.Equal(("{NHOM}-", "VT-00001", (string?)null), (list["item"].Prefix, list["item"].Sample, list["item"].NextCode));

        // Nhóm hàng có dải riêng: mã hàng theo dải đó thay vì dòng mặc định {NHOM}-.
        var category = await ReadAsync<ItemCategoryDetailDto>(await head.PostAsJsonAsync("/api/item-categories", new CreateItemCategoryRequest(null, "Nhóm dải riêng", null, null), Ct));
        var cai = (await head.GetFromJsonAsync<List<UomDto>>("/api/uoms", Ct))!.Single(u => u.Code == "CAI").Id;
        var prefix = "R" + category.Code[2..];
        var own = await ReadAsync<CatalogNumberingDto>(await head.PostAsJsonAsync("/api/catalog-numberings",
            new SaveCatalogNumberingRequest("item", category.Id, prefix, 3, 100, 999), Ct));
        Assert.Equal(($"{prefix}100", $"{prefix}100"), (own.Sample, own.NextCode));
        Assert.Contains("đã có dải mã hàng riêng", await TitleAsync(await head.PostAsJsonAsync("/api/catalog-numberings",
            new SaveCatalogNumberingRequest("item", category.Id, "X", 3, 1, 999), Ct)));
        CreateItemRequest Item() => new(null, "Mặt hàng dải riêng", null, "Inventory", category.Id, cai, null, null, null, null, "10", null, false, false, null, null, []);
        Assert.Equal($"{prefix}100", (await ReadAsync<ItemDetailDto>(await head.PostAsJsonAsync("/api/items", Item(), Ct))).Code);

        // Sửa dải: số đầu dải mới lớn hơn số đang có → mã kế tiếp nhảy lên.
        await ReadAsync<CatalogNumberingDto>(await head.PutAsJsonAsync($"/api/catalog-numberings/{own.Id}",
            new SaveCatalogNumberingRequest("item", category.Id, prefix, 3, 500, 501), Ct));
        Assert.Equal($"{prefix}500", (await ReadAsync<ItemDetailDto>(await head.PostAsJsonAsync("/api/items", Item(), Ct))).Code);
        Assert.Equal($"{prefix}501", (await ReadAsync<ItemDetailDto>(await head.PostAsJsonAsync("/api/items", Item(), Ct))).Code);
        Assert.StartsWith("Hết dải mã hàng hóa", await TitleAsync(await head.PostAsJsonAsync("/api/items", Item(), Ct)));

        // Cấu hình sai bị chặn; công ty thành viên không cấu hình được.
        Assert.Contains("chỉ gồm chữ không dấu", await TitleAsync(await head.PostAsJsonAsync("/api/catalog-numberings",
            new SaveCatalogNumberingRequest("item-category", null, "NH-", 3, 1, 999), Ct)));
        var bha = await _factory.CreateClientInCompanyAsync(ProcApiFactory.AdminUserName, ProcApiFactory.AdminPassword, "BHA");
        Assert.Equal(HttpStatusCode.Forbidden, (await bha.PutAsJsonAsync($"/api/catalog-numberings/{own.Id}",
            new SaveCatalogNumberingRequest("item", category.Id, prefix, 3, 1, 999), Ct)).StatusCode);

        // Bỏ dải mặc định của nhóm NCC: không thêm được nhóm NCC mới; cấu hình lại thì được.
        var groupRange = list["supplier-group"];
        Assert.Equal(HttpStatusCode.NoContent, (await head.DeleteAsync($"/api/catalog-numberings/{groupRange.Id}", Ct)).StatusCode);
        try
        {
            Assert.StartsWith("Chưa cấu hình đánh số cho danh mục nhóm nhà cung cấp", await TitleAsync(await head.PostAsJsonAsync("/api/supplier-groups",
                new CreateSupplierGroupRequest(null, "Nhóm chưa có dải"), Ct)));
        }
        finally
        {
            await ReadAsync<CatalogNumberingDto>(await head.PostAsJsonAsync("/api/catalog-numberings",
                new SaveCatalogNumberingRequest("supplier-group", null, groupRange.Prefix, groupRange.Digits, groupRange.FromNumber, groupRange.ToNumber), Ct));
        }
    }

    [Fact]
    public async Task ThemDongThoi_KhongTrungMa()
    {
        var groupId = (await (await _factory.CreateClientInCompanyAsync(ProcApiFactory.AdminUserName, ProcApiFactory.AdminPassword, "TCT"))
            .GetFromJsonAsync<List<SupplierGroupDto>>("/api/supplier-groups", Ct))!.Single(g => g.Code == "TN").Id;
        // Mỗi luồng một client (một kết nối, một giao dịch riêng) – khóa theo tiền tố bắt luồng sau chờ luồng trước lưu xong.
        var clients = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ =>
            _factory.CreateClientInCompanyAsync(ProcApiFactory.AdminUserName, ProcApiFactory.AdminPassword, "TCT")));
        var created = await Task.WhenAll(clients.Select((c, i) => c.PostAsJsonAsync("/api/suppliers", NewSupplier(groupId, $"NCC đồng thời {i}"), Ct)));
        var codes = new List<string>();
        foreach (var response in created) codes.Add((await ReadAsync<SupplierDetailDto>(response)).Code);
        Assert.Equal(6, codes.Distinct().Count());
        Assert.All(codes, c => Assert.Matches("^NCC\\d{6}$", c));
    }
}
