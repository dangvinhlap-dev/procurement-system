using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proc.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Lát QT8 – trung tâm phát hành (màn hình REL): phiếu phát hành ReleaseTicket + mục kiểm thử, lỗi phát hiện, lịch sử cài. Chỉ thêm bảng – bản
    /// trước chạy được trên CSDL này (quay về mức 1).
    /// </summary>
    public partial class V0_36_0_TrungTamPhatHanh : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReleaseTicket",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SourceUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PackageFileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    PackageSize = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Changelog = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: true),
                    DatabaseChanges = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    MinRollbackLevel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Risks = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    RollbackPlan = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Note = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ReceivedByName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UatSignedByName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    UatSignedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UatComment = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    UatWithoutTest = table.Column<bool>(type: "boolean", nullable: false),
                    ProdInstalledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EndedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EndedByName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    EndReason = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReleaseTicket", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReleaseDefect",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TicketId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Screen = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ReporterName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ReportedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Resolution = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ResolvedByName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReleaseDefect", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReleaseDefect_ReleaseTicket_TicketId",
                        column: x => x.TicketId,
                        principalTable: "ReleaseTicket",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReleaseInstall",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TicketId = table.Column<Guid>(type: "uuid", nullable: false),
                    Environment = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AgentCommandId = table.Column<Guid>(type: "uuid", nullable: false),
                    PreviousVersion = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    TargetVersion = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Level = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    InstalledSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Reason = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ByName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FinishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Message = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReleaseInstall", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReleaseInstall_ReleaseTicket_TicketId",
                        column: x => x.TicketId,
                        principalTable: "ReleaseTicket",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReleaseTestItem",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TicketId = table.Column<Guid>(type: "uuid", nullable: false),
                    Seq = table.Column<int>(type: "integer", nullable: false),
                    Section = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Content = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Result = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TesterName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    TestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Comment = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReleaseTestItem", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReleaseTestItem_ReleaseTicket_TicketId",
                        column: x => x.TicketId,
                        principalTable: "ReleaseTicket",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReleaseDefect_TicketId",
                table: "ReleaseDefect",
                column: "TicketId");

            migrationBuilder.CreateIndex(
                name: "IX_ReleaseInstall_AgentCommandId",
                table: "ReleaseInstall",
                column: "AgentCommandId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReleaseInstall_TicketId",
                table: "ReleaseInstall",
                column: "TicketId");

            migrationBuilder.CreateIndex(
                name: "IX_ReleaseTestItem_TicketId",
                table: "ReleaseTestItem",
                column: "TicketId");

            migrationBuilder.CreateIndex(
                name: "IX_ReleaseTicket_Version",
                table: "ReleaseTicket",
                column: "Version",
                unique: true,
                filter: "\"Status\" <> 'Cancelled'");
        }

        /// <summary>Xóa bốn bảng – mất lịch sử phiếu phát hành (bản cũ không dùng các bảng này).</summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReleaseDefect");

            migrationBuilder.DropTable(
                name: "ReleaseInstall");

            migrationBuilder.DropTable(
                name: "ReleaseTestItem");

            migrationBuilder.DropTable(
                name: "ReleaseTicket");
        }
    }
}
