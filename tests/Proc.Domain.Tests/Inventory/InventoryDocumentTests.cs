using Proc.Domain.Common;
using Proc.Domain.Inventory;
using Proc.Domain.MasterData;

namespace Proc.Domain.Tests.Inventory;

/// <summary>
/// Chứng từ kho – phiếu nhập mua NCC ở trạng thái nháp (design.md mục 10.4, 12.1).
/// </summary>
public class InventoryDocumentTests
{
    private static readonly Guid Company = Guid.CreateVersion7();
    private static readonly ItemCategory Category = ItemCategory.Create("VT", "Vật tư");
    private static readonly Uom Hop = Uom.Create("HOP", "Hộp");
    private static readonly Uom Thung = Uom.Create("THUNG", "Thùng");

    private static ReceiptHeader Header(DateOnly? date = null) =>
        new(date ?? new DateOnly(2026, 10, 30), Guid.CreateVersion7(), Guid.CreateVersion7(), "0000123", "1C26TAA", null, null, null, null);

    private static Item NewItem(bool trackLot = false, ItemType type = ItemType.Inventory)
    {
        var item = Item.Create($"VT{Guid.NewGuid():N}"[..10], type, Category, Hop, new ItemDescription("Bu lông", null, null, null, null, null, "10", null));
        if (type == ItemType.Inventory) item.SetStockControl(type, trackLot, trackLot, null, null);
        item.SetConversions([(Thung, 24m)]);
        return item;
    }

    private static InventoryLineData Line(Item item, Guid uomId, decimal qty, decimal price, string? vat = "10", string? lot = null, DateOnly? expiry = null) =>
        new(null, item.Id, uomId, qty, price, vat, lot, expiry, null);

    [Fact]
    public void PhieuNhap_TinhSoLuongCoSoThanhTienVat()
    {
        var item = NewItem();
        var doc = InventoryDocument.CreateReceipt(Company, "GRN-BHA-2610-0001", Header());

        // 2 thùng × 24 = 48 hộp; 2 × 480.000 = 960.000; VAT 10% = 96.000. Dòng 2: 3 hộp × 20.333,333333 = 61.000 (làm tròn), KCT.
        doc.SetLines([(Line(item, Thung.Id, 2, 480_000m), item), (Line(item, Hop.Id, 3, 20_333.333333m, vat: "kct"), item)]);

        var first = doc.Lines[0];
        Assert.Equal((1, 48m, 24m, 960_000m, 96_000m), (first.LineNo, first.BaseQty, first.UomFactor, first.Amount, first.VatAmount));
        var second = doc.Lines[1];
        Assert.Equal((3m, 61_000m, "KCT", 0m), (second.BaseQty, second.Amount, second.VatCode, second.VatAmount));
        Assert.Equal((1_021_000m, 96_000m), (doc.TotalAmount, doc.TotalVat));
        Assert.Equal((DocumentStatus.Draft, "GRN"), (doc.Status, doc.DocType));
        Assert.Equal("GRN-BHA-2610-0001", doc.DocumentNumber);
    }

    [Fact]
    public void Dong_SuaTheoId_ThemVaXoa()
    {
        var item = NewItem();
        var doc = InventoryDocument.CreateReceipt(Company, "GRN-1", Header());
        doc.SetLines([(Line(item, Hop.Id, 1, 100), item), (Line(item, Hop.Id, 2, 100), item)]);
        var keep = doc.Lines[1];

        doc.SetLines([(Line(item, Hop.Id, 5, 100) with { Id = keep.Id }, item), (Line(item, Thung.Id, 1, 2_400), item)]);

        Assert.Equal(2, doc.Lines.Count);
        Assert.Same(keep, doc.Lines.Single(l => l.LineNo == 1));
        Assert.Equal(5m, keep.Qty);
        Assert.Equal(2_900m, doc.TotalAmount);
    }

    [Fact]
    public void Dong_TuChoiDuLieuSai()
    {
        var item = NewItem();
        var doc = InventoryDocument.CreateReceipt(Company, "GRN-1", Header());
        var kg = Uom.Create("KG", "Kg");

        Assert.Throws<BusinessRuleException>(() => doc.SetLines([]));
        Assert.Contains("ĐVT", Assert.Throws<BusinessRuleException>(() => doc.SetLines([(Line(item, kg.Id, 1, 1), item)])).Message);
        Assert.Throws<BusinessRuleException>(() => doc.SetLines([(Line(item, Hop.Id, 0, 1), item)]));
        Assert.Throws<BusinessRuleException>(() => doc.SetLines([(Line(item, Hop.Id, 1.00001m, 1), item)]));
        Assert.Throws<BusinessRuleException>(() => doc.SetLines([(Line(item, Hop.Id, 1, -1), item)]));
        Assert.Throws<BusinessRuleException>(() => doc.SetLines([(Line(item, Hop.Id, 1, 1, vat: "7"), item)]));

        // Chỉ hàng tồn kho mới đi qua kho.
        var service = NewItem(type: ItemType.Service);
        Assert.Contains("hàng tồn kho", Assert.Throws<BusinessRuleException>(() => doc.SetLines([(Line(service, Hop.Id, 1, 1), service)])).Message);
    }

    [Fact]
    public void Dong_HangQuanLyLoVaHanDung_BatBuocNhap()
    {
        var item = NewItem(trackLot: true);
        var doc = InventoryDocument.CreateReceipt(Company, "GRN-1", Header());

        Assert.Contains("số lô", Assert.Throws<BusinessRuleException>(() => doc.SetLines([(Line(item, Hop.Id, 1, 1), item)])).Message);
        Assert.Contains("hạn dùng", Assert.Throws<BusinessRuleException>(() => doc.SetLines([(Line(item, Hop.Id, 1, 1, lot: "L01"), item)])).Message);
        doc.SetLines([(Line(item, Hop.Id, 1, 1, lot: " L01 ", expiry: new DateOnly(2027, 12, 31)), item)]);
        Assert.Equal("L01", doc.Lines[0].LotNo);
    }

    [Fact]
    public void HuyPhieu_KhongSuaDuocNua()
    {
        var item = NewItem();
        var doc = InventoryDocument.CreateReceipt(Company, "GRN-1", Header());
        doc.SetLines([(Line(item, Hop.Id, 1, 1), item)]);

        doc.Cancel();

        Assert.Equal(DocumentStatus.Cancelled, doc.Status);
        Assert.Throws<BusinessRuleException>(() => doc.UpdateReceipt(Header()));
        Assert.Throws<BusinessRuleException>(doc.Cancel);
    }
}
