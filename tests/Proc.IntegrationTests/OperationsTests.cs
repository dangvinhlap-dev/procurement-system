using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Proc.Application.Common;
using Proc.Application.Notifications;
using Proc.Application.Operations;
using Proc.Domain.Common;
using Proc.Domain.Operations;
using Proc.Domain.Organization;
using Proc.Infrastructure.Notifications;
using Proc.Infrastructure.Operations;
using Proc.Infrastructure.Persistence;

namespace Proc.IntegrationTests;

/// <summary>
/// Lát QT1 – module Quản trị vận hành (design.md mục 21): tổng quan SYS (quyền <c>System.Ops.View</c>, chỉ ở tổng công ty, số migration),
/// nhật ký vận hành chỉ thêm và có bản ở file, chuyển hướng email của máy TEST, API môi trường ẩn danh.
/// </summary>
[Collection(ApiCollection.Name)]
public class OperationsTests
{
    private readonly ProcApiFactory _factory;

    public OperationsTests(ProcApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTime.UtcNow, VietnamTime.Zone));

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(Ct)}");
        return (await response.Content.ReadFromJsonAsync<T>(Ct))!;
    }

    /// <summary>
    /// SYS ở tổng công ty: không còn migration chờ, đã chạy đủ migration của bản cài, có dòng phiên bản và dung lượng CSDL. Công ty
    /// thành viên (dù là quản trị) và người thiếu quyền đều 403.
    /// </summary>
    [Fact]
    public async Task TongQuan_ChiTongCongTy_CoQuyen_SoMigrationKhopBanCai()
    {
        var head = await _factory.CreateClientInCompanyAsync(ProcApiFactory.AdminUserName, ProcApiFactory.AdminPassword, "TCT");
        var overview = await ReadAsync<SystemOverviewDto>(await head.GetAsync("/api/ops/v1/overview", Ct));

        using (var scope = _factory.Services.CreateScope())
        {
            // Lịch sử CSDL có thể còn dòng của migration nhánh đã bỏ (CSDL test còn V0_26_0_HoaDonNcc của P6a) nên chỉ so "đủ mọi migration của code".
            var db = scope.ServiceProvider.GetRequiredService<ProcDbContext>();
            Assert.True(overview.AppliedMigrations >= db.Database.GetMigrations().Count());
        }

        Assert.Empty(overview.PendingMigrations);
        Assert.Equal(AppEnvironments.Dev, overview.Environment);
        Assert.Contains(overview.Rows, r => r.Item == "Phiên bản" && r.Value == overview.Version);
        Assert.Contains(overview.Rows, r => r.Item == "Migration đang chờ" && r.Status == "Ok");
        Assert.Contains(overview.Rows, r => r.Item == "Dung lượng CSDL" && r.Value.EndsWith("B", StringComparison.Ordinal));
        Assert.Contains(overview.Rows, r => r.Group == "Ổ đĩa");

        var member = await _factory.CreateClientInCompanyAsync(ProcApiFactory.AdminUserName, ProcApiFactory.AdminPassword, "BHA");
        var atMember = await member.GetAsync("/api/ops/v1/overview", Ct);
        Assert.Equal(HttpStatusCode.Forbidden, atMember.StatusCode);
        Assert.Contains("tổng công ty", await atMember.Content.ReadAsStringAsync(Ct));
        Assert.Equal(HttpStatusCode.Forbidden, (await member.GetAsync($"/api/ops/v1/operation-logs?from={Today:yyyy-MM-dd}&to={Today:yyyy-MM-dd}", Ct)).StatusCode);

        var userName = $"op.{Guid.NewGuid():N}"[..16];
        await _factory.CreateUserAsync(userName, ProcApiFactory.TestPassword, ["TCT"]);
        var roleCode = $"V{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        await _factory.CreateRoleAsync(roleCode, Permissions.System.LogView);
        var viewerName = $"ov.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(viewerName, "TCT", roleCode);
        var noOps = await _factory.CreateClientInCompanyAsync(viewerName, ProcApiFactory.TestPassword, "TCT");
        Assert.Equal(HttpStatusCode.Forbidden, (await noOps.GetAsync("/api/ops/v1/overview", Ct)).StatusCode);
    }

    /// <summary>API môi trường ẩn danh chỉ trả tên môi trường (trang đăng nhập vẽ dải màu); cấu hình Testing đặt Dev.</summary>
    [Fact]
    public async Task MoiTruong_AnDanh_ChiTraTen()
    {
        var anonymous = _factory.CreateClient();
        var body = await ReadAsync<Dictionary<string, string>>(await anonymous.GetAsync("/api/ops/v1/environment", Ct));
        Assert.Equal(["environment"], body.Keys);
        Assert.Equal(AppEnvironments.Dev, body["environment"]);
    }

    /// <summary>
    /// Nhật ký vận hành: ghi một dòng thì có ở CSDL, ở file ngày (JSON) và ở API danh sách; CSDL chặn sửa / xóa.
    /// </summary>
    [Fact]
    public async Task NhatKyVanHanh_GhiCsdlVaFile_ChiThem()
    {
        var tag = $"Thử {Guid.NewGuid():N}";
        OperationLogItemDto written;
        string directory;
        using (var scope = _factory.Services.CreateScope())
        {
            written = await scope.ServiceProvider.GetRequiredService<IOperationLog>().WriteAsync(
                new OperationLogEntry("TestEntry", tag, "Kiểm tra lát QT1", OperationResult.Succeeded, "Chi tiết thử"), Ct);
            directory = scope.ServiceProvider.GetRequiredService<IOptions<OperationsOptions>>().Value.LogDirectory;
        }

        Assert.Equal((AppEnvironments.Dev, "Succeeded", (string?)null), (written.Environment, written.Result, written.UserName));
        var file = OperationLogWriter.FilePath(directory, Today);
        Assert.True(Path.IsPathRooted(directory));
        Assert.Contains(written.Id.ToString(), await File.ReadAllTextAsync(file, Ct));

        var head = await _factory.CreateClientInCompanyAsync(ProcApiFactory.AdminUserName, ProcApiFactory.AdminPassword, "TCT");
        var page = await ReadAsync<OperationLogPage>(await head.GetAsync($"/api/ops/v1/operation-logs?from={Today:yyyy-MM-dd}&to={Today:yyyy-MM-dd}", Ct));
        var row = Assert.Single(page.Items, i => i.Id == written.Id);
        Assert.Equal((tag, "Kiểm tra lát QT1"), (row.Target, row.Reason));
        Assert.Equal(HttpStatusCode.BadRequest,
            (await head.GetAsync($"/api/ops/v1/operation-logs?from={Today:yyyy-MM-dd}&to={Today.AddDays(-1):yyyy-MM-dd}", Ct)).StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ProcDbContext>();
            var update = await Assert.ThrowsAnyAsync<Exception>(() =>
                db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"OperationLog\" SET \"Reason\" = 'sửa' WHERE \"Id\" = {written.Id}", Ct));
            Assert.Contains("chỉ được thêm", update.Message);
            await Assert.ThrowsAnyAsync<Exception>(() =>
                db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM \"OperationLog\" WHERE \"Id\" = {written.Id}", Ct));
        }
    }

    /// <summary>Dòng nhật ký: môi trường lạ hoặc thiếu loại việc bị từ chối; chữ dài cắt theo cột.</summary>
    [Fact]
    public void NhatKyVanHanh_KiemTraDuLieu()
    {
        Assert.Throws<BusinessRuleException>(() =>
            OperationLog.Create(DateTimeOffset.UtcNow, "Staging", null, null, "X", null, null, OperationResult.Info, null));
        Assert.Throws<BusinessRuleException>(() =>
            OperationLog.Create(DateTimeOffset.UtcNow, "Test", null, null, " ", null, null, OperationResult.Info, null));
        var log = OperationLog.Create(DateTimeOffset.UtcNow, "test", null, " ", "Kind", new string('a', 600), null, OperationResult.Info, null);
        Assert.Equal((AppEnvironments.Test, (string?)null, OperationLog.TextMaxLength), (log.Environment, log.UserName, log.Target!.Length));
    }

    /// <summary>Bộ gửi giả ghi lại thư.</summary>
    private sealed class FakeSender : IEmailSender
    {
        public List<EmailMessage> Sent { get; } = [];

        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
        {
            Sent.Add(message);
            return Task.CompletedTask;
        }
    }

    /// <summary>Cấu hình email cố định.</summary>
    private sealed class FixedOptions(EmailOptions value) : IOptionsMonitor<EmailOptions>
    {
        public EmailOptions CurrentValue => value;

        public EmailOptions Get(string? name) => value;

        public IDisposable? OnChange(Action<EmailOptions, string?> listener) => null;
    }

    /// <summary>
    /// Chuyển hướng email (E5): có RedirectAllTo thì thư đi về hộp thư test, tiêu đề ghi người nhận gốc; máy TEST chưa khai thì không gửi;
    /// máy PROD không khai thì gửi thật như cũ. Ứng dụng đăng ký bộ gửi qua chốt chuyển hướng.
    /// </summary>
    [Fact]
    public async Task Email_MayTest_ChuyenVeHopThuTest_ChuaKhaiThiKhongGui()
    {
        var message = new EmailMessage("an@congty.vn", "Nguyễn An", "Phiếu chờ duyệt", "<p>x</p>", "x");
        RedirectingEmailSender Sender(FakeSender inner, string? redirect, string environment) =>
            new(inner, new FixedOptions(new EmailOptions { RedirectAllTo = redirect }),
                Options.Create(new AppEnvironmentOptions { Environment = environment }));

        var test = new FakeSender();
        await Sender(test, " test@congty.vn ", AppEnvironments.Test).SendAsync(message, Ct);
        var sent = Assert.Single(test.Sent);
        Assert.Equal(("test@congty.vn", "[TEST] gửi cho: an@congty.vn – Phiếu chờ duyệt"), (sent.ToAddress, sent.Subject));

        var blocked = new FakeSender();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => Sender(blocked, null, AppEnvironments.Test).SendAsync(message, Ct));
        Assert.Contains("RedirectAllTo", error.Message);
        Assert.Empty(blocked.Sent);

        var prod = new FakeSender();
        await Sender(prod, "", AppEnvironments.Prod).SendAsync(message, Ct);
        Assert.Equal(message, Assert.Single(prod.Sent));

        Assert.IsType<RedirectingEmailSender>(_factory.Services.GetRequiredService<IEmailSender>());
    }
}
