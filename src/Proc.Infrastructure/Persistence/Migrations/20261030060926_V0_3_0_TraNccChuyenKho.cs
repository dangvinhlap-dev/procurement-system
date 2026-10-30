using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proc.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Phiếu trả NCC và chuyển kho (design.md mục 10.5, 10.6): cột LinkedDocumentId, Receiver của chứng từ, SourceLineId của dòng.
    /// Down() bỏ ba cột – MẤT liên kết phiếu trả ↔ phiếu nhập gốc và người nhận của phiếu chuyển.
    /// </summary>
    public partial class V0_3_0_TraNccChuyenKho : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SourceLineId",
                table: "InventoryDocumentLine",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LinkedDocumentId",
                table: "InventoryDocument",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Receiver",
                table: "InventoryDocument",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryDocumentLine_SourceLineId",
                table: "InventoryDocumentLine",
                column: "SourceLineId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryDocument_LinkedDocumentId",
                table: "InventoryDocument",
                column: "LinkedDocumentId");

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryDocument_InventoryDocument_LinkedDocumentId",
                table: "InventoryDocument",
                column: "LinkedDocumentId",
                principalTable: "InventoryDocument",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryDocumentLine_InventoryDocumentLine_SourceLineId",
                table: "InventoryDocumentLine",
                column: "SourceLineId",
                principalTable: "InventoryDocumentLine",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InventoryDocument_InventoryDocument_LinkedDocumentId",
                table: "InventoryDocument");

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryDocumentLine_InventoryDocumentLine_SourceLineId",
                table: "InventoryDocumentLine");

            migrationBuilder.DropIndex(
                name: "IX_InventoryDocumentLine_SourceLineId",
                table: "InventoryDocumentLine");

            migrationBuilder.DropIndex(
                name: "IX_InventoryDocument_LinkedDocumentId",
                table: "InventoryDocument");

            migrationBuilder.DropColumn(
                name: "SourceLineId",
                table: "InventoryDocumentLine");

            migrationBuilder.DropColumn(
                name: "LinkedDocumentId",
                table: "InventoryDocument");

            migrationBuilder.DropColumn(
                name: "Receiver",
                table: "InventoryDocument");
        }
    }
}
