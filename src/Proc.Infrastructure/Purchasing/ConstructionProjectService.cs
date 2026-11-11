using Microsoft.EntityFrameworkCore;
using Proc.Application.Common;
using Proc.Application.Purchasing;
using Proc.Domain.Common;
using Proc.Domain.Purchasing;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.Purchasing;

/// <summary>
/// Danh mục công trình và dự toán (lát Y2, design.md mục 19.2). Bảng lọc theo công ty đang làm việc qua global query filter; phòng ban đọc
/// qua bảng bộ phận đã lọc nên không gắn được phòng ban của công ty khác (CSDL còn khóa ngoại kép chặn thêm).
/// </summary>
public sealed class ConstructionProjectService : IConstructionProjectService
{
    private readonly ProcDbContext _db;
    private readonly ICurrentUserScope _scope;
    private readonly IClock _clock;

    /// <summary>Tạo dịch vụ công trình.</summary>
    /// <param name="db">DbContext.</param>
    /// <param name="scope">Phạm vi của người thao tác.</param>
    /// <param name="clock">Đồng hồ (ngày Việt Nam của dự toán ban đầu).</param>
    public ConstructionProjectService(ProcDbContext db, ICurrentUserScope scope, IClock clock)
    {
        _db = db;
        _scope = scope;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ConstructionProjectDto>> ListAsync(CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        var projects = await _db.ConstructionProjects.AsNoTracking().Include(p => p.Revisions).OrderBy(p => p.Code).ToListAsync(cancellationToken);
        return await ToDtosAsync(projects, withRevisions: false, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ConstructionProjectOptionDto>> OptionsAsync(CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        return await _db.ConstructionProjects.AsNoTracking()
            .Where(p => p.Status == ConstructionProjectStatus.InProgress)
            .OrderBy(p => p.Code)
            .Select(p => new ConstructionProjectOptionDto(p.Id, p.Code, p.Name, p.DepartmentId, p.EstimateAmount, p.Status.ToString()))
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ConstructionProjectDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        var project = await _db.ConstructionProjects.AsNoTracking().Include(p => p.Revisions).SingleOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy công trình.");
        return (await ToDtosAsync([project], withRevisions: true, cancellationToken))[0];
    }

    /// <inheritdoc />
    public async Task<ConstructionProjectDto> CreateAsync(CreateConstructionProjectRequest request, CancellationToken cancellationToken)
    {
        var companyId = _scope.RequireCompany();
        await EnsureDepartmentAsync(request.DepartmentId, currentDepartmentId: null, cancellationToken);
        var project = ConstructionProject.Create(
            companyId, request.Code, request.Name, request.DepartmentId, request.Location, request.StartDate, request.ExpectedEndDate,
            request.EstimateAmount, request.Basis, request.Note, _clock.TodayInVietnam());
        if (await _db.ConstructionProjects.AnyAsync(p => p.Code == project.Code, cancellationToken))
            throw new BusinessRuleException($"Mã công trình {project.Code} đã có ở công ty này.");
        _db.ConstructionProjects.Add(project);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(project.Id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ConstructionProjectDto> UpdateAsync(Guid id, UpdateConstructionProjectRequest request, CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        var project = await _db.ConstructionProjects.SingleOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy công trình.");
        await EnsureDepartmentAsync(request.DepartmentId, project.DepartmentId, cancellationToken);
        var status = Enum.Parse<ConstructionProjectStatus>(request.Status);
        project.Update(request.Name, request.DepartmentId, request.Location, request.StartDate, request.ExpectedEndDate, status, request.Note);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ConstructionProjectDto> ReviseEstimateAsync(Guid id, ReviseConstructionEstimateRequest request, CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        var project = await _db.ConstructionProjects.Include(p => p.Revisions).SingleOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy công trình.");
        var revision = project.ReviseEstimate(request.NewAmount, request.EffectiveDate, request.Basis, request.Reason);
        // Dòng con mới thêm qua trường riêng: đánh dấu Added để EF không hiểu nhầm là dòng có sẵn (khóa sinh sẵn uuid v7).
        _db.Entry(revision).State = EntityState.Added;
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Hai người điều chỉnh cùng lúc trùng số lần (chỉ mục duy nhất) – báo người sau tải lại.
            throw new BusinessRuleException("Dự toán vừa được người khác điều chỉnh – hãy tải lại công trình rồi làm lại.");
        }
        return await GetAsync(id, cancellationToken);
    }

    /// <summary>
    /// Phòng ban phải thuộc công ty đang làm việc (bảng bộ phận đã lọc theo công ty) và còn dùng – trừ khi giữ nguyên phòng ban cũ đã ngừng dùng.
    /// </summary>
    private async Task EnsureDepartmentAsync(Guid departmentId, Guid? currentDepartmentId, CancellationToken cancellationToken)
    {
        var department = await _db.Departments.AsNoTracking().Where(d => d.Id == departmentId)
                .Select(d => new { d.IsActive }).SingleOrDefaultAsync(cancellationToken)
            ?? throw new BusinessRuleException("Phòng ban đề xuất không thuộc công ty đang làm việc.");
        if (!department.IsActive && departmentId != currentDepartmentId)
            throw new BusinessRuleException("Phòng ban đề xuất đã ngừng dùng.");
    }

    /// <summary>Đổi sang DTO kèm tên phòng ban, người tạo, người ghi dự toán.</summary>
    private async Task<List<ConstructionProjectDto>> ToDtosAsync(
        IReadOnlyList<ConstructionProject> projects, bool withRevisions, CancellationToken cancellationToken)
    {
        var departmentIds = projects.Select(p => p.DepartmentId).Distinct().ToList();
        var departments = await _db.Departments.AsNoTracking()
            .Where(d => departmentIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id, d => new { d.Code, d.Name }, cancellationToken);
        var userIds = projects.SelectMany(p => p.Revisions.Select(r => r.CreatedBy).Append(p.CreatedBy))
            .OfType<Guid>().Distinct().ToList();
        var users = await _db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken);
        string? UserName(Guid? id) => id is { } x && users.TryGetValue(x, out var n) ? n : null;

        return projects.Select(p =>
        {
            var revisions = p.Revisions.OrderBy(r => r.RevisionNo).ToList();
            var department = departments.GetValueOrDefault(p.DepartmentId);
            var revisionDtos = withRevisions
                ? revisions.Select(r => new ConstructionProjectRevisionDto(
                    r.Id, r.RevisionNo, r.EffectiveDate, r.PreviousAmount, r.NewAmount, r.NewAmount - r.PreviousAmount, r.Basis, r.Reason, r.CreatedAt,
                    UserName(r.CreatedBy))).ToList()
                : [];
            return new ConstructionProjectDto(
                p.Id, p.Code, p.Name, p.DepartmentId, department?.Code ?? string.Empty, department?.Name ?? string.Empty, p.Location, p.StartDate,
                p.ExpectedEndDate, p.EstimateAmount, revisions.FirstOrDefault()?.NewAmount ?? p.EstimateAmount, Math.Max(0, revisions.Count - 1),
                p.Status.ToString(), p.Note, p.CreatedAt, UserName(p.CreatedBy), revisionDtos);
        }).ToList();
    }
}
