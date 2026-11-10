using Proc.Application.Common;
using Proc.Domain.Organization;

namespace Proc.Domain.Tests.Organization;

/// <summary>
/// Danh sách quyền của phân hệ kho và cấu hình tắt phân hệ (lát K1 – design.md mục 19.4).
/// </summary>
public class InventoryModuleTests
{
    [Fact]
    public void QuyenPhanHeKho_GomMoiQuyenKho_BaoCaoTon_XuatKeToan_KhongGomQuyenMua()
    {
        Assert.All(Permissions.All.Where(p => p.Module == "Inventory"), p => Assert.Contains(p.Code, Permissions.InventoryModule));
        Assert.Contains(Permissions.Report.StockView, Permissions.InventoryModule);
        Assert.Contains(Permissions.Report.StockViewAllCompanies, Permissions.InventoryModule);
        Assert.Contains(Permissions.Report.AccountingExport, Permissions.InventoryModule);
        Assert.DoesNotContain(Permissions.Report.PurchaseView, Permissions.InventoryModule);
        Assert.DoesNotContain(Permissions.Purchase.PoView, Permissions.InventoryModule);
        Assert.DoesNotContain(Permissions.Organization.WarehouseView, Permissions.InventoryModule);
    }

    [Fact]
    public void TatKho_BoQuyenKho_BatKho_GiuNguyen()
    {
        string[] codes = [Permissions.Purchase.PoView, Permissions.Inventory.StockView, Permissions.Report.StockView];

        Assert.Equal([Permissions.Purchase.PoView], new ModuleOptions().Filter(codes));
        Assert.True(new ModuleOptions().IsDisabled(Permissions.Inventory.DocumentPost));
        Assert.Equal(codes, new ModuleOptions { InventoryEnabled = true }.Filter(codes));
        Assert.False(new ModuleOptions { InventoryEnabled = true }.IsDisabled(Permissions.Inventory.DocumentPost));
    }
}
