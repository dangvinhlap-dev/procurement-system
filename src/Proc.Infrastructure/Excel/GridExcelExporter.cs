using System.Globalization;
using ClosedXML.Excel;
using Proc.Application.Common;
using Proc.Application.Excel;

namespace Proc.Infrastructure.Excel;

/// <summary>
/// Dựng file Excel từ lưới đang xem (design.md mục 11.2): dòng 1 tiêu đề, dòng 2 điều kiện lọc và giờ xuất, rồi dòng tiêu đề cột
/// (cố định khi cuộn, có lọc) và dữ liệu. Ô số giữ kiểu số để Excel cộng được; ô ngày giữ kiểu ngày định dạng dd/MM/yyyy.
/// </summary>
public sealed class GridExcelExporter : IGridExcelExporter
{
    /// <summary>Hàng đầu tiên của bảng dữ liệu (dòng tiêu đề cột).</summary>
    private const int HeaderRow = 4;

    private readonly IClock _clock;

    /// <summary>Tạo bộ dựng file.</summary>
    /// <param name="clock">Đồng hồ (ghi giờ xuất trên file).</param>
    public GridExcelExporter(IClock clock) => _clock = clock;

    /// <inheritdoc />
    public ExcelFile Build(GridExportRequest request)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Dữ liệu");
        var columns = request.Columns;

        sheet.Cell(1, 1).Value = request.Title;
        sheet.Cell(1, 1).Style.Font.SetBold().Font.SetFontSize(14);
        var exportedAt = TimeZoneInfo.ConvertTime(_clock.UtcNow, VietnamTime.Zone);
        var subtitle = string.IsNullOrWhiteSpace(request.Subtitle) ? "" : $"{request.Subtitle} – ";
        sheet.Cell(2, 1).Value =
            $"{subtitle}Xuất lúc {exportedAt:dd/MM/yyyy HH:mm} – {request.Rows.Count.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"))} dòng";
        sheet.Cell(2, 1).Style.Font.SetItalic();

        for (var c = 0; c < columns.Count; c++)
        {
            var cell = sheet.Cell(HeaderRow, c + 1);
            cell.Value = columns[c].Title;
            cell.Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.FromHtml("#DCE6F4"))
                .Border.SetOutsideBorder(XLBorderStyleValues.Thin);
            // Độ rộng Excel tính theo số ký tự; ~7 px một ký tự ở cỡ chữ mặc định.
            sheet.Column(c + 1).Width = Math.Clamp((columns[c].Width ?? 100) / 7.0, 6, 80);
            var format = columns[c].Type switch
            {
                "money" => "#,##0",
                "quantity" => "#,##0.####",
                "date" => "dd/mm/yyyy",
                "datetime" => "dd/mm/yyyy hh:mm",
                _ => null,
            };
            if (format is not null) sheet.Column(c + 1).Style.NumberFormat.Format = format;
        }

        for (var r = 0; r < request.Rows.Count; r++)
        {
            var row = request.Rows[r];
            for (var c = 0; c < columns.Count; c++)
                sheet.Cell(HeaderRow + 1 + r, c + 1).Value = ToCellValue(columns[c].Type, row[c]);
        }

        var lastRow = HeaderRow + Math.Max(request.Rows.Count, 1);
        sheet.Range(HeaderRow, 1, lastRow, columns.Count).SetAutoFilter();
        sheet.SheetView.FreezeRows(HeaderRow);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return new ExcelFile($"{request.FileName}.xlsx", stream.ToArray());
    }

    /// <summary>
    /// Đổi giá trị thô từ giao diện sang ô Excel theo kiểu cột. Giá trị không đọc được theo kiểu cột thì giữ nguyên dạng chữ –
    /// không làm hỏng cả file chỉ vì một ô.
    /// </summary>
    /// <param name="type">Kiểu cột.</param>
    /// <param name="raw">Giá trị thô: số 1234.5, ngày yyyy-MM-dd, ngày giờ ISO 8601 UTC.</param>
    /// <returns>Giá trị ô.</returns>
    private static XLCellValue ToCellValue(string? type, string? raw)
    {
        if (string.IsNullOrEmpty(raw)) return Blank.Value;
        switch (type)
        {
            case "money" or "quantity" when decimal.TryParse(raw, NumberStyles.Number | NumberStyles.AllowExponent, CultureInfo.InvariantCulture, out var number):
                return number;
            case "date" when DateOnly.TryParseExact(raw, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date):
                return date.ToDateTime(TimeOnly.MinValue);
            case "datetime" when DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var at):
                // Lưu UTC, hiển thị giờ Việt Nam (CLAUDE.md mục 5).
                return TimeZoneInfo.ConvertTime(at, VietnamTime.Zone).DateTime;
            default:
                return raw;
        }
    }
}
