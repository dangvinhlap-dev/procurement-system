using Proc.Domain.Common;
using Proc.Domain.Inventory;
using Proc.Domain.MasterData;

namespace Proc.Domain.Tests.Inventory;

/// <summary>
/// Ghi sổ phiếu nhập: dòng sổ kho và trạng thái phiếu (design.md mục 6.1, 12).
/// </summary>
public class StockLedgerTests
{
    private static readonly Uom Hop = Uom.Create("HOP", "Hộp");
    private static readonly Uom Thung = Uom.Create("THUNG", "Thùng");

    private static (InventoryDocument Doc, Guid Warehouse) PostableReceipt()
    {
        var item = Item.Create("VT01", ItemType.Inventory, ItemCategory.Create("VT", "Vật tư"), Hop,
            new ItemDescription("Bu lông", null, null, null, null, null, null, null));
        item.SetConversions([(Thung, 24m)]);
        var warehouse = Guid.CreateVersion7();
        var doc = InventoryDocument.CreateReceipt(Guid.CreateVersion7(), "GRN-1",
            new ReceiptHeader(new DateOnly(2026, 10, 30), warehouse, Guid.CreateVersion7(), null, null, null, null, null, null));
        doc.SetLines([(new InventoryLineData(null, item.Id, Thung.Id, 2, 480_000m, "10", "L01", null, null), item)]);
        return (doc, warehouse);
    }

    [Fact]
    public void DongSoNhap_TheoDvtCoSo_GiaVonBangThanhTienChiaSoLuong()
    {
        var (doc, warehouse) = PostableReceipt();
        var period = Guid.CreateVersion7();

        var entry = StockLedgerEntry.Receipt(doc, doc.Lines[0], period);

        // 2 thùng × 24 = 48 hộp; 960.000 ÷ 48 = 20.000 đ/hộp; giá trị không gồm VAT.
        Assert.Equal((warehouse, 1, 48m, 20_000m, 960_000m, "L01"), (entry.WarehouseId, entry.Direction, entry.BaseQty, entry.UnitCost, entry.Amount, entry.LotNo));
        Assert.Equal((period, "GRN", new DateOnly(2026, 10, 30), 48m, true), (entry.PeriodId, entry.DocType, entry.PostingDate, entry.SignedQty, entry.IsCostFinal));
    }

    [Fact]
    public void GhiSo_BoGhiSo_ChuyenTrangThai()
    {
        var (doc, _) = PostableReceipt();
        var period = Guid.CreateVersion7();

        doc.MarkPosted(period, DateTimeOffset.UtcNow, Guid.CreateVersion7());
        Assert.Equal((DocumentStatus.Posted, period), (doc.Status, doc.PeriodId));
        // Đã ghi sổ thì không sửa trực tiếp, không ghi sổ lần hai, không hủy (CLAUDE.md mục 4).
        Assert.Throws<BusinessRuleException>(() => doc.MarkPosted(period, DateTimeOffset.UtcNow, null));
        Assert.Throws<BusinessRuleException>(doc.Cancel);

        doc.MarkUnposted();
        Assert.Equal((DocumentStatus.Draft, (Guid?)null, (DateTimeOffset?)null), (doc.Status, doc.PeriodId, doc.PostedAt));
        Assert.Throws<BusinessRuleException>(doc.MarkUnposted);
    }
}
