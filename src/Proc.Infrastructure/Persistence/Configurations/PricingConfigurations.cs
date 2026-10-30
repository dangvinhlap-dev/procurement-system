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
