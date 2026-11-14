using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Proc.Application.Common;
using Proc.Application.MasterData;
using Proc.Application.Organization;
using Proc.Application.Purchasing;
using Proc.Domain.Organization;
using Proc.Infrastructure.Persistence;

namespace Proc.IntegrationTests;

/// <summary>
/// Danh mục bộ phận theo công ty và PR chọn bộ phận (design.md mục 5.1, 18.5). Mỗi test một công ty mới để danh mục không lẫn nhau.
/// </summary>
[Collection(ApiCollection.Name)]
public class DepartmentTests
{
    private readonly ProcApiFactory _factory;

    public DepartmentTests(ProcApiFactory factory) => _factory = factory;

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

    private sealed record Fixture(HttpClient Client, string CompanyCode, Guid Kt, Guid ItemId, Guid Cai);

    /// <summary>Công ty mới với một mặt hàng (cái); người thao tác là quản trị của công ty đó.</summary>
    private async Task<Fixture> ArrangeAsync()
    {
        var userName = $"bp.{Guid.NewGuid():N}"[..16];
        await _factory.CreateUserAsync(userName, ProcApiFactory.TestPassword, ["TCT"]);
        var head = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, "TCT");
        var company = await ReadAsync<CompanyDetailDto>(await head.PostAsJsonAsync("/api/companies",
            new CreateCompanyRequest(UniqueCode("B"), "Công ty thử bộ phận", null, null, null, null, new DateOnly(Today.Year, Today.Month, 1), "KT", "Kho tổng"), Ct));
        var client = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, company.Code);
        var kt = (await client.GetFromJsonAsync<List<WarehouseDetailDto>>("/api/warehouses", Ct))!.Single().Id;
        var cai = (await head.GetFromJsonAsync<List<UomDto>>("/api/uoms", Ct))!.Single(u => u.Code == "CAI").Id;
        var category = await ReadAsync<ItemCategoryDetailDto>(await TestCategories.PostLeafAsync(head,
            new CreateItemCategoryRequest(TestCodes.Category(), $"Nhóm thử bộ phận {Guid.NewGuid():N}", null, null), Ct));
        var item = await ReadAsync<ItemDetailDto>(await head.PostAsJsonAsync("/api/items",
            new CreateItemRequest(null, "Bulong", null, "Inventory", category.Id, cai, null, null, null, null, "10", null, false, false, null, null, []), Ct));
        return new Fixture(client, company.Code, kt, item.Id, cai);
    }

    private static async Task<DepartmentDto> CreateDepartmentAsync(HttpClient client, string code, string name) =>
        await ReadAsync<DepartmentDto>(await client.PostAsJsonAsync("/api/departments", new CreateDepartmentRequest(code, name, null), Ct));

    private static SavePurchaseRequestRequest Pr(Fixture f, Guid? departmentId) =>
        new(Today, departmentId, "Bảo trì", Today.AddDays(7), f.Kt, "Normal", null, [new(null, f.ItemId, f.Cai, 2, null, null, null)]);

    [Fact]
    public async Task DanhMuc_ThemSuaNgungDung_TrungMa_CongTyKhacKhongThay()
    {
        var f = await ArrangeAsync();

        var created = await CreateDepartmentAsync(f.Client, " px1 ", "Phân xưởng cán 1");
        Assert.Equal(("PX1", "Phân xưởng cán 1", true, 0), (created.Code, created.Name, created.IsActive, created.RequestCount));

        var duplicate = await f.Client.PostAsJsonAsync("/api/departments", new CreateDepartmentRequest("PX1", "Trùng", null), Ct);
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        Assert.Equal("Mã bộ phận PX1 đã có ở công ty này.", await TitleAsync(duplicate));
        var badCode = await f.Client.PostAsJsonAsync("/api/departments", new CreateDepartmentRequest("PX 1", "Sai mã", null), Ct);
        Assert.Equal(HttpStatusCode.BadRequest, badCode.StatusCode);

        var updated = await ReadAsync<DepartmentDto>(await f.Client.PutAsJsonAsync(
            $"/api/departments/{created.Id}", new UpdateDepartmentRequest("Phân xưởng cán", "Nguyễn Văn A", false), Ct));
        Assert.Equal(("PX1", "Phân xưởng cán", "Nguyễn Văn A", false), (updated.Code, updated.Name, updated.ManagerName, updated.IsActive));

        // Mỗi công ty là pháp nhân độc lập (CLAUDE.md mục 4): công ty khác dùng lại được mã, không thấy bộ phận của công ty này.
        var bha = await _factory.CreateClientInCompanyAsync(ProcApiFactory.AdminUserName, ProcApiFactory.AdminPassword, "BHA");
        Assert.Equal(HttpStatusCode.NotFound, (await bha.GetAsync($"/api/departments/{created.Id}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await bha.PutAsJsonAsync(
            $"/api/departments/{created.Id}", new UpdateDepartmentRequest("Sửa trộm", null, true), Ct)).StatusCode);
        Assert.DoesNotContain((await bha.GetFromJsonAsync<List<DepartmentDto>>("/api/departments", Ct))!, d => d.Id == created.Id);

        // Thêm/sửa bộ phận vào nhật ký (entity [Auditable]).
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProcDbContext>();
        Assert.True(await db.AuditLogs.AsNoTracking().CountAsync(a => a.EntityId == created.Id.ToString(), Ct) >= 2);
    }

    [Fact]
    public async Task Pr_ChonBoPhan_HienTen_ChanBoPhanNgungDungVaCongTyKhac()
    {
        var f = await ArrangeAsync();
        var department = await CreateDepartmentAsync(f.Client, "KT01", "Phòng kỹ thuật");

        Assert.Equal(HttpStatusCode.BadRequest, (await f.Client.PostAsJsonAsync("/api/purchase-requests", Pr(f, null), Ct)).StatusCode);
        var pr = await ReadAsync<PurchaseRequestDto>(await f.Client.PostAsJsonAsync("/api/purchase-requests", Pr(f, department.Id), Ct));
        Assert.Equal((department.Id, "Phòng kỹ thuật"), (pr.DepartmentId, pr.Department));
        var list = await f.Client.GetFromJsonAsync<PurchaseDocumentListResult>("/api/purchase-requests", Ct);
        Assert.Equal("Phòng kỹ thuật", list!.Items.Single(i => i.Id == pr.Id).Description);
        Assert.Equal(1, (await f.Client.GetFromJsonAsync<DepartmentDto>($"/api/departments/{department.Id}", Ct))!.RequestCount);

        // Bộ phận ngừng dùng: PR đã chọn vẫn lưu lại được (sửa dòng không buộc đổi bộ phận), PR mới không chọn được.
        await ReadAsync<DepartmentDto>(await f.Client.PutAsJsonAsync(
            $"/api/departments/{department.Id}", new UpdateDepartmentRequest(department.Name, null, false), Ct));
        pr = await ReadAsync<PurchaseRequestDto>(await f.Client.PutAsJsonAsync($"/api/purchase-requests/{pr.Id}", Pr(f, department.Id), Ct));
        Assert.Equal("Phòng kỹ thuật", pr.Department);
        var inactive = await f.Client.PostAsJsonAsync("/api/purchase-requests", Pr(f, department.Id), Ct);
        Assert.Equal(HttpStatusCode.BadRequest, inactive.StatusCode);
        Assert.Equal("Bộ phận đề nghị đã ngừng dùng.", await TitleAsync(inactive));

        // Bộ phận của công ty khác như không tồn tại.
        var bha = await _factory.CreateClientInCompanyAsync(ProcApiFactory.AdminUserName, ProcApiFactory.AdminPassword, "BHA");
        var other = await CreateDepartmentAsync(bha, UniqueCode("X"), "Bộ phận BHA");
        Assert.Equal(HttpStatusCode.NotFound, (await f.Client.PostAsJsonAsync("/api/purchase-requests", Pr(f, other.Id), Ct)).StatusCode);
    }

    [Fact]
    public async Task PrCu_ChuaCoDanhMuc_HienChuCu_SuaLaiPhaiChonBoPhan()
    {
        var f = await ArrangeAsync();
        var department = await CreateDepartmentAsync(f.Client, "VT", "Phòng vật tư");
        var pr = await ReadAsync<PurchaseRequestDto>(await f.Client.PostAsJsonAsync("/api/purchase-requests", Pr(f, department.Id), Ct));

        // Giả lập PR lập trước lát A2: không có bộ phận trong danh mục, chỉ có cột chữ "Department" (ngừng dùng, không ghi mới).
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ProcDbContext>();
            await db.Database.ExecuteSqlRawAsync(
                "UPDATE \"PurchaseRequest\" SET \"Department\" = 'Xưởng cũ', \"DepartmentId\" = NULL WHERE \"Id\" = {0}", [pr.Id], Ct);
        }

        var legacy = await f.Client.GetFromJsonAsync<PurchaseRequestDto>($"/api/purchase-requests/{pr.Id}", Ct);
        Assert.Equal((null, "Xưởng cũ"), (legacy!.DepartmentId, legacy.Department));
        var list = await f.Client.GetFromJsonAsync<PurchaseDocumentListResult>("/api/purchase-requests", Ct);
        Assert.Equal("Xưởng cũ", list!.Items.Single(i => i.Id == pr.Id).Description);

        // PR bắt buộc có bộ phận (design.md mục 18.5): sửa lại phiếu cũ phải chọn bộ phận từ danh mục.
        var missing = await f.Client.PutAsJsonAsync($"/api/purchase-requests/{pr.Id}", Pr(f, null), Ct);
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Contains("Hãy chọn bộ phận đề nghị.", await missing.Content.ReadAsStringAsync(Ct));
        pr = await ReadAsync<PurchaseRequestDto>(await f.Client.PutAsJsonAsync($"/api/purchase-requests/{pr.Id}", Pr(f, department.Id), Ct));
        Assert.Equal((department.Id, "Phòng vật tư"), (pr.DepartmentId, pr.Department));
    }

    [Fact]
    public async Task NguoiLapPr_XemDuocDanhSachBoPhan_KhongThemSuaDuoc()
    {
        var f = await ArrangeAsync();
        await CreateDepartmentAsync(f.Client, "HC", "Phòng hành chính");
        var roleCode = UniqueCode("L");
        await _factory.CreateRoleAsync(roleCode, Permissions.Purchase.PrView, Permissions.Purchase.PrCreate);
        var userName = $"lp.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(userName, f.CompanyCode, roleCode);
        var buyer = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, f.CompanyCode);

        var departments = await buyer.GetFromJsonAsync<List<DepartmentDto>>("/api/departments", Ct);
        Assert.Equal(["HC"], departments!.Select(d => d.Code));
        Assert.Equal(HttpStatusCode.Forbidden, (await buyer.PostAsJsonAsync(
            "/api/departments", new CreateDepartmentRequest("KD", "Phòng kinh doanh", null), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await buyer.PutAsJsonAsync(
            $"/api/departments/{departments![0].Id}", new UpdateDepartmentRequest("Đổi tên", null, true), Ct)).StatusCode);
    }
}
