using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Proc.IntegrationTests;

/// <summary>
/// Dựng API trong bộ nhớ ở môi trường "Testing", trỏ tới database procurement_test (appsettings.Testing.json).
/// Có thể đổi chuỗi kết nối qua biến môi trường <c>ConnectionStrings__Procurement</c> (ví dụ trên máy CI).
/// </summary>
public sealed class ProcApiFactory : WebApplicationFactory<Program>
{
    /// <summary>Tên database bắt buộc khi chạy test – chặn test vô tình chạy trên CSDL dev hoặc thật.</summary>
    public const string ExpectedDatabase = "procurement_test";

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
    }
}
