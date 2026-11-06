using System.Net;
using System.Net.Http.Json;
using Proc.Application.MasterData;
using Proc.Application.Pricing;
using Proc.Application.Purchasing;
using Proc.Application.Sourcing;

namespace Proc.IntegrationTests;

/// <summary>
/// Sinh hợp đồng nguyên tắc từ kết quả chọn thầu (design.md mục 8.6, 18.7): mỗi NCC trúng thầu một hợp đồng nháp gắn bảng giá đã sinh từ
/// chính RFQ, sản lượng dự kiến = SL trúng thầu; không tạo trùng, hợp đồng đã hủy thì tạo lại được.
/// </summary>
public partial class SourcingTests
{
    [Fact]
    public async Task TaoHopDongNguyenTacTuKetQuaThau()
    {
        var f = await ArrangeAsync();
        var (a, b, c) = (f.Suppliers[0], f.Suppliers[1], f.Suppliers[2]);
        var rfq = await ReadAsync<RfqDto>(await f.Client.PostAsJsonAsync("/api/rfqs", Rfq(f, null, a, b, c), Ct));
        rfq = await ReadAsync<RfqDto>(await f.Client.PostAsync($"/api/rfqs/{rfq.Id}/send", null, Ct));
        var (cai, thung) = (rfq.Lines[0].Id, rfq.Lines[1].Id);
        await ReadAsync<RfqDto>(await f.Client.PutAsJsonAsync($"/api/rfqs/{rfq.Id}/quotations/{a}", Quote(Q(cai, 50_000), Q(thung, 600_000, 5)), Ct));
        await ReadAsync<RfqDto>(await f.Client.PutAsJsonAsync($"/api/rfqs/{rfq.Id}/quotations/{b}", Quote(Q(cai, 52_000)), Ct));
        rfq = await ReadAsync<RfqDto>(await f.Client.PostAsJsonAsync($"/api/rfqs/{rfq.Id}/award", new AwardRfqRequest([new(cai, a, null), new(thung, a, null)], "Thử nghiệm: NCC khác không chào giá"), Ct));
        var url = $"/api/rfqs/{rfq.Id}/create-contracts";
        Assert.Contains("chưa được duyệt", await TitleAsync(await f.Client.PostAsync(url, null, Ct)));
        await ReadAsync<RfqDto>(await (await ApproverAsync(f)).PostAsJsonAsync($"/api/rfqs/{rfq.Id}/approve", new ApprovalActionRequest(null), Ct));

        // Chưa "Tạo bảng giá" thì chưa tạo hợp đồng được – hợp đồng phải gắn bảng giá dùng chung.
        Assert.Contains("Tạo bảng giá", await TitleAsync(await f.Client.PostAsync(url, null, Ct)));
        var priceList = Assert.Single(await ReadAsync<List<CreatedPriceListDto>>(await f.Client.PostAsync($"/api/rfqs/{rfq.Id}/create-price-lists", null, Ct)));
        Assert.True((await ReadAsync<RfqDto>(await f.Client.GetAsync($"/api/rfqs/{rfq.Id}", Ct))).Actions.CanCreateContracts);

        // Hợp đồng nháp: bảng giá từ RFQ, hiệu lực theo bảng giá (hết hiệu lực báo giá), thanh toán theo báo giá, sản lượng = SL trúng thầu.
        // Giá trị dự kiến = 100 × 50.000 + 10 × 600.000 × 95% = 10.700.000.
        var created = Assert.Single(await ReadAsync<List<CreatedContractDto>>(await f.Client.PostAsync(url, null, Ct)));
        var contract = await ReadAsync<ContractDto>(await f.Client.GetAsync($"/api/contracts/{created.ContractId}", Ct));
        Assert.Equal(("Draft", a, priceList.PriceListId, Today, Today.AddDays(30), "Thanh toán 30 ngày", 10_700_000m),
            (contract.Status, contract.SupplierId, contract.PriceListId, contract.ValidFrom, contract.ValidTo, contract.PaymentTerms, contract.ExpectedAmount));
        Assert.Equal([(f.Cai, 100m), (f.Thung, 10m)], contract.Lines.Select(l => (l.UomId, l.ExpectedQty)));
        Assert.Contains(rfq.No, contract.Note);

        rfq = await ReadAsync<RfqDto>(await f.Client.GetAsync($"/api/rfqs/{rfq.Id}", Ct));
        Assert.Equal(contract.No, rfq.Suppliers.Single(s => s.SupplierId == a).ContractNo);
        Assert.False(rfq.Actions.CanCreateContracts);
        Assert.Contains("đã có hợp đồng", await TitleAsync(await f.Client.PostAsync(url, null, Ct)));

        // Hợp đồng đã hủy thì tạo lại được (hợp đồng mới).
        await ReadAsync<ContractDto>(await f.Client.PostAsync($"/api/contracts/{contract.Id}/cancel", null, Ct));
        rfq = await ReadAsync<RfqDto>(await f.Client.GetAsync($"/api/rfqs/{rfq.Id}", Ct));
        Assert.Null(rfq.Suppliers.Single(s => s.SupplierId == a).ContractNo);
        var again = Assert.Single(await ReadAsync<List<CreatedContractDto>>(await f.Client.PostAsync(url, null, Ct)));
        Assert.NotEqual(contract.Id, again.ContractId);

        // Không có quyền lập hợp đồng (hội đồng chỉ xem + duyệt RFQ) thì không tạo được.
        Assert.Equal(HttpStatusCode.Forbidden, (await (await ApproverAsync(f)).PostAsync(url, null, Ct)).StatusCode);
        // RFQ ở công ty thành viên: bảng giá chỉ của công ty (thành viên không lập được bảng toàn tập đoàn).
        Assert.False((await ReadAsync<PriceListDto>(await f.Client.GetAsync($"/api/price-lists/{priceList.PriceListId}", Ct))).IsGroupWide);
    }

    [Fact]
    public async Task TaoBangGiaTuRfqTongCongTy_DanhDauSanToanTapDoan_BoDanhDauDuocKhiNhap()
    {
        // Yêu cầu người dùng 03/11/2026: RFQ lập ở tổng công ty thì bảng giá sinh ra mặc định "toàn tập đoàn"; còn nháp thì bỏ được.
        var f = await ArrangeAsync();
        var (a, b) = (f.Suppliers[0], f.Suppliers[1]);
        foreach (var s in new[] { a, b })
            await ReadAsync<SupplierCompanyDto>(await f.Head.PutAsJsonAsync($"/api/suppliers/{s}/company", new SaveSupplierCompanyRequest(null, null, true), Ct));
        var rfq = await ReadAsync<RfqDto>(await f.Head.PostAsJsonAsync("/api/rfqs", Rfq(f, null, a, b), Ct));
        rfq = await ReadAsync<RfqDto>(await f.Head.PostAsJsonAsync($"/api/rfqs/{rfq.Id}/send", new SendRfqRequest("Thử tổng công ty"), Ct));
        var (cai, thung) = (rfq.Lines[0].Id, rfq.Lines[1].Id);
        await ReadAsync<RfqDto>(await f.Head.PutAsJsonAsync($"/api/rfqs/{rfq.Id}/quotations/{a}", Quote(Q(cai, 50_000), Q(thung, 600_000)), Ct));
        await ReadAsync<RfqDto>(await f.Head.PostAsJsonAsync($"/api/rfqs/{rfq.Id}/award", new AwardRfqRequest([new(cai, a, null), new(thung, a, null)], "Thử nghiệm: NCC khác không chào giá"), Ct));
        await _factory.FinishRfqAwardApprovalAsync(rfq.Id);

        var created = Assert.Single(await ReadAsync<List<CreatedPriceListDto>>(await f.Head.PostAsync($"/api/rfqs/{rfq.Id}/create-price-lists", null, Ct)));
        var list = await ReadAsync<PriceListDto>(await f.Head.GetAsync($"/api/price-lists/{created.PriceListId}", Ct));
        Assert.Equal(("Draft", true), (list.Status, list.IsGroupWide));

        // Người lập bỏ đánh dấu trước khi gửi duyệt: bảng nháp sửa được như lập tay.
        var request = new SavePriceListRequest(list.SupplierId, list.Title, false, list.ValidFrom, list.ValidTo, list.DeliveryTerms, list.Note,
            list.Lines.Select(l => new SavePriceListLineRequest(l.Id, l.ItemId, l.UomId, l.MinQty, l.UnitPrice, l.DiscountPct, l.VatCode, l.Note)).ToList());
        Assert.False((await ReadAsync<PriceListDto>(await f.Head.PutAsJsonAsync($"/api/price-lists/{list.Id}", request, Ct))).IsGroupWide);
    }
}
