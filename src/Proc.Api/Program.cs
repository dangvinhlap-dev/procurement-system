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
        "Thiếu chuỗi kết nối 'ConnectionStrings:Procurement': đặt trong appsettings.{Môi trường}.json (máy dev: appsettings.Development.json; " +
        "Windows/IIS: appsettings.Production.json) hoặc biến môi trường ConnectionStrings__Procurement (Docker).");

builder.Services.AddHttpContextAccessor();
// Đăng ký trước AddInfrastructure để thay cho mặc định "không có người dùng".
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddScoped<IRequestContext, HttpRequestContext>();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(connectionString);

builder.Services.AddOptions<DatabaseOptions>().Bind(builder.Configuration.GetSection("Database"));
builder.Services.AddOptions<SeedOptions>().Bind(builder.Configuration.GetSection("Seed"));
// Bật/tắt phân hệ – kho tắt mặc định từ 0.22.0 (design.md mục 19.4).
builder.Services.AddOptions<Proc.Application.Common.ModuleOptions>().Bind(builder.Configuration.GetSection(Proc.Application.Common.ModuleOptions.Section));
builder.Services.AddOptions<Proc.Infrastructure.Printing.PrintingOptions>().Bind(builder.Configuration.GetSection("Printing"));
// Email bản sao thông báo qua SMTP (lát O2b, design.md mục 19.3) – mặc định tắt; mật khẩu đặt bằng biến môi trường Email__Password.
builder.Services.AddOptions<Proc.Infrastructure.Notifications.EmailOptions>()
    .Bind(builder.Configuration.GetSection(Proc.Infrastructure.Notifications.EmailOptions.Section))
    .Validate(o => !o.Enabled || (!string.IsNullOrWhiteSpace(o.Host) && !string.IsNullOrWhiteSpace(o.FromAddress)),
        "Email:Enabled = true thì phải khai Email:Host và Email:FromAddress.")
    .ValidateOnStart();
builder.Services.AddHostedService<Proc.Infrastructure.Notifications.EmailBackgroundService>();
// Nhắc quá hạn duyệt sau 1 ngày làm việc (lát O2c, design.md mục 19.3) – mặc định bật, môi trường Testing tắt.
builder.Services.AddOptions<Proc.Infrastructure.Purchasing.ApprovalReminderOptions>()
    .Bind(builder.Configuration.GetSection(Proc.Infrastructure.Purchasing.ApprovalReminderOptions.Section));
builder.Services.AddHostedService<Proc.Infrastructure.Purchasing.ApprovalReminderBackgroundService>();
// Đường dẫn tương đối tính từ thư mục ứng dụng (ContentRoot), không phụ thuộc thư mục đang đứng khi chạy lệnh (design.md mục 18.4).
builder.Services.AddOptions<Proc.Infrastructure.Documents.AttachmentOptions>()
    .Bind(builder.Configuration.GetSection("Attachments"))
    .PostConfigure(o => o.RootPath = Path.GetFullPath(o.RootPath, builder.Environment.ContentRootPath))
    // Trên 25 MB vượt giới hạn request mặc định của Kestrel/IIS (30 MB gồm cả phần đầu multipart) – báo lỗi ngay khi khởi động.
    .Validate(
        o => o.MaxFileSizeMb is >= 1 and <= Proc.Domain.Documents.Attachment.MaxConfigurableFileSizeMb,
        $"Attachments:MaxFileSizeMb phải từ 1 đến {Proc.Domain.Documents.Attachment.MaxConfigurableFileSizeMb}.")
    .ValidateOnStart();
builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection("Jwt"))
    .Validate(
        o => o.SigningKey.Length >= JwtOptions.MinSigningKeyLength,
        $"Jwt:SigningKey phải dài ít nhất {JwtOptions.MinSigningKeyLength} ký tự (server thật đặt qua biến môi trường Jwt__SigningKey).")
    .ValidateOnStart();

// Portal NCC (design.md 19.5, lát P1): token audience riêng. Mã OTP / link chỉ được trả trong response ở máy dev, test tự động và máy chủ
// thử nghiệm trong LAN (Portal:TestServer – bản test cho người dùng, chưa có máy chủ email).
var isDevelopmentOrTesting = builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing");
builder.Services.AddOptions<Proc.Infrastructure.Portal.PortalOptions>()
    .Bind(builder.Configuration.GetSection(Proc.Infrastructure.Portal.PortalOptions.Section))
    .PostConfigure(o => o.ExposeCodesForTesting = o.CanExposeCodes(isDevelopmentOrTesting))
    .Validate(
        o => !o.TestServer || o.HostList().Count == 0,
        "Portal:TestServer (hiện mã OTP trên màn hình) không được bật khi Portal mở ra internet (Portal:Hosts có tên miền).")
    .Validate(
        o => !string.IsNullOrWhiteSpace(o.Audience)
            && !string.Equals(o.Audience, builder.Configuration["Jwt:Audience"] ?? new JwtOptions().Audience, StringComparison.Ordinal),
        "Portal:Audience không được trống và phải khác Jwt:Audience (token NCC không được dùng cho API nội bộ).")
    .ValidateOnStart();

// Giới hạn tần suất API Portal theo IP (lát P1b, design.md 19.5 P1b-1).
builder.Services.AddPortalRateLimiting();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer()
    .AddJwtBearer(Proc.Infrastructure.Portal.PortalClaims.Scheme);
builder.Services.AddOptions<JwtBearerOptions>(Proc.Infrastructure.Portal.PortalClaims.Scheme)
    .Configure<IOptions<JwtOptions>, IOptions<Proc.Infrastructure.Portal.PortalOptions>>((bearer, jwt, portal) =>
    {
        bearer.MapInboundClaims = false;
        bearer.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwt.Value.Issuer,
            // Chỉ nhận token Portal; token nội bộ (audience nội bộ) bị từ chối ở API Portal và ngược lại.
            ValidAudience = portal.Value.Audience,
            IssuerSigningKey = jwt.Value.CreateSecurityKey(),
            ClockSkew = TimeSpan.FromMinutes(1),
        };
    });
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

// Thư mục đính kèm nằm trong thư mục ứng dụng thì lần cài bản mới (chép đè/xóa thư mục web trên IIS) có thể làm mất file
// (docs/deploy-windows.md). Chỉ cảnh báo, không chặn: máy dev cố ý để trong src/Proc.Api/data.
if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing"))
{
    var attachmentRoot = app.Services.GetRequiredService<IOptions<Proc.Infrastructure.Documents.AttachmentOptions>>().Value.RootPath;
    var contentRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(app.Environment.ContentRootPath)) + Path.DirectorySeparatorChar;
    if (attachmentRoot.StartsWith(contentRoot, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
        app.Logger.LogWarning(
            "Thư mục file đính kèm {AttachmentRoot} nằm trong thư mục ứng dụng – có thể mất file khi cài bản mới. Đặt Attachments:RootPath ra ngoài.",
            attachmentRoot);
}

if (!isDevelopmentOrTesting && app.Services.GetRequiredService<IOptions<Proc.Infrastructure.Portal.PortalOptions>>().Value.ExposeCodesForTesting)
    app.Logger.LogWarning("Portal:TestServer đang bật – mã OTP / link đặt lại mật khẩu Portal hiện trên màn hình. Chỉ dùng cho máy chủ thử nghiệm.");

await DatabaseInitializer.InitializeAsync(app.Services);

app.UseExceptionHandler();
app.UseSerilogRequestLogging();
app.UseMiddleware<SecurityHeadersMiddleware>();
// Tên miền Portal chỉ mở trang /ncc + API Portal (lát P1b) – đặt trước file tĩnh và xác thực để chặn sớm.
app.UseMiddleware<PortalHostMiddleware>();

// Chạy server: image chứa sẵn giao diện React đã build trong wwwroot (design.md mục 14.1).
// Máy dev không có wwwroot (Vite tự phục vụ ở cổng 5173) nên bỏ qua toàn bộ phần này.
var hostsWebApp = app.Environment.WebRootFileProvider.GetFileInfo("index.html").Exists;
if (hostsWebApp)
{
    // File tĩnh đặt trước xác thực: giao diện phải tải được trước khi đăng nhập.
    // File trong assets/ có mã băm trong tên nên lưu đệm lâu được; index.html thì không (xem MapFallbackToFile bên dưới).
    app.UseStaticFiles(new StaticFileOptions
    {
        OnPrepareResponse = ctx => ctx.Context.Response.Headers.CacheControl =
            ctx.File.Name == "index.html" ? "no-cache" : "public, max-age=31536000, immutable",
    });
}

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
// Sau xác thực: nạp phạm vi (công ty từ X-Company-Id, quyền, kho, nhóm hàng) trước khi vào endpoint.
app.UseMiddleware<CompanyScopeMiddleware>();
app.UseMiddleware<ExpectedVersionMiddleware>();

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
app.MapMasterDataEndpoints();
app.MapExcelEndpoints();
app.MapReportEndpoints();
app.MapControlEndpoints();
app.MapAnalysisEndpoints();
app.MapPrintingEndpoints();
app.MapInventoryEndpoints();
app.MapPricingEndpoints();
app.MapPurchasingEndpoints();
app.MapSourcingEndpoints();
app.MapContractEndpoints();
app.MapAdministrationEndpoints();
app.MapLogEndpoints();
app.MapDocumentNumberingEndpoints();
app.MapSystemParameterEndpoints();
app.MapNotificationEndpoints();
app.MapDocumentRegistryEndpoints();
app.MapAttachmentEndpoints();
app.MapAppraisalOpinionEndpoints();
app.MapPortalEndpoints();

// /api lạ trả 404 (không phải 401 do chính sách mặc định, cũng không rơi vào trang giao diện bên dưới).
app.MapFallback("/api/{**path}", () => Results.NotFound()).AllowAnonymous();

if (hostsWebApp)
{
    // Đường dẫn của React Router (gõ thẳng hoặc F5) đều trả index.html, để giao diện tự định tuyến.
    app.MapFallbackToFile("index.html", new StaticFileOptions
    {
        OnPrepareResponse = ctx => ctx.Context.Response.Headers.CacheControl = "no-cache",
    }).AllowAnonymous();
}

app.Run();

/// <summary>
/// Điểm vào của ứng dụng API. Khai báo public để test tích hợp dùng được WebApplicationFactory.
/// </summary>
public partial class Program;
