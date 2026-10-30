using System.Globalization;
using ClosedXML.Excel;
using Proc.Domain.Common;

namespace Proc.Infrastructure.Excel;

/// <summary>Một cột của file Excel danh mục.</summary>
/// <param name="Header">Tiêu đề cột (dòng 1) – nhập đọc cột theo tiêu đề nên đổi thứ tự cột không sao.</param>
/// <param name="Note">Giải thích trên sheet Hướng dẫn.</param>
/// <param name="Required">Bắt buộc khi nhập.</param>
/// <param name="Text">Cột chữ: định dạng ô là Text để Excel không làm mất số 0 đầu (mã số thuế 0101…, số tài khoản).</param>
internal sealed record ExcelColumn(string Header, string Note, bool Required = false, bool Text = false);

/// <summary>Lỗi dữ liệu của một dòng khi nhập (thiếu cột bắt buộc, sai kiểu số…), báo kèm số dòng.</summary>
/// <param name="message">Thông điệp tiếng Việt.</param>
internal sealed class ExcelRowException(string message) : Exception(message);

/// <summary>
/// Đọc giá trị một dòng theo tiêu đề cột. Chấp nhận cả ô số lẫn ô chữ kiểu Việt Nam (1.234,5) hay kiểu quốc tế (1234.5).
/// </summary>
internal sealed class ExcelRowReader
{
    private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi-VN");
    private readonly IXLRow _row;
    private readonly IReadOnlyDictionary<string, int> _columns;

    /// <summary>
    /// Tạo bộ đọc dòng.
    /// </summary>
    /// <param name="row">Dòng trên sheet.</param>
    /// <param name="columns">Tiêu đề cột (đã chuẩn hóa) → số thứ tự cột.</param>
    public ExcelRowReader(IXLRow row, IReadOnlyDictionary<string, int> columns)
    {
        _row = row;
        _columns = columns;
    }

    /// <summary>Số dòng trên sheet.</summary>
    public int RowNumber => _row.RowNumber();

    /// <summary>Mọi ô của các cột đã biết đều trống.</summary>
    public bool IsEmpty => _columns.Values.All(c => _row.Cell(c).IsEmpty() || string.IsNullOrWhiteSpace(_row.Cell(c).GetFormattedString()));

    /// <summary>
    /// Chữ trong ô (đã bỏ khoảng trắng đầu/cuối); null nếu cột không có trong file hoặc ô trống.
    /// Ô số được đổi về chữ không có phần thập phân thừa (Excel lưu mã 0101 dạng số 101 thì không cứu được – vì vậy file mẫu
    /// để các cột mã ở dạng Text).
    /// </summary>
    /// <param name="header">Tiêu đề cột.</param>
    /// <returns>Chữ hoặc null.</returns>
    public string? Text(string header)
    {
        if (!_columns.TryGetValue(ExcelHeaders.Normalize(header), out var column)) return null;
        var cell = _row.Cell(column);
        if (cell.IsEmpty()) return null;
        var text = cell.DataType == XLDataType.Number
            ? cell.GetDouble().ToString("0.############", CultureInfo.InvariantCulture)
            : cell.GetFormattedString();
        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    /// <summary>
    /// Chữ bắt buộc.
    /// </summary>
    /// <param name="header">Tiêu đề cột.</param>
    /// <returns>Chữ.</returns>
    /// <exception cref="ExcelRowException">Khi ô trống.</exception>
    public string RequiredText(string header) => Text(header) ?? throw new ExcelRowException($"Thiếu \"{header}\".");

    /// <summary>
    /// Số thập phân.
    /// </summary>
    /// <param name="header">Tiêu đề cột.</param>
    /// <returns>Số hoặc null nếu trống.</returns>
    /// <exception cref="ExcelRowException">Khi không phải số.</exception>
    public decimal? Decimal(string header)
    {
        if (!_columns.TryGetValue(ExcelHeaders.Normalize(header), out var column)) return null;
        var cell = _row.Cell(column);
        if (cell.IsEmpty()) return null;
        // double → decimal làm tròn 10 chữ số có nghĩa để bỏ sai số dấu phẩy động (24 lưu thành 23,9999999999).
        if (cell.DataType == XLDataType.Number) return Math.Round((decimal)cell.GetDouble(), 10);
        var text = cell.GetFormattedString().Trim();
        if (text.Length == 0) return null;
        if (decimal.TryParse(text, NumberStyles.Number, Vietnamese, out var vi)) return vi;
        if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var inv)) return inv;
        throw new ExcelRowException($"\"{header}\" phải là số (đang là \"{text}\").");
    }

    /// <summary>
    /// Số nguyên.
    /// </summary>
    /// <param name="header">Tiêu đề cột.</param>
    /// <returns>Số hoặc null nếu trống.</returns>
    /// <exception cref="ExcelRowException">Khi không phải số nguyên.</exception>
    public int? Int(string header)
    {
        var value = Decimal(header);
        if (value is null) return null;
        if (value != decimal.Truncate(value.Value) || value is > int.MaxValue or < int.MinValue)
            throw new ExcelRowException($"\"{header}\" phải là số nguyên.");
        return (int)value.Value;
    }

    /// <summary>
    /// Có/Không. Nhận "Có", "Không", "x", "1", "0", "true", "false" (có dấu hay không dấu).
    /// </summary>
    /// <param name="header">Tiêu đề cột.</param>
    /// <returns>Giá trị hoặc null nếu trống.</returns>
    /// <exception cref="ExcelRowException">Khi giá trị lạ.</exception>
    public bool? Bool(string header)
    {
        var text = Text(header);
        if (text is null) return null;
        return SearchText.Normalize(text) switch
        {
            "co" or "x" or "1" or "true" => true,
            "khong" or "0" or "false" => false,
            _ => throw new ExcelRowException($"\"{header}\" ghi \"Có\" hoặc \"Không\" (đang là \"{text}\")."),
        };
    }

    /// <summary>
    /// Ngày (ô ngày của Excel hoặc chữ dd/MM/yyyy).
    /// </summary>
    /// <param name="header">Tiêu đề cột.</param>
    /// <returns>Ngày hoặc null nếu trống.</returns>
    /// <exception cref="ExcelRowException">Khi không đọc được ngày.</exception>
    public DateOnly? Date(string header)
    {
        if (!_columns.TryGetValue(ExcelHeaders.Normalize(header), out var column)) return null;
        var cell = _row.Cell(column);
        if (cell.IsEmpty()) return null;
        if (cell.DataType == XLDataType.DateTime) return DateOnly.FromDateTime(cell.GetDateTime());
        var text = cell.GetFormattedString().Trim();
        if (text.Length == 0) return null;
        if (DateOnly.TryParseExact(text, ["dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return date;
        throw new ExcelRowException($"\"{header}\" phải là ngày dd/MM/yyyy (đang là \"{text}\").");
    }
}

/// <summary>Chuẩn hóa tiêu đề cột để so khớp: bỏ dấu *, bỏ dấu tiếng Việt, chữ thường.</summary>
internal static class ExcelHeaders
{
    /// <summary>
    /// Chuẩn hóa tiêu đề.
    /// </summary>
    /// <param name="header">Tiêu đề như trên file, ví dụ "Mã hàng *".</param>
    /// <returns>Ví dụ "ma hang".</returns>
    public static string Normalize(string header) => SearchText.Normalize(header.Replace("*", string.Empty, StringComparison.Ordinal));

    /// <summary>Chữ "Có"/"Không" cho cột Có/Không khi xuất.</summary>
    /// <param name="value">Giá trị.</param>
    /// <returns>"Có" hoặc "Không".</returns>
    public static string YesNo(bool value) => value ? "Có" : "Không";
}

/// <summary>
/// Ghi sheet danh mục: dòng tiêu đề tô màu, cố định dòng tiêu đề, cột chữ định dạng Text; kèm sheet Hướng dẫn.
/// </summary>
internal static class ExcelWriter
{
    /// <summary>
    /// Tạo file Excel.
    /// </summary>
    /// <param name="sheetName">Tên sheet dữ liệu.</param>
    /// <param name="columns">Các cột.</param>
    /// <param name="rows">Các dòng dữ liệu (cùng thứ tự cột); rỗng = file mẫu.</param>
    /// <param name="guide">Các dòng hướng dẫn chung đặt đầu sheet Hướng dẫn.</param>
    /// <returns>Nội dung .xlsx.</returns>
    public static byte[] Write(string sheetName, IReadOnlyList<ExcelColumn> columns, IEnumerable<IReadOnlyList<object?>> rows, IReadOnlyList<string> guide)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(sheetName);
        for (var c = 0; c < columns.Count; c++)
        {
            var header = sheet.Cell(1, c + 1);
            header.Value = columns[c].Required ? $"{columns[c].Header} *" : columns[c].Header;
            header.Style.Font.Bold = true;
            header.Style.Fill.BackgroundColor = XLColor.FromHtml("#DCE6F1");
            if (columns[c].Text) sheet.Column(c + 1).Style.NumberFormat.Format = "@";
        }

        var r = 2;
        foreach (var row in rows)
        {
            for (var c = 0; c < columns.Count && c < row.Count; c++)
            {
                var cell = sheet.Cell(r, c + 1);
                switch (row[c])
                {
                    case null:
                        break;
                    case decimal d:
                        cell.Value = d;
                        break;
                    case int i:
                        cell.Value = i;
                        break;
                    case DateOnly date:
                        cell.Value = date.ToDateTime(TimeOnly.MinValue);
                        cell.Style.DateFormat.Format = "dd/MM/yyyy";
                        break;
                    default:
                        // Gán chữ cho mọi ô còn lại – kể cả cột Text, để mã có số 0 đầu giữ nguyên.
                        cell.SetValue(row[c]!.ToString());
                        break;
                }
            }

            r++;
        }

        sheet.SheetView.FreezeRows(1);
        sheet.Columns(1, columns.Count).AdjustToContents(1, Math.Min(r, 200), 10, 60);

        var help = workbook.Worksheets.Add("Hướng dẫn");
        var line = 1;
        foreach (var text in guide) help.Cell(line++, 1).Value = text;
        line++;
        help.Cell(line, 1).Value = "Cột";
        help.Cell(line, 2).Value = "Bắt buộc";
        help.Cell(line, 3).Value = "Ghi chú";
        help.Range(line, 1, line, 3).Style.Font.Bold = true;
        foreach (var column in columns)
        {
            line++;
            help.Cell(line, 1).Value = column.Header;
            help.Cell(line, 2).Value = column.Required ? "Có" : string.Empty;
            help.Cell(line, 3).Value = column.Note;
        }

        help.Column(1).Width = 28;
        help.Column(2).Width = 10;
        help.Column(3).Width = 100;

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
