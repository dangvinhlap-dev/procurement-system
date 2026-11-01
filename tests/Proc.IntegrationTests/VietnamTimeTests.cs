using Proc.Application.Common;

namespace Proc.IntegrationTests;

/// <summary>
/// Múi giờ Việt Nam tìm được trên cả Linux container và Windows Server (CLAUDE.md mục 5, docs/deploy-windows.md).
/// Không cần CSDL – đặt ở đây vì dự án test này đã tham chiếu lớp Application.
/// </summary>
public class VietnamTimeTests
{
    [Fact]
    public void MuiGioVietNam_LaUtcCong7_KhongDoiGioMuaHe()
    {
        Assert.Equal(TimeSpan.FromHours(7), VietnamTime.Zone.BaseUtcOffset);
        Assert.False(VietnamTime.Zone.SupportsDaylightSavingTime);
    }

    [Fact]
    public void TenDauKhongCo_ThuTenTiepTheo()
    {
        // Mô phỏng Windows cũ không nhận tên IANA: tên đầu không tồn tại thì lấy tên sau.
        var zone = VietnamTime.FindZone(["Khong/Ton_Tai", .. VietnamTime.ZoneIds]);
        Assert.Equal(TimeSpan.FromHours(7), zone.BaseUtcOffset);
    }

    [Fact]
    public void KhongTenNaoCo_BaoLoiRoRang()
    {
        var ex = Assert.Throws<TimeZoneNotFoundException>(() => VietnamTime.FindZone(["Khong/Ton_Tai"]));
        Assert.Contains("Khong/Ton_Tai", ex.Message);
    }
}
