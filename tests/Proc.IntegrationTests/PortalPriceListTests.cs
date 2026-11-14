using System.Net;
using System.Net.Http.Json;
using Proc.Application.MasterData;
using Proc.Application.Portal;
using Proc.Application.Pricing;

namespace Proc.IntegrationTests;

/// <summary>
/// Lát P7a (design.md mục 19.5, P7-1…P7-4): NCC xem bảng giá của mình trên Portal – chỉ bảng đã duyệt (hiệu lực / ngừng), tình trạng theo
/// ngày hôm nay, dòng giá sau chiết khấu; không thấy bảng nháp, chờ duyệt, bảng của NCC khác; token nội bộ không gọi được.
/// </summary>
public partial class PurchasingTests
{
    [Fact]
    public async Task PortalBangGia_ChiBangDaDuyet_TinhTrangTheoNgay_GiaSauChietKhau_CoLapNcc()
    {
        var f = await ArrangeAsync();
        var ncc = await PortalClientAsync(f.Buyer, f.SupplierId);

        async Task<PriceListDto> ListAsync(string title, DateOnly from, DateOnly? to, bool approve, decimal price = 50_000)
        {
            var list = await ReadAsync<PriceListDto>(await f.Buyer.PostAsJsonAsync("/api/price-lists",
                new SavePriceListRequest(f.SupplierId, title, false, from, to, "Giao tại kho bên mua", "Ghi chú nội bộ",
                    [new(null, f.ItemId, f.Cai, 10, price, 4, null, "Ghi chú dòng nội bộ")]), Ct));
            if (!approve) return list;
            await ReadAsync<PriceListDto>(await f.Buyer.PostAsync($"/api/price-lists/{list.Id}/submit", null, Ct));
            await _factory.FinishPriceListApprovalAsync(list.Id);
            return list;
        }

        var draft = await ListAsync("Bảng nháp", Today, null, approve: false);
        var submitted = await ListAsync("Bảng chờ duyệt", Today, null, approve: false);
        await ReadAsync<PriceListDto>(await f.Buyer.PostAsync($"/api/price-lists/{submitted.Id}/submit", null, Ct));
        var upcoming = await ListAsync("Bảng tháng sau", Today.AddDays(1), null, approve: true);
        var expired = await ListAsync("Bảng đã hết hạn", Today.AddDays(-30), Today.AddDays(-1), approve: true);
        var stopped = await ListAsync("Bảng đã ngừng", Today.AddDays(-10), null, approve: true);
        await ReadAsync<PriceListDto>(await f.Buyer.PostAsync($"/api/price-lists/{stopped.Id}/deactivate", null, Ct));

        // Danh sách: bảng của fixture (đang áp dụng) + 3 bảng đã duyệt; không có nháp, chờ duyệt. Đang áp dụng, chưa đến ngày lên trước.
        var lists = await ReadAsync<List<PortalPriceListDto>>(await ncc.GetAsync("/api/portal/price-lists", Ct));
        Assert.DoesNotContain(lists, l => l.Id == draft.Id || l.Id == submitted.Id);
        Assert.Equal(["Current", "Upcoming", "Expired", "Inactive"], lists.Select(l => l.Status).ToArray());
        Assert.Equal((upcoming.Id, 1, "Giao tại kho bên mua"), (lists[1].Id, lists[1].LineCount, lists[1].DeliveryTerms));
        Assert.Equal(expired.Id, lists[2].Id);
        Assert.Equal(stopped.Id, lists[3].Id);
        Assert.Equal("Công ty thử mua hàng", lists[0].CompanyName);

        // Chi tiết: đơn giá sau CK = 50.000 × 96% = 48.000; thuế suất trống thì theo mặt hàng (10%); không có ghi chú nội bộ.
        var detail = await ReadAsync<PortalPriceListDetailDto>(await ncc.GetAsync($"/api/portal/price-lists/{upcoming.Id}", Ct));
        var line = Assert.Single(detail.Lines);
        Assert.Equal(("CAI", 10m, 50_000m, 4m, 48_000m, "10"), (line.UomCode, line.MinQty, line.UnitPrice, line.DiscountPct, line.NetPrice, line.VatCode));
        Assert.Equal("Dây curoa", line.ItemName);
        Assert.Equal(upcoming.No, detail.Header.No);
        var json = await (await ncc.GetAsync($"/api/portal/price-lists/{upcoming.Id}", Ct)).Content.ReadAsStringAsync(Ct);
        Assert.DoesNotContain("nội bộ", json, StringComparison.Ordinal);

        // Bảng nháp / chờ duyệt mở thẳng → 404 như không tồn tại.
        Assert.Equal(HttpStatusCode.NotFound, (await ncc.GetAsync($"/api/portal/price-lists/{draft.Id}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await ncc.GetAsync($"/api/portal/price-lists/{submitted.Id}", Ct)).StatusCode);

        // NCC khác không thấy bảng giá của NCC này.
        var headName = $"hd.{Guid.NewGuid():N}"[..16];
        await _factory.CreateUserAsync(headName, ProcApiFactory.TestPassword, ["TCT"]);
        var head = await _factory.CreateClientInCompanyAsync(headName, ProcApiFactory.TestPassword, "TCT");
        var groupId = (await head.GetFromJsonAsync<List<SupplierGroupDto>>("/api/supplier-groups", Ct))!.Single(g => g.Code == "TN").Id;
        var other = await ReadAsync<SupplierDetailDto>(await head.PostAsJsonAsync("/api/suppliers",
            new SaveSupplierRequest(null, groupId, "NCC khác – bảng giá Portal", null, false, null, null, null, null, null, null, null, null,
                "Approved", null, null, [], []), Ct));
        await _factory.FinishSupplierApprovalAsync(other.Id);
        var otherNcc = await PortalClientAsync(head, other.Id);
        Assert.Empty(await ReadAsync<List<PortalPriceListDto>>(await otherNcc.GetAsync("/api/portal/price-lists", Ct)));
        Assert.Equal(HttpStatusCode.NotFound, (await otherNcc.GetAsync($"/api/portal/price-lists/{upcoming.Id}", Ct)).StatusCode);

        // Token nội bộ không gọi được API Portal.
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Buyer.GetAsync("/api/portal/price-lists", Ct)).StatusCode);
    }
}
