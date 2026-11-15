using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace Proc.IntegrationTests;

/// <summary>
/// Chỉ có trong test: gán IP người gọi từ header <see cref="Header"/> (TestServer không có IP – coi như máy chủ tự gọi). Đứng đầu chuỗi middleware
/// nên bộ giới hạn tần suất, chốt dải IP quản trị thấy đúng IP như request thật (lát QT7).
/// </summary>
public sealed class TestClientIpStartupFilter : IStartupFilter
{
    /// <summary>Tên header mang IP giả lập.</summary>
    public const string Header = "X-Test-Client-Ip";

    /// <inheritdoc />
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use(async (context, nextMiddleware) =>
        {
            if (IPAddress.TryParse(context.Request.Headers[Header].ToString(), out var ip)) context.Connection.RemoteIpAddress = ip;
            await nextMiddleware(context);
        });
        next(app);
    };
}
