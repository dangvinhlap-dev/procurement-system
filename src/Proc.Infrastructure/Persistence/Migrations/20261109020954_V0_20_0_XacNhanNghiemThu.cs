using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proc.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Lát H4: xác nhận nghiệm thu của đơn vị đặt hàng trên phiếu nhập mua (Acceptance, AcceptanceNote, AcceptedBy, AcceptedAt).
    /// Down() xóa bốn cột – mất kết quả và ý kiến nghiệm thu đã ghi (AuditLog vẫn giữ lịch sử).
    /// </summary>
    public partial class V0_20_0_XacNhanNghiemThu : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Acceptance",
                table: "InventoryDocument",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "AcceptanceNote",
                table: "InventoryDocument",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AcceptedAt",
                table: "InventoryDocument",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AcceptedBy",
                table: "InventoryDocument",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Acceptance",
                table: "InventoryDocument");

            migrationBuilder.DropColumn(
                name: "AcceptanceNote",
                table: "InventoryDocument");

            migrationBuilder.DropColumn(
                name: "AcceptedAt",
                table: "InventoryDocument");

            migrationBuilder.DropColumn(
                name: "AcceptedBy",
                table: "InventoryDocument");
        }
    }
}
