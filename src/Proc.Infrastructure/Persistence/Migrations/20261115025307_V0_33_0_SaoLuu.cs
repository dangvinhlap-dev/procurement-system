using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proc.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Lát QT3b – sao lưu Full / Diff / pg_dump / đính kèm (màn hình BAK, design.md mục 21.5): bảng BackupRun (lịch sử sao lưu, một lượt lịch một
    /// lần mỗi kiểu) và bốn cột tham số BACKUP_FULL_DAY (7 = Chủ nhật), BACKUP_TIME (01:00), BACKUP_KEEP_WEEKS (4), DUMP_KEEP_DAYS (14). Down() mất
    /// lịch sử sao lưu (file sao lưu trên đĩa vẫn còn) và giá trị tham số đã sửa – không phải dữ liệu nghiệp vụ.
    /// </summary>
    public partial class V0_33_0_SaoLuu : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BackupFullDay",
                table: "CompanyParameter",
                type: "integer",
                nullable: false,
                defaultValue: 7);

            migrationBuilder.AddColumn<int>(
                name: "BackupKeepWeeks",
                table: "CompanyParameter",
                type: "integer",
                nullable: false,
                defaultValue: 4);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "BackupTime",
                table: "CompanyParameter",
                type: "time without time zone",
                nullable: false,
                defaultValue: new TimeOnly(1, 0, 0));

            migrationBuilder.AddColumn<int>(
                name: "DumpKeepDays",
                table: "CompanyParameter",
                type: "integer",
                nullable: false,
                defaultValue: 14);

            migrationBuilder.CreateTable(
                name: "BackupRun",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Trigger = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ScheduledDate = table.Column<DateOnly>(type: "date", nullable: true),
                    RequestedByName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    QueuedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FinishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RelativePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: true),
                    FileCount = table.Column<int>(type: "integer", nullable: true),
                    Sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    BaseFullRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    StartWalFile = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: true),
                    Verified = table.Column<bool>(type: "boolean", nullable: true),
                    VerifyMessage = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    SecondaryCopied = table.Column<bool>(type: "boolean", nullable: true),
                    SecondaryMessage = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Note = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    PurgedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BackupRun", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BackupRun_Kind_ScheduledDate",
                table: "BackupRun",
                columns: new[] { "Kind", "ScheduledDate" },
                unique: true,
                filter: "\"ScheduledDate\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BackupRun_QueuedAt",
                table: "BackupRun",
                column: "QueuedAt");

            migrationBuilder.CreateIndex(
                name: "IX_BackupRun_Status_QueuedAt",
                table: "BackupRun",
                columns: new[] { "Status", "QueuedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BackupRun");

            migrationBuilder.DropColumn(
                name: "BackupFullDay",
                table: "CompanyParameter");

            migrationBuilder.DropColumn(
                name: "BackupKeepWeeks",
                table: "CompanyParameter");

            migrationBuilder.DropColumn(
                name: "BackupTime",
                table: "CompanyParameter");

            migrationBuilder.DropColumn(
                name: "DumpKeepDays",
                table: "CompanyParameter");
        }
    }
}
