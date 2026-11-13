using Microsoft.EntityFrameworkCore;
using Proc.Application.Common;
using Proc.Application.Documents;
using Proc.Application.Portal;
using Proc.Application.Purchasing;
using Proc.Domain.Common;
using Proc.Domain.Documents;
using Proc.Domain.MasterData;
using Proc.Domain.Organization;
using Proc.Domain.Purchasing;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.Purchasing;

/// <summary>
/// Đề nghị thanh toán 5A/5B (lát Y8, design.md mục 19.3 bước 7). Phiếu thuộc công ty đang làm việc (filter công ty giữ nguyên); lập cho một PO đã
/// duyệt / đã đóng, dòng là XNH đã xác nhận có hóa đơn; mỗi XNH chỉ ở một đề nghị chưa hủy – kiểm tra dưới khóa PO để hai người lập cùng lúc không
/// lấy trùng. Duyệt qua <see cref="ApprovalEngine"/> theo bộ cấp DNT; duyệt xong kế toán ghi Đã chi.
/// </summary>
public sealed class PaymentRequestService : IPaymentRequestService
{
    /// <summary>Số phiếu tối đa trên danh sách.</summary>
    public const int ListLimit = 2000;

    private readonly ProcDbContext _db;
    private readonly ICurrentUserScope _scope;
    private readonly IDocumentNumberService _numbers;
    private readonly IClock _clock;
    private readonly ApprovalEngine _approvals;
    private readonly IPortalEmailService _portalEmails;

    /// <summary>Định dạng số kiểu Việt Nam trong thư (1.234.567).</summary>
    private static readonly System.Globalization.CultureInfo Vi = System.Globalization.CultureInfo.GetCultureInfo("vi-VN");

    /// <summary>Tạo dịch vụ.</summary>
    /// <param name="db">DbContext.</param>
    /// <param name="scope">Phạm vi người dùng.</param>
    /// <param name="numbers">Đánh số chứng từ.</param>
    /// <param name="clock">Đồng hồ.</param>
    /// <param name="portalEmails">Thư báo NCC qua Portal (báo đã thanh toán – lát P6b).</param>
    public PaymentRequestService(
        ProcDbContext db, ICurrentUserScope scope, IDocumentNumberService numbers, IClock clock, IPortalEmailService portalEmails)
    {
        _db = db;
        _portalEmails = portalEmails;
        _scope = scope;
        _numbers = numbers;
        _clock = clock;
        _approvals = new ApprovalEngine(db, scope, clock);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PaymentRequestListItemDto>> ListAsync(PaymentRequestFilter filter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);
        EnsureCanView();
        var query = _db.PaymentRequests.AsNoTracking();
        if (filter.From is { } from) query = query.Where(r => r.Date >= from);
        if (filter.To is { } to) query = query.Where(r => r.Date <= to);
        // 1C (lát Y11b) không có PO, NCC – nối trái.
        var rows = await (
                from r in query
                join o in _db.PurchaseOrders on r.PurchaseOrderId equals o.Id into orders
                from o in orders.DefaultIfEmpty()
                join s in _db.Suppliers on r.SupplierId equals s.Id into suppliers
                from s in suppliers.DefaultIfEmpty()
                join d in _db.Departments on r.DepartmentId equals d.Id into departments
                from d in departments.DefaultIfEmpty()
                join u in _db.Users on r.CreatedBy equals u.Id into users
                from u in users.DefaultIfEmpty()
                orderby r.Date descending, r.No descending
                select new
                {
                    r.Id, r.No, r.Date, r.Form, r.Status, r.PaidDate, r.PurchaseOrderId, OrderNo = o == null ? null : o.No,
                    Code = s == null ? null : s.Code, Name = s == null ? null : s.Name, r.Method, r.TotalAmount, r.DeductionAmount, r.PayableAmount,
                    CreatedBy = u == null ? null : u.FullName, r.PayeeName, Department = d == null ? null : d.Name,
                })
            .Take(ListLimit)
            .ToListAsync(cancellationToken);
        return rows.Select(r => new PaymentRequestListItemDto(
                r.Id, r.No, r.Date, FormCode(r.Form), r.Status.ToString(), r.PaidDate is not null, r.PaidDate, r.PurchaseOrderId, r.OrderNo, r.Code,
                r.Name, r.Method.ToString(), r.TotalAmount, r.DeductionAmount, r.PayableAmount, r.CreatedBy, r.PayeeName, r.Department))
            .ToList();
    }

    /// <inheritdoc />
    public async Task<PaymentRequestDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        EnsureCanView();
        var request = await _db.PaymentRequests.AsNoTracking().Include(r => r.Lines).SingleOrDefaultAsync(r => r.Id == id, cancellationToken)
                      ?? throw new NotFoundException("Không tìm thấy đề nghị thanh toán.");
        return await ToDtoAsync(request, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PaymentOrderOptionDto>> OrdersAsync(CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        _scope.EnsurePermission(Permissions.Purchase.PaymentCreate);
        var held = HeldReceiptIds(null);
        var rows = await (
                from c in _db.GoodsReceiptConfirmations.AsNoTracking()
                join o in _db.PurchaseOrders on c.PurchaseOrderId equals o.Id
                join s in _db.Suppliers on o.SupplierId equals s.Id
                where c.Status == ReceiptConfirmationStatus.Confirmed && c.InvoiceNo != null && c.InvoiceAmount > 0 && !held.Contains(c.Id)
                      && (o.Status == ApprovalDocumentStatus.Approved || o.Status == ApprovalDocumentStatus.Closed)
                group c by new { o.Id, o.No, o.Date, s.Code, s.Name } into g
                orderby g.Key.Date descending, g.Key.No descending
                select new PaymentOrderOptionDto(g.Key.Id, g.Key.No, g.Key.Date, g.Key.Code, g.Key.Name, g.Count(), g.Sum(x => x.InvoiceAmount ?? 0)))
            .Take(ListLimit)
            .ToListAsync(cancellationToken);
        return rows;
    }

    /// <inheritdoc />
    public async Task<PaymentRequestSourceDto> SourceAsync(Guid purchaseOrderId, Guid? requestId, CancellationToken cancellationToken)
    {
        EnsureCanView();
        var order = await _db.PurchaseOrders.AsNoTracking().SingleOrDefaultAsync(o => o.Id == purchaseOrderId, cancellationToken)
                    ?? throw new NotFoundException("Không tìm thấy đơn mua hàng.");
        var supplier = await _db.Suppliers.AsNoTracking().Include(s => s.BankAccounts).SingleAsync(s => s.Id == order.SupplierId, cancellationToken);
        var heldBy = await HeldByAsync(order.Id, requestId, cancellationToken);
        var receipts = await _db.GoodsReceiptConfirmations.AsNoTracking()
            .Where(c => c.PurchaseOrderId == order.Id && c.Status == ReceiptConfirmationStatus.Confirmed)
            .OrderBy(c => c.Date).ThenBy(c => c.No)
            .Select(c => new { c.Id, c.No, c.Date, c.DeliveryNoteNo, c.InvoiceNo, c.InvoiceSeries, c.InvoiceDate, c.InvoiceAmount, Received = c.TotalAmount + c.TotalVat })
            .ToListAsync(cancellationToken);
        return new PaymentRequestSourceDto(
            order.Id, order.No, FormCode(FormOf(order)), supplier.Code, supplier.Name, await DepartmentNameAsync(order.DepartmentId, cancellationToken),
            await ProjectCodeAsync(order.Category == PurchaseCategory.Construction ? order.ConstructionProjectId : null, cancellationToken),
            supplier.BankAccounts.OrderByDescending(b => b.IsDefault).ThenBy(b => b.BankName)
                .Select(b => new PaymentBankAccountDto(b.Id, b.BankName, b.Branch, b.AccountNumber, b.AccountName, b.IsDefault)).ToList(),
            receipts.Select(r => new PaymentReceiptOptionDto(
                r.Id, r.No, r.Date, r.DeliveryNoteNo, r.InvoiceNo, r.InvoiceSeries, r.InvoiceDate, r.InvoiceAmount, r.Received, heldBy.GetValueOrDefault(r.Id))).ToList());
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SundryPaymentSheetOptionDto>> SundrySheetsAsync(Guid? departmentId, Guid? requestId, CancellationToken cancellationToken)
    {
        EnsureCanView();
        var rows = await (
                from s in _db.SundryExpenseSheets.AsNoTracking()
                join d in _db.Departments on s.DepartmentId equals d.Id
                where s.Status == ApprovalDocumentStatus.Approved && (departmentId == null || s.DepartmentId == departmentId)
                orderby s.Date descending, s.No descending
                select new { s.Id, s.No, s.Date, s.DepartmentId, d.Code, d.Name, LineCount = s.Lines.Count, s.TotalAmount, s.Note })
            .Take(ListLimit)
            .ToListAsync(cancellationToken);
        var held = await HeldSheetsAsync(_db, rows.Select(r => r.Id).ToList(), requestId, cancellationToken);
        return rows.Select(r => new SundryPaymentSheetOptionDto(
                r.Id, r.No, r.Date, r.DepartmentId, r.Code, r.Name, r.LineCount, r.TotalAmount, r.Note, held.GetValueOrDefault(r.Id).No))
            .ToList();
    }

    /// <summary>
    /// Đề nghị chưa hủy đang giữ từng bảng kê tạp vụ (lát Y11b) – mỗi bảng kê chỉ ở một đề nghị 1C; bảng kê đang ở đề nghị thì không hủy riêng được.
    /// </summary>
    /// <param name="db">DbContext (filter công ty giữ nguyên).</param>
    /// <param name="sheetIds">Bảng kê.</param>
    /// <param name="exceptRequestId">Đề nghị không tính (đề nghị đang sửa).</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Bảng kê → (Id, số) đề nghị.</returns>
    internal static async Task<Dictionary<Guid, (Guid? Id, string? No)>> HeldSheetsAsync(
        ProcDbContext db, IReadOnlyCollection<Guid> sheetIds, Guid? exceptRequestId, CancellationToken cancellationToken)
    {
        var rows = await (
                from l in db.Set<PaymentRequestLine>()
                join r in db.PaymentRequests on l.RequestId equals r.Id
                where l.SundryExpenseSheetId != null && sheetIds.Contains(l.SundryExpenseSheetId.Value)
                      && r.Status != ApprovalDocumentStatus.Cancelled && r.Id != exceptRequestId
                select new { SheetId = l.SundryExpenseSheetId!.Value, r.Id, r.No })
            .ToListAsync(cancellationToken);
        return rows.GroupBy(x => x.SheetId).ToDictionary(g => g.Key, g => ((Guid?)g.First().Id, (string?)g.First().No));
    }

    /// <inheritdoc />
    public async Task<PaymentRequestDto> CreateAsync(SavePaymentRequestRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var companyId = _scope.RequireCompany();
        _scope.EnsurePermission(Permissions.Purchase.PaymentCreate);
        if (request.IsSundry) return await CreateSundryAsync(companyId, request, cancellationToken);
        await using var transaction = await RowLocks.BeginOrJoinAsync(_db, cancellationToken);
        // Khóa PO tới hết giao dịch: hai người cùng lập đề nghị cho một PO phải lần lượt – người sau thấy XNH người trước đã chọn.
        var orderId = request.PurchaseOrderId ?? throw new BusinessRuleException("Hãy chọn đơn mua hàng.");
        await RowLocks.LockAsync<PurchaseOrder>(_db, [orderId], cancellationToken);
        var order = await LoadOrderAsync(orderId, cancellationToken);
        var bank = await BankAccountAsync(order.SupplierId, request, cancellationToken);
        var lines = await ResolveLinesAsync(order, request.Lines, null, cancellationToken);
        var no = await _numbers.NextAsync(DocumentTypes.PaymentRequest, request.Date, cancellationToken);
        var payment = PaymentRequest.Create(companyId, no, order, ToHeader(request), bank);
        payment.SetLines(lines);
        _db.PaymentRequests.Add(payment);
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetAsync(payment.Id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PaymentRequestDto> UpdateAsync(Guid id, SavePaymentRequestRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        _scope.EnsurePermission(Permissions.Purchase.PaymentCreate);
        await using var transaction = await RowLocks.BeginOrJoinAsync(_db, cancellationToken);
        var payment = await LoadAsync(id, cancellationToken);
        if (payment.IsSundry != request.IsSundry) throw new BusinessRuleException("Không đổi biểu mẫu của đề nghị đã lập.");
        if (payment.IsSundry)
        {
            if (request.DepartmentId != payment.DepartmentId)
                throw new BusinessRuleException("Không đổi bộ phận của đề nghị 1C đã lập – hủy phiếu và lập đề nghị mới.");
            var sheets = await ResolveSheetsAsync(request.Lines, payment.Id, cancellationToken);
            payment.UpdateSundry(ToHeader(request), request.PayeeName, ManualBank(request));
            payment.SetSundryLines(sheets);
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return await GetAsync(id, cancellationToken);
        }

        if (request.PurchaseOrderId != payment.PurchaseOrderId)
            throw new BusinessRuleException("Không đổi đơn mua hàng của đề nghị đã lập – hủy phiếu và lập đề nghị mới cho đơn khác.");
        await RowLocks.LockAsync<PurchaseOrder>(_db, [payment.PurchaseOrderId!.Value], cancellationToken);
        var order = await LoadOrderAsync(payment.PurchaseOrderId.Value, cancellationToken);
        var bank = await BankAccountAsync(order.SupplierId, request, cancellationToken);
        var lines = await ResolveLinesAsync(order, request.Lines, payment.Id, cancellationToken);
        payment.Update(ToHeader(request), bank);
        payment.SetLines(lines);
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PaymentRequestDto> SubmitAsync(Guid id, CancellationToken cancellationToken)
    {
        _scope.EnsurePermission(Permissions.Purchase.PaymentCreate);
        // Dùng chung giao dịch của bên gọi khi có: xem trước chuỗi duyệt chạy thử lệnh gửi rồi hủy (như bảng tổng hợp – lát Y5c-2).
        await using var transaction = await RowLocks.BeginOrJoinAsync(_db, cancellationToken);
        var payment = await LoadAsync(id, cancellationToken);
        if (payment.IsSundry)
        {
            // 1C: kiểm lại bảng kê còn Đã duyệt, chưa ở đề nghị khác; chụp lại tổng bảng kê.
            var lines = payment.Lines.OrderBy(l => l.LineNo).Select(l => new SavePaymentRequestLineRequest(null, l.Description, l.SundryExpenseSheetId)).ToList();
            payment.SetSundryLines(await ResolveSheetsAsync(lines, payment.Id, cancellationToken));
        }
        else
        {
            await RowLocks.LockAsync<PurchaseOrder>(_db, [payment.PurchaseOrderId!.Value], cancellationToken);
            var order = await LoadOrderAsync(payment.PurchaseOrderId.Value, cancellationToken);
            // Lúc gửi chụp lại hóa đơn từ XNH (người nhận hàng có thể vừa sửa hóa đơn khi phiếu còn nháp) và kiểm lại XNH chưa thuộc đề nghị khác.
            var data = payment.Lines.OrderBy(l => l.LineNo).Select(l => new SavePaymentRequestLineRequest(l.ReceiptConfirmationId, l.Description)).ToList();
            payment.SetLines(await ResolveLinesAsync(order, data, payment.Id, cancellationToken));
        }

        payment.EnsureSubmittable();
        await _approvals.SubmitAsync(payment, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PaymentRequestDto> ApproveAsync(Guid id, ApprovalActionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var payment = await LoadAsync(id, cancellationToken);
        await _approvals.ApproveAsync(payment, request.Comment, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PaymentRequestDto> RejectAsync(Guid id, ApprovalActionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var payment = await LoadAsync(id, cancellationToken);
        await _approvals.RejectAsync(payment, request.Comment, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PaymentRequestDto> RecallAsync(Guid id, CancellationToken cancellationToken)
    {
        var payment = await LoadAsync(id, cancellationToken);
        if (!CanRecall(payment))
            throw new ForbiddenAccessException($"Chỉ người lập (hoặc quản trị) rút được đề nghị {payment.No} về nháp.");
        await _approvals.SkipPendingAsync(payment, cancellationToken);
        payment.ReturnToDraft(null);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PaymentRequestDto> CancelAsync(Guid id, CancelDocumentRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var payment = await LoadAsync(id, cancellationToken);
        // Đã chi là tiền đã ra khỏi công ty – không hủy đề nghị (xử lý thu hồi ở phần mềm kế toán).
        if (payment.IsPaid) throw new BusinessRuleException($"Đề nghị {payment.No} đã ghi Đã chi ngày {payment.PaidDate:dd/MM/yyyy} – không hủy được.");
        // Quyền hủy + lý do bắt buộc (design.md mục 20.2a): nháp – người lập; đã gửi duyệt trở đi – chỉ quản trị. Hủy xong các XNH chọn lại được.
        var reason = DocumentCancelPolicy.Authorize(
            _scope, $"đề nghị thanh toán {payment.No}", payment.Status == ApprovalDocumentStatus.Draft, payment.CreatedBy, request.Reason);
        payment.Cancel(reason);
        await _approvals.SkipPendingAsync(payment, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PaymentRequestDto> MarkPaidAsync(Guid id, MarkPaymentPaidRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        _scope.EnsurePermission(Permissions.Purchase.PaymentPay);
        var payment = await LoadAsync(id, cancellationToken);
        var approved = payment.ApprovedAt is { } at ? DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(at, VietnamTime.Zone).DateTime) : payment.Date;
        payment.MarkPaid(request.PaidDate, request.Note, _scope.UserId ?? throw new ForbiddenAccessException("Chưa đăng nhập."), approved,
            _clock.TodayInVietnam(), _clock.UtcNow);
        await QueuePaidEmailAsync(payment, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <summary>
    /// Báo NCC qua Portal khi ghi Đã chi (lát P6b, P6b-5): thư vào hàng chờ cùng giao dịch, gửi mọi tài khoản Portal đang dùng của NCC của PO.
    /// Đề nghị 1C tạp vụ (không PO, NCC) và PO mua nội bộ không có thư. Thư không có ghi chú chi của kế toán (số UNC, phiếu chi – nội bộ).
    /// </summary>
    private async Task QueuePaidEmailAsync(PaymentRequest payment, CancellationToken cancellationToken)
    {
        if (payment.PurchaseOrderId is not { } orderId) return;
        var order = await _db.PurchaseOrders.SingleAsync(o => o.Id == orderId, cancellationToken);
        string Money(decimal value) => value.ToString("N0", Vi);
        var method = payment.Method == PaymentMethod.BankTransfer
            ? $"chuyển khoản vào tài khoản {payment.BankAccountNumber} – {payment.BankName}"
            : "tiền mặt";
        var invoices = string.Join(", ", payment.Lines.OrderBy(l => l.LineNo).Select(l => l.InvoiceNo).Where(n => !string.IsNullOrEmpty(n)));
        List<string> lines =
        [
            $"Số tiền thanh toán: {Money(payment.PayableAmount)} đồng, {method}, ngày {payment.PaidDate:dd/MM/yyyy}.",
            .. invoices.Length == 0 ? Array.Empty<string>() : [$"Hóa đơn: {invoices} – tổng {Money(payment.TotalAmount)} đồng."],
            .. payment.DeductionAmount > 0 ? [$"Giảm trừ: {Money(payment.DeductionAmount)} đồng – {payment.DeductionReason}."] : Array.Empty<string>(),
        ];
        await _portalEmails.QueuePurchaseOrderAsync(order, SupplierPortalEmailKind.PaymentPaid, string.Join('\n', lines), cancellationToken);
    }

    /// <summary>
    /// Số đề nghị chưa hủy đang giữ XNH (Y8-2) – để khóa "Cập nhật hóa đơn" trên XNH: hóa đơn đã đưa vào đề nghị thì đổi trên XNH sẽ lệch phiếu.
    /// </summary>
    /// <param name="db">DbContext (filter công ty giữ nguyên).</param>
    /// <param name="receiptId">XNH.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Số đề nghị; null nếu không có.</returns>
    internal static Task<string?> ActiveRequestNoAsync(ProcDbContext db, Guid receiptId, CancellationToken cancellationToken) =>
        (from l in db.Set<PaymentRequestLine>()
         join r in db.PaymentRequests on l.RequestId equals r.Id
         where l.ReceiptConfirmationId == receiptId && r.Status != ApprovalDocumentStatus.Cancelled
         select r.No).FirstOrDefaultAsync(cancellationToken);

    /// <summary>XNH đang thuộc đề nghị chưa hủy (trừ đề nghị <paramref name="exceptRequestId"/>).</summary>
    private IQueryable<Guid> HeldReceiptIds(Guid? exceptRequestId) =>
        from l in _db.Set<PaymentRequestLine>()
        join r in _db.PaymentRequests on l.RequestId equals r.Id
        where r.Status != ApprovalDocumentStatus.Cancelled && r.Id != exceptRequestId && l.ReceiptConfirmationId != null
        select l.ReceiptConfirmationId!.Value;

    /// <summary>XNH của PO → số đề nghị chưa hủy đang giữ (trừ đề nghị đang sửa).</summary>
    private async Task<Dictionary<Guid, string>> HeldByAsync(Guid orderId, Guid? exceptRequestId, CancellationToken cancellationToken) =>
        await (
                from l in _db.Set<PaymentRequestLine>()
                join r in _db.PaymentRequests on l.RequestId equals r.Id
                where r.PurchaseOrderId == orderId && r.Status != ApprovalDocumentStatus.Cancelled && r.Id != exceptRequestId
                      && l.ReceiptConfirmationId != null
                select new { ReceiptConfirmationId = l.ReceiptConfirmationId!.Value, r.No })
            .ToDictionaryAsync(x => x.ReceiptConfirmationId, x => x.No, cancellationToken);

    /// <summary>
    /// Nạp XNH cho các dòng gửi lên (người gọi đã khóa PO): XNH phải của PO, chưa thuộc đề nghị khác chưa hủy. Trạng thái, hóa đơn kiểm ở Domain.
    /// </summary>
    private async Task<List<(PaymentRequestLineData, PaymentRequestLineSource)>> ResolveLinesAsync(
        PurchaseOrder order, IReadOnlyList<SavePaymentRequestLineRequest> lines, Guid? requestId, CancellationToken cancellationToken)
    {
        if (lines.Any(l => l.ReceiptConfirmationId is null)) throw new BusinessRuleException("Có dòng chưa chọn phiếu nhận hàng.");
        var ids = lines.Select(l => l.ReceiptConfirmationId!.Value).Distinct().ToList();
        var sources = await _db.GoodsReceiptConfirmations.AsNoTracking().Where(c => ids.Contains(c.Id))
            .Select(c => new PaymentRequestLineSource(c.Id, c.No, c.PurchaseOrderId, c.Status, c.InvoiceNo, c.InvoiceSeries, c.InvoiceDate, c.InvoiceAmount))
            .ToDictionaryAsync(c => c.ReceiptConfirmationId, cancellationToken);
        if (sources.Count != ids.Count) throw new BusinessRuleException("Có phiếu nhận hàng không còn hoặc ngoài phạm vi công ty.");
        var heldBy = await HeldByAsync(order.Id, requestId, cancellationToken);
        foreach (var id in ids)
            if (heldBy.TryGetValue(id, out var no))
                throw new BusinessRuleException($"Phiếu nhận hàng {sources[id].ReceiptNo} đã ở đề nghị thanh toán {no} – mỗi phiếu nhận hàng chỉ đề nghị một lần.");
        return lines.Select(l => (new PaymentRequestLineData(l.ReceiptConfirmationId!.Value, l.Description), sources[l.ReceiptConfirmationId.Value])).ToList();
    }

    /// <summary>
    /// Lập đề nghị tổng hợp 1C (lát Y11b): khóa các bảng kê tới hết giao dịch để hai người cùng lập không gộp trùng bảng kê.
    /// </summary>
    private async Task<PaymentRequestDto> CreateSundryAsync(Guid companyId, SavePaymentRequestRequest request, CancellationToken cancellationToken)
    {
        var departmentId = request.DepartmentId ?? throw new BusinessRuleException("Hãy chọn bộ phận của các bảng kê tạp vụ.");
        if (!await _db.Departments.AnyAsync(d => d.Id == departmentId, cancellationToken))
            throw new BusinessRuleException("Bộ phận không có hoặc thuộc công ty khác.");
        await using var transaction = await RowLocks.BeginOrJoinAsync(_db, cancellationToken);
        var sheets = await ResolveSheetsAsync(request.Lines, null, cancellationToken);
        var no = await _numbers.NextAsync(DocumentTypes.PaymentRequest, request.Date, cancellationToken);
        var payment = PaymentRequest.CreateSundry(companyId, no, departmentId, ToHeader(request), request.PayeeName, ManualBank(request));
        payment.SetSundryLines(sheets);
        _db.PaymentRequests.Add(payment);
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetAsync(payment.Id, cancellationToken);
    }

    /// <summary>
    /// Nạp bảng kê cho các dòng 1C (khóa bảng kê trong giao dịch đang mở): bảng kê của công ty, chưa thuộc đề nghị chưa hủy khác. Trạng thái, bộ
    /// phận kiểm ở Domain.
    /// </summary>
    private async Task<List<PaymentRequestSundrySource>> ResolveSheetsAsync(
        IReadOnlyList<SavePaymentRequestLineRequest> lines, Guid? requestId, CancellationToken cancellationToken)
    {
        if (lines.Any(l => l.SundryExpenseSheetId is null)) throw new BusinessRuleException("Có dòng chưa chọn bảng kê tạp vụ.");
        var ids = lines.Select(l => l.SundryExpenseSheetId!.Value).Distinct().ToList();
        await RowLocks.LockAsync<SundryExpenseSheet>(_db, ids, cancellationToken);
        var sheets = await _db.SundryExpenseSheets.AsNoTracking().Where(s => ids.Contains(s.Id))
            .Select(s => new { s.Id, s.No, s.Date, s.DepartmentId, s.Status, s.TotalAmount })
            .ToDictionaryAsync(s => s.Id, cancellationToken);
        if (sheets.Count != ids.Count) throw new BusinessRuleException("Có bảng kê tạp vụ không còn hoặc ngoài phạm vi công ty.");
        var held = await HeldSheetsAsync(_db, ids, requestId, cancellationToken);
        foreach (var id in ids)
            if (held.TryGetValue(id, out var by))
                throw new BusinessRuleException($"Bảng kê {sheets[id].No} đã ở đề nghị thanh toán {by.No} – mỗi bảng kê chỉ đề nghị một lần.");
        return lines.Select(l =>
        {
            var s = sheets[l.SundryExpenseSheetId!.Value];
            return new PaymentRequestSundrySource(s.Id, s.No, s.Date, s.DepartmentId, s.Status, s.TotalAmount, l.Description);
        }).ToList();
    }

    /// <summary>Tài khoản người nhận nhập tay của 1C (chuyển khoản); null khi tiền mặt hoặc chưa nhập gì.</summary>
    private static PaymentBankAccount? ManualBank(SavePaymentRequestRequest request) =>
        ParseMethod(request.Method) != PaymentMethod.BankTransfer || (string.IsNullOrWhiteSpace(request.BankName) && string.IsNullOrWhiteSpace(request.BankAccountNumber))
            ? null
            : new PaymentBankAccount(Guid.Empty, request.BankName ?? string.Empty, request.BankBranch, request.BankAccountNumber ?? string.Empty, request.BankAccountName);

    /// <summary>Tài khoản NCC khi chuyển khoản: phải thuộc NCC của PO (danh mục hiện hành – đổi tài khoản đã qua duyệt NCC).</summary>
    private async Task<PaymentBankAccount?> BankAccountAsync(Guid supplierId, SavePaymentRequestRequest request, CancellationToken cancellationToken)
    {
        if (ParseMethod(request.Method) != PaymentMethod.BankTransfer || request.BankAccountId is not { } bankId) return null;
        var account = await _db.Set<SupplierBankAccount>().AsNoTracking().Where(b => b.Id == bankId && b.SupplierId == supplierId)
                          .Select(b => new PaymentBankAccount(b.Id, b.BankName, b.Branch, b.AccountNumber, b.AccountName)).SingleOrDefaultAsync(cancellationToken)
                      ?? throw new BusinessRuleException("Tài khoản ngân hàng không thuộc NCC của đơn mua hàng.");
        return account;
    }

    /// <summary>PO của công ty đang làm việc (theo dõi để domain đọc trạng thái).</summary>
    private async Task<PurchaseOrder> LoadOrderAsync(Guid orderId, CancellationToken cancellationToken) =>
        await _db.PurchaseOrders.AsNoTracking().SingleOrDefaultAsync(o => o.Id == orderId, cancellationToken)
        ?? throw new NotFoundException("Không tìm thấy đơn mua hàng.");

    /// <summary>Tải đề nghị kèm dòng (theo dõi thay đổi).</summary>
    private async Task<PaymentRequest> LoadAsync(Guid id, CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        return await _db.PaymentRequests.Include(r => r.Lines).SingleOrDefaultAsync(r => r.Id == id, cancellationToken)
               ?? throw new NotFoundException("Không tìm thấy đề nghị thanh toán.");
    }

    private void EnsureCanView()
    {
        _scope.RequireCompany();
        if (!new[] { Permissions.Purchase.PaymentView, Permissions.Purchase.PaymentCreate, Permissions.Purchase.PaymentApprove, Permissions.Purchase.PaymentPay }
                .Any(_scope.HasPermission))
            throw new ForbiddenAccessException("Bạn không có quyền xem đề nghị thanh toán.");
    }

    /// <summary>Người dùng hiện tại rút được phiếu về nháp không: phiếu chờ duyệt, là người lập hoặc có quyền Hủy phiếu.</summary>
    private bool CanRecall(PaymentRequest payment) =>
        payment.Status == ApprovalDocumentStatus.Submitted
        && (payment.CreatedBy == _scope.UserId || _scope.HasPermission(Permissions.System.DocumentCancel));

    private async Task<string?> DepartmentNameAsync(Guid? id, CancellationToken cancellationToken) =>
        id is { } d ? await _db.Departments.AsNoTracking().Where(x => x.Id == d).Select(x => x.Name).SingleOrDefaultAsync(cancellationToken) : null;

    private async Task<string?> ProjectCodeAsync(Guid? id, CancellationToken cancellationToken) =>
        id is { } p ? await _db.ConstructionProjects.AsNoTracking().Where(x => x.Id == p).Select(x => x.Code).SingleOrDefaultAsync(cancellationToken) : null;

    /// <summary>Dựng DTO chi tiết.</summary>
    private async Task<PaymentRequestDto> ToDtoAsync(PaymentRequest payment, CancellationToken cancellationToken)
    {
        // 1C (lát Y11b) không có PO, NCC.
        var order = await _db.PurchaseOrders.AsNoTracking().Where(o => o.Id == payment.PurchaseOrderId).Select(o => new { o.No, o.Date })
            .SingleOrDefaultAsync(cancellationToken);
        var supplier = await _db.Suppliers.AsNoTracking().Where(s => s.Id == payment.SupplierId).Select(s => new { s.Code, s.Name, s.Address })
            .SingleOrDefaultAsync(cancellationToken);
        var project = payment.ConstructionProjectId is { } projectId
            ? await _db.ConstructionProjects.AsNoTracking().Where(p => p.Id == projectId).Select(p => new { p.Code, p.Name }).SingleOrDefaultAsync(cancellationToken)
            : null;
        var receiptIds = payment.Lines.Select(l => l.ReceiptConfirmationId).OfType<Guid>().ToList();
        var receipts = await _db.GoodsReceiptConfirmations.AsNoTracking().Where(c => receiptIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => new { c.No, c.Date }, cancellationToken);
        var sheetIds = payment.Lines.Select(l => l.SundryExpenseSheetId).OfType<Guid>().ToList();
        var sheets = await _db.SundryExpenseSheets.AsNoTracking().Where(s => sheetIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.No, cancellationToken);
        var userIds = new[] { payment.CreatedBy, payment.PaidBy }.OfType<Guid>().Distinct().ToList();
        var users = await _db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken);
        var lines = payment.Lines.OrderBy(l => l.LineNo).Select(l =>
        {
            var receipt = l.ReceiptConfirmationId is { } rid ? receipts.GetValueOrDefault(rid) : null;
            return new PaymentRequestLineDto(l.Id, l.LineNo, l.ReceiptConfirmationId, receipt?.No, receipt?.Date, l.InvoiceNo, l.InvoiceSeries,
                l.InvoiceDate, l.Amount, l.Description, l.SundryExpenseSheetId,
                l.SundryExpenseSheetId is { } sid ? sheets.GetValueOrDefault(sid) : null);
        }).ToList();

        var step = payment.Status == ApprovalDocumentStatus.Submitted ? await _approvals.CurrentStepAsync(payment, cancellationToken) : null;
        var canApprove = step is not null && await _approvals.ApproveBlockerAsync(payment, step, cancellationToken) is null;
        string? readOnly = !_scope.HasPermission(Permissions.Purchase.PaymentCreate) ? "Bạn không có quyền lập, sửa đề nghị thanh toán." : null;
        var draft = payment.Status == ApprovalDocumentStatus.Draft;
        var actions = new PurchaseDocumentActions(
            draft && readOnly is null,
            draft && readOnly is null,
            canApprove,
            // Hủy: nháp – người lập; đã gửi duyệt trở đi – chỉ quản trị (design.md mục 20.2a); đã chi thì không hủy.
            payment.Status is not ApprovalDocumentStatus.Cancelled && !payment.IsPaid
                && DocumentCancelPolicy.CanCancel(_scope, draft, payment.CreatedBy),
            readOnly,
            CanRecall: CanRecall(payment));
        var canPay = payment.Status == ApprovalDocumentStatus.Approved && !payment.IsPaid && _scope.HasPermission(Permissions.Purchase.PaymentPay);

        return new PaymentRequestDto(
            payment.Id, payment.No, payment.Date, FormCode(payment.Form), payment.Status.ToString(), payment.PurchaseOrderId, order?.No, order?.Date,
            payment.SupplierId, supplier?.Code, supplier?.Name, supplier?.Address, await DepartmentNameAsync(payment.DepartmentId, cancellationToken),
            project?.Code, project?.Name, payment.Method.ToString(), payment.BankAccountId, payment.BankName, payment.BankBranch,
            payment.BankAccountNumber, payment.BankAccountName, payment.TotalAmount, payment.DeductionAmount, payment.DeductionReason,
            payment.PayableAmount, payment.Note, payment.PaidDate, payment.PaidBy is { } p ? users.GetValueOrDefault(p) : null,
            payment.PaidRecordedAt, payment.PaymentNote, canPay, payment.RejectReason, payment.CancelReason, payment.CreatedAt,
            payment.CreatedBy is { } c ? users.GetValueOrDefault(c) : null, payment.ApprovedAt, lines,
            await _approvals.HistoryAsync(DocumentTypes.PaymentRequest, payment.Id, cancellationToken, payment), actions, payment.Version,
            payment.PayeeName, payment.DepartmentId);
    }

    /// <summary>Đổi yêu cầu lưu thành đầu phiếu cho Domain.</summary>
    private static PaymentRequestHeader ToHeader(SavePaymentRequestRequest r) =>
        new(r.Date, ParseMethod(r.Method), r.DeductionAmount, r.DeductionReason, r.Note);

    /// <summary>Đọc hình thức: Cash / BankTransfer.</summary>
    private static PaymentMethod ParseMethod(string? method) => method?.Trim().ToUpperInvariant() switch
    {
        "CASH" => PaymentMethod.Cash,
        "BANKTRANSFER" => PaymentMethod.BankTransfer,
        _ => throw new BusinessRuleException("Hình thức thanh toán phải là tiền mặt (Cash) hoặc chuyển khoản (BankTransfer)."),
    };

    /// <summary>Biểu mẫu theo loại mua của PO (Y8-1).</summary>
    private static PaymentRequestForm FormOf(PurchaseOrder order) =>
        order.Category == PurchaseCategory.Construction ? PaymentRequestForm.Construction : PaymentRequestForm.Goods;

    /// <summary>Mã biểu mẫu hiển thị.</summary>
    internal static string FormCode(PaymentRequestForm form) => form switch
    {
        PaymentRequestForm.Construction => "5A",
        PaymentRequestForm.Sundry => "1C",
        _ => "5B",
    };
}
