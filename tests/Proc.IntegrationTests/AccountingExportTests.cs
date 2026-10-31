using System.Net;
using System.Net.Http.Json;
using ClosedXML.Excel;
using Proc.Application.Common;
using Proc.Application.Inventory;
using Proc.Application.MasterData;
using Proc.Application.Organization;
using Proc.Application.Reports;
using Proc.Domain.Organization;

namespace Proc.IntegrationTests;

/// <summary>
/// Xuất chứng từ kho đã ghi sổ ra Excel cho kế toán (design.md mục 11.5): chỉ phiếu đã ghi sổ; mã NCC tại công ty, MST, TK kho theo nhóm
/// hàng (kế thừa nhóm cha), giá vốn theo sổ kho; lọc loại chứng từ; cần quyền riêng.
/// </summary>
[Collection(ApiCollection.Name)]
public class AccountingExportTests
{
    private readonly ProcApiFactory _factory;

    public AccountingExportTests(ProcApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string UniqueCode(string prefix) => $"{prefix}{Guid.NewGuid():N}".ToUpperInvariant()[..10];

    private static DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTime.UtcNow, VietnamTime.Zone));

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(Ct)}");
        return (await response.Content.ReadFromJsonAsync<T>(Ct))!;
    }

    private static async Task<InventoryDocumentDto> PostAsync(HttpClient client, HttpResponseMessage created)
    {
        var doc = await ReadAsync<InventoryDocumentDto>(created);
        return await ReadAsync<InventoryDocumentDto>(await client.PostAsync($"/api/inventory-documents/{doc.Id}/post", null, Ct));
    }

    [Fact]
    public async Task XuatKeToan_ChiPhieuDaGhiSo_TkKhoVaGiaVon()
    {
        var userName = $"kt.{Guid.NewGuid():N}"[..16];
        await _factory.CreateUserAsync(userName, ProcApiFactory.TestPassword, ["TCT"]);
        var head = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, "TCT");
        var company = await ReadAsync<CompanyDetailDto>(await head.PostAsJsonAsync("/api/companies",
            new CreateCompanyRequest(UniqueCode("X"), "Công ty thử xuất kế toán", null, null, null, null, new DateOnly(Today.Year, Today.Month, 1), "KT", "Kho tổng"), Ct));
        var client = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, company.Code);
        var calendar = await ReadAsync<InventoryCalendarDto>(await client.PostAsJsonAsync("/api/inventory-periods/years", new CreateInventoryYearRequest(Today.Year), Ct));
        await ReadAsync<InventoryPeriodDto>(await client.PostAsync($"/api/inventory-periods/{calendar.Periods.Single(p => p.Month == Today.Month).Id}/open", null, Ct));
        var kt = (await client.GetFromJsonAsync<List<WarehouseDetailDto>>("/api/warehouses", Ct))!.Single().Id;
        var kp = (await ReadAsync<WarehouseDetailDto>(await client.PostAsJsonAsync("/api/warehouses", new CreateWarehouseRequest("KP", "Kho phụ", kt, null, null, false), Ct))).Id;
        var cai = (await head.GetFromJsonAsync<List<UomDto>>("/api/uoms", Ct))!.Single(u => u.Code == "CAI").Id;
        // Nhóm cha khai báo TK 1521; nhóm con không khai báo → dùng TK của nhóm cha.
        var parent = await ReadAsync<ItemCategoryDetailDto>(await head.PostAsJsonAsync("/api/item-categories",
            new CreateItemCategoryRequest(null, "Vật tư xuất kế toán", null, "1521"), Ct));
        var child = await ReadAsync<ItemCategoryDetailDto>(await head.PostAsJsonAsync("/api/item-categories",
            new CreateItemCategoryRequest(null, "Vật tư con", parent.Id, null), Ct));
        var item = await ReadAsync<ItemDetailDto>(await head.PostAsJsonAsync("/api/items",
            new CreateItemRequest(null, "Gioăng cao su", null, "Inventory", child.Id, cai, null, null, null, null, "10", null, false, false, null, null, []), Ct));
        var groupId = (await head.GetFromJsonAsync<List<SupplierGroupDto>>("/api/supplier-groups", Ct))!.Single(g => g.Code == "TN").Id;
        // MST của NCC là duy nhất: sinh ngẫu nhiên 10 chữ số (bắt đầu bằng 0 để thử giữ số 0 đầu trong Excel).
        var taxCode = $"0{Random.Shared.NextInt64(100_000_000, 999_999_999)}";
        var supplier = await ReadAsync<SupplierDetailDto>(await head.PostAsJsonAsync("/api/suppliers",
            new SaveSupplierRequest(null, groupId, "Công ty TNHH Gioăng Việt", null, false, taxCode, null, null, null, null, null, null, null, "Approved", null, null, [], []), Ct));
        await ReadAsync<SupplierCompanyDto>(await client.PutAsJsonAsync($"/api/suppliers/{supplier.Id}/company", new SaveSupplierCompanyRequest("331-GV", null, true), Ct));

        // Nhập 10 × 100.000 (VAT 10%), trả 2, chuyển 3 sang KP; một phiếu nhập nháp (không xuất).
        var grn = await PostAsync(client, await client.PostAsJsonAsync("/api/goods-receipts",
            new SaveReceiptRequest(Today, kt, supplier.Id, "0000123", "1C26TGV", Today, null, null, null, [new(null, item.Id, cai, 10, 100_000, "10", null, null, null)]), Ct));
        var rtv = await PostAsync(client, await client.PostAsJsonAsync("/api/goods-returns",
            new SaveReturnRequest(Today, grn.Id, null, "Hàng lỗi", [new(null, grn.Lines[0].Id, 2, null)]), Ct));
        var trf = await PostAsync(client, await client.PostAsJsonAsync("/api/stock-transfers",
            new SaveTransferRequest(Today, kt, kp, null, null, null, [new(null, item.Id, cai, 3, null, null)]), Ct));
        await ReadAsync<InventoryDocumentDto>(await client.PostAsJsonAsync("/api/goods-receipts",
            new SaveReceiptRequest(Today, kt, supplier.Id, null, null, null, null, null, null, [new(null, item.Id, cai, 1, 1, "10", null, null, null)]), Ct));

        var range = $"from={Today:yyyy-MM-dd}&to={Today:yyyy-MM-dd}";
        var docs = (await ReadAsync<List<AccountingDocumentDto>>(await client.GetAsync($"/api/accounting-export/documents?{range}", Ct)))
            .ToDictionary(d => d.DocType);
        Assert.Equal(["GRN", "RTV", "TRF"], docs.Keys.Order());
        Assert.Equal((supplier.Code, "331-GV", taxCode, 1_000_000m, 100_000m, 1_000_000m, true),
            (docs["GRN"].PartnerCode, docs["GRN"].PartnerLocalCode, docs["GRN"].PartnerTaxCode, docs["GRN"].Amount, docs["GRN"].VatAmount, docs["GRN"].CostAmount,
                docs["GRN"].IsCostFinal));
        // Phiếu nhập: giá vốn = giá nhập, chốt ngay khi ghi sổ. Giá vốn xuất theo sổ kho: trả 2 × 100.000; chuyển 3 × 100.000 (chiều xuất).
        Assert.Equal((200_000m, 300_000m, (string?)null), (docs["RTV"].CostAmount, docs["TRF"].CostAmount, docs["TRF"].PartnerCode));
        var onlyGrn = await ReadAsync<List<AccountingDocumentDto>>(await client.GetAsync($"/api/accounting-export/documents?{range}&docTypes=GRN", Ct));
        Assert.Equal([grn.No], onlyGrn.Select(d => d.No));
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"/api/accounting-export/documents?{range}&docTypes=PO", Ct)).StatusCode);

        var file = await client.GetAsync($"/api/accounting-export/file?{range}", Ct);
        Assert.True(file.IsSuccessStatusCode, await file.Content.ReadAsStringAsync(Ct));
        using var workbook = new XLWorkbook(await file.Content.ReadAsStreamAsync(Ct));
        Assert.Equal(["Chứng từ", "Tổng hợp", "Hướng dẫn"], workbook.Worksheets.Select(w => w.Name));
        var sheet = workbook.Worksheet("Chứng từ");
        var headers = sheet.Row(4).CellsUsed().ToDictionary(c => c.GetString(), c => c.Address.ColumnNumber);
        var row = sheet.Row(5);
        Assert.Equal((grn.No, "1521", taxCode, "331-GV"),
            (row.Cell(headers["Số CT"]).GetString(), row.Cell(headers["TK kho"]).GetString(), row.Cell(headers["MST đối tượng"]).GetString(),
                row.Cell(headers["Mã NCC tại công ty"]).GetString()));
        Assert.Equal((1_000_000d, 1_100_000d, Today.ToDateTime(TimeOnly.MinValue)),
            (row.Cell(headers["Thành tiền"]).GetDouble(), row.Cell(headers["Tổng thanh toán"]).GetDouble(), row.Cell(headers["Ngày CT"]).GetDateTime()));
        Assert.Equal(3, workbook.Worksheet("Tổng hợp").RowsUsed().Count() - 3);
        Assert.Equal(rtv.No, sheet.Row(6).Cell(headers["Số CT"]).GetString());
        Assert.Equal(trf.No, sheet.Row(7).Cell(headers["Số CT"]).GetString());

        // Có quyền xem chứng từ kho nhưng không có quyền xuất cho kế toán.
        var roleCode = UniqueCode("R");
        await _factory.CreateRoleAsync(roleCode, Permissions.Inventory.DocumentView);
        var keeperName = $"tk.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(keeperName, company.Code, roleCode);
        var keeper = await _factory.CreateClientInCompanyAsync(keeperName, ProcApiFactory.TestPassword, company.Code);
        Assert.Equal(HttpStatusCode.Forbidden, (await keeper.GetAsync($"/api/accounting-export/file?{range}", Ct)).StatusCode);
    }
}
