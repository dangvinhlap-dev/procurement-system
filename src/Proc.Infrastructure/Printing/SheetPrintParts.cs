using MigraDoc.DocumentObjectModel;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace Proc.Infrastructure.Printing;

/// <summary>
/// Phần in dùng chung của mọi phiếu có chữ ký (lát IN1 đề nghị mua, lát IN2 các phiếu CUVT khác – design.md mục 20.4): chèn ảnh chữ ký / logo giữ
/// tỉ lệ, thông tin hệ thống góc dưới trái trang cuối (chữ 9), dấu chìm "BẢN NHÁP" / "CHƯA DUYỆT" / "ĐÃ HỦY".
/// </summary>
internal static class SheetPrintParts
{
    /// <summary>Khung thông tin hệ thống cách mép trái giấy (mm) – bằng lề trái của các khuôn in.</summary>
    private const double StampLeftMm = 6;

    /// <summary>Khung thông tin hệ thống cách mép dưới giấy (mm).</summary>
    private const double StampBottomMm = 4;

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
    /// Thông tin hệ thống (giờ xem / in, người, số phiếu, lần in, số trang, tình trạng phê duyệt) ở <b>góc dưới bên trái trang cuối cùng</b> (người
    /// dùng yêu cầu 20/11/2026 – thay cho góc dưới phải mọi trang): khung viền mảnh, chữ 9 phông không chân, vẽ sau khi đã nối mọi trang (1B kèm 2B,
    /// 3a/3B nhiều NCC) nên chỉ một khung cho cả file; "{PAGE}" / "{PAGES}" đều thay bằng tổng số trang. Các trang trước trang cuối có một dòng nhỏ
    /// (chữ 8, xám) cùng góc: "Số phiếu: … · Trang x/y" (người dùng yêu cầu 20/11/2026). Khung, dòng nằm trong lề dưới 26 mm mà các khuôn in chừa sẵn
    /// nên không đè nội dung.
    /// </summary>
    /// <param name="pdf">File PDF đã dựng xong.</param>
    /// <param name="stamp">Các dòng thông tin.</param>
    /// <returns>File PDF có khung thông tin ở trang cuối.</returns>
    public static byte[] StampLastPage(byte[] pdf, SheetStamp stamp)
    {
        using var document = PdfReader.Open(new MemoryStream(pdf), PdfDocumentOpenMode.Modify);
        var count = document.PageCount;
        var left = XUnit.FromMillimeter(StampLeftMm).Point;
        if (stamp.DocNo is { } no)
        {
            var small = new XFont(PdfFonts.Family, 8, XFontStyleEx.Regular);
            for (var n = 0; n < count - 1; n++)
            {
                var other = document.Pages[n];
                using var g = XGraphics.FromPdfPage(other, XGraphicsPdfPageOptions.Append);
                var y = other.Height.Point - XUnit.FromMillimeter(StampBottomMm).Point - small.GetHeight();
                g.DrawString($"Số phiếu: {no} · Trang {n + 1}/{count}", small, new XSolidBrush(XColor.FromArgb(0x50, 0x50, 0x50)),
                    new XRect(left, y, other.Width.Point - 2 * left, small.GetHeight()), XStringFormats.TopLeft);
            }
        }

        var page = document.Pages[count - 1];
        var lines = stamp.Lines.Select(l => l.Replace("{PAGES}", count.ToString(), StringComparison.Ordinal)
            .Replace("{PAGE}", count.ToString(), StringComparison.Ordinal)).ToList();
        using (var gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append))
        {
            var font = new XFont(PdfFonts.Family, 9, XFontStyleEx.Regular);
            const double pad = 3;
            var lineHeight = font.GetHeight();
            var width = lines.Select(l => gfx.MeasureString(l, font).Width).DefaultIfEmpty(0).Max() + 2 * pad;
            var height = lines.Count * lineHeight + 2 * pad;
            var top = page.Height.Point - XUnit.FromMillimeter(StampBottomMm).Point - height;
            gfx.DrawRectangle(new XPen(XColor.FromArgb(0x70, 0x70, 0x70), 0.5), left, top, width, height);
            for (var i = 0; i < lines.Count; i++)
                gfx.DrawString(lines[i], font, XBrushes.Black, new XRect(left + pad, top + pad + i * lineHeight, width, lineHeight), XStringFormats.TopLeft);
        }

        using var stream = new MemoryStream();
        document.Save(stream, false);
        return stream.ToArray();
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
