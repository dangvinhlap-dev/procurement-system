using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Proc.Application.MasterData;
using Proc.Application.Portal;
using Proc.Domain.Organization;

namespace Proc.IntegrationTests;

/// <summary>
/// Portal NCC lát P1 (design.md 19.5): mời → kích hoạt → đăng nhập mật khẩu + OTP; token Portal tách hẳn API nội bộ; quản trị NCC quản lý
/// người dùng con; khóa / đổi mật khẩu làm token cũ mất hiệu lực; quên mật khẩu.
/// </summary>
[Collection(ApiCollection.Name)]
public class SupplierPortalAccountTests
{
    private const string PortalPassword = "ncc12345";
    private readonly ProcApiFactory _factory;

    /// <summary>Tạo bộ test.</summary>
    /// <param name="factory">API thử.</param>
    public SupplierPortalAccountTests(ProcApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(Ct)}");
        return (await response.Content.ReadFromJsonAsync<T>(Ct))!;
    }

    private static string TokenOf(string inviteUrl) => Uri.UnescapeDataString(inviteUrl[(inviteUrl.IndexOf("token=", StringComparison.Ordinal) + 6)..]);

    private async Task<(HttpClient Head, SupplierDetailDto Supplier)> ArrangeAsync()
    {
        var userName = $"pt.{Guid.NewGuid():N}"[..16];
        await _factory.CreateUserAsync(userName, ProcApiFactory.TestPassword, ["TCT"]);
        var head = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, "TCT");
        var groupId = (await head.GetFromJsonAsync<List<SupplierGroupDto>>("/api/supplier-groups", Ct))!.Single(g => g.Code == "TN").Id;
        var supplier = await ReadAsync<SupplierDetailDto>(await head.PostAsJsonAsync("/api/suppliers",
            new SaveSupplierRequest(null, groupId, $"NCC Portal {Guid.NewGuid():N}"[..20], null, false, null, null, null, null, null, null,
                null, null, "Approved", null, null, [], []), Ct));
        return (head, supplier);
    }

    /// <summary>Kích hoạt từ link rồi đăng nhập 2 bước, trả client Portal.</summary>
    private async Task<(HttpClient Client, PortalTokenResult Token)> ActivateAndLoginAsync(string inviteUrl, string email)
    {
        var anon = _factory.CreateClient();
        var activated = await anon.PostAsJsonAsync("/api/portal/auth/activate", new PortalSetPasswordRequest(TokenOf(inviteUrl), PortalPassword), Ct);
        Assert.Equal(HttpStatusCode.NoContent, activated.StatusCode);
        return await LoginAsync(email, PortalPassword);
    }

    private async Task<(HttpClient Client, PortalTokenResult Token)> LoginAsync(string email, string password)
    {
        var anon = _factory.CreateClient();
        var challenge = await ReadAsync<PortalLoginChallenge>(
            await anon.PostAsJsonAsync("/api/portal/auth/login", new PortalLoginRequest(email, password), Ct));
        Assert.NotNull(challenge.DevCode);
        var token = await ReadAsync<PortalTokenResult>(
            await anon.PostAsJsonAsync("/api/portal/auth/verify-otp", new PortalVerifyOtpRequest(challenge.ChallengeId, challenge.DevCode!), Ct));
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        return (client, token);
    }

    [Fact]
    public async Task MoiKichHoat_DangNhapOtp_TokenPortalTachApiNoiBo()
    {
        var (head, supplier) = await ArrangeAsync();
        var email = $"{Guid.NewGuid():N}"[..10] + "@NCC.vn";
        var invite = await ReadAsync<PortalInviteResult>(await head.PostAsJsonAsync(
            $"/api/suppliers/{supplier.Id}/portal-users", new PortalInviteRequest(email, "Trần Bán Hàng", "0901", "Giám đốc"), Ct));
        Assert.False(invite.EmailSent); // Testing không bật email – người mời tự chuyển link.
        Assert.Contains("/ncc/kich-hoat?token=", invite.InviteUrl);
        Assert.True(invite.User.IsSupplierAdmin);
        Assert.Equal("Invited", invite.User.Status);
        Assert.Equal(email.ToLowerInvariant(), invite.User.Email);

        // Một email chỉ thuộc một NCC.
        Assert.Equal(HttpStatusCode.BadRequest, (await head.PostAsJsonAsync(
            $"/api/suppliers/{supplier.Id}/portal-users", new PortalInviteRequest(email, "Trùng", null, null), Ct)).StatusCode);

        // Người chỉ xem NCC không mời được.
        await _factory.CreateRoleAsync("XEMNCCPT", Permissions.MasterData.SupplierView);
        var viewerName = $"xv.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(viewerName, "TCT", "XEMNCCPT");
        var viewer = await _factory.CreateClientInCompanyAsync(viewerName, ProcApiFactory.TestPassword, "TCT");
        Assert.Single((await viewer.GetFromJsonAsync<List<SupplierPortalUserDto>>($"/api/suppliers/{supplier.Id}/portal-users", Ct))!);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsJsonAsync(
            $"/api/suppliers/{supplier.Id}/portal-users", new PortalInviteRequest("x@ncc.vn", "X", null, null), Ct)).StatusCode);

        var anon = _factory.CreateClient();
        var info = await anon.GetFromJsonAsync<PortalInviteInfo>($"/api/portal/auth/invite?token={Uri.EscapeDataString(TokenOf(invite.InviteUrl))}", Ct);
        Assert.Equal(supplier.Name, info!.SupplierName);
        // Chưa kích hoạt: đăng nhập báo chung như sai mật khẩu.
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.PostAsJsonAsync(
            "/api/portal/auth/login", new PortalLoginRequest(email, PortalPassword), Ct)).StatusCode);
        // Mật khẩu yếu bị chặn.
        Assert.Equal(HttpStatusCode.BadRequest, (await anon.PostAsJsonAsync(
            "/api/portal/auth/activate", new PortalSetPasswordRequest(TokenOf(invite.InviteUrl), "abcdefgh"), Ct)).StatusCode);

        var (portal, token) = await ActivateAndLoginAsync(invite.InviteUrl, email);
        Assert.Equal(supplier.Code, token.Me.Supplier.Code);
        // Link mời chỉ dùng một lần.
        Assert.Equal(HttpStatusCode.BadRequest, (await anon.PostAsJsonAsync(
            "/api/portal/auth/activate", new PortalSetPasswordRequest(TokenOf(invite.InviteUrl), PortalPassword), Ct)).StatusCode);

        // OTP sai không cấp token.
        var challenge = await ReadAsync<PortalLoginChallenge>(await anon.PostAsJsonAsync(
            "/api/portal/auth/login", new PortalLoginRequest(email, PortalPassword), Ct));
        Assert.Contains("*", challenge.MaskedEmail);
        var wrong = challenge.DevCode == "000000" ? "111111" : "000000";
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.PostAsJsonAsync(
            "/api/portal/auth/verify-otp", new PortalVerifyOtpRequest(challenge.ChallengeId, wrong), Ct)).StatusCode);

        var me = await portal.GetFromJsonAsync<PortalMeDto>("/api/portal/me", Ct);
        Assert.Equal(supplier.Id, me!.Supplier.Id);
        Assert.True(me.IsSupplierAdmin);

        // Token NCC không gọi được API nội bộ; token nội bộ không gọi được API Portal.
        Assert.Equal(HttpStatusCode.Unauthorized, (await portal.GetAsync("/api/suppliers", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await head.GetAsync("/api/portal/me", Ct)).StatusCode);

        var users = await head.GetFromJsonAsync<List<SupplierPortalUserDto>>($"/api/suppliers/{supplier.Id}/portal-users", Ct);
        Assert.Equal("Active", Assert.Single(users!).Status);
        Assert.NotNull(users![0].LastLoginAt);
    }

    [Fact]
    public async Task QuanTriNcc_MoiNguoiDungCon_KhoaVaDoiMatKhauLamTokenCuHetHieuLuc()
    {
        var (head, supplier) = await ArrangeAsync();
        var adminEmail = $"qt{Guid.NewGuid():N}"[..10] + "@ncc.vn";
        var invite = await ReadAsync<PortalInviteResult>(await head.PostAsJsonAsync(
            $"/api/suppliers/{supplier.Id}/portal-users", new PortalInviteRequest(adminEmail, "Quản trị NCC", null, null), Ct));
        var (admin, adminToken) = await ActivateAndLoginAsync(invite.InviteUrl, adminEmail);

        // Quản trị NCC mời người dùng con.
        var subEmail = $"con{Guid.NewGuid():N}"[..10] + "@ncc.vn";
        var subInvite = await ReadAsync<PortalInviteResult>(await admin.PostAsJsonAsync(
            "/api/portal/users", new PortalInviteRequest(subEmail, "Nhân viên NCC", null, null), Ct));
        Assert.False(subInvite.User.IsSupplierAdmin);
        Assert.Equal(supplier.Id, subInvite.User.SupplierId);
        // Gửi lại lời mời: link cũ mất hiệu lực.
        var resent = await ReadAsync<PortalInviteResult>(await admin.PostAsync($"/api/portal/users/{subInvite.User.Id}/resend-invite", null, Ct));
        Assert.Equal(HttpStatusCode.BadRequest, (await _factory.CreateClient().PostAsJsonAsync(
            "/api/portal/auth/activate", new PortalSetPasswordRequest(TokenOf(subInvite.InviteUrl), PortalPassword), Ct)).StatusCode);
        var (sub, _) = await ActivateAndLoginAsync(resent.InviteUrl, subEmail);

        // Người dùng con không quản lý người dùng được; quản trị không tự khóa mình.
        Assert.Equal(HttpStatusCode.Forbidden, (await sub.PostAsJsonAsync(
            "/api/portal/users", new PortalInviteRequest("x" + subEmail, "X", null, null), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync($"/api/portal/users/{adminToken.Me.UserId}/disable", null, Ct)).StatusCode);
        Assert.Equal(2, (await admin.GetFromJsonAsync<List<SupplierPortalUserDto>>("/api/portal/users", Ct))!.Count);

        // Khóa người dùng con → token của họ hết hiệu lực ngay.
        await ReadAsync<SupplierPortalUserDto>(await admin.PostAsync($"/api/portal/users/{subInvite.User.Id}/disable", null, Ct));
        Assert.Equal(HttpStatusCode.Unauthorized, (await sub.GetAsync("/api/portal/me", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _factory.CreateClient().PostAsJsonAsync(
            "/api/portal/auth/login", new PortalLoginRequest(subEmail, PortalPassword), Ct)).StatusCode);

        // Đổi mật khẩu: token cũ hết hiệu lực, token mới dùng được.
        var changed = await ReadAsync<PortalTokenResult>(await admin.PostAsJsonAsync(
            "/api/portal/change-password", new PortalChangePasswordRequest(PortalPassword, "moi12345"), Ct));
        Assert.Equal(HttpStatusCode.Unauthorized, (await admin.GetAsync("/api/portal/me", Ct)).StatusCode);
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", changed.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/portal/me", Ct)).StatusCode);

        // Quên mật khẩu → đặt lại → đăng nhập bằng mật khẩu mới.
        var anon = _factory.CreateClient();
        var forgot = await ReadAsync<PortalForgotPasswordResult>(await anon.PostAsJsonAsync(
            "/api/portal/auth/forgot-password", new { email = adminEmail }, Ct));
        Assert.Equal(HttpStatusCode.NoContent, (await anon.PostAsJsonAsync(
            "/api/portal/auth/reset-password", new PortalSetPasswordRequest(forgot.DevToken!, "datlai123"), Ct)).StatusCode);
        // Email không có tài khoản: vẫn trả 200, không có mã.
        Assert.Null((await ReadAsync<PortalForgotPasswordResult>(await anon.PostAsJsonAsync(
            "/api/portal/auth/forgot-password", new { email = "khongco@ncc.vn" }, Ct))).DevToken);
        var (fresh, _) = await LoginAsync(adminEmail, "datlai123");

        // Phòng CUVT khóa quản trị NCC.
        await ReadAsync<SupplierPortalUserDto>(await head.PostAsync($"/api/suppliers/{supplier.Id}/portal-users/{adminToken.Me.UserId}/disable", null, Ct));
        Assert.Equal(HttpStatusCode.Unauthorized, (await fresh.GetAsync("/api/portal/me", Ct)).StatusCode);
        var reopened = await ReadAsync<SupplierPortalUserDto>(await head.PostAsync(
            $"/api/suppliers/{supplier.Id}/portal-users/{adminToken.Me.UserId}/enable", null, Ct));
        Assert.Equal("Active", reopened.Status);
    }
}
