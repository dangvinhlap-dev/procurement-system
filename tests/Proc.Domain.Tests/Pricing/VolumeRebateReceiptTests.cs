using Proc.Domain.Common;
using Proc.Domain.MasterData;
using Proc.Domain.Pricing;
using Proc.Domain.Purchasing;

namespace Proc.Domain.Tests.Pricing;

/// <summary>
/// Thực nhận chiết khấu sản lượng gộp (design.md mục 18.25 – lát G6b): điều kiện ghi nhận, chia phần thực nhận theo tỷ lệ doanh số kỳ lúc ghi,
/// hủy có lý do, tình trạng kỳ dùng chung với chiết khấu hợp đồng (mục 18.21).
/// </summary>
public class VolumeRebateReceiptTests
{
    private static readonly Guid Head = Guid.CreateVersion7();
    private static readonly Guid MemberA = Guid.CreateVersion7();
    private static readonly Guid MemberB = Guid.CreateVersion7();
    private static readonly DateOnly Today = new(2026, 5, 10);

    /// <summary>Thỏa thuận theo quý 15/02 – 10/08/2026, áp ba công ty, đã hiệu lực (nếu <paramref name="active"/>).</summary>
    private static VolumeRebateAgreement NewAgreement(bool active = true)
    {
        var tan = Uom.Create("TAN", "Tấn");
        var item = Item.Create("PCB30", ItemType.Inventory, ItemCategory.Create("XM", "Xi măng"), tan,
            new ItemDescription("Xi măng PCB30", null, null, null, null, null, null, null));
        var agreement = VolumeRebateAgreement.Create(Head, "CKB-1", new VolumeRebateHeader(
            "Chiết khấu xi măng", Guid.CreateVersion7(), Guid.CreateVersion7(), new DateOnly(2026, 2, 15), new DateOnly(2026, 8, 10),
            VolumeRebatePeriod.Quarter, VolumeRebateBasis.Quantity, null, [Head, MemberA, MemberB], null));
        agreement.SetLines([(new VolumeRebateLineData(null, item.Id, tan.Id, null), item)]);
        agreement.SetTiers([new(100m, 1m)]);
        if (!active) return agreement;
        agreement.Submit();
        agreement.MarkApproved(DateTimeOffset.UtcNow);
        return agreement;
    }

    private static VolumeRebateReceiptData Data(decimal amount = 100_000m, DateOnly? periodFrom = null, DateOnly? receivedOn = null) =>
        new(periodFrom ?? new DateOnly(2026, 2, 15), receivedOn ?? new DateOnly(2026, 4, 20), amount, RebateReceiptForm.DebtOffset, " BB-01 ", " ");

    private static readonly VolumeRebateCompanyVolume[] Volumes =
        [new(Head, 10m, 600_000_000m), new(MemberA, 5m, 300_000_000m), new(MemberB, 2m, 100_000_000m)];

    [Fact]
    public void GhiNhan_ChiaTheoTyLeDoanhSoKy_TongKhopSoNhan()
    {
        var receipt = VolumeRebateReceipt.Create(NewAgreement(), Data(), Volumes, Today);
        Assert.Equal((Head, 100_000m, "BB-01", (string?)null), (receipt.CompanyId, receipt.Amount, receipt.RefNo, receipt.Note));
        Assert.Equal(
            [(Head, 60m, 60_000m), (MemberA, 30m, 30_000m), (MemberB, 10m, 10_000m)],
            receipt.Shares.Select(s => (s.CompanyId, s.SharePercent, s.Amount)));
        Assert.Equal(300_000_000m, receipt.Shares[1].PurchaseAmount);

        // Số lẻ dồn vào công ty doanh số lớn nhất: 100.000,01 chia 3 công ty bằng nhau.
        var odd = VolumeRebateReceipt.Create(NewAgreement(), Data(100_000.01m),
            [new(Head, 1m, 50m), new(MemberA, 1m, 50m), new(MemberB, 1m, 50m)], Today);
        Assert.Equal(100_000.01m, odd.Shares.Sum(s => s.Amount));
    }

    [Fact]
    public void GhiNhan_ChiCongTyApDungCoDoanhSo_KyKhongDoanhSoThiChan()
    {
        var other = Guid.CreateVersion7();
        var receipt = VolumeRebateReceipt.Create(NewAgreement(), Data(),
            [new(Head, 1m, 200m), new(MemberA, 0m, 0m), new(other, 1m, 800m)], Today);
        Assert.Equal([(Head, 100_000m)], receipt.Shares.Select(s => (s.CompanyId, s.Amount)));

        var ex = Assert.Throws<BusinessRuleException>(() => VolumeRebateReceipt.Create(NewAgreement(), Data(), [], Today));
        Assert.Contains("chưa có doanh số", ex.Message);
    }

    [Fact]
    public void GhiNhan_KiemTraTrangThaiKyNgaySoTien()
    {
        Assert.Contains("Hiệu lực", Assert.Throws<BusinessRuleException>(
            () => VolumeRebateReceipt.Create(NewAgreement(active: false), Data(), Volumes, Today)).Message);
        Assert.Contains("không thuộc thỏa thuận", Assert.Throws<BusinessRuleException>(
            () => VolumeRebateReceipt.Create(NewAgreement(), Data(periodFrom: new DateOnly(2026, 3, 1)), Volumes, Today)).Message);
        // Kỳ 2 của thỏa thuận theo quý bắt đầu 01/04.
        Assert.Equal(new DateOnly(2026, 4, 1),
            VolumeRebateReceipt.Create(NewAgreement(), Data(periodFrom: new DateOnly(2026, 4, 1)), Volumes, Today).PeriodFrom);
        Assert.Throws<BusinessRuleException>(() => VolumeRebateReceipt.Create(NewAgreement(), Data(receivedOn: new DateOnly(2026, 2, 14)), Volumes, Today));
        Assert.Throws<BusinessRuleException>(() => VolumeRebateReceipt.Create(NewAgreement(), Data(receivedOn: Today.AddDays(1)), Volumes, Today));
        Assert.Throws<BusinessRuleException>(() => VolumeRebateReceipt.Create(NewAgreement(), Data(0m), Volumes, Today));
        Assert.Throws<BusinessRuleException>(() => VolumeRebateReceipt.Create(NewAgreement(), Data(1.001m), Volumes, Today));
        Assert.Throws<BusinessRuleException>(() => VolumeRebateReceipt.Create(NewAgreement(), Data() with { Form = (RebateReceiptForm)9 }, Volumes, Today));
        Assert.Throws<BusinessRuleException>(() => VolumeRebateReceipt.Create(NewAgreement(), Data() with { RefNo = new string('x', 101) }, Volumes, Today));
    }

    [Fact]
    public void Huy_BatBuocLyDo_KhongHuyHaiLan()
    {
        var receipt = VolumeRebateReceipt.Create(NewAgreement(), Data(), Volumes, Today);
        Assert.Throws<BusinessRuleException>(() => receipt.Cancel(" "));
        receipt.Cancel(" Ghi nhầm kỳ ");
        Assert.Equal((true, "Ghi nhầm kỳ"), (receipt.IsCancelled, receipt.CancelReason));
        Assert.Throws<BusinessRuleException>(() => receipt.Cancel("Lần nữa"));
    }

    [Fact]
    public void TinhTrangKy_HanGhiNhan30NgaySauCuoiKy()
    {
        var end = new DateOnly(2026, 6, 30);
        Assert.Equal(new DateOnly(2026, 7, 30), RebateSettlement.DueDate(end));
        Assert.Equal(RebateSettlement.Received, RebateSettlement.StatusOf(new DateOnly(2026, 6, 1), end, 100m, 100m));
        Assert.Equal(RebateSettlement.NotDue, RebateSettlement.StatusOf(end, end, 100m, 0m));
        Assert.Equal(RebateSettlement.NotReached, RebateSettlement.StatusOf(new DateOnly(2026, 8, 1), end, 0m, 0m));
        Assert.Equal(RebateSettlement.Waiting, RebateSettlement.StatusOf(new DateOnly(2026, 7, 30), end, 100m, 40m));
        Assert.Equal(RebateSettlement.Short, RebateSettlement.StatusOf(new DateOnly(2026, 7, 31), end, 100m, 40m));
    }
}
