using Proc.Domain.Analysis;
using Proc.Domain.Common;

namespace Proc.Domain.Tests.Analysis;

/// <summary>
/// Dữ liệu mua lịch sử (design.md mục 18.1): kiểm tra một dòng nhập từ Excel.
/// </summary>
public class PurchaseHistoryTests
{
    private static readonly DateOnly Today = new(2026, 11, 1);
    private static readonly Guid Company = Guid.NewGuid();
    private static readonly Guid Batch = Guid.NewGuid();

    private static PurchaseHistoryData Data(
        decimal quantity = 10m, decimal unitPrice = 15_000m, DateOnly? purchaseDate = null, DateOnly? requestDate = null,
        decimal? vat = 10m, string? currency = null, decimal? relatedCost = null, string itemCode = " vt-001 ") =>
        new("Phân xưởng 1", itemCode, "Bu lông M8", "M8x30", "Hòa Phát", "Việt Nam", "TCVN 1916", "Cái",
            requestDate, purchaseDate ?? new DateOnly(2026, 3, 15), quantity, unitPrice, vat, currency, relatedCost, null,
            "Công ty Thép Việt", " 0101234567 ", "Anh Nam 0912", "30 ngày", "2%", null, true, false, "Đổi 5 cái lỗi");

    [Fact]
    public void TaoDong_ChuanHoaMa_TinhThanhTien_ChuoiTimKiemKhongDau()
    {
        var record = PurchaseHistoryRecord.Create(Company, Batch, 2, Data(quantity: 3.5m, unitPrice: 1_234.567m), Today);

        Assert.Equal("VT-001", record.ItemCode);
        Assert.Equal("0101234567", record.SupplierTaxCode);
        Assert.Equal("VND", record.Currency);
        // 3,5 × 1.234,567 = 4.320,9845 → làm tròn tiền 2 số lẻ = 4.320,98.
        Assert.Equal(4_320.98m, record.Amount);
        Assert.Contains("bu long m8", record.SearchText);
        Assert.Contains("thep viet", record.SearchText);
        Assert.Null(record.ItemId);
        Assert.Null(record.SupplierId);
    }

    [Theory]
    [InlineData(0, 1000)]
    [InlineData(-1, 1000)]
    [InlineData(5, -1)]
    public void SoLuongPhaiDuong_DonGiaKhongAm(decimal quantity, decimal unitPrice)
    {
        Assert.Throws<BusinessRuleException>(() => PurchaseHistoryRecord.Create(Company, Batch, 2, Data(quantity, unitPrice), Today));
    }

    [Fact]
    public void DonGiaBang0_DuocNhap()
    {
        // Hàng tặng kèm, tài trợ: giá 0 vẫn là dữ liệu mua thật.
        var record = PurchaseHistoryRecord.Create(Company, Batch, 2, Data(unitPrice: 0m), Today);

        Assert.Equal(0m, record.Amount);
    }

    [Fact]
    public void NgayMuaSauHomNay_Loi()
    {
        Assert.Throws<BusinessRuleException>(() =>
            PurchaseHistoryRecord.Create(Company, Batch, 2, Data(purchaseDate: Today.AddDays(1)), Today));
    }

    [Fact]
    public void NgayYeuCauSauNgayMua_Loi()
    {
        Assert.Throws<BusinessRuleException>(() =>
            PurchaseHistoryRecord.Create(Company, Batch, 2, Data(purchaseDate: new DateOnly(2026, 3, 1), requestDate: new DateOnly(2026, 3, 2)), Today));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void VatNgoai0Den100_Loi(decimal vat)
    {
        Assert.Throws<BusinessRuleException>(() => PurchaseHistoryRecord.Create(Company, Batch, 2, Data(vat: vat), Today));
    }

    [Fact]
    public void ChiPhiLienQuanAm_Loi()
    {
        Assert.Throws<BusinessRuleException>(() => PurchaseHistoryRecord.Create(Company, Batch, 2, Data(relatedCost: -5m), Today));
    }

    [Theory]
    [InlineData("VN")]
    [InlineData("ĐỒNG")]
    public void TienTeKhongPhaiMa3Chu_Loi(string currency)
    {
        Assert.Throws<BusinessRuleException>(() => PurchaseHistoryRecord.Create(Company, Batch, 2, Data(currency: currency), Today));
    }

    [Fact]
    public void ThieuMaVatTu_Loi()
    {
        Assert.Throws<BusinessRuleException>(() => PurchaseHistoryRecord.Create(Company, Batch, 2, Data(itemCode: "  "), Today));
    }

    [Fact]
    public void GanKetQuaKhop()
    {
        var record = PurchaseHistoryRecord.Create(Company, Batch, 2, Data(), Today);
        var item = Guid.NewGuid();

        record.Match(item, null);

        Assert.Equal(item, record.ItemId);
        Assert.Null(record.SupplierId);
    }
}
