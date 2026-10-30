using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ClosedXML.Excel;
using Proc.Application.Excel;
using Proc.Application.MasterData;

namespace Proc.IntegrationTests;

/// <summary>
/// Nhập/xuất danh mục bằng Excel (design.md mục 5.10): file mẫu, kiểm tra thử không lưu, có lỗi thì không lưu dòng nào,
/// xuất – sửa – nhập lại, giữ số 0 đầu của mã số thuế.
/// </summary>
[Collection(ApiCollection.Name)]
public class ExcelTransferTests
{
    private readonly ProcApiFactory _factory;

    public ExcelTransferTests(ProcApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string UniqueCode(string prefix) => $"{prefix}{Guid.NewGuid():N}".ToUpperInvariant()[..10];

    private async Task<HttpClient> HeadOfficeAdminAsync()
    {
        var userName = $"qt.{Guid.NewGuid():N}"[..16];
        await _factory.CreateUserAsync(userName, ProcApiFactory.TestPassword, ["TCT", "BHA"]);
        return await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, "TCT");
    }

    /// <summary>Tạo file .xlsx: dòng 1 là tiêu đề, các dòng sau là dữ liệu (ô chữ).</summary>
    private static byte[] Workbook(string sheetName, string[] headers, params string?[][] rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(sheetName);
        for (var c = 0; c < headers.Length; c++) sheet.Cell(1, c + 1).Value = headers[c];
        for (var r = 0; r < rows.Length; r++)
        {
            for (var c = 0; c < rows[r].Length; c++)
            {
                if (rows[r][c] is { } value) sheet.Cell(r + 2, c + 1).SetValue(value);
            }
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static async Task<ExcelImportResult> ImportAsync(HttpClient client, string catalog, byte[] file, bool commit)
    {
        using var content = new MultipartFormDataContent();
        var part = new ByteArrayContent(file);
        part.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        content.Add(part, "file", "import.xlsx");
        var response = await client.PostAsync($"/api/excel/{catalog}/import?commit={commit.ToString().ToLowerInvariant()}", content, Ct);
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(Ct)}");
        return (await response.Content.ReadFromJsonAsync<ExcelImportResult>(Ct))!;
    }

    private static readonly string[] UomHeaders = ["Mã ĐVT *", "Tên ĐVT *", "Còn dùng"];

    private static readonly string[] ItemHeaders =
        ["Mã hàng", "Tên hàng", "Loại hàng", "Mã nhóm hàng", "ĐVT cơ sở", "Thuế suất", "ĐVT quy đổi 1", "Hệ số 1"];

    [Fact]
    public async Task FileMau_CoTieuDeVaHuongDan()
    {
        var head = await HeadOfficeAdminAsync();

        var response = await head.GetAsync("/api/excel/items?template=true", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("mau-hang-hoa.xlsx", response.Content.Headers.ContentDisposition?.FileNameStar ?? response.Content.Headers.ContentDisposition?.FileName);
        using var workbook = new XLWorkbook(await response.Content.ReadAsStreamAsync(Ct));
        var sheet = workbook.Worksheet("Hàng hóa");
        Assert.Equal("Mã hàng *", sheet.Cell(1, 1).GetString());
        Assert.Equal(1, sheet.LastRowUsed()!.RowNumber());
        Assert.True(workbook.Worksheets.Contains("Hướng dẫn"));
    }

    [Fact]
    public async Task KiemTraThu_KhongLuu_NhapThatMoiLuu()
    {
        var head = await HeadOfficeAdminAsync();
        var code = UniqueCode("U")[..8];
        var file = Workbook("Đơn vị tính", UomHeaders, [code.ToLowerInvariant(), "Ram giấy", null]);

        var dryRun = await ImportAsync(head, ExcelCatalogs.Uoms, file, commit: false);
        Assert.Equal((1, 1, 0, false), (dryRun.TotalRows, dryRun.Created, dryRun.Updated, dryRun.Committed));
        Assert.Empty(dryRun.Errors);
        Assert.DoesNotContain((await head.GetFromJsonAsync<List<UomDto>>("/api/uoms", Ct))!, u => u.Code == code);

        var committed = await ImportAsync(head, ExcelCatalogs.Uoms, file, commit: true);
        Assert.True(committed.Committed);
        Assert.Contains((await head.GetFromJsonAsync<List<UomDto>>("/api/uoms", Ct))!, u => u.Code == code && u.Name == "Ram giấy");

        // Nhập lại cùng mã: cập nhật tên, ngừng dùng.
        var again = await ImportAsync(head, ExcelCatalogs.Uoms, Workbook("Đơn vị tính", UomHeaders, [code, "Ram", "Không"]), commit: true);
        Assert.Equal((0, 1), (again.Created, again.Updated));
        Assert.Contains((await head.GetFromJsonAsync<List<UomDto>>("/api/uoms", Ct))!, u => u.Code == code && u.Name == "Ram" && !u.IsActive);
    }

    [Fact]
    public async Task CoLoi_BaoTheoDong_KhongLuuDongNaoKeCaDongDung()
    {
        var head = await HeadOfficeAdminAsync();
        var category = await (await head.PostAsJsonAsync("/api/item-categories", new CreateItemCategoryRequest(UniqueCode("R"), "Nhóm Excel", null, null), Ct))
            .Content.ReadFromJsonAsync<ItemCategoryDetailDto>(Ct);
        var good = $"{category!.Code}-01";
        var file = Workbook("Hàng hóa", ItemHeaders,
            [good, "Bu lông M8", "Hàng tồn kho", category.Code, "HOP", "10%", "THUNG", "24"],
            [$"{category.Code}-02", "Đai ốc", "Hàng hoá", category.Code, "HOP", null, null, null],
            [$"{category.Code}-03", "Vòng đệm", "Dịch vụ", "KHONGCO", "HOP", null, null, null],
            [$"{category.Code}-04", "Long đền", "Hàng tồn kho", category.Code, "HOP", null, "THUNG", "abc"],
            [good, "Trùng mã", "Hàng tồn kho", category.Code, "HOP", null, null, null]);

        var result = await ImportAsync(head, ExcelCatalogs.Items, file, commit: true);

        Assert.False(result.Committed);
        Assert.Equal([3, 4, 5, 6], result.Errors.Select(e => e.Row));
        Assert.Contains("Loại hàng", result.Errors[0].Message);
        Assert.Contains("KHONGCO", result.Errors[1].Message);
        Assert.Contains("Hệ số 1", result.Errors[2].Message);
        Assert.Equal($"Mã {good} trùng với dòng 2.", result.Errors[3].Message);
        // Dòng 2 hợp lệ nhưng cả file có lỗi nên cũng không được lưu.
        var items = await head.GetFromJsonAsync<ItemListResult>($"/api/items?search={good}", Ct);
        Assert.Empty(items!.Items);
    }

    [Fact]
    public async Task XuatSuaNhapLai_CapNhatHangVaGiuQuyDoi()
    {
        var head = await HeadOfficeAdminAsync();
        var category = await (await head.PostAsJsonAsync("/api/item-categories", new CreateItemCategoryRequest(UniqueCode("R"), "Nhóm Excel", null, null), Ct))
            .Content.ReadFromJsonAsync<ItemCategoryDetailDto>(Ct);
        var code = $"{category!.Code}-01";
        Assert.True((await ImportAsync(head, ExcelCatalogs.Items, Workbook("Hàng hóa", ItemHeaders,
            [code, "Bu lông M8", "Hàng tồn kho", category.Code, "HOP", "10", "THUNG", "24"]), commit: true)).Committed);

        // Xuất toàn bộ, sửa tên đúng dòng của mặt hàng, nhập lại.
        using var exported = new XLWorkbook(new MemoryStream(await head.GetByteArrayAsync("/api/excel/items", Ct)));
        var sheet = exported.Worksheet("Hàng hóa");
        var row = sheet.RowsUsed().Single(r => r.Cell(1).GetString() == code);
        Assert.Equal("THUNG", row.Cell(16).GetString());
        row.Cell(2).Value = "Bu lông M8 mạ kẽm";
        using var stream = new MemoryStream();
        exported.SaveAs(stream);

        var result = await ImportAsync(head, ExcelCatalogs.Items, stream.ToArray(), commit: true);

        Assert.True(result.Committed, string.Join("; ", result.Errors.Select(e => $"{e.Row}: {e.Message}")));
        var item = (await head.GetFromJsonAsync<ItemListResult>($"/api/items?search={code}", Ct))!.Items.Single();
        Assert.Equal("Bu lông M8 mạ kẽm", item.Name);
        var detail = await head.GetFromJsonAsync<ItemDetailDto>($"/api/items/{item.Id}", Ct);
        Assert.Equal(("THUNG", 24m), (Assert.Single(detail!.Conversions).UomCode, detail.Conversions[0].Factor));
    }

    [Fact]
    public async Task Ncc_ThemCoLienHeVaNganHang_GiuSo0DauCuaMaSoThue()
    {
        var head = await HeadOfficeAdminAsync();
        var code = UniqueCode("N");
        var taxCode = $"0{Random.Shared.NextInt64(100_000_000, 999_999_999)}";
        var file = Workbook("Nhà cung cấp",
            ["Mã NCC", "Tên NCC", "Mã nhóm NCC", "Mã số thuế", "Trạng thái", "Người liên hệ chính", "ĐT người liên hệ", "Ngân hàng", "Số tài khoản"],
            [code, "Công ty Excel", "TN", taxCode, "Đã duyệt", "Võ Thị Lan", "0909 111 222", "ACB", "0123 4567"]);

        var result = await ImportAsync(head, ExcelCatalogs.Suppliers, file, commit: true);

        Assert.True(result.Committed, string.Join("; ", result.Errors.Select(e => e.Message)));
        var row = (await head.GetFromJsonAsync<SupplierListResult>($"/api/suppliers?search={code}", Ct))!.Items.Single();
        Assert.Equal((taxCode, "Approved", "Võ Thị Lan – 0909 111 222"), (row.TaxCode, row.Status, row.PrimaryContact));
        var detail = await head.GetFromJsonAsync<SupplierDetailDto>($"/api/suppliers/{row.Id}", Ct);
        Assert.Equal("01234567", Assert.Single(detail!.BankAccounts).AccountNumber);
    }

    [Fact]
    public async Task CongTyThanhVien_BaoMotLoi_FileSaiDinhDangVaThieuCot()
    {
        var bha = await _factory.CreateClientInCompanyAsync(ProcApiFactory.AdminUserName, ProcApiFactory.AdminPassword, "BHA");
        var file = Workbook("Đơn vị tính", UomHeaders, [UniqueCode("U")[..8], "A"], [UniqueCode("U")[..8], "B"]);
        var forbidden = await ImportAsync(bha, ExcelCatalogs.Uoms, file, commit: false);
        Assert.Equal(("Chỉ thêm, sửa đơn vị tính khi đang làm việc ở tổng công ty.", 0), (Assert.Single(forbidden.Errors).Message, forbidden.Errors[0].Row));

        var head = await HeadOfficeAdminAsync();
        var notExcel = await ImportAsync(head, ExcelCatalogs.Uoms, "không phải excel"u8.ToArray(), commit: false);
        Assert.StartsWith("Không đọc được file", Assert.Single(notExcel.Errors).Message);

        var missing = await ImportAsync(head, ExcelCatalogs.Uoms, Workbook("Đơn vị tính", ["Mã ĐVT"], ["X1"]), commit: false);
        Assert.Contains("Tên ĐVT", Assert.Single(missing.Errors).Message);

        Assert.Equal(HttpStatusCode.NotFound, (await head.GetAsync("/api/excel/khong-co", Ct)).StatusCode);
    }
}
