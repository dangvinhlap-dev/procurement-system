using Microsoft.EntityFrameworkCore;
using Proc.Application.Common;
using Proc.Application.MasterData;
using Proc.Domain.Common;
using Proc.Domain.MasterData;
using Proc.Infrastructure.Organization;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.MasterData;

/// <summary>
/// Quản lý đơn vị tính (design.md mục 5.2, 5.7). Bảng Uom không có filter (danh mục dùng chung), nhưng số mặt hàng
/// đếm qua filter nhóm hàng – nên thêm/sửa cần phạm vi mọi nhóm hàng để kiểm tra "còn hàng đang dùng" là đủ.
/// </summary>
public sealed class UomAdministration : IUomAdministration
{
    private readonly ProcDbContext _db;
    private readonly ICurrentUserScope _scope;

    /// <summary>
    /// Tạo dịch vụ.
    /// </summary>
    /// <param name="db">DbContext.</param>
    /// <param name="scope">Phạm vi của người thao tác.</param>
    public UomAdministration(ProcDbContext db, ICurrentUserScope scope)
    {
        _db = db;
        _scope = scope;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<UomDto>> ListAsync(CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        var counts = await _db.Items.AsNoTracking()
            .GroupBy(i => i.BaseUomId)
            .Select(g => new { UomId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.UomId, x => x.Count, cancellationToken);
        var uoms = await _db.Uoms.AsNoTracking().OrderBy(u => u.Code).ToListAsync(cancellationToken);
        return uoms.Select(u => new UomDto(u.Id, u.Code, u.Name, u.IsActive, counts.GetValueOrDefault(u.Id))).ToList();
    }

    /// <inheritdoc />
    public async Task<UomDto> CreateAsync(CreateUomRequest request, CancellationToken cancellationToken)
    {
        await RequireManageAsync(cancellationToken);
        var uom = Uom.Create(request.Code, request.Name);
        if (await _db.Uoms.AnyAsync(u => u.Code == uom.Code, cancellationToken))
            throw new BusinessRuleException($"Mã đơn vị tính {uom.Code} đã có.");
        _db.Uoms.Add(uom);
        await _db.SaveChangesAsync(cancellationToken);
        return new UomDto(uom.Id, uom.Code, uom.Name, uom.IsActive, 0);
    }

    /// <inheritdoc />
    public async Task<UomDto> UpdateAsync(Guid id, UpdateUomRequest request, CancellationToken cancellationToken)
    {
        await RequireManageAsync(cancellationToken);
        var uom = await _db.Uoms.SingleOrDefaultAsync(u => u.Id == id, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy đơn vị tính.");
        uom.Rename(request.Name);
        if (request.IsActive)
        {
            uom.Activate();
        }
        else if (uom.IsActive)
        {
            // ĐVT cơ sở của hàng đang dùng mà ngừng thì hàng đó không lập chứng từ được nữa – ngừng hàng hoặc đổi ĐVT trước.
            var inUse = await _db.Items
                .Where(i => i.IsActive && (i.BaseUomId == id || i.Conversions.Any(c => c.UomId == id)))
                .OrderBy(i => i.Code)
                .Select(i => i.Code)
                .Take(5)
                .ToListAsync(cancellationToken);
            if (inUse.Count > 0)
                throw new BusinessRuleException($"Đơn vị tính {uom.Code} đang dùng cho hàng {string.Join(", ", inUse)}… Đổi ĐVT của các hàng đó trước.");
            uom.Deactivate();
        }

        await _db.SaveChangesAsync(cancellationToken);
        var count = await _db.Items.CountAsync(i => i.BaseUomId == id, cancellationToken);
        return new UomDto(uom.Id, uom.Code, uom.Name, uom.IsActive, count);
    }

    private async Task RequireManageAsync(CancellationToken cancellationToken)
    {
        await HeadOffice.RequireAsync(_db, _scope, "đơn vị tính", cancellationToken);
        if (!_scope.AllItemCategories)
            throw new ForbiddenAccessException("Bạn chỉ được làm việc với một số nhóm hàng nên không sửa được đơn vị tính dùng chung.");
    }
}

/// <summary>
/// Quản lý nhóm hàng (design.md mục 5.2, 5.7). Mọi truy vấn qua filter phạm vi nhóm hàng; được một nhóm là được mọi nhóm con,
/// nên nhóm con mới thêm dưới nhóm được phép vẫn thấy ngay.
/// </summary>
public sealed class ItemCategoryAdministration : IItemCategoryAdministration
{
    private readonly ProcDbContext _db;
    private readonly ICurrentUserScope _scope;

    /// <summary>
    /// Tạo dịch vụ.
    /// </summary>
    /// <param name="db">DbContext.</param>
    /// <param name="scope">Phạm vi của người thao tác.</param>
    public ItemCategoryAdministration(ProcDbContext db, ICurrentUserScope scope)
    {
        _db = db;
        _scope = scope;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ItemCategoryDetailDto>> ListAsync(CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        var categories = await _db.ItemCategories.AsNoTracking().ToListAsync(cancellationToken);
        var counts = await _db.Items.AsNoTracking()
            .GroupBy(i => i.CategoryId)
            .Select(g => new { CategoryId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.CategoryId, x => x.Count, cancellationToken);
        var byId = categories.ToDictionary(c => c.Id);

        return categories
            .Select(c => ToDto(c, byId, counts.GetValueOrDefault(c.Id)))
            .OrderBy(d => d.Path, StringComparer.Ordinal)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<ItemCategoryDetailDto> CreateAsync(CreateItemCategoryRequest request, CancellationToken cancellationToken)
    {
        await HeadOffice.RequireAsync(_db, _scope, "nhóm hàng", cancellationToken);
        // Nhóm gốc mới nằm ngoài các nhánh được phép của người bị giới hạn – thêm xong chính họ không thấy (design.md mục 5.7).
        if (request.ParentId is null && !_scope.AllItemCategories)
            throw new ForbiddenAccessException("Bạn chỉ được làm việc với một số nhóm hàng nên chỉ thêm được nhóm con trong các nhóm đó.");

        var parent = request.ParentId is { } parentId ? await FindParentAsync(parentId, cancellationToken) : null;
        var category = ItemCategory.Create(request.Code, request.Name, parent);
        category.Update(request.Name, request.InventoryAccount);
        if (await _db.ItemCategories.AnyAsync(c => c.Code == category.Code, cancellationToken))
            throw new BusinessRuleException($"Mã nhóm hàng {category.Code} đã có.");

        _db.ItemCategories.Add(category);
        await _db.SaveChangesAsync(cancellationToken);

        // Không đọc lại qua filter: danh sách nhóm được phép (gồm nhóm con) nạp từ đầu request, lúc nhóm mới chưa có,
        // nên người bị giới hạn chưa thấy nhóm vừa thêm cho tới request sau. Dựng kết quả từ nhóm cha thay vì đọc lại.
        var parentDto = parent is null ? null : (await ListAsync(cancellationToken)).Single(c => c.Id == parent.Id);
        return new ItemCategoryDetailDto(
            category.Id, category.Code, category.Name, category.ParentId, parent?.Code,
            parentDto is null ? category.Code : $"{parentDto.Path} / {category.Code}", (parentDto?.Level ?? 0) + 1,
            category.InventoryAccount, category.IsActive, 0);
    }

    /// <inheritdoc />
    public async Task<ItemCategoryDetailDto> UpdateAsync(Guid id, UpdateItemCategoryRequest request, CancellationToken cancellationToken)
    {
        await HeadOffice.RequireAsync(_db, _scope, "nhóm hàng", cancellationToken);
        var category = await _db.ItemCategories.SingleOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy nhóm hàng.");
        category.Update(request.Name, request.InventoryAccount);

        if (request.ParentId != category.ParentId)
        {
            // Người bị giới hạn chỉ thấy một phần cây: không kiểm tra được vòng lặp, và chuyển nhóm ra khỏi nhánh được phép
            // là làm mất quyền xem của chính mình – chỉ người được mọi nhóm hàng mới đổi nhóm cha.
            if (!_scope.AllItemCategories)
                throw new ForbiddenAccessException("Bạn chỉ được làm việc với một số nhóm hàng nên không đổi được nhóm cha.");
            var parent = request.ParentId is { } parentId ? await FindParentAsync(parentId, cancellationToken) : null;
            if (parent is not null) await EnsureNoCycleAsync(category, parent, cancellationToken);
            category.MoveTo(parent);
        }

        if (request.IsActive) category.Activate();
        else if (category.IsActive) await DeactivateAsync(category, cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    private async Task DeactivateAsync(ItemCategory category, CancellationToken cancellationToken)
    {
        var children = await _db.ItemCategories.Where(c => c.ParentId == category.Id && c.IsActive).Select(c => c.Code).ToListAsync(cancellationToken);
        if (children.Count > 0)
            throw new BusinessRuleException($"Nhóm {category.Code} còn nhóm con đang dùng: {string.Join(", ", children)}. Ngừng dùng nhóm con trước.");
        var items = await _db.Items.CountAsync(i => i.CategoryId == category.Id && i.IsActive, cancellationToken);
        if (items > 0)
            throw new BusinessRuleException($"Nhóm {category.Code} còn {items} mặt hàng đang dùng. Chuyển hoặc ngừng dùng các mặt hàng đó trước.");
        category.Deactivate();
    }

    /// <summary>Đi ngược từ nhóm cha mới lên gốc; gặp lại nhóm đang sửa là cây thành vòng.</summary>
    private async Task EnsureNoCycleAsync(ItemCategory category, ItemCategory newParent, CancellationToken cancellationToken)
    {
        var parents = await _db.ItemCategories.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.ParentId, cancellationToken);
        var visited = new HashSet<Guid>();
        for (Guid? current = newParent.Id; current is { } id && visited.Add(id); current = parents.GetValueOrDefault(id))
        {
            if (id == category.Id)
                throw new BusinessRuleException($"Không đặt được nhóm {newParent.Code} làm nhóm cha của {category.Code}: cây nhóm hàng sẽ thành vòng.");
        }
    }

    private async Task<ItemCategory> FindParentAsync(Guid parentId, CancellationToken cancellationToken)
    {
        var parent = await _db.ItemCategories.SingleOrDefaultAsync(c => c.Id == parentId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy nhóm cha.");
        if (!parent.IsActive) throw new BusinessRuleException($"Nhóm cha {parent.Code} đã ngừng dùng.");
        return parent;
    }

    private async Task<ItemCategoryDetailDto> GetAsync(Guid id, CancellationToken cancellationToken) =>
        (await ListAsync(cancellationToken)).Single(c => c.Id == id);

    /// <summary>Đường dẫn và cấp tính trên phần cây người xem thấy được (nhóm cha ngoài phạm vi thì dừng ở đó).</summary>
    private static ItemCategoryDetailDto ToDto(ItemCategory c, Dictionary<Guid, ItemCategory> byId, int itemCount)
    {
        var codes = new List<string>();
        var visited = new HashSet<Guid>();
        for (var current = c; current is not null && visited.Add(current.Id);
             current = current.ParentId is { } p ? byId.GetValueOrDefault(p) : null)
        {
            codes.Insert(0, current.Code);
        }

        var parentCode = c.ParentId is { } parentId ? byId.GetValueOrDefault(parentId)?.Code : null;
        return new ItemCategoryDetailDto(
            c.Id, c.Code, c.Name, c.ParentId, parentCode, string.Join(" / ", codes), codes.Count, c.InventoryAccount, c.IsActive, itemCount);
    }
}
