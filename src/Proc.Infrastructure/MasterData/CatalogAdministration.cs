using Microsoft.EntityFrameworkCore;
using Proc.Application.Common;
using Proc.Application.MasterData;
using Proc.Domain.Common;
using Proc.Domain.MasterData;
using Proc.Domain.Organization;
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
        await SharedCatalog.RequireEditAsync(_db, _scope, "đơn vị tính", Permissions.MasterData.ItemManage, cancellationToken);
        RequireAllCategories();
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

    /// <summary>Chỉ tổng công ty và người có quyền mọi nhóm hàng mới được sửa danh mục dùng chung.</summary>
    private async Task RequireManageAsync(CancellationToken cancellationToken)
    {
        await HeadOffice.RequireAsync(_db, _scope, "đơn vị tính", cancellationToken);
        RequireAllCategories();
    }

    /// <summary>Đơn vị tính dùng cho mọi nhóm hàng: người chỉ được một số nhóm hàng không sửa được.</summary>
    private void RequireAllCategories()
    {
        if (!_scope.AllItemCategories)
            throw new ForbiddenAccessException("Bạn chỉ được làm việc với một số nhóm hàng nên không sửa được đơn vị tính dùng chung.");
    }
}

/// <summary>
/// Quản lý nhóm hàng (design.md mục 5.2, 5.7). <b>Xem</b> danh mục: đủ cả cây cho mọi người (người dùng chốt 07/11/2026 – để chọn ngành
/// hàng NCC và tránh tạo trùng nhóm; design.md mục 9.2), mỗi nhóm kèm cờ trong/ngoài phạm vi. <b>Ghi</b> (thêm, sửa, làm nhóm cha) vẫn qua
/// filter phạm vi nhóm hàng; được một nhóm là được mọi nhóm con.
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
        // Ngoại lệ có chủ đích với filter nhóm hàng (CLAUDE.md mục 4): chỉ đọc danh mục tên nhóm; mặt hàng (số đếm bên dưới), tồn kho,
        // chứng từ vẫn lọc theo phạm vi.
        var categories = await _db.ItemCategories.IgnoreQueryFilters([ProcDbContext.ItemCategoryFilter]).AsNoTracking().ToListAsync(cancellationToken);
        var counts = await _db.Items.AsNoTracking()
            .GroupBy(i => i.CategoryId)
            .Select(g => new { CategoryId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.CategoryId, x => x.Count, cancellationToken);
        var byId = categories.ToDictionary(c => c.Id);
        var allowed = _scope.AllItemCategories ? null : _scope.ItemCategoryIds.ToHashSet();
        var childCounts = categories.Where(c => c.ParentId is not null).GroupBy(c => c.ParentId!.Value).ToDictionary(g => g.Key, g => g.Count());

        return categories
            .Select(c => ToDto(c, byId, counts.GetValueOrDefault(c.Id), allowed, childCounts.GetValueOrDefault(c.Id)))
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
        await EnsureUniqueNameAsync(parent?.Id, request.Name, null, cancellationToken);
        // Mã nhóm do người dùng tự gõ (người dùng chốt 14/11/2026 – design.md mục 5.7, N4-7), không còn cấp theo dải đánh số.
        if (string.IsNullOrWhiteSpace(request.Code)) throw new BusinessRuleException("Hãy nhập mã nhóm hàng.");
        var category = ItemCategory.Create(request.Code, request.Name, parent);
        category.Update(request.Name, request.InventoryAccount);
        // Xét cả nhóm ngoài phạm vi để báo trùng rõ ràng thay vì lỗi chỉ mục duy nhất của CSDL.
        var duplicate = await _db.ItemCategories.IgnoreQueryFilters([ProcDbContext.ItemCategoryFilter]).AsNoTracking()
            .Where(c => c.Code == category.Code).Select(c => c.Name).FirstOrDefaultAsync(cancellationToken);
        if (duplicate is not null) throw new BusinessRuleException($"Mã nhóm hàng {category.Code} đã có ({duplicate}) – chọn mã khác.");

        _db.ItemCategories.Add(category);
        await _db.SaveChangesAsync(cancellationToken);

        // Danh mục đọc đủ cây nên đọc lại được ngay; cờ phạm vi tính theo nhóm cha (nhóm con mới dưới nhóm được phép là trong phạm vi).
        return await GetAsync(category.Id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ItemCategoryDetailDto> UpdateAsync(Guid id, UpdateItemCategoryRequest request, CancellationToken cancellationToken)
    {
        await SharedCatalog.RequireEditAsync(_db, _scope, "nhóm hàng", Permissions.MasterData.ItemCategoryManage, cancellationToken);
        var category = await _db.ItemCategories.SingleOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw await OutOfScopeOrNotFoundAsync(id, "Không tìm thấy nhóm hàng.", cancellationToken);
        // Đổi tên hoặc nhóm cha mới kiểm tra trùng – nhóm trùng có từ trước vẫn sửa được thông tin khác.
        if (request.ParentId != category.ParentId || SearchText.Normalize(request.Name) != SearchText.Normalize(category.Name))
            await EnsureUniqueNameAsync(request.ParentId, request.Name, category.Id, cancellationToken);
        category.Update(request.Name, request.InventoryAccount);

        if (request.ParentId != category.ParentId)
        {
            // Người bị giới hạn chỉ thấy một phần cây: không kiểm tra được vòng lặp, và chuyển nhóm ra khỏi nhánh được phép
            // là làm mất quyền xem của chính mình – chỉ người được mọi nhóm hàng mới đổi nhóm cha.
            if (!_scope.AllItemCategories)
                throw new ForbiddenAccessException("Bạn chỉ được làm việc với một số nhóm hàng nên không đổi được nhóm cha.");
            var parent = request.ParentId is { } parentId ? await FindParentAsync(parentId, cancellationToken) : null;
            if (parent is not null) await EnsureNoCycleAsync(category, parent, cancellationToken);
            await MoveBranchAsync(category, parent, cancellationToken);
        }

        if (request.IsActive) category.Activate();
        else if (category.IsActive) await DeactivateAsync(category, cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <summary>Ngừng dùng nhóm hàng; còn nhóm con đang dùng thì báo lỗi.</summary>
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

    /// <summary>
    /// Chuyển nhóm sang nhóm cha mới cùng cả nhánh con (N4-4): đếm độ sâu nhánh để chặn vượt 4 cấp, rồi tính lại cấp từng nhóm con, cháu từ
    /// trên xuống – lưu cùng một lần SaveChanges với nhóm được chuyển. Chỉ người được mọi nhóm hàng mới đổi nhóm cha nên filter nhóm hàng không
    /// che mất nhóm con nào.
    /// </summary>
    private async Task MoveBranchAsync(ItemCategory category, ItemCategory? parent, CancellationToken cancellationToken)
    {
        var all = await _db.ItemCategories.ToListAsync(cancellationToken);
        var children = all.Where(c => c.ParentId is not null).ToLookup(c => c.ParentId!.Value);
        // Thứ tự duyệt theo chiều rộng: cha luôn được tính cấp trước con.
        var branch = new List<(ItemCategory Node, int Depth)>();
        var queue = new Queue<(ItemCategory Node, int Depth)>(children[category.Id].Select(c => (c, 1)));
        while (queue.Count > 0)
        {
            var (node, depth) = queue.Dequeue();
            branch.Add((node, depth));
            foreach (var child in children[node.Id]) queue.Enqueue((child, depth + 1));
        }

        category.MoveTo(parent, branch.Count == 0 ? 0 : branch.Max(b => b.Depth));
        var byId = all.ToDictionary(c => c.Id);
        foreach (var (node, _) in branch) node.FollowParent(byId[node.ParentId!.Value]);
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

    /// <summary>Tìm nhóm cha theo Id trong phạm vi người dùng; ngoài phạm vi hoặc không có thì báo lỗi.</summary>
    private async Task<ItemCategory> FindParentAsync(Guid parentId, CancellationToken cancellationToken)
    {
        var parent = await _db.ItemCategories.SingleOrDefaultAsync(c => c.Id == parentId, cancellationToken)
            ?? throw await OutOfScopeOrNotFoundAsync(parentId, "Không tìm thấy nhóm cha.", cancellationToken);
        if (!parent.IsActive) throw new BusinessRuleException($"Nhóm cha {parent.Code} đã ngừng dùng.");
        return parent;
    }

    /// <summary>
    /// Không trùng tên (so không dấu, không phân biệt hoa thường) trong cùng nhóm cha – xét cả nhóm ngoài phạm vi người dùng và nhóm ngừng
    /// dùng (yêu cầu người dùng 07/11/2026: thiếu nhóm thì tạo, nhưng không tạo trùng).
    /// </summary>
    /// <exception cref="BusinessRuleException">Khi đã có nhóm cùng tên.</exception>
    private async Task EnsureUniqueNameAsync(Guid? parentId, string name, Guid? excludeId, CancellationToken cancellationToken)
    {
        var normalized = SearchText.Normalize(name);
        var siblings = await _db.ItemCategories.IgnoreQueryFilters([ProcDbContext.ItemCategoryFilter]).AsNoTracking()
            .Where(c => c.ParentId == parentId && c.Id != excludeId)
            .Select(c => new { c.Code, c.Name, c.IsActive })
            .ToListAsync(cancellationToken);
        var same = siblings.FirstOrDefault(c => SearchText.Normalize(c.Name) == normalized);
        if (same is not null)
            throw new BusinessRuleException(
                $"Đã có nhóm hàng {same.Code} – {same.Name}{(same.IsActive ? "" : " (ngừng dùng – dùng lại nhóm này)")} trong cùng nhóm cha.");
    }

    /// <summary>Nhóm có nhưng ngoài phạm vi người dùng thì báo rõ không có quyền (danh mục vẫn hiện nhóm đó); không có thì báo không tìm thấy.</summary>
    private async Task<Exception> OutOfScopeOrNotFoundAsync(Guid id, string notFound, CancellationToken cancellationToken)
    {
        var code = await _db.ItemCategories.IgnoreQueryFilters([ProcDbContext.ItemCategoryFilter]).AsNoTracking()
            .Where(c => c.Id == id).Select(c => c.Code).SingleOrDefaultAsync(cancellationToken);
        return code is null
            ? new NotFoundException(notFound)
            : new ForbiddenAccessException($"Nhóm hàng {code} ngoài phạm vi nhóm hàng của bạn – chỉ xem được.");
    }

    /// <summary>Lấy chi tiết một nhóm hàng (tính cả đường dẫn và cấp trên cây).</summary>
    private async Task<ItemCategoryDetailDto> GetAsync(Guid id, CancellationToken cancellationToken) =>
        (await ListAsync(cancellationToken)).Single(c => c.Id == id);

    /// <summary>
    /// Đường dẫn và cấp trên cả cây; trong phạm vi khi chính nhóm hoặc một nhóm cha thuộc nhóm được phép (được nhóm cha là được nhóm con –
    /// kể cả nhóm con vừa thêm trong request này, chưa có trong danh sách phạm vi nạp từ đầu request).
    /// </summary>
    /// <param name="c">Nhóm.</param>
    /// <param name="byId">Cả cây theo Id.</param>
    /// <param name="itemCount">Số mặt hàng trong nhóm người xem thấy.</param>
    /// <param name="allowed">Nhóm được phép; null = mọi nhóm.</param>
    /// <param name="childCount">Số nhóm con trực tiếp.</param>
    private static ItemCategoryDetailDto ToDto(
        ItemCategory c, Dictionary<Guid, ItemCategory> byId, int itemCount, HashSet<Guid>? allowed, int childCount)
    {
        var codes = new List<string>();
        var visited = new HashSet<Guid>();
        var inScope = allowed is null;
        for (var current = c; current is not null && visited.Add(current.Id);
             current = current.ParentId is { } p ? byId.GetValueOrDefault(p) : null)
        {
            codes.Insert(0, current.Code);
            inScope = inScope || allowed!.Contains(current.Id);
        }

        var parentCode = c.ParentId is { } parentId ? byId.GetValueOrDefault(parentId)?.Code : null;
        return new ItemCategoryDetailDto(
            c.Id, c.Code, c.Name, c.ParentId, parentCode, string.Join(" / ", codes), c.Level, c.InventoryAccount, c.IsActive, itemCount,
            inScope, ItemCategory.LevelName(c.Level), childCount);
    }
}
