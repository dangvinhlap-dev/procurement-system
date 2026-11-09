using Microsoft.EntityFrameworkCore;
using Proc.Application.Common;
using Proc.Application.Reports;
using Proc.Domain.Common;
using Proc.Domain.Documents;
using Proc.Domain.Inventory;
using Proc.Domain.MasterData;
using Proc.Domain.Organization;
using Proc.Domain.Pricing;
using Proc.Domain.Purchasing;
using Proc.Domain.Sourcing;
using Proc.Infrastructure.MasterData;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.Reports;

/// <summary>
/// Báo cáo PR chưa xử lý, mua hàng theo nhóm, đánh giá NCC, tiết kiệm từ đấu thầu (design.md mục 11, 11.3). Giá trị đều trước VAT, VND.
/// <para>
/// Xem toàn tập đoàn (quyền <c>Report.Purchase.ViewAllCompanies</c>, mục 9.2) thì bỏ filter công ty và filter kho ở truy vấn – phạm vi kho
/// khai báo theo từng công ty nên không áp được sang công ty khác; filter nhóm hàng vẫn giữ (mọi truy vấn đều join mặt hàng).
/// IgnoreQueryFilters áp cho cả câu truy vấn, nên chỉ gọi khi đã kiểm quyền; khi không xem toàn tập đoàn thì không bỏ filter nào.
/// </para>
/// </summary>
public sealed class PurchaseReportService : IPurchaseReportService
{
    /// <summary>Khoảng ngày tối đa mỗi lần xem (hai năm).</summary>
    public const int MaxDays = 731;

    private readonly ProcDbContext _db;
    private readonly ICurrentUserScope _scope;
    private readonly IClock _clock;

    /// <summary>Tạo dịch vụ.</summary>
    /// <param name="db">Ngữ cảnh CSDL (đã gắn phạm vi người dùng).</param>
    /// <param name="scope">Phạm vi người dùng.</param>
    /// <param name="clock">Đồng hồ.</param>
    public PurchaseReportService(ProcDbContext db, ICurrentUserScope scope, IClock clock)
    {
        _db = db;
        _scope = scope;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task<ReceiptQualityResult> GetReceiptQualityAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        if (to < from) throw new BusinessRuleException("Đến ngày phải sau từ ngày.");
        if (to.DayNumber - from.DayNumber > 366) throw new BusinessRuleException("Khoảng ngày tối đa 1 năm.");
        // Đi qua phiếu (filter công ty, kho) và mặt hàng (filter nhóm hàng) – chỉ thấy dòng trong phạm vi của người dùng.
        var lines = await (
                from l in _db.InventoryDocumentLines.AsNoTracking()
                join d in _db.InventoryDocuments on l.DocumentId equals d.Id
                join i in _db.Items on l.ItemId equals i.Id
                join u in _db.Uoms on l.UomId equals u.Id
                join s in _db.Suppliers on d.SupplierId equals s.Id
                where d.DocType == DocumentTypes.GoodsReceipt && d.Status == DocumentStatus.Posted && d.Date >= @from && d.Date <= to
                select new
                {
                    d.Id, d.No, d.Date, SupplierCode = s.Code, SupplierName = s.Name, d.PurchaseOrderId, l.LineNo, ItemCode = i.Code, ItemName = i.Name,
                    UomCode = u.Code, l.DeliveredQty, l.Qty, l.RejectedQty, l.QcResult, l.QcNote,
                })
            .ToListAsync(cancellationToken);
        var orderIds = lines.Select(l => l.PurchaseOrderId).OfType<Guid>().Distinct().ToList();
        var orderNos = await _db.PurchaseOrders.AsNoTracking().Where(o => orderIds.Contains(o.Id)).ToDictionaryAsync(o => o.Id, o => o.No, cancellationToken);
        var rows = lines
            .Select(l => new ReceiptQualityRowDto(
                l.Id, l.No, l.Date, l.SupplierCode, l.SupplierName, l.PurchaseOrderId is { } o ? orderNos.GetValueOrDefault(o) : null, l.LineNo,
                l.ItemCode, l.ItemName, l.UomCode, l.DeliveredQty, l.Qty, l.RejectedQty,
                // Như InventoryDocumentLine.DiscrepancyQty: thực nhận + không đạt − chứng từ giao.
                l.DeliveredQty is { } delivered ? l.Qty + l.RejectedQty - delivered : 0m, l.QcResult.ToString(), l.QcNote))
            .Where(r => r.RejectedQty > 0 || r.DiscrepancyQty != 0 || r.QcResult == nameof(QcResult.Failed))
            .OrderBy(r => r.Date).ThenBy(r => r.DocumentNo).ThenBy(r => r.LineNo)
            .ToList();
        var checkedLines = lines.Count(l => l.QcResult != QcResult.NotChecked);
        var passed = lines.Count(l => l.QcResult == QcResult.Passed);
        return new ReceiptQualityResult(rows, lines.Count, checkedLines, passed,
            checkedLines == 0 ? null : Math.Round(passed * 100m / checkedLines, 1, MidpointRounding.AwayFromZero));
    }

    /// <inheritdoc />
    public async Task<ThreeWayMatchResult> GetThreeWayMatchAsync(
        DateOnly from, DateOnly to, decimal tolerance, bool onlyIssues, CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        if (to < from) throw new BusinessRuleException("Đến ngày phải sau từ ngày.");
        if (to.DayNumber - from.DayNumber > 366) throw new BusinessRuleException("Khoảng ngày tối đa 1 năm.");
        if (tolerance < 0) throw new BusinessRuleException("Dung sai không được âm.");
        var (matched, hiddenCount) = await ThreeWayRowsAsync(_db, from, to, tolerance, false, cancellationToken);
        var all = matched.Select(m => m.Row).ToList();
        var rows = all.Where(r => !onlyIssues || r.Issues.Count > 0).OrderBy(r => r.Date).ThenBy(r => r.DocumentNo).ToList();
        return new ThreeWayMatchResult(
            rows, all.Count(r => r.Result == "Matched"), all.Count(r => r.Result == "NoInvoice"), all.Count(r => r.Result == "Mismatch"),
            all.Count(r => r.Issues.Count > 0), all.Sum(r => r.ReceiptTotal), all.Sum(r => r.InvoiceAmount ?? 0m), hiddenCount);
    }

    /// <summary>
    /// Đối chiếu từng phiếu nhập mua đã ghi sổ trong khoảng ngày (design.md mục 18.15) – dùng chung cho báo cáo MATCH3 và dấu hiệu bất thường
    /// KSNB (mục 18.19). Phiếu có mặt hàng ngoài phạm vi nhóm hàng của người xem bị bỏ qua cả phiếu.
    /// </summary>
    /// <param name="db">DbContext đã gắn phạm vi người dùng.</param>
    /// <param name="fromDate">Từ ngày nhập.</param>
    /// <param name="toDate">Đến ngày nhập.</param>
    /// <param name="tolerance">Dung sai tiền hóa đơn (đ).</param>
    /// <param name="allCompanies">Toàn tập đoàn – bỏ filter công ty, kho; người gọi phải kiểm quyền trước.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Các dòng kèm công ty của phiếu và số phiếu bỏ qua.</returns>
    internal static async Task<(List<(Guid CompanyId, ThreeWayMatchRowDto Row)> Rows, int Hidden)> ThreeWayRowsAsync(
        ProcDbContext db, DateOnly fromDate, DateOnly toDate, decimal tolerance, bool allCompanies, CancellationToken cancellationToken)
    {
        // Phiếu, dòng đi qua filter công ty, kho; mặt hàng đi qua filter nhóm hàng để biết phiếu nào người xem thấy đủ. Toàn tập đoàn
        // (người gọi đã kiểm quyền) thì bỏ filter công ty, kho – phạm vi kho khai báo theo từng công ty; nhóm hàng vẫn giữ.
        var documentSet = allCompanies
            ? db.InventoryDocuments.IgnoreQueryFilters([ProcDbContext.CompanyFilter, ProcDbContext.WarehouseFilter])
            : db.InventoryDocuments;
        var lineSet = allCompanies
            ? db.InventoryDocumentLines.IgnoreQueryFilters([ProcDbContext.CompanyFilter, ProcDbContext.WarehouseFilter])
            : db.InventoryDocumentLines;
        var orderSet = allCompanies ? db.PurchaseOrders.IgnoreQueryFilters([ProcDbContext.CompanyFilter]) : db.PurchaseOrders;
        var documents = await documentSet.AsNoTracking()
            .Where(d => d.DocType == DocumentTypes.GoodsReceipt && d.Status == DocumentStatus.Posted && d.Date >= fromDate && d.Date <= toDate)
            .Join(db.Suppliers, d => d.SupplierId, s => s.Id, (d, s) => new
            {
                d.Id, d.CompanyId, d.No, d.Date, SupplierCode = s.Code, SupplierName = s.Name, d.PurchaseOrderId, d.TotalAmount, d.TotalVat,
                d.InvoiceNo, d.InvoiceSeries, d.InvoiceDate, d.InvoiceAmount, d.Acceptance,
            })
            .ToListAsync(cancellationToken);
        var documentIds = documents.Select(d => d.Id).ToList();
        var allLines = await lineSet.AsNoTracking()
            .Where(l => documentIds.Contains(l.DocumentId))
            .Select(l => new { l.DocumentId, l.ItemId, l.Qty, l.DeliveredQty })
            .ToListAsync(cancellationToken);
        var itemIds = allLines.Select(l => l.ItemId).Distinct().ToList();
        // Chỉ đọc Id mặt hàng người xem thấy (filter nhóm hàng); phiếu có mặt hàng bị ẩn thì bỏ qua cả phiếu.
        var visibleItems = (await db.Items.AsNoTracking().Where(i => itemIds.Contains(i.Id)).Select(i => i.Id).ToListAsync(cancellationToken))
            .ToHashSet();
        var hidden = allLines.Where(l => !visibleItems.Contains(l.ItemId)).Select(l => l.DocumentId).ToHashSet();
        var shortDocuments = allLines.Where(l => l.DeliveredQty is { } delivered && l.Qty < delivered).Select(l => l.DocumentId).ToHashSet();

        var orderIds = documents.Select(d => d.PurchaseOrderId).OfType<Guid>().Distinct().ToList();
        var orders = await orderSet.AsNoTracking().Where(o => orderIds.Contains(o.Id))
            .Select(o => new { o.Id, o.No, o.Date, o.ApprovedAt, Total = o.TotalAmount + o.TotalVat })
            .ToDictionaryAsync(o => o.Id, cancellationToken);

        var all = new List<(Guid CompanyId, ThreeWayMatchRowDto Row)>();
        foreach (var d in documents.Where(d => !hidden.Contains(d.Id)))
        {
            var order = d.PurchaseOrderId is { } oid ? orders.GetValueOrDefault(oid) : null;
            DateOnly? approvedDate = order?.ApprovedAt is { } at
                ? DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(at, VietnamTime.Zone).DateTime)
                : null;
            var receiptTotal = d.TotalAmount + d.TotalVat;
            var hasInvoice = !string.IsNullOrWhiteSpace(d.InvoiceNo) && d.InvoiceAmount is not null;
            decimal? difference = d.InvoiceAmount is { } amount ? amount - receiptTotal : null;
            var issues = new List<string>();
            string result;
            if (!hasInvoice)
            {
                result = "NoInvoice";
                issues.Add(ThreeWayMatchIssues.NoInvoice);
            }
            else if (Math.Abs(difference!.Value) > tolerance)
            {
                result = "Mismatch";
                issues.Add(ThreeWayMatchIssues.AmountMismatch);
            }
            else result = "Matched";
            // Nhận thiếu/không đạt mà hóa đơn vẫn cao hơn tiền thực nhận: NCC tính tiền cả hàng không nhập kho (V7, V8).
            if (difference > tolerance && shortDocuments.Contains(d.Id)) issues.Add(ThreeWayMatchIssues.InvoiceIncludesShortage);
            if (order is null) issues.Add(ThreeWayMatchIssues.NoPurchaseOrder);
            else
            {
                // Kiểm soát chống hợp thức hóa: hàng về/hóa đơn có trước đơn – design.md mục 18.11, 18.15.
                if (d.Date < order.Date) issues.Add(ThreeWayMatchIssues.ReceivedBeforeOrder);
                else if (approvedDate is { } approved && d.Date < approved) issues.Add(ThreeWayMatchIssues.ReceivedBeforeApproval);
                if (d.InvoiceDate is { } invoiceDate && invoiceDate < order.Date) issues.Add(ThreeWayMatchIssues.InvoiceBeforeOrder);
            }
            // Đơn vị đặt hàng không đồng ý nghiệm thu: kế toán chờ Phòng cung ứng xử lý trước khi thanh toán (lát H4). Chưa xác nhận chỉ hiện ở cột
            // Nghiệm thu, không tính là cảnh báo – công ty chưa dùng bước nghiệm thu không bị báo hàng loạt.
            if (d.Acceptance == ReceiptAcceptance.Disputed) issues.Add(ThreeWayMatchIssues.AcceptanceDisputed);

            all.Add((d.CompanyId, new ThreeWayMatchRowDto(
                d.Id, d.No, d.Date, d.SupplierCode, d.SupplierName, d.PurchaseOrderId, order?.No, order?.Date, approvedDate, order?.Total,
                receiptTotal, d.InvoiceNo, d.InvoiceSeries, d.InvoiceDate, d.InvoiceAmount, difference, result, issues, d.Acceptance.ToString())));

        }

        return (all, hidden.Count);
    }

    /// <inheritdoc />
    public async Task<PurchaseSlaResult> GetPurchaseSlaAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var companyId = _scope.RequireCompany();
        if (to < from) throw new BusinessRuleException("Đến ngày phải sau từ ngày.");
        if (to.DayNumber - from.DayNumber > 366) throw new BusinessRuleException("Khoảng ngày tối đa 1 năm.");
        var today = _clock.TodayInVietnam();
        var parameter = await _db.CompanyParameters.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        var slaDays = new Dictionary<SlaCategory, int>
        {
            [SlaCategory.Priced] = parameter?.SlaPricedDays ?? CompanyParameter.DefaultSlaPricedDays,
            [SlaCategory.Sourcing] = parameter?.SlaSourcingDays ?? CompanyParameter.DefaultSlaSourcingDays,
            [SlaCategory.Urgent] = parameter?.SlaUrgentDays ?? CompanyParameter.DefaultSlaUrgentDays,
        };

        // Lấy rộng một ngày mỗi đầu rồi lọc theo ngày giờ Việt Nam (ApprovedAt lưu UTC).
        var fromUtc = new DateTimeOffset(from.AddDays(-1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var toUtc = new DateTimeOffset(to.AddDays(2).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var requests = (await _db.PurchaseRequests.AsNoTracking().Include(r => r.Lines).Include(r => r.NewItems).AsSplitQuery()
                .Where(r => (r.Status == ApprovalDocumentStatus.Approved || r.Status == ApprovalDocumentStatus.Closed)
                            && r.ApprovedAt != null && r.ApprovedAt >= fromUtc && r.ApprovedAt < toUtc)
                .ToListAsync(cancellationToken))
            .Select(r => (Request: r, Approved: DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(r.ApprovedAt!.Value, VietnamTime.Zone).DateTime)))
            .Where(x => x.Approved >= from && x.Approved <= to)
            .ToList();

        var itemIds = requests.SelectMany(x => x.Request.Lines).Select(l => l.ItemId).Distinct().ToList();
        var items = await _db.Items.AsNoTracking().Where(i => itemIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, i => new { i.Code, i.Name }, cancellationToken);
        var departmentIds = requests.Select(x => x.Request.DepartmentId).OfType<Guid>().Distinct().ToList();
        var departments = await _db.Departments.AsNoTracking().Where(d => departmentIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id, d => d.Name, cancellationToken);
        // Hàng "đã có hợp đồng/bảng giá": có trong bảng giá NCC hiệu lực vào ngày PR duyệt mà công ty dùng được (bảng của công ty hoặc toàn
        // tập đoàn – hợp đồng nguyên tắc luôn gắn một bảng giá). Bỏ filter công ty cho bảng toàn tập đoàn nên lọc công ty tường minh.
        var priced = await (
                from p in _db.SupplierPriceLists.IgnoreQueryFilters([ProcDbContext.CompanyFilter]).AsNoTracking()
                where (p.CompanyId == companyId || p.IsGroupWide)
                      && (p.Status == Proc.Domain.Pricing.PriceListStatus.Active || p.Status == Proc.Domain.Pricing.PriceListStatus.Inactive)
                      && p.ActivatedAt != null
                from l in p.Lines
                where itemIds.Contains(l.ItemId)
                select new { l.ItemId, p.ValidFrom, p.ValidTo, p.ActivatedAt })
            .ToListAsync(cancellationToken);
        var lineIds = requests.SelectMany(x => x.Request.Lines).Select(l => l.Id).ToList();
        var orders = (await (
                    from ol in _db.Set<PurchaseOrderLine>().AsNoTracking()
                    join o in _db.PurchaseOrders on ol.OrderId equals o.Id
                    where ol.SourcePrLineId != null && lineIds.Contains(ol.SourcePrLineId.Value)
                          && (o.Status == ApprovalDocumentStatus.Approved || o.Status == ApprovalDocumentStatus.Closed) && o.ApprovedAt != null
                    select new { PrLineId = ol.SourcePrLineId!.Value, o.Id, o.No, ApprovedAt = o.ApprovedAt!.Value })
                .ToListAsync(cancellationToken))
            .GroupBy(x => x.PrLineId)
            .ToDictionary(g => g.Key, g => g.MinBy(x => x.ApprovedAt)!);

        var rows = new List<PurchaseSlaRowDto>();
        foreach (var (pr, approved) in requests)
        {
            var department = pr.DepartmentId is { } d ? departments.GetValueOrDefault(d) : pr.LegacyDepartment;
            var urgent = pr.Priority == PurchasePriority.Urgent;
            foreach (var line in pr.Lines)
            {
                var hasPrice = priced.Any(p => p.ItemId == line.ItemId && p.ValidFrom <= approved && (p.ValidTo == null || p.ValidTo >= approved)
                                               && p.ActivatedAt <= pr.ApprovedAt);
                var category = urgent ? SlaCategory.Urgent : hasPrice ? SlaCategory.Priced : SlaCategory.Sourcing;
                var item = items.GetValueOrDefault(line.ItemId);
                orders.TryGetValue(line.Id, out var order);
                rows.Add(Row(pr, approved, department, line.LineNo, item?.Code ?? "(ngoài phạm vi)", item?.Name ?? "", category, order?.Id, order?.No,
                    order is null ? null : DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(order.ApprovedAt, VietnamTime.Zone).DateTime)));
            }

            // Hàng mới chưa gắn mã (mục 18.12): chưa có NCC được duyệt, chưa thể có PO.
            foreach (var n in pr.NewItems.Where(n => n.LinkedLineId is null))
                rows.Add(Row(pr, approved, department, n.LineNo, "(hàng mới)", n.Description, urgent ? SlaCategory.Urgent : SlaCategory.Sourcing, null, null, null));
        }

        var onTime = rows.Count(r => r.Status == "OnTime");
        var late = rows.Count(r => r.Status == "Late");
        var overdue = rows.Count(r => r.Status == "Overdue");
        var measured = onTime + late + overdue;
        return new PurchaseSlaResult(
            rows.OrderBy(r => r.DueDate).ThenBy(r => r.RequestNo).ThenBy(r => r.LineNo).ToList(),
            onTime, late, rows.Count(r => r.Status == "InProgress"), overdue,
            measured == 0 ? null : Math.Round(onTime * 100m / measured, 1, MidpointRounding.AwayFromZero));

        PurchaseSlaRowDto Row(
            PurchaseRequest pr, DateOnly approved, string? department, int lineNo, string code, string name, SlaCategory category, Guid? orderId,
            string? orderNo, DateOnly? orderDate)
        {
            var days = slaDays[category];
            var due = PurchaseSla.DueDate(approved, days);
            // Đúng hạn: PO được duyệt chậm nhất vào ngày đến hạn. Chưa có PO: còn hạn là đang xử lý, qua hạn là quá hạn.
            var status = orderDate is { } od ? (od <= due ? "OnTime" : "Late") : today <= due ? "InProgress" : "Overdue";
            return new PurchaseSlaRowDto(pr.Id, pr.No, approved, department, lineNo, code, name, category.ToString(), days, due, orderId, orderNo,
                orderDate, status);
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PendingRequestLineDto>> GetPendingRequestsAsync(CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        var today = _clock.TodayInVietnam();
        var lines = await (
                from l in _db.Set<PurchaseRequestLine>().AsNoTracking()
                join r in _db.PurchaseRequests on l.RequestId equals r.Id
                join i in _db.Items on l.ItemId equals i.Id
                join u in _db.Uoms on l.UomId equals u.Id
                where r.Status == ApprovalDocumentStatus.Submitted || r.Status == ApprovalDocumentStatus.Approved
                select new
                {
                    l.Id, RequestId = r.Id, r.No, r.Date, r.Status, r.Priority, l.LineNo, l.ItemId, ItemCode = i.Code, ItemName = i.Name,
                    UomCode = u.Code, l.Qty, l.UomFactor, l.BaseQty, l.EstPrice, NeededDate = l.NeededDate ?? r.NeededDate,
                    // Tên bộ phận theo danh mục; PR cũ chưa có danh mục thì hiện chữ ghi trước đây (design.md mục 18.5).
                    Department = _db.Departments.Where(d => d.Id == r.DepartmentId).Select(d => d.Name).FirstOrDefault() ?? r.LegacyDepartment,
                })
            .ToListAsync(cancellationToken);

        // SL đã lên PO chưa hủy theo dòng PR (ĐVT cơ sở) – cùng cách tính với màn hình lập PO từ PR (mục 6.7).
        var lineIds = lines.Select(l => l.Id).ToList();
        var ordered = await (
                from ol in _db.Set<PurchaseOrderLine>().AsNoTracking()
                join o in _db.PurchaseOrders on ol.OrderId equals o.Id
                where ol.SourcePrLineId != null && lineIds.Contains(ol.SourcePrLineId.Value) && o.Status != ApprovalDocumentStatus.Cancelled
                group ol by ol.SourcePrLineId!.Value into g
                select new { g.Key, Qty = g.Sum(x => x.BaseQty) })
            .ToDictionaryAsync(x => x.Key, x => x.Qty, cancellationToken);

        return lines
            .Select(l =>
            {
                // Quy SL cơ sở về ĐVT của dòng PR, ví dụ đề nghị 5 thùng (60 cái), đã đặt 24 cái → đã đặt 2 thùng, còn 3 thùng.
                var orderedQty = Math.Round(ordered.GetValueOrDefault(l.Id) / l.UomFactor, 4, MidpointRounding.AwayFromZero);
                var remaining = Math.Max(0, l.Qty - orderedQty);
                var overdue = l.NeededDate is { } needed ? Math.Max(0, today.DayNumber - needed.DayNumber) : 0;
                return new PendingRequestLineDto(
                    l.RequestId, l.No, l.Date, l.Department, l.Status.ToString(), l.Priority.ToString(), l.LineNo, l.ItemId, l.ItemCode, l.ItemName,
                    l.UomCode, l.Qty, orderedQty, remaining, Math.Round(remaining * l.EstPrice, 2, MidpointRounding.AwayFromZero), l.NeededDate, overdue);
            })
            .Where(l => l.RemainingQty > 0)
            .OrderBy(l => l.NeededDate is null).ThenBy(l => l.NeededDate).ThenBy(l => l.RequestNo).ThenBy(l => l.LineNo)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<PurchaseSpendResult> GetPurchaseSpendAsync(PurchaseReportPeriod period, string groupBy, CancellationToken cancellationToken)
    {
        if (!PurchaseSpendGroups.All.Contains(groupBy)) throw new BusinessRuleException("Cách nhóm không hợp lệ.");
        Check(period);
        var lines = await SupplierLinesAsync(period, cancellationToken);

        Func<SupplierLine, Guid> key = groupBy switch
        {
            PurchaseSpendGroups.Category => l => l.CategoryId,
            PurchaseSpendGroups.Item => l => l.ItemId,
            PurchaseSpendGroups.Company => l => l.CompanyId,
            _ => l => l.SupplierId,
        };
        var groups = lines.GroupBy(key).Select(g => new
            {
                Key = g.Key,
                ReceiptCount = g.Where(l => l.IsReceipt).Select(l => l.DocumentId).Distinct().Count(),
                Receipt = g.Where(l => l.IsReceipt).Sum(l => l.Amount),
                Return = g.Where(l => !l.IsReceipt).Sum(l => l.Amount),
            })
            .ToList();
        var names = await NamesAsync(groupBy, groups.Select(g => g.Key).ToList(), cancellationToken);
        var total = groups.Sum(g => g.Receipt - g.Return);
        var rows = groups
            .Select(g =>
            {
                var net = g.Receipt - g.Return;
                // Tỷ trọng, ví dụ 25.000.000 / 100.000.000 = 25,00%.
                var share = total == 0 ? 0 : Math.Round(net / total * 100, 2, MidpointRounding.AwayFromZero);
                var (code, name) = names.GetValueOrDefault(g.Key, ("?", "?"));
                return new PurchaseSpendRowDto(g.Key, code, name, g.ReceiptCount, g.Receipt, g.Return, net, share);
            })
            .OrderByDescending(r => r.NetAmount).ThenBy(r => r.Code)
            .ToList();
        return new PurchaseSpendResult(groupBy, period.AllCompanies, total, rows);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SupplierEvaluationRowDto>> GetSupplierEvaluationAsync(PurchaseReportPeriod period, CancellationToken cancellationToken)
    {
        Check(period);
        var lines = await SupplierLinesAsync(period, cancellationToken);

        // Dòng nhập theo PO: hạn giao = ngày giao của dòng PO, không có thì ngày giao dự kiến của PO; dòng không có hạn thì không chấm.
        var docLines = period.AllCompanies
            ? _db.InventoryDocumentLines.IgnoreQueryFilters([ProcDbContext.CompanyFilter, ProcDbContext.WarehouseFilter])
            : _db.InventoryDocumentLines;
        var deliveries = await (
                from l in docLines.AsNoTracking()
                join d in _db.InventoryDocuments on l.DocumentId equals d.Id
                join i in _db.Items on l.ItemId equals i.Id
                join pl in _db.Set<PurchaseOrderLine>() on l.PurchaseOrderLineId equals pl.Id
                join o in _db.PurchaseOrders on pl.OrderId equals o.Id
                where d.Status == DocumentStatus.Posted && d.DocType == DocumentTypes.GoodsReceipt && d.SupplierId != null
                      && d.Date >= period.From && d.Date <= period.To && (pl.DeliveryDate != null || o.ExpectedDate != null)
                select new { SupplierId = d.SupplierId!.Value, d.Date, Due = pl.DeliveryDate ?? o.ExpectedDate })
            .ToListAsync(cancellationToken);
        var delivery = deliveries
            .GroupBy(x => x.SupplierId)
            .ToDictionary(g => g.Key, g =>
            {
                var late = g.Select(x => x.Date.DayNumber - x.Due!.Value.DayNumber).Where(days => days > 0).ToList();
                return (Total: g.Count(), OnTime: g.Count() - late.Count, AvgLate: late.Count == 0 ? (decimal?)null : Math.Round((decimal)late.Average(), 1));
            });

        var names = await NamesAsync(PurchaseSpendGroups.Supplier, lines.Select(l => l.SupplierId).Distinct().ToList(), cancellationToken);
        // Ngưỡng xếp loại do người dùng cấu hình (mục 11.3), không code cứng.
        var rules = await _db.SupplierRatingRules.AsNoTracking().ToListAsync(cancellationToken);
        return lines
            .GroupBy(l => l.SupplierId)
            .Select(g =>
            {
                var receipt = g.Where(l => l.IsReceipt).Sum(l => l.Amount);
                var returned = g.Where(l => !l.IsReceipt).Sum(l => l.Amount);
                var d = delivery.GetValueOrDefault(g.Key);
                var (code, name) = names.GetValueOrDefault(g.Key, ("?", "?"));
                // Tỷ lệ trả, ví dụ nhập 10.000.000, trả 500.000 → 5,00%. Tỷ lệ đúng hạn, ví dụ 9/10 dòng → 90,00%.
                decimal? returnPct = receipt == 0 ? null : Math.Round(returned / receipt * 100, 2, MidpointRounding.AwayFromZero);
                decimal? onTimePct = d.Total == 0 ? null : Math.Round((decimal)d.OnTime / d.Total * 100, 2, MidpointRounding.AwayFromZero);
                return new SupplierEvaluationRowDto(
                    g.Key, code, name, g.Where(l => l.IsReceipt).Select(l => l.DocumentId).Distinct().Count(), receipt, returned, returnPct,
                    d.Total, d.OnTime, onTimePct, d.AvgLate, SupplierRating.Evaluate(rules, onTimePct, returnPct));
            })
            .OrderByDescending(r => r.ReceiptAmount).ThenBy(r => r.SupplierCode)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SourcingSavingRowDto>> GetSourcingSavingsAsync(PurchaseReportPeriod period, CancellationToken cancellationToken)
    {
        Check(period);
        var rfqs = period.AllCompanies ? _db.Rfqs.IgnoreQueryFilters([ProcDbContext.CompanyFilter]) : _db.Rfqs;
        var awarded = await (
                from r in rfqs.AsNoTracking()
                where r.Status == RfqStatus.Awarded && r.AwardDate >= period.From && r.AwardDate <= period.To
                from l in r.Lines
                where l.AwardedSupplierId != null
                join i in _db.Items on l.ItemId equals i.Id
                join u in _db.Uoms on l.UomId equals u.Id
                join c in _db.Companies on r.CompanyId equals c.Id
                select new
                {
                    RfqId = r.Id, r.No, r.Title, AwardDate = r.AwardDate!.Value, CompanyCode = c.Code, LineId = l.Id, ItemCode = i.Code,
                    ItemName = i.Name, UomCode = u.Code, l.Qty, SupplierId = l.AwardedSupplierId!.Value, l.AwardReason,
                })
            .ToListAsync(cancellationToken);
        var lineIds = awarded.Select(a => a.LineId).ToList();
        var quotes = (await (
                    from r in rfqs.AsNoTracking()
                    from s in r.Suppliers
                    from q in s.QuotationLines
                    where lineIds.Contains(q.RfqLineId)
                    select new { q.RfqLineId, s.SupplierId, q.UnitPrice, q.DiscountPct })
                .ToListAsync(cancellationToken))
            // Giá sau chiết khấu, ví dụ 600.000 CK 5% → 570.000; làm tròn 6 số lẻ theo numeric(19,6).
            .Select(q => (q.RfqLineId, q.SupplierId, Price: Math.Round(q.UnitPrice * (100m - q.DiscountPct) / 100m, 6, MidpointRounding.AwayFromZero)))
            .ToLookup(q => q.RfqLineId);
        var names = await NamesAsync(PurchaseSpendGroups.Supplier, awarded.Select(a => a.SupplierId).Distinct().ToList(), cancellationToken);

        var rows = new List<SourcingSavingRowDto>();
        foreach (var a in awarded)
        {
            var prices = quotes[a.LineId].ToList();
            var won = prices.Where(p => p.SupplierId == a.SupplierId).Select(p => (decimal?)p.Price).FirstOrDefault();
            if (won is not { } price) continue;
            var (min, max) = (prices.Min(p => p.Price), prices.Max(p => p.Price));
            var avg = Math.Round(prices.Average(p => p.Price), 6, MidpointRounding.AwayFromZero);
            var (code, name) = names.GetValueOrDefault(a.SupplierId, ("?", "?"));
            // Ví dụ 100 cái, chào 50.000 / 48.000 / 52.000, chọn 48.000: bình quân 50.000 → tiết kiệm 200.000; so cao nhất → 400.000.
            rows.Add(new SourcingSavingRowDto(
                a.RfqId, a.No, a.Title, a.AwardDate, a.CompanyCode, a.ItemCode, a.ItemName, a.UomCode, a.Qty, code, name, prices.Count,
                price, min, avg, max, Money(a.Qty * price), Money((avg - price) * a.Qty), Money((max - price) * a.Qty), price == min, a.AwardReason));
        }

        return rows.OrderByDescending(r => r.AwardDate).ThenBy(r => r.RfqNo).ThenBy(r => r.ItemCode).ToList();
    }

    /// <inheritdoc />
    public async Task<ContractComplianceResult> GetContractComplianceAsync(PurchaseReportPeriod period, CancellationToken cancellationToken)
    {
        Check(period);
        var lines = await OrderLinesAsync(period.From, period.To, period.AllCompanies, null, cancellationToken);
        var total = lines.Sum(l => l.Amount);
        var order = new[] { "MonthlyApproval", "Rfq", "Contract", "PriceList", "Deviation", "None", "Unknown" };
        var sources = lines.GroupBy(l => l.Kind)
            .Select(g => new PriceSourceSummaryDto(g.Key, g.Count(), g.Sum(l => l.Amount), Share(g.Sum(l => l.Amount), total)))
            .OrderBy(x => Array.IndexOf(order, x.Source))
            .ToList();

        // Tỷ lệ mua theo giá đã duyệt (P5, KPI ≥ 95%): dòng không xác định nguồn (PO trước 0.12.0) không tính vào mẫu số.
        // Ví dụ theo bảng giá 475.000, lệch giá 494.000, không có giá 300.000 → 475.000 ÷ 1.269.000 = 37,43%.
        var known = lines.Where(l => l.Kind != "Unknown").Sum(l => l.Amount);
        // Giá phiếu duyệt tháng (G3) cũng là giá đã duyệt.
        var compliant = lines.Where(l => l.Kind is "MonthlyApproval" or "Rfq" or "Contract" or "PriceList").Sum(l => l.Amount);
        decimal? compliancePct = known > 0 ? Math.Round(compliant * 100m / known, 2, MidpointRounding.AwayFromZero) : null;

        var outLines = lines
            .Where(l => l.Kind is "Deviation" or "None")
            .Select(l =>
            {
                var deviation = l.ApprovedNet is { } a ? (decimal?)Money((l.NetPrice - a) * l.Qty) : null;
                // % lệch so với giá đã duyệt, ví dụ 49.400 so 47.500 → 4,00%.
                var pct = l.ApprovedNet is { } b && b != 0 ? (decimal?)Math.Round((l.NetPrice - b) * 100m / b, 2, MidpointRounding.AwayFromZero) : null;
                return new OutOfContractLineDto(
                    l.OrderId, l.OrderNo, l.Date, l.CompanyCode, l.Department, l.SupplierCode, l.SupplierName, l.LineNo, l.ItemCode, l.ItemName,
                    l.UomCode, l.Qty, l.NetPrice, l.ApprovedNet, pct, l.Amount, deviation, l.Kind, l.Reason);
            })
            .OrderByDescending(l => Math.Abs(l.DeviationAmount ?? 0m)).ThenByDescending(l => l.Amount).ThenBy(l => l.OrderNo).ThenBy(l => l.LineNo)
            .ToList();
        return new ContractComplianceResult(lines.Select(l => l.OrderId).Distinct().Count(), total, compliancePct, sources, outLines);
    }

    /// <inheritdoc />
    public async Task<PriceVarianceResult> GetPriceVarianceAsync(PurchaseReportPeriod period, Guid? categoryId, CancellationToken cancellationToken)
    {
        Check(period);
        var (prevFrom, prevTo) = PreviousPeriod(period.From, period.To);
        List<Guid>? categoryIds = null;
        if (categoryId is { } c)
        {
            var parents = await _db.ItemCategories.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.ParentId, cancellationToken);
            categoryIds = parents.Keys.Where(id => ItemAdministration.IsUnder(id, c, parents)).ToList();
        }

        var lines = (await OrderLinesAsync(prevFrom, period.To, period.AllCompanies, categoryIds, cancellationToken)).Where(l => l.BaseQty > 0).ToList();
        var previous = lines.Where(l => l.Date <= prevTo).GroupBy(l => l.ItemId)
            .ToDictionary(g => g.Key, g => (Qty: g.Sum(l => l.BaseQty), Amount: g.Sum(l => l.Amount)));

        // Giá bình quân gia quyền theo ĐVT cơ sở = Σ thành tiền ÷ Σ SL cơ sở (6 số lẻ). Ví dụ 10 cái 475.000 + 10 cái 494.000 + 5 cái 300.000
        // → 1.269.000 ÷ 25 = 50.760; kỳ trước 45.000 → +12,80%, ảnh hưởng (50.760 − 45.000) × 25 = 144.000.
        static decimal Avg(decimal amount, decimal qty) => Math.Round(amount / qty, 6, MidpointRounding.AwayFromZero);
        var rows = lines.Where(l => l.Date >= period.From).GroupBy(l => l.ItemId)
            .Select(g =>
            {
                var first = g.First();
                var qty = g.Sum(l => l.BaseQty);
                var avg = Avg(g.Sum(l => l.Amount), qty);
                var unitPrices = g.Select(l => Avg(l.Amount, l.BaseQty)).ToList();
                var prev = previous.GetValueOrDefault(g.Key);
                decimal? prevAvg = prev.Qty > 0 ? Avg(prev.Amount, prev.Qty) : null;
                decimal? change = prevAvg is { } pa && pa != 0 ? Math.Round((avg - pa) * 100m / pa, 2, MidpointRounding.AwayFromZero) : null;
                decimal? impact = prevAvg is { } pb ? Money((avg - pb) * qty) : null;
                return new PriceVarianceRowDto(
                    g.Key, first.ItemCode, first.ItemName, first.BaseUomCode, prev.Qty, prevAvg, qty, avg, unitPrices.Min(), unitPrices.Max(), change,
                    impact, g.Select(l => l.OrderId).Distinct().Count(), g.Select(l => l.SupplierCode).Distinct().Count());
            })
            .OrderByDescending(r => Math.Abs(r.ImpactAmount ?? 0m)).ThenBy(r => r.ItemCode)
            .ToList();
        return new PriceVarianceResult(prevFrom, prevTo, rows.Sum(r => r.ImpactAmount ?? 0m), rows);
    }

    /// <inheritdoc />
    public async Task<StandardSavingResult> GetStandardSavingsAsync(PurchaseReportPeriod period, Guid? categoryId, CancellationToken cancellationToken)
    {
        Check(period);
        List<Guid>? categoryIds = null;
        if (categoryId is { } c)
        {
            var parents = await _db.ItemCategories.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.ParentId, cancellationToken);
            categoryIds = parents.Keys.Where(id => ItemAdministration.IsUnder(id, c, parents)).ToList();
        }

        // Toàn tập đoàn đã kiểm quyền ở Check; filter nhóm hàng giữ nhờ join mặt hàng. Phiếu tháng thuộc tổng công ty nên bỏ filter công ty,
        // nhóm hàng của phiếu – chỉ đọc đúng dòng phiếu đã ghi trên dòng PO người dùng xem được (như PurchaseOrderService).
        // Phiếu tháng tính bằng VND nên chỉ xét PO bằng VND (như cảnh báo vượt trần G4b).
        var orders = period.AllCompanies ? _db.PurchaseOrders.IgnoreQueryFilters([ProcDbContext.CompanyFilter]) : _db.PurchaseOrders;
        var items = categoryIds is null ? _db.Items : _db.Items.Where(i => categoryIds.Contains(i.CategoryId));
        var monthly =
            from ml in _db.Set<MonthlySupplierApprovalLine>()
            join a in _db.MonthlySupplierApprovals.IgnoreQueryFilters([ProcDbContext.CompanyFilter, ProcDbContext.ItemCategoryFilter]) on ml.ApprovalId equals a.Id
            select new { ml.Id, a.No, ml.UomFactor, ml.StandardPrice, ml.CeilingPrice, ml.TargetPrice };
        var rows = await (
                from o in orders.AsNoTracking()
                where (o.Status == ApprovalDocumentStatus.Approved || o.Status == ApprovalDocumentStatus.Closed)
                      && o.Date >= period.From && o.Date <= period.To && o.CurrencyCode == "VND"
                from l in o.Lines
                join i in items on l.ItemId equals i.Id
                join bu in _db.Uoms on i.BaseUomId equals bu.Id
                join m in monthly on l.MonthlyApprovalLineId equals (Guid?)m.Id into ms
                from m in ms.DefaultIfEmpty()
                select new
                {
                    OrderId = o.Id, l.ItemId, ItemCode = i.Code, ItemName = i.Name, BaseUomCode = bu.Code, l.Qty, l.BaseQty, l.UomFactor,
                    l.UnitPrice, l.DiscountPct, l.Amount, ApprovalNo = m == null ? null : m.No, MonthlyFactor = m == null ? (decimal?)null : m.UomFactor,
                    Standard = m == null ? null : m.StandardPrice, Ceiling = m == null ? null : m.CeilingPrice, Target = m == null ? null : m.TargetPrice,
                })
            .ToListAsync(cancellationToken);

        // Giá phiếu tháng quy về ĐVT dòng PO = giá ÷ hệ số ĐVT phiếu × hệ số ĐVT dòng (6 số lẻ) – như giá tháng/giá trần (G3, G4b).
        // Ví dụ giá chuẩn 146.400/thùng 12 cái, PO tính theo cái → 12.200/cái.
        static decimal? ToLine(decimal? price, decimal? monthlyFactor, decimal lineFactor) =>
            price is { } p && monthlyFactor is { } f && f != 0 ? Math.Round(p / f * lineFactor, 6, MidpointRounding.AwayFromZero) : null;
        static decimal Net(decimal price, decimal discount) => Math.Round(price * (100m - discount) / 100m, 6, MidpointRounding.AwayFromZero);
        static decimal? Pct(decimal part, decimal total) => total == 0 ? null : Math.Round(part * 100m / total, 2, MidpointRounding.AwayFromZero);
        static decimal Avg(decimal amount, decimal qty) => qty == 0 ? 0 : Math.Round(amount / qty, 6, MidpointRounding.AwayFromZero);

        var covered = rows
            .Select(r => new
            {
                Row = r,
                Standard = ToLine(r.Standard, r.MonthlyFactor, r.UomFactor),
                Target = ToLine(r.Target, r.MonthlyFactor, r.UomFactor),
                Ceiling = ToLine(r.Ceiling, r.MonthlyFactor, r.UomFactor),
            })
            .Where(x => x.Standard is not null)
            .ToList();

        // Tiết kiệm = Σ giá chuẩn (ĐVT dòng) × SL − Σ thành tiền. Ví dụ chuẩn 12.200/cái: 60 cái × 12.000 = 720.000 so 732.000 → +12.000;
        // 39 cái × 12.500 = 487.500 so 475.800 → −11.700; mặt hàng tiết kiệm 300 đ = 300 ÷ 1.207.800 = 0,02%.
        var result = covered.GroupBy(x => x.Row.ItemId)
            .Select(g =>
            {
                var first = g.First().Row;
                var qty = g.Sum(x => x.Row.BaseQty);
                var amount = g.Sum(x => x.Row.Amount);
                var standard = g.Sum(x => Money(x.Standard!.Value * x.Row.Qty));
                var withTarget = g.Where(x => x.Target is not null).ToList();
                decimal? targetAmount = withTarget.Count > 0 ? withTarget.Sum(x => Money(x.Target!.Value * x.Row.Qty)) : null;
                decimal? targetGap = targetAmount is { } t ? withTarget.Sum(x => x.Row.Amount) - t : null;
                var over = g.Count(x => x.Ceiling is { } ceiling && Net(x.Row.UnitPrice, x.Row.DiscountPct) > ceiling);
                var nos = string.Join("; ", g.Select(x => x.Row.ApprovalNo!).Distinct().Order(StringComparer.Ordinal));
                return new StandardSavingRowDto(
                    g.Key, first.ItemCode, first.ItemName, first.BaseUomCode, qty, Avg(amount, qty), Avg(standard, qty), amount, standard,
                    standard - amount, Pct(standard - amount, standard), targetAmount, targetGap, over, g.Select(x => x.Row.OrderId).Distinct().Count(), nos);
            })
            .OrderByDescending(r => r.Saving).ThenBy(r => r.ItemCode, StringComparer.Ordinal)
            .ToList();

        var total = rows.Sum(r => r.Amount);
        var coveredAmount = result.Sum(r => r.Amount);
        var standardAmount = result.Sum(r => r.StandardAmount);
        var saving = standardAmount - coveredAmount;
        return new StandardSavingResult(total, coveredAmount, Pct(coveredAmount, total), standardAmount, saving, Pct(saving, standardAmount), result);
    }

    /// <summary>
    /// Kỳ so sánh liền trước kỳ này: khoảng trọn tháng (từ ngày 1 đến cuối tháng) thì lùi cùng số tháng – tháng 10 → tháng 9, quý IV → quý III;
    /// còn lại lùi cùng số ngày – 09/11–15/11 → 02/11–08/11.
    /// </summary>
    /// <param name="from">Đầu kỳ này.</param>
    /// <param name="to">Cuối kỳ này.</param>
    /// <returns>Đầu, cuối kỳ trước.</returns>
    public static (DateOnly From, DateOnly To) PreviousPeriod(DateOnly from, DateOnly to)
    {
        if (from.Day == 1 && to.AddDays(1).Day == 1)
        {
            var months = (to.Year * 12 + to.Month) - (from.Year * 12 + from.Month) + 1;
            return (from.AddMonths(-months), from.AddDays(-1));
        }

        var days = to.DayNumber - from.DayNumber + 1;
        return (from.AddDays(-days), from.AddDays(-1));
    }

    /// <inheritdoc />
    public async Task<SupplierCoverageResult> GetSupplierCoverageAsync(int minSuppliers, Guid? categoryId, bool onlyShort, CancellationToken cancellationToken)
    {
        if (minSuppliers is < 1 or > 20) throw new BusinessRuleException("Số NCC tối thiểu từ 1 đến 20.");
        var today = _clock.TodayInVietnam();
        // Mặt hàng, NCC – sản phẩm đi qua filter phạm vi nhóm hàng; phiếu độc quyền qua filter công ty đang làm việc (CLAUDE.md mục 4).
        var items = await _db.Items.AsNoTracking().Where(i => i.IsActive)
            .Join(_db.ItemCategories.AsNoTracking(), i => i.CategoryId, c => c.Id, (i, c) => new { i.Id, i.CategoryId, CategoryCode = c.Code, CategoryName = c.Name })
            .ToListAsync(cancellationToken);
        if (categoryId is { } root)
        {
            var parents = await _db.ItemCategories.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.ParentId, cancellationToken);
            items = items.Where(i => ItemAdministration.IsUnder(i.CategoryId, root, parents)).ToList();
        }

        // Đếm theo nhóm hàng trực tiếp của mặt hàng (Q1 08/11/2026 – lát G1b): NCC có ngành hàng là nhóm/nhóm cha hoặc NCC – sản phẩm
        // của mặt hàng trong nhóm (QualifiedSuppliers).
        var groups = items.GroupBy(i => (i.CategoryId, i.CategoryCode, i.CategoryName)).ToList();
        var candidates = await QualifiedSuppliers.ByCategoryAsync(_db, groups.Select(g => g.Key.CategoryId).ToList(), cancellationToken);
        var soleSources = (await _db.SoleSourceApprovals.AsNoTracking()
                .Where(x => x.Status == SoleSourceStatus.Approved && x.ValidFrom <= today && x.ReviewDate >= today)
                .Select(x => new { x.ItemId, x.No, x.ValidFrom })
                .ToListAsync(cancellationToken))
            .GroupBy(x => x.ItemId).ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.ValidFrom).First().No);

        var rows = groups.Select(g =>
            {
                var l = candidates[g.Key.CategoryId];
                var approved = l.Where(x => x.Status == SupplierStatus.Approved).OrderByDescending(x => x.IsPreferred).ThenBy(x => x.Name).ToList();
                var sole = g.Select(i => soleSources.GetValueOrDefault(i.Id)).ToList();
                // Độc quyền đã duyệt thì không thể có đủ NCC (design.md mục 18.9) – nhưng phiếu độc quyền theo mặt hàng: nhóm chỉ được miễn
                // khi mọi mặt hàng đang dùng của nhóm đều có phiếu còn tác dụng.
                var status = approved.Count >= minSuppliers ? SupplierCoverageStatuses.Ok
                    : sole.All(n => n is not null) ? SupplierCoverageStatuses.SoleSource : SupplierCoverageStatuses.Short;
                var missing = status == SupplierCoverageStatuses.Short ? minSuppliers - approved.Count : 0;
                var soleNos = sole.OfType<string>().Distinct().Order(StringComparer.Ordinal).ToList();
                return new SupplierCoverageRowDto(g.Key.CategoryId, g.Key.CategoryCode, g.Key.CategoryName, g.Count(), approved.Count,
                    l.Count(x => x.Status == SupplierStatus.Potential), l.Count(x => x.Status == SupplierStatus.Suspended), missing, status,
                    string.Join("; ", approved.Select(x => x.Name)), sole.Count(n => n is not null), soleNos.Count == 0 ? null : string.Join("; ", soleNos));
            })
            .ToList();
        var shortCount = rows.Count(r => r.Status == SupplierCoverageStatuses.Short);
        var shown = rows.Where(r => !onlyShort || r.Status == SupplierCoverageStatuses.Short)
            .OrderByDescending(r => r.Missing).ThenBy(r => r.CategoryCode, StringComparer.Ordinal).ToList();
        return new SupplierCoverageResult(minSuppliers, rows.Count, shortCount, shown);
    }

    private static decimal Share(decimal part, decimal total) => total == 0 ? 0 : Math.Round(part * 100m / total, 2, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Một dòng PO đã duyệt cho báo cáo giá mua. <c>Kind</c>: nguồn giá (Rfq, Contract, PriceList, None, Unknown), riêng dòng lệch giá đã
    /// duyệt là Deviation – không còn mua theo giá được duyệt nên không tính là theo nguồn đó.
    /// </summary>
    internal sealed record OrderLine(
        Guid OrderId, string OrderNo, DateOnly Date, string CompanyCode, string? Department, string SupplierCode, string SupplierName, int LineNo,
        Guid ItemId, string ItemCode, string ItemName, string BaseUomCode, string UomCode, decimal Qty, decimal BaseQty, decimal NetPrice,
        decimal Amount, string Kind, decimal? ApprovedNet, string? Reason)
    {
        /// <summary>Công ty lập PO.</summary>
        public Guid CompanyId { get; init; }

        /// <summary>NCC.</summary>
        public Guid SupplierId { get; init; }

        /// <summary>Người lập PO.</summary>
        public Guid? CreatedBy { get; init; }
    }

    /// <summary>
    /// Dòng PO Đã duyệt hoặc Đã đóng có ngày PO trong khoảng – đơn hàng đã cam kết với NCC. PO nháp/chờ duyệt chưa phải giá mua; PO hủy không mua.
    /// </summary>
    private Task<List<OrderLine>> OrderLinesAsync(
        DateOnly fromDate, DateOnly toDate, bool allCompanies, List<Guid>? categoryIds, CancellationToken cancellationToken) =>
        OrderLinesAsync(_db, fromDate, toDate, allCompanies, categoryIds, cancellationToken);

    /// <summary>
    /// Như bản không tĩnh – dùng chung cho báo cáo KSNB (design.md mục 18.19). <paramref name="allCompanies"/> bỏ filter công ty nên người
    /// gọi phải kiểm quyền xem toàn tập đoàn trước.
    /// </summary>
    internal static async Task<List<OrderLine>> OrderLinesAsync(
        ProcDbContext db, DateOnly fromDate, DateOnly toDate, bool allCompanies, List<Guid>? categoryIds, CancellationToken cancellationToken)
    {
        // Toàn tập đoàn (đã kiểm quyền ở Check) – xem ghi chú lớp; filter nhóm hàng giữ nhờ join mặt hàng.
        var orders = allCompanies ? db.PurchaseOrders.IgnoreQueryFilters([ProcDbContext.CompanyFilter]) : db.PurchaseOrders;
        var items = categoryIds is null ? db.Items : db.Items.Where(i => categoryIds.Contains(i.CategoryId));
        var rows = await (
                from o in orders.AsNoTracking()
                where (o.Status == ApprovalDocumentStatus.Approved || o.Status == ApprovalDocumentStatus.Closed) && o.Date >= fromDate && o.Date <= toDate
                from l in o.Lines
                join i in items on l.ItemId equals i.Id
                join bu in db.Uoms on i.BaseUomId equals bu.Id
                join u in db.Uoms on l.UomId equals u.Id
                join s in db.Suppliers on o.SupplierId equals s.Id
                join c in db.Companies on o.CompanyId equals c.Id
                select new
                {
                    OrderId = o.Id, o.No, o.Date, o.CompanyId, o.SupplierId, o.CreatedBy, CompanyCode = c.Code,
                    Department = db.Departments.Where(d => d.Id == o.DepartmentId).Select(d => d.Name).FirstOrDefault(),
                    SupplierCode = s.Code, SupplierName = s.Name, l.LineNo, l.ItemId, ItemCode = i.Code, ItemName = i.Name, BaseUomCode = bu.Code,
                    UomCode = u.Code, l.Qty, l.BaseQty, l.UnitPrice, l.DiscountPct, l.Amount, l.PriceSource, l.ApprovedUnitPrice,
                    l.ApprovedDiscountPct, l.PriceDeviationReason,
                })
            .ToListAsync(cancellationToken);
        // Giá sau chiết khấu làm tròn 6 số như PurchaseOrderLine.NetUnitPrice / IsPriceDeviation (design.md mục 18.10).
        static decimal Net(decimal price, decimal discount) => Math.Round(price * (100m - discount) / 100m, 6, MidpointRounding.AwayFromZero);
        return rows.Select(r =>
            {
                var net = Net(r.UnitPrice, r.DiscountPct);
                decimal? approved = r.ApprovedUnitPrice is { } a ? Net(a, r.ApprovedDiscountPct ?? 0m) : null;
                var kind = approved is { } ap && ap != net ? "Deviation" : r.PriceSource.ToString();
                return new OrderLine(
                    r.OrderId, r.No, r.Date, r.CompanyCode, r.Department, r.SupplierCode, r.SupplierName, r.LineNo, r.ItemId, r.ItemCode, r.ItemName,
                    r.BaseUomCode, r.UomCode, r.Qty, r.BaseQty, net, r.Amount, kind, approved, r.PriceDeviationReason)
                {
                    CompanyId = r.CompanyId, SupplierId = r.SupplierId, CreatedBy = r.CreatedBy,
                };
            })
            .ToList();
    }

    private static decimal Money(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    /// <summary>Kiểm tra khoảng ngày và quyền xem toàn tập đoàn.</summary>
    private void Check(PurchaseReportPeriod period)
    {
        _scope.RequireCompany();
        if (period.From > period.To) throw new BusinessRuleException("Từ ngày phải trước hoặc bằng đến ngày.");
        if (period.To.DayNumber - period.From.DayNumber >= MaxDays) throw new BusinessRuleException("Báo cáo xem tối đa hai năm mỗi lần.");
        if (period.AllCompanies) _scope.EnsurePermission(Permissions.Report.PurchaseViewAllCompanies);
    }

    /// <summary>Một dòng phiếu nhập mua NCC (IsReceipt) hoặc phiếu trả NCC đã ghi sổ, giá trị trước VAT.</summary>
    private sealed record SupplierLine(Guid DocumentId, Guid CompanyId, Guid SupplierId, Guid ItemId, Guid CategoryId, bool IsReceipt, decimal Amount);

    /// <summary>
    /// Dòng phiếu nhập mua NCC và trả NCC đã ghi sổ trong khoảng ngày. Không gồm nhập mua nội bộ (ICR): đó là mua giữa các công ty trong tập
    /// đoàn, không phải chi tiêu với NCC bên ngoài.
    /// </summary>
    private async Task<List<SupplierLine>> SupplierLinesAsync(PurchaseReportPeriod period, CancellationToken cancellationToken)
    {
        // Toàn tập đoàn (đã kiểm quyền ở Check) – xem ghi chú lớp.
        var docLines = period.AllCompanies
            ? _db.InventoryDocumentLines.IgnoreQueryFilters([ProcDbContext.CompanyFilter, ProcDbContext.WarehouseFilter])
            : _db.InventoryDocumentLines;
        return await (
                from l in docLines.AsNoTracking()
                join d in _db.InventoryDocuments on l.DocumentId equals d.Id
                join i in _db.Items on l.ItemId equals i.Id
                where d.Status == DocumentStatus.Posted && d.SupplierId != null
                      && (d.DocType == DocumentTypes.GoodsReceipt || d.DocType == DocumentTypes.ReturnToVendor)
                      && d.Date >= period.From && d.Date <= period.To
                select new SupplierLine(d.Id, d.CompanyId, d.SupplierId!.Value, l.ItemId, i.CategoryId, d.DocType == DocumentTypes.GoodsReceipt, l.Amount))
            .ToListAsync(cancellationToken);
    }

    /// <summary>Mã, tên theo cách nhóm.</summary>
    private async Task<Dictionary<Guid, (string Code, string Name)>> NamesAsync(string groupBy, List<Guid> ids, CancellationToken cancellationToken) =>
        groupBy switch
        {
            PurchaseSpendGroups.Category => await _db.ItemCategories.AsNoTracking().Where(c => ids.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id, c => (c.Code, c.Name), cancellationToken),
            PurchaseSpendGroups.Item => await _db.Items.AsNoTracking().Where(i => ids.Contains(i.Id))
                .ToDictionaryAsync(i => i.Id, i => (i.Code, i.Name), cancellationToken),
            PurchaseSpendGroups.Company => await _db.Companies.AsNoTracking().Where(c => ids.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id, c => (c.Code, c.Name), cancellationToken),
            _ => await _db.Suppliers.AsNoTracking().Where(s => ids.Contains(s.Id))
                .ToDictionaryAsync(s => s.Id, s => (s.Code, s.Name), cancellationToken),
        };
}
