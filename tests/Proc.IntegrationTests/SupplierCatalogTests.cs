using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Proc.Application.MasterData;
using Proc.Application.Organization;

namespace Proc.IntegrationTests;

/// <summary>
/// Nhóm NCC và nhà cung cấp (design.md mục 5.3, 5.8): người liên hệ, tài khoản ngân hàng, mã số thuế không trùng,
/// trạng thái có lý do, NCC nội bộ; chỉ sửa ở tổng công ty.
/// </summary>
[Collection(ApiCollection.Name)]
public class SupplierCatalogTests
{
    private readonly ProcApiFactory _factory;

    public SupplierCatalogTests(ProcApiFactory factory) => _factory = factory;

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

    private async Task<HttpClient> HeadOfficeAdminAsync()
    {
        var userName = $"qt.{Guid.NewGuid():N}"[..16];
        await _factory.CreateUserAsync(userName, ProcApiFactory.TestPassword, ["TCT", "BHA"]);
        return await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, "TCT");
    }

    private static async Task<Guid> GroupIdAsync(HttpClient client, string code) =>
        (await client.GetFromJsonAsync<List<SupplierGroupDto>>("/api/supplier-groups", Ct))!.Single(g => g.Code == code).Id;

    private static SaveSupplierRequest NewSupplier(
        string code, Guid groupId, string? taxCode = null, string status = "Potential", string? note = null, Guid? internalCompanyId = null) =>
        new(code, groupId, "Công ty TNHH Thép Việt", "Thép Việt", false, taxCode ?? UniqueTaxCode(), "Hà Nội", "024 3888 9999",
            "ban@thepviet.vn", "thepviet.vn", "Chuyển khoản 30 ngày", "Thép tấm", 4, status, note, internalCompanyId,
            [new(null, "Nguyễn Văn An", "Giám đốc kinh doanh", "0912 345 678", "an@thepviet.vn", true), new(null, "Trần Bình", null, null, null, false)],
            [new(null, "Vietcombank", "Hà Nội", "0011 0000 12345", "CTY TNHH THEP VIET", true)]);

    private static SaveSupplierRequest Edit(SupplierDetailDto d) =>
        new(null, d.GroupId, d.Name, d.ShortName, d.IsForeign, d.TaxCode, d.Address, d.Phone, d.Email, d.Website, d.PaymentTerms,
            d.SupplyScope, d.Rating, d.Status, d.StatusNote, d.InternalCompanyId, d.Contacts, d.BankAccounts);

    [Fact]
    public async Task NhomNcc_ThemTrungMaVaChiLamOTongCongTy()
    {
        var head = await HeadOfficeAdminAsync();
        Assert.Contains((await head.GetFromJsonAsync<List<SupplierGroupDto>>("/api/supplier-groups", Ct))!, g => g.Code == "TN");

        var code = UniqueCode("G");
        await ReadAsync<SupplierGroupDto>(await head.PostAsJsonAsync("/api/supplier-groups", new CreateSupplierGroupRequest(code, "Nhóm thử"), Ct));
        var duplicate = await head.PostAsJsonAsync("/api/supplier-groups", new CreateSupplierGroupRequest(code, "Trùng"), Ct);
        Assert.Equal($"Mã nhóm NCC {code} đã có.", await TitleAsync(duplicate));

        var bha = await _factory.CreateClientInCompanyAsync(ProcApiFactory.AdminUserName, ProcApiFactory.AdminPassword, "BHA");
        var atMember = await bha.PostAsJsonAsync("/api/supplier-groups", new CreateSupplierGroupRequest(UniqueCode("G"), "Thử"), Ct);
        Assert.Equal(HttpStatusCode.Forbidden, atMember.StatusCode);
    }

    [Fact]
    public async Task Ncc_ThemCoLienHeVaNganHang_TimKhongDauVaTheoMaSoThue()
    {
        var head = await HeadOfficeAdminAsync();
        var code = UniqueCode("N");
        var taxCode = UniqueTaxCode();

        var supplier = await ReadAsync<SupplierDetailDto>(await head.PostAsJsonAsync(
            "/api/suppliers", NewSupplier(code.ToLowerInvariant(), await GroupIdAsync(head, "TN"), taxCode), Ct));

        Assert.Equal((code, "Potential", 4, true), (supplier.Code, supplier.Status, supplier.Rating, supplier.CanEdit));
        Assert.Equal(2, supplier.Contacts.Count);
        Assert.Equal("0011000012345", Assert.Single(supplier.BankAccounts).AccountNumber);

        var byName = await head.GetFromJsonAsync<SupplierListResult>($"/api/suppliers?search=thep%20viet%20{code}", Ct);
        var row = Assert.Single(byName!.Items);
        Assert.Equal(("TN", "Nguyễn Văn An – 0912 345 678"), (row.GroupCode, row.PrimaryContact));
        var byTax = await head.GetFromJsonAsync<SupplierListResult>($"/api/suppliers?search={taxCode}", Ct);
        Assert.Equal(code, Assert.Single(byTax!.Items).Code);

        var sameTax = await head.PostAsJsonAsync("/api/suppliers", NewSupplier(UniqueCode("N"), supplier.GroupId, taxCode), Ct);
        Assert.Equal($"Mã số thuế {taxCode} đã dùng cho NCC {code}.", await TitleAsync(sameTax));
        var sameCode = await head.PostAsJsonAsync("/api/suppliers", NewSupplier(code, supplier.GroupId), Ct);
        Assert.Equal($"Mã NCC {code} đã có.", await TitleAsync(sameCode));
    }

    [Fact]
    public async Task Ncc_SuaLienHeTheoId_GiuDongCu_ThemVaXoaDong()
    {
        var head = await HeadOfficeAdminAsync();
        var supplier = await ReadAsync<SupplierDetailDto>(await head.PostAsJsonAsync(
            "/api/suppliers", NewSupplier(UniqueCode("N"), await GroupIdAsync(head, "TN")), Ct));
        var an = supplier.Contacts.Single(c => c.Name == "Nguyễn Văn An");

        var edited = await ReadAsync<SupplierDetailDto>(await head.PutAsJsonAsync($"/api/suppliers/{supplier.Id}",
            Edit(supplier) with
            {
                Contacts = [an with { Position = "Phó giám đốc" }, new(null, "Lê Cường", "Kế toán", null, "cuong@thepviet.vn", false)],
                BankAccounts = [],
            }, Ct));

        Assert.Equal(["Lê Cường", "Nguyễn Văn An"], edited.Contacts.Select(c => c.Name).Order(StringComparer.Ordinal));
        Assert.Equal((an.Id, "Phó giám đốc"), (edited.Contacts.Single(c => c.Name == "Nguyễn Văn An").Id, edited.Contacts.Single(c => c.Name == "Nguyễn Văn An").Position));
        Assert.Empty(edited.BankAccounts);

        var twoPrimary = await head.PutAsJsonAsync($"/api/suppliers/{supplier.Id}",
            Edit(edited) with { Contacts = edited.Contacts.Select(c => c with { IsPrimary = true }).ToList() }, Ct);
        Assert.Equal("Chỉ chọn một người liên hệ chính.", await TitleAsync(twoPrimary));
    }

    [Fact]
    public async Task Ncc_DanhSachDenPhaiCoLyDo_LocTheoTrangThai()
    {
        var head = await HeadOfficeAdminAsync();
        var groupId = await GroupIdAsync(head, "TN");
        var noReason = await head.PostAsJsonAsync("/api/suppliers", NewSupplier(UniqueCode("N"), groupId, status: "Blacklisted"), Ct);
        Assert.Equal("Tạm ngưng hoặc đưa NCC vào danh sách đen phải ghi lý do.", await TitleAsync(noReason));

        var code = UniqueCode("N");
        await ReadAsync<SupplierDetailDto>(await head.PostAsJsonAsync("/api/suppliers",
            NewSupplier(code, groupId, status: "Blacklisted", note: "Giao hàng giả"), Ct));
        var blacklisted = await head.GetFromJsonAsync<SupplierListResult>($"/api/suppliers?status=Blacklisted&search={code}", Ct);
        Assert.Equal("Blacklisted", Assert.Single(blacklisted!.Items).Status);
        var approved = await head.GetFromJsonAsync<SupplierListResult>($"/api/suppliers?status=Approved&search={code}", Ct);
        Assert.Empty(approved!.Items);
    }

    [Fact]
    public async Task Ncc_NoiBo_MoiCongTyToiDaMotNcc()
    {
        var head = await HeadOfficeAdminAsync();
        var company = await ReadAsync<CompanyDetailDto>(await head.PostAsJsonAsync("/api/companies",
            new CreateCompanyRequest(UniqueCode("C"), "Công ty thử NCC nội bộ", null, null, null, null, null, "KT", "Kho tổng"), Ct));
        var groupId = await GroupIdAsync(head, "NB");

        var first = await ReadAsync<SupplierDetailDto>(await head.PostAsJsonAsync("/api/suppliers",
            NewSupplier(UniqueCode("NB"), groupId, internalCompanyId: company.Id), Ct));
        Assert.Equal(company.Id, first.InternalCompanyId);
        var list = await head.GetFromJsonAsync<SupplierListResult>($"/api/suppliers?search={first.Code}", Ct);
        Assert.Equal(company.Code, Assert.Single(list!.Items).InternalCompanyCode);

        var second = await head.PostAsJsonAsync("/api/suppliers", NewSupplier(UniqueCode("NB"), groupId, internalCompanyId: company.Id), Ct);
        Assert.Equal($"Công ty {company.Code} đã có NCC nội bộ {first.Code}.", await TitleAsync(second));
    }

    [Fact]
    public async Task Ncc_CongTyThanhVienSuaTheoQuyenSuaDanhMucDungChung_NhomConNccKhongNgungDuoc()
    {
        var head = await HeadOfficeAdminAsync();
        var groupCode = UniqueCode("G");
        var group = await ReadAsync<SupplierGroupDto>(await head.PostAsJsonAsync("/api/supplier-groups", new CreateSupplierGroupRequest(groupCode, "Nhóm thử"), Ct));
        var supplier = await ReadAsync<SupplierDetailDto>(await head.PostAsJsonAsync("/api/suppliers", NewSupplier(UniqueCode("N"), group.Id), Ct));

        var stop = await head.PutAsJsonAsync($"/api/supplier-groups/{group.Id}", new UpdateSupplierGroupRequest("Nhóm thử", false), Ct);
        Assert.Equal(HttpStatusCode.BadRequest, stop.StatusCode);
        Assert.Contains("1 NCC", await TitleAsync(stop));

        var bha = await _factory.CreateClientInCompanyAsync(ProcApiFactory.AdminUserName, ProcApiFactory.AdminPassword, "BHA");
        // Quản trị có quyền "Sửa thông tin danh mục dùng chung": sửa được ở công ty thành viên (design.md mục 5.7); người chỉ có quyền
        // quản lý NCC thì không – xem SharedCatalogEditTests.
        var detail = await bha.GetFromJsonAsync<SupplierDetailDto>($"/api/suppliers/{supplier.Id}", Ct);
        Assert.True(detail!.CanEdit);
        Assert.Equal(supplier.Code, (await ReadAsync<SupplierDetailDto>(await bha.PutAsJsonAsync($"/api/suppliers/{supplier.Id}", Edit(detail), Ct))).Code);
    }
}
