using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Proc.Api.Security;
using Proc.Application.Auth;
using Proc.Application.Operations;
using Proc.Domain.Operations;
using Proc.Domain.Organization;
using Proc.Infrastructure.Persistence;

namespace Proc.IntegrationTests;

/// <summary>
/// Lát QT7 – cấu hình truy cập CFG (ke-hoach-qt.md QT7, design.md mục 21.16): giới hạn tần suất 429, header bảo mật, thu hồi token (đổi mật khẩu,
/// đặt lại, đăng xuất tất cả), dải IP quản trị, mật khẩu yếu / phổ biến, khóa theo chính sách, quyền.
/// Request cần IP cụ thể gửi qua <c>Server.SendAsync</c> (TestServer mặc định không có IP – coi như máy chủ tự gọi, không giới hạn).
/// </summary>
[Collection(ApiCollection.Name)]
public class AccessConfigTests
{
    private const string ConfigPath = "/api/ops/v1/access-config";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly ProcApiFactory _factory;

    public AccessConfigTests(ProcApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(Ct)}");
        return (await response.Content.ReadFromJsonAsync<T>(Ct))!;
    }

    private static SaveAccessConfigRequest Defaults(string reason = "Trả về mặc định sau test") => new(
        AccessPolicy.DefaultMinPasswordLength, true, null, AccessPolicy.DefaultMaxFailedLogins, AccessPolicy.DefaultLockoutMinutes,
        AccessPolicy.DefaultSessionMinutes, null, AccessPolicy.DefaultLoginPerMinutePerIp, AccessPolicy.DefaultApiPerMinutePerUser,
        AccessPolicy.DefaultHeavyConcurrentPerUser, reason);

    private Task<HttpClient> HeadAsync() =>
        _factory.CreateClientInCompanyAsync(ProcApiFactory.AdminUserName, ProcApiFactory.AdminPassword, "TCT");

    private async Task<string> LoginTokenAsync(string userName, string password)
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/api/auth/login", new LoginRequest(userName, password), Ct);
        return (await ReadAsync<LoginResult>(response)).AccessToken;
    }

    /// <summary>Gửi request với IP người gọi cụ thể (header giả lập của <see cref="TestClientIpStartupFilter"/>).</summary>
    private async Task<HttpResponseMessage> SendFromAsync(
        string ip, HttpMethod method, string path, string? token = null, Guid? companyId = null, object? body = null)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Add(TestClientIpStartupFilter.Header, ip);
        if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (companyId is not null) request.Headers.Add(CompanyScopeMiddleware.CompanyHeader, companyId.ToString());
        if (body is not null) request.Content = JsonContent.Create(body, body.GetType(), options: Json);
        return await _factory.CreateClient().SendAsync(request, Ct);
    }

    private static Task<string> BodyAsync(HttpResponseMessage response) => response.Content.ReadAsStringAsync(Ct);

    /// <summary>IP ngẫu nhiên trong 10.x – mỗi lần chạy một nhóm đếm mới (bộ đếm nằm trong bộ nhớ của API test).</summary>
    private static string RandomIp(int second) => $"10.{Random.Shared.Next(1, 250)}.{second}.{Random.Shared.Next(1, 250)}";

    /// <summary>Mọi phản hồi có đủ header bảo mật; HTTP thì không gửi HSTS. /health ẩn danh chỉ trạng thái + phiên bản, người có quyền xem chi tiết.</summary>
    [Fact]
    public async Task HeaderBaoMat_VaHealthAnDanh()
    {
        var anonymous = _factory.CreateClient();
        var response = await anonymous.GetAsync("/health", Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(SecurityHeaderValues.ContentSecurityPolicy, response.Headers.GetValues("Content-Security-Policy").Single());
        Assert.Equal(SecurityHeaderValues.PermissionsPolicy, response.Headers.GetValues("Permissions-Policy").Single());
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("SAMEORIGIN", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("same-origin", response.Headers.GetValues("Referrer-Policy").Single());
        Assert.False(response.Headers.Contains("Strict-Transport-Security"));

        using (var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct)))
        {
            Assert.Equal("Healthy", body.RootElement.GetProperty("status").GetString());
            Assert.Equal(0, body.RootElement.GetProperty("checks").GetArrayLength());
        }

        var head = await HeadAsync();
        using var detailed = JsonDocument.Parse(await head.GetStringAsync("/health", Ct));
        Assert.Contains(detailed.RootElement.GetProperty("checks").EnumerateArray(), c => c.GetProperty("name").GetString() == "database");
    }

    /// <summary>
    /// Đăng nhập từ một IP: quá số lần / phút (mặc định 20) thì 429 kèm câu tiếng Việt và Retry-After; IP khác không bị ảnh hưởng. API theo phiên: hạ
    /// giới hạn còn 60 / phút thì lần thứ 61 nhận 429. Việc nặng nhận diện đúng đường dẫn.
    /// </summary>
    [Fact]
    public async Task GioiHanTanSuat_DangNhapTheoIp_ApiTheoPhien()
    {
        var ip = RandomIp(1);
        var login = new LoginRequest("khong-co-ai", "sai-mat-khau");
        for (var i = 0; i < AccessPolicy.DefaultLoginPerMinutePerIp; i++)
        {
            var attempt = await SendFromAsync(ip, HttpMethod.Post, "/api/auth/login", body: login);
            Assert.True(attempt.StatusCode == HttpStatusCode.Unauthorized, $"{attempt.StatusCode}: {await BodyAsync(attempt)}");
        }
        var blocked = await SendFromAsync(ip, HttpMethod.Post, "/api/auth/login", body: login);
        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
        Assert.True(blocked.Headers.Contains("Retry-After"));
        Assert.Contains("Đăng nhập quá nhiều lần", await BodyAsync(blocked));
        Assert.Equal(HttpStatusCode.Unauthorized, (await SendFromAsync(RandomIp(2), HttpMethod.Post, "/api/auth/login", body: login)).StatusCode);

        var head = await HeadAsync();
        await ReadAsync<AccessConfigDto>(await head.PutAsJsonAsync(ConfigPath,
            Defaults("Thử giới hạn API") with { ApiPerMinutePerUser = AccessPolicy.ApiPerMinutePerUserFloor }, Ct));
        try
        {
            var token = await LoginTokenAsync(ProcApiFactory.AdminUserName, ProcApiFactory.AdminPassword);
            var apiIp = RandomIp(3);
            for (var i = 0; i < AccessPolicy.ApiPerMinutePerUserFloor; i++)
                Assert.Equal(HttpStatusCode.OK, (await SendFromAsync(apiIp, HttpMethod.Get, "/api/auth/me", token)).StatusCode);
            var over = await SendFromAsync(apiIp, HttpMethod.Get, "/api/auth/me", token);
            Assert.Equal(HttpStatusCode.TooManyRequests, over.StatusCode);
            Assert.Contains("Quá nhiều yêu cầu", await BodyAsync(over));
        }
        finally
        {
            await ReadAsync<AccessConfigDto>(await head.PutAsJsonAsync(ConfigPath, Defaults(), Ct));
        }

        Assert.True(ApiRateLimiting.IsHeavy(new PathString("/api/excel/items/export")));
        Assert.True(ApiRateLimiting.IsHeavy(new PathString("/api/reports/spend")));
        Assert.True(ApiRateLimiting.IsHeavy(new PathString($"/api/purchase-orders/{Guid.NewGuid()}/pdf")));
        Assert.False(ApiRateLimiting.IsHeavy(new PathString("/api/purchase-orders")));
    }

    /// <summary>
    /// Đổi mật khẩu: token cũ bị từ chối (401), token mới trả về dùng được; quản trị đặt lại mật khẩu: token của người đó bị từ chối.
    /// Mật khẩu phổ biến, ngắn hơn chính sách, nằm trong danh sách cấm thêm bị từ chối kèm câu tiếng Việt.
    /// </summary>
    [Fact]
    public async Task DoiMatKhau_ThuHoiToken_MatKhauYeuBiTuChoi()
    {
        var name = $"cfg.{Guid.NewGuid():N}"[..16];
        var userId = await _factory.CreateUserAsync(name, ProcApiFactory.TestPassword, ["TCT"]);
        var oldClient = await _factory.CreateAuthenticatedClientAsync(name, ProcApiFactory.TestPassword);
        Assert.Equal(HttpStatusCode.OK, (await oldClient.GetAsync("/api/auth/me", Ct)).StatusCode);

        var common = await oldClient.PostAsJsonAsync("/api/auth/change-password",
            new ChangePasswordRequest(ProcApiFactory.TestPassword, "qwerty123"), Ct);
        Assert.Equal(HttpStatusCode.BadRequest, common.StatusCode);
        Assert.Contains("quá phổ biến", (await common.Content.ReadFromJsonAsync<ProblemDetails>(Ct))!.Title);

        var head = await HeadAsync();
        await ReadAsync<AccessConfigDto>(await head.PutAsJsonAsync(ConfigPath,
            Defaults("Thử chính sách mật khẩu") with { MinPasswordLength = 12, ExtraBlockedPasswords = "TongCongTy@2026" }, Ct));
        try
        {
            var shortPassword = await oldClient.PostAsJsonAsync("/api/auth/change-password",
                new ChangePasswordRequest(ProcApiFactory.TestPassword, "ngan2026ab"), Ct);
            Assert.Contains("ít nhất 12 ký tự", (await shortPassword.Content.ReadFromJsonAsync<ProblemDetails>(Ct))!.Title);
            var banned = await oldClient.PostAsJsonAsync("/api/auth/change-password",
                new ChangePasswordRequest(ProcApiFactory.TestPassword, "tongcongty@2026"), Ct);
            Assert.Contains("quá phổ biến", (await banned.Content.ReadFromJsonAsync<ProblemDetails>(Ct))!.Title);

            var changed = await ReadAsync<LoginResult>(await oldClient.PostAsJsonAsync("/api/auth/change-password",
                new ChangePasswordRequest(ProcApiFactory.TestPassword, "moimoi2026dai"), Ct));
            Assert.Equal(HttpStatusCode.Unauthorized, (await oldClient.GetAsync("/api/auth/me", Ct)).StatusCode);
            var newClient = _factory.CreateClient();
            newClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", changed.AccessToken);
            Assert.Equal(HttpStatusCode.OK, (await newClient.GetAsync("/api/auth/me", Ct)).StatusCode);

            // Quản trị đặt lại mật khẩu → phiên đang mở của người đó hết hiệu lực.
            Assert.Equal(HttpStatusCode.NoContent, (await head.PostAsJsonAsync($"/api/users/{userId}/reset-password",
                new Proc.Application.Administration.ResetPasswordRequest("datlai2026dai"), Ct)).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await newClient.GetAsync("/api/auth/me", Ct)).StatusCode);
        }
        finally
        {
            await ReadAsync<AccessConfigDto>(await head.PutAsJsonAsync(ConfigPath, Defaults(), Ct));
        }
    }

    /// <summary>
    /// Đăng xuất tất cả: chữ xác nhận sai / thiếu lý do → 400; đúng → token cũ của mọi người (kể cả người bấm) bị từ chối, người bấm nhận token mới
    /// dùng được; đăng nhập lại bình thường. Nhật ký vận hành có dòng SessionsRevoked.
    /// </summary>
    [Fact]
    public async Task DangXuatTatCa_ThuHoiMoiToken()
    {
        var name = $"cfo.{Guid.NewGuid():N}"[..16];
        await _factory.CreateUserAsync(name, ProcApiFactory.TestPassword, ["TCT"]);
        var other = await _factory.CreateAuthenticatedClientAsync(name, ProcApiFactory.TestPassword);
        var head = await HeadAsync();

        Assert.Equal(HttpStatusCode.BadRequest, (await head.PostAsJsonAsync($"{ConfigPath}/sign-out-all",
            new SignOutAllRequest("Nghi lộ mật khẩu", ProcApiFactory.AdminPassword, "ĐĂNG XUẤT"), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await head.PostAsJsonAsync($"{ConfigPath}/sign-out-all",
            new SignOutAllRequest("abc", ProcApiFactory.AdminPassword, IAccessConfigService.SignOutAllConfirmation), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await other.GetAsync("/api/auth/me", Ct)).StatusCode);

        // Token cấp trong cùng giây với mốc vẫn hợp lệ (iat tính bằng giây) – chờ sang giây mới để token cũ chắc chắn cấp trước mốc.
        await Task.Delay(1100, Ct);
        var result = await ReadAsync<SignOutAllResult>(await head.PostAsJsonAsync($"{ConfigPath}/sign-out-all",
            new SignOutAllRequest("Nghi lộ mật khẩu quản trị", ProcApiFactory.AdminPassword, "đăng xuất tất cả"), Ct));

        Assert.Equal(HttpStatusCode.Unauthorized, (await other.GetAsync("/api/auth/me", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await head.GetAsync("/api/auth/me", Ct)).StatusCode);
        var renewed = _factory.CreateClient();
        renewed.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", result.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await renewed.GetAsync("/api/auth/me", Ct)).StatusCode);
        var again = await _factory.CreateAuthenticatedClientAsync(name, ProcApiFactory.TestPassword);
        Assert.Equal(HttpStatusCode.OK, (await again.GetAsync("/api/auth/me", Ct)).StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProcDbContext>();
        Assert.True(await db.OperationLogs.AnyAsync(l => l.Kind == OperationKinds.SessionsRevoked && l.Reason == "Nghi lộ mật khẩu quản trị", Ct));
    }

    /// <summary>
    /// Dải IP quản trị: IP ngoài dải bị chặn mọi API QT (403 kèm IP, ghi nhật ký OpsIpDenied), trong dải vào được, API thường không ảnh hưởng; lưu dải
    /// không gồm IP người lưu bị từ chối; dải sai cú pháp → 400.
    /// </summary>
    [Fact]
    public async Task DaiIpQuanTri_ChanNgoaiDai()
    {
        var head = await HeadAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await head.PutAsJsonAsync(ConfigPath,
            Defaults("Khai dải IP") with { OpsAllowedIpRanges = "10.0.0.300" }, Ct)).StatusCode);
        var saved = await ReadAsync<AccessConfigDto>(await head.PutAsJsonAsync(ConfigPath,
            Defaults("Khai dải IP") with { OpsAllowedIpRanges = "10.0.0.0/8, 192.168.9.9" }, Ct));
        Assert.Equal("10.0.0.0/8\n192.168.9.9/32", saved.OpsAllowedIpRanges);
        try
        {
            var token = await LoginTokenAsync(ProcApiFactory.AdminUserName, ProcApiFactory.AdminPassword);
            var tct = await _factory.GetCompanyIdAsync("TCT");

            var denied = await SendFromAsync("192.168.5.5", HttpMethod.Get, "/api/ops/v1/overview", token, tct);
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
            Assert.Contains("192.168.5.5", await BodyAsync(denied));
            Assert.Equal(HttpStatusCode.OK, (await SendFromAsync("10.20.30.40", HttpMethod.Get, "/api/ops/v1/overview", token, tct)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await SendFromAsync("192.168.9.9", HttpMethod.Get, ConfigPath, token, tct)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await SendFromAsync("192.168.5.5", HttpMethod.Get, "/api/auth/me", token, tct)).StatusCode);

            // Người đang lưu ở 192.168.9.9 bỏ chính IP mình khỏi danh sách → từ chối, không tự khóa.
            var selfLock = await SendFromAsync("192.168.9.9", HttpMethod.Put, ConfigPath, token, tct,
                Defaults("Bỏ IP của tôi") with { OpsAllowedIpRanges = "10.0.0.0/8" });
            Assert.Equal(HttpStatusCode.BadRequest, selfLock.StatusCode);
            Assert.Contains("không gồm địa chỉ của bạn", await BodyAsync(selfLock));

            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ProcDbContext>();
                Assert.True(await db.OperationLogs.AnyAsync(l => l.Kind == OperationKinds.OpsIpDenied && l.Target == "192.168.5.5", Ct));
            }

            // Cách gỡ khi lỡ khóa mình: Security:IgnoreOpsIpRanges = true trong file cấu hình máy chủ → bỏ kiểm tra dải, CFG báo đỏ.
            SetIgnoreOpsIpRanges(true);
            try
            {
                Assert.Equal(HttpStatusCode.OK, (await SendFromAsync("192.168.5.5", HttpMethod.Get, "/api/ops/v1/overview", token, tct)).StatusCode);
                var cfg = JsonSerializer.Deserialize<AccessConfigDto>(
                    await BodyAsync(await SendFromAsync("192.168.5.5", HttpMethod.Get, ConfigPath, token, tct)), Json)!;
                Assert.Contains(cfg.Security, r => r.Status == "Error" && r.Value.Contains("IgnoreOpsIpRanges", StringComparison.Ordinal));
            }
            finally
            {
                SetIgnoreOpsIpRanges(false);
            }

            Assert.Equal(HttpStatusCode.Forbidden, (await SendFromAsync("192.168.5.5", HttpMethod.Get, "/api/ops/v1/overview", token, tct)).StatusCode);
        }
        finally
        {
            await ReadAsync<AccessConfigDto>(await head.PutAsJsonAsync(ConfigPath, Defaults(), Ct));
        }
    }

    /// <summary>Giả lập sửa file cấu hình trên máy chủ: đổi giá trị rồi bỏ bản đệm của IOptionsMonitor (như khi appsettings được đọc lại).</summary>
    private void SetIgnoreOpsIpRanges(bool value)
    {
        _factory.Services.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>()["Security:IgnoreOpsIpRanges"] =
            value ? "true" : "false";
        _factory.Services.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitorCache<SecurityOptions>>()
            .TryRemove(Microsoft.Extensions.Options.Options.DefaultName);
    }

    /// <summary>
    /// Khóa theo chính sách: 3 lần sai, 2 phút → lần sai thứ 3 bị 423 kèm số lần, số phút; người chỉ có quyền xem vận hành xem được CFG nhưng không
    /// thấy danh sách mật khẩu cấm, không sửa được (403); thiếu lý do → 400; giá trị ngoài khoảng → 400.
    /// </summary>
    [Fact]
    public async Task KhoaTheoChinhSach_QuyenVaLyDo()
    {
        var head = await HeadAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await head.PutAsJsonAsync(ConfigPath, Defaults("abc"), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await head.PutAsJsonAsync(ConfigPath,
            Defaults("Thử ngoài khoảng") with { SessionMinutes = 5 }, Ct)).StatusCode);

        await ReadAsync<AccessConfigDto>(await head.PutAsJsonAsync(ConfigPath,
            Defaults("Thử khóa 3 lần") with { MaxFailedLogins = 3, LockoutMinutes = 2, ExtraBlockedPasswords = "MatKhauNoiBo1" }, Ct));
        try
        {
            var name = $"cfk.{Guid.NewGuid():N}"[..16];
            await _factory.CreateUserAsync(name, ProcApiFactory.TestPassword, ["TCT"]);
            var anonymous = _factory.CreateClient();
            for (var i = 1; i < 3; i++)
                Assert.Equal(HttpStatusCode.Unauthorized,
                    (await anonymous.PostAsJsonAsync("/api/auth/login", new LoginRequest(name, "sai-mat-khau"), Ct)).StatusCode);
            var third = await anonymous.PostAsJsonAsync("/api/auth/login", new LoginRequest(name, "sai-mat-khau"), Ct);
            Assert.Equal(HttpStatusCode.Locked, third.StatusCode);
            Assert.StartsWith("Nhập sai mật khẩu 3 lần liên tiếp – chờ 2 phút", (await third.Content.ReadFromJsonAsync<ProblemDetails>(Ct))!.Title);

            var role = $"C{Guid.NewGuid():N}"[..10].ToUpperInvariant();
            await _factory.CreateRoleAsync(role, Permissions.System.OpsView);
            var viewerName = $"cfv.{Guid.NewGuid():N}"[..16];
            await _factory.CreateScopedUserAsync(viewerName, "TCT", role);
            var viewer = await _factory.CreateClientInCompanyAsync(viewerName, ProcApiFactory.TestPassword, "TCT");
            var seen = await ReadAsync<AccessConfigDto>(await viewer.GetAsync(ConfigPath, Ct));
            Assert.Equal(3, seen.MaxFailedLogins);
            Assert.Null(seen.ExtraBlockedPasswords);
            Assert.True(seen.CommonPasswordCount > 100);
            Assert.Contains(seen.Security, r => r.Item == "Content-Security-Policy");
            Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PutAsJsonAsync(ConfigPath, Defaults("Người xem sửa"), Ct)).StatusCode);

            var full = await ReadAsync<AccessConfigDto>(await head.GetAsync(ConfigPath, Ct));
            Assert.Equal("MatKhauNoiBo1", full.ExtraBlockedPasswords);
            Assert.Equal(ProcApiFactory.AdminUserName, full.UpdatedByName);
        }
        finally
        {
            await ReadAsync<AccessConfigDto>(await head.PutAsJsonAsync(ConfigPath, Defaults(), Ct));
        }
    }
}
