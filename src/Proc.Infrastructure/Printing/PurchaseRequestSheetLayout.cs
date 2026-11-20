using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using PdfSharp.Drawing;

namespace Proc.Infrastructure.Printing;

/// <summary>
/// Khuôn in đề nghị mua <b>giữ nguyên sheet "1A.DN VT-CT"</b> của file biểu mẫu CUVT (lát IN1 – design.md mục 20.4; người dùng chốt 18/11/2026:
/// "form tôi cung cấp như thế nào thì giữ nguyên như vậy"). Dùng chung cho 1B và đề nghị dịch vụ (sheet 1B cùng bố cục, cùng khối ký). Đo từ sheet:
/// <list type="bullet">
/// <item>Sheet in tỉ lệ 85 %, lề 0 → mọi cỡ chữ / chiều cao dòng trong Excel nhân <see cref="K"/> = 0,85; vùng in rộng 198 mm (lề 6 mm) như bản in
/// thật (ví dụ chữ bảng 13 pt × 0,85 = 11,05 pt; dòng tiêu đề 24,9 pt × 0,85 = 21,2 pt).</item>
/// <item>Khung đầu phiếu (dòng 1–5) và khung thông tin (dòng 6–9) kẻ viền ngoài; logo trái, số biểu mẫu trong vòng tròn phải; tiêu đề 18 đậm
/// (chữ "CÁC" đỏ), phụ đề 12 đậm xanh #0000CC, ngày 16 đậm xanh; dòng thông tin 14, IN HOA, căn giữa, số tiền đậm.</item>
/// <item>Bảng: độ rộng cột theo tỉ lệ cột A–L của sheet; chữ căn giữa (tên vật tư căn trái); dòng "Tổng cộng" 15 đậm, nền xanh nhạt, chữ gộp 3 cột đầu.</item>
/// <item>Khối ký vẽ lại theo ảnh trong sheet: tỉ lệ cột 25/23/22/30 %, dòng "Kính trình…", 8 ô + ô Chủ tịch cả hàng.</item>
/// <item>Ngoài mẫu (yêu cầu 18/11/2026): góc dưới trái trang cuối chữ 9 – giờ in, người in, lần in, số phiếu, tình trạng phê duyệt; dấu chìm khi
/// phiếu chưa duyệt.</item>
/// </list>
/// </summary>
public static class PurchaseRequestSheetLayout
{
    /// <summary>Tỉ lệ in của sheet (85 %).</summary>
    public const double K = 0.85;

    /// <summary>Lề trái/phải/trên (mm).</summary>
    public const double MarginMm = 6;

    /// <summary>Độ rộng vùng in A4 dọc (210 − 2 × 6 mm).</summary>
    public const double WidthMm = 198;

    /// <summary>Độ rộng cột logo / cột số biểu mẫu của khung đầu phiếu (mm) – hai bên bằng nhau để tiêu đề căn giữa trang như sheet.</summary>
    private const double SideMm = 32;

    /// <summary>Cao các dòng khung đầu phiếu (pt Excel): 24,9 rồi 4 dòng 20,1.</summary>
    private static readonly double[] HeaderRowsPt = [24.9, 20.1, 20.1, 20.1, 20.1];

    /// <summary>Độ rộng cột A–L của sheet (đơn vị ký tự) gộp theo cột logic: TT, Tên (B:C), Mô tả (D:E), ĐVT, 3 cột số lượng, Đơn giá, Thành tiền, Ghi chú.</summary>
    private static readonly double[] SheetColumns = [5.6, 15.5, 16.2, 8.43, 8.6, 7.6, 9.1, 12.3, 15.4, 11.7];

    /// <summary>Tỉ lệ 4 cột khối ký đo từ ảnh trong sheet (208 / 192 / 185 / 246 điểm ảnh trên 831).</summary>
    private static readonly double[] SignColumns = [208, 192, 185, 246];

    private static readonly Color Blue = new(0x00, 0x00, 0xCC);
    private static readonly Color Red = new(0xFF, 0x00, 0x00);
    private static readonly Color TotalFill = new(0xDC, 0xE6, 0xF1);
    private static readonly Color Muted = new(0x70, 0x70, 0x70);

    private static Unit Pt(double excelPt) => Unit.FromPoint(excelPt * K);

    private static Unit Mm(double mm) => Unit.FromMillimeter(mm);

    /// <summary>Dựng PDF.</summary>
    /// <param name="form">Nội dung phiếu (tiêu đề, phụ đề, thông tin, cột, dòng, tổng, dòng dưới bảng) từ <see cref="CuvtPrintForms.PurchaseRequest"/>.</param>
    /// <param name="logo">Logo công ty của phiếu; null = để trống.</param>
    /// <param name="sign">Khối ký.</param>
    /// <param name="stamp">Thông tin hệ thống (khung vẽ sau ở trang cuối – SheetPrintParts.StampLastPage), dấu chìm.</param>
    /// <returns>Nội dung PDF.</returns>
    public static byte[] Render(CuvtForm form, SheetImage? logo, SheetSignBlock sign, SheetStamp stamp)
    {
        var document = new Document();
        document.Info.Title = $"{form.Title} {form.No}";
        var normal = document.Styles[StyleNames.Normal]!;
        normal.Font.Name = PdfFonts.SerifFamily;
        normal.Font.Size = Pt(13);

        var section = document.AddSection();
        var setup = section.PageSetup;
        setup.PageFormat = PageFormat.A4;
        setup.Orientation = Orientation.Portrait;
        setup.LeftMargin = setup.RightMargin = setup.TopMargin = Mm(MarginMm);
        setup.BottomMargin = Mm(26);
        setup.FooterDistance = Mm(4);

        AddHeader(section, form);
        AddInfo(section, form);
        AddLines(section, form);
        AddFootnote(section, form);
        AddSignBlock(section, sign);

        var renderer = new PdfDocumentRenderer { Document = document };
        renderer.RenderDocument();
        DrawOverlays(renderer.PdfDocument, form.FormNo, logo, stamp.Watermark);
        using var stream = new MemoryStream();
        renderer.PdfDocument.Save(stream, false);
        return stream.ToArray();
    }

    /// <summary>
    /// Khung đầu phiếu như sheet: tiêu đề, 3 phụ đề, ngày căn giữa <b>cả chiều ngang trang</b> (ô gộp A:L); logo và vòng tròn số biểu mẫu là hình nằm
    /// đè ở hai góc nên vẽ sau (<see cref="DrawOverlays"/>). Dòng nào dài hơn phần giữa hai hình thì thu nhỏ cỡ chữ cho vừa một dòng (như Excel
    /// "vừa ô") – ví dụ phụ đề 1B "NHÓM VẬT TƯ TIÊU HAO… HÀNG THÁNG." 10,2 pt.
    /// </summary>
    private static void AddHeader(Section section, CuvtForm form)
    {
        var table = section.AddTable();
        table.Borders.Visible = false;
        table.AddColumn(Mm(WidthMm));
        var lines = new List<(string Text, double Size, Color Color)> { (form.Title, 18, Colors.Black) };
        lines.AddRange(form.Subtitles.Take(3).Select(s => (s, 12.0, Blue)));
        lines.Add(($"Ngày {form.Date.Day} tháng {form.Date.Month} năm {form.Date.Year}", 16, Blue));
        var measure = XGraphics.CreateMeasureContext(new XSize(2000, 2000), XGraphicsUnit.Point, XPageDirection.Downwards);
        var room = (WidthMm - 2 * SideMm) * 72 / 25.4;
        for (var r = 0; r < HeaderRowsPt.Length; r++)
        {
            var row = table.AddRow();
            row.HeightRule = RowHeightRule.Exactly;
            row.Height = Pt(HeaderRowsPt[r]);
            row.VerticalAlignment = VerticalAlignment.Center;
            if (r >= lines.Count) continue;
            var (text, size, color) = lines[r];
            var points = size * K;
            var width = measure.MeasureString(text, new XFont(PdfFonts.SerifFamily, points, XFontStyleEx.Bold)).Width;
            if (width > room) points *= room / width;
            var p = row.Cells[0].AddParagraph();
            p.Format.Alignment = ParagraphAlignment.Center;
            p.Format.Font.Size = Unit.FromPoint(points);
            p.Format.Font.Bold = true;
            p.Format.Font.Color = color;
            // Sheet 1A tô đỏ chữ "CÁC" trong tiêu đề – giữ nguyên.
            var red = r == 0 ? text.IndexOf(" CÁC ", StringComparison.Ordinal) : -1;
            if (red < 0) p.AddText(text);
            else
            {
                p.AddText(text[..(red + 1)]);
                p.AddFormattedText("CÁC").Color = Red;
                p.AddText(text[(red + 4)..]);
            }
        }

        table.SetEdge(0, 0, 1, HeaderRowsPt.Length, Edge.Box, BorderStyle.Single, 0.75, Colors.Black);
    }

    /// <summary>Khung thông tin: mỗi dòng cao 28,5 pt, IN HOA, căn giữa; giá trị tiền in đậm (ghi chú sheet "Số tiền phải IN ĐẬM").</summary>
    private static void AddInfo(Section section, CuvtForm form)
    {
        var table = section.AddTable();
        table.Borders.Visible = false;
        table.AddColumn(Mm(WidthMm));
        foreach (var line in form.Info)
        {
            var row = table.AddRow();
            row.HeightRule = RowHeightRule.AtLeast;
            row.Height = Pt(28.5);
            row.VerticalAlignment = VerticalAlignment.Center;
            var p = row.Cells[0].AddParagraph();
            p.Format.Alignment = ParagraphAlignment.Center;
            p.Format.Font.Size = Pt(14);
            p.AddText(line.Label.ToUpperInvariant() + ": ");
            // Nội dung IN HOA (chú thích sheet "NỘI DUNG VIẾT IN HOA & CĂN GIỮA"), riêng số tiền giữ đơn vị "đ" như sheet ("64.000.000 đ").
            if (line.Strong) p.AddFormattedText(line.Value, TextFormat.Bold);
            else p.AddText(line.Value.ToUpperInvariant());
        }

        if (form.Info.Count > 0) table.SetEdge(0, 0, 1, form.Info.Count, Edge.Box, BorderStyle.Single, 0.75, Colors.Black);
    }

    /// <summary>Bảng dòng hàng: tiêu đề hai tầng (nhóm "Số lượng"), lặp lại khi sang trang; dòng "Tổng cộng" nền xanh nhạt.</summary>
    private static void AddLines(Section section, CuvtForm form)
    {
        var table = section.AddTable();
        table.Borders.Width = 0.75;
        table.Borders.Color = Colors.Black;
        table.LeftPadding = table.RightPadding = Mm(1);
        table.TopPadding = table.BottomPadding = Mm(0.5);
        table.Format.Font.Size = Pt(13);
        var weights = form.Columns.Count == SheetColumns.Length ? SheetColumns : form.Columns.Select(c => c.WidthMm).ToArray();
        var sum = weights.Sum();
        foreach (var w in weights) table.AddColumn(Mm(WidthMm * w / sum));

        var grouped = form.Columns.Any(c => c.Group is not null);
        var top = table.AddRow();
        var bottom = grouped ? table.AddRow() : null;
        top.HeadingFormat = true;
        top.HeightRule = RowHeightRule.AtLeast;
        top.Height = Pt(22.5);
        if (bottom is not null)
        {
            bottom.HeadingFormat = true;
            bottom.HeightRule = RowHeightRule.AtLeast;
            bottom.Height = Pt(33.6);
        }

        foreach (var header in bottom is null ? [top] : new[] { top, bottom })
        {
            header.Format.Font.Bold = true;
            header.Format.Alignment = ParagraphAlignment.Center;
            header.VerticalAlignment = VerticalAlignment.Center;
        }

        for (var c = 0; c < form.Columns.Count; c++)
        {
            var column = form.Columns[c];
            if (bottom is null || column.Group is null)
            {
                HeaderText(top.Cells[c], column.Title);
                if (bottom is not null) top.Cells[c].MergeDown = 1;
                continue;
            }

            HeaderText(bottom.Cells[c], column.Title);
            if (c > 0 && form.Columns[c - 1].Group == column.Group) continue;
            var span = 1;
            while (c + span < form.Columns.Count && form.Columns[c + span].Group == column.Group) span++;
            top.Cells[c].AddParagraph(column.Group);
            top.Cells[c].MergeRight = span - 1;
        }

        foreach (var cells in form.Rows)
        {
            var row = table.AddRow();
            row.HeightRule = RowHeightRule.AtLeast;
            row.Height = Pt(33);
            row.VerticalAlignment = VerticalAlignment.Center;
            for (var c = 0; c < form.Columns.Count; c++)
            {
                // Sheet căn giữa mọi ô dòng hàng, riêng tên vật tư căn trái.
                row.Cells[c].Format.Alignment = c == 1 ? ParagraphAlignment.Left : ParagraphAlignment.Center;
                row.Cells[c].AddParagraph(cells[c]);
            }
        }

        // Dòng tổng như sheet: "Tổng cộng" gộp TT – Tên – Mô tả (A14:E14), Thành tiền ở cột áp chót, các ô khác để trống.
        var total = table.AddRow();
        total.HeightRule = RowHeightRule.AtLeast;
        total.Height = Pt(20.25);
        total.VerticalAlignment = VerticalAlignment.Center;
        total.Format.Font.Bold = true;
        total.Format.Font.Size = Pt(15);
        total.Format.Alignment = ParagraphAlignment.Center;
        total.Shading.Color = TotalFill;
        total.Cells[0].AddParagraph(form.TotalLabel);
        total.Cells[0].MergeRight = 2;
        total.Cells[form.Columns.Count - 2].AddParagraph(form.TotalCells.FirstOrDefault() ?? "");
    }

    /// <summary>Tiêu đề cột: "Đơn giá (tham khảo)" → "Đơn Giá" đậm, "(tham khảo)" nghiêng thường như sheet; "Thành tiền" xuống dòng.</summary>
    private static void HeaderText(Cell cell, string title)
    {
        var open = title.IndexOf(" (", StringComparison.Ordinal);
        if (open < 0)
        {
            cell.AddParagraph(title);
            return;
        }

        var head = title[..open];
        cell.AddParagraph(head.StartsWith("Đơn giá", StringComparison.Ordinal) ? "Đơn Giá" + head[7..] : head);
        var note = cell.AddParagraph(title[(open + 1)..]);
        note.Format.Font.Bold = false;
        note.Format.Font.Italic = head.StartsWith("Đơn giá", StringComparison.Ordinal);
    }

    /// <summary>Dòng dưới bảng: "Thời gian cần sử dụng: ngày (đỏ)" bên trái, "Địa điểm tập kết/giao hàng: NƠI NHẬN" từ cột G.</summary>
    private static void AddFootnote(Section section, CuvtForm form)
    {
        var table = section.AddTable();
        table.Borders.Visible = false;
        table.Format.Font.Size = Pt(13);
        var left = WidthMm * SheetColumns.Take(4).Sum() / SheetColumns.Sum();
        table.AddColumn(Mm(left));
        table.AddColumn(Mm(WidthMm - left));
        var row = table.AddRow();
        row.HeightRule = RowHeightRule.AtLeast;
        row.Height = Pt(24.75);
        row.VerticalAlignment = VerticalAlignment.Center;
        var (label, value) = Split(form.Footnote.Left ?? "");
        var p = row.Cells[0].AddParagraph(label);
        p.AddFormattedText(value).Color = Red;
        var (placeLabel, place) = Split(form.Footnote.Right ?? "");
        row.Cells[1].AddParagraph(placeLabel + place.ToUpperInvariant());
    }

    private static (string Label, string Value) Split(string text)
    {
        var colon = text.IndexOf(": ", StringComparison.Ordinal);
        return colon < 0 ? (text, "") : (text[..(colon + 2)], text[(colon + 2)..]);
    }

    /// <summary>
    /// Khối ký theo ảnh trong sheet: dòng "Kính trình…" (gộp 4 cột), hàng chức danh + hàng ký × 2, (hàng ô phát sinh), ô Chủ tịch gộp 4 cột.
    /// Chiều cao đo từ ảnh 831 × 511 điểm ảnh co về 198 mm (0,238 mm/điểm ảnh): đầu 9,5 mm, chức danh 9,5 mm, ô ký 26 mm, ô Chủ tịch 39 mm.
    /// </summary>
    private static void AddSignBlock(Section section, SheetSignBlock sign)
    {
        section.AddParagraph().Format.SpaceBefore = Mm(1);
        var table = section.AddTable();
        table.Borders.Width = 0.75;
        table.Borders.Color = Colors.Black;
        table.KeepTogether = true;
        var total = SignColumns.Sum();
        var widths = SignColumns.Select(w => WidthMm * w / total).ToArray();
        foreach (var w in widths) table.AddColumn(Mm(w));

        var head = table.AddRow();
        head.HeightRule = RowHeightRule.AtLeast;
        head.Height = Mm(9.5);
        head.VerticalAlignment = VerticalAlignment.Center;
        head.Cells[0].MergeRight = 3;
        var heading = head.Cells[0].AddParagraph(sign.Heading);
        heading.Format.Alignment = ParagraphAlignment.Center;
        heading.Format.Font.Bold = true;
        heading.Format.Font.Italic = true;
        heading.Format.Font.Size = 11.5;

        foreach (var chunk in sign.Boxes.Chunk(4).Concat(sign.Extra.Chunk(4)))
        {
            var titles = table.AddRow();
            titles.HeightRule = RowHeightRule.AtLeast;
            titles.Height = Mm(9.5);
            titles.VerticalAlignment = VerticalAlignment.Center;
            titles.Borders.Bottom.Visible = true;
            var signs = table.AddRow();
            signs.HeightRule = RowHeightRule.AtLeast;
            signs.Height = Mm(26);
            signs.VerticalAlignment = VerticalAlignment.Bottom;
            for (var i = 0; i < chunk.Length; i++)
            {
                TitleCell(titles.Cells[i], chunk[i]);
                SignCell(signs.Cells[i], chunk[i], widths[i]);
            }

            // Hàng ô phát sinh ít hơn 4 ô thì ô cuối gộp phần còn lại để khung kín như mẫu.
            if (chunk.Length < 4)
            {
                titles.Cells[chunk.Length - 1].MergeRight = 4 - chunk.Length;
                signs.Cells[chunk.Length - 1].MergeRight = 4 - chunk.Length;
            }
        }

        var final = table.AddRow();
        final.HeightRule = RowHeightRule.AtLeast;
        final.Height = Mm(39);
        final.Cells[0].MergeRight = 3;
        TitleCell(final.Cells[0], sign.Final, chairman: true);
        SignCell(final.Cells[0], sign.Final, WidthMm, chairman: true);
    }

    private static void TitleCell(Cell cell, SheetSignBox box, bool chairman = false)
    {
        var title = cell.AddParagraph(box.Title);
        title.Format.Alignment = ParagraphAlignment.Center;
        title.Format.Font.Bold = true;
        title.Format.Font.Size = chairman ? 12 : 11;
        if (chairman) title.Format.SpaceBefore = Mm(2);
        var caption = cell.AddParagraph(box.Caption);
        caption.Format.Alignment = ParagraphAlignment.Center;
        caption.Format.Font.Italic = true;
        caption.Format.Font.Size = 8.5;
    }

    /// <summary>Ô ký: ảnh chữ ký (cao tối đa 15 mm, ô Chủ tịch 20 mm, giữ tỉ lệ) → họ tên đậm → dòng giờ duyệt nhỏ nghiêng.</summary>
    private static void SignCell(Cell cell, SheetSignBox box, double widthMm, bool chairman = false)
    {
        var image = cell.AddParagraph();
        image.Format.Alignment = ParagraphAlignment.Center;
        image.Format.SpaceBefore = chairman ? Mm(1) : Mm(0.5);
        var maxHeight = chairman ? 20.0 : 15.0;
        if (box.Signature is not null) SheetPrintParts.AddImage(image, box.Signature, Math.Min(widthMm - 4, chairman ? 80 : widthMm), maxHeight);
        else image.Format.SpaceAfter = Mm(maxHeight);

        var name = cell.AddParagraph(box.Name ?? "");
        name.Format.Alignment = ParagraphAlignment.Center;
        name.Format.Font.Bold = true;
        name.Format.Font.Size = chairman ? 11.5 : 10;
        var detail = cell.AddParagraph(box.Detail ?? "");
        detail.Format.Alignment = ParagraphAlignment.Center;
        detail.Format.Font.Italic = true;
        detail.Format.Font.Size = 7;
        detail.Format.SpaceAfter = Mm(1);
        if (!box.InChain) detail.Format.Font.Color = Muted;
    }

    /// <summary>
    /// Logo góc trên trái khung đầu phiếu như sheet: khung tối đa 26 × 28 mm, cách viền 2 mm, giữ tỉ lệ, căn giữa theo chiều cao khung đầu phiếu.
    /// </summary>
    private static void DrawLogo(XGraphics gfx, SheetImage logo)
    {
        const double mmToPt = 72 / 25.4;
        var headerMm = HeaderRowsPt.Sum() * K / mmToPt;
        var scale = Math.Min(26.0 / logo.Width, (headerMm - 4) / logo.Height);
        var (w, h) = (logo.Width * scale, logo.Height * scale);
        using var image = XImage.FromStream(new MemoryStream(logo.Content));
        gfx.DrawImage(image, (MarginMm + 2 + (26 - w) / 2) * mmToPt, (MarginMm + (headerMm - h) / 2) * mmToPt, w * mmToPt, h * mmToPt);
    }

    /// <summary>
    /// Vẽ sau khi dựng trang: vòng tròn số biểu mẫu (sheet dùng hình tròn, MigraDoc không có hình tròn) ở trang 1, dấu chìm mờ nghiêng mọi trang.
    /// Vị trí tròn tính từ khung đầu phiếu (cao cố định): tâm cách mép phải giấy 6 + 16 mm, cách mép trên 6 + 11 mm, đường kính 20 mm như sheet.
    /// </summary>
    private static void DrawOverlays(PdfSharp.Pdf.PdfDocument pdf, string formNo, SheetImage? logo, string? watermark)
    {
        const double mmToPt = 72 / 25.4;
        for (var i = 0; i < pdf.PageCount; i++)
        {
            var page = pdf.Pages[i];
            using var gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);
            if (i == 0)
            {
                var cx = (MarginMm + WidthMm - SideMm / 2) * mmToPt;
                var cy = (MarginMm + 11) * mmToPt;
                var r = 10 * mmToPt;
                gfx.DrawEllipse(new XPen(XColors.Black, 1), cx - r, cy - r, 2 * r, 2 * r);
                var font = new XFont(PdfFonts.SerifFamily, 25 * K, XFontStyleEx.Bold);
                gfx.DrawString(formNo, font, XBrushes.Black, new XRect(cx - r, cy - r, 2 * r, 2 * r), XStringFormats.Center);
                if (logo is not null) DrawLogo(gfx, logo);
            }

            if (watermark is not null) SheetPrintParts.DrawWatermark(gfx, page, watermark);
        }
    }
}
