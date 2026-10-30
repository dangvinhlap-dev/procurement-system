using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Proc.Domain.Inventory;
using Proc.Domain.Organization;

namespace Proc.Infrastructure.Persistence.Configurations;

/// <summary>Cấu hình bảng InventoryPeriod (design.md mục 7, 12.1).</summary>
internal sealed class InventoryPeriodConfiguration : IEntityTypeConfiguration<InventoryPeriod>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<InventoryPeriod> builder)
    {
        builder.ToTable("InventoryPeriod");
        // Mỗi công ty mỗi tháng (và mỗi kỳ năm, Month = 0) đúng một kỳ.
        builder.HasIndex(x => new { x.CompanyId, x.Year, x.Month }).IsUnique();
        builder.Ignore(x => x.IsYear);
        builder.Ignore(x => x.Label);
        builder.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        // Tháng 0–12; kỳ tháng nằm gọn trong một tháng dương lịch.
        builder.ToTable(t => t.HasCheckConstraint("CK_InventoryPeriod_Month", "\"Month\" BETWEEN 0 AND 12"));
    }
}
