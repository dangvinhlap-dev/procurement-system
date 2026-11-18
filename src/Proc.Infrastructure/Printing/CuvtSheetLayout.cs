using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using PdfSharp.Drawing;

namespace Proc.Infrastructure.Printing;

/// <summary>Một hình nằm đè trên sheet (logo, vòng tròn số biểu mẫu) – vị trí đo từ neo hình trong file Excel.</summary>
/// <param name="Col">Cột neo (0 = A).</param>
/// <param name="OffsetPx">Lệch phải trong cột neo (điểm ảnh sheet).</param>
/// <param name="TopPx">Cách mép trên sheet (điểm ảnh).</param>
/// <param name="WidthPx">Rộng (điểm ảnh).</param>
/// <param name="HeightPx">Cao (điểm ảnh).</param>
public sealed record SheetPicture(int Col, double OffsetPx, double TopPx, double WidthPx, double HeightPx);

/// <summary>
/// Số đo khối ký – ảnh khối ký trong sheet (đo đường kẻ trên ảnh gốc) và cỡ hiển thị của ảnh trên sheet.
/// </summary>
/// <param name="ColumnsPx">Độ rộng từng cột trên ảnh gốc (điểm ảnh).</param>
/// <param name="RowsPx">Cao từng hàng trên ảnh gốc: dòng "Kính trình…", hàng chức danh, hàng ký (và hàng Chủ tịch nếu ô Chủ tịch cả hàng).</param>
/// <param name="WidthPx">Rộng ảnh khi đặt trên sheet (điểm ảnh sheet).</param>
/// <param name="HeightPx">Cao ảnh khi đặt trên sheet.</param>
/// <param name="GapPx">Khoảng cách từ dòng cuối bảng tới mép trên ảnh.</param>
/// <param name="ChairmanInRow">Ô Chủ tịch là cột cuối cùng hàng (2A, 3A, 4A); false = ô Chủ tịch cả hàng dưới (5A, 5B).</param>
/// <param name="HeadingPt">Cỡ chữ dòng "Kính trình…" (pt theo sheet, chưa nhân tỉ lệ).</param>
/// <param name="TitlePt">Cỡ chữ chức danh.</param>
public sealed record SheetSignStyle(
    double[] ColumnsPx, double[] RowsPx, double WidthPx, double HeightPx, double GapPx, bool ChairmanInRow, double HeadingPt, double TitlePt);

/// <summary>
/// Số đo một sheet biểu mẫu CUVT để <see cref="CuvtSheetLayout"/> vẽ lại giữ nguyên (lát IN2 – design.md mục 20.4; người dùng 18/11/2026: "các phiếu
/// này giống hệt sheet Excel như 1A").
/// </summary>
/// <param name="Landscape">Khổ ngang.</param>
/// <param name="SheetColumns">Độ rộng cột A… của sheet (đơn vị ký tự Excel; điểm ảnh = rộng × 7 + 5).</param>
/// <param name="ColumnMap">
/// Số cột sheet của từng cột logic của bảng (ví dụ "Tên hàng" gộp B:C = 2). Biểu mẫu có nhiều cột hơn bản đồ (cột "Đơn vị mua" của 2A/2B – mỗi đơn
/// vị một cột) thì các cột thêm lấy độ rộng bình quân của các cột sheet còn lại: 2A có 2 cột đơn vị K, L (rộng 8,6 / 7,5) → mỗi đơn vị 61 điểm
/// ảnh; bảng 3 đơn vị rộng thêm một cột như khi chèn cột trong Excel.
/// </param>
/// <param name="HeaderRowsPt">Cao các dòng khung đầu phiếu (pt): tiêu đề, phụ đề, ngày.</param>
/// <param name="DateColor">Màu dòng ngày (4A đen, 4B xanh #0000CC).</param>
/// <param name="InfoRowPt">Cao mỗi dòng khung thông tin.</param>
/// <param name="TableHeaderPt">Cao dòng tiêu đề bảng (tầng trên khi có cột nhóm).</param>
/// <param name="RowPt">Cao tối thiểu dòng hàng.</param>
/// <param name="TotalRowPt">Cao dòng tổng.</param>
/// <param name="Logo">Vị trí khung logo.</param>
/// <param name="Circle">Vị trí vòng tròn số biểu mẫu.</param>
/// <param name="Sign">Số đo khối ký.</param>
/// <param name="CenteredColumns">Cột logic căn giữa ở dòng hàng (ngoài cột có cờ <see cref="CuvtColumn.Center"/>).</param>
/// <param name="Scale">Tỉ lệ in của sheet (Page Setup – 4A 85 %, 4B 75 %); sheet rộng hơn vùng in thì co thêm cho vừa.</param>
/// <param name="TableHeader2Pt">Cao tầng dưới của tiêu đề bảng khi có cột nhóm (2A "Nội dung đề xuất", "Đơn vị mua").</param>
/// <param name="HeaderFilled">Tiêu đề bảng nền xanh nhạt (4A/4B có, 2A/2B không).</param>
/// <param name="TotalDash">Ô trống dòng tổng in "-" (định dạng kế toán của 4A/4B); false = để trống.</param>
/// <param name="RedColumns">Cột logic chữ đỏ ở dòng hàng (2A/2B "Ngày cung ứng").</param>
public sealed record SheetLayoutSpec(
    bool Landscape, double[] SheetColumns, int[] ColumnMap, double[] HeaderRowsPt, Color DateColor, double InfoRowPt, double TableHeaderPt,
    double RowPt, double TotalRowPt, SheetPicture Logo, SheetPicture Circle, SheetSignStyle Sign, int[]? CenteredColumns = null, double Scale = 0.85,
    double TableHeader2Pt = 0, bool HeaderFilled = true, bool TotalDash = true, int[]? RedColumns = null);

/// <summary>
/// Khuôn in giữ nguyên sheet biểu mẫu CUVT (lát IN2, cùng cách làm của 1A – <see cref="PurchaseRequestSheetLayout"/>): mọi kích thước lấy từ sheet
/// theo điểm ảnh rồi co cả trang theo tỉ lệ in của sheet <c>k</c> (sheet rộng hơn vùng in thì co thêm cho vừa), căn giữa trang như sheet
/// (Page Setup "Center horizontally"). Ví dụ 4A: cột A–M = 1.297 điểm ảnh = 343 mm; tỉ lệ 85 % → 292 mm &gt; vùng in 285 mm → k = 0,83, chữ bảng
/// 13 pt in 10,8 pt; 4B: 1.247 điểm ảnh = 330 mm × 75 % = 247 mm, lề hai bên 25 mm.
/// <list type="bullet">
/// <item>Khung đầu phiếu kẻ viền ngoài: tiêu đề 18 đậm, phụ đề 12 đậm xanh #0000CC, ngày 16 đậm; logo, vòng tròn số biểu mẫu vẽ đè đúng vị trí neo.</item>
/// <item>Khung thông tin: dòng IN HOA căn giữa cỡ 14, số tiền đậm.</item>
/// <item>Bảng: tiêu đề nền xanh nhạt; dòng tổng cỡ 15 đậm nền xanh nhạt, ô trống in "-" như định dạng kế toán của sheet; cột gộp dọc (NCC, lũy kế, còn
/// lại). Dòng sheet ẩn (VAT của 4A/4B) không in; dòng "Bằng chữ", ghi chú không có trên sheet nên không in.</item>
/// <item>Khối ký vẽ lại theo ảnh trong sheet (chức danh nguyên văn) – có chữ ký, họ tên, giờ duyệt.</item>
/// <item>Ngoài sheet (yêu cầu 18/11/2026): góc thông tin hệ thống dưới phải chữ 9, dấu chìm khi phiếu chưa duyệt.</item>
/// </list>
/// </summary>
public static class CuvtSheetLayout
{
    private const double MarginMm = 6;
    private const double MmPerPx = 25.4 / 96;

    private static readonly Color Blue = new(0x00, 0x00, 0xCC);
    private static readonly Color HeaderFill = new(0xDA, 0xEE, 0xF3);
    private static readonly Color TotalFill = new(0xDC, 0xE6, 0xF1);
    private static readonly Color Muted = new(0x70, 0x70, 0x70);
    private static readonly Color SignLine = new(0x44, 0x54, 0x6A);

    /// <summary>Số đo sheet "4A.Theo dõi MH" (A4 ngang, cột A–M).</summary>
    public static readonly SheetLayoutSpec Sheet4A = new(
        true, [4.4, 7.0, 12.4, 20.3, 6.7, 8.6, 12.7, 10.5, 12.3, 15.4, 15.0, 16.0, 14.7], [1, 2, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1],
        [23.25, 20.1, 20.1, 20.1, 20.1], Colors.Black, 24.9, 50.4, 29.25, 29.25,
        new SheetPicture(1, 7, 11, 71, 69), new SheetPicture(11, 59, 16, 97, 96),
        new SheetSignStyle([204, 265, 320, 258, 297], [45, 41, 161], 1258, 260, 22, true, 15, 14));

    /// <summary>Số đo sheet "4B.Theo dõi MH" – như 4A, cột rộng hơn, tiêu đề bảng cao 66 pt, ngày xanh.</summary>
    public static readonly SheetLayoutSpec Sheet4B = new(
        true, [4.4, 7.0, 16.7, 20.3, 6.7, 8.6, 12.7, 14.6, 13.3, 16.4, 17.0, 16.0, 15.2], [1, 2, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1],
        [23.25, 20.1, 20.1, 20.1, 20.1], Blue, 24.9, 66, 29.25, 29.25,
        new SheetPicture(0, 25, 12, 101, 98), new SheetPicture(11, 77, 28, 97, 96),
        new SheetSignStyle([204, 265, 320, 258, 297], [45, 41, 161], 1360, 274, 16, true, 15, 14), [3, 4], 0.75);

    /// <summary>
    /// Số đo sheet "2A.TH MUA VT" (A4 ngang 85 %, cột A–L): tiêu đề bảng hai tầng ("Nội dung đề xuất", "Đơn vị mua") không nền, dòng tổng "Tổng cộng"
    /// gộp A:C căn giữa, ngày cung ứng chữ đỏ; ĐVT, số lượng căn trái như sheet.
    /// </summary>
    public static readonly SheetLayoutSpec Sheet2A = new(
        true, [11.8, 6.9, 11.9, 22.3, 6.7, 7.3, 15.6, 19.4, 14.0, 14.3, 8.6, 7.5], [1, 1, 1, 1, 1, 1, 1, 1, 1, 1],
        [23.25, 20.1, 20.1, 20.1, 20.1], Colors.Black, 26.1, 24, 33.6, 20.25,
        new SheetPicture(0, 39, 9, 95, 90), new SheetPicture(9, 84, 9, 89, 84),
        new SheetSignStyle([168, 204, 186, 192, 216], [34, 28, 149], 1179, 242, 16, true, 15, 14),
        TableHeader2Pt: 33.6, HeaderFilled: false, TotalDash: false, RedColumns: [8]);

    /// <summary>Số đo sheet "2B.TH MUA VT" – như 2A, 4 dòng thông tin, ngày xanh, ngày đề xuất và ngày cung ứng chữ đỏ, các cột căn giữa.</summary>
    public static readonly SheetLayoutSpec Sheet2B = new(
        true, [11.3, 6.9, 12.9, 19.6, 6.7, 8.4, 15.6, 19.4, 14.0, 13.4, 8.6, 9.3], [1, 1, 1, 1, 1, 1, 1, 1, 1, 1],
        [23.25, 20.1, 20.1, 20.1, 20.1], Blue, 26.1, 24, 33, 33,
        new SheetPicture(0, 39, 9, 95, 90), new SheetPicture(10, 29, 16, 89, 84),
        new SheetSignStyle([168, 204, 186, 192, 216], [34, 28, 149], 1179, 248, 5, true, 15, 14), [4, 5, 6, 8],
        TableHeader2Pt: 33.6, HeaderFilled: false, TotalDash: false, RedColumns: [0, 8]);

    /// <summary>Dựng PDF.</summary>
    /// <param name="form">Nội dung phiếu (tiêu đề, phụ đề, thông tin, cột, dòng, dòng tổng).</param>
    /// <param name="spec">Số đo sheet.</param>
    /// <param name="logo">Logo công ty của phiếu; null = để trống.</param>
    /// <param name="sign">Khối ký đã ghép bước duyệt (<see cref="SheetSign.Build"/>).</param>
    /// <param name="stamp">Góc thông tin hệ thống, dấu chìm.</param>
    /// <returns>Nội dung PDF.</returns>
    public static byte[] Render(CuvtForm form, SheetLayoutSpec spec, SheetImage? logo, SheetSignBlock sign, SheetStamp stamp)
    {
        var document = new Document();
        document.Info.Title = $"{form.Title} {form.No}";
        var columnPx = LogicalPx(form, spec);
        var g = new Geometry(spec, columnPx.Sum());
        var normal = document.Styles[StyleNames.Normal]!;
        normal.Font.Name = PdfFonts.SerifFamily;
        normal.Font.Size = g.Pt(13);

        var section = document.AddSection();
        var setup = section.PageSetup;
        setup.PageFormat = PageFormat.A4;
        setup.Orientation = spec.Landscape ? Orientation.Landscape : Orientation.Portrait;
        setup.LeftMargin = Unit.FromMillimeter(g.LeftMm);
        setup.RightMargin = Unit.FromMillimeter(g.PageWidthMm - g.LeftMm - g.WidthMm);
        setup.TopMargin = Unit.FromMillimeter(MarginMm);
        setup.BottomMargin = Unit.FromMillimeter(26);
        setup.FooterDistance = Unit.FromMillimeter(4);

        SheetPrintParts.AddStamp(section, stamp, g.WidthMm);
        AddHeader(section, form, spec, g);
        AddInfo(section, form, spec, g);
        AddLines(section, form, spec, g, columnPx);
        AddSignBlock(section, sign, spec.Sign, g);

        var renderer = new PdfDocumentRenderer { Document = document };
        renderer.RenderDocument();
        DrawOverlays(renderer.PdfDocument, form.FormNo, logo, stamp.Watermark, spec, g);
        using var stream = new MemoryStream();
        renderer.PdfDocument.Save(stream, false);
        return stream.ToArray();
    }

    /// <summary>Quy đổi điểm ảnh sheet ↔ mm trên giấy: cả sheet co theo một tỉ lệ để vừa bề ngang vùng in.</summary>
    private sealed class Geometry
    {
        public Geometry(SheetLayoutSpec spec, double totalPx)
        {
            ColumnPx = spec.SheetColumns.Select(ColumnWidthPx).ToArray();
            TotalPx = totalPx;
            PageWidthMm = spec.Landscape ? 297 : 210;
            var natural = totalPx * MmPerPx;
            K = Math.Min(spec.Scale, (PageWidthMm - 2 * MarginMm) / natural);
            WidthMm = natural * K;
            MmPx = MmPerPx * K;
            LeftMm = (PageWidthMm - WidthMm) / 2;
        }

        /// <summary>Rộng giấy (mm).</summary>
        public double PageWidthMm { get; }

        /// <summary>Lề trái để sheet nằm giữa trang (mm).</summary>
        public double LeftMm { get; }

        /// <summary>Rộng từng cột sheet (điểm ảnh).</summary>
        public double[] ColumnPx { get; }

        /// <summary>Rộng bảng của phiếu đang in (điểm ảnh) – bằng rộng sheet, trừ khi có thêm cột đơn vị mua.</summary>
        public double TotalPx { get; }

        /// <summary>Rộng sheet trên giấy (mm).</summary>
        public double WidthMm { get; }

        /// <summary>mm trên giấy của một điểm ảnh sheet.</summary>
        public double MmPx { get; }

        /// <summary>Tỉ lệ in so với cỡ thật của sheet.</summary>
        public double K { get; }

        public Unit Pt(double sheetPt) => Unit.FromPoint(sheetPt * K);

        public Unit Px(double px) => Unit.FromMillimeter(px * MmPx);

        /// <summary>Mép trái cột <paramref name="col"/> (điểm ảnh).</summary>
        public double Left(int col) => ColumnPx.Take(col).Sum();

        /// <summary>
        /// Mép trái hình neo trên sheet. Vòng tròn số biểu mẫu nằm sát phải khung đầu phiếu nên giữ khoảng cách tới mép phải (bảng rộng thêm cột
        /// đơn vị mua thì vòng tròn dời theo); logo giữ khoảng cách tới mép trái.
        /// </summary>
        public double PictureLeft(SheetPicture p, bool fromRight) =>
            fromRight ? TotalPx - (ColumnPx.Sum() - (Left(p.Col) + p.OffsetPx)) : Left(p.Col) + p.OffsetPx;
    }

    /// <summary>Rộng một cột Excel ra điểm ảnh (cỡ chữ mặc định 7 điểm ảnh một ký tự + 5 lề): cột 8,43 = 64 điểm ảnh.</summary>
    private static double ColumnWidthPx(double width) => width * 7 + 5;

    /// <summary>
    /// Khung đầu phiếu: tiêu đề, phụ đề, ngày căn giữa cả bề ngang; dòng nào dài hơn phần giữa logo và vòng tròn thì thu nhỏ cỡ chữ cho vừa một dòng
    /// (như Excel "vừa ô").
    /// </summary>
    private static void AddHeader(Section section, CuvtForm form, SheetLayoutSpec spec, Geometry g)
    {
        var table = section.AddTable();
        table.Borders.Visible = false;
        table.AddColumn(Unit.FromMillimeter(g.WidthMm));
        var lines = new List<(string Text, double Size, Color Color)> { (form.Title, 18, Colors.Black) };
        lines.AddRange(form.Subtitles.Take(spec.HeaderRowsPt.Length - 2).Select(s => (s, 12.0, Blue)));
        while (lines.Count < spec.HeaderRowsPt.Length - 1) lines.Add(("", 12, Blue));
        lines.Add(($"Ngày {form.Date.Day} tháng {form.Date.Month} năm {form.Date.Year}", 16, spec.DateColor));
        var measure = XGraphics.CreateMeasureContext(new XSize(4000, 4000), XGraphicsUnit.Point, XPageDirection.Downwards);
        var leftPx = g.PictureLeft(spec.Logo, false) + spec.Logo.WidthPx;
        var rightPx = g.TotalPx - g.PictureLeft(spec.Circle, true);
        var room = (g.TotalPx - 2 * Math.Max(leftPx, rightPx)) * g.MmPx * 72 / 25.4;
        for (var r = 0; r < spec.HeaderRowsPt.Length; r++)
        {
            var row = table.AddRow();
            row.HeightRule = RowHeightRule.Exactly;
            row.Height = g.Pt(spec.HeaderRowsPt[r]);
            row.VerticalAlignment = VerticalAlignment.Center;
            var (text, size, color) = lines[r];
            var points = size * g.K;
            var width = measure.MeasureString(text, new XFont(PdfFonts.SerifFamily, points, XFontStyleEx.Bold)).Width;
            if (width > room) points *= room / width;
            var p = row.Cells[0].AddParagraph(text);
            p.Format.Alignment = ParagraphAlignment.Center;
            p.Format.Font.Size = Unit.FromPoint(points);
            p.Format.Font.Bold = true;
            p.Format.Font.Color = color;
        }

        table.SetEdge(0, 0, 1, spec.HeaderRowsPt.Length, Edge.Box, BorderStyle.Single, 0.75, Colors.Black);
    }

    /// <summary>Khung thông tin: dòng IN HOA căn giữa cỡ 14 (ghi chú sheet "Phải tạo thành chữ V"), số tiền in đậm có đơn vị "đ".</summary>
    private static void AddInfo(Section section, CuvtForm form, SheetLayoutSpec spec, Geometry g)
    {
        if (form.Info.Count == 0) return;
        var table = section.AddTable();
        table.Borders.Visible = false;
        table.AddColumn(Unit.FromMillimeter(g.WidthMm));
        foreach (var line in form.Info)
        {
            var row = table.AddRow();
            row.HeightRule = RowHeightRule.AtLeast;
            row.Height = g.Pt(spec.InfoRowPt);
            row.VerticalAlignment = VerticalAlignment.Center;
            var p = row.Cells[0].AddParagraph();
            p.Format.Alignment = ParagraphAlignment.Center;
            p.Format.Font.Size = g.Pt(14);
            p.AddText(line.Label.ToUpperInvariant() + ": ");
            if (line.Strong) p.AddFormattedText(line.Value, TextFormat.Bold);
            else p.AddText(line.Value.ToUpperInvariant());
        }

        table.SetEdge(0, 0, 1, form.Info.Count, Edge.Box, BorderStyle.Single, 0.75, Colors.Black);
    }

    /// <summary>
    /// Bảng dòng hàng theo cột sheet: tiêu đề một tầng, hoặc hai tầng khi có cột nhóm (nhóm gộp ngang, cột không nhóm gộp dọc). Cột gộp dọc
    /// (<see cref="CuvtColumn.MergeRows"/>) chỉ gộp khi bảng không quá <see cref="CuvtFormLayout.MaxMergedRows"/> dòng (ô gộp của MigraDoc không
    /// tách sang trang sau).
    /// </summary>
    private static void AddLines(Section section, CuvtForm form, SheetLayoutSpec spec, Geometry g, double[] columnPx)
    {
        var table = section.AddTable();
        table.Borders.Width = 0.75;
        table.Borders.Color = Colors.Black;
        table.LeftPadding = table.RightPadding = Unit.FromMillimeter(1);
        table.TopPadding = table.BottomPadding = Unit.FromMillimeter(0.5);
        table.Format.Font.Size = g.Pt(13);
        foreach (var px in columnPx) table.AddColumn(Unit.FromMillimeter(px * g.MmPx));

        var grouped = form.Columns.Any(c => c.Group is not null);
        var top = table.AddRow();
        var bottom = grouped ? table.AddRow() : null;
        foreach (var header in bottom is null ? [top] : new[] { top, bottom })
        {
            header.HeadingFormat = true;
            header.HeightRule = RowHeightRule.AtLeast;
            header.VerticalAlignment = VerticalAlignment.Center;
            header.Format.Font.Bold = true;
            header.Format.Alignment = ParagraphAlignment.Center;
            if (spec.HeaderFilled) header.Shading.Color = HeaderFill;
        }

        top.Height = g.Pt(spec.TableHeaderPt);
        if (bottom is not null) bottom.Height = g.Pt(spec.TableHeader2Pt);
        for (var c = 0; c < form.Columns.Count; c++)
        {
            var column = form.Columns[c];
            if (bottom is null || column.Group is null)
            {
                top.Cells[c].AddParagraph(column.Title);
                if (bottom is not null) top.Cells[c].MergeDown = 1;
                continue;
            }

            bottom.Cells[c].AddParagraph(column.Title);
            if (c > 0 && form.Columns[c - 1].Group == column.Group) continue;
            var span = 1;
            while (c + span < form.Columns.Count && form.Columns[c + span].Group == column.Group) span++;
            top.Cells[c].AddParagraph(column.Group);
            top.Cells[c].MergeRight = span - 1;
        }

        var centered = spec.CenteredColumns ?? [];
        var red = spec.RedColumns ?? [];
        var mergeRows = form.Rows.Count > 1 && form.Rows.Count <= CuvtFormLayout.MaxMergedRows;
        for (var r = 0; r < form.Rows.Count; r++)
        {
            var row = table.AddRow();
            row.HeightRule = RowHeightRule.AtLeast;
            row.Height = g.Pt(spec.RowPt);
            row.VerticalAlignment = VerticalAlignment.Center;
            for (var c = 0; c < form.Columns.Count; c++)
            {
                var column = form.Columns[c];
                var cell = row.Cells[c];
                cell.Format.Alignment = column.Center || centered.Contains(c) ? ParagraphAlignment.Center
                    : column.AlignRight ? ParagraphAlignment.Right : ParagraphAlignment.Left;
                if (red.Contains(c)) cell.Format.Font.Color = Colors.Red;
                // Tên hàng 4A/4B cỡ 12 như sheet (B12 Times New Roman 12), các cột khác 13.
                if (c == 1 && !grouped) cell.Format.Font.Size = g.Pt(12);
                if (column.MergeRows && r > 0 && mergeRows) continue;
                cell.AddParagraph(form.Rows[r][c]);
                if (column.MergeRows && mergeRows) cell.MergeDown = form.Rows.Count - 1;
            }
        }

        var total = table.AddRow();
        total.HeightRule = RowHeightRule.AtLeast;
        total.Height = g.Pt(spec.TotalRowPt);
        total.VerticalAlignment = VerticalAlignment.Center;
        total.Format.Font.Bold = true;
        total.Format.Font.Size = g.Pt(15);
        total.Shading.Color = TotalFill;
        // 4A/4B: "Tổng chưa bao gồm VAT" căn phải (A14:G14); 2A/2B: "Tổng cộng" căn giữa (A15:C15).
        total.Cells[0].AddParagraph(form.TotalLabel).Format.Alignment = spec.TotalDash ? ParagraphAlignment.Right : ParagraphAlignment.Center;
        total.Cells[0].MergeRight = form.TotalSpan - 1;
        for (var i = 0; i < form.TotalCells.Count && form.TotalSpan + i < form.Columns.Count; i++)
        {
            // Ô tổng trống in "-" như định dạng kế toán của sheet 4A/4B (=SUM của cột trống).
            var text = form.TotalCells[i];
            var cell = total.Cells[form.TotalSpan + i];
            var dash = spec.TotalDash && string.IsNullOrEmpty(text);
            cell.AddParagraph(dash ? "-" : text);
            cell.Format.Alignment = dash ? ParagraphAlignment.Center : ParagraphAlignment.Right;
        }
    }

    /// <summary>Rộng cột logic (điểm ảnh sheet) – xem <see cref="SheetLayoutSpec.ColumnMap"/>.</summary>
    private static double[] LogicalPx(CuvtForm form, SheetLayoutSpec spec)
    {
        var sheet = spec.SheetColumns.Select(ColumnWidthPx).ToArray();
        if (form.Columns.Count < spec.ColumnMap.Length)
        {
            // Biểu mẫu ít cột hơn sheet (không xảy ra với mẫu hiện có): chia theo độ rộng ghi trong biểu mẫu, giữ rộng sheet.
            var sum = form.Columns.Sum(c => c.WidthMm);
            return form.Columns.Select(c => sheet.Sum() * c.WidthMm / sum).ToArray();
        }

        var widths = new List<double>();
        var col = 0;
        foreach (var span in spec.ColumnMap)
        {
            widths.Add(sheet.Skip(col).Take(span).Sum());
            col += span;
        }

        var extra = form.Columns.Count - spec.ColumnMap.Length;
        if (extra > 0)
        {
            var rest = sheet.Skip(col).ToArray();
            var each = rest.Length == 0 ? sheet.Average() : rest.Average();
            widths.AddRange(Enumerable.Repeat(each, extra));
        }

        return widths.ToArray();
    }

    /// <summary>
    /// Khối ký theo ảnh trong sheet: dòng "Kính trình…" gộp cả hàng, hàng chức danh, hàng ký (chữ ký, họ tên, giờ duyệt nằm đáy ô); ô Chủ tịch là cột
    /// cuối (2A, 3A, 4A) hoặc cả hàng dưới (5A, 5B). Bước không có ô trên sheet (chặng vượt, vai trò tự tạo) in hàng ô phát sinh trước hàng Chủ tịch /
    /// sau hàng ký. Đường kẻ màu xanh xám như ảnh.
    /// </summary>
    private static void AddSignBlock(Section section, SheetSignBlock sign, SheetSignStyle style, Geometry g)
    {
        var gap = section.AddParagraph();
        gap.Format.SpaceBefore = Unit.FromMillimeter(style.GapPx * g.MmPx);
        gap.Format.Font.Size = 1;
        var widthMm = Math.Min(g.WidthMm, style.WidthPx * g.MmPx);
        var mmPerImagePx = widthMm / style.ColumnsPx.Sum();
        // Ảnh đặt trên sheet có thể bị kéo dãn khác chiều ngang – chiều cao hàng theo cỡ hiển thị.
        var mmPerImageRowPx = style.HeightPx * g.MmPx / style.RowsPx.Sum();
        var table = section.AddTable();
        table.Borders.Width = 0.75;
        table.Borders.Color = SignLine;
        table.KeepTogether = true;
        var widths = style.ColumnsPx.Select(w => w * mmPerImagePx).ToArray();
        foreach (var w in widths) table.AddColumn(Unit.FromMillimeter(w));
        var columns = widths.Length;

        var head = table.AddRow();
        head.HeightRule = RowHeightRule.AtLeast;
        head.Height = Unit.FromMillimeter(style.RowsPx[0] * mmPerImageRowPx);
        head.VerticalAlignment = VerticalAlignment.Center;
        head.Cells[0].MergeRight = columns - 1;
        var heading = head.Cells[0].AddParagraph(sign.Heading);
        heading.Format.Alignment = ParagraphAlignment.Center;
        heading.Format.Font.Bold = true;
        heading.Format.Font.Size = g.Pt(style.HeadingPt);

        var first = style.ChairmanInRow ? sign.Boxes.Append(sign.Final).ToList() : sign.Boxes.ToList();
        foreach (var chunk in first.Chunk(columns).Concat(sign.Extra.Chunk(columns)))
        {
            var titles = table.AddRow();
            titles.HeightRule = RowHeightRule.AtLeast;
            titles.Height = Unit.FromMillimeter(style.RowsPx[1] * mmPerImageRowPx);
            titles.VerticalAlignment = VerticalAlignment.Center;
            var signs = table.AddRow();
            signs.HeightRule = RowHeightRule.AtLeast;
            signs.Height = Unit.FromMillimeter(style.RowsPx[2] * mmPerImageRowPx);
            signs.VerticalAlignment = VerticalAlignment.Bottom;
            for (var i = 0; i < chunk.Length; i++)
            {
                TitleCell(titles.Cells[i], chunk[i], style.TitlePt, g);
                SignCell(signs.Cells[i], chunk[i], widths[i], signs.Height.Millimeter, g, upper: ReferenceEquals(chunk[i], sign.Final));
            }

            // Hàng ô phát sinh ít ô hơn số cột thì ô cuối gộp phần còn lại để khung kín như ảnh.
            if (chunk.Length < columns)
            {
                titles.Cells[chunk.Length - 1].MergeRight = columns - chunk.Length;
                signs.Cells[chunk.Length - 1].MergeRight = columns - chunk.Length;
            }
        }

        // Khối ký không tách sang hai trang (như ảnh dán trên sheet).
        if (style.ChairmanInRow)
        {
            head.KeepWith = table.Rows.Count - 1;
            return;
        }

        var final = table.AddRow();
        final.HeightRule = RowHeightRule.AtLeast;
        final.Height = Unit.FromMillimeter(style.RowsPx[3] * mmPerImageRowPx);
        final.Cells[0].MergeRight = columns - 1;
        TitleCell(final.Cells[0], sign.Final, style.TitlePt, g);
        SignCell(final.Cells[0], sign.Final, widthMm, final.Height.Millimeter * 0.55, g, upper: true);
        head.KeepWith = table.Rows.Count - 1;
    }

    private static void TitleCell(Cell cell, SheetSignBox box, double titlePt, Geometry g)
    {
        var title = cell.AddParagraph(box.Title);
        title.Format.Alignment = ParagraphAlignment.Center;
        title.Format.Font.Bold = true;
        title.Format.Font.Size = g.Pt(titlePt);
        if (string.IsNullOrEmpty(box.Caption)) return;
        var caption = cell.AddParagraph(box.Caption);
        caption.Format.Alignment = ParagraphAlignment.Center;
        caption.Format.Font.Italic = true;
        caption.Format.Font.Size = g.Pt(titlePt * 0.75);
    }

    /// <summary>Ô ký: ảnh chữ ký (cao tối đa 55 % ô, giữ tỉ lệ) → họ tên đậm → dòng giờ duyệt nhỏ nghiêng, nằm đáy ô như tên "AHLĐ. …" trên ảnh mẫu.</summary>
    private static void SignCell(Cell cell, SheetSignBox box, double widthMm, double heightMm, Geometry g, bool upper)
    {
        var image = cell.AddParagraph();
        image.Format.Alignment = ParagraphAlignment.Center;
        if (box.Signature is not null) SheetPrintParts.AddImage(image, box.Signature, Math.Min(widthMm - 4, 70), heightMm * 0.55);

        var name = cell.AddParagraph(box.Name ?? "");
        name.Format.Alignment = ParagraphAlignment.Center;
        name.Format.Font.Bold = true;
        name.Format.Font.Size = g.Pt(upper ? 15 : 13);
        var detail = cell.AddParagraph(box.Detail ?? "");
        detail.Format.Alignment = ParagraphAlignment.Center;
        detail.Format.Font.Italic = true;
        detail.Format.Font.Size = 7;
        detail.Format.SpaceAfter = Unit.FromMillimeter(1);
        if (!box.InChain) detail.Format.Font.Color = Muted;
    }

    /// <summary>Vẽ sau khi dựng trang: logo và vòng tròn số biểu mẫu (MigraDoc không có hình tròn) ở trang 1 đúng vị trí neo; dấu chìm mọi trang.</summary>
    private static void DrawOverlays(PdfSharp.Pdf.PdfDocument pdf, string formNo, SheetImage? logo, string? watermark, SheetLayoutSpec spec, Geometry g)
    {
        const double mmToPt = 72 / 25.4;
        double X(SheetPicture p) => (g.LeftMm + g.PictureLeft(p, p == spec.Circle) * g.MmPx) * mmToPt;
        double Y(SheetPicture p) => (MarginMm + p.TopPx * g.MmPx) * mmToPt;
        for (var i = 0; i < pdf.PageCount; i++)
        {
            var page = pdf.Pages[i];
            using var gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);
            if (i == 0)
            {
                var c = spec.Circle;
                var w = c.WidthPx * g.MmPx * mmToPt;
                var h = c.HeightPx * g.MmPx * mmToPt;
                gfx.DrawEllipse(new XPen(XColors.Black, 1), X(c), Y(c), w, h);
                var font = new XFont(PdfFonts.SerifFamily, 25 * g.K, XFontStyleEx.Bold);
                gfx.DrawString(formNo, font, XBrushes.Black, new XRect(X(c), Y(c), w, h), XStringFormats.Center);
                if (logo is not null)
                {
                    // Logo công ty co vừa khung logo của sheet, giữ tỉ lệ, căn giữa khung.
                    var box = spec.Logo;
                    var bw = box.WidthPx * g.MmPx * mmToPt;
                    var bh = box.HeightPx * g.MmPx * mmToPt;
                    var scale = Math.Min(bw / logo.Width, bh / logo.Height);
                    using var image = XImage.FromStream(new MemoryStream(logo.Content));
                    gfx.DrawImage(image, X(box) + (bw - logo.Width * scale) / 2, Y(box) + (bh - logo.Height * scale) / 2,
                        logo.Width * scale, logo.Height * scale);
                }
            }

            if (watermark is not null) SheetPrintParts.DrawWatermark(gfx, page, watermark);
        }
    }
}
