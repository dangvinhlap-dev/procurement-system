using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ClosedXML.Excel;
using Proc.Application.MasterData;
using Proc.Application.Pricing;
using Proc.Application.Purchasing;
using Proc.Domain.Organization;

namespace Proc.IntegrationTests;

/// <summary>
/// Nhập Excel hợp đồng nguyên tắc hiện hữu (lát B4c – design.md mục 18.7): mỗi dòng một dòng sản lượng, gộp theo số HĐ giấy; nhập thành
/// hợp đồng nháp qua đúng quy tắc lập tay; có lỗi thì không lưu gì.
/// </summary>
public partial class ContractTests
{
    private static readonly string[] ImportHeaders =
    [
        "Số HĐ giấy", "Mã NCC", "Số bảng giá", "Tên hợp đồng", "Ngày ký", "Hiệu lực từ", "Hiệu lực đến", "Hạn mức công nợ", "Thanh toán",
        "Bộ phận được đặt", "Mã hàng", "ĐVT", "SL dự kiến", "SL cam kết", "Ghi chú dòng",
    ];

    /// <summary>File Excel một sheet, dòng 1 là tiêu đề <see cref="ImportHeaders"/>.</summary>
    private static byte[] ContractWorkbook(params object?[][] rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Hợp đồng");
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

    private static async Task<HttpResponseMessage> PostContractFileAsync(HttpClient client, byte[] file, bool commit)
    {
        using var content = new MultipartFormDataContent();
        var part = new ByteArrayContent(file);
        part.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        content.Add(part, "file", "hop-dong.xlsx");
        return await client.PostAsync($"/api/contracts/import?commit={commit.ToString().ToLowerInvariant()}", content, Ct);
    }

    private static async Task<ContractImportResult> ImportContractsAsync(HttpClient client, byte[] file, bool commit) =>
        await ReadAsync<ContractImportResult>(await PostContractFileAsync(client, file, commit));

    /// <summary>Mã NCC, mã hàng, số bảng giá của fixture – các cột tra theo mã trên file.</summary>
    private async Task<(string Supplier, string Item, string PriceList)> CodesAsync(Fixture f) =>
        ((await ReadAsync<SupplierDetailDto>(await f.Head.GetAsync($"/api/suppliers/{f.SupplierId}", Ct))).Code,
            (await ReadAsync<ItemDetailDto>(await f.Head.GetAsync($"/api/items/{f.ItemId}", Ct))).Code,
            (await ReadAsync<PriceListDto>(await f.Head.GetAsync($"/api/price-lists/{f.GroupPriceListId}", Ct))).No);

    [Fact]
    public async Task NhapExcel_HopDongHienHuu_KiemTraThuRoiNhapThanhNhap()
    {
        var f = await ArrangeAsync();
        var (supplier, item, priceList) = await CodesAsync(f);
        var paperNo = UniqueCode("HD");
        // Dòng 2 ghi đủ đầu hợp đồng; dòng 3 chỉ ghi số HĐ giấy (cột đầu để trống = như dòng đầu).
        var file = ContractWorkbook(
            [paperNo, supplier, priceList, "Hợp đồng vòng bi 2026", Today.AddMonths(-2), Today.AddMonths(-2), Today.AddMonths(10), 500_000_000m, "30 ngày", "PX1", item, "CAI", 1_000, 200, "Dự kiến năm"],
            [paperNo, null, null, null, null, null, null, null, null, null, item, "THUNG", 10, null, null]);

        // Kiểm tra thử: hợp lệ nhưng không lưu gì.
        var check = await ImportContractsAsync(f.Client, file, commit: false);
        Assert.Equal((2, 1, 2, 0, false), (check.TotalRows, check.Contracts, check.Lines, check.Errors.Count, check.Committed));
        var list = await ReadAsync<List<ContractListItemDto>>(await f.Client.GetAsync("/api/contracts", Ct));
        Assert.DoesNotContain(list, c => c.ContractNo == paperNo);

        // Nhập thật: hợp đồng nháp, giá theo bảng giá (1.000 × 50.000 × 95% + 10 × 600.000 = 53.500.000), bộ phận theo mã.
        var done = await ImportContractsAsync(f.Client, file, commit: true);
        Assert.Equal((1, 2, true), (done.Contracts, done.Lines, done.Committed));
        list = await ReadAsync<List<ContractListItemDto>>(await f.Client.GetAsync("/api/contracts", Ct));
        var row = Assert.Single(list, c => c.ContractNo == paperNo);
        var contract = await ReadAsync<ContractDto>(await f.Client.GetAsync($"/api/contracts/{row.Id}", Ct));
        Assert.Equal(("Draft", 53_500_000m, 500_000_000m, "30 ngày", Today.AddMonths(-2)),
            (contract.Status, contract.ExpectedAmount, contract.CreditLimit, contract.PaymentTerms, contract.ValidFrom));
        Assert.Equal(["PX1 – Phân xưởng 1"], contract.DepartmentNames);
        Assert.Equal([(1_000m, 200m), (10m, (decimal?)null)], contract.Lines.Select(l => (l.ExpectedQty, l.MinCommitQty)).ToList());

        // Nhập lại cùng file: số HĐ giấy đã có với NCC này thì báo lỗi, không tạo trùng.
        var again = await ImportContractsAsync(f.Client, file, commit: true);
        Assert.False(again.Committed);
        Assert.Contains(again.Errors, e => e.Row == 2 && e.Code == paperNo && e.Message.Contains(contract.No));
    }

    [Fact]
    public async Task NhapExcel_LoiTheoDong_KhongLuuGi_VaCanQuyenLapHopDong()
    {
        var f = await ArrangeAsync();
        var (supplier, item, priceList) = await CodesAsync(f);
        var (ok, wrongUom, mismatch, noSupplier) = (UniqueCode("HA"), UniqueCode("HB"), UniqueCode("HC"), UniqueCode("HD"));
        var kg = (await f.Head.GetFromJsonAsync<List<UomDto>>("/api/uoms", Ct))!.First(u => u.Code is not "CAI" and not "THUNG").Code;
        var file = ContractWorkbook(
            [ok, supplier, priceList, "Hợp đồng đúng", null, Today, Today.AddMonths(6), null, null, null, item, "CAI", 100, null, null],
            // Dòng 3–4: ĐVT không có trong bảng giá – lỗi chỉ đúng dòng Excel 4.
            [wrongUom, supplier, priceList, "Hợp đồng sai ĐVT", null, Today, Today.AddMonths(6), null, null, null, item, "CAI", 100, null, null],
            [wrongUom, null, null, null, null, null, null, null, null, null, item, kg, 5, null, null],
            // Dòng 5–6: dòng sau ghi NCC khác dòng đầu của cùng hợp đồng.
            [mismatch, supplier, priceList, "Hợp đồng lệch", null, Today, Today.AddMonths(6), null, null, null, item, "CAI", 100, null, null],
            [mismatch, "KHONGCO", null, null, null, null, null, null, null, null, item, "THUNG", 1, null, null],
            // Dòng 7: mã NCC không có.
            [noSupplier, "KHONGCO", priceList, "Hợp đồng NCC lạ", null, Today, Today.AddMonths(6), null, null, null, null, null, null, null, null]);

        var result = await ImportContractsAsync(f.Client, file, commit: true);
        Assert.False(result.Committed);
        Assert.Equal(3, result.Errors.Count);
        Assert.Contains(result.Errors, e => e.Row == 4 && e.Code == wrongUom && e.Message.Contains("không có trong bảng giá"));
        Assert.Contains(result.Errors, e => e.Row == 6 && e.Code == mismatch && e.Message.Contains("Mã NCC"));
        Assert.Contains(result.Errors, e => e.Row == 7 && e.Code == noSupplier && e.Message.Contains("KHONGCO"));
        // Hợp đồng đúng cũng không được lưu vì file còn lỗi.
        var list = await ReadAsync<List<ContractListItemDto>>(await f.Client.GetAsync("/api/contracts", Ct));
        Assert.DoesNotContain(list, c => c.ContractNo == ok);

        // Người duyệt (chỉ xem + duyệt) không nhập được, không tải được mẫu.
        var approver = await ApproverAsync(f);
        Assert.Equal(HttpStatusCode.Forbidden, (await PostContractFileAsync(approver, file, commit: false)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await approver.GetAsync("/api/contracts/import-template", Ct)).StatusCode);

        // File mẫu có đủ cột bắt buộc ở sheet Hợp đồng.
        var template = await f.Client.GetAsync("/api/contracts/import-template", Ct);
        Assert.Equal(HttpStatusCode.OK, template.StatusCode);
        using var workbook = new XLWorkbook(await template.Content.ReadAsStreamAsync(Ct));
        var headers = workbook.Worksheet("Hợp đồng").Row(1).CellsUsed().Select(c => c.GetString()).ToList();
        Assert.Contains("Số HĐ giấy *", headers);
        Assert.Contains("Mã hàng", headers);
    }
}
