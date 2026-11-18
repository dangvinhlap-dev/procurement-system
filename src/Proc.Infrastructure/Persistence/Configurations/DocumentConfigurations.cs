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

/// <summary>Cấu hình bảng AppraisalOpinion – ý kiến thẩm định của thành viên chuyên môn (lát H1, design.md mục 18.25).</summary>
public sealed class AppraisalOpinionConfiguration : IEntityTypeConfiguration<AppraisalOpinion>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AppraisalOpinion> builder)
    {
        builder.ToTable("AppraisalOpinion");
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.OwnerType).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Content).HasMaxLength(AppraisalOpinion.ContentMaxLength).IsRequired();
        builder.Property(x => x.WithdrawReason).HasMaxLength(AppraisalOpinion.ReasonMaxLength);
        // Như đính kèm: chủ nằm ở nhiều bảng (OwnerType) nên không có khóa ngoại tới chủ; truy vấn chính là ý kiến của một đối tượng.
        builder.HasIndex(x => new { x.OwnerType, x.OwnerId });
        builder.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>
/// Cấu hình bảng UserSignature – chữ ký người dùng theo phiên bản (lát IN1, design.md mục 20.4). Mỗi người một dãy số phiên bản; tra chữ ký lúc
/// in theo (UserId, ValidFrom).
/// </summary>
public sealed class UserSignatureConfiguration : IEntityTypeConfiguration<UserSignature>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<UserSignature> builder)
    {
        builder.ToTable("UserSignature");
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Image).IsRequired();
        builder.Property(x => x.Reason).HasMaxLength(UserSignature.ReasonMaxLength);
        builder.Property(x => x.RetireReason).HasMaxLength(UserSignature.ReasonMaxLength);
        builder.HasIndex(x => new { x.UserId, x.VersionNo }).IsUnique();
        // Mỗi người tối đa một chữ ký đang dùng – chặn hai lần tải cùng lúc tạo hai bản đang dùng.
        builder.HasIndex(x => x.UserId).IsUnique().HasFilter("\"ValidTo\" IS NULL").HasDatabaseName("IX_UserSignature_UserId_Current");
        builder.HasOne<Proc.Infrastructure.Identity.AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>
/// Cấu hình bảng DocumentPrintLog – nhật ký in (lát IN1, design.md mục 20.4). Lần in thứ n duy nhất theo phiếu: hai người bấm In cùng lúc thì
/// một người lấy số kế tiếp ở lần thử lại.
/// </summary>
public sealed class DocumentPrintLogConfiguration : IEntityTypeConfiguration<DocumentPrintLog>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<DocumentPrintLog> builder)
    {
        builder.ToTable("DocumentPrintLog");
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.DocType).HasMaxLength(10).IsRequired();
        builder.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.ApprovalStatus).HasMaxLength(200).IsRequired();
        builder.HasIndex(x => new { x.DocType, x.DocumentId, x.PrintNo }).IsUnique();
        builder.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
    }
}
