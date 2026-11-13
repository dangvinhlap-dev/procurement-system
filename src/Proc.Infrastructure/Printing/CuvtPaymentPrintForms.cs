using Proc.Application.Purchasing;
using Proc.Domain.Common;

namespace Proc.Infrastructure.Printing;

/// <summary>Đề nghị thanh toán 5A (công trình) / 5B (hàng hóa, dịch vụ) – tiền mặt hoặc chuyển khoản (lát Y8, design.md mục 19.3 bước 7).</summary>
public static partial class CuvtPrintForms
{
    /// <summary>
    /// Phiếu đề nghị thanh toán theo biểu mẫu Excel CUVT "5A/5B.ĐN TTTM / TTCK" (khổ dọc). Đầu phiếu lấy khung chi phí của PO như 4A/4B: định mức
    /// (5A tổng dự toán công trình, 5B ngân sách tháng của bộ phận), lũy kế số tiền đã mua (đã mua trước đây + đơn này, chưa VAT), số tiền còn lại;
    /// sau đó NCC, địa chỉ, ngân hàng – số tài khoản (chuyển khoản). Bảng chứng từ: ngày, số hóa đơn, nội dung, số tiền; dòng Tổng cộng, Số tiền
    /// giảm trừ, Số tiền còn lại (in đậm) và số tiền bằng chữ. Ví dụ hai hóa đơn 220.000 + 330.000, giảm trừ 50.000 → còn lại 500.000.
    /// </summary>
    /// <param name="payment">Đề nghị đã đọc theo phạm vi.</param>
    /// <param name="order">Khung chi phí của PO (cùng số liệu với bảng theo dõi 4A/4B).</param>
    /// <param name="orderNet">Tiền hàng chưa VAT của PO (để tính lũy kế = đã mua trước đây + đơn này).</param>
    /// <returns>Nội dung in.</returns>
    public static CuvtForm PaymentRequest(PaymentRequestDto payment, PoPrintContext order, decimal orderNet)
    {
        var is5A = payment.Form == "5A";
        var transfer = payment.Method == "BankTransfer";
        var spent = order.PastNet + orderNet;
        var info = new List<CuvtInfoLine>
        {
            new("Kính trình", $"Chủ tịch HĐQT – Tổng Giám đốc {order.CompanyName}"),
            is5A ? new("Tên công trình", order.ProjectName ?? "") : new("Tên hàng hóa", order.GoodsTitle ?? ""),
            new("Phòng ban đề xuất", order.DepartmentName ?? "Chưa khai báo"),
            new("Định mức/dự trù ngân sách", order.BudgetAmount is { } b ? Vnd(b) : "Chưa khai báo", order.BudgetAmount is not null),
            new("Lũy kế số tiền đã mua", Vnd(spent), true),
            new("Số tiền còn lại", order.BudgetAmount is { } bb ? Vnd(bb - spent) : "Chưa khai báo", order.BudgetAmount is not null),
            new("Tên NCC", payment.SupplierName ?? ""),
            new("Địa chỉ", payment.SupplierAddress ?? ""),
        };
        if (transfer)
        {
            info.Add(new("Tên ngân hàng", string.Join(" – ", new[] { payment.BankName, payment.BankBranch }.Where(x => !string.IsNullOrWhiteSpace(x)))));
            info.Add(new("Số tài khoản",
                payment.BankAccountNumber + (string.IsNullOrWhiteSpace(payment.BankAccountName) ? "" : $" ({payment.BankAccountName.Trim()})")));
        }

        var columns = new List<CuvtColumn>
        {
            new("Ngày", 24, Group: "Chứng từ", Center: true), new("Số hóa đơn", 32, Group: "Chứng từ", Center: true),
            new("Nội dung thanh toán", 92), new("Số tiền", 38, true),
        };
        var rows = payment.Lines.OrderBy(l => l.LineNo).Select(l => new[]
        {
            Date(l.InvoiceDate) ?? "", string.IsNullOrWhiteSpace(l.InvoiceSeries) ? l.InvoiceNo ?? "" : $"{l.InvoiceSeries} – {l.InvoiceNo}", l.Description,
            Money(l.Amount),
        }).ToList();
        var extra = new List<CuvtExtraRow>
        {
            new("Số tiền giảm trừ" + (string.IsNullOrWhiteSpace(payment.DeductionReason) ? "" : $" ({payment.DeductionReason.Trim()})"), 3,
                [new(Money(payment.DeductionAmount))]),
            new("Số tiền còn lại", 3, [new(Money(payment.PayableAmount))], 15, true),
        };
        var after = new List<CuvtTextLine> { new("Bằng chữ", VietnameseNumberText.ToVndWords(payment.PayableAmount) + " ./.") };
        if (payment.PaidDate is { } paid)
            after.Add(new("Đã chi", $"ngày {paid:dd/MM/yyyy}" + (string.IsNullOrWhiteSpace(payment.PaymentNote) ? "" : $" – {payment.PaymentNote.Trim()}")));

        return new CuvtForm(
            payment.Form, transfer ? "PHIẾU ĐỀ NGHỊ THANH TOÁN CHUYỂN KHOẢN" : "PHIẾU ĐỀ NGHỊ THANH TOÁN TIỀN MẶT", is5A ? Subtitles1A : Subtitles1B,
            order.CompanyName, payment.No, payment.Date, false, StatusNote(payment.Status), info, columns, rows, 3, [Money(payment.TotalAmount)],
            (null, null), payment.Note, Signers("Người lập", payment.CreatedByName, payment.Approvals), extra, "Tổng cộng", after);
    }

    /// <summary>
    /// Phụ đề mẫu 1C – theo file giả lập "docs/CUVT nhóm C 13.11.2026 (giả lập).xlsx" (lát Y11c); sửa khi người dùng gửi mẫu thật.
    /// </summary>
    private static readonly string[] Subtitles1C = ["CHI PHÍ TẠP VỤ – THANH TOÁN TỔNG HỢP THEO BẢNG KÊ"];

    /// <summary>Phụ đề mẫu 2C – theo file giả lập nhóm C (lát Y11c).</summary>
    private static readonly string[] Subtitles2C = ["CHI PHÍ LẶT VẶT ĐÃ PHÁT SINH – THANH TOÁN GỘP"];

    /// <summary>
    /// Phiếu đề nghị thanh toán tổng hợp chi phí tạp vụ 1C (lát Y11b – <b>mẫu giả lập</b> sheet "1C.DN TT TAP VU", dựng theo khung 5B): đầu phiếu phòng ban đề xuất,
    /// người nhận tiền, ngân hàng – số tài khoản (chuyển khoản); bảng chứng từ là các bảng kê 2C (ngày, số bảng kê, nội dung, số tiền); Tổng cộng,
    /// giảm trừ (tạm ứng đã nhận…), Số tiền còn lại, bằng chữ; khối ký theo chuỗi duyệt. Ví dụ hai bảng kê 550.500 + 195.500, giảm trừ 46.000 → 700.000.
    /// </summary>
    /// <param name="payment">Đề nghị 1C đã đọc theo phạm vi.</param>
    /// <param name="companyName">Tên công ty in đầu phiếu.</param>
    /// <returns>Nội dung in.</returns>
    public static CuvtForm SundryPaymentRequest(PaymentRequestDto payment, string companyName)
    {
        var transfer = payment.Method == "BankTransfer";
        var info = new List<CuvtInfoLine>
        {
            new("Kính trình", $"Chủ tịch HĐQT – Tổng Giám đốc {companyName}"),
            new("Nội dung", "Thanh toán chi phí tạp vụ theo bảng kê đã duyệt"),
            new("Phòng ban đề xuất", payment.DepartmentName ?? "Chưa khai báo"),
            new("Người nhận tiền", payment.PayeeName ?? "", true),
        };
        if (transfer)
        {
            info.Add(new("Tên ngân hàng", string.Join(" – ", new[] { payment.BankName, payment.BankBranch }.Where(x => !string.IsNullOrWhiteSpace(x)))));
            info.Add(new("Số tài khoản",
                payment.BankAccountNumber + (string.IsNullOrWhiteSpace(payment.BankAccountName) ? "" : $" ({payment.BankAccountName.Trim()})")));
        }

        var columns = new List<CuvtColumn>
        {
            new("Ngày", 24, Group: "Bảng kê", Center: true), new("Số bảng kê", 32, Group: "Bảng kê", Center: true),
            new("Nội dung thanh toán", 92), new("Số tiền", 38, true),
        };
        var rows = payment.Lines.OrderBy(l => l.LineNo)
            .Select(l => new[] { Date(l.InvoiceDate) ?? "", l.SundryExpenseSheetNo ?? "", l.Description, Money(l.Amount) }).ToList();
        var extra = new List<CuvtExtraRow>
        {
            new("Số tiền giảm trừ" + (string.IsNullOrWhiteSpace(payment.DeductionReason) ? "" : $" ({payment.DeductionReason.Trim()})"), 3,
                [new(Money(payment.DeductionAmount))]),
            new("Số tiền còn lại", 3, [new(Money(payment.PayableAmount))], 15, true),
        };
        var after = new List<CuvtTextLine> { new("Bằng chữ", VietnameseNumberText.ToVndWords(payment.PayableAmount) + " ./.") };
        if (payment.PaidDate is { } paid)
            after.Add(new("Đã chi", $"ngày {paid:dd/MM/yyyy}" + (string.IsNullOrWhiteSpace(payment.PaymentNote) ? "" : $" – {payment.PaymentNote.Trim()}")));

        return new CuvtForm(
            "1C", transfer ? "PHIẾU ĐỀ NGHỊ THANH TOÁN CHUYỂN KHOẢN" : "PHIẾU ĐỀ NGHỊ THANH TOÁN TIỀN MẶT", Subtitles1C,
            companyName, payment.No, payment.Date, false, StatusNote(payment.Status), info, columns, rows, 3, [Money(payment.TotalAmount)],
            (null, null), payment.Note, Signers("Người lập", payment.CreatedByName, payment.Approvals), extra, "Tổng cộng", after);
    }

    /// <summary>
    /// Bảng kê chi phí tạp vụ 2C (lát Y11c – <b>mẫu giả lập</b> sheet "2C.BK CP TAP VU", khổ ngang như 2A/2B): đầu phiếu phòng ban đề xuất, định mức
    /// tháng, đã dùng trong tháng (không gồm bảng kê này), tổng bảng kê – số liệu định mức lúc lưu / gửi gần nhất; bảng STT, Ngày chi, Nội dung chi,
    /// Nơi mua / người bán, Số hóa đơn / chứng từ, Số tiền; dòng Tổng cộng, bằng chữ; khối ký theo chuỗi duyệt. Ví dụ ba khoản 300.000 + 250.500
    /// + 195.500 → tổng 746.000.
    /// </summary>
    /// <param name="sheet">Bảng kê đã đọc theo phạm vi.</param>
    /// <param name="companyName">Tên công ty in đầu phiếu.</param>
    /// <returns>Nội dung in.</returns>
    public static CuvtForm SundryExpenseSheet(SundryExpenseSheetDto sheet, string companyName)
    {
        var budget = sheet.Budget;
        var period = $"{sheet.Date.Month:00}/{sheet.Date.Year}";
        var info = new List<CuvtInfoLine>
        {
            new("Phòng ban đề xuất", sheet.DepartmentName),
            new($"Định mức/dự trù chi phí tháng {period}", budget is null ? "Chưa khai báo" : Vnd(budget.BudgetAmount), budget is not null),
            new("Đã dùng trong tháng (không gồm bảng kê này)", budget is null ? "Chưa khai báo" : Vnd(budget.UsedAmount)),
            new("Tổng tiền bảng kê", Vnd(sheet.TotalAmount), true),
        };
        // Cột cố định 171 mm; Nội dung chi lấy phần còn lại của khổ ngang.
        var columns = new List<CuvtColumn>
        {
            new("STT", 12, Center: true), new("Ngày chi", 24, Center: true), new("Nội dung chi", CuvtFormLayout.LandscapeWidthMm - 171),
            new("Nơi mua / người bán", 60), new("Số hóa đơn / chứng từ", 40, Center: true), new("Số tiền", 35, true),
        };
        var rows = sheet.Lines.OrderBy(l => l.LineNo)
            .Select(l => new[] { l.LineNo.ToString(System.Globalization.CultureInfo.InvariantCulture), Date(l.ExpenseDate) ?? "", l.Description, l.Vendor ?? "", l.ReceiptNo ?? "", Money(l.Amount) })
            .ToList();
        var after = new List<CuvtTextLine> { new("Bằng chữ", VietnameseNumberText.ToVndWords(sheet.TotalAmount) + " ./.") };
        return new CuvtForm(
            "2C", "BẢNG KÊ CHI PHÍ TẠP VỤ", Subtitles2C, companyName, sheet.No, sheet.Date, true, StatusNote(sheet.Status), info, columns, rows, 5,
            [Money(sheet.TotalAmount)], (null, null), sheet.Note, Signers("Người lập", sheet.CreatedByName, sheet.Approvals), null, "Tổng cộng", after);
    }
}
