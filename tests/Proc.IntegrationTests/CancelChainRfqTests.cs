using System.Net.Http.Json;
using Proc.Application.Common;
using Proc.Application.Pricing;
using Proc.Application.Purchasing;
using Proc.Application.Sourcing;

namespace Proc.IntegrationTests;

/// <summary>
/// Lát Z4c (người dùng chốt 11/11/2026, design.md mục 20.2 E2, E3): RFQ đã chọn thầu hủy được theo dây chuyền – hủy PO tạo từ RFQ; hợp đồng,
/// bảng giá tạo từ RFQ chỉ hủy kèm khi còn Nháp, đã gửi duyệt / hiệu lực thì chặn.
/// </summary>
public partial class SourcingTests
{
    [Fact]
    public async Task HuyDayChuyenRfq_DaChonThau_HuyPoHopDongBangGiaNhap_ChanKhiHopDongDaGuiDuyet()
    {
        var f = await ArrangeAsync();
        var (a, b, c) = (f.Suppliers[0], f.Suppliers[1], f.Suppliers[2]);
        var rfq = await ReadAsync<RfqDto>(await f.Client.PostAsJsonAsync("/api/rfqs", Rfq(f, null, a, b, c), Ct));
        rfq = await ReadAsync<RfqDto>(await f.Client.PostAsync($"/api/rfqs/{rfq.Id}/send", null, Ct));
        var (cai, thung) = (rfq.Lines[0].Id, rfq.Lines[1].Id);
        await ReadAsync<RfqDto>(await f.Client.PutAsJsonAsync($"/api/rfqs/{rfq.Id}/quotations/{a}", Quote(Q(cai, 50_000), Q(thung, 600_000, 5)), Ct));
        await ReadAsync<RfqDto>(await f.Client.PutAsJsonAsync($"/api/rfqs/{rfq.Id}/quotations/{b}", Quote(Q(cai, 52_000)), Ct));
        await ReadAsync<RfqDto>(await f.Client.PostAsJsonAsync($"/api/rfqs/{rfq.Id}/award",
            new AwardRfqRequest([new(cai, a, null), new(thung, a, null)], "Thử nghiệm: NCC khác không chào giá"), Ct));
        await ReadAsync<RfqDto>(await (await ApproverAsync(f)).PostAsJsonAsync($"/api/rfqs/{rfq.Id}/approve", new ApprovalActionRequest(null), Ct));
        var order = Assert.Single(await ReadAsync<List<CreatedOrderDto>>(await f.Client.PostAsync($"/api/rfqs/{rfq.Id}/create-orders", null, Ct)));
        var list = Assert.Single(await ReadAsync<List<CreatedPriceListDto>>(await f.Client.PostAsync($"/api/rfqs/{rfq.Id}/create-price-lists", null, Ct)));
        var contract = Assert.Single(await ReadAsync<List<CreatedContractDto>>(await f.Client.PostAsync($"/api/rfqs/{rfq.Id}/create-contracts", null, Ct)));
        Assert.True((await ReadAsync<RfqDto>(await f.Client.GetAsync($"/api/rfqs/{rfq.Id}", Ct))).Actions.CanCancel);

        // Hợp đồng tạo từ RFQ đã gửi duyệt → chặn cả chuỗi, không phiếu nào đổi.
        await ReadAsync<ContractDto>(await f.Client.PostAsync($"/api/contracts/{contract.ContractId}/submit", null, Ct));
        var blocked = await ReadAsync<CancelChainDto>(await f.Client.GetAsync($"/api/cancel-chain/RFQ/{rfq.Id}", Ct));
        Assert.Contains("Chấm dứt", blocked.BlockedReason);
        Assert.Contains("Không hủy được", await TitleAsync(await f.Client.PostAsJsonAsync($"/api/cancel-chain/RFQ/{rfq.Id}", new CancelChainRequest("Thử"), Ct)));
        Assert.Equal("Awarded", (await ReadAsync<RfqDto>(await f.Client.GetAsync($"/api/rfqs/{rfq.Id}", Ct))).Status);

        // Rút hợp đồng về nháp → hủy được: PO, hợp đồng, bảng giá (đều nháp) rồi RFQ.
        await ReadAsync<ContractDto>(await f.Client.PostAsync($"/api/contracts/{contract.ContractId}/recall", null, Ct));
        var chain = await ReadAsync<CancelChainDto>(await f.Client.GetAsync($"/api/cancel-chain/RFQ/{rfq.Id}", Ct));
        Assert.Null(chain.BlockedReason);
        Assert.Equal(["PO", "CTR", "PRL", "RFQ"], chain.Items.Select(i => i.DocType));
        await ReadAsync<CancelChainDto>(await f.Client.PostAsJsonAsync($"/api/cancel-chain/RFQ/{rfq.Id}", new CancelChainRequest("Mời thầu lại"), Ct));

        Assert.Equal("Cancelled", (await ReadAsync<RfqDto>(await f.Client.GetAsync($"/api/rfqs/{rfq.Id}", Ct))).Status);
        Assert.Equal("Cancelled", (await ReadAsync<PurchaseOrderDto>(await f.Client.GetAsync($"/api/purchase-orders/{order.PurchaseOrderId}", Ct))).Status);
        Assert.Equal("Cancelled", (await ReadAsync<ContractDto>(await f.Client.GetAsync($"/api/contracts/{contract.ContractId}", Ct))).Status);
        var priceList = await ReadAsync<PriceListDto>(await f.Client.GetAsync($"/api/price-lists/{list.PriceListId}", Ct));
        Assert.Equal("Cancelled", priceList.Status);
    }
}
