using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Proc.Application.Common;
using Proc.Application.Inventory;
using Proc.Application.Printing;
using Proc.Application.Purchasing;
using Proc.Domain.Common;
using Proc.Domain.Documents;
using Proc.Infrastructure.Persistence;

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

    /// <summary>Tạo dịch vụ.</summary>
    /// <param name="db">DbContext (đọc thông tin công ty, tên kho).</param>
    /// <param name="scope">Phạm vi người dùng.</param>
    /// <param name="orders">Dịch vụ đơn mua hàng.</param>
    /// <param name="requests">Dịch vụ đề nghị mua hàng.</param>
    /// <param name="inventory">Dịch vụ chứng từ kho.</param>
    /// <param name="options">Cấu hình in (thư mục phông).</param>
    public DocumentPdfService(
        ProcDbContext db, ICurrentUserScope scope, IPurchaseOrderService orders, IPurchaseRequestService requests, IInventoryDocumentService inventory,
        IOptions<PrintingOptions> options)
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
                l.LineNo.ToString(Vi), l.ItemCode, l.ItemName + (l.DeliveryDate is { } d ? $" (giao {d:dd/MM/yyyy})" : ""), l.UomCode,
                Qty(l.Qty), Price(l.UnitPrice), l.DiscountPct > 0 ? Qty(l.DiscountPct) : "", Money(l.Amount), Vat(l.VatCode),
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
        var pr = await _requests.GetAsync(id, cancellationToken);
        var approver = pr.Approvals.LastOrDefault(a => a.Status == "Approved")?.ActedByName;
        var warehouse = pr.WarehouseId is { } w ? await WarehouseNameAsync(w, cancellationToken) : null;
        var doc = new PrintDocument(
            "PHIẾU ĐỀ NGHỊ MUA HÀNG", pr.No, pr.Date, ApprovalNote(pr.Status),
            [
                ("Bộ phận đề nghị", pr.Department),
                ("Người đề nghị", pr.RequesterName),
                ("Mục đích sử dụng", pr.Purpose),
                ("Kho nhận", warehouse),
                ("Ngày cần hàng", Date(pr.NeededDate)),
                ("Mức ưu tiên", pr.Priority switch { "Urgent" => "Gấp", "Low" => "Thấp", _ => "Thường" }),
            ],
            [
                new("STT", 8), new("Mã hàng", 27), new("Tên hàng", 53), new("ĐVT", 12), new("SL", 16, true), new("Giá tham khảo", 20, true),
                new("Thành tiền", 24, true), new("Ngày cần", 20),
            ],
            pr.Lines.Select(l => new[]
            {
                l.LineNo.ToString(Vi), l.ItemCode, l.ItemName + (string.IsNullOrWhiteSpace(l.Note) ? "" : $" – {l.Note}"), l.UomCode, Qty(l.Qty),
                Price(l.EstPrice), Money(l.Amount), Date(l.NeededDate ?? pr.NeededDate) ?? "",
            }).ToList(),
            [new("Tổng giá trị ước tính", Money(pr.TotalAmount), true)],
            VietnameseNumberText.ToVndWords(pr.TotalAmount), pr.Note,
            ["Người đề nghị", "Trưởng bộ phận", "Người duyệt"], [pr.RequesterName ?? pr.CreatedByName, null, approver]);
        return new PdfFile($"{pr.No}.pdf", PdfLayout.Render(await CompanyAsync(cancellationToken), doc));
    }

    /// <inheritdoc />
    public async Task<PdfFile> InventoryDocumentAsync(Guid id, CancellationToken cancellationToken)
    {
        var d = await _inventory.GetAsync(id, cancellationToken);
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

    /// <summary>Ghi chú trạng thái của PR/PO chưa có hiệu lực.</summary>
    private static string? ApprovalNote(string status) => status switch
    {
        "Draft" => "BẢN NHÁP – chưa duyệt",
        "Submitted" => "ĐANG CHỜ DUYỆT – chưa có hiệu lực",
        "Cancelled" => "ĐÃ HỦY",
        _ => null,
    };

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

    private static string Money(decimal value) => Math.Round(value, 0, MidpointRounding.AwayFromZero).ToString("#,##0", Vi);

    private static string Qty(decimal value) => value.ToString("#,##0.####", Vi);

    private static string Price(decimal value) => value.ToString("#,##0.##", Vi);

    private static string Vat(string? code) => code is null ? "" : code.All(char.IsDigit) ? $"{code}%" : code;

    private static string? Date(DateOnly? value) => value?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
}
