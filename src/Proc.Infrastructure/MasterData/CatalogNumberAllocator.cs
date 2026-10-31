using Microsoft.EntityFrameworkCore;
using Proc.Domain.Common;
using Proc.Domain.MasterData;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.MasterData;

/// <summary>
/// Cấp mã mới cho danh mục đánh số (design.md mục 5.11): tìm dải (hàng hóa: dải của nhóm hàng, không có thì dòng mặc định), lấy số lớn nhất
/// đang có trong dải + 1. Hai người thêm cùng lúc không trùng mã nhờ khóa tư vấn (advisory lock) theo danh mục + tiền tố, giữ tới hết giao dịch –
/// vì vậy nơi gọi phải mở giao dịch bao cả bước lưu (<see cref="BeginAsync"/>).
/// </summary>
public sealed class CatalogNumberAllocator
{
    private readonly ProcDbContext _db;

    /// <summary>Tạo bộ cấp mã.</summary>
    /// <param name="db">DbContext.</param>
    public CatalogNumberAllocator(ProcDbContext db) => _db = db;

    /// <summary>
    /// Mở giao dịch cho thao tác thêm mới (nếu chưa có giao dịch bên ngoài, ví dụ nhập Excel đã mở sẵn).
    /// </summary>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Giao dịch mới cần Commit; null nếu đang nằm trong giao dịch bên ngoài.</returns>
    public async Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction?> BeginAsync(CancellationToken cancellationToken) =>
        _db.Database.CurrentTransaction is null ? await _db.Database.BeginTransactionAsync(cancellationToken) : null;

    /// <summary>
    /// Báo lỗi khi người dùng tự nhập mã cho danh mục đánh số – mã do hệ thống cấp.
    /// </summary>
    /// <param name="catalog">Danh mục.</param>
    /// <param name="code">Mã người dùng gửi lên.</param>
    /// <exception cref="BusinessRuleException">Khi có mã.</exception>
    public static void RejectManualCode(string catalog, string? code)
    {
        if (!string.IsNullOrWhiteSpace(code))
            throw new BusinessRuleException(
                $"Mã {NumberedCatalogs.All[catalog].Name.ToLowerInvariant()} do hệ thống tự cấp theo dải đã cấu hình – để trống mã.");
    }

    /// <summary>
    /// Mã kế tiếp của danh mục.
    /// </summary>
    /// <param name="catalog">Danh mục (<see cref="NumberedCatalogs"/>).</param>
    /// <param name="category">Nhóm hàng (bắt buộc với hàng hóa).</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Mã mới.</returns>
    /// <exception cref="BusinessRuleException">Khi chưa cấu hình dải hoặc hết dải.</exception>
    /// <exception cref="InvalidOperationException">Khi gọi ngoài giao dịch.</exception>
    public async Task<string> NextAsync(string catalog, ItemCategory? category, CancellationToken cancellationToken)
    {
        if (_db.Database.CurrentTransaction is null) throw new InvalidOperationException("Cấp mã danh mục phải nằm trong giao dịch.");
        var configs = await _db.CatalogNumberings.AsNoTracking().Where(n => n.Catalog == catalog).ToListAsync(cancellationToken);
        var numbering = (catalog == NumberedCatalogs.Item && category is not null ? configs.FirstOrDefault(n => n.CategoryId == category.Id) : null)
            ?? configs.FirstOrDefault(n => n.CategoryId == null)
            ?? throw new BusinessRuleException(
                $"Chưa cấu hình đánh số cho danh mục {NumberedCatalogs.All[catalog].Name.ToLowerInvariant()}" +
                (category is null ? "" : $" (nhóm hàng {category.Code})") + " – vào Hệ thống → Đánh số danh mục để khai báo dải mã.");
        var prefix = numbering.ResolvePrefix(category?.Code);

        // Khóa theo danh mục + tiền tố tới hết giao dịch: người thứ hai chờ người thứ nhất lưu xong rồi mới đọc số lớn nhất.
        var lockKey = $"catalog-number:{catalog}:{prefix}";
        await _db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtext({lockKey}))", cancellationToken);

        // Đọc mã của mọi bản ghi (bỏ filter nhóm hàng – mã trùng ở nhóm người dùng không thấy cũng phải tránh) và bản ghi đang thêm chưa lưu.
        var existing = await CodesAsync(catalog, prefix, cancellationToken);
        return numbering.NextCode(prefix, existing);
    }

    private async Task<List<string>> CodesAsync(string catalog, string prefix, CancellationToken cancellationToken)
    {
        var stored = await StoredCodesAsync(catalog, prefix, cancellationToken);
        IEnumerable<string> pending = catalog switch
        {
            NumberedCatalogs.Supplier => _db.ChangeTracker.Entries<Supplier>().Where(e => e.State == EntityState.Added).Select(e => e.Entity.Code),
            NumberedCatalogs.SupplierGroup => _db.ChangeTracker.Entries<SupplierGroup>().Where(e => e.State == EntityState.Added).Select(e => e.Entity.Code),
            NumberedCatalogs.ItemCategory => _db.ChangeTracker.Entries<ItemCategory>().Where(e => e.State == EntityState.Added).Select(e => e.Entity.Code),
            _ => _db.ChangeTracker.Entries<Item>().Where(e => e.State == EntityState.Added).Select(e => e.Entity.Code),
        };
        return [.. stored, .. pending];
    }

    /// <summary>
    /// Mã đã lưu bắt đầu bằng tiền tố, của mọi bản ghi (bỏ filter nhóm hàng).
    /// </summary>
    /// <param name="catalog">Danh mục.</param>
    /// <param name="prefix">Tiền tố thật.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Các mã.</returns>
    public async Task<List<string>> StoredCodesAsync(string catalog, string prefix, CancellationToken cancellationToken)
    {
        var pattern = LikePatterns.StartsWith(prefix);
        return catalog switch
        {
            NumberedCatalogs.Supplier => await _db.Suppliers.IgnoreQueryFilters().Where(x => EF.Functions.Like(x.Code, pattern, LikePatterns.Escape))
                .Select(x => x.Code).ToListAsync(cancellationToken),
            NumberedCatalogs.SupplierGroup => await _db.SupplierGroups.IgnoreQueryFilters().Where(x => EF.Functions.Like(x.Code, pattern, LikePatterns.Escape))
                .Select(x => x.Code).ToListAsync(cancellationToken),
            NumberedCatalogs.ItemCategory => await _db.ItemCategories.IgnoreQueryFilters().Where(x => EF.Functions.Like(x.Code, pattern, LikePatterns.Escape))
                .Select(x => x.Code).ToListAsync(cancellationToken),
            _ => await _db.Items.IgnoreQueryFilters().Where(x => EF.Functions.Like(x.Code, pattern, LikePatterns.Escape))
                .Select(x => x.Code).ToListAsync(cancellationToken),
        };
    }
}
