using ClosedXML.Excel;
using Proc.Application.Common;
using Proc.Application.Excel;
using Proc.Application.Reports;

namespace Proc.Infrastructure.Reports;

/// <summary>
/// Dựng file Excel báo cáo tháng cho Ban (lát G12, design.md mục 18.25): sheet "Tổng quan" (chỉ số chính, trạng thái các mục) và một sheet
/// cho mỗi mục có số liệu. Ô số giữ kiểu số (định dạng #,##0) để Ban cộng, lọc được trong Excel.
/// </summary>
public static class BoardMonthlyReportExcel
{
    private const string Money = "#,##0";
    private const string Quantity = "#,##0.####";
    private const string Percent = "#,##0.00";
    private const string Date = "dd/mm/yyyy";

    /// <summary>Tên nguồn giá – cùng nhãn báo cáo PRCW trên giao diện.</summary>
    private static readonly Dictionary<string, string> SourceLabels = new()
    {
        ["MonthlyApproval"] = "Duyệt tháng", ["Rfq"] = "Chọn thầu", ["Contract"] = "Hợp đồng", ["PriceList"] = "Bảng giá",
        ["Deviation"] = "Lệch giá đã duyệt", ["None"] = "Không có giá đã duyệt", ["Unknown"] = "Không xác định",
    };

    /// <summary>Tình trạng so bậc mục tiêu – cùng nhãn màn hình CKBT.</summary>
    private static readonly Dictionary<string, string> TargetLabels = new()
    {
        ["None"] = "Không đặt mục tiêu", ["NotStarted"] = "Chưa bắt đầu", ["Reached"] = "Đã đạt bậc mục tiêu", ["OnTrack"] = "Dự báo đạt",
        ["AtRisk"] = "Nguy cơ hụt bậc", ["Missed"] = "Hụt bậc mục tiêu",
    };

    /// <summary>Trạng thái hồ sơ NCC mới – cùng nhãn màn hình HSN.</summary>
    private static readonly Dictionary<string, string> OnboardingLabels = new()
    {
        ["Draft"] = "Nháp", ["Evaluated"] = "Đã đánh giá", ["Submitted"] = "Chờ Hội đồng duyệt", ["Approved"] = "Đã duyệt", ["Rejected"] = "Từ chối",
        ["NotRecommended"] = "Không đề xuất", ["Cancelled"] = "Đã hủy",
    };

    /// <summary>Tên loại dấu hiệu bất thường – cùng nhãn màn hình ANOM.</summary>
    private static readonly Dictionary<string, string> AnomalyLabels = new()
    {
        ["HighPriceRepeat"] = "Giá cao lặp lại một NCC", ["NearThreshold"] = "PO sát dưới ngưỡng duyệt", ["BankAccountChange"] = "Đổi tài khoản ngân hàng NCC",
        ["OffContract"] = "Mua ngoài hợp đồng cao", ["InvoiceShortage"] = "Hóa đơn tính cả hàng thiếu", ["BackdatedOrder"] = "Hàng/hóa đơn trước PO",
        ["ConflictOfInterest"] = "Xung đột lợi ích", ["UnrecordedRebate"] = "Chiết khấu/tài trợ chưa ghi nhận",
        ["UnrecordedVolumeRebate"] = "Chiết khấu sản lượng gộp chưa ghi nhận",
    };

    /// <summary>Dựng file.</summary>
    /// <param name="report">Báo cáo đã ghép.</param>
    /// <param name="exportedAtUtc">Giờ xuất (UTC) – ghi trên sheet Tổng quan theo giờ Việt Nam.</param>
    /// <returns>File Excel, tên <c>BaoCaoThangBan_{yyyy-MM}_{công ty hoặc TapDoan}.xlsx</c>.</returns>
    public static ExcelFile Build(BoardMonthlyReportDto report, DateTimeOffset exportedAtUtc)
    {
        using var workbook = new XLWorkbook();
        var scope = report.AllCompanies ? "Toàn tập đoàn" : $"Công ty {report.CompanyCode}";
        var exportedAt = TimeZoneInfo.ConvertTime(exportedAtUtc, VietnamTime.Zone);

        var overview = workbook.AddWorksheet("Tổng quan");
        overview.Cell(1, 1).Value = $"Báo cáo tháng {report.Month:00}/{report.Year} cho Ban cung ứng";
        overview.Cell(1, 1).Style.Font.SetBold().Font.SetFontSize(14);
        overview.Cell(2, 1).Value = $"{scope} – từ {report.From:dd/MM/yyyy} đến {report.To:dd/MM/yyyy}, so tháng trước {report.PreviousFrom:dd/MM/yyyy}–" +
                                    $"{report.PreviousTo:dd/MM/yyyy} – xuất lúc {exportedAt:dd/MM/yyyy HH:mm}";
        overview.Cell(2, 1).Style.Font.SetItalic();
        var row = 4;
        if (report.Purchase is { } p)
        {
            // Dòng tỷ lệ là dòng dữ liệu thứ 3: tiêu đề bảng ở startRow, tên cột startRow + 1, dữ liệu từ startRow + 2.
            var percentRow = row + 4;
            row = Table(overview, row, "Tổng quan mua hàng", ["Chỉ tiêu", "Tháng này", "Tháng trước"], [null, Money, Money],
            [
                ["Số PO", p.OrderCount, p.PreviousOrderCount],
                ["Giá trị mua (trước VAT)", p.TotalAmount, p.PreviousTotalAmount],
                ["Tỷ lệ mua theo giá đã duyệt (%)", p.CompliancePct, p.PreviousCompliancePct],
                ["Mua ngoài giá đã duyệt", p.OutOfContractAmount, null],
            ]);
            overview.Range(percentRow, 2, percentRow, 3).Style.NumberFormat.Format = Percent;
            row = Table(overview, row + 1, "Cơ cấu theo nguồn giá", ["Nguồn giá", "Số dòng", "Giá trị", "Tỷ trọng (%)"], [null, Money, Money, Percent],
                p.Sources.Select(s => new object?[] { Label(SourceLabels, s.Source), s.LineCount, s.Amount, s.SharePct }).ToList());
        }

        if (report.Savings is { } s)
        {
            row = Table(overview, row + 1, "Tiết kiệm", ["Chỉ tiêu", "Giá trị", "Tỷ lệ (%)"], [null, Money, Percent],
            [
                [$"Đấu thầu: {s.RfqCount} RFQ, giá trị trúng {s.AwardedAmount:N0}", s.SourcingSaving, s.SourcingSavingPct],
                ["So giá chuẩn (âm = mua cao hơn chuẩn)", s.StandardSaving, s.StandardSavingPct],
                ["Giá trị mua có giá chuẩn", s.StandardCoveredAmount, s.StandardCoveragePct],
            ]);
        }

        row = Table(overview, row + 1, "Các mục của báo cáo", ["Mục", "Có số liệu", "Ghi chú", "Báo cáo chi tiết"], [null, null, null, null],
            report.Sections.Select(x => new object?[] { x.Title, x.Available ? "Có" : "Không", x.Note, x.Report }).ToList());
        Widths(overview, 46, 18, 60, 16);

        if (report.Kpi is { } kpi)
        {
            var sheet = workbook.AddWorksheet("KPI");
            Table(sheet, 1, $"KPI cung ứng – đạt {kpi.MetCount}/{kpi.MeasuredCount} chỉ số có kết quả",
                ["Chỉ số", "Mục tiêu", "Tháng này", "Tháng trước", "Đạt", "Cách đo"], [null, Percent, Percent, Percent, null, null],
                kpi.Items.Select(k => new object?[]
                {
                    k.Name, k.Target, k.Value, k.PreviousValue, k.Met switch { true => "Đạt", false => "Không đạt", _ => "Chưa có dữ liệu" }, k.Formula,
                }).ToList());
            Widths(sheet, 36, 12, 12, 12, 16, 80);
        }

        if (report.OutOfContract is { } lines)
        {
            var sheet = workbook.AddWorksheet("Mua ngoài giá duyệt");
            Table(sheet, 1, $"Mua ngoài giá đã duyệt – {lines.Count}/{report.OutOfContractCount} dòng tiền chênh lớn nhất",
                ["Số PO", "Ngày", "Công ty", "NCC", "Mặt hàng", "SL", "ĐVT", "Giá thực", "Giá đã duyệt", "% lệch", "Tiền chênh", "Loại", "Lý do"],
                [null, Date, null, null, null, Quantity, null, Money, Money, Percent, Money, null, null],
                lines.Select(l => new object?[]
                {
                    l.OrderNo, l.Date, l.CompanyCode, $"{l.SupplierCode} – {l.SupplierName}", $"{l.ItemCode} – {l.ItemName}", l.Qty, l.UomCode,
                    l.NetUnitPrice, l.ApprovedNetPrice, l.DeviationPct, l.DeviationAmount, Label(SourceLabels, l.Kind), l.Reason,
                }).ToList());
            Widths(sheet, 18, 12, 9, 34, 40, 10, 8, 14, 14, 9, 14, 20, 40);
        }

        if (report.PriceVariance is { } v)
        {
            var sheet = workbook.AddWorksheet("Biến động giá");
            Table(sheet, 1, $"Biến động giá – tổng ảnh hưởng {v.ImpactAmount:N0}; {v.IncreasedCount} mặt hàng tăng, {v.DecreasedCount} mặt hàng giảm",
                ["Mặt hàng", "ĐVT", "Giá BQ tháng trước", "Giá BQ tháng này", "% thay đổi", "SL tháng này", "Ảnh hưởng", "Số PO", "Số NCC"],
                [null, null, Money, Money, Percent, Quantity, Money, Money, Money],
                v.TopIncreases.Select(i => new object?[]
                {
                    $"{i.ItemCode} – {i.ItemName}", i.BaseUomCode, i.PreviousAvgPrice, i.CurrentAvgPrice, i.ChangePct, i.CurrentQty, i.ImpactAmount,
                    i.OrderCount, i.SupplierCount,
                }).ToList());
            Widths(sheet, 44, 8, 16, 16, 11, 14, 16, 8, 8);
        }

        if (report.VolumeRebates is { } rebates)
        {
            var sheet = workbook.AddWorksheet("Chiết khấu gộp");
            Table(sheet, 1, "Chiết khấu sản lượng gộp – kỳ tính bậc đang chạy",
                ["Thỏa thuận", "NCC", "Kỳ từ", "Kỳ đến", "Lũy kế", "Bậc đạt", "% CK", "CK phải nhận", "Bậc kế", "Còn thiếu", "So bậc mục tiêu", "Còn thiếu thực nhận"],
                [null, null, Date, Date, Quantity, null, Percent, Money, null, Quantity, null, Money],
                rebates.Select(r => new object?[]
                {
                    $"{r.No} – {r.Title}", $"{r.SupplierCode} – {r.SupplierName}", r.Period.PeriodFrom, r.Period.PeriodTo, r.Period.Volume,
                    r.Period.TierNo, r.Period.Percent, r.Period.RebateAmount, r.Period.NextTierNo, r.Period.Shortfall,
                    Label(TargetLabels, r.Period.TargetStatus), r.UnreceivedAmount,
                }).ToList());
            Widths(sheet, 40, 34, 12, 12, 14, 8, 8, 16, 8, 14, 22, 16);
        }

        if (report.SupplierCoverage is { } coverage)
        {
            var sheet = workbook.AddWorksheet("NCC đủ điều kiện");
            Table(sheet, 1, $"Nhóm hàng chưa đủ {coverage.MinSuppliers} NCC đã duyệt – {coverage.ShortCount}/{coverage.CategoryCount} nhóm",
                ["Nhóm hàng", "Số mặt hàng", "NCC đã duyệt", "Còn thiếu", "NCC tiềm năng", "NCC đã duyệt (danh sách)"],
                [null, Money, Money, Money, Money, null],
                coverage.Categories.Select(c => new object?[]
                {
                    $"{c.CategoryCode} – {c.CategoryName}", c.ItemCount, c.ApprovedCount, c.Missing, c.PotentialCount, c.ApprovedSuppliers,
                }).ToList());
            Widths(sheet, 44, 12, 13, 10, 13, 60);
        }

        if (report.Onboarding is { } o)
        {
            var sheet = workbook.AddWorksheet("Hồ sơ NCC mới");
            Table(sheet, 1, $"Hồ sơ đánh giá NCC mới – {o.Count} hồ sơ: {o.ApprovedCount} đã duyệt, {o.InProgressCount} đang xử lý, {o.ClosedCount} không đạt",
                ["Số", "Ngày", "NCC", "Nhóm hàng", "Điểm", "Kết quả", "Trạng thái"], [null, Date, null, null, Percent, null, null],
                o.Items.Select(i => new object?[]
                {
                    i.No, i.Date, $"{i.SupplierCode} – {i.SupplierName}", i.CategoryName, i.Score, i.Result, Label(OnboardingLabels, i.Status),
                }).ToList());
            Widths(sheet, 18, 12, 40, 30, 8, 16, 20);
        }

        if (report.Control is { } c2)
        {
            var sheet = workbook.AddWorksheet("Kiểm soát nội bộ");
            var next = Table(sheet, 1, $"Dấu hiệu bất thường – Cao {c2.HighCount?.ToString() ?? "–"}, Trung bình {c2.MediumCount?.ToString() ?? "–"}",
                ["Loại dấu hiệu", "Số dấu hiệu", "Ghi chú"], [null, Money, null],
                c2.Summary.Select(a => new object?[] { Label(AnomalyLabels, a.Code), a.Count, a.Note }).ToList());
            Table(sheet, next + 1, $"Sổ vi phạm – {c2.FindingCount} vụ, {c2.SeriousConcludedCount} vụ nghiêm trọng đã kết luận",
                ["Số", "Ngày phát hiện", "Mức", "Trạng thái", "Tiêu đề", "NCC"], [null, Date, null, null, null, null],
                c2.Findings.Select(f => new object?[]
                {
                    f.No, f.Date, f.Severity == "Serious" ? "Nghiêm trọng" : "Thường", f.Status == "Concluded" ? "Đã kết luận" : "Đang mở", f.Title,
                    f.SupplierCode,
                }).ToList());
            Widths(sheet, 40, 14, 14, 14, 50, 14);
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        var suffix = report.AllCompanies ? "TapDoan" : report.CompanyCode;
        return new ExcelFile($"BaoCaoThangBan_{report.Year}-{report.Month:00}_{suffix}.xlsx", stream.ToArray());
    }

    /// <summary>Ghi một bảng: dòng tiêu đề đậm, dòng tên cột có nền, rồi dữ liệu theo định dạng từng cột.</summary>
    /// <param name="sheet">Sheet.</param>
    /// <param name="startRow">Dòng bắt đầu.</param>
    /// <param name="title">Tiêu đề bảng.</param>
    /// <param name="headers">Tên cột.</param>
    /// <param name="formats">Định dạng số của từng cột (null = chữ).</param>
    /// <param name="rows">Dữ liệu.</param>
    /// <returns>Dòng kế tiếp sau bảng (để ghi bảng tiếp theo cách một dòng).</returns>
    private static int Table(IXLWorksheet sheet, int startRow, string title, string[] headers, string?[] formats, IReadOnlyList<object?[]> rows)
    {
        sheet.Cell(startRow, 1).Value = title;
        sheet.Cell(startRow, 1).Style.Font.SetBold();
        for (var c = 0; c < headers.Length; c++)
        {
            var cell = sheet.Cell(startRow + 1, c + 1);
            cell.Value = headers[c];
            cell.Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.FromHtml("#DCE6F4")).Border.SetOutsideBorder(XLBorderStyleValues.Thin);
        }

        for (var r = 0; r < rows.Count; r++)
        {
            for (var c = 0; c < headers.Length; c++)
            {
                var cell = sheet.Cell(startRow + 2 + r, c + 1);
                cell.Value = rows[r][c] switch
                {
                    null => Blank.Value,
                    string text => text,
                    int n => n,
                    decimal d => d,
                    DateOnly date => date.ToDateTime(TimeOnly.MinValue),
                    var other => other.ToString(),
                };
                if (formats[c] is { } format) cell.Style.NumberFormat.Format = format;
            }
        }

        if (rows.Count == 0)
        {
            sheet.Cell(startRow + 2, 1).Value = "Không có dữ liệu.";
            sheet.Cell(startRow + 2, 1).Style.Font.SetItalic();
            return startRow + 4;
        }

        return startRow + 3 + rows.Count;
    }

    private static void Widths(IXLWorksheet sheet, params double[] widths)
    {
        for (var c = 0; c < widths.Length; c++) sheet.Column(c + 1).Width = widths[c];
    }

    private static string Label(Dictionary<string, string> labels, string code) => labels.GetValueOrDefault(code, code);
}
