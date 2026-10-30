using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Proc.Application.Common;
using Proc.Application.Inventory;
using Proc.Application.MasterData;
using Proc.Application.Organization;
using Proc.Domain.Organization;

namespace Proc.IntegrationTests;

/// <summary>
/// Phiếu nhập mua NCC ở trạng thái nháp (design.md mục 6.1, 10.4): cấp số, tính tiền, NCC đã duyệt và áp dụng tại công ty,
/// phạm vi kho và nhóm hàng.
/// </summary>
[Collection(ApiCollection.Name)]
public class GoodsReceiptTests
{
    private readonly ProcApiFactory _factory;

    public GoodsReceiptTests(ProcApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string UniqueCode(string prefix) => $"{prefix}{Guid.NewGuid():N}".ToUpperInvariant()[..10];

    private static async Task<string?> TitleAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(Ct))?.Title;

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(Ct)}");
        return (await response.Content.ReadFromJsonAsync<T>(Ct))!;
    }

    /// <summary>Dữ liệu cho một phiếu: nhóm hàng + mặt hàng (Hộp, Thùng = 24 hộp), NCC đã duyệt và áp dụng tại BHA, kho KT của BHA.</summary>
    private sealed record Fixture(HttpClient Head, HttpClient Bha, ItemCategoryDetailDto Category, ItemDetailDto Item, Guid Hop, Guid Thung, SupplierDetailDto Supplier, Guid MainWarehouse);

    private async Task<Fixture> ArrangeAsync(string supplierStatus = "Approved", bool applyAtBha = true)
    {
        var userName = $"qt.{Guid.NewGuid():N}"[..16];
        await _factory.CreateUserAsync(userName, ProcApiFactory.TestPassword, ["TCT", "BHA"]);
        var head = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, "TCT");
        var bha = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, "BHA");

        var uoms = (await head.GetFromJsonAsync<List<UomDto>>("/api/uoms", Ct))!.ToDictionary(u => u.Code, u => u.Id);
        var category = await ReadAsync<ItemCategoryDetailDto>(await head.PostAsJsonAsync("/api/item-categories",
            new CreateItemCategoryRequest(UniqueCode("R"), "Nhóm phiếu nhập", null, null), Ct));
        var item = await ReadAsync<ItemDetailDto>(await head.PostAsJsonAsync("/api/items",
            new CreateItemRequest($"{category.Code}-01", "Bu lông lục giác M8", null, "Inventory", category.Id, uoms["HOP"], null, null, null,
                null, "10", null, false, false, null, null, [new(uoms["THUNG"], 24m)]), Ct));
        var groupId = (await head.GetFromJsonAsync<List<SupplierGroupDto>>("/api/supplier-groups", Ct))!.Single(g => g.Code == "TN").Id;
        var supplier = await ReadAsync<SupplierDetailDto>(await head.PostAsJsonAsync("/api/suppliers",
            new SaveSupplierRequest(UniqueCode("N"), groupId, "NCC phiếu nhập", null, false, null, null, null, null, null, null, null, null,
                supplierStatus, supplierStatus == "Approved" ? null : "Thử", null, [], []), Ct));
        if (applyAtBha)
        {
            await ReadAsync<SupplierCompanyDto>(await bha.PutAsJsonAsync($"/api/suppliers/{supplier.Id}/company",
                new SaveSupplierCompanyRequest(null, null, true), Ct));
        }

        var main = (await bha.GetFromJsonAsync<List<WarehouseDetailDto>>("/api/warehouses", Ct))!.Single(w => w.Code == "KT").Id;
        return new Fixture(head, bha, category, item, uoms["HOP"], uoms["THUNG"], supplier, main);
    }

    private static DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTime.UtcNow, VietnamTime.Zone));

    private static SaveReceiptRequest Receipt(Fixture f, IReadOnlyList<SaveInventoryLineRequest>? lines = null, Guid? warehouse = null) =>
        new(Today, warehouse ?? f.MainWarehouse, f.Supplier.Id, "0000123", "1C26TAA", Today, null, "Nguyễn Giao", null,
            lines ?? [new(null, f.Item.Id, f.Thung, 2, 480_000, "10", null, null, null), new(null, f.Item.Id, f.Hop, 3, 20_000, null, null, null, "Hàng lẻ")]);

    [Fact]
    public async Task LapPhieuNhap_CapSoTheoMau_TinhTien()
    {
        var f = await ArrangeAsync();

        var doc = await ReadAsync<InventoryDocumentDto>(await f.Bha.PostAsJsonAsync("/api/goods-receipts", Receipt(f), Ct));

        Assert.Matches($"^GRN-BHA-{Today:yyMM}-\\d{{4}}$", doc.No);
        Assert.Equal(("Draft", true, 1_020_000m, 96_000m), (doc.Status, doc.CanEdit, doc.TotalAmount, doc.TotalVat));
        Assert.Equal((48m, "THUNG", "HOP"), (doc.Lines[0].BaseQty, doc.Lines[0].UomCode, doc.Lines[0].BaseUomCode));
        Assert.Equal((2, "Hàng lẻ"), (doc.Lines[1].LineNo, doc.Lines[1].Note));
        Assert.Equal(f.Supplier.Code, doc.SupplierCode);
        Assert.NotNull(doc.CreatedByName);

        var list = await f.Bha.GetFromJsonAsync<InventoryDocumentListResult>("/api/inventory-documents?docType=GRN", Ct);
        var row = list!.Items.Single(r => r.Id == doc.Id);
        Assert.Equal(("KT", 2, 1_020_000m), (row.WarehouseCode, row.LineCount, row.TotalAmount));
    }

    [Fact]
    public async Task SuaPhieuNhap_ThayDong_HuyThiKhongSuaDuocNua()
    {
        var f = await ArrangeAsync();
        var doc = await ReadAsync<InventoryDocumentDto>(await f.Bha.PostAsJsonAsync("/api/goods-receipts", Receipt(f), Ct));

        var first = doc.Lines[0];
        var edited = await ReadAsync<InventoryDocumentDto>(await f.Bha.PutAsJsonAsync($"/api/goods-receipts/{doc.Id}",
            Receipt(f, [new(first.Id, f.Item.Id, f.Thung, 1, 480_000, "10", null, null, null)]), Ct));
        Assert.Equal((1, first.Id, 480_000m, doc.No), (edited.Lines.Count, edited.Lines[0].Id, edited.TotalAmount, edited.No));

        var cancelled = await ReadAsync<InventoryDocumentDto>(await f.Bha.PostAsync($"/api/inventory-documents/{doc.Id}/cancel", null, Ct));
        Assert.Equal(("Cancelled", false), (cancelled.Status, cancelled.CanEdit));
        var afterCancel = await f.Bha.PutAsJsonAsync($"/api/goods-receipts/{doc.Id}", Receipt(f), Ct);
        Assert.Equal($"Phiếu {doc.No} không còn ở trạng thái nháp nên không sửa, hủy được.", await TitleAsync(afterCancel));
    }

    [Fact]
    public async Task NccChuaDuyetHoacChuaApDungTaiCongTy_KhongLapPhieuDuoc()
    {
        var notApproved = await ArrangeAsync(supplierStatus: "Potential");
        var r1 = await notApproved.Bha.PostAsJsonAsync("/api/goods-receipts", Receipt(notApproved), Ct);
        Assert.Contains("chưa ở trạng thái Đã duyệt", await TitleAsync(r1));

        var notApplied = await ArrangeAsync(applyAtBha: false);
        var r2 = await notApplied.Bha.PostAsJsonAsync("/api/goods-receipts", Receipt(notApplied), Ct);
        Assert.Contains("chưa được áp dụng tại công ty", await TitleAsync(r2));
    }

    [Fact]
    public async Task DongSai_BaoLoiTiengViet()
    {
        var f = await ArrangeAsync();
        var noLines = await f.Bha.PostAsJsonAsync("/api/goods-receipts", Receipt(f, []), Ct);
        Assert.Equal(HttpStatusCode.BadRequest, noLines.StatusCode);

        var kg = (await f.Head.GetFromJsonAsync<List<UomDto>>("/api/uoms", Ct))!.Single(u => u.Code == "KG").Id;
        var badUom = await f.Bha.PostAsJsonAsync("/api/goods-receipts", Receipt(f, [new(null, f.Item.Id, kg, 1, 1, null, null, null, null)]), Ct);
        Assert.Contains("Dòng 1", await TitleAsync(badUom));
    }

    [Fact]
    public async Task PhamViKho_ThuKhoKP1_KhongLapVaKhongThayPhieuKhoTong()
    {
        var f = await ArrangeAsync();
        var doc = await ReadAsync<InventoryDocumentDto>(await f.Bha.PostAsJsonAsync("/api/goods-receipts", Receipt(f), Ct));

        var userName = $"tk.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(userName, "BHA", "THUKHO", warehouseCodes: ["KP1"]);
        var keeper = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, "BHA");

        Assert.Equal(HttpStatusCode.NotFound, (await keeper.GetAsync($"/api/inventory-documents/{doc.Id}", Ct)).StatusCode);
        var list = await keeper.GetFromJsonAsync<InventoryDocumentListResult>("/api/inventory-documents?docType=GRN", Ct);
        Assert.DoesNotContain(list!.Items, r => r.Id == doc.Id);
        var create = await keeper.PostAsJsonAsync("/api/goods-receipts", Receipt(f), Ct);
        Assert.Equal(HttpStatusCode.NotFound, create.StatusCode);
    }

    [Fact]
    public async Task PhamViNhomHang_PhieuCoHangNgoaiPhamVi_ChiXem()
    {
        var f = await ArrangeAsync();
        var doc = await ReadAsync<InventoryDocumentDto>(await f.Bha.PostAsJsonAsync("/api/goods-receipts", Receipt(f), Ct));

        var roleCode = UniqueCode("LKHO");
        await _factory.CreateRoleAsync(roleCode, Permissions.Inventory.DocumentView, Permissions.Inventory.DocumentCreate);
        var userName = $"lk.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(userName, "BHA", roleCode, categoryCodes: ["VPP"]);
        var limited = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, "BHA");

        var view = await limited.GetFromJsonAsync<InventoryDocumentDto>($"/api/inventory-documents/{doc.Id}", Ct);
        Assert.Equal(("(ngoài phạm vi)", false), (view!.Lines[0].ItemCode, view.CanEdit));
        Assert.Contains("ngoài phạm vi nhóm hàng", view.ReadOnlyReason);
        Assert.Equal(HttpStatusCode.Forbidden, (await limited.PutAsJsonAsync($"/api/goods-receipts/{doc.Id}", Receipt(f), Ct)).StatusCode);
    }

    [Fact]
    public async Task TraHang_ChoOChonHang_CoDvtVaHeSo()
    {
        var f = await ArrangeAsync();

        var found = await f.Bha.GetFromJsonAsync<List<ItemLookupDto>>($"/api/items/lookup?search=bu%20long%20{f.Category.Code}&inventoryOnly=true", Ct);

        var item = Assert.Single(found!);
        Assert.Equal((f.Item.Code, "10", f.Hop), (item.Code, item.VatCode, item.BaseUomId));
        Assert.Equal([("HOP", 1m), ("THUNG", 24m)], item.Uoms.Select(u => (u.Code, u.Factor)));

        var byId = await f.Bha.GetFromJsonAsync<List<ItemLookupDto>>($"/api/items/lookup?ids={f.Item.Id}", Ct);
        Assert.Equal(f.Item.Id, Assert.Single(byId!).Id);
    }

    [Fact]
    public async Task TraNcc_ChoPhieuNhap_ChiNccDaDuyetVaApDungTaiCongTy_ThuKhoCungDungDuoc()
    {
        var applied = await ArrangeAsync();
        var notApplied = await ArrangeAsync(applyAtBha: false);
        var keeper = await _factory.CreateClientInCompanyAsync("thukho.bha", "Demo@2026", "BHA");

        var suppliers = await keeper.GetFromJsonAsync<List<SupplierLookupDto>>("/api/suppliers/lookup", Ct);

        Assert.Contains(suppliers!, s => s.Id == applied.Supplier.Id);
        Assert.DoesNotContain(suppliers!, s => s.Id == notApplied.Supplier.Id);
        var withOld = await keeper.GetFromJsonAsync<List<SupplierLookupDto>>($"/api/suppliers/lookup?ids={notApplied.Supplier.Id}", Ct);
        Assert.Contains(withOld!, s => s.Id == notApplied.Supplier.Id);
    }
}
