using Microsoft.EntityFrameworkCore;
using Proc.Application.Documents;
using Proc.Application.Printing;
using Proc.Domain.Documents;
using Proc.Domain.Purchasing;
using Proc.Infrastructure.Purchasing;

namespace Proc.Infrastructure.Printing;

/// <summary>
/// In đề nghị mua giữ nguyên sheet 1A (lát IN1 – design.md mục 20.4): chữ ký của người đã duyệt (phiên bản có hiệu lực lúc duyệt), logo công ty
/// của phiếu, góc dưới phải ghi giờ in, người in, lần in, tình trạng phê duyệt; In / Tải PDF ghi nhật ký in, Xem thì không.
/// Đề nghị loại B in 1B kèm trang 2B (người dùng yêu cầu 20/11/2026 – design.md mục 20.4 IN1-13).
/// </summary>
public sealed partial class DocumentPdfService
{
    /// <summary>Đơn vị mua in khi danh mục đơn vị mua của công ty chưa có gì.</summary>
    private static readonly CuvtPrintForms.SummaryUnit DefaultUnit = new(Guid.Empty, "CUVT", "Phòng CUVT");

    /// <inheritdoc />
    public Task<PdfFile> PurchaseRequestAsync(Guid id, CancellationToken cancellationToken) => RenderPurchaseRequestAsync(id, null, cancellationToken);

    /// <inheritdoc />
    public Task<PdfFile> PurchaseRequestPrintAsync(Guid id, PrintKind kind, CancellationToken cancellationToken) =>
        RenderPurchaseRequestAsync(id, kind, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<DocumentPrintDto>> PurchaseRequestPrintsAsync(Guid id, CancellationToken cancellationToken) =>
        DocumentPrintsAsync(DocumentTypes.PurchaseRequest, id, cancellationToken);

    /// <summary>
    /// Dựng bản in; <paramref name="kind"/> null = bản xem (không ghi nhật ký in). Loại B (1B): trang 1 là 1B khổ dọc, trang sau là 2B khổ ngang
    /// dựng từ chính đề nghị – một lần in, một lần ghi nhật ký, chữ ký 2B lấy từ chuỗi duyệt của đề nghị (TP CUVT, GĐ khối, Phó CT, Chủ tịch đều nằm
    /// trong chuỗi 1B) nên không phải lập và duyệt bảng tổng hợp riêng mới in được 2B.
    /// </summary>
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
        var people = await _db.PurchaseRequests.AsNoTracking().Where(r => r.Id == pr.Id)
            .Select(r => new { Requester = r.RequesterId ?? r.CreatedBy, r.CreatedBy }).SingleAsync(cancellationToken);
        var data = await SignDataAsync(DocumentTypes.PurchaseRequest, pr.Id, pr.Status, people.Requester, people.CreatedBy, cancellationToken);
        var firstName = data.FirstName ?? pr.RequesterName ?? pr.CreatedByName;
        var sign = SheetSign.Build(
            PurchaseRequestSheet.Spec, pr.Status, firstName, data.SubmittedAt, data.FirstSignature, data.SubmittedByOther, data.Steps);
        var (logo, stamp) = await DecorAsync(DocumentTypes.PurchaseRequest, pr.Id, pr.No, pr.Status, kind, sign.ApprovalStatus, cancellationToken);
        var sheet1 = PurchaseRequestSheetLayout.Render(form, logo, sign, stamp);
        if (pr.Category != nameof(PurchaseCategory.Goods)) return new PdfFile($"{pr.No}.pdf", sheet1);

        var form2 = CuvtPrintForms.PurchaseRequestSummary(
            pr, company.Name, monthSpent, await SummaryUnitsAsync(pr, cancellationToken), await DefaultSummaryUnitAsync(cancellationToken));
        if (form2 is null) return new PdfFile($"{pr.No}.pdf", sheet1);
        // "Người lập bảng" của 2B là người đề nghị (bảng dựng từ chính đề nghị); tình trạng phê duyệt góc dưới phải giữ theo cả chuỗi 1B.
        var sign2 = SheetSign.Build(
            Sign2, pr.Status, firstName, data.SubmittedAt, data.FirstSignature, data.SubmittedByOther, SheetSign.OnlySlotsOf(Sign2, data.Steps))
            with { ApprovalStatus = sign.ApprovalStatus };
        var sheet2 = CuvtSheetLayout.Render(form2, CuvtSheetLayout.Sheet2B, logo, sign2, stamp);
        return new PdfFile($"{pr.No}.pdf", CuvtSheetLayout.Merge($"{form.Title} {form.No}", [sheet1, sheet2]));
    }

    /// <summary>Đơn vị mua của dòng đề nghị đã chọn vào bảng tổng hợp chưa hủy (bảng lập sau cùng nếu có nhiều).</summary>
    private async Task<IReadOnlyDictionary<Guid, CuvtPrintForms.SummaryUnit>> SummaryUnitsAsync(
        Application.Purchasing.PurchaseRequestDto pr, CancellationToken cancellationToken)
    {
        var lineIds = pr.Lines.Select(l => l.Id).ToList();
        var rows = await (
                from l in _db.Set<PurchaseSummaryLine>().AsNoTracking()
                join s in _db.PurchaseSummaries on l.SummaryId equals s.Id
                join u in _db.PurchasingUnits on l.PurchasingUnitId equals u.Id
                where lineIds.Contains(l.RequestLineId) && s.Status != ApprovalDocumentStatus.Cancelled
                select new { l.RequestLineId, s.CreatedAt, u.Id, u.Code, u.Name })
            .ToListAsync(cancellationToken);
        return rows.GroupBy(r => r.RequestLineId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.CreatedAt).Select(r => new CuvtPrintForms.SummaryUnit(r.Id, r.Code, r.Name)).First());
    }

    /// <summary>Đơn vị mua mặc định cho dòng chưa ở bảng tổng hợp: mã "CUVT" nếu có, không thì đơn vị còn dùng có mã nhỏ nhất.</summary>
    private async Task<CuvtPrintForms.SummaryUnit> DefaultSummaryUnitAsync(CancellationToken cancellationToken)
    {
        var units = await _db.PurchasingUnits.AsNoTracking().Where(u => u.IsActive).OrderBy(u => u.Code)
            .Select(u => new CuvtPrintForms.SummaryUnit(u.Id, u.Code, u.Name)).ToListAsync(cancellationToken);
        return units.FirstOrDefault(u => string.Equals(u.Code, "CUVT", StringComparison.OrdinalIgnoreCase)) ?? units.FirstOrDefault() ?? DefaultUnit;
    }
}
