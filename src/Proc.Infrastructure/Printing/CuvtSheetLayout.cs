using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

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
/// <param name="GapPx">Khoảng cách từ dòng cuối phía trên (bảng / dòng chữ dưới bảng) tới mép trên ảnh.</param>
/// <param name="ChairmanInRow">Ô Chủ tịch là cột cuối cùng hàng (2A, 3A, 4A); false = ô Chủ tịch cả hàng dưới (5A, 5B).</param>
/// <param name="HeadingPt">Cỡ chữ dòng "Kính trình…" (pt theo sheet, chưa nhân tỉ lệ).</param>
/// <param name="TitlePt">Cỡ chữ chức danh (dòng chú thích nghiêng bằng 3/4).</param>
public sealed record SheetSignStyle(
    double[] ColumnsPx, double[] RowsPx, double WidthPx, double HeightPx, double GapPx, bool ChairmanInRow, double HeadingPt, double TitlePt);

/// <summary>Cách in các dòng chữ dưới bảng (<see cref="CuvtForm.AfterTable"/>) theo sheet.</summary>
public enum SheetAfterStyle
{
    /// <summary>Sheet không có dòng chữ dưới bảng (2A/2B, 4A/4B) – không in.</summary>
    None = 0,

    /// <summary>3a/3B: chữ nghiêng, "Bằng chữ" đậm nghiêng, giá trị in đậm khi <see cref="CuvtTextLine.Strong"/>.</summary>
    Italic = 1,

    /// <summary>3A: từng cặp dòng trên một hàng hai nửa ("3. Đề xuất chọn" – "4. Lý do chọn"), chữ đậm; dòng lẻ cả hàng.</summary>
    Paired = 2,
}

/// <summary>
/// Số đo một sheet biểu mẫu CUVT để <see cref="CuvtSheetLayout"/> vẽ lại giữ nguyên (lát IN2 – design.md mục 20.4; người dùng 18/11/2026: "các phiếu
/// này giống hệt sheet Excel như 1A").
/// </summary>
/// <param name="Landscape">Khổ ngang.</param>
/// <param name="SheetColumns">Độ rộng các cột in của sheet A… (đơn vị ký tự Excel; điểm ảnh = rộng × 7 + 5).</param>
/// <param name="ColumnMap">
/// Số cột sheet của từng cột logic của bảng (ví dụ "Tên hàng" gộp B:C = 2). Cột sheet còn lại: <see cref="DynamicGroup"/> = 0 thì chỉ thuộc khung
/// đầu phiếu (3a/3B: bảng A–G, khung A–I); &gt; 0 thì là cột lặp theo số đơn vị / NCC của phiếu.
/// </param>
/// <param name="HeaderRowsPt">Cao các dòng khung đầu phiếu (pt): tiêu đề, phụ đề, ngày.</param>
/// <param name="DateColor">Màu dòng ngày (4A đen, 4B xanh #0000CC).</param>
/// <param name="InfoRowPt">Cao mỗi dòng khung thông tin.</param>
/// <param name="TableHeaderPt">Cao tầng trên của tiêu đề bảng.</param>
/// <param name="RowPt">Cao tối thiểu dòng hàng.</param>
/// <param name="TotalRowPt">Cao dòng tổng và các dòng tổng phụ.</param>
/// <param name="Logo">Vị trí khung logo.</param>
/// <param name="Circle">Vị trí vòng tròn số biểu mẫu.</param>
/// <param name="Sign">Số đo khối ký.</param>
/// <param name="CenteredColumns">Cột logic căn giữa ở dòng hàng (ngoài cột có cờ <see cref="CuvtColumn.Center"/>).</param>
/// <param name="Scale">Tỉ lệ in của sheet (Page Setup – 4A 85 %, 4B 75 %, 3A 80 %); sheet rộng hơn vùng in thì co thêm cho vừa.</param>
/// <param name="MoreHeaderPt">Cao các tầng dưới của tiêu đề bảng khi có cột nhóm (2A: 33,6; 3A: 36,75 – 26,25 – 21).</param>
/// <param name="HeaderFilled">Tiêu đề bảng nền xanh nhạt (4A/4B, 3a/3B có; 2A/2B, 3A không).</param>
/// <param name="HeaderFontPt">Cỡ chữ tiêu đề bảng (3A 14, còn lại 13).</param>
/// <param name="NameFontPt">Cỡ chữ cột tên hàng (cột logic 1): 4A/4B, 3a/3B 12; còn lại 13.</param>
/// <param name="TotalDash">Ô trống dòng tổng in "-" (định dạng kế toán của 4A/4B); false = để trống.</param>
/// <param name="TotalLabelCenter">Chữ dòng tổng căn giữa (2A/2B "Tổng cộng"); false = căn phải.</param>
/// <param name="TotalFontPt">Cỡ chữ dòng tổng (15; 3a/3B "Cộng", 3A 13).</param>
/// <param name="TotalFilled">Dòng tổng nền xanh nhạt.</param>
/// <param name="ShowExtraRows">In các dòng tổng phụ (VAT…); 4A/4B false – các dòng này ẩn trên sheet.</param>
/// <param name="MergeBlankTotals">3A: ô trống của dòng tổng gộp dọc với ô trống cùng cột ở các dòng tổng phụ ngay dưới (F16:F18).</param>
/// <param name="RedColumns">Cột logic chữ đỏ ở dòng hàng (2A/2B "Ngày cung ứng").</param>
/// <param name="DynamicGroup">Số cột của một nhóm lặp (2A/2B: 1 – mỗi đơn vị mua một cột; 3A: 2 – Đơn giá, Thành tiền mỗi NCC); 0 = không lặp.</param>
/// <param name="After">Cách in dòng chữ dưới bảng.</param>
public sealed record SheetLayoutSpec(
    bool Landscape, double[] SheetColumns, int[] ColumnMap, double[] HeaderRowsPt, Color DateColor, double InfoRowPt, double TableHeaderPt,
    double RowPt, double TotalRowPt, SheetPicture Logo, SheetPicture Circle, SheetSignStyle Sign, int[]? CenteredColumns = null, double Scale = 0.85,
    double[]? MoreHeaderPt = null, bool HeaderFilled = true, double HeaderFontPt = 13, double NameFontPt = 13, bool TotalDash = false,
    bool TotalLabelCenter = false, double TotalFontPt = 15, bool TotalFilled = true, bool ShowExtraRows = true, bool MergeBlankTotals = false,
    int[]? RedColumns = null, int DynamicGroup = 0, SheetAfterStyle After = SheetAfterStyle.None);

/// <summary>Một phiếu trong file in: nội dung, số đo sheet, khối ký, góc thông tin.</summary>
/// <param name="Form">Nội dung phiếu.</param>
/// <param name="Spec">Số đo sheet.</param>
/// <param name="Sign">Khối ký đã ghép bước duyệt.</param>
/// <param name="Stamp">Góc thông tin hệ thống, dấu chìm.</param>
public sealed record SheetPage(CuvtForm Form, SheetLayoutSpec Spec, SheetSignBlock Sign, SheetStamp Stamp);

/// <summary>
/// Khuôn in giữ nguyên sheet biểu mẫu CUVT (lát IN2, cùng cách làm của 1A – <see cref="PurchaseRequestSheetLayout"/>): mọi kích thước lấy từ sheet
/// theo điểm ảnh rồi co cả trang theo tỉ lệ in của sheet <c>k</c> (sheet rộng hơn vùng in thì co thêm cho vừa), căn giữa trang như sheet
/// (Page Setup "Center horizontally"). Ví dụ 4A: cột A–M = 1.157 điểm ảnh = 306 mm × 85 % = 260 mm, chữ bảng 13 pt in 11,05 pt; 4B: 1.247 điểm
/// ảnh = 330 mm × 75 % = 247 mm.
/// <list type="bullet">
/// <item>Khung đầu phiếu kẻ viền ngoài: tiêu đề 18 đậm, phụ đề 12 đậm xanh #0000CC, ngày 16 đậm; logo, vòng tròn số biểu mẫu vẽ đè đúng vị trí neo.</item>
/// <item>Khung thông tin: dòng IN HOA căn giữa cỡ 14, số tiền đậm.</item>
/// <item>Bảng: tiêu đề nhiều tầng theo nhóm cột (nhóm lồng nhau tách bằng "|", ví dụ 3A "CÁC NHÀ CUNG CẤP|NCC1…|- Điện thoại: …"), dòng tổng,
/// dòng tổng phụ; dòng ẩn trên sheet không in.</item>
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

    /// <summary>Khối ký 4A/4B (ảnh "Kính trình…" 5 ô, ô Chủ tịch cột cuối).</summary>
    private static readonly double[] Sign4Columns = [204, 265, 320, 258, 297];

    /// <summary>Khối ký 2A/2B.</summary>
    private static readonly double[] Sign2Columns = [168, 204, 186, 192, 216];

    /// <summary>Khối ký 3a/3A/3B (6 ô có dòng chú thích nghiêng).</summary>
    private static readonly double[] Sign3Columns = [148, 167, 161, 161, 219, 232];

    /// <summary>Số đo sheet "4A.Theo dõi MH" (A4 ngang 85 %, cột A–M); dòng VAT ẩn trên sheet nên không in.</summary>
    public static readonly SheetLayoutSpec Sheet4A = new(
        true, [4.4, 7.0, 12.4, 20.3, 6.7, 8.6, 12.7, 10.5, 12.3, 15.4, 15.0, 16.0, 14.7], [1, 2, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1],
        [23.25, 20.1, 20.1, 20.1, 20.1], Colors.Black, 24.9, 50.4, 29.25, 29.25,
        new SheetPicture(1, 7, 11, 71, 69), new SheetPicture(11, 59, 16, 97, 96),
        new SheetSignStyle(Sign4Columns, [45, 41, 161], 1258, 260, 22, true, 15, 14),
        NameFontPt: 12, TotalDash: true, ShowExtraRows: false);

    /// <summary>Số đo sheet "4B.Theo dõi MH" – như 4A, tỉ lệ 75 %, cột rộng hơn, tiêu đề bảng cao 66 pt, ngày xanh.</summary>
    public static readonly SheetLayoutSpec Sheet4B = new(
        true, [4.4, 7.0, 16.7, 20.3, 6.7, 8.6, 12.7, 14.6, 13.3, 16.4, 17.0, 16.0, 15.2], [1, 2, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1],
        [23.25, 20.1, 20.1, 20.1, 20.1], Blue, 24.9, 66, 29.25, 29.25,
        new SheetPicture(0, 25, 12, 101, 98), new SheetPicture(11, 77, 28, 97, 96),
        new SheetSignStyle(Sign4Columns, [45, 41, 161], 1360, 274, 16, true, 15, 14), [3, 4], 0.75,
        NameFontPt: 12, TotalDash: true, ShowExtraRows: false);

    /// <summary>
    /// Số đo sheet "2A.TH MUA VT" (A4 ngang 85 %, cột A–L): tiêu đề bảng hai tầng ("Nội dung đề xuất", "Đơn vị mua") không nền, dòng tổng "Tổng cộng"
    /// gộp A:C căn giữa, ngày cung ứng chữ đỏ; ĐVT, số lượng căn trái như sheet. Mỗi đơn vị mua một cột (K, L của sheet là 2 đơn vị mẫu).
    /// </summary>
    public static readonly SheetLayoutSpec Sheet2A = new(
        true, [11.8, 6.9, 11.9, 22.3, 6.7, 7.3, 15.6, 19.4, 14.0, 14.3, 8.6, 7.5], [1, 1, 1, 1, 1, 1, 1, 1, 1, 1],
        [23.25, 20.1, 20.1, 20.1, 20.1], Colors.Black, 26.1, 24, 33.6, 20.25,
        new SheetPicture(0, 39, 9, 95, 90), new SheetPicture(9, 84, 9, 89, 84),
        new SheetSignStyle(Sign2Columns, [34, 28, 149], 1179, 242, 16, true, 15, 14),
        MoreHeaderPt: [33.6], HeaderFilled: false, TotalLabelCenter: true, RedColumns: [8], DynamicGroup: 1);

    /// <summary>Số đo sheet "2B.TH MUA VT" – như 2A, 4 dòng thông tin, ngày xanh, ngày đề xuất và ngày cung ứng chữ đỏ, các cột căn giữa.</summary>
    public static readonly SheetLayoutSpec Sheet2B = new(
        true, [11.3, 6.9, 12.9, 19.6, 6.7, 8.4, 15.6, 19.4, 14.0, 13.4, 8.6, 9.3], [1, 1, 1, 1, 1, 1, 1, 1, 1, 1],
        [23.25, 20.1, 20.1, 20.1, 20.1], Blue, 26.1, 24, 33, 33,
        new SheetPicture(0, 39, 9, 95, 90), new SheetPicture(10, 29, 16, 89, 84),
        new SheetSignStyle(Sign2Columns, [34, 28, 149], 1179, 248, 5, true, 15, 14), [4, 5, 6, 8],
        MoreHeaderPt: [33.6], HeaderFilled: false, TotalLabelCenter: true, RedColumns: [0, 8], DynamicGroup: 1);

    /// <summary>
    /// Số đo sheet "3a.Bang trình giá" (A4 ngang 85 %): bảng A–G, khung đầu phiếu A–I; tiêu đề bảng nền xanh nhạt; "Cộng" (13 đậm, gộp A:B) – "Thuế VAT"
    /// – "Tổng cộng" (15 đậm nền xanh); dòng chữ dưới bảng nghiêng.
    /// </summary>
    public static readonly SheetLayoutSpec Sheet3a = new(
        true, [11.3, 40.0, 11.7, 13.0, 15.4, 16.9, 16.6, 13.0, 13.0], [1, 1, 1, 1, 1, 1, 1],
        [23.25, 20.1, 20.1, 20.1, 20.1], Colors.Black, 24.9, 37.5, 24.9, 24.9,
        new SheetPicture(0, 18, 19, 87, 82), new SheetPicture(8, 4, 11, 89, 82),
        new SheetSignStyle(Sign3Columns, [35, 52, 157], 1064, 250, 4, true, 13, 13), [3, 6],
        NameFontPt: 12, TotalFontPt: 13, TotalFilled: false, After: SheetAfterStyle.Italic);

    /// <summary>Số đo sheet "3B.Bang trình giá" – như 3a, ngày xanh.</summary>
    public static readonly SheetLayoutSpec Sheet3B = Sheet3a with
    {
        DateColor = Blue,
        Logo = new SheetPicture(0, 15, 7, 87, 82),
        Circle = new SheetPicture(8, 12, 20, 93, 85),
        Sign = new SheetSignStyle(Sign3Columns, [35, 52, 157], 1064, 217, 4, true, 13, 13),
    };

    /// <summary>
    /// Số đo sheet "3A.So Sanh chon NCC" (A4 ngang 80 %, cột A–K): tiêu đề bảng 4 tầng chữ 14 không nền – "CÁC NHÀ CUNG CẤP" / từng NCC (tên, cũ/mới) /
    /// điện thoại / Đơn giá – Thành tiền; mỗi NCC 2 cột (sheet mẫu 3 NCC F–K); dòng tổng phụ 13 đậm, ô đơn giá trống gộp dọc 3 dòng tiền.
    /// </summary>
    public static readonly SheetLayoutSpec Sheet3A = new(
        true, [7.4, 8.1, 24.6, 8.1, 11.4, 12.0, 20.0, 17.1, 19.4, 13.7, 22.1], [1, 2, 1, 1],
        [23.25, 20.1, 20.1, 20.1, 20.1], Colors.Black, 24.9, 29.25, 24.9, 24.9,
        new SheetPicture(0, 33, 17, 101, 96), new SheetPicture(10, 28, 19, 96, 92),
        new SheetSignStyle(Sign3Columns, [35, 52, 157], 1319, 220, 8, true, 13, 13), [2, 3], 0.80,
        MoreHeaderPt: [36.75, 26.25, 21], HeaderFilled: false, HeaderFontPt: 14, TotalFontPt: 13, TotalFilled: false, MergeBlankTotals: true,
        DynamicGroup: 2, After: SheetAfterStyle.Paired);

    /// <summary>Dựng PDF một phiếu.</summary>
    /// <param name="form">Nội dung phiếu (tiêu đề, phụ đề, thông tin, cột, dòng, dòng tổng).</param>
    /// <param name="spec">Số đo sheet.</param>
    /// <param name="logo">Logo công ty của phiếu; null = để trống.</param>
    /// <param name="sign">Khối ký đã ghép bước duyệt (<see cref="SheetSign.Build"/>).</param>
    /// <param name="stamp">Góc thông tin hệ thống, dấu chìm.</param>
    /// <returns>Nội dung PDF.</returns>
    public static byte[] Render(CuvtForm form, SheetLayoutSpec spec, SheetImage? logo, SheetSignBlock sign, SheetStamp stamp) =>
        RenderMany([new SheetPage(form, spec, sign, stamp)], logo);

    /// <summary>
    /// Dựng nhiều phiếu vào một file PDF (bảng trình giá 3a/3B của từng NCC trúng thầu – lát Y6): mỗi phiếu dựng riêng (trang đầu có logo, vòng tròn
    /// số biểu mẫu; số trang đếm trong phiếu) rồi nối trang.
    /// </summary>
    /// <param name="pages">Các phiếu (ít nhất một).</param>
    /// <param name="logo">Logo công ty.</param>
    /// <returns>Nội dung PDF; tiêu đề tài liệu theo phiếu đầu.</returns>
    /// <exception cref="ArgumentException">Khi danh sách rỗng.</exception>
    public static byte[] RenderMany(IReadOnlyList<SheetPage> pages, SheetImage? logo)
    {
        if (pages.Count == 0) throw new ArgumentException("Cần ít nhất một phiếu.", nameof(pages));
        var parts = pages.Select(p => RenderOne(p, logo)).ToList();
        if (parts.Count == 1) return parts[0];
        var output = new PdfDocument();
        output.Info.Title = $"{pages[0].Form.Title} {pages[0].Form.No}";
        foreach (var part in parts)
        {
            var source = PdfReader.Open(new MemoryStream(part), PdfDocumentOpenMode.Import);
            foreach (var page in source.Pages) output.AddPage(page);
        }

        using var stream = new MemoryStream();
        output.Save(stream, false);
        return stream.ToArray();
    }

    private static byte[] RenderOne(SheetPage page, SheetImage? logo)
    {
        var (form, spec, sign, stamp) = page;
        var document = new Document();
        document.Info.Title = $"{form.Title} {form.No}";
        var columnPx = LogicalPx(form, spec);
        var g = new Geometry(spec, columnPx.Sum(), NaturalHeightPt(form, spec, sign));
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
        AddAfter(section, form, spec, g, columnPx);
        AddSignBlock(section, sign, spec.Sign, g);

        var renderer = new PdfDocumentRenderer { Document = document };
        renderer.RenderDocument();
        DrawOverlays(renderer.PdfDocument, form.FormNo, logo, stamp.Watermark, spec, g);
        using var stream = new MemoryStream();
        renderer.PdfDocument.Save(stream, false);
        return stream.ToArray();
    }

    /// <summary>Quy đổi điểm ảnh sheet ↔ mm trên giấy: cả sheet co theo một tỉ lệ (tỉ lệ in của sheet, hoặc nhỏ hơn cho vừa bề ngang vùng in).</summary>
    private sealed class Geometry
    {
        public Geometry(SheetLayoutSpec spec, double tablePx, double heightPt)
        {
            ColumnPx = spec.SheetColumns.Select(ColumnWidthPx).ToArray();
            // Khung đầu phiếu rộng bằng sheet; bảng có thêm cột đơn vị / NCC thì khung rộng theo bảng.
            TotalPx = Math.Max(ColumnPx.Sum(), tablePx);
            PageWidthMm = spec.Landscape ? 297 : 210;
            var natural = TotalPx * MmPerPx;
            // Sheet in lề 0 vừa một trang; bản in chừa lề trên 6 mm và 26 mm đáy cho góc thông tin hệ thống nên phiếu chỉ vừa tràn thì co thêm cho vừa
            // một trang (như Excel "vừa trang"), nhưng không co quá 80 % tỉ lệ sheet – bảng dài thì sang trang như Excel. Cao ước lượng cộng 7 % cho
            // lề ô và dòng chữ xuống hàng. Ví dụ 3a 2 dòng hàng: cao 677 pt × 1,07 = 256 mm; còn 176 mm → k = 0,69 (sheet 0,85).
            var usableMm = (spec.Landscape ? 210 : 297) - MarginMm - 26 - 2;
            var byHeight = usableMm / (heightPt * 1.07 * 25.4 / 72);
            K = Math.Min(Math.Min(spec.Scale, (PageWidthMm - 2 * MarginMm) / natural), Math.Max(byHeight, spec.Scale * 0.8));
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

        /// <summary>Rộng khung phiếu đang in (điểm ảnh) – bằng rộng sheet, trừ khi bảng có thêm cột đơn vị mua / NCC.</summary>
        public double TotalPx { get; }

        /// <summary>Rộng khung phiếu trên giấy (mm).</summary>
        public double WidthMm { get; }

        /// <summary>mm trên giấy của một điểm ảnh sheet.</summary>
        public double MmPx { get; }

        /// <summary>Tỉ lệ in so với cỡ thật của sheet.</summary>
        public double K { get; }

        public Unit Pt(double sheetPt) => Unit.FromPoint(sheetPt * K);

        /// <summary>Mép trái cột <paramref name="col"/> (điểm ảnh).</summary>
        public double Left(int col) => ColumnPx.Take(col).Sum();

        /// <summary>
        /// Mép trái hình neo trên sheet. Vòng tròn số biểu mẫu nằm sát phải khung đầu phiếu nên giữ khoảng cách tới mép phải (bảng rộng thêm cột
        /// thì vòng tròn dời theo); logo giữ khoảng cách tới mép trái.
        /// </summary>
        public double PictureLeft(SheetPicture p, bool fromRight) =>
            fromRight ? TotalPx - (ColumnPx.Sum() - (Left(p.Col) + p.OffsetPx)) : Left(p.Col) + p.OffsetPx;
    }

    /// <summary>
    /// Cao tự nhiên của phiếu (pt, tỉ lệ 100 %) tính theo chiều cao dòng tối thiểu của sheet: khung đầu, khung thông tin, tiêu đề bảng, dòng hàng, dòng
    /// tổng, dòng chữ dưới bảng, khối ký (cỡ ảnh trên sheet; 1 điểm ảnh = 0,75 pt). Dòng chữ dài xuống hàng thì thực tế cao hơn.
    /// </summary>
    private static double NaturalHeightPt(CuvtForm form, SheetLayoutSpec spec, SheetSignBlock sign)
    {
        var tiers = 1 + form.Columns.Max(c => c.Group?.Split('|').Length ?? 0);
        var header = new[] { spec.TableHeaderPt }.Concat(spec.MoreHeaderPt ?? []).Take(tiers).Sum();
        var totals = 1 + (spec.ShowExtraRows ? form.ExtraRows?.Count ?? 0 : 0);
        var after = spec.After switch
        {
            SheetAfterStyle.Italic => form.AfterTable?.Count ?? 0,
            SheetAfterStyle.Paired => ((form.AfterTable?.Count ?? 0) + 1) / 2,
            _ => 0,
        };
        // Hàng ô phát sinh (chặng vượt…) thêm hàng chức danh + hàng ký như ảnh.
        var signPx = spec.Sign.HeightPx * (1 + (sign.Extra.Count > 0 ? (spec.Sign.RowsPx[1] + spec.Sign.RowsPx[2]) / spec.Sign.RowsPx.Sum() : 0));
        return spec.HeaderRowsPt.Sum() + form.Info.Count * spec.InfoRowPt + header + form.Rows.Count * spec.RowPt + totals * spec.TotalRowPt
               + after * spec.RowPt + (spec.Sign.GapPx + signPx) * 0.75;
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
    /// Bảng dòng hàng theo cột sheet. Tiêu đề nhiều tầng: nhóm cột lồng nhau ghi trong <see cref="CuvtColumn.Group"/> tách bằng "|" (tầng trên trước),
    /// mỗi tầng gộp ngang các cột liền nhau cùng nhóm; cột không nhóm gộp dọc hết các tầng; đoạn nhóm bắt đầu "- " (dòng điện thoại NCC của 3A) in chữ
    /// thường căn trái. Cột gộp dọc (<see cref="CuvtColumn.MergeRows"/>) chỉ gộp khi bảng không quá <see cref="CuvtFormLayout.MaxMergedRows"/> dòng
    /// (ô gộp của MigraDoc không tách sang trang sau).
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

        AddTableHeader(table, form, spec, g);

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
                if (c == 1 && Math.Abs(spec.NameFontPt - 13) > 0.01) cell.Format.Font.Size = g.Pt(spec.NameFontPt);
                if (column.MergeRows && r > 0 && mergeRows) continue;
                cell.AddParagraph(form.Rows[r][c]);
                if (column.MergeRows && mergeRows) cell.MergeDown = form.Rows.Count - 1;
            }
        }

        AddTotals(table, form, spec, g);
    }

    /// <summary>Tiêu đề bảng một hoặc nhiều tầng (xem <see cref="AddLines"/>).</summary>
    private static void AddTableHeader(Table table, CuvtForm form, SheetLayoutSpec spec, Geometry g)
    {
        var groups = form.Columns.Select(c => c.Group?.Split('|') ?? []).ToList();
        var tiers = 1 + groups.Max(s => s.Length);
        var heights = new[] { spec.TableHeaderPt }.Concat(spec.MoreHeaderPt ?? []).ToArray();
        var rows = new List<Row>();
        for (var t = 0; t < tiers; t++)
        {
            var row = table.AddRow();
            row.HeadingFormat = true;
            row.HeightRule = RowHeightRule.AtLeast;
            row.Height = g.Pt(heights[Math.Min(t, heights.Length - 1)]);
            row.VerticalAlignment = VerticalAlignment.Center;
            row.Format.Font.Bold = true;
            row.Format.Font.Size = g.Pt(spec.HeaderFontPt);
            row.Format.Alignment = ParagraphAlignment.Center;
            if (spec.HeaderFilled) row.Shading.Color = HeaderFill;
            rows.Add(row);
        }

        // Hai cột cùng nhóm ở tầng t khi mọi đoạn nhóm từ tầng 0 đến t trùng nhau.
        bool SamePrefix(int a, int b, int t) =>
            groups[a].Length > t && groups[b].Length > t && groups[a].Take(t + 1).SequenceEqual(groups[b].Take(t + 1));

        for (var c = 0; c < form.Columns.Count; c++)
        {
            var segments = groups[c];
            for (var t = 0; t < segments.Length; t++)
            {
                if (c > 0 && SamePrefix(c - 1, c, t)) continue;
                var span = 1;
                while (c + span < form.Columns.Count && SamePrefix(c, c + span, t)) span++;
                var cell = rows[t].Cells[c];
                foreach (var text in segments[t].Split('\n'))
                {
                    var p = cell.AddParagraph(text);
                    if (!text.StartsWith("- ", StringComparison.Ordinal)) continue;
                    p.Format.Font.Bold = false;
                    p.Format.Alignment = ParagraphAlignment.Left;
                }

                cell.MergeRight = span - 1;
            }

            var titleTier = segments.Length;
            rows[titleTier].Cells[c].AddParagraph(form.Columns[c].Title);
            if (titleTier < tiers - 1) rows[titleTier].Cells[c].MergeDown = tiers - 1 - titleTier;
        }
    }

    /// <summary>
    /// Dòng tổng và dòng tổng phụ (VAT, tổng gồm thuế, hình thức thanh toán…). Ô trống dòng tổng: 4A/4B in "-" (định dạng kế toán); 3A gộp dọc với ô
    /// trống cùng cột của các dòng tổng phụ ngay dưới (F16:F18 của sheet).
    /// </summary>
    private static void AddTotals(Table table, CuvtForm form, SheetLayoutSpec spec, Geometry g)
    {
        var columns = form.Columns.Count;
        var labelAlign = spec.TotalLabelCenter ? ParagraphAlignment.Center : ParagraphAlignment.Right;
        var total = table.AddRow();
        total.HeightRule = RowHeightRule.AtLeast;
        total.Height = g.Pt(spec.TotalRowPt);
        total.VerticalAlignment = VerticalAlignment.Center;
        total.Format.Font.Bold = true;
        total.Format.Font.Size = g.Pt(spec.TotalFontPt);
        if (spec.TotalFilled) total.Shading.Color = TotalFill;
        total.Cells[0].AddParagraph(form.TotalLabel).Format.Alignment = labelAlign;
        total.Cells[0].MergeRight = form.TotalSpan - 1;
        var blanks = new List<int>();
        for (var i = 0; i < form.TotalCells.Count && form.TotalSpan + i < columns; i++)
        {
            var text = form.TotalCells[i];
            var cell = total.Cells[form.TotalSpan + i];
            var dash = spec.TotalDash && string.IsNullOrEmpty(text);
            cell.AddParagraph(dash ? "-" : text);
            cell.Format.Alignment = dash ? ParagraphAlignment.Center : ParagraphAlignment.Right;
            if (string.IsNullOrEmpty(text)) blanks.Add(form.TotalSpan + i);
        }

        if (!spec.ShowExtraRows) return;
        var extras = form.ExtraRows ?? [];
        var extraCells = new List<Dictionary<int, CuvtCell>>();
        foreach (var extra in extras)
        {
            var row = table.AddRow();
            row.HeightRule = RowHeightRule.AtLeast;
            row.Height = g.Pt(spec.TotalRowPt);
            row.VerticalAlignment = VerticalAlignment.Center;
            row.Format.Font.Size = g.Pt(extra.FontSize);
            row.Format.Font.Bold = extra.Bold;
            if (extra.Fill) row.Shading.Color = TotalFill;
            // Nhãn dòng tổng phụ luôn đậm như sheet ("Thuế VAT (10%)" đậm, số tiền thường – 3a dòng 16); cờ Bold áp cho các ô số.
            var label = row.Cells[0].AddParagraph(extra.Label);
            label.Format.Alignment = labelAlign;
            label.Format.Font.Bold = true;
            row.Cells[0].MergeRight = extra.LabelSpan - 1;
            var map = new Dictionary<int, CuvtCell>();
            var c = extra.LabelSpan;
            foreach (var cellValue in extra.Cells)
            {
                if (c >= columns) break;
                map[c] = cellValue;
                row.Cells[c].AddParagraph(cellValue.Text);
                row.Cells[c].Format.Alignment = cellValue.AlignRight ? ParagraphAlignment.Right : ParagraphAlignment.Center;
                row.Cells[c].MergeRight = Math.Max(0, Math.Min(cellValue.Span, columns - c) - 1);
                c += cellValue.Span;
            }

            extraCells.Add(map);
        }

        if (!spec.MergeBlankTotals) return;
        foreach (var c in blanks)
        {
            var down = extraCells.TakeWhile(m => m.TryGetValue(c, out var v) && v.Span == 1 && string.IsNullOrEmpty(v.Text)).Count();
            if (down > 0) total.Cells[c].MergeDown = down;
        }
    }

    /// <summary>Rộng cột logic (điểm ảnh sheet) – xem <see cref="SheetLayoutSpec.ColumnMap"/>, <see cref="SheetLayoutSpec.DynamicGroup"/>.</summary>
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
        if (extra <= 0) return widths.ToArray();
        // Cột lặp (đơn vị mua, NCC): vị trí j trong nhóm lấy độ rộng bình quân các cột sheet cùng vị trí – 3A: Đơn giá (12; 17,1; 13,7),
        // Thành tiền (20; 19,4; 22,1). Như chèn thêm cột trong Excel: phiếu nhiều đơn vị / NCC hơn sheet mẫu thì bảng rộng ra.
        var rest = sheet.Skip(col).ToArray();
        var group = Math.Max(1, spec.DynamicGroup);
        for (var i = 0; i < extra; i++)
        {
            var position = rest.Where((_, k) => k % group == i % group).ToArray();
            widths.Add(position.Length == 0 ? sheet.Average() : position.Average());
        }

        return widths.ToArray();
    }

    /// <summary>
    /// Dòng chữ dưới bảng theo sheet: 3a/3B nghiêng ("Bằng chữ" đậm nghiêng, giá trị đậm khi Strong); 3A từng cặp trên một hàng hai nửa, chữ đậm
    /// ("3. Đề xuất chọn: …" – "4. Lý do chọn: …"), dòng lẻ (lý do không chọn giá thấp nhất từng dòng) cả hàng.
    /// </summary>
    private static void AddAfter(Section section, CuvtForm form, SheetLayoutSpec spec, Geometry g, double[] columnPx)
    {
        var lines = form.AfterTable ?? [];
        if (spec.After == SheetAfterStyle.None || lines.Count == 0) return;
        var table = section.AddTable();
        table.Borders.Visible = false;
        table.LeftPadding = table.RightPadding = Unit.FromMillimeter(0.5);
        table.Format.Font.Size = g.Pt(13);
        var tableMm = columnPx.Sum() * g.MmPx;
        if (spec.After == SheetAfterStyle.Italic)
        {
            table.AddColumn(Unit.FromMillimeter(tableMm));
            foreach (var line in lines)
            {
                var row = table.AddRow();
                row.HeightRule = RowHeightRule.AtLeast;
                row.Height = g.Pt(spec.RowPt);
                row.VerticalAlignment = VerticalAlignment.Center;
                var p = row.Cells[0].AddParagraph();
                p.Format.Font.Italic = true;
                p.AddFormattedText(line.Label + ": ").Bold = line.BoldLabel;
                p.AddFormattedText(line.Value).Bold = line.Strong;
            }

            return;
        }

        // 3A: nửa trái rộng tới hết cột đơn giá NCC đầu (A–F của sheet), như "4. Lý do chọn" bắt đầu ở cột G.
        var left = columnPx.Take(Math.Min(spec.ColumnMap.Length + 1, columnPx.Length)).Sum() * g.MmPx;
        table.AddColumn(Unit.FromMillimeter(left));
        table.AddColumn(Unit.FromMillimeter(Math.Max(10, tableMm - left)));
        foreach (var pair in lines.Chunk(2))
        {
            var row = table.AddRow();
            row.HeightRule = RowHeightRule.AtLeast;
            row.Height = g.Pt(spec.RowPt);
            row.VerticalAlignment = VerticalAlignment.Center;
            row.Format.Font.Bold = true;
            for (var i = 0; i < pair.Length; i++) row.Cells[i].AddParagraph($"{pair[i].Label}: {pair[i].Value}");
            if (pair.Length == 1) row.Cells[0].MergeRight = 1;
        }
    }

    /// <summary>
    /// Khối ký theo ảnh trong sheet: dòng "Kính trình…" gộp cả hàng, hàng chức danh (+ chú thích nghiêng), hàng ký (chữ ký, họ tên, giờ duyệt nằm đáy
    /// ô); ô Chủ tịch là cột cuối (2A, 3A, 4A) hoặc cả hàng dưới (5A, 5B). Bước không có ô trên sheet (chặng vượt, vai trò tự tạo) in hàng ô phát sinh
    /// sau hàng ký. Đường kẻ màu xanh xám như ảnh. Khối ký không tách sang hai trang.
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

        if (!style.ChairmanInRow)
        {
            var final = table.AddRow();
            final.HeightRule = RowHeightRule.AtLeast;
            final.Height = Unit.FromMillimeter(style.RowsPx[3] * mmPerImageRowPx);
            final.Cells[0].MergeRight = columns - 1;
            TitleCell(final.Cells[0], sign.Final, style.TitlePt, g);
            SignCell(final.Cells[0], sign.Final, widthMm, final.Height.Millimeter * 0.55, g, upper: true);
        }

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
    private static void DrawOverlays(PdfDocument pdf, string formNo, SheetImage? logo, string? watermark, SheetLayoutSpec spec, Geometry g)
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
