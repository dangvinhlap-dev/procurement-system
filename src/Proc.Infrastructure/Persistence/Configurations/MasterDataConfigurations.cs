using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Proc.Domain.MasterData;

namespace Proc.Infrastructure.Persistence.Configurations;

/// <summary>Cấu hình bảng Uom (design.md mục 5.2).</summary>
internal sealed class UomConfiguration : IEntityTypeConfiguration<Uom>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Uom> builder)
    {
        builder.ToTable("Uom");
        builder.Property(x => x.Code).HasMaxLength(Uom.CodeMaxLength).IsRequired();
        builder.HasIndex(x => x.Code).IsUnique();
        builder.Property(x => x.Name).HasMaxLength(50).IsRequired();
    }
}

/// <summary>Cấu hình bảng Item và ItemUomConversion (design.md mục 5.2).</summary>
internal sealed class ItemConfiguration : IEntityTypeConfiguration<Item>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Item> builder)
    {
        builder.ToTable("Item");
        builder.Property(x => x.Code).HasMaxLength(Item.CodeMaxLength).IsRequired();
        builder.HasIndex(x => x.Code).IsUnique();
        builder.Property(x => x.Name).HasMaxLength(300).IsRequired();
        builder.Property(x => x.EnglishName).HasMaxLength(300);
        builder.Property(x => x.Specification).HasMaxLength(500);
        builder.Property(x => x.Brand).HasMaxLength(100);
        builder.Property(x => x.Origin).HasMaxLength(100);
        builder.Property(x => x.Barcode).HasMaxLength(50);
        builder.HasIndex(x => x.Barcode);
        builder.Property(x => x.VatCode).HasMaxLength(5);
        builder.Property(x => x.TechnicalSpecs).HasMaxLength(4000);
        // Số lượng numeric(18,4) – CLAUDE.md mục 4.
        builder.Property(x => x.MinStock).HasPrecision(18, 4);
        builder.Property(x => x.MaxStock).HasPrecision(18, 4);
        builder.Property(x => x.SearchText).HasMaxLength(800).IsRequired();
        builder.HasIndex(x => x.CategoryId);

        builder.HasOne<ItemCategory>().WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Uom>().WithMany().HasForeignKey(x => x.BaseUomId).OnDelete(DeleteBehavior.Restrict);

        // Quy đổi ĐVT là phần của mặt hàng (owned): đọc/ghi cùng mặt hàng và theo luôn filter phạm vi nhóm hàng của Item.
        builder.OwnsMany(x => x.Conversions, c =>
        {
            c.ToTable("ItemUomConversion");
            c.WithOwner().HasForeignKey(x => x.ItemId);
            c.HasKey(x => x.Id);
            c.Property(x => x.Id).ValueGeneratedNever();
            // Hệ số quy đổi cần 6 chữ số thập phân (1 g = 0,001 kg).
            c.Property(x => x.Factor).HasPrecision(18, 6);
            c.HasIndex(x => new { x.ItemId, x.UomId }).IsUnique();
            c.HasOne<Uom>().WithMany().HasForeignKey(x => x.UomId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Navigation(x => x.Conversions).HasField("_conversions").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

/// <summary>Cấu hình bảng SupplierGroup (design.md mục 5.3).</summary>
internal sealed class SupplierGroupConfiguration : IEntityTypeConfiguration<SupplierGroup>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<SupplierGroup> builder)
    {
        builder.ToTable("SupplierGroup");
        builder.Property(x => x.Code).HasMaxLength(20).IsRequired();
        builder.HasIndex(x => x.Code).IsUnique();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
    }
}

/// <summary>Cấu hình bảng Supplier, SupplierContact, SupplierBankAccount (design.md mục 5.3).</summary>
internal sealed class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Supplier> builder)
    {
        builder.ToTable("Supplier");
        builder.Property(x => x.Code).HasMaxLength(Supplier.CodeMaxLength).IsRequired();
        builder.HasIndex(x => x.Code).IsUnique();
        builder.Property(x => x.Name).HasMaxLength(300).IsRequired();
        builder.Property(x => x.ShortName).HasMaxLength(100);
        // Mã số thuế NCC là duy nhất (design.md mục 5.3); NCC chưa khai báo thì bỏ qua.
        builder.Property(x => x.TaxCode).HasMaxLength(20);
        builder.HasIndex(x => x.TaxCode).IsUnique().HasFilter("\"TaxCode\" IS NOT NULL");
        builder.Property(x => x.Address).HasMaxLength(500);
        builder.Property(x => x.Phone).HasMaxLength(50);
        builder.Property(x => x.Email).HasMaxLength(256);
        builder.Property(x => x.Website).HasMaxLength(200);
        builder.Property(x => x.PaymentTerms).HasMaxLength(200);
        builder.Property(x => x.SupplyScope).HasMaxLength(500);
        builder.Property(x => x.StatusNote).HasMaxLength(500);
        builder.Property(x => x.SearchText).HasMaxLength(800).IsRequired();
        builder.HasIndex(x => x.GroupId);

        // Mỗi công ty thành viên tối đa một NCC nội bộ đại diện cho nó.
        builder.HasIndex(x => x.InternalCompanyId).IsUnique().HasFilter("\"InternalCompanyId\" IS NOT NULL");
        builder.HasOne<Proc.Domain.Organization.Company>().WithMany().HasForeignKey(x => x.InternalCompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SupplierGroup>().WithMany().HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.Restrict);

        // Người liên hệ và tài khoản ngân hàng là phần của NCC (owned): đọc/ghi cùng NCC.
        builder.OwnsMany(x => x.Contacts, c =>
        {
            c.ToTable("SupplierContact");
            c.WithOwner().HasForeignKey(x => x.SupplierId);
            c.HasKey(x => x.Id);
            c.Property(x => x.Id).ValueGeneratedNever();
            c.Property(x => x.Name).HasMaxLength(200).IsRequired();
            c.Property(x => x.Position).HasMaxLength(100);
            c.Property(x => x.Phone).HasMaxLength(50);
            c.Property(x => x.Email).HasMaxLength(256);
        });
        builder.Navigation(x => x.Contacts).HasField("_contacts").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsMany(x => x.BankAccounts, b =>
        {
            b.ToTable("SupplierBankAccount");
            b.WithOwner().HasForeignKey(x => x.SupplierId);
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.BankName).HasMaxLength(200).IsRequired();
            b.Property(x => x.Branch).HasMaxLength(200);
            b.Property(x => x.AccountNumber).HasMaxLength(30).IsRequired();
            b.Property(x => x.AccountName).HasMaxLength(200);
        });
        builder.Navigation(x => x.BankAccounts).HasField("_bankAccounts").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
