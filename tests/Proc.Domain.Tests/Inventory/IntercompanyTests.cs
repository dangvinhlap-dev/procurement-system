using Proc.Domain.Common;
using Proc.Domain.Inventory;
using Proc.Domain.MasterData;
using Proc.Domain.Pricing;

namespace Proc.Domain.Tests.Inventory;

/// <summary>Bán nội bộ (design.md mục 4, 6.3, 10.7, 10.8) và bảng giá nội bộ.</summary>
public class IntercompanyTests
{
    private static readonly Guid Seller = Guid.CreateVersion7();
    private static readonly Guid Buyer = Guid.CreateVersion7();
    private static readonly Uom Cai = Uom.Create("CAI", "Cái");

    private static Item NewItem() =>
        Item.Create("VB01", ItemType.Inventory, ItemCategory.Create("VT", "Vật tư"), Cai,
            new ItemDescription("Vòng bi", null, null, null, null, null, "10", null));

    private static (InventoryDocument Sale, Item Item) PostedSale()
    {
        var item = NewItem();
        var sale = InventoryDocument.CreateSale(Seller, "ICS-1",
            new SaleHeader(new DateOnly(2026, 10, 10), Guid.CreateVersion7(), Buyer, "HD-01", null, "Xe 29C", null));
        sale.SetLines([(new InventoryLineData(null, item.Id, Cai.Id, 10, 15_000m, "10", null, null, null), item)]);
        sale.MarkPosted(Guid.CreateVersion7(), DateTimeOffset.UtcNow, null);
        return (sale, item);
    }

    [Fact]
    public void BanNoiBo_KhongBanChoChinhMinh()
    {
        Assert.Contains("khác công ty bán", Assert.Throws<BusinessRuleException>(() =>
            InventoryDocument.CreateSale(Seller, "ICS-1", new SaleHeader(new DateOnly(2026, 10, 10), Guid.CreateVersion7(), Seller, null, null, null, null))).Message);
    }

    [Fact]
    public void PhieuNhapNoiBo_SinhTuPhieuBan_LienKetHaiChieu_GiaBanNoiBo()
    {
        var (sale, _) = PostedSale();
        var buyerMain = Guid.CreateVersion7();

        var receipt = sale.CreateIntercompanyReceipt("ICR-1", buyerMain);

        Assert.Equal((Buyer, "ICR", buyerMain, Seller, sale.Id, DocumentStatus.Draft), (receipt.CompanyId, receipt.DocType, receipt.ToWarehouseId, receipt.CounterCompanyId, receipt.LinkedDocumentId, receipt.Status));
        Assert.Equal(receipt.Id, sale.LinkedDocumentId);
        Assert.Equal((10m, 15_000m, 150_000m, 15_000m, sale.Lines[0].Id), (receipt.Lines[0].Qty, receipt.Lines[0].UnitPrice, receipt.TotalAmount, receipt.TotalVat, receipt.Lines[0].SourceLineId));
    }

    [Fact]
    public void XacNhanNhan_NhanThieu_KhongVuotSlGui_KhongTruocNgayXuat()
    {
        var (sale, _) = PostedSale();
        var receipt = sale.CreateIntercompanyReceipt("ICR-1", Guid.CreateVersion7());
        var line = receipt.Lines[0];
        var sent = new Dictionary<Guid, decimal> { [sale.Lines[0].Id] = 10 };

        Assert.Throws<BusinessRuleException>(() => receipt.ConfirmIntercompanyReceipt(new DateOnly(2026, 10, 12), sale.Date, null, [new(line.Id, 11, null, null)], sent));
        Assert.Throws<BusinessRuleException>(() => receipt.ConfirmIntercompanyReceipt(new DateOnly(2026, 10, 9), sale.Date, null, [new(line.Id, 8, null, null)], sent));
        Assert.Throws<BusinessRuleException>(() => receipt.ConfirmIntercompanyReceipt(new DateOnly(2026, 10, 12), sale.Date, null, [new(line.Id, 0, null, null)], sent));

        receipt.ConfirmIntercompanyReceipt(new DateOnly(2026, 10, 12), sale.Date, "Thiếu 2 cái, vỡ khi vận chuyển", [new(line.Id, 8, null, "Vỡ 2")], sent);
        Assert.Equal((8m, 120_000m, 12_000m, new DateOnly(2026, 10, 12)), (receipt.Lines[0].Qty, receipt.TotalAmount, receipt.TotalVat, receipt.Date));

        var entry = StockLedgerEntry.Receipt(receipt, receipt.Lines[0], Guid.CreateVersion7());
        Assert.Equal((1, 8m, 15_000m, 120_000m, true), (entry.Direction, entry.BaseQty, entry.UnitCost, entry.Amount, entry.IsCostFinal));
    }

    [Fact]
    public void BoGhiSoPhieuBan_HuyPhieuChoNhan_ChanKhiBenMuaDaNhan()
    {
        var (sale, _) = PostedSale();
        var receipt = sale.CreateIntercompanyReceipt("ICR-1", Guid.CreateVersion7());
        Assert.Throws<BusinessRuleException>(receipt.Cancel);

        receipt.MarkPosted(Guid.CreateVersion7(), DateTimeOffset.UtcNow, null);
        Assert.Contains("đã xác nhận", Assert.Throws<BusinessRuleException>(() => sale.DetachIntercompanyReceipt(receipt)).Message);

        receipt.MarkUnposted();
        sale.DetachIntercompanyReceipt(receipt);
        Assert.Equal((DocumentStatus.Cancelled, (Guid?)null), (receipt.Status, sale.LinkedDocumentId));
    }

    [Fact]
    public void XuatBan_GhiSoTheoGiaVonTamTinh_ChuaChot()
    {
        var (sale, _) = PostedSale();

        var entry = StockLedgerEntry.Sale(sale, sale.Lines[0], Guid.CreateVersion7(), 12_345.5m);

        Assert.Equal((-1, 10m, 12_345.5m, 123_455m, false), (entry.Direction, entry.BaseQty, entry.UnitCost, entry.Amount, entry.IsCostFinal));
    }

    [Fact]
    public void GiaNoiBo_KhongAm_HieuLucHopLe_ChongNhau()
    {
        var item = NewItem();
        Assert.Throws<BusinessRuleException>(() => InternalPrice.Create(Seller, Seller, item, 1, null, new DateOnly(2026, 1, 1), null, null));
        Assert.Throws<BusinessRuleException>(() => InternalPrice.Create(Seller, Buyer, item, -1, null, new DateOnly(2026, 1, 1), null, null));
        Assert.Throws<BusinessRuleException>(() => InternalPrice.Create(Seller, Buyer, item, 1, null, new DateOnly(2026, 2, 1), new DateOnly(2026, 1, 1), null));

        var price = InternalPrice.Create(Seller, Buyer, item, 15_000, "10", new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 30), null);
        Assert.True(price.Overlaps(new DateOnly(2026, 6, 1), null));
        Assert.False(price.Overlaps(new DateOnly(2026, 7, 1), null));
    }
}
