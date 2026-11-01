using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Proc.Domain.Documents;
using Proc.Domain.Organization;

namespace Proc.Infrastructure.Persistence.Configurations;

/// <summary>Bảng DocumentNumbering: mỗi công ty tối đa một cấu hình cho mỗi loại chứng từ.</summary>
public sealed class DocumentNumberingConfiguration : IEntityTypeConfiguration<DocumentNumbering>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<DocumentNumbering> builder)
    {
        builder.ToTable("DocumentNumbering");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.DocType).HasMaxLength(10).IsRequired();
        builder.Property(x => x.Format).HasMaxLength(DocumentNumberFormat.MaxPatternLength).IsRequired();
        builder.HasIndex(x => new { x.CompanyId, x.DocType }).IsUnique();
        builder.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>
/// Bảng DocumentSequence: khóa chính (CompanyId, DocType, Period) chính là đích của ON CONFLICT khi tăng số.
/// </summary>
public sealed class DocumentSequenceConfiguration : IEntityTypeConfiguration<DocumentSequence>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<DocumentSequence> builder)
    {
        builder.ToTable("DocumentSequence");
        builder.HasKey(x => new { x.CompanyId, x.DocType, x.Period });
        builder.Property(x => x.DocType).HasMaxLength(10);
        builder.Property(x => x.Period).HasMaxLength(6);
        builder.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>Cấu hình bảng Attachment – file đính kèm dùng chung (design.md mục 18.4).</summary>
public sealed class AttachmentConfiguration : IEntityTypeConfiguration<Attachment>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Attachment> builder)
    {
        builder.ToTable("Attachment");
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.OwnerType).HasMaxLength(30).IsRequired();
        builder.Property(x => x.FileName).HasMaxLength(Attachment.FileNameMaxLength).IsRequired();
        builder.Property(x => x.Extension).HasMaxLength(10).IsRequired();
        builder.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Sha256).HasMaxLength(64).IsRequired();
        builder.Property(x => x.StoragePath).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Note).HasMaxLength(Attachment.NoteMaxLength);
        builder.HasIndex(x => x.StoragePath).IsUnique();
        // Truy vấn chính: file của một đối tượng. Không khóa ngoại tới chủ vì chủ nằm ở nhiều bảng khác nhau (OwnerType).
        builder.HasIndex(x => new { x.OwnerType, x.OwnerId });
        builder.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
    }
}
