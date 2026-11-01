using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Proc.Domain.MasterData;
using Proc.Domain.Organization;
using Proc.Domain.Pricing;
using Proc.Domain.Purchasing;

namespace Proc.Infrastructure.Persistence.Configurations;

/// <summary>Cấu hình bảng PurchaseContract – hợp đồng nguyên tắc (lát B4a, design.md mục 18.7).</summary>
internal sealed class PurchaseContractConfiguration : IEntityTypeConfiguration<PurchaseContract>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<PurchaseContract> builder)
    {
        builder.ToTable("PurchaseContract");
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Ignore(x => x.DocumentNumber);
        builder.Property(x => x.No).HasMaxLength(50).IsRequired();
        builder.HasIndex(x => new { x.CompanyId, x.No }).IsUnique();
        // Kiểm tra chồng hiệu lực và tra hợp đồng hiệu lực khi đặt hàng: công ty × NCC × trạng thái.
        builder.HasIndex(x => new { x.CompanyId, x.SupplierId, x.Status });
        builder.Property(x => x.ContractNo).HasMaxLength(100);
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.PriceAdjustmentTerms).HasMaxLength(2000);
        builder.Property(x => x.PaymentTerms).HasMaxLength(2000);
        builder.Property(x => x.DeliveryTerms).HasMaxLength(2000);
        builder.Property(x => x.DiscountTerms).HasMaxLength(2000);
        builder.Property(x => x.SponsorshipTerms).HasMaxLength(2000);
        builder.Property(x => x.PenaltyTerms).HasMaxLength(2000);
        builder.Property(x => x.OtherTerms).HasMaxLength(4000);
        builder.Property(x => x.Note).HasMaxLength(1000);
        builder.Property(x => x.TerminationReason).HasMaxLength(500);
        builder.Property(x => x.RejectReason).HasMaxLength(500);
        // Tiền numeric(19,2) – CLAUDE.md mục 4.
        builder.Property(x => x.CreditLimit).HasPrecision(19, 2);
        builder.Ignore(x => x.DocType);
        builder.Ignore(x => x.ApprovalAmount);
        builder.Ignore(x => x.IsAwaitingApproval);
        builder.Ignore(x => x.ExpectedAmount);
        builder.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Supplier>().WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SupplierPriceList>().WithMany().HasForeignKey(x => x.PriceListId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Lines).WithOne().HasForeignKey(l => l.ContractId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Lines).HasField("_lines").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasMany(x => x.Departments).WithOne().HasForeignKey(d => d.ContractId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Departments).HasField("_departments").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.ToTable(t => t.HasCheckConstraint("CK_PurchaseContract_Validity", "\"ValidTo\" >= \"ValidFrom\""));
    }
}

/// <summary>Cấu hình bảng PurchaseContractLine – sản lượng dự kiến.</summary>
internal sealed class PurchaseContractLineConfiguration : IEntityTypeConfiguration<PurchaseContractLine>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<PurchaseContractLine> builder)
    {
        builder.ToTable("PurchaseContractLine");
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.HasIndex(x => new { x.ContractId, x.ItemId, x.UomId }).IsUnique();
        // Số lượng numeric(18,4), đơn giá numeric(19,6), tiền numeric(19,2) – CLAUDE.md mục 4.
        builder.Property(x => x.ExpectedQty).HasPrecision(18, 4);
        builder.Property(x => x.MinCommitQty).HasPrecision(18, 4);
        builder.Property(x => x.UnitPrice).HasPrecision(19, 6);
        builder.Property(x => x.DiscountPct).HasPrecision(9, 4);
        builder.Property(x => x.ExpectedAmount).HasPrecision(19, 2);
        builder.Property(x => x.Note).HasMaxLength(500);
        builder.HasOne<Item>().WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Uom>().WithMany().HasForeignKey(x => x.UomId).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>Cấu hình bảng PurchaseContractDepartment – bộ phận được phép đặt hàng.</summary>
internal sealed class PurchaseContractDepartmentConfiguration : IEntityTypeConfiguration<PurchaseContractDepartment>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<PurchaseContractDepartment> builder)
    {
        builder.ToTable("PurchaseContractDepartment");
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.HasIndex(x => new { x.ContractId, x.DepartmentId }).IsUnique();
        builder.HasOne<Department>().WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
    }
}
