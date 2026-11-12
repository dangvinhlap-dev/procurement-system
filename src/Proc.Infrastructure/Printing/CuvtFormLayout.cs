using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;

namespace Proc.Infrastructure.Printing;

/// <summary>Một dòng thông tin đầu phiếu của biểu mẫu CUVT, in "NHÃN: giá trị".</summary>
/// <param name="Label">Nhãn (in hoa khi in).</param>
/// <param name="Value">Giá trị đã định dạng; không bao giờ để "…" (biểu mẫu cấm dấu chấm lửng).</param>
/// <param name="Strong">In đậm giá trị – số tiền phải in đậm theo biểu mẫu.</param>
public sealed record CuvtInfoLine(string Label, string Value, bool Strong = false);

/// <summary>Một cột bảng của biểu mẫu CUVT.</summary>
/// <param name="Title">Tiêu đề cột.</param>
/// <param name="WidthMm">Độ rộng (mm); tổng = 186 mm (A4 dọc) hoặc 273 mm (A4 ngang).</param>
/// <param name="AlignRight">Cột số – căn phải.</param>
/// <param name="Group">Tiêu đề nhóm ở tầng trên (ví dụ "Số lượng" gồm Cần sử dụng – Tồn kho – Cần mua); null = cột gộp hai tầng.</param>
/// <param name="Center">Căn giữa (cột STT, ngày, dấu "x" đơn vị mua).</param>
/// <param name="MergeRows">
/// Gộp dọc mọi dòng của bảng thành một ô, lấy giá trị dòng đầu – cột "Tên Nhà cung cấp đã mua", "Lũy kế", "Còn lại" của mẫu 4A/4B (lát Y7).
/// </param>
public sealed record CuvtColumn(string Title, double WidthMm, bool AlignRight = false, string? Group = null, bool Center = false, bool MergeRows = false);

/// <summary>Một ô ký ở cuối biểu mẫu – sinh từ chuỗi duyệt thực tế (design.md mục 19.1 Q13).</summary>
/// <param name="Title">Chức danh / bước duyệt, ví dụ "Người đề nghị", "Thủ kho KT", "Trưởng phòng ban PX1".</param>
/// <param name="Name">Họ tên người đã ký/duyệt; null = chưa duyệt (để trống chờ ký tay).</param>
/// <param name="Detail">Dòng phụ: "Đã duyệt 10:30 12/11/2026", "Chưa duyệt"…</param>
/// <param name="Position">Chức vụ người duyệt (nếu có).</param>
public sealed record CuvtSigner(string Title, string? Name, string? Detail, string? Position = null);

/// <summary>
/// Nội dung một phiếu in theo biểu mẫu CUVT (1A, 1B, 2A, 2B…) – đổ vào khuôn <see cref="CuvtFormLayout"/>.
/// </summary>
/// <param name="FormNo">Số biểu mẫu in góc trên phải, ví dụ "1A".</param>
/// <param name="Title">Tên biểu mẫu in hoa.</param>
/// <param name="Subtitles">Các dòng phụ đề (nhóm vật tư áp dụng).</param>
/// <param name="CompanyName">Tên công ty đầu phiếu.</param>
/// <param name="No">Số phiếu trên hệ thống.</param>
/// <param name="Date">Ngày phiếu.</param>
/// <param name="Landscape">Khổ ngang (mẫu 2, 3, 4).</param>
/// <param name="StatusNote">Dòng đỏ trạng thái chưa có hiệu lực; null khi phiếu đã duyệt.</param>
/// <param name="Info">Các dòng thông tin đầu phiếu.</param>
/// <param name="Columns">Cột bảng.</param>
/// <param name="Rows">Các dòng, mỗi dòng đủ số ô bằng số cột.</param>
/// <param name="TotalSpan">Số cột đầu gộp cho chữ "Tổng cộng".</param>
/// <param name="TotalCells">Giá trị các cột còn lại của dòng tổng (đủ Columns.Count − TotalSpan ô).</param>
/// <param name="Footnote">Dòng dưới bảng: trái, phải (ví dụ thời gian cần sử dụng – địa điểm giao hàng).</param>
/// <param name="Note">Ghi chú phiếu.</param>
/// <param name="Signers">Ô ký theo thứ tự chuỗi duyệt.</param>
/// <param name="ExtraRows">Dòng tổng phụ sau dòng tổng (lát Y6); null = không có.</param>
/// <param name="TotalLabel">Chữ của dòng tổng ("Tổng cộng"; mẫu 3a/3B là "Cộng").</param>
/// <param name="AfterTable">Dòng chữ ngay dưới bảng (bằng chữ, hình thức thanh toán, đề xuất chọn…); null = không có.</param>
public sealed record CuvtForm(
    string FormNo, string Title, IReadOnlyList<string> Subtitles, string CompanyName, string No, DateOnly Date, bool Landscape, string? StatusNote,
    IReadOnlyList<CuvtInfoLine> Info, IReadOnlyList<CuvtColumn> Columns, IReadOnlyList<string[]> Rows, int TotalSpan,
    IReadOnlyList<string> TotalCells, (string? Left, string? Right) Footnote, string? Note, IReadOnlyList<CuvtSigner> Signers,
    IReadOnlyList<CuvtExtraRow>? ExtraRows = null, string TotalLabel = "Tổng cộng", IReadOnlyList<CuvtTextLine>? AfterTable = null);

/// <summary>Một ô của dòng tổng phụ dưới bảng (lát Y6).</summary>
/// <param name="Text">Nội dung.</param>
/// <param name="Span">Số cột gộp.</param>
/// <param name="AlignRight">Căn phải (số tiền); không thì căn giữa.</param>
public sealed record CuvtCell(string Text, int Span = 1, bool AlignRight = true);

/// <summary>
/// Dòng tổng phụ sau dòng tổng của bảng, ví dụ "Thuế VAT (10%)", "Tổng tiền bao gồm thuế", "Hình thức thanh toán" của mẫu 3a/3A/3B.
/// </summary>
/// <param name="Label">Nhãn ở các cột đầu.</param>
/// <param name="LabelSpan">Số cột đầu gộp cho nhãn.</param>
/// <param name="Cells">Các ô còn lại, tổng Span = số cột − LabelSpan.</param>
/// <param name="FontSize">Cỡ chữ theo biểu mẫu (13 thường, 15 dòng "Tổng cộng").</param>
/// <param name="Bold">In đậm.</param>
public sealed record CuvtExtraRow(string Label, int LabelSpan, IReadOnlyList<CuvtCell> Cells, double FontSize = 13, bool Bold = false);

/// <summary>Dòng chữ dưới bảng "Nhãn: nội dung" căn trái, ví dụ "Bằng chữ", "Hình thức thanh toán", "Đề xuất chọn".</summary>
/// <param name="Label">Nhãn (in đậm).</param>
/// <param name="Value">Nội dung.</param>
/// <param name="Strong">In đậm nội dung.</param>
public sealed record CuvtTextLine(string Label, string Value, bool Strong = false);

/// <summary>
/// Khuôn in theo biểu mẫu CUVT của công ty (lát Y10, design.md mục 10.10 và 19.1 Q13). Quy tắc lấy từ ghi chú trong file Excel biểu mẫu:
/// <list type="bullet">
/// <item>Phông Times New Roman (<see cref="PdfFonts.SerifFamily"/>); số biểu mẫu ở góc trên phải cỡ 25.</item>
/// <item>Cỡ chữ: tiêu đề 18, phụ đề 12, ngày 16, dòng thông tin 14, bảng 13, dòng tổng 15; giãn dòng 20 pt. Mọi cỡ nhân
/// <see cref="Scale"/> = 0,8 – như Excel in "vừa trang" – để bảng 10 cột đủ chỗ trên A4 dọc mà vẫn giữ tỉ lệ (ví dụ nội dung 13 × 0,8 = 10,4 pt).</item>
/// <item>Khối tiêu đề canh giữa, dòng trên dài, ngắn dần xuống ("chữ V"); số tiền in đậm, đơn vị "đ"; không dùng "…".</item>
/// <item>Mẫu 2 khổ ngang. Khối ký sinh từ chuỗi duyệt: chức danh, họ tên, thời điểm duyệt; vẫn chừa chỗ ký tay.</item>
/// </list>
/// </summary>
public static class CuvtFormLayout
{
    /// <summary>Số dòng tối đa của bảng còn gộp dọc các cột <see cref="CuvtColumn.MergeRows"/>.</summary>
    public const int MaxMergedRows = 15;

    /// <summary>Hệ số cỡ chữ so với cỡ ghi trong biểu mẫu Excel (in vừa trang).</summary>
    public const double Scale = 0.8;

    private const double MarginMm = 12;

    /// <summary>Độ rộng vùng in A4 dọc (210 − 2 × 12 mm).</summary>
    public const double PortraitWidthMm = 186;

    /// <summary>Độ rộng vùng in A4 ngang (297 − 2 × 12 mm).</summary>
    public const double LandscapeWidthMm = 273;

    private static Unit Pt(double size) => Unit.FromPoint(size * Scale);

    /// <summary>Dựng file PDF theo biểu mẫu.</summary>
    /// <param name="form">Nội dung phiếu.</param>
    /// <returns>Nội dung file PDF; tiêu đề tài liệu là "tên biểu mẫu + số phiếu".</returns>
    public static byte[] Render(CuvtForm form) => RenderMany([form]);

    /// <summary>
    /// Dựng nhiều biểu mẫu vào một file PDF, mỗi biểu mẫu bắt đầu trang mới – ví dụ bảng trình giá 3a/3B của từng NCC trúng thầu (lát Y6).
    /// </summary>
    /// <param name="forms">Các biểu mẫu (ít nhất một).</param>
    /// <returns>Nội dung file PDF; tiêu đề tài liệu theo biểu mẫu đầu.</returns>
    /// <exception cref="ArgumentException">Khi danh sách rỗng.</exception>
    public static byte[] RenderMany(IReadOnlyList<CuvtForm> forms)
    {
        if (forms.Count == 0) throw new ArgumentException("Cần ít nhất một biểu mẫu.", nameof(forms));
        var document = new Document();
        document.Info.Title = $"{forms[0].Title} {forms[0].No}";
        var normal = document.Styles[StyleNames.Normal]!;
        normal.Font.Name = PdfFonts.SerifFamily;
        normal.Font.Size = Pt(13);
        foreach (var form in forms) AddForm(document, form);

        var renderer = new PdfDocumentRenderer { Document = document };
        renderer.RenderDocument();
        using var stream = new MemoryStream();
        renderer.PdfDocument.Save(stream, false);
        return stream.ToArray();
    }

    /// <summary>Một biểu mẫu là một section riêng (trang mới, hướng giấy riêng).</summary>
    private static void AddForm(Document document, CuvtForm form)
    {
        var section = document.AddSection();
        var setup = section.PageSetup;
        setup.PageFormat = PageFormat.A4;
        setup.Orientation = form.Landscape ? Orientation.Landscape : Orientation.Portrait;
        setup.LeftMargin = setup.RightMargin = Unit.FromMillimeter(MarginMm);
        setup.TopMargin = Unit.FromMillimeter(10);
        setup.BottomMargin = Unit.FromMillimeter(14);
        var width = form.Landscape ? LandscapeWidthMm : PortraitWidthMm;

        var footer = section.Footers.Primary.AddParagraph();
        footer.Format.Alignment = ParagraphAlignment.Right;
        footer.Format.Font.Size = 7.5;
        footer.AddText($"Biểu mẫu {form.FormNo} – {form.No} – Trang ");
        footer.AddPageField();
        footer.AddText("/");
        footer.AddNumPagesField();

        AddTop(section, form, width);
        AddTitle(section, form);
        AddLines(section, form);
        foreach (var line in form.AfterTable ?? [])
        {
            var p = section.AddParagraph();
            p.Format.SpaceBefore = Unit.FromMillimeter(1);
            p.AddFormattedText(line.Label + ": ", TextFormat.Bold);
            if (line.Strong) p.AddFormattedText(line.Value, TextFormat.Bold);
            else p.AddText(line.Value);
        }

        if (form.Footnote.Left is not null || form.Footnote.Right is not null)
        {
            var table = section.AddTable();
            table.Format.SpaceBefore = Unit.FromMillimeter(2);
            table.AddColumn(Unit.FromMillimeter(width / 2));
            table.AddColumn(Unit.FromMillimeter(width / 2));
            var row = table.AddRow();
            row.Cells[0].AddParagraph(form.Footnote.Left ?? "");
            row.Cells[1].AddParagraph(form.Footnote.Right ?? "");
        }

        if (!string.IsNullOrWhiteSpace(form.Note))
        {
            var note = section.AddParagraph();
            note.Format.SpaceBefore = Unit.FromMillimeter(1);
            note.AddFormattedText("Ghi chú: ", TextFormat.Bold);
            note.AddText(form.Note);
        }

        AddSigners(section, form, width);
    }

    /// <summary>Dòng đầu: tên công ty + số phiếu bên trái; số biểu mẫu cỡ 25 bên phải.</summary>
    private static void AddTop(Section section, CuvtForm form, double width)
    {
        var table = section.AddTable();
        table.AddColumn(Unit.FromMillimeter(width - 40));
        table.AddColumn(Unit.FromMillimeter(40));
        var row = table.AddRow();
        row.VerticalAlignment = VerticalAlignment.Center;
        var company = row.Cells[0].AddParagraph(form.CompanyName.ToUpperInvariant());
        company.Format.Font.Bold = true;
        company.Format.Font.Size = Pt(12);
        row.Cells[0].AddParagraph($"Số phiếu: {form.No}").Format.Font.Size = Pt(12);
        var formNo = row.Cells[1].AddParagraph(form.FormNo);
        formNo.Format.Alignment = ParagraphAlignment.Right;
        formNo.Format.Font.Size = Pt(25);
        formNo.Format.Font.Bold = true;
    }

    /// <summary>Khối tiêu đề canh giữa "chữ V": tên biểu mẫu, phụ đề, ngày, trạng thái, các dòng thông tin.</summary>
    private static void AddTitle(Section section, CuvtForm form)
    {
        Paragraph Centered(string text, double size, bool bold = false, bool italic = false)
        {
            var p = section.AddParagraph(text);
            p.Format.Alignment = ParagraphAlignment.Center;
            p.Format.Font.Size = Pt(size);
            p.Format.Font.Bold = bold;
            p.Format.Font.Italic = italic;
            // Giãn dòng 20 pt theo biểu mẫu (nhân hệ số in vừa trang).
            p.Format.LineSpacingRule = LineSpacingRule.AtLeast;
            p.Format.LineSpacing = Pt(20);
            return p;
        }

        Centered(form.Title, 18, bold: true).Format.SpaceBefore = Unit.FromMillimeter(2);
        foreach (var subtitle in form.Subtitles) Centered(subtitle, 12, bold: true);
        Centered($"Ngày {form.Date.Day} tháng {form.Date.Month} năm {form.Date.Year}", 16, italic: true);
        if (form.StatusNote is not null)
        {
            var status = Centered(form.StatusNote, 13, bold: true);
            status.Format.Font.Color = Colors.DarkRed;
        }

        foreach (var line in form.Info)
        {
            var p = Centered("", 14);
            p.AddFormattedText(line.Label.ToUpperInvariant() + ": ", TextFormat.Bold);
            if (line.Strong) p.AddFormattedText(line.Value, TextFormat.Bold);
            else p.AddText(line.Value);
        }
    }

    /// <summary>
    /// Bảng dòng: tiêu đề hai tầng khi có cột nhóm (nhóm gộp ngang, cột không nhóm gộp dọc), lặp lại khi sang trang; dòng "Tổng cộng" cỡ 15.
    /// </summary>
    private static void AddLines(Section section, CuvtForm form)
    {
        section.AddParagraph().Format.SpaceBefore = Unit.FromMillimeter(2);
        var table = section.AddTable();
        table.Borders.Width = 0.5;
        table.Borders.Color = Colors.Black;
        table.LeftPadding = table.RightPadding = Unit.FromMillimeter(1);
        table.TopPadding = table.BottomPadding = Unit.FromMillimeter(0.5);
        foreach (var column in form.Columns) table.AddColumn(Unit.FromMillimeter(column.WidthMm));

        var grouped = form.Columns.Any(c => c.Group is not null);
        var top = table.AddRow();
        var bottom = grouped ? table.AddRow() : null;
        foreach (var header in bottom is null ? new[] { top } : new[] { top, bottom })
        {
            header.HeadingFormat = true;
            header.Format.Font.Bold = true;
            header.Format.Alignment = ParagraphAlignment.Center;
            header.VerticalAlignment = VerticalAlignment.Center;
        }

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
            // Tiêu đề nhóm nhiều dòng (mẫu 3A: "NCC1: tên / Cũ, mới / Điện thoại") – mỗi dòng một đoạn.
            foreach (var text in column.Group.Split('\n')) top.Cells[c].AddParagraph(text);
            top.Cells[c].MergeRight = span - 1;
        }

        // Ô gộp dọc của MigraDoc không tách sang trang sau: bảng dài hơn MaxMergedRows dòng thì chỉ ghi giá trị ở dòng đầu, không gộp (tự đặt –
        // 15 dòng × 2 tầng chữ vẫn nằm gọn một trang ngang).
        var mergeRows = form.Rows.Count > 1 && form.Rows.Count <= MaxMergedRows;
        for (var r = 0; r < form.Rows.Count; r++)
        {
            var cells = form.Rows[r];
            var row = table.AddRow();
            for (var c = 0; c < form.Columns.Count; c++)
            {
                var column = form.Columns[c];
                row.Cells[c].Format.Alignment = Align(column);
                if (column.MergeRows && r > 0) continue;
                row.Cells[c].AddParagraph(cells[c]);
                if (column.MergeRows && mergeRows)
                {
                    row.Cells[c].MergeDown = form.Rows.Count - 1;
                    row.Cells[c].VerticalAlignment = VerticalAlignment.Center;
                }
            }
        }

        var total = table.AddRow();
        total.Format.Font.Bold = true;
        total.Format.Font.Size = Pt(15);
        total.Cells[0].AddParagraph(form.TotalLabel).Format.Alignment = ParagraphAlignment.Center;
        total.Cells[0].MergeRight = form.TotalSpan - 1;
        for (var i = 0; i < form.TotalCells.Count; i++)
        {
            var c = form.TotalSpan + i;
            total.Cells[c].AddParagraph(form.TotalCells[i]);
            total.Cells[c].Format.Alignment = Align(form.Columns[c]);
        }

        foreach (var extra in form.ExtraRows ?? [])
        {
            var row = table.AddRow();
            row.Format.Font.Size = Pt(extra.FontSize);
            row.Format.Font.Bold = extra.Bold;
            row.Cells[0].AddParagraph(extra.Label).Format.Alignment = ParagraphAlignment.Center;
            row.Cells[0].MergeRight = extra.LabelSpan - 1;
            var c = extra.LabelSpan;
            foreach (var cell in extra.Cells)
            {
                if (c >= form.Columns.Count) break;
                row.Cells[c].AddParagraph(cell.Text);
                row.Cells[c].Format.Alignment = cell.AlignRight ? ParagraphAlignment.Right : ParagraphAlignment.Center;
                row.Cells[c].MergeRight = Math.Max(0, Math.Min(cell.Span, form.Columns.Count - c) - 1);
                c += cell.Span;
            }
        }
    }

    private static ParagraphAlignment Align(CuvtColumn column) =>
        column.AlignRight ? ParagraphAlignment.Right : column.Center ? ParagraphAlignment.Center : ParagraphAlignment.Left;

    /// <summary>
    /// Khối ký: mỗi hàng tối đa 4 ô (A4 dọc) / 5 ô (A4 ngang); ô gồm chức danh, thời điểm duyệt, khoảng trống ký tay, họ tên, chức vụ.
    /// Hàng ký không tách sang hai trang.
    /// </summary>
    private static void AddSigners(Section section, CuvtForm form, double width)
    {
        if (form.Signers.Count == 0) return;
        var perRow = form.Landscape ? 5 : 4;
        var columns = Math.Min(perRow, form.Signers.Count);
        var table = section.AddTable();
        table.Format.SpaceBefore = Unit.FromMillimeter(6);
        foreach (var _ in Enumerable.Range(0, columns)) table.AddColumn(Unit.FromMillimeter(width / columns));
        foreach (var chunk in form.Signers.Chunk(columns))
        {
            var row = table.AddRow();
            row.Format.Alignment = ParagraphAlignment.Center;
            row.BottomPadding = Unit.FromMillimeter(4);
            for (var i = 0; i < chunk.Length; i++)
            {
                var signer = chunk[i];
                var cell = row.Cells[i];
                cell.AddParagraph(signer.Title.ToUpperInvariant()).Format.Font.Bold = true;
                var detail = cell.AddParagraph(signer.Detail ?? "");
                detail.Format.Font.Italic = true;
                detail.Format.Font.Size = Pt(11);
                var name = cell.AddParagraph(signer.Name ?? "");
                name.Format.SpaceBefore = Unit.FromMillimeter(14);
                name.Format.Font.Bold = true;
                if (!string.IsNullOrWhiteSpace(signer.Position)) cell.AddParagraph(signer.Position).Format.Font.Size = Pt(11);
            }
        }
    }
}
