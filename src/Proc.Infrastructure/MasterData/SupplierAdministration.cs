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
/// Quản lý nhóm NCC (design.md mục 5.3, 5.8). Danh mục dùng chung, không có filter phạm vi.
/// </summary>
public sealed class SupplierGroupAdministration : ISupplierGroupAdministration
{
    private readonly ProcDbContext _db;
    private readonly ICurrentUserScope _scope;

    /// <summary>
    /// Tạo dịch vụ.
    /// </summary>
    /// <param name="db">DbContext.</param>
    /// <param name="scope">Phạm vi của người thao tác.</param>
    public SupplierGroupAdministration(ProcDbContext db, ICurrentUserScope scope)
    {
        _db = db;
        _scope = scope;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SupplierGroupDto>> ListAsync(CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        var counts = await _db.Suppliers.AsNoTracking()
            .GroupBy(s => s.GroupId)
            .Select(g => new { GroupId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.GroupId, x => x.Count, cancellationToken);
        var groups = await _db.SupplierGroups.AsNoTracking().OrderBy(g => g.Code).ToListAsync(cancellationToken);
        return groups.Select(g => new SupplierGroupDto(g.Id, g.Code, g.Name, g.IsActive, counts.GetValueOrDefault(g.Id))).ToList();
    }

    /// <inheritdoc />
    public async Task<SupplierGroupDto> CreateAsync(CreateSupplierGroupRequest request, CancellationToken cancellationToken)
    {
        await HeadOffice.RequireAsync(_db, _scope, "nhóm NCC", cancellationToken);
        var group = SupplierGroup.Create(request.Code, request.Name);
        if (await _db.SupplierGroups.AnyAsync(g => g.Code == group.Code, cancellationToken))
            throw new BusinessRuleException($"Mã nhóm NCC {group.Code} đã có.");
        _db.SupplierGroups.Add(group);
        await _db.SaveChangesAsync(cancellationToken);
        return new SupplierGroupDto(group.Id, group.Code, group.Name, group.IsActive, 0);
    }

    /// <inheritdoc />
    public async Task<SupplierGroupDto> UpdateAsync(Guid id, UpdateSupplierGroupRequest request, CancellationToken cancellationToken)
    {
        await HeadOffice.RequireAsync(_db, _scope, "nhóm NCC", cancellationToken);
        var group = await _db.SupplierGroups.SingleOrDefaultAsync(g => g.Id == id, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy nhóm NCC.");
        group.Rename(request.Name);
        if (request.IsActive)
        {
            group.Activate();
        }
        else if (group.IsActive)
        {
            // NCC còn giao dịch được (không phải danh sách đen) mà nhóm ngừng thì sửa NCC đó phải đổi nhóm – chuyển NCC trước.
            var active = await _db.Suppliers.CountAsync(s => s.GroupId == id && s.Status != SupplierStatus.Blacklisted, cancellationToken);
            if (active > 0)
                throw new BusinessRuleException($"Nhóm {group.Code} còn {active} NCC đang giao dịch. Chuyển các NCC đó sang nhóm khác trước.");
            group.Deactivate();
        }

        await _db.SaveChangesAsync(cancellationToken);
        var count = await _db.Suppliers.CountAsync(s => s.GroupId == id, cancellationToken);
        return new SupplierGroupDto(group.Id, group.Code, group.Name, group.IsActive, count);
    }
}

/// <summary>
/// Quản lý nhà cung cấp (design.md mục 5.3, 5.8). Danh mục dùng chung, không có filter phạm vi: mọi công ty xem được,
/// chỉ tổng công ty thêm, sửa. (NCC áp dụng cho công ty nào – bảng SupplierCompany – làm ở lát sau.)
/// </summary>
public sealed class SupplierAdministration : ISupplierAdministration
{
    /// <summary>Số dòng tối đa mỗi lần tìm.</summary>
    public const int SearchLimit = 5000;

    private readonly ProcDbContext _db;
    private readonly ICurrentUserScope _scope;

    /// <summary>
    /// Tạo dịch vụ.
    /// </summary>
    /// <param name="db">DbContext.</param>
    /// <param name="scope">Phạm vi của người thao tác.</param>
    public SupplierAdministration(ProcDbContext db, ICurrentUserScope scope)
    {
        _db = db;
        _scope = scope;
    }

    /// <inheritdoc />
    public async Task<SupplierListResult> SearchAsync(SupplierQuery query, CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        var suppliers = _db.Suppliers.AsNoTracking();
        if (query.GroupId is { } groupId) suppliers = suppliers.Where(s => s.GroupId == groupId);
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var status = ParseStatus(query.Status);
            suppliers = suppliers.Where(s => s.Status == status);
        }

        foreach (var word in SearchText.Normalize(query.Search).Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var pattern = LikePatterns.Contains(word);
            suppliers = suppliers.Where(s => EF.Functions.Like(s.SearchText, pattern, LikePatterns.Escape));
        }

        var rows = await (
                from s in suppliers
                join g in _db.SupplierGroups on s.GroupId equals g.Id
                join c in _db.Companies on s.InternalCompanyId equals c.Id into internalCompany
                from c in internalCompany.DefaultIfEmpty()
                orderby s.Code
                select new
                {
                    s.Id, s.Code, s.Name, s.ShortName, GroupCode = g.Code, GroupName = g.Name, s.IsForeign, s.TaxCode,
                    s.Phone, s.Email, s.Address, s.Status, s.Rating, CompanyCode = c == null ? null : c.Code,
                    Primary = s.Contacts.Where(x => x.IsPrimary).Select(x => new { x.Name, x.Phone }).FirstOrDefault(),
                })
            .Take(SearchLimit + 1)
            .ToListAsync(cancellationToken);

        var items = rows.Take(SearchLimit)
            .Select(r => new SupplierListItemDto(
                r.Id, r.Code, r.Name, r.ShortName, r.GroupCode, r.GroupName, r.IsForeign, r.TaxCode, r.Phone, r.Email, r.Address,
                r.Primary is null ? null : r.Primary.Phone is null ? r.Primary.Name : $"{r.Primary.Name} – {r.Primary.Phone}",
                r.Status.ToString(), r.Rating, r.CompanyCode))
            .ToList();
        return new SupplierListResult(items, rows.Count > SearchLimit, SearchLimit);
    }

    /// <inheritdoc />
    public async Task<SupplierDetailDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var canEdit = _scope.HasPermission(Permissions.MasterData.SupplierManage)
            && await HeadOffice.IsWorkingAtAsync(_db, _scope, cancellationToken);
        var s = await _db.Suppliers.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy nhà cung cấp.");
        return new SupplierDetailDto(
            s.Id, s.Code, s.GroupId, s.Name, s.ShortName, s.IsForeign, s.TaxCode, s.Address, s.Phone, s.Email, s.Website,
            s.PaymentTerms, s.SupplyScope, s.Rating, s.Status.ToString(), s.StatusNote, s.InternalCompanyId,
            s.Contacts.Select(c => new SupplierContactDto(c.Id, c.Name, c.Position, c.Phone, c.Email, c.IsPrimary)).ToList(),
            s.BankAccounts.Select(b => new SupplierBankAccountDto(b.Id, b.BankName, b.Branch, b.AccountNumber, b.AccountName, b.IsDefault)).ToList(),
            canEdit);
    }

    /// <inheritdoc />
    public async Task<SupplierDetailDto> CreateAsync(SaveSupplierRequest request, CancellationToken cancellationToken)
    {
        await HeadOffice.RequireAsync(_db, _scope, "nhà cung cấp", cancellationToken);
        if (string.IsNullOrWhiteSpace(request.Code)) throw new BusinessRuleException("Hãy nhập mã NCC.");
        var supplier = Supplier.Create(request.Code, await FindGroupAsync(request.GroupId, cancellationToken), ToInfo(request));
        if (await _db.Suppliers.AnyAsync(s => s.Code == supplier.Code, cancellationToken))
            throw new BusinessRuleException($"Mã NCC {supplier.Code} đã có.");
        await ApplyAsync(supplier, request, cancellationToken);

        _db.Suppliers.Add(supplier);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(supplier.Id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<SupplierDetailDto> UpdateAsync(Guid id, SaveSupplierRequest request, CancellationToken cancellationToken)
    {
        await HeadOffice.RequireAsync(_db, _scope, "nhà cung cấp", cancellationToken);
        var supplier = await _db.Suppliers.SingleOrDefaultAsync(s => s.Id == id, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy nhà cung cấp.");
        supplier.Update(ToInfo(request));
        if (request.GroupId != supplier.GroupId) supplier.ChangeGroup(await FindGroupAsync(request.GroupId, cancellationToken));
        await ApplyAsync(supplier, request, cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <summary>Trạng thái, NCC nội bộ, người liên hệ, tài khoản ngân hàng, kiểm tra trùng mã số thuế – chung cho thêm và sửa.</summary>
    private async Task ApplyAsync(Supplier supplier, SaveSupplierRequest request, CancellationToken cancellationToken)
    {
        supplier.SetStatus(ParseStatus(request.Status), request.StatusNote);
        supplier.SetContacts(request.Contacts.Select(c => new SupplierContactData(c.Id, c.Name, c.Position, c.Phone, c.Email, c.IsPrimary)));
        supplier.SetBankAccounts(request.BankAccounts.Select(b =>
            new SupplierBankAccountData(b.Id, b.BankName, b.Branch, b.AccountNumber, b.AccountName, b.IsDefault)));

        if (supplier.TaxCode is { } taxCode)
        {
            var other = await _db.Suppliers.AsNoTracking()
                .Where(s => s.TaxCode == taxCode && s.Id != supplier.Id)
                .Select(s => s.Code)
                .FirstOrDefaultAsync(cancellationToken);
            if (other is not null) throw new BusinessRuleException($"Mã số thuế {taxCode} đã dùng cho NCC {other}.");
        }

        if (request.InternalCompanyId is { } companyId && companyId != supplier.InternalCompanyId)
        {
            var company = await _db.Companies.AsNoTracking().SingleOrDefaultAsync(c => c.Id == companyId, cancellationToken)
                ?? throw new NotFoundException("Không tìm thấy công ty thành viên.");
            var other = await _db.Suppliers.AsNoTracking()
                .Where(s => s.InternalCompanyId == companyId && s.Id != supplier.Id)
                .Select(s => s.Code)
                .FirstOrDefaultAsync(cancellationToken);
            if (other is not null) throw new BusinessRuleException($"Công ty {company.Code} đã có NCC nội bộ {other}.");
        }

        supplier.LinkInternalCompany(request.InternalCompanyId);
    }

    private async Task<SupplierGroup> FindGroupAsync(Guid id, CancellationToken cancellationToken) =>
        await _db.SupplierGroups.SingleOrDefaultAsync(g => g.Id == id, cancellationToken)
        ?? throw new NotFoundException("Không tìm thấy nhóm NCC.");

    private static SupplierInfo ToInfo(SaveSupplierRequest r) =>
        new(r.Name, r.ShortName, r.IsForeign, r.TaxCode, r.Address, r.Phone, r.Email, r.Website, r.PaymentTerms, r.SupplyScope, r.Rating);

    private static SupplierStatus ParseStatus(string? status) =>
        Enum.TryParse<SupplierStatus>(status, ignoreCase: true, out var value) && Enum.IsDefined(value)
            ? value
            : throw new BusinessRuleException("Trạng thái NCC phải là Potential, Approved, Suspended hoặc Blacklisted.");

}
