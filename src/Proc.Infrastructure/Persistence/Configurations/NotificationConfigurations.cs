using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Proc.Domain.Notifications;
using Proc.Domain.Organization;
using Proc.Infrastructure.Identity;

namespace Proc.Infrastructure.Persistence.Configurations;

/// <summary>Cấu hình bảng Notification – thông báo (design.md mục 11.6).</summary>
internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notification");
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Type).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Title).HasMaxLength(Notification.TitleMaxLength).IsRequired();
        builder.Property(x => x.Message).HasMaxLength(Notification.MessageMaxLength).IsRequired();
        builder.Property(x => x.ScreenCode).HasMaxLength(20).IsRequired();
        // Truy vấn chính: thông báo của một người tại một công ty, mới nhất trước; đếm chưa đọc.
        builder.HasIndex(x => new { x.UserId, x.CompanyId, x.CreatedAt });
        // Email bản sao (lát O2b): dịch vụ nền quét thông báo chờ gửi theo thời gian tạo. Chỉ mục lọc nên chỉ chứa dòng còn chờ – nhỏ.
        builder.Property(x => x.EmailNote).HasMaxLength(Notification.EmailNoteMaxLength);
        builder.HasIndex(x => x.CreatedAt).HasFilter("\"EmailState\" = 0").HasDatabaseName("IX_Notification_EmailPending");
        builder.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
