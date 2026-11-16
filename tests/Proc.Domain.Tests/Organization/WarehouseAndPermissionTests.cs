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
    public void Kho_LaNoiNhanHang_DanhSachPhang()
    {
        // Từ 0.38.0 kho chỉ là nơi nhận hàng (design.md mục 19.4 – bỏ kho tổng / kho phụ, cờ tồn âm): không kho cha.
        var kho = Warehouse.Create(CompanyA, " kt ", "Kho nhận hàng");

        Assert.Equal("KT", kho.Code);
        Assert.Equal(CompanyA, kho.CompanyId);
        Assert.Null(kho.ParentWarehouseId);
        Assert.True(kho.IsActive);
    }

    [Theory]
    [InlineData("K")]
    [InlineData("KHO 1")]
    [InlineData("")]
    public void Kho_TuChoiMaKhongHopLe(string code)
    {
        Assert.Throws<BusinessRuleException>(() => Warehouse.Create(CompanyA, code, "Kho"));
    }

    [Fact]
    public void Kho_PhaiThuocCongTy()
    {
        Assert.Throws<BusinessRuleException>(() => Warehouse.Create(Guid.Empty, "KT", "Kho"));
    }

    [Fact]
    public void Kho_SuaThongTin()
    {
        var kho = Warehouse.Create(CompanyA, "KT", "Kho nhận hàng");

        kho.Update(" Kho mới ", " Hà Nội ", " Trần Bình ");

        Assert.Equal("Kho mới", kho.Name);
        Assert.Equal("Hà Nội", kho.Address);
        Assert.Equal("Trần Bình", kho.Keeper);
        Assert.Throws<BusinessRuleException>(() => kho.Update(" ", null, null));
    }

    [Fact]
    public void Kho_NaoCungNgungDungDuoc()
    {
        // Không còn kho tổng bắt buộc (bỏ giao dịch liên công ty ICS/ICR từ K3) – kho nào cũng ngừng / dùng lại được.
        var kho = Warehouse.Create(CompanyA, "KT", "Kho nhận hàng");
        kho.Deactivate();
        Assert.False(kho.IsActive);
        kho.Activate();
        Assert.True(kho.IsActive);
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
            "Purchase.PO.Create", "Purchase.PO.Approve", "Purchase.Receipt.Manage", "Report.Price.ViewAllCompanies",
        ];
        Assert.All(required, r => Assert.Contains(Permissions.All, p => p.Code == r));
        // Quyền kho gỡ ở lát K3 (design.md mục 19.4).
        Assert.DoesNotContain(Permissions.All, p => p.Code.StartsWith("Inventory.", StringComparison.Ordinal) || p.Code == "Report.Stock.View");
    }

    [Fact]
    public void DanhMucQuyen_MoiModuleDeuCoTenTiengViet()
    {
        var modules = Permissions.Modules.Select(m => m.Module).ToList();
        Assert.All(Permissions.All, p => Assert.Contains(p.Module, modules));
    }
}
