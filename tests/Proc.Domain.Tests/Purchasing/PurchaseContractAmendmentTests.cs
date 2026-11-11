using Proc.Domain.Common;
using Proc.Domain.MasterData;
using Proc.Domain.Purchasing;

namespace Proc.Domain.Tests.Purchasing;

/// <summary>Phụ lục hợp đồng nguyên tắc (lát B4b – design.md mục 18.7).</summary>
public class PurchaseContractAmendmentTests
{
    private static readonly Guid Company = Guid.CreateVersion7();
    private static readonly Uom Cai = Uom.Create("CAI", "Cái");
    private static readonly DateOnly Today = new(2026, 11, 1);

    private static (PurchaseContract Contract, Item Item) ActiveContract()
    {
        var item = Item.Create("VB01", ItemType.Inventory, ItemCategory.Create("VT", "Vật tư"), Cai,
            new ItemDescription("Vòng bi", null, null, null, null, null, "10", null));
        var contract = PurchaseContract.Create(Company, "CTR-1", new ContractHeader(
            Guid.CreateVersion7(), Guid.CreateVersion7(), "HĐ 01", "Hợp đồng vòng bi", Today, Today, new DateOnly(2026, 12, 31),
            null, "30 ngày", 500_000_000, null, null, null, null, null, null));
        // 1.000 cái × 50.000 × 95% = 47.500.000.
        contract.SetLines([(new ContractLineData(null, item.Id, Cai.Id, 1_000, null, null), item, new ContractLinePrice(50_000, 5))]);
        contract.Submit();
        contract.MarkApproved(DateTimeOffset.UtcNow);
        return (contract, item);
    }

    private static ContractAmendmentData Data(PurchaseContract c) =>
        new("PL 01", "Gia hạn thêm 6 tháng", Today, c.ValidTo, c.PriceListId, c.Terms, null);

    [Fact]
    public void LapPhuLuc_PhaiHieuLuc_CoLyDo_CoThayDoi()
    {
        var (contract, _) = ActiveContract();
        // Không đổi gì, thiếu lý do, ngày hết hạn mới trước hôm nay, hạn mức âm đều bị chặn.
        Assert.Throws<BusinessRuleException>(() => PurchaseContractAmendment.Create(contract, 1, Data(contract), Today));
        Assert.Throws<BusinessRuleException>(() => PurchaseContractAmendment.Create(contract, 1, Data(contract) with { Reason = " ", NewValidTo = new DateOnly(2027, 6, 30) }, Today));
        Assert.Throws<BusinessRuleException>(() => PurchaseContractAmendment.Create(contract, 1, Data(contract) with { NewValidTo = new DateOnly(2026, 10, 31) }, Today));
        Assert.Throws<BusinessRuleException>(() => PurchaseContractAmendment.Create(contract, 1,
            Data(contract) with { Terms = contract.Terms with { CreditLimit = -1 } }, Today));
        // Chỉ khác khoảng trắng thì không tính là đổi điều khoản.
        Assert.Throws<BusinessRuleException>(() => PurchaseContractAmendment.Create(contract, 1,
            Data(contract) with { Terms = contract.Terms with { PaymentTerms = " 30 ngày " } }, Today));

        var amendment = PurchaseContractAmendment.Create(contract, 1,
            Data(contract) with { NewValidTo = new DateOnly(2027, 6, 30), Terms = contract.Terms with { PaymentTerms = "45 ngày", CreditLimit = 800_000_000 } }, Today);
        Assert.Equal(("CTR-1-PL01", ContractAmendmentStatus.Draft, new DateOnly(2026, 12, 31)), (amendment.No, amendment.Status, amendment.OldValidTo));
        Assert.Equal(["thanh toán", "hạn mức công nợ"], amendment.NewTerms.DifferencesFrom(contract.Terms));

        // Hợp đồng chưa hiệu lực thì không lập phụ lục.
        var draft = PurchaseContract.Create(Company, "CTR-2", new ContractHeader(
            Guid.CreateVersion7(), Guid.CreateVersion7(), null, "Nháp", null, Today, Today.AddMonths(1), null, null, null, null, null, null, null, null, null));
        Assert.Throws<BusinessRuleException>(() => PurchaseContractAmendment.Create(draft, 1, Data(draft) with { NewValidTo = Today.AddMonths(2) }, Today));
    }

    [Fact]
    public void DuyetPhuLuc_ApVaoHopDong_DoiBangGiaThiTinhLaiGia()
    {
        var (contract, _) = ActiveContract();
        var newList = Guid.CreateVersion7();
        var amendment = PurchaseContractAmendment.Create(contract, 1,
            Data(contract) with { NewValidTo = new DateOnly(2027, 6, 30), NewPriceListId = newList, Terms = contract.Terms with { PaymentTerms = "45 ngày" } }, Today);
        // Chưa duyệt thì không áp được.
        Assert.Throws<BusinessRuleException>(() => contract.ApplyAmendment(amendment, new Dictionary<Guid, ContractLinePrice>()));

        Assert.Equal(1, amendment.Submit());
        Assert.Throws<BusinessRuleException>(() => amendment.Update(contract, Data(contract), Today));
        amendment.ReturnToDraft("Thiếu biên bản");
        Assert.Equal((ContractAmendmentStatus.Draft, "Thiếu biên bản"), (amendment.Status, amendment.RejectReason));
        Assert.Equal(2, amendment.Submit());
        amendment.MarkApproved(DateTimeOffset.UtcNow);

        // Đổi bảng giá mà thiếu giá dòng thì chặn; đủ giá: 1.000 × 48.000 × 95% = 45.600.000.
        Assert.Throws<BusinessRuleException>(() => contract.ApplyAmendment(amendment, new Dictionary<Guid, ContractLinePrice>()));
        contract.ApplyAmendment(amendment, new Dictionary<Guid, ContractLinePrice> { [contract.Lines[0].Id] = new(48_000, 5) });
        Assert.Equal((new DateOnly(2027, 6, 30), newList, "45 ngày", 45_600_000m),
            (contract.ValidTo, contract.PriceListId, contract.PaymentTerms, contract.ExpectedAmount));
        Assert.False(amendment.IsOpen);
        Assert.Throws<BusinessRuleException>(() => amendment.Cancel("Thử hủy"));
    }
}
