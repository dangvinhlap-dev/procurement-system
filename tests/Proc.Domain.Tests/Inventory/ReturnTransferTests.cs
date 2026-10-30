using Proc.Domain.Common;
using Proc.Domain.Inventory;
using Proc.Domain.MasterData;

namespace Proc.Domain.Tests.Inventory;

/// <summary>
/// Phiếu xuất trả NCC và phiếu chuyển kho nội bộ (design.md mục 6.1, 10.5, 10.6).
/// </summary>
public class ReturnTransferTests
{
    private static readonly Guid Company = Guid.CreateVersion7();
    private static readonly Uom Hop = Uom.Create("HOP", "Hộp");
    private static readonly Uom Thung = Uom.Create("THUNG", "Thùng");

    private static Item NewItem()
    {
        var item = Item.Create("VT01", ItemType.Inventory, ItemCategory.Create("VT", "Vật tư"), Hop,
            new ItemDescription("Bu lông", null, null, null, null, null, "10", null));
        item.SetConversions([(Thung, 24m)]);
        return item;
    }

    private static (InventoryDocument Receipt, Item Item) PostedReceipt(bool post = true)
    {
        var item = NewItem();
        var receipt = InventoryDocument.CreateReceipt(Company, "GRN-1",
            new ReceiptHeader(new DateOnly(2026, 10, 10), Guid.CreateVersion7(), Guid.CreateVersion7(), null, null, null, null, null, null));
        receipt.SetLines([(new InventoryLineData(null, item.Id, Thung.Id, 2, 480_000m, "10", null, null, null), item)]);
        if (post) receipt.MarkPosted(Guid.CreateVersion7(), DateTimeOffset.UtcNow, null);
        return (receipt, item);
    }

    [Fact]
    public void PhieuTra_LayKhoVaNccTheoPhieuNhapGoc_GiaGoc()
    {
        var (receipt, item) = PostedReceipt();
        var source = receipt.Lines[0];

        var ret = InventoryDocument.CreateReturn(Company, "RTV-1", new ReturnHeader(new DateOnly(2026, 10, 20), receipt, "HD-DC-01", "Hàng lỗi"));
        ret.SetLines([(new InventoryLineData(null, item.Id, Thung.Id, 1, source.UnitPrice, source.VatCode, null, null, null, source.Id), item)]);

        Assert.Equal((receipt.ToWarehouseId, receipt.SupplierId, receipt.Id, "RTV"), (ret.FromWarehouseId, ret.SupplierId, ret.LinkedDocumentId, ret.DocType));
        Assert.Equal((source.Id, 480_000m, 48_000m), (ret.Lines[0].SourceLineId, ret.TotalAmount, ret.TotalVat));

        var entry = StockLedgerEntry.Return(ret, ret.Lines[0], Guid.CreateVersion7());
        Assert.Equal((-1, 24m, 20_000m, 480_000m, true), (entry.Direction, entry.BaseQty, entry.UnitCost, entry.Amount, entry.IsCostFinal));
    }

    [Fact]
    public void PhieuTra_PhieuGocPhaiDaGhiSo_NgayTraKhongTruocNgayNhap_DongPhaiTroDongGoc()
    {
        var (draft, item) = PostedReceipt(post: false);
        Assert.Contains("đã ghi sổ", Assert.Throws<BusinessRuleException>(() =>
            InventoryDocument.CreateReturn(Company, "RTV-1", new ReturnHeader(new DateOnly(2026, 10, 20), draft, null, null))).Message);

        var (receipt, _) = PostedReceipt();
        Assert.Throws<BusinessRuleException>(() =>
            InventoryDocument.CreateReturn(Company, "RTV-1", new ReturnHeader(new DateOnly(2026, 10, 9), receipt, null, null)));
        var ret = InventoryDocument.CreateReturn(Company, "RTV-1", new ReturnHeader(new DateOnly(2026, 10, 20), receipt, null, null));
        Assert.Throws<BusinessRuleException>(() =>
            ret.SetLines([(new InventoryLineData(null, item.Id, Thung.Id, 1, 480_000m, "10", null, null, null), item)]));
    }

    [Fact]
    public void PhieuChuyen_HaiKhoKhacNhau_KhongCoDonGia_SinhCapDongSoCungGia()
    {
        var item = NewItem();
        var from = Guid.CreateVersion7();
        var to = Guid.CreateVersion7();
        Assert.Throws<BusinessRuleException>(() =>
            InventoryDocument.CreateTransfer(Company, "TRF-1", new TransferHeader(new DateOnly(2026, 10, 20), from, from, null, null, null)));

        var transfer = InventoryDocument.CreateTransfer(Company, "TRF-1", new TransferHeader(new DateOnly(2026, 10, 20), from, to, "An", "Bình", null));
        Assert.Throws<BusinessRuleException>(() =>
            transfer.SetLines([(new InventoryLineData(null, item.Id, Thung.Id, 2, 1_000m, null, null, null, null), item)]));
        transfer.SetLines([(new InventoryLineData(null, item.Id, Thung.Id, 2, 0, null, null, null, null), item)]);

        // 48 hộp × 20.416,666667 = 980.000,00 – xuất kho đi và nhập kho đến cùng giá, chưa chốt (đóng kỳ tính lại).
        var (outEntry, inEntry) = StockLedgerEntry.Transfer(transfer, transfer.Lines[0], Guid.CreateVersion7(), 20_416.6666666m);
        Assert.Equal((from, -1, 48m, 20_416.666667m, 980_000m, false), (outEntry.WarehouseId, outEntry.Direction, outEntry.BaseQty, outEntry.UnitCost, outEntry.Amount, outEntry.IsCostFinal));
        Assert.Equal((to, 1, 980_000m), (inEntry.WarehouseId, inEntry.Direction, inEntry.Amount));
        Assert.Equal((0m, "Bình"), (transfer.TotalAmount, transfer.Receiver));
    }
}
