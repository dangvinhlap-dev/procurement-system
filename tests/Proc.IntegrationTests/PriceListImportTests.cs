using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ClosedXML.Excel;
using Proc.Application.MasterData;
using Proc.Application.Pricing;
using Proc.Domain.Organization;

namespace Proc.IntegrationTests;

/// <summary>
/// Nhập Excel bảng giá NCC (design.md mục 8.5): mỗi dòng một bậc giá, gộp theo mã NCC + tên bảng giá; nhập thành bảng giá nháp qua đúng
/// quy tắc lập tay; có lỗi thì không lưu gì.
/// </summary>
public partial class SupplierPricingTests
{
    private static readonly string[] ImportHeaders =
    [
        "Mã NCC", "Tên bảng giá", "Toàn tập đoàn", "Hiệu lực từ", "Hiệu lực đến", "Điều kiện giao hàng", "Ghi chú",
        "Mã hàng", "ĐVT", "SL từ", "Đơn giá", "% CK", "Thuế suất", "Ghi chú dòng",
    ];

    /// <summary>File Excel một sheet, dòng 1 là tiêu đề <see cref="ImportHeaders"/>.</summary>
    private static byte[] PriceListWorkbook(params object?[][] rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Bảng giá");
        for (var c = 0; c < ImportHeaders.Length; c++) sheet.Cell(1, c + 1).Value = ImportHeaders[c];
        for (var r = 0; r < rows.Length; r++)
        for (var c = 0; c < rows[r].Length; c++)
        {
            var cell = sheet.Cell(r + 2, c + 1);
            switch (rows[r][c])
            {
                case null: break;
                case decimal d: cell.Value = d; break;
                case int i: cell.Value = i; break;
                case DateOnly date: cell.Value = date.ToDateTime(TimeOnly.MinValue); break;
                case var v: cell.SetValue(v.ToString()); break;
            }
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static async Task<HttpResponseMessage> PostPriceListFileAsync(HttpClient client, byte[] file, bool commit)
    {
        using var content = new MultipartFormDataContent();
        var part = new ByteArrayContent(file);
        part.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        content.Add(part, "file", "bang-gia.xlsx");
        return await client.PostAsync($"/api/price-lists/import?commit={commit.ToString().ToLowerInvariant()}", content, Ct);
    }

    private static async Task<PriceListImportResult> ImportPriceListsAsync(HttpClient client, byte[] file, bool commit) =>
        await ReadAsync<PriceListImportResult>(await PostPriceListFileAsync(client, file, commit));

    /// <summary>Mã NCC, mã hàng của fixture – các cột tra theo mã trên file.</summary>
    private static async Task<(string Supplier, string Item)> CodesAsync(Fixture f) =>
        ((await ReadAsync<SupplierDetailDto>(await f.Head.GetAsync($"/api/suppliers/{f.SupplierId}", Ct))).Code,
            (await ReadAsync<ItemDetailDto>(await f.Head.GetAsync($"/api/items/{f.ItemId}", Ct))).Code);

    [Fact]
    public async Task NhapExcel_BangGia_KiemTraThuRoiNhapThanhNhap()
    {
        var f = await ArrangeAsync();
        var (supplier, item) = await CodesAsync(f);
        var title = $"Bảng giá nhập {Guid.NewGuid():N}"[..30];
        // Dòng 2 ghi đủ đầu bảng giá; dòng 3–4 chỉ ghi mã NCC + tên (cột đầu để trống = như dòng đầu). Bậc SL từ trống = 0.
        var file = PriceListWorkbook(
            [supplier, title, "Có", Today, Today.AddMonths(12), "Giao tại kho", "Giá năm 2026", item, "CAI", null, 50_000m, 5m, null, "Giá lẻ"],
            [supplier, title, null, null, null, null, null, item, "CAI", 100, 48_000m, null, "8", null],
            [supplier, title, null, null, null, null, null, item, "THUNG", null, 600_000m, null, null, null]);

        // Kiểm tra thử: hợp lệ nhưng không lưu gì.
        var check = await ImportPriceListsAsync(f.Head, file, commit: false);
        Assert.Equal((3, 1, 3, 0, false), (check.TotalRows, check.PriceLists, check.Lines, check.Errors.Count, check.Committed));
        var list = await ReadAsync<List<PriceListListItemDto>>(await f.Head.GetAsync("/api/price-lists", Ct));
        Assert.DoesNotContain(list, p => p.Title == title);

        // Nhập thật: bảng giá nháp toàn tập đoàn của công ty đang làm việc, đủ bậc giá.
        var done = await ImportPriceListsAsync(f.Head, file, commit: true);
        Assert.Equal((1, 3, true), (done.PriceLists, done.Lines, done.Committed));
        list = await ReadAsync<List<PriceListListItemDto>>(await f.Head.GetAsync("/api/price-lists", Ct));
        var row = Assert.Single(list, p => p.Title == title);
        var priceList = await ReadAsync<PriceListDto>(await f.Head.GetAsync($"/api/price-lists/{row.Id}", Ct));
        Assert.Equal(("Draft", true, Today, Today.AddMonths(12), "Giao tại kho", "Giá năm 2026"),
            (priceList.Status, priceList.IsGroupWide, priceList.ValidFrom, priceList.ValidTo, priceList.DeliveryTerms, priceList.Note));
        Assert.Equal(
            [("CAI", 0m, 50_000m, 5m, (string?)null, "Giá lẻ"), ("CAI", 100m, 48_000m, 0m, "8", null), ("THUNG", 0m, 600_000m, 0m, null, null)],
            priceList.Lines.Select(l => (l.UomCode, l.MinQty, l.UnitPrice, l.DiscountPct, l.VatCode, l.Note)).ToList());

        // Nhập lại cùng file: bảng giá cùng NCC, cùng tên, cùng ngày hiệu lực đã có thì báo lỗi, không tạo trùng.
        var again = await ImportPriceListsAsync(f.Head, file, commit: true);
        Assert.False(again.Committed);
        Assert.Contains(again.Errors, e => e.Row == 2 && e.Message.Contains(priceList.No));
    }

    [Fact]
    public async Task NhapExcel_BangGia_LoiTheoDong_KhongLuuGi_VaCanQuyenSuaBangGia()
    {
        var f = await ArrangeAsync();
        var (supplier, item) = await CodesAsync(f);
        var (ok, badVat, mismatch, badItem) = ("BG đúng " + UniqueCode("A"), "BG sai thuế " + UniqueCode("B"), "BG lệch " + UniqueCode("C"), "BG hàng lạ " + UniqueCode("D"));
        var file = PriceListWorkbook(
            [supplier, ok, null, Today, null, null, null, item, "CAI", null, 10_000m, null, null, null],
            // Dòng 3–4: thuế suất 7% không hợp lệ ở dòng thứ hai của bảng – lỗi chỉ đúng dòng Excel 4.
            [supplier, badVat, null, Today, null, null, null, item, "CAI", null, 10_000m, null, null, null],
            [supplier, badVat, null, null, null, null, null, item, "THUNG", null, 100_000m, null, "7", null],
            // Dòng 5–6: dòng sau ghi ngày hiệu lực khác dòng đầu của cùng bảng giá.
            [supplier, mismatch, null, Today, null, null, null, item, "CAI", null, 10_000m, null, null, null],
            [supplier, mismatch, null, Today.AddDays(1), null, null, null, item, "THUNG", null, 100_000m, null, null, null],
            // Dòng 7: mã hàng không có; dòng 8: mã NCC không có.
            [supplier, badItem, null, Today, null, null, null, "KHONGCO", "CAI", null, 10_000m, null, null, null],
            ["KHONGCO", ok, null, Today, null, null, null, item, "CAI", null, 10_000m, null, null, null]);

        var result = await ImportPriceListsAsync(f.Head, file, commit: true);
        Assert.False(result.Committed);
        Assert.Equal(4, result.Errors.Count);
        Assert.Contains(result.Errors, e => e.Row == 4 && e.Message.Contains("thuế suất"));
        Assert.Contains(result.Errors, e => e.Row == 6 && e.Message.Contains("Hiệu lực từ"));
        Assert.Contains(result.Errors, e => e.Row == 7 && e.Message.Contains("KHONGCO"));
        Assert.Contains(result.Errors, e => e.Row == 8 && e.Message.Contains("KHONGCO"));
        // Bảng giá đúng cũng không được lưu vì file còn lỗi.
        var list = await ReadAsync<List<PriceListListItemDto>>(await f.Head.GetAsync("/api/price-lists", Ct));
        Assert.DoesNotContain(list, p => p.Title == ok);

        // Người chỉ xem + duyệt bảng giá không nhập được, không tải được mẫu.
        var roleCode = UniqueCode("G");
        await _factory.CreateRoleAsync(roleCode, Permissions.Pricing.PriceListView, Permissions.Pricing.PriceListApprove);
        var userName = $"dg.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(userName, "TCT", roleCode);
        var approver = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, "TCT");
        Assert.Equal(HttpStatusCode.Forbidden, (await PostPriceListFileAsync(approver, file, commit: false)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await approver.GetAsync("/api/price-lists/import-template", Ct)).StatusCode);

        // File mẫu có đủ cột bắt buộc ở sheet Bảng giá.
        var template = await f.Head.GetAsync("/api/price-lists/import-template", Ct);
        Assert.Equal(HttpStatusCode.OK, template.StatusCode);
        using var workbook = new XLWorkbook(await template.Content.ReadAsStreamAsync(Ct));
        var headers = workbook.Worksheet("Bảng giá").Row(1).CellsUsed().Select(c => c.GetString()).ToList();
        Assert.Contains("Mã NCC *", headers);
        Assert.Contains("Đơn giá *", headers);
    }
}
