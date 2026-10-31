using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;

namespace Proc.Infrastructure.Printing;

/// <summary>Thông tin công ty in ở đầu phiếu.</summary>
/// <param name="Name">Tên công ty.</param>
/// <param name="Address">Địa chỉ.</param>
/// <param name="TaxCode">Mã số thuế.</param>
internal sealed record PrintCompany(string Name, string? Address, string? TaxCode);

/// <summary>Một cột của bảng dòng hàng.</summary>
/// <param name="Title">Tiêu đề cột.</param>
/// <param name="WidthMm">Độ rộng (mm); tổng các cột = 180 mm (A4 dọc trừ lề 15 mm hai bên).</param>
/// <param name="AlignRight">Cột số – căn phải.</param>
internal sealed record PrintColumn(string Title, double WidthMm, bool AlignRight = false);

/// <summary>Một dòng tổng ở chân bảng.</summary>
/// <param name="Label">Nhãn.</param>
/// <param name="Value">Giá trị đã định dạng.</param>
/// <param name="Strong">In đậm (tổng thanh toán).</param>
internal sealed record PrintTotal(string Label, string Value, bool Strong = false);

/// <summary>Nội dung một phiếu in – dùng chung một khuôn cho mọi loại phiếu.</summary>
/// <param name="Title">Tên phiếu in hoa, ví dụ "ĐƠN ĐẶT HÀNG".</param>
/// <param name="No">Số phiếu.</param>
/// <param name="Date">Ngày phiếu.</param>
/// <param name="StatusNote">Ghi chú trạng thái nổi bật (ví dụ "BẢN NHÁP – chưa duyệt"); null khi phiếu đã chính thức.</param>
/// <param name="Info">Các cặp nhãn – giá trị đầu phiếu (giá trị rỗng thì bỏ).</param>
/// <param name="Columns">Cột bảng dòng hàng.</param>
/// <param name="Rows">Các dòng, mỗi dòng đủ số ô bằng số cột.</param>
/// <param name="Totals">Các dòng tổng.</param>
/// <param name="AmountInWords">Số tiền bằng chữ; null nếu phiếu không có tiền.</param>
/// <param name="Note">Ghi chú phiếu.</param>
/// <param name="Signers">Chức danh ký tên, ví dụ "Người lập phiếu".</param>
/// <param name="SignerNames">Tên in sẵn dưới chữ ký (cùng thứ tự, null = để trống).</param>
internal sealed record PrintDocument(
    string Title, string No, DateOnly Date, string? StatusNote, IReadOnlyList<(string Label, string? Value)> Info,
    IReadOnlyList<PrintColumn> Columns, IReadOnlyList<string[]> Rows, IReadOnlyList<PrintTotal> Totals, string? AmountInWords, string? Note,
    IReadOnlyList<string> Signers, IReadOnlyList<string?> SignerNames);

/// <summary>
/// Khuôn phiếu in A4 dọc (design.md mục 10.10): đầu trang công ty (tên, địa chỉ, MST); tên phiếu, số, ngày; thông tin phiếu; bảng dòng hàng
/// (dòng tiêu đề lặp lại khi sang trang); tổng tiền; số tiền bằng chữ; ghi chú; ô ký tên; chân trang "Trang x/y".
/// </summary>
internal static class PdfLayout
{
    private const double MarginMm = 15;

    /// <summary>
    /// Dựng file PDF.
    /// </summary>
    /// <param name="company">Công ty in ở đầu phiếu.</param>
    /// <param name="doc">Nội dung phiếu.</param>
    /// <returns>Nội dung file PDF.</returns>
    public static byte[] Render(PrintCompany company, PrintDocument doc)
    {
        var document = new Document();
        document.Info.Title = $"{doc.Title} {doc.No}";
        var normal = document.Styles[StyleNames.Normal]!;
        normal.Font.Name = PdfFonts.Family;
        normal.Font.Size = 9;

        var section = document.AddSection();
        var setup = section.PageSetup;
        setup.PageFormat = PageFormat.A4;
        setup.Orientation = Orientation.Portrait;
        setup.LeftMargin = setup.RightMargin = Unit.FromMillimeter(MarginMm);
        setup.TopMargin = Unit.FromMillimeter(12);
        setup.BottomMargin = Unit.FromMillimeter(15);

        var footer = section.Footers.Primary.AddParagraph();
        footer.Format.Alignment = ParagraphAlignment.Right;
        footer.Format.Font.Size = 7.5;
        footer.AddText($"{doc.No} – Trang ");
        footer.AddPageField();
        footer.AddText("/");
        footer.AddNumPagesField();

        var name = section.AddParagraph(company.Name.ToUpperInvariant());
        name.Format.Font.Bold = true;
        name.Format.Font.Size = 10;
        if (!string.IsNullOrWhiteSpace(company.Address)) section.AddParagraph($"Địa chỉ: {company.Address}");
        if (!string.IsNullOrWhiteSpace(company.TaxCode)) section.AddParagraph($"Mã số thuế: {company.TaxCode}");

        var title = section.AddParagraph(doc.Title);
        title.Format.Alignment = ParagraphAlignment.Center;
        title.Format.Font.Size = 15;
        title.Format.Font.Bold = true;
        title.Format.SpaceBefore = Unit.FromMillimeter(5);
        var number = section.AddParagraph($"Số: {doc.No}    Ngày {doc.Date:dd} tháng {doc.Date:MM} năm {doc.Date:yyyy}");
        number.Format.Alignment = ParagraphAlignment.Center;
        number.Format.Font.Italic = true;
        if (doc.StatusNote is not null)
        {
            var status = section.AddParagraph(doc.StatusNote);
            status.Format.Alignment = ParagraphAlignment.Center;
            status.Format.Font.Bold = true;
            status.Format.Font.Color = Colors.DarkRed;
        }

        AddInfo(section, doc.Info);
        AddLines(section, doc);
        AddTotals(section, doc.Totals);

        if (doc.AmountInWords is not null)
        {
            var words = section.AddParagraph();
            words.Format.SpaceBefore = Unit.FromMillimeter(2);
            words.AddFormattedText("Số tiền bằng chữ: ", TextFormat.Bold);
            words.AddFormattedText(doc.AmountInWords + ".", TextFormat.Italic);
        }

        if (!string.IsNullOrWhiteSpace(doc.Note))
        {
            var note = section.AddParagraph();
            note.Format.SpaceBefore = Unit.FromMillimeter(1);
            note.AddFormattedText("Ghi chú: ", TextFormat.Bold);
            note.AddText(doc.Note);
        }

        AddSignatures(section, doc);

        var renderer = new PdfDocumentRenderer { Document = document };
        renderer.RenderDocument();
        using var stream = new MemoryStream();
        renderer.PdfDocument.Save(stream, false);
        return stream.ToArray();
    }

    /// <summary>Thông tin phiếu hai cột nhãn – giá trị, bỏ dòng không có giá trị.</summary>
    private static void AddInfo(Section section, IReadOnlyList<(string Label, string? Value)> info)
    {
        var table = section.AddTable();
        table.Format.SpaceBefore = Unit.FromMillimeter(4);
        table.AddColumn(Unit.FromMillimeter(40));
        table.AddColumn(Unit.FromMillimeter(140));
        foreach (var (label, value) in info.Where(i => !string.IsNullOrWhiteSpace(i.Value)))
        {
            var row = table.AddRow();
            row.Cells[0].AddParagraph(label + ":");
            row.Cells[1].AddParagraph(value!).Format.Font.Bold = true;
        }
    }

    /// <summary>Bảng dòng hàng có kẻ ô; tiêu đề cột nền xám, lặp lại ở mỗi trang.</summary>
    private static void AddLines(Section section, PrintDocument doc)
    {
        section.AddParagraph().Format.SpaceBefore = Unit.FromMillimeter(3);
        var table = section.AddTable();
        table.Borders.Width = 0.5;
        table.Borders.Color = Colors.Gray;
        table.LeftPadding = table.RightPadding = Unit.FromMillimeter(1);
        foreach (var column in doc.Columns) table.AddColumn(Unit.FromMillimeter(column.WidthMm));

        var header = table.AddRow();
        header.HeadingFormat = true;
        header.Shading.Color = new Color(230, 230, 230);
        header.Format.Font.Bold = true;
        header.VerticalAlignment = VerticalAlignment.Center;
        for (var c = 0; c < doc.Columns.Count; c++)
        {
            header.Cells[c].AddParagraph(doc.Columns[c].Title);
            header.Cells[c].Format.Alignment = ParagraphAlignment.Center;
        }

        foreach (var cells in doc.Rows)
        {
            var row = table.AddRow();
            for (var c = 0; c < doc.Columns.Count; c++)
            {
                row.Cells[c].AddParagraph(cells[c]);
                row.Cells[c].Format.Alignment = doc.Columns[c].AlignRight ? ParagraphAlignment.Right : ParagraphAlignment.Left;
            }
        }
    }

    /// <summary>Các dòng tổng căn phải dưới bảng.</summary>
    private static void AddTotals(Section section, IReadOnlyList<PrintTotal> totals)
    {
        if (totals.Count == 0) return;
        var table = section.AddTable();
        table.Format.SpaceBefore = Unit.FromMillimeter(1);
        table.AddColumn(Unit.FromMillimeter(130));
        table.AddColumn(Unit.FromMillimeter(50));
        foreach (var total in totals)
        {
            var row = table.AddRow();
            row.Format.Alignment = ParagraphAlignment.Right;
            row.Format.Font.Bold = total.Strong;
            row.Cells[0].AddParagraph(total.Label + ":");
            row.Cells[1].AddParagraph(total.Value);
        }
    }

    /// <summary>Ô ký tên chia đều chiều ngang: chức danh, "(Ký, ghi rõ họ tên)", khoảng trống ký, tên in sẵn.</summary>
    private static void AddSignatures(Section section, PrintDocument doc)
    {
        if (doc.Signers.Count == 0) return;
        var table = section.AddTable();
        table.Format.SpaceBefore = Unit.FromMillimeter(8);
        table.KeepTogether = true;
        var width = 180.0 / doc.Signers.Count;
        foreach (var _ in doc.Signers) table.AddColumn(Unit.FromMillimeter(width));
        var row = table.AddRow();
        row.Format.Alignment = ParagraphAlignment.Center;
        for (var i = 0; i < doc.Signers.Count; i++)
        {
            var cell = row.Cells[i];
            cell.AddParagraph(doc.Signers[i]).Format.Font.Bold = true;
            cell.AddParagraph("(Ký, ghi rõ họ tên)").Format.Font.Italic = true;
            var signed = cell.AddParagraph(doc.SignerNames.ElementAtOrDefault(i) ?? string.Empty);
            signed.Format.SpaceBefore = Unit.FromMillimeter(18);
            signed.Format.Alignment = ParagraphAlignment.Center;
        }
    }
}
