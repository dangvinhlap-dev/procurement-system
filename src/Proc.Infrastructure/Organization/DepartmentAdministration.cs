using Microsoft.EntityFrameworkCore;
using Proc.Application.Common;
using Proc.Application.Organization;
using Proc.Domain.Common;
using Proc.Domain.Organization;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.Organization;

/// <summary>
/// Quản lý danh mục bộ phận (design.md mục 5.1, 18.5). Mọi truy vấn đi qua global query filter theo công ty đang làm việc,
/// nên bộ phận của công ty khác như không tồn tại.
/// </summary>
public sealed class DepartmentAdministration : IDepartmentAdministration
{
    private readonly ProcDbContext _db;
    private readonly ICurrentUserScope _scope;

    /// <summary>
    /// Tạo dịch vụ quản lý bộ phận.
    /// </summary>
    /// <param name="db">DbContext.</param>
    /// <param name="scope">Phạm vi của người thao tác.</param>
    public DepartmentAdministration(ProcDbContext db, ICurrentUserScope scope)
    {
        _db = db;
        _scope = scope;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DepartmentDto>> ListAsync(CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        return await Project(_db.Departments.AsNoTracking().OrderBy(d => d.Code)).ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<DepartmentDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        return await Project(_db.Departments.AsNoTracking().Where(d => d.Id == id)).SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy bộ phận.");
    }

    /// <inheritdoc />
    public async Task<DepartmentDto> CreateAsync(CreateDepartmentRequest request, CancellationToken cancellationToken)
    {
        var companyId = _scope.RequireCompany();
        var department = Department.Create(companyId, request.Code, request.Name, request.ManagerName);
        if (await _db.Departments.AnyAsync(d => d.Code == department.Code, cancellationToken))
            throw new BusinessRuleException($"Mã bộ phận {department.Code} đã có ở công ty này.");

        _db.Departments.Add(department);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(department.Id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<DepartmentDto> UpdateAsync(Guid id, UpdateDepartmentRequest request, CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        var department = await _db.Departments.SingleOrDefaultAsync(d => d.Id == id, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy bộ phận.");
        // Ngừng dùng luôn được, kể cả khi đã có PR: PR cũ giữ Id bộ phận và vẫn hiện tên, chỉ PR mới không chọn được nữa.
        department.Update(request.Name, request.ManagerName, request.IsActive);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    private IQueryable<DepartmentDto> Project(IQueryable<Department> departments) =>
        departments.Select(d => new DepartmentDto(
            d.Id, d.Code, d.Name, d.ManagerName, d.IsActive, _db.PurchaseRequests.Count(r => r.DepartmentId == d.Id)));
}
