using System.Reflection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Proc.Api.Health;
using Proc.Infrastructure;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Log kỹ thuật qua Serilog (cấu hình ở mục "Serilog" của appsettings);
// log nghiệp vụ để ở bảng AuditLog riêng – design.md mục 2.2.
builder.Services.AddSerilog((services, logger) => logger
    .ReadFrom.Configuration(builder.Configuration)
    .ReadFrom.Services(services));

var connectionString = builder.Configuration.GetConnectionString("Procurement")
    ?? throw new InvalidOperationException(
        "Thiếu chuỗi kết nối 'ConnectionStrings:Procurement' (đặt trong appsettings.Development.json).");

builder.Services.AddInfrastructure(connectionString);
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

var version = typeof(Program).Assembly
    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";

// Bỏ phần "+<mã commit>" mà SDK tự gắn vào InformationalVersion, chỉ giữ số phiên bản.
version = version.Split('+')[0];

// Health check: 200 khi CSDL kết nối được, 503 khi không; trả JSON để giao diện và giám sát đọc được.
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = (context, report) => HealthResponseWriter.WriteAsync(context, report, version),
});

app.Run();

/// <summary>
/// Điểm vào của ứng dụng API. Khai báo public để test tích hợp dùng được WebApplicationFactory.
/// </summary>
public partial class Program;
