using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Proc.Application.Common;
using Proc.Application.Documents;
using Proc.Application.Printing;
using Proc.Domain.Common;
using Proc.Domain.Documents;
using Proc.Domain.Purchasing;

namespace Proc.Infrastructure.Printing;

/// <summary>
/// Phần dùng chung của bản in giữ nguyên sheet biểu mẫu (lát IN1 đề nghị mua, lát IN2 các phiếu khác – design.md mục 20.4): khối ký có chữ ký theo
/// phiên bản, logo, góc thông tin hệ thống, dấu chìm, nhật ký in (In / Tải PDF ghi một lần in, Xem thì không).
/// </summary>
public sealed partial class DocumentPdfService
{
    /// <summary>Khối ký 4A/4B nguyên văn ảnh trong sheet "4A.Theo dõi MH" / "4B.Theo dõi MH" (5 ô cùng hàng, không có dòng chú thích).</summary>
    public static readonly SheetSignSpec Sign4 = new(
        SheetSign.Heading,
        new("", "Người lập bảng", ""),
        [
            new("TPCUVT", "Trưởng phòng CUVT", ""),
            new("TPTCKT", "Trưởng phòng TC-NQ- KT", ""),
            new("GDTCKT", "GĐ Phòng TC-NQ-KT", ""),
        ],
        new("", "Chủ tịch HĐQT/TGĐ", ""));

    /// <inheritdoc />
    public Task<PdfFile> DocumentPrintAsync(string docType, Guid id, PrintKind kind, CancellationToken cancellationToken) => docType switch
    {
        DocumentTypes.PurchaseRequest => RenderPurchaseRequestAsync(id, kind, cancellationToken),
        DocumentTypes.PurchaseOrder => RenderPurchaseOrderSheetAsync(id, kind, cancellationToken),
        _ => throw new BusinessRuleException($"Phiếu loại {docType} chưa in theo biểu mẫu."),
    };

    /// <inheritdoc />
    public async Task<IReadOnlyList<DocumentPrintDto>> DocumentPrintsAsync(string docType, Guid id, CancellationToken cancellationToken)
    {
        // Đọc phiếu qua dịch vụ xem để giữ phạm vi (phiếu công ty khác / ngoài phạm vi → 404).
        switch (docType)
        {
            case DocumentTypes.PurchaseRequest:
                await _requests.GetAsync(id, cancellationToken);
                break;
            case DocumentTypes.PurchaseOrder:
                await _orders.GetAsync(id, cancellationToken);
                break;
            default:
                throw new BusinessRuleException($"Phiếu loại {docType} chưa in theo biểu mẫu.");
        }

        var logs = await _db.DocumentPrintLogs.AsNoTracking()
            .Where(l => l.DocType == docType && l.DocumentId == id).OrderByDescending(l => l.PrintNo)
            .ToListAsync(cancellationToken);
        var userIds = logs.Select(l => l.PrintedBy).Distinct().ToList();
        var users = await _db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => new { u.FullName, u.UserName }, cancellationToken);
        return logs.Select(l => new DocumentPrintDto(
            l.PrintNo, l.Kind.ToString(), l.PrintedAt, users.GetValueOrDefault(l.PrintedBy)?.FullName, users.GetValueOrDefault(l.PrintedBy)?.UserName,
            l.ApprovalStatus)).ToList();
    }

    /// <summary>Bảng theo dõi mua hàng 4A/4B giữ nguyên sheet; <paramref name="kind"/> null = bản xem.</summary>
    private async Task<PdfFile> RenderPurchaseOrderSheetAsync(Guid id, PrintKind? kind, CancellationToken cancellationToken)
    {
        var form = await PurchaseOrderFormAsync(id, cancellationToken);
        var po = await _db.PurchaseOrders.AsNoTracking().Where(o => o.Id == id)
            .Select(o => new { o.CreatedBy, o.Status }).SingleAsync(cancellationToken);
        var status = po.Status.ToString();
        var firstName = form.Signers.FirstOrDefault()?.Name;
        var sign = await SignBlockAsync(Sign4, DocumentTypes.PurchaseOrder, id, status, po.CreatedBy, firstName, cancellationToken);
        var (logo, stamp) = await DecorAsync(DocumentTypes.PurchaseOrder, id, form.No, status, kind, sign.ApprovalStatus, cancellationToken);
        var spec = form.FormNo == "4A" ? CuvtSheetLayout.Sheet4A : CuvtSheetLayout.Sheet4B;
        return new PdfFile($"{form.No}.pdf", CuvtSheetLayout.Render(form, spec, logo, sign, stamp));
    }

    /// <summary>
    /// Logo công ty của phiếu và góc thông tin hệ thống chữ 9 (người dùng yêu cầu 18/11/2026): giờ in, người in, số phiếu, lần in, trang, tình trạng
    /// phê duyệt; In / Tải PDF ghi một lần in trước khi dựng để bản in mang đúng "Lần in thứ n".
    /// </summary>
    /// <param name="docType">Loại chứng từ.</param>
    /// <param name="id">Phiếu.</param>
    /// <param name="no">Số phiếu.</param>
    /// <param name="status">Trạng thái phiếu (dấu chìm).</param>
    /// <param name="kind">In / tải; null = bản xem.</param>
    /// <param name="approvalStatus">Tình trạng phê duyệt dạng chữ.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    private async Task<(SheetImage? Logo, SheetStamp Stamp)> DecorAsync(
        string docType, Guid id, string no, string status, PrintKind? kind, string approvalStatus, CancellationToken cancellationToken)
    {
        var companyId = _scope.RequireCompany();
        var logo = await _db.CompanyLogos.AsNoTracking().Where(l => l.CompanyId == companyId)
            .Select(l => new SheetImage(l.Image, l.Width, l.Height)).SingleOrDefaultAsync(cancellationToken);
        var now = _clock.UtcNow;
        var me = _scope.UserId ?? throw new ForbiddenAccessException("Chưa đăng nhập.");
        var printer = await _db.Users.AsNoTracking().Where(u => u.Id == me).Select(u => new { u.UserName, u.FullName }).SingleAsync(cancellationToken);
        var printNo = kind is { } k
            ? await LogPrintAsync(companyId, docType, id, k, me, now, approvalStatus, cancellationToken)
            : await _db.DocumentPrintLogs.CountAsync(l => l.DocType == docType && l.DocumentId == id, cancellationToken);
        var at = TimeZoneInfo.ConvertTime(now, VietnamTime.Zone).ToString("HH:mm dd/MM/yyyy", CultureInfo.InvariantCulture);
        // Số phiếu ở góc này vì thân phiếu giữ nguyên mẫu (mẫu không có ô số phiếu).
        string[] lines = kind is null
            ?
            [
                $"Bản xem lúc: {at} (chưa in)",
                $"Người xem: {printer.UserName} – {printer.FullName}",
                $"Số phiếu: {no} · Đã in {printNo} lần · Trang {{PAGE}}/{{PAGES}}",
                $"Tình trạng phê duyệt: {approvalStatus}",
            ]
            :
            [
                $"In lúc: {at}",
                $"Người in: {printer.UserName} – {printer.FullName}",
                $"Số phiếu: {no} · Lần in thứ {printNo} · Trang {{PAGE}}/{{PAGES}}",
                $"Tình trạng phê duyệt: {approvalStatus}",
            ];
        var watermark = status switch
        {
            "Draft" => "BẢN NHÁP",
            "Cancelled" => "ĐÃ HỦY",
            "Submitted" or "Rejected" => "CHƯA DUYỆT",
            _ => null,
        };
        return (logo, new SheetStamp(lines, watermark));
    }

    /// <summary>
    /// Ghi một lần in; số lần in = lớn nhất hiện có + 1. Hai người bấm In cùng lúc đụng chỉ mục duy nhất (phiếu, lần in) thì thử lại với số kế tiếp.
    /// </summary>
    private async Task<int> LogPrintAsync(
        Guid companyId, string docType, Guid id, PrintKind kind, Guid userId, DateTimeOffset at, string status, CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            var last = await _db.DocumentPrintLogs.Where(l => l.DocType == docType && l.DocumentId == id)
                .MaxAsync(l => (int?)l.PrintNo, cancellationToken) ?? 0;
            var log = DocumentPrintLog.Create(companyId, docType, id, last + 1, kind, userId, at, status);
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
    /// Bước duyệt của lượt gần nhất + chữ ký có hiệu lực lúc duyệt (người dùng chốt 18/11/2026: thay chữ ký mới thì phiếu cũ vẫn in chữ ký cũ), ghép
    /// vào khối ký của sheet. Người lập: chữ ký của chính người lập lúc gửi duyệt; người khác gửi thay thì không in chữ ký, ghi "Gửi duyệt bởi …".
    /// </summary>
    /// <param name="spec">Khối ký của sheet.</param>
    /// <param name="docType">Loại chứng từ của bước duyệt.</param>
    /// <param name="id">Phiếu.</param>
    /// <param name="status">Trạng thái phiếu.</param>
    /// <param name="firstUserId">Người lập / đề xuất (ô đầu).</param>
    /// <param name="firstFallbackName">Tên ô đầu khi không đọc được tài khoản.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    private async Task<SheetSignBlock> SignBlockAsync(
        SheetSignSpec spec, string docType, Guid id, string status, Guid? firstUserId, string? firstFallbackName, CancellationToken cancellationToken)
    {
        var all = await _db.ApprovalSteps.AsNoTracking().Where(s => s.DocType == docType && s.DocumentId == id).ToListAsync(cancellationToken);
        var round = all.Count == 0 ? 0 : all.Max(s => s.Round);
        var steps = all.Where(s => s.Round == round && s.Status != ApprovalStepStatus.Skipped).ToList();
        var roleIds = steps.Select(s => s.RoleId).OfType<Guid>().Distinct().ToList();
        var roles = await _db.Roles.AsNoTracking().Where(r => roleIds.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, r => new { r.Code, r.Name }, cancellationToken);
        var submittedAt = steps.Select(s => s.SubmittedAt).FirstOrDefault(t => t is not null);
        var submittedBy = steps.Select(s => s.SubmittedBy).FirstOrDefault(t => t is not null);
        var userIds = steps.Select(s => s.ActedBy).Concat(steps.Select(s => s.OnBehalfOf)).Append(firstUserId).Append(submittedBy)
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
        var byOther = submittedBy is { } sb && firstUserId is { } fu && sb != fu ? users.GetValueOrDefault(sb)?.FullName : null;
        return SheetSign.Build(
            spec, status, Display(firstUserId) ?? firstFallbackName,
            submittedAt, byOther is null ? SignatureAt(firstUserId, submittedAt) : null, byOther, printSteps);
    }
}
