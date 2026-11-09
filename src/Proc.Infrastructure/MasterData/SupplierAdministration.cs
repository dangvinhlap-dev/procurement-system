using Microsoft.EntityFrameworkCore;
using Proc.Application.Common;
using Proc.Application.MasterData;
using Proc.Domain.Common;
using Proc.Domain.MasterData;
using Proc.Domain.Organization;
using Proc.Infrastructure.Organization;
using Proc.Infrastructure.Persistence;
using Proc.Infrastructure.Purchasing;

namespace Proc.Infrastructure.MasterData;

/// <summary>
/// Quản lý nhóm NCC (design.md mục 5.3, 5.8). Danh mục dùng chung, không có filter phạm vi.
/// </summary>
public sealed class SupplierGroupAdministration : ISupplierGroupAdministration
{
    private readonly ProcDbContext _db;
    private readonly ICurrentUserScope _scope;
    private readonly CatalogNumberAllocator _numbers;

    /// <summary>
    /// Tạo dịch vụ.
    /// </summary>
    /// <param name="db">DbContext.</param>
    /// <param name="scope">Phạm vi của người thao tác.</param>
    /// <param name="numbers">Bộ cấp mã theo dải đánh số (design.md mục 5.11).</param>
    public SupplierGroupAdministration(ProcDbContext db, ICurrentUserScope scope, CatalogNumberAllocator numbers)
    {
        _db = db;
        _scope = scope;
        _numbers = numbers;
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
        // Mã do hệ thống cấp theo dải đánh số (design.md mục 5.11) – người dùng không tự đặt.
        CatalogNumberAllocator.RejectManualCode(NumberedCatalogs.SupplierGroup, request.Code);
        await using var transaction = await _numbers.BeginAsync(cancellationToken);
        var group = SupplierGroup.Create(await _numbers.NextAsync(NumberedCatalogs.SupplierGroup, null, cancellationToken), request.Name);
        _db.SupplierGroups.Add(group);
        await _db.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        return new SupplierGroupDto(group.Id, group.Code, group.Name, group.IsActive, 0);
    }

    /// <inheritdoc />
    public async Task<SupplierGroupDto> UpdateAsync(Guid id, UpdateSupplierGroupRequest request, CancellationToken cancellationToken)
    {
        await SharedCatalog.RequireEditAsync(_db, _scope, "nhóm NCC", Permissions.MasterData.SupplierManage, cancellationToken);
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

    /// <summary>Số ngành hàng tối đa của một NCC.</summary>
    public const int MaxCategories = 50;

    private readonly ProcDbContext _db;
    private readonly ICurrentUserScope _scope;
    private readonly CatalogNumberAllocator _numbers;
    private readonly IClock _clock;

    /// <summary>
    /// Tạo dịch vụ.
    /// </summary>
    /// <param name="db">DbContext.</param>
    /// <param name="scope">Phạm vi của người thao tác.</param>
    /// <param name="numbers">Bộ cấp mã theo dải đánh số (design.md mục 5.11).</param>
    /// <param name="clock">Đồng hồ (thời điểm duyệt).</param>
    public SupplierAdministration(ProcDbContext db, ICurrentUserScope scope, CatalogNumberAllocator numbers, IClock clock)
    {
        _db = db;
        _scope = scope;
        _numbers = numbers;
        _clock = clock;
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

        if (query.CategoryId is { } categoryId)
        {
            // Chọn một ngành hàng là gồm mọi nhóm con của nó (như bộ lọc nhóm hàng ở danh mục hàng hóa).
            var branch = await CategoryBranchAsync(categoryId, cancellationToken);
            suppliers = suppliers.Where(s => _db.SupplierCategories.Any(x => x.SupplierId == s.Id && branch.Contains(x.ItemCategoryId)));
        }

        // SupplierCompanies tự lọc theo công ty đang làm việc, nên đây là "áp dụng tại công ty này".
        if (query.AppliedOnly) suppliers = suppliers.Where(s => _db.SupplierCompanies.Any(sc => sc.SupplierId == s.Id && sc.IsActive));

        var rows = await (
                from s in suppliers
                join g in _db.SupplierGroups on s.GroupId equals g.Id
                join c in _db.Companies on s.InternalCompanyId equals c.Id into internalCompany
                from c in internalCompany.DefaultIfEmpty()
                join sc in _db.SupplierCompanies on s.Id equals sc.SupplierId into applied
                from sc in applied.DefaultIfEmpty()
                orderby s.Code
                select new
                {
                    s.Id, s.Code, s.Name, s.ShortName, GroupCode = g.Code, GroupName = g.Name, s.IsForeign, s.TaxCode,
                    s.Phone, s.Email, s.Address, s.Status, s.Rating, CompanyCode = c == null ? null : c.Code,
                    Primary = s.Contacts.Where(x => x.IsPrimary).Select(x => new { x.Name, x.Phone }).FirstOrDefault(),
                    Applied = sc != null && sc.IsActive,
                    LocalCode = sc == null ? null : sc.LocalCode,
                })
            .Take(SearchLimit + 1)
            .ToListAsync(cancellationToken);

        var page = rows.Take(SearchLimit).ToList();
        var categories = (await CategoriesOfAsync(page.Select(r => r.Id).ToList(), cancellationToken))
            .ToDictionary(g => g.Key, g => string.Join("; ", g.Value.Select(c => c.Name)));
        var items = page
            .Select(r => new SupplierListItemDto(
                r.Id, r.Code, r.Name, r.ShortName, r.GroupCode, r.GroupName, r.IsForeign, r.TaxCode, r.Phone, r.Email, r.Address,
                r.Primary is null ? null : r.Primary.Phone is null ? r.Primary.Name : $"{r.Primary.Name} – {r.Primary.Phone}",
                r.Status.ToString(), r.Rating, r.CompanyCode, r.Applied, r.LocalCode) { Categories = categories.GetValueOrDefault(r.Id) })
            .ToList();
        return new SupplierListResult(items, rows.Count > SearchLimit, SearchLimit);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SupplierLookupDto>> LookupForPurchaseAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        // SupplierCompanies tự lọc theo công ty đang làm việc.
        return await _db.Suppliers.AsNoTracking()
            .Where(s => ids.Contains(s.Id)
                        || (s.Status == SupplierStatus.Approved && _db.SupplierCompanies.Any(sc => sc.SupplierId == s.Id && sc.IsActive)))
            .OrderBy(s => s.Code)
            .Select(s => new SupplierLookupDto(s.Id, s.Code, s.Name, s.TaxCode))
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<SupplierDetailDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var canEdit = await SharedCatalog.CanEditAsync(_db, _scope, Permissions.MasterData.SupplierManage, cancellationToken);
        var s = await _db.Suppliers.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy nhà cung cấp.");
        return new SupplierDetailDto(
            s.Id, s.Code, s.GroupId, s.Name, s.ShortName, s.IsForeign, s.TaxCode, s.Address, s.Phone, s.Email, s.Website,
            s.PaymentTerms, s.SupplyScope, s.Rating, s.Status.ToString(), s.StatusNote, s.InternalCompanyId,
            s.Contacts.Select(c => new SupplierContactDto(c.Id, c.Name, c.Position, c.Phone, c.Email, c.IsPrimary)).ToList(),
            s.BankAccounts.Select(b => new SupplierBankAccountDto(b.Id, b.BankName, b.Branch, b.AccountNumber, b.AccountName, b.IsDefault)).ToList(),
            canEdit, await PendingRequestsAsync(id, cancellationToken))
        {
            Version = s.Version,
            Categories = (await CategoriesOfAsync([id], cancellationToken)).GetValueOrDefault(id) ?? [],
            RequiresOnboarding = s.RequiresOnboarding,
            // Hồ sơ thuộc tổng công ty: filter công ty giữ nguyên, ở công ty thành viên thì không thấy (null).
            LatestOnboarding = await _db.SupplierOnboardings.AsNoTracking().Where(o => o.SupplierId == id).OrderByDescending(o => o.CreatedAt)
                .Select(o => new SupplierOnboardingRefDto(o.Id, o.No, o.Status.ToString())).FirstOrDefaultAsync(cancellationToken),
        };
    }

    /// <summary>
    /// Ngành hàng của các NCC, theo mã nhóm. Đọc nhóm hàng bỏ filter phạm vi: danh mục nhóm hàng ai cũng xem đủ (người dùng chốt 07/11/2026,
    /// design.md mục 9.2) – chỉ là tên nhóm, không mở mặt hàng ngoài phạm vi.
    /// </summary>
    private async Task<Dictionary<Guid, List<SupplierCategoryRefDto>>> CategoriesOfAsync(IReadOnlyList<Guid> supplierIds, CancellationToken cancellationToken)
    {
        if (supplierIds.Count == 0) return [];
        var rows = await (
                from x in _db.SupplierCategories.AsNoTracking()
                join c in _db.ItemCategories.IgnoreQueryFilters([ProcDbContext.ItemCategoryFilter]) on x.ItemCategoryId equals c.Id
                where supplierIds.Contains(x.SupplierId)
                orderby c.Code
                select new { x.SupplierId, Category = new SupplierCategoryRefDto(c.Id, c.Code, c.Name, c.IsActive) })
            .ToListAsync(cancellationToken);
        return rows.GroupBy(r => r.SupplierId).ToDictionary(g => g.Key, g => g.Select(r => r.Category).ToList());
    }

    /// <summary>Nhóm hàng và mọi nhóm con cháu của nó (cả cây, không lọc phạm vi).</summary>
    private async Task<List<Guid>> CategoryBranchAsync(Guid categoryId, CancellationToken cancellationToken)
    {
        var parents = await _db.ItemCategories.IgnoreQueryFilters([ProcDbContext.ItemCategoryFilter]).AsNoTracking()
            .Select(c => new { c.Id, c.ParentId }).ToListAsync(cancellationToken);
        var children = parents.Where(c => c.ParentId != null).ToLookup(c => c.ParentId!.Value, c => c.Id);
        var branch = new List<Guid>();
        var queue = new Queue<Guid>([categoryId]);
        while (queue.TryDequeue(out var id))
        {
            if (branch.Contains(id)) continue;
            branch.Add(id);
            foreach (var child in children[id]) queue.Enqueue(child);
        }

        return branch;
    }

    /// <summary>
    /// Thay ngành hàng của NCC (design.md mục 5.3): bỏ nhóm không còn chọn, thêm nhóm mới chọn (phải đang dùng); nhóm đã ngừng dùng mà vẫn
    /// giữ thì để nguyên. Không cần duyệt (người dùng chốt 07/11/2026). Mỗi thêm/bỏ là một dòng nhật ký.
    /// </summary>
    /// <param name="supplierId">NCC.</param>
    /// <param name="categoryIds">Nhóm hàng chọn; null = không đổi.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <exception cref="NotFoundException">Khi có nhóm hàng không tồn tại.</exception>
    /// <exception cref="BusinessRuleException">Khi quá 50 nhóm hoặc thêm nhóm đã ngừng dùng.</exception>
    private async Task SetCategoriesAsync(Guid supplierId, IReadOnlyList<Guid>? categoryIds, CancellationToken cancellationToken)
    {
        if (categoryIds is null) return;
        var wanted = categoryIds.Where(id => id != Guid.Empty).Distinct().ToList();
        if (wanted.Count > MaxCategories) throw new BusinessRuleException($"Mỗi NCC tối đa {MaxCategories} ngành hàng.");
        var current = await _db.SupplierCategories.Where(x => x.SupplierId == supplierId).ToListAsync(cancellationToken);
        _db.SupplierCategories.RemoveRange(current.Where(x => !wanted.Contains(x.ItemCategoryId)));
        var added = wanted.Where(id => current.All(x => x.ItemCategoryId != id)).ToList();
        if (added.Count == 0) return;
        var categories = await _db.ItemCategories.IgnoreQueryFilters([ProcDbContext.ItemCategoryFilter])
            .Where(c => added.Contains(c.Id)).ToListAsync(cancellationToken);
        if (categories.Count != added.Count) throw new NotFoundException("Không tìm thấy nhóm hàng đã chọn làm ngành hàng.");
        _db.SupplierCategories.AddRange(categories.Select(c => SupplierCategory.Create(supplierId, c)));
    }

    /// <summary>Yêu cầu duyệt đang chờ của NCC (design.md mục 18.6) – hiện trên ngăn kéo NCC.</summary>
    private async Task<IReadOnlyList<SupplierChangeRequestDto>> PendingRequestsAsync(Guid supplierId, CancellationToken cancellationToken)
    {
        var pending = await _db.SupplierChangeRequests.AsNoTracking()
            .Where(r => r.SupplierId == supplierId && r.Status == SupplierChangeRequestStatus.Submitted)
            .OrderBy(r => r.CreatedAt)
            .ToListAsync(cancellationToken);
        return pending.Count == 0 ? [] : await new SupplierChangeRequestService(_db, _scope, _clock).ToDtosAsync(pending, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<SupplierDetailDto> CreateAsync(SaveSupplierRequest request, CancellationToken cancellationToken)
    {
        await HeadOffice.RequireAsync(_db, _scope, "nhà cung cấp", cancellationToken);
        // Mã do hệ thống cấp theo dải đánh số (design.md mục 5.11) – người dùng không tự đặt.
        CatalogNumberAllocator.RejectManualCode(NumberedCatalogs.Supplier, request.Code);
        var group = await FindGroupAsync(request.GroupId, cancellationToken);
        await using var transaction = await _numbers.BeginAsync(cancellationToken);
        var supplier = Supplier.Create(await _numbers.NextAsync(NumberedCatalogs.Supplier, null, cancellationToken), group, ToInfo(request));
        var deferred = await ApplyAsync(supplier, request, isNew: true, cancellationToken);

        _db.Suppliers.Add(supplier);
        await SetCategoriesAsync(supplier.Id, request.CategoryIds, cancellationToken);
        await SubmitDeferredAsync(supplier, deferred, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        return await GetAsync(supplier.Id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<SupplierDetailDto> UpdateAsync(Guid id, SaveSupplierRequest request, CancellationToken cancellationToken)
    {
        await SharedCatalog.RequireEditAsync(_db, _scope, "nhà cung cấp", Permissions.MasterData.SupplierManage, cancellationToken);
        var supplier = await _db.Suppliers.SingleOrDefaultAsync(s => s.Id == id, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy nhà cung cấp.");
        supplier.Update(ToInfo(request));
        if (request.GroupId != supplier.GroupId) supplier.ChangeGroup(await FindGroupAsync(request.GroupId, cancellationToken));
        var deferred = await ApplyAsync(supplier, request, isNew: false, cancellationToken);
        await SetCategoriesAsync(supplier.Id, request.CategoryIds, cancellationToken);

        await SubmitDeferredAsync(supplier, deferred, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <summary>Thay đổi phải qua duyệt, tách khỏi lần lưu NCC (design.md mục 18.6).</summary>
    private sealed record DeferredChanges(SupplierStatus? Status, string? StatusNote, IReadOnlyList<SupplierBankAccountData>? BankAccounts);

    /// <summary>
    /// Trạng thái, NCC nội bộ, người liên hệ, tài khoản ngân hàng, kiểm tra trùng mã số thuế – chung cho thêm và sửa. Đưa vào Đã duyệt /
    /// Danh sách đen và đổi tài khoản ngân hàng của NCC đã có không áp ngay mà trả về để lập yêu cầu duyệt (quyết định Q7, mục 18.6);
    /// NCC mới khai báo tài khoản ban đầu được ngay (NCC mới ở Tiềm năng, chưa mua được cho tới khi được duyệt).
    /// </summary>
    /// <returns>Thay đổi chờ duyệt.</returns>
    private async Task<DeferredChanges> ApplyAsync(Supplier supplier, SaveSupplierRequest request, bool isNew, CancellationToken cancellationToken)
    {
        var status = ParseStatus(request.Status);
        var deferStatus = supplier.StatusChangeNeedsApproval(status);
        if (deferStatus && status == SupplierStatus.Approved) await EnsureOnboardingNotRequiredAsync(supplier, request.InternalCompanyId, cancellationToken);
        if (!deferStatus) supplier.SetStatus(status, request.StatusNote);
        supplier.SetContacts(request.Contacts.Select(c => new SupplierContactData(c.Id, c.Name, c.Position, c.Phone, c.Email, c.IsPrimary)));
        var banks = Supplier.NormalizeBankAccounts(request.BankAccounts.Select(b =>
            new SupplierBankAccountData(b.Id, b.BankName, b.Branch, b.AccountNumber, b.AccountName, b.IsDefault)));
        var deferBanks = !isNew && supplier.BankAccountsDifferFrom(banks);
        if (!deferBanks) supplier.SetBankAccounts(banks);

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
        return new DeferredChanges(deferStatus ? status : null, request.StatusNote, deferBanks ? banks : null);
    }

    /// <summary>
    /// NCC tạo từ lát G8 trở đi lên Đã duyệt phải qua hồ sơ đánh giá NCC mới (HSN): Phòng cung ứng đánh giá, Văn phòng Ban đề xuất, Hội đồng
    /// duyệt (design.md mục 18.25, người dùng chốt 09/11/2026). Ngoại lệ: NCC nội bộ (công ty thành viên – không cần thẩm định năng lực) và khi
    /// tổng công ty bật tham số <see cref="CompanyParameter.AllowSupplierWithoutOnboarding"/> (chuyển tiếp).
    /// </summary>
    /// <exception cref="BusinessRuleException">Khi NCC mới chưa qua hồ sơ.</exception>
    private async Task EnsureOnboardingNotRequiredAsync(Supplier supplier, Guid? internalCompanyId, CancellationToken cancellationToken)
    {
        if (!supplier.RequiresOnboarding || internalCompanyId is not null) return;
        // Danh mục NCC dùng chung: chỉ tham số của tổng công ty có tác dụng, dù người lưu đang làm việc ở công ty nào – bỏ filter công ty, chỉ đọc
        // đúng một cờ của tổng công ty.
        var allowed = await _db.CompanyParameters.IgnoreQueryFilters([ProcDbContext.CompanyFilter])
            .AnyAsync(p => p.AllowSupplierWithoutOnboarding && _db.Companies.Any(c => c.Id == p.CompanyId && c.IsHeadOffice), cancellationToken);
        if (allowed) return;
        throw new BusinessRuleException(
            $"NCC {supplier.Code} là NCC mới – muốn đưa vào Đã duyệt hãy lập hồ sơ đánh giá NCC mới (màn hình HSN): Phòng cung ứng đánh giá, " +
            "Văn phòng Ban đề xuất, Hội đồng duyệt.");
    }

    /// <summary>Lập yêu cầu duyệt cho phần thay đổi bị giữ lại (chưa lưu – lưu cùng NCC).</summary>
    private async Task SubmitDeferredAsync(Supplier supplier, DeferredChanges deferred, CancellationToken cancellationToken)
    {
        var companyId = _scope.RequireCompany();
        var engine = new ApprovalEngine(_db, _scope, _clock);
        if (deferred.Status is { } status)
        {
            await SupplierChangeRequestService.SubmitAsync(_db, engine, supplier,
                no => SupplierChangeRequest.ForStatus(supplier, companyId, no, status, deferred.StatusNote), cancellationToken);
        }

        if (deferred.BankAccounts is { } banks)
        {
            await SupplierChangeRequestService.SubmitAsync(_db, engine, supplier,
                no => SupplierChangeRequest.ForBankAccounts(supplier, companyId, no, banks), cancellationToken);
        }
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
