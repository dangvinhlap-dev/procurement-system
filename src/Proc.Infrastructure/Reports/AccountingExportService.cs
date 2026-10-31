using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Proc.Application.Common;
using Proc.Application.Excel;
using Proc.Application.Reports;
using Proc.Domain.Common;
using Proc.Domain.Documents;
using Proc.Domain.Inventory;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.Reports;

/// <summary>
/// Xuất chứng từ kho đã ghi sổ ra Excel cho kế toán (design.md mục 11.5). Đọc qua global query filter: công ty đang làm việc, kho và nhóm hàng
/// được phép. Giá vốn lấy từ sổ kho của đúng dòng chứng từ; tài khoản kho lấy theo nhóm hàng (nhóm không khai báo thì lấy của nhóm cha gần nhất).
/// </summary>
public sealed class AccountingExportService : IAccountingExportService
{
    /// <summary>Khoảng ngày tối đa mỗi lần xuất (hai năm).</summary>
    public const int MaxDays = 731;

    /// <summary>Số dòng hàng tối đa mỗi file.</summary>
    public const int MaxLines = 50_000;

    /// <summary>Tên loại chứng từ in trong file.</summary>
    public static readonly IReadOnlyDictionary<string, string> DocTypeNames = new Dictionary<string, string>
    {
        [DocumentTypes.GoodsReceipt] = "Nhập mua NCC",
        [DocumentTypes.ReturnToVendor] = "Xuất trả NCC",
        [DocumentTypes.Transfer] = "Chuyển kho nội bộ",
        [DocumentTypes.IntercompanySale] = "Xuất bán nội bộ",
        [DocumentTypes.IntercompanyReceipt] = "Nhập mua nội bộ",
    };

    private readonly ProcDbContext _db;
    private readonly ICurrentUserScope _scope;
    private readonly IClock _clock;

    /// <summary>Tạo dịch vụ.</summary>
    /// <param name="db">DbContext (đã gắn phạm vi người dùng).</param>
    /// <param name="scope">Phạm vi người dùng.</param>
    /// <param name="clock">Đồng hồ (giờ xuất file).</param>
    public AccountingExportService(ProcDbContext db, ICurrentUserScope scope, IClock clock)
    {
        _db = db;
        _scope = scope;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AccountingDocumentDto>> ListAsync(AccountingExportQuery query, CancellationToken cancellationToken) =>
        Summarize(await LinesAsync(query, cancellationToken));

    /// <inheritdoc />
    public async Task<ExcelFile> ExportAsync(AccountingExportQuery query, CancellationToken cancellationToken)
    {
        var lines = await LinesAsync(query, cancellationToken);
        var documents = Summarize(lines);
        var company = await _db.Companies.AsNoTracking().Where(c => c.Id == _scope.RequireCompany()).Select(c => c.Code).SingleAsync(cancellationToken);

        using var workbook = new XLWorkbook();
        var title = $"Chứng từ kho đã ghi sổ {company} từ {query.From:dd/MM/yyyy} đến {query.To:dd/MM/yyyy}";
        WriteSheet(workbook.AddWorksheet("Chứng từ"), title, LineColumns, lines.Select(LineValues));
        WriteSheet(workbook.AddWorksheet("Tổng hợp"), title, DocumentColumns, documents.Select(DocumentValues));
        WriteGuide(workbook.AddWorksheet("Hướng dẫn"));
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return new ExcelFile($"chung-tu-ke-toan-{company.ToLowerInvariant()}-{query.From:yyyyMMdd}-{query.To:yyyyMMdd}.xlsx", stream.ToArray());
    }

    /// <summary>Một dòng hàng của chứng từ đã ghi sổ, đủ thông tin cho kế toán.</summary>
    private sealed record Line(
        Guid DocumentId, string DocType, string No, DateOnly Date, DateOnly? PostedDate, string? PartnerCode, string? PartnerLocalCode, string? PartnerName,
        string? PartnerTaxCode, string? InvoiceNo, string? InvoiceSeries, DateOnly? InvoiceDate, string? FromWarehouse, string? ToWarehouse, string? PurchaseOrderNo,
        string? LinkedNo, string? Note, int LineNo, string ItemCode, string ItemName, string? InventoryAccount, string UomCode, decimal Qty, decimal UnitPrice,
        decimal Amount, string? VatCode, decimal VatAmount, string BaseUomCode, decimal BaseQty, decimal UnitCost, decimal CostAmount, bool IsCostFinal,
        string? LotNo, DateOnly? ExpiryDate);

    private async Task<List<Line>> LinesAsync(AccountingExportQuery query, CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        if (query.From > query.To) throw new BusinessRuleException("Từ ngày phải trước hoặc bằng đến ngày.");
        if (query.To.DayNumber - query.From.DayNumber >= MaxDays) throw new BusinessRuleException("Xuất tối đa hai năm mỗi lần.");
        var types = query.DocTypes.Count == 0 ? DocTypeNames.Keys.ToList() : query.DocTypes.ToList();
        if (types.FirstOrDefault(t => !DocTypeNames.ContainsKey(t)) is { } bad) throw new BusinessRuleException($"Loại chứng từ {bad} không xuất cho kế toán.");

        var rows = await (
                from l in _db.InventoryDocumentLines.AsNoTracking()
                join d in _db.InventoryDocuments on l.DocumentId equals d.Id
                join i in _db.Items on l.ItemId equals i.Id
                join u in _db.Uoms on l.UomId equals u.Id
                join bu in _db.Uoms on i.BaseUomId equals bu.Id
                where d.Status == DocumentStatus.Posted && types.Contains(d.DocType) && d.Date >= query.From && d.Date <= query.To
                orderby d.Date, d.No, l.LineNo
                select new
                {
                    LineId = l.Id, d.Id, d.DocType, d.No, d.Date, d.PostedAt, d.SupplierId, d.CounterCompanyId, d.InvoiceNo, d.InvoiceSeries, d.InvoiceDate,
                    d.FromWarehouseId, d.ToWarehouseId, d.PurchaseOrderId, d.LinkedDocumentId, d.Note, l.LineNo, ItemCode = i.Code, ItemName = i.Name,
                    i.CategoryId, UomCode = u.Code, l.Qty, l.UnitPrice, l.Amount, l.VatCode, l.VatAmount, BaseUomCode = bu.Code, l.BaseQty, l.LotNo, l.ExpiryDate,
                })
            .Take(MaxLines + 1)
            .ToListAsync(cancellationToken);
        if (rows.Count > MaxLines) throw new BusinessRuleException("Quá 50.000 dòng hàng – hãy chọn khoảng ngày ngắn hơn hoặc ít loại chứng từ hơn.");

        // Giá vốn của dòng theo sổ kho: phiếu xuất (trả NCC, bán nội bộ) và chuyển kho lấy chiều xuất; phiếu nhập lấy chiều nhập.
        var lineIds = rows.Select(r => r.LineId).ToList();
        var ledger = (await _db.StockLedger.AsNoTracking().Where(x => lineIds.Contains(x.DocumentLineId))
                .Select(x => new { x.DocumentLineId, x.Direction, x.BaseQty, x.Amount, x.IsCostFinal })
                .ToListAsync(cancellationToken))
            .ToLookup(x => x.DocumentLineId);

        var supplierIds = rows.Where(r => r.SupplierId != null).Select(r => r.SupplierId!.Value).Distinct().ToList();
        var suppliers = await _db.Suppliers.AsNoTracking().Where(s => supplierIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => new { s.Code, s.Name, s.TaxCode }, cancellationToken);
        var localCodes = await _db.SupplierCompanies.AsNoTracking().Where(s => supplierIds.Contains(s.SupplierId))
            .ToDictionaryAsync(s => s.SupplierId, s => s.LocalCode, cancellationToken);
        var companyIds = rows.Where(r => r.CounterCompanyId != null).Select(r => r.CounterCompanyId!.Value).Distinct().ToList();
        var companies = await _db.Companies.AsNoTracking().Where(c => companyIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => new { c.Code, c.Name, c.TaxCode }, cancellationToken);
        var warehouses = await _db.Warehouses.AsNoTracking().ToDictionaryAsync(w => w.Id, w => w.Code, cancellationToken);
        var orderIds = rows.Where(r => r.PurchaseOrderId != null).Select(r => r.PurchaseOrderId!.Value).Distinct().ToList();
        var orders = await _db.PurchaseOrders.AsNoTracking().Where(o => orderIds.Contains(o.Id)).ToDictionaryAsync(o => o.Id, o => o.No, cancellationToken);
        // Chứng từ liên quan của công ty khác (phiếu xuất bán của bên bán) nằm ngoài filter công ty nên không có số – để trống.
        var linkedIds = rows.Where(r => r.LinkedDocumentId != null).Select(r => r.LinkedDocumentId!.Value).Distinct().ToList();
        var linked = await _db.InventoryDocuments.AsNoTracking().Where(d => linkedIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id, d => d.No, cancellationToken);
        var categories = await _db.ItemCategories.AsNoTracking().ToDictionaryAsync(c => c.Id, c => (c.ParentId, c.InventoryAccount), cancellationToken);

        return rows.Select(r =>
        {
            var outgoing = r.DocType is DocumentTypes.ReturnToVendor or DocumentTypes.Transfer or DocumentTypes.IntercompanySale;
            var entries = ledger[r.LineId].Where(x => x.Direction == (outgoing ? -1 : 1)).ToList();
            var cost = entries.Sum(x => x.Amount);
            var baseQty = entries.Sum(x => x.BaseQty);
            // Giá vốn đơn vị theo ĐVT cơ sở, ví dụ 600.000 / 12 cái = 50.000; 6 số lẻ theo numeric(19,6).
            var unitCost = baseQty == 0 ? 0 : Math.Round(cost / baseQty, 6, MidpointRounding.AwayFromZero);
            var (partnerCode, localCode, partnerName, taxCode) = r.SupplierId is { } sid && suppliers.TryGetValue(sid, out var s)
                ? (s.Code, localCodes.GetValueOrDefault(sid), s.Name, s.TaxCode)
                : r.CounterCompanyId is { } cid && companies.TryGetValue(cid, out var c)
                    ? (c.Code, null, c.Name, c.TaxCode)
                    : ((string?)null, (string?)null, (string?)null, (string?)null);
            return new Line(
                r.Id, r.DocType, r.No, r.Date, r.PostedAt is { } at ? DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(at, VietnamTime.Zone).DateTime) : null,
                partnerCode, localCode, partnerName, taxCode, r.InvoiceNo, r.InvoiceSeries, r.InvoiceDate,
                r.FromWarehouseId is { } f ? warehouses.GetValueOrDefault(f) : null, r.ToWarehouseId is { } t ? warehouses.GetValueOrDefault(t) : null,
                r.PurchaseOrderId is { } o ? orders.GetValueOrDefault(o) : null, r.LinkedDocumentId is { } ld ? linked.GetValueOrDefault(ld) : null,
                r.Note, r.LineNo, r.ItemCode, r.ItemName, AccountOf(r.CategoryId, categories), r.UomCode, r.Qty, r.UnitPrice, r.Amount, r.VatCode, r.VatAmount,
                r.BaseUomCode, r.BaseQty, unitCost, cost, entries.Count > 0 && entries.All(x => x.IsCostFinal), r.LotNo, r.ExpiryDate);
        }).ToList();
    }

    /// <summary>Tài khoản kho của nhóm hàng; không khai báo thì lấy của nhóm cha gần nhất.</summary>
    private static string? AccountOf(Guid categoryId, Dictionary<Guid, (Guid? ParentId, string? InventoryAccount)> categories)
    {
        var visited = new HashSet<Guid>();
        for (Guid? id = categoryId; id is { } c && visited.Add(c) && categories.TryGetValue(c, out var node); id = node.ParentId)
        {
            if (!string.IsNullOrWhiteSpace(node.InventoryAccount)) return node.InventoryAccount;
        }

        return null;
    }

    private static List<AccountingDocumentDto> Summarize(IEnumerable<Line> lines) =>
        lines.GroupBy(l => l.DocumentId)
            .Select(g =>
            {
                var f = g.First();
                return new AccountingDocumentDto(
                    f.DocumentId, f.DocType, f.No, f.Date, f.PartnerCode, f.PartnerLocalCode, f.PartnerName, f.PartnerTaxCode, f.InvoiceNo, f.InvoiceSeries,
                    f.InvoiceDate, g.Count(), g.Sum(l => l.Amount), g.Sum(l => l.VatAmount), g.Sum(l => l.CostAmount), g.All(l => l.IsCostFinal));
            })
            .ToList();

    /// <summary>Cột của sheet: tiêu đề, kiểu (text, date, money, qty, price), độ rộng.</summary>
    private sealed record Column(string Title, string Type, double Width, string Guide);

    private static readonly Column[] LineColumns =
    [
        new("Ngày CT", "date", 11, "Ngày chứng từ (ngày hạch toán)."),
        new("Số CT", "text", 20, "Số chứng từ trong hệ thống mua hàng – kho."),
        new("Loại CT", "text", 8, "GRN nhập mua NCC, RTV xuất trả NCC, TRF chuyển kho, ICS xuất bán nội bộ, ICR nhập mua nội bộ."),
        new("Tên loại CT", "text", 18, "Tên loại chứng từ."),
        new("Ngày ghi sổ", "date", 11, "Ngày ghi sổ kho."),
        new("Mã đối tượng", "text", 14, "Mã NCC (GRN, RTV) hoặc mã công ty đối ứng (ICS, ICR)."),
        new("Mã NCC tại công ty", "text", 14, "Mã NCC khai báo riêng ở công ty – dùng khớp mã đối tượng trong phần mềm kế toán."),
        new("Tên đối tượng", "text", 30, "Tên NCC hoặc công ty đối ứng."),
        new("MST đối tượng", "text", 14, "Mã số thuế."),
        new("Số HĐ", "text", 12, "Số hóa đơn."),
        new("Ký hiệu HĐ", "text", 10, "Ký hiệu hóa đơn."),
        new("Ngày HĐ", "date", 11, "Ngày hóa đơn."),
        new("Kho xuất", "text", 9, "Mã kho xuất."),
        new("Kho nhập", "text", 9, "Mã kho nhập."),
        new("Theo PO", "text", 18, "Số đơn mua hàng (phiếu nhập theo PO)."),
        new("CT liên quan", "text", 18, "Phiếu nhập gốc (RTV) hoặc phiếu xuất bán / nhập mua đối ứng."),
        new("STT dòng", "qty", 7, "Số thứ tự dòng trên chứng từ."),
        new("Mã hàng", "text", 16, "Mã hàng."),
        new("Tên hàng", "text", 30, "Tên hàng."),
        new("TK kho", "text", 8, "Tài khoản kho theo nhóm hàng (nhóm không khai báo thì theo nhóm cha)."),
        new("ĐVT", "text", 7, "Đơn vị tính trên chứng từ."),
        new("Số lượng", "qty", 11, "Số lượng theo ĐVT chứng từ."),
        new("Đơn giá", "price", 13, "Đơn giá trước thuế (chuyển kho: 0)."),
        new("Thành tiền", "money", 15, "Tiền hàng trước thuế."),
        new("Thuế suất", "text", 8, "Mã thuế suất (10, 8, 5, 0, KCT…)."),
        new("Tiền thuế", "money", 13, "Tiền thuế GTGT."),
        new("Tổng thanh toán", "money", 15, "Thành tiền + tiền thuế."),
        new("ĐVT cơ sở", "text", 8, "Đơn vị tính cơ sở của mặt hàng."),
        new("SL cơ sở", "qty", 11, "Số lượng quy về ĐVT cơ sở."),
        new("Giá vốn đơn vị", "price", 13, "Giá vốn theo ĐVT cơ sở (phiếu nhập = giá nhập)."),
        new("Tiền vốn", "money", 15, "Giá vốn của dòng theo sổ kho."),
        new("Giá vốn chính thức", "text", 10, "Có = kỳ đã đóng; Tạm tính = kỳ còn mở, số có thể đổi khi đóng kỳ."),
        new("Số lô", "text", 12, "Số lô."),
        new("Hạn dùng", "date", 11, "Hạn dùng của lô."),
        new("Diễn giải", "text", 30, "Ghi chú chứng từ."),
    ];

    private static object?[] LineValues(Line l) =>
    [
        l.Date, l.No, l.DocType, DocTypeNames[l.DocType], l.PostedDate, l.PartnerCode, l.PartnerLocalCode, l.PartnerName, l.PartnerTaxCode,
        l.InvoiceNo, l.InvoiceSeries, l.InvoiceDate, l.FromWarehouse, l.ToWarehouse, l.PurchaseOrderNo, l.LinkedNo, l.LineNo, l.ItemCode, l.ItemName,
        l.InventoryAccount, l.UomCode, l.Qty, l.UnitPrice, l.Amount, l.VatCode, l.VatAmount, l.Amount + l.VatAmount, l.BaseUomCode, l.BaseQty,
        l.UnitCost, l.CostAmount, l.IsCostFinal ? "Có" : "Tạm tính", l.LotNo, l.ExpiryDate, l.Note,
    ];

    private static readonly Column[] DocumentColumns =
    [
        new("Ngày CT", "date", 11, ""), new("Số CT", "text", 20, ""), new("Loại CT", "text", 8, ""), new("Tên loại CT", "text", 18, ""),
        new("Mã đối tượng", "text", 14, ""), new("Mã NCC tại công ty", "text", 14, ""), new("Tên đối tượng", "text", 30, ""), new("MST đối tượng", "text", 14, ""),
        new("Số HĐ", "text", 12, ""), new("Ký hiệu HĐ", "text", 10, ""), new("Ngày HĐ", "date", 11, ""), new("Số dòng", "qty", 8, ""),
        new("Tiền hàng", "money", 15, ""), new("Tiền thuế", "money", 13, ""), new("Tổng thanh toán", "money", 15, ""), new("Tiền vốn", "money", 15, ""),
        new("Giá vốn chính thức", "text", 10, ""),
    ];

    private static object?[] DocumentValues(AccountingDocumentDto d) =>
    [
        d.Date, d.No, d.DocType, DocTypeNames[d.DocType], d.PartnerCode, d.PartnerLocalCode, d.PartnerName, d.PartnerTaxCode, d.InvoiceNo, d.InvoiceSeries,
        d.InvoiceDate, d.LineCount, d.Amount, d.VatAmount, d.Amount + d.VatAmount, d.CostAmount, d.IsCostFinal ? "Có" : "Tạm tính",
    ];

    /// <summary>
    /// Ghi một sheet: dòng 1 tiêu đề, dòng 2 giờ xuất, dòng 4 tiêu đề cột (cố định, có lọc), dữ liệu từ dòng 5. Cột chữ để kiểu Text (mã có số 0
    /// đầu, MST không bị Excel đổi thành số); cột số, ngày giữ kiểu số, ngày để kế toán cộng và nhập vào phần mềm.
    /// </summary>
    private void WriteSheet(IXLWorksheet sheet, string title, Column[] columns, IEnumerable<object?[]> rows)
    {
        sheet.Cell(1, 1).Value = title;
        sheet.Cell(1, 1).Style.Font.SetBold().Font.SetFontSize(13);
        sheet.Cell(2, 1).Value = $"Xuất lúc {TimeZoneInfo.ConvertTime(_clock.UtcNow, VietnamTime.Zone):dd/MM/yyyy HH:mm}. Chỉ gồm chứng từ đã ghi sổ.";
        sheet.Cell(2, 1).Style.Font.SetItalic();
        const int header = 4;
        for (var c = 0; c < columns.Length; c++)
        {
            var cell = sheet.Cell(header, c + 1);
            cell.Value = columns[c].Title;
            cell.Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.FromHtml("#DCE6F4")).Border.SetOutsideBorder(XLBorderStyleValues.Thin);
            var column = sheet.Column(c + 1);
            column.Width = columns[c].Width;
            column.Style.NumberFormat.Format = columns[c].Type switch
            {
                "date" => "dd/mm/yyyy",
                "money" => "#,##0",
                "qty" => "#,##0.####",
                "price" => "#,##0.######",
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

        sheet.Range(header, 1, Math.Max(r, header + 1), columns.Length).SetAutoFilter();
        sheet.SheetView.FreezeRows(header);
    }

    private static void WriteGuide(IXLWorksheet sheet)
    {
        sheet.Cell(1, 1).Value = "Hướng dẫn dùng file cho phần mềm kế toán";
        sheet.Cell(1, 1).Style.Font.SetBold().Font.SetFontSize(13);
        string[] notes =
        [
            "Sheet \"Chứng từ\": mỗi dòng hàng một dòng; sheet \"Tổng hợp\": mỗi chứng từ một dòng.",
            "Dùng chức năng nhập chứng từ từ Excel của phần mềm kế toán (MISA, Fast…), ghép cột theo tên dưới đây; định khoản trong phần mềm kế toán.",
            "Chỉ gồm chứng từ đã ghi sổ của công ty đang làm việc, trong phạm vi kho và nhóm hàng của người xuất.",
            "Giá vốn \"Tạm tính\": kỳ kho còn mở, giá vốn xuất chốt khi đóng kỳ – nên xuất lại sau khi đóng kỳ.",
        ];
        for (var i = 0; i < notes.Length; i++) sheet.Cell(i + 3, 1).Value = notes[i];
        var start = notes.Length + 4;
        sheet.Cell(start, 1).Value = "Cột";
        sheet.Cell(start, 2).Value = "Ý nghĩa";
        sheet.Range(start, 1, start, 2).Style.Font.SetBold();
        for (var i = 0; i < LineColumns.Length; i++)
        {
            sheet.Cell(start + 1 + i, 1).Value = LineColumns[i].Title;
            sheet.Cell(start + 1 + i, 2).Value = LineColumns[i].Guide;
        }

        sheet.Column(1).Width = 22;
        sheet.Column(2).Width = 110;
    }
}
