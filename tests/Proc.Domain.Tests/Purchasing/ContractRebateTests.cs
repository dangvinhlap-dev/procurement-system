using Proc.Domain.Common;
using Proc.Domain.Purchasing;

namespace Proc.Domain.Tests.Purchasing;

/// <summary>Chiết khấu doanh số/tài trợ trên hợp đồng nguyên tắc và thực nhận (lát F4b – design.md mục 18.21).</summary>
public class ContractRebateTests
{
    private static readonly Guid Company = Guid.CreateVersion7();

    private static PurchaseContract NewContract(DateOnly from, DateOnly to) =>
        PurchaseContract.Create(Company, "CTR-1", new ContractHeader(
            Guid.CreateVersion7(), Guid.CreateVersion7(), null, "Hợp đồng vòng bi", null, from, to, null, null, null, null, null, null, null, null, null));

    private static ContractRebateData Percent(decimal pct, decimal? min = null, RebatePeriod period = RebatePeriod.Quarter) =>
        new(null, RebateKind.SalesVolume, RebateMethod.Percent, pct, min, period, "Chiết khấu doanh số theo quý");

    [Fact]
    public void CamKet_KyQuyetToan_CatTheoHieuLuc()
    {
        var contract = NewContract(new DateOnly(2026, 2, 15), new DateOnly(2027, 8, 10));
        contract.SetRebates([
            Percent(2),
            new(null, RebateKind.Sponsorship, RebateMethod.FixedAmount, 50_000_000, null, RebatePeriod.Year, "Tài trợ hội nghị khách hàng"),
            new(null, RebateKind.Other, RebateMethod.FixedAmount, 10_000_000, null, RebatePeriod.WholeContract, "Thưởng ký hợp đồng"),
        ]);
        var quarters = contract.Rebates[0].PeriodsWithin(contract.ValidFrom, contract.EndDate);
        Assert.Equal(7, quarters.Count);
        Assert.Equal((new DateOnly(2026, 2, 15), new DateOnly(2026, 3, 31)), quarters[0]);
        Assert.Equal((new DateOnly(2026, 4, 1), new DateOnly(2026, 6, 30)), quarters[1]);
        Assert.Equal((new DateOnly(2027, 7, 1), new DateOnly(2027, 8, 10)), quarters[^1]);
        var years = contract.Rebates[1].PeriodsWithin(contract.ValidFrom, contract.EndDate);
        Assert.Equal([(new DateOnly(2026, 2, 15), new DateOnly(2026, 12, 31)), (new DateOnly(2027, 1, 1), new DateOnly(2027, 8, 10))], years);
        Assert.Single(contract.Rebates[2].PeriodsWithin(contract.ValidFrom, contract.EndDate));
        Assert.Equal((2m, (decimal?)null), (contract.Rebates[0].Rate!.Value, contract.Rebates[0].Amount));
    }

    [Fact]
    public void SoPhaiNhan_TheoPhanTram_CoDinh_NguongDoanhSo()
    {
        var contract = NewContract(new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));
        contract.SetRebates([
            Percent(2, 1_500_000_000),
            new(null, RebateKind.Sponsorship, RebateMethod.FixedAmount, 50_000_000, null, RebatePeriod.Year, "Tài trợ"),
        ]);
        // Chưa đạt doanh số tối thiểu 1,5 tỷ → 0; đạt: 1.600.000.000 × 2% = 32.000.000.
        Assert.Equal(0m, contract.Rebates[0].ExpectedFor(1_250_000_000));
        Assert.Equal(32_000_000m, contract.Rebates[0].ExpectedFor(1_600_000_000));
        // 1.234.567,89 × 2% = 24.691,3578 → làm tròn 24.691,36 (khi không có ngưỡng).
        contract.SetRebates([
            Percent(2) with { Id = contract.Rebates[0].Id },
            new(contract.Rebates[1].Id, RebateKind.Sponsorship, RebateMethod.FixedAmount, 50_000_000, null, RebatePeriod.Year, "Tài trợ"),
        ]);
        Assert.Equal(24_691.36m, contract.Rebates[0].ExpectedFor(1_234_567.89m));
        Assert.Equal(50_000_000m, contract.Rebates[1].ExpectedFor(0));
    }

    [Fact]
    public void CamKet_DuLieuSai_HoacHetNhap_BiChan()
    {
        var contract = NewContract(new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));
        Assert.Throws<BusinessRuleException>(() => contract.SetRebates([Percent(0)]));
        Assert.Throws<BusinessRuleException>(() => contract.SetRebates([Percent(100.5m)]));
        Assert.Throws<BusinessRuleException>(() => contract.SetRebates([Percent(2, -1)]));
        Assert.Throws<BusinessRuleException>(() => contract.SetRebates([Percent(2) with { Description = " " }]));
        Assert.Throws<BusinessRuleException>(() => contract.SetRebates([Percent(2) with { Method = RebateMethod.FixedAmount, Value = 0.001m }]));
        Assert.Throws<BusinessRuleException>(() => contract.SetRebates([Percent(2) with { Id = Guid.CreateVersion7() }]));
        contract.SetRebates([Percent(2)]);
        contract.Submit();
        Assert.Throws<BusinessRuleException>(() => contract.SetRebates([]));
    }

    [Fact]
    public void ThucNhan_KyHopLe_HopDongHieuLuc_HuyCoLyDo()
    {
        var today = new DateOnly(2026, 11, 2);
        var contract = NewContract(new DateOnly(2026, 2, 15), new DateOnly(2027, 2, 14));
        contract.SetRebates([Percent(2)]);
        var rebateId = contract.Rebates[0].Id;
        var data = new ContractRebateReceiptData(new DateOnly(2026, 4, 1), new DateOnly(2026, 7, 20), 20_000_000, RebateReceiptForm.DebtOffset, " BB-01 ", null);
        // Nháp chưa hiệu lực → chặn.
        Assert.Throws<BusinessRuleException>(() => ContractRebateReceipt.Create(contract, rebateId, data, today));
        contract.Submit();
        contract.MarkApproved(DateTimeOffset.UtcNow);
        var receipt = ContractRebateReceipt.Create(contract, rebateId, data, today);
        Assert.Equal((Company, new DateOnly(2026, 4, 1), "BB-01", false), (receipt.CompanyId, receipt.PeriodFrom, receipt.RefNo, receipt.IsCancelled));
        // Kỳ không phải đầu kỳ quyết toán, ngày nhận trước đầu kỳ hoặc sau hôm nay, số tiền ≤ 0 → chặn.
        Assert.Throws<BusinessRuleException>(() => ContractRebateReceipt.Create(contract, rebateId, data with { PeriodFrom = new DateOnly(2026, 4, 2) }, today));
        Assert.Throws<BusinessRuleException>(() => ContractRebateReceipt.Create(contract, rebateId, data with { ReceivedOn = new DateOnly(2026, 3, 31) }, today));
        Assert.Throws<BusinessRuleException>(() => ContractRebateReceipt.Create(contract, rebateId, data with { ReceivedOn = today.AddDays(1) }, today));
        Assert.Throws<BusinessRuleException>(() => ContractRebateReceipt.Create(contract, rebateId, data with { Amount = 0 }, today));
        Assert.Throws<BusinessRuleException>(() => ContractRebateReceipt.Create(contract, Guid.CreateVersion7(), data, today));
        // Kỳ đầu bắt đầu từ ngày hiệu lực 15/02 (không phải 01/01).
        ContractRebateReceipt.Create(contract, rebateId, data with { PeriodFrom = new DateOnly(2026, 2, 15), ReceivedOn = new DateOnly(2026, 4, 10) }, today);

        Assert.Throws<BusinessRuleException>(() => receipt.Cancel(" "));
        receipt.Cancel("Ghi nhầm số tiền");
        Assert.Equal((true, "Ghi nhầm số tiền"), (receipt.IsCancelled, receipt.CancelReason));
        Assert.Throws<BusinessRuleException>(() => receipt.Cancel("Lần nữa"));

        // Chấm dứt 15/08: kỳ cuối là 01/07–15/08, kỳ 01/10 không còn.
        contract.Terminate(new DateOnly(2026, 8, 15), "Thanh lý trước hạn");
        Assert.Equal((new DateOnly(2026, 7, 1), new DateOnly(2026, 8, 15)), contract.Rebates[0].PeriodsWithin(contract.ValidFrom, contract.EndDate)[^1]);
        Assert.Throws<BusinessRuleException>(() => ContractRebateReceipt.Create(contract, rebateId, data with { PeriodFrom = new DateOnly(2026, 10, 1) }, today));
    }
}
