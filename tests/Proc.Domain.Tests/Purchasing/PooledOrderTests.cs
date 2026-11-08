using Proc.Domain.Common;
using Proc.Domain.Purchasing;

namespace Proc.Domain.Tests.Purchasing;

/// <summary>Phiếu đặt hàng gộp (design.md mục 18.25 – lát G7): phân bổ theo kế hoạch, biên độ, trạng thái.</summary>
public class PooledOrderTests
{
    private static readonly Guid HeadOffice = Guid.CreateVersion7();
    private static readonly Guid CompanyA = Guid.CreateVersion7();
    private static readonly Guid CompanyB = Guid.CreateVersion7();
    private static readonly Guid Warehouse = Guid.CreateVersion7();
    private static readonly DateOnly Date = new(2026, 11, 10);

    private static PooledOrderHeader Header() => new(Date, Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), Date.AddDays(3), " Đặt gộp tháng 11 ");

    private static PooledOrderLineSource Source(string code = "VB01") =>
        new(Guid.CreateVersion7(), Guid.CreateVersion7(), code, Guid.CreateVersion7(), 1m, 50_000m);

    private static PooledOrderLineData Data(PooledOrderLineSource source, decimal a, decimal b, string? reason = null) =>
        new(null, source.MonthlyApprovalLineId, [new(CompanyA, a), new(CompanyB, b)], reason, null);

    private static PooledOrder NewOrder()
    {
        var order = PooledOrder.Create(HeadOffice, "DHG-1", Header());
        order.SetCompanies([new(CompanyA, Warehouse, null), new(CompanyB, Warehouse, null)]);
        return order;
    }

    [Fact]
    public void DeXuat_ChiaTheoTyLeKeHoach_CongDungTong()
    {
        // Tổng 10, kế hoạch A 2, B 1 → 6,667 / 3,333 → phần nguyên 6 / 3, dư 1 cho A (phần lẻ lớn hơn).
        var integer = PooledAllocation.Suggest(10m, [new(CompanyA, 2m), new(CompanyB, 1m)]);
        Assert.Equal((7m, 3m), (integer[CompanyA], integer[CompanyB]));
        // Tổng có số lẻ: chia 4 số lẻ, phần dư dồn vào kế hoạch lớn nhất.
        var fraction = PooledAllocation.Suggest(10.5m, [new(CompanyA, 2m), new(CompanyB, 1m)]);
        Assert.Equal(10.5m, fraction.Values.Sum());
        Assert.Equal(3.5m, fraction[CompanyB]);
        Assert.Empty(PooledAllocation.Suggest(10m, [new(CompanyA, 0m)]));
        Assert.Equal(6.6667m, Math.Round(PooledAllocation.Expected(10m, CompanyA, [new(CompanyA, 2m), new(CompanyB, 1m)]), 4));
    }

    [Fact]
    public void DungSai_LonHon_BienDoHoacMotDvt()
    {
        Assert.Equal(10m, PooledAllocation.Tolerance(100m, 10m));
        Assert.Equal(1m, PooledAllocation.Tolerance(5m, 10m));
        Assert.Equal(1m, PooledAllocation.Tolerance(100m, 0m));
    }

    [Fact]
    public void BienDo_TrongDungSaiQua_VuotThiChan_KhongKeHoachPhaiCoLyDo()
    {
        var source = Source();
        var order = NewOrder();
        // Kế hoạch A 60, B 40, tổng 100, biên độ 10% → A được 50–70.
        IReadOnlyList<CompanyPlan> Plans(PooledOrderLine _) => [new(CompanyA, 60m), new(CompanyB, 40m)];
        order.SetLines([(Data(source, 70m, 30m), source)]);
        order.EnsureWithinMargin(Plans, 10m, id => id == CompanyA ? "A" : "B");
        order.SetLines([(Data(source, 71m, 29m) with { Id = order.Lines[0].Id }, source)]);
        var error = Assert.Throws<BusinessRuleException>(() => order.EnsureWithinMargin(Plans, 10m, id => id == CompanyA ? "A" : "B"));
        Assert.Contains("A: phân bổ 71, đề xuất theo kế hoạch 60 ± 10", error.Message);

        // Không có kế hoạch ở công ty nào: phân bổ tự do nhưng bắt buộc lý do.
        IReadOnlyList<CompanyPlan> NoPlan(PooledOrderLine _) => [];
        Assert.Throws<BusinessRuleException>(() => order.EnsureWithinMargin(NoPlan, 10m, _ => "X"));
        order.SetLines([(Data(source, 71m, 29m, "Hàng phát sinh") with { Id = order.Lines[0].Id }, source)]);
        order.EnsureWithinMargin(NoPlan, 10m, _ => "X");
    }

    [Fact]
    public void Dong_KhongTrungMatHang_SlHopLe_CongTyTrongDanhSach()
    {
        var order = NewOrder();
        var source = Source();
        Assert.Throws<BusinessRuleException>(() => order.SetLines([(Data(source, 1m, 0m), source), (Data(source, 2m, 0m), source)]));
        Assert.Throws<BusinessRuleException>(() => order.SetLines([(Data(source, 0m, 0m), source)]));
        Assert.Throws<BusinessRuleException>(() => order.SetLines([(Data(source, -1m, 5m), source)]));
        Assert.Throws<BusinessRuleException>(() => order.SetLines([(Data(source, 1.00001m, 0m), source)]));
        var outsider = new PooledOrderLineData(null, source.MonthlyApprovalLineId, [new(Guid.CreateVersion7(), 5m)], null, null);
        Assert.Throws<BusinessRuleException>(() => order.SetLines([(outsider, source)]));

        order.SetLines([(Data(source, 5m, 0m), source)]);
        var line = Assert.Single(order.Lines);
        // Chỉ lưu phân bổ > 0; tổng dòng là tổng phân bổ.
        Assert.Equal((5m, 5m, 0m, 50_000m), (line.TotalQty, line.QtyFor(CompanyA), line.QtyFor(CompanyB), line.UnitPrice));
        Assert.Single(line.Allocations);
    }

    [Fact]
    public void CongTy_KhongTrung_CoKho()
    {
        var order = PooledOrder.Create(HeadOffice, "DHG-1", Header());
        Assert.Equal("Đặt gộp tháng 11", order.Note);
        Assert.Throws<BusinessRuleException>(() => order.SetCompanies([]));
        Assert.Throws<BusinessRuleException>(() => order.SetCompanies([new(CompanyA, Warehouse, null), new(CompanyA, Warehouse, null)]));
        Assert.Throws<BusinessRuleException>(() => order.SetCompanies([new(CompanyA, Guid.Empty, null)]));
        Assert.Throws<BusinessRuleException>(() => PooledOrder.Create(HeadOffice, "DHG-1", Header() with { ExpectedDate = Date.AddDays(-1) }));
    }

    [Fact]
    public void TaoPo_KhoaPhieu_KhongHuy_HuyNhapCanLyDo()
    {
        var order = NewOrder();
        var source = Source();
        order.SetLines([(Data(source, 5m, 3m), source)]);
        var poA = Guid.CreateVersion7();
        Assert.Throws<BusinessRuleException>(() => order.MarkOrdered(DateTimeOffset.UtcNow, Guid.CreateVersion7(), 10m, new Dictionary<Guid, Guid>()));
        order.MarkOrdered(DateTimeOffset.UtcNow, Guid.CreateVersion7(), 10m, new Dictionary<Guid, Guid> { [CompanyA] = poA });
        Assert.Equal((PooledOrderStatus.Ordered, poA, (Guid?)null, 10m), (order.Status, order.Companies[0].PurchaseOrderId!.Value, order.Companies[1].PurchaseOrderId, order.MarginPercent!.Value));
        Assert.Throws<BusinessRuleException>(() => order.SetLines([(Data(source, 1m, 1m), source)]));
        Assert.Throws<BusinessRuleException>(() => order.Cancel(Date, "Sai"));

        var draft = NewOrder();
        Assert.Throws<BusinessRuleException>(() => draft.Cancel(Date, " "));
        draft.Cancel(Date, "Đặt nhầm NCC");
        Assert.Equal(PooledOrderStatus.Cancelled, draft.Status);
    }
}
