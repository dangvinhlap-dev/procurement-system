using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Proc.Infrastructure.Persistence;
using Proc.Infrastructure.Persistence.Migrations;

namespace Proc.IntegrationTests;

/// <summary>
/// Migration tạo dải đánh số mặc định (design.md mục 5.11): máy chạy thật nâng cấp xong vẫn thêm được danh mục; chỉ thêm cho danh mục chưa có
/// dải mặc định, không ghi đè dải người dùng đã sửa, chạy lại không sinh trùng.
/// </summary>
[Collection(ApiCollection.Name)]
public class DefaultCatalogNumberingMigrationTests
{
    private readonly ProcApiFactory _factory;

    public DefaultCatalogNumberingMigrationTests(ProcApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ThieuDaiMacDinh_TaoLai_KhongGhiDeDaiDaCo()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProcDbContext>();
        var defaults = db.CatalogNumberings.IgnoreQueryFilters().AsNoTracking().Where(n => n.CategoryId == null);
        var before = await defaults.ToDictionaryAsync(n => n.Catalog, Ct);
        var group = before["supplier-group"];
        var supplier = before["supplier"];

        // Giả lập máy chạy thật: chưa có dải nhóm NCC.
        await db.Database.ExecuteSqlRawAsync("DELETE FROM \"CatalogNumbering\" WHERE \"Id\" = {0}", [group.Id], Ct);
        try
        {
            // ExecuteSqlRaw coi {…} là chỗ điền tham số nên phải nhân đôi ngoặc; migrationBuilder.Sql chạy nguyên văn.
            var sql = V0_9_0_DaiMaMacDinh.InsertDefaultsSql.Replace("{", "{{").Replace("}", "}}");
            await db.Database.ExecuteSqlRawAsync(sql, Ct);
            await db.Database.ExecuteSqlRawAsync(sql, Ct);

            var after = await defaults.ToListAsync(Ct);
            var created = Assert.Single(after, n => n.Catalog == "supplier-group");
            Assert.Equal(("GN", 4, 1L, 9999L), (created.Prefix, created.Digits, created.FromNumber, created.ToNumber));
            // Dải đã có (NCC) giữ nguyên, không thêm dòng thứ hai.
            var kept = Assert.Single(after, n => n.Catalog == "supplier");
            Assert.Equal((supplier.Id, supplier.Prefix, supplier.ToNumber), (kept.Id, kept.Prefix, kept.ToNumber));
            Assert.Equal(4, after.Count);
        }
        finally
        {
            // Trả lại dải ban đầu cho các test khác.
            await db.Database.ExecuteSqlRawAsync("DELETE FROM \"CatalogNumbering\" WHERE \"Catalog\" = 'supplier-group' AND \"CategoryId\" IS NULL", Ct);
            await db.Database.ExecuteSqlRawAsync(
                "INSERT INTO \"CatalogNumbering\" (\"Id\", \"Catalog\", \"CategoryId\", \"Prefix\", \"Digits\", \"FromNumber\", \"ToNumber\", \"CreatedAt\") VALUES ({0}, 'supplier-group', NULL, {1}, {2}, {3}, {4}, now())",
                [group.Id, group.Prefix, group.Digits, group.FromNumber, group.ToNumber], Ct);
        }
    }
}
