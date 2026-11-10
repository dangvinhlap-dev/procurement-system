using Microsoft.EntityFrameworkCore;
using Proc.Application.Common;
using Proc.Application.Organization;
using Proc.Domain.Common;
using Proc.Domain.Organization;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.Organization;

/// <summary>
/// Quản lý danh mục bộ phận (design.md mục 5.1, 18.5; khối và Trưởng phòng ban – lát O1, mục 19.3). Mọi truy vấn đi qua global query
/// filter theo công ty đang làm việc, nên bộ phận của công ty khác như không tồn tại.
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
        await CheckOrganizationAsync(companyId, null, request.DivisionId, request.ManagerUserId, cancellationToken);
        department.AssignOrganization(request.DivisionId, request.ManagerUserId);

        _db.Departments.Add(department);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(department.Id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<DepartmentDto> UpdateAsync(Guid id, UpdateDepartmentRequest request, CancellationToken cancellationToken)
    {
        var companyId = _scope.RequireCompany();
        var department = await _db.Departments.SingleOrDefaultAsync(d => d.Id == id, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy bộ phận.");
        // Ngừng dùng luôn được, kể cả khi đã có PR: PR cũ giữ Id bộ phận và vẫn hiện tên, chỉ PR mới không chọn được nữa.
        department.Update(request.Name, request.ManagerName, request.IsActive);
        await CheckOrganizationAsync(companyId, department, request.DivisionId, request.ManagerUserId, cancellationToken);
        department.AssignOrganization(request.DivisionId, request.ManagerUserId);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<OrganizationUserOptionDto>> UserOptionsAsync(CancellationToken cancellationToken)
    {
        var companyId = _scope.RequireCompany();
        return await OrganizationUsers.Options(_db, companyId).ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Guid?> MineAsync(CancellationToken cancellationToken)
    {
        var companyId = _scope.RequireCompany();
        if (_scope.UserId is not { } userId) return null;
        // Bộ phận đã ngừng dùng thì không điền sẵn – PR mới không chọn được bộ phận ngừng dùng.
        return await _db.UserDepartments.AsNoTracking()
            .Where(u => u.UserId == userId && u.CompanyId == companyId)
            .Join(_db.Departments.Where(d => d.IsActive), u => u.DepartmentId, d => d.Id, (_, d) => (Guid?)d.Id)
            .SingleOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Kiểm tra khối và Trưởng phòng ban trước khi gán (lát O1): khối phải thuộc công ty (filter công ty) và còn dùng; Trưởng phòng ban phải
    /// là tài khoản còn hoạt động có vai trò tại công ty. Chỉ kiểm tra giá trị thay đổi – sửa tên không bị chặn vì dữ liệu cũ.
    /// </summary>
    private async Task CheckOrganizationAsync(
        Guid companyId, Department? current, Guid? divisionId, Guid? managerUserId, CancellationToken cancellationToken)
    {
        if (divisionId is { } did && did != Guid.Empty && did != current?.DivisionId)
        {
            var division = await _db.Divisions.AsNoTracking().SingleOrDefaultAsync(d => d.Id == did, cancellationToken)
                ?? throw new BusinessRuleException("Khối không thuộc công ty đang làm việc.");
            if (!division.IsActive) throw new BusinessRuleException($"Khối {division.Code} đã ngừng dùng.");
        }

        if (managerUserId is { } uid && uid != Guid.Empty && uid != current?.ManagerUserId)
            await OrganizationUsers.EnsureApproverAsync(_db, companyId, uid, "Trưởng phòng ban", cancellationToken);
    }

    /// <summary>Chiếu bộ phận sang DTO kèm số PR đã dùng, khối, Trưởng phòng ban, Giám đốc khối và số người thuộc bộ phận.</summary>
    private IQueryable<DepartmentDto> Project(IQueryable<Department> departments) =>
        from d in departments
        join v in _db.Divisions on d.DivisionId equals v.Id into dv
        from v in dv.DefaultIfEmpty()
        select new DepartmentDto(
            d.Id, d.Code, d.Name, d.ManagerName, d.IsActive, _db.PurchaseRequests.Count(r => r.DepartmentId == d.Id),
            d.DivisionId, v == null ? null : v.Code + " – " + v.Name,
            d.ManagerUserId, _db.Users.Where(u => u.Id == d.ManagerUserId).Select(u => u.FullName).FirstOrDefault(),
            v == null ? null : _db.Users.Where(u => u.Id == v.DirectorUserId).Select(u => u.FullName).FirstOrDefault(),
            _db.UserDepartments.Count(u => u.DepartmentId == d.Id));
}

/// <summary>Quản lý danh mục khối (lát O1, design.md mục 19.3) – filter công ty như danh mục bộ phận.</summary>
public sealed class DivisionAdministration : IDivisionAdministration
{
    private readonly ProcDbContext _db;
    private readonly ICurrentUserScope _scope;

    /// <summary>Tạo dịch vụ quản lý khối.</summary>
    /// <param name="db">DbContext.</param>
    /// <param name="scope">Phạm vi của người thao tác.</param>
    public DivisionAdministration(ProcDbContext db, ICurrentUserScope scope)
    {
        _db = db;
        _scope = scope;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DivisionDto>> ListAsync(CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        return await Project(_db.Divisions.AsNoTracking().OrderBy(d => d.Code)).ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<DivisionDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        return await Project(_db.Divisions.AsNoTracking().Where(d => d.Id == id)).SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy khối.");
    }

    /// <inheritdoc />
    public async Task<DivisionDto> CreateAsync(CreateDivisionRequest request, CancellationToken cancellationToken)
    {
        var companyId = _scope.RequireCompany();
        var division = Division.Create(companyId, request.Code, request.Name, request.DirectorUserId);
        if (await _db.Divisions.AnyAsync(d => d.Code == division.Code, cancellationToken))
            throw new BusinessRuleException($"Mã khối {division.Code} đã có ở công ty này.");
        if (division.DirectorUserId is { } uid) await OrganizationUsers.EnsureApproverAsync(_db, companyId, uid, "Giám đốc khối", cancellationToken);

        _db.Divisions.Add(division);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(division.Id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<DivisionDto> UpdateAsync(Guid id, UpdateDivisionRequest request, CancellationToken cancellationToken)
    {
        var companyId = _scope.RequireCompany();
        var division = await _db.Divisions.SingleOrDefaultAsync(d => d.Id == id, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy khối.");
        // Ngừng dùng luôn được: bộ phận đang gán giữ khối (phiếu đang chờ duyệt vẫn tìm được GĐ khối), chỉ không gán mới được nữa.
        var before = division.DirectorUserId;
        division.Update(request.Name, request.DirectorUserId, request.IsActive);
        // Chỉ kiểm tra khi đổi người – sửa tên khối không bị chặn vì GĐ khối cũ vừa bị đổi quyền.
        if (division.DirectorUserId is { } uid && uid != before) await OrganizationUsers.EnsureApproverAsync(_db, companyId, uid, "Giám đốc khối", cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <summary>Chiếu khối sang DTO kèm họ tên Giám đốc khối và số bộ phận thuộc khối.</summary>
    private IQueryable<DivisionDto> Project(IQueryable<Division> divisions) =>
        divisions.Select(d => new DivisionDto(
            d.Id, d.Code, d.Name, d.DirectorUserId, _db.Users.Where(u => u.Id == d.DirectorUserId).Select(u => u.FullName).FirstOrDefault(),
            d.IsActive, _db.Departments.Count(x => x.DivisionId == d.Id)));
}

/// <summary>Tài khoản làm được Trưởng phòng ban / Giám đốc khối tại một công ty (lát O1).</summary>
internal static class OrganizationUsers
{
    /// <summary>Tài khoản còn hoạt động có ít nhất một vai trò tại công ty, theo họ tên.</summary>
    /// <param name="db">DbContext.</param>
    /// <param name="companyId">Công ty.</param>
    /// <returns>Truy vấn danh sách chọn.</returns>
    public static IQueryable<OrganizationUserOptionDto> Options(ProcDbContext db, Guid companyId) =>
        Eligible(db, companyId)
            .OrderBy(u => u.FullName).ThenBy(u => u.UserName)
            .Select(u => new OrganizationUserOptionDto(u.Id, u.UserName ?? string.Empty, u.FullName));

    /// <summary>Tài khoản còn hoạt động có vai trò tại công ty.</summary>
    private static IQueryable<Identity.AppUser> Eligible(ProcDbContext db, Guid companyId) =>
        db.Users.AsNoTracking().Where(u => u.IsActive && db.UserCompanyRoles.Any(r => r.UserId == u.Id && r.CompanyId == companyId));

    /// <summary>
    /// Kiểm tra tài khoản làm được người duyệt động tại công ty: còn hoạt động và có vai trò tại công ty – nếu không, phiếu sẽ kẹt ở cấp
    /// không ai duyệt được.
    /// </summary>
    /// <param name="db">DbContext.</param>
    /// <param name="companyId">Công ty.</param>
    /// <param name="userId">Tài khoản.</param>
    /// <param name="title">Chức danh trong câu báo lỗi ("Trưởng phòng ban", "Giám đốc khối").</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <exception cref="BusinessRuleException">Khi tài khoản không có, đã khóa hoặc không có vai trò tại công ty.</exception>
    public static async Task EnsureApproverAsync(ProcDbContext db, Guid companyId, Guid userId, string title, CancellationToken cancellationToken)
    {
        if (!await Eligible(db, companyId).AnyAsync(u => u.Id == userId, cancellationToken))
            throw new BusinessRuleException($"{title} phải là tài khoản còn hoạt động và đã được phân quyền tại công ty đang làm việc.");
    }
}
