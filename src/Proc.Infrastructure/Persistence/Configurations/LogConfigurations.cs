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

/// <summary>Bảng OperationLog – nhật ký vận hành (module QT, design.md mục 21).</summary>
public sealed class OperationLogConfiguration : IEntityTypeConfiguration<Proc.Domain.Operations.OperationLog>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Proc.Domain.Operations.OperationLog> builder)
    {
        // Bảng chỉ thêm: trigger "TR_OperationLog_ChiThem" (migration V0_32_0_NhatKyVanHanh) chặn UPDATE / DELETE như AuditLog.
        builder.ToTable("OperationLog");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Environment).HasMaxLength(10).IsRequired();
        builder.Property(x => x.UserName).HasMaxLength(256);
        builder.Property(x => x.Kind).HasMaxLength(Proc.Domain.Operations.OperationLog.KindMaxLength).IsRequired();
        builder.Property(x => x.Target).HasMaxLength(Proc.Domain.Operations.OperationLog.TextMaxLength);
        builder.Property(x => x.Reason).HasMaxLength(Proc.Domain.Operations.OperationLog.TextMaxLength);
        builder.Property(x => x.Result).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Detail).HasMaxLength(Proc.Domain.Operations.OperationLog.DetailMaxLength);
        builder.HasIndex(x => x.OccurredAt);
        builder.HasIndex(x => new { x.Kind, x.OccurredAt });
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
