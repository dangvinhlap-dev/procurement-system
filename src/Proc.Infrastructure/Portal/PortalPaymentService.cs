using Microsoft.EntityFrameworkCore;
using Proc.Application.Common;
using Proc.Application.Portal;
using Proc.Domain.Common;
using Proc.Domain.Documents;
using Proc.Domain.Purchasing;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.Portal;

/// <summary>
/// Trạng thái thanh toán trên Portal (lát P6b – design.md 19.5, P6b-1…P6b-4). Phiên Portal không có công ty nên mọi truy vấn bỏ bộ lọc toàn
/// cục và tự lọc theo NCC của phiên (P1b-4): chỉ PO NCC được thấy (P4-1). Số liệu cùng cách tính báo cáo theo dõi thanh toán TTPO (Y9-2).
/// </summary>
public sealed class PortalPaymentService : IPortalPaymentService
{
    private readonly ProcDbContext _db;

    /// <summary>Tạo dịch vụ.</summary>
    /// <param name="db">DbContext.</param>
    public PortalPaymentService(ProcDbContext db) => _db = db;

    /// <inheritdoc />
    public async Task<IReadOnlyList<PortalPaymentOrderDto>> ListAsync(PortalSession session, CancellationToken cancellationToken)
    {
        var data = await LoadAsync(session.SupplierId, null, cancellationToken);
        return data.Orders
            .Where(o => data.Receipts.Any(r => r.PurchaseOrderId == o.Id) || data.Payments.Any(p => p.PurchaseOrderId == o.Id))
            .OrderByDescending(o => o.Date).ThenByDescending(o => o.No, StringComparer.Ordinal)
            .Select(o => Summary(o, data))
            .ToList();
    }

    /// <inheritdoc />
    public async Task<PortalPaymentDetailDto> GetAsync(PortalSession session, Guid orderId, CancellationToken cancellationToken)
    {
        var data = await LoadAsync(session.SupplierId, orderId, cancellationToken);
        var order = data.Orders.SingleOrDefault() ?? throw new NotFoundException("Không tìm thấy đơn đặt hàng.");
        var payments = data.Payments.ToDictionary(p => p.Id);
        var heldBy = data.Lines.Where(l => payments.ContainsKey(l.RequestId))
            .GroupBy(l => l.ReceiptConfirmationId)
            .ToDictionary(g => g.Key, g => payments[g.First().RequestId]);
        var invoices = data.Receipts
            .OrderByDescending(r => r.Date).ThenByDescending(r => r.No, StringComparer.Ordinal)
            .Select(r =>
            {
                var payment = heldBy.GetValueOrDefault(r.Id);
                var status = payment is null ? (r.HasInvoice ? "Invoiced" : "AwaitingInvoice")
                    : payment.PaidDate is not null ? "Paid"
                    : payment.Status == ApprovalDocumentStatus.Approved ? "Approved"
                    : "Processing";
                // Số đợt chỉ hiện khi đề nghị đã duyệt – đề nghị đang lập / chờ duyệt là việc nội bộ, còn đổi.
                var shown = payment?.Status == ApprovalDocumentStatus.Approved ? payment : null;
                return new PortalPaymentInvoiceDto(
                    r.No, r.Date, r.Received, r.DeliveryNoteNo, r.InvoiceNo, r.InvoiceSeries, r.InvoiceDate, r.InvoiceAmount, status, shown?.No,
                    shown?.PaidDate);
            })
            .ToList();
        var lineInvoices = data.Lines.ToLookup(l => l.RequestId);
        var rounds = data.Payments.Where(p => p.Status == ApprovalDocumentStatus.Approved)
            .OrderByDescending(p => p.ApprovedAt).ThenByDescending(p => p.No, StringComparer.Ordinal)
            .Select(p => new PortalPaymentRoundDto(
                p.No, p.ApprovedAt is { } at ? DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(at, VietnamTime.Zone).DateTime) : p.Date,
                p.Method.ToString(), p.BankName, p.BankAccountNumber, p.BankAccountName,
                string.Join(", ", lineInvoices[p.Id].OrderBy(l => l.LineNo).Select(l => l.InvoiceNo).Where(n => !string.IsNullOrEmpty(n))),
                p.TotalAmount, p.DeductionAmount, p.DeductionReason, p.PayableAmount, p.PaidDate is null ? "Approved" : "Paid", p.PaidDate))
            .ToList();
        return new PortalPaymentDetailDto(Summary(order, data), invoices, rounds);
    }

    /// <summary>
    /// Tổng hợp một đơn (P6b-4). Còn phải trả = tiền hóa đơn − đã thanh toán − giảm trừ của đợt đã thanh toán; hàng nhận chưa có hóa đơn chưa
    /// tính. Ví dụ hóa đơn 1.045.000, đã thanh toán 600.000 kèm giảm trừ 27.000 → còn 418.000.
    /// </summary>
    private static PortalPaymentOrderDto Summary(OrderRow o, PaymentData data)
    {
        var rs = data.Receipts.Where(r => r.PurchaseOrderId == o.Id).ToList();
        var ps = data.Payments.Where(p => p.PurchaseOrderId == o.Id).ToList();
        var invoice = rs.Where(r => r.HasInvoice).Sum(r => r.InvoiceAmount ?? 0m);
        var paid = ps.Where(p => p.PaidDate is not null).ToList();
        var paidAmount = paid.Sum(p => p.PayableAmount);
        var deduction = paid.Sum(p => p.DeductionAmount);
        var outstanding = invoice - paidAmount - deduction;
        var status = invoice == 0 ? "NoInvoice" : outstanding <= 0 ? "Paid" : paid.Count > 0 ? "PartlyPaid" : "Unpaid";
        return new PortalPaymentOrderDto(
            o.Id, o.No, data.Companies.GetValueOrDefault(o.CompanyId) ?? "", o.Date, o.Amount, rs.Sum(r => r.Received), rs.Count,
            rs.Count(r => !r.HasInvoice), invoice,
            // Đề nghị bị từ chối quay về nháp vẫn giữ hóa đơn → vẫn là "đang xử lý" (như cột đang lập / chờ duyệt của TTPO).
            ps.Where(p => p.Status is ApprovalDocumentStatus.Draft or ApprovalDocumentStatus.Submitted).Sum(p => p.TotalAmount),
            ps.Where(p => p.Status == ApprovalDocumentStatus.Approved && p.PaidDate is null).Sum(p => p.PayableAmount),
            paidAmount, deduction, outstanding, paid.Count == 0 ? null : paid.Max(p => p.PaidDate), status);
    }

    /// <summary>Nạp đơn NCC thấy được (một đơn hoặc tất cả), XNH đã xác nhận, đề nghị thanh toán chưa hủy và dòng của chúng.</summary>
    private async Task<PaymentData> LoadAsync(Guid supplierId, Guid? orderId, CancellationToken cancellationToken)
    {
        var orders = await PortalOrderService.VisibleOrders(_db, supplierId)
            .Where(o => orderId == null || o.Id == orderId)
            .Select(o => new OrderRow(o.Id, o.No, o.CompanyId, o.Date, o.TotalAmount + o.TotalVat))
            .ToListAsync(cancellationToken);
        var ids = orders.Select(o => o.Id).ToList();
        var receipts = await _db.GoodsReceiptConfirmations.AsNoTracking().IgnoreQueryFilters()
            .Where(c => ids.Contains(c.PurchaseOrderId) && c.SupplierId == supplierId && c.Status == ReceiptConfirmationStatus.Confirmed)
            .Select(c => new ReceiptRow(
                c.Id, c.PurchaseOrderId, c.No, c.Date, c.TotalAmount + c.TotalVat, c.DeliveryNoteNo, c.InvoiceNo, c.InvoiceSeries, c.InvoiceDate,
                c.InvoiceAmount, c.InvoiceNo != null && c.InvoiceAmount > 0))
            .ToListAsync(cancellationToken);
        var payments = await _db.PaymentRequests.AsNoTracking().IgnoreQueryFilters()
            .Where(p => p.PurchaseOrderId != null && ids.Contains(p.PurchaseOrderId.Value) && p.SupplierId == supplierId
                        && p.Status != ApprovalDocumentStatus.Cancelled)
            .ToListAsync(cancellationToken);
        var paymentIds = payments.Select(p => p.Id).ToList();
        var lines = await _db.Set<PaymentRequestLine>().AsNoTracking().IgnoreQueryFilters()
            .Where(l => paymentIds.Contains(l.RequestId) && l.ReceiptConfirmationId != null)
            .Select(l => new LineRow(l.RequestId, l.LineNo, l.ReceiptConfirmationId!.Value, l.InvoiceNo))
            .ToListAsync(cancellationToken);
        var companyIds = orders.Select(o => o.CompanyId).Distinct().ToList();
        var companies = await _db.Companies.AsNoTracking().IgnoreQueryFilters().Where(c => companyIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);
        return new PaymentData(orders, receipts, payments, lines, companies);
    }

    private sealed record OrderRow(Guid Id, string No, Guid CompanyId, DateOnly Date, decimal Amount);

    private sealed record ReceiptRow(
        Guid Id, Guid PurchaseOrderId, string No, DateOnly Date, decimal Received, string? DeliveryNoteNo, string? InvoiceNo, string? InvoiceSeries,
        DateOnly? InvoiceDate, decimal? InvoiceAmount, bool HasInvoice);

    private sealed record LineRow(Guid RequestId, int LineNo, Guid ReceiptConfirmationId, string? InvoiceNo);

    private sealed record PaymentData(
        IReadOnlyList<OrderRow> Orders, IReadOnlyList<ReceiptRow> Receipts, IReadOnlyList<PaymentRequest> Payments, IReadOnlyList<LineRow> Lines,
        IReadOnlyDictionary<Guid, string> Companies);
}
