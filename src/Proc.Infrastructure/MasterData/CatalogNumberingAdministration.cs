using Microsoft.EntityFrameworkCore;
using Proc.Application.Common;
using Proc.Application.MasterData;
using Proc.Domain.Common;
using Proc.Domain.MasterData;
using Proc.Infrastructure.Organization;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.MasterData;

/// <summary>
/// Cấu hình dải đánh số danh mục (design.md mục 5.11). Bảng dùng chung toàn tập đoàn nên lệnh ghi bắt buộc làm việc ở tổng công ty; mọi
/// thay đổi vào nhật ký qua interceptor ([Auditable]).
/// </summary>
public sealed class CatalogNumberingAdministration : ICatalogNumberingAdministration
{
    private const string What = "đánh số danh mục";

    private readonly ProcDbContext _db;
    private readonly ICurrentUserScope _scope;
    private readonly CatalogNumberAllocator _allocator;

    /// <summary>Tạo dịch vụ.</summary>
    /// <param name="db">DbContext.</param>
    /// <param name="scope">Phạm vi người dùng.</param>
    /// <param name="allocator">Bộ cấp mã (đọc mã đang có để xem trước mã kế tiếp).</param>
    public CatalogNumberingAdministration(ProcDbContext db, ICurrentUserScope scope, CatalogNumberAllocator allocator)
    {
        _db = db;
        _scope = scope;
        _allocator = allocator;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CatalogNumberingDto>> ListAsync(CancellationToken cancellationToken)
    {
        var rows = await _db.CatalogNumberings.AsNoTracking().ToListAsync(cancellationToken);
        var categoryIds = rows.Where(r => r.CategoryId != null).Select(r => r.CategoryId!.Value).ToList();
        var categories = await _db.ItemCategories.IgnoreQueryFilters().AsNoTracking().Where(c => categoryIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, cancellationToken);
        var result = new List<CatalogNumberingDto>();
        foreach (var row in rows)
        {
            var category = row.CategoryId is { } id ? categories.GetValueOrDefault(id) : null;
            result.Add(await ToDtoAsync(row, category, cancellationToken));
        }

        return result
            .OrderBy(r => r.Catalog, StringComparer.Ordinal)
            .ThenBy(r => r.CategoryCode is not null)
            .ThenBy(r => r.CategoryCode, StringComparer.Ordinal)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<CatalogNumberingDto> CreateAsync(SaveCatalogNumberingRequest request, CancellationToken cancellationToken)
    {
        await HeadOffice.RequireAsync(_db, _scope, What, cancellationToken);
        var category = request.CategoryId is { } categoryId
            ? await _db.ItemCategories.IgnoreQueryFilters().SingleOrDefaultAsync(c => c.Id == categoryId, cancellationToken)
              ?? throw new NotFoundException("Không tìm thấy nhóm hàng.")
            : null;
        var numbering = CatalogNumbering.Create(request.Catalog, request.CategoryId, request.Prefix ?? string.Empty, request.Digits, request.FromNumber, request.ToNumber);
        if (await _db.CatalogNumberings.AnyAsync(n => n.Catalog == numbering.Catalog && n.CategoryId == numbering.CategoryId, cancellationToken))
            throw new BusinessRuleException(category is null
                ? $"Danh mục {NumberedCatalogs.All[numbering.Catalog].Name.ToLowerInvariant()} đã có dải đánh số – sửa dải đó."
                : $"Nhóm hàng {category.Code} đã có dải mã hàng riêng – sửa dải đó.");
        _db.CatalogNumberings.Add(numbering);
        await _db.SaveChangesAsync(cancellationToken);
        return await ToDtoAsync(numbering, category, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<CatalogNumberingDto> UpdateAsync(Guid id, SaveCatalogNumberingRequest request, CancellationToken cancellationToken)
    {
        await HeadOffice.RequireAsync(_db, _scope, What, cancellationToken);
        var numbering = await _db.CatalogNumberings.SingleOrDefaultAsync(n => n.Id == id, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy dải đánh số.");
        numbering.Update(request.Prefix ?? string.Empty, request.Digits, request.FromNumber, request.ToNumber);
        await _db.SaveChangesAsync(cancellationToken);
        var category = numbering.CategoryId is { } categoryId
            ? await _db.ItemCategories.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(c => c.Id == categoryId, cancellationToken)
            : null;
        return await ToDtoAsync(numbering, category, cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        await HeadOffice.RequireAsync(_db, _scope, What, cancellationToken);
        var numbering = await _db.CatalogNumberings.SingleOrDefaultAsync(n => n.Id == id, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy dải đánh số.");
        _db.CatalogNumberings.Remove(numbering);
        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Dựng DTO kèm ví dụ mã đầu dải và mã kế tiếp (đọc mã đang có, không khóa – chỉ để xem).</summary>
    private async Task<CatalogNumberingDto> ToDtoAsync(CatalogNumbering n, ItemCategory? category, CancellationToken cancellationToken)
    {
        var hasToken = n.Prefix.Contains(NumberedCatalogs.CategoryToken, StringComparison.Ordinal);
        var samplePrefix = n.ResolvePrefix(category?.Code ?? "VT");
        var sample = samplePrefix + n.FromNumber.ToString(new string('0', n.Digits), System.Globalization.CultureInfo.InvariantCulture);
        string? next = null;
        // Dòng mặc định của hàng hóa có {NHOM}: mỗi nhóm một bộ đếm riêng nên không có một "mã kế tiếp" chung.
        if (!hasToken || category is not null)
        {
            var prefix = n.ResolvePrefix(category?.Code);
            var codes = await _allocator.StoredCodesAsync(n.Catalog, prefix, cancellationToken);
            try
            {
                next = n.NextCode(prefix, codes);
            }
            catch (BusinessRuleException)
            {
                next = null; // Hết dải: để trống, màn hình báo "Hết dải".
            }
        }

        return new CatalogNumberingDto(
            n.Id, n.Catalog, NumberedCatalogs.All[n.Catalog].Name, n.CategoryId, category?.Code, category?.Name, n.Prefix, n.Digits,
            n.FromNumber, n.ToNumber, sample, next);
    }
}
