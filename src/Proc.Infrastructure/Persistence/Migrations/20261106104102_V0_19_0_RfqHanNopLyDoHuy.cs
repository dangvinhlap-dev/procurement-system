using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proc.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Lát R7 (design.md mục 18.26): RFQ lưu giải trình ít báo giá (FewQuotesReason) và lý do gia hạn (DeadlineExtensionReason); PR/PO lưu
    /// lý do hủy (CancelReason). Down() xóa các cột – mất các lý do đã ghi (nhật ký AuditLog vẫn giữ).
    /// </summary>
    public partial class V0_19_0_RfqHanNopLyDoHuy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DeadlineExtensionReason",
                table: "Rfq",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FewQuotesReason",
                table: "Rfq",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CancelReason",
                table: "PurchaseRequest",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CancelReason",
                table: "PurchaseOrder",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeadlineExtensionReason",
                table: "Rfq");

            migrationBuilder.DropColumn(
                name: "FewQuotesReason",
                table: "Rfq");

            migrationBuilder.DropColumn(
                name: "CancelReason",
                table: "PurchaseRequest");

            migrationBuilder.DropColumn(
                name: "CancelReason",
                table: "PurchaseOrder");
        }
    }
}
