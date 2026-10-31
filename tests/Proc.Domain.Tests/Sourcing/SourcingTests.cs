using Proc.Domain.Common;
using Proc.Domain.MasterData;
using Proc.Domain.Pricing;
using Proc.Domain.Sourcing;

namespace Proc.Domain.Tests.Sourcing;

/// <summary>Bảng giá NCC (design.md mục 8.1), RFQ – báo giá – chọn thầu và chấm điểm (mục 8.4).</summary>
public class SourcingTests
{
    private static readonly Guid Company = Guid.CreateVersion7();
    private static readonly Uom Cai = Uom.Create("CAI", "Cái");
    private static readonly Uom Thung = Uom.Create("THUNG", "Thùng");
    private static readonly DateOnly Today = new(2026, 10, 31);

    private static Item NewItem()
    {
        var item = Item.Create("VB01", ItemType.Inventory, ItemCategory.Create("VT", "Vật tư"), Cai,
            new ItemDescription("Vòng bi", null, null, null, null, null, "10", null));
        item.SetConversions([(Thung, 12m)]);
        return item;
    }

    [Fact]
    public void BangGia_GiaQuyDoi_TrungBac_HieuLucKhongSua()
    {
        var item = NewItem();
        var list = SupplierPriceList.Create(Company, "PRL-1", new PriceListHeader(Guid.CreateVersion7(), "Bảng giá", false, Today, null, null, null));
        list.SetLines([(new PriceListLineData(null, item.Id, Thung.Id, 0, 600_000, 5, null, null), item)]);
        // 600.000 đ/thùng 12 cái, CK 5% → 570.000 / 12 = 47.500 đ/cái.
        Assert.Equal(47_500m, list.Lines[0].BaseNetPrice);

        Assert.Throws<BusinessRuleException>(() => list.SetLines([
            (new PriceListLineData(null, item.Id, Cai.Id, 10, 1, 0, null, null), item),
            (new PriceListLineData(null, item.Id, Cai.Id, 10, 2, 0, null, null), item),
        ]));
        Assert.True(list.AppliesOn(Today) is false);
        list.Activate(DateTimeOffset.UtcNow);
        Assert.True(list.AppliesOn(Today));
        Assert.False(list.AppliesOn(Today.AddDays(-1)));
        Assert.Throws<BusinessRuleException>(() => list.SetLines([(new PriceListLineData(null, item.Id, Cai.Id, 0, 1, 0, null, null), item)]));
        list.Deactivate();
        Assert.False(list.AppliesOn(Today));
    }

    private static Rfq SentRfq(Item item, params Guid[] suppliers)
    {
        var rfq = Rfq.Create(Company, "RFQ-1", new RfqHeader(Today, "Vòng bi quý 4", Today.AddDays(7), null, RfqWeights.Default, null));
        rfq.SetLines([(new RfqLineData(null, item.Id, Cai.Id, 100, null, null), item), (new RfqLineData(null, item.Id, Thung.Id, 10, null, null), item)]);
        rfq.SetSuppliers(suppliers);
        rfq.Send(DateTimeOffset.UtcNow);
        return rfq;
    }

    [Fact]
    public void Rfq_GuiItHon3Ncc_TrongSoKhacTram_BiChan()
    {
        var item = NewItem();
        Assert.Throws<BusinessRuleException>(() =>
            Rfq.Create(Company, "RFQ-1", new RfqHeader(Today, "Vòng bi", Today.AddDays(7), null, new RfqWeights(50, 20, 10, 5, 5), null)));
        Assert.Throws<BusinessRuleException>(() =>
            Rfq.Create(Company, "RFQ-1", new RfqHeader(Today, "Vòng bi", Today.AddDays(-1), null, RfqWeights.Default, null)));
        var ex = Assert.Throws<BusinessRuleException>(() => SentRfq(item, Guid.CreateVersion7(), Guid.CreateVersion7()));
        Assert.Contains("ít nhất 3", ex.Message);
    }

    [Fact]
    public void ChonThau_KhongChonGiaThapNhatPhaiCoLyDo()
    {
        var item = NewItem();
        Guid a = Guid.CreateVersion7(), b = Guid.CreateVersion7(), c = Guid.CreateVersion7();
        var rfq = SentRfq(item, a, b, c);
        var (cai, thung) = (rfq.Lines[0].Id, rfq.Lines[1].Id);
        var header = new QuotationHeader(Today, null, "30 ngày", 8, 10, 10, 6, null);
        rfq.SaveQuotation(a, header, [new(cai, 50_000, 0, "10", 7, null), new(thung, 600_000, 5, "10", 7, null)]);
        rfq.SaveQuotation(b, header, [new(cai, 48_000, 0, "10", 14, null)]);
        Assert.Throws<BusinessRuleException>(() => rfq.SaveQuotation(Guid.CreateVersion7(), header, [new(cai, 1, 0, null, null, null)]));

        // Dòng thùng: B không chào → không chọn được B.
        Assert.Throws<BusinessRuleException>(() => rfq.Award([new(thung, b, null)], DateTimeOffset.UtcNow, Today));
        // Dòng cái: A đắt hơn B → chọn A phải có lý do.
        Assert.Throws<BusinessRuleException>(() => rfq.Award([new(cai, a, null)], DateTimeOffset.UtcNow, Today));
        rfq.Award([new(cai, a, "Giao nhanh hơn"), new(thung, a, null)], DateTimeOffset.UtcNow, Today);
        Assert.Equal((RfqStatus.Awarded, a, "Giao nhanh hơn"), (rfq.Status, rfq.Lines[0].AwardedSupplierId, rfq.Lines[0].AwardReason));
        Assert.Throws<BusinessRuleException>(() => rfq.LinkPurchaseOrder(b, Guid.CreateVersion7()));
        rfq.LinkPurchaseOrder(a, Guid.CreateVersion7());
        Assert.Throws<BusinessRuleException>(() => rfq.LinkPurchaseOrder(a, Guid.CreateVersion7()));
        Assert.Throws<BusinessRuleException>(() => rfq.Cancel());
    }

    [Fact]
    public void ChamDiem_TheoTrongSo()
    {
        // Dòng 1 thấp nhất (10 điểm), dòng 2 đắt hơn 25% (8 điểm) → 9.
        Assert.Equal(9m, QuotationScoring.PriceScore([(100m, 100m), (100m, 125m)]));
        Assert.Equal(8.8m, QuotationScoring.Total(RfqWeights.Default, 9, 8, 10, 10, 6));
    }

    [Fact]
    public void TimNguon_TheoDoiTienDoTuRfq_KhongLuiTrangThai()
    {
        var supplier = Guid.CreateVersion7();
        var request = SourcingRequest.Create(Company, "SRC-1", new SourcingHeader(Today, null, "Vòng bi chịu nhiệt", null, 50, null, "Anh Nam", null));
        Assert.Throws<BusinessRuleException>(() => request.Update(new SourcingHeader(Today, null, " ", null, null, null, null, null)));
        request.TrackRfq(supplier, CandidateStatus.RfqSent);
        Assert.Equal(CandidateStatus.RfqSent, request.Candidates.Single().Status);
        request.SetCandidates([new(request.Candidates[0].Id, supplier, CandidateStatus.Passed, "Có ISO")]);
        request.TrackRfq(supplier, CandidateStatus.Quoted);
        Assert.Equal(CandidateStatus.Passed, request.Candidates.Single().Status);
        request.Close();
        Assert.Throws<BusinessRuleException>(() => request.SetCandidates([]));
    }
}
