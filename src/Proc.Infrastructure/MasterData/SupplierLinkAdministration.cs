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
/// NCC áp dụng cho công ty và NCC – sản phẩm (design.md mục 5.3, 5.9). SupplierCompany đi qua filter công ty,
/// SupplierItem đi qua filter phạm vi nhóm hàng của mặt hàng – không truy vấn nào bỏ qua filter.
/// </summary>
public sealed class SupplierLinkAdministration : ISupplierLinkAdministration
{
    private readonly ProcDbContext _db;
    private readonly ICurrentUserScope _scope;

    /// <summary>
    /// Tạo dịch vụ.
    /// </summary>
    /// <param name="db">DbContext.</param>
    /// <param name="scope">Phạm vi của người thao tác.</param>
    public SupplierLinkAdministration(ProcDbContext db, ICurrentUserScope scope)
    {
        _db = db;
        _scope = scope;
    }

    /// <inheritdoc />
    public async Task<SupplierCompanyDto> GetCompanyLinkAsync(Guid supplierId, CancellationToken cancellationToken)
    {
        var companyId = _scope.RequireCompany();
        if (!await _db.Suppliers.AnyAsync(s => s.Id == supplierId, cancellationToken))
            throw new NotFoundException("Không tìm thấy nhà cung cấp.");
        var companyCode = await _db.Companies.Where(c => c.Id == companyId).Select(c => c.Code).SingleAsync(cancellationToken);
        var link = await _db.SupplierCompanies.AsNoTracking().SingleOrDefaultAsync(x => x.SupplierId == supplierId, cancellationToken);
        var canEdit = _scope.HasPermission(Permissions.MasterData.SupplierManage);
        return link is null
            ? new SupplierCompanyDto(supplierId, companyCode, false, null, null, false, canEdit)
            : new SupplierCompanyDto(supplierId, companyCode, true, link.LocalCode, link.StartDate, link.IsActive, canEdit);
    }

    /// <inheritdoc />
    public async Task<SupplierCompanyDto> SaveCompanyLinkAsync(
        Guid supplierId, SaveSupplierCompanyRequest request, CancellationToken cancellationToken)
    {
        // Mỗi công ty tự quyết định mua của NCC nào – làm tại công ty đang làm việc, không cần ở tổng công ty (design.md mục 5.9).
        var companyId = _scope.RequireCompany();
        var supplier = await _db.Suppliers.AsNoTracking().SingleOrDefaultAsync(s => s.Id == supplierId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy nhà cung cấp.");
        var link = await _db.SupplierCompanies.SingleOrDefaultAsync(x => x.SupplierId == supplierId, cancellationToken);
        if (link is null)
        {
            link = SupplierCompany.Apply(supplier, companyId, request.LocalCode, request.StartDate);
            if (!request.IsActive) link.Update(supplier, request.LocalCode, request.StartDate, isActive: false);
            _db.SupplierCompanies.Add(link);
        }
        else
        {
            link.Update(supplier, request.LocalCode, request.StartDate, request.IsActive);
        }

        if (link.LocalCode is { } localCode)
        {
            var other = await _db.SupplierCompanies.AsNoTracking()
                .Where(x => x.LocalCode == localCode && x.SupplierId != supplierId)
                .Join(_db.Suppliers, x => x.SupplierId, s => s.Id, (x, s) => s.Code)
                .FirstOrDefaultAsync(cancellationToken);
            if (other is not null) throw new BusinessRuleException($"Mã {localCode} đã dùng cho NCC {other} tại công ty này.");
        }

        await _db.SaveChangesAsync(cancellationToken);
        return await GetCompanyLinkAsync(supplierId, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<SupplierItemDto>> ListItemsOfSupplierAsync(Guid supplierId, CancellationToken cancellationToken) =>
        QueryAsync(_db.SupplierItems.Where(x => x.SupplierId == supplierId), byItem: true, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<SupplierItemDto>> ListSuppliersOfItemAsync(Guid itemId, CancellationToken cancellationToken) =>
        QueryAsync(_db.SupplierItems.Where(x => x.ItemId == itemId), byItem: false, cancellationToken);

    /// <inheritdoc />
    public async Task<SupplierItemDto> CreateItemAsync(Guid supplierId, SaveSupplierItemRequest request, CancellationToken cancellationToken)
    {
        await HeadOffice.RequireAsync(_db, _scope, "NCC – sản phẩm", cancellationToken);
        if (!await _db.Suppliers.AnyAsync(s => s.Id == supplierId, cancellationToken))
            throw new NotFoundException("Không tìm thấy nhà cung cấp.");
        var item = await FindItemAsync(request.ItemId, cancellationToken);
        if (await _db.SupplierItems.AnyAsync(x => x.SupplierId == supplierId && x.ItemId == item.Id, cancellationToken))
            throw new BusinessRuleException($"NCC đã có mặt hàng {item.Code}. Sửa dòng đó thay vì thêm mới.");

        var link = SupplierItem.Create(supplierId, item, ToData(request));
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        if (link.IsPreferred) await ClearOtherPreferredAsync(item.Id, link.Id, cancellationToken);
        _db.SupplierItems.Add(link);
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return (await ListItemsOfSupplierAsync(supplierId, cancellationToken)).Single(x => x.Id == link.Id);
    }

    /// <inheritdoc />
    public async Task<SupplierItemDto> UpdateItemAsync(Guid id, SaveSupplierItemRequest request, CancellationToken cancellationToken)
    {
        await HeadOffice.RequireAsync(_db, _scope, "NCC – sản phẩm", cancellationToken);
        var link = await _db.SupplierItems.SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy dòng NCC – sản phẩm.");
        var item = await FindItemAsync(request.ItemId, cancellationToken);
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        if (request.IsPreferred) await ClearOtherPreferredAsync(link.ItemId, link.Id, cancellationToken);
        link.Update(item, ToData(request));
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return (await ListItemsOfSupplierAsync(link.SupplierId, cancellationToken)).Single(x => x.Id == id);
    }

    /// <inheritdoc />
    public async Task DeleteItemAsync(Guid id, CancellationToken cancellationToken)
    {
        await HeadOffice.RequireAsync(_db, _scope, "NCC – sản phẩm", cancellationToken);
        var link = await _db.SupplierItems.SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy dòng NCC – sản phẩm.");
        _db.SupplierItems.Remove(link);
        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Bỏ ưu tiên của NCC khác cùng mặt hàng và lưu ngay, trước khi lưu dòng mới được chọn ưu tiên (cùng giao dịch):
    /// chỉ mục duy nhất "một NCC ưu tiên mỗi mặt hàng" kiểm tra theo từng câu lệnh, nên cái cũ phải được bỏ trước.
    /// Gọi trước khi sửa dòng đang lưu, để lần lưu này chỉ có các câu lệnh bỏ ưu tiên.
    /// </summary>
    private async Task ClearOtherPreferredAsync(Guid itemId, Guid exceptId, CancellationToken cancellationToken)
    {
        var others = await _db.SupplierItems
            .Where(x => x.ItemId == itemId && x.IsPreferred && x.Id != exceptId)
            .ToListAsync(cancellationToken);
        if (others.Count == 0) return;
        foreach (var other in others) other.ClearPreferred();
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<SupplierItemDto>> QueryAsync(IQueryable<SupplierItem> links, bool byItem, CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        var rows = await (
                from x in links.AsNoTracking()
                join s in _db.Suppliers on x.SupplierId equals s.Id
                join i in _db.Items on x.ItemId equals i.Id
                join bu in _db.Uoms on i.BaseUomId equals bu.Id
                join pu in _db.Uoms on x.PurchaseUomId equals pu.Id
                select new { x, s, i, Base = bu.Code, Purchase = pu.Code })
            .ToListAsync(cancellationToken);
        var dtos = rows.Select(r => new SupplierItemDto(
            r.x.Id, r.s.Id, r.s.Code, r.s.Name, r.s.Status.ToString(), r.i.Id, r.i.Code, r.i.Name, r.Base, r.x.SupplierItemCode,
            r.x.PurchaseUomId, r.Purchase, r.x.Moq, r.x.LeadTimeDays, r.x.IsPreferred));
        return (byItem
                ? dtos.OrderBy(d => d.ItemCode, StringComparer.Ordinal)
                : dtos.OrderByDescending(d => d.IsPreferred).ThenBy(d => d.SupplierCode, StringComparer.Ordinal))
            .ToList();
    }

    private async Task<Item> FindItemAsync(Guid id, CancellationToken cancellationToken) =>
        await _db.Items.AsNoTracking().SingleOrDefaultAsync(i => i.Id == id, cancellationToken)
        ?? throw new NotFoundException("Không tìm thấy mặt hàng (hoặc mặt hàng ngoài phạm vi nhóm hàng của bạn).");

    private static SupplierItemData ToData(SaveSupplierItemRequest r) =>
        new(r.SupplierItemCode, r.PurchaseUomId, r.Moq, r.LeadTimeDays, r.IsPreferred);
}
