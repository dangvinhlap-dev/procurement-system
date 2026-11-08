using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Proc.Domain.MasterData;
using Proc.Domain.Organization;
using Proc.Domain.Pricing;
using Proc.Domain.Purchasing;

namespace Proc.Infrastructure.Persistence.Configurations;

/// <summary>Cấu hình bảng PooledOrder – phiếu đặt hàng gộp (design.md mục 18.25, lát G7).</summary>
internal sealed class PooledOrderConfiguration : IEntityTypeConfiguration<PooledOrder>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<PooledOrder> builder)
    {
        builder.ToTable("PooledOrder");
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Ignore(x => x.DocumentNumber);
        builder.Property(x => x.No).HasMaxLength(50).IsRequired();
        builder.HasIndex(x => new { x.CompanyId, x.No }).IsUnique();
        builder.HasIndex(x => new { x.OrderDate, x.Status });
        builder.HasIndex(x => x.MonthlyApprovalId);
        builder.Property(x => x.Note).HasMaxLength(PooledOrder.TextMaxLength);
        builder.Property(x => x.CancelReason).HasMaxLength(PooledOrder.TextMaxLength);
        builder.Property(x => x.MarginPercent).HasPrecision(9, 4);
        builder.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ItemCategory>().WithMany().HasForeignKey(x => x.ItemCategoryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MonthlySupplierApproval>().WithMany().HasForeignKey(x => x.MonthlyApprovalId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Supplier>().WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Lines).WithOne().HasForeignKey(l => l.PooledOrderId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Lines).HasField("_lines").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasMany(x => x.Companies).WithOne().HasForeignKey(c => c.PooledOrderId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Companies).HasField("_companies").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

/// <summary>Cấu hình bảng PooledOrderCompany – công ty nhận hàng của phiếu đặt gộp.</summary>
internal sealed class PooledOrderCompanyConfiguration : IEntityTypeConfiguration<PooledOrderCompany>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<PooledOrderCompany> builder)
    {
        builder.ToTable("PooledOrderCompany");
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.HasIndex(x => new { x.PooledOrderId, x.CompanyId }).IsUnique();
        builder.HasIndex(x => x.PurchaseOrderId);
        builder.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Warehouse>().WithMany().HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Department>().WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PurchaseOrder>().WithMany().HasForeignKey(x => x.PurchaseOrderId).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>Cấu hình bảng PooledOrderLine – dòng phiếu đặt gộp.</summary>
internal sealed class PooledOrderLineConfiguration : IEntityTypeConfiguration<PooledOrderLine>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<PooledOrderLine> builder)
    {
        builder.ToTable("PooledOrderLine");
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Ignore(x => x.TotalQty);
        builder.HasIndex(x => new { x.PooledOrderId, x.MonthlyApprovalLineId }).IsUnique();
        builder.HasIndex(x => x.ItemId);
        builder.Property(x => x.UomFactor).HasPrecision(18, 6);
        builder.Property(x => x.UnitPrice).HasPrecision(19, 6);
        builder.Property(x => x.AllocationReason).HasMaxLength(500);
        builder.Property(x => x.Note).HasMaxLength(500);
        builder.HasOne<MonthlySupplierApprovalLine>().WithMany().HasForeignKey(x => x.MonthlyApprovalLineId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Item>().WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Uom>().WithMany().HasForeignKey(x => x.UomId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Allocations).WithOne().HasForeignKey(a => a.LineId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Allocations).HasField("_allocations").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

/// <summary>Cấu hình bảng PooledOrderAllocation – SL phân bổ dòng × công ty.</summary>
internal sealed class PooledOrderAllocationConfiguration : IEntityTypeConfiguration<PooledOrderAllocation>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<PooledOrderAllocation> builder)
    {
        builder.ToTable("PooledOrderAllocation");
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.HasIndex(x => new { x.LineId, x.CompanyId }).IsUnique();
        builder.Property(x => x.Qty).HasPrecision(18, 4);
        builder.ToTable(t => t.HasCheckConstraint("CK_PooledOrderAllocation_Qty", "\"Qty\" > 0"));
        builder.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
    }
}
