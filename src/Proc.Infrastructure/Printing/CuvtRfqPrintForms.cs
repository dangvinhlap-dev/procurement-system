using System.Globalization;
using Proc.Application.Sourcing;
using Proc.Domain.Common;
using Proc.Domain.MasterData;

namespace Proc.Infrastructure.Printing;

/// <summary>Thông tin NCC in trên biểu mẫu 3A/3a/3B.</summary>
/// <param name="Address">Địa chỉ.</param>
/// <param name="Phone">Điện thoại.</param>
/// <param name="IsExisting">NCC cũ = đã có PO đã duyệt với công ty trước đây (không tính PO sinh từ chính RFQ).</param>
public sealed record RfqPrintSupplier(string? Address, string? Phone, bool IsExisting);

/// <summary>
/// Số liệu ngoài phiếu RFQ cho biểu mẫu 3A/3a/3B (lát Y6) – dịch vụ in đọc theo phạm vi người dùng rồi truyền vào hàm thuần.
/// </summary>
/// <param name="CompanyName">Tên công ty.</param>
/// <param name="Date">Ngày trình (ngày chọn thầu; chưa chọn thì ngày lập RFQ).</param>
/// <param name="ProjectName">Tên công trình (loại A).</param>
/// <param name="EstimateAmount">Tổng dự toán hiện hành của công trình (loại A).</param>
/// <param name="SpentAmount">
/// Lũy kế đã mua, chưa VAT, "chỉ số liệu quá khứ" như 1A: loại A = PO đã duyệt của công trình; loại B = PO đã duyệt trong tháng của bộ phận.
/// </param>
/// <param name="DepartmentName">Phòng ban đề xuất (loại A: của công trình; loại B: khi mọi dòng cùng một bộ phận).</param>
/// <param name="BudgetAmount">Loại B: định mức/dự trù chi phí tháng của bộ phận; null = chưa khai báo hoặc nhiều bộ phận.</param>
/// <param name="Purpose">Mục đích mua (mục đích của các đề nghị nguồn).</param>
/// <param name="Suppliers">NCC → thông tin in.</param>
/// <param name="LastPrices">Dòng RFQ → giá cũ đã mua gần nhất theo ĐVT của dòng (sau CK, trước VAT).</param>
/// <param name="ItemVat">Dòng RFQ → thuế suất mặc định của mặt hàng (báo giá không ghi thuế suất thì dùng).</param>
public sealed record RfqPrintContext(
    string CompanyName, DateOnly Date, string? ProjectName, decimal? EstimateAmount, decimal SpentAmount, string? DepartmentName,
    decimal? BudgetAmount, string? Purpose, IReadOnlyDictionary<Guid, RfqPrintSupplier> Suppliers, IReadOnlyDictionary<Guid, decimal> LastPrices,
    IReadOnlyDictionary<Guid, string?> ItemVat);

/// <summary>Biểu mẫu kết quả hỏi giá 3A (so sánh chọn NCC), 3a và 3B (bảng trình duyệt giá) – lát Y6, design.md mục 19.3.</summary>
public static partial class CuvtPrintForms
{
    private static readonly string[] Subtitles3B =
    [
        "NHÓM VẬT TƯ TIÊU HAO, VẬT TƯ CÓ ĐỊNH MỨC CỐ ĐỊNH SỬ DỤNG HÀNG THÁNG.",
        "NHÓM DV SỬ DỤNG MỘT LẦN, SỬA-CHỮA MÁY MÓC THIẾT BỊ, MUA TÀI SẢN",
        "VẬT TƯ CỦA CÔNG TRÌNH PHÁT SINH CHƯA CÓ DỰ TOÁN",
    ];

    /// <summary>
    /// Các biểu mẫu in cho một RFQ: <paramref name="form"/> = "3A" in bảng so sánh (chỉ RFQ loại A); null in theo biểu mẫu của phiếu – 3A một
    /// bảng so sánh, 3a/3B mỗi NCC một bảng trình giá (NCC trúng thầu; chưa chọn thầu thì mọi NCC đã chào giá).
    /// </summary>
    /// <param name="rfq">RFQ đã đọc theo phạm vi.</param>
    /// <param name="context">Số liệu ngoài phiếu.</param>
    /// <param name="form">Biểu mẫu muốn in ("3A") hoặc null.</param>
    /// <returns>Danh sách biểu mẫu (mỗi biểu mẫu một trang mới).</returns>
    /// <exception cref="BusinessRuleException">Khi chưa có báo giá nào, hoặc in 3A cho RFQ không phải loại A.</exception>
    public static IReadOnlyList<CuvtForm> Rfq(RfqDto rfq, RfqPrintContext context, string? form)
    {
        if (!rfq.Suppliers.Any(s => s.HasQuoted)) throw new BusinessRuleException($"RFQ {rfq.No} chưa có báo giá nào để in.");
        var isConstruction = rfq.Category == "Construction";
        var wanted = form?.Trim();
        if (string.Equals(wanted, "3A", StringComparison.Ordinal))
        {
            if (!isConstruction) throw new BusinessRuleException("Biểu mẫu so sánh chọn NCC 3A chỉ dùng cho vật tư công trình (loại A).");
            return [RfqComparison(rfq, context)];
        }

        // Loại A chưa chọn thầu: chưa biết 3A hay 3a – in bảng so sánh để hội đồng xem.
        var code = rfq.FormCode ?? (isConstruction ? "3A" : "3B");
        if (code == "3A") return [RfqComparison(rfq, context)];
        var awarded = rfq.Suppliers.Where(s => rfq.Lines.Any(l => l.AwardedSupplierId == s.SupplierId)).ToList();
        var suppliers = awarded.Count > 0 ? awarded : rfq.Suppliers.Where(s => s.HasQuoted).ToList();
        return suppliers.Select(s => RfqQuote(rfq, context, s, code, awarded.Count > 0)).ToList();
    }

    /// <summary>
    /// Biểu mẫu 3A "So sánh giá và chọn nhà cung cấp" (khổ ngang): mỗi NCC đã chào giá hai cột Đơn giá – Thành tiền (giá sau chiết khấu, trước
    /// VAT), tiêu đề NCC ghi cũ/mới và điện thoại; dưới bảng tổng chưa thuế, thuế VAT, tổng gồm thuế, hình thức thanh toán, thời gian giao; rồi
    /// "Đề xuất chọn" và "Lý do chọn". Ví dụ 100 cây × 300.000 = 30.000.000; VAT 10% = 3.000.000; gồm thuế 33.000.000.
    /// </summary>
    private static CuvtForm RfqComparison(RfqDto rfq, RfqPrintContext context)
    {
        // NCC trúng thầu đứng trước (như mẫu: NCC được đề xuất là NCC1), rồi theo tổng điểm.
        var suppliers = rfq.Suppliers.Where(s => s.HasQuoted)
            .OrderByDescending(s => rfq.Lines.Any(l => l.AwardedSupplierId == s.SupplierId))
            .ThenByDescending(s => s.TotalScore).ThenBy(s => s.SupplierCode).ToList();
        // Cột cố định TT 8, ĐVT 12, Số lượng 16 mm; mỗi NCC 2 cột chia đều phần còn lại sau Tên (tối thiểu 50 mm).
        var supplierWidth = Math.Min(64, (CuvtFormLayout.LandscapeWidthMm - 36 - 50) / suppliers.Count);
        var nameWidth = CuvtFormLayout.LandscapeWidthMm - 36 - supplierWidth * suppliers.Count;
        var columns = new List<CuvtColumn>
        {
            new("TT", 8, Center: true), new("Tên hàng hóa - vật tư", nameWidth), new("ĐVT", 12, Center: true), new("Số lượng", 16, true),
        };
        for (var i = 0; i < suppliers.Count; i++)
        {
            var s = suppliers[i];
            var contact = context.Suppliers.GetValueOrDefault(s.SupplierId);
            var group = $"NCC{i + 1}: {s.SupplierName}\nCũ/mới: {(contact?.IsExisting == true ? "CŨ" : "MỚI")}\nĐiện thoại: {Text(contact?.Phone)}";
            columns.Add(new("Đơn giá", supplierWidth * 0.45, true, group));
            columns.Add(new("Thành tiền", supplierWidth * 0.55, true, group));
        }

        var rows = new List<string[]>();
        foreach (var line in rfq.Lines.OrderBy(l => l.LineNo))
        {
            var cells = new List<string> { No(rows.Count), line.ItemName, line.UomCode, Qty(line.Qty) };
            foreach (var s in suppliers)
            {
                var q = s.Lines.FirstOrDefault(x => x.RfqLineId == line.Id);
                cells.Add(q is null ? "" : Price(NetPrice(q)));
                cells.Add(q is null ? "" : Money(LineAmount(line.Qty, q)));
            }

            rows.Add([.. cells]);
        }

        var totals = suppliers.Select(s => Totals(rfq, context, s, s.Lines.Select(q => q.RfqLineId).ToHashSet())).ToList();
        var extra = new List<CuvtExtraRow>
        {
            new("Thuế VAT", 4, totals.SelectMany(t => new[] { new CuvtCell(""), new CuvtCell(Money(t.Vat)) }).ToList()),
            new("Tổng tiền bao gồm thuế", 4, totals.SelectMany(t => new[] { new CuvtCell(""), new CuvtCell(Money(t.Net + t.Vat)) }).ToList(), Bold: true),
            new("Hình thức thanh toán", 4, suppliers.Select(s => new CuvtCell(Text(s.PaymentTerms), 2, false)).ToList()),
            new("Thời gian giao hàng", 4, suppliers.Select(s => new CuvtCell(LeadTime(s), 2, false)).ToList()),
        };

        var awarded = suppliers.Where(s => rfq.Lines.Any(l => l.AwardedSupplierId == s.SupplierId)).ToList();
        var proposal = awarded.Count switch
        {
            0 => "Chưa chọn",
            1 when rfq.Lines.All(l => l.AwardedSupplierId == awarded[0].SupplierId) => awarded[0].SupplierName,
            _ => string.Join("; ", awarded.Select(s =>
                $"{s.SupplierName} (dòng {string.Join(", ", rfq.Lines.Where(l => l.AwardedSupplierId == s.SupplierId).OrderBy(l => l.LineNo).Select(l => l.LineNo))})")),
        };
        var after = new List<CuvtTextLine> { new("Đề xuất chọn", proposal, true), new("Lý do chọn", rfq.SelectionReason ?? "") };
        after.AddRange(LineReasons(rfq));

        var info = new List<CuvtInfoLine>
        {
            new("Phòng ban đề xuất", context.DepartmentName ?? ""),
            new("Định mức/tổng dự toán", context.EstimateAmount is { } e ? Vnd(e) : "Chưa khai báo", context.EstimateAmount is not null),
            new("Mục đích mua", context.Purpose ?? rfq.Title),
        };
        return new CuvtForm(
            "3A", "SO SÁNH GIÁ VÀ CHỌN NHÀ CUNG CẤP", Subtitles1A, context.CompanyName, rfq.No, context.Date, true, RfqStatusNote(rfq.Status),
            info, columns, rows, 4, totals.SelectMany(t => new[] { "", Money(t.Net) }).ToList(), (null, null), rfq.Note,
            Signers("Người lập", rfq.AwardedByName ?? rfq.CreatedByName, rfq.Approvals), extra, "Tổng tiền chưa bao gồm thuế", after);
    }

    /// <summary>
    /// Biểu mẫu 3a (công trình) / 3B (hàng hóa, dịch vụ) "Bảng trình duyệt giá khách hàng" của một NCC (khổ ngang): dòng NCC trúng thầu (chưa
    /// chọn thầu thì mọi dòng NCC chào giá), cột giá cũ đã mua gần nhất; Cộng (chưa VAT) – Thuế VAT – Tổng cộng, bằng chữ, hình thức thanh toán.
    /// </summary>
    private static CuvtForm RfqQuote(RfqDto rfq, RfqPrintContext context, RfqSupplierDto supplier, string code, bool awardedOnly)
    {
        var lines = rfq.Lines.OrderBy(l => l.LineNo)
            .Where(l => awardedOnly ? l.AwardedSupplierId == supplier.SupplierId : supplier.Lines.Any(q => q.RfqLineId == l.Id))
            .ToList();
        var columns = new List<CuvtColumn>
        {
            new("STT", 10, Center: true), new("Tên hàng hóa - vật tư", 129), new("ĐVT", 16, Center: true), new("Số lượng", 20, true),
            new("Đơn giá", 30, true), new("Thành tiền", 34, true), new("Giá cũ đã mua gần nhất", 34, true),
        };
        var rows = lines.Select((l, i) =>
        {
            var q = supplier.Lines.Single(x => x.RfqLineId == l.Id);
            return new[]
            {
                No(i), l.ItemName, l.UomCode, Qty(l.Qty), Price(NetPrice(q)), Money(LineAmount(l.Qty, q)),
                context.LastPrices.TryGetValue(l.Id, out var last) ? Price(last) : "",
            };
        }).ToList();
        var (net, vat, rates) = Totals(rfq, context, supplier, lines.Select(l => l.Id).ToHashSet());
        var vatLabel = rates.Count == 1 ? $"Thuế VAT ({rates[0]})" : "Thuế VAT";
        var extra = new List<CuvtExtraRow>
        {
            new(vatLabel, 5, [new CuvtCell(Money(vat)), new CuvtCell("")]),
            new("Tổng cộng", 5, [new CuvtCell(Money(net + vat)), new CuvtCell("")], 15, true),
        };
        var info = context.Suppliers.GetValueOrDefault(supplier.SupplierId);
        var header = new List<CuvtInfoLine>();
        if (code == "3a")
        {
            header.Add(new("Tên công trình", context.ProjectName ?? ""));
            header.Add(new("Tổng dự toán công trình", context.EstimateAmount is { } e ? Vnd(e) : "Chưa khai báo", context.EstimateAmount is not null));
        }
        else
        {
            header.Add(new("Tên hàng hóa", rfq.Title));
            header.Add(new("Định mức/dự trù chi phí", context.BudgetAmount is { } b ? Vnd(b) : "Chưa khai báo", context.BudgetAmount is not null));
        }

        // "Chỉ điền số liệu quá khứ (giống 1A)" – lũy kế PO đã duyệt, chưa gồm lần mua đang trình.
        header.Add(new("Lũy kế số tiền đã mua", Vnd(context.SpentAmount), true));
        header.Add(new("Tên NCC", supplier.SupplierName));
        header.Add(new("Địa chỉ", Text(info?.Address)));
        header.Add(new("Số điện thoại", Text(info?.Phone)));

        var note = string.Join(" ", new[] { supplier.Note, rfq.Note }.Where(n => !string.IsNullOrWhiteSpace(n)));
        var after = new List<CuvtTextLine>
        {
            new("Bằng chữ", VietnameseNumberText.ToVndWords(net + vat) + " ./."),
            new("Hình thức thanh toán", Text(supplier.PaymentTerms)),
            new("Ghi chú", note),
        };
        after.AddRange(LineReasons(rfq).Where(r => lines.Any(l => r.Label == $"Dòng {l.LineNo}")));
        return new CuvtForm(
            code, "BẢNG TRÌNH DUYỆT GIÁ KHÁCH HÀNG", code == "3a" ? Subtitles1A : Subtitles3B, context.CompanyName, rfq.No, context.Date, true,
            RfqStatusNote(rfq.Status), header, columns, rows, 5, [Money(net), ""], (null, null), null,
            Signers("Người lập", rfq.AwardedByName ?? rfq.CreatedByName, rfq.Approvals), extra, "Cộng", after);
    }

    /// <summary>
    /// Cộng tiền chưa VAT và tiền VAT của một NCC trên các dòng chọn; thuế suất theo báo giá, trống thì theo mặt hàng, KCT/chưa khai báo = 0.
    /// Mỗi dòng: thành tiền × thuế suất / 100, làm tròn đồng, rồi cộng (như PO).
    /// </summary>
    private static (decimal Net, decimal Vat, IReadOnlyList<string> Rates) Totals(
        RfqDto rfq, RfqPrintContext context, RfqSupplierDto supplier, HashSet<Guid> lineIds)
    {
        decimal net = 0, vat = 0;
        var rates = new HashSet<string>();
        foreach (var line in rfq.Lines.Where(l => lineIds.Contains(l.Id)))
        {
            var q = supplier.Lines.FirstOrDefault(x => x.RfqLineId == line.Id);
            if (q is null) continue;
            var amount = LineAmount(line.Qty, q);
            net += amount;
            var code = q.VatCode ?? context.ItemVat.GetValueOrDefault(line.Id);
            rates.Add(code is null or VatRates.NotSubject ? "KCT" : code + "%");
            if (code is not null and not VatRates.NotSubject)
                vat += Math.Round(amount * decimal.Parse(code, CultureInfo.InvariantCulture) / 100m, 0, MidpointRounding.AwayFromZero);
        }

        return (net, vat, rates.ToList());
    }

    /// <summary>Lý do không chọn giá thấp nhất ở từng dòng (design.md mục 8.6) – in kèm để người duyệt thấy.</summary>
    private static IEnumerable<CuvtTextLine> LineReasons(RfqDto rfq) =>
        rfq.Lines.Where(l => !string.IsNullOrWhiteSpace(l.AwardReason)).OrderBy(l => l.LineNo)
            .Select(l => new CuvtTextLine($"Dòng {l.LineNo}", $"không chọn giá thấp nhất – {l.AwardReason}"));

    /// <summary>Đơn giá sau chiết khấu (giá trình duyệt), ví dụ 330.000 − 5% = 313.500.</summary>
    private static decimal NetPrice(QuotationLineDto q) => Math.Round(q.UnitPrice * (100m - q.DiscountPct) / 100m, 6, MidpointRounding.AwayFromZero);

    /// <summary>Thành tiền dòng = SL × đơn giá × (100 − %CK)/100, làm tròn 2 số – cùng cách tính giá trị trúng thầu.</summary>
    private static decimal LineAmount(decimal qty, QuotationLineDto q) =>
        Math.Round(qty * q.UnitPrice * (100m - q.DiscountPct) / 100m, 2, MidpointRounding.AwayFromZero);

    /// <summary>Thời gian giao theo báo giá: "2 ngày sau khi đặt hàng", nhiều mức thì "2–4 ngày…"; không ghi thì "Chưa ghi".</summary>
    private static string LeadTime(RfqSupplierDto supplier)
    {
        var days = supplier.Lines.Select(q => q.LeadTimeDays).OfType<int>().ToList();
        if (days.Count == 0) return "Chưa ghi";
        var (min, max) = (days.Min(), days.Max());
        return (min == max ? $"{min}" : $"{min}–{max}") + " ngày sau khi đặt hàng";
    }

    /// <summary>Chữ thay ô trống – biểu mẫu cấm dấu "…".</summary>
    private static string Text(string? value) => string.IsNullOrWhiteSpace(value) ? "Chưa ghi" : value.Trim();

    /// <summary>Dòng đỏ trạng thái của RFQ chưa được duyệt kết quả.</summary>
    private static string? RfqStatusNote(string status) => status switch
    {
        "Draft" or "Sent" => "CHƯA CHỌN THẦU – chưa có hiệu lực",
        "AwardPending" => "ĐANG CHỜ DUYỆT – chưa có hiệu lực",
        "Cancelled" => "ĐÃ HỦY",
        _ => null,
    };
}
