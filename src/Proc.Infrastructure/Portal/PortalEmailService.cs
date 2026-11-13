using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Proc.Application.Common;
using Proc.Application.Notifications;
using Proc.Application.Portal;
using Proc.Domain.Common;
using Proc.Domain.MasterData;
using Proc.Domain.Notifications;
using Proc.Domain.Sourcing;
using Proc.Infrastructure.Notifications;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.Portal;

/// <summary>
/// Thư báo NCC qua Portal (lát P3b-3 – design.md mục 19.5, P3-15…P3-18; thư về PO – lát P4a, P4-5). Thư soạn sẵn vào bảng <see cref="SupplierPortalEmail"/> cùng giao dịch
/// với thao tác RFQ; <see cref="EmailBackgroundService"/> gọi <see cref="DispatchAsync"/> để gửi. Nội dung thư **không có giá, không nêu NCC
/// khác, không ghi lý do nội bộ** (lý do gia hạn / hủy có thể nhắc tên NCC khác) – NCC xem chi tiết trên Portal sau khi đăng nhập.
/// </summary>
public sealed class PortalEmailService : IPortalEmailService
{
    /// <summary>Loại chứng từ của thư về RFQ.</summary>
    public const string RfqDocType = "RFQ";

    /// <summary>Loại chứng từ của thư về PO (lát P4a).</summary>
    public const string PurchaseOrderDocType = "PO";

    private readonly ProcDbContext _db;
    private readonly IClock _clock;
    private readonly IEmailSender _sender;
    private readonly IOptionsMonitor<EmailOptions> _emailOptions;
    private readonly PortalOptions _options;
    private readonly ILogger<PortalEmailService> _logger;

    /// <summary>Tạo dịch vụ.</summary>
    /// <param name="db">DbContext.</param>
    /// <param name="clock">Đồng hồ.</param>
    /// <param name="sender">Bộ gửi thư.</param>
    /// <param name="emailOptions">Cấu hình email (bật/tắt, lô, số lần thử, tuổi tối đa).</param>
    /// <param name="options">Cấu hình Portal (địa chỉ Portal cho link trong thư).</param>
    /// <param name="logger">Nhật ký.</param>
    public PortalEmailService(
        ProcDbContext db, IClock clock, IEmailSender sender, IOptionsMonitor<EmailOptions> emailOptions, IOptions<PortalOptions> options,
        ILogger<PortalEmailService> logger)
    {
        _db = db;
        _clock = clock;
        _sender = sender;
        _emailOptions = emailOptions;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<int> QueueRfqAsync(
        Rfq rfq, SupplierPortalEmailKind kind, Guid? onlySupplierId, string? detail, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(rfq);
        var supplierIds = rfq.Suppliers.Select(s => s.SupplierId).Where(id => onlySupplierId is null || id == onlySupplierId).Distinct().ToList();
        var recipients = await RecipientsAsync(supplierIds, cancellationToken);
        if (recipients.Count == 0) return 0;

        var company = await _db.Companies.AsNoTracking().IgnoreQueryFilters().Where(c => c.Id == rfq.CompanyId)
            .Select(c => new { c.Code, c.Name }).SingleAsync(cancellationToken);
        var link = PortalLink($"bao-gia/{rfq.Id}");
        var now = _clock.UtcNow;
        foreach (var r in recipients)
        {
            var (subject, title, lines) = Content(rfq, kind, company.Name, r.SupplierName, detail);
            subject = $"[{company.Code}] {subject}";
            _db.SupplierPortalEmails.Add(SupplierPortalEmail.Queue(
                rfq.CompanyId, r.SupplierId, r.Id, r.Email, r.FullName, kind, RfqDocType, rfq.Id, subject,
                Html(r.FullName, title, lines, link), Text(r.FullName, title, lines, link), now));
        }

        return recipients.Count;
    }

    /// <inheritdoc />
    public async Task<int> QueuePurchaseOrderAsync(
        Proc.Domain.Purchasing.PurchaseOrder order, SupplierPortalEmailKind kind, string? detail, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(order);
        // PO mua nội bộ: công ty bán xác nhận ở màn hình Đơn bán nội bộ (lát X7), không qua Portal.
        if (order.InternalSellerCompanyId is not null) return 0;
        var recipients = await RecipientsAsync([order.SupplierId], cancellationToken);
        if (recipients.Count == 0) return 0;
        var company = await _db.Companies.AsNoTracking().IgnoreQueryFilters().Where(c => c.Id == order.CompanyId)
            .Select(c => new { c.Code, c.Name }).SingleAsync(cancellationToken);
        // Thư thanh toán mở trang Thanh toán của đơn (lát P6b); thư khác mở trang đơn.
        var link = PortalLink(kind == SupplierPortalEmailKind.PaymentPaid ? $"thanh-toan/{order.Id}" : $"don-hang/{order.Id}");
        var now = _clock.UtcNow;
        foreach (var r in recipients)
        {
            var (subject, title, lines) = OrderContent(order, kind, company.Name, r.SupplierName, detail);
            _db.SupplierPortalEmails.Add(SupplierPortalEmail.Queue(
                order.CompanyId, r.SupplierId, r.Id, r.Email, r.FullName, kind, PurchaseOrderDocType, order.Id, $"[{company.Code}] {subject}",
                Html(r.FullName, title, lines, link), Text(r.FullName, title, lines, link), now));
        }

        return recipients.Count;
    }

    /// <summary>
    /// Người nhận: tài khoản Portal đang dùng (đã kích hoạt, chưa khóa) của các NCC không ở danh sách đen – người chưa kích hoạt chưa đăng nhập
    /// được để xem trên Portal (P3-15).
    /// </summary>
    private async Task<List<Recipient>> RecipientsAsync(IReadOnlyCollection<Guid> supplierIds, CancellationToken cancellationToken)
    {
        if (supplierIds.Count == 0) return [];
        return await (
                from u in _db.SupplierPortalUsers.AsNoTracking()
                join s in _db.Suppliers.AsNoTracking().IgnoreQueryFilters() on u.SupplierId equals s.Id
                where supplierIds.Contains(u.SupplierId) && u.Status == SupplierPortalUserStatus.Active && s.Status != SupplierStatus.Blacklisted
                orderby s.Code, u.Email
                select new Recipient(u.Id, u.SupplierId, u.Email, u.FullName, s.Name))
            .ToListAsync(cancellationToken);
    }

    private sealed record Recipient(Guid Id, Guid SupplierId, string Email, string FullName, string SupplierName);

    /// <inheritdoc />
    public async Task<EmailDispatchResult> DispatchAsync(CancellationToken cancellationToken)
    {
        var o = _emailOptions.CurrentValue;
        if (!o.Enabled) return new(0, 0, 0);
        var now = _clock.UtcNow;
        // Chạy ngoài request: bỏ bộ lọc công ty, chỉ để gửi đúng địa chỉ đã ghi trên thư.
        var batch = await _db.SupplierPortalEmails.IgnoreQueryFilters()
            .Where(x => x.State == NotificationEmailState.Pending)
            .OrderBy(x => x.CreatedAt)
            .Take(Math.Max(1, o.BatchSize))
            .ToListAsync(cancellationToken);
        if (batch.Count == 0) return new(0, 0, 0);

        var userIds = batch.Select(x => x.PortalUserId).Distinct().ToList();
        var users = await (
                from u in _db.SupplierPortalUsers.AsNoTracking()
                join s in _db.Suppliers.AsNoTracking().IgnoreQueryFilters() on u.SupplierId equals s.Id
                where userIds.Contains(u.Id)
                select new { u.Id, u.Status, SupplierStatus = s.Status })
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        int sent = 0, skipped = 0, failed = 0;
        foreach (var mail in batch)
        {
            var user = users.GetValueOrDefault(mail.PortalUserId);
            // Kiểm lại lúc gửi: tài khoản có thể bị khóa, NCC vào danh sách đen sau lúc soạn. Thư quá cũ (email vừa bật lại, máy chủ thư hỏng lâu)
            // thì thôi gửi – thư mời / gia hạn muộn dễ gây nhầm hạn nộp; NCC vẫn thấy mọi thứ trên Portal.
            if (mail.CreatedAt < now.AddHours(-Math.Max(1, o.MaxAgeHours))) mail.Skip(now, $"Thư quá {o.MaxAgeHours} giờ khi đến lượt gửi.");
            else if (user is null || user.Status != SupplierPortalUserStatus.Active) mail.Skip(now, "Tài khoản Portal không còn đang dùng.");
            else if (user.SupplierStatus == SupplierStatus.Blacklisted) mail.Skip(now, "NCC đang trong danh sách đen.");
            else
            {
                try
                {
                    await _sender.SendAsync(new EmailMessage(mail.ToAddress, mail.ToName, mail.Subject, mail.HtmlBody, mail.TextBody), cancellationToken);
                    mail.MarkSent(_clock.UtcNow);
                    sent++;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Không ghi nội dung thư vào nhật ký – chỉ Id thư và lỗi.
                    _logger.LogWarning(ex, "Gửi thư Portal {EmailId} lỗi (lần {Attempt}).", mail.Id, mail.Attempts + 1);
                    mail.MarkFailed(_clock.UtcNow, ex.Message, Math.Max(1, o.MaxAttempts));
                    failed++;
                }
            }

            if (mail.State == NotificationEmailState.Skipped) skipped++;
            // Lưu từng thư: dừng giữa chừng thì thư đã gửi không gửi lại.
            await _db.SaveChangesAsync(cancellationToken);
        }

        return new(sent, skipped, failed);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SupplierPortalEmailDto>> ListForRfqAsync(Guid rfqId, CancellationToken cancellationToken)
    {
        // RFQ đi qua bộ lọc công ty đang làm việc; thư cũng có CompanyId nên tự lọc.
        if (!await _db.Rfqs.AsNoTracking().AnyAsync(r => r.Id == rfqId, cancellationToken)) throw new NotFoundException("Không tìm thấy RFQ.");
        return await ListAsync(RfqDocType, rfqId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SupplierPortalEmailDto>> ListForPurchaseOrderAsync(Guid orderId, CancellationToken cancellationToken)
    {
        if (!await _db.PurchaseOrders.AsNoTracking().AnyAsync(o => o.Id == orderId, cancellationToken)) throw new NotFoundException("Không tìm thấy PO.");
        return await ListAsync(PurchaseOrderDocType, orderId, cancellationToken);
    }

    private async Task<IReadOnlyList<SupplierPortalEmailDto>> ListAsync(string docType, Guid docId, CancellationToken cancellationToken)
    {
        var rows = await (
                from m in _db.SupplierPortalEmails.AsNoTracking()
                join s in _db.Suppliers.AsNoTracking().IgnoreQueryFilters() on m.SupplierId equals s.Id
                where m.DocType == docType && m.DocId == docId
                orderby m.CreatedAt descending, s.Code, m.ToAddress
                select new { m, s.Code, s.Name })
            .ToListAsync(cancellationToken);
        return rows.Select(x => new SupplierPortalEmailDto(
                x.m.Id, x.m.SupplierId, x.Code, x.Name, x.m.ToAddress, x.m.ToName, x.m.Kind.ToString(), x.m.Subject, x.m.CreatedAt,
                x.m.State.ToString(), x.m.Attempts, x.m.SentAt, x.m.Note))
            .ToList();
    }

    /// <summary>
    /// Tiêu đề và các đoạn của thư theo loại. Hạn nộp ghi giờ + ngày giờ Việt Nam (P3-1), ví dụ "17:00 ngày 20/11/2026".
    /// </summary>
    internal static (string Subject, string Title, IReadOnlyList<string> Lines) Content(
        Rfq rfq, SupplierPortalEmailKind kind, string companyName, string supplierName, string? detail)
    {
        var deadline = $"{rfq.DeadlineTime:HH\\:mm} ngày {rfq.Deadline:dd/MM/yyyy} (giờ Việt Nam)";
        var subjectRfq = $"{rfq.No} – {rfq.Title}";
        var extra = string.IsNullOrWhiteSpace(detail) ? null : detail.Trim();
        return kind switch
        {
            SupplierPortalEmailKind.RfqInvitation => ($"Thư mời báo giá {subjectRfq}", $"Thư mời báo giá {rfq.No}",
            [
                $"{companyName} trân trọng mời {supplierName} báo giá: {rfq.Title}.",
                $"Hạn nộp báo giá: {deadline}.",
                "Quý công ty đăng nhập Portal nhà cung cấp để xem dòng hàng, gửi câu hỏi làm rõ và nộp báo giá. Báo giá nộp trên Portal được niêm phong đến sau giờ hạn nộp.",
            ]),
            SupplierPortalEmailKind.RfqDeadlineExtended => ($"Gia hạn nộp báo giá {subjectRfq}", $"Gia hạn nộp báo giá {rfq.No}",
            [
                $"{companyName} gia hạn nộp báo giá {rfq.No} – {rfq.Title}.",
                $"Hạn nộp mới: {deadline}.",
                "Báo giá đã nộp vẫn giữ nguyên; quý công ty có thể nộp lại trên Portal đến hạn mới (bản nộp cuối có hiệu lực).",
            ]),
            SupplierPortalEmailKind.RfqCancelled => ($"Hủy thư mời báo giá {subjectRfq}", $"Hủy thư mời báo giá {rfq.No}",
            [
                $"{companyName} đã hủy thư mời báo giá {rfq.No} – {rfq.Title}.",
                "Quý công ty không cần nộp báo giá cho thư mời này. Cảm ơn quý công ty đã quan tâm.",
            ]),
            SupplierPortalEmailKind.RfqAnswer => ($"Trả lời câu hỏi – {subjectRfq}", $"Trả lời câu hỏi về thư mời báo giá {rfq.No}",
            [
                $"{companyName} đã trả lời câu hỏi của quý công ty về thư mời báo giá {rfq.No} – {rfq.Title}. Câu trả lời chỉ gửi riêng quý công ty.",
                .. extra is null ? Array.Empty<string>() : [extra],
                $"Hạn nộp báo giá: {deadline}.",
            ]),
            SupplierPortalEmailKind.RfqNotice => ($"Thông báo làm rõ – {subjectRfq}", $"Thông báo làm rõ thư mời báo giá {rfq.No}",
            [
                $"{companyName} thông báo làm rõ thư mời báo giá {rfq.No} – {rfq.Title}:",
                .. extra is null ? Array.Empty<string>() : [extra],
                $"Hạn nộp báo giá: {deadline}.",
            ]),
            // Kết quả chọn thầu (lát P4b – P4-8): không giá, không nêu NCC trúng, không lý do chọn; chi tiết dòng trúng NCC xem trên Portal.
            SupplierPortalEmailKind.RfqAwarded => ($"Thông báo trúng thầu – {subjectRfq}", $"Thông báo kết quả chọn thầu {rfq.No}",
            [
                $"{companyName} trân trọng thông báo {supplierName} được chọn cung cấp theo thư mời báo giá {rfq.No} – {rfq.Title}.",
                .. extra is null ? Array.Empty<string>() : [extra],
                "Quý công ty đăng nhập Portal nhà cung cấp để xem các dòng trúng thầu. Đơn đặt hàng hoặc hợp đồng sẽ được gửi sau.",
            ]),
            SupplierPortalEmailKind.RfqNotAwarded => ($"Thông báo kết quả – {subjectRfq}", $"Thông báo kết quả chọn thầu {rfq.No}",
            [
                $"{companyName} cảm ơn {supplierName} đã gửi báo giá theo thư mời {rfq.No} – {rfq.Title}.",
                "Lần này báo giá của quý công ty chưa được chọn. Rất mong tiếp tục nhận được báo giá của quý công ty trong các lần mời sau.",
            ]),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
    }

    /// <summary>
    /// Tiêu đề và các đoạn thư về PO (lát P4a – P4-5). Thư **không có giá** (NCC xem đủ dòng, giá trên Portal sau khi đăng nhập); thư hủy không ghi
    /// lý do hủy (lý do nội bộ). Thư mở lại ghi lý do – lý do này Phòng CUVT viết để gửi NCC.
    /// </summary>
    internal static (string Subject, string Title, IReadOnlyList<string> Lines) OrderContent(
        Proc.Domain.Purchasing.PurchaseOrder order, SupplierPortalEmailKind kind, string companyName, string supplierName, string? detail)
    {
        var expected = order.ExpectedDate is { } d ? $"Ngày giao dự kiến: {d:dd/MM/yyyy}." : null;
        var extra = string.IsNullOrWhiteSpace(detail) ? null : detail.Trim();
        return kind switch
        {
            SupplierPortalEmailKind.PurchaseOrderIssued => ($"Đơn đặt hàng {order.No}", $"Đơn đặt hàng {order.No}",
            [
                $"{companyName} gửi {supplierName} đơn đặt hàng {order.No} ngày {order.Date:dd/MM/yyyy}.",
                .. expected is null ? Array.Empty<string>() : [expected],
                "Quý công ty đăng nhập Portal nhà cung cấp để xem dòng hàng, giá và xác nhận (kèm ngày giao cam kết) hoặc từ chối đơn.",
            ]),
            SupplierPortalEmailKind.PurchaseOrderCancelled => ($"Hủy đơn đặt hàng {order.No}", $"Hủy đơn đặt hàng {order.No}",
            [
                $"{companyName} đã hủy đơn đặt hàng {order.No} ngày {order.Date:dd/MM/yyyy}.",
                "Quý công ty không giao hàng theo đơn này. Cần trao đổi thêm xin liên hệ Phòng Cung ứng vật tư.",
            ]),
            SupplierPortalEmailKind.PurchaseOrderReopened => ($"Đề nghị phản hồi lại đơn đặt hàng {order.No}", $"Phản hồi lại đơn đặt hàng {order.No}",
            [
                $"{companyName} đề nghị quý công ty xác nhận hoặc từ chối lại đơn đặt hàng {order.No} ngày {order.Date:dd/MM/yyyy}.",
                .. extra is null ? Array.Empty<string>() : [$"Lý do: {extra}"],
                "Quý công ty đăng nhập Portal nhà cung cấp để phản hồi lại đơn.",
            ]),
            SupplierPortalEmailKind.PaymentPaid => ($"Thanh toán đơn đặt hàng {order.No}", $"Thanh toán đơn đặt hàng {order.No}",
            [
                $"{companyName} đã thanh toán cho {supplierName} theo đơn đặt hàng {order.No} ngày {order.Date:dd/MM/yyyy}.",
                .. extra is null ? Array.Empty<string>() : extra.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries),
                "Quý công ty đăng nhập Portal nhà cung cấp, mục Thanh toán, để xem hóa đơn, giảm trừ và số tiền còn phải trả của đơn.",
            ]),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
    }

    /// <summary>Link mở trang trên Portal (ví dụ "bao-gia/{id}"); null khi chưa cấu hình địa chỉ (Portal:BaseUrl, rồi Email:AppBaseUrl).</summary>
    private string? PortalLink(string path)
    {
        var baseUrl = new[] { _options.BaseUrl, _emailOptions.CurrentValue.AppBaseUrl }.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
        return baseUrl is null ? null : $"{baseUrl.TrimEnd('/')}/ncc/{path}";
    }

    /// <summary>Thân thư HTML kiểu đơn giản như email thông báo nội bộ; mọi nội dung được mã hóa HTML, xuống dòng giữ nguyên.</summary>
    private static string Html(string toName, string title, IReadOnlyList<string> lines, string? link)
    {
        static string E(string s) => WebUtility.HtmlEncode(s).Replace("\n", "<br>", StringComparison.Ordinal);
        var body = new StringBuilder();
        body.Append("<div style=\"font-family:Tahoma,Arial,sans-serif;font-size:13px;color:#1b2a3a\">")
            .Append("<div style=\"background:#d6e1ee;padding:6px 10px;font-weight:bold;color:#1f3d6b\">").Append(E(title)).Append("</div>")
            .Append("<div style=\"padding:8px 10px;border:1px solid #b8c5d6;border-top:0\">")
            .Append("<p>Kính gửi ").Append(E(toName)).Append(",</p>");
        foreach (var line in lines) body.Append("<p>").Append(E(line)).Append("</p>");
        if (link is not null)
            body.Append("<p><a href=\"").Append(WebUtility.HtmlEncode(link))
                .Append("\" style=\"display:inline-block;padding:4px 12px;background:#f3efdf;border:1px solid #b5a97f;color:#1b2a3a;text-decoration:none\">")
                .Append("Mở trên Portal nhà cung cấp</a></p>");
        body.Append("<p style=\"color:#888;font-size:11px\">Thư gửi tự động từ Portal nhà cung cấp – không trả lời thư này.</p></div></div>");
        return body.ToString();
    }

    private static string Text(string toName, string title, IReadOnlyList<string> lines, string? link) =>
        $"{title}\n\nKính gửi {toName},\n\n{string.Join("\n\n", lines)}\n\n"
        + (link is null ? string.Empty : $"Mở trên Portal nhà cung cấp: {link}\n\n")
        + "Thư gửi tự động từ Portal nhà cung cấp – không trả lời thư này.\n";
}
