using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Proc.Application.Common;
using Proc.Application.Notifications;
using Proc.Domain.Notifications;
using Proc.Infrastructure.Notifications;
using Proc.Infrastructure.Persistence;

namespace Proc.IntegrationTests;

/// <summary>
/// Email bản sao thông báo (lát O2b, design.md mục 19.3): dịch vụ quét gửi thư cho người nhận có email, bỏ qua người chưa khai email và thông báo
/// quá cũ, gửi lỗi thì thử lại đến hết lượt. Dùng bộ gửi giả – không cần máy chủ SMTP.
/// </summary>
[Collection(ApiCollection.Name)]
public class NotificationEmailTests
{
    private readonly ProcApiFactory _factory;

    public NotificationEmailTests(ProcApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Bộ gửi giả: ghi lại thư; địa chỉ chứa "loi" thì báo lỗi như máy chủ thư từ chối.</summary>
    private sealed class FakeSender : IEmailSender
    {
        public List<EmailMessage> Sent { get; } = [];

        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
        {
            if (message.ToAddress.Contains("loi")) throw new InvalidOperationException("550 Mailbox unavailable");
            Sent.Add(message);
            return Task.CompletedTask;
        }
    }

    /// <summary>Cấu hình cố định cho test.</summary>
    private sealed class FixedOptions(EmailOptions value) : IOptionsMonitor<EmailOptions>
    {
        public EmailOptions CurrentValue => value;

        public EmailOptions Get(string? name) => value;

        public IDisposable? OnChange(Action<EmailOptions, string?> listener) => null;
    }

    private static NotificationEmailService Service(IServiceProvider services, FakeSender sender, EmailOptions options) =>
        new(services.GetRequiredService<ProcDbContext>(), services.GetRequiredService<ICurrentUserScope>(), services.GetRequiredService<IClock>(),
            sender, new FixedOptions(options), NullLogger<NotificationEmailService>.Instance);

    [Fact]
    public async Task GuiThuChoNguoiCoEmail_BoQuaKhongEmailVaQuaCu_ThuLaiKhiLoi()
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        var withEmail = await _factory.CreateUserAsync($"em.{tag}", ProcApiFactory.TestPassword, ["TCT"]);
        var noEmail = await _factory.CreateUserAsync($"ne.{tag}", ProcApiFactory.TestPassword, ["TCT"]);
        var badEmail = await _factory.CreateUserAsync($"be.{tag}", ProcApiFactory.TestPassword, ["TCT"]);
        var prId = Guid.NewGuid();
        Guid sentId, skippedId, oldId, failingId;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ProcDbContext>();
            // Thông báo chờ gửi của các test khác không thuộc test này – đánh dấu bỏ qua để lần quét chỉ còn thông báo dưới đây.
            await db.Notifications.IgnoreQueryFilters()
                .Where(n => n.EmailState == NotificationEmailState.Pending)
                .ExecuteUpdateAsync(u => u.SetProperty(n => n.EmailState, NotificationEmailState.Skipped), Ct);
            foreach (var (id, email) in new[] { (withEmail, $"em.{tag}@congty.vn"), (badEmail, $"loi.{tag}@congty.vn") })
                (await db.Users.SingleAsync(u => u.Id == id, Ct)).Email = email;
            var tct = await db.Companies.IgnoreQueryFilters().Where(c => c.Code == "TCT").Select(c => c.Id).SingleAsync(Ct);
            var now = DateTimeOffset.UtcNow;
            Notification Make(Guid user, DateTimeOffset at) =>
                Notification.Create(tct, user, NotificationTypes.ApprovalPending, "Đề nghị mua hàng PR-1 chờ bạn duyệt",
                    "Đề nghị mua hàng PR-1 trị giá 1.000.000 đ <đang chờ> duyệt cấp 1.", "PR", prId, at);
            var sent = Make(withEmail, now);
            var skipped = Make(noEmail, now);
            var old = Make(withEmail, now.AddHours(-30));
            var failing = Make(badEmail, now);
            db.Notifications.AddRange(sent, skipped, old, failing);
            await db.SaveChangesAsync(Ct);
            (sentId, skippedId, oldId, failingId) = (sent.Id, skipped.Id, old.Id, failing.Id);
        }

        var sender = new FakeSender();
        var options = new EmailOptions { Enabled = true, Host = "smtp.test", FromAddress = "muahang@congty.vn", AppBaseUrl = "http://muahang.local/", MaxAttempts = 2 };
        using (var scope = _factory.Services.CreateScope())
        {
            // Email tắt thì không gửi gì.
            Assert.Equal(new EmailDispatchResult(0, 0, 0), await Service(scope.ServiceProvider, sender, new EmailOptions()).DispatchAsync(Ct));
            Assert.Equal(new EmailDispatchResult(1, 2, 1), await Service(scope.ServiceProvider, sender, options).DispatchAsync(Ct));
        }

        var mail = Assert.Single(sender.Sent);
        Assert.Equal(($"em.{tag}@congty.vn", "[TCT] Đề nghị mua hàng PR-1 chờ bạn duyệt"), (mail.ToAddress, mail.Subject));
        Assert.Contains($"http://muahang.local/mo/PR/{prId}?cty=", mail.TextBody);
        // Nội dung thông báo được mã hóa HTML.
        Assert.Contains(WebUtility.HtmlEncode("<đang chờ>"), mail.HtmlBody);

        using (var scope = _factory.Services.CreateScope())
        {
            // Lần quét sau: thư lỗi hết lượt thử (2) thì thôi.
            Assert.Equal(new EmailDispatchResult(0, 0, 1), await Service(scope.ServiceProvider, sender, options).DispatchAsync(Ct));
            var db = scope.ServiceProvider.GetRequiredService<ProcDbContext>();
            var states = await db.Notifications.IgnoreQueryFilters().AsNoTracking()
                .Where(n => new[] { sentId, skippedId, oldId, failingId }.Contains(n.Id))
                .ToDictionaryAsync(n => n.Id, n => (n.EmailState, n.EmailAttempts, n.EmailNote), Ct);
            Assert.Equal((NotificationEmailState.Sent, 1, (string?)null), states[sentId]);
            Assert.Equal((NotificationEmailState.Skipped, 0, "Người nhận chưa khai email."), states[skippedId]);
            Assert.Equal(NotificationEmailState.Skipped, states[oldId].EmailState);
            Assert.Equal((NotificationEmailState.Failed, 2, "550 Mailbox unavailable"), states[failingId]);
            Assert.True((await Service(scope.ServiceProvider, sender, options).StatusAsync(Ct)).Failed >= 1);
        }
    }
}
