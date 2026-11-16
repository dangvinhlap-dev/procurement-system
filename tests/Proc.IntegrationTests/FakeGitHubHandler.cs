using System.Collections.Concurrent;
using System.Net;

namespace Proc.IntegrationTests;

/// <summary>
/// GitHub giả cho test trung tâm phát hành (lát QT8): trả nội dung đã đăng ký theo đường dẫn tuyệt đối; chưa đăng ký thì 404. Gắn làm handler chính
/// của HttpClient "release-github" trong <see cref="ProcApiFactory"/> – test không gọi mạng thật.
/// </summary>
public sealed class FakeGitHubHandler : HttpMessageHandler
{
    /// <summary>Bản dùng chung của API test.</summary>
    public static FakeGitHubHandler Shared { get; } = new();

    private readonly ConcurrentDictionary<string, byte[]> _responses = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Đăng ký nội dung trả về cho một địa chỉ.</summary>
    /// <param name="url">Địa chỉ tuyệt đối.</param>
    /// <param name="body">Nội dung.</param>
    public void Set(string url, byte[] body) => _responses[url] = body;

    /// <summary>Đăng ký nội dung chữ.</summary>
    /// <param name="url">Địa chỉ tuyệt đối.</param>
    /// <param name="text">Nội dung.</param>
    public void Set(string url, string text) => Set(url, System.Text.Encoding.UTF8.GetBytes(text));

    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var url = request.RequestUri!.ToString();
        return Task.FromResult(_responses.TryGetValue(url, out var body)
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(body) }
            : new HttpResponseMessage(HttpStatusCode.NotFound));
    }
}
