using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Proc.Application.MasterData;
using Proc.Domain.Organization;

namespace Proc.IntegrationTests;

/// <summary>
/// Danh mục đơn vị tính, nhóm hàng, hàng hóa (design.md mục 5.2, 5.7). Mỗi test tự tạo nhóm hàng gốc riêng để không đụng
/// cây nhóm hàng mẫu mà test phạm vi khác đang đếm.
/// </summary>
[Collection(ApiCollection.Name)]
public class ItemCatalogTests
{
    private readonly ProcApiFactory _factory;

    public ItemCatalogTests(ProcApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string UniqueCode(string prefix) => $"{prefix}{Guid.NewGuid():N}".ToUpperInvariant()[..10];

    private static async Task<string?> TitleAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(Ct))?.Title;

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(Ct)}");
        return (await response.Content.ReadFromJsonAsync<T>(Ct))!;
    }

    private async Task<HttpClient> HeadOfficeAdminAsync()
    {
        var userName = $"qt.{Guid.NewGuid():N}"[..16];
        await _factory.CreateUserAsync(userName, ProcApiFactory.TestPassword, ["TCT", "BHA"]);
        return await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, "TCT");
    }

    private static async Task<ItemCategoryDetailDto> CreateCategoryAsync(HttpClient client, string code, Guid? parentId = null) =>
        await ReadAsync<ItemCategoryDetailDto>(await client.PostAsJsonAsync(
            "/api/item-categories", new CreateItemCategoryRequest(null, $"Nhóm {code}", parentId, null), Ct));

    private static async Task<Guid> UomIdAsync(HttpClient client, string code) =>
        (await client.GetFromJsonAsync<List<UomDto>>("/api/uoms", Ct))!.Single(u => u.Code == code).Id;

    /// <summary>Hàng mới – mã để trống, hệ thống cấp theo dải của nhóm hàng (design.md mục 5.11).</summary>
    private static CreateItemRequest NewItem(Guid categoryId, Guid baseUomId, string name = "Bu lông lục giác M8",
        string type = "Inventory", IReadOnlyList<ItemUomConversionInput>? conversions = null, bool trackLot = false) =>
        new(null, name, "Hex bolt M8", type, categoryId, baseUomId, "M8x50", "Đài Loan", "Đài Loan", null, "10", null,
            trackLot, false, null, null, conversions ?? []);

    private static UpdateItemRequest Edit(ItemDetailDto d, bool? isActive = null) =>
        new(d.Name, d.EnglishName, d.Type, d.CategoryId, d.BaseUomId, d.Specification, d.Brand, d.Origin, d.Barcode, d.VatCode,
            d.TechnicalSpecs, d.TrackLot, d.TrackExpiry, d.MinStock, d.MaxStock,
            d.Conversions.Select(c => new ItemUomConversionInput(c.UomId, c.Factor)).ToList(), isActive ?? d.IsActive);

    [Fact]
    public async Task DonViTinh_ThemSuaVaChiLamOTongCongTy()
    {
        var head = await HeadOfficeAdminAsync();
        Assert.Contains((await head.GetFromJsonAsync<List<UomDto>>("/api/uoms", Ct))!, u => u.Code == "CAI" && u.Name == "Cái");

        var code = UniqueCode("U")[..8];
        var uom = await ReadAsync<UomDto>(await head.PostAsJsonAsync("/api/uoms", new CreateUomRequest(code.ToLowerInvariant(), "Ram"), Ct));
        Assert.Equal(code, uom.Code);

        var duplicate = await head.PostAsJsonAsync("/api/uoms", new CreateUomRequest(code, "Trùng"), Ct);
        Assert.Equal($"Mã đơn vị tính {code} đã có.", await TitleAsync(duplicate));

        var renamed = await ReadAsync<UomDto>(await head.PutAsJsonAsync($"/api/uoms/{uom.Id}", new UpdateUomRequest("Ram giấy", true), Ct));
        Assert.Equal("Ram giấy", renamed.Name);

        // Danh mục dùng chung do tổng công ty quản lý (design.md mục 5.7): ADMIN ở BHA cũng không thêm được.
        var bha = await _factory.CreateClientInCompanyAsync(ProcApiFactory.AdminUserName, ProcApiFactory.AdminPassword, "BHA");
        var atMember = await bha.PostAsJsonAsync("/api/uoms", new CreateUomRequest(UniqueCode("U")[..8], "Thử"), Ct);
        Assert.Equal(HttpStatusCode.Forbidden, atMember.StatusCode);
        Assert.Equal("Chỉ thêm, sửa đơn vị tính khi đang làm việc ở tổng công ty.", await TitleAsync(atMember));
    }

    [Fact]
    public async Task NhomHang_CayDuongDanVaChanVongLap()
    {
        var head = await HeadOfficeAdminAsync();
        var root = await CreateCategoryAsync(head, UniqueCode("R"));
        var child = await CreateCategoryAsync(head, UniqueCode("C"), root.Id);
        var grandChild = await CreateCategoryAsync(head, UniqueCode("G"), child.Id);

        Assert.Equal(($"{root.Code} / {child.Code} / {grandChild.Code}", 3, child.Code), (grandChild.Path, grandChild.Level, grandChild.ParentCode));
        var list = (await head.GetFromJsonAsync<List<ItemCategoryDetailDto>>("/api/item-categories", Ct))!;
        // Xếp theo đường dẫn: nhóm con đứng ngay dưới nhóm cha.
        var idx = list.FindIndex(c => c.Id == root.Id);
        Assert.Equal([root.Id, child.Id, grandChild.Id], list.Skip(idx).Take(3).Select(c => c.Id));

        var cycle = await head.PutAsJsonAsync($"/api/item-categories/{root.Id}",
            new UpdateItemCategoryRequest(root.Name, grandChild.Id, null, true), Ct);
        Assert.Equal(HttpStatusCode.BadRequest, cycle.StatusCode);
        Assert.Contains("vòng", await TitleAsync(cycle));

        var withChild = await head.PutAsJsonAsync($"/api/item-categories/{child.Id}",
            new UpdateItemCategoryRequest(child.Name, root.Id, "1521", false), Ct);
        Assert.Contains(grandChild.Code, await TitleAsync(withChild));

        var account = await ReadAsync<ItemCategoryDetailDto>(await head.PutAsJsonAsync($"/api/item-categories/{grandChild.Id}",
            new UpdateItemCategoryRequest("Bu lông inox", root.Id, "1521", true), Ct));
        Assert.Equal(("Bu lông inox", root.Id, "1521", 2), (account.Name, account.ParentId, account.InventoryAccount, account.Level));
    }

    [Fact]
    public async Task NhomHang_NguoiBiGioiHan_ChiThemNhomConTrongNhanhDuocPhep()
    {
        var head = await HeadOfficeAdminAsync();
        var allowed = await CreateCategoryAsync(head, UniqueCode("R"));
        var roleCode = UniqueCode("QLN");
        await _factory.CreateRoleAsync(roleCode, Permissions.MasterData.ItemCategoryView, Permissions.MasterData.ItemCategoryManage);
        var userName = $"qln.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(userName, "TCT", roleCode, categoryCodes: [allowed.Code]);
        var limited = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, "TCT");

        var root = await limited.PostAsJsonAsync("/api/item-categories", new CreateItemCategoryRequest(null, "Gốc mới", null, null), Ct);
        Assert.Equal(HttpStatusCode.Forbidden, root.StatusCode);

        var child = await CreateCategoryAsync(limited, UniqueCode("C"), allowed.Id);
        var visible = (await limited.GetFromJsonAsync<List<ItemCategoryDetailDto>>("/api/item-categories", Ct))!;
        Assert.Equal([allowed.Code, child.Code], visible.Select(c => c.Code));
    }

    [Fact]
    public async Task HangHoa_ThemCoQuyDoi_TimKhongDauVaTheoNhomCha()
    {
        var head = await HeadOfficeAdminAsync();
        var root = await CreateCategoryAsync(head, UniqueCode("R"));
        var child = await CreateCategoryAsync(head, UniqueCode("C"), root.Id);
        var hop = await UomIdAsync(head, "HOP");
        var thung = await UomIdAsync(head, "THUNG");
        // Dữ liệu mẫu: mã hàng = {NHOM}-5 chữ số, theo nhóm của chính mặt hàng (nhóm con).
        var code = $"{child.Code}-00001";

        var item = await ReadAsync<ItemDetailDto>(await head.PostAsJsonAsync("/api/items",
            NewItem(child.Id, hop, conversions: [new(thung, 24m)], trackLot: true), Ct));
        Assert.Equal((code, "Inventory", true), (item.Code, item.Type, item.TrackLot));
        Assert.Equal(("THUNG", 24m), (Assert.Single(item.Conversions).UomCode, item.Conversions[0].Factor));
        Assert.True(item.CanEdit);

        // Gõ không dấu, nhiều từ không theo thứ tự; lọc theo nhóm cha gồm hàng của nhóm con.
        var found = await head.GetFromJsonAsync<ItemListResult>($"/api/items?search=luc%20giac%20bu%20long&categoryId={root.Id}", Ct);
        var row = Assert.Single(found!.Items);
        Assert.Equal((code, child.Code, "HOP", false), (row.Code, row.CategoryCode, row.BaseUomCode, found.Truncated));

        var manual = await head.PostAsJsonAsync("/api/items", NewItem(child.Id, hop) with { Code = $"{child.Code}-99999" }, Ct);
        Assert.Equal("Mã hàng hóa do hệ thống tự cấp theo dải đã cấu hình – để trống mã.", await TitleAsync(manual));
        Assert.Equal($"{child.Code}-00002", (await ReadAsync<ItemDetailDto>(await head.PostAsJsonAsync("/api/items", NewItem(child.Id, hop), Ct))).Code);

        var service = await head.PostAsJsonAsync("/api/items", NewItem(child.Id, hop, type: "Service", trackLot: true), Ct);
        Assert.Equal("Chỉ hàng tồn kho mới quản lý lô, hạn dùng và tồn tối thiểu/tối đa.", await TitleAsync(service));
    }

    [Fact]
    public async Task HangHoa_SuaQuyDoiVaDoiDvtCoSo()
    {
        var head = await HeadOfficeAdminAsync();
        var category = await CreateCategoryAsync(head, UniqueCode("R"));
        var (hop, thung, cai) = (await UomIdAsync(head, "HOP"), await UomIdAsync(head, "THUNG"), await UomIdAsync(head, "CAI"));
        var item = await ReadAsync<ItemDetailDto>(await head.PostAsJsonAsync("/api/items",
            NewItem(category.Id, hop, conversions: [new(thung, 24m)]), Ct));

        // Đổi ĐVT cơ sở sang Cái, Hộp thành ĐVT quy đổi (1 hộp = 10 cái), bỏ Thùng – trong một lần lưu.
        var edited = await ReadAsync<ItemDetailDto>(await head.PutAsJsonAsync($"/api/items/{item.Id}",
            Edit(item) with { BaseUomId = cai, Conversions = [new(hop, 10m)], MinStock = 5, MaxStock = 50 }, Ct));
        Assert.Equal(cai, edited.BaseUomId);
        Assert.Equal(("HOP", 10m), (Assert.Single(edited.Conversions).UomCode, edited.Conversions[0].Factor));
        Assert.Equal((5m, 50m), (edited.MinStock, edited.MaxStock));

        // ĐVT đang dùng cho hàng còn dùng thì không ngừng được.
        var uoms = await head.GetFromJsonAsync<List<UomDto>>("/api/uoms", Ct);
        var caiDto = uoms!.Single(u => u.Id == cai);
        var stop = await head.PutAsJsonAsync($"/api/uoms/{cai}", new UpdateUomRequest(caiDto.Name, false), Ct);
        Assert.Equal(HttpStatusCode.BadRequest, stop.StatusCode);
    }

    [Fact]
    public async Task HangHoa_PhamViNhomHang_KhongThayHangNhomKhac()
    {
        var head = await HeadOfficeAdminAsync();
        var allowed = await CreateCategoryAsync(head, UniqueCode("R"));
        var hidden = await CreateCategoryAsync(head, UniqueCode("H"));
        var hop = await UomIdAsync(head, "HOP");
        var first = await ReadAsync<ItemDetailDto>(await head.PostAsJsonAsync("/api/items", NewItem(allowed.Id, hop), Ct));
        var secret = await ReadAsync<ItemDetailDto>(await head.PostAsJsonAsync("/api/items", NewItem(hidden.Id, hop), Ct));

        var roleCode = UniqueCode("QLH");
        await _factory.CreateRoleAsync(roleCode, Permissions.MasterData.ItemView, Permissions.MasterData.ItemManage);
        var userName = $"qlh.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(userName, "TCT", roleCode, categoryCodes: [allowed.Code]);
        var limited = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, "TCT");

        var mine = await limited.GetFromJsonAsync<ItemListResult>("/api/items", Ct);
        Assert.Equal([first.Code], mine!.Items.Select(i => i.Code));
        Assert.Equal(HttpStatusCode.NotFound, (await limited.GetAsync($"/api/items/{secret.Id}", Ct)).StatusCode);

        // Đặt hàng mới vào nhóm ngoài phạm vi: như không có nhóm đó.
        var intoHidden = await limited.PostAsJsonAsync("/api/items", NewItem(hidden.Id, hop), Ct);
        Assert.Equal(HttpStatusCode.NotFound, intoHidden.StatusCode);

        // Người bị giới hạn nhóm hàng vẫn nhận mã kế tiếp đúng dải của nhóm mình (mã do hệ thống cấp, không tự đặt).
        var next = await ReadAsync<ItemDetailDto>(await limited.PostAsJsonAsync("/api/items", NewItem(allowed.Id, hop), Ct));
        Assert.Equal($"{allowed.Code}-00002", next.Code);
    }

    [Fact]
    public async Task HangHoa_CongTyThanhVienSuaTheoQuyenSuaDanhMucDungChung()
    {
        var head = await HeadOfficeAdminAsync();
        var category = await CreateCategoryAsync(head, UniqueCode("R"));
        var item = await ReadAsync<ItemDetailDto>(await head.PostAsJsonAsync("/api/items",
            NewItem(category.Id, await UomIdAsync(head, "HOP")), Ct));

        var bha = await _factory.CreateClientInCompanyAsync(ProcApiFactory.AdminUserName, ProcApiFactory.AdminPassword, "BHA");
        // Quản trị có quyền "Sửa thông tin danh mục dùng chung": sửa được ở công ty thành viên, mã giữ nguyên (design.md mục 5.7); người chỉ
        // có quyền quản lý hàng hóa thì không – xem SharedCatalogEditTests.
        var detail = await bha.GetFromJsonAsync<ItemDetailDto>($"/api/items/{item.Id}", Ct);
        Assert.True(detail!.CanEdit);
        var updated = await ReadAsync<ItemDetailDto>(await bha.PutAsJsonAsync($"/api/items/{item.Id}", Edit(detail, isActive: false), Ct));
        Assert.Equal((item.Code, false), (updated.Code, updated.IsActive));
    }
}
