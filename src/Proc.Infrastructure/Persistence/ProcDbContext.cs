using System.Linq.Expressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Proc.Application.Common;
using Proc.Domain.Common;
using Proc.Domain.Documents;
using Proc.Domain.Inventory;
using Proc.Domain.MasterData;
using Proc.Domain.Organization;
using Proc.Infrastructure.Identity;
using Proc.Infrastructure.Logging;

namespace Proc.Infrastructure.Persistence;

/// <summary>
/// DbContext chính của hệ thống, dùng chung cho mọi module (modular monolith, một CSDL – design.md mục 3).
/// Kế thừa IdentityUserContext để chứa bảng tài khoản của ASP.NET Core Identity (không gồm bảng vai trò của Identity,
/// vì vai trò gán theo công ty ở bảng UserCompanyRole – design.md mục 9.1).
/// Global query filter lọc dữ liệu theo phạm vi của người dùng (design.md mục 9.2) – xem <see cref="ApplyScopeFilters"/>.
/// </summary>
public sealed class ProcDbContext : IdentityUserContext<AppUser, Guid>
{
    /// <summary>Tên filter lọc theo công ty đang làm việc.</summary>
    public const string CompanyFilter = "Company";

    /// <summary>Tên filter lọc theo kho được phép.</summary>
    public const string WarehouseFilter = "WarehouseScope";

    /// <summary>Tên filter lọc theo nhóm hàng được phép.</summary>
    public const string ItemCategoryFilter = "ItemCategoryScope";

    private readonly ICurrentUserScope _scope;

    /// <summary>
    /// Khởi tạo DbContext với cấu hình do DI cung cấp.
    /// </summary>
    /// <param name="options">Tùy chọn kết nối (Npgsql, chuỗi kết nối, interceptor).</param>
    /// <param name="scope">Phạm vi của người dùng trong request hiện tại.</param>
    public ProcDbContext(DbContextOptions<ProcDbContext> options, ICurrentUserScope scope)
        : base(options)
    {
        _scope = scope;
    }

    // Các thuộc tính Scope* được global query filter đọc lại ở MỖI truy vấn (EF Core tham số hóa thành viên của DbContext),
    // nên cùng một mô hình dùng được cho mọi người dùng.

    /// <summary>Công ty đang làm việc; null thì filter không trả dòng nào.</summary>
    public Guid? ScopeCompanyId => _scope.CompanyId;

    /// <summary>Được mọi kho.</summary>
    public bool ScopeAllWarehouses => _scope.AllWarehouses;

    /// <summary>Kho được phép.</summary>
    public IReadOnlyList<Guid> ScopeWarehouseIds => _scope.WarehouseIds;

    /// <summary>Được mọi nhóm hàng.</summary>
    public bool ScopeAllItemCategories => _scope.AllItemCategories;

    /// <summary>Nhóm hàng được phép (gồm nhóm con).</summary>
    public IReadOnlyList<Guid> ScopeItemCategoryIds => _scope.ItemCategoryIds;

    /// <summary>Công ty.</summary>
    public DbSet<Company> Companies => Set<Company>();

    /// <summary>Vai trò.</summary>
    public DbSet<Role> Roles => Set<Role>();

    /// <summary>Gán vai trò cho người dùng theo công ty.</summary>
    public DbSet<UserCompanyRole> UserCompanyRoles => Set<UserCompanyRole>();

    /// <summary>Quyền chức năng (đồng bộ từ Permissions.All).</summary>
    public DbSet<Permission> Permissions => Set<Permission>();

    /// <summary>Quyền của vai trò.</summary>
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    /// <summary>Kho (lọc theo công ty và phạm vi kho).</summary>
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();

    /// <summary>Nhóm hàng (lọc theo phạm vi nhóm hàng).</summary>
    public DbSet<ItemCategory> ItemCategories => Set<ItemCategory>();

    /// <summary>Đơn vị tính (dùng chung).</summary>
    public DbSet<Uom> Uoms => Set<Uom>();

    /// <summary>Hàng hóa (lọc theo phạm vi nhóm hàng).</summary>
    public DbSet<Item> Items => Set<Item>();

    /// <summary>Nhóm nhà cung cấp (dùng chung).</summary>
    public DbSet<SupplierGroup> SupplierGroups => Set<SupplierGroup>();

    /// <summary>Nhà cung cấp (dùng chung).</summary>
    public DbSet<Supplier> Suppliers => Set<Supplier>();

    /// <summary>NCC áp dụng cho công ty (lọc theo công ty).</summary>
    public DbSet<SupplierCompany> SupplierCompanies => Set<SupplierCompany>();

    /// <summary>NCC – sản phẩm (lọc theo phạm vi nhóm hàng của mặt hàng).</summary>
    public DbSet<SupplierItem> SupplierItems => Set<SupplierItem>();

    /// <summary>Kỳ kho (lọc theo công ty).</summary>
    public DbSet<InventoryPeriod> InventoryPeriods => Set<InventoryPeriod>();

    /// <summary>Chứng từ kho (lọc theo công ty và kho được phép).</summary>
    public DbSet<InventoryDocument> InventoryDocuments => Set<InventoryDocument>();

    /// <summary>Dòng chứng từ kho (theo chứng từ thấy được).</summary>
    public DbSet<InventoryDocumentLine> InventoryDocumentLines => Set<InventoryDocumentLine>();

    /// <summary>Sổ kho (lọc theo công ty, kho, nhóm hàng).</summary>
    public DbSet<StockLedgerEntry> StockLedger => Set<StockLedgerEntry>();

    /// <summary>Tồn tức thời (lọc theo công ty, kho, nhóm hàng).</summary>
    public DbSet<StockOnHand> StockOnHand => Set<StockOnHand>();

    /// <summary>Số dư kỳ đã đóng (lọc theo công ty, kho, nhóm hàng).</summary>
    public DbSet<StockPeriodBalance> StockPeriodBalances => Set<StockPeriodBalance>();

    /// <summary>Giá vốn kỳ theo mặt hàng (lọc theo công ty, nhóm hàng).</summary>
    public DbSet<ItemPeriodCost> ItemPeriodCosts => Set<ItemPeriodCost>();

    /// <summary>Phạm vi kho của người dùng.</summary>
    public DbSet<UserWarehouseScope> UserWarehouseScopes => Set<UserWarehouseScope>();

    /// <summary>Phạm vi nhóm hàng của người dùng.</summary>
    public DbSet<UserItemCategoryScope> UserItemCategoryScopes => Set<UserItemCategoryScope>();

    /// <summary>Cấu hình đánh số chứng từ (lọc theo công ty).</summary>
    public DbSet<DocumentNumbering> DocumentNumberings => Set<DocumentNumbering>();

    /// <summary>Bộ đếm số chứng từ (lọc theo công ty).</summary>
    public DbSet<DocumentSequence> DocumentSequences => Set<DocumentSequence>();

    /// <summary>Nhật ký thao tác (chỉ thêm).</summary>
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    /// <summary>Nhật ký truy cập (chỉ thêm).</summary>
    public DbSet<AccessLog> AccessLogs => Set<AccessLog>();

    /// <summary>
    /// Cấu hình bảng Identity, áp dụng các lớp IEntityTypeConfiguration trong assembly Infrastructure, rồi gắn filter phạm vi.
    /// </summary>
    /// <param name="modelBuilder">Bộ dựng mô hình của EF Core.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Đặt lại tên bảng Identity cho cùng kiểu với các bảng khác (số ít, không tiền tố AspNet).
        modelBuilder.Entity<IdentityUserClaim<Guid>>().ToTable("UserClaim");
        modelBuilder.Entity<IdentityUserLogin<Guid>>().ToTable("UserLogin");
        modelBuilder.Entity<IdentityUserToken<Guid>>().ToTable("UserToken");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ProcDbContext).Assembly);
        ApplyScopeFilters(modelBuilder);
    }

    /// <summary>
    /// Gắn global query filter theo phạm vi (design.md mục 9.2):
    /// <list type="bullet">
    /// <item>Mọi entity có CompanyId (<see cref="ICompanyOwned"/>): chỉ thấy dòng của công ty đang làm việc – tự động,
    /// để bảng giao dịch thêm sau không thể quên lọc.</item>
    /// <item>Kho: thêm điều kiện kho được phép. Nhóm hàng: thêm điều kiện nhóm được phép.</item>
    /// </list>
    /// Filter có tên riêng (EF Core 10) để sau này tắt riêng từng filter khi thật cần, có ghi lý do.
    /// </summary>
    private void ApplyScopeFilters(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes()
                     .Where(t => typeof(ICompanyOwned).IsAssignableFrom(t.ClrType) && t.BaseType is null))
        {
            // Dựng biểu thức e => (Guid?)e.CompanyId == this.ScopeCompanyId cho từng loại entity.
            var e = Expression.Parameter(entityType.ClrType, "e");
            var body = Expression.Equal(
                Expression.Convert(Expression.Property(e, nameof(ICompanyOwned.CompanyId)), typeof(Guid?)),
                Expression.Property(Expression.Constant(this), nameof(ScopeCompanyId)));
            modelBuilder.Entity(entityType.ClrType).HasQueryFilter(CompanyFilter, Expression.Lambda(body, e));
        }

        modelBuilder.Entity<Warehouse>().HasQueryFilter(
            WarehouseFilter, w => ScopeAllWarehouses || ScopeWarehouseIds.Contains(w.Id));
        modelBuilder.Entity<ItemCategory>().HasQueryFilter(
            ItemCategoryFilter, c => ScopeAllItemCategories || ScopeItemCategoryIds.Contains(c.Id));
        // Hàng hóa theo nhóm hàng của nó: người chỉ được nhóm Vật tư không thấy mã hàng nhóm khác (design.md mục 9.2).
        modelBuilder.Entity<Item>().HasQueryFilter(
            ItemCategoryFilter, i => ScopeAllItemCategories || ScopeItemCategoryIds.Contains(i.CategoryId));
        // Chứng từ kho theo kho được phép: thủ kho chỉ được kho KP1 thì chỉ thấy phiếu nhập/xuất của KP1 (design.md mục 9.2).
        modelBuilder.Entity<InventoryDocument>().HasQueryFilter(
            WarehouseFilter,
            d => ScopeAllWarehouses
                 || (d.ToWarehouseId != null && ScopeWarehouseIds.Contains(d.ToWarehouseId.Value))
                 || (d.FromWarehouseId != null && ScopeWarehouseIds.Contains(d.FromWarehouseId.Value)));
        // Dòng đi theo chứng từ: chỉ thấy dòng của chứng từ thấy được (công ty, kho).
        modelBuilder.Entity<InventoryDocumentLine>().HasQueryFilter(
            CompanyFilter, l => InventoryDocuments.Any(d => d.Id == l.DocumentId));
        // Sổ kho và tồn kho: chỉ thấy kho được phép và mặt hàng thuộc nhóm được phép (design.md mục 9.2).
        modelBuilder.Entity<StockLedgerEntry>().HasQueryFilter(
            WarehouseFilter, x => ScopeAllWarehouses || ScopeWarehouseIds.Contains(x.WarehouseId));
        modelBuilder.Entity<StockLedgerEntry>().HasQueryFilter(
            ItemCategoryFilter, x => ScopeAllItemCategories || Items.Any(i => i.Id == x.ItemId));
        modelBuilder.Entity<StockOnHand>().HasQueryFilter(
            WarehouseFilter, x => ScopeAllWarehouses || ScopeWarehouseIds.Contains(x.WarehouseId));
        modelBuilder.Entity<StockOnHand>().HasQueryFilter(
            ItemCategoryFilter, x => ScopeAllItemCategories || Items.Any(i => i.Id == x.ItemId));
        modelBuilder.Entity<StockPeriodBalance>().HasQueryFilter(
            WarehouseFilter, x => ScopeAllWarehouses || ScopeWarehouseIds.Contains(x.WarehouseId));
        modelBuilder.Entity<StockPeriodBalance>().HasQueryFilter(
            ItemCategoryFilter, x => ScopeAllItemCategories || Items.Any(i => i.Id == x.ItemId));
        modelBuilder.Entity<ItemPeriodCost>().HasQueryFilter(
            ItemCategoryFilter, x => ScopeAllItemCategories || Items.Any(i => i.Id == x.ItemId));
        // NCC – sản phẩm theo phạm vi của mặt hàng: Items đã có filter nhóm hàng, nên chỉ thấy dòng của hàng mình được xem.
        modelBuilder.Entity<SupplierItem>().HasQueryFilter(
            ItemCategoryFilter, si => ScopeAllItemCategories || Items.Any(i => i.Id == si.ItemId));
    }
}
