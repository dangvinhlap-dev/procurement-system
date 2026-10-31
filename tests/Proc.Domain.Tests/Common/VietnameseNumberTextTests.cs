using Proc.Domain.Common;

namespace Proc.Domain.Tests.Common;

/// <summary>Đọc số tiền bằng chữ trên phiếu in (design.md mục 10.10).</summary>
public class VietnameseNumberTextTests
{
    [Theory]
    [InlineData("0", "Không đồng")]
    [InlineData("5", "Năm đồng")]
    [InlineData("10", "Mười đồng")]
    [InlineData("15", "Mười lăm đồng")]
    [InlineData("21", "Hai mươi mốt đồng")]
    [InlineData("25", "Hai mươi lăm đồng")]
    [InlineData("105", "Một trăm lẻ năm đồng")]
    [InlineData("110", "Một trăm mười đồng")]
    [InlineData("1000", "Một nghìn đồng")]
    [InlineData("1250000", "Một triệu hai trăm năm mươi nghìn đồng")]
    [InlineData("1005000", "Một triệu không trăm lẻ năm nghìn đồng")]
    [InlineData("2000015", "Hai triệu không trăm mười lăm đồng")]
    [InlineData("10700000", "Mười triệu bảy trăm nghìn đồng")]
    [InlineData("1000000000", "Một tỷ đồng")]
    [InlineData("10500000000", "Mười tỷ năm trăm triệu đồng")]
    [InlineData("1000000001", "Một tỷ không trăm lẻ một đồng")]
    [InlineData("1000000000000", "Một nghìn tỷ đồng")]
    [InlineData("11770000.4", "Mười một triệu bảy trăm bảy mươi nghìn đồng")]
    [InlineData("-500000", "Âm năm trăm nghìn đồng")]
    public void DocSoTienBangChu(string amount, string expected) =>
        Assert.Equal(expected, VietnameseNumberText.ToVndWords(decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture)));
}
