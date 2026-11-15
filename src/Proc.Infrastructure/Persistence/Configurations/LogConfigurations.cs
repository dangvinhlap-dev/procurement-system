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

/// <summary>
/// Bảng ErrorGroup – nhóm lỗi (lát QT2). Không chỉ thêm như các bảng log khác: người xử lý đổi trạng thái, máy chủ tăng số lần, dịch vụ nền
/// xóa nhóm hết hạn giữ.
/// </summary>
public sealed class ErrorGroupConfiguration : IEntityTypeConfiguration<Proc.Domain.Operations.ErrorGroup>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Proc.Domain.Operations.ErrorGroup> builder)
    {
        builder.ToTable("ErrorGroup");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Fingerprint).HasMaxLength(Proc.Domain.Operations.ErrorGroup.FingerprintLength).IsRequired();
        builder.HasIndex(x => x.Fingerprint).IsUnique();
        builder.Property(x => x.Source).HasConversion<string>().HasMaxLength(10);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.ErrorType).HasMaxLength(Proc.Domain.Operations.ErrorGroup.TypeMaxLength).IsRequired();
        builder.Property(x => x.Message).HasMaxLength(Proc.Domain.Operations.ErrorGroup.TextMaxLength).IsRequired();
        builder.Property(x => x.Location).HasMaxLength(Proc.Domain.Operations.ErrorGroup.TextMaxLength);
        builder.Property(x => x.Path).HasMaxLength(Proc.Domain.Operations.ErrorGroup.PathMaxLength);
        builder.Property(x => x.HandledByName).HasMaxLength(256);
        builder.Property(x => x.Note).HasMaxLength(Proc.Domain.Operations.ErrorGroup.NoteMaxLength);
        builder.HasIndex(x => x.LastSeenAt);
    }
}

/// <summary>Bảng ErrorOccurrence – từng lần xảy ra lỗi (lát QT2); xóa theo nhóm (cascade) và theo hạn giữ.</summary>
public sealed class ErrorOccurrenceConfiguration : IEntityTypeConfiguration<Proc.Domain.Operations.ErrorOccurrence>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Proc.Domain.Operations.ErrorOccurrence> builder)
    {
        builder.ToTable("ErrorOccurrence");
        builder.HasKey(x => x.Id);
        builder.HasOne<Proc.Domain.Operations.ErrorGroup>().WithMany().HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.Cascade);
        builder.Property(x => x.TrackingCode).HasMaxLength(Proc.Domain.Operations.ErrorOccurrence.TrackingCodeMaxLength).IsRequired();
        builder.HasIndex(x => x.TrackingCode);
        builder.Property(x => x.Source).HasConversion<string>().HasMaxLength(10);
        builder.Property(x => x.UserName).HasMaxLength(256);
        builder.Property(x => x.CompanyCode).HasMaxLength(50);
        builder.Property(x => x.Path).HasMaxLength(Proc.Domain.Operations.ErrorGroup.PathMaxLength);
        builder.Property(x => x.Method).HasMaxLength(10);
        builder.Property(x => x.Message).HasMaxLength(Proc.Domain.Operations.ErrorGroup.TextMaxLength).IsRequired();
        builder.Property(x => x.Detail).HasMaxLength(Proc.Domain.Operations.ErrorOccurrence.DetailMaxLength);
        builder.Property(x => x.Version).HasMaxLength(50);
        builder.Property(x => x.Environment).HasMaxLength(10);
        builder.Property(x => x.UserAgent).HasMaxLength(500);
        builder.HasIndex(x => new { x.GroupId, x.OccurredAt });
        builder.HasIndex(x => x.OccurredAt);
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

/// <summary>
/// Bảng BackupRun – lịch sử sao lưu (lát QT3b). Không chỉ thêm: bộ chạy cập nhật trạng thái, dọn theo hạn giữ ghi lúc dọn; dòng không xóa
/// (giữ lịch sử đã từng có bản nào).
/// </summary>
public sealed class BackupRunConfiguration : IEntityTypeConfiguration<Proc.Domain.Operations.BackupRun>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Proc.Domain.Operations.BackupRun> builder)
    {
        builder.ToTable("BackupRun");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Trigger).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.RequestedByName).HasMaxLength(256);
        builder.Property(x => x.RelativePath).HasMaxLength(Proc.Domain.Operations.BackupRun.PathMaxLength);
        builder.Property(x => x.Sha256).HasMaxLength(64);
        builder.Property(x => x.StartWalFile).HasMaxLength(Proc.Domain.Operations.BackupRun.WalFileLength);
        builder.Property(x => x.VerifyMessage).HasMaxLength(Proc.Domain.Operations.BackupRun.MessageMaxLength);
        builder.Property(x => x.SecondaryMessage).HasMaxLength(Proc.Domain.Operations.BackupRun.MessageMaxLength);
        builder.Property(x => x.ErrorMessage).HasMaxLength(Proc.Domain.Operations.BackupRun.MessageMaxLength);
        builder.Property(x => x.Note).HasMaxLength(Proc.Domain.Operations.BackupRun.MessageMaxLength);
        builder.Ignore(x => x.IsUsable);
        builder.HasIndex(x => x.QueuedAt);
        builder.HasIndex(x => new { x.Status, x.QueuedAt });
        // Một lượt lịch chỉ sinh một lần mỗi kiểu (lát QT3b-2) – nhiều tiến trình cùng đặt lịch thì lần sau vi phạm khóa, không sao lưu trùng.
        builder.HasIndex(x => new { x.Kind, x.ScheduledDate }).IsUnique().HasFilter("\"ScheduledDate\" IS NOT NULL");
    }
}

/// <summary>
/// Bảng MaintenanceRun – lịch sử bảo trì CSDL (lát QT4): VACUUM, REINDEX, ngắt phiên. Bộ chạy cập nhật trạng thái; dòng không xóa.
/// </summary>
public sealed class MaintenanceRunConfiguration : IEntityTypeConfiguration<Proc.Domain.Operations.MaintenanceRun>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Proc.Domain.Operations.MaintenanceRun> builder)
    {
        builder.ToTable("MaintenanceRun");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Kind).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.Trigger).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Target).HasMaxLength(Proc.Domain.Operations.MaintenanceRun.TargetMaxLength);
        builder.Property(x => x.Reason).HasMaxLength(Proc.Domain.Operations.MaintenanceRun.ReasonMaxLength);
        builder.Property(x => x.RequestedByName).HasMaxLength(256);
        builder.Property(x => x.Note).HasMaxLength(Proc.Domain.Operations.MaintenanceRun.MessageMaxLength);
        builder.Property(x => x.ErrorMessage).HasMaxLength(Proc.Domain.Operations.MaintenanceRun.MessageMaxLength);
        builder.HasIndex(x => x.QueuedAt);
        builder.HasIndex(x => new { x.Status, x.QueuedAt });
        // Một lượt lịch tuần chỉ sinh một lần (nhiều tiến trình cùng đặt lịch thì lần sau vi phạm khóa).
        builder.HasIndex(x => new { x.Kind, x.ScheduledDate }).IsUnique().HasFilter("\"ScheduledDate\" IS NOT NULL");
    }
}

/// <summary>
/// Bảng ReleaseTicket – phiếu phát hành (lát QT8) và ba bảng con: mục kiểm thử, lỗi phát hiện, lịch sử cài. Phiên bản không trùng giữa các phiếu
/// chưa hủy (nhận lại gói sau khi hủy phiếu cũ được).
/// </summary>
public sealed class ReleaseTicketConfiguration : IEntityTypeConfiguration<Proc.Domain.Operations.ReleaseTicket>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Proc.Domain.Operations.ReleaseTicket> builder)
    {
        builder.ToTable("ReleaseTicket");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Version).HasMaxLength(Proc.Domain.Operations.ReleaseTicket.VersionMaxLength).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Source).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.MinRollbackLevel).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.SourceUrl).HasMaxLength(500);
        builder.Property(x => x.PackageFileName).HasMaxLength(260).IsRequired();
        builder.Property(x => x.Sha256).HasMaxLength(Proc.Domain.Operations.ReleaseTicket.Sha256Length).IsRequired();
        builder.Property(x => x.Changelog).HasMaxLength(Proc.Domain.Operations.ReleaseTicket.ChangelogMaxLength);
        builder.Property(x => x.DatabaseChanges).HasMaxLength(Proc.Domain.Operations.ReleaseTicket.TextMaxLength);
        builder.Property(x => x.Risks).HasMaxLength(Proc.Domain.Operations.ReleaseTicket.TextMaxLength);
        builder.Property(x => x.RollbackPlan).HasMaxLength(Proc.Domain.Operations.ReleaseTicket.TextMaxLength);
        builder.Property(x => x.Note).HasMaxLength(Proc.Domain.Operations.ReleaseTicket.TextMaxLength);
        builder.Property(x => x.UatComment).HasMaxLength(Proc.Domain.Operations.ReleaseTicket.TextMaxLength);
        builder.Property(x => x.EndReason).HasMaxLength(Proc.Domain.Operations.ReleaseTicket.TextMaxLength);
        builder.Property(x => x.ReceivedByName).HasMaxLength(256);
        builder.Property(x => x.UatSignedByName).HasMaxLength(256);
        builder.Property(x => x.EndedByName).HasMaxLength(256);
        builder.HasIndex(x => x.Version).IsUnique().HasFilter("\"Status\" <> 'Cancelled'");
        builder.HasMany(x => x.Items).WithOne().HasForeignKey(i => i.TicketId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Items).HasField("_items").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasMany(x => x.Defects).WithOne().HasForeignKey(d => d.TicketId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Defects).HasField("_defects").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasMany(x => x.Installs).WithOne().HasForeignKey(i => i.TicketId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Installs).HasField("_installs").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

/// <summary>Bảng ReleaseTestItem – mục kiểm thử UAT của phiếu phát hành.</summary>
public sealed class ReleaseTestItemConfiguration : IEntityTypeConfiguration<Proc.Domain.Operations.ReleaseTestItem>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Proc.Domain.Operations.ReleaseTestItem> builder)
    {
        builder.ToTable("ReleaseTestItem");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Section).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Content).HasMaxLength(Proc.Domain.Operations.ReleaseTestItem.ContentMaxLength).IsRequired();
        builder.Property(x => x.Result).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.TesterName).HasMaxLength(256);
        builder.Property(x => x.Comment).HasMaxLength(Proc.Domain.Operations.ReleaseTicket.TextMaxLength);
    }
}

/// <summary>Bảng ReleaseDefect – lỗi phát hiện trong UAT.</summary>
public sealed class ReleaseDefectConfiguration : IEntityTypeConfiguration<Proc.Domain.Operations.ReleaseDefect>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Proc.Domain.Operations.ReleaseDefect> builder)
    {
        builder.ToTable("ReleaseDefect");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).HasMaxLength(Proc.Domain.Operations.ReleaseDefect.TitleMaxLength).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(Proc.Domain.Operations.ReleaseTicket.TextMaxLength);
        builder.Property(x => x.Screen).HasMaxLength(200);
        builder.Property(x => x.ReporterName).HasMaxLength(256);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Resolution).HasMaxLength(Proc.Domain.Operations.ReleaseTicket.TextMaxLength);
        builder.Property(x => x.ResolvedByName).HasMaxLength(256);
    }
}

/// <summary>Bảng ReleaseInstall – lịch sử cài / quay về, gắn lệnh bộ thực thi.</summary>
public sealed class ReleaseInstallConfiguration : IEntityTypeConfiguration<Proc.Domain.Operations.ReleaseInstall>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Proc.Domain.Operations.ReleaseInstall> builder)
    {
        builder.ToTable("ReleaseInstall");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Environment).HasMaxLength(10).IsRequired();
        builder.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Level).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.PreviousVersion).HasMaxLength(Proc.Domain.Operations.ReleaseTicket.VersionMaxLength);
        builder.Property(x => x.TargetVersion).HasMaxLength(Proc.Domain.Operations.ReleaseTicket.VersionMaxLength);
        builder.Property(x => x.InstalledSha256).HasMaxLength(Proc.Domain.Operations.ReleaseTicket.Sha256Length);
        builder.Property(x => x.Reason).HasMaxLength(Proc.Domain.Operations.ReleaseTicket.TextMaxLength);
        builder.Property(x => x.Message).HasMaxLength(Proc.Domain.Operations.ReleaseTicket.TextMaxLength);
        builder.Property(x => x.ByName).HasMaxLength(256);
        builder.HasIndex(x => x.AgentCommandId).IsUnique();
    }
}

/// <summary>Bảng AccessPolicy – cấu hình truy cập CFG (lát QT7): một dòng duy nhất; thay đổi vào AuditLog, lý do vào nhật ký vận hành.</summary>
public sealed class AccessPolicyConfiguration : IEntityTypeConfiguration<Proc.Domain.Operations.AccessPolicy>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Proc.Domain.Operations.AccessPolicy> builder)
    {
        builder.ToTable("AccessPolicy");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ExtraBlockedPasswords).HasMaxLength(Proc.Domain.Operations.AccessPolicy.ExtraBlockedPasswordsMaxLength);
        builder.Property(x => x.OpsAllowedIpRanges).HasMaxLength(Proc.Domain.Operations.AccessPolicy.OpsAllowedIpRangesMaxLength);
    }
}

/// <summary>Bảng MaintenanceModeState – chế độ bảo trì (lát QT5): một dòng duy nhất, lịch sử bật / tắt ở nhật ký vận hành.</summary>
public sealed class MaintenanceModeStateConfiguration : IEntityTypeConfiguration<Proc.Domain.Operations.MaintenanceModeState>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Proc.Domain.Operations.MaintenanceModeState> builder)
    {
        builder.ToTable("MaintenanceModeState");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Message).HasMaxLength(Proc.Domain.Operations.MaintenanceModeState.MessageMaxLength);
        builder.Property(x => x.Reason).HasMaxLength(Proc.Domain.Operations.MaintenanceModeState.ReasonMaxLength);
        builder.Property(x => x.ChangedByName).HasMaxLength(256);
    }
}

/// <summary>Bảng AgentEnvironment – môi trường có bộ thực thi (lát QT5): mỗi mã Dev / Test / Prod một dòng; chỉ lưu băm khóa.</summary>
public sealed class AgentEnvironmentConfiguration : IEntityTypeConfiguration<Proc.Domain.Operations.AgentEnvironment>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Proc.Domain.Operations.AgentEnvironment> builder)
    {
        builder.ToTable("AgentEnvironment");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(10).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(Proc.Domain.Operations.AgentEnvironment.NameMaxLength).IsRequired();
        builder.Property(x => x.Address).HasMaxLength(Proc.Domain.Operations.AgentEnvironment.AddressMaxLength);
        builder.Property(x => x.AgentKeyHash).HasMaxLength(Proc.Domain.Operations.AgentEnvironment.KeyHashLength).IsFixedLength();
        builder.Property(x => x.AgentKeyHint).HasMaxLength(8);
        builder.Property(x => x.KeyCreatedByName).HasMaxLength(256);
        builder.Property(x => x.AgentVersion).HasMaxLength(Proc.Domain.Operations.AgentEnvironment.InfoMaxLength);
        builder.Property(x => x.AppVersion).HasMaxLength(Proc.Domain.Operations.AgentEnvironment.InfoMaxLength);
        builder.Property(x => x.HostName).HasMaxLength(Proc.Domain.Operations.AgentEnvironment.InfoMaxLength);
        builder.HasIndex(x => x.Code).IsUnique();
        // Xác thực bộ thực thi tìm theo băm khóa; hai môi trường không bao giờ trùng khóa.
        builder.HasIndex(x => x.AgentKeyHash).IsUnique().HasFilter("\"AgentKeyHash\" IS NOT NULL");
    }
}

/// <summary>Bảng AgentCommand – lệnh giao bộ thực thi (lát QT5); dòng không xóa.</summary>
public sealed class AgentCommandConfiguration : IEntityTypeConfiguration<Proc.Domain.Operations.AgentCommand>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Proc.Domain.Operations.AgentCommand> builder)
    {
        builder.ToTable("AgentCommand");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Kind).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Parameters).HasMaxLength(Proc.Domain.Operations.AgentCommand.ParametersMaxLength);
        builder.Property(x => x.Reason).HasMaxLength(Proc.Domain.Operations.AgentCommand.ReasonMaxLength).IsRequired();
        builder.Property(x => x.CreatedByName).HasMaxLength(256);
        builder.Property(x => x.CancelledByName).HasMaxLength(256);
        builder.Property(x => x.ResultMessage).HasMaxLength(Proc.Domain.Operations.AgentCommand.MessageMaxLength);
        builder.Property(x => x.Journal).HasMaxLength(Proc.Domain.Operations.AgentCommand.JournalMaxLength);
        builder.HasOne<Proc.Domain.Operations.AgentEnvironment>().WithMany().HasForeignKey(x => x.EnvironmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.CreatedAt);
        builder.HasIndex(x => new { x.EnvironmentId, x.Status, x.CreatedAt });
        // Mỗi môi trường chỉ một lệnh Đang chạy (ke-hoach-qt.md QT5) – chặn ở CSDL, ngoài khóa dòng khi giao lệnh.
        builder.HasIndex(x => x.EnvironmentId).IsUnique().HasFilter("\"Status\" = 'Running'").HasDatabaseName("IX_AgentCommand_OneRunning");
    }
}

/// <summary>
/// Bảng RestoreRun – lịch sử phục hồi (lát QT6). Dòng không xóa. Không khóa ngoại tới BackupRun / AgentCommand: khi hoán đổi, bảng được chép sang
/// bản khôi phục cùng các bảng vận hành khác (<c>RestoreCarryOver</c>) – không phụ thuộc thứ tự chép.
/// </summary>
public sealed class RestoreRunConfiguration : IEntityTypeConfiguration<Proc.Domain.Operations.RestoreRun>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Proc.Domain.Operations.RestoreRun> builder)
    {
        builder.ToTable("RestoreRun");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.LiveDatabase).HasMaxLength(Proc.Domain.Operations.RestoreRun.DatabaseNameMaxLength).IsRequired();
        builder.Property(x => x.RestoreDatabase).HasMaxLength(Proc.Domain.Operations.RestoreRun.DatabaseNameMaxLength).IsRequired();
        builder.Property(x => x.OldDatabase).HasMaxLength(Proc.Domain.Operations.RestoreRun.TextMaxLength);
        builder.Property(x => x.RelativePath).HasMaxLength(Proc.Domain.Operations.RestoreRun.TextMaxLength);
        builder.Property(x => x.Reason).HasMaxLength(Proc.Domain.Operations.RestoreRun.TextMaxLength).IsRequired();
        builder.Property(x => x.SwapReason).HasMaxLength(Proc.Domain.Operations.RestoreRun.TextMaxLength);
        builder.Property(x => x.RequestedByName).HasMaxLength(256);
        builder.Property(x => x.SwapRequestedByName).HasMaxLength(256);
        builder.Property(x => x.DiscardedByName).HasMaxLength(256);
        builder.Property(x => x.PlanJson).HasMaxLength(Proc.Domain.Operations.RestoreRun.JsonMaxLength);
        builder.Property(x => x.ComparisonJson).HasMaxLength(Proc.Domain.Operations.RestoreRun.JsonMaxLength);
        builder.Property(x => x.Note).HasMaxLength(Proc.Domain.Operations.RestoreRun.MessageMaxLength);
        builder.Property(x => x.ErrorMessage).HasMaxLength(Proc.Domain.Operations.RestoreRun.MessageMaxLength);
        builder.HasIndex(x => x.QueuedAt);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.AgentCommandId);
    }
}
