using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Proc.Infrastructure.Portal;

namespace Proc.IntegrationTests;

/// <summary>
/// Hạ tầng Portal NCC – lát P1b, design.md 19.5 (P1b-1 giới hạn tần suất, P1b-2 tên miền riêng, P1b-4 cô lập API Portal).
/// </summary>
[Collection(ApiCollection.Name)]
public class PortalInfrastructureTests
{
    private const string PortalHost = "ncc.vidu.vn";
    private const string IndexMarker = "<!doctype html><title>proc-portal-test</title>";

    private readonly ProcApiFactory _factory;

    public PortalInfrastructureTests(ProcApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task DangNhapPortal_QuaGioiHanMoiPhut_Tra429CoRetryAfter()
    {
        using var factory = _factory.WithWebHostBuilder(b =>
        {
            b.UseSetting("Portal:RateLimit:AuthPermitPerMinute", "3");
            b.UseSetting("Portal:RateLimit:ApiPermitPerMinute", "1000");
        });
        var client = factory.CreateClient();

        for (var i = 0; i < 3; i++)
        {
            var ok = await client.PostAsJsonAsync("/api/portal/auth/forgot-password", new { email = $"a{i}@ncc.vn" }, Ct);
            Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        }

        var blocked = await client.PostAsJsonAsync("/api/portal/auth/forgot-password", new { email = "b@ncc.vn" }, Ct);
        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
        Assert.NotNull(blocked.Headers.RetryAfter?.Delta);
        var problem = await blocked.Content.ReadFromJsonAsync<ProblemDetails>(Ct);
        Assert.Contains("Quá nhiều yêu cầu", problem!.Title);

        // Giới hạn chỉ áp cho Portal: API nội bộ (mạng LAN) không bị đếm chung.
        var health = await client.GetAsync("/health", Ct);
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
    }

    [Fact]
    public async Task TenMienPortal_ChiMoPortal_ApiNoiBoVaTrangNoiBoBiChan()
    {
        var webRoot = Directory.CreateTempSubdirectory("proc-portal-wwwroot-");
        try
        {
            await File.WriteAllTextAsync(Path.Combine(webRoot.FullName, "index.html"), IndexMarker, Ct);
            Directory.CreateDirectory(Path.Combine(webRoot.FullName, "assets"));
            await File.WriteAllTextAsync(Path.Combine(webRoot.FullName, "assets", "app-abc123.js"), "console.log(1)", Ct);

            using var factory = _factory.WithWebHostBuilder(b =>
            {
                b.UseWebRoot(webRoot.FullName);
                b.UseSetting("Portal:Hosts", $"{PortalHost}, ncc2.vidu.vn");
            });
            var portal = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri($"https://{PortalHost.ToUpperInvariant()}/"),
                AllowAutoRedirect = false,
            });

            // Trang Portal và file tĩnh mở được.
            foreach (var path in new[] { "/ncc", "/ncc/dang-nhap" })
            {
                var page = await portal.GetAsync(path, Ct);
                Assert.Equal(HttpStatusCode.OK, page.StatusCode);
                Assert.Equal(IndexMarker, await page.Content.ReadAsStringAsync(Ct));
            }

            Assert.Equal(HttpStatusCode.OK, (await portal.GetAsync("/assets/app-abc123.js", Ct)).StatusCode);
            Assert.Equal(HttpStatusCode.OK,
                (await portal.PostAsJsonAsync("/api/portal/auth/forgot-password", new { email = "x@ncc.vn" }, Ct)).StatusCode);

            // Trang gốc và trang nội bộ chuyển về /ncc; API nội bộ, health như không tồn tại.
            foreach (var path in new[] { "/", "/mua-hang/don-mua", "/nccx" })
            {
                var redirect = await portal.GetAsync(path, Ct);
                Assert.Equal(HttpStatusCode.Redirect, redirect.StatusCode);
                Assert.Equal("/ncc", redirect.Headers.Location?.OriginalString);
            }

            foreach (var path in new[] { "/api/auth/me", "/api/suppliers", "/health", "/api/portalx" })
                Assert.Equal(HttpStatusCode.NotFound, (await portal.GetAsync(path, Ct)).StatusCode);
            var login = await portal.PostAsJsonAsync(
                "/api/auth/login", new { userName = ProcApiFactory.AdminUserName, password = ProcApiFactory.AdminPassword }, Ct);
            Assert.Equal(HttpStatusCode.NotFound, login.StatusCode);

            // Địa chỉ nội bộ (không phải tên miền Portal) vẫn dùng đủ như cũ – chạy thử trong LAN (Q14).
            var internalClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
            Assert.Equal(HttpStatusCode.OK, (await internalClient.GetAsync("/health", Ct)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await internalClient.GetAsync("/", Ct)).StatusCode);
        }
        finally
        {
            webRoot.Delete(recursive: true);
        }
    }

    [Theory]
    [InlineData(true, false, false, true)]
    [InlineData(true, false, true, false)]
    [InlineData(true, true, false, true)]
    [InlineData(false, true, true, false)]
    public void LoMaOtp_ChiMayDevTestHoacMayChuThuNghiem(bool expose, bool testServer, bool production, bool expected)
    {
        var options = new PortalOptions { ExposeCodesForTesting = expose, TestServer = testServer };

        Assert.Equal(expected, options.CanExposeCodes(isDevelopmentOrTesting: !production));
    }

    [Fact]
    public async Task MoiPhanHoi_CoTieuDeBaoMat()
    {
        var response = await _factory.CreateClient().GetAsync("/health", Ct);

        Assert.Equal("nosniff", string.Join(",", response.Headers.GetValues("X-Content-Type-Options")));
        Assert.Equal("SAMEORIGIN", string.Join(",", response.Headers.GetValues("X-Frame-Options")));
        Assert.Equal("same-origin", string.Join(",", response.Headers.GetValues("Referrer-Policy")));
    }

    [Fact]
    public void ApiPortal_TruDangNhap_DeuBatBuocPhienPortal_ApiNoiBoKhongNhanPhienPortal()
    {
        var endpoints = _factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>().ToList();
        var portal = endpoints.Where(e => IsUnder(e, "/api/portal")).ToList();
        Assert.NotEmpty(portal);

        foreach (var endpoint in portal)
        {
            if (IsUnder(endpoint, "/api/portal/auth"))
            {
                // Đăng nhập, kích hoạt, quên mật khẩu: ẩn danh có chủ đích.
                Assert.NotNull(endpoint.Metadata.GetMetadata<IAllowAnonymous>());
                continue;
            }

            Assert.Null(endpoint.Metadata.GetMetadata<IAllowAnonymous>());
            Assert.Contains(endpoint.Metadata.GetOrderedMetadata<AuthorizationPolicy>(),
                p => p.AuthenticationSchemes.SequenceEqual([PortalClaims.Scheme]));
        }

        // Endpoint ngoài /api/portal không được khai báo nhận token Portal (NCC chỉ thấy dữ liệu của mình qua API Portal).
        foreach (var endpoint in endpoints.Where(e => !IsUnder(e, "/api/portal")))
        {
            Assert.DoesNotContain(endpoint.Metadata.GetOrderedMetadata<AuthorizationPolicy>(),
                p => p.AuthenticationSchemes.Contains(PortalClaims.Scheme));
            Assert.DoesNotContain(endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>(),
                a => a.AuthenticationSchemes?.Contains(PortalClaims.Scheme, StringComparison.Ordinal) == true);
        }
    }

    private static bool IsUnder(RouteEndpoint endpoint, string prefix)
    {
        var route = "/" + (endpoint.RoutePattern.RawText ?? string.Empty).TrimStart('/');
        return route.Equals(prefix, StringComparison.OrdinalIgnoreCase)
            || route.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase);
    }
}
