using System.Globalization;
using ClosedXML.Excel;
using Proc.Application.Common;
using Proc.Application.Excel;

namespace Proc.Infrastructure.Excel;

/// <summary>
/// Dựng file Excel từ lưới đang xem (design.md mục 11.2): dòng 1 tiêu đề, dòng 2 điều kiện lọc và giờ xuất, rồi dòng tiêu đề cột
/// (cố định khi cuộn, có lọc) và dữ liệu. Ô số giữ kiểu số để Excel cộng được; ô ngày giữ kiểu ngày định dạng dd/MM/yyyy.
/// Hình thức giống lưới trên màn hình (<see cref="ExcelGridStyle"/>, ui-style.md mục 5i).
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
        using var workbook = ExcelGridStyle.CreateWorkbook();
        var sheet = ExcelGridStyle.AddSheet(workbook, "Dữ liệu");
        var columns = request.Columns;

        ExcelGridStyle.Title(sheet.Cell(1, 1), request.Title);
        var exportedAt = TimeZoneInfo.ConvertTime(_clock.UtcNow, VietnamTime.Zone);
        var subtitle = string.IsNullOrWhiteSpace(request.Subtitle) ? "" : $"{request.Subtitle} – ";
        ExcelGridStyle.Note(sheet.Cell(2, 1),
            $"{subtitle}Xuất lúc {exportedAt:dd/MM/yyyy HH:mm} – {request.Rows.Count.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"))} dòng");

        // Không có dòng nào vẫn kẻ một dòng trống để bảng có khung như lưới rỗng trên màn hình.
        var lastRow = HeaderRow + Math.Max(request.Rows.Count, 1);
        for (var c = 0; c < columns.Count; c++)
        {
            sheet.Cell(HeaderRow, c + 1).Value = columns[c].Title;
            // Độ rộng Excel tính theo số ký tự; ~7 px một ký tự ở cỡ chữ mặc định.
            sheet.Column(c + 1).Width = Math.Clamp((columns[c].Width ?? 100) / 7.0, 6, 80);
            var data = sheet.Range(HeaderRow + 1, c + 1, lastRow, c + 1);
            switch (columns[c].Type)
            {
                case "money":
                    data.Style.NumberFormat.Format = "#,##0";
                    ExcelGridStyle.Number(data);
                    break;
                case "quantity":
                    data.Style.NumberFormat.Format = "#,##0.####";
                    ExcelGridStyle.Number(data);
                    break;
                case "date":
                    data.Style.NumberFormat.Format = "dd/mm/yyyy";
                    ExcelGridStyle.Date(data);
                    break;
                case "datetime":
                    data.Style.NumberFormat.Format = "dd/mm/yyyy hh:mm";
                    ExcelGridStyle.Date(data);
                    break;
                case "docno":
                    ExcelGridStyle.DocNo(data);
                    break;
            }
        }

        ExcelGridStyle.Header(sheet.Range(HeaderRow, 1, HeaderRow, columns.Count));
        ExcelGridStyle.Body(sheet.Range(HeaderRow, 1, lastRow, columns.Count));

        for (var r = 0; r < request.Rows.Count; r++)
        {
            var row = request.Rows[r];
            for (var c = 0; c < columns.Count; c++)
            {
                var cell = sheet.Cell(HeaderRow + 1 + r, c + 1);
                // Cột trạng thái có màu đèn: ô vuông ■ + chữ tô màu như lưới trên màn hình.
                if (columns[c].Tones is { } tones && columns[c].Type == "status" && !string.IsNullOrEmpty(row[c]))
                    ExcelGridStyle.Status(cell, row[c]!, tones[r]);
                else
                    cell.Value = ToCellValue(columns[c].Type, row[c]);
            }
        }

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
