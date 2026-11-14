using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Proc.Application.Common;
using Proc.Application.Excel;
using Proc.Application.Reports;
using Proc.Domain.Common;
using Proc.Domain.Documents;
using Proc.Domain.Purchasing;
using Proc.Infrastructure.Excel;
using Proc.Infrastructure.Persistence;

using Proc.Infrastructure.Purchasing;

namespace Proc.Infrastructure.Reports;

/// <summary>
/// Xuất chứng từ mua ra Excel cho kế toán (lát Y9b, design.md mục 19.3 "Lát Y9" – thay ACCX từ khi bỏ phân hệ kho, mục 19.4). Đọc qua global
/// query filter (công ty đang làm việc, nhóm hàng, kho). Ngày lọc: PO và đề nghị thanh toán theo **ngày duyệt** (chứng từ có hiệu lực từ lúc
/// duyệt), XNH theo **ngày nhận**.
/// </summary>
public sealed class PurchaseAccountingExportService : IPurchaseAccountingExportService
{
    /// <summary>Khoảng ngày tối đa mỗi lần xuất (hai năm).</summary>
    public const int MaxDays = 731;

    /// <summary>Số dòng chi tiết tối đa mỗi loại chứng từ mỗi file.</summary>
    public const int MaxLines = 50_000;

    /// <summary>Tên loại chứng từ in trong file.</summary>
    public static readonly IReadOnlyDictionary<string, string> DocTypeNames = new Dictionary<string, string>
    {
        [DocumentTypes.PurchaseOrder] = "Đơn mua hàng",
        [DocumentTypes.GoodsReceiptConfirmation] = "Xác nhận nhận hàng",
        [DocumentTypes.PaymentRequest] = "Đề nghị thanh toán",
    };

    private readonly ProcDbContext _db;
    private readonly ICurrentUserScope _scope;
    private readonly IClock _clock;

    /// <summary>Tạo dịch vụ.</summary>
    /// <param name="db">DbContext (đã gắn phạm vi người dùng).</param>
    /// <param name="scope">Phạm vi người dùng.</param>
    /// <param name="clock">Đồng hồ (giờ xuất file).</param>
    public PurchaseAccountingExportService(ProcDbContext db, ICurrentUserScope scope, IClock clock)
    {
        _db = db;
        _scope = scope;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PurchaseAccountingDocumentDto>> ListAsync(PurchaseAccountingQuery query, CancellationToken cancellationToken) =>
        (await LoadAsync(query, cancellationToken)).Documents;

    /// <inheritdoc />
    public async Task<ExcelFile> ExportAsync(PurchaseAccountingQuery query, CancellationToken cancellationToken)
    {
        var data = await LoadAsync(query, cancellationToken);
        var company = await _db.Companies.AsNoTracking().Where(c => c.Id == _scope.RequireCompany()).Select(c => c.Code).SingleAsync(cancellationToken);
        using var workbook = ExcelGridStyle.CreateWorkbook();
        var title = $"Chứng từ mua {company} từ {query.From:dd/MM/yyyy} đến {query.To:dd/MM/yyyy}";
        WriteSheet(ExcelGridStyle.AddSheet(workbook, "Tổng hợp"), title, DocumentColumns, data.Documents.Select(DocumentValues));
        WriteSheet(ExcelGridStyle.AddSheet(workbook, "Đơn mua hàng"), title, OrderColumns, data.OrderLines);
        WriteSheet(ExcelGridStyle.AddSheet(workbook, "Nhận hàng – hóa đơn"), title, ReceiptColumns, data.ReceiptLines);
        WriteSheet(ExcelGridStyle.AddSheet(workbook, "Đề nghị thanh toán"), title, PaymentColumns, data.PaymentLines);
        WriteGuide(ExcelGridStyle.AddSheet(workbook, "Hướng dẫn"));
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return new ExcelFile($"chung-tu-mua-ke-toan-{company.ToLowerInvariant()}-{query.From:yyyyMMdd}-{query.To:yyyyMMdd}.xlsx", stream.ToArray());
    }

    /// <summary>Dữ liệu đã đọc: danh sách chứng từ và dòng chi tiết của từng sheet (giá trị theo thứ tự cột).</summary>
    private sealed record Data(
        List<PurchaseAccountingDocumentDto> Documents, List<object?[]> OrderLines, List<object?[]> ReceiptLines, List<object?[]> PaymentLines);

    /// <summary>Thông tin NCC dùng chung cho mọi loại chứng từ.</summary>
    private sealed record SupplierInfo(string Code, string? LocalCode, string Name, string? TaxCode);

    /// <summary>Kiểm điều kiện, đọc PO, XNH, đề nghị thanh toán trong khoảng ngày.</summary>
    private async Task<Data> LoadAsync(PurchaseAccountingQuery query, CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        if (query.From > query.To) throw new BusinessRuleException("Từ ngày phải trước hoặc bằng đến ngày.");
        if (query.To.DayNumber - query.From.DayNumber >= MaxDays) throw new BusinessRuleException("Xuất tối đa hai năm mỗi lần.");
        var types = query.DocTypes.Count == 0 ? DocTypeNames.Keys.ToHashSet() : query.DocTypes.ToHashSet();
        if (types.FirstOrDefault(t => !DocTypeNames.ContainsKey(t)) is { } bad)
            throw new BusinessRuleException($"Loại chứng từ {bad} không có trong xuất chứng từ mua (chỉ PO, XNH, DNT).");
        // Ngày duyệt lưu UTC: đổi khoảng ngày Việt Nam [từ 00:00, đến hết ngày) sang UTC.
        var fromUtc = VietnamTime.VietnamToUtc(query.From.ToDateTime(TimeOnly.MinValue));
        var toUtc = VietnamTime.VietnamToUtc(query.To.AddDays(1).ToDateTime(TimeOnly.MinValue));

        var documents = new List<PurchaseAccountingDocumentDto>();
        var orderLines = new List<object?[]>();
        var receiptLines = new List<object?[]>();
        var paymentLines = new List<object?[]>();
        var suppliers = new Dictionary<Guid, SupplierInfo>();

        async Task<SupplierInfo> SupplierAsync(Guid id)
        {
            if (suppliers.TryGetValue(id, out var info)) return info;
            var s = await _db.Suppliers.AsNoTracking().Where(x => x.Id == id).Select(x => new { x.Code, x.Name, x.TaxCode }).SingleAsync(cancellationToken);
            var local = await _db.SupplierCompanies.AsNoTracking().Where(x => x.SupplierId == id).Select(x => x.LocalCode).FirstOrDefaultAsync(cancellationToken);
            return suppliers[id] = new SupplierInfo(s.Code, local, s.Name, s.TaxCode);
        }

        if (types.Contains(DocumentTypes.PurchaseOrder))
        {
            var rows = await (
                    from l in _db.Set<PurchaseOrderLine>().AsNoTracking()
                    join o in _db.PurchaseOrders on l.OrderId equals o.Id
                    join i in _db.Items on l.ItemId equals i.Id
                    join u in _db.Uoms on l.UomId equals u.Id
                    join d in _db.Departments on o.DepartmentId equals d.Id into ds
                    from d in ds.DefaultIfEmpty()
                    join p in _db.ConstructionProjects on o.ConstructionProjectId equals p.Id into ps
                    from p in ps.DefaultIfEmpty()
                    where (o.Status == ApprovalDocumentStatus.Approved || o.Status == ApprovalDocumentStatus.Closed)
                          && o.ApprovedAt >= fromUtc && o.ApprovedAt < toUtc
                    orderby o.ApprovedAt, o.No, l.LineNo
                    select new
                    {
                        o.Id, o.No, o.Date, o.ApprovedAt, o.SupplierId, o.Category, o.Status, o.CurrencyCode, Department = d == null ? null : d.Code,
                        Project = p == null ? null : p.Code, l.LineNo, ItemCode = i.Code, ItemName = i.Name, UomCode = u.Code, l.Qty, l.UnitPrice, l.DiscountPct,
                        l.Amount, l.VatCode, l.VatAmount,
                    })
                .Take(MaxLines + 1)
                .ToListAsync(cancellationToken);
            if (rows.Count > MaxLines) throw new BusinessRuleException("Quá 50.000 dòng PO – hãy chọn khoảng ngày ngắn hơn.");
            foreach (var g in rows.GroupBy(r => r.Id))
            {
                var o = g.First();
                var s = await SupplierAsync(o.SupplierId);
                var approved = VietnamDate(o.ApprovedAt!.Value);
                var status = o.Status == ApprovalDocumentStatus.Closed ? "Đã đóng" : "Đã duyệt";
                documents.Add(new PurchaseAccountingDocumentDto(
                    o.Id, DocumentTypes.PurchaseOrder, o.No, o.Date, approved, o.No, s.Code, s.LocalCode, s.Name, s.TaxCode, null, null, null, g.Count(),
                    g.Sum(x => x.Amount), g.Sum(x => x.VatAmount), g.Sum(x => x.Amount + x.VatAmount), status));
                orderLines.AddRange(g.Select(l => new object?[]
                {
                    approved, l.No, l.Date, CategoryName(l.Category), s.Code, s.LocalCode, s.Name, s.TaxCode, l.Department, l.Project, l.LineNo, l.ItemCode,
                    l.ItemName, l.UomCode, l.Qty, l.UnitPrice, l.DiscountPct, l.Amount, l.VatCode, l.VatAmount, l.Amount + l.VatAmount, l.CurrencyCode, status,
                }));
            }
        }

        if (types.Contains(DocumentTypes.GoodsReceiptConfirmation))
        {
            var rows = await (
                    from l in _db.Set<GoodsReceiptConfirmationLine>().AsNoTracking()
                    join c in _db.GoodsReceiptConfirmations on l.ConfirmationId equals c.Id
                    join o in _db.PurchaseOrders on c.PurchaseOrderId equals o.Id
                    join w in _db.Warehouses on c.WarehouseId equals w.Id
                    join i in _db.Items on l.ItemId equals i.Id
                    join u in _db.Uoms on l.UomId equals u.Id
                    where c.Status == ReceiptConfirmationStatus.Confirmed && c.Date >= query.From && c.Date <= query.To
                    orderby c.Date, c.No, l.LineNo
                    select new
                    {
                        c.Id, c.No, c.Date, c.SupplierId, OrderNo = o.No, c.DeliveryNoteNo, c.InvoiceNo, c.InvoiceSeries, c.InvoiceDate, c.InvoiceAmount,
                        Warehouse = w.Code, l.LineNo, ItemCode = i.Code, ItemName = i.Name, UomCode = u.Code, l.ReceivedQty, l.UnitPrice, l.Amount, l.VatCode,
                        l.VatAmount,
                    })
                .Take(MaxLines + 1)
                .ToListAsync(cancellationToken);
            if (rows.Count > MaxLines) throw new BusinessRuleException("Quá 50.000 dòng phiếu nhận hàng – hãy chọn khoảng ngày ngắn hơn.");
            foreach (var g in rows.GroupBy(r => r.Id))
            {
                var c = g.First();
                var s = await SupplierAsync(c.SupplierId);
                var status = c.InvoiceNo is null ? "Chưa có hóa đơn" : "Có hóa đơn";
                documents.Add(new PurchaseAccountingDocumentDto(
                    c.Id, DocumentTypes.GoodsReceiptConfirmation, c.No, c.Date, c.Date, c.OrderNo, s.Code, s.LocalCode, s.Name, s.TaxCode, c.InvoiceNo,
                    c.InvoiceSeries, c.InvoiceDate, g.Count(), g.Sum(x => x.Amount), g.Sum(x => x.VatAmount), g.Sum(x => x.Amount + x.VatAmount), status));
                receiptLines.AddRange(g.Select(l => new object?[]
                {
                    l.Date, l.No, l.OrderNo, s.Code, s.LocalCode, s.Name, s.TaxCode, l.DeliveryNoteNo, l.InvoiceSeries, l.InvoiceNo, l.InvoiceDate, l.InvoiceAmount,
                    l.Warehouse, l.LineNo, l.ItemCode, l.ItemName, l.UomCode, l.ReceivedQty, l.UnitPrice, l.Amount, l.VatCode, l.VatAmount, l.Amount + l.VatAmount,
                }));
            }
        }

        if (types.Contains(DocumentTypes.PaymentRequest))
        {
            var rows = await (
                    from l in _db.Set<PaymentRequestLine>().AsNoTracking()
                    join r in _db.PaymentRequests on l.RequestId equals r.Id
                    // 1C tạp vụ (lát Y11b): không có PO, XNH – dòng là bảng kê 2C; đối tượng là người nhận tiền.
                    join o in _db.PurchaseOrders on r.PurchaseOrderId equals o.Id into orders
                    from o in orders.DefaultIfEmpty()
                    join c in _db.GoodsReceiptConfirmations on l.ReceiptConfirmationId equals c.Id into receipts
                    from c in receipts.DefaultIfEmpty()
                    join k in _db.SundryExpenseSheets on l.SundryExpenseSheetId equals k.Id into sheets
                    from k in sheets.DefaultIfEmpty()
                    where r.Status == ApprovalDocumentStatus.Approved && r.ApprovedAt >= fromUtc && r.ApprovedAt < toUtc
                    orderby r.ApprovedAt, r.No, l.LineNo
                    select new
                    {
                        r.Id, r.No, r.Date, r.ApprovedAt, r.SupplierId, r.Form, r.Method, OrderNo = o == null ? null : o.No, r.BankName, r.BankBranch,
                        r.BankAccountNumber, r.BankAccountName, r.TotalAmount, r.DeductionAmount, r.DeductionReason, r.PayableAmount, r.PaidDate,
                        r.PaymentNote, l.LineNo, ReceiptNo = c != null ? c.No : k != null ? k.No : null, l.InvoiceSeries, l.InvoiceNo, l.InvoiceDate,
                        l.Description, l.Amount, r.PayeeName,
                    })
                .Take(MaxLines + 1)
                .ToListAsync(cancellationToken);
            if (rows.Count > MaxLines) throw new BusinessRuleException("Quá 50.000 dòng đề nghị thanh toán – hãy chọn khoảng ngày ngắn hơn.");
            foreach (var g in rows.GroupBy(r => r.Id))
            {
                var r = g.First();
                var s = r.SupplierId is { } supplierId ? await SupplierAsync(supplierId) : new SupplierInfo(string.Empty, null, r.PayeeName ?? string.Empty, null);
                var approved = VietnamDate(r.ApprovedAt!.Value);
                var status = r.PaidDate is { } paid ? $"Đã chi {paid:dd/MM/yyyy}" : "Đã duyệt, chờ chi";
                var form = PaymentRequestService.FormCode(r.Form);
                var method = r.Method == PaymentMethod.Cash ? "Tiền mặt" : "Chuyển khoản";
                // Một phiếu nhiều hóa đơn: danh sách xem trước nối số hóa đơn; ngày hóa đơn lấy ngày sớm nhất.
                documents.Add(new PurchaseAccountingDocumentDto(
                    r.Id, DocumentTypes.PaymentRequest, r.No, r.Date, approved, r.OrderNo, s.Code, s.LocalCode, s.Name, s.TaxCode,
                    string.Join(", ", g.Select(x => x.InvoiceNo).OfType<string>()), g.Select(x => x.InvoiceSeries).FirstOrDefault(x => x != null), g.Min(x => x.InvoiceDate),
                    g.Count(), r.TotalAmount, 0m, r.PayableAmount, status));
                paymentLines.AddRange(g.Select(l => new object?[]
                {
                    approved, l.No, l.Date, form, method, l.OrderNo, s.Code, s.LocalCode, s.Name, s.TaxCode, l.BankName, l.BankBranch, l.BankAccountNumber,
                    l.BankAccountName, l.LineNo, l.ReceiptNo, l.InvoiceSeries, l.InvoiceNo, l.InvoiceDate, l.Description, l.Amount, l.TotalAmount,
                    l.DeductionAmount, l.DeductionReason, l.PayableAmount, l.PaidDate, l.PaymentNote,
                }));
            }
        }

        return new Data(documents, orderLines, receiptLines, paymentLines);
    }

    /// <summary>Ngày Việt Nam của một thời điểm UTC.</summary>
    private static DateOnly VietnamDate(DateTimeOffset at) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(at, VietnamTime.Zone).DateTime);

    /// <summary>Tên loại mua của PO theo biểu mẫu CUVT (design.md mục 19.2).</summary>
    private static string CategoryName(PurchaseCategory category) => category switch
    {
        PurchaseCategory.Construction => "A – Công trình",
        PurchaseCategory.Service => "DV – Dịch vụ",
        _ => "B – Hàng hóa",
    };

    /// <summary>Cột của sheet: tiêu đề, kiểu (text, date, money, qty, price, pct), độ rộng, ý nghĩa (sheet hướng dẫn).</summary>
    private sealed record Column(string Title, string Type, double Width, string Guide = "");

    private static readonly Column[] DocumentColumns =
    [
        new("Loại CT", "text", 8), new("Tên loại CT", "text", 18), new("Ngày hiệu lực", "date", 11), new("Số CT", "docno", 20), new("Ngày CT", "date", 11),
        new("Số PO", "docno", 20), new("Mã NCC", "text", 14), new("Mã NCC tại công ty", "text", 14), new("Tên NCC", "text", 30), new("MST NCC", "text", 14),
        new("Số HĐ", "text", 14), new("Ký hiệu HĐ", "text", 10), new("Ngày HĐ", "date", 11), new("Số dòng", "qty", 8), new("Tiền trước thuế", "money", 15),
        new("Tiền thuế", "money", 13), new("Tổng tiền", "money", 15), new("Tình trạng", "text", 20),
    ];

    /// <summary>Giá trị các cột của một chứng từ trong sheet "Tổng hợp".</summary>
    private static object?[] DocumentValues(PurchaseAccountingDocumentDto d) =>
    [
        d.DocType, DocTypeNames[d.DocType], d.EffectiveDate, d.No, d.Date, d.PurchaseOrderNo, d.SupplierCode, d.SupplierLocalCode, d.SupplierName,
        d.SupplierTaxCode, d.InvoiceNo, d.InvoiceSeries, d.InvoiceDate, d.LineCount, d.Amount, d.VatAmount, d.TotalAmount, d.Status,
    ];

    private static readonly Column[] OrderColumns =
    [
        new("Ngày duyệt", "date", 11, "Ngày PO được duyệt (ngày lọc)."),
        new("Số PO", "docno", 20, "Số đơn mua hàng."),
        new("Ngày PO", "date", 11, "Ngày đơn mua hàng."),
        new("Loại mua", "text", 14, "A – Công trình, B – Hàng hóa, DV – Dịch vụ (biểu mẫu CUVT)."),
        new("Mã NCC", "text", 14, "Mã NCC trong hệ thống."),
        new("Mã NCC tại công ty", "text", 14, "Mã đối tượng NCC trong phần mềm kế toán của công ty (khai ở danh mục NCC)."),
        new("Tên NCC", "text", 30, "Tên NCC."),
        new("MST NCC", "text", 14, "Mã số thuế NCC."),
        new("Bộ phận", "text", 10, "Bộ phận đặt hàng."),
        new("Công trình", "text", 12, "Mã công trình (PO loại A)."),
        new("STT dòng", "qty", 7, "Số thứ tự dòng."),
        new("Mã hàng", "text", 16, "Mã hàng."),
        new("Tên hàng", "text", 30, "Tên hàng."),
        new("ĐVT", "text", 7, "Đơn vị tính trên PO."),
        new("Số lượng", "qty", 11, "Số lượng đặt."),
        new("Đơn giá", "price", 13, "Đơn giá trước chiết khấu, trước thuế."),
        new("% CK", "qty", 7, "Phần trăm chiết khấu."),
        new("Thành tiền", "money", 15, "Tiền hàng sau chiết khấu, trước thuế."),
        new("Thuế suất", "text", 8, "Mã thuế suất (10, 8, 5, 0, KCT…)."),
        new("Tiền thuế", "money", 13, "Tiền thuế GTGT."),
        new("Tổng thanh toán", "money", 15, "Thành tiền + tiền thuế."),
        new("Tiền tệ", "text", 6, "Mã tiền tệ."),
        new("Tình trạng", "text", 10, "Đã duyệt / Đã đóng (nhận đủ hoặc đóng phần còn lại)."),
    ];

    private static readonly Column[] ReceiptColumns =
    [
        new("Ngày nhận", "date", 11, "Ngày nhận hàng của phiếu xác nhận nhận hàng (ngày lọc)."),
        new("Số XNH", "docno", 20, "Số phiếu xác nhận nhận hàng."),
        new("Số PO", "docno", 20, "Đơn mua hàng của phiếu."),
        new("Mã NCC", "text", 14, "Mã NCC trong hệ thống."),
        new("Mã NCC tại công ty", "text", 14, "Mã đối tượng NCC trong phần mềm kế toán."),
        new("Tên NCC", "text", 30, "Tên NCC."),
        new("MST NCC", "text", 14, "Mã số thuế NCC."),
        new("Số phiếu giao", "text", 14, "Số phiếu giao hàng của NCC."),
        new("Ký hiệu HĐ", "text", 10, "Ký hiệu hóa đơn (nhập trên XNH – Cập nhật hóa đơn)."),
        new("Số HĐ", "text", 14, "Số hóa đơn; trống = chưa có hóa đơn."),
        new("Ngày HĐ", "date", 11, "Ngày hóa đơn."),
        new("Tiền HĐ (cả phiếu)", "money", 15, "Tiền hóa đơn gồm VAT của cả phiếu – lặp lại trên mọi dòng của phiếu, không cộng theo dòng."),
        new("Kho nhận", "text", 9, "Nơi nhận hàng."),
        new("STT dòng", "qty", 7, "Số thứ tự dòng."),
        new("Mã hàng", "text", 16, "Mã hàng."),
        new("Tên hàng", "text", 30, "Tên hàng."),
        new("ĐVT", "text", 7, "Đơn vị tính theo dòng PO."),
        new("SL thực nhận", "qty", 11, "Số lượng thực nhận (đạt + điều chỉnh)."),
        new("Đơn giá", "price", 13, "Đơn giá thực mua của PO (sau chiết khấu, trước thuế)."),
        new("Thành tiền", "money", 15, "SL thực nhận × đơn giá."),
        new("Thuế suất", "text", 8, "Mã thuế suất."),
        new("Tiền thuế", "money", 13, "Tiền thuế GTGT."),
        new("Tổng thanh toán", "money", 15, "Thành tiền + tiền thuế."),
    ];

    private static readonly Column[] PaymentColumns =
    [
        new("Ngày duyệt", "date", 11, "Ngày đề nghị thanh toán được duyệt (ngày lọc)."),
        new("Số DNT", "docno", 20, "Số đề nghị thanh toán."),
        new("Ngày DNT", "date", 11, "Ngày lập đề nghị."),
        new("Biểu mẫu", "text", 7, "5A (công trình) / 5B (hàng hóa, dịch vụ)."),
        new("Hình thức", "text", 12, "Tiền mặt / Chuyển khoản."),
        new("Số PO", "docno", 20, "Đơn mua hàng được thanh toán."),
        new("Mã NCC", "text", 14, "Mã NCC trong hệ thống."),
        new("Mã NCC tại công ty", "text", 14, "Mã đối tượng NCC trong phần mềm kế toán."),
        new("Tên NCC", "text", 30, "Tên NCC."),
        new("MST NCC", "text", 14, "Mã số thuế NCC."),
        new("Ngân hàng", "text", 20, "Ngân hàng nhận (chuyển khoản – chụp lúc lập phiếu)."),
        new("Chi nhánh", "text", 16, "Chi nhánh ngân hàng."),
        new("Số tài khoản", "text", 18, "Số tài khoản nhận."),
        new("Chủ tài khoản", "text", 24, "Chủ tài khoản."),
        new("STT dòng", "qty", 7, "Số thứ tự dòng hóa đơn."),
        new("Số XNH", "docno", 20, "Phiếu xác nhận nhận hàng của hóa đơn."),
        new("Ký hiệu HĐ", "text", 10, "Ký hiệu hóa đơn."),
        new("Số HĐ", "text", 14, "Số hóa đơn."),
        new("Ngày HĐ", "date", 11, "Ngày hóa đơn."),
        new("Nội dung", "text", 40, "Nội dung thanh toán của dòng."),
        new("Số tiền dòng", "money", 15, "Tiền hóa đơn gồm VAT."),
        new("Tổng tiền HĐ (phiếu)", "money", 15, "Tổng các dòng – lặp lại trên mọi dòng của phiếu."),
        new("Giảm trừ (phiếu)", "money", 13, "Số tiền giảm trừ của phiếu (tạm ứng, phạt…) – lặp lại trên mọi dòng."),
        new("Lý do giảm trừ", "text", 24, "Lý do giảm trừ."),
        new("Số tiền đề nghị (phiếu)", "money", 15, "Tổng − giảm trừ – số tiền chi cho NCC, lặp lại trên mọi dòng."),
        new("Ngày chi", "date", 11, "Ngày kế toán ghi Đã chi; trống = chưa chi."),
        new("Ghi chú chi", "text", 20, "Số ủy nhiệm chi, phiếu chi."),
    ];

    /// <summary>
    /// Ghi một sheet: dòng 1 tiêu đề, dòng 2 giờ xuất, dòng 4 tiêu đề cột (cố định, có lọc), dữ liệu từ dòng 5. Cột chữ để kiểu Text (mã có số 0
    /// đầu, MST không bị Excel đổi thành số); cột số, ngày giữ kiểu số, ngày để kế toán cộng và nhập vào phần mềm.
    /// </summary>
    private void WriteSheet(IXLWorksheet sheet, string title, Column[] columns, IEnumerable<object?[]> rows)
    {
        ExcelGridStyle.Title(sheet.Cell(1, 1), title);
        ExcelGridStyle.Note(sheet.Cell(2, 1), $"Xuất lúc {TimeZoneInfo.ConvertTime(_clock.UtcNow, VietnamTime.Zone):dd/MM/yyyy HH:mm}. PO, đề nghị thanh toán theo ngày duyệt; phiếu nhận hàng theo ngày nhận.");
        const int header = 4;
        for (var c = 0; c < columns.Length; c++)
        {
            sheet.Cell(header, c + 1).Value = columns[c].Title;
            var column = sheet.Column(c + 1);
            column.Width = columns[c].Width;
            column.Style.NumberFormat.Format = columns[c].Type switch
            {
                "date" => "dd/mm/yyyy",
                "money" => "#,##0",
                "qty" => "#,##0.####",
                "price" => "#,##0.######",
                // Số chứng từ, mã, MST: kiểu Text để Excel không đổi chuỗi toàn chữ số thành số.
                _ => "@",
            };
        }

        var r = header;
        foreach (var values in rows)
        {
            r++;
            for (var c = 0; c < columns.Length; c++)
            {
                sheet.Cell(r, c + 1).Value = values[c] switch
                {
                    null => Blank.Value,
                    DateOnly date => date.ToDateTime(TimeOnly.MinValue),
                    decimal number => number,
                    int number => number,
                    var text => text.ToString(),
                };
            }
        }

        var last = Math.Max(r, header + 1);
        for (var c = 0; c < columns.Length; c++)
        {
            var data = sheet.Range(header + 1, c + 1, last, c + 1);
            switch (columns[c].Type)
            {
                case "money" or "qty" or "price":
                    ExcelGridStyle.Number(data);
                    break;
                case "date":
                    ExcelGridStyle.Date(data);
                    break;
                case "docno":
                    ExcelGridStyle.DocNo(data);
                    break;
            }
        }

        ExcelGridStyle.Header(sheet.Range(header, 1, header, columns.Length));
        ExcelGridStyle.Body(sheet.Range(header, 1, last, columns.Length));
        sheet.Range(header, 1, last, columns.Length).SetAutoFilter();
        sheet.SheetView.FreezeRows(header);
    }

    /// <summary>Ghi sheet hướng dẫn dùng file cho phần mềm kế toán: ghi chú chung và ý nghĩa cột của ba sheet chi tiết.</summary>
    private static void WriteGuide(IXLWorksheet sheet)
    {
        ExcelGridStyle.Title(sheet.Cell(1, 1), "Hướng dẫn dùng file cho phần mềm kế toán");
        string[] notes =
        [
            "Sheet \"Tổng hợp\": mỗi chứng từ một dòng; các sheet còn lại: mỗi dòng hàng / dòng hóa đơn một dòng.",
            "Đơn mua hàng: PO đã duyệt hoặc đã đóng, theo ngày duyệt. Nhận hàng – hóa đơn: phiếu xác nhận nhận hàng đã xác nhận, theo ngày nhận, kèm hóa đơn nhập trên phiếu.",
            "Đề nghị thanh toán: phiếu đã duyệt (gồm đã chi), theo ngày duyệt. Hệ thống không làm sổ cái – công nợ, định khoản làm ở phần mềm kế toán.",
            "Dùng chức năng nhập chứng từ từ Excel của phần mềm kế toán (MISA, Fast…), ghép cột theo tên dưới đây. Chỉ gồm công ty đang làm việc.",
        ];
        for (var i = 0; i < notes.Length; i++) sheet.Cell(i + 3, 1).Value = notes[i];
        var row = notes.Length + 4;
        foreach (var (name, columns) in new[] { ("Đơn mua hàng", OrderColumns), ("Nhận hàng – hóa đơn", ReceiptColumns), ("Đề nghị thanh toán", PaymentColumns) })
        {
            ExcelGridStyle.SectionTitle(sheet.Cell(row, 1), $"Sheet \"{name}\"");
            row++;
            var tableStart = row;
            sheet.Cell(row, 1).Value = "Cột";
            sheet.Cell(row, 2).Value = "Ý nghĩa";
            row++;
            foreach (var column in columns)
            {
                sheet.Cell(row, 1).Value = column.Title;
                sheet.Cell(row, 2).Value = column.Guide;
                row++;
            }

            ExcelGridStyle.Header(sheet.Range(tableStart, 1, tableStart, 2));
            ExcelGridStyle.Body(sheet.Range(tableStart, 1, row - 1, 2));
            row++;
        }

        sheet.Column(1).Width = 24;
        sheet.Column(2).Width = 110;
    }
}
