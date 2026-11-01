using Proc.Domain.Analysis;
using Proc.Domain.Common;

namespace Proc.Domain.Tests.Analysis;

/// <summary>
/// Ánh xạ dữ liệu mua lịch sử (design.md mục 18.2): khóa NCC cũ, điểm giống nhau để gợi ý mã chung, tạo ánh xạ.
/// </summary>
public class PurchaseHistoryMappingTests
{
    [Fact]
    public void KhoaNcc_CoMst_DungMst()
    {
        Assert.Equal("MST:0101234567", PurchaseHistoryKeys.Supplier(" 0101234567 ", "Công ty Thép Việt"));
    }

    [Fact]
    public void KhoaNcc_KhongMst_DungTenKhongDau()
    {
        // Gõ khác dấu, khác hoa thường, thừa khoảng trắng vẫn là cùng một NCC.
        Assert.Equal("TEN:cong ty thep viet", PurchaseHistoryKeys.Supplier(null, "Công ty  THÉP Việt"));
        Assert.Equal(PurchaseHistoryKeys.Supplier("", "cong ty thep viet"), PurchaseHistoryKeys.Supplier(null, "Công ty Thép Việt"));
    }

    [Fact]
    public void KhoaMaVatTu_VietHoaBoKhoangTrang()
    {
        Assert.Equal("VT-001", PurchaseHistoryKeys.Item(" vt-001 "));
    }

    [Fact]
    public void DiemGiong_TrungHet_100_KhongTrung_0()
    {
        Assert.Equal(100, NameSimilarity.Score("Bu lông M8x30", "bu long m8x30"));
        Assert.Equal(0, NameSimilarity.Score("Bu lông M8", "Dầu thủy lực"));
        Assert.Equal(0, NameSimilarity.Score("", "Dầu thủy lực"));
    }

    [Fact]
    public void DiemGiong_TrungMotPhan_TheoDice()
    {
        // {bu, long, m8} và {bu, long, m10}: trùng 2 từ → 2×2 / (3+3) = 66,7% → 67.
        Assert.Equal(67, NameSimilarity.Score("Bu lông M8", "Bu-lông M10"));
    }

    [Fact]
    public void DiemGiongNcc_BoTuChungLoaiHinhDoanhNghiep()
    {
        // "Công ty TNHH" không làm hai NCC khác nhau thành giống nhau.
        Assert.Equal(0, NameSimilarity.Score("Công ty TNHH Thép Việt", "Công ty TNHH Dầu khí", NameSimilarity.CompanyWords));
        Assert.Equal(100, NameSimilarity.Score("Công ty TNHH Thép Việt", "Thép Việt JSC", NameSimilarity.CompanyWords));
    }

    [Fact]
    public void TaoAnhXaMaVatTu_DoiDich()
    {
        var company = Guid.NewGuid();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var mapping = PurchaseHistoryItemMapping.Create(company, " vt-001 ", first);

        Assert.Equal("VT-001", mapping.SourceCode);
        Assert.Equal(first, mapping.ItemId);
        mapping.Retarget(second);
        Assert.Equal(second, mapping.ItemId);
    }

    [Fact]
    public void TaoAnhXa_ThieuKhoaHoacDich_Loi()
    {
        Assert.Throws<BusinessRuleException>(() => PurchaseHistoryItemMapping.Create(Guid.NewGuid(), "  ", Guid.NewGuid()));
        Assert.Throws<BusinessRuleException>(() => PurchaseHistoryItemMapping.Create(Guid.NewGuid(), "VT-1", Guid.Empty));
        Assert.Throws<BusinessRuleException>(() => PurchaseHistorySupplierMapping.Create(Guid.NewGuid(), "MST:1", Guid.Empty));
        Assert.Throws<BusinessRuleException>(() => PurchaseHistorySupplierMapping.Create(Guid.NewGuid(), "0101234567", Guid.NewGuid()));
    }
}
