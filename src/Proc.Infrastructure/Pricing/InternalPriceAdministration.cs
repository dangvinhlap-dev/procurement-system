using Microsoft.EntityFrameworkCore;
using Proc.Application.Common;
using Proc.Application.Pricing;
using Proc.Domain.Common;
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

    /// <summary>Tạo dịch vụ.</summary>
    /// <param name="db">DbContext.</param>
    /// <param name="scope">Phạm vi người dùng.</param>
    public InternalPriceAdministration(ProcDbContext db, ICurrentUserScope scope)
    {
        _db = db;
        _scope = scope;
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
        _scope.RequireCompany();
        var candidates = await (
                from p in _db.InternalPrices.AsNoTracking()
                join i in _db.Items on p.ItemId equals i.Id
                where itemIds.Contains(p.ItemId)
                      && (p.BuyerCompanyId == null || p.BuyerCompanyId == buyerCompanyId)
                      && p.ValidFrom <= date && (p.ValidTo == null || p.ValidTo >= date)
                select new { p.ItemId, p.BuyerCompanyId, p.UnitPrice, VatCode = p.VatCode ?? i.VatCode, p.ValidFrom })
            .ToListAsync(cancellationToken);
        // Giá riêng cho công ty mua thắng giá chung; trong cùng loại không có chồng hiệu lực nên mỗi loại tối đa một dòng.
        return candidates
            .GroupBy(c => c.ItemId)
            .Select(g => g.OrderByDescending(c => c.BuyerCompanyId.HasValue).ThenByDescending(c => c.ValidFrom).First())
            .Select(c => new ResolvedInternalPriceDto(c.ItemId, c.UnitPrice, c.VatCode))
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
