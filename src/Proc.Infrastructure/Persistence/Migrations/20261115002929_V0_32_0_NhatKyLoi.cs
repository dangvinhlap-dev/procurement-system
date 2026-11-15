using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proc.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Lát QT2 – nhật ký lỗi (màn hình ERR, design.md mục 21.4): bảng ErrorGroup (nhóm lỗi, dấu vết duy nhất), ErrorOccurrence (từng lần, xóa theo
    /// nhóm) và cột tham số ErrorLogKeepDays (ERRLOG_KEEP_DAYS, mặc định 90). Down() mất toàn bộ nhật ký lỗi và giá trị tham số đã sửa – chỉ là
    /// nhật ký kỹ thuật, không phải dữ liệu nghiệp vụ.
    /// </summary>
    public partial class V0_32_0_NhatKyLoi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ErrorLogKeepDays",
                table: "CompanyParameter",
                type: "integer",
                nullable: false,
                defaultValue: 90);

            migrationBuilder.CreateTable(
                name: "ErrorGroup",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Source = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ErrorType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Location = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    FirstSeenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastSeenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    OccurrenceCount = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    HandledByName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    HandledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ReopenCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ErrorGroup", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ErrorOccurrence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TrackingCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Source = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Method = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Detail = table.Column<string>(type: "character varying(16384)", maxLength: 16384, nullable: true),
                    Version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Environment = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ErrorOccurrence", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ErrorOccurrence_ErrorGroup_GroupId",
                        column: x => x.GroupId,
                        principalTable: "ErrorGroup",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ErrorGroup_Fingerprint",
                table: "ErrorGroup",
                column: "Fingerprint",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ErrorGroup_LastSeenAt",
                table: "ErrorGroup",
                column: "LastSeenAt");

            migrationBuilder.CreateIndex(
                name: "IX_ErrorOccurrence_GroupId_OccurredAt",
                table: "ErrorOccurrence",
                columns: new[] { "GroupId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ErrorOccurrence_OccurredAt",
                table: "ErrorOccurrence",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_ErrorOccurrence_TrackingCode",
                table: "ErrorOccurrence",
                column: "TrackingCode");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ErrorOccurrence");

            migrationBuilder.DropTable(
                name: "ErrorGroup");

            migrationBuilder.DropColumn(
                name: "ErrorLogKeepDays",
                table: "CompanyParameter");
        }
    }
}
