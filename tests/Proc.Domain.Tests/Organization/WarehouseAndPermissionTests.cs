using System.Text.RegularExpressions;
using Proc.Domain.Common;
using Proc.Domain.MasterData;
using Proc.Domain.Organization;

namespace Proc.Domain.Tests.Organization;

/// <summary>
/// Quy tắc kho (design.md mục 4, 5.1), nhóm hàng (mục 5.2) và danh mục quyền (mục 9.1).
/// </summary>
public class WarehouseAndPermissionTests
{
    private static readonly Guid CompanyA = Guid.CreateVersion7();

    [Fact]
    public void KhoTong_KhongCoKhoCha()
    {
        var main = Warehouse.CreateMain(CompanyA, " kt ", "Kho tổng");

        Assert.Equal(WarehouseType.Main, main.Type);
        Assert.Equal("KT", main.Code);
        Assert.Null(main.ParentWarehouseId);
        Assert.False(main.AllowNegativeStock);
    }

    [Fact]
    public void KhoPhu_LuonCungCongTyVoiKhoCha()
    {
        // Kho Sub không được thuộc công ty khác với kho cha – design.md mục 4.
        var main = Warehouse.CreateMain(CompanyA, "KT", "Kho tổng");
        var sub = Warehouse.CreateSub(main, "KP1", "Kho phụ 1");

        Assert.Equal(WarehouseType.Sub, sub.Type);
        Assert.Equal(CompanyA, sub.CompanyId);
        Assert.Equal(main.Id, sub.ParentWarehouseId);
    }

    [Theory]
    [InlineData("K")]
    [InlineData("KHO 1")]
    [InlineData("")]
    public void Kho_TuChoiMaKhongHopLe(string code)
    {
        Assert.Throws<BusinessRuleException>(() => Warehouse.CreateMain(CompanyA, code, "Kho"));
    }

    [Fact]
    public void Kho_PhaiThuocCongTy()
    {
        Assert.Throws<BusinessRuleException>(() => Warehouse.CreateMain(Guid.Empty, "KT", "Kho"));
    }

    [Fact]
    public void NhomHang_GhiNhoNhomCha()
    {
        var root = ItemCategory.Create("vt", "Vật tư");
        var child = ItemCategory.Create("VTD", "Vật tư điện", root);

        Assert.Equal("VT", root.Code);
        Assert.Null(root.ParentId);
        Assert.Equal(root.Id, child.ParentId);
    }

    [Fact]
    public void DanhMucQuyen_MaDuyNhatVaDungDinhDang()
    {
        var codes = Permissions.All.Select(p => p.Code).ToList();

        Assert.Equal(codes.Count, codes.Distinct().Count());
        // Module.Đối tượng.Hành động – design.md mục 9.1.
        Assert.All(codes, c => Assert.Matches(new Regex("^[A-Z][A-Za-z]+\\.[A-Z][A-Za-z]+\\.[A-Z][A-Za-z]+$"), c));
        Assert.All(Permissions.All, p => Assert.False(string.IsNullOrWhiteSpace(p.Name)));
        Assert.Equal("Purchase", new PermissionDefinition(Permissions.Purchase.PoCreate, "x").Module);
    }

    [Fact]
    public void DanhMucQuyen_CoDuCacQuyenMauTrongThietKe()
    {
        // Các ví dụ quyền nêu ở design.md mục 9.1.
        string[] required =
        [
            "Purchase.PO.Create", "Purchase.PO.Approve", "Inventory.Period.Close",
            "Inventory.Period.Reopen", "Report.Stock.View", "Report.Price.ViewAllCompanies",
        ];
        Assert.All(required, r => Assert.Contains(Permissions.All, p => p.Code == r));
    }

    [Fact]
    public void DanhMucQuyen_MoiModuleDeuCoTenTiengViet()
    {
        var modules = Permissions.Modules.Select(m => m.Module).ToList();
        Assert.All(Permissions.All, p => Assert.Contains(p.Module, modules));
    }
}
