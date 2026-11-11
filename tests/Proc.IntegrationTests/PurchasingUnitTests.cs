using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Proc.Application.Common;
using Proc.Application.Organization;
using Proc.Application.Purchasing;
using Proc.Domain.Organization;

namespace Proc.IntegrationTests;

/// <summary>
/// Danh mục đơn vị mua (lát Y5a, design.md mục 19.3 – Q3): mã duy nhất trong công ty, không đổi; ngừng dùng thì không còn trong danh sách
/// chọn; phạm vi công ty; quyền xem / quản lý riêng. Mỗi test một công ty mới.
/// </summary>
[Collection(ApiCollection.Name)]
public class PurchasingUnitTests
{
    private readonly ProcApiFactory _factory;

    public PurchasingUnitTests(ProcApiFactory factory) => _factory = factory;

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

    /// <summary>Công ty mới, người dùng là quản trị.</summary>
    private async Task<(HttpClient Client, string CompanyCode)> ArrangeAsync()
    {
        var userName = $"dvm.{Guid.NewGuid():N}"[..16];
        await _factory.CreateUserAsync(userName, ProcApiFactory.TestPassword, ["TCT"]);
        var tct = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, "TCT");
        var company = await ReadAsync<CompanyDetailDto>(await tct.PostAsJsonAsync("/api/companies",
            new CreateCompanyRequest(UniqueCode("C"), "Công ty thử đơn vị mua", null, null, null, null, new DateOnly(Today.Year, Today.Month, 1), "KT", "Kho tổng"), Ct));
        var client = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, company.Code);
        return (client, company.Code);
    }

    [Fact]
    public async Task ThemSuaDonViMua_MaDuyNhat_NgungDungThiKhongConTrongDanhSachChon()
    {
        var (client, _) = await ArrangeAsync();

        var created = await ReadAsync<PurchasingUnitDto>(await client.PostAsJsonAsync("/api/purchasing-units",
            new CreatePurchasingUnitRequest("vpndd", "Văn phòng người đại diện", "Mua hàng ăn uống"), Ct));
        Assert.Equal("VPNDD", created.Code);
        Assert.True(created.IsActive);
        await ReadAsync<PurchasingUnitDto>(await client.PostAsJsonAsync("/api/purchasing-units",
            new CreatePurchasingUnitRequest("CUVT", "Phòng Cung ứng vật tư", null), Ct));

        // Mã trùng trong công ty bị chặn (mã tự viết hoa); mã có dấu / ký tự lạ bị chặn.
        var duplicate = await client.PostAsJsonAsync("/api/purchasing-units", new CreatePurchasingUnitRequest("VPNDD", "Trùng", null), Ct);
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        Assert.Contains("đã có", await TitleAsync(duplicate));
        var invalid = await client.PostAsJsonAsync("/api/purchasing-units", new CreatePurchasingUnitRequest("Đơn vị", "Sai mã", null), Ct);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        var options = await client.GetFromJsonAsync<List<PurchasingUnitOptionDto>>("/api/purchasing-units/options", Ct);
        Assert.Equal(["CUVT", "VPNDD"], options!.Select(o => o.Code));

        // Ngừng dùng: vẫn trong danh mục, không còn trong danh sách chọn; ghi chú rỗng thì bỏ.
        var updated = await ReadAsync<PurchasingUnitDto>(await client.PutAsJsonAsync($"/api/purchasing-units/{created.Id}",
            new UpdatePurchasingUnitRequest("VP người đại diện", "  ", false), Ct));
        Assert.Equal("VPNDD", updated.Code);
        Assert.Equal("VP người đại diện", updated.Name);
        Assert.Null(updated.Note);
        Assert.False(updated.IsActive);
        Assert.Equal(2, (await client.GetFromJsonAsync<List<PurchasingUnitDto>>("/api/purchasing-units", Ct))!.Count);
        options = await client.GetFromJsonAsync<List<PurchasingUnitOptionDto>>("/api/purchasing-units/options", Ct);
        Assert.Equal(["CUVT"], options!.Select(o => o.Code));
    }

    [Fact]
    public async Task DonViMua_TheoCongTy_VaQuyenXemQuanLy()
    {
        var (client, companyCode) = await ArrangeAsync();
        var unit = await ReadAsync<PurchasingUnitDto>(await client.PostAsJsonAsync("/api/purchasing-units",
            new CreatePurchasingUnitRequest("CUVT", "Phòng Cung ứng vật tư", null), Ct));

        // Công ty khác không thấy, không sửa được; cùng mã ở công ty khác vẫn tạo được (mã duy nhất trong công ty).
        var otherName = $"dvm2.{Guid.NewGuid():N}"[..16];
        await _factory.CreateUserAsync(otherName, ProcApiFactory.TestPassword, ["TCT"]);
        var tct = await _factory.CreateClientInCompanyAsync(otherName, ProcApiFactory.TestPassword, "TCT");
        Assert.Equal(HttpStatusCode.NotFound, (await tct.GetAsync($"/api/purchasing-units/{unit.Id}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await tct.PutAsJsonAsync($"/api/purchasing-units/{unit.Id}",
            new UpdatePurchasingUnitRequest("Sửa trộm", null, true), Ct)).StatusCode);

        // Chỉ quyền xem: đọc được, không thêm / sửa được.
        var viewerRole = $"DVV{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        await _factory.CreateRoleAsync(viewerRole, Permissions.Purchase.UnitView);
        var viewerName = $"dvv.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(viewerName, companyCode, viewerRole);
        var viewer = await _factory.CreateClientInCompanyAsync(viewerName, ProcApiFactory.TestPassword, companyCode);
        Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync($"/api/purchasing-units/{unit.Id}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsJsonAsync("/api/purchasing-units",
            new CreatePurchasingUnitRequest("NEW", "Mới", null), Ct)).StatusCode);

        // Chỉ quyền xem đề nghị mua: lấy được danh sách chọn, không xem danh mục.
        var prRole = $"DVP{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        await _factory.CreateRoleAsync(prRole, Permissions.Purchase.PrView);
        var prName = $"dvp.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(prName, companyCode, prRole);
        var prViewer = await _factory.CreateClientInCompanyAsync(prName, ProcApiFactory.TestPassword, companyCode);
        Assert.Contains((await prViewer.GetFromJsonAsync<List<PurchasingUnitOptionDto>>("/api/purchasing-units/options", Ct))!, o => o.Id == unit.Id);
        Assert.Equal(HttpStatusCode.Forbidden, (await prViewer.GetAsync("/api/purchasing-units", Ct)).StatusCode);
    }
}
