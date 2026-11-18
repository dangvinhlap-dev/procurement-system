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
/// </summary>
public sealed partial class DocumentPdfService
{
    /// <inheritdoc />
    public Task<PdfFile> PurchaseRequestAsync(Guid id, CancellationToken cancellationToken) => RenderPurchaseRequestAsync(id, null, cancellationToken);

    /// <inheritdoc />
    public Task<PdfFile> PurchaseRequestPrintAsync(Guid id, PrintKind kind, CancellationToken cancellationToken) =>
        RenderPurchaseRequestAsync(id, kind, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<DocumentPrintDto>> PurchaseRequestPrintsAsync(Guid id, CancellationToken cancellationToken) =>
        DocumentPrintsAsync(DocumentTypes.PurchaseRequest, id, cancellationToken);

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
        var requesterId = await _db.PurchaseRequests.AsNoTracking().Where(r => r.Id == pr.Id)
            .Select(r => r.RequesterId ?? r.CreatedBy).SingleAsync(cancellationToken);
        var sign = await SignBlockAsync(
            PurchaseRequestSheet.Spec, DocumentTypes.PurchaseRequest, pr.Id, pr.Status, requesterId, pr.RequesterName ?? pr.CreatedByName, cancellationToken);
        var (logo, stamp) = await DecorAsync(DocumentTypes.PurchaseRequest, pr.Id, pr.No, pr.Status, kind, sign.ApprovalStatus, cancellationToken);
        return new PdfFile($"{pr.No}.pdf", PurchaseRequestSheetLayout.Render(form, logo, sign, stamp));
    }
}
