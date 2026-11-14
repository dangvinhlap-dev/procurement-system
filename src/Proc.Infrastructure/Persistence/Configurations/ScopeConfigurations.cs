using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Proc.Domain.MasterData;
using Proc.Domain.Organization;
using Proc.Infrastructure.Identity;

namespace Proc.Infrastructure.Persistence.Configurations;

/// <summary>Cấu hình bảng Permission (đồng bộ từ Permissions.All).</summary>
internal sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("Permission");
        builder.HasKey(x => x.Code);
        builder.Property(x => x.Code).HasMaxLength(100);
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Module).HasMaxLength(50).IsRequired();
    }
}

/// <summary>Cấu hình bảng RolePermission.</summary>
internal sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.ToTable("RolePermission");
        builder.HasKey(x => new { x.RoleId, x.PermissionCode });
        builder.Property(x => x.PermissionCode).HasMaxLength(100);
        builder.HasOne<Role>().WithMany().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Restrict);
        // Quyền bị bỏ khỏi code thì khi đồng bộ xóa quyền, các dòng gán quyền đó xóa theo.
        builder.HasOne<Permission>().WithMany().HasForeignKey(x => x.PermissionCode).OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>Cấu hình bảng Warehouse (design.md mục 5.1, 12.2).</summary>
internal sealed class WarehouseConfiguration : IEntityTypeConfiguration<Warehouse>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Warehouse> builder)
    {
        builder.ToTable("Warehouse");
        builder.Property(x => x.Code).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Address).HasMaxLength(500);
        builder.Property(x => x.Keeper).HasMaxLength(200);
        builder.HasIndex(x => new { x.CompanyId, x.Code }).IsUnique();

        // Mỗi công ty tối đa một kho Main (design.md mục 4, 5.1): chỉ mục duy nhất theo CompanyId trên các dòng Type = Main.
        builder.HasIndex(x => x.CompanyId).IsUnique().HasFilter($"\"Type\" = {(int)WarehouseType.Main}")
            .HasDatabaseName("IX_Warehouse_CompanyId_Main");

        // Khóa (Id, CompanyId) để các bảng khác trỏ tới kho kèm công ty, CSDL tự chặn ghép kho với công ty khác.
        builder.HasAlternateKey(x => new { x.Id, x.CompanyId });
        builder.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);

        // Kho cha phải cùng công ty – kho Sub không được treo dưới kho công ty khác (design.md mục 4, 12.2).
        builder.HasOne<Warehouse>().WithMany()
            .HasForeignKey(x => new { x.ParentWarehouseId, x.CompanyId })
            .HasPrincipalKey(x => new { x.Id, x.CompanyId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>Cấu hình bảng Department – bộ phận theo công ty (design.md mục 5.1, 18.5).</summary>
internal sealed class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Department> builder)
    {
        builder.ToTable("Department");
        builder.Property(x => x.Code).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(Department.NameMaxLength).IsRequired();
        builder.Property(x => x.ManagerName).HasMaxLength(Department.ManagerNameMaxLength);
        builder.HasIndex(x => new { x.CompanyId, x.Code }).IsUnique();

        // Khóa (Id, CompanyId) để PR trỏ tới bộ phận kèm công ty – CSDL tự chặn PR dùng bộ phận của công ty khác.
        builder.HasAlternateKey(x => new { x.Id, x.CompanyId });
        builder.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        // Khối cùng công ty (khóa ngoại kép) và Trưởng phòng ban là tài khoản có thật – lát O1, design.md mục 19.3.
        builder.HasOne<Division>().WithMany()
            .HasForeignKey(x => new { x.DivisionId, x.CompanyId }).HasPrincipalKey(d => new { d.Id, d.CompanyId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AppUser>().WithMany().HasForeignKey(x => x.ManagerUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>Cấu hình bảng Division – khối của công ty (lát O1, design.md mục 19.3).</summary>
internal sealed class DivisionConfiguration : IEntityTypeConfiguration<Division>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Division> builder)
    {
        builder.ToTable("Division");
        builder.Property(x => x.Code).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(Division.NameMaxLength).IsRequired();
        builder.HasIndex(x => new { x.CompanyId, x.Code }).IsUnique();
        // Khóa (Id, CompanyId) để bộ phận trỏ tới khối kèm công ty – CSDL chặn gán khối của công ty khác.
        builder.HasAlternateKey(x => new { x.Id, x.CompanyId });
        builder.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AppUser>().WithMany().HasForeignKey(x => x.DirectorUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>Cấu hình bảng UserDepartment – bộ phận của người dùng tại công ty (lát O1).</summary>
internal sealed class UserDepartmentConfiguration : IEntityTypeConfiguration<UserDepartment>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<UserDepartment> builder)
    {
        builder.ToTable("UserDepartment");
        // Mỗi người tối đa một bộ phận ở mỗi công ty.
        builder.HasIndex(x => new { x.UserId, x.CompanyId }).IsUnique();
        builder.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Department>().WithMany()
            .HasForeignKey(x => new { x.DepartmentId, x.CompanyId }).HasPrincipalKey(d => new { d.Id, d.CompanyId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>Cấu hình bảng ItemCategory (design.md mục 5.2).</summary>
internal sealed class ItemCategoryConfiguration : IEntityTypeConfiguration<ItemCategory>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ItemCategory> builder)
    {
        // Cây nhóm hàng tối đa 4 cấp (design.md mục 5.7, N4-2) – chặn cả ở CSDL, không chỉ ở máy chủ.
        builder.ToTable("ItemCategory", t => t.HasCheckConstraint("CK_ItemCategory_Level", "\"Level\" BETWEEN 1 AND 4"));
        builder.Property(x => x.Code).HasMaxLength(20).IsRequired();
        builder.HasIndex(x => x.Code).IsUnique();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.InventoryAccount).HasMaxLength(10);
        builder.HasOne<ItemCategory>().WithMany().HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>Cấu hình bảng UserWarehouseScope (design.md mục 9.2).</summary>
internal sealed class UserWarehouseScopeConfiguration : IEntityTypeConfiguration<UserWarehouseScope>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<UserWarehouseScope> builder)
    {
        builder.ToTable("UserWarehouseScope");
        builder.HasIndex(x => new { x.UserId, x.CompanyId, x.WarehouseId }).IsUnique();
        builder.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Warehouse>().WithMany()
            .HasForeignKey(x => new { x.WarehouseId, x.CompanyId })
            .HasPrincipalKey(x => new { x.Id, x.CompanyId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>Cấu hình bảng UserItemCategoryScope (design.md mục 9.2).</summary>
internal sealed class UserItemCategoryScopeConfiguration : IEntityTypeConfiguration<UserItemCategoryScope>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<UserItemCategoryScope> builder)
    {
        builder.ToTable("UserItemCategoryScope");
        builder.HasIndex(x => new { x.UserId, x.CompanyId, x.ItemCategoryId }).IsUnique();
        builder.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ItemCategory>().WithMany().HasForeignKey(x => x.ItemCategoryId).OnDelete(DeleteBehavior.Restrict);
    }
}
