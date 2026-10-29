using System.Reflection;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Proc.Api.Endpoints;
using Proc.Api.Errors;
using Proc.Api.Health;
using Proc.Api.Security;
using Proc.Application;
using Proc.Application.Common;
using Proc.Infrastructure;
using Proc.Infrastructure.Identity;
using Proc.Infrastructure.Persistence;
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

builder.Services.AddHttpContextAccessor();
// Đăng ký trước AddInfrastructure để thay cho mặc định "không có người dùng".
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(connectionString);

builder.Services.AddOptions<DatabaseOptions>().Bind(builder.Configuration.GetSection("Database"));
builder.Services.AddOptions<SeedOptions>().Bind(builder.Configuration.GetSection("Seed"));
builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection("Jwt"))
    .Validate(
        o => o.SigningKey.Length >= JwtOptions.MinSigningKeyLength,
        $"Jwt:SigningKey phải dài ít nhất {JwtOptions.MinSigningKeyLength} ký tự (server thật đặt qua biến môi trường Jwt__SigningKey).")
    .ValidateOnStart();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((bearer, jwt) =>
    {
        // Giữ nguyên tên claim chuẩn ("sub") thay vì đổi sang tên dài kiểu WS-Federation.
        bearer.MapInboundClaims = false;
        bearer.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwt.Value.Issuer,
            ValidAudience = jwt.Value.Audience,
            IssuerSigningKey = jwt.Value.CreateSecurityKey(),
            ClockSkew = TimeSpan.FromMinutes(1),
        };
    });

// Mặc định mọi endpoint đều phải đăng nhập; endpoint công khai phải ghi rõ AllowAnonymous.
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ProblemExceptionHandler>();
builder.Services.AddOpenApi();

var app = builder.Build();

await DatabaseInitializer.InitializeAsync(app.Services);

app.UseExceptionHandler();
app.UseSerilogRequestLogging();
app.UseAuthentication();
app.UseAuthorization();
// Sau xác thực: nạp phạm vi (công ty từ X-Company-Id, quyền, kho, nhóm hàng) trước khi vào endpoint.
app.UseMiddleware<CompanyScopeMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

var version = typeof(Program).Assembly
    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";

// Bỏ phần "+<mã commit>" mà SDK tự gắn vào InformationalVersion, chỉ giữ số phiên bản.
version = version.Split('+')[0];

// Health check: 200 khi CSDL kết nối được, 503 khi không; trả JSON để giao diện và giám sát đọc được.
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = (context, report) => HealthResponseWriter.WriteAsync(context, report, version),
}).AllowAnonymous();

app.MapAuthEndpoints();
app.MapOrganizationEndpoints();

app.Run();

/// <summary>
/// Điểm vào của ứng dụng API. Khai báo public để test tích hợp dùng được WebApplicationFactory.
/// </summary>
public partial class Program;
