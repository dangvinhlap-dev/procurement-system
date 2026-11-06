using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proc.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Lát R6 (design.md mục 18.26): lý do, người, thời điểm đóng phần còn lại của PR/PO (ClosedReason/ClosedBy/ClosedAt).
    /// Down() xóa các cột – mất lý do đóng; phiếu vẫn ở trạng thái Đã đóng nhưng không còn đánh dấu đóng tay.
    /// </summary>
    public partial class V0_19_0_DongPhanConLai : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ClosedAt",
                table: "PurchaseRequest",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ClosedBy",
                table: "PurchaseRequest",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClosedReason",
                table: "PurchaseRequest",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ClosedAt",
                table: "PurchaseOrder",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ClosedBy",
                table: "PurchaseOrder",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClosedReason",
                table: "PurchaseOrder",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClosedAt",
                table: "PurchaseRequest");

            migrationBuilder.DropColumn(
                name: "ClosedBy",
                table: "PurchaseRequest");

            migrationBuilder.DropColumn(
                name: "ClosedReason",
                table: "PurchaseRequest");

            migrationBuilder.DropColumn(
                name: "ClosedAt",
                table: "PurchaseOrder");

            migrationBuilder.DropColumn(
                name: "ClosedBy",
                table: "PurchaseOrder");

            migrationBuilder.DropColumn(
                name: "ClosedReason",
                table: "PurchaseOrder");
        }
    }
}
