using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Proc.Domain.MasterData;
using Proc.Domain.Organization;
using Proc.Domain.Pricing;

namespace Proc.Infrastructure.Persistence.Configurations;

/// <summary>Cấu hình bảng InternalPrice – bảng giá nội bộ (design.md mục 4, 6.3).</summary>
internal sealed class InternalPriceConfiguration : IEntityTypeConfiguration<InternalPrice>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<InternalPrice> builder)
    {
        builder.ToTable("InternalPrice");
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Ignore(x => x.Label);
        // Đơn giá numeric(19,6) – CLAUDE.md mục 4.
        builder.Property(x => x.UnitPrice).HasPrecision(19, 6);
        builder.Property(x => x.VatCode).HasMaxLength(5);
        builder.Property(x => x.Note).HasMaxLength(500);
        // Tra giá khi lập phiếu: công ty bán × mặt hàng × công ty mua × ngày.
        builder.HasIndex(x => new { x.CompanyId, x.ItemId, x.BuyerCompanyId, x.ValidFrom });
        builder.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Company>().WithMany().HasForeignKey(x => x.BuyerCompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Item>().WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable(t => t.HasCheckConstraint("CK_InternalPrice_Buyer", "\"BuyerCompanyId\" IS NULL OR \"BuyerCompanyId\" <> \"CompanyId\""));
    }
}

/// <summary>Cấu hình bảng SupplierPriceList – bảng giá NCC (design.md mục 8.1).</summary>
internal sealed class SupplierPriceListConfiguration : IEntityTypeConfiguration<SupplierPriceList>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<SupplierPriceList> builder)
    {
        builder.ToTable("SupplierPriceList");
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Ignore(x => x.DocumentNumber);
        builder.Property(x => x.No).HasMaxLength(50).IsRequired();
        builder.HasIndex(x => new { x.CompanyId, x.No }).IsUnique();
        // Tra giá gợi ý cho PO: NCC × trạng thái × hiệu lực.
        builder.HasIndex(x => new { x.SupplierId, x.Status, x.ValidFrom });
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.CurrencyCode).HasMaxLength(3).IsRequired();
        builder.Property(x => x.DeliveryTerms).HasMaxLength(200);
        builder.Property(x => x.Note).HasMaxLength(1000);
        // Thông tin duyệt (lát B1): lượt gửi, lý do từ chối; các thuộc tính tính toán của IApprovalSubject không lưu.
        builder.Property(x => x.RejectReason).HasMaxLength(500);
        builder.Ignore(x => x.DocType);
        builder.Ignore(x => x.ApprovalAmount);
        builder.Ignore(x => x.IsAwaitingApproval);
        builder.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Supplier>().WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Lines).WithOne().HasForeignKey(l => l.PriceListId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Lines).HasField("_lines").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

/// <summary>Cấu hình bảng SupplierPriceListLine.</summary>
internal sealed class SupplierPriceListLineConfiguration : IEntityTypeConfiguration<SupplierPriceListLine>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<SupplierPriceListLine> builder)
    {
        builder.ToTable("SupplierPriceListLine");
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Ignore(x => x.BaseNetPrice);
        builder.HasIndex(x => new { x.PriceListId, x.LineNo });
        // So sánh giá theo mặt hàng.
        builder.HasIndex(x => x.ItemId);
        // Số lượng numeric(18,4), hệ số numeric(18,6), đơn giá numeric(19,6) – CLAUDE.md mục 4.
        builder.Property(x => x.UomFactor).HasPrecision(18, 6);
        builder.Property(x => x.MinQty).HasPrecision(18, 4);
        builder.Property(x => x.UnitPrice).HasPrecision(19, 6);
        builder.Property(x => x.DiscountPct).HasPrecision(9, 4);
        builder.Property(x => x.VatCode).HasMaxLength(5);
        builder.Property(x => x.Note).HasMaxLength(500);
        builder.HasOne<Item>().WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Uom>().WithMany().HasForeignKey(x => x.UomId).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>Cấu hình bảng MonthlySupplierApproval – phiếu duyệt NCC và giá tháng (design.md mục 18.25, lát G2).</summary>
internal sealed class MonthlySupplierApprovalConfiguration : IEntityTypeConfiguration<MonthlySupplierApproval>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<MonthlySupplierApproval> builder)
    {
        builder.ToTable("MonthlySupplierApproval");
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Ignore(x => x.DocumentNumber);
        builder.Ignore(x => x.DocType);
        builder.Ignore(x => x.ApprovalAmount);
        builder.Ignore(x => x.IsAwaitingApproval);
        builder.Ignore(x => x.RelatedSupplierIds);
        builder.Ignore(x => x.IsLive);
        builder.Property(x => x.No).HasMaxLength(50).IsRequired();
        builder.HasIndex(x => new { x.CompanyId, x.No }).IsUnique();
        // Dò trùng mặt hàng × tháng và tra kết quả tháng cho PO (lát G3): tháng × trạng thái.
        builder.HasIndex(x => new { x.Period, x.Status });
        builder.HasIndex(x => x.ReplacesId);
        builder.Property(x => x.Note).HasMaxLength(MonthlySupplierApproval.TextMaxLength);
        builder.Property(x => x.RejectReason).HasMaxLength(500);
        builder.Property(x => x.CancelReason).HasMaxLength(MonthlySupplierApproval.TextMaxLength);
        builder.ToTable(t => t.HasCheckConstraint("CK_MonthlySupplierApproval_Period", "EXTRACT(DAY FROM \"Period\") = 1"));
        builder.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ItemCategory>().WithMany().HasForeignKey(x => x.ItemCategoryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MonthlySupplierApproval>().WithMany().HasForeignKey(x => x.ReplacesId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Lines).WithOne().HasForeignKey(l => l.ApprovalId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Lines).HasField("_lines").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasMany(x => x.Companies).WithOne().HasForeignKey(c => c.ApprovalId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Companies).HasField("_companies").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

/// <summary>Cấu hình bảng MonthlySupplierApprovalCompany – công ty áp dụng phiếu duyệt tháng.</summary>
internal sealed class MonthlySupplierApprovalCompanyConfiguration : IEntityTypeConfiguration<MonthlySupplierApprovalCompany>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<MonthlySupplierApprovalCompany> builder)
    {
        builder.ToTable("MonthlySupplierApprovalCompany");
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.HasIndex(x => new { x.ApprovalId, x.CompanyId }).IsUnique();
        builder.HasIndex(x => x.CompanyId);
        builder.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>Cấu hình bảng MonthlySupplierApprovalLine – dòng phiếu duyệt tháng.</summary>
internal sealed class MonthlySupplierApprovalLineConfiguration : IEntityTypeConfiguration<MonthlySupplierApprovalLine>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<MonthlySupplierApprovalLine> builder)
    {
        builder.ToTable("MonthlySupplierApprovalLine");
        builder.Property(x => x.Id).ValueGeneratedNever();
        // Mỗi mặt hàng một dòng trên một phiếu (quyết định 06/11/2026).
        builder.HasIndex(x => new { x.ApprovalId, x.ItemId }).IsUnique();
        builder.HasIndex(x => x.ItemId);
        // Số lượng numeric(18,4), hệ số numeric(18,6), đơn giá numeric(19,6) – CLAUDE.md mục 4.
        builder.Property(x => x.UomFactor).HasPrecision(18, 6);
        builder.Property(x => x.UnitPrice).HasPrecision(19, 6);
        builder.Property(x => x.BackupUnitPrice).HasPrecision(19, 6);
        builder.Property(x => x.QuantityLimit).HasPrecision(18, 4);
        builder.Property(x => x.Note).HasMaxLength(500);
        builder.ToTable(t => t.HasCheckConstraint("CK_MonthlySupplierApprovalLine_Backup",
            "\"BackupSupplierId\" IS NULL OR \"BackupSupplierId\" <> \"MainSupplierId\""));
        builder.HasOne<Item>().WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Uom>().WithMany().HasForeignKey(x => x.UomId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Supplier>().WithMany().HasForeignKey(x => x.MainSupplierId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Supplier>().WithMany().HasForeignKey(x => x.BackupSupplierId).OnDelete(DeleteBehavior.Restrict);
    }
}
