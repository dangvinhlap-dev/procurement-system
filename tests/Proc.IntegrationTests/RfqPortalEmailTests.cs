using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Proc.Application.Common;
using Proc.Application.MasterData;
using Proc.Application.Notifications;
using Proc.Application.Portal;
using Proc.Application.Purchasing;
using Proc.Application.Sourcing;
using Proc.Domain.MasterData;
using Proc.Domain.Notifications;
using Proc.Infrastructure.Notifications;
using Proc.Infrastructure.Persistence;
using Proc.Infrastructure.Portal;

namespace Proc.IntegrationTests;

/// <summary>
/// Lát P3b-3 (design.md mục 19.5, P3-15…P3-18): thư báo NCC qua Portal – thư mời khi gửi RFQ, gia hạn, trả lời câu hỏi (chỉ NCC hỏi), thông báo
/// làm rõ (mọi NCC), hủy RFQ đã gửi. Chỉ tài khoản Portal đang dùng nhận thư; thư không có lý do nội bộ, không nêu NCC khác; dịch vụ gửi bỏ
/// qua tài khoản bị khóa sau lúc soạn. Dùng bộ gửi giả – không cần máy chủ SMTP.
/// </summary>
public partial class PurchasingTests
{
    /// <summary>Bộ gửi giả: ghi lại thư.</summary>
    private sealed class PortalFakeSender : IEmailSender
    {
        public List<EmailMessage> Sent { get; } = [];

        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
        {
            Sent.Add(message);
            return Task.CompletedTask;
        }
    }

    /// <summary>Cấu hình email cố định (bật) cho test.</summary>
    private sealed class PortalEmailOptions(EmailOptions value) : IOptionsMonitor<EmailOptions>
    {
        public EmailOptions CurrentValue => value;

        public EmailOptions Get(string? name) => value;

        public IDisposable? OnChange(Action<EmailOptions, string?> listener) => null;
    }

    [Fact]
    public async Task EmailPortal_ThuMoiGiaHanTraLoiThongBaoHuy_ChiTaiKhoanDangDung()
    {
        var f = await ArrangeAsync();

        // NCC thứ hai được mời cùng RFQ.
        var headName = $"he.{Guid.NewGuid():N}"[..16];
        await _factory.CreateUserAsync(headName, ProcApiFactory.TestPassword, ["TCT"]);
        var head = await _factory.CreateClientInCompanyAsync(headName, ProcApiFactory.TestPassword, "TCT");
        var groupId = (await head.GetFromJsonAsync<List<SupplierGroupDto>>("/api/supplier-groups", Ct))!.Single(g => g.Code == "TN").Id;
        var second = await ReadAsync<SupplierDetailDto>(await head.PostAsJsonAsync("/api/suppliers",
            new SaveSupplierRequest(null, groupId, "NCC thứ hai – thư Portal", null, false, null, null, null, null, null, null, null, null,
                "Approved", null, null, [], []), Ct));
        await _factory.FinishSupplierApprovalAsync(second.Id);
        await ReadAsync<SupplierCompanyDto>(await f.Buyer.PutAsJsonAsync($"/api/suppliers/{second.Id}/company",
            new SaveSupplierCompanyRequest(null, null, true), Ct));
        var supplierName = (await ReadAsync<SupplierDetailDto>(await f.Buyer.GetAsync($"/api/suppliers/{f.SupplierId}", Ct))).Name;

        // Tài khoản Portal: NCC 1 một tài khoản đang dùng; NCC 2 một đang dùng + một mới mời chưa kích hoạt (không nhận thư).
        var ncc1 = await PortalClientAsync(f.Buyer, f.SupplierId);
        await PortalClientAsync(head, second.Id);
        var pendingEmail = $"{Guid.NewGuid():N}"[..10] + "@ncc.vn";
        await ReadAsync<PortalInviteResult>(await head.PostAsJsonAsync($"/api/suppliers/{second.Id}/portal-users",
            new PortalInviteRequest(pendingEmail, "Chưa kích hoạt", null, null), Ct));
        var active1 = await ActivePortalEmailsAsync(f.SupplierId);
        var active2 = await ActivePortalEmailsAsync(second.Id);
        Assert.DoesNotContain(pendingEmail, active2);

        var rfq = await ReadAsync<RfqDto>(await f.Buyer.PostAsJsonAsync("/api/rfqs",
            RfqOf(f, "Bulông – thư Portal", new SaveRfqLineRequest(null, f.ItemId, f.Cai, 10, null, null)) with
            {
                Deadline = Today.AddDays(1), DeadlineTime = new TimeOnly(17, 0), SupplierIds = [f.SupplierId, second.Id],
            }, Ct));
        var url = $"/api/rfqs/{rfq.Id}/portal-emails";
        Assert.Empty(await ReadAsync<List<SupplierPortalEmailDto>>(await f.Buyer.GetAsync(url, Ct)));

        // Gửi RFQ → thư mời cho mọi tài khoản đang dùng của cả hai NCC, chờ gửi.
        await ReadAsync<RfqDto>(await f.Buyer.PostAsJsonAsync($"/api/rfqs/{rfq.Id}/send", new SendRfqRequest("Chỉ hai NCC cung cấp"), Ct));
        var mails = await ReadAsync<List<SupplierPortalEmailDto>>(await f.Buyer.GetAsync(url, Ct));
        var invitations = mails.Where(m => m.Kind == "RfqInvitation").ToList();
        Assert.Equal(active1.Count + active2.Count, invitations.Count);
        Assert.Equal(active1.Concat(active2).Order(), invitations.Select(m => m.ToAddress).Order());
        Assert.All(invitations, m => Assert.Equal(("Pending", true), (m.State, m.Subject.Contains(rfq.No, StringComparison.Ordinal))));

        // Gia hạn → thư hạn mới, không có lý do nội bộ.
        await ReadAsync<RfqDto>(await f.Buyer.PostAsJsonAsync($"/api/rfqs/{rfq.Id}/extend-deadline",
            new ExtendRfqDeadlineRequest(Today.AddDays(3), "Lý do nội bộ bí mật", new TimeOnly(10, 30)), Ct));

        // NCC 1 hỏi, Phòng CUVT trả lời → thư chỉ tới NCC 1. Thông báo làm rõ → mọi NCC.
        var asked = await ReadAsync<PortalRfqDto>(await ncc1.PostAsJsonAsync($"/api/portal/rfqs/{rfq.Id}/questions",
            new PortalAskRequest("Bulông mạ kẽm hay inox?"), Ct));
        await ReadAsync<List<RfqClarificationDto>>(await f.Buyer.PostAsJsonAsync(
            $"/api/rfqs/{rfq.Id}/clarifications/{asked.Clarifications.Single().Id}/answer", new ReplyClarificationRequest("Inox 304."), Ct));
        await ReadAsync<List<RfqClarificationDto>>(await f.Buyer.PostAsJsonAsync($"/api/rfqs/{rfq.Id}/clarifications",
            new AnnounceClarificationRequest("Giao hàng tại kho Bình Dương."), Ct));

        mails = await ReadAsync<List<SupplierPortalEmailDto>>(await f.Buyer.GetAsync(url, Ct));
        Assert.Equal(active1.Count + active2.Count, mails.Count(m => m.Kind == "RfqDeadlineExtended"));
        Assert.Equal(active1.Count + active2.Count, mails.Count(m => m.Kind == "RfqNotice"));
        Assert.All(mails.Where(m => m.Kind == "RfqAnswer"), m => Assert.Equal(f.SupplierId, m.SupplierId));
        Assert.Equal(active1.Count, mails.Count(m => m.Kind == "RfqAnswer"));

        // Khóa một tài khoản NCC 2 sau khi soạn thư → lúc gửi thì thôi gửi thư của tài khoản đó.
        string lockedEmail;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ProcDbContext>();
            // Thư chờ gửi của các test khác không thuộc test này – đánh dấu bỏ qua để lần quét chỉ còn thư của RFQ này.
            await db.SupplierPortalEmails.IgnoreQueryFilters()
                .Where(m => m.State == NotificationEmailState.Pending && m.DocId != rfq.Id)
                .ExecuteUpdateAsync(u => u.SetProperty(m => m.State, NotificationEmailState.Skipped), Ct);
            var locked = await db.SupplierPortalUsers.FirstAsync(u => u.SupplierId == second.Id && u.Status == SupplierPortalUserStatus.Active, Ct);
            locked.Disable();
            lockedEmail = locked.Email;
            await db.SaveChangesAsync(Ct);

            // Đọc nội dung thư: thư mời gửi NCC 2 không nêu NCC 1; thư gia hạn có hạn mới, không có lý do; thư trả lời có câu hỏi và câu trả lời.
            var stored = await db.SupplierPortalEmails.IgnoreQueryFilters().Where(m => m.DocId == rfq.Id).ToListAsync(Ct);
            Assert.All(stored.Where(m => m.SupplierId == second.Id), m =>
                Assert.DoesNotContain(supplierName, m.HtmlBody + m.TextBody, StringComparison.Ordinal));
            var extended = stored.First(m => m.Kind == SupplierPortalEmailKind.RfqDeadlineExtended);
            Assert.Contains($"10:30 ngày {Today.AddDays(3):dd/MM/yyyy}", extended.TextBody, StringComparison.Ordinal);
            Assert.DoesNotContain("bí mật", extended.HtmlBody + extended.TextBody, StringComparison.Ordinal);
            var answer = stored.First(m => m.Kind == SupplierPortalEmailKind.RfqAnswer);
            Assert.Contains("Câu hỏi: Bulông mạ kẽm hay inox?", answer.TextBody, StringComparison.Ordinal);
            Assert.Contains("Trả lời: Inox 304.", answer.HtmlBody.Replace("<br>", "\n", StringComparison.Ordinal), StringComparison.Ordinal);
        }

        var sender = new PortalFakeSender();
        using (var scope = _factory.Services.CreateScope())
        {
            var sp = scope.ServiceProvider;
            var service = new PortalEmailService(sp.GetRequiredService<ProcDbContext>(), sp.GetRequiredService<IClock>(), sender,
                new PortalEmailOptions(new EmailOptions { Enabled = true, BatchSize = 500, MaxAttempts = 3, MaxAgeHours = 24 }),
                Options.Create(new PortalOptions()), NullLogger<PortalEmailService>.Instance);
            var result = await service.DispatchAsync(Ct);
            var lockedCount = mails.Count(m => m.ToAddress == lockedEmail);
            Assert.Equal((mails.Count - lockedCount, lockedCount, 0), (result.Sent, result.Skipped, result.Failed));
        }

        Assert.DoesNotContain(sender.Sent, m => m.ToAddress == lockedEmail);
        // Link Portal soạn lúc ghi thư theo Portal:BaseUrl của máy chủ (môi trường test không cấu hình nên thư không có link).
        Assert.All(sender.Sent, m => Assert.Contains(rfq.No, m.TextBody, StringComparison.Ordinal));
        mails = await ReadAsync<List<SupplierPortalEmailDto>>(await f.Buyer.GetAsync(url, Ct));
        Assert.All(mails, m => Assert.Equal(m.ToAddress == lockedEmail ? "Skipped" : "Sent", m.State));

        // Hủy RFQ đã gửi (người mua của bộ test có quyền Hủy phiếu như quản trị) → thư hủy cho tài khoản còn đang dùng.
        await ReadAsync<RfqDto>(await f.Buyer.PostAsJsonAsync($"/api/rfqs/{rfq.Id}/cancel", new CancelDocumentRequest("Đổi phương án mua"), Ct));
        mails = await ReadAsync<List<SupplierPortalEmailDto>>(await f.Buyer.GetAsync(url, Ct));
        var cancelled = mails.Where(m => m.Kind == "RfqCancelled").Select(m => m.ToAddress).ToList();
        Assert.Equal(active1.Concat(active2).Where(e => e != lockedEmail).Order(), cancelled.Order());
    }

    /// <summary>Email các tài khoản Portal đang dùng của một NCC.</summary>
    private async Task<List<string>> ActivePortalEmailsAsync(Guid supplierId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProcDbContext>();
        return await db.SupplierPortalUsers.Where(u => u.SupplierId == supplierId && u.Status == SupplierPortalUserStatus.Active)
            .Select(u => u.Email).ToListAsync(Ct);
    }
}
