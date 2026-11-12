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

    /// <summary>Tạo dịch vụ.</summary>
    /// <param name="db">DbContext (đọc thông tin công ty, tên kho).</param>
    /// <param name="scope">Phạm vi người dùng.</param>
    /// <param name="orders">Dịch vụ đơn mua hàng.</param>
    /// <param name="requests">Dịch vụ đề nghị mua hàng.</param>
    /// <param name="inventory">Dịch vụ chứng từ kho.</param>
    /// <param name="summaries">Dịch vụ bảng tổng hợp đề xuất mua.</param>
    /// <param name="rfqs">Dịch vụ yêu cầu báo giá.</param>
    /// <param name="options">Cấu hình in (thư mục phông).</param>
    public DocumentPdfService(
        ProcDbContext db, ICurrentUserScope scope, IPurchaseOrderService orders, IPurchaseRequestService requests, IInventoryDocumentService inventory,
        IPurchaseSummaryService summaries, IRfqService rfqs, IOptions<PrintingOptions> options)
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
    }

    /// <inheritdoc />
    public async Task<PdfFile> PurchaseOrderAsync(Guid id, CancellationToken cancellationToken)
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

        // Giá cũ đã mua gần nhất: dòng PO đã duyệt gần nhất (ngày PO ≤ ngày trình) cùng mặt hàng, giá sau CK trước VAT theo ĐVT cơ sở
        // (= thành tiền / SL cơ sở), quy về ĐVT của dòng RFQ. Ví dụ PO 2 thùng (24 cái) thành tiền 1.200.000 → 50.000/cái; dòng RFQ ĐVT thùng 12 → 600.000.
        var itemIds = rfq.Lines.Select(l => l.ItemId).Distinct().ToList();
        var history = await (
                from ol in _db.Set<PurchaseOrderLine>().AsNoTracking()
                join o in _db.PurchaseOrders on ol.OrderId equals o.Id
                where itemIds.Contains(ol.ItemId) && ol.BaseQty > 0 && o.Date <= date && !ownOrders.Contains(o.Id)
                      && (o.Status == ApprovalDocumentStatus.Approved || o.Status == ApprovalDocumentStatus.Closed)
                select new { ol.ItemId, o.Date, o.CreatedAt, ol.Amount, ol.BaseQty })
            .ToListAsync(cancellationToken);
        var lastBase = history.GroupBy(h => h.ItemId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(h => h.Date).ThenByDescending(h => h.CreatedAt).Select(h => h.Amount / h.BaseQty).First());
        var lastPrices = rfq.Lines.Where(l => lastBase.ContainsKey(l.ItemId))
            .ToDictionary(l => l.Id, l => Math.Round(lastBase[l.ItemId] * l.UomFactor, 2, MidpointRounding.AwayFromZero));
        var itemVat = await _db.Items.AsNoTracking().Where(i => itemIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, i => i.VatCode, cancellationToken);

        var context = new RfqPrintContext(
            company.Name, date, projectName, estimate, spent, departmentName, budget, purpose, suppliers, lastPrices,
            rfq.Lines.ToDictionary(l => l.Id, l => itemVat.GetValueOrDefault(l.ItemId)));
        var forms = CuvtPrintForms.Rfq(rfq, context, form);
        return new PdfFile($"{rfq.No}.pdf", CuvtFormLayout.RenderMany(forms));
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
