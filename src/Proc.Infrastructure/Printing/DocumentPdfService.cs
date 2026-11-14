using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Proc.Application.Common;
using Proc.Application.Inventory;
using Proc.Application.Printing;
using Proc.Application.Purchasing;
using Proc.Application.Sourcing;
using Proc.Domain.Common;
using Proc.Domain.Documents;
using Proc.Domain.Purchasing;
using Proc.Infrastructure.Persistence;
using Proc.Infrastructure.Purchasing;

namespace Proc.Infrastructure.Printing;

/// <summary>
/// In phiếu PDF (design.md mục 10.10). Đọc phiếu qua dịch vụ xem phiếu của module (giữ phạm vi công ty, kho và quyền), rồi đổ vào khuôn
/// <see cref="PdfLayout"/>. Số theo kiểu Việt Nam, tiền VND không số lẻ (CLAUDE.md mục 5).
/// </summary>
public sealed class DocumentPdfService : IDocumentPdfService
{
    private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

    private readonly ProcDbContext _db;
    private readonly ICurrentUserScope _scope;
    private readonly IPurchaseOrderService _orders;
    private readonly IPurchaseRequestService _requests;
    private readonly IInventoryDocumentService _inventory;
    private readonly IPurchaseSummaryService _summaries;
    private readonly IRfqService _rfqs;
    private readonly IPaymentRequestService _payments;
    private readonly ISundryExpenseSheetService _sundries;
    private readonly IReceiptConfirmationService _receipts;

    /// <summary>Tạo dịch vụ.</summary>
    /// <param name="db">DbContext (đọc thông tin công ty, tên kho).</param>
    /// <param name="scope">Phạm vi người dùng.</param>
    /// <param name="orders">Dịch vụ đơn mua hàng.</param>
    /// <param name="requests">Dịch vụ đề nghị mua hàng.</param>
    /// <param name="inventory">Dịch vụ chứng từ kho.</param>
    /// <param name="summaries">Dịch vụ bảng tổng hợp đề xuất mua.</param>
    /// <param name="rfqs">Dịch vụ yêu cầu báo giá.</param>
    /// <param name="payments">Dịch vụ đề nghị thanh toán (lát Y8).</param>
    /// <param name="sundries">Bảng kê chi phí tạp vụ (in 2C – lát Y11c).</param>
    /// <param name="receipts">Phiếu xác nhận nhận hàng XNH (lát K2b).</param>
    /// <param name="options">Cấu hình in (thư mục phông).</param>
    public DocumentPdfService(
        ProcDbContext db, ICurrentUserScope scope, IPurchaseOrderService orders, IPurchaseRequestService requests, IInventoryDocumentService inventory,
        IPurchaseSummaryService summaries, IRfqService rfqs, IPaymentRequestService payments, ISundryExpenseSheetService sundries,
        IReceiptConfirmationService receipts, IOptions<PrintingOptions> options)
    {
        try
        {
            PdfFonts.Install(options.Value.FontDirectories);
        }
        catch (InvalidOperationException ex)
        {
            // Thiếu phông là lỗi cài đặt server: báo rõ cách sửa cho người dùng/quản trị thay vì lỗi máy chủ chung chung.
            throw new BusinessRuleException(ex.Message);
        }

        _db = db;
        _scope = scope;
        _orders = orders;
        _requests = requests;
        _inventory = inventory;
        _summaries = summaries;
        _rfqs = rfqs;
        _payments = payments;
        _sundries = sundries;
        _receipts = receipts;
    }

    /// <inheritdoc />
    public async Task<PdfFile> SundryExpenseSheetAsync(Guid id, CancellationToken cancellationToken)
    {
        var sheet = await _sundries.GetAsync(id, cancellationToken);
        var company = await CompanyAsync(cancellationToken);
        return new PdfFile($"{sheet.No}.pdf", CuvtFormLayout.Render(CuvtPrintForms.SundryExpenseSheet(sheet, company.Name)));
    }

    /// <inheritdoc />
    public async Task<PdfFile> PaymentRequestAsync(Guid id, CancellationToken cancellationToken)
    {
        // Đọc đề nghị qua dịch vụ (quyền xem đề nghị); PO đọc thẳng qua filter công ty – người duyệt, kế toán không cần quyền xem PO để in.
        var payment = await _payments.GetAsync(id, cancellationToken);
        if (payment.PurchaseOrderId is null)
        {
            // 1C tạp vụ (lát Y11b): không có PO – in theo bảng kê.
            var sundry = CuvtPrintForms.SundryPaymentRequest(payment, (await CompanyAsync(cancellationToken)).Name);
            return new PdfFile($"{payment.No}.pdf", CuvtFormLayout.Render(sundry));
        }

        var po = await _db.PurchaseOrders.AsNoTracking().Include(o => o.Lines).SingleAsync(o => o.Id == payment.PurchaseOrderId, cancellationToken);
        var itemIds = po.Lines.Select(l => l.ItemId).Distinct().ToList();
        var items = await _db.Items.AsNoTracking().Where(i => itemIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, i => i.Name, cancellationToken);
        var names = po.Lines.OrderBy(l => l.LineNo).Select(l => items.GetValueOrDefault(l.ItemId) ?? "").Where(n => n.Length > 0).Distinct().ToList();
        var company = await CompanyAsync(cancellationToken);
        var frame = await OrderFrameAsync(po.Id, po.Date, po.ConstructionProjectId, po.DepartmentId,
            po.Lines.Select(l => l.SourcePrLineId).OfType<Guid>().ToList(), names, cancellationToken);
        var context = new PoPrintContext(company.Name, frame.ProjectName, frame.DepartmentName, frame.GoodsTitle, frame.Budget, frame.PastNet,
            frame.PastVat, null, new Dictionary<Guid, string?>(), new Dictionary<Guid, decimal>());
        var form = CuvtPrintForms.PaymentRequest(payment, context, po.TotalAmount);
        return new PdfFile($"{payment.No}.pdf", CuvtFormLayout.Render(form));
    }

    /// <inheritdoc />
    public async Task<PdfFile> PurchaseOrderAsync(Guid id, string? form, CancellationToken cancellationToken)
    {
        var wanted = form?.Trim();
        if (string.Equals(wanted, "supplier", StringComparison.OrdinalIgnoreCase)) return await SupplierOrderAsync(id, cancellationToken);
        if (!string.IsNullOrEmpty(wanted) && wanted is not ("4A" or "4B"))
            throw new BusinessRuleException("Mẫu in đơn mua hàng không hợp lệ (4A, 4B hoặc supplier).");

        var form4 = await PurchaseOrderFormAsync(id, cancellationToken);
        if (!string.IsNullOrEmpty(wanted) && wanted != form4.FormNo)
            throw new BusinessRuleException($"Đơn {form4.No} in theo biểu mẫu {form4.FormNo} (theo loại mua của dòng đề nghị nguồn).");
        return new PdfFile($"{form4.No}.pdf", CuvtFormLayout.Render(form4));
    }

    /// <summary>
    /// Nội dung bảng theo dõi mua hàng 4A/4B của một PO (lát Y7): đọc PO theo phạm vi, số liệu khung chi phí (dự toán công trình / ngân sách
    /// tháng của bộ phận), phần đã mua trước đây không gồm chính đơn này, giá cũ gần nhất của từng dòng.
    /// </summary>
    /// <param name="id">PO.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Biểu mẫu để dựng PDF.</returns>
    /// <exception cref="NotFoundException">Khi không có phiếu hoặc phiếu ngoài phạm vi.</exception>
    public async Task<CuvtForm> PurchaseOrderFormAsync(Guid id, CancellationToken cancellationToken)
    {
        var po = await _orders.GetAsync(id, cancellationToken);
        var company = await CompanyAsync(cancellationToken);
        var itemIds = po.Lines.Select(l => l.ItemId).Distinct().ToList();
        var specifications = await _db.Items.AsNoTracking().Where(i => itemIds.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, i => i.Specification, cancellationToken);
        var phone = await _db.Suppliers.AsNoTracking().Where(s => s.Id == po.SupplierId).Select(s => s.Phone).SingleOrDefaultAsync(cancellationToken);
        var lastPrices = await LastPricesAsync(
            po.Lines.Select(l => (l.Id, l.ItemId, l.UomFactor)).ToList(), po.Date, [po.Id], cancellationToken);

        var frame = await OrderFrameAsync(po.Id, po.Date, po.ConstructionProjectId, po.DepartmentId,
            po.Lines.Select(l => l.SourcePrLineId).OfType<Guid>().ToList(),
            po.Lines.OrderBy(l => l.LineNo).Select(l => l.ItemName).Distinct().ToList(), cancellationToken);
        var context = new PoPrintContext(
            company.Name, frame.ProjectName, frame.DepartmentName, frame.GoodsTitle, frame.Budget, frame.PastNet, frame.PastVat, phone, specifications,
            lastPrices);
        return CuvtPrintForms.PurchaseOrder(po, context);
    }

    /// <summary>
    /// Khung chi phí của một PO dùng chung cho bảng theo dõi 4A/4B (lát Y7) và đề nghị thanh toán 5A/5B (lát Y8): 4A/5A tổng dự toán công trình,
    /// đã mua trước đây = PO đã duyệt khác của công trình; 4B/5B ngân sách tháng của bộ phận (bộ phận các đề nghị nguồn nếu chỉ một, không thì bộ
    /// phận đặt hàng), đã mua trước đây = PO đã duyệt khác trong tháng của bộ phận; "Tên hàng hóa" = mục đích đề nghị nguồn hoặc tên mặt hàng.
    /// </summary>
    private async Task<(string? ProjectName, string? DepartmentName, string? GoodsTitle, decimal? Budget, decimal PastNet, decimal PastVat)> OrderFrameAsync(
        Guid poId, DateOnly poDate, Guid? constructionProjectId, Guid? orderDepartmentId, IReadOnlyList<Guid> prLineIds, IReadOnlyList<string> itemNames,
        CancellationToken cancellationToken)
    {
        // Đề nghị nguồn: bộ phận và mục đích (4B "Tên hàng hóa").
        var requests = await (
                from l in _db.Set<PurchaseRequestLine>().AsNoTracking()
                join r in _db.PurchaseRequests on l.RequestId equals r.Id
                where prLineIds.Contains(l.Id)
                select new { r.Id, r.DepartmentId, r.Purpose })
            .Distinct().ToListAsync(cancellationToken);

        string? projectName = null, departmentName, goodsTitle = null;
        decimal? budget;
        (decimal Net, decimal Vat) past;
        if (constructionProjectId is { } projectId)
        {
            // 4A: khung chi phí là tổng dự toán công trình; đã mua trước đây = PO đã duyệt khác của công trình (lũy kế 1A, Q4).
            var project = await _db.ConstructionProjects.AsNoTracking().Where(p => p.Id == projectId)
                .Select(p => new { p.Name, p.EstimateAmount, p.DepartmentId }).SingleAsync(cancellationToken);
            projectName = project.Name;
            budget = project.EstimateAmount;
            departmentName = await DepartmentNameAsync(project.DepartmentId, cancellationToken);
            past = (await ConstructionSpending.ByProjectAsync(_db, [projectId], cancellationToken, poId)).GetValueOrDefault(projectId);
        }
        else
        {
            // 4B: bộ phận của các đề nghị nguồn (một bộ phận), không thì bộ phận đặt hàng của PO; định mức = ngân sách tháng theo ngày PO;
            // đã mua trước đây = PO đã duyệt khác trong tháng của bộ phận (lũy kế 1B – người dùng chốt 12/11/2026).
            var departments = requests.Select(r => r.DepartmentId).OfType<Guid>().Distinct().ToList();
            Guid? department = departments.Count == 1 ? departments[0] : departments.Count == 0 ? orderDepartmentId : null;
            departmentName = department is { } d ? await DepartmentNameAsync(d, cancellationToken) : null;
            budget = department is { } bd
                ? await _db.DepartmentBudgets.AsNoTracking().Where(b => b.DepartmentId == bd && b.Year == poDate.Year && b.Month == poDate.Month)
                    .Select(b => (decimal?)b.Amount).FirstOrDefaultAsync(cancellationToken)
                : null;
            past = department is { } sd ? await DepartmentSpending.MonthDetailAsync(_db, sd, poDate, poId, cancellationToken) : (0m, 0m);
            var purposes = requests.Select(r => r.Purpose?.Trim()).Where(p => !string.IsNullOrEmpty(p)).Distinct().ToList();
            var names = itemNames;
            goodsTitle = purposes.Count > 0 ? string.Join("; ", purposes)
                : names.Count <= 3 ? string.Join(", ", names) : $"{string.Join(", ", names.Take(3))} và {names.Count - 3} mặt hàng khác";
        }

        return (projectName, departmentName, goodsTitle, budget, past.Net, past.Vat);
    }

    /// <summary>Đơn đặt hàng gửi NCC theo khuôn chung (mục 10.10) – NCC cần mã hàng, giá, chiết khấu, thuế, điều khoản giao.</summary>
    private async Task<PdfFile> SupplierOrderAsync(Guid id, CancellationToken cancellationToken)
    {
        var po = await _orders.GetAsync(id, cancellationToken);
        var approver = po.Approvals.LastOrDefault(a => a.Status == "Approved")?.ActedByName;
        var doc = new PrintDocument(
            "ĐƠN ĐẶT HÀNG", po.No, po.Date, ApprovalNote(po.Status),
            [
                ("Kính gửi nhà cung cấp", $"{po.SupplierCode} – {po.SupplierName}"),
                ("Kho nhận hàng", po.WarehouseCode),
                ("Địa chỉ giao hàng", po.DeliveryAddress),
                ("Ngày giao dự kiến", Date(po.ExpectedDate)),
                ("Điều khoản thanh toán", po.PaymentTerms),
                ("Điều kiện giao hàng", po.DeliveryTerms),
                ("Dung sai nhận hàng", po.ReceiveTolerancePct > 0 ? $"{Qty(po.ReceiveTolerancePct)}%" : null),
            ],
            [
                new("STT", 8), new("Mã hàng", 27), new("Tên hàng", 55), new("ĐVT", 12), new("SL", 16, true), new("Đơn giá", 20, true),
                new("CK %", 10, true), new("Thành tiền", 24, true), new("Thuế", 8, true),
            ],
            po.Lines.Select(l => new[]
            {
                // Mã hàng theo NCC ghi kèm tên hàng để NCC nhận ra sản phẩm của họ (design.md mục 5.9).
                l.LineNo.ToString(Vi), l.ItemCode,
                l.ItemName + (l.SupplierItemCode is { } sc ? $" – mã NCC: {sc}" : "") + (l.DeliveryDate is { } d ? $" (giao {d:dd/MM/yyyy})" : ""),
                l.UomCode, Qty(l.Qty), Price(l.UnitPrice), l.DiscountPct > 0 ? Qty(l.DiscountPct) : "", Money(l.Amount), Vat(l.VatCode),
            }).ToList(),
            [
                new("Tiền hàng", Money(po.GrossAmount)),
                new("Chiết khấu", Money(po.TotalDiscount)),
                new("Tiền trước thuế", Money(po.TotalAmount)),
                new("Thuế GTGT", Money(po.TotalVat)),
                new("Tổng thanh toán", Money(po.TotalAmount + po.TotalVat), true),
            ],
            VietnameseNumberText.ToVndWords(po.TotalAmount + po.TotalVat), po.Note,
            ["Người lập", "Người duyệt", "Xác nhận của nhà cung cấp"], [po.CreatedByName, approver, null]);
        return new PdfFile($"{po.No}.pdf", PdfLayout.Render(await CompanyAsync(cancellationToken), doc));
    }

    /// <inheritdoc />
    public async Task<PdfFile> PurchaseRequestAsync(Guid id, CancellationToken cancellationToken)
    {
        // In theo biểu mẫu CUVT 1A/1B (lát Y10, design.md mục 10.10) thay khuôn chung.
        var pr = await _requests.GetAsync(id, cancellationToken);
        var itemIds = pr.Lines.Select(l => l.ItemId).Distinct().ToList();
        var specifications = await _db.Items.AsNoTracking().Where(i => itemIds.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, i => i.Specification, cancellationToken);
        var company = await CompanyAsync(cancellationToken);
        var monthSpent = pr.DepartmentId is { } department && pr.Category != nameof(PurchaseCategory.Construction)
            ? await DepartmentSpending.MonthAsync(_db, department, pr.Date, cancellationToken)
            : 0;
        return new PdfFile($"{pr.No}.pdf", CuvtFormLayout.Render(CuvtPrintForms.PurchaseRequest(pr, company.Name, specifications, monthSpent)));
    }

    /// <inheritdoc />
    public async Task<PdfFile> PurchaseSummaryAsync(Guid id, CancellationToken cancellationToken)
    {
        var summary = await _summaries.GetAsync(id, cancellationToken);
        var company = await CompanyAsync(cancellationToken);
        var monthSpent = summary.Form == nameof(PurchaseSummaryForm.Goods)
            ? await DepartmentSpending.MonthAsync(_db, summary.DepartmentId, summary.Date, cancellationToken)
            : 0;
        return new PdfFile($"{summary.No}.pdf", CuvtFormLayout.Render(CuvtPrintForms.PurchaseSummary(summary, company.Name, monthSpent)));
    }

    /// <inheritdoc />
    public async Task<PdfFile> RfqAsync(Guid id, string? form, CancellationToken cancellationToken)
    {
        var rfq = await _rfqs.GetAsync(id, cancellationToken);
        var company = await CompanyAsync(cancellationToken);
        var date = await _db.Rfqs.AsNoTracking().Where(r => r.Id == id).Select(r => r.AwardDate).SingleOrDefaultAsync(cancellationToken) ?? rfq.Date;

        // Đề nghị nguồn: bộ phận và mục đích mua.
        var prLineIds = rfq.Lines.Select(l => l.SourcePrLineId).OfType<Guid>().ToList();
        var requests = await (
                from l in _db.Set<PurchaseRequestLine>().AsNoTracking()
                join r in _db.PurchaseRequests on l.RequestId equals r.Id
                where prLineIds.Contains(l.Id)
                select new { r.Id, r.DepartmentId, r.Purpose })
            .Distinct().ToListAsync(cancellationToken);
        var purposes = requests.Select(r => r.Purpose?.Trim()).Where(p => !string.IsNullOrEmpty(p)).Distinct().ToList();
        var purpose = purposes.Count == 0 ? null : string.Join("; ", purposes);

        string? projectName = null, departmentName = null;
        decimal? estimate = null, budget = null;
        decimal spent;
        if (rfq.ConstructionProjectId is { } projectId)
        {
            var project = await _db.ConstructionProjects.AsNoTracking().Where(p => p.Id == projectId)
                .Select(p => new { p.Name, p.EstimateAmount, p.DepartmentId }).SingleAsync(cancellationToken);
            projectName = project.Name;
            estimate = project.EstimateAmount;
            departmentName = await DepartmentNameAsync(project.DepartmentId, cancellationToken);
            spent = (await ConstructionSpending.ByProjectAsync(_db, [projectId], cancellationToken)).GetValueOrDefault(projectId).Net;
        }
        else
        {
            // Loại B: số liệu bộ phận chỉ in khi mọi dòng đề nghị cùng một bộ phận (RFQ gộp nhiều bộ phận thì không có một định mức chung).
            var departments = requests.Select(r => r.DepartmentId).Distinct().ToList();
            spent = 0;
            if (departments is [{ } department])
            {
                departmentName = await DepartmentNameAsync(department, cancellationToken);
                budget = await _db.DepartmentBudgets.AsNoTracking()
                    .Where(b => b.DepartmentId == department && b.Year == date.Year && b.Month == date.Month)
                    .Select(b => (decimal?)b.Amount).FirstOrDefaultAsync(cancellationToken);
                spent = await DepartmentSpending.MonthAsync(_db, department, date, cancellationToken);
            }
        }

        // NCC cũ = đã có PO đã duyệt với công ty (không tính PO sinh từ chính RFQ này).
        var ownOrders = rfq.Suppliers.Select(s => s.PurchaseOrderId).OfType<Guid>().ToList();
        var supplierIds = rfq.Suppliers.Select(s => s.SupplierId).ToList();
        var existing = await _db.PurchaseOrders.AsNoTracking()
            .Where(o => supplierIds.Contains(o.SupplierId) && !ownOrders.Contains(o.Id)
                && (o.Status == ApprovalDocumentStatus.Approved || o.Status == ApprovalDocumentStatus.Closed))
            .Select(o => o.SupplierId).Distinct().ToListAsync(cancellationToken);
        var suppliers = await _db.Suppliers.AsNoTracking().Where(s => supplierIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => new RfqPrintSupplier(s.Address, s.Phone, existing.Contains(s.Id)), cancellationToken);

        var itemIds = rfq.Lines.Select(l => l.ItemId).Distinct().ToList();
        var lastPrices = await LastPricesAsync(rfq.Lines.Select(l => (l.Id, l.ItemId, l.UomFactor)).ToList(), date, ownOrders, cancellationToken);
        var itemVat = await _db.Items.AsNoTracking().Where(i => itemIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, i => i.VatCode, cancellationToken);

        var context = new RfqPrintContext(
            company.Name, date, projectName, estimate, spent, departmentName, budget, purpose, suppliers, lastPrices,
            rfq.Lines.ToDictionary(l => l.Id, l => itemVat.GetValueOrDefault(l.ItemId)));
        var forms = CuvtPrintForms.Rfq(rfq, context, form);
        return new PdfFile($"{rfq.No}.pdf", CuvtFormLayout.RenderMany(forms));
    }

    /// <summary>
    /// Giá cũ đã mua gần nhất (mẫu 3a/3B, 4A/4B): dòng PO đã duyệt / đã đóng gần nhất (ngày PO ≤ <paramref name="date"/>) cùng mặt hàng, giá
    /// sau CK trước VAT theo ĐVT cơ sở (= thành tiền / SL cơ sở), quy về ĐVT của dòng. Ví dụ PO 2 thùng (24 cái) thành tiền 1.200.000 →
    /// 50.000/cái; dòng ĐVT thùng 12 → 600.000.
    /// </summary>
    /// <param name="lines">Dòng cần tra: Id dòng, mặt hàng, hệ số ĐVT của dòng.</param>
    /// <param name="date">Ngày trình / ngày PO.</param>
    /// <param name="excludeOrders">PO không tính (PO sinh từ chính RFQ, hoặc chính PO đang in).</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Id dòng → giá cũ; dòng mua lần đầu không có trong từ điển.</returns>
    private async Task<Dictionary<Guid, decimal>> LastPricesAsync(
        IReadOnlyList<(Guid LineId, Guid ItemId, decimal UomFactor)> lines, DateOnly date, IReadOnlyList<Guid> excludeOrders,
        CancellationToken cancellationToken)
    {
        var itemIds = lines.Select(l => l.ItemId).Distinct().ToList();
        var history = await (
                from ol in _db.Set<PurchaseOrderLine>().AsNoTracking()
                join o in _db.PurchaseOrders on ol.OrderId equals o.Id
                where itemIds.Contains(ol.ItemId) && ol.BaseQty > 0 && o.Date <= date && !excludeOrders.Contains(o.Id)
                      && (o.Status == ApprovalDocumentStatus.Approved || o.Status == ApprovalDocumentStatus.Closed)
                select new { ol.ItemId, o.Date, o.CreatedAt, ol.Amount, ol.BaseQty })
            .ToListAsync(cancellationToken);
        var lastBase = history.GroupBy(h => h.ItemId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(h => h.Date).ThenByDescending(h => h.CreatedAt).Select(h => h.Amount / h.BaseQty).First());
        return lines.Where(l => lastBase.ContainsKey(l.ItemId))
            .ToDictionary(l => l.LineId, l => Math.Round(lastBase[l.ItemId] * l.UomFactor, 2, MidpointRounding.AwayFromZero));
    }

    /// <summary>Tên bộ phận (trong phạm vi công ty đang làm việc).</summary>
    private Task<string?> DepartmentNameAsync(Guid departmentId, CancellationToken cancellationToken) =>
        _db.Departments.AsNoTracking().Where(d => d.Id == departmentId).Select(d => d.Name).SingleOrDefaultAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<PdfFile> InventoryDocumentAsync(Guid id, CancellationToken cancellationToken)
    {
        var d = await _inventory.GetAsync(id, cancellationToken);
        if (d.DocType == DocumentTypes.Adjustment) return await AdjustmentAsync(d, cancellationToken);
        var from = d.FromWarehouseId is { } f ? await WarehouseNameAsync(f, cancellationToken) : null;
        var to = d.ToWarehouseId is { } t ? await WarehouseNameAsync(t, cancellationToken) : null;
        var supplier = d.SupplierCode is null ? null : $"{d.SupplierCode} – {d.SupplierName}";
        var counter = d.CounterCompanyCode is null ? null : $"{d.CounterCompanyCode} – {d.CounterCompanyName}";
        var invoice = d.InvoiceNo is null ? null : $"{d.InvoiceSeries} {d.InvoiceNo}".Trim() + (d.InvoiceDate is { } at ? $" ngày {at:dd/MM/yyyy}" : "");
        var (title, signers, info) = d.DocType switch
        {
            DocumentTypes.GoodsReceipt => ("PHIẾU NHẬP KHO",
                new[] { "Người lập phiếu", "Người giao hàng", "Thủ kho", "Kế toán trưởng" },
                new (string, string?)[] { ("Nhà cung cấp", supplier), ("Nhập vào kho", to), ("Hóa đơn", invoice), ("Phiếu giao hàng", d.DeliveryNoteNo),
                    ("Theo đơn mua hàng", d.PurchaseOrderNo), ("Người giao hàng", d.Deliverer) }),
            DocumentTypes.ReturnToVendor => ("PHIẾU XUẤT TRẢ HÀNG NHÀ CUNG CẤP",
                new[] { "Người lập phiếu", "Người nhận hàng", "Thủ kho", "Kế toán trưởng" },
                new (string, string?)[] { ("Nhà cung cấp", supplier), ("Xuất từ kho", from), ("Theo phiếu nhập", d.LinkedDocumentNo),
                    ("Hóa đơn điều chỉnh", invoice) }),
            DocumentTypes.Transfer => ("PHIẾU CHUYỂN KHO NỘI BỘ",
                new[] { "Người lập phiếu", "Người giao", "Người nhận", "Thủ kho" },
                new (string, string?)[] { ("Xuất từ kho", from), ("Nhập vào kho", to), ("Người giao", d.Deliverer), ("Người nhận", d.Receiver) }),
            DocumentTypes.IntercompanySale => ("PHIẾU XUẤT BÁN NỘI BỘ",
                new[] { "Người lập phiếu", "Người giao hàng", "Thủ kho", "Kế toán trưởng" },
                new (string, string?)[] { ("Bán cho công ty", counter), ("Xuất từ kho", from), ("Hóa đơn", invoice), ("Người giao hàng", d.Deliverer) }),
            _ => ("PHIẾU NHẬP MUA NỘI BỘ",
                new[] { "Người lập phiếu", "Người giao hàng", "Thủ kho", "Kế toán trưởng" },
                new (string, string?)[] { ("Mua của công ty", counter), ("Nhập vào kho", to), ("Theo phiếu xuất bán", d.LinkedDocumentNo),
                    ("Hóa đơn", invoice), ("Người giao hàng", d.Deliverer) }),
        };

        // Phiếu chuyển kho không có đơn giá (giá vốn tính khi ghi sổ – design.md mục 10.6) nên không in cột tiền.
        var priced = d.DocType != DocumentTypes.Transfer;
        PrintColumn[] columns = priced
            ? [new("STT", 8), new("Mã hàng", 27), new("Tên hàng", 45), new("ĐVT", 12), new("Số lô", 16), new("Hạn dùng", 16), new("SL", 16, true),
                new("Đơn giá", 18, true), new("Thành tiền", 22, true)]
            : [new("STT", 8), new("Mã hàng", 27), new("Tên hàng", 69), new("ĐVT", 14), new("Số lô", 22), new("Hạn dùng", 20), new("SL", 20, true)];
        var rows = d.Lines.Select(l =>
        {
            string[] common = [l.LineNo.ToString(Vi), l.ItemCode, l.ItemName, l.UomCode, l.LotNo ?? "", Date(l.ExpiryDate) ?? "", Qty(l.Qty)];
            return priced ? [.. common, Price(l.UnitPrice), Money(l.Amount)] : common;
        }).ToList();
        PrintTotal[] totals = priced
            ? [new("Tiền hàng", Money(d.TotalAmount)), new("Thuế GTGT", Money(d.TotalVat)), new("Tổng cộng", Money(d.TotalAmount + d.TotalVat), true)]
            : [];
        var note = d.Status switch
        {
            "Draft" => "BẢN NHÁP – chưa ghi sổ",
            "Cancelled" => "ĐÃ HỦY",
            _ => null,
        };
        var doc = new PrintDocument(
            title, d.No, d.Date, note, info, columns, rows, totals,
            priced ? VietnameseNumberText.ToVndWords(d.TotalAmount + d.TotalVat) : null, d.Note, signers,
            [d.CreatedByName, .. Enumerable.Repeat<string?>(null, signers.Length - 1)]);
        return new PdfFile($"{d.No}.pdf", PdfLayout.Render(await CompanyAsync(cancellationToken), doc));
    }

    /// <summary>
    /// Biên bản kiểm kê, điều chỉnh kho (phiếu ADJ – design.md mục 18.23): mỗi dòng ghi tồn sổ, thực đếm (dòng lập theo kiểm kê) và SL tăng
    /// hoặc giảm. Dòng tăng in giá nhập tay; dòng giảm xuất theo giá vốn chốt khi đóng kỳ nên không in tiền – tổng chỉ là giá trị tăng.
    /// </summary>
    private async Task<PdfFile> AdjustmentAsync(InventoryDocumentDto d, CancellationToken cancellationToken)
    {
        var warehouse = d.ToWarehouseId is { } w ? await WarehouseNameAsync(w, cancellationToken) : null;
        PrintColumn[] columns =
        [
            new("STT", 8), new("Mã hàng", 22), new("Tên hàng", 42), new("ĐVT", 10), new("Số lô", 14), new("Tồn sổ", 16, true),
            new("Thực đếm", 16, true), new("Tăng", 15, true), new("Giảm", 15, true), new("Giá trị tăng", 22, true),
        ];
        var rows = d.Lines.Select(l => (string[])
        [
            l.LineNo.ToString(Vi), l.ItemCode, l.ItemName, l.UomCode, l.LotNo ?? "",
            l.BookQty is { } book ? Qty(book) : "", l.CountedQty is { } counted ? Qty(counted) : "",
            l.Direction == 1 ? Qty(l.Qty) : "", l.Direction == -1 ? Qty(l.Qty) : "", l.Direction == 1 ? Money(l.Amount) : "",
        ]).ToList();
        var note = d.Status switch
        {
            "Draft" => "BẢN NHÁP – chưa ghi sổ",
            "Cancelled" => "ĐÃ HỦY",
            _ => null,
        };
        string[] signers = ["Người lập phiếu", "Thủ kho", "Kế toán", "Trưởng ban kiểm kê"];
        var doc = new PrintDocument(
            "BIÊN BẢN KIỂM KÊ, ĐIỀU CHỈNH KHO", d.No, d.Date, note,
            [("Kho", warehouse), ("Số biên bản kiểm kê", d.CountReportNo), ("Lý do điều chỉnh", d.Reason)],
            columns, rows, [new("Giá trị tăng", Money(d.TotalAmount), true)], null, d.Note, signers,
            [d.CreatedByName, null, null, null]);
        return new PdfFile($"{d.No}.pdf", PdfLayout.Render(await CompanyAsync(cancellationToken), doc));
    }

    /// <inheritdoc />
    public async Task<PdfFile> GoodsReceiptRecordAsync(Guid id, CancellationToken cancellationToken)
    {
        var d = await _inventory.GetAsync(id, cancellationToken);
        // Chỉ phiếu nhập mua NCC có kiểm đếm, kiểm tra chất lượng – design.md mục 18.14.
        if (d.DocType != DocumentTypes.GoodsReceipt)
            throw new BusinessRuleException("Chỉ in biên bản giao nhận cho phiếu nhập mua từ nhà cung cấp.");

        var to = d.ToWarehouseId is { } t ? await WarehouseNameAsync(t, cancellationToken) : null;
        var invoice = d.InvoiceNo is null ? null : $"{d.InvoiceSeries} {d.InvoiceNo}".Trim() + (d.InvoiceDate is { } at ? $" ngày {at:dd/MM/yyyy}" : "");
        // Dòng sai lệch: có hàng không đạt, thiếu/thừa so với chứng từ giao, hoặc kết quả Không đạt (cùng điều kiện báo cáo QCDEV).
        var deviations = d.Lines.Count(l => l.RejectedQty > 0 || l.DiscrepancyQty != 0 || l.QcResult == "Failed");
        // Chưa ghi SL chứng từ / chưa kiểm tra thì không kết luận "đúng, đạt" – chỉ nêu phần còn thiếu.
        var pending = new List<string>();
        var noDelivered = d.Lines.Count(l => l.DeliveredQty is null);
        if (noDelivered > 0) pending.Add($"{noDelivered} dòng chưa ghi SL theo chứng từ giao");
        var notChecked = d.Lines.Count(l => l.QcResult == "NotChecked");
        if (notChecked > 0) pending.Add($"{notChecked} dòng chưa kiểm tra chất lượng");
        var conclusion = deviations == 0
            ? pending.Count == 0 ? "Hàng giao đúng số lượng theo chứng từ, đạt chất lượng." : $"Không ghi nhận sai lệch; còn {string.Join(", ", pending)}."
            : $"Có {deviations}/{d.Lines.Count} dòng sai lệch hoặc không đạt (xem cột Thiếu/thừa, Không đạt, Ghi chú). "
              + "Hàng không đạt không nhập kho; đề nghị nhà cung cấp xử lý theo hợp đồng.";
        var doc = new PrintDocument(
            "BIÊN BẢN GIAO NHẬN VÀ KIỂM TRA HÀNG HÓA", d.No, d.Date, d.Status switch
            {
                "Draft" => "BẢN NHÁP – phiếu nhập chưa ghi sổ",
                "Cancelled" => "ĐÃ HỦY",
                _ => null,
            },
            [
                ("Bên giao (nhà cung cấp)", d.SupplierCode is null ? null : $"{d.SupplierCode} – {d.SupplierName}"),
                ("Người giao hàng", d.Deliverer),
                ("Bên nhận (kho)", to),
                ("Theo đơn mua hàng", d.PurchaseOrderNo),
                ("Phiếu giao hàng", d.DeliveryNoteNo),
                ("Hóa đơn", invoice),
            ],
            [
                new("STT", 8), new("Mã hàng", 22), new("Tên hàng", 36), new("ĐVT", 10), new("SL chứng từ", 15, true), new("SL đạt nhập kho", 15, true),
                new("SL không đạt", 15, true), new("Thiếu/ thừa", 15, true), new("Kết quả", 16), new("Ghi chú kiểm tra", 28),
            ],
            d.Lines.Select(l => new[]
            {
                l.LineNo.ToString(Vi), l.ItemCode, l.ItemName + (l.LotNo is null ? "" : $" (lô {l.LotNo})"), l.UomCode,
                l.DeliveredQty is { } q ? Qty(q) : "", Qty(l.Qty), l.RejectedQty > 0 ? Qty(l.RejectedQty) : "",
                // Dấu + là thừa, − là thiếu so với chứng từ giao.
                l.DiscrepancyQty == 0 ? "" : (l.DiscrepancyQty > 0 ? "+" : "") + Qty(l.DiscrepancyQty),
                l.QcResult switch { "Passed" => "Đạt", "Failed" => "Không đạt", _ => "Chưa kiểm tra" }, l.QcNote ?? "",
            }).ToList(),
            [],
            null, d.Note,
            // Người lập PO không một mình xác nhận giao nhận (G4) – biên bản có đủ bên giao, thủ kho và người kiểm tra.
            ["Đại diện bên giao", "Thủ kho", "Người kiểm tra", "Người lập biên bản"], [d.Deliverer, null, null, d.CreatedByName], conclusion);
        return new PdfFile($"BB-{d.No}.pdf", PdfLayout.Render(await CompanyAsync(cancellationToken), doc));
    }

    /// <inheritdoc />
    public async Task<PdfFile> ReceiptConfirmationAsync(Guid id, CancellationToken cancellationToken)
    {
        // Đọc qua dịch vụ xem XNH: phiếu ngoài công ty đang làm việc báo không tìm thấy như khi mở trên màn hình.
        var x = await _receipts.GetAsync(id, cancellationToken);
        var invoice = x.InvoiceNo is null ? null
            : $"{x.InvoiceSeries} {x.InvoiceNo}".Trim() + (x.InvoiceDate is { } at ? $" ngày {at:dd/MM/yyyy}" : "")
              + (x.InvoiceAmount is { } amount ? $" – {Money(amount)} VND" : "");
        var acceptance = x.Acceptance switch
        {
            "Accepted" => "Đạt",
            "Disputed" => "Không đồng ý nghiệm thu",
            _ => null,
        };
        if (acceptance is not null)
            acceptance += (x.AcceptedByName is null ? "" : $" – {x.AcceptedByName}") + (x.AcceptedAt is { } accepted ? $" {Stamp(accepted)}" : "")
                + (string.IsNullOrWhiteSpace(x.AcceptanceNote) ? "" : $"; ý kiến: {x.AcceptanceNote}");

        // Thiếu so với phiếu giao = đạt + không đạt − SL phiếu giao (XNH chỉ cho đạt + không đạt ≤ phiếu giao, nên chỉ có thiếu – design.md 19.4).
        // Ví dụ phiếu giao 8, đạt 6, không đạt 1 → thiếu 1 (in "−1").
        static decimal Shortage(ReceiptConfirmationLineDto l) => l.AcceptedQty + l.RejectedQty - l.DeliveredQty;
        var adjusted = x.Lines.Any(l => l.AdjustedQty != 0);
        // Có điều chỉnh thì thêm cột SL thực nhận (đạt ± điều chỉnh) và thu hẹp cột ghi chú để tổng độ rộng vẫn 180 mm (A4 dọc).
        List<PrintColumn> columns =
        [
            new("STT", 8), new("Mã hàng", 18), new("Tên hàng", 32), new("ĐVT", 9), new("SL phiếu giao", 14, true), new("SL đạt", 14, true),
            new("SL không đạt", 14, true), new("Thiếu", 13, true),
        ];
        if (adjusted) columns.Add(new("SL thực nhận", 14, true));
        columns.Add(new("Kết quả", 14));
        columns.Add(new("Ghi chú kiểm tra", adjusted ? 30 : 44));
        var rows = x.Lines.Select(l =>
        {
            List<string> cells =
            [
                l.LineNo.ToString(Vi), l.ItemCode, l.ItemName, l.UomCode, Qty(l.DeliveredQty), Qty(l.AcceptedQty),
                l.RejectedQty > 0 ? Qty(l.RejectedQty) : "", Shortage(l) == 0 ? "" : Qty(Shortage(l)),
            ];
            if (adjusted) cells.Add(Qty(l.ReceivedQty));
            cells.Add(l.CheckResult switch { "Passed" => "Đạt", "Failed" => "Không đạt", _ => "Chưa kiểm tra" });
            cells.Add(l.CheckNote ?? "");
            return cells.ToArray();
        }).ToList();

        // Kết luận cùng điều kiện với biên bản phiếu nhập cũ: dòng có hàng không đạt, thiếu so phiếu giao hoặc kết quả Không đạt là sai lệch.
        var deviations = x.Lines.Count(l => l.RejectedQty > 0 || Shortage(l) != 0 || l.CheckResult == "Failed");
        var notChecked = x.Lines.Count(l => l.CheckResult == "NotChecked");
        var conclusion = deviations == 0
            ? notChecked == 0 ? "Hàng giao đúng số lượng theo phiếu giao, đạt chất lượng."
                : $"Không ghi nhận sai lệch; còn {notChecked} dòng chưa kiểm tra chất lượng."
            : $"Có {deviations}/{x.Lines.Count} dòng thiếu hoặc không đạt (xem cột Thiếu, SL không đạt, Ghi chú). "
              + "Hàng không đạt không tính vào SL thực nhận; đề nghị nhà cung cấp xử lý theo hợp đồng.";
        if (x.Adjustments.Count > 0)
        {
            // Điều chỉnh SL thực nhận thay cho trả NCC (design.md 19.4) – in đủ lịch sử để biên bản khớp với SL đã nhận của PO.
            conclusion += " Điều chỉnh SL thực nhận: " + string.Join("; ", x.Adjustments.OrderBy(a => a.AdjustedAt).Select(a =>
                $"dòng {a.LineNo}: {(a.Qty > 0 ? "+" : "")}{Qty(a.Qty)} ({Stamp(a.AdjustedAt)}" + (a.AdjustedByName is null ? "" : $", {a.AdjustedByName}")
                + $") – {a.Reason}")) + ".";
        }

        var doc = new PrintDocument(
            "BIÊN BẢN GIAO NHẬN VÀ KIỂM TRA HÀNG HÓA", x.No, x.Date, x.Status switch
            {
                "Draft" => "BẢN NHÁP – phiếu nhận hàng chưa xác nhận",
                "Cancelled" => "ĐÃ HỦY" + (string.IsNullOrWhiteSpace(x.CancelReason) ? "" : $" – {x.CancelReason}"),
                _ => null,
            },
            [
                ("Bên giao (nhà cung cấp)", $"{x.SupplierCode} – {x.SupplierName}"),
                ("Người giao hàng", x.Deliverer),
                ("Bên nhận (nơi nhận)", x.WarehouseCode is null ? null : $"{x.WarehouseCode} – {x.WarehouseName}"),
                ("Đơn vị đặt hàng", x.DepartmentName),
                ("Theo đơn mua hàng", $"{x.PurchaseOrderNo} ngày {x.PurchaseOrderDate:dd/MM/yyyy}"),
                ("Phiếu giao hàng", x.DeliveryNoteNo),
                ("Phiếu báo giao NCC", x.DeliveryNoticeNo),
                ("Hóa đơn", invoice),
                ("Xác nhận nhận hàng", x.ConfirmedAt is { } confirmed ? $"{x.ConfirmedByName} {Stamp(confirmed)}".Trim() : null),
                ("Nghiệm thu (đơn vị đặt hàng)", acceptance),
            ],
            columns, rows,
            // Giá trị theo giá thực mua của dòng PO × SL thực nhận – căn cứ đối chiếu hóa đơn, đề nghị thanh toán (lát Y8).
            [
                new("Giá trị thực nhận (giá PO, trước thuế)", Money(x.TotalAmount)), new("Thuế GTGT", Money(x.TotalVat)),
                new("Tổng cộng", Money(x.TotalAmount + x.TotalVat), true),
            ],
            null, x.Note,
            ["Đại diện bên giao", "Người nhận hàng", "Người kiểm tra", "Người lập phiếu"],
            [x.Deliverer, x.ConfirmedByName, null, x.CreatedByName], conclusion);
        return new PdfFile($"{x.No}.pdf", PdfLayout.Render(await CompanyAsync(cancellationToken), doc));
    }

    /// <summary>Giờ ngày theo múi giờ Việt Nam, dạng dd/MM/yyyy HH:mm (CLAUDE.md mục 5).</summary>
    private static string Stamp(DateTimeOffset value) =>
        TimeZoneInfo.ConvertTime(value, VietnamTime.Zone).ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);

    /// <summary>Ghi chú trạng thái của PR/PO chưa có hiệu lực.</summary>
    private static string? ApprovalNote(string status) => status switch
    {
        "Draft" => "BẢN NHÁP – chưa duyệt",
        "Submitted" => "ĐANG CHỜ DUYỆT – chưa có hiệu lực",
        "Cancelled" => "ĐÃ HỦY",
        _ => null,
    };

    /// <summary>Lấy thông tin công ty đang làm việc để in đầu phiếu (tên, địa chỉ, mã số thuế).</summary>
    private async Task<PrintCompany> CompanyAsync(CancellationToken cancellationToken)
    {
        var companyId = _scope.RequireCompany();
        return await _db.Companies.AsNoTracking().Where(c => c.Id == companyId)
            .Select(c => new PrintCompany(c.Name, c.Address, c.TaxCode))
            .SingleAsync(cancellationToken);
    }

    /// <summary>Mã – tên kho (kho ngoài phạm vi thì null, không in).</summary>
    private async Task<string?> WarehouseNameAsync(Guid id, CancellationToken cancellationToken) =>
        await _db.Warehouses.AsNoTracking().Where(w => w.Id == id).Select(w => w.Code + " – " + w.Name).SingleOrDefaultAsync(cancellationToken);

    /// <summary>Định dạng tiền VND kiểu Việt Nam, không số lẻ.</summary>
    private static string Money(decimal value) => Math.Round(value, 0, MidpointRounding.AwayFromZero).ToString("#,##0", Vi);

    /// <summary>Định dạng số lượng kiểu Việt Nam, tối đa 4 số lẻ.</summary>
    private static string Qty(decimal value) => value.ToString("#,##0.####", Vi);

    /// <summary>Định dạng đơn giá kiểu Việt Nam, tối đa 2 số lẻ.</summary>
    private static string Price(decimal value) => value.ToString("#,##0.##", Vi);

    /// <summary>Hiện mã VAT: mã toàn số thì thêm "%", mã khác (KCT, KKKNT) giữ nguyên.</summary>
    private static string Vat(string? code) => code is null ? "" : code.All(char.IsDigit) ? $"{code}%" : code;

    /// <summary>Định dạng ngày dd/MM/yyyy.</summary>
    private static string? Date(DateOnly? value) => value?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
}
