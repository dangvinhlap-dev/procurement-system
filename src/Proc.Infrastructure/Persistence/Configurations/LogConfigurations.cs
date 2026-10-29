using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Proc.Infrastructure.Logging;

namespace Proc.Infrastructure.Persistence.Configurations;

/// <summary>
/// Bảng AuditLog. Bảng thật được tạo bằng SQL trong migration (phân vùng theo tháng, trigger chỉ cho thêm);
/// cấu hình ở đây để EF Core đọc/ghi đúng cột.
/// </summary>
public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLog");
        // Khóa chính phải chứa cột phân vùng (OccurredAt) – yêu cầu của bảng phân vùng PostgreSQL.
        builder.HasKey(x => new { x.Id, x.OccurredAt });
        builder.Property(x => x.UserName).HasMaxLength(256);
        builder.Property(x => x.IpAddress).HasMaxLength(64);
        builder.Property(x => x.UserAgent).HasMaxLength(512);
        builder.Property(x => x.CorrelationId).HasMaxLength(128);
        builder.Property(x => x.EntityType).HasMaxLength(100).IsRequired();
        builder.Property(x => x.EntityId).HasMaxLength(200).IsRequired();
        builder.Property(x => x.EntityLabel).HasMaxLength(256);
        builder.Property(x => x.DocumentNumber).HasMaxLength(50);
        builder.Property(x => x.Action).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Changes).HasColumnType("jsonb").IsRequired();
        builder.HasIndex(x => new { x.CompanyId, x.OccurredAt });
        builder.HasIndex(x => new { x.UserId, x.OccurredAt });
        builder.HasIndex(x => new { x.EntityType, x.EntityId });
        builder.HasIndex(x => x.DocumentNumber);
    }
}

/// <summary>Bảng AccessLog (chỉ thêm – trigger trong migration).</summary>
public sealed class AccessLogConfiguration : IEntityTypeConfiguration<AccessLog>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AccessLog> builder)
    {
        builder.ToTable("AccessLog");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Event).HasMaxLength(30).IsRequired();
        builder.Property(x => x.UserName).HasMaxLength(256);
        builder.Property(x => x.Detail).HasMaxLength(500);
        builder.Property(x => x.IpAddress).HasMaxLength(64);
        builder.Property(x => x.UserAgent).HasMaxLength(512);
        builder.Property(x => x.CorrelationId).HasMaxLength(128);
        builder.HasIndex(x => x.OccurredAt);
        builder.HasIndex(x => new { x.UserId, x.OccurredAt });
        builder.HasIndex(x => new { x.CompanyId, x.OccurredAt });
    }
}
