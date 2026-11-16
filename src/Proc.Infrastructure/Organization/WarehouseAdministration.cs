using Microsoft.EntityFrameworkCore;
using Proc.Application.Common;
using Proc.Application.Organization;
using Proc.Domain.Common;
using Proc.Domain.Organization;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.Organization;

/// <summary>
/// Quản lý kho – nơi nhận hàng, danh sách phẳng (design.md mục 5.1, 5.6, 19.4). Mọi truy vấn đi qua global query filter của Warehouse (công ty đang làm việc
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
        var warehouses = await _db.Warehouses.AsNoTracking().OrderBy(w => w.Code).ToListAsync(cancellationToken);
        return warehouses.Select(ToDto).ToList();
    }

    /// <inheritdoc />
    public async Task<WarehouseDetailDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        return ToDto(await FindAsync(id, cancellationToken));
    }

    /// <inheritdoc />
    public async Task<WarehouseDetailDto> CreateAsync(CreateWarehouseRequest request, CancellationToken cancellationToken)
    {
        var companyId = _scope.RequireCompany();
        // Kho mới không nằm trong danh sách kho được gán của người bị giới hạn: thêm xong chính họ không thấy kho đó,
        // và tự thêm kho vào phạm vi của mình là tự cấp quyền (design.md mục 9.5) – nên chỉ người được mọi kho mới thêm được.
        if (!_scope.AllWarehouses)
            throw new ForbiddenAccessException("Bạn chỉ được làm việc với một số kho nên không thêm được kho mới.");

        var warehouse = Warehouse.Create(companyId, request.Code, request.Name);
        warehouse.Update(request.Name, request.Address, request.Keeper);
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
        warehouse.Update(request.Name, request.Address, request.Keeper);
        if (request.IsActive) warehouse.Activate();
        else warehouse.Deactivate();

        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <summary>Tìm kho theo Id; không thấy thì báo lỗi 404.</summary>
    private async Task<Warehouse> FindAsync(Guid id, CancellationToken cancellationToken) =>
        await _db.Warehouses.SingleOrDefaultAsync(w => w.Id == id, cancellationToken)
        ?? throw new NotFoundException("Không tìm thấy kho.");

    /// <summary>Đổi kho sang DTO chi tiết.</summary>
    private static WarehouseDetailDto ToDto(Warehouse w) => new(w.Id, w.Code, w.Name, w.Address, w.Keeper, w.IsActive);
}
