using System.Net;
using System.Net.Http.Json;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using Proc.Application.Common;
using Proc.Application.Inventory;
using Proc.Application.MasterData;
using Proc.Application.Organization;
using Proc.Application.Purchasing;
using Proc.Domain.Organization;

namespace Proc.IntegrationTests;

/// <summary>
/// In phiếu PDF (design.md mục 10.10): file đúng định dạng PDF, tiêu đề tài liệu là tên phiếu + số; phiếu công ty khác → 404; thiếu quyền
/// xem phiếu → 403.
/// </summary>
[Collection(ApiCollection.Name)]
public class PrintingTests
{
    private readonly ProcApiFactory _factory;

    public PrintingTests(ProcApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string UniqueCode(string prefix) => $"{prefix}{Guid.NewGuid():N}".ToUpperInvariant()[..10];

    private static DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTime.UtcNow, VietnamTime.Zone));

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(Ct)}");
        return (await response.Content.ReadFromJsonAsync<T>(Ct))!;
    }

    /// <summary>Đọc file PDF trả về: kiểm tra kiểu nội dung, mở bằng PDFsharp.</summary>
    private static async Task<PdfDocument> PdfAsync(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(Ct)}");
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        var bytes = await response.Content.ReadAsByteArrayAsync(Ct);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
        return PdfReader.Open(new MemoryStream(bytes), PdfDocumentOpenMode.Import);
    }

    [Fact]
    public async Task InPhieu_PoPrChungTuKho_PhamViVaQuyen()
    {
        var userName = $"in.{Guid.NewGuid():N}"[..16];
        await _factory.CreateUserAsync(userName, ProcApiFactory.TestPassword, ["TCT"]);
        var head = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, "TCT");
        var company = await ReadAsync<CompanyDetailDto>(await head.PostAsJsonAsync("/api/companies",
            new CreateCompanyRequest(UniqueCode("I"), "Công ty thử in phiếu", null, "0101234567", "12 Lê Lợi, Hà Nội", null, new DateOnly(Today.Year, Today.Month, 1), "KT", "Kho tổng"), Ct));
        var client = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, company.Code);
        var calendar = await ReadAsync<InventoryCalendarDto>(await client.PostAsJsonAsync("/api/inventory-periods/years", new CreateInventoryYearRequest(Today.Year), Ct));
        await ReadAsync<InventoryPeriodDto>(await client.PostAsync($"/api/inventory-periods/{calendar.Periods.Single(p => p.Month == Today.Month).Id}/open", null, Ct));
        var kt = (await client.GetFromJsonAsync<List<WarehouseDetailDto>>("/api/warehouses", Ct))!.Single().Id;
        var kp = (await ReadAsync<WarehouseDetailDto>(await client.PostAsJsonAsync("/api/warehouses", new CreateWarehouseRequest("KP", "Kho phụ", kt, null, null, false), Ct))).Id;
        var cai = (await head.GetFromJsonAsync<List<UomDto>>("/api/uoms", Ct))!.Single(u => u.Code == "CAI").Id;
        var category = await ReadAsync<ItemCategoryDetailDto>(await head.PostAsJsonAsync("/api/item-categories",
            new CreateItemCategoryRequest(UniqueCode("Q"), "Nhóm in phiếu", null, null), Ct));
        var item = await ReadAsync<ItemDetailDto>(await head.PostAsJsonAsync("/api/items",
            new CreateItemRequest($"{category.Code}-01", "Bu lông mạ kẽm M8 – loại dài để thử xuống dòng trong ô tên hàng", null, "Inventory", category.Id, cai,
                null, null, null, null, "10", null, false, false, null, null, []), Ct));
        var groupId = (await head.GetFromJsonAsync<List<SupplierGroupDto>>("/api/supplier-groups", Ct))!.Single(g => g.Code == "TN").Id;
        var supplier = await ReadAsync<SupplierDetailDto>(await head.PostAsJsonAsync("/api/suppliers",
            new SaveSupplierRequest(UniqueCode("N"), groupId, "Công ty TNHH Bu lông Việt", null, false, null, null, null, null, null, null, null, null, "Approved", null, null, [], []), Ct));
        await ReadAsync<SupplierCompanyDto>(await client.PutAsJsonAsync($"/api/suppliers/{supplier.Id}/company", new SaveSupplierCompanyRequest(null, null, true), Ct));

        // PO nháp nhiều dòng (thử tiêu đề cột lặp lại khi sang trang).
        var lines = Enumerable.Range(1, 60).Select(i => new SavePurchaseOrderLineRequest(null, item.Id, cai, i, 12_500, 5, "10", null, null, null)).ToList();
        var po = await ReadAsync<PurchaseOrderDto>(await client.PostAsJsonAsync("/api/purchase-orders",
            new SavePurchaseOrderRequest(Today, supplier.Id, kt, Today.AddDays(5), "30 ngày", "Giao tại kho", null, 0, "Giao giờ hành chính", lines), Ct));
        var poPdf = await PdfAsync(await client.GetAsync($"/api/purchase-orders/{po.Id}/pdf", Ct));
        Assert.Equal($"ĐƠN ĐẶT HÀNG {po.No}", poPdf.Info.Title);
        Assert.True(poPdf.PageCount >= 2, $"60 dòng phải sang trang, có {poPdf.PageCount} trang.");

        var pr = await ReadAsync<PurchaseRequestDto>(await client.PostAsJsonAsync("/api/purchase-requests",
            new SavePurchaseRequestRequest(Today, "Xưởng", "Bảo trì", Today.AddDays(7), kt, "Urgent", null, [new(null, item.Id, cai, 3, null, null, null)]), Ct));
        Assert.Equal($"PHIẾU ĐỀ NGHỊ MUA HÀNG {pr.No}", (await PdfAsync(await client.GetAsync($"/api/purchase-requests/{pr.Id}/pdf", Ct))).Info.Title);

        var grn = await ReadAsync<InventoryDocumentDto>(await client.PostAsJsonAsync("/api/goods-receipts",
            new SaveReceiptRequest(Today, kt, supplier.Id, "0001234", "1C26TBL", Today, null, "Anh Bình", null, [new(null, item.Id, cai, 10, 12_500, "10", null, null, null)]), Ct));
        await ReadAsync<InventoryDocumentDto>(await client.PostAsync($"/api/inventory-documents/{grn.Id}/post", null, Ct));
        Assert.Equal($"PHIẾU NHẬP KHO {grn.No}", (await PdfAsync(await client.GetAsync($"/api/inventory-documents/{grn.Id}/pdf", Ct))).Info.Title);
        var trf = await ReadAsync<InventoryDocumentDto>(await client.PostAsJsonAsync("/api/stock-transfers",
            new SaveTransferRequest(Today, kt, kp, "Anh Bình", "Chị Lan", null, [new(null, item.Id, cai, 4, null, null)]), Ct));
        Assert.Equal($"PHIẾU CHUYỂN KHO NỘI BỘ {trf.No}", (await PdfAsync(await client.GetAsync($"/api/inventory-documents/{trf.Id}/pdf", Ct))).Info.Title);

        // Phiếu của công ty khác: không thấy (404) – như khi mở phiếu.
        var other = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, "TCT");
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/purchase-orders/{po.Id}/pdf", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/inventory-documents/{grn.Id}/pdf", Ct)).StatusCode);

        // Chỉ có quyền xem chứng từ kho: in được phiếu kho, không in được PO.
        var roleCode = UniqueCode("K");
        await _factory.CreateRoleAsync(roleCode, Permissions.Inventory.DocumentView);
        var keeperName = $"tk.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(keeperName, company.Code, roleCode);
        var keeper = await _factory.CreateClientInCompanyAsync(keeperName, ProcApiFactory.TestPassword, company.Code);
        Assert.Equal(HttpStatusCode.Forbidden, (await keeper.GetAsync($"/api/purchase-orders/{po.Id}/pdf", Ct)).StatusCode);
        await PdfAsync(await keeper.GetAsync($"/api/inventory-documents/{grn.Id}/pdf", Ct));
    }
}
