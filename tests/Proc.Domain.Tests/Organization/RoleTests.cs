using Proc.Domain.Common;
using Proc.Domain.Organization;

namespace Proc.Domain.Tests.Organization;

/// <summary>
/// Quy tắc vai trò (design.md mục 9.1): ADMIN là vai trò hệ thống, không ngừng dùng được; quyền phải có trong danh mục.
/// </summary>
public class RoleTests
{
    [Fact]
    public void SuaVaiTro_DoiTenVaNgungDung()
    {
        var role = Role.Create("thukho", "Thủ kho");

        role.Update(" Thủ kho chính ", isActive: false);

        Assert.Equal("THUKHO", role.Code);
        Assert.Equal("Thủ kho chính", role.Name);
        Assert.False(role.IsActive);
        Assert.False(role.IsSystem);
    }

    [Fact]
    public void VaiTroAdmin_KhongNgungDungDuoc()
    {
        var admin = Role.Create(Role.AdminCode, "Quản trị hệ thống");

        Assert.True(admin.IsSystem);
        Assert.Throws<BusinessRuleException>(() => admin.Update("Quản trị", isActive: false));
    }

    [Fact]
    public void SuaVaiTro_TenRong_BaoLoi()
    {
        var role = Role.Create("NVMUA", "Nhân viên mua hàng");
        Assert.Throws<BusinessRuleException>(() => role.Update("  ", isActive: true));
    }

    [Fact]
    public void KiemTraMaQuyen_TuChoiMaKhongCoTrongDanhMuc()
    {
        Assert.Empty(Permissions.FindUnknown([Permissions.Purchase.PoView, Permissions.Inventory.StockView]));
        Assert.Equal(["Purchase.PO.Delete"], Permissions.FindUnknown([Permissions.Purchase.PoView, "Purchase.PO.Delete"]));
    }
}
