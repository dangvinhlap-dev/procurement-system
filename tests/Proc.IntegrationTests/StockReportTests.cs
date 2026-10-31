using System.Net;
using System.Net.Http.Json;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Proc.Application.Common;
using Proc.Application.Excel;
using Proc.Application.Inventory;
using Proc.Application.MasterData;
using Proc.Application.Organization;
using Proc.Application.Reports;
using Proc.Domain.Organization;

namespace Proc.IntegrationTests;

/// <summary>
/// Báo cáo hàng chậm luân chuyển, sắp hết hạn, tồn tập đoàn và xuất Excel lưới đang xem (design.md mục 11.2).
/// Mỗi test dùng công ty, mặt hàng, NCC mới; chứng từ ghi ở kỳ tháng 1–2/2025 để số ngày không xuất đủ lớn.
/// </summary>
[Collection(ApiCollection.Name)]
public class StockReportTests
{
    private readonly ProcApiFactory _factory;

    public StockReportTests(ProcApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string UniqueCode(string prefix) => $"{prefix}{Guid.NewGuid():N}".ToUpperInvariant()[..10];

    private static DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTime.UtcNow, VietnamTime.Zone));

    private static readonly DateOnly Jan5 = new(2025, 1, 5);
    private static readonly DateOnly Feb20 = new(2025, 2, 20);

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(Ct)}");
        return (await response.Content.ReadFromJsonAsync<T>(Ct))!;
    }

    private sealed record Company(HttpClient Client, string Code, Guid Kt, Guid Kp);

    private sealed record Fixture(string UserName, HttpClient Head, Guid Cai, Guid CategoryId, string CategoryCode, Guid SupplierId);

    private async Task<Fixture> ArrangeAsync()
    {
        var userName = $"bc.{Guid.NewGuid():N}"[..16];
        await _factory.CreateUserAsync(userName, ProcApiFactory.TestPassword, ["TCT"]);
        var head = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, "TCT");
        var cai = (await head.GetFromJsonAsync<List<UomDto>>("/api/uoms", Ct))!.Single(u => u.Code == "CAI").Id;
        var category = await ReadAsync<ItemCategoryDetailDto>(await head.PostAsJsonAsync("/api/item-categories",
            new CreateItemCategoryRequest(UniqueCode("B"), "Nhóm báo cáo kho", null, null), Ct));
        var groupId = (await head.GetFromJsonAsync<List<SupplierGroupDto>>("/api/supplier-groups", Ct))!.Single(g => g.Code == "TN").Id;
        var supplier = await ReadAsync<SupplierDetailDto>(await head.PostAsJsonAsync("/api/suppliers",
            new SaveSupplierRequest(UniqueCode("N"), groupId, "NCC báo cáo kho", null, false, null, null, null, null, null, null, null, null,
                "Approved", null, null, [], []), Ct));
        return new Fixture(userName, head, cai, category.Id, category.Code, supplier.Id);
    }

    private async Task<Guid> ItemAsync(Fixture f, string suffix, bool trackLot)
    {
        var item = await ReadAsync<ItemDetailDto>(await f.Head.PostAsJsonAsync("/api/items",
            new CreateItemRequest($"{f.CategoryCode}-{suffix}", $"Hàng báo cáo {suffix}", null, "Inventory", f.CategoryId, f.Cai, null, null, null,
                null, "10", null, trackLot, trackLot, null, null, []), Ct));
        return item.Id;
    }

    /// <summary>Công ty mới bắt đầu 01/2025, mở kỳ 1 và 2/2025, có kho Sub KP; NCC của fixture áp dụng tại công ty.</summary>
    private async Task<Company> CompanyAsync(Fixture f)
    {
        var company = await ReadAsync<CompanyDetailDto>(await f.Head.PostAsJsonAsync("/api/companies",
            new CreateCompanyRequest(UniqueCode("R"), "Công ty thử báo cáo kho", null, null, null, null, new DateOnly(2025, 1, 1), "KT", "Kho tổng"), Ct));
        var client = await _factory.CreateClientInCompanyAsync(f.UserName, ProcApiFactory.TestPassword, company.Code);
        var kt = (await client.GetFromJsonAsync<List<WarehouseDetailDto>>("/api/warehouses", Ct))!.Single().Id;
        var kp = (await ReadAsync<WarehouseDetailDto>(await client.PostAsJsonAsync("/api/warehouses",
            new CreateWarehouseRequest("KP", "Kho phụ", kt, null, null, false), Ct))).Id;
        var calendar = await ReadAsync<InventoryCalendarDto>(await client.PostAsJsonAsync("/api/inventory-periods/years", new CreateInventoryYearRequest(2025), Ct));
        foreach (var month in new[] { 1, 2 })
            await ReadAsync<InventoryPeriodDto>(await client.PostAsync($"/api/inventory-periods/{calendar.Periods.Single(p => p.Month == month).Id}/open", null, Ct));
        await ReadAsync<SupplierCompanyDto>(await client.PutAsJsonAsync($"/api/suppliers/{f.SupplierId}/company", new SaveSupplierCompanyRequest(null, null, true), Ct));
        return new Company(client, company.Code, kt, kp);
    }

    private static async Task ReceiveAsync(Fixture f, Company c, DateOnly date, params SaveInventoryLineRequest[] lines)
    {
        var grn = await ReadAsync<InventoryDocumentDto>(await c.Client.PostAsJsonAsync("/api/goods-receipts",
            new SaveReceiptRequest(date, c.Kt, f.SupplierId, null, null, null, null, null, null, lines), Ct));
        await ReadAsync<InventoryDocumentDto>(await c.Client.PostAsync($"/api/inventory-documents/{grn.Id}/post", null, Ct));
    }

    private static async Task TransferAsync(Company c, DateOnly date, Guid itemId, Guid uom, decimal qty, string? lotNo = null)
    {
        var trf = await ReadAsync<InventoryDocumentDto>(await c.Client.PostAsJsonAsync("/api/stock-transfers",
            new SaveTransferRequest(date, c.Kt, c.Kp, null, null, null, [new(null, itemId, uom, qty, lotNo, null)]), Ct));
        await ReadAsync<InventoryDocumentDto>(await c.Client.PostAsync($"/api/inventory-documents/{trf.Id}/post", null, Ct));
    }

    private static SaveInventoryLineRequest Line(Fixture f, Guid itemId, decimal qty, decimal price, string? lot = null, DateOnly? expiry = null) =>
        new(null, itemId, f.Cai, qty, price, "10", lot, expiry, null);

    [Fact]
    public async Task ChamLuanChuyen_TinhTuNgayXuatCuoiHoacNgayNhapDau()
    {
        var f = await ArrangeAsync();
        var c = await CompanyAsync(f);
        var a = await ItemAsync(f, "A", false);
        var b = await ItemAsync(f, "B", false);
        await ReceiveAsync(f, c, Jan5, Line(f, a, 10, 100_000), Line(f, b, 20, 50_000));
        // B chuyển 5 cái sang KP ngày 20/02: KT có ngày xuất cuối 20/02, KP có ngày nhập đầu 20/02.
        await TransferAsync(c, Feb20, b, f.Cai, 5);

        var idleA = Today.DayNumber - Jan5.DayNumber;
        var idleB = Today.DayNumber - Feb20.DayNumber;
        var all = await ReadAsync<SlowMovingResult>(await c.Client.GetAsync($"/api/reports/slow-moving?days={idleB}", Ct));
        Assert.Equal(Today, all.AsOf);
        Assert.Equal(3, all.Items.Count);
        var rowA = all.Items[0];
        Assert.Equal(("KT", a, 10m, 1_000_000m, idleA, (DateOnly?)null), (rowA.WarehouseCode, rowA.ItemId, rowA.Qty, rowA.Amount, rowA.IdleDays, rowA.LastOutDate));
        var bKt = all.Items.Single(r => r.ItemId == b && r.WarehouseCode == "KT");
        Assert.Equal((15m, idleB, (DateOnly?)Feb20, (DateOnly?)Jan5), (bKt.Qty, bKt.IdleDays, bKt.LastOutDate, bKt.FirstInDate));

        // Ngưỡng lớn hơn số ngày của B: chỉ còn A.
        var onlyA = await ReadAsync<SlowMovingResult>(await c.Client.GetAsync($"/api/reports/slow-moving?days={idleB + 1}", Ct));
        Assert.Equal([a], onlyA.Items.Select(r => r.ItemId));
        var kp = await ReadAsync<SlowMovingResult>(await c.Client.GetAsync($"/api/reports/slow-moving?days=1&warehouseId={c.Kp}", Ct));
        Assert.Equal([("KP", b, 5m)], kp.Items.Select(r => (r.WarehouseCode, r.ItemId, r.Qty)));
        Assert.Equal(HttpStatusCode.BadRequest, (await c.Client.GetAsync("/api/reports/slow-moving?days=0", Ct)).StatusCode);
    }

    [Fact]
    public async Task SapHetHan_GomLoDaHetHan_KhongGomLoConXa()
    {
        var f = await ArrangeAsync();
        var c = await CompanyAsync(f);
        var item = await ItemAsync(f, "LO", true);
        await ReceiveAsync(f, c, Jan5,
            Line(f, item, 10, 10_000, "L1", Today.AddDays(10)),
            Line(f, item, 20, 10_000, "L2", Today.AddDays(-5)),
            Line(f, item, 30, 10_000, "L3", Today.AddDays(100)));
        // Lô L1 chuyển một phần sang KP: phiếu chuyển chép theo lô, KP cũng thấy hạn dùng.
        await TransferAsync(c, Feb20, item, f.Cai, 4, "L1");

        var result = await ReadAsync<ExpiringResult>(await c.Client.GetAsync("/api/reports/expiring?days=30", Ct));
        Assert.Equal(
            [("L2", "KT", -5, 20m, 200_000m), ("L1", "KP", 10, 4m, 40_000m), ("L1", "KT", 10, 6m, 60_000m)],
            result.Items.Select(r => (r.LotNo, r.WarehouseCode, r.DaysLeft, r.Qty, r.Amount)));
        var wide = await ReadAsync<ExpiringResult>(await c.Client.GetAsync("/api/reports/expiring?days=365", Ct));
        Assert.Contains(wide.Items, r => r.LotNo == "L3");
    }

    [Fact]
    public async Task TonTapDoan_ChiSoLuong_CanQuyenXemToanTapDoan()
    {
        var f = await ArrangeAsync();
        var a = await CompanyAsync(f);
        var b = await CompanyAsync(f);
        var item = await ItemAsync(f, "TD", false);
        await ReceiveAsync(f, a, Jan5, Line(f, item, 40, 10_000));
        await TransferAsync(a, Feb20, item, f.Cai, 10);
        await ReceiveAsync(f, b, Jan5, Line(f, item, 5, 12_000));

        var response = await a.Client.GetAsync($"/api/reports/group-stock?itemId={item}", Ct);
        var json = await response.Content.ReadAsStringAsync(Ct);
        // Không có giá trị, giá vốn trong kết quả – chỉ số lượng.
        Assert.DoesNotContain("amount", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("cost", json, StringComparison.OrdinalIgnoreCase);
        var result = await ReadAsync<GroupStockResult>(response);
        Assert.Equal(
            new[] { (a.Code, true, 30m, 30m, 10m, 40m), (b.Code, false, 5m, 5m, 0m, 5m) }.OrderBy(x => x.Item1, StringComparer.Ordinal),
            result.Items.Select(r => (r.CompanyCode, r.IsCurrent, r.MainQty, r.MainAvailable, r.SubQty, r.TotalQty)));

        // Có quyền xem báo cáo kho nhưng không có quyền toàn tập đoàn → 403; báo cáo trong công ty vẫn xem được.
        var roleCode = UniqueCode("K");
        await _factory.CreateRoleAsync(roleCode, Permissions.Report.StockView);
        var userName = $"tk.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(userName, a.Code, roleCode);
        var viewer = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, a.Code);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.GetAsync($"/api/reports/group-stock?itemId={item}", Ct)).StatusCode);
        var slow = await ReadAsync<SlowMovingResult>(await viewer.GetAsync("/api/reports/slow-moving?days=1", Ct));
        Assert.Equal(["KP", "KT"], slow.Items.Where(r => r.ItemId == item).Select(r => r.WarehouseCode).Order());
    }

    [Fact]
    public async Task XuatExcelLuoi_GiuKieuSoVaNgay()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var request = new GridExportRequest("Hàng chậm luân chuyển", "Không xuất từ 90 ngày", "hang-cham-luan-chuyen",
            [new("Mã hàng", "text", 120), new("Ngày nhập", "date", 90), new("Tồn", "quantity", 80), new("Giá trị", "money", 120)],
            [["0101", "2025-01-05", "12.5", "1250000"], ["VB-02", null, "x", "0"]]);
        var response = await client.PostAsJsonAsync("/api/excel/grid", request, Ct);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync(Ct));
        Assert.Equal("hang-cham-luan-chuyen.xlsx", response.Content.Headers.ContentDisposition?.FileNameStar);

        using var workbook = new XLWorkbook(await response.Content.ReadAsStreamAsync(Ct));
        var sheet = workbook.Worksheet(1);
        Assert.Equal("Hàng chậm luân chuyển", sheet.Cell(1, 1).GetString());
        Assert.StartsWith("Không xuất từ 90 ngày – Xuất lúc", sheet.Cell(2, 1).GetString());
        Assert.Equal("Mã hàng", sheet.Cell(4, 1).GetString());
        // Mã có số 0 đầu giữ nguyên dạng chữ; ngày là ngày thật; số là số để Excel cộng được.
        Assert.Equal("0101", sheet.Cell(5, 1).GetString());
        Assert.Equal(new DateTime(2025, 1, 5), sheet.Cell(5, 2).GetDateTime());
        Assert.Equal(12.5, sheet.Cell(5, 3).GetDouble());
        Assert.Equal(1_250_000, sheet.Cell(5, 4).GetDouble());
        Assert.True(sheet.Cell(6, 2).IsEmpty());
        // Ô không đọc được theo kiểu cột vẫn giữ dạng chữ, không làm hỏng file.
        Assert.Equal("x", sheet.Cell(6, 3).GetString());

        var bad = await client.PostAsJsonAsync("/api/excel/grid", request with { Rows = [["chỉ một ô"]] }, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        Assert.Contains("Số ô", (await bad.Content.ReadFromJsonAsync<ProblemDetails>(Ct))?.Title + await bad.Content.ReadAsStringAsync(Ct));
    }
}
