using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Proc.Application.Common;
using Proc.Application.Inventory;
using Proc.Application.MasterData;
using Proc.Application.Organization;
using Proc.Infrastructure.Persistence;
using Proc.Infrastructure.Security;

namespace Proc.IntegrationTests;

/// <summary>
/// Ghi sổ / bỏ ghi sổ phiếu nhập mua (design.md mục 12.2, CLAUDE.md mục 4): sổ kho, tồn tức thời, kỳ mở, không tồn âm,
/// ghi sổ đồng thời không mất số.
/// </summary>
[Collection(ApiCollection.Name)]
public class InventoryPostingTests
{
    private readonly ProcApiFactory _factory;

    public InventoryPostingTests(ProcApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string UniqueCode(string prefix) => $"{prefix}{Guid.NewGuid():N}".ToUpperInvariant()[..10];

    private static DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTime.UtcNow, VietnamTime.Zone));

    private static async Task<string?> TitleAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(Ct))?.Title;

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(Ct)}");
        return (await response.Content.ReadFromJsonAsync<T>(Ct))!;
    }

    /// <summary>Mặt hàng mới (Hộp, Thùng = 24) và NCC đã duyệt, áp dụng tại BHA; client quản trị ở TCT và BHA; kho KT của BHA.</summary>
    private sealed record Fixture(HttpClient Head, HttpClient Bha, ItemDetailDto Item, Guid Hop, Guid Thung, SupplierDetailDto Supplier, Guid Kt);

    private async Task<Fixture> ArrangeAsync()
    {
        var userName = $"qt.{Guid.NewGuid():N}"[..16];
        await _factory.CreateUserAsync(userName, ProcApiFactory.TestPassword, ["TCT", "BHA"]);
        var head = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, "TCT");
        var bha = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, "BHA");
        var uoms = (await head.GetFromJsonAsync<List<UomDto>>("/api/uoms", Ct))!.ToDictionary(u => u.Code, u => u.Id);
        var category = await ReadAsync<ItemCategoryDetailDto>(await head.PostAsJsonAsync("/api/item-categories",
            new CreateItemCategoryRequest(UniqueCode("R"), "Nhóm ghi sổ", null, null), Ct));
        var item = await ReadAsync<ItemDetailDto>(await head.PostAsJsonAsync("/api/items",
            new CreateItemRequest($"{category.Code}-01", "Bu lông ghi sổ", null, "Inventory", category.Id, uoms["HOP"], null, null, null,
                null, "10", null, false, false, null, null, [new(uoms["THUNG"], 24m)]), Ct));
        var groupId = (await head.GetFromJsonAsync<List<SupplierGroupDto>>("/api/supplier-groups", Ct))!.Single(g => g.Code == "TN").Id;
        var supplier = await ReadAsync<SupplierDetailDto>(await head.PostAsJsonAsync("/api/suppliers",
            new SaveSupplierRequest(UniqueCode("N"), groupId, "NCC ghi sổ", null, false, null, null, null, null, null, null, null, null,
                "Approved", null, null, [], []), Ct));
        await ReadAsync<SupplierCompanyDto>(await bha.PutAsJsonAsync($"/api/suppliers/{supplier.Id}/company", new SaveSupplierCompanyRequest(null, null, true), Ct));
        var kt = (await bha.GetFromJsonAsync<List<WarehouseDetailDto>>("/api/warehouses", Ct))!.Single(w => w.Code == "KT").Id;
        return new Fixture(head, bha, item, uoms["HOP"], uoms["THUNG"], supplier, kt);
    }

    /// <summary>Phiếu nhập nháp: <paramref name="boxes"/> thùng (× 24 hộp), đơn giá 480.000 đ/thùng.</summary>
    private static async Task<InventoryDocumentDto> DraftAsync(Fixture f, decimal boxes, DateOnly? date = null) =>
        await ReadAsync<InventoryDocumentDto>(await f.Bha.PostAsJsonAsync("/api/goods-receipts",
            new SaveReceiptRequest(date ?? Today, f.Kt, f.Supplier.Id, null, null, null, null, null, null,
                [new(null, f.Item.Id, f.Thung, boxes, 480_000, "10", null, null, null)]), Ct));

    private static Task<HttpResponseMessage> PostAsync(Fixture f, Guid id) => f.Bha.PostAsync($"/api/inventory-documents/{id}/post", null, Ct);

    private static Task<HttpResponseMessage> UnpostAsync(Fixture f, Guid id) => f.Bha.PostAsync($"/api/inventory-documents/{id}/unpost", null, Ct);

    /// <summary>Đọc tồn và sổ kho của mặt hàng ở BHA trực tiếp từ CSDL (màn hình tồn kho làm ở lát sau).</summary>
    private async Task<(decimal OnHand, int LedgerRows, decimal LedgerQty, decimal LedgerAmount)> StockAsync(Fixture f)
    {
        using var scope = _factory.Services.CreateScope();
        var bhaId = await _factory.GetCompanyIdAsync("BHA");
        scope.ServiceProvider.GetRequiredService<CurrentUserScope>().Initialize(Guid.Empty, bhaId, [], true, [], true, []);
        var db = scope.ServiceProvider.GetRequiredService<ProcDbContext>();
        var onHand = await db.StockOnHand.Where(s => s.ItemId == f.Item.Id && s.WarehouseId == f.Kt).SumAsync(s => s.Qty, Ct);
        var ledger = await db.StockLedger.Where(l => l.ItemId == f.Item.Id).ToListAsync(Ct);
        return (onHand, ledger.Count, ledger.Sum(l => l.Direction * l.BaseQty), ledger.Sum(l => l.Direction * l.Amount));
    }

    [Fact]
    public async Task GhiSo_SinhSoKhoVaCongTon_TheoDvtCoSo()
    {
        var f = await ArrangeAsync();
        var draft = await DraftAsync(f, 2);
        Assert.True(draft.CanPost);

        var posted = await ReadAsync<InventoryDocumentDto>(await PostAsync(f, draft.Id));

        Assert.Equal(("Posted", false, false, true), (posted.Status, posted.CanEdit, posted.CanPost, posted.CanUnpost));
        Assert.NotNull(posted.PostedAt);
        // 2 thùng × 24 = 48 hộp; giá trị nhập = 960.000 (trước VAT).
        Assert.Equal((48m, 1, 48m, 960_000m), await StockAsync(f));
        Assert.Contains("không ở trạng thái nháp", await TitleAsync(await PostAsync(f, draft.Id)));
        Assert.Contains("không còn ở trạng thái nháp", await TitleAsync(await f.Bha.PostAsync($"/api/inventory-documents/{draft.Id}/cancel", null, Ct)));
    }

    [Fact]
    public async Task BoGhiSo_KyConMo_TraLaiTon_PhieuVeNhap()
    {
        var f = await ArrangeAsync();
        var draft = await DraftAsync(f, 2);
        await ReadAsync<InventoryDocumentDto>(await PostAsync(f, draft.Id));

        var unposted = await ReadAsync<InventoryDocumentDto>(await UnpostAsync(f, draft.Id));

        Assert.Equal(("Draft", true, (DateTimeOffset?)null), (unposted.Status, unposted.CanEdit, unposted.PostedAt));
        Assert.Equal((0m, 0, 0m, 0m), await StockAsync(f));
        // Ghi sổ lại sau khi sửa được.
        await ReadAsync<InventoryDocumentDto>(await PostAsync(f, draft.Id));
        Assert.Equal(48m, (await StockAsync(f)).OnHand);
    }

    [Fact]
    public async Task BoGhiSo_LamTonAm_BiChan_TruKhoChoPhepTonAm()
    {
        var f = await ArrangeAsync();
        var draft = await DraftAsync(f, 2);
        await ReadAsync<InventoryDocumentDto>(await PostAsync(f, draft.Id));
        // Giả lập hàng đã xuất dùng 40 hộp (phiếu xuất làm ở lát sau): còn 8 hộp, bỏ ghi sổ 48 hộp thì tồn âm 40.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ProcDbContext>();
            await db.Database.ExecuteSqlAsync($"UPDATE \"StockOnHand\" SET \"Qty\" = \"Qty\" - 40 WHERE \"ItemId\" = {f.Item.Id}", Ct);
        }

        var blocked = await UnpostAsync(f, draft.Id);
        Assert.Equal(HttpStatusCode.BadRequest, blocked.StatusCode);
        Assert.Contains("tại kho KT sẽ âm (-40)", await TitleAsync(blocked));
        // Cả giao dịch bị hủy: phiếu vẫn Đã ghi sổ, tồn giữ nguyên 8.
        Assert.Equal("Posted", (await f.Bha.GetFromJsonAsync<InventoryDocumentDto>($"/api/inventory-documents/{draft.Id}", Ct))!.Status);
        Assert.Equal(8m, (await StockAsync(f)).OnHand);

        var kt = (await f.Bha.GetFromJsonAsync<List<WarehouseDetailDto>>("/api/warehouses", Ct))!.Single(w => w.Id == f.Kt);
        try
        {
            await ReadAsync<WarehouseDetailDto>(await f.Bha.PutAsJsonAsync($"/api/warehouses/{f.Kt}",
                new UpdateWarehouseRequest(kt.Name, null, kt.Address, kt.Keeper, true, true), Ct));
            await ReadAsync<InventoryDocumentDto>(await UnpostAsync(f, draft.Id));
            Assert.Equal(-40m, (await StockAsync(f)).OnHand);
        }
        finally
        {
            await f.Bha.PutAsJsonAsync($"/api/warehouses/{f.Kt}", new UpdateWarehouseRequest(kt.Name, null, kt.Address, kt.Keeper, kt.AllowNegativeStock, true), Ct);
        }
    }

    [Fact]
    public async Task GhiSo_NgayNgoaiKyDangMo_BiChan()
    {
        var f = await ArrangeAsync();
        // Dữ liệu mẫu chỉ mở tháng hiện tại: tháng sau chưa mở (hoặc chưa có lịch nếu là tháng 1 năm sau).
        var draft = await DraftAsync(f, 1, Today.AddMonths(1));

        var response = await PostAsync(f, draft.Id);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("ỳ kho", await TitleAsync(response));
        Assert.Equal((0m, 0), ((await StockAsync(f)).OnHand, (await StockAsync(f)).LedgerRows));
    }

    [Fact]
    public async Task GhiSo_NccBiTamNgungSauKhiLapNhap_BiChan()
    {
        var f = await ArrangeAsync();
        var draft = await DraftAsync(f, 1);
        var supplier = (await f.Head.GetFromJsonAsync<SupplierDetailDto>($"/api/suppliers/{f.Supplier.Id}", Ct))!;
        await ReadAsync<SupplierDetailDto>(await f.Head.PutAsJsonAsync($"/api/suppliers/{supplier.Id}",
            new SaveSupplierRequest(null, supplier.GroupId, supplier.Name, null, false, null, null, null, null, null, null, null, null,
                "Suspended", "Giao hàng lỗi", null, [], []), Ct));

        Assert.Contains("chưa ở trạng thái Đã duyệt", await TitleAsync(await PostAsync(f, draft.Id)));
    }

    [Fact]
    public async Task GhiSoDongThoi_CungMatHangCungKho_KhongMatSo()
    {
        var f = await ArrangeAsync();
        var drafts = new List<InventoryDocumentDto>();
        for (var i = 0; i < 6; i++) drafts.Add(await DraftAsync(f, 1));

        var responses = await Task.WhenAll(drafts.Select(d => PostAsync(f, d.Id)));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        Assert.Equal((144m, 6, 144m), ((await StockAsync(f)).OnHand, (await StockAsync(f)).LedgerRows, (await StockAsync(f)).LedgerQty));
    }

    [Fact]
    public async Task ThuKho_GhiSoDuoc_NhungKhongCoQuyenBoGhiSo()
    {
        var f = await ArrangeAsync();
        var draft = await DraftAsync(f, 1);
        var keeper = await _factory.CreateClientInCompanyAsync("thukho.bha", "Demo@2026", "BHA");

        // Thủ kho mẫu chỉ được kho KP1: phiếu kho KT như không tồn tại với họ.
        Assert.Equal(HttpStatusCode.NotFound, (await keeper.PostAsync($"/api/inventory-documents/{draft.Id}/post", null, Ct)).StatusCode);
        await ReadAsync<InventoryDocumentDto>(await PostAsync(f, draft.Id));
        Assert.Equal(HttpStatusCode.Forbidden, (await keeper.PostAsync($"/api/inventory-documents/{draft.Id}/unpost", null, Ct)).StatusCode);
    }
}
