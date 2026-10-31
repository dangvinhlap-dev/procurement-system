using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Proc.Domain.MasterData;
using Proc.Domain.Organization;
using Proc.Domain.Purchasing;

namespace Proc.Infrastructure.Persistence.Configurations;

/// <summary>Cấu hình bảng ApprovalRule – quy trình duyệt (design.md mục 5, 6.2).</summary>
internal sealed class ApprovalRuleConfiguration : IEntityTypeConfiguration<ApprovalRule>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ApprovalRule> builder)
    {
        builder.ToTable("ApprovalRule");
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Ignore(x => x.Label);
        builder.Property(x => x.DocType).HasMaxLength(10).IsRequired();
        builder.Property(x => x.MinAmount).HasPrecision(19, 2);
        // Mỗi công ty × loại chứng từ mỗi cấp một dòng.
        builder.HasIndex(x => new { x.CompanyId, x.DocType, x.Level }).IsUnique();
        builder.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Role>().WithMany().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>Cấu hình bảng ApprovalStep – lịch sử duyệt (design.md mục 12.1).</summary>
internal sealed class ApprovalStepConfiguration : IEntityTypeConfiguration<ApprovalStep>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ApprovalStep> builder)
    {
        builder.ToTable("ApprovalStep");
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.DocType).HasMaxLength(10).IsRequired();
        builder.Property(x => x.Comment).HasMaxLength(500);
        builder.HasIndex(x => new { x.DocType, x.DocumentId, x.Round, x.Level });
        // Việc cần làm: tìm bước đang chờ theo công ty.
        builder.HasIndex(x => new { x.CompanyId, x.Status });
        builder.HasOne<Role>().WithMany().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>Cấu hình bảng PurchaseRequest (design.md mục 10.2, 12.1).</summary>
internal sealed class PurchaseRequestConfiguration : IEntityTypeConfiguration<PurchaseRequest>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<PurchaseRequest> builder)
    {
        builder.ToTable("PurchaseRequest");
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Ignore(x => x.DocumentNumber);
        builder.Ignore(x => x.ApprovalAmount);
        builder.Ignore(x => x.DocType);
        builder.Property(x => x.No).HasMaxLength(50).IsRequired();
        builder.HasIndex(x => new { x.CompanyId, x.No }).IsUnique();
        builder.HasIndex(x => new { x.CompanyId, x.Date });
        builder.Property(x => x.Department).HasMaxLength(200);
        builder.Property(x => x.Purpose).HasMaxLength(500);
        builder.Property(x => x.Note).HasMaxLength(1000);
        builder.Property(x => x.RejectReason).HasMaxLength(500);
        builder.Property(x => x.TotalAmount).HasPrecision(19, 2);
        builder.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Warehouse>().WithMany()
            .HasForeignKey(x => new { x.WarehouseId, x.CompanyId }).HasPrincipalKey(w => new { w.Id, w.CompanyId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Lines).WithOne().HasForeignKey(l => l.RequestId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Lines).HasField("_lines").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

/// <summary>Cấu hình bảng PurchaseRequestLine.</summary>
internal sealed class PurchaseRequestLineConfiguration : IEntityTypeConfiguration<PurchaseRequestLine>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<PurchaseRequestLine> builder)
    {
        builder.ToTable("PurchaseRequestLine");
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.HasIndex(x => new { x.RequestId, x.LineNo });
        // Số lượng numeric(18,4), hệ số numeric(18,6), đơn giá numeric(19,6), tiền numeric(19,2) – CLAUDE.md mục 4.
        builder.Property(x => x.Qty).HasPrecision(18, 4);
        builder.Property(x => x.UomFactor).HasPrecision(18, 6);
        builder.Property(x => x.BaseQty).HasPrecision(18, 4);
        builder.Property(x => x.EstPrice).HasPrecision(19, 6);
        builder.Property(x => x.Amount).HasPrecision(19, 2);
        builder.Property(x => x.Note).HasMaxLength(500);
        builder.HasOne<Item>().WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Uom>().WithMany().HasForeignKey(x => x.UomId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Supplier>().WithMany().HasForeignKey(x => x.SuggestedSupplierId).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>Cấu hình bảng PurchaseOrder (design.md mục 10.3, 12.1).</summary>
internal sealed class PurchaseOrderConfiguration : IEntityTypeConfiguration<PurchaseOrder>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<PurchaseOrder> builder)
    {
        builder.ToTable("PurchaseOrder");
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Ignore(x => x.DocumentNumber);
        builder.Ignore(x => x.ApprovalAmount);
        builder.Ignore(x => x.DocType);
        builder.Property(x => x.No).HasMaxLength(50).IsRequired();
        builder.HasIndex(x => new { x.CompanyId, x.No }).IsUnique();
        builder.HasIndex(x => new { x.CompanyId, x.Date });
        builder.Property(x => x.PaymentTerms).HasMaxLength(200);
        builder.Property(x => x.DeliveryTerms).HasMaxLength(200);
        builder.Property(x => x.DeliveryAddress).HasMaxLength(500);
        builder.Property(x => x.CurrencyCode).HasMaxLength(3).IsRequired();
        builder.Property(x => x.Note).HasMaxLength(1000);
        builder.Property(x => x.RejectReason).HasMaxLength(500);
        builder.Property(x => x.ReceiveTolerancePct).HasPrecision(9, 4);
        builder.Property(x => x.GrossAmount).HasPrecision(19, 2);
        builder.Property(x => x.TotalDiscount).HasPrecision(19, 2);
        builder.Property(x => x.TotalAmount).HasPrecision(19, 2);
        builder.Property(x => x.TotalVat).HasPrecision(19, 2);
        builder.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Supplier>().WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Warehouse>().WithMany()
            .HasForeignKey(x => new { x.WarehouseId, x.CompanyId }).HasPrincipalKey(w => new { w.Id, w.CompanyId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Lines).WithOne().HasForeignKey(l => l.OrderId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Lines).HasField("_lines").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

/// <summary>Cấu hình bảng PurchaseOrderLine.</summary>
internal sealed class PurchaseOrderLineConfiguration : IEntityTypeConfiguration<PurchaseOrderLine>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<PurchaseOrderLine> builder)
    {
        builder.ToTable("PurchaseOrderLine");
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Ignore(x => x.NetUnitPrice);
        builder.HasIndex(x => new { x.OrderId, x.LineNo });
        builder.Property(x => x.Qty).HasPrecision(18, 4);
        builder.Property(x => x.UomFactor).HasPrecision(18, 6);
        builder.Property(x => x.BaseQty).HasPrecision(18, 4);
        builder.Property(x => x.UnitPrice).HasPrecision(19, 6);
        builder.Property(x => x.DiscountPct).HasPrecision(9, 4);
        builder.Property(x => x.GrossAmount).HasPrecision(19, 2);
        builder.Property(x => x.DiscountAmount).HasPrecision(19, 2);
        builder.Property(x => x.Amount).HasPrecision(19, 2);
        builder.Property(x => x.VatAmount).HasPrecision(19, 2);
        builder.Property(x => x.VatCode).HasMaxLength(5);
        builder.Property(x => x.Note).HasMaxLength(500);
        builder.HasIndex(x => x.SourcePrLineId);
        builder.HasOne<Item>().WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Uom>().WithMany().HasForeignKey(x => x.UomId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PurchaseRequestLine>().WithMany().HasForeignKey(x => x.SourcePrLineId).OnDelete(DeleteBehavior.Restrict);
    }
}
