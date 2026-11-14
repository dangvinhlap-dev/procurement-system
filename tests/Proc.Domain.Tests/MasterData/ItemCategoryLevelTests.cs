using Proc.Domain.Common;
using Proc.Domain.MasterData;

namespace Proc.Domain.Tests.MasterData;

/// <summary>
/// Nhóm hàng tối đa 4 cấp (người dùng chốt 14/11/2026 – design.md mục 5.7, N4-1…N4-4): cấp tính theo nhóm cha, nhóm cấp 4 không có nhóm
/// con, chuyển nhánh không đẩy nhóm cuối nhánh quá cấp 4.
/// </summary>
public class ItemCategoryLevelTests
{
    private static (ItemCategory L1, ItemCategory L2, ItemCategory L3, ItemCategory L4) Chain()
    {
        var l1 = ItemCategory.Create("VT", "Vật tư");
        var l2 = ItemCategory.Create("VTD", "Vật tư điện", l1);
        var l3 = ItemCategory.Create("DCD", "Dây cáp điện", l2);
        var l4 = ItemCategory.Create("CDPVC", "Cáp đồng bọc PVC", l3);
        return (l1, l2, l3, l4);
    }

    [Fact]
    public void TaoNhom_CapTheoNhomCha_TenCap_CapBonKhongCoNhomCon()
    {
        var (l1, l2, l3, l4) = Chain();
        Assert.Equal([1, 2, 3, 4], new[] { l1.Level, l2.Level, l3.Level, l4.Level });
        Assert.Equal("Ngành hàng", ItemCategory.LevelName(1));
        Assert.Equal("Chủng loại", ItemCategory.LevelName(4));

        var error = Assert.Throws<BusinessRuleException>(() => ItemCategory.Create("CCC", "Cáp chống cháy loại 2", l4));
        Assert.Contains("CDPVC – Cáp đồng bọc PVC đã ở cấp 4 (cấp cuối)", error.Message);
    }

    [Fact]
    public void ChuyenNhom_TinhLaiCapCaNhanh_VuotCapBonThiChan()
    {
        var (l1, l2, l3, l4) = Chain();
        var other = ItemCategory.Create("HCH", "Hóa chất");
        var otherL2 = ItemCategory.Create("HCC", "Hóa chất công nghiệp", other);
        var otherL3 = ItemCategory.Create("AXIT", "Axit", otherL2);

        // VTD có con, cháu (sâu 2): vào nhóm cấp 3 thì cháu ở cấp 6 → chặn, gợi ý chỉ vào nhóm cấp 1.
        var error = Assert.Throws<BusinessRuleException>(() => l2.MoveTo(otherL3, 2));
        Assert.Contains("cấp 6", error.Message);
        Assert.Contains("vào nhóm cấp 1", error.Message);
        Assert.Equal(2, l2.Level);

        // Nhóm không có con vào nhóm cấp 4 → chặn.
        var leaf = ItemCategory.Create("LE", "Nhóm lẻ");
        Assert.Contains("không làm nhóm cha được", Assert.Throws<BusinessRuleException>(() => leaf.MoveTo(l4)).Message);

        // Chuyển DCD (có con, sâu 1) từ VTD sang HCC (cấp 2): DCD thành cấp 3, con cấp 4.
        l3.MoveTo(otherL2, 1);
        l4.FollowParent(l3);
        Assert.Equal((otherL2.Id, 3, 4), (l3.ParentId!.Value, l3.Level, l4.Level));

        // Đưa VTD lên làm nhóm gốc: cấp 1.
        l2.MoveTo(null, 0);
        Assert.Equal((null, 1), (l2.ParentId, l2.Level));
        Assert.Throws<BusinessRuleException>(() => l4.FollowParent(l1));
    }
}
