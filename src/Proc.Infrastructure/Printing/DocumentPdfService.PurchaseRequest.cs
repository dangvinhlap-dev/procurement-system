using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Proc.Application.Common;
using Proc.Application.Documents;
using Proc.Application.Printing;
using Proc.Application.Purchasing;
using Proc.Domain.Documents;
using Proc.Domain.Purchasing;
using Proc.Infrastructure.Purchasing;

namespace Proc.Infrastructure.Printing;

/// <summary>
/// In đề nghị mua giữ nguyên sheet 1A (lát IN1 – design.md mục 20.4): chữ ký của người đã duyệt (phiên bản có hiệu lực lúc duyệt), logo công ty
/// của phiếu, góc dưới phải ghi giờ in, người in, lần in, tình trạng phê duyệt; In / Tải PDF ghi nhật ký in, Xem thì không.
/// </summary>
public sealed partial class DocumentPdfService
{
    /// <inheritdoc />
    public Task<PdfFile> PurchaseRequestAsync(Guid id, CancellationToken cancellationToken) => RenderPurchaseRequestAsync(id, null, cancellationToken);

    /// <inheritdoc />
    public Task<PdfFile> PurchaseRequestPrintAsync(Guid id, PrintKind kind, CancellationToken cancellationToken) =>
        RenderPurchaseRequestAsync(id, kind, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<DocumentPrintDto>> PurchaseRequestPrintsAsync(Guid id, CancellationToken cancellationToken)
    {
        // Đọc phiếu qua dịch vụ xem để giữ phạm vi (phiếu công ty khác / ngoài phạm vi → 404).
        await _requests.GetAsync(id, cancellationToken);
        var logs = await _db.DocumentPrintLogs.AsNoTracking()
            .Where(l => l.DocType == DocumentTypes.PurchaseRequest && l.DocumentId == id).OrderByDescending(l => l.PrintNo)
            .ToListAsync(cancellationToken);
        var userIds = logs.Select(l => l.PrintedBy).Distinct().ToList();
        var users = await _db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => new { u.FullName, u.UserName }, cancellationToken);
        return logs.Select(l => new DocumentPrintDto(
            l.PrintNo, l.Kind.ToString(), l.PrintedAt, users.GetValueOrDefault(l.PrintedBy)?.FullName, users.GetValueOrDefault(l.PrintedBy)?.UserName,
            l.ApprovalStatus)).ToList();
    }

    /// <summary>Dựng bản in; <paramref name="kind"/> null = bản xem (không ghi nhật ký in).</summary>
    private async Task<PdfFile> RenderPurchaseRequestAsync(Guid id, PrintKind? kind, CancellationToken cancellationToken)
    {
        var pr = await _requests.GetAsync(id, cancellationToken);
        var itemIds = pr.Lines.Select(l => l.ItemId).Distinct().ToList();
        var specifications = await _db.Items.AsNoTracking().Where(i => itemIds.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, i => i.Specification, cancellationToken);
        var company = await CompanyAsync(cancellationToken);
        var monthSpent = pr.DepartmentId is { } department && pr.Category != nameof(PurchaseCategory.Construction)
            ? await DepartmentSpending.MonthAsync(_db, department, pr.Date, cancellationToken)
            : 0;
        var form = CuvtPrintForms.PurchaseRequest(pr, company.Name, specifications, monthSpent);
        var sign = await SignBlockAsync(pr, cancellationToken);
        var companyId = _scope.RequireCompany();
        var logo = await _db.CompanyLogos.AsNoTracking().Where(l => l.CompanyId == companyId)
            .Select(l => new SheetImage(l.Image, l.Width, l.Height)).SingleOrDefaultAsync(cancellationToken);

        var now = _clock.UtcNow;
        var me = _scope.UserId ?? throw new ForbiddenAccessException("Chưa đăng nhập.");
        var printer = await _db.Users.AsNoTracking().Where(u => u.Id == me).Select(u => new { u.UserName, u.FullName }).SingleAsync(cancellationToken);
        var printNo = kind is { } k
            ? await LogPrintAsync(companyId, id, k, me, now, sign.ApprovalStatus, cancellationToken)
            : await _db.DocumentPrintLogs.CountAsync(l => l.DocType == DocumentTypes.PurchaseRequest && l.DocumentId == id, cancellationToken);
        var at = TimeZoneInfo.ConvertTime(now, VietnamTime.Zone).ToString("HH:mm dd/MM/yyyy", CultureInfo.InvariantCulture);
        // Góc dưới phải mọi trang, chữ 9 (người dùng yêu cầu 18/11/2026): giờ phút in, người in, số lần in, tình trạng phê duyệt; kèm số phiếu
        // và số trang vì thân phiếu giữ nguyên mẫu (mẫu không có ô số phiếu).
        string[] lines = kind is null
            ?
            [
                $"Bản xem lúc: {at} (chưa in)",
                $"Người xem: {printer.UserName} – {printer.FullName}",
                $"Số phiếu: {pr.No} · Đã in {printNo} lần · Trang {{PAGE}}/{{PAGES}}",
                $"Tình trạng phê duyệt: {sign.ApprovalStatus}",
            ]
            :
            [
                $"In lúc: {at}",
                $"Người in: {printer.UserName} – {printer.FullName}",
                $"Số phiếu: {pr.No} · Lần in thứ {printNo} · Trang {{PAGE}}/{{PAGES}}",
                $"Tình trạng phê duyệt: {sign.ApprovalStatus}",
            ];
        var watermark = pr.Status switch
        {
            "Draft" => "BẢN NHÁP",
            "Cancelled" => "ĐÃ HỦY",
            "Submitted" or "Rejected" => "CHƯA DUYỆT",
            _ => null,
        };
        var pdf = PurchaseRequestSheetLayout.Render(form, logo, sign, new SheetStamp(lines, watermark));
        return new PdfFile($"{pr.No}.pdf", pdf);
    }

    /// <summary>
    /// Ghi một lần in; số lần in = lớn nhất hiện có + 1. Hai người bấm In cùng lúc đụng chỉ mục duy nhất (phiếu, lần in) thì thử lại với số kế tiếp.
    /// </summary>
    private async Task<int> LogPrintAsync(
        Guid companyId, Guid id, PrintKind kind, Guid userId, DateTimeOffset at, string status, CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            var last = await _db.DocumentPrintLogs.Where(l => l.DocType == DocumentTypes.PurchaseRequest && l.DocumentId == id)
                .MaxAsync(l => (int?)l.PrintNo, cancellationToken) ?? 0;
            var log = DocumentPrintLog.Create(companyId, DocumentTypes.PurchaseRequest, id, last + 1, kind, userId, at, status);
            _db.DocumentPrintLogs.Add(log);
            try
            {
                await _db.SaveChangesAsync(cancellationToken);
                return log.PrintNo;
            }
            catch (DbUpdateException) when (attempt < 3)
            {
                _db.Entry(log).State = EntityState.Detached;
            }
        }
    }

    /// <summary>
    /// Bước duyệt của lượt gần nhất + chữ ký có hiệu lực lúc duyệt (người dùng chốt 18/11/2026: thay chữ ký mới thì phiếu cũ vẫn in chữ ký cũ).
    /// Người đề xuất: chữ ký của chính người đề xuất lúc gửi duyệt; người khác gửi thay thì không in chữ ký, ghi "Gửi duyệt bởi …".
    /// </summary>
    private async Task<SheetSignBlock> SignBlockAsync(PurchaseRequestDto pr, CancellationToken cancellationToken)
    {
        var doc = await _db.PurchaseRequests.AsNoTracking().Where(r => r.Id == pr.Id)
            .Select(r => new { r.RequesterId, r.CreatedBy, r.ApprovalRound }).SingleAsync(cancellationToken);
        var steps = await _db.ApprovalSteps.AsNoTracking()
            .Where(s => s.DocType == DocumentTypes.PurchaseRequest && s.DocumentId == pr.Id && s.Round == doc.ApprovalRound
                        && s.Status != ApprovalStepStatus.Skipped)
            .ToListAsync(cancellationToken);
        var roleIds = steps.Select(s => s.RoleId).OfType<Guid>().Distinct().ToList();
        var roles = await _db.Roles.AsNoTracking().Where(r => roleIds.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, r => new { r.Code, r.Name }, cancellationToken);
        var requesterId = doc.RequesterId ?? doc.CreatedBy;
        var submittedAt = steps.Select(s => s.SubmittedAt).FirstOrDefault(t => t is not null);
        var submittedBy = steps.Select(s => s.SubmittedBy).FirstOrDefault(t => t is not null);
        var userIds = steps.Select(s => s.ActedBy).Concat(steps.Select(s => s.OnBehalfOf)).Append(requesterId).Append(submittedBy)
            .OfType<Guid>().Distinct().ToList();
        var users = await _db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => new { u.FullName, u.Honorific }, cancellationToken);
        var signatures = (await _db.UserSignatures.AsNoTracking().Where(s => userIds.Contains(s.UserId)).ToListAsync(cancellationToken))
            .ToLookup(s => s.UserId);

        string? Display(Guid? userId) =>
            userId is { } u && users.TryGetValue(u, out var user)
                ? string.IsNullOrWhiteSpace(user.Honorific) ? user.FullName : $"{user.Honorific.Trim()} {user.FullName}"
                : null;
        SheetImage? SignatureAt(Guid? userId, DateTimeOffset? at) =>
            userId is { } u && at is { } t && signatures[u].FirstOrDefault(s => s.IsValidAt(t)) is { } s
                ? new SheetImage(s.Image, s.Width, s.Height)
                : null;

        var printSteps = steps.Select(s => new SheetApprovalStep(
            s.Stage, s.Level, s.Kind, s.RoleId is { } r ? roles.GetValueOrDefault(r)?.Code : null,
            s.RoleId is { } rn ? roles.GetValueOrDefault(rn)?.Name : null, s.Status.ToString(),
            // Duyệt theo ủy quyền: chữ ký và tên của người bấm duyệt, ghi "(thay …)" như "Kiểm tra phê duyệt".
            s.OnBehalfOf is { } o ? $"{Display(s.ActedBy)} (thay {users.GetValueOrDefault(o)?.FullName})" : Display(s.ActedBy),
            s.ActedAt, s.Status == ApprovalStepStatus.Approved ? SignatureAt(s.ActedBy, s.ActedAt) : null)).ToList();
        var byOther = submittedBy is { } sb && requesterId is { } rq && sb != rq ? users.GetValueOrDefault(sb)?.FullName : null;
        return PurchaseRequestSheet.Build(
            pr.Status, Display(requesterId) ?? pr.RequesterName ?? pr.CreatedByName,
            submittedAt, byOther is null ? SignatureAt(requesterId, submittedAt) : null, byOther, printSteps);
    }
}
