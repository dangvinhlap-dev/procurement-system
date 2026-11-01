using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ClosedXML.Excel;
using Proc.Application.Analysis;
using Proc.Application.Logging;
using Proc.Application.MasterData;
using Proc.Domain.Organization;

namespace Proc.IntegrationTests;

/// <summary>
/// Dữ liệu mua lịch sử (design.md mục 18.1, lát E1): file mẫu, kiểm tra thử không lưu, một dòng lỗi thì cả file không lưu, nhập thật
/// ghi bằng COPY, tự khớp mã vật tư/MST, phạm vi công ty, xóa lô có nhật ký, quyền.
/// </summary>
[Collection(ApiCollection.Name)]
public class PurchaseHistoryTests
{
    private static readonly string[] Headers =
        ["Bộ phận sử dụng", "Mã vật tư *", "Tên vật tư *", "ĐVT *", "Ngày yêu cầu", "Ngày mua *", "Số lượng *", "Đơn giá trước VAT *",
         "% VAT", "Tên NCC *", "MST NCC", "Đúng hạn", "Đạt chất lượng"];

    private readonly ProcApiFactory _factory;

    public PurchaseHistoryTests(ProcApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string Today => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7)).ToString("yyyy-MM-dd");

    private async Task<(HttpClient Head, HttpClient Bha)> AdminAsync()
    {
        var userName = $"ph.{Guid.NewGuid():N}"[..16];
        await _factory.CreateUserAsync(userName, ProcApiFactory.TestPassword, ["TCT", "BHA"]);
        return (await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, "TCT"),
            await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, "BHA"));
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(Ct)}");
        return (await response.Content.ReadFromJsonAsync<T>(Ct))!;
    }

    /// <summary>File .xlsx: dòng 1 tiêu đề, các dòng sau là ô chữ.</summary>
    private static byte[] Workbook(params string?[][] rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Dữ liệu mua");
        for (var c = 0; c < Headers.Length; c++) sheet.Cell(1, c + 1).Value = Headers[c];
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

    private static string?[] Row(string itemCode, string date = "15/03/2026", string quantity = "10", string price = "1.234,5", string? taxCode = null) =>
        ["Phân xưởng cán", itemCode, "Bu lông M8", "Cái", null, date, quantity, price, "10", "Công ty Thép Việt", taxCode, "Có", "khong"];

    private static async Task<PurchaseHistoryImportResult> ImportAsync(HttpClient client, byte[] file, bool commit)
    {
        using var content = new MultipartFormDataContent();
        var part = new ByteArrayContent(file);
        part.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        content.Add(part, "file", "mua-2025.xlsx");
        return await ReadAsync<PurchaseHistoryImportResult>(
            await client.PostAsync($"/api/purchase-history/import?commit={commit.ToString().ToLowerInvariant()}", content, Ct));
    }

    private static async Task<PurchaseHistoryListResult> ListAsync(HttpClient client, string query = "") =>
        (await client.GetFromJsonAsync<PurchaseHistoryListResult>($"/api/purchase-history?{query}", Ct))!;

    /// <summary>Mặt hàng mới ở danh mục chung (tạo ở tổng công ty).</summary>
    private static async Task<ItemDetailDto> NewItemAsync(HttpClient head)
    {
        var uoms = (await head.GetFromJsonAsync<List<UomDto>>("/api/uoms", Ct))!.ToDictionary(u => u.Code, u => u.Id);
        var category = await ReadAsync<ItemCategoryDetailDto>(await head.PostAsJsonAsync("/api/item-categories",
            new CreateItemCategoryRequest(null, "Nhóm thử mua lịch sử", null, null), Ct));
        return await ReadAsync<ItemDetailDto>(await head.PostAsJsonAsync("/api/items",
            new CreateItemRequest(null, "Bu lông M8", null, "Inventory", category.Id, uoms["HOP"], null, null, null,
                null, null, null, false, false, null, null, []), Ct));
    }

    /// <summary>NCC mới có MST ngẫu nhiên 10 chữ số.</summary>
    private static async Task<SupplierDetailDto> NewSupplierAsync(HttpClient head)
    {
        var taxCode = string.Concat(Guid.NewGuid().ToByteArray().Take(10).Select(b => (char)('0' + b % 10)));
        var groupId = (await head.GetFromJsonAsync<List<SupplierGroupDto>>("/api/supplier-groups", Ct))!.Single(g => g.Code == "TN").Id;
        return await ReadAsync<SupplierDetailDto>(await head.PostAsJsonAsync("/api/suppliers",
            new SaveSupplierRequest(null, groupId, $"NCC thử {Guid.NewGuid():N}"[..20], null, false, taxCode, null, null, null,
                null, null, null, null, "Approved", null, null, [], []), Ct));
    }

    [Fact]
    public async Task FileMau_CoTieuDeVaHuongDan()
    {
        var (_, bha) = await AdminAsync();

        var response = await bha.GetAsync("/api/purchase-history/template", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var workbook = new XLWorkbook(await response.Content.ReadAsStreamAsync(Ct));
        var sheet = workbook.Worksheet("Dữ liệu mua");
        Assert.Equal("Bộ phận sử dụng", sheet.Cell(1, 1).GetString());
        Assert.Equal("Mã vật tư *", sheet.Cell(1, 2).GetString());
        Assert.True(workbook.Worksheets.Contains("Hướng dẫn"));
    }

    [Fact]
    public async Task KiemTraThu_KhongLuu_NhapThat_TuKhopMaVaMst()
    {
        var (head, bha) = await AdminAsync();
        var item = await NewItemAsync(head);
        var supplier = await NewSupplierAsync(head);
        var unknown = $"CU-{Guid.NewGuid():N}"[..12];
        var file = Workbook(Row(item.Code.ToLowerInvariant(), taxCode: supplier.TaxCode), Row(unknown, quantity: "2.5", price: "1000"));

        var check = await ImportAsync(bha, file, commit: false);

        Assert.Empty(check.Errors);
        Assert.Equal((2, 2, 1, 1, false), (check.TotalRows, check.ValidRows, check.MatchedItems, check.MatchedSuppliers, check.Committed));
        Assert.Empty((await ListAsync(bha, $"search={unknown}")).Rows);

        var done = await ImportAsync(bha, file, commit: true);

        Assert.True(done.Committed);
        var rows = (await ListAsync(bha, $"batchId={done.BatchId}")).Rows;
        Assert.Equal(2, rows.Count);
        var matched = rows.Single(r => r.ItemCode == item.Code);
        Assert.Equal((item.Id, item.Code, supplier.Id, supplier.Code), (matched.ItemId, matched.MatchedItemCode, matched.SupplierId, matched.MatchedSupplierCode));
        // 10 × 1.234,5 = 12.345,00; số kiểu Việt Nam đọc đúng.
        Assert.Equal((10m, 1234.5m, 12345m, "VND", true, false), (matched.Quantity, matched.UnitPrice, matched.Amount, matched.Currency, matched.OnTime, matched.QualityPassed));
        var other = rows.Single(r => r.ItemCode == unknown.ToUpperInvariant());
        Assert.Equal((2.5m, (Guid?)null, (Guid?)null), (other.Quantity, other.ItemId, other.SupplierId));

        // Lọc chưa khớp / tìm không dấu.
        Assert.Equal([unknown.ToUpperInvariant()], (await ListAsync(bha, $"batchId={done.BatchId}&match=unmatched")).Rows.Select(r => r.ItemCode));
        Assert.Equal(2, (await ListAsync(bha, $"batchId={done.BatchId}&search=bu%20long")).Rows.Count);

        var batch = Assert.Single((await bha.GetFromJsonAsync<List<PurchaseHistoryBatchDto>>("/api/purchase-history/batches", Ct))!, b => b.Id == done.BatchId);
        Assert.Equal(("mua-2025.xlsx", 2, new DateOnly(2026, 3, 15)), (batch.FileName, batch.RecordCount, batch.FromDate));
        Assert.NotNull(batch.ImportedBy);
    }

    [Fact]
    public async Task MotDongLoi_CaFileKhongLuu_BaoTheoSoDong()
    {
        var (_, bha) = await AdminAsync();
        var code = $"LOI-{Guid.NewGuid():N}"[..12];
        var file = Workbook(Row(code), Row(code, quantity: "0"), Row(code, date: "31/02/2026"), Row(code, price: "abc"));

        var result = await ImportAsync(bha, file, commit: true);

        Assert.False(result.Committed);
        Assert.Equal([3, 4, 5], result.Errors.Select(e => e.Row));
        Assert.Contains("Số lượng phải lớn hơn 0", result.Errors[0].Message);
        Assert.Empty((await ListAsync(bha, $"search={code}")).Rows);
    }

    [Fact]
    public async Task ThieuCotBatBuoc_BaoLoiCaFile()
    {
        var (_, bha) = await AdminAsync();
        using var workbook = new XLWorkbook();
        workbook.Worksheets.Add("Dữ liệu mua").Cell(1, 1).Value = "Mã vật tư";
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);

        var result = await ImportAsync(bha, stream.ToArray(), commit: false);

        var error = Assert.Single(result.Errors);
        Assert.Contains("thiếu cột bắt buộc", error.Message);
    }

    [Fact]
    public async Task PhamViCongTy_CongTyKhacKhongThayKhongXoaDuoc()
    {
        var (head, bha) = await AdminAsync();
        var code = $"PV-{Guid.NewGuid():N}"[..12];
        var done = await ImportAsync(bha, Workbook(Row(code)), commit: true);

        // Mỗi pháp nhân là độc lập: tổng công ty không thấy dữ liệu BHA nhập, không xóa được lô của BHA.
        Assert.Empty((await ListAsync(head, $"search={code}")).Rows);
        Assert.DoesNotContain((await head.GetFromJsonAsync<List<PurchaseHistoryBatchDto>>("/api/purchase-history/batches", Ct))!, b => b.Id == done.BatchId);
        Assert.Equal(HttpStatusCode.NotFound, (await head.DeleteAsync($"/api/purchase-history/batches/{done.BatchId}", Ct)).StatusCode);
        Assert.Single((await ListAsync(bha, $"search={code}")).Rows);
    }

    [Fact]
    public async Task XoaLo_XoaMoiDong_GhiNhatKy()
    {
        var (_, bha) = await AdminAsync();
        var code = $"XL-{Guid.NewGuid():N}"[..12];
        var done = await ImportAsync(bha, Workbook(Row(code), Row(code)), commit: true);

        var response = await bha.DeleteAsync($"/api/purchase-history/batches/{done.BatchId}", Ct);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty((await ListAsync(bha, $"search={code}")).Rows);
        var audit = (await bha.GetFromJsonAsync<LogPage<AuditLogItemDto>>(
            $"/api/audit-logs?from={Today}&to={Today}&entityType=PurchaseHistoryBatch", Ct))!.Items;
        Assert.Contains(audit, a => a.EntityId == done.BatchId.ToString() && a.Action == "Create");
        Assert.Contains(audit, a => a.EntityId == done.BatchId.ToString() && a.Action == "Delete");
    }

    [Fact]
    public async Task ChiQuyenXem_KhongNhapDuoc()
    {
        var roleCode = $"PHV{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        await _factory.CreateRoleAsync(roleCode, Permissions.Analysis.PurchaseHistoryView);
        var userName = $"pv.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(userName, "BHA", roleCode);
        var viewer = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, "BHA");

        Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync("/api/purchase-history", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.GetAsync("/api/purchase-history/template", Ct)).StatusCode);
        using var content = new MultipartFormDataContent { { new ByteArrayContent(Workbook(Row("X1"))), "file", "a.xlsx" } };
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsync("/api/purchase-history/import?commit=true", content, Ct)).StatusCode);
    }
}
