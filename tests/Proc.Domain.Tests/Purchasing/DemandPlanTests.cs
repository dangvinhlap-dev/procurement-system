using Proc.Domain.Common;
using Proc.Domain.MasterData;
using Proc.Domain.Purchasing;

namespace Proc.Domain.Tests.Purchasing;

/// <summary>Phiếu kế hoạch nhu cầu (design.md mục 18.25 – lát G5a): năm kế hoạch, dòng SL 12 tháng, chốt, thay, hủy.</summary>
public class DemandPlanTests
{
    private static readonly Guid Company = Guid.CreateVersion7();
    private static readonly Guid Category = Guid.CreateVersion7();
    private static readonly Uom Cai = Uom.Create("CAI", "Cái");
    private static readonly Uom Thung = Uom.Create("THUNG", "Thùng");
    private static readonly DateOnly Today = new(2026, 11, 7);

    private static Item NewItem(string code = "VB01")
    {
        var item = Item.Create(code, ItemType.Inventory, TestCategories.Leaf("VT", "Vật tư"), Cai,
            new ItemDescription("Vòng bi", null, null, null, null, null, "10", null));
        item.SetConversions([(Thung, 12m)]);
        return item;
    }

    private static decimal[] Months(params decimal[] first)
    {
        var months = new decimal[12];
        first.CopyTo(months, 0);
        return months;
    }

    private static DemandPlanLineData Line(Item item, decimal[]? months = null, Guid? uom = null) =>
        new(null, item.Id, uom ?? Cai.Id, months ?? Months(10, 20, 30), null);

    private static DemandPlan NewPlan(int year = 2027) => DemandPlan.Create(Company, "KHN-1", new DemandPlanHeader(year, Category, " Theo định mức "), Today);

    [Fact]
    public void NamKeHoach_TuNamHienTai_ToiDaHaiNamToi()
    {
        Assert.Equal(("Theo định mức", DemandPlanStatus.Draft), (NewPlan(2026).Note, NewPlan(2026).Status));
        Assert.Equal(2028, NewPlan(2028).Year);
        Assert.Contains("đã qua", Assert.Throws<BusinessRuleException>(() => NewPlan(2025)).Message);
        Assert.Throws<BusinessRuleException>(() => NewPlan(2029));
        Assert.Throws<BusinessRuleException>(() =>
            DemandPlan.Create(Company, "KHN-1", new DemandPlanHeader(2027, Guid.Empty, null), Today));
    }

    [Fact]
    public void Dong_Du12Thang_KhongAm_KhongToanSo0_KhongTrungMatHang_QuyDvt()
    {
        var plan = NewPlan();
        var item = NewItem();
        Assert.Contains("12 tháng", Assert.Throws<BusinessRuleException>(() => plan.SetLines([(Line(item, [1m, 2m]), item)])).Message);
        Assert.Contains("tháng 2", Assert.Throws<BusinessRuleException>(() => plan.SetLines([(Line(item, Months(1, -1)), item)])).Message);
        Assert.Throws<BusinessRuleException>(() => plan.SetLines([(Line(item, Months(1.23456m)), item)]));
        Assert.Contains("ít nhất một tháng", Assert.Throws<BusinessRuleException>(() => plan.SetLines([(Line(item, Months()), item)])).Message);
        Assert.Contains("trùng mặt hàng", Assert.Throws<BusinessRuleException>(() => plan.SetLines([(Line(item), item), (Line(item), item)])).Message);
        Assert.Throws<BusinessRuleException>(() => plan.SetLines([(Line(item, uom: Guid.CreateVersion7()), item)]));

        plan.SetLines([(Line(item, Months(1, 2, 3, 4), Thung.Id), item)]);
        var line = Assert.Single(plan.Lines);
        Assert.Equal((12m, 10m, 2m), (line.UomFactor, line.Total, line.Months[1]));
        Assert.Equal(12, line.Months.Count);
    }

    [Fact]
    public void Chot_KhongSua_DieuChinhThay_HuyCanLyDo()
    {
        var plan = NewPlan();
        var item = NewItem();
        Assert.Throws<BusinessRuleException>(() => plan.Confirm(DateTimeOffset.UtcNow, Guid.CreateVersion7(), Today));
        plan.SetLines([(Line(item), item)]);
        // Chốt cũng xét năm: phiếu nháp năm 2027 để đến năm 2028 thì không chốt được nữa.
        Assert.Throws<BusinessRuleException>(() => plan.Confirm(DateTimeOffset.UtcNow, Guid.CreateVersion7(), new DateOnly(2028, 1, 2)));
        plan.Confirm(DateTimeOffset.UtcNow, Guid.CreateVersion7(), Today);
        Assert.Equal(DemandPlanStatus.Confirmed, plan.Status);
        Assert.Throws<BusinessRuleException>(() => plan.SetLines([(Line(item), item)]));
        Assert.Equal(60m, Assert.Single(plan.CopyLines()).Months.Sum());

        Assert.Throws<BusinessRuleException>(() => plan.Cancel(Today, " "));
        plan.MarkReplaced();
        Assert.Equal(DemandPlanStatus.Replaced, plan.Status);
        Assert.Throws<BusinessRuleException>(() => plan.Cancel(Today, "Nhập sai"));

        var other = NewPlan();
        other.Cancel(Today, " Nhập sai ");
        Assert.Equal(("Nhập sai", DemandPlanStatus.Cancelled), (other.CancelReason, other.Status));
        Assert.Throws<BusinessRuleException>(() => other.MarkReplaced());
    }
}
