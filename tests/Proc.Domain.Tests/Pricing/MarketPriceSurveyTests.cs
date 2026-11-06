using Proc.Domain.Common;
using Proc.Domain.MasterData;
using Proc.Domain.Pricing;

namespace Proc.Domain.Tests.Pricing;

/// <summary>Phiếu khảo sát giá thị trường (design.md mục 18.25 – lát G4a): đầu phiếu, dòng giá theo nguồn, chốt, hủy.</summary>
public class MarketPriceSurveyTests
{
    private static readonly Guid Company = Guid.CreateVersion7();
    private static readonly Guid Category = Guid.CreateVersion7();
    private static readonly Guid Supplier = Guid.CreateVersion7();
    private static readonly Uom Cai = Uom.Create("CAI", "Cái");
    private static readonly Uom Thung = Uom.Create("THUNG", "Thùng");
    private static readonly DateOnly Today = new(2026, 11, 6);

    private static Item NewItem()
    {
        var item = Item.Create("VB01", ItemType.Inventory, ItemCategory.Create("VT", "Vật tư"), Cai,
            new ItemDescription("Vòng bi", null, null, null, null, null, "10", null));
        item.SetConversions([(Thung, 12m)]);
        return item;
    }

    private static MarketSurveyHeader Header(DateOnly? date = null) => new(date ?? Today, Category, " Khảo sát chợ Kim Biên ");

    private static MarketSurveyLineData Line(Item item, Guid? supplier = null, string? source = "Cửa hàng A", decimal price = 50_000m, Guid? uom = null) =>
        new(null, item.Id, uom ?? Cai.Id, supplier, source, price, null);

    [Fact]
    public void DauPhieu_KhongSauHomNay_CoNhomHang()
    {
        var survey = MarketPriceSurvey.Create(Company, "KSG-1", Header(), Today);
        Assert.Equal(("Khảo sát chợ Kim Biên", MarketSurveyStatus.Draft), (survey.Note, survey.Status));
        Assert.Throws<BusinessRuleException>(() => MarketPriceSurvey.Create(Company, "KSG-1", Header(Today.AddDays(1)), Today));
        Assert.Throws<BusinessRuleException>(() => MarketPriceSurvey.Create(Company, "KSG-1", Header() with { ItemCategoryId = Guid.Empty }, Today));
    }

    [Fact]
    public void Dong_CanNguon_KhongTrungNguon_QuyGiaVeDvtCoSo()
    {
        var item = NewItem();
        var survey = MarketPriceSurvey.Create(Company, "KSG-1", Header(), Today);
        // Không NCC, không tên nguồn: không truy được nguồn giá.
        Assert.Throws<BusinessRuleException>(() => survey.SetLines([(Line(item, source: " "), item)]));
        Assert.Throws<BusinessRuleException>(() => survey.SetLines([(Line(item, price: 0), item)]));
        // Cùng mặt hàng, cùng tên nguồn (không phân biệt hoa thường) là trùng.
        Assert.Throws<BusinessRuleException>(() => survey.SetLines([(Line(item), item), (Line(item, source: "cửa hàng a"), item)]));

        // Một mặt hàng nhiều nguồn: cửa hàng A theo thùng, NCC trong danh mục theo cái.
        survey.SetLines([(Line(item, price: 600_000m, uom: Thung.Id), item), (Line(item, Supplier, null, 48_000m), item)]);
        Assert.Equal([(1, 50_000m), (2, 48_000m)], survey.Lines.Select(l => (l.LineNo, l.BaseUnitPrice)));
    }

    [Fact]
    public void Chot_KhongSuaDuoc_HuyBatBuocLyDo()
    {
        var item = NewItem();
        var survey = MarketPriceSurvey.Create(Company, "KSG-1", Header(), Today);
        survey.SetLines([(Line(item), item)]);
        var user = Guid.CreateVersion7();
        survey.Confirm(DateTimeOffset.UtcNow, user);
        Assert.Equal((MarketSurveyStatus.Confirmed, user), (survey.Status, survey.ConfirmedBy!.Value));
        Assert.Throws<BusinessRuleException>(() => survey.SetLines([(Line(item, price: 1), item)]));
        Assert.Throws<BusinessRuleException>(() => survey.Update(Header(), Today));

        Assert.Throws<BusinessRuleException>(() => survey.Cancel(Today, " "));
        survey.Cancel(Today, "Nhập nhầm đơn vị tính");
        Assert.Equal((MarketSurveyStatus.Cancelled, "Nhập nhầm đơn vị tính"), (survey.Status, survey.CancelReason));
        Assert.Throws<BusinessRuleException>(() => survey.Cancel(Today, "Lần nữa"));
    }
}
