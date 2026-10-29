using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Proc.Application.Auth;

namespace Proc.IntegrationTests;

/// <summary>
/// Đổi mật khẩu của chính người dùng (design.md mục 9.4).
/// </summary>
[Collection(ApiCollection.Name)]
public class ChangePasswordTests
{
    private readonly ProcApiFactory _factory;

    public ChangePasswordTests(ProcApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<(string UserName, HttpClient Client)> NewUserAsync()
    {
        var userName = $"doimk{Guid.NewGuid():N}"[..20];
        await _factory.CreateScopedUserAsync(userName, "BHA", "THUKHO");
        return (userName, await _factory.CreateAuthenticatedClientAsync(userName, ProcApiFactory.TestPassword));
    }

    private static async Task<string?> TitleAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(Ct))?.Title;

    [Fact]
    public async Task DoiMatKhau_ThanhCong_DangNhapBangMatKhauMoi()
    {
        var (userName, client) = await NewUserAsync();

        var response = await client.PostAsJsonAsync(
            "/api/auth/change-password", new ChangePasswordRequest(ProcApiFactory.TestPassword, "moimoi2026"), Ct);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var login = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK,
            (await login.PostAsJsonAsync("/api/auth/login", new LoginRequest(userName, "moimoi2026"), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await login.PostAsJsonAsync("/api/auth/login", new LoginRequest(userName, ProcApiFactory.TestPassword), Ct)).StatusCode);
    }

    [Fact]
    public async Task DoiMatKhau_SaiMatKhauHienTai_BaoTiengViet()
    {
        var (_, client) = await NewUserAsync();

        var response = await client.PostAsJsonAsync(
            "/api/auth/change-password", new ChangePasswordRequest("khong-dung-1", "moimoi2026"), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Mật khẩu hiện tại không đúng.", await TitleAsync(response));
    }

    [Fact]
    public async Task DoiMatKhau_MatKhauMoiThieuChuSo_BaoTiengViet()
    {
        var (_, client) = await NewUserAsync();

        var response = await client.PostAsJsonAsync(
            "/api/auth/change-password", new ChangePasswordRequest(ProcApiFactory.TestPassword, "chicochu"), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Mật khẩu phải có ít nhất một chữ số (0–9).", await TitleAsync(response));
    }

    [Fact]
    public async Task DoiMatKhau_TrungMatKhauCu_Tra400()
    {
        var (_, client) = await NewUserAsync();

        var response = await client.PostAsJsonAsync(
            "/api/auth/change-password", new ChangePasswordRequest(ProcApiFactory.TestPassword, ProcApiFactory.TestPassword), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(Ct);
        Assert.Contains("Mật khẩu mới phải khác mật khẩu hiện tại.", problem!.Errors["NewPassword"]);
    }

    [Fact]
    public async Task DoiMatKhau_ChuaDangNhap_Tra401()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync(
            "/api/auth/change-password", new ChangePasswordRequest("a", "b"), Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
