using Proc.Domain.Common;
using Proc.Domain.MasterData;
using Proc.Domain.Sourcing;

namespace Proc.Domain.Tests.Sourcing;

/// <summary>Kết quả chọn thầu NCC thấy trên Portal (lát P4b – design.md mục 19.5, P4-7).</summary>
public sealed class RfqSupplierResultTests
{
    private static readonly Guid Company = Guid.CreateVersion7();
    private static readonly Uom Cai = Uom.Create("CAI", "Cái");
    private static readonly Uom Thung = Uom.Create("THUNG", "Thùng");
    private static readonly DateOnly Today = new(2026, 11, 13);
    private static readonly DateTime Now = Today.ToDateTime(new TimeOnly(9, 0));

    private static Item NewItem()
    {
        var item = Item.Create("VB01", ItemType.Inventory, ItemCategory.Create("VT", "Vật tư"), Cai,
            new ItemDescription("Vòng bi", null, null, null, null, null, "10", null));
        item.SetConversions([(Thung, 12m)]);
        return item;
    }

    [Fact]
    public void KetQua_ChiCoSauKhiDuyetXong_TungNccChiThayCuaMinh()
    {
        var item = NewItem();
        Guid a = Guid.CreateVersion7(), b = Guid.CreateVersion7(), c = Guid.CreateVersion7(), d = Guid.CreateVersion7();
        var rfq = Rfq.Create(Company, "RFQ-P4B", new RfqHeader(Today, "Vòng bi", Today.AddDays(7), null, RfqWeights.Default, null));
        rfq.SetLines([(new RfqLineData(null, item.Id, Cai.Id, 100, null, null), item), (new RfqLineData(null, item.Id, Thung.Id, 10, null, null), item)]);
        rfq.SetSuppliers([a, b, c, d]);
        rfq.Send(DateTimeOffset.UtcNow, 1, null);
        var (cai, thung) = (rfq.Lines[0].Id, rfq.Lines[1].Id);
        var header = new QuotationHeader(Today, null, null, 8, 8, 8, 8, null);
        rfq.SaveQuotation(a, header, [new(cai, 50_000, 0, "10", 7, null), new(thung, 600_000, 5, "10", 7, null)], Now);
        rfq.SaveQuotation(b, header, [new(cai, 48_000, 0, "8", 5, null)], Now);
        rfq.SaveQuotation(c, header, [new(cai, 55_000, 0, "10", 7, null), new(thung, 650_000, 0, "10", 7, null)], Now);
        rfq.Award([new(cai, b, null), new(thung, a, null)], Guid.CreateVersion7(), Today, 1, null);
        rfq.Submit();

        // Đang duyệt kết quả: chưa lộ cho NCC.
        Assert.Null(rfq.ResultFor(a));
        Assert.Throws<BusinessRuleException>(() => rfq.MarkResultSent(Guid.CreateVersion7(), DateTimeOffset.UtcNow));

        rfq.MarkApproved(DateTimeOffset.UtcNow);

        // A chào 2 dòng, trúng dòng thùng: 10 × 600.000 × 95% = 5.700.000.
        var ra = rfq.ResultFor(a)!;
        Assert.Equal((RfqSupplierOutcome.PartlyWon, 2), (ra.Outcome, ra.QuotedLineCount));
        var won = Assert.Single(ra.WonLines);
        Assert.Equal((thung, 600_000m, 5m, "10", 5_700_000m), (won.RfqLineId, won.UnitPrice, won.DiscountPct, won.VatCode, won.Amount));

        // B chỉ chào dòng cái và trúng: trúng mọi dòng đã chào.
        var rb = rfq.ResultFor(b)!;
        Assert.Equal((RfqSupplierOutcome.Won, 1, 4_800_000m), (rb.Outcome, rb.QuotedLineCount, rb.WonLines.Sum(l => l.Amount)));

        Assert.Equal(RfqSupplierOutcome.Lost, rfq.ResultFor(c)!.Outcome);
        Assert.Empty(rfq.ResultFor(c)!.WonLines);
        Assert.Equal((RfqSupplierOutcome.NoQuote, 0), (rfq.ResultFor(d)!.Outcome, rfq.ResultFor(d)!.QuotedLineCount));
        // NCC không được mời: không có kết quả.
        Assert.Null(rfq.ResultFor(Guid.CreateVersion7()));

        // Phòng CUVT tự bấm gửi kết quả, một lần.
        var sender = Guid.CreateVersion7();
        rfq.MarkResultSent(sender, DateTimeOffset.UtcNow);
        Assert.Equal(sender, rfq.ResultSentBy);
        Assert.NotNull(rfq.ResultSentAt);
        Assert.Contains("đã gửi", Assert.Throws<BusinessRuleException>(() => rfq.MarkResultSent(sender, DateTimeOffset.UtcNow)).Message);
    }

    [Fact]
    public void KetQua_RfqBiHuySauDuyet_KhongConKetQua()
    {
        var item = NewItem();
        var a = Guid.CreateVersion7();
        var rfq = Rfq.Create(Company, "RFQ-P4B2", new RfqHeader(Today, "Vòng bi", Today.AddDays(7), null, RfqWeights.Default, null));
        rfq.SetLines([(new RfqLineData(null, item.Id, Cai.Id, 1, null, null), item)]);
        rfq.SetSuppliers([a]);
        rfq.Send(DateTimeOffset.UtcNow, 1, null);
        rfq.SaveQuotation(a, new QuotationHeader(Today, null, null, 0, 0, 0, 0, null), [new(rfq.Lines[0].Id, 1_000, 0, null, null, null)], Now);
        rfq.Award([new(rfq.Lines[0].Id, a, null)], Guid.CreateVersion7(), Today, 1, null);
        rfq.Submit();
        rfq.MarkApproved(DateTimeOffset.UtcNow);
        Assert.Equal(RfqSupplierOutcome.Won, rfq.ResultFor(a)!.Outcome);

        rfq.Cancel("Hủy dây chuyền", inChain: true);
        Assert.Null(rfq.ResultFor(a));
    }
}
