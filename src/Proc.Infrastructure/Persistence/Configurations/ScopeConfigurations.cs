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

/// <summary>Cấu hình bảng ItemCategory (design.md mục 5.2).</summary>
internal sealed class ItemCategoryConfiguration : IEntityTypeConfiguration<ItemCategory>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ItemCategory> builder)
    {
        builder.ToTable("ItemCategory");
        builder.Property(x => x.Code).HasMaxLength(20).IsRequired();
        builder.HasIndex(x => x.Code).IsUnique();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
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
