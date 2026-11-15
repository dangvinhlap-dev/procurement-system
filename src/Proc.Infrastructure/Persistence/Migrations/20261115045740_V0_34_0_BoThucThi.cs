using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proc.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Lát QT5 – bộ thực thi và chế độ bảo trì (ke-hoach-qt.md QT5): bảng MaintenanceModeState (một dòng bật / tắt), AgentEnvironment (môi trường
    /// có bộ thực thi, băm khóa, lần liên lạc cuối), AgentCommand (lệnh, mỗi môi trường chỉ một lệnh Đang chạy – chỉ mục lọc). Down() mất khai báo
    /// môi trường, khóa (phải tạo lại khóa, dán lại vào máy) và lịch sử lệnh – không phải dữ liệu nghiệp vụ.
    /// </summary>
    public partial class V0_34_0_BoThucThi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AgentEnvironment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    AgentKeyHash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: true),
                    AgentKeyHint = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    KeyCreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    KeyCreatedByName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    LastSeenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AgentVersion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    AppVersion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    HostName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentEnvironment", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MaintenanceModeState",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IsOn = table.Column<bool>(type: "boolean", nullable: false),
                    Message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ChangedByName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ChangedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceModeState", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AgentCommand",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EnvironmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Parameters = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CreatedByName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FinishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ResultMessage = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Journal = table.Column<string>(type: "character varying(64000)", maxLength: 64000, nullable: true),
                    CancelledByName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentCommand", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgentCommand_AgentEnvironment_EnvironmentId",
                        column: x => x.EnvironmentId,
                        principalTable: "AgentEnvironment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgentCommand_CreatedAt",
                table: "AgentCommand",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AgentCommand_EnvironmentId_Status_CreatedAt",
                table: "AgentCommand",
                columns: new[] { "EnvironmentId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AgentCommand_OneRunning",
                table: "AgentCommand",
                column: "EnvironmentId",
                unique: true,
                filter: "\"Status\" = 'Running'");

            migrationBuilder.CreateIndex(
                name: "IX_AgentEnvironment_AgentKeyHash",
                table: "AgentEnvironment",
                column: "AgentKeyHash",
                unique: true,
                filter: "\"AgentKeyHash\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AgentEnvironment_Code",
                table: "AgentEnvironment",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgentCommand");

            migrationBuilder.DropTable(
                name: "MaintenanceModeState");

            migrationBuilder.DropTable(
                name: "AgentEnvironment");
        }
    }
}
