using ClosedXML.Excel;

namespace Proc.Infrastructure.Excel;

/// <summary>
/// Định dạng chung cho mọi file Excel xuất ra (yêu cầu người dùng 14/11/2026, ảnh mẫu <c>docs/diagrams/formatexportExcel.png</c> –
/// ui-style.md mục 5i): giống lưới danh sách trên màn hình. Chữ Tahoma 9pt (= 12px), dòng tiêu đề cột nền xám xanh chữ thường, kẻ ô 1px
/// đủ dọc ngang, dòng cao 22px, nền trắng; cột số canh phải phông đơn cách; số chứng từ phông đơn cách chữ xanh đậm; trạng thái chỉ tô màu
/// chữ theo đèn. Màu lấy đúng mã của biến <c>--erp-*</c> (ui-style.md mục 2) để file và màn hình cùng một kiểu.
/// Chỉ đổi hình thức – không đổi vị trí ô, kiểu ô (số, ngày, chữ), nên file danh mục xuất ra vẫn nhập lại được.
/// </summary>
internal static class ExcelGridStyle
{
    /// <summary>Phông chữ chung (ui-style.md mục 3).</summary>
    public const string FontName = "Tahoma";

    /// <summary>Phông đơn cách cho số, tiền, số chứng từ.</summary>
    public const string MonoFontName = "Consolas";

    /// <summary>Cỡ chữ: 12px trên màn hình = 9pt trong Excel.</summary>
    public const double FontSize = 9;

    /// <summary>Chiều cao dòng lưới: 22px = 16,5pt.</summary>
    public const double RowHeight = 16.5;

    /// <summary>Nền dòng tiêu đề cột (<c>--erp-grid-head</c>).</summary>
    public const string HeaderFillHex = "#D3DDEA";

    /// <summary>Đường kẻ ô (<c>--erp-grid-line</c>).</summary>
    public const string GridLineHex = "#B8C5D6";

    /// <summary>Chữ chính (<c>--erp-text</c>).</summary>
    public const string TextHex = "#1B2A3A";

    /// <summary>Chữ tiêu đề báo cáo và số chứng từ (<c>--erp-title</c>).</summary>
    public const string TitleHex = "#1F3D6B";

    /// <summary>Chữ phụ – dòng điều kiện lọc, giờ xuất (<c>--erp-text-muted</c>).</summary>
    public const string MutedHex = "#5A6B7D";

    private static readonly XLColor HeaderFill = XLColor.FromHtml(HeaderFillHex);
    private static readonly XLColor GridLine = XLColor.FromHtml(GridLineHex);
    private static readonly XLColor TextColor = XLColor.FromHtml(TextHex);
    private static readonly XLColor TitleColor = XLColor.FromHtml(TitleHex);
    private static readonly XLColor MutedColor = XLColor.FromHtml(MutedHex);

    /// <summary>
    /// Màu chữ của đèn trạng thái – cùng màu chữ trạng thái của <c>DataGrid</c> (theme.ts: colorSuccess, colorWarning, colorError).
    /// Đèn trung tính: chữ màu thường.
    /// </summary>
    private static readonly Dictionary<string, string> ToneColors = new(StringComparer.Ordinal)
    {
        ["success"] = "#1E8F2E",
        ["warning"] = "#A86F00",
        ["error"] = "#D0281E",
        ["neutral"] = TextHex,
    };

    /// <summary>
    /// Tạo workbook với phông mặc định Tahoma 9pt chữ <c>--erp-text</c> – mọi ô, mọi sheet thêm sau đều mang phông này.
    /// </summary>
    /// <returns>Workbook mới (người gọi dispose).</returns>
    public static XLWorkbook CreateWorkbook()
    {
        var workbook = new XLWorkbook();
        workbook.Style.Font.FontName = FontName;
        workbook.Style.Font.FontSize = FontSize;
        workbook.Style.Font.FontColor = TextColor;
        workbook.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        return workbook;
    }

    /// <summary>
    /// Thêm sheet báo cáo: dòng cao 22px, tắt lưới mờ của Excel để chỉ còn đường kẻ của bảng như lưới trên màn hình.
    /// </summary>
    /// <param name="workbook">Workbook tạo bởi <see cref="CreateWorkbook"/>.</param>
    /// <param name="name">Tên sheet.</param>
    /// <param name="showGridLines">true = giữ lưới mờ của Excel (file mẫu nhập danh mục – người dùng gõ thêm dòng ngoài bảng).</param>
    /// <returns>Sheet.</returns>
    public static IXLWorksheet AddSheet(XLWorkbook workbook, string name, bool showGridLines = false)
    {
        var sheet = workbook.AddWorksheet(name);
        sheet.ShowGridLines = showGridLines;
        sheet.RowHeight = RowHeight;
        return sheet;
    }

    /// <summary>Tiêu đề báo cáo: 18px đậm (13,5pt) chữ <c>--erp-title</c>.</summary>
    /// <param name="cell">Ô.</param>
    /// <param name="text">Tiêu đề.</param>
    public static void Title(IXLCell cell, string text)
    {
        cell.Value = text;
        cell.Style.Font.SetBold().Font.SetFontSize(13.5).Font.SetFontColor(TitleColor);
        cell.WorksheetRow().Height = 22;
    }

    /// <summary>Tiêu đề một bảng con (báo cáo nhiều bảng trên một sheet): đậm, chữ <c>--erp-title</c>, cỡ thường.</summary>
    /// <param name="cell">Ô.</param>
    /// <param name="text">Tiêu đề.</param>
    public static void SectionTitle(IXLCell cell, string text)
    {
        cell.Value = text;
        cell.Style.Font.SetBold().Font.SetFontColor(TitleColor);
    }

    /// <summary>Dòng ghi chú dưới tiêu đề (điều kiện lọc, giờ xuất): nghiêng, chữ <c>--erp-text-muted</c>.</summary>
    /// <param name="cell">Ô.</param>
    /// <param name="text">Nội dung.</param>
    public static void Note(IXLCell cell, string text)
    {
        cell.Value = text;
        cell.Style.Font.SetItalic().Font.SetFontColor(MutedColor);
    }

    /// <summary>
    /// Dòng tiêu đề cột: nền <c>--erp-grid-head</c>, chữ thường (không đậm – như lưới trên màn hình), canh trái, kẻ ô đủ bốn cạnh.
    /// </summary>
    /// <param name="range">Các ô tiêu đề (một dòng).</param>
    public static void Header(IXLRange range)
    {
        range.Style.Fill.SetBackgroundColor(HeaderFill)
            .Font.SetBold(false).Font.SetFontColor(TextColor)
            .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Left).Alignment.SetVertical(XLAlignmentVerticalValues.Center);
        Borders(range);
    }

    /// <summary>Thân bảng: nền trắng, kẻ ô 1px <c>--erp-grid-line</c> đủ dọc ngang.</summary>
    /// <param name="range">Các ô dữ liệu (có thể gồm cả dòng tiêu đề).</param>
    public static void Body(IXLRange range) => Borders(range);

    /// <summary>Cột số lượng, đơn giá, tiền: phông đơn cách, canh phải (ui-style.md mục 3).</summary>
    /// <param name="range">Các ô dữ liệu của cột (không gồm tiêu đề).</param>
    public static void Number(IXLRange range) =>
        range.Style.Font.SetFontName(MonoFontName).Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right);

    /// <summary>Cột ngày: canh trái phông thường như lưới trên màn hình (Excel mặc định canh phải ô ngày).</summary>
    /// <param name="range">Các ô dữ liệu của cột (không gồm tiêu đề).</param>
    public static void Date(IXLRange range) => range.Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Left);

    /// <summary>Cột số chứng từ: phông đơn cách, chữ xanh đậm như ô số chứng từ trên màn hình (ui-style.md mục 5e).</summary>
    /// <param name="range">Các ô dữ liệu của cột (không gồm tiêu đề).</param>
    public static void DocNo(IXLRange range) => range.Style.Font.SetFontName(MonoFontName).Font.SetFontColor(TitleColor);

    /// <summary>
    /// Ô trạng thái: chỉ tô màu chữ theo đèn, không thêm ký tự nào (người dùng chốt 14/11/2026) – ô giữ đúng chữ như màn hình nên lọc,
    /// tìm, sao chép trong Excel không vướng ký hiệu. Tông lạ hoặc trống: chữ màu thường.
    /// </summary>
    /// <param name="cell">Ô.</param>
    /// <param name="text">Chữ trạng thái đang hiển thị.</param>
    /// <param name="tone">success / warning / error / neutral; null = không có đèn.</param>
    public static void Status(IXLCell cell, string text, string? tone)
    {
        cell.Value = text;
        if (tone is not null && ToneColors.TryGetValue(tone, out var color)) cell.Style.Font.SetFontColor(XLColor.FromHtml(color));
    }

    /// <summary>Kẻ ô 1px màu <c>--erp-grid-line</c> mọi cạnh trong và ngoài vùng.</summary>
    private static void Borders(IXLRange range) =>
        range.Style.Border.SetOutsideBorder(XLBorderStyleValues.Thin).Border.SetOutsideBorderColor(GridLine)
            .Border.SetInsideBorder(XLBorderStyleValues.Thin).Border.SetInsideBorderColor(GridLine);
}
