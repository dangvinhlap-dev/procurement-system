using Proc.Domain.MasterData;

namespace Proc.Domain.Tests;

/// <summary>
/// Nhóm hàng dựng sẵn cho test: mặt hàng chỉ gắn vào nhóm cấp 4 – Chủng loại (design.md mục 5.7, N4-5) nên test tạo mặt hàng dùng nhánh đủ
/// 4 cấp.
/// </summary>
public static class TestCategories
{
    /// <summary>
    /// Nhánh 4 cấp, trả về nhóm cấp 4 mang mã và tên cho trước (ba nhóm trên đặt mã/tên theo nó).
    /// </summary>
    /// <param name="code">Mã nhóm cấp 4 (2–16 chữ/số để còn chỗ cho hậu tố của nhóm trên).</param>
    /// <param name="name">Tên nhóm cấp 4.</param>
    /// <returns>Nhóm cấp 4.</returns>
    public static ItemCategory Leaf(string code, string name)
    {
        var l1 = ItemCategory.Create($"{code}N1", $"{name} – ngành");
        var l2 = ItemCategory.Create($"{code}N2", $"{name} – nhóm", l1);
        var l3 = ItemCategory.Create($"{code}N3", $"{name} – loại", l2);
        return ItemCategory.Create(code, name, l3);
    }
}
