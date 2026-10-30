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
