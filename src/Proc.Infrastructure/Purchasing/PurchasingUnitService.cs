using Microsoft.EntityFrameworkCore;
using Proc.Application.Common;
using Proc.Application.Purchasing;
using Proc.Domain.Common;
using Proc.Domain.Purchasing;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.Purchasing;

/// <summary>
/// Danh mục đơn vị mua (lát Y5a, design.md mục 19.3 – Q3). Bảng lọc theo công ty đang làm việc qua global query filter nên mã chỉ cần duy nhất
/// trong công ty và không đọc/sửa được đơn vị của công ty khác.
/// </summary>
public sealed class PurchasingUnitService : IPurchasingUnitService
{
    private readonly ProcDbContext _db;
    private readonly ICurrentUserScope _scope;

    /// <summary>Tạo dịch vụ đơn vị mua.</summary>
    /// <param name="db">DbContext.</param>
    /// <param name="scope">Phạm vi của người thao tác.</param>
    public PurchasingUnitService(ProcDbContext db, ICurrentUserScope scope)
    {
        _db = db;
        _scope = scope;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PurchasingUnitDto>> ListAsync(CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        return await Project(_db.PurchasingUnits.AsNoTracking().OrderBy(u => u.Code)).ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PurchasingUnitOptionDto>> OptionsAsync(CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        return await _db.PurchasingUnits.AsNoTracking().Where(u => u.IsActive).OrderBy(u => u.Code)
            .Select(u => new PurchasingUnitOptionDto(u.Id, u.Code, u.Name)).ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PurchasingUnitDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        return await Project(_db.PurchasingUnits.AsNoTracking().Where(u => u.Id == id)).SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy đơn vị mua.");
    }

    /// <inheritdoc />
    public async Task<PurchasingUnitDto> CreateAsync(CreatePurchasingUnitRequest request, CancellationToken cancellationToken)
    {
        var companyId = _scope.RequireCompany();
        var unit = PurchasingUnit.Create(companyId, request.Code, request.Name, request.Note);
        if (await _db.PurchasingUnits.AnyAsync(u => u.Code == unit.Code, cancellationToken))
            throw new BusinessRuleException($"Mã đơn vị mua {unit.Code} đã có ở công ty này.");
        _db.PurchasingUnits.Add(unit);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(unit.Id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PurchasingUnitDto> UpdateAsync(Guid id, UpdatePurchasingUnitRequest request, CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        var unit = await _db.PurchasingUnits.SingleOrDefaultAsync(u => u.Id == id, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy đơn vị mua.");
        // Ngừng dùng luôn được: dòng bảng tổng hợp đã gán giữ đơn vị (lịch sử), chỉ không chọn mới được nữa.
        unit.Update(request.Name, request.Note, request.IsActive);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <summary>Chiếu đơn vị mua sang DTO.</summary>
    private static IQueryable<PurchasingUnitDto> Project(IQueryable<PurchasingUnit> units) =>
        units.Select(u => new PurchasingUnitDto(u.Id, u.Code, u.Name, u.Note, u.IsActive));
}
