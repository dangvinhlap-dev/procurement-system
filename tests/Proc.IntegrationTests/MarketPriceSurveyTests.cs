using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Proc.Application.Common;
using Proc.Application.Documents;
using Proc.Application.MasterData;
using Proc.Application.Pricing;
using Proc.Domain.Organization;

namespace Proc.IntegrationTests;

/// <summary>
/// Phiếu khảo sát giá thị trường – ngân hàng giá (lát G4a, design.md mục 18.25): Văn phòng Ban lập ở tổng công ty theo nhóm hàng, chốt để
/// giá vào ngân hàng giá (không sửa), hủy bắt buộc lý do; tra giá thị trường chỉ lấy phiếu đã chốt.
/// </summary>
[Collection(ApiCollection.Name)]
public class MarketPriceSurveyTests
{
    private const string Url = "/api/market-price-surveys";
    private readonly ProcApiFactory _factory;

    public MarketPriceSurveyTests(ProcApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string UniqueCode(string prefix) => $"{prefix}{Guid.NewGuid():N}".ToUpperInvariant()[..8];

    private static DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTime.UtcNow, VietnamTime.Zone));

    private static async Task<string?> TitleAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(Ct))?.Title;

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(Ct)}");
        return (await response.Content.ReadFromJsonAsync<T>(Ct))!;
    }

    [Fact]
    public async Task LapChotHuy_ChiTongCongTy_TraGiaChiPhieuDaChot()
    {
        var userName = $"ks.{Guid.NewGuid():N}"[..16];
        await _factory.CreateUserAsync(userName, ProcApiFactory.TestPassword, ["TCT", "BHA"]);
        var head = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, "TCT");
        var member = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, "BHA");
        var cai = (await head.GetFromJsonAsync<List<UomDto>>("/api/uoms", Ct))!.Single(u => u.Code == "CAI").Id;
        var category = await ReadAsync<ItemCategoryDetailDto>(await head.PostAsJsonAsync("/api/item-categories",
            new CreateItemCategoryRequest(null, $"Nhóm khảo sát giá {Guid.NewGuid():N}", null, null), Ct));
        var other = await ReadAsync<ItemCategoryDetailDto>(await head.PostAsJsonAsync("/api/item-categories",
            new CreateItemCategoryRequest(null, $"Nhóm khác khảo sát {Guid.NewGuid():N}", null, null), Ct));
        async Task<Guid> ItemAsync(Guid categoryId, string name) => (await ReadAsync<ItemDetailDto>(await head.PostAsJsonAsync("/api/items",
            new CreateItemRequest(null, name, null, "Inventory", categoryId, cai, null, null, null, null, "10", null, false, false, null, null, []), Ct))).Id;
        var item = await ItemAsync(category.Id, "Que hàn khảo sát");
        var outside = await ItemAsync(other.Id, "Ống thép khảo sát");
        var groupId = (await head.GetFromJsonAsync<List<SupplierGroupDto>>("/api/supplier-groups", Ct))!.Single(g => g.Code == "TN").Id;
        // NCC tiềm năng (chưa duyệt) vẫn là nguồn khảo sát được.
        var supplier = await ReadAsync<SupplierDetailDto>(await head.PostAsJsonAsync("/api/suppliers",
            new SaveSupplierRequest(null, groupId, "NCC khảo sát giá", null, false, null, null, null, null, null, null, null, null, "Potential", null, null, [], []), Ct));

        SaveMarketSurveyRequest Request(params SaveMarketSurveyLineRequest[] lines) => new(Today, category.Id, "Khảo sát định kỳ", lines);
        SaveMarketSurveyLineRequest Line(Guid itemId, Guid? supplierId, string? source, decimal price) => new(null, itemId, cai, supplierId, source, price, null);

        // Công ty thành viên không lập; mặt hàng ngoài nhóm hàng; dòng không có nguồn.
        Assert.Equal(HttpStatusCode.Forbidden, (await member.PostAsJsonAsync(Url, Request(Line(item, null, "Chợ", 1)), Ct)).StatusCode);
        Assert.Contains("không thuộc nhóm hàng", await TitleAsync(await head.PostAsJsonAsync(Url, Request(Line(outside, null, "Chợ", 1)), Ct)));
        Assert.Contains("tên nguồn", await TitleAsync(await head.PostAsJsonAsync(Url, Request(Line(item, null, null, 1)), Ct)));

        var draft = await ReadAsync<MarketSurveyDto>(await head.PostAsJsonAsync(Url,
            Request(Line(item, supplier.Id, null, 15_000), Line(item, null, "Cửa hàng Bình Tây", 14_500)), Ct));
        Assert.StartsWith("KSG", draft.No);
        Assert.Equal(("Draft", 2, true), (draft.Status, draft.Lines.Count, draft.Actions.CanEdit));
        Assert.Equal(supplier.Code, draft.Lines[0].SupplierCode);

        // Nháp chưa vào ngân hàng giá.
        var pricesUrl = $"{Url}/prices?itemIds={item}";
        Assert.Empty(await ReadAsync<List<MarketPriceDto>>(await head.GetAsync(pricesUrl, Ct)));

        var confirmed = await ReadAsync<MarketSurveyDto>(await head.PostAsync($"{Url}/{draft.Id}/confirm", null, Ct));
        Assert.Equal(("Confirmed", false, true), (confirmed.Status, confirmed.Actions.CanEdit, confirmed.Actions.CanCancel));
        Assert.NotNull(confirmed.ConfirmedByName);
        Assert.Contains("không ở trạng thái nháp", await TitleAsync(await head.PutAsJsonAsync($"{Url}/{draft.Id}", Request(Line(item, null, "Chợ", 1)), Ct)));
        var prices = await ReadAsync<List<MarketPriceDto>>(await head.GetAsync(pricesUrl, Ct));
        Assert.Equal([15_000m, 14_500m], prices.Select(p => p.BaseUnitPrice));
        Assert.Equal("Cửa hàng Bình Tây", prices[1].Source);

        // Có trong tra cứu chứng từ của tổng công ty.
        var registry = await head.GetFromJsonAsync<DocumentRegistryResult>(
            $"/api/document-registry?from={Today:yyyy-MM-dd}&to={Today:yyyy-MM-dd}&docTypes=KSG", Ct);
        Assert.Contains(registry!.Rows, r => r.Id == draft.Id);

        // Hủy bắt buộc lý do; hủy xong giá thôi dùng để tham khảo.
        Assert.Equal(HttpStatusCode.BadRequest, (await head.PostAsJsonAsync($"{Url}/{draft.Id}/cancel", new CancelMarketSurveyRequest(""), Ct)).StatusCode);
        var cancelled = await ReadAsync<MarketSurveyDto>(await head.PostAsJsonAsync($"{Url}/{draft.Id}/cancel", new CancelMarketSurveyRequest("Nhập nhầm giá"), Ct));
        Assert.Equal(("Cancelled", false), (cancelled.Status, cancelled.Actions.CanCancel));
        Assert.Empty(await ReadAsync<List<MarketPriceDto>>(await head.GetAsync(pricesUrl, Ct)));

        // Chỉ quyền xem: đọc được, không lập được.
        var role = UniqueCode("K");
        await _factory.CreateRoleAsync(role, Permissions.Pricing.PriceBankView);
        var viewerName = $"kv.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(viewerName, "TCT", role);
        var viewer = await _factory.CreateClientInCompanyAsync(viewerName, ProcApiFactory.TestPassword, "TCT");
        Assert.Contains((await ReadAsync<List<MarketSurveyListItemDto>>(await viewer.GetAsync(Url, Ct))), r => r.Id == draft.Id);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsJsonAsync(Url, Request(Line(item, null, "Chợ", 1)), Ct)).StatusCode);
    }
}
