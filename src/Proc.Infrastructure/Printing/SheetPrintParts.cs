using MigraDoc.DocumentObjectModel;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace Proc.Infrastructure.Printing;

/// <summary>
/// Phần in dùng chung của mọi phiếu có chữ ký (lát IN1 đề nghị mua, lát IN2 các phiếu CUVT khác – design.md mục 20.4): chèn ảnh chữ ký / logo giữ
/// tỉ lệ, góc thông tin hệ thống dưới phải mọi trang (chữ 9), dấu chìm "BẢN NHÁP" / "CHƯA DUYỆT" / "ĐÃ HỦY".
/// </summary>
internal static class SheetPrintParts
{
    /// <summary>Độ rộng khung góc thông tin hệ thống (mm) – đủ một dòng "Tình trạng phê duyệt: Đang chờ duyệt – đã duyệt 3/8 bước, chờ …".</summary>
    public const double StampWidthMm = 124;

    private static readonly Color Muted = new(0x70, 0x70, 0x70);

    /// <summary>Chèn ảnh co vừa khung, giữ tỉ lệ điểm ảnh – như chữ ký mực trên giấy: nền PNG trong suốt nên không che đường kẻ.</summary>
    /// <param name="paragraph">Đoạn chứa ảnh.</param>
    /// <param name="image">Ảnh.</param>
    /// <param name="maxWidthMm">Rộng tối đa (mm).</param>
    /// <param name="maxHeightMm">Cao tối đa (mm).</param>
    public static void AddImage(Paragraph paragraph, SheetImage image, double maxWidthMm, double maxHeightMm)
    {
        var scale = Math.Min(maxWidthMm / image.Width, maxHeightMm / image.Height);
        var picture = paragraph.AddImage("base64:" + Convert.ToBase64String(image.Content));
        picture.LockAspectRatio = false;
        picture.Width = Unit.FromMillimeter(image.Width * scale);
        picture.Height = Unit.FromMillimeter(image.Height * scale);
    }

    /// <summary>
    /// Góc dưới phải mọi trang của section: khung viền mảnh, chữ 9 phông không chân (người dùng yêu cầu 18/11/2026), "{PAGE}" / "{PAGES}" thay bằng
    /// số trang. Section cần lề dưới đủ chỗ (khoảng 26 mm cho 4 dòng).
    /// </summary>
    /// <param name="section">Section của phiếu.</param>
    /// <param name="stamp">Các dòng thông tin.</param>
    /// <param name="widthMm">Độ rộng vùng in của section (mm) – khung nằm sát lề phải.</param>
    public static void AddStamp(Section section, SheetStamp stamp, double widthMm)
    {
        var table = section.Footers.Primary.AddTable();
        table.Borders.Visible = false;
        table.AddColumn(Unit.FromMillimeter(widthMm - StampWidthMm));
        table.AddColumn(Unit.FromMillimeter(StampWidthMm));
        var row = table.AddRow();
        var cell = row.Cells[1];
        cell.Borders.Visible = true;
        cell.Borders.Width = 0.5;
        cell.Borders.Color = Muted;
        cell.Format.Font.Name = PdfFonts.Family;
        cell.Format.Font.Size = 9;
        foreach (var line in stamp.Lines)
        {
            var p = cell.AddParagraph();
            var rest = line;
            while (rest.Length > 0)
            {
                var page = rest.IndexOf("{PAGE}", StringComparison.Ordinal);
                var pages = rest.IndexOf("{PAGES}", StringComparison.Ordinal);
                var next = new[] { page, pages }.Where(i => i >= 0).DefaultIfEmpty(-1).Min();
                if (next < 0)
                {
                    p.AddText(rest);
                    break;
                }

                p.AddText(rest[..next]);
                if (next == pages)
                {
                    p.AddNumPagesField();
                    rest = rest[(next + 7)..];
                }
                else
                {
                    p.AddPageField();
                    rest = rest[(next + 6)..];
                }
            }
        }
    }

    /// <summary>Dấu chìm đỏ nhạt nghiêng 35° giữa trang – phiếu chưa có hiệu lực vẫn in được nhưng không thể nhầm với bản đã duyệt.</summary>
    /// <param name="gfx">Bút vẽ của trang (chế độ vẽ thêm).</param>
    /// <param name="page">Trang.</param>
    /// <param name="watermark">Chữ dấu chìm.</param>
    public static void DrawWatermark(XGraphics gfx, PdfPage page, string watermark)
    {
        var w = page.Width.Point;
        var h = page.Height.Point;
        var state = gfx.Save();
        gfx.TranslateTransform(w / 2, h / 2);
        gfx.RotateTransform(-35);
        var mark = new XFont(PdfFonts.Family, 60, XFontStyleEx.Bold);
        gfx.DrawString(watermark, mark, new XSolidBrush(XColor.FromArgb(40, 200, 0, 0)), new XRect(-w, -60, 2 * w, 120), XStringFormats.Center);
        gfx.Restore(state);
    }
}
