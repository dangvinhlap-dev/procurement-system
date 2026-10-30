using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Proc.Application.Auth;
using Proc.Application.Organization;
using Proc.Domain.Organization;

namespace Proc.IntegrationTests;

/// <summary>
/// Danh mục công ty và kho (design.md mục 4, 5.1, 5.6). Mỗi test tự tạo công ty mới bằng một quản trị riêng của tổng công ty,
/// để không đụng dữ liệu mẫu BHA/NMI mà các test khác đếm số kho.
/// </summary>
[Collection(ApiCollection.Name)]
public class CompanyWarehouseTests
{
    private readonly ProcApiFactory _factory;

    public CompanyWarehouseTests(ProcApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string UniqueCode(string prefix) => $"{prefix}{Guid.NewGuid():N}".ToUpperInvariant()[..10];

    private static string UniqueTaxCode() => Random.Shared.NextInt64(1_000_000_000, 9_999_999_999).ToString();

    private static async Task<string?> TitleAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(Ct))?.Title;

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(Ct)}");
        return (await response.Content.ReadFromJsonAsync<T>(Ct))!;
    }

    /// <summary>Quản trị mới, chỉ được gán ADMIN ở tổng công ty.</summary>
    private async Task<(string UserName, HttpClient Client)> HeadOfficeAdminAsync()
    {
        var userName = $"qt.{Guid.NewGuid():N}"[..16];
        await _factory.CreateUserAsync(userName, ProcApiFactory.TestPassword, ["TCT"]);
        return (userName, await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, "TCT"));
    }

    private static CreateCompanyRequest NewCompany(string code, string? taxCode = null) =>
        new(code, $"Công ty thử {code}", null, taxCode, "Hà Nội", null, new DateOnly(2026, 11, 1), "KT", $"Kho tổng {code}");

    /// <summary>Tạo công ty mới rồi trả client của người tạo, đang làm việc ở công ty đó.</summary>
    private async Task<(CompanyDetailDto Company, HttpClient Client, string UserName)> NewCompanyAsync()
    {
        var (userName, head) = await HeadOfficeAdminAsync();
        var company = await ReadAsync<CompanyDetailDto>(await head.PostAsJsonAsync("/api/companies", NewCompany(UniqueCode("C")), Ct));
        return (company, await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, company.Code), userName);
    }

    private static async Task<WarehouseDetailDto> CreateSubAsync(HttpClient client, string code, Guid parentId) =>
        await ReadAsync<WarehouseDetailDto>(await client.PostAsJsonAsync(
            "/api/warehouses", new CreateWarehouseRequest(code, $"Kho {code}", parentId, null, null, false), Ct));

    private static UpdateWarehouseRequest Edit(WarehouseDetailDto w, Guid? parentId = null, bool? isActive = null) =>
        new(w.Name, parentId ?? w.ParentWarehouseId, w.Address, w.Keeper, w.AllowNegativeStock, isActive ?? w.IsActive);

    [Fact]
    public async Task DanhSach_TongCongTyThayMoiCongTy_CongTyThanhVienChiThayCongTyMinh()
    {
        var (_, head) = await HeadOfficeAdminAsync();
        var all = await head.GetFromJsonAsync<List<CompanyListItemDto>>("/api/companies", Ct);
        Assert.True(all![0].IsHeadOffice);
        Assert.Contains(all, c => c.Code == "BHA");
        Assert.Contains(all, c => c.Code == "NMI");
        Assert.True(all.Single(c => c.Code == "DTV").UserCount >= 1);

        var bha = await _factory.CreateClientInCompanyAsync(ProcApiFactory.AdminUserName, ProcApiFactory.AdminPassword, "BHA");
        var own = await bha.GetFromJsonAsync<List<CompanyListItemDto>>("/api/companies", Ct);
        Assert.Equal(["BHA"], own!.Select(c => c.Code));

        // Ở công ty thành viên: xem được công ty mình nhưng không sửa được; công ty khác như không tồn tại.
        var detail = await bha.GetFromJsonAsync<CompanyDetailDto>($"/api/companies/{own![0].Id}", Ct);
        Assert.False(detail!.CanEdit);
        var nmi = await _factory.GetCompanyIdAsync("NMI");
        Assert.Equal(HttpStatusCode.NotFound, (await bha.GetAsync($"/api/companies/{nmi}", Ct)).StatusCode);
    }

    [Fact]
    public async Task ThemCongTy_TaoKhoTongVaGanNguoiTaoDeVaoLamViecNgay()
    {
        var (userName, head) = await HeadOfficeAdminAsync();
        var code = UniqueCode("C").ToLowerInvariant();

        var company = await ReadAsync<CompanyDetailDto>(await head.PostAsJsonAsync("/api/companies", NewCompany(code, "0101234567-001"), Ct));

        Assert.Equal(code.ToUpperInvariant(), company.Code);
        Assert.False(company.IsHeadOffice);
        Assert.Equal("VND", company.AccountingCurrency);
        Assert.Equal("0101234567-001", company.TaxCode);
        Assert.True(company.CanEdit);

        // Người tạo được gán vai trò của mình ở tổng công ty (ADMIN) tại công ty mới.
        var me = await head.GetFromJsonAsync<CurrentUserInfo>("/api/auth/me", Ct);
        Assert.Equal(["ADMIN"], me!.Companies.Single(c => c.Id == company.Id).RoleCodes);

        var inNew = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, company.Code);
        var warehouses = await inNew.GetFromJsonAsync<List<WarehouseDetailDto>>("/api/warehouses", Ct);
        var main = Assert.Single(warehouses!);
        Assert.Equal(("KT", "Main"), (main.Code, main.Type));
    }

    [Fact]
    public async Task ThemCongTy_ChiLamDuocOTongCongTy()
    {
        // Danh mục tổ chức do tổng công ty quản lý (design.md mục 5.6): ADMIN ở BHA cũng không thêm được công ty.
        var bha = await _factory.CreateClientInCompanyAsync(ProcApiFactory.AdminUserName, ProcApiFactory.AdminPassword, "BHA");

        var response = await bha.PostAsJsonAsync("/api/companies", NewCompany(UniqueCode("C")), Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("tổng công ty", await TitleAsync(response));
    }

    [Fact]
    public async Task ThemCongTy_KhongCoQuyenQuanLy_Tra403()
    {
        await _factory.CreateRoleAsync("XEMCTY", Permissions.System.CompanyView);
        var userName = $"xem.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(userName, "TCT", "XEMCTY");
        var client = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, "TCT");

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/companies", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/companies", NewCompany(UniqueCode("C")), Ct)).StatusCode);
    }

    [Fact]
    public async Task ThemCongTy_TrungMaHoacMaSoThue_BaoLoi()
    {
        var (_, head) = await HeadOfficeAdminAsync();
        var taxCode = UniqueTaxCode();
        await ReadAsync<CompanyDetailDto>(await head.PostAsJsonAsync("/api/companies", NewCompany(UniqueCode("C"), taxCode), Ct));

        var sameCode = await head.PostAsJsonAsync("/api/companies", NewCompany("bha"), Ct);
        Assert.Equal(HttpStatusCode.BadRequest, sameCode.StatusCode);
        Assert.Equal("Mã công ty BHA đã có.", await TitleAsync(sameCode));

        var sameTax = await head.PostAsJsonAsync("/api/companies", NewCompany(UniqueCode("C"), taxCode), Ct);
        Assert.Equal(HttpStatusCode.BadRequest, sameTax.StatusCode);
        Assert.Contains($"Mã số thuế {taxCode}", await TitleAsync(sameTax));

        var badTax = await head.PostAsJsonAsync("/api/companies", NewCompany(UniqueCode("C"), "12345"), Ct);
        Assert.Equal(HttpStatusCode.BadRequest, badTax.StatusCode);
    }

    [Fact]
    public async Task SuaCongTy_NgungDung_KhongChonLamViecDuocNua()
    {
        var (userName, head) = await HeadOfficeAdminAsync();
        var company = await ReadAsync<CompanyDetailDto>(await head.PostAsJsonAsync("/api/companies", NewCompany(UniqueCode("C")), Ct));

        var updated = await ReadAsync<CompanyDetailDto>(await head.PutAsJsonAsync($"/api/companies/{company.Id}",
            new UpdateCompanyRequest("Tên mới", "TM", null, "Đà Nẵng", "Lê Văn B", null, IsActive: false), Ct));

        Assert.Equal(("Tên mới", "TM", "Đà Nẵng", false), (updated.Name, updated.ShortName, updated.Address, updated.IsActive));
        var inNew = await _factory.CreateAuthenticatedClientAsync(userName, ProcApiFactory.TestPassword);
        inNew.DefaultRequestHeaders.Add("X-Company-Id", company.Id.ToString());
        Assert.Equal(HttpStatusCode.Forbidden, (await inNew.GetAsync("/api/warehouses", Ct)).StatusCode);
    }

    [Fact]
    public async Task SuaCongTy_KhongNgungDungDuocTongCongTy()
    {
        var (_, head) = await HeadOfficeAdminAsync();
        var tct = await head.GetFromJsonAsync<CompanyDetailDto>($"/api/companies/{await _factory.GetCompanyIdAsync("TCT")}", Ct);

        var response = await head.PutAsJsonAsync($"/api/companies/{tct!.Id}",
            new UpdateCompanyRequest(tct.Name, tct.ShortName, tct.TaxCode, tct.Address, tct.Representative, tct.StartDate, IsActive: false), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Không ngừng dùng được tổng công ty.", await TitleAsync(response));
    }

    [Fact]
    public async Task Kho_ThemKhoPhu_TrungMaBaoLoi()
    {
        var (_, client, _) = await NewCompanyAsync();
        var main = (await client.GetFromJsonAsync<List<WarehouseDetailDto>>("/api/warehouses", Ct))!.Single();

        var sub = await CreateSubAsync(client, "kp1", main.Id);
        Assert.Equal(("KP1", "Sub", main.Id, "KT"), (sub.Code, sub.Type, sub.ParentWarehouseId, sub.ParentCode));

        var duplicate = await client.PostAsJsonAsync("/api/warehouses", new CreateWarehouseRequest("KP1", "Trùng", main.Id, null, null, false), Ct);
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        Assert.Equal("Mã kho KP1 đã có ở công ty này.", await TitleAsync(duplicate));

        var list = await client.GetFromJsonAsync<List<WarehouseDetailDto>>("/api/warehouses", Ct);
        Assert.Equal(["KT", "KP1"], list!.Select(w => w.Code));
    }

    [Fact]
    public async Task Kho_KhoChaCuaCongTyKhac_KhongTimThay()
    {
        // Kho cha lấy qua global query filter: kho của công ty khác như không tồn tại – design.md mục 4.
        var (_, client, _) = await NewCompanyAsync();
        var admin = await _factory.CreateClientInCompanyAsync(ProcApiFactory.AdminUserName, ProcApiFactory.AdminPassword, "NMI");
        var nmiMain = (await admin.GetFromJsonAsync<List<WarehouseDetailDto>>("/api/warehouses", Ct))!.First(w => w.Type == "Main");

        var response = await client.PostAsJsonAsync("/api/warehouses", new CreateWarehouseRequest("KP9", "Kho lạ", nmiMain.Id, null, null, false), Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Kho_SuaThongTinVaDoiKhoCha_ChanVongLap()
    {
        var (_, client, _) = await NewCompanyAsync();
        var main = (await client.GetFromJsonAsync<List<WarehouseDetailDto>>("/api/warehouses", Ct))!.Single();
        var a = await CreateSubAsync(client, "KA", main.Id);
        var b = await CreateSubAsync(client, "KB", a.Id);

        var edited = await ReadAsync<WarehouseDetailDto>(await client.PutAsJsonAsync($"/api/warehouses/{b.Id}",
            new UpdateWarehouseRequest("Kho B mới", main.Id, "Bình Dương", "Trần Bình", true, true), Ct));
        Assert.Equal(("Kho B mới", main.Id, "Bình Dương", "Trần Bình", true), (edited.Name, edited.ParentWarehouseId, edited.Address, edited.Keeper, edited.AllowNegativeStock));

        // A → con của B, rồi B → con của A thì thành vòng.
        await ReadAsync<WarehouseDetailDto>(await client.PutAsJsonAsync($"/api/warehouses/{a.Id}", Edit(a, parentId: b.Id), Ct));
        var cycle = await client.PutAsJsonAsync($"/api/warehouses/{b.Id}", Edit(edited, parentId: a.Id), Ct);
        Assert.Equal(HttpStatusCode.BadRequest, cycle.StatusCode);
        Assert.Contains("vòng", await TitleAsync(cycle));
    }

    [Fact]
    public async Task Kho_NgungDung_ChanKhoTongVaKhoConDangDung()
    {
        var (_, client, _) = await NewCompanyAsync();
        var main = (await client.GetFromJsonAsync<List<WarehouseDetailDto>>("/api/warehouses", Ct))!.Single();
        var parent = await CreateSubAsync(client, "KCHA", main.Id);
        var child = await CreateSubAsync(client, "KCON", parent.Id);

        var mainOff = await client.PutAsJsonAsync($"/api/warehouses/{main.Id}", Edit(main, isActive: false), Ct);
        Assert.Equal("Không ngừng dùng được kho tổng của công ty.", await TitleAsync(mainOff));

        var parentOff = await client.PutAsJsonAsync($"/api/warehouses/{parent.Id}", Edit(parent, isActive: false), Ct);
        Assert.Equal(HttpStatusCode.BadRequest, parentOff.StatusCode);
        Assert.Contains("KCON", await TitleAsync(parentOff));

        await ReadAsync<WarehouseDetailDto>(await client.PutAsJsonAsync($"/api/warehouses/{child.Id}", Edit(child, isActive: false), Ct));
        await ReadAsync<WarehouseDetailDto>(await client.PutAsJsonAsync($"/api/warehouses/{parent.Id}", Edit(parent, isActive: false), Ct));

        // Kho cha đã ngừng thì không treo kho mới vào được.
        var under = await client.PostAsJsonAsync("/api/warehouses", new CreateWarehouseRequest("KMOI", "Kho mới", parent.Id, null, null, false), Ct);
        Assert.Equal(HttpStatusCode.BadRequest, under.StatusCode);
    }

    [Fact]
    public async Task Kho_NguoiBiGioiHanKho_KhongThemDuoc_ChiSuaKhoDuocGan()
    {
        var (company, client, _) = await NewCompanyAsync();
        var main = (await client.GetFromJsonAsync<List<WarehouseDetailDto>>("/api/warehouses", Ct))!.Single();
        var kp1 = await CreateSubAsync(client, "KP1", main.Id);
        var kp2 = await CreateSubAsync(client, "KP2", main.Id);

        var roleCode = UniqueCode("QLK");
        await _factory.CreateRoleAsync(roleCode, Permissions.Organization.WarehouseView, Permissions.Organization.WarehouseManage);
        var userName = $"qlk.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(userName, company.Code, roleCode, warehouseCodes: ["KP1"]);
        var limited = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, company.Code);

        // Kho mới nằm ngoài danh sách kho được gán – người bị giới hạn kho không thêm được.
        var create = await limited.PostAsJsonAsync("/api/warehouses", new CreateWarehouseRequest("KP3", "Kho 3", main.Id, null, null, false), Ct);
        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);

        // Sửa kho được gán mà không đổi kho cha: không cần thấy kho cha (kho tổng nằm ngoài phạm vi của người này).
        await ReadAsync<WarehouseDetailDto>(await limited.PutAsJsonAsync($"/api/warehouses/{kp1.Id}", Edit(kp1) with { Keeper = "Thủ kho 1" }, Ct));
        Assert.Equal(HttpStatusCode.NotFound, (await limited.PutAsJsonAsync($"/api/warehouses/{kp2.Id}", Edit(kp2), Ct)).StatusCode);
    }
}
