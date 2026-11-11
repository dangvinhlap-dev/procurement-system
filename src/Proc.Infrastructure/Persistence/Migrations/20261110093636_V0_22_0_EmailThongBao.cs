using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proc.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Lát O2b (design.md mục 19.3): trạng thái gửi email bản sao của thông báo. Thông báo có trước bản này đánh dấu "Bỏ qua" (2) để khi bật email
    /// không gửi dồn thư cũ.
    /// </summary>
    public partial class V0_22_0_EmailThongBao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EmailAttempts",
                table: "Notification",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "EmailNote",
                table: "Notification",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EmailState",
                table: "Notification",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EmailedAt",
                table: "Notification",
                type: "timestamp with time zone",
                nullable: true);

            // Thông báo cũ không gửi email (2 = Skipped). Câu SQL kết thúc bằng ";" – script idempotent bọc trong khối DO (db/scripts/README.md).
            migrationBuilder.Sql("UPDATE \"Notification\" SET \"EmailState\" = 2, \"EmailNote\" = 'Thông báo có trước bản 0.22.0.';");

            migrationBuilder.CreateIndex(
                name: "IX_Notification_EmailPending",
                table: "Notification",
                column: "CreatedAt",
                filter: "\"EmailState\" = 0");
        }

        /// <inheritdoc />
        /// <remarks>Mất dữ liệu: trạng thái gửi email của thông báo (thông báo vẫn giữ).</remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Notification_EmailPending",
                table: "Notification");

            migrationBuilder.DropColumn(
                name: "EmailAttempts",
                table: "Notification");

            migrationBuilder.DropColumn(
                name: "EmailNote",
                table: "Notification");

            migrationBuilder.DropColumn(
                name: "EmailState",
                table: "Notification");

            migrationBuilder.DropColumn(
                name: "EmailedAt",
                table: "Notification");
        }
    }
}
