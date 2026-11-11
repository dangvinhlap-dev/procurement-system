using Proc.Domain.Common;
using Proc.Domain.MasterData;
using Proc.Domain.Purchasing;

namespace Proc.Domain.Tests.Purchasing;

/// <summary>Hợp đồng nguyên tắc phương án 2 (lát B4a – design.md mục 18.7).</summary>
public class PurchaseContractTests
{
    private static readonly Guid Company = Guid.CreateVersion7();
    private static readonly Uom Cai = Uom.Create("CAI", "Cái");
    private static readonly Uom Thung = Uom.Create("THUNG", "Thùng");
    private static readonly DateOnly Today = new(2026, 11, 1);

    private static Item NewItem()
    {
        var item = Item.Create("VB01", ItemType.Inventory, ItemCategory.Create("VT", "Vật tư"), Cai,
            new ItemDescription("Vòng bi", null, null, null, null, null, "10", null));
        item.SetConversions([(Thung, 12m)]);
        return item;
    }

    private static ContractHeader Header(DateOnly from, DateOnly to, Guid? supplier = null) =>
        new(supplier ?? Guid.CreateVersion7(), Guid.CreateVersion7(), "HĐ 01/2026", "Hợp đồng nguyên tắc vòng bi", Today, from, to,
            "Điều chỉnh theo quý", "30 ngày", 500_000_000, "Giao tại kho", "2% doanh số năm", null, "Phạt 0,1%/ngày chậm giao", null, null);

    [Fact]
    public void HopDong_DauSai_BiChan()
    {
        Assert.Throws<BusinessRuleException>(() => PurchaseContract.Create(Company, "CTR-1", Header(Today, Today.AddDays(-1))));
        Assert.Throws<BusinessRuleException>(() => PurchaseContract.Create(Company, "CTR-1", Header(Today, Today) with { Title = " " }));
        Assert.Throws<BusinessRuleException>(() => PurchaseContract.Create(Company, "CTR-1", Header(Today, Today) with { PriceListId = Guid.Empty }));
        Assert.Throws<BusinessRuleException>(() => PurchaseContract.Create(Company, "CTR-1", Header(Today, Today) with { CreditLimit = -1 }));
        var contract = PurchaseContract.Create(Company, "CTR-1", Header(Today, Today.AddYears(1)) with { ContractNo = "  ", PaymentTerms = " 30 ngày " });
        Assert.Equal((null, "30 ngày", ContractStatus.Draft), (contract.ContractNo, contract.PaymentTerms, contract.Status));
    }

    [Fact]
    public void SanLuongDuKien_GiaTheoBangGia_GiaTriXetDuyet()
    {
        var item = NewItem();
        var contract = PurchaseContract.Create(Company, "CTR-1", Header(Today, Today.AddYears(1)));
        // 1.000 cái × 50.000 × 95% = 47.500.000; 10 thùng × 600.000 = 6.000.000 → 53.500.000.
        contract.SetLines([
            (new ContractLineData(null, item.Id, Cai.Id, 1_000, 200, null), item, new ContractLinePrice(50_000, 5)),
            (new ContractLineData(null, item.Id, Thung.Id, 10, null, null), item, new ContractLinePrice(600_000, 0)),
        ]);
        Assert.Equal((47_500_000m, 53_500_000m), (contract.Lines[0].ExpectedAmount, contract.ApprovalAmount));
        // Trùng mặt hàng × ĐVT, SL cam kết lớn hơn SL dự kiến, SL ≤ 0 đều bị chặn.
        Assert.Throws<BusinessRuleException>(() => contract.SetLines([
            (new ContractLineData(null, item.Id, Cai.Id, 1, null, null), item, new ContractLinePrice(1, 0)),
            (new ContractLineData(null, item.Id, Cai.Id, 2, null, null), item, new ContractLinePrice(1, 0)),
        ]));
        Assert.Throws<BusinessRuleException>(() => contract.SetLines([(new ContractLineData(null, item.Id, Cai.Id, 10, 11, null), item, new ContractLinePrice(1, 0))]));
        Assert.Throws<BusinessRuleException>(() => contract.SetLines([(new ContractLineData(null, item.Id, Cai.Id, 0, null, null), item, new ContractLinePrice(1, 0))]));
        Assert.Throws<BusinessRuleException>(() => contract.SetDepartments([Guid.Empty, Guid.Empty]));
    }

    [Fact]
    public void VongDoi_GuiDuyet_TuChoi_HieuLuc_ChamDut()
    {
        var contract = PurchaseContract.Create(Company, "CTR-1", Header(Today, new DateOnly(2026, 11, 30)));
        contract.SetDepartments([Guid.CreateVersion7()]);
        Assert.Equal(1, contract.Submit());
        Assert.True(contract.IsAwaitingApproval);
        Assert.Throws<BusinessRuleException>(() => contract.Update(Header(Today, Today)));
        Assert.Throws<BusinessRuleException>(() => contract.Cancel("Thử hủy"));
        contract.ReturnToDraft("Thiếu biên bản đàm phán");
        Assert.Equal((ContractStatus.Draft, "Thiếu biên bản đàm phán"), (contract.Status, contract.RejectReason));
        Assert.Equal(2, contract.Submit());
        Assert.Null(contract.RejectReason);
        contract.MarkApproved(DateTimeOffset.UtcNow);
        Assert.True(contract.AppliesOn(Today));
        Assert.False(contract.AppliesOn(new DateOnly(2026, 12, 1)));
        // Hết hạn 30/11, hôm nay 01/11: còn 29 ngày → cảnh báo (ngưỡng 30).
        Assert.Equal((29, true, false), (contract.DaysToExpiry(Today), contract.ExpiresWithin(Today, 30), contract.ExpiresWithin(Today, 10)));
        Assert.True(contract.Overlaps(new DateOnly(2026, 11, 30), new DateOnly(2027, 1, 1)));
        Assert.False(contract.Overlaps(new DateOnly(2026, 12, 1), new DateOnly(2027, 1, 1)));

        Assert.Throws<BusinessRuleException>(() => contract.Terminate(Today, " "));
        Assert.Throws<BusinessRuleException>(() => contract.Terminate(new DateOnly(2026, 12, 5), "NCC vi phạm"));
        contract.Terminate(new DateOnly(2026, 11, 15), "NCC vi phạm chất lượng");
        Assert.Equal((ContractStatus.Terminated, false), (contract.Status, contract.AppliesOn(Today)));
    }
}
