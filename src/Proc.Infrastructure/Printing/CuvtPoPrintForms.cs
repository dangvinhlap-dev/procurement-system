using System.Globalization;
using Proc.Application.Purchasing;
using Proc.Domain.Common;
using Proc.Domain.MasterData;

namespace Proc.Infrastructure.Printing;

/// <summary>
/// Số liệu ngoài phiếu PO cho bảng theo dõi mua hàng 4A/4B (lát Y7) – dịch vụ in đọc theo phạm vi người dùng rồi truyền vào hàm thuần.
/// </summary>
/// <param name="CompanyName">Tên công ty.</param>
/// <param name="ProjectName">Tên công trình (4A).</param>
/// <param name="DepartmentName">Phòng ban đề xuất: 4A của công trình; 4B của các đề nghị nguồn (một bộ phận), không thì bộ phận đặt hàng.</param>
/// <param name="GoodsTitle">4B: "Tên hàng hóa" – mục đích các đề nghị nguồn, không có thì tên các mặt hàng.</param>
/// <param name="BudgetAmount">Định mức/dự trù ngân sách: 4A tổng dự toán công trình; 4B ngân sách tháng của bộ phận; null = chưa khai báo.</param>
/// <param name="PastNet">Đã mua trước đây (không gồm đơn này), chưa VAT – cùng cách tính lũy kế của 1A/1B.</param>
/// <param name="PastVat">Tiền VAT của phần đã mua trước đây.</param>
/// <param name="SupplierPhone">Điện thoại NCC.</param>
/// <param name="Specifications">Mặt hàng → đặc điểm/quy cách.</param>
/// <param name="LastPrices">Dòng PO → đơn giá cũ đã mua gần nhất theo ĐVT của dòng (sau CK, trước VAT); không có = mua lần đầu.</param>
public sealed record PoPrintContext(
    string CompanyName, string? ProjectName, string? DepartmentName, string? GoodsTitle, decimal? BudgetAmount, decimal PastNet, decimal PastVat,
    string? SupplierPhone, IReadOnlyDictionary<Guid, string?> Specifications, IReadOnlyDictionary<Guid, decimal> LastPrices);

/// <summary>Bảng theo dõi mua hàng 4A (công trình) / 4B (hàng hóa, dịch vụ) – biểu mẫu ký trước khi đặt hàng = duyệt PO (design.md 19.1 Q10).</summary>
public static partial class CuvtPrintForms
{
    /// <summary>
    /// Bảng theo dõi mua hàng của một PO (khổ ngang): giá cũ gần nhất, giá mới ghi vào cột <b>tăng</b> hoặc <b>bằng–giảm</b>, NCC, lũy kế và
    /// số còn lại theo dự toán/định mức (gộp ô dọc như mẫu). Lũy kế = đã mua trước đây + đơn này (ghi chú biểu mẫu: "ghi gộp số tiền đã mua
    /// trước đây + số tiền kèm theo đề xuất"). Ví dụ dự toán 64.000.000, trước đây 0, đơn 54.500.000 → lũy kế 54.500.000, còn lại 9.500.000.
    /// </summary>
    /// <param name="po">PO đã đọc theo phạm vi.</param>
    /// <param name="context">Số liệu ngoài phiếu.</param>
    /// <returns>Nội dung in.</returns>
    public static CuvtForm PurchaseOrder(PurchaseOrderDto po, PoPrintContext context)
    {
        var is4A = po.FormCode == "4A";
        var columns = new List<CuvtColumn>
        {
            new("Stt", 9, Center: true), new("Tên hàng hóa - vật tư", 40), new("Đặc điểm/quy cách", 28), new("ĐVT", 12, Center: true),
            new("Số lượng", 16, true), new("Đơn giá cũ gần nhất", 22, true), new("Đơn giá mới tăng", 21, true), new("Đơn giá mới bằng-giảm", 21, true),
            new("Thành tiền", 26, true), new("Tên Nhà cung cấp đã mua", 28, Center: true, MergeRows: true),
            new("Lũy kế số tiền đã mua", 25, true, MergeRows: true), new("Số tiền còn lại theo dự toán/định mức", 25, true, MergeRows: true),
        };

        var spentNet = context.PastNet + po.TotalAmount;
        var spentVat = context.PastVat + po.TotalVat;
        var remaining = context.BudgetAmount is { } budget ? Money(budget - spentNet) : "Chưa khai báo";
        var supplier = po.SupplierName + (string.IsNullOrWhiteSpace(context.SupplierPhone) ? "" : $" ({context.SupplierPhone.Trim()})");

        var rows = po.Lines.OrderBy(l => l.LineNo).Select((l, i) =>
        {
            // Giá mới = đơn giá sau chiết khấu, trước VAT (cùng cách so với giá cũ); bằng hoặc thấp hơn giá cũ, hay mua lần đầu, ghi cột bằng–giảm.
            var price = Math.Round(l.UnitPrice * (100m - l.DiscountPct) / 100m, 2, MidpointRounding.AwayFromZero);
            var hasLast = context.LastPrices.TryGetValue(l.Id, out var last);
            var increased = hasLast && price > last;
            return new[]
            {
                No(i), l.ItemName, context.Specifications.GetValueOrDefault(l.ItemId) ?? "", l.UomCode, Qty(l.Qty),
                hasLast ? Price(last) : "Lần đầu", increased ? Price(price) : "", increased ? "" : Price(price), Money(l.Amount),
                supplier, Money(spentNet), remaining,
            };
        }).ToList();

        var rates = po.Lines.Select(l => l.VatCode is null or VatRates.NotSubject ? "KCT" : l.VatCode + "%").Distinct().ToList();
        var extra = new List<CuvtExtraRow>
        {
            new(rates.Count == 1 ? $"Thuế VAT {rates[0]}" : "Thuế VAT", 6,
                [new(""), new(""), new(Money(po.TotalVat)), new(""), new(Money(spentVat)), new("")]),
            new("TỔNG CỘNG (BAO GỒM VAT)", 6,
                [new(""), new(""), new(Money(po.TotalAmount + po.TotalVat)), new(""), new(Money(spentNet + spentVat)), new("")], 15, true),
        };

        var info = new List<CuvtInfoLine>();
        if (is4A) info.Add(new("Tên công trình", context.ProjectName ?? ""));
        else info.Add(new("Tên hàng hóa", context.GoodsTitle ?? ""));
        info.Add(new("Phòng ban đề xuất", context.DepartmentName ?? "Chưa khai báo"));
        info.Add(new("Định mức/dự trù ngân sách", context.BudgetAmount is { } b ? Vnd(b) : "Chưa khai báo", context.BudgetAmount is not null));
        info.Add(new("Lũy kế số tiền đã mua", Vnd(spentNet), true));
        info.Add(new("Số tiền còn lại", context.BudgetAmount is { } bb ? Vnd(bb - spentNet) : "Chưa khai báo", context.BudgetAmount is not null));

        var after = new List<CuvtTextLine>
        {
            new("Bằng chữ", VietnameseNumberText.ToVndWords(po.TotalAmount + po.TotalVat) + " ./."),
            new("Hình thức thanh toán", Text(po.PaymentTerms)),
            new("Ngày giao dự kiến", po.ExpectedDate?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "Chưa ghi"),
            new("Địa điểm giao hàng", Text(po.DeliveryAddress)),
        };

        return new CuvtForm(
            po.FormCode, "BẢNG THEO DÕI MUA HÀNG", is4A ? Subtitles1A : Subtitles3B, context.CompanyName, po.No, po.Date, true, StatusNote(po.Status),
            info, columns, rows, 6, ["", "", Money(po.TotalAmount), "", Money(spentNet), remaining], (null, null), po.Note,
            Signers("Người lập", po.CreatedByName, po.Approvals), extra, "Tổng chưa bao gồm VAT", after);
    }
}
