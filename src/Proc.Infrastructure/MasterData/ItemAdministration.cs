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
/// Quản lý hàng hóa – dịch vụ (design.md mục 5.2, 5.7). Mọi truy vấn qua filter phạm vi nhóm hàng của Item và ItemCategory:
/// hàng thuộc nhóm ngoài phạm vi, hay chọn nhóm ngoài phạm vi khi lưu, đều như không tồn tại (404).
/// </summary>
public sealed class ItemAdministration : IItemAdministration
{
    /// <summary>Số dòng tối đa mỗi lần tìm – danh mục tới ~100.000 mã (design.md mục 1.3, câu 9), không tải hết về trình duyệt.</summary>
    public const int SearchLimit = 5000;

    private readonly ProcDbContext _db;
    private readonly ICurrentUserScope _scope;

    /// <summary>
    /// Tạo dịch vụ.
    /// </summary>
    /// <param name="db">DbContext.</param>
    /// <param name="scope">Phạm vi của người thao tác.</param>
    public ItemAdministration(ProcDbContext db, ICurrentUserScope scope)
    {
        _db = db;
        _scope = scope;
    }

    /// <inheritdoc />
    public async Task<ItemListResult> SearchAsync(ItemQuery query, CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        var items = _db.Items.AsNoTracking();
        if (query.ActiveOnly) items = items.Where(i => i.IsActive);
        if (!string.IsNullOrWhiteSpace(query.Type))
        {
            var type = ParseType(query.Type);
            items = items.Where(i => i.Type == type);
        }

        if (query.CategoryId is { } categoryId)
        {
            // Chọn nhóm cha thì gồm cả hàng của mọi nhóm con.
            var parents = await _db.ItemCategories.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.ParentId, cancellationToken);
            var ids = parents.Keys.Where(id => IsUnder(id, categoryId, parents)).ToList();
            items = items.Where(i => ids.Contains(i.CategoryId));
        }

        // Mỗi từ khóa (đã bỏ dấu) phải có trong chuỗi tìm kiếm – gõ "bu long m8" tìm được "Bu lông M8".
        foreach (var word in SearchText.Normalize(query.Search).Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var pattern = LikePatterns.Contains(word);
            items = items.Where(i => EF.Functions.Like(i.SearchText, pattern, LikePatterns.Escape));
        }

        var rows = await (
                from i in items
                join c in _db.ItemCategories on i.CategoryId equals c.Id
                join u in _db.Uoms on i.BaseUomId equals u.Id
                orderby i.Code
                select new ItemListItemDto(
                    i.Id, i.Code, i.Name, i.Type.ToString(), i.CategoryId, c.Code, c.Name, u.Code,
                    i.Specification, i.Brand, i.Origin, i.Barcode, i.VatCode, i.IsActive))
            .Take(SearchLimit + 1)
            .ToListAsync(cancellationToken);

        var truncated = rows.Count > SearchLimit;
        return new ItemListResult(truncated ? rows[..SearchLimit] : rows, truncated, SearchLimit);
    }

    /// <inheritdoc />
    public async Task<ItemDetailDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var canEdit = _scope.HasPermission(Permissions.MasterData.ItemManage)
            && await HeadOffice.IsWorkingAtAsync(_db, _scope, cancellationToken);
        var item = await _db.Items.AsNoTracking().SingleOrDefaultAsync(i => i.Id == id, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy mặt hàng.");
        var uomIds = item.Conversions.Select(c => c.UomId).ToList();
        var uoms = await _db.Uoms.AsNoTracking().Where(u => uomIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, cancellationToken);

        var conversions = item.Conversions
            .Select(c => new ItemUomConversionDto(c.UomId, uoms[c.UomId].Code, uoms[c.UomId].Name, c.Factor))
            .OrderBy(c => c.Factor)
            .ToList();
        return new ItemDetailDto(
            item.Id, item.Code, item.Name, item.EnglishName, item.Type.ToString(), item.CategoryId, item.BaseUomId,
            item.Specification, item.Brand, item.Origin, item.Barcode, item.VatCode, item.TechnicalSpecs,
            item.TrackLot, item.TrackExpiry, item.MinStock, item.MaxStock, item.IsActive, conversions, canEdit);
    }

    /// <inheritdoc />
    public async Task<ItemDetailDto> CreateAsync(CreateItemRequest request, CancellationToken cancellationToken)
    {
        await HeadOffice.RequireAsync(_db, _scope, "hàng hóa", cancellationToken);
        var type = ParseType(request.Type);
        var item = Item.Create(
            request.Code, type,
            await FindCategoryAsync(request.CategoryId, cancellationToken),
            await FindUomAsync(request.BaseUomId, cancellationToken),
            new ItemDescription(request.Name, request.EnglishName, request.Specification, request.Brand, request.Origin,
                request.Barcode, request.VatCode, request.TechnicalSpecs));
        item.SetStockControl(type, request.TrackLot, request.TrackExpiry, request.MinStock, request.MaxStock);
        item.SetConversions(await ResolveConversionsAsync(request.Conversions, cancellationToken));

        if (await _db.Items.AnyAsync(i => i.Code == item.Code, cancellationToken))
            throw new BusinessRuleException($"Mã hàng {item.Code} đã có.");
        _db.Items.Add(item);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(item.Id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ItemDetailDto> UpdateAsync(Guid id, UpdateItemRequest request, CancellationToken cancellationToken)
    {
        await HeadOffice.RequireAsync(_db, _scope, "hàng hóa", cancellationToken);
        var item = await _db.Items.SingleOrDefaultAsync(i => i.Id == id, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy mặt hàng.");
        var type = ParseType(request.Type);

        item.Describe(new ItemDescription(request.Name, request.EnglishName, request.Specification, request.Brand,
            request.Origin, request.Barcode, request.VatCode, request.TechnicalSpecs));
        if (request.CategoryId != item.CategoryId) item.ChangeCategory(await FindCategoryAsync(request.CategoryId, cancellationToken));
        // Quy đổi mới đặt trước khi đổi ĐVT cơ sở: đổi Hộp ↔ Thùng trong một lần lưu không bị báo trùng với quy đổi cũ.
        var conversions = await ResolveConversionsAsync(request.Conversions, cancellationToken);
        if (request.BaseUomId != item.BaseUomId)
        {
            var oldBaseId = item.BaseUomId;
            item.SetConversions(conversions.Where(c => c.Uom.Id != request.BaseUomId && c.Uom.Id != oldBaseId));
            item.ChangeBaseUom(await FindUomAsync(request.BaseUomId, cancellationToken));
        }

        item.SetConversions(conversions);
        item.SetStockControl(type, request.TrackLot, request.TrackExpiry, request.MinStock, request.MaxStock);
        if (request.IsActive) item.Activate();
        else item.Deactivate();

        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    private async Task<List<(Uom Uom, decimal Factor)>> ResolveConversionsAsync(
        IReadOnlyList<ItemUomConversionInput> inputs, CancellationToken cancellationToken)
    {
        var ids = inputs.Select(c => c.UomId).Distinct().ToList();
        var uoms = await _db.Uoms.Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, cancellationToken);
        return inputs
            .Select(c => (uoms.GetValueOrDefault(c.UomId) ?? throw new NotFoundException("Không tìm thấy đơn vị tính quy đổi."), c.Factor))
            .ToList();
    }

    private async Task<ItemCategory> FindCategoryAsync(Guid id, CancellationToken cancellationToken) =>
        await _db.ItemCategories.SingleOrDefaultAsync(c => c.Id == id, cancellationToken)
        ?? throw new NotFoundException("Không tìm thấy nhóm hàng (hoặc nhóm ngoài phạm vi của bạn).");

    private async Task<Uom> FindUomAsync(Guid id, CancellationToken cancellationToken) =>
        await _db.Uoms.SingleOrDefaultAsync(u => u.Id == id, cancellationToken)
        ?? throw new NotFoundException("Không tìm thấy đơn vị tính.");

    private static ItemType ParseType(string? type) =>
        Enum.TryParse<ItemType>(type, ignoreCase: true, out var value) && Enum.IsDefined(value)
            ? value
            : throw new BusinessRuleException("Loại hàng phải là Inventory, Service, Asset hoặc Consumable.");

    private static bool IsUnder(Guid id, Guid ancestorId, Dictionary<Guid, Guid?> parents)
    {
        var visited = new HashSet<Guid>();
        for (Guid? current = id; current is { } c && visited.Add(c); current = parents.GetValueOrDefault(c))
        {
            if (c == ancestorId) return true;
        }

        return false;
    }

}
