using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proc.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Lát QT4 – bảo trì CSDL (màn hình DBM, design.md mục 21.4): bảng MaintenanceRun (lịch sử VACUUM, REINDEX, ngắt phiên; một lượt lịch tuần một
    /// lần) và hai cột tham số DB_MAINT_DAY (7 = Chủ nhật), DB_MAINT_TIME (03:00) – dòng tham số đã có nhận giá trị mặc định. Down() mất lịch sử
    /// bảo trì và giá trị tham số đã sửa – không phải dữ liệu nghiệp vụ.
    /// </summary>
    public partial class V0_33_0_BaoTriCsdl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DbMaintenanceDay",
                table: "CompanyParameter",
                type: "integer",
                nullable: false,
                defaultValue: 7);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "DbMaintenanceTime",
                table: "CompanyParameter",
                type: "time without time zone",
                nullable: false,
                defaultValue: new TimeOnly(3, 0, 0));

            migrationBuilder.CreateTable(
                name: "MaintenanceRun",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Trigger = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Target = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    SessionPid = table.Column<int>(type: "integer", nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ScheduledDate = table.Column<DateOnly>(type: "date", nullable: true),
                    RequestedByName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    QueuedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FinishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SizeBeforeBytes = table.Column<long>(type: "bigint", nullable: true),
                    SizeAfterBytes = table.Column<long>(type: "bigint", nullable: true),
                    Note = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceRun", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceRun_Kind_ScheduledDate",
                table: "MaintenanceRun",
                columns: new[] { "Kind", "ScheduledDate" },
                unique: true,
                filter: "\"ScheduledDate\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceRun_QueuedAt",
                table: "MaintenanceRun",
                column: "QueuedAt");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceRun_Status_QueuedAt",
                table: "MaintenanceRun",
                columns: new[] { "Status", "QueuedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MaintenanceRun");

            migrationBuilder.DropColumn(
                name: "DbMaintenanceDay",
                table: "CompanyParameter");

            migrationBuilder.DropColumn(
                name: "DbMaintenanceTime",
                table: "CompanyParameter");
        }
    }
}
