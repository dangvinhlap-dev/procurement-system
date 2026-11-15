using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proc.Infrastructure.Persistence.Migrations
{
    /// <summary>Lát QT6 – phục hồi (màn hình RST): bảng RestoreRun lưu lịch sử khôi phục pg_dump / về mốc thời gian, so sánh, hoán đổi.</summary>
    public partial class V0_34_0_PhucHoi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RestoreRun",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DumpRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    FullRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    DiffRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    TargetTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LiveDatabase = table.Column<string>(type: "character varying(63)", maxLength: 63, nullable: false),
                    RestoreDatabase = table.Column<string>(type: "character varying(63)", maxLength: 63, nullable: false),
                    OldDatabase = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RelativePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TempPort = table.Column<int>(type: "integer", nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    RequestedByName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    QueuedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReadyAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SwapReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SwapRequestedByName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    SwapRequestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FinishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DiscardedByName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    AgentCommandId = table.Column<Guid>(type: "uuid", nullable: true),
                    PlanJson = table.Column<string>(type: "character varying(200000)", maxLength: 200000, nullable: true),
                    ComparisonJson = table.Column<string>(type: "character varying(200000)", maxLength: 200000, nullable: true),
                    CanSwap = table.Column<bool>(type: "boolean", nullable: true),
                    Note = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RestoreRun", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RestoreRun_AgentCommandId",
                table: "RestoreRun",
                column: "AgentCommandId");

            migrationBuilder.CreateIndex(
                name: "IX_RestoreRun_QueuedAt",
                table: "RestoreRun",
                column: "QueuedAt");

            migrationBuilder.CreateIndex(
                name: "IX_RestoreRun_Status",
                table: "RestoreRun",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Mất dữ liệu: xóa lịch sử phục hồi (bảng RestoreRun). CSDL khôi phục / CSDL cũ trên máy chủ không bị xóa – xóa tay nếu cần.
            migrationBuilder.DropTable(
                name: "RestoreRun");
        }
    }
}
