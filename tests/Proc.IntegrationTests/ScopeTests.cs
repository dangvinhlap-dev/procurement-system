using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Proc.Application.Auth;
using Proc.Application.Common;
using Proc.Application.Organization;
using Proc.Domain.Organization;
using Proc.Infrastructure.Persistence;
using Proc.Infrastructure.Security;

namespace Proc.IntegrationTests;

/// <summary>
/// Phân quyền và phạm vi dữ liệu (design.md mục 9.1, 9.2; CLAUDE.md mục 4) trên procurement_test.
/// Dữ liệu mẫu: BHA có kho KT, KP1, KP2; NMI có KT, KP1; cây nhóm hàng VT → VTD, VTC → VTCK; NVL; VPP.
/// </summary>
[Collection(ApiCollection.Name)]
public class ScopeTests
{
    private readonly ProcApiFactory _factory;

    public ScopeTests(ProcApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string UniqueName(string prefix) => $"{prefix}{Guid.NewGuid():N}"[..20];

    private async Task<HttpClient> ClientForNewUserAsync(
        string company, string role, string[]? warehouses = null, string[]? categories = null, string? headerCompany = "")
    {
        var userName = UniqueName("u");
        await _factory.CreateScopedUserAsync(userName, company, role, warehouses, categories);
        return await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, headerCompany == "" ? company : headerCompany);
    }

    [Fact]
    public async Task NguoiCongTyA_ChiThayKhoCongTyA()
    {
        var bha = await ClientForNewUserAsync("BHA", "THUKHO");

        var warehouses = await bha.GetFromJsonAsync<List<WarehouseDto>>("/api/warehouses", Ct);
        Assert.NotNull(warehouses);

        // Kho tổng đứng đầu, rồi kho phụ theo mã; không có kho nào của NMI (NMI cũng có mã KT, KP1 nhưng tên khác).
        Assert.Equal(["KT", "KP1", "KP2"], warehouses.Select(w => w.Code));
        Assert.Equal("Kho tổng Bắc Hà", warehouses[0].Name);
        Assert.DoesNotContain(warehouses, w => w.Name.Contains("Miền Nam") || w.Name.Contains("Bình Dương"));
    }

    [Fact]
    public async Task NguoiCongTyA_GuiHeaderCongTyB_BiTuChoi403()
    {
        // Tự sửa header X-Company-Id sang công ty không được gán cũng không đọc được dữ liệu – không tin client.
        var client = await ClientForNewUserAsync("BHA", "THUKHO", headerCompany: "NMI");

        var response = await client.GetAsync("/api/warehouses", Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("Bạn không được làm việc tại công ty này.",
            (await response.Content.ReadFromJsonAsync<ProblemDetails>(Ct))?.Title);
    }

    [Fact]
    public async Task CungMotNguoi_DoiCongTy_ThayDuLieuCongTyDo()
    {
        var admin = await _factory.CreateClientInCompanyAsync(ProcApiFactory.AdminUserName, ProcApiFactory.AdminPassword, "NMI");

        var warehouses = await admin.GetFromJsonAsync<List<WarehouseDto>>("/api/warehouses", Ct);

        Assert.Equal(["Kho tổng Miền Nam", "Kho phụ Bình Dương"], warehouses!.Select(w => w.Name));
    }

    [Fact]
    public async Task KhongGuiCongTy_Tra400()
    {
        var client = await ClientForNewUserAsync("BHA", "THUKHO", headerCompany: null);

        var response = await client.GetAsync("/api/warehouses", Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.StartsWith("Chưa chọn công ty làm việc", (await response.Content.ReadFromJsonAsync<ProblemDetails>(Ct))?.Title);
    }

    [Fact]
    public async Task HeaderCongTySaiDinhDang_Tra400()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        client.DefaultRequestHeaders.Add("X-Company-Id", "khong-phai-guid");

        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/warehouses", Ct)).StatusCode);
    }

    [Fact]
    public async Task PhamViKho_ChiThayKhoDuocGan()
    {
        var client = await ClientForNewUserAsync("BHA", "THUKHO", warehouses: ["KP1"]);

        var warehouses = await client.GetFromJsonAsync<List<WarehouseDto>>("/api/warehouses", Ct);

        Assert.Equal(["KP1"], warehouses!.Select(w => w.Code));
    }

    [Fact]
    public async Task PhamViNhomHang_KeThuaXuongNhomCon()
    {
        // Được phép VTC (Vật tư cơ khí) thì được cả VTCK (con của VTC), không được VT (cha) hay VTD (anh em).
        var client = await ClientForNewUserAsync("BHA", "THUKHO", categories: ["VTC"]);

        var categories = await client.GetFromJsonAsync<List<ItemCategoryDto>>("/api/item-categories", Ct);

        Assert.Equal(["VTC", "VTCK"], categories!.Select(c => c.Code));
    }

    [Fact]
    public async Task PhamViNhomHang_GanNhomGoc_ThayCaCay()
    {
        var client = await ClientForNewUserAsync("BHA", "THUKHO", categories: ["VT"]);

        var categories = await client.GetFromJsonAsync<List<ItemCategoryDto>>("/api/item-categories", Ct);

        Assert.Equal(["VT", "VTC", "VTCK", "VTD"], categories!.Select(c => c.Code));
    }

    [Fact]
    public async Task KhongPhamViNhomHang_ThayMoiNhom()
    {
        var client = await ClientForNewUserAsync("BHA", "THUKHO");

        var categories = await client.GetFromJsonAsync<List<ItemCategoryDto>>("/api/item-categories", Ct);

        Assert.Equal(6, categories!.Count);
    }

    [Fact]
    public async Task ThieuQuyen_Tra403KemMaQuyen()
    {
        // Nhân viên mua hàng không có quyền xem danh mục kho.
        var client = await ClientForNewUserAsync("NMI", "NVMUA");

        var response = await client.GetAsync("/api/warehouses", Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains(Permissions.Organization.WarehouseView, (await response.Content.ReadFromJsonAsync<ProblemDetails>(Ct))?.Title);
    }

    [Fact]
    public async Task Me_TraQuyenTheoTungCongTy()
    {
        var admin = await _factory.CreateAuthenticatedClientAsync();
        var thukho = await _factory.CreateAuthenticatedClientAsync("thukho.bha", "Demo@2026");

        var adminInfo = await admin.GetFromJsonAsync<CurrentUserInfo>("/api/auth/me", Ct);
        var thukhoInfo = await thukho.GetFromJsonAsync<CurrentUserInfo>("/api/auth/me", Ct);

        // ADMIN luôn có đủ mọi quyền trong danh mục, kể cả quyền thêm ở bản sau.
        Assert.All(adminInfo!.Companies, c => Assert.Equal(Permissions.All.Count, c.Permissions.Count));
        var bha = Assert.Single(thukhoInfo!.Companies);
        Assert.Equal("BHA", bha.Code);
        Assert.Contains(Permissions.Inventory.DocumentPost, bha.Permissions);
        Assert.DoesNotContain(Permissions.Purchase.PoCreate, bha.Permissions);
    }

    [Fact]
    public async Task KiemTraLaiPhamViKhiGhi_ChanSaiCongTyVaSaiKho()
    {
        // Lệnh ghi kiểm tra lại phạm vi ở tầng Application, không tin CompanyId/WarehouseId client gửi lên (CLAUDE.md mục 4).
        var userId = await _factory.CreateScopedUserAsync(UniqueName("ghi"), "BHA", "THUKHO", warehouseCodes: ["KP1"]);
        var bha = await _factory.GetCompanyIdAsync("BHA");
        var nmi = await _factory.GetCompanyIdAsync("NMI");

        using var scope = _factory.Services.CreateScope();
        Assert.Equal(ScopeLoadResult.Ok, await scope.ServiceProvider.GetRequiredService<UserScopeLoader>().LoadAsync(userId, bha, Ct));
        var currentScope = scope.ServiceProvider.GetRequiredService<ICurrentUserScope>();
        var db = scope.ServiceProvider.GetRequiredService<ProcDbContext>();
        var kp1 = await db.Warehouses.SingleAsync(w => w.Code == "KP1", Ct);

        currentScope.EnsureCompany(bha);
        currentScope.EnsureWarehouse(kp1.Id);
        Assert.Throws<ForbiddenAccessException>(() => currentScope.EnsureCompany(nmi));
        Assert.Throws<ForbiddenAccessException>(() => currentScope.EnsureWarehouse(Guid.CreateVersion7()));
        Assert.Throws<ForbiddenAccessException>(() => currentScope.EnsurePermission(Permissions.Purchase.PoCreate));
    }

    [Fact]
    public async Task ChuaNapPhamVi_TruyVanKhongTraDongNao()
    {
        // Mặc định an toàn: quên nạp phạm vi thì không thấy dữ liệu, thay vì thấy hết.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProcDbContext>();

        Assert.Equal(0, await db.Warehouses.CountAsync(Ct));
        Assert.Equal(0, await db.ItemCategories.CountAsync(Ct));
    }

    [Fact]
    public async Task MoiCongTyToiDaMotKhoTong()
    {
        using var scope = _factory.Services.CreateScope();
        var bha = await _factory.GetCompanyIdAsync("BHA");
        scope.ServiceProvider.GetRequiredService<CurrentUserScope>().Initialize(Guid.Empty, bha, [], true, [], true, []);
        var db = scope.ServiceProvider.GetRequiredService<ProcDbContext>();

        db.Warehouses.Add(Warehouse.CreateMain(bha, "KT2", "Kho tổng thứ hai"));

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(Ct));
    }

    [Fact]
    public async Task BangQuyen_KhopDanhMucTrongCode()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProcDbContext>();

        var codes = await db.Permissions.Select(p => p.Code).ToListAsync(Ct);

        Assert.Equal(Permissions.All.Select(p => p.Code).Order(), codes.Order());
    }
}
