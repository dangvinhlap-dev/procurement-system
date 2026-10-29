using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Proc.Application.Auth;
using Proc.Domain.Organization;
using Proc.Infrastructure;
using Proc.Infrastructure.Persistence;

namespace Proc.IntegrationTests;

/// <summary>
/// Đăng nhập, khóa tạm, công ty được làm việc (design.md mục 2.2, 5.1, 9.1) – chạy trên procurement_test.
/// </summary>
[Collection(ApiCollection.Name)]
public class AuthTests
{
    private const string Password = "matkhau123";
    private readonly ProcApiFactory _factory;

    public AuthTests(ProcApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string UniqueName(string prefix) => $"{prefix}{Guid.NewGuid():N}"[..20];

    private Task<HttpResponseMessage> LoginAsync(string userName, string password) =>
        _factory.CreateClient().PostAsJsonAsync("/api/auth/login", new LoginRequest(userName, password), Ct);

    [Fact]
    public async Task Login_DungMatKhau_TraTokenVaCongTyDuocLamViec()
    {
        var response = await LoginAsync(ProcApiFactory.AdminUserName, ProcApiFactory.AdminPassword);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var login = await response.Content.ReadFromJsonAsync<LoginResult>(Ct);
        Assert.NotNull(login);
        Assert.False(string.IsNullOrEmpty(login.AccessToken));
        Assert.True(login.ExpiresAt > DateTimeOffset.UtcNow.AddHours(7));
        Assert.Equal("Quản trị hệ thống", login.User.FullName);

        // Tổng công ty đứng đầu, rồi các công ty theo mã; mặc định là tổng công ty.
        Assert.Equal(["TCT", "BHA", "DTV", "NMI"], login.User.Companies.Select(c => c.Code));
        Assert.True(login.User.Companies[0].IsHeadOffice);
        Assert.Equal(login.User.Companies[0].Id, login.User.DefaultCompanyId);
        Assert.All(login.User.Companies, c => Assert.Equal([Role.AdminCode], c.RoleCodes));
    }

    [Fact]
    public async Task Login_KhongPhanBietHoaThuongTenDangNhap()
    {
        var response = await LoginAsync("ADMIN", ProcApiFactory.AdminPassword);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("admin", "sai-mat-khau-1")]
    [InlineData("khong-ton-tai", "bat-ky-123")]
    public async Task Login_SaiThongTin_TraCungMotThongBao(string userName, string password)
    {
        // Sai tên hay sai mật khẩu đều cùng một câu, để không dò được tài khoản nào tồn tại.
        var response = await LoginAsync(userName, password);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(Ct);
        Assert.Equal("Tên đăng nhập hoặc mật khẩu không đúng.", problem?.Title);
    }

    [Fact]
    public async Task Login_Sai5Lan_KhoaTamCaKhiNhapDungMatKhau()
    {
        var userName = UniqueName("khoa");
        await _factory.CreateUserAsync(userName, Password, ["BHA"]);

        for (var i = 1; i < DependencyInjection.MaxFailedLoginAttempts; i++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(userName, "sai-mat-khau")).StatusCode);

        var fifth = await LoginAsync(userName, "sai-mat-khau");
        Assert.Equal(HttpStatusCode.Locked, fifth.StatusCode);
        var problem = await fifth.Content.ReadFromJsonAsync<ProblemDetails>(Ct);
        Assert.StartsWith("Tài khoản tạm khóa", problem?.Title);

        Assert.Equal(HttpStatusCode.Locked, (await LoginAsync(userName, Password)).StatusCode);
    }

    [Fact]
    public async Task Login_DungMatKhauSauVaiLanSai_DatLaiSoLanSai()
    {
        var userName = UniqueName("datlai");
        await _factory.CreateUserAsync(userName, Password, ["BHA"]);

        for (var i = 1; i < DependencyInjection.MaxFailedLoginAttempts; i++)
            await LoginAsync(userName, "sai-mat-khau");
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(userName, Password)).StatusCode);

        // Bộ đếm đã về 0: sai thêm một lần chưa bị khóa.
        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(userName, "sai-mat-khau")).StatusCode);
    }

    [Fact]
    public async Task Login_TaiKhoanNgungDung_Tra403()
    {
        var userName = UniqueName("ngung");
        await _factory.CreateUserAsync(userName, Password, ["BHA"], isActive: false);

        var response = await LoginAsync(userName, Password);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("ngừng sử dụng", (await response.Content.ReadFromJsonAsync<ProblemDetails>(Ct))?.Title);
    }

    [Fact]
    public async Task Login_ChuaGanCongTy_Tra403()
    {
        var userName = UniqueName("chuagan");
        await _factory.CreateUserAsync(userName, Password, []);

        var response = await LoginAsync(userName, Password);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("chưa được gán", (await response.Content.ReadFromJsonAsync<ProblemDetails>(Ct))?.Title);
    }

    [Fact]
    public async Task Login_ChiThayCongTyDuocGan()
    {
        // Mỗi công ty là pháp nhân độc lập: người chỉ được gán BHA thì chỉ thấy BHA (CLAUDE.md mục 4).
        var userName = UniqueName("bha");
        await _factory.CreateUserAsync(userName, Password, ["BHA"]);

        var login = await (await LoginAsync(userName, Password)).Content.ReadFromJsonAsync<LoginResult>(Ct);

        Assert.Equal(["BHA"], login!.User.Companies.Select(c => c.Code));
        Assert.Equal(login.User.Companies[0].Id, login.User.DefaultCompanyId);
    }

    [Fact]
    public async Task Login_ThieuDuLieu_Tra400KemThongBaoTiengViet()
    {
        var response = await LoginAsync("", "");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(Ct);
        Assert.Contains("Hãy nhập tên đăng nhập.", problem!.Errors["UserName"]);
        Assert.Contains("Hãy nhập mật khẩu.", problem.Errors["Password"]);
    }

    [Fact]
    public async Task Me_KhongCoToken_Tra401()
    {
        var response = await _factory.CreateClient().GetAsync("/api/auth/me", Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_TokenGiaMao_Tra401()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjMifQ.c2FpLWNodS1reQ");

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me", Ct)).StatusCode);
    }

    [Fact]
    public async Task Me_CoToken_TraThongTinNguoiDung()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();

        var me = await client.GetFromJsonAsync<CurrentUserInfo>("/api/auth/me", Ct);

        Assert.Equal(ProcApiFactory.AdminUserName, me!.UserName);
        Assert.Equal(4, me.Companies.Count);
    }

    [Fact]
    public async Task Me_TaiKhoanBiNgungSauKhiDangNhap_Tra401()
    {
        // Token chỉ chứa định danh nên ngừng tài khoản thì /me hết hiệu lực ngay, không chờ token hết hạn.
        var userName = UniqueName("ngungsau");
        var userId = await _factory.CreateUserAsync(userName, Password, ["BHA"]);
        var client = await _factory.CreateAuthenticatedClientAsync(userName, Password);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ProcDbContext>();
            var user = await db.Users.SingleAsync(u => u.Id == userId, Ct);
            user.IsActive = false;
            await db.SaveChangesAsync(Ct);
        }

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me", Ct)).StatusCode);
    }

    [Fact]
    public async Task DangNhapDongThoi_DungVaSaiMatKhau_KhongLoiHeThong()
    {
        // Nhiều người (hoặc nhiều tab) đăng nhập cùng tài khoản một lúc: cập nhật bộ đếm sai mật khẩu
        // không được làm hỏng lần đăng nhập đúng (lỗi xung đột ghi đồng thời).
        var userName = UniqueName("dongthoi");
        await _factory.CreateUserAsync(userName, Password, ["BHA"]);

        var attempts = Enumerable.Range(0, 12)
            .Select(i => LoginAsync(userName, i % 3 == 0 ? "sai-mat-khau-1" : Password));
        var responses = await Task.WhenAll(attempts);

        Assert.All(responses, r => Assert.Contains(r.StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.Unauthorized }));
        Assert.Equal(8, responses.Count(r => r.StatusCode == HttpStatusCode.OK));
    }
}
