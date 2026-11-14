using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Proc.Application.Excel;
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

    /// <summary>
    /// Tạo nhóm hàng tên "Nhóm {code}". Không có nhóm cha thì <paramref name="level"/> cho biết cấp cần tạo (tự dựng nhóm phía trên) – mặt hàng
    /// chỉ gắn nhóm cấp 4 (design.md mục 5.7, N4-5).
    /// </summary>
    private static async Task<ItemCategoryDetailDto> CreateCategoryAsync(HttpClient client, string code, Guid? parentId = null, int level = 1)
    {
        var request = new CreateItemCategoryRequest(TestCodes.Category(), $"Nhóm {code}", parentId, null);
        return await ReadAsync<ItemCategoryDetailDto>(parentId is null && level > 1
            ? await TestCategories.PostAtLevelAsync(client, request, level, Ct)
            : await client.PostAsJsonAsync("/api/item-categories", request, Ct));
    }

    /// <summary>Nhập file Excel nhóm hàng (cột Mã nhóm, Tên nhóm, Mã nhóm cha) – <paramref name="commit"/> = false là kiểm tra thử.</summary>
    private static async Task<ExcelImportResult> ImportCategoriesAsync(HttpClient client, bool commit, params string?[][] rows)
    {
        using var workbook = new ClosedXML.Excel.XLWorkbook();
        var sheet = workbook.AddWorksheet("Nhóm hàng");
        string[] headers = ["Mã nhóm *", "Tên nhóm *", "Mã nhóm cha"];
        for (var c = 0; c < headers.Length; c++) sheet.Cell(1, c + 1).Value = headers[c];
        for (var r = 0; r < rows.Length; r++)
            for (var c = 0; c < rows[r].Length; c++)
                if (rows[r][c] is { } value) sheet.Cell(r + 2, c + 1).SetValue(value);
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        using var content = new MultipartFormDataContent();
        var part = new ByteArrayContent(stream.ToArray());
        part.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        content.Add(part, "file", "nhom-hang.xlsx");
        return await ReadAsync<ExcelImportResult>(await client.PostAsync($"/api/excel/{ExcelCatalogs.ItemCategories}/import?commit={commit.ToString().ToLowerInvariant()}", content, Ct));
    }

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

    /// <summary>
    /// Nhóm hàng tối đa 4 cấp, mã do người dùng gõ (người dùng chốt 14/11/2026 – design.md mục 5.7, N4-1…N4-8): cấp + tên cấp + số nhóm con
    /// trên danh mục; nhóm cấp 4 không có con; chuyển nhánh vượt cấp bị chặn, chuyển hợp lệ thì cấp cả nhánh tính lại; mã trống / trùng /
    /// sai quy tắc báo lỗi; nhập Excel theo mã gõ tay, nhóm mới làm nhóm cha được cho dòng bên dưới.
    /// </summary>
    [Fact]
    public async Task NhomHang_ToiDa4Cap_MaGoTay_ChuyenNhanhTinhLaiCap()
    {
        var head = await HeadOfficeAdminAsync();
        async Task<ItemCategoryDetailDto> AddAsync(string code, string name, Guid? parentId) =>
            await ReadAsync<ItemCategoryDetailDto>(await head.PostAsJsonAsync("/api/item-categories", new CreateItemCategoryRequest(code, name, parentId, null), Ct));

        var c1 = TestCodes.Category();
        var l1 = await AddAsync(c1.ToLowerInvariant(), "Vật tư thử 4 cấp", null);
        Assert.Equal((c1, 1, "Ngành hàng"), (l1.Code, l1.Level, l1.LevelName));
        var l2 = await AddAsync(TestCodes.Category(), "Vật tư điện", l1.Id);
        var l3 = await AddAsync(TestCodes.Category(), "Dây cáp điện", l2.Id);
        var l4 = await AddAsync(TestCodes.Category(), "Cáp đồng bọc PVC", l3.Id);
        Assert.Equal((4, "Chủng loại"), (l4.Level, l4.LevelName));

        // N4-3: nhóm cấp 4 không thêm nhóm con.
        Assert.Contains("đã ở cấp 4 (cấp cuối)", await TitleAsync(await head.PostAsJsonAsync("/api/item-categories",
            new CreateItemCategoryRequest(TestCodes.Category(), "Cấp năm", l4.Id, null), Ct)));
        // N4-7: mã bắt buộc, không trùng (kể cả khác hoa thường), đúng quy tắc.
        Assert.Equal("Hãy nhập mã nhóm hàng.", await TitleAsync(await head.PostAsJsonAsync("/api/item-categories",
            new CreateItemCategoryRequest(null, "Không mã", null, null), Ct)));
        Assert.Equal($"Mã nhóm hàng {c1} đã có (Vật tư thử 4 cấp) – chọn mã khác.", await TitleAsync(await head.PostAsJsonAsync("/api/item-categories",
            new CreateItemCategoryRequest(c1.ToLowerInvariant(), "Trùng mã", null, null), Ct)));
        Assert.Contains("2–20 ký tự", await TitleAsync(await head.PostAsJsonAsync("/api/item-categories",
            new CreateItemCategoryRequest("VT-01", "Sai mã", null, null), Ct)));

        // N4-4: chuyển l2 (nhánh sâu 2) vào một nhóm cấp 3 khác → nhóm cuối nhánh cấp 6, chặn; cấp không đổi.
        var o1 = await AddAsync(TestCodes.Category(), "Hóa chất thử", null);
        var o2 = await AddAsync(TestCodes.Category(), "Hóa chất công nghiệp", o1.Id);
        var o3 = await AddAsync(TestCodes.Category(), "Axit", o2.Id);
        var tooDeep = await head.PutAsJsonAsync($"/api/item-categories/{l2.Id}", new UpdateItemCategoryRequest(l2.Name, o3.Id, null, true), Ct);
        Assert.Equal(HttpStatusCode.BadRequest, tooDeep.StatusCode);
        Assert.Contains("cấp 6", await TitleAsync(tooDeep));

        // Chuyển l3 (có con) sang o1 (cấp 1): l3 thành cấp 2, l4 thành cấp 3; số nhóm con cập nhật.
        await ReadAsync<ItemCategoryDetailDto>(await head.PutAsJsonAsync($"/api/item-categories/{l3.Id}", new UpdateItemCategoryRequest(l3.Name, o1.Id, null, true), Ct));
        var list = (await head.GetFromJsonAsync<List<ItemCategoryDetailDto>>("/api/item-categories", Ct))!.ToDictionary(c => c.Id);
        Assert.Equal((2, 3, "Loại hàng"), (list[l3.Id].Level, list[l4.Id].Level, list[l4.Id].LevelName));
        Assert.Equal((0, 2), (list[l2.Id].ChildCount, list[o1.Id].ChildCount));
        Assert.Equal($"{o1.Code} / {l3.Code} / {l4.Code}", list[l4.Id].Path);

        // Nhập Excel: mã gõ tay bắt buộc; nhóm mới (mã chưa có) làm nhóm cha được cho dòng dưới; nhóm cha cấp 4 → lỗi dòng.
        var o4 = await AddAsync(TestCodes.Category(), "Axit sunfuric", o3.Id);
        var newRoot = TestCodes.Category();
        var newChild = TestCodes.Category();
        var bad = await ImportCategoriesAsync(head, false,
            [newRoot, "Ngành nhập Excel", null], [TestCodes.Category(), "Vượt cấp", o4.Code], [null, "Thiếu mã", null]);
        Assert.Equal([3, 4], bad.Errors.Select(e => e.Row).Order());
        Assert.Contains(bad.Errors, e => e.Row == 3 && e.Message.Contains("đã ở cấp 4", StringComparison.Ordinal));
        var good = await ImportCategoriesAsync(head, true, [newRoot, "Ngành nhập Excel", null], [newChild, "Nhóm con nhập Excel", newRoot]);
        Assert.Equal((2, 0, true), (good.Created, good.Updated, good.Committed));
        list = (await head.GetFromJsonAsync<List<ItemCategoryDetailDto>>("/api/item-categories", Ct))!.ToDictionary(c => c.Id);
        var imported = list.Values.Single(c => c.Code == newChild);
        Assert.Equal((newRoot, 2), (imported.ParentCode, imported.Level));
    }

    /// <summary>
    /// Mặt hàng chỉ gắn vào nhóm cấp 4 – Chủng loại (người dùng chốt 14/11/2026 – design.md mục 5.7, N4-5): thêm hàng hoặc chuyển hàng
    /// sang nhóm cấp 1–3 báo lỗi nêu cấp của nhóm; nhóm cấp 4 thì được.
    /// </summary>
    [Fact]
    public async Task HangHoa_ChiGanNhomCapBon()
    {
        var head = await HeadOfficeAdminAsync();
        var hop = await UomIdAsync(head, "HOP");
        var l1 = await CreateCategoryAsync(head, UniqueCode("R"));
        var l2 = await CreateCategoryAsync(head, UniqueCode("C"), l1.Id);
        var l3 = await CreateCategoryAsync(head, UniqueCode("L"), l2.Id);
        var l4 = await CreateCategoryAsync(head, UniqueCode("CL"), l3.Id);

        var atLevel3 = await head.PostAsJsonAsync("/api/items", NewItem(l3.Id, hop), Ct);
        Assert.Equal(HttpStatusCode.BadRequest, atLevel3.StatusCode);
        var title = await TitleAsync(atLevel3);
        Assert.Contains("chỉ gắn vào nhóm cấp 4 (Chủng loại)", title);
        Assert.Contains($"{l3.Code} – {l3.Name} đang ở cấp 3 (Loại hàng)", title);

        var item = await ReadAsync<ItemDetailDto>(await head.PostAsJsonAsync("/api/items", NewItem(l4.Id, hop), Ct));
        Assert.Equal(l4.Id, item.CategoryId);
        var moveUp = await head.PutAsJsonAsync($"/api/items/{item.Id}", Edit(item) with { CategoryId = l1.Id }, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, moveUp.StatusCode);
        Assert.Contains("đang ở cấp 1 (Ngành hàng)", await TitleAsync(moveUp));
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

        var root = await limited.PostAsJsonAsync("/api/item-categories", new CreateItemCategoryRequest(TestCodes.Category(), $"Gốc mới {Guid.NewGuid():N}", null, null), Ct);
        Assert.Equal(HttpStatusCode.Forbidden, root.StatusCode);

        var child = await CreateCategoryAsync(limited, UniqueCode("C"), allowed.Id);
        Assert.True(child.IsInScope);

        // Danh mục nhóm hàng ai cũng xem đủ (người dùng chốt 07/11/2026 – design.md mục 9.2): nhóm ngoài phạm vi vẫn hiện nhưng gắn cờ,
        // không sửa được, không thêm nhóm con vào được.
        var outside = await CreateCategoryAsync(head, UniqueCode("O"));
        var visible = (await limited.GetFromJsonAsync<List<ItemCategoryDetailDto>>("/api/item-categories", Ct))!.ToDictionary(c => c.Id);
        Assert.True(visible[allowed.Id].IsInScope);
        Assert.True(visible[child.Id].IsInScope);
        Assert.False(visible[outside.Id].IsInScope);
        var edit = await limited.PutAsJsonAsync($"/api/item-categories/{outside.Id}", new UpdateItemCategoryRequest("Đổi tên", null, null, true), Ct);
        Assert.Equal(HttpStatusCode.Forbidden, edit.StatusCode);
        var under = await limited.PostAsJsonAsync("/api/item-categories", new CreateItemCategoryRequest(TestCodes.Category(), "Con ngoài phạm vi", outside.Id, null), Ct);
        Assert.Equal(HttpStatusCode.Forbidden, under.StatusCode);
    }

    [Fact]
    public async Task NhomHang_KhongTrungTenTrongCungNhomCha()
    {
        var head = await HeadOfficeAdminAsync();
        var root = await CreateCategoryAsync(head, UniqueCode("R"));
        var first = await ReadAsync<ItemCategoryDetailDto>(await head.PostAsJsonAsync("/api/item-categories",
            new CreateItemCategoryRequest(TestCodes.Category(), "Vật tư điện", root.Id, null), Ct));

        // Trùng tên không phân biệt dấu, hoa thường, khoảng trắng thừa → chặn, báo nhóm đã có.
        var duplicate = await head.PostAsJsonAsync("/api/item-categories", new CreateItemCategoryRequest(TestCodes.Category(), "  vat tu  DIEN ", root.Id, null), Ct);
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        Assert.Contains(first.Code, await TitleAsync(duplicate));

        // Cùng tên ở nhóm cha khác thì được; đổi tên/chuyển nhóm cha thành trùng thì chặn; sửa thông tin khác không đổi tên thì được.
        var other = await CreateCategoryAsync(head, UniqueCode("R"));
        var elsewhere = await ReadAsync<ItemCategoryDetailDto>(await head.PostAsJsonAsync("/api/item-categories",
            new CreateItemCategoryRequest(TestCodes.Category(), "Vật tư điện", other.Id, null), Ct));
        var move = await head.PutAsJsonAsync($"/api/item-categories/{elsewhere.Id}", new UpdateItemCategoryRequest("Vật tư điện", root.Id, null, true), Ct);
        Assert.Equal(HttpStatusCode.BadRequest, move.StatusCode);
        Assert.Equal("1521", (await ReadAsync<ItemCategoryDetailDto>(await head.PutAsJsonAsync($"/api/item-categories/{elsewhere.Id}",
            new UpdateItemCategoryRequest("Vật tư điện", other.Id, "1521", true), Ct))).InventoryAccount);
    }

    [Fact]
    public async Task HangHoa_ThemCoQuyDoi_TimKhongDauVaTheoNhomCha()
    {
        var head = await HeadOfficeAdminAsync();
        var root = await CreateCategoryAsync(head, UniqueCode("R"), level: 3);
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
        var category = await CreateCategoryAsync(head, UniqueCode("R"), level: 4);
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
        var allowed = await CreateCategoryAsync(head, UniqueCode("R"), level: 4);
        var hidden = await CreateCategoryAsync(head, UniqueCode("H"), level: 4);
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
        var category = await CreateCategoryAsync(head, UniqueCode("R"), level: 4);
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
