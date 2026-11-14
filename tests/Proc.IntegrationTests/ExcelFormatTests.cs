using System.Net;
using System.Net.Http.Json;
using ClosedXML.Excel;
using Proc.Application.Excel;

namespace Proc.IntegrationTests;

/// <summary>
/// Mọi file Excel xuất ra cùng định dạng lưới trên màn hình (yêu cầu 14/11/2026, ảnh mẫu docs/diagrams/formatexportExcel.png –
/// ui-style.md mục 5i): Tahoma 9pt, tiêu đề cột nền #D3DDEA chữ thường, kẻ ô #B8C5D6, số phông đơn cách canh phải, số chứng từ chữ xanh
/// đậm, trạng thái ô vuông ■ + chữ màu theo đèn.
/// </summary>
[Collection(ApiCollection.Name)]
public class ExcelFormatTests
{
    private readonly ProcApiFactory _factory;

    /// <summary>Nhận factory dùng chung.</summary>
    public ExcelFormatTests(ProcApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Mã màu ARGB của màu ClosedXML đọc từ file (FF + mã HTML).</summary>
    private static string Argb(XLColor color) => color.Color.ToArgb().ToString("X8");

    [Fact]
    public async Task XuatLuoi_DinhDangNhuLuoiTrenManHinh()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var request = new GridExportRequest("Danh sách chứng từ", null, "danh-sach-chung-tu",
        [
            new("Loại chứng từ", "text", 150), new("Số chứng từ", "docno", 120), new("Ngày", "date", 90),
            new("Tình trạng", "status", 130, ["warning", "success"]), new("Giá trị", "money", 120),
        ],
        [
            ["Đề nghị mua hàng", "PR-TCT-2611-0001", "2026-11-12", "Chưa hoàn thành", "1250000"],
            ["Bảng giá NCC", "PRL-TCT-2611-0001", "2026-11-07", "Hoàn thành", null],
        ]);

        var response = await client.PostAsJsonAsync("/api/excel/grid", request, Ct);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync(Ct));
        using var workbook = new XLWorkbook(await response.Content.ReadAsStreamAsync(Ct));
        var sheet = workbook.Worksheet(1);

        // Tắt lưới mờ của Excel – chỉ còn đường kẻ của bảng; tiêu đề báo cáo đậm xanh đậm.
        Assert.False(sheet.ShowGridLines);
        Assert.True(sheet.Cell(1, 1).Style.Font.Bold);
        Assert.Equal("FF1F3D6B", Argb(sheet.Cell(1, 1).Style.Font.FontColor));

        // Dòng tiêu đề cột: nền #D3DDEA, chữ thường Tahoma 9pt, kẻ ô #B8C5D6.
        var header = sheet.Cell(4, 2);
        Assert.Equal("Số chứng từ", header.GetString());
        Assert.Equal("FFD3DDEA", Argb(header.Style.Fill.BackgroundColor));
        Assert.False(header.Style.Font.Bold);
        Assert.Equal(("Tahoma", 9d), (header.Style.Font.FontName, header.Style.Font.FontSize));
        Assert.Equal(XLBorderStyleValues.Thin, header.Style.Border.BottomBorder);
        Assert.Equal("FFB8C5D6", Argb(header.Style.Border.BottomBorderColor));

        // Thân bảng: nền trắng (không tô), kẻ ô đủ dọc ngang; chữ Tahoma.
        var text = sheet.Cell(5, 1);
        Assert.Equal(("Tahoma", XLBorderStyleValues.Thin, XLBorderStyleValues.Thin), (text.Style.Font.FontName, text.Style.Border.RightBorder, text.Style.Border.BottomBorder));
        Assert.Equal("FFB8C5D6", Argb(text.Style.Border.RightBorderColor));
        Assert.Equal(XLFillPatternValues.None, text.Style.Fill.PatternType);

        // Số chứng từ: ô chữ phông đơn cách chữ xanh đậm.
        var docNo = sheet.Cell(5, 2);
        Assert.Equal("PR-TCT-2611-0001", docNo.GetString());
        Assert.Equal(("Consolas", "FF1F3D6B"), (docNo.Style.Font.FontName, Argb(docNo.Style.Font.FontColor)));

        // Ngày canh trái như lưới; tiền là số, phông đơn cách canh phải.
        Assert.Equal(new DateTime(2026, 11, 12), sheet.Cell(5, 3).GetDateTime());
        Assert.Equal(XLAlignmentHorizontalValues.Left, sheet.Cell(5, 3).Style.Alignment.Horizontal);
        var money = sheet.Cell(5, 5);
        Assert.Equal(1_250_000, money.GetDouble());
        Assert.Equal(("Consolas", XLAlignmentHorizontalValues.Right), (money.Style.Font.FontName, money.Style.Alignment.Horizontal));

        // Trạng thái: ô vuông ■ + chữ tô màu theo đèn (vàng: ô #E8C860, chữ #A86F00; xanh lá: #1E8F2E).
        Assert.Equal("■ Chưa hoàn thành", sheet.Cell(5, 4).GetString());
        var warning = sheet.Cell(5, 4).GetRichText().ToList();
        Assert.Equal(("FFE8C860", "FFA86F00"), (Argb(warning[0].FontColor), Argb(warning[1].FontColor)));
        var success = sheet.Cell(6, 4).GetRichText().ToList();
        Assert.Equal(("■ ", "Hoàn thành"), (success[0].Text, success[1].Text));
        Assert.Equal(("FF1E8F2E", "FF1E8F2E"), (Argb(success[0].FontColor), Argb(success[1].FontColor)));

        // Màu đèn lạ hoặc số màu không bằng số dòng → 400.
        var badTone = request with { Columns = [.. request.Columns.Take(3), new("Tình trạng", "status", 130, ["xanh", null]), request.Columns[4]] };
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/excel/grid", badTone, Ct)).StatusCode);
        var shortTones = request with { Columns = [.. request.Columns.Take(3), new("Tình trạng", "status", 130, ["success"]), request.Columns[4]] };
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/excel/grid", shortTones, Ct)).StatusCode);
    }

    [Fact]
    public async Task FileMauDanhMuc_TieuDeCotCungDinhDang_GiuLuoiDeGoThem()
    {
        var userName = $"xl.{Guid.NewGuid():N}"[..16];
        await _factory.CreateUserAsync(userName, ProcApiFactory.TestPassword, ["TCT"]);
        var client = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, "TCT");
        using var workbook = new XLWorkbook(new MemoryStream(await client.GetByteArrayAsync("/api/excel/items?template=true", Ct)));
        var sheet = workbook.Worksheet(1);

        // File mẫu nhập: tiêu đề cột vẫn ở dòng 1 (để nhập lại), cùng nền/viền/phông như lưới; giữ lưới mờ để gõ thêm dòng.
        var header = sheet.Cell(1, 1);
        Assert.False(header.IsEmpty());
        Assert.Equal("FFD3DDEA", Argb(header.Style.Fill.BackgroundColor));
        Assert.Equal(("Tahoma", 9d, false), (header.Style.Font.FontName, header.Style.Font.FontSize, header.Style.Font.Bold));
        Assert.Equal("FFB8C5D6", Argb(header.Style.Border.BottomBorderColor));
        Assert.True(sheet.ShowGridLines);
    }
}
