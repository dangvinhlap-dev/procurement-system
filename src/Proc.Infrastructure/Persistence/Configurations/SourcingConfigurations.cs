using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Proc.Domain.MasterData;
using Proc.Domain.Organization;
using Proc.Domain.Purchasing;
using Proc.Domain.Sourcing;

namespace Proc.Infrastructure.Persistence.Configurations;

/// <summary>Cấu hình bảng SourcingRequest – danh sách tìm nguồn (design.md mục 8.3).</summary>
internal sealed class SourcingRequestConfiguration : IEntityTypeConfiguration<SourcingRequest>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<SourcingRequest> builder)
    {
        builder.ToTable("SourcingRequest");
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Ignore(x => x.DocumentNumber);
        builder.Property(x => x.No).HasMaxLength(50).IsRequired();
        builder.HasIndex(x => new { x.CompanyId, x.No }).IsUnique();
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.Property(x => x.Specification).HasMaxLength(2000);
        builder.Property(x => x.Responsible).HasMaxLength(200);
        builder.Property(x => x.Note).HasMaxLength(1000);
        builder.Property(x => x.Qty).HasPrecision(18, 4);
        builder.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Item>().WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Candidates).WithOne().HasForeignKey(c => c.SourcingId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Candidates).HasField("_candidates").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

/// <summary>Cấu hình bảng SourcingCandidate.</summary>
internal sealed class SourcingCandidateConfiguration : IEntityTypeConfiguration<SourcingCandidate>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<SourcingCandidate> builder)
    {
        builder.ToTable("SourcingCandidate");
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Note).HasMaxLength(500);
        builder.HasIndex(x => new { x.SourcingId, x.SupplierId }).IsUnique();
        builder.HasOne<Supplier>().WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>Cấu hình bảng Rfq – yêu cầu báo giá (design.md mục 8.4, 12.1).</summary>
internal sealed class RfqConfiguration : IEntityTypeConfiguration<Rfq>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Rfq> builder)
    {
        builder.ToTable("Rfq");
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Ignore(x => x.DocumentNumber);
        builder.Ignore(x => x.Weights);
        builder.Property(x => x.No).HasMaxLength(50).IsRequired();
        builder.HasIndex(x => new { x.CompanyId, x.No }).IsUnique();
        builder.HasIndex(x => new { x.CompanyId, x.Status });
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Note).HasMaxLength(1000);
        builder.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SourcingRequest>().WithMany().HasForeignKey(x => x.SourcingId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Lines).WithOne().HasForeignKey(l => l.RfqId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Lines).HasField("_lines").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasMany(x => x.Suppliers).WithOne().HasForeignKey(s => s.RfqId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Suppliers).HasField("_suppliers").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.ToTable(t => t.HasCheckConstraint(
            "CK_Rfq_Weights", "\"PriceWeight\" + \"QualityWeight\" + \"DeliveryWeight\" + \"PaymentWeight\" + \"WarrantyWeight\" = 100"));
    }
}

/// <summary>Cấu hình bảng RfqLine.</summary>
internal sealed class RfqLineConfiguration : IEntityTypeConfiguration<RfqLine>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<RfqLine> builder)
    {
        builder.ToTable("RfqLine");
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.HasIndex(x => new { x.RfqId, x.LineNo });
        builder.Property(x => x.UomFactor).HasPrecision(18, 6);
        builder.Property(x => x.Qty).HasPrecision(18, 4);
        builder.Property(x => x.Note).HasMaxLength(500);
        builder.Property(x => x.AwardReason).HasMaxLength(500);
        builder.HasOne<Item>().WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Uom>().WithMany().HasForeignKey(x => x.UomId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PurchaseRequestLine>().WithMany().HasForeignKey(x => x.SourcePrLineId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Supplier>().WithMany().HasForeignKey(x => x.AwardedSupplierId).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>Cấu hình bảng RfqSupplier – NCC được mời kèm đầu báo giá.</summary>
internal sealed class RfqSupplierConfiguration : IEntityTypeConfiguration<RfqSupplier>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<RfqSupplier> builder)
    {
        builder.ToTable("RfqSupplier");
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.HasIndex(x => new { x.RfqId, x.SupplierId }).IsUnique();
        builder.Property(x => x.PaymentTerms).HasMaxLength(200);
        builder.Property(x => x.Note).HasMaxLength(1000);
        builder.Property(x => x.QualityScore).HasPrecision(5, 2);
        builder.Property(x => x.DeliveryScore).HasPrecision(5, 2);
        builder.Property(x => x.PaymentScore).HasPrecision(5, 2);
        builder.Property(x => x.WarrantyScore).HasPrecision(5, 2);
        builder.HasOne<Supplier>().WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PurchaseOrder>().WithMany().HasForeignKey(x => x.PurchaseOrderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.QuotationLines).WithOne().HasForeignKey(q => q.RfqSupplierId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.QuotationLines).HasField("_quotationLines").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

/// <summary>Cấu hình bảng QuotationLine – giá NCC chào từng dòng.</summary>
internal sealed class QuotationLineConfiguration : IEntityTypeConfiguration<QuotationLine>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<QuotationLine> builder)
    {
        builder.ToTable("QuotationLine");
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.HasIndex(x => new { x.RfqSupplierId, x.RfqLineId }).IsUnique();
        builder.Property(x => x.UnitPrice).HasPrecision(19, 6);
        builder.Property(x => x.DiscountPct).HasPrecision(9, 4);
        builder.Property(x => x.VatCode).HasMaxLength(5);
        builder.Property(x => x.Note).HasMaxLength(500);
        builder.HasOne<RfqLine>().WithMany().HasForeignKey(x => x.RfqLineId).OnDelete(DeleteBehavior.Restrict);
    }
}
