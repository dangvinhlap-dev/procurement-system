using Microsoft.EntityFrameworkCore;
using Proc.Application.Common;
using Proc.Application.Documents;
using Proc.Application.Pricing;
using Proc.Domain.Common;
using Proc.Domain.Documents;
using Proc.Domain.MasterData;
using Proc.Domain.Organization;
using Proc.Domain.Pricing;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.Pricing;

/// <summary>
/// Bảng giá NCC (design.md mục 8.1). Bảng giá thuộc công ty lập nên tự lọc theo công ty đang làm việc; bảng giá
/// <c>IsGroupWide</c> của tổng công ty thì mọi công ty đọc được (chỉ đọc) – các truy vấn đó bỏ filter công ty có điều kiện
/// IsGroupWide, ghi rõ tại chỗ.
/// </summary>
public sealed class PriceListService : IPriceListService
{
    private readonly ProcDbContext _db;
    private readonly ICurrentUserScope _scope;
    private readonly IDocumentNumberService _numbers;
    private readonly IClock _clock;

    /// <summary>Tạo dịch vụ.</summary>
    /// <param name="db">DbContext.</param>
    /// <param name="scope">Phạm vi người dùng.</param>
    /// <param name="numbers">Đánh số chứng từ.</param>
    /// <param name="clock">Đồng hồ.</param>
    public PriceListService(ProcDbContext db, ICurrentUserScope scope, IDocumentNumberService numbers, IClock clock)
    {
        _db = db;
        _scope = scope;
        _numbers = numbers;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PriceListListItemDto>> ListAsync(CancellationToken cancellationToken)
    {
        var companyId = _scope.RequireCompany();
        return await (
                from p in Visible().AsNoTracking()
                join c in _db.Companies on p.CompanyId equals c.Id
                join s in _db.Suppliers on p.SupplierId equals s.Id
                orderby p.ValidFrom descending, p.No descending
                select new PriceListListItemDto(
                    p.Id, p.No, p.Title, c.Code, p.IsGroupWide, s.Code, s.Name, p.ValidFrom, p.ValidTo, p.Status.ToString(), p.Lines.Count,
                    p.CompanyId == companyId))
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PriceListDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        var list = await Visible().AsNoTracking().Include(p => p.Lines).SingleOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy bảng giá.");
        return await ToDtoAsync(list, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PriceListDto> CreateAsync(SavePriceListRequest request, CancellationToken cancellationToken)
    {
        var companyId = _scope.RequireCompany();
        await CheckHeaderAsync(request, cancellationToken);
        var lines = await ResolveLinesAsync(request.Lines, cancellationToken);
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var no = await _numbers.NextAsync(DocumentTypes.PriceList, request.ValidFrom, cancellationToken);
        var list = SupplierPriceList.Create(companyId, no, ToHeader(request));
        list.SetLines(lines);
        _db.SupplierPriceLists.Add(list);
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetAsync(list.Id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PriceListDto> UpdateAsync(Guid id, SavePriceListRequest request, CancellationToken cancellationToken)
    {
        var list = await LoadOwnAsync(id, cancellationToken);
        await EnsureItemsVisibleAsync(list, cancellationToken);
        await CheckHeaderAsync(request, cancellationToken);
        var lines = await ResolveLinesAsync(request.Lines, cancellationToken);
        list.Update(ToHeader(request));
        list.SetLines(lines);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PriceListDto> ActivateAsync(Guid id, CancellationToken cancellationToken)
    {
        var list = await LoadOwnAsync(id, cancellationToken);
        await EnsureItemsVisibleAsync(list, cancellationToken);
        // Kiểm tra lại lúc đưa vào hiệu lực: NCC có thể đã vào danh sách đen từ lúc soạn nháp.
        await CheckSupplierAsync(list.SupplierId, cancellationToken);
        list.Activate(_clock.UtcNow);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PriceListDto> DeactivateAsync(Guid id, CancellationToken cancellationToken)
    {
        var list = await LoadOwnAsync(id, cancellationToken);
        list.Deactivate();
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ResolvedSupplierPriceDto>> ResolveAsync(
        Guid supplierId, DateOnly date, IReadOnlyList<Guid> itemIds, CancellationToken cancellationToken)
    {
        var companyId = _scope.RequireCompany();
        var rows = await (
                from p in Visible().AsNoTracking()
                where p.SupplierId == supplierId && p.Status == PriceListStatus.Active && p.ValidFrom <= date && (p.ValidTo == null || p.ValidTo >= date)
                from l in p.Lines
                where itemIds.Contains(l.ItemId)
                join i in _db.Items on l.ItemId equals i.Id
                select new
                {
                    p.Id, p.No, Own = p.CompanyId == companyId, p.ValidFrom, p.ActivatedAt, l.ItemId, l.UomId, l.UomFactor, l.MinQty, l.UnitPrice,
                    l.DiscountPct, VatCode = l.VatCode ?? i.VatCode,
                })
            .ToListAsync(cancellationToken);
        // Mỗi mặt hàng lấy một bảng giá: bảng của công ty thắng bảng toàn tập đoàn (thỏa thuận riêng), rồi bảng hiệu lực mới nhất.
        return rows
            .GroupBy(r => r.ItemId)
            .SelectMany(g =>
            {
                var chosen = g.OrderByDescending(r => r.Own).ThenByDescending(r => r.ValidFrom).ThenByDescending(r => r.ActivatedAt).First().Id;
                return g.Where(r => r.Id == chosen);
            })
            .OrderBy(r => r.ItemId).ThenBy(r => r.UomFactor).ThenBy(r => r.MinQty)
            .Select(r => new ResolvedSupplierPriceDto(r.ItemId, r.UomId, r.UomFactor, r.MinQty, r.UnitPrice, r.DiscountPct, r.VatCode, r.No))
            .ToList();
    }

    /// <summary>
    /// Bảng giá đọc được ở công ty đang làm việc: của công ty mình (filter công ty) cộng bảng toàn tập đoàn đã đưa vào hiệu lực của
    /// công ty khác. Bỏ filter công ty ở nhánh thứ hai vì bảng toàn tập đoàn áp dụng cho mọi công ty (design.md mục 8.1); nháp của
    /// công ty khác vẫn không thấy.
    /// </summary>
    private IQueryable<SupplierPriceList> Visible()
    {
        var companyId = _scope.RequireCompany();
        return _db.SupplierPriceLists.IgnoreQueryFilters([ProcDbContext.CompanyFilter])
            .Where(p => p.CompanyId == companyId || (p.IsGroupWide && p.Status != PriceListStatus.Draft));
    }

    private async Task<SupplierPriceList> LoadOwnAsync(Guid id, CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        var list = await _db.SupplierPriceLists.Include(p => p.Lines).SingleOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (list is not null) return list;
        // Bảng toàn tập đoàn của công ty khác: thấy được nhưng chỉ công ty lập mới sửa.
        if (await Visible().AnyAsync(p => p.Id == id, cancellationToken))
            throw new ForbiddenAccessException("Bảng giá toàn tập đoàn do công ty khác lập – chỉ công ty lập mới sửa được.");
        throw new NotFoundException("Không tìm thấy bảng giá.");
    }

    /// <summary>
    /// NCC không ở danh sách đen (design.md mục 5.8); "toàn tập đoàn" chỉ khi đang làm việc ở tổng công ty – thỏa thuận áp cho mọi
    /// pháp nhân thì do tổng công ty quản lý (design.md mục 8.1).
    /// </summary>
    private async Task CheckHeaderAsync(SavePriceListRequest request, CancellationToken cancellationToken)
    {
        await CheckSupplierAsync(request.SupplierId, cancellationToken);
        if (request.IsGroupWide)
        {
            var companyId = _scope.RequireCompany();
            if (!await _db.Companies.AnyAsync(c => c.Id == companyId && c.IsHeadOffice, cancellationToken))
                throw new BusinessRuleException("Chỉ tổng công ty mới lập được bảng giá áp dụng toàn tập đoàn.");
        }
    }

    private async Task CheckSupplierAsync(Guid supplierId, CancellationToken cancellationToken)
    {
        var supplier = await _db.Suppliers.AsNoTracking().SingleOrDefaultAsync(s => s.Id == supplierId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy nhà cung cấp.");
        if (supplier.Status == SupplierStatus.Blacklisted)
            throw new BusinessRuleException($"NCC {supplier.Code} đang ở danh sách đen – không lập bảng giá được.");
    }

    private async Task<List<(PriceListLineData, Item)>> ResolveLinesAsync(IReadOnlyList<SavePriceListLineRequest> lines, CancellationToken cancellationToken)
    {
        var ids = lines.Select(l => l.ItemId).Distinct().ToList();
        var items = await _db.Items.AsNoTracking().Include(i => i.Conversions).Where(i => ids.Contains(i.Id)).ToDictionaryAsync(i => i.Id, cancellationToken);
        return lines.Select((l, index) => (
                new PriceListLineData(l.Id, l.ItemId, l.UomId, l.MinQty, l.UnitPrice, l.DiscountPct, l.VatCode, l.Note),
                items.GetValueOrDefault(l.ItemId)
                ?? throw new NotFoundException($"Dòng {index + 1}: không tìm thấy mặt hàng (hoặc mặt hàng ngoài phạm vi nhóm hàng của bạn).")))
            .ToList();
    }

    private async Task EnsureItemsVisibleAsync(SupplierPriceList list, CancellationToken cancellationToken)
    {
        var ids = list.Lines.Select(l => l.ItemId).Distinct().ToList();
        if (await _db.Items.CountAsync(i => ids.Contains(i.Id), cancellationToken) < ids.Count)
            throw new ForbiddenAccessException("Bảng giá có mặt hàng ngoài phạm vi nhóm hàng của bạn nên bạn không thao tác được.");
    }

    private async Task<PriceListDto> ToDtoAsync(SupplierPriceList list, CancellationToken cancellationToken)
    {
        var companyId = _scope.RequireCompany();
        var itemIds = list.Lines.Select(l => l.ItemId).Distinct().ToList();
        var items = await _db.Items.AsNoTracking().Where(i => itemIds.Contains(i.Id)).Select(i => new { i.Id, i.Code, i.Name }).ToDictionaryAsync(i => i.Id, cancellationToken);
        var uomIds = list.Lines.Select(l => l.UomId).Distinct().ToList();
        var uoms = await _db.Uoms.AsNoTracking().Where(u => uomIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Code, cancellationToken);
        var supplier = await _db.Suppliers.AsNoTracking().Where(s => s.Id == list.SupplierId).Select(s => new { s.Code, s.Name }).SingleAsync(cancellationToken);
        var company = await _db.Companies.AsNoTracking().Where(c => c.Id == list.CompanyId).Select(c => c.Code).SingleAsync(cancellationToken);
        var createdBy = list.CreatedBy is { } uid
            ? await _db.Users.AsNoTracking().Where(u => u.Id == uid).Select(u => u.FullName).SingleOrDefaultAsync(cancellationToken)
            : null;
        var lines = list.Lines.OrderBy(l => l.LineNo).Select(l =>
        {
            var item = items.GetValueOrDefault(l.ItemId);
            return new PriceListLineDto(
                l.Id, l.LineNo, l.ItemId, item?.Code ?? "(ngoài phạm vi)", item?.Name ?? "", l.UomId, uoms.GetValueOrDefault(l.UomId) ?? "",
                l.UomFactor, l.MinQty, l.UnitPrice, l.DiscountPct, l.VatCode, l.BaseNetPrice, l.Note);
        }).ToList();

        var own = list.CompanyId == companyId;
        string? readOnly = !own ? "Bảng giá toàn tập đoàn do công ty khác lập – chỉ xem."
            : items.Count < itemIds.Count ? "Bảng giá có mặt hàng ngoài phạm vi nhóm hàng của bạn nên chỉ xem được."
            : !_scope.HasPermission(Permissions.Pricing.PriceListManage) ? "Bạn không có quyền thêm, sửa bảng giá." : null;
        var actions = new PriceListActions(
            list.Status == PriceListStatus.Draft && readOnly is null,
            list.Status == PriceListStatus.Draft && readOnly is null,
            list.Status == PriceListStatus.Active && readOnly is null,
            readOnly);
        return new PriceListDto(
            list.Id, list.No, list.Title, list.CompanyId, company, list.IsGroupWide, list.SupplierId, supplier.Code, supplier.Name, list.ValidFrom,
            list.ValidTo, list.CurrencyCode, list.DeliveryTerms, list.Note, list.Status.ToString(), list.CreatedAt, createdBy, list.ActivatedAt, lines, actions);
    }

    private static PriceListHeader ToHeader(SavePriceListRequest r) =>
        new(r.SupplierId, r.Title, r.IsGroupWide, r.ValidFrom, r.ValidTo, r.DeliveryTerms, r.Note);
}
