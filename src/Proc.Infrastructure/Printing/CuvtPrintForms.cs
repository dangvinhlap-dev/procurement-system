using System.Globalization;
using Proc.Application.Common;
using Proc.Application.Purchasing;
using Proc.Domain.Purchasing;

namespace Proc.Infrastructure.Printing;

/// <summary>
/// Dựng nội dung in theo biểu mẫu CUVT (lát Y10, design.md mục 10.10, 19.2): đề nghị mua 1A (công trình), 1B (hàng hóa), đề nghị dịch vụ
/// (mẫu 1B không có cột tồn – dịch vụ không qua Thủ kho), bảng tổng hợp 2A/2B. Hàm thuần – chỉ đọc DTO đã lấy theo phạm vi người dùng.
/// </summary>
public static partial class CuvtPrintForms
{
    private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

    private static readonly string[] Subtitles1A =
    [
        "NHÓM VẬT TƯ THUỘC CÔNG TRÌNH MỚI CÓ TỔNG DỰ TOÁN CHI PHÍ",
        "VÀ THỰC HIỆN THEO TỪNG CÔNG ĐOẠN/TỪNG HẠNG MỤC",
        "ĐẾN KHI CÔNG TRÌNH/DỰ ÁN HOÀN THIỆN",
    ];

    private static readonly string[] Subtitles1B =
    [
        "NHÓM VẬT TƯ TIÊU HAO, VẬT TƯ CÓ ĐỊNH MỨC CỐ ĐỊNH SỬ DỤNG HÀNG THÁNG",
        "NHÓM DV SỬ DỤNG MỘT LẦN, SỬA CHỮA MÁY MÓC THIẾT BỊ, MUA TÀI SẢN",
        "VẬT TƯ CỦA CÔNG TRÌNH PHÁT SINH CHƯA CÓ DỰ TOÁN",
    ];

    /// <summary>
    /// Đề nghị mua theo biểu mẫu: loại A → 1A, loại B → 1B, dịch vụ → 1B bỏ ba cột Cần sử dụng – Tồn kho – Cần mua (chỉ "Số lượng").
    /// Dòng hàng mới chưa gắn mã in nối sau, mô tả = quy cách (design.md mục 18.12).
    /// </summary>
    /// <param name="pr">Đề nghị mua đã đọc theo phạm vi.</param>
    /// <param name="companyName">Tên công ty in đầu phiếu.</param>
    /// <param name="specifications">Quy cách của mặt hàng trên các dòng (cột "Mô tả vật tư cần mua"); thiếu thì để trống.</param>
    /// <param name="monthSpent">1B / dịch vụ: lũy kế PO đã duyệt trong tháng của bộ phận, chưa VAT (người dùng chốt 12/11/2026).</param>
    /// <returns>Nội dung in.</returns>
    public static CuvtForm PurchaseRequest(
        PurchaseRequestDto pr, string companyName, IReadOnlyDictionary<Guid, string?> specifications, decimal monthSpent = 0)
    {
        var category = Enum.TryParse<PurchaseCategory>(pr.Category, out var c) ? c : PurchaseCategory.Goods;
        var withStock = category != PurchaseCategory.Service;
        List<CuvtColumn> columns = withStock
            ?
            [
                new("TT", 8, Center: true), new("Tên vật tư", 36), new("Mô tả vật tư cần mua", 30), new("ĐVT", 12, Center: true),
                new("Cần sử dụng", 15, true, "Số lượng"), new("Tồn kho", 14, true, "Số lượng"), new("Cần mua", 15, true, "Số lượng"),
                new("Đơn giá (tham khảo)", 18, true), new("Thành tiền (tham khảo)", 22, true), new("Ghi chú", 16),
            ]
            :
            [
                new("TT", 8, Center: true), new("Tên dịch vụ / vật tư", 42), new("Mô tả cần mua", 36), new("ĐVT", 13, Center: true),
                new("Số lượng", 17, true), new("Đơn giá (tham khảo)", 20, true), new("Thành tiền (tham khảo)", 26, true), new("Ghi chú", 24),
            ];

        var rows = new List<string[]>();
        foreach (var l in pr.Lines.OrderBy(l => l.LineNo))
        {
            var spec = specifications.GetValueOrDefault(l.ItemId) ?? "";
            rows.Add(withStock
                ? [No(rows.Count), l.ItemName, spec, l.UomCode, Qty(l.RequiredQty), l.StockQty is { } s ? Qty(s) : "", Qty(l.Qty), Price(l.EstPrice),
                    Money(l.Amount), l.Note ?? ""]
                : [No(rows.Count), l.ItemName, spec, l.UomCode, Qty(l.Qty), Price(l.EstPrice), Money(l.Amount), l.Note ?? ""]);
        }

        // Hàng mới đã gắn mã đã sinh dòng có mã – không in lại để tổng khớp tổng giá trị của phiếu.
        foreach (var n in pr.NewItems.Where(n => n.ItemId is null).OrderBy(n => n.LineNo))
        {
            var name = n.Description + " (chưa có mã)";
            rows.Add(withStock
                ? [No(rows.Count), name, n.Specification ?? "", n.UomCode, Qty(n.Qty), "", Qty(n.Qty), Price(n.EstPrice), Money(n.Amount), n.Note ?? ""]
                : [No(rows.Count), name, n.Specification ?? "", n.UomCode, Qty(n.Qty), Price(n.EstPrice), Money(n.Amount), n.Note ?? ""]);
        }

        List<CuvtInfoLine> info;
        if (category == PurchaseCategory.Construction)
        {
            var project = pr.Construction;
            info =
            [
                new("Tên công trình", project is null ? "Chưa chọn công trình" : project.Name),
                new("Tổng dự toán công trình", project is null ? "0 đ" : Vnd(project.EstimateAmount), true),
                // Biểu mẫu 1A: "chỉ điền số liệu quá khứ" – lũy kế PO đã duyệt trước đề nghị này, mua lần đầu thì 0.
                new("Lũy kế số tiền đã mua", project is null ? "0 đ" : Vnd(project.SpentAmount), true),
                new("Phòng ban đề xuất", pr.Department ?? ""),
            ];
        }
        else
        {
            info =
            [
                new("Phòng ban sử dụng", pr.Department ?? ""),
                new("Dự trù chi phí/định mức mỗi tháng", pr.Budget is { } b ? Vnd(b.BudgetAmount) : "Chưa khai báo", pr.Budget is not null),
                // Chỉ PO đã duyệt trong tháng, chưa VAT – giống lũy kế công trình ở 1A (người dùng chốt 12/11/2026), không lấy "đã dùng" của ngân sách.
                new("Lũy kế số tiền đã mua trong tháng", Vnd(monthSpent), true),
                new("Mục đích sử dụng", pr.Purpose ?? ""),
            ];
        }

        string[] total = [Money(pr.TotalAmount), ""];
        return new CuvtForm(
            category == PurchaseCategory.Construction ? "1A" : "1B",
            category == PurchaseCategory.Construction ? "ĐỀ NGHỊ MUA VẬT TƯ CÁC CÔNG TRÌNH" : "ĐỀ NGHỊ MUA HÀNG HÓA THIẾT BỊ",
            category == PurchaseCategory.Construction ? Subtitles1A : Subtitles1B,
            companyName, pr.No, pr.Date, false, StatusNote(pr.Status), info, columns, rows, columns.Count - 2, total,
            ($"Thời gian cần sử dụng: {Date(pr.NeededDate) ?? "theo từng dòng"}", $"Địa điểm tập kết/giao hàng: {pr.WarehouseName ?? ""}"),
            pr.Note, Signers("Người đề nghị", pr.RequesterName ?? pr.CreatedByName, pr.Approvals));
    }

    /// <summary>
    /// Bảng tổng hợp đề xuất mua theo biểu mẫu 2A/2B (khổ ngang): mỗi đơn vị mua một cột, dòng thuộc đơn vị nào đánh "x".
    /// Lũy kế = đã mua trước đây + tổng đề xuất của bảng (ghi chú biểu mẫu 2A, 2B): 2A đã mua là lũy kế PO của công trình; 2B là PO đã duyệt
    /// trong tháng của bộ phận, chưa VAT (người dùng chốt 12/11/2026).
    /// </summary>
    /// <param name="summary">Bảng tổng hợp đã đọc theo phạm vi.</param>
    /// <param name="companyName">Tên công ty in đầu phiếu.</param>
    /// <param name="monthSpent">2B: lũy kế PO đã duyệt trong tháng của bộ phận, chưa VAT.</param>
    /// <returns>Nội dung in.</returns>
    public static CuvtForm PurchaseSummary(PurchaseSummaryDto summary, string companyName, decimal monthSpent = 0)
    {
        var is2A = summary.Form == nameof(PurchaseSummaryForm.Construction);
        var lines = summary.Lines.Select(l => new SummaryRow(
            l.LineNo, l.RequestDate, l.RequestNo, l.ItemName, l.UomCode, l.Qty, l.EstPrice, l.Amount, l.NeededDate, l.RequestPurpose,
            new SummaryUnit(l.PurchasingUnitId, l.PurchasingUnitCode, l.PurchasingUnitName))).ToList();
        var frame = summary.Frame;
        List<CuvtInfoLine> info = is2A
            ?
            [
                new("Tên công trình", frame.ProjectName ?? ""),
                new("Phòng ban đề xuất", summary.DepartmentName ?? ""),
                new("Tổng dự toán công trình", Vnd(frame.LimitAmount ?? 0), true),
                new("Lũy kế số tiền đã mua", Vnd(frame.AccumulatedAmount + frame.ProposedAmount), true),
                new("Tổng tiền đề xuất", Vnd(frame.ProposedAmount), true),
            ]
            : Info2B(summary.DepartmentName, frame.LimitAmount, monthSpent + frame.ProposedAmount, frame.ProposedAmount);
        return Summary(
            is2A, companyName, summary.No, summary.Date, summary.Status, summary.DepartmentName, lines, summary.TotalAmount, info, summary.Note,
            Signers("Người lập", summary.CreatedByName, summary.Approvals));
    }

    /// <summary>Đơn vị mua của một dòng – mỗi đơn vị một cột "Đơn vị mua" trên 2A/2B.</summary>
    /// <param name="Id">Đơn vị mua.</param>
    /// <param name="Code">Mã (thứ tự cột).</param>
    /// <param name="Name">Tên in ở tiêu đề cột.</param>
    public sealed record SummaryUnit(Guid Id, string Code, string Name);

    /// <summary>Một dòng của bảng tổng hợp 2A/2B (từ dòng bảng tổng hợp, hoặc từ dòng đề nghị khi in 2B kèm 1B).</summary>
    private sealed record SummaryRow(
        int LineNo, DateOnly? RequestDate, string RequestNo, string ItemName, string UomCode, decimal Qty, decimal EstPrice, decimal Amount,
        DateOnly? NeededDate, string? Purpose, SummaryUnit Unit);

    /// <summary>
    /// Trang 2B in kèm đề nghị mua 1B (người dùng yêu cầu 20/11/2026 – sheet 1B.DN VT-CT và 2B.TH MUA VT "in ra 1 lần", không tạo và duyệt hai
    /// lần): bảng tổng hợp dựng từ chính đề nghị – các dòng còn cần mua (cần mua &gt; 0), số phiếu / ngày lập / trạng thái của đề nghị; đơn vị mua lấy
    /// từ bảng tổng hợp đã chọn dòng đó (nếu có), không thì đơn vị mặc định.
    /// </summary>
    /// <param name="pr">Đề nghị mua loại B đã đọc theo phạm vi.</param>
    /// <param name="companyName">Tên công ty in đầu phiếu.</param>
    /// <param name="monthSpent">Lũy kế PO đã duyệt trong tháng của bộ phận, chưa VAT (cùng cách tính 1B / 2B).</param>
    /// <param name="unitByLine">Đơn vị mua theo dòng đề nghị (dòng đã ở bảng tổng hợp chưa hủy).</param>
    /// <param name="fallbackUnit">Đơn vị mua cho dòng chưa ở bảng tổng hợp nào.</param>
    /// <returns>Nội dung in mẫu 2B; null khi không còn dòng cần mua.</returns>
    public static CuvtForm? PurchaseRequestSummary(
        PurchaseRequestDto pr, string companyName, decimal monthSpent, IReadOnlyDictionary<Guid, SummaryUnit> unitByLine, SummaryUnit fallbackUnit)
    {
        var lines = pr.Lines.Where(l => l.Qty > 0).OrderBy(l => l.LineNo).Select(l => new SummaryRow(
            l.LineNo, pr.Date, pr.No, l.ItemName, l.UomCode, l.Qty, l.EstPrice, l.Amount, l.NeededDate ?? pr.NeededDate, pr.Purpose,
            unitByLine.GetValueOrDefault(l.Id) ?? fallbackUnit)).ToList();
        if (lines.Count == 0) return null;
        var proposed = lines.Sum(l => l.Amount);
        return Summary(
            false, companyName, pr.No, pr.Date, pr.Status, pr.Department, lines, proposed,
            // "2B giống 1B, chỉ khác form" (người dùng chốt 20/11/2026): lũy kế in đúng số của 1B – chỉ đã mua trong tháng, không cộng đề xuất.
            Info2B(pr.Department, pr.Budget?.BudgetAmount, monthSpent, proposed), pr.Note,
            Signers("Người lập", pr.RequesterName ?? pr.CreatedByName, pr.Approvals));
    }

    /// <summary>
    /// Khung thông tin 2B: phòng ban, định mức/dự trù (ngân sách tháng), lũy kế, tổng đề xuất. Bảng tổng hợp: lũy kế = đã mua trong tháng + tổng đề
    /// xuất (ghi chú biểu mẫu 2B); 2B in kèm đề nghị: lũy kế = đã mua trong tháng như 1B.
    /// </summary>
    private static List<CuvtInfoLine> Info2B(string? department, decimal? limit, decimal accumulated, decimal proposed) =>
    [
        new("Phòng ban đề xuất", department ?? ""),
        new("Định mức/dự trù chi phí", limit is { } l ? Vnd(l) : "Chưa khai báo", limit is not null),
        new("Lũy kế số tiền đã mua", Vnd(accumulated), true),
        new("Tổng tiền đề xuất", Vnd(proposed), true),
    ];

    /// <summary>Dựng bảng 2A/2B từ các dòng đã quy về <see cref="SummaryRow"/>.</summary>
    private static CuvtForm Summary(
        bool is2A, string companyName, string no, DateOnly date, string status, string? departmentName, IReadOnlyList<SummaryRow> lines,
        decimal totalAmount, List<CuvtInfoLine> info, string? note, IReadOnlyList<CuvtSigner> signers)
    {
        var units = lines.Select(l => l.Unit).DistinctBy(u => u.Id).OrderBy(u => u.Code).ToList();
        // Độ rộng, căn lề in theo số đo sheet 2A/2B (CuvtSheetLayout.Sheet2A/Sheet2B – lát IN2); cột "Đơn vị mua" mỗi đơn vị một cột.
        const string content = "Nội dung đề xuất";
        var columns = new List<CuvtColumn>
        {
            new("Ngày đề xuất", 20, Center: true), new("Số phiếu", 34, Center: true), new("Phòng ban đề xuất", 24, Center: true),
            new("Tên vật tư", 40, Group: content), new("ĐVT", 13, Group: content), new("Số lượng", 17, Group: content),
            new("Đơn giá", 20, true, content), new("Thành tiền", 25, true, content), new("Ngày cung ứng", 22, Group: content),
            new("Mục đích sử dụng", 30, Group: content, Center: true),
        };
        columns.AddRange(units.Select(u => new CuvtColumn(u.Name, 14, Group: "Đơn vị mua", Center: true)));

        var rows = lines.OrderBy(l => l.LineNo).Select(l => new[]
            {
                Date(l.RequestDate) ?? "", l.RequestNo, departmentName ?? "", l.ItemName, l.UomCode, Qty(l.Qty), Price(l.EstPrice),
                Money(l.Amount), Date(l.NeededDate) ?? "", l.Purpose ?? "",
            }.Concat(units.Select(u => u.Id == l.Unit.Id ? "x" : "")).ToArray())
            .ToList();

        // Dòng tổng như sheet: "Tổng cộng" gộp 3 cột đầu (A15:C15), tổng số lượng ở cột Số lượng, tổng tiền ở cột Thành tiền. Tự đặt: sheet cộng cả
        // cột Đơn giá (=SUM) – tổng đơn giá các mặt hàng không có nghĩa nên để trống; tổng số lượng chỉ ghi khi mọi dòng cùng ĐVT.
        var oneUom = lines.Select(l => l.UomCode).Distinct().Count() == 1;
        var totalCells = new List<string> { "", "", oneUom ? Qty(lines.Sum(l => l.Qty)) : "", "", Money(totalAmount) };
        totalCells.AddRange(Enumerable.Repeat("", columns.Count - 3 - totalCells.Count));
        return new CuvtForm(
            is2A ? "2A" : "2B",
            is2A ? "BẢNG TỔNG HỢP CÁC ĐỀ XUẤT MUA VẬT TƯ" : "BẢNG TỔNG HỢP CÁC ĐỀ XUẤT MUA HÀNG HÓA",
            // Phụ đề 2B nguyên văn sheet (có dấu chấm cuối dòng 1, "SỬA-CHỮA") – như 3B, 4B.
            is2A ? Subtitles1A : Subtitles3B,
            companyName, no, date, true, StatusNote(status), info, columns, rows, 3, totalCells, (null, null), note, signers);
    }

    /// <summary>
    /// Ô ký theo chuỗi duyệt thực tế: người lập/đề nghị rồi từng bước của lượt duyệt gần nhất (theo chặng, cấp), bỏ bước được bỏ qua. Bước đã
    /// duyệt in họ tên, chức vụ và thời điểm (giờ Việt Nam) – duyệt điện tử là chính thức (design.md 19.1 Q13); bước chưa duyệt để trống.
    /// </summary>
    /// <param name="firstTitle">Chức danh ô đầu ("Người đề nghị", "Người lập").</param>
    /// <param name="firstName">Họ tên ô đầu.</param>
    /// <param name="steps">Các bước duyệt của phiếu.</param>
    /// <returns>Danh sách ô ký.</returns>
    public static IReadOnlyList<CuvtSigner> Signers(string firstTitle, string? firstName, IReadOnlyList<ApprovalStepDto> steps)
    {
        var round = steps.Count == 0 ? 0 : steps.Max(s => s.Round);
        var current = steps.Where(s => s.Round == round && s.Status != "Skipped").OrderBy(s => s.Stage).ThenBy(s => s.Level).ToList();
        var submittedAt = current.Select(s => s.SubmittedAt).FirstOrDefault(t => t is not null);
        var signers = new List<CuvtSigner>
        {
            new(firstTitle, firstName, submittedAt is { } at ? $"Gửi duyệt {Time(at)}" : null),
        };
        signers.AddRange(current.Select(s => s.Status switch
        {
            "Approved" => new CuvtSigner(s.RoleName ?? $"Cấp {s.Level}", s.ActedByName, $"Đã duyệt {Time(s.ActedAt)}", s.ActedByPosition),
            "Rejected" => new CuvtSigner(s.RoleName ?? $"Cấp {s.Level}", s.ActedByName, $"Từ chối {Time(s.ActedAt)}", s.ActedByPosition),
            _ => new CuvtSigner(s.RoleName ?? $"Cấp {s.Level}", null, "Chưa duyệt"),
        }));
        return signers;
    }

    /// <summary>Dòng đỏ trạng thái chưa có hiệu lực.</summary>
    private static string? StatusNote(string status) => status switch
    {
        "Draft" => "BẢN NHÁP – chưa duyệt",
        "Submitted" => "ĐANG CHỜ DUYỆT – chưa có hiệu lực",
        "Rejected" => "BỊ TỪ CHỐI – chưa có hiệu lực",
        "Cancelled" => "ĐÃ HỦY",
        _ => null,
    };

    private static string No(int index) => (index + 1).ToString(Vi);

    /// <summary>Tiền VND không số lẻ, kiểu Việt Nam.</summary>
    private static string Money(decimal value) => Math.Round(value, 0, MidpointRounding.AwayFromZero).ToString("#,##0", Vi);

    /// <summary>Tiền kèm đơn vị "đ" – biểu mẫu cho viết "đ" hoặc "Đồng".</summary>
    private static string Vnd(decimal value) => Money(value) + " đ";

    private static string Qty(decimal value) => value.ToString("#,##0.####", Vi);

    private static string Price(decimal value) => value.ToString("#,##0.##", Vi);

    private static string? Date(DateOnly? value) => value?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    /// <summary>Giờ ngày theo giờ Việt Nam "HH:mm dd/MM/yyyy".</summary>
    private static string Time(DateTimeOffset? value) =>
        value is { } v ? TimeZoneInfo.ConvertTime(v, VietnamTime.Zone).ToString("HH:mm dd/MM/yyyy", CultureInfo.InvariantCulture) : "";
}
