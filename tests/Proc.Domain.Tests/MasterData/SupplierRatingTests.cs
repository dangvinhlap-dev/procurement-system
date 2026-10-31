using Proc.Domain.Common;
using Proc.Domain.MasterData;

namespace Proc.Domain.Tests.MasterData;

/// <summary>Xếp loại NCC theo hạng cấu hình (design.md mục 11.3).</summary>
public class SupplierRatingTests
{
    private static readonly SupplierRatingRule[] Rules =
    [
        SupplierRatingRule.Create("C", "Đạt", 3, 70, 5),
        SupplierRatingRule.Create("a", "Tốt", 1, 95, 1),
        SupplierRatingRule.Create("B", "Khá", 2, 85, 3),
    ];

    [Theory]
    [InlineData(100.0, 0.0, "A")]
    [InlineData(95.0, 1.0, "A")]
    [InlineData(90.0, 2.0, "B")]
    [InlineData(99.0, 4.0, "C")]
    [InlineData(60.0, 0.0, SupplierRating.Failed)]
    [InlineData(null, 0.5, "A")]
    [InlineData(80.0, null, "C")]
    public void XepLoai_XetTuHangTotNhat_BoQuaChiSoKhongCoDuLieu(double? onTime, double? returned, string expected) =>
        Assert.Equal(expected, SupplierRating.Evaluate(Rules, (decimal?)onTime, (decimal?)returned));

    [Fact]
    public void XepLoai_ChuaCauHinhHoacKhongCoDuLieu_KhongXep()
    {
        Assert.Null(SupplierRating.Evaluate([], 100, 0));
        Assert.Null(SupplierRating.Evaluate(Rules, null, null));
    }

    [Fact]
    public void BoHang_TrungMaTrungThuTu_HangSauKhatKheHon_BiChan()
    {
        SupplierRating.ValidateSet(Rules);
        Assert.Contains("bị trùng", Assert.Throws<BusinessRuleException>(() =>
            SupplierRating.ValidateSet([.. Rules, SupplierRatingRule.Create("A", "Trùng", 9, 0, 100)])).Message);
        Assert.Contains("Thứ tự 2", Assert.Throws<BusinessRuleException>(() =>
            SupplierRating.ValidateSet([.. Rules, SupplierRatingRule.Create("D", "Trùng thứ tự", 2, 0, 100)])).Message);
        Assert.Contains("không được khắt khe hơn", Assert.Throws<BusinessRuleException>(() =>
            SupplierRating.ValidateSet([Rules[1], SupplierRatingRule.Create("B", "Khá", 2, 98, 3)])).Message);
        Assert.Throws<BusinessRuleException>(() => SupplierRatingRule.Create("A", "Tốt", 1, 101, 0));
        Assert.Throws<BusinessRuleException>(() => SupplierRatingRule.Create("A", " ", 1, 90, 0));
    }
}
