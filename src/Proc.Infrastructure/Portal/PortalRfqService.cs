using Microsoft.EntityFrameworkCore;
using Proc.Application.Common;
using Proc.Application.Documents;
using Proc.Application.Portal;
using Proc.Domain.Common;
using Proc.Domain.Documents;
using Proc.Domain.MasterData;
using Proc.Domain.Notifications;
using Proc.Domain.Organization;
using Proc.Domain.Sourcing;
using Proc.Infrastructure.Documents;
using Proc.Infrastructure.Notifications;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.Portal;

/// <summary>
/// Thư mời và báo giá online trên Portal (lát P3a – design.md mục 19.5). Phiên Portal không có công ty / người dùng nội bộ nên bộ lọc công ty
/// và nhóm hàng (vốn theo người dùng nội bộ) không dùng được: mọi truy vấn ở đây bỏ bộ lọc toàn cục và **tự lọc theo NCC của phiên** – chỉ
/// RFQ đã gửi có mời NCC này, chỉ bản nộp của NCC này (quy tắc P1b-4). Không trả giá, tên, số lượng NCC khác (Q15).
/// </summary>
public sealed class PortalRfqService : IPortalRfqService
{
    private readonly ProcDbContext _db;
    private readonly IClock _clock;
    private readonly AttachmentService _attachments;
    private readonly ICurrentUserScope _scope;

    /// <summary>Tạo dịch vụ.</summary>
    /// <param name="db">DbContext.</param>
    /// <param name="clock">Đồng hồ.</param>
    /// <param name="attachments">Kho file đính kèm (file báo giá Portal – lát P3b-1).</param>
    /// <param name="scope">Phạm vi người dùng nội bộ – phiên Portal không có người dùng nội bộ; chỉ dùng cho bộ sinh thông báo (lát P3b-2).</param>
    public PortalRfqService(ProcDbContext db, IClock clock, AttachmentService attachments, ICurrentUserScope scope)
    {
        _db = db;
        _clock = clock;
        _attachments = attachments;
        _scope = scope;
    }

    /// <inheritdoc />
    public async Task<PortalRfqDto> AskAsync(PortalSession session, Guid id, PortalAskRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var rfq = await LoadAsync(session.SupplierId, id, cancellationToken);
        var question = RfqClarification.Ask(
            rfq, session.SupplierId, session.UserId, session.Email, request.Question, _clock.UtcNow, _clock.NowInVietnam());
        _db.RfqClarifications.Add(question);

        // Báo người lập RFQ (người trả lời chính); RFQ không rõ người lập thì báo mọi người có quyền lập RFQ tại công ty mời.
        var publisher = new NotificationPublisher(_db, _scope, _clock);
        List<Guid> receivers = rfq.CreatedBy is { } creator && await _db.Users.AnyAsync(u => u.Id == creator && u.IsActive, cancellationToken)
            ? [creator]
            : await publisher.UsersWithPermissionAsync(rfq.CompanyId, Permissions.Sourcing.RfqManage, null, null, cancellationToken);
        var supplier = await _db.Suppliers.AsNoTracking().IgnoreQueryFilters().Where(s => s.Id == session.SupplierId)
            .Select(s => s.Name).SingleOrDefaultAsync(cancellationToken);
        publisher.Add(rfq.CompanyId, receivers, NotificationTypes.PortalQuestion, $"Câu hỏi làm rõ RFQ {rfq.No}",
            $"{supplier}: {question.Question}", "RFQ", rfq.Id);
        await _db.SaveChangesAsync(cancellationToken);
        return await ToDtoAsync(session.SupplierId, rfq, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AttachmentDto>> ListAttachmentsAsync(PortalSession session, Guid id, CancellationToken cancellationToken)
    {
        var (rfq, invitationId) = await InvitationAsync(session.SupplierId, id, cancellationToken);
        return await _attachments.ListForPortalAsync(AttachmentOwnerTypes.RfqPortalQuote, invitationId,
            IsOpen(rfq) ? null : "Đã hết hạn nộp hoặc bên mời đã mở báo giá – file đã khóa.", cancellationToken);
    }

    /// <inheritdoc />
    public async Task<AttachmentDto> UploadAttachmentAsync(
        PortalSession session, Guid id, string fileName, long sizeBytes, Stream content, string? note, CancellationToken cancellationToken)
    {
        var (rfq, invitationId) = await InvitationAsync(session.SupplierId, id, cancellationToken);
        RequireOpen(rfq);
        return await _attachments.UploadForPortalAsync(AttachmentOwnerTypes.RfqPortalQuote, invitationId, rfq.CompanyId, session.UserId, fileName, sizeBytes, content, note, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<AttachmentContent> OpenAttachmentAsync(PortalSession session, Guid id, Guid attachmentId, CancellationToken cancellationToken)
    {
        var (_, invitationId) = await InvitationAsync(session.SupplierId, id, cancellationToken);
        return await _attachments.OpenForPortalAsync(AttachmentOwnerTypes.RfqPortalQuote, attachmentId, invitationId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteAttachmentAsync(PortalSession session, Guid id, Guid attachmentId, CancellationToken cancellationToken)
    {
        var (rfq, invitationId) = await InvitationAsync(session.SupplierId, id, cancellationToken);
        RequireOpen(rfq);
        await _attachments.DeleteForPortalAsync(AttachmentOwnerTypes.RfqPortalQuote, attachmentId, invitationId, session.UserId, cancellationToken);
    }

    /// <summary>RFQ và dòng NCC được mời của phiên (chủ của file báo giá Portal); RFQ không mời NCC này trả 404.</summary>
    private async Task<(Rfq Rfq, Guid InvitationId)> InvitationAsync(Guid supplierId, Guid id, CancellationToken cancellationToken)
    {
        var rfq = await LoadAsync(supplierId, id, cancellationToken);
        return (rfq, rfq.Suppliers.Single(s => s.SupplierId == supplierId).Id);
    }

    /// <summary>
    /// Còn nhận báo giá: đang nhận, chưa mở, chưa qua giờ hạn – file báo giá thêm, xóa được đến lúc đó như giá (P3-3); sau đó khóa để bản
    /// Phòng CUVT mở đúng bản NCC đã nộp.
    /// </summary>
    private bool IsOpen(Rfq rfq) =>
        rfq.Status == RfqStatus.Sent && rfq.PortalQuotesOpenedAt is null && _clock.NowInVietnam() <= rfq.DeadlineAt;

    private void RequireOpen(Rfq rfq)
    {
        if (!IsOpen(rfq))
            throw new BusinessRuleException($"Đã hết hạn nộp báo giá ({rfq.DeadlineText}) hoặc bên mời đã mở báo giá – không thêm, xóa file được.");
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PortalRfqListItemDto>> ListAsync(PortalSession session, CancellationToken cancellationToken)
    {
        var supplierId = session.SupplierId;
        var rfqs = await InvitedRfqs(supplierId)
            .OrderByDescending(r => r.Date).ThenByDescending(r => r.No)
            .Select(r => new
            {
                r.Id, r.No, r.CompanyId, r.Title, r.Date, r.Deadline, r.DeadlineTime, r.Status, r.PortalQuotesOpenedAt, r.ResultSentAt, LineCount = r.Lines.Count,
            })
            .ToListAsync(cancellationToken);
        var ids = rfqs.Select(r => r.Id).ToList();
        var stats = (await Submissions(supplierId).Where(x => ids.Contains(x.RfqId))
                .Select(x => new { x.RfqId, x.SubmittedAt }).ToListAsync(cancellationToken))
            .GroupBy(x => x.RfqId)
            .ToDictionary(g => g.Key, g => (Count: g.Count(), Last: g.Max(x => x.SubmittedAt)));
        var companies = await CompanyNamesAsync(rfqs.Select(r => r.CompanyId), cancellationToken);
        // Kết quả chọn thầu (lát P4b): chỉ RFQ đã duyệt xong và Phòng CUVT đã bấm gửi kết quả – nạp riêng các RFQ này kèm dòng và báo giá để
        // tính kết quả của chính NCC.
        var awardedIds = rfqs.Where(r => r.Status == RfqStatus.Awarded && r.ResultSentAt != null).Select(r => r.Id).ToList();
        var outcomes = awardedIds.Count == 0
            ? []
            : (await InvitedRfqs(supplierId).Where(r => awardedIds.Contains(r.Id))
                    .Include(r => r.Lines).Include(r => r.Suppliers).ThenInclude(x => x.QuotationLines).AsSplitQuery()
                    .ToListAsync(cancellationToken))
                .ToDictionary(r => r.Id, r => r.ResultFor(supplierId)?.Outcome);
        var now = _clock.NowInVietnam();
        return rfqs.Select(r =>
            {
                var deadline = r.Deadline.ToDateTime(r.DeadlineTime);
                var stat = stats.TryGetValue(r.Id, out var s) ? s : (Count: 0, Last: (DateTimeOffset?)null);
                return new PortalRfqListItemDto(r.Id, r.No, companies.GetValueOrDefault(r.CompanyId) ?? "", r.Title, r.Date, deadline,
                    StatusOf(r.Status, r.PortalQuotesOpenedAt, r.ResultSentAt, deadline, now), r.LineCount, stat.Count, stat.Last)
                {
                    Outcome = outcomes.GetValueOrDefault(r.Id)?.ToString(),
                };
            })
            .ToList();
    }

    /// <inheritdoc />
    public async Task<PortalRfqDto> GetAsync(PortalSession session, Guid id, CancellationToken cancellationToken)
    {
        var rfq = await LoadAsync(session.SupplierId, id, cancellationToken);
        return await ToDtoAsync(session.SupplierId, rfq, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PortalRfqDto> SubmitAsync(PortalSession session, Guid id, PortalSubmitQuoteRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var rfq = await LoadAsync(session.SupplierId, id, cancellationToken);
        var sequence = await Submissions(session.SupplierId).Where(x => x.RfqId == rfq.Id)
            .Select(x => (int?)x.Sequence).MaxAsync(cancellationToken) ?? 0;
        var submission = RfqPortalSubmission.Create(
            rfq, session.SupplierId, sequence + 1, session.UserId, session.Email, _clock.UtcNow, _clock.NowInVietnam(),
            new PortalQuoteHeader(request.ValidUntil, request.PaymentTerms, request.Note),
            (request.Lines ?? []).Select(l => new QuotationLineData(l.RfqLineId, l.UnitPrice, l.DiscountPct, l.VatCode, l.LeadTimeDays, l.Note))
                .ToList());
        _db.RfqPortalSubmissions.Add(submission);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: Npgsql.PostgresErrorCodes.UniqueViolation })
        {
            // Hai người cùng NCC nộp cùng lúc: chỉ mục duy nhất (RFQ, NCC, lần nộp) chặn bản thứ hai thay vì ghi đè lặng lẽ.
            throw new BusinessRuleException("Có người khác của công ty bạn vừa nộp báo giá cho RFQ này – tải lại trang, kiểm tra rồi nộp lại.");
        }

        return await ToDtoAsync(session.SupplierId, rfq, cancellationToken);
    }

    /// <summary>RFQ đã gửi (không còn nháp) có mời NCC – bỏ bộ lọc công ty vì phiên Portal không có công ty; điều kiện NCC thay thế.</summary>
    private IQueryable<Rfq> InvitedRfqs(Guid supplierId) =>
        _db.Rfqs.AsNoTracking().IgnoreQueryFilters()
            .Where(r => r.Status != RfqStatus.Draft && r.Suppliers.Any(s => s.SupplierId == supplierId));

    /// <summary>Bản nộp của chính NCC.</summary>
    private IQueryable<RfqPortalSubmission> Submissions(Guid supplierId) =>
        _db.RfqPortalSubmissions.AsNoTracking().IgnoreQueryFilters().Where(x => x.SupplierId == supplierId);

    /// <summary>Nạp RFQ kèm dòng và NCC được mời; RFQ không mời NCC này trả 404 như RFQ không tồn tại.</summary>
    private async Task<Rfq> LoadAsync(Guid supplierId, Guid id, CancellationToken cancellationToken) =>
        await InvitedRfqs(supplierId).Include(r => r.Lines).Include(r => r.Suppliers).ThenInclude(x => x.QuotationLines).AsSplitQuery()
            .SingleOrDefaultAsync(r => r.Id == id, cancellationToken)
        ?? throw new NotFoundException("Không tìm thấy thư mời báo giá.");

    private async Task<Dictionary<Guid, string>> CompanyNamesAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken)
    {
        var list = ids.Distinct().ToList();
        return await _db.Companies.AsNoTracking().IgnoreQueryFilters().Where(c => list.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);
    }

    private async Task<PortalRfqDto> ToDtoAsync(Guid supplierId, Rfq rfq, CancellationToken cancellationToken)
    {
        var itemIds = rfq.Lines.Select(l => l.ItemId).Distinct().ToList();
        var items = await _db.Items.AsNoTracking().IgnoreQueryFilters().Where(i => itemIds.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, i => new { i.Code, i.Name }, cancellationToken);
        var uomIds = rfq.Lines.Select(l => l.UomId).Distinct().ToList();
        var uoms = await _db.Uoms.AsNoTracking().IgnoreQueryFilters().Where(u => uomIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Code, cancellationToken);
        // Mã hàng của chính NCC giúp NCC nhận ra mặt hàng (lát NCC-SP2).
        var supplierCodes = (await _db.SupplierItems.AsNoTracking().IgnoreQueryFilters()
                .Where(si => si.SupplierId == supplierId && itemIds.Contains(si.ItemId) && si.SupplierItemCode != null)
                .Select(si => new { si.ItemId, si.SupplierItemCode }).ToListAsync(cancellationToken))
            .GroupBy(x => x.ItemId).ToDictionary(g => g.Key, g => g.First().SupplierItemCode);
        var submissions = await Submissions(supplierId).Include(x => x.Lines).AsSplitQuery()
            .Where(x => x.RfqId == rfq.Id).OrderByDescending(x => x.Sequence).ToListAsync(cancellationToken);
        var companyName = (await CompanyNamesAsync([rfq.CompanyId], cancellationToken)).GetValueOrDefault(rfq.CompanyId) ?? "";
        var qty = rfq.Lines.ToDictionary(l => l.Id, l => l.Qty);

        // Thành tiền = SL × đơn giá × (100 − %CK)/100, làm tròn 2 số (như giá trị trúng thầu).
        decimal Amount(RfqPortalSubmissionLine l) =>
            Math.Round(qty[l.RfqLineId] * l.UnitPrice * (100m - l.DiscountPct) / 100m, 2, MidpointRounding.AwayFromZero);

        var lines = rfq.Lines.OrderBy(l => l.LineNo).Select(l => new PortalRfqLineDto(
                l.Id, l.LineNo, items.GetValueOrDefault(l.ItemId)?.Code ?? "", items.GetValueOrDefault(l.ItemId)?.Name ?? "",
                supplierCodes.GetValueOrDefault(l.ItemId), uoms.GetValueOrDefault(l.UomId) ?? "", l.Qty, l.Note))
            .ToList();
        var latest = submissions.FirstOrDefault() is { } last
            ? new PortalLatestQuoteDto(last.Sequence, last.SubmittedAt, last.ValidUntil, last.PaymentTerms, last.Note, last.ContentHash,
                last.Lines.Select(l => new PortalQuoteLineDto(l.RfqLineId, l.UnitPrice, l.DiscountPct, l.VatCode, l.LeadTimeDays, l.Note, Amount(l)))
                    .ToList())
            : null;
        var history = submissions.Select(x => new PortalSubmissionDto(x.Sequence, x.SubmittedAt, x.SubmittedByEmail, x.ContentHash,
            x.Lines.Sum(Amount))).ToList();

        var now = _clock.NowInVietnam();
        var status = StatusOf(rfq.Status, rfq.PortalQuotesOpenedAt, rfq.ResultSentAt, rfq.DeadlineAt, now);
        var closedReason = status switch
        {
            "Open" => null,
            "Closed" => $"Đã hết hạn nộp báo giá ({rfq.DeadlineText}).",
            "Cancelled" => "Bên mời đã hủy yêu cầu báo giá.",
            "Awarded" => "Bên mời đã duyệt kết quả chọn thầu – không nộp lại được.",
            _ => "Bên mời đã mở báo giá – không nộp lại được.",
        };
        // Hỏi đáp: câu hỏi – trả lời của chính NCC (riêng, bảo mật với NCC khác) và thông báo làm rõ công khai (người dùng chốt 12/11/2026).
        var clarifications = await _db.RfqClarifications.AsNoTracking().IgnoreQueryFilters()
            .Where(x => x.RfqId == rfq.Id && (x.SupplierId == supplierId || x.SupplierId == null))
            .OrderBy(x => x.AskedAt ?? x.AnsweredAt).ThenBy(x => x.Id)
            .Select(x => new PortalClarificationDto(x.Id, x.SupplierId == null, x.Question, x.AskedAt, x.AskedByEmail, x.Answer, x.AnsweredAt))
            .ToListAsync(cancellationToken);
        return new PortalRfqDto(rfq.Id, rfq.No, companyName, rfq.Title, rfq.Date, rfq.DeadlineAt, rfq.Note, status, status == "Open", closedReason,
            VatRates.All, lines, latest, history)
        {
            MaxFileSizeMb = _attachments.MaxFileSizeMb, CanAsk = status == "Open", Clarifications = clarifications,
            Result = rfq.ResultSentAt is not null && rfq.ResultFor(supplierId) is { } result
                ? new PortalRfqResultDto(result.Outcome.ToString(), rfq.ResultSentAt, result.QuotedLineCount, result.WonLines.Sum(l => l.Amount),
                    result.WonLines.Select(l => new PortalWonLineDto(l.RfqLineId, l.UnitPrice, l.DiscountPct, l.VatCode, l.Amount)).ToList())
                : null,
        };
    }

    /// <summary>
    /// Trạng thái NCC thấy: Open (đang nhận, trước giờ hạn), Closed (hết giờ, chưa mở), Reviewing (đã mở hoặc đang duyệt kết quả),
    /// Awarded (đã có kết quả – Phòng CUVT đã gửi kết quả), Cancelled (đã hủy). Kết quả duyệt xong nhưng chưa gửi vẫn là Reviewing: Phòng CUVT tự
    /// bấm gửi (lát P4b, người dùng yêu cầu 13/11/2026). Kết quả của chính NCC trả riêng (Outcome / Result), không nói NCC khác trúng.
    /// </summary>
    private static string StatusOf(RfqStatus status, DateTimeOffset? openedAt, DateTimeOffset? resultSentAt, DateTime deadline, DateTime now) => status switch
    {
        RfqStatus.Cancelled => "Cancelled",
        RfqStatus.Awarded when resultSentAt is not null => "Awarded",
        RfqStatus.Awarded => "Reviewing",
        RfqStatus.AwardPending => "Reviewing",
        _ when openedAt is not null => "Reviewing",
        _ when now > deadline => "Closed",
        _ => "Open",
    };
}
