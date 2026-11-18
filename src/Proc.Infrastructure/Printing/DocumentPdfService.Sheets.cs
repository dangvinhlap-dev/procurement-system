using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Proc.Application.Common;
using Proc.Application.Documents;
using Proc.Application.Printing;
using Proc.Domain.Common;
using Proc.Domain.Documents;
using Proc.Domain.Purchasing;
using Proc.Infrastructure.Purchasing;

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

    /// <summary>
    /// Khối ký 2A/2B nguyên văn ảnh trong sheet "2A.TH MUA VT" / "2B.TH MUA VT": Người lập bảng · Trưởng phòng CUVT · GĐ Khối liên quan ·
    /// Phó CT.UBCL&amp;PT · Chủ tịch HĐQT/TGĐ (chuỗi duyệt 2A/2B – design.md mục 19.2).
    /// </summary>
    public static readonly SheetSignSpec Sign2 = new(
        SheetSign.Heading,
        new("", "Người lập bảng", ""),
        [
            new("TPCUVT", "Trưởng phòng CUVT", ""),
            new("DivisionDirector", "GĐ Khối liên quan", ""),
            new("PHOCTUBCL", "Phó CT.UBCL&PT", ""),
        ],
        new("", "Chủ tịch HĐQT/TGĐ", ""));

    /// <summary>
    /// Khối ký 3a/3A/3B nguyên văn ảnh trong sheet: Người đề nghị (Ký đề nghị) · TP. CUVT (Ký kiểm tra) · TP. TC-NQ-KT (Ký kiểm tra) · GĐ TC-NQ-KT
    /// (Ký, kiểm soát chi phí) · P.Chủ tịch UBCL&amp;PT (Ký duyệt đề nghị) · Chủ tịch HĐQT/TGĐ (Ký duyệt) – chuỗi duyệt 3a/3A/3B (design.md mục 19.2).
    /// </summary>
    public static readonly SheetSignSpec Sign3 = new(
        SheetSign.Heading,
        new("", "Người đề nghị", "(Ký đề nghị)"),
        [
            new("TPCUVT", "TP. CUVT", "(Ký kiểm tra)"),
            new("TPTCKT", "TP. TC-NQ-KT", "(Ký kiểm tra)"),
            new("GDTCKT", "GĐ TC-NQ-KT", "(Ký, kiểm soát chi phí)"),
            new("PHOCTUBCL", "P.Chủ tịch UBCL&PT", "(Ký duyệt đề nghị)"),
        ],
        new("", "Chủ tịch HĐQT/TGĐ", "(Ký duyệt)"));

    /// <summary>
    /// Khối ký 5A/5B nguyên văn ảnh trong sheet: Người đề xuất · GĐK đề nghị (GĐ khối của phiếu) · TP. TC-NQ-KT · GĐ Phòng TC-NQ-KT, mỗi ô "(Ký, ghi rõ
    /// họ tên)"; ô CHỦ TỊCH HĐQT/TGĐ (Duyệt đồng ý) cả hàng – chuỗi duyệt 5A/5B (design.md mục 19.2).
    /// </summary>
    public static readonly SheetSignSpec Sign5 = new(
        SheetSign.Heading,
        new("", "Người đề xuất", "(Ký, ghi rõ họ tên)"),
        [
            new("DivisionDirector", "GĐK đề nghị", "(Ký, ghi rõ họ tên)"),
            new("TPTCKT", "TP. TC-NQ-KT", "(Ký, ghi rõ họ tên)"),
            new("GDTCKT", "GĐ Phòng TC-NQ-KT", "(Ký, ghi rõ họ tên)"),
        ],
        new("", "CHỦ TỊCH HĐQT/TGĐ", "(Duyệt đồng ý)"));

    /// <inheritdoc />
    public Task<PdfFile> DocumentPrintAsync(string docType, Guid id, PrintKind kind, string? form, CancellationToken cancellationToken) => docType switch
    {
        DocumentTypes.PurchaseRequest => RenderPurchaseRequestAsync(id, kind, cancellationToken),
        DocumentTypes.PurchaseOrder => RenderPurchaseOrderSheetAsync(id, kind, cancellationToken),
        DocumentTypes.PurchaseSummary => RenderSummarySheetAsync(id, kind, cancellationToken),
        DocumentTypes.RequestForQuotation => RenderRfqSheetAsync(id, form, kind, cancellationToken),
        DocumentTypes.PaymentRequest => RenderPaymentSheetAsync(id, kind, cancellationToken),
        DocumentTypes.SundryExpenseSheet => RenderSundrySheetAsync(id, kind, cancellationToken),
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
            case DocumentTypes.PurchaseSummary:
                await _summaries.GetAsync(id, cancellationToken);
                break;
            case DocumentTypes.RequestForQuotation:
                await _rfqs.GetAsync(id, cancellationToken);
                break;
            case DocumentTypes.PaymentRequest:
                await _payments.GetAsync(id, cancellationToken);
                break;
            case DocumentTypes.SundryExpenseSheet:
                await _sundries.GetAsync(id, cancellationToken);
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

    /// <summary>
    /// Kết quả hỏi giá 3A (bảng so sánh) / 3a, 3B (bảng trình giá từng NCC trúng thầu, nối trong một file) giữ nguyên sheet; <paramref name="kind"/>
    /// null = bản xem. Người đề nghị là người chọn thầu (không có thì người lập RFQ).
    /// </summary>
    private async Task<PdfFile> RenderRfqSheetAsync(Guid id, string? wanted, PrintKind? kind, CancellationToken cancellationToken)
    {
        var (rfq, forms) = await RfqFormsAsync(id, wanted, cancellationToken);
        var entity = await _db.Rfqs.AsNoTracking().Where(r => r.Id == id).Select(r => new { r.AwardedBy, r.CreatedBy }).SingleAsync(cancellationToken);
        var sign = await SignBlockAsync(
            Sign3, DocumentTypes.RequestForQuotation, id, rfq.Status, entity.AwardedBy ?? entity.CreatedBy, rfq.AwardedByName ?? rfq.CreatedByName,
            cancellationToken);
        var (logo, stamp) = await DecorAsync(DocumentTypes.RequestForQuotation, id, rfq.No, rfq.Status, kind, sign.ApprovalStatus, cancellationToken);
        var pages = forms.Select(f => new SheetPage(
            f, f.FormNo switch { "3A" => CuvtSheetLayout.Sheet3A, "3a" => CuvtSheetLayout.Sheet3a, _ => CuvtSheetLayout.Sheet3B }, sign, stamp)).ToList();
        return new PdfFile($"{rfq.No}.pdf", CuvtSheetLayout.RenderMany(pages, logo));
    }

    /// <summary>
    /// Đề nghị thanh toán: 5A/5B giữ nguyên sheet tiền mặt / chuyển khoản theo hình thức của phiếu; 1C tạp vụ (mẫu giả lập) in khuôn chung kèm chữ ký
    /// người duyệt, logo, góc thông tin. <paramref name="kind"/> null = bản xem.
    /// </summary>
    private async Task<PdfFile> RenderPaymentSheetAsync(Guid id, PrintKind? kind, CancellationToken cancellationToken)
    {
        var (payment, form) = await PaymentFormAsync(id, cancellationToken);
        var createdBy = await _db.PaymentRequests.AsNoTracking().Where(p => p.Id == id).Select(p => p.CreatedBy).SingleAsync(cancellationToken);
        if (payment.PurchaseOrderId is null)
            return await RenderGenericAsync(form, DocumentTypes.PaymentRequest, id, payment.Status, createdBy, kind, cancellationToken);

        var sign = await SignBlockAsync(Sign5, DocumentTypes.PaymentRequest, id, payment.Status, createdBy, payment.CreatedByName, cancellationToken);
        var (logo, stamp) = await DecorAsync(DocumentTypes.PaymentRequest, id, payment.No, payment.Status, kind, sign.ApprovalStatus, cancellationToken);
        var transfer = payment.Method == "BankTransfer";
        var spec = (form.FormNo == "5A", transfer) switch
        {
            (true, true) => CuvtSheetLayout.Sheet5ATransfer,
            (true, false) => CuvtSheetLayout.Sheet5ACash,
            (false, true) => CuvtSheetLayout.Sheet5BTransfer,
            _ => CuvtSheetLayout.Sheet5BCash,
        };
        return new PdfFile($"{payment.No}.pdf", CuvtSheetLayout.Render(form, spec, logo, sign, stamp));
    }

    /// <summary>Bảng kê chi phí tạp vụ 2C (mẫu giả lập) – khuôn chung kèm chữ ký người duyệt, logo, góc thông tin; <paramref name="kind"/> null = bản xem.</summary>
    private async Task<PdfFile> RenderSundrySheetAsync(Guid id, PrintKind? kind, CancellationToken cancellationToken)
    {
        var sheet = await _sundries.GetAsync(id, cancellationToken);
        var company = await CompanyAsync(cancellationToken);
        var form = CuvtPrintForms.SundryExpenseSheet(sheet, company.Name);
        var createdBy = await _db.SundryExpenseSheets.AsNoTracking().Where(s => s.Id == id).Select(s => s.CreatedBy).SingleAsync(cancellationToken);
        return await RenderGenericAsync(form, DocumentTypes.SundryExpenseSheet, id, sheet.Status, createdBy, kind, cancellationToken);
    }

    /// <summary>
    /// Mẫu giả lập 1C/2C (chưa có sheet thật – design.md 19.3): khuôn chung <see cref="CuvtFormLayout"/>, ô ký theo chuỗi duyệt (chức danh = tên vai
    /// trò của bước) có chữ ký theo phiên bản, logo, góc thông tin hệ thống, dấu chìm, nhật ký in như các mẫu giữ nguyên sheet.
    /// </summary>
    private async Task<PdfFile> RenderGenericAsync(
        CuvtForm form, string docType, Guid id, string status, Guid? createdBy, PrintKind? kind, CancellationToken cancellationToken)
    {
        var data = await SignDataAsync(docType, id, createdBy, cancellationToken);
        var draft = status == "Draft" || data.SubmittedAt is null;
        var signers = new List<CuvtSigner>
        {
            new(form.Signers.FirstOrDefault()?.Title ?? "Người lập", data.FirstName ?? form.Signers.FirstOrDefault()?.Name,
                draft ? "Chưa gửi duyệt"
                : data.SubmittedByOther is { } other ? $"Gửi duyệt bởi {other} {SheetSign.Time(data.SubmittedAt)}"
                : $"Gửi duyệt {SheetSign.Time(data.SubmittedAt)}",
                Signature: draft ? null : data.FirstSignature),
        };
        signers.AddRange(data.Steps.OrderBy(s => s.Stage).ThenBy(s => s.Level).Select(s => s.Status switch
        {
            "Approved" when !draft => new CuvtSigner(s.RoleName ?? $"Cấp {s.Level}", s.ActorName,
                s.Signature is null ? $"Đã duyệt điện tử {SheetSign.Time(s.ActedAt)}" : $"Đã duyệt {SheetSign.Time(s.ActedAt)}", Signature: s.Signature),
            "Rejected" when !draft => new CuvtSigner(s.RoleName ?? $"Cấp {s.Level}", s.ActorName, $"Từ chối {SheetSign.Time(s.ActedAt)}"),
            _ => new CuvtSigner(s.RoleName ?? $"Cấp {s.Level}", null, "Chưa duyệt"),
        }));
        // Tình trạng phê duyệt cùng cách viết với các mẫu giữ nguyên sheet (khối ký tạm không có ô cố định – mọi bước vào ô phát sinh).
        var approval = SheetSign.Build(new SheetSignSpec(SheetSign.Heading, new("", "Người lập", ""), [], new("", "Cấp cao nhất", "")), status,
            data.FirstName, data.SubmittedAt, null, data.SubmittedByOther, data.Steps).ApprovalStatus;
        var (logo, stamp) = await DecorAsync(docType, id, form.No, status, kind, approval, cancellationToken);
        return new PdfFile($"{form.No}.pdf", CuvtFormLayout.Render(form with { Signers = signers }, new CuvtDecor(logo, stamp)));
    }

    /// <summary>Bảng tổng hợp đề xuất mua 2A/2B giữ nguyên sheet; <paramref name="kind"/> null = bản xem.</summary>
    private async Task<PdfFile> RenderSummarySheetAsync(Guid id, PrintKind? kind, CancellationToken cancellationToken)
    {
        var summary = await _summaries.GetAsync(id, cancellationToken);
        var company = await CompanyAsync(cancellationToken);
        var monthSpent = summary.Form == nameof(PurchaseSummaryForm.Goods)
            ? await DepartmentSpending.MonthAsync(_db, summary.DepartmentId, summary.Date, cancellationToken)
            : 0;
        var form = CuvtPrintForms.PurchaseSummary(summary, company.Name, monthSpent);
        var createdBy = await _db.PurchaseSummaries.AsNoTracking().Where(s => s.Id == id).Select(s => s.CreatedBy).SingleAsync(cancellationToken);
        var sign = await SignBlockAsync(Sign2, DocumentTypes.PurchaseSummary, id, summary.Status, createdBy, summary.CreatedByName, cancellationToken);
        var (logo, stamp) = await DecorAsync(DocumentTypes.PurchaseSummary, id, summary.No, summary.Status, kind, sign.ApprovalStatus, cancellationToken);
        var spec = form.FormNo == "2A" ? CuvtSheetLayout.Sheet2A : CuvtSheetLayout.Sheet2B;
        return new PdfFile($"{summary.No}.pdf", CuvtSheetLayout.Render(form, spec, logo, sign, stamp));
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
        // RFQ: "Sent" (đã gửi NCC, chưa chọn thầu), "AwardPending" (chờ duyệt kết quả) cũng chưa có hiệu lực.
        var watermark = status switch
        {
            "Draft" => "BẢN NHÁP",
            "Cancelled" => "ĐÃ HỦY",
            "Submitted" or "Rejected" or "Sent" or "AwardPending" => "CHƯA DUYỆT",
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
        var data = await SignDataAsync(docType, id, firstUserId, cancellationToken);
        return SheetSign.Build(
            spec, status, data.FirstName ?? firstFallbackName, data.SubmittedAt, data.FirstSignature, data.SubmittedByOther, data.Steps);
    }

    /// <summary>Số liệu ô ký: bước duyệt (đã gồm tên in và chữ ký), người lập (tên, chữ ký lúc gửi), lúc gửi, người gửi thay.</summary>
    /// <param name="FirstName">Tên người lập kèm danh xưng; null khi không đọc được tài khoản.</param>
    /// <param name="FirstSignature">Chữ ký người lập lúc gửi duyệt – chỉ khi chính người lập gửi.</param>
    /// <param name="SubmittedAt">Lúc gửi duyệt của lượt hiện tại.</param>
    /// <param name="SubmittedByOther">Người gửi duyệt khi khác người lập.</param>
    /// <param name="Steps">Bước duyệt của lượt hiện tại (đã bỏ bước "Skipped").</param>
    private sealed record SignData(
        string? FirstName, SheetImage? FirstSignature, DateTimeOffset? SubmittedAt, string? SubmittedByOther, IReadOnlyList<SheetApprovalStep> Steps);

    /// <summary>Đọc bước duyệt của lượt gần nhất, tên in (danh xưng + họ tên, "(thay …)"), chữ ký có hiệu lực lúc duyệt / gửi.</summary>
    private async Task<SignData> SignDataAsync(string docType, Guid id, Guid? firstUserId, CancellationToken cancellationToken)
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
        return new SignData(Display(firstUserId), byOther is null ? SignatureAt(firstUserId, submittedAt) : null, submittedAt, byOther, printSteps);
    }
}
