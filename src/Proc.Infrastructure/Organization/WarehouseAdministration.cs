using Microsoft.EntityFrameworkCore;
using Proc.Application.Common;
using Proc.Application.Organization;
using Proc.Domain.Common;
using Proc.Domain.Organization;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.Organization;

/// <summary>
/// Quản lý kho (design.md mục 4, 5.1, 5.6). Mọi truy vấn đi qua global query filter của Warehouse (công ty đang làm việc
/// và kho được phép), nên kho của công ty khác hoặc ngoài phạm vi đều như không tồn tại.
/// </summary>
public sealed class WarehouseAdministration : IWarehouseAdministration
{
    private readonly ProcDbContext _db;
    private readonly ICurrentUserScope _scope;

    /// <summary>
    /// Tạo dịch vụ quản lý kho.
    /// </summary>
    /// <param name="db">DbContext.</param>
    /// <param name="scope">Phạm vi của người thao tác.</param>
    public WarehouseAdministration(ProcDbContext db, ICurrentUserScope scope)
    {
        _db = db;
        _scope = scope;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<WarehouseDetailDto>> ListAsync(CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        var warehouses = await _db.Warehouses.AsNoTracking()
            .OrderBy(w => w.Type)
            .ThenBy(w => w.Code)
            .ToListAsync(cancellationToken);
        // Kho cha ngoài phạm vi của người xem thì không hiện mã (không đọc vượt global query filter).
        var codes = warehouses.ToDictionary(w => w.Id, w => w.Code);
        return warehouses.Select(w => ToDto(w, w.ParentWarehouseId is { } p ? codes.GetValueOrDefault(p) : null)).ToList();
    }

    /// <inheritdoc />
    public async Task<WarehouseDetailDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        var warehouse = await FindAsync(id, cancellationToken);
        var parentCode = warehouse.ParentWarehouseId is { } parentId
            ? await _db.Warehouses.Where(w => w.Id == parentId).Select(w => w.Code).FirstOrDefaultAsync(cancellationToken)
            : null;
        return ToDto(warehouse, parentCode);
    }

    /// <inheritdoc />
    public async Task<WarehouseDetailDto> CreateAsync(CreateWarehouseRequest request, CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        // Kho mới không nằm trong danh sách kho được gán của người bị giới hạn: thêm xong chính họ không thấy kho đó,
        // và tự thêm kho vào phạm vi của mình là tự cấp quyền (design.md mục 9.5) – nên chỉ người được mọi kho mới thêm được.
        if (!_scope.AllWarehouses)
            throw new ForbiddenAccessException("Bạn chỉ được làm việc với một số kho nên không thêm được kho mới.");

        var parent = await FindParentAsync(request.ParentWarehouseId, cancellationToken);
        var warehouse = Warehouse.CreateSub(parent, request.Code, request.Name);
        warehouse.Update(request.Name, request.Address, request.Keeper, request.AllowNegativeStock);
        if (await _db.Warehouses.AnyAsync(w => w.Code == warehouse.Code, cancellationToken))
            throw new BusinessRuleException($"Mã kho {warehouse.Code} đã có ở công ty này.");

        _db.Warehouses.Add(warehouse);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(warehouse.Id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<WarehouseDetailDto> UpdateAsync(Guid id, UpdateWarehouseRequest request, CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        var warehouse = await FindAsync(id, cancellationToken);
        warehouse.Update(request.Name, request.Address, request.Keeper, request.AllowNegativeStock);

        // Chỉ tra kho cha khi đổi: người chỉ được gán kho phụ vẫn sửa được kho đó dù không thấy kho cha.
        if (warehouse.Type == WarehouseType.Sub && request.ParentWarehouseId is { } parentId && parentId != warehouse.ParentWarehouseId)
        {
            // Người bị giới hạn kho chỉ thấy một phần cây kho nên không kiểm tra được vòng lặp – chỉ người được mọi kho mới đổi kho cha.
            if (!_scope.AllWarehouses)
                throw new ForbiddenAccessException("Bạn chỉ được làm việc với một số kho nên không đổi được kho cha.");
            var parent = await FindParentAsync(parentId, cancellationToken);
            await EnsureNoCycleAsync(warehouse, parent, cancellationToken);
            warehouse.MoveTo(parent);
        }

        if (request.IsActive) warehouse.Activate();
        else if (warehouse.IsActive) await DeactivateAsync(warehouse, cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    private async Task DeactivateAsync(Warehouse warehouse, CancellationToken cancellationToken)
    {
        // Quy tắc kho tổng (trong Domain) báo trước: kho tổng không bao giờ ngừng được, dù có hay không có kho con.
        if (warehouse.Type == WarehouseType.Main) warehouse.Deactivate();

        // Kho con đang dùng mà kho cha ngừng thì cây kho có nhánh treo dưới kho không dùng nữa – ngừng kho con trước.
        var activeChildren = await _db.Warehouses
            .Where(w => w.ParentWarehouseId == warehouse.Id && w.IsActive)
            .Select(w => w.Code)
            .ToListAsync(cancellationToken);
        if (activeChildren.Count > 0)
            throw new BusinessRuleException($"Kho {warehouse.Code} còn kho con đang dùng: {string.Join(", ", activeChildren)}. Ngừng dùng kho con trước.");
        warehouse.Deactivate();
    }

    /// <summary>
    /// Kho cha mới không được là chính kho này hay kho con, cháu của nó – nếu không cây kho thành vòng.
    /// Đi ngược từ kho cha mới lên gốc; gặp lại kho đang sửa là có vòng.
    /// </summary>
    private async Task EnsureNoCycleAsync(Warehouse warehouse, Warehouse newParent, CancellationToken cancellationToken)
    {
        var parents = await _db.Warehouses.AsNoTracking()
            .ToDictionaryAsync(w => w.Id, w => w.ParentWarehouseId, cancellationToken);
        var visited = new HashSet<Guid>();
        for (Guid? current = newParent.Id; current is { } id && visited.Add(id); current = parents.GetValueOrDefault(id))
        {
            if (id == warehouse.Id)
                throw new BusinessRuleException($"Không đặt được kho {newParent.Code} làm kho cha của {warehouse.Code}: cây kho sẽ thành vòng.");
        }
    }

    private async Task<Warehouse> FindParentAsync(Guid parentId, CancellationToken cancellationToken)
    {
        var parent = await _db.Warehouses.SingleOrDefaultAsync(w => w.Id == parentId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy kho cha.");
        if (!parent.IsActive) throw new BusinessRuleException($"Kho cha {parent.Code} đã ngừng dùng.");
        return parent;
    }

    private async Task<Warehouse> FindAsync(Guid id, CancellationToken cancellationToken) =>
        await _db.Warehouses.SingleOrDefaultAsync(w => w.Id == id, cancellationToken)
        ?? throw new NotFoundException("Không tìm thấy kho.");

    private static WarehouseDetailDto ToDto(Warehouse w, string? parentCode) =>
        new(w.Id, w.Code, w.Name, w.Type == WarehouseType.Main ? "Main" : "Sub", w.ParentWarehouseId, parentCode,
            w.Address, w.Keeper, w.AllowNegativeStock, w.IsActive);
}
