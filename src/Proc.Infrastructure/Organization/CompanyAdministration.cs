using Microsoft.EntityFrameworkCore;
using Proc.Application.Common;
using Proc.Application.Organization;
using Proc.Domain.Common;
using Proc.Domain.Organization;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.Organization;

/// <summary>
/// Quản lý danh mục công ty (design.md mục 5.1, 5.6). Bảng Company không có filter theo công ty (công ty là gốc của
/// phạm vi), nên phạm vi xem/sửa được kiểm tra ở đây: tổng công ty thấy và sửa mọi công ty, công ty thành viên chỉ xem công ty mình.
/// </summary>
public sealed class CompanyAdministration : ICompanyAdministration
{
    private readonly ProcDbContext _db;
    private readonly ICurrentUserScope _scope;

    /// <summary>
    /// Tạo dịch vụ quản lý công ty.
    /// </summary>
    /// <param name="db">DbContext.</param>
    /// <param name="scope">Phạm vi của người thao tác.</param>
    public CompanyAdministration(ProcDbContext db, ICurrentUserScope scope)
    {
        _db = db;
        _scope = scope;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CompanyListItemDto>> ListAsync(CancellationToken cancellationToken)
    {
        var atHeadOffice = await AtHeadOfficeAsync(cancellationToken);
        var companyId = _scope.RequireCompany();
        var userCounts = await _db.UserCompanyRoles.AsNoTracking()
            .GroupBy(x => x.CompanyId)
            .Select(g => new { CompanyId = g.Key, Count = g.Select(x => x.UserId).Distinct().Count() })
            .ToDictionaryAsync(x => x.CompanyId, x => x.Count, cancellationToken);

        var companies = await _db.Companies.AsNoTracking()
            .Where(c => atHeadOffice || c.Id == companyId)
            .OrderByDescending(c => c.IsHeadOffice)
            .ThenBy(c => c.Code)
            .ToListAsync(cancellationToken);
        return companies
            .Select(c => new CompanyListItemDto(
                c.Id, c.Code, c.Name, c.ShortName, c.TaxCode, c.IsHeadOffice, c.IsActive, userCounts.GetValueOrDefault(c.Id)))
            .ToList();
    }

    /// <inheritdoc />
    public async Task<CompanyDetailDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var atHeadOffice = await AtHeadOfficeAsync(cancellationToken);
        // Ở công ty thành viên, công ty khác coi như không tồn tại (404) – mỗi công ty là pháp nhân độc lập (CLAUDE.md mục 4).
        if (!atHeadOffice && id != _scope.CompanyId) throw new NotFoundException("Không tìm thấy công ty.");
        var company = await _db.Companies.AsNoTracking().SingleOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy công ty.");
        return ToDetail(company, atHeadOffice && _scope.HasPermission(Permissions.System.CompanyManage));
    }

    /// <inheritdoc />
    public async Task<CompanyDetailDto> CreateAsync(CreateCompanyRequest request, CancellationToken cancellationToken)
    {
        var headOfficeId = await RequireHeadOfficeAsync(cancellationToken);
        var company = Company.Create(request.Code, request.Name, isHeadOffice: false);
        company.UpdateInfo(request.Name, request.ShortName, request.TaxCode, request.Address, request.Representative, request.StartDate);
        if (await _db.Companies.AnyAsync(c => c.Code == company.Code, cancellationToken))
            throw new BusinessRuleException($"Mã công ty {company.Code} đã có.");
        await EnsureTaxCodeUniqueAsync(company, cancellationToken);

        // Mỗi công ty có đúng một kho tổng (design.md mục 4) – tạo cùng lúc để công ty không bao giờ thiếu kho tổng.
        var main = Warehouse.CreateMain(company.Id, request.MainWarehouseCode, request.MainWarehouseName);
        _db.Companies.Add(company);
        _db.Warehouses.Add(main);

        await AssignCreatorAsync(headOfficeId, company.Id, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(company.Id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<CompanyDetailDto> UpdateAsync(Guid id, UpdateCompanyRequest request, CancellationToken cancellationToken)
    {
        await RequireHeadOfficeAsync(cancellationToken);
        var company = await _db.Companies.SingleOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy công ty.");

        company.UpdateInfo(request.Name, request.ShortName, request.TaxCode, request.Address, request.Representative, request.StartDate);
        await EnsureTaxCodeUniqueAsync(company, cancellationToken);
        if (request.IsActive) company.Activate();
        else company.Deactivate();

        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <summary>
    /// Người thêm công ty được gán các vai trò mình đang có ở tổng công ty, cùng phạm vi nhóm hàng như ở tổng công ty, mọi kho
    /// (công ty mới chỉ có kho tổng vừa tạo). Không cấp thêm quyền nào người đó chưa có, nhưng nếu không gán thì không ai vào
    /// được công ty mới để khai báo kho phụ và gán người dùng (gán quyền chỉ làm tại công ty đang làm việc – design.md mục 9.5).
    /// </summary>
    private async Task AssignCreatorAsync(Guid headOfficeId, Guid companyId, CancellationToken cancellationToken)
    {
        var userId = _scope.UserId ?? throw new ForbiddenAccessException("Chưa đăng nhập.");
        var roleIds = await _db.UserCompanyRoles.AsNoTracking()
            .Where(x => x.UserId == userId && x.CompanyId == headOfficeId)
            .Select(x => x.RoleId)
            .ToListAsync(cancellationToken);
        foreach (var roleId in roleIds)
            _db.UserCompanyRoles.Add(UserCompanyRole.Assign(userId, companyId, roleId));

        var categoryIds = await _db.UserItemCategoryScopes.AsNoTracking()
            .Where(x => x.UserId == userId && x.CompanyId == headOfficeId)
            .Select(x => x.ItemCategoryId)
            .ToListAsync(cancellationToken);
        foreach (var categoryId in categoryIds)
            _db.UserItemCategoryScopes.Add(UserItemCategoryScope.Allow(userId, companyId, categoryId));
    }

    private async Task EnsureTaxCodeUniqueAsync(Company company, CancellationToken cancellationToken)
    {
        // Mỗi công ty là một pháp nhân, có mã số thuế riêng (chỉ mục duy nhất trong CSDL chặn lần cuối).
        if (company.TaxCode is null) return;
        var other = await _db.Companies.AsNoTracking()
            .Where(c => c.TaxCode == company.TaxCode && c.Id != company.Id)
            .Select(c => c.Code)
            .FirstOrDefaultAsync(cancellationToken);
        if (other is not null)
            throw new BusinessRuleException($"Mã số thuế {company.TaxCode} đã dùng cho công ty {other}.");
    }

    private Task<bool> AtHeadOfficeAsync(CancellationToken cancellationToken) =>
        HeadOffice.IsWorkingAtAsync(_db, _scope, cancellationToken);

    private Task<Guid> RequireHeadOfficeAsync(CancellationToken cancellationToken) =>
        HeadOffice.RequireAsync(_db, _scope, "công ty", cancellationToken);

    private static CompanyDetailDto ToDetail(Company c, bool canEdit) =>
        new(c.Id, c.Code, c.Name, c.ShortName, c.TaxCode, c.Address, c.Representative, c.IsHeadOffice,
            c.AccountingCurrency, c.StartDate, c.IsActive, canEdit);
}
