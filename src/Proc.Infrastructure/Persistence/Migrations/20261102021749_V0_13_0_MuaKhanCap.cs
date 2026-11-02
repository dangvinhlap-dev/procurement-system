using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proc.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Lát C2a (design.md mục 18.11): loại mua của PO (thường/khẩn cấp), lý do khẩn, hạn bổ sung hồ sơ và xác nhận đã bổ sung. PO cũ là mua thường.
    /// </summary>
    public partial class V0_13_0_MuaKhanCap : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DocsCompletedAt",
                table: "PurchaseOrder",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DocsCompletedBy",
                table: "PurchaseOrder",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DocsCompletionNote",
                table: "PurchaseOrder",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "DocsDueDate",
                table: "PurchaseOrder",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmergencyReason",
                table: "PurchaseOrder",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PurchaseType",
                table: "PurchaseOrder",
                type: "integer",
                nullable: false,
                defaultValue: 1);
        }

        /// <summary>Gỡ các cột – MẤT DỮ LIỆU: loại mua khẩn cấp, lý do, hạn và xác nhận bổ sung hồ sơ trên PO (AuditLog vẫn giữ).</summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DocsCompletedAt",
                table: "PurchaseOrder");

            migrationBuilder.DropColumn(
                name: "DocsCompletedBy",
                table: "PurchaseOrder");

            migrationBuilder.DropColumn(
                name: "DocsCompletionNote",
                table: "PurchaseOrder");

            migrationBuilder.DropColumn(
                name: "DocsDueDate",
                table: "PurchaseOrder");

            migrationBuilder.DropColumn(
                name: "EmergencyReason",
                table: "PurchaseOrder");

            migrationBuilder.DropColumn(
                name: "PurchaseType",
                table: "PurchaseOrder");
        }
    }
}
