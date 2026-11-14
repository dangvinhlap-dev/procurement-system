using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proc.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Lát QT1 (design.md mục 21): bảng nhật ký vận hành <c>OperationLog</c>, chỉ thêm – trigger dùng lại hàm <c>log_prevent_change()</c> của
    /// AuditLog (migration V0_1_0_NhatKyThaoTac) chặn UPDATE / DELETE. Down xóa bảng – mất các dòng nhật ký vận hành trong CSDL (bản ở file ngày
    /// <c>Operations:LogDirectory</c> vẫn còn).
    /// </summary>
    public partial class V0_32_0_NhatKyVanHanh : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OperationLog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Environment = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Kind = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Target = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Result = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Detail = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperationLog", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OperationLog_Kind_OccurredAt",
                table: "OperationLog",
                columns: new[] { "Kind", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_OperationLog_OccurredAt",
                table: "OperationLog",
                column: "OccurredAt");

            migrationBuilder.Sql("""
                CREATE TRIGGER "TR_OperationLog_ChiThem" BEFORE UPDATE OR DELETE ON "OperationLog"
                    FOR EACH ROW EXECUTE FUNCTION log_prevent_change();
                """);
        }

        /// <inheritdoc />
        /// <remarks>Mất dữ liệu: xóa bảng OperationLog (trigger đi theo bảng).</remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OperationLog");
        }
    }
}
