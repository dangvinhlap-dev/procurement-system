using Microsoft.EntityFrameworkCore;
using Proc.Application.Common;
using Proc.Application.Organization;
using Proc.Application.Pricing;
using Proc.Domain.Common;
using Proc.Domain.Inventory;
using Proc.Domain.Pricing;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.Pricing;

/// <summary>
/// Bảng giá nội bộ (design.md mục 4, 6.3). Bảng có CompanyId = công ty bán nên mọi truy vấn tự lọc theo công ty đang làm việc:
/// mỗi công ty chỉ thấy và sửa giá mình bán. Mặt hàng đọc qua filter nhóm hàng như mọi nơi.
/// </summary>
public sealed class InternalPriceAdministration : IInternalPriceAdministration
{
    private readonly ProcDbContext _db;
    private readonly ICurrentUserScope _scope;
    private readonly ISystemParameterService _parameters;

    /// <summary>Tạo dịch vụ.</summary>
    /// <param name="db">DbContext.</param>
    /// <param name="scope">Phạm vi người dùng.</param>
    /// <param name="parameters">Tham số công ty (% markup nội bộ mặc định).</param>
    public InternalPriceAdministration(ProcDbContext db, ICurrentUserScope scope, ISystemParameterService parameters)
    {
        _db = db;
        _scope = scope;
        _parameters = parameters;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<InternalPriceDto>> ListAsync(CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        return await Query().ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<InternalPriceDto> CreateAsync(SaveInternalPriceRequest request, CancellationToken cancellationToken)
    {
        var companyId = _scope.RequireCompany();
        var item = await _db.Items.SingleOrDefaultAsync(i => i.Id == request.ItemId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy mặt hàng.");
        if (request.BuyerCompanyId is { } buyer && !await _db.Companies.AnyAsync(c => c.Id == buyer && c.IsActive, cancellationToken))
            throw new NotFoundException("Không tìm thấy công ty mua.");

        var price = InternalPrice.Create(companyId, request.BuyerCompanyId, item, request.UnitPrice, request.VatCode, request.ValidFrom, request.ValidTo, request.Note);
        await EnsureNoOverlapAsync(price, cancellationToken);
        _db.InternalPrices.Add(price);
        await _db.SaveChangesAsync(cancellationToken);
        return await Query(price.Id).SingleAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<InternalPriceDto> UpdateAsync(Guid id, SaveInternalPriceRequest request, CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        var price = await _db.InternalPrices.SingleOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy giá nội bộ.");
        // Hàng ngoài nhóm được phép như không tồn tại – không cho sửa giá của nó.
        if (!await _db.Items.AnyAsync(i => i.Id == price.ItemId, cancellationToken)) throw new NotFoundException("Không tìm thấy giá nội bộ.");
        price.Update(request.UnitPrice, request.VatCode, request.ValidFrom, request.ValidTo, request.Note);
        await EnsureNoOverlapAsync(price, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return await Query(id).SingleAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ResolvedInternalPriceDto>> ResolveAsync(
        Guid buyerCompanyId, DateOnly date, IReadOnlyList<Guid> itemIds, CancellationToken cancellationToken)
    {
        var sellerCompanyId = _scope.RequireCompany();
        // Công ty đang làm việc là công ty bán – cùng cách tra với PO mua nội bộ (lát X7).
        return (await InternalPriceLookup.ResolveAsync(_db, sellerCompanyId, buyerCompanyId, date, itemIds, cancellationToken))
            .Select(kv => new ResolvedInternalPriceDto(kv.Key, kv.Value.UnitPrice, kv.Value.VatCode))
            .ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<InternalPriceSuggestionDto>> SuggestAsync(IReadOnlyList<Guid> itemIds, CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        if (itemIds.Count == 0) return [];
        // Chỉ kỳ tháng đã đóng (Closed/Locked) mới có giá vốn chính thức (design.md mục 7.4). Lấy theo từng mặt hàng: kỳ đóng gần
        // nhất có giá vốn của mặt hàng đó – mặt hàng không phát sinh ở tháng vừa đóng vẫn có gợi ý từ tháng trước.
        var costs = await (
                from c in _db.ItemPeriodCosts.AsNoTracking()
                join p in _db.InventoryPeriods.AsNoTracking() on c.PeriodId equals p.Id
                join i in _db.Items on c.ItemId equals i.Id
                where itemIds.Contains(c.ItemId) && p.Month > 0 && (p.Status == PeriodStatus.Closed || p.Status == PeriodStatus.Locked)
                select new { c.ItemId, c.UnitCost, p.Year, p.Month })
            .ToListAsync(cancellationToken);
        if (costs.Count == 0) return [];

        var markup = await _parameters.InternalMarkupPercentAsync(cancellationToken);
        return costs
            .GroupBy(c => c.ItemId)
            .Select(g => g.OrderByDescending(c => c.Year).ThenByDescending(c => c.Month).First())
            .Select(c => new InternalPriceSuggestionDto(c.ItemId, c.UnitCost, c.Year, c.Month, markup, InternalPrice.Suggest(c.UnitCost, markup)))
            .ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<GroupCompanyDto>> OtherCompaniesAsync(CancellationToken cancellationToken)
    {
        var companyId = _scope.RequireCompany();
        return await _db.Companies.AsNoTracking()
            .Where(c => c.IsActive && c.Id != companyId)
            .OrderBy(c => c.Code)
            .Select(c => new GroupCompanyDto(c.Id, c.Code, c.Name))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Cùng công ty mua (hoặc cùng "mọi công ty"), cùng mặt hàng thì khoảng hiệu lực không được chồng nhau – nếu chồng thì một ngày
    /// có hai giá, phiếu xuất không biết lấy giá nào.
    /// </summary>
    private async Task EnsureNoOverlapAsync(InternalPrice price, CancellationToken cancellationToken)
    {
        var others = await _db.InternalPrices.AsNoTracking()
            .Where(p => p.Id != price.Id && p.ItemId == price.ItemId && p.BuyerCompanyId == price.BuyerCompanyId)
            .ToListAsync(cancellationToken);
        if (others.FirstOrDefault(o => o.Overlaps(price.ValidFrom, price.ValidTo)) is { } clash)
            throw new BusinessRuleException(
                $"Đã có giá cùng mặt hàng, cùng công ty mua hiệu lực từ {clash.ValidFrom:dd/MM/yyyy}" +
                (clash.ValidTo is { } to ? $" đến {to:dd/MM/yyyy}" : "") + " – chồng khoảng hiệu lực. Đặt ngày hết hiệu lực cho giá cũ trước.");
    }

    /// <summary>Truy vấn giá bán nội bộ kèm mã/tên mặt hàng, ĐVT; truyền id để lấy một dòng.</summary>
    private IQueryable<InternalPriceDto> Query(Guid? id = null) =>
        from p in _db.InternalPrices.AsNoTracking()
        where id == null || p.Id == id
        join i in _db.Items on p.ItemId equals i.Id
        join u in _db.Uoms on i.BaseUomId equals u.Id
        join c in _db.Companies on p.BuyerCompanyId equals c.Id into cj
        from c in cj.DefaultIfEmpty()
        orderby i.Code, c.Code, p.ValidFrom
        select new InternalPriceDto(
            p.Id, p.BuyerCompanyId, c != null ? c.Code : null, p.ItemId, i.Code, i.Name, u.Code, p.UnitPrice, p.VatCode, p.ValidFrom, p.ValidTo, p.Note);
}
