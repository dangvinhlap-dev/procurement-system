using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Proc.Domain.Control;
using Proc.Domain.MasterData;
using Proc.Domain.Organization;

namespace Proc.Infrastructure.Persistence.Configurations;

/// <summary>Cấu hình bảng ControlFinding – vi phạm KSNB (design.md mục 18.19).</summary>
internal sealed class ControlFindingConfiguration : IEntityTypeConfiguration<ControlFinding>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ControlFinding> builder)
    {
        builder.ToTable("ControlFinding");
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.No).HasMaxLength(50).IsRequired();
        builder.HasIndex(x => new { x.CompanyId, x.No }).IsUnique();
        // KPI vi phạm nghiêm trọng đếm theo công ty × ngày phát hiện.
        builder.HasIndex(x => new { x.CompanyId, x.Date });
        builder.Property(x => x.AnomalyCode).HasMaxLength(40);
        builder.Property(x => x.Title).HasMaxLength(ControlFinding.TitleMaxLength).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(ControlFinding.TextMaxLength).IsRequired();
        builder.Property(x => x.RefScreen).HasMaxLength(20);
        builder.Property(x => x.RefNo).HasMaxLength(50);
        builder.Property(x => x.Conclusion).HasMaxLength(ControlFinding.TextMaxLength);
        builder.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Supplier>().WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
    }
}
