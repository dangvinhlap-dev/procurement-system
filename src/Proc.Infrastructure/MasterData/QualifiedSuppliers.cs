using Microsoft.EntityFrameworkCore;
using Proc.Domain.MasterData;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.MasterData;

/// <summary>
/// NCC cung cấp một nhóm hàng – cách đếm "tối thiểu 03 NCC" theo <b>nhóm hàng</b> (lát G1b, quyết định Q1 ngày 08/11/2026, design.md mục 18.25),
/// dùng chung cho báo cáo SUPCOV và ngoại lệ phiếu GTH (lát H2).
/// NCC cung cấp nhóm X khi: (1) có <b>ngành hàng</b> (<see cref="SupplierCategory"/>) là X hoặc một nhóm cha của X – NCC đăng ký ngành
/// "Vật tư điện" thì cung cấp cả nhóm con "Dây cáp"; ngành chỉ ở nhóm con không tính cho nhóm cha vì chỉ phủ một phần; hoặc (2) có dòng
/// NCC – sản phẩm (<see cref="SupplierItem"/>) của một mặt hàng thuộc trực tiếp nhóm X.
/// </summary>
internal static class QualifiedSuppliers
{
    /// <summary>Một NCC cung cấp nhóm hàng.</summary>
    /// <param name="SupplierId">NCC.</param>
    /// <param name="Name">Tên NCC.</param>
    /// <param name="Status">Trạng thái NCC (người gọi tự lọc Đã duyệt / Tiềm năng / Tạm ngưng).</param>
    /// <param name="IsPreferred">NCC ưu tiên của ít nhất một mặt hàng trong nhóm.</param>
    internal sealed record Candidate(Guid SupplierId, string Name, SupplierStatus Status, bool IsPreferred);

    /// <summary>
    /// NCC cung cấp từng nhóm hàng (mọi trạng thái, mỗi NCC một lần mỗi nhóm).
    /// Ví dụ: nhóm cha P (NCC A đăng ký ngành P), nhóm con X (NCC B có dòng NCC – sản phẩm của mặt hàng thuộc X, NCC C đăng ký ngành X)
    /// → X: A, B, C; P: A.
    /// </summary>
    /// <param name="db">Ngữ cảnh CSDL – dòng NCC – sản phẩm và mặt hàng đi qua filter phạm vi nhóm hàng như mọi truy vấn khác; cây nhóm hàng đọc
    /// đủ (danh mục tên nhóm hàng ai cũng xem đủ – ngoại lệ đã duyệt 07/11/2026, design.md mục 9.2) để tìm nhóm cha.</param>
    /// <param name="categoryIds">Các nhóm hàng cần đếm.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Khóa theo nhóm hàng; nhóm không có NCC nào thì danh sách rỗng.</returns>
    internal static async Task<Dictionary<Guid, List<Candidate>>> ByCategoryAsync(
        ProcDbContext db, IReadOnlyCollection<Guid> categoryIds, CancellationToken cancellationToken)
    {
        var wanted = categoryIds.Distinct().ToList();
        var result = wanted.ToDictionary(id => id, _ => new List<Candidate>());
        if (wanted.Count == 0) return result;

        var parents = await db.ItemCategories.IgnoreQueryFilters([ProcDbContext.ItemCategoryFilter]).AsNoTracking()
            .ToDictionaryAsync(c => c.Id, c => c.ParentId, cancellationToken);
        // Nhóm tự thân + các nhóm cha → những nhóm hàng cần đếm nằm dưới nó.
        var coveredBy = new Dictionary<Guid, List<Guid>>();
        foreach (var id in wanted)
        {
            var visited = new HashSet<Guid>();
            for (Guid? current = id; current is { } c && visited.Add(c); current = parents.GetValueOrDefault(c))
            {
                if (!coveredBy.TryGetValue(c, out var list)) coveredBy[c] = list = [];
                list.Add(id);
            }
        }

        var ancestorIds = coveredBy.Keys.ToList();
        var registrations = await db.SupplierCategories.AsNoTracking().Where(x => ancestorIds.Contains(x.ItemCategoryId))
            .Select(x => new { x.SupplierId, x.ItemCategoryId }).ToListAsync(cancellationToken);
        var viaItems = await db.SupplierItems.AsNoTracking()
            .Join(db.Items.AsNoTracking().Where(i => wanted.Contains(i.CategoryId)), l => l.ItemId, i => i.Id,
                (l, i) => new { l.SupplierId, i.CategoryId, l.IsPreferred })
            .ToListAsync(cancellationToken);

        var pairs = registrations.SelectMany(r => coveredBy[r.ItemCategoryId].Select(c => (Category: c, r.SupplierId, IsPreferred: false)))
            .Concat(viaItems.Select(v => (Category: v.CategoryId, v.SupplierId, v.IsPreferred)))
            .ToList();
        var supplierIds = pairs.Select(p => p.SupplierId).Distinct().ToList();
        var suppliers = await db.Suppliers.AsNoTracking().Where(s => supplierIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => new { s.Name, s.Status }, cancellationToken);

        foreach (var group in pairs.Where(p => suppliers.ContainsKey(p.SupplierId)).GroupBy(p => (p.Category, p.SupplierId)))
        {
            var supplier = suppliers[group.Key.SupplierId];
            result[group.Key.Category].Add(new Candidate(group.Key.SupplierId, supplier.Name, supplier.Status, group.Any(p => p.IsPreferred)));
        }

        return result;
    }
}
