using Proc.Domain.Common;
using Proc.Domain.Documents;

namespace Proc.Domain.Tests.Documents;

/// <summary>
/// Mẫu số chứng từ PO-{CTY}-{YYMM}-{0000} (design.md mục 5.4, 12.2).
/// </summary>
public class DocumentNumberFormatTests
{
    private static readonly DateOnly Oct2026 = new(2026, 10, 29);

    [Fact]
    public void MauMacDinh_SinhSoTheoCongTyVaThangCuaNgayChungTu()
    {
        var format = DocumentNumberFormat.Default(DocumentTypes.PurchaseOrder);

        Assert.Equal("PO-{CTY}-{YYMM}-{0000}", format.Pattern);
        Assert.Equal("PO-BHA-2610-0001", format.Render("BHA", Oct2026, 1));
        Assert.Equal("PO-BHA-2610-0125", format.Render("BHA", Oct2026, 125));
    }

    [Fact]
    public void VuotSoChuSo_KhongCatBotMaNoiRong()
    {
        // Cắt bớt sẽ sinh số trùng; nới rộng thì số vẫn duy nhất, chỉ dài hơn.
        var format = DocumentNumberFormat.Parse("PO-{0000}");
        Assert.Equal("PO-10000", format.Render("BHA", Oct2026, 10000));
    }

    [Theory]
    [InlineData("PO-{CTY}-{YYMM}-{0000}", "202610")]
    [InlineData("PO/{YYYY}/{MM}/{000}", "202610")]
    [InlineData("PO-{CTY}-{YYYY}-{00000}", "2026")]
    [InlineData("PO-{YY}{000}", "2026")]
    [InlineData("PO-{CTY}-{000000}", "")]
    public void KyDanhSo_TheoMocThoiGianTrongMau(string pattern, string period)
    {
        // Mẫu có tháng thì mỗi tháng đếm lại từ 1; chỉ có năm thì mỗi năm; không có ngày thì đếm liên tục.
        Assert.Equal(period, DocumentNumberFormat.Parse(pattern).PeriodKey(Oct2026));
    }

    [Fact]
    public void CacMaThoiGian_HienDungGiaTri()
    {
        var format = DocumentNumberFormat.Parse("GRN.{YYYY}.{YY}.{MM}.{YYMM}.{CTY}.{000}");
        Assert.Equal("GRN.2026.26.10.2610.NMI.007", format.Render("NMI", Oct2026, 7));
    }

    [Theory]
    [InlineData("", "trống")]
    [InlineData("PO-{CTY}-{YYMM}", "nhóm số")]
    [InlineData("PO-{000}-{0000}", "một nhóm số")]
    [InlineData("PO-{00}", "3–10")]
    [InlineData("PO-{MM}-{0000}", "năm")]
    [InlineData("PO-{NGAY}-{0000}", "{NGAY}")]
    [InlineData("PO {0000}", "ký tự")]
    [InlineData("PO-{CTY}-{YYMM}-{0000}-QUA-DAI-QUA-DAI-QUA-DAI", "40")]
    public void MauKhongHopLe_BaoLoiTiengViet(string pattern, string expected)
    {
        var error = Assert.Throws<BusinessRuleException>(() => DocumentNumberFormat.Parse(pattern));
        Assert.Contains(expected, error.Message);
    }

    [Fact]
    public void MauChuThuong_DuocChuanHoaChuHoa()
    {
        Assert.Equal("PO-{CTY}-{YYMM}-{0000}", DocumentNumberFormat.Parse(" po-{cty}-{yymm}-{0000} ").Pattern);
    }

    [Fact]
    public void LoaiChungTu_CoDuCacLoaiTrongThietKe()
    {
        // design.md mục 6.1, 6.2: PR, RFQ, PO và các phiếu kho.
        string[] required = ["PR", "RFQ", "PO", "GRN", "RTV", "TRF", "ICS", "ICR", "ADJ"];
        Assert.All(required, code => Assert.True(DocumentTypes.IsKnown(code), code));
        Assert.False(DocumentTypes.IsKnown("XYZ"));
        Assert.Equal("Đơn mua hàng", DocumentTypes.All.Single(t => t.Code == "PO").Name);
    }

    [Fact]
    public void CauHinhDanhSo_TuChoiLoaiChungTuLa()
    {
        Assert.Throws<BusinessRuleException>(() => DocumentNumbering.Create(Guid.CreateVersion7(), "XYZ", "XYZ-{0000}"));
        var numbering = DocumentNumbering.Create(Guid.CreateVersion7(), "po", "PO-{CTY}-{YYYY}-{00000}");
        Assert.Equal("PO", numbering.DocType);
        numbering.ChangeFormat("po-{yymm}-{000}");
        Assert.Equal("PO-{YYMM}-{000}", numbering.Format);
    }
}
