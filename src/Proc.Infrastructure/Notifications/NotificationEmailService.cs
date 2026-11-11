using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Proc.Application.Common;
using Proc.Application.Notifications;
using Proc.Domain.Common;
using Proc.Domain.Notifications;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.Notifications;

/// <summary>
/// Gửi email bản sao thông báo (lát O2b, design.md mục 19.3). Thông báo vẫn sinh cùng giao dịch với nghiệp vụ như cũ (NotificationPublisher);
/// dịch vụ này chỉ đọc thông báo "chờ gửi email" rồi gửi – không phải sửa từng nơi sinh thông báo, gửi thư lỗi không làm hỏng thao tác gốc.
/// Chạy ngoài request nên đọc thông báo của mọi công ty (bỏ filter công ty) – chỉ để gửi cho đúng người nhận đã ghi trên thông báo.
/// </summary>
public sealed class NotificationEmailService : INotificationEmailService
{
    private readonly ProcDbContext _db;
    private readonly ICurrentUserScope _scope;
    private readonly IClock _clock;
    private readonly IEmailSender _sender;
    private readonly IOptionsMonitor<EmailOptions> _options;
    private readonly ILogger<NotificationEmailService> _logger;

    /// <summary>Tạo dịch vụ.</summary>
    /// <param name="db">DbContext.</param>
    /// <param name="scope">Người đang đăng nhập (chỉ dùng cho thư thử).</param>
    /// <param name="clock">Đồng hồ.</param>
    /// <param name="sender">Bộ gửi thư.</param>
    /// <param name="options">Cấu hình email.</param>
    /// <param name="logger">Nhật ký.</param>
    public NotificationEmailService(
        ProcDbContext db, ICurrentUserScope scope, IClock clock, IEmailSender sender, IOptionsMonitor<EmailOptions> options,
        ILogger<NotificationEmailService> logger)
    {
        _db = db;
        _scope = scope;
        _clock = clock;
        _sender = sender;
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<EmailDispatchResult> DispatchAsync(CancellationToken cancellationToken)
    {
        var o = _options.CurrentValue;
        if (!o.Enabled) return new(0, 0, 0);
        var now = _clock.UtcNow;
        var batch = await _db.Notifications.IgnoreQueryFilters([ProcDbContext.CompanyFilter])
            .Where(n => n.EmailState == NotificationEmailState.Pending)
            .OrderBy(n => n.CreatedAt)
            .Take(Math.Max(1, o.BatchSize))
            .ToListAsync(cancellationToken);
        if (batch.Count == 0) return new(0, 0, 0);

        var userIds = batch.Select(n => n.UserId).Distinct().ToList();
        var users = await _db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => new { u.Email, u.FullName, u.IsActive }, cancellationToken);
        var companyIds = batch.Select(n => n.CompanyId).Distinct().ToList();
        var companies = await _db.Companies.AsNoTracking().IgnoreQueryFilters().Where(c => companyIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => new { c.Code, c.Name }, cancellationToken);

        int sent = 0, skipped = 0, failed = 0;
        foreach (var n in batch)
        {
            var user = users.GetValueOrDefault(n.UserId);
            // Quá cũ thì thôi gửi: vừa bật email (thông báo tồn từ lúc tắt) hoặc máy chủ thư hỏng lâu – thư muộn gây nhầm, thông báo vẫn có trong
            // phần mềm.
            if (n.CreatedAt < now.AddHours(-Math.Max(1, o.MaxAgeHours))) n.SkipEmail(now, $"Thông báo quá {o.MaxAgeHours} giờ khi đến lượt gửi email.");
            else if (user is null || !user.IsActive) n.SkipEmail(now, "Người nhận đã ngừng dùng.");
            else if (string.IsNullOrWhiteSpace(user.Email)) n.SkipEmail(now, "Người nhận chưa khai email.");
            else
            {
                var company = companies.GetValueOrDefault(n.CompanyId);
                try
                {
                    await _sender.SendAsync(Compose(n, user.Email!, user.FullName, company?.Code, company?.Name, o.AppBaseUrl), cancellationToken);
                    n.MarkEmailSent(_clock.UtcNow);
                    sent++;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Không ghi nội dung thư vào nhật ký (có số phiếu, số tiền) – chỉ Id thông báo và lỗi.
                    _logger.LogWarning(ex, "Gửi email thông báo {NotificationId} lỗi (lần {Attempt}).", n.Id, n.EmailAttempts + 1);
                    n.MarkEmailFailed(_clock.UtcNow, ex.Message, Math.Max(1, o.MaxAttempts));
                    failed++;
                }
            }

            if (n.EmailState == NotificationEmailState.Skipped) skipped++;
            // Lưu từng thư: dừng giữa chừng (tắt máy chủ) thì thư đã gửi không gửi lại.
            await _db.SaveChangesAsync(cancellationToken);
        }

        return new(sent, skipped, failed);
    }

    /// <inheritdoc />
    public async Task<EmailStatusDto> StatusAsync(CancellationToken cancellationToken)
    {
        var o = _options.CurrentValue;
        var counts = await _db.Notifications.IgnoreQueryFilters([ProcDbContext.CompanyFilter])
            .Where(n => n.EmailState == NotificationEmailState.Pending || n.EmailState == NotificationEmailState.Failed)
            .GroupBy(n => n.EmailState)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);
        return new EmailStatusDto(
            o.Enabled, o.Host, o.Port, o.FromAddress, o.AppBaseUrl,
            counts.GetValueOrDefault(NotificationEmailState.Pending), counts.GetValueOrDefault(NotificationEmailState.Failed));
    }

    /// <inheritdoc />
    public async Task<string> SendTestAsync(CancellationToken cancellationToken)
    {
        var o = _options.CurrentValue;
        if (!o.Enabled) throw new BusinessRuleException("Email đang tắt (Email:Enabled = false trong cấu hình máy chủ).");
        var userId = _scope.UserId ?? throw new ForbiddenAccessException("Chưa đăng nhập.");
        var user = await _db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => new { u.Email, u.FullName }).SingleAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(user.Email)) throw new BusinessRuleException("Tài khoản của bạn chưa khai email – sửa ở Hệ thống → Người dùng.");
        var body = $"Thư thử từ phần mềm mua hàng lúc {TimeZoneInfo.ConvertTime(_clock.UtcNow, VietnamTime.Zone):dd/MM/yyyy HH:mm}. "
            + "Nhận được thư này nghĩa là cấu hình máy chủ thư đã đúng.";
        try
        {
            await _sender.SendAsync(new EmailMessage(user.Email!, user.FullName, "Thư thử – phần mềm mua hàng", Html("Thư thử", body, null, null), body), cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new BusinessRuleException($"Máy chủ thư báo lỗi: {ex.Message}");
        }

        return user.Email!;
    }

    /// <summary>
    /// Soạn thư của một thông báo. Tiêu đề có mã công ty để người làm nhiều công ty biết ngay thư của công ty nào, ví dụ
    /// "[BHA] Đề nghị mua hàng PR-BHA-2611-0001 chờ bạn duyệt".
    /// </summary>
    private static EmailMessage Compose(Notification n, string email, string fullName, string? companyCode, string? companyName, string appBaseUrl)
    {
        var link = OpenLink(appBaseUrl, n);
        var subject = companyCode is null ? n.Title : $"[{companyCode}] {n.Title}";
        var text = $"{n.Title}\n\n{n.Message}\n\nCông ty: {companyName}\n" + (link is null ? string.Empty : $"Mở trên phần mềm: {link}\n")
            + "\nThư gửi tự động từ phần mềm mua hàng – không trả lời thư này.";
        return new EmailMessage(email, fullName, subject, Html(n.Title, n.Message, companyName, link), text);
    }

    /// <summary>
    /// Đường dẫn mở thông báo trên phần mềm: trang <c>/mo/{mã màn hình}/{Id}?cty={công ty}</c> của giao diện tự đổi sang công ty của thông báo
    /// rồi mở phiếu. Null khi chưa cấu hình địa chỉ phần mềm.
    /// </summary>
    internal static string? OpenLink(string appBaseUrl, Notification n)
    {
        if (string.IsNullOrWhiteSpace(appBaseUrl)) return null;
        var path = n.TargetId is { } id ? $"/mo/{Uri.EscapeDataString(n.ScreenCode)}/{id}" : $"/mo/{Uri.EscapeDataString(n.ScreenCode)}";
        return $"{appBaseUrl.TrimEnd('/')}{path}?cty={n.CompanyId}";
    }

    /// <summary>Thân thư HTML kiểu đơn giản (bảng, màu nhạt như giao diện cổ điển); mọi nội dung đều được mã hóa HTML.</summary>
    private static string Html(string title, string message, string? companyName, string? link)
    {
        static string E(string? s) => WebUtility.HtmlEncode(s ?? string.Empty);
        var button = link is null
            ? string.Empty
            : $"<p><a href=\"{E(link)}\" style=\"display:inline-block;padding:4px 12px;background:#f3efdf;border:1px solid #b5a97f;color:#1b2a3a;text-decoration:none\">Mở trên phần mềm</a></p>";
        var company = companyName is null ? string.Empty : $"<p style=\"color:#555\">Công ty: {E(companyName)}</p>";
        return "<div style=\"font-family:Tahoma,Arial,sans-serif;font-size:13px;color:#1b2a3a\">"
            + $"<div style=\"background:#d6e1ee;padding:6px 10px;font-weight:bold;color:#1f3d6b\">{E(title)}</div>"
            + $"<div style=\"padding:8px 10px;border:1px solid #b8c5d6;border-top:0\"><p>{E(message)}</p>{company}{button}"
            + "<p style=\"color:#888;font-size:11px\">Thư gửi tự động từ phần mềm mua hàng – không trả lời thư này.</p></div></div>";
    }
}
