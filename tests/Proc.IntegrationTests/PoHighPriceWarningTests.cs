using System.Net.Http.Json;
using Proc.Application.Organization;
using Proc.Application.Pricing;
using Proc.Domain.Organization;

namespace Proc.IntegrationTests;

/// <summary>
/// Cảnh báo giá cao trên PO (design.md mục 8.5, 10.3): giá thấp nhất tập đoàn theo mặt hàng (cùng cách tính với So sánh giá toàn tập đoàn)
/// và % cảnh báo của công ty đang làm việc (tham số SEC).
/// </summary>
public partial class SupplierPricingTests
{
    [Fact]
    public async Task CanhBaoGiaCaoPo_GiaThapNhatTapDoan_AnChiTietCongTyKhac_NguongTheoCongTy()
    {
        var f = await ArrangeAsync();
        var a = await MemberAsync(f);
        var b = await MemberAsync(f);
        await ReceiveAsync(f, a, 10, 100_000);
        await ReceiveAsync(f, b, 10, 120_000);
        var url = $"/api/purchase-orders/group-min-prices?itemIds={f.ItemId}&itemIds={Guid.NewGuid()}";

        // Người lập PO ở B (không có quyền xem giá toàn tập đoàn): thấy giá thấp nhất tập đoàn (của A) nhưng không thấy A, NCC, số phiếu.
        var roleCode = UniqueCode("P");
        await _factory.CreateRoleAsync(roleCode, Permissions.Purchase.PoCreate);
        var userName = $"po.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(userName, b.Code, roleCode);
        var buyer = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, b.Code);
        var hidden = await ReadAsync<GroupMinPricesDto>(await buyer.GetAsync(url, Ct));
        Assert.Equal(CompanyParameter.DefaultPoHighPriceWarnPercent, hidden.WarnPercent);
        var min = Assert.Single(hidden.Items);
        Assert.Equal((f.ItemId, 100_000m, "Receipt", Today), (min.ItemId, min.BasePrice, min.Source, min.Date));
        Assert.Equal(((string?)null, (string?)null, (string?)null), (min.CompanyCode, min.SupplierCode, min.DocumentNo));

        // Người có quyền xem giá toàn tập đoàn (quản trị của fixture) thấy chi tiết.
        var shown = Assert.Single((await ReadAsync<GroupMinPricesDto>(await b.Client.GetAsync(url, Ct))).Items);
        Assert.Equal(a.Code, shown.CompanyCode);
        Assert.NotNull(shown.DocumentNo);

        // Ngưỡng là tham số riêng của từng công ty: B đổi 5% (gõ kiểu Việt Nam "5,0"), A vẫn 10%.
        (await b.Client.PutAsJsonAsync($"/api/system-parameters/{SystemParameterCodes.PoHighPriceWarnPercent}", new UpdateSystemParameterRequest("5,0"), Ct))
            .EnsureSuccessStatusCode();
        Assert.Equal(5m, (await ReadAsync<GroupMinPricesDto>(await buyer.GetAsync(url, Ct))).WarnPercent);
        Assert.Equal(10m, (await ReadAsync<GroupMinPricesDto>(await a.Client.GetAsync(url, Ct))).WarnPercent);
        var bad = await b.Client.PutAsJsonAsync($"/api/system-parameters/{SystemParameterCodes.PoHighPriceWarnPercent}", new UpdateSystemParameterRequest("-1"), Ct);
        Assert.False(bad.IsSuccessStatusCode);
    }
}
