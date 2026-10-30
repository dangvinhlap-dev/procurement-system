using System.Net;
using Microsoft.AspNetCore.Hosting;

namespace Proc.IntegrationTests;

/// <summary>
/// Khi chạy server, một image chứa cả API và giao diện React đã build (design.md mục 14.1):
/// API phục vụ file tĩnh trong wwwroot, mọi đường dẫn giao diện trả index.html, còn /api lạ trả 404.
/// </summary>
[Collection(ApiCollection.Name)]
public class SpaHostingTests
{
    private const string IndexMarker = "<!doctype html><title>proc-test</title>";

    private readonly ProcApiFactory _factory;

    public SpaHostingTests(ProcApiFactory factory) => _factory = factory;

    [Fact]
    public async Task ApiKhongTonTai_Tra404ChuKhongPhai401HayTrangGiaoDien()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/khong-co-endpoint-nay", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CoWwwroot_PhucVuGiaoDienVaDuongDanCuaReactRouter()
    {
        var ct = TestContext.Current.CancellationToken;
        var webRoot = Directory.CreateTempSubdirectory("proc-wwwroot-");
        try
        {
            await File.WriteAllTextAsync(Path.Combine(webRoot.FullName, "index.html"), IndexMarker, ct);
            Directory.CreateDirectory(Path.Combine(webRoot.FullName, "assets"));
            await File.WriteAllTextAsync(Path.Combine(webRoot.FullName, "assets", "app-abc123.js"), "console.log(1)", ct);

            using var factory = _factory.WithWebHostBuilder(b => b.UseWebRoot(webRoot.FullName));
            var client = factory.CreateClient();

            // Trang gốc và đường dẫn do React Router xử lý (gõ thẳng hoặc F5) đều trả index.html, không cần đăng nhập.
            foreach (var path in new[] { "/", "/he-thong/nguoi-dung" })
            {
                var page = await client.GetAsync(path, ct);
                Assert.Equal(HttpStatusCode.OK, page.StatusCode);
                Assert.Equal(IndexMarker, await page.Content.ReadAsStringAsync(ct));
                // index.html không được lưu đệm, để trình duyệt nhận bản mới ngay sau khi nâng cấp.
                Assert.True(page.Headers.CacheControl?.NoCache, path);
            }

            var asset = await client.GetAsync("/assets/app-abc123.js", ct);
            Assert.Equal(HttpStatusCode.OK, asset.StatusCode);
            Assert.Equal("console.log(1)", await asset.Content.ReadAsStringAsync(ct));

            var api = await client.GetAsync("/api/khong-co-endpoint-nay", ct);
            Assert.Equal(HttpStatusCode.NotFound, api.StatusCode);
            Assert.NotEqual("text/html", api.Content.Headers.ContentType?.MediaType);

            // API thật vẫn đòi đăng nhập như trước.
            var users = await client.GetAsync("/api/users", ct);
            Assert.Equal(HttpStatusCode.Unauthorized, users.StatusCode);
        }
        finally
        {
            webRoot.Delete(recursive: true);
        }
    }
}
