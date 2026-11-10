using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Proc.Api.Security;
using Proc.Application.Administration;
using Proc.Application.Auth;
using Proc.Domain.Organization;

namespace Proc.IntegrationTests;

/// <summary>
/// Lát K1 – tắt phân hệ kho từ bản 0.22.0 (design.md mục 19.4): cấu hình <c>Modules:InventoryEnabled=false</c> bỏ quyền kho khi nạp phạm vi,
/// khi trả quyền cho giao diện, trên màn hình phân quyền và vai trò mẫu; API kho trả 403 kèm lý do; quyền kho cũ trong vai trò không bị xóa.
/// CSDL test mặc định bật kho (test cũ dựa trên phiếu nhập) nên ở đây dựng API riêng với cấu hình tắt.
/// </summary>
[Collection(ApiCollection.Name)]
public class InventoryModuleDisabledTests
{
    private readonly ProcApiFactory _factory;

    public InventoryModuleDisabledTests(ProcApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>API dùng chung CSDL test nhưng tắt phân hệ kho (cấu hình thêm sau cùng nên thắng appsettings.Testing.json).</summary>
    private WebApplicationFactory<Program> Disabled() =>
        _factory.WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, c) =>
            c.AddInMemoryCollection(new Dictionary<string, string?> { ["Modules:InventoryEnabled"] = "false" })));

    /// <summary>Đăng nhập vào API đã cho và chọn công ty làm việc.</summary>
    private async Task<HttpClient> LoginAsync(WebApplicationFactory<Program> app, string userName, string password, string company)
    {
        var client = app.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(userName, password), Ct);
        response.EnsureSuccessStatusCode();
        var login = await response.Content.ReadFromJsonAsync<LoginResult>(Ct);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.AccessToken);
        client.DefaultRequestHeaders.Add("X-Company-Id", (await _factory.GetCompanyIdAsync(company)).ToString());
        return client;
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(Ct)}");
        return (await response.Content.ReadFromJsonAsync<T>(Ct))!;
    }

    private static async Task AssertDisabledAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(PermissionEndpointExtensions.InventoryDisabledMessage,
            (await response.Content.ReadFromJsonAsync<ProblemDetails>(Ct))?.Title);
    }

    [Fact]
    public async Task TatKho_ThuKhoMatQuyenKho_ApiKhoTra403CoLyDo_PhanQuyenVaVaiTroMauKhongConQuyenKho()
    {
        await using var app = Disabled();

        // Thủ kho (vai trò THUKHO có quyền kho trong CSDL): giao diện không nhận quyền kho nào, còn quyền xem danh mục kho (nơi nhận hàng).
        var thukho = await LoginAsync(app, "thukho.bha", "Demo@2026", "BHA");
        var me = await thukho.GetFromJsonAsync<CurrentUserInfo>("/api/auth/me", Ct);
        var permissions = me!.Companies.Single(c => c.Code == "BHA").Permissions;
        Assert.DoesNotContain(permissions, Permissions.InventoryModule.Contains);
        Assert.Contains(Permissions.Organization.WarehouseView, permissions);

        // API kho – đọc, ghi, kỳ kho, báo cáo kho – đều khóa với câu báo phân hệ đã ngừng dùng.
        await AssertDisabledAsync(await thukho.GetAsync("/api/stock-on-hand", Ct));
        await AssertDisabledAsync(await thukho.GetAsync("/api/inventory-documents?docType=GRN", Ct));
        await AssertDisabledAsync(await thukho.GetAsync("/api/inventory-periods", Ct));
        await AssertDisabledAsync(await thukho.PostAsync($"/api/inventory-documents/{Guid.NewGuid()}/post", null, Ct));

        // Quản trị: danh mục quyền không còn nhóm Kho, báo cáo tồn, xuất kế toán; vai trò mẫu không còn quyền kho.
        var admin = await LoginAsync(app, ProcApiFactory.AdminUserName, ProcApiFactory.AdminPassword, "BHA");
        var groups = await admin.GetFromJsonAsync<List<PermissionGroupDto>>("/api/permissions", Ct);
        Assert.DoesNotContain(groups!, g => g.Module == "Inventory");
        var codes = groups!.SelectMany(g => g.Permissions).Select(p => p.Code).ToList();
        Assert.DoesNotContain(codes, Permissions.InventoryModule.Contains);
        Assert.Contains(Permissions.Report.PurchaseView, codes);
        await AssertDisabledAsync(await admin.GetAsync("/api/stock-on-hand", Ct));
        // Bảng giá nội bộ (IPR) vẫn dùng – danh sách công ty không còn phụ thuộc quyền kho.
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/group-companies", Ct)).StatusCode);

        var templates = await admin.GetFromJsonAsync<List<RoleTemplateDto>>("/api/roles/templates", Ct);
        Assert.Contains(templates!, t => t.Code == "KHO");
        Assert.DoesNotContain(templates!.SelectMany(t => t.PermissionCodes), Permissions.InventoryModule.Contains);
    }

    [Fact]
    public async Task TatKho_SuaVaiTroCoQuyenKhoCu_SuaDuoc_QuyenKhoCuGiuNguyenTrongCsdl()
    {
        var code = $"K1{Guid.NewGuid():N}".ToUpperInvariant()[..12];
        var roleId = await _factory.CreateRoleAsync(code, Permissions.MasterData.ItemView, Permissions.Inventory.StockView);
        await using var app = Disabled();
        var admin = await LoginAsync(app, ProcApiFactory.AdminUserName, ProcApiFactory.AdminPassword, "BHA");

        // Quyền kho cũ không hiện và không chặn sửa (quản trị cũng không còn quyền kho).
        var role = await admin.GetFromJsonAsync<RoleDetailDto>($"/api/roles/{roleId}", Ct);
        Assert.Equal([Permissions.MasterData.ItemView], role!.PermissionCodes);
        Assert.True(role.CanEdit);

        var updated = await ReadAsync<RoleDetailDto>(await admin.PutAsJsonAsync($"/api/roles/{roleId}",
            new UpdateRoleRequest("Vai trò K1", true, [Permissions.MasterData.ItemView, Permissions.Purchase.PrView]), Ct));
        Assert.Equal([Permissions.MasterData.ItemView, Permissions.Purchase.PrView], updated.PermissionCodes.Order(StringComparer.Ordinal));

        // Không xóa dữ liệu: bật lại kho thì vai trò vẫn có quyền kho cũ (design.md mục 19.4).
        var enabledAdmin = await _factory.CreateClientInCompanyAsync(ProcApiFactory.AdminUserName, ProcApiFactory.AdminPassword, "BHA");
        var stored = await enabledAdmin.GetFromJsonAsync<RoleDetailDto>($"/api/roles/{roleId}", Ct);
        Assert.Contains(Permissions.Inventory.StockView, stored!.PermissionCodes);
        Assert.Contains(Permissions.Purchase.PrView, stored.PermissionCodes);
    }
}
