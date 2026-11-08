using Proc.Domain.Common;
using Proc.Domain.MasterData;
using Proc.Domain.Pricing;

namespace Proc.Domain.Tests.Pricing;

/// <summary>
/// Thỏa thuận chiết khấu bậc sản lượng gộp (design.md mục 18.25 – lát G6a): đầu thỏa thuận, dòng, bậc, vòng đời, kỳ tính bậc, bậc đạt,
/// chia chiết khấu theo tỷ lệ doanh số, cảnh báo sắp chạm/hụt bậc.
/// </summary>
public class VolumeRebateAgreementTests
{
    private static readonly Guid Company = Guid.CreateVersion7();
    private static readonly Guid Member = Guid.CreateVersion7();
    private static readonly Guid Supplier = Guid.CreateVersion7();
    private static readonly Guid Category = Guid.CreateVersion7();
    private static readonly Uom Tan = Uom.Create("TAN", "Tấn");
    private static readonly Uom Bao = Uom.Create("BAO", "Bao");

    private static Item NewItem(string code)
    {
        var item = Item.Create(code, ItemType.Inventory, ItemCategory.Create("XM", "Xi măng"), Bao,
            new ItemDescription("Xi măng " + code, null, null, null, null, null, null, null));
        item.SetConversions([(Tan, 20m)]);
        return item;
    }

    private static VolumeRebateHeader Header(
        VolumeRebatePeriod period = VolumeRebatePeriod.Month, VolumeRebateBasis basis = VolumeRebateBasis.Quantity, int? target = 2) =>
        new(" Chiết khấu xi măng 2026 ", Supplier, Category, new DateOnly(2026, 2, 15), new DateOnly(2026, 8, 10), period, basis, target,
            [Company, Member], null);

    /// <summary>Thỏa thuận 2 mặt hàng tính theo tấn, bậc 100 tấn 1% – 200 tấn 2% – 400 tấn 3%, mục tiêu bậc 2.</summary>
    private static VolumeRebateAgreement NewAgreement(VolumeRebatePeriod period = VolumeRebatePeriod.Month, int? target = 2)
    {
        var agreement = VolumeRebateAgreement.Create(Company, "CKB-1", Header(period, target: target));
        var a = NewItem("PCB30");
        var b = NewItem("PCB40");
        agreement.SetLines([(new VolumeRebateLineData(null, a.Id, Tan.Id, null), a), (new VolumeRebateLineData(null, b.Id, Tan.Id, null), b)]);
        agreement.SetTiers([new(400m, 3m), new(100m, 1m), new(200m, 2m)]);
        return agreement;
    }

    [Fact]
    public void DauThoaThuan_KiemTraDuLieu()
    {
        var agreement = VolumeRebateAgreement.Create(Company, "CKB-1", Header());
        Assert.Equal(("Chiết khấu xi măng 2026", 2, VolumeRebateStatus.Draft), (agreement.Title, agreement.Companies.Count, agreement.Status));
        Assert.Throws<BusinessRuleException>(() => VolumeRebateAgreement.Create(Company, "CKB-1", Header() with { Title = " " }));
        Assert.Throws<BusinessRuleException>(() => VolumeRebateAgreement.Create(Company, "CKB-1", Header() with { SupplierId = Guid.Empty }));
        Assert.Throws<BusinessRuleException>(() => VolumeRebateAgreement.Create(Company, "CKB-1", Header() with { ValidTo = new DateOnly(2026, 2, 14) }));
        Assert.Throws<BusinessRuleException>(() => VolumeRebateAgreement.Create(Company, "CKB-1", Header() with { ValidTo = new DateOnly(2031, 3, 1) }));
        Assert.Throws<BusinessRuleException>(() => VolumeRebateAgreement.Create(Company, "CKB-1", Header() with { CompanyIds = [] }));
        Assert.Throws<BusinessRuleException>(() => VolumeRebateAgreement.Create(Company, "CKB-1", Header(target: 0)));
    }

    [Fact]
    public void Dong_TheoSoLuong_PhaiCungDvt_KhongTrungMatHang()
    {
        var agreement = VolumeRebateAgreement.Create(Company, "CKB-1", Header());
        var a = NewItem("PCB30");
        var b = NewItem("PCB40");
        var ex = Assert.Throws<BusinessRuleException>(() => agreement.SetLines(
            [(new VolumeRebateLineData(null, a.Id, Tan.Id, null), a), (new VolumeRebateLineData(null, b.Id, Bao.Id, null), b)]));
        Assert.Contains("cùng một ĐVT", ex.Message);
        Assert.Throws<BusinessRuleException>(() => agreement.SetLines(
            [(new VolumeRebateLineData(null, a.Id, Tan.Id, null), a), (new VolumeRebateLineData(null, a.Id, Tan.Id, null), a)]));

        // Tính theo doanh số thì ĐVT khác nhau vẫn được; hệ số ĐVT lấy theo quy đổi của mặt hàng.
        agreement.Update(Header(basis: VolumeRebateBasis.Amount));
        agreement.SetLines([(new VolumeRebateLineData(null, a.Id, Tan.Id, null), a), (new VolumeRebateLineData(null, b.Id, Bao.Id, null), b)]);
        Assert.Equal([20m, 1m], agreement.Lines.Select(l => l.UomFactor));
    }

    [Fact]
    public void Bac_SapTheoNguong_TangDan_MucTieuKhongVuotSoBac()
    {
        var agreement = NewAgreement();
        Assert.Equal([(1, 100m, 1m), (2, 200m, 2m), (3, 400m, 3m)], agreement.Tiers.Select(t => (t.TierNo, t.FromValue, t.Percent)));
        Assert.Throws<BusinessRuleException>(() => agreement.SetTiers([new(100m, 2m), new(200m, 1m)]));
        Assert.Throws<BusinessRuleException>(() => agreement.SetTiers([new(100m, 1m), new(100m, 2m)]));
        Assert.Throws<BusinessRuleException>(() => agreement.SetTiers([new(0m, 1m)]));
        Assert.Throws<BusinessRuleException>(() => agreement.SetTiers([new(100m, 101m)]));
        Assert.Throws<BusinessRuleException>(() => agreement.SetTiers([new(100.00001m, 1m)]));
        var ex = Assert.Throws<BusinessRuleException>(() => agreement.SetTiers([new(100m, 1m)]));
        Assert.Contains("Bậc mục tiêu 2", ex.Message);
        Assert.Throws<BusinessRuleException>(() => agreement.SetTiers([]));
    }

    [Fact]
    public void VongDoi_GuiDuyet_HieuLuc_KhongSua_HuyCanLyDo()
    {
        var agreement = NewAgreement();
        Assert.Equal(1, agreement.Submit());
        Assert.Throws<BusinessRuleException>(() => agreement.Update(Header()));
        Assert.Throws<BusinessRuleException>(() => agreement.Cancel(new DateOnly(2026, 3, 1), "x"));
        agreement.ReturnToDraft("Thiếu bậc 4");
        Assert.Equal((VolumeRebateStatus.Draft, "Thiếu bậc 4"), (agreement.Status, agreement.RejectReason));
        agreement.Submit();
        agreement.MarkApproved(DateTimeOffset.UtcNow);
        Assert.Equal(VolumeRebateStatus.Active, agreement.Status);
        Assert.Throws<BusinessRuleException>(() => agreement.SetTiers([new(100m, 1m), new(200m, 2m)]));
        Assert.Throws<BusinessRuleException>(() => agreement.Cancel(new DateOnly(2026, 3, 1), " "));
        agreement.Cancel(new DateOnly(2026, 3, 1), "NCC đổi chính sách");
        Assert.Equal((VolumeRebateStatus.Cancelled, false), (agreement.Status, agreement.IsLive));
    }

    [Fact]
    public void KyTinhBac_CatTheoThoiHan()
    {
        Assert.Equal(
            [(new DateOnly(2026, 2, 15), new DateOnly(2026, 2, 28)), (new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31))],
            NewAgreement().Periods().Take(2));
        Assert.Equal(7, NewAgreement().Periods().Count);
        Assert.Equal(
            [(new DateOnly(2026, 2, 15), new DateOnly(2026, 3, 31)), (new DateOnly(2026, 4, 1), new DateOnly(2026, 6, 30)),
                (new DateOnly(2026, 7, 1), new DateOnly(2026, 8, 10))],
            NewAgreement(VolumeRebatePeriod.Quarter).Periods());
        Assert.Equal([(new DateOnly(2026, 2, 15), new DateOnly(2026, 8, 10))], NewAgreement(VolumeRebatePeriod.Year).Periods());
        Assert.Equal([(new DateOnly(2026, 2, 15), new DateOnly(2026, 8, 10))], NewAgreement(VolumeRebatePeriod.Whole).Periods());
    }

    [Fact]
    public void BacDat_ChietKhauGop_ChiaTheoTyLeDoanhSo()
    {
        var agreement = NewAgreement();
        // Kỳ tháng 3 đã hết: tổng công ty 150 tấn / 600 triệu, công ty thành viên 100 tấn / 400 triệu → 250 tấn đạt bậc 2 (2%).
        var r = agreement.Evaluate(new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31),
            [new(Company, 150m, 600_000_000m), new(Member, 100m, 400_000_000m)], new DateOnly(2026, 4, 5), 10m);
        Assert.Equal(("Ended", 250m, 2, 20_000_000m), (r.State, r.Volume, r.Tier!.TierNo, r.Rebate));
        Assert.Equal((3, 150m, "Reached"), (r.Next!.TierNo, r.Shortfall!.Value, r.TargetStatus));
        Assert.Equal([(60m, 12_000_000m), (40m, 8_000_000m)], r.Shares.Select(s => (s.SharePercent, s.Rebate)));
        Assert.False(r.IsNearNextTier);

        // Chưa đạt bậc nào: không có chiết khấu, hết kỳ mà dưới bậc mục tiêu là hụt bậc.
        var low = agreement.Evaluate(new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31), [new(Company, 80m, 300_000_000m)], new DateOnly(2026, 4, 5), 10m);
        Assert.Equal((null, 0m, 1, 20m, "Missed"), (low.Tier, low.Rebate, low.Next!.TierNo, low.Shortfall!.Value, low.TargetStatus));
    }

    [Fact]
    public void ChiaChietKhau_SoLeDonVaoCongTyDoanhSoLonNhat()
    {
        var agreement = VolumeRebateAgreement.Create(Company, "CKB-2", Header(basis: VolumeRebateBasis.Amount, target: null));
        var a = NewItem("PCB30");
        agreement.SetLines([(new VolumeRebateLineData(null, a.Id, Tan.Id, null), a)]);
        agreement.SetTiers([new(1_000_000m, 1m)]);
        var third = Guid.CreateVersion7();
        var r = agreement.Evaluate(new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31),
            [new(Company, 0m, 5_000_000m), new(Member, 0m, 2_500_000m), new(third, 0m, 2_500_001m)], new DateOnly(2026, 4, 1), 10m);
        // Gộp 10.000.001 × 1% = 100.000,01; phần 50.000 / 25.000 / 25.000,01 = 100.000,01.
        Assert.Equal(100_000.01m, r.Rebate);
        Assert.Equal(r.Rebate, r.Shares.Sum(s => s.Rebate));

        var equal = agreement.Evaluate(new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31),
            [new(Company, 0m, 3_000_000m), new(Member, 0m, 3_000_000m), new(third, 0m, 3_000_000m)], new DateOnly(2026, 4, 1), 10m);
        // Gộp 9.000.000 × 1% = 90.000 – chia đều 30.000.
        Assert.Equal([30_000m, 30_000m, 30_000m], equal.Shares.Select(s => s.Rebate));
        Assert.Equal("None", equal.TargetStatus);

        // Chia không hết: gộp 1.000.000 × 10% = 100.000; phần làm tròn 33.333,33 × 3 = 99.999,99 → 0,01 dồn vào công ty doanh số lớn nhất.
        agreement.SetTiers([new(1_000_000m, 10m)]);
        var odd = agreement.Evaluate(new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31),
            [new(Company, 0m, 333_333.33m), new(Member, 0m, 333_333.34m), new(third, 0m, 333_333.33m)], new DateOnly(2026, 4, 1), 10m);
        Assert.Equal(100_000m, odd.Rebate);
        Assert.Equal([33_333.33m, 33_333.34m, 33_333.33m], odd.Shares.Select(s => s.Rebate));
    }

    [Fact]
    public void KyDangChay_DuBao_SapChamBac_NguyCoHut()
    {
        var agreement = NewAgreement();
        var from = new DateOnly(2026, 4, 1);
        var to = new DateOnly(2026, 4, 30);
        // Ngày 10/30 đã mua 60 tấn → dự báo 180 tấn < 200 (bậc mục tiêu 2) → nguy cơ hụt.
        var risk = agreement.Evaluate(from, to, [new(Company, 60m, 240_000_000m)], new DateOnly(2026, 4, 10), 10m);
        Assert.Equal(("Open", 180m, "AtRisk", false), (risk.State, risk.Forecast!.Value, risk.TargetStatus, risk.IsNearNextTier));

        // 95 tấn ngày 10 → dự báo 285 tấn (đúng hướng), 95 ≥ 100 × 90% → sắp chạm bậc 1.
        var near = agreement.Evaluate(from, to, [new(Company, 95m, 380_000_000m)], new DateOnly(2026, 4, 10), 10m);
        Assert.Equal((285m, "OnTrack", true, 1), (near.Forecast!.Value, near.TargetStatus, near.IsNearNextTier, near.Next!.TierNo));

        // Kỳ chưa tới: không cảnh báo, chưa bắt đầu.
        var future = agreement.Evaluate(from, to, [], new DateOnly(2026, 3, 20), 10m);
        Assert.Equal(("Future", "NotStarted", false, null), (future.State, future.TargetStatus, future.IsNearNextTier, future.Forecast));

        // Đã ở bậc cao nhất: không còn bậc kế tiếp.
        var top = agreement.Evaluate(from, to, [new(Company, 450m, 1_800_000_000m)], new DateOnly(2026, 4, 10), 10m);
        Assert.Equal((3, null, null, false), (top.Tier!.TierNo, top.Next, top.Shortfall, top.IsNearNextTier));
    }
}
