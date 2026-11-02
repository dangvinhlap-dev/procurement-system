using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proc.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Lát C1 (design.md mục 18.10): nguồn giá của dòng PO (chọn thầu / hợp đồng / bảng giá / ngoài hợp đồng), giá đã duyệt, lý do lệch
    /// giá; cờ "ngoài hợp đồng" và "ngoại lệ giá" trên PO. Dòng PO cũ nhận PriceSource = 0 (không xác định), PO cũ không gắn cờ.
    /// </summary>
    public partial class V0_12_0_NguonGiaPo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ApprovedDiscountPct",
                table: "PurchaseOrderLine",
                type: "numeric(9,4)",
                precision: 9,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ApprovedUnitPrice",
                table: "PurchaseOrderLine",
                type: "numeric(19,6)",
                precision: 19,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ContractId",
                table: "PurchaseOrderLine",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PriceDeviationReason",
                table: "PurchaseOrderLine",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PriceListId",
                table: "PurchaseOrderLine",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PriceSource",
                table: "PurchaseOrderLine",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "SourceRfqLineId",
                table: "PurchaseOrderLine",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasPriceException",
                table: "PurchaseOrder",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsOutOfContract",
                table: "PurchaseOrder",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderLine_ContractId",
                table: "PurchaseOrderLine",
                column: "ContractId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderLine_PriceListId",
                table: "PurchaseOrderLine",
                column: "PriceListId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderLine_SourceRfqLineId",
                table: "PurchaseOrderLine",
                column: "SourceRfqLineId");

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseOrderLine_PurchaseContract_ContractId",
                table: "PurchaseOrderLine",
                column: "ContractId",
                principalTable: "PurchaseContract",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseOrderLine_RfqLine_SourceRfqLineId",
                table: "PurchaseOrderLine",
                column: "SourceRfqLineId",
                principalTable: "RfqLine",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseOrderLine_SupplierPriceList_PriceListId",
                table: "PurchaseOrderLine",
                column: "PriceListId",
                principalTable: "SupplierPriceList",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <summary>Gỡ các cột nguồn giá – MẤT DỮ LIỆU: nguồn giá, giá đã duyệt, lý do lệch giá đã ghi trên PO (AuditLog vẫn giữ).</summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseOrderLine_PurchaseContract_ContractId",
                table: "PurchaseOrderLine");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseOrderLine_RfqLine_SourceRfqLineId",
                table: "PurchaseOrderLine");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseOrderLine_SupplierPriceList_PriceListId",
                table: "PurchaseOrderLine");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrderLine_ContractId",
                table: "PurchaseOrderLine");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrderLine_PriceListId",
                table: "PurchaseOrderLine");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrderLine_SourceRfqLineId",
                table: "PurchaseOrderLine");

            migrationBuilder.DropColumn(
                name: "ApprovedDiscountPct",
                table: "PurchaseOrderLine");

            migrationBuilder.DropColumn(
                name: "ApprovedUnitPrice",
                table: "PurchaseOrderLine");

            migrationBuilder.DropColumn(
                name: "ContractId",
                table: "PurchaseOrderLine");

            migrationBuilder.DropColumn(
                name: "PriceDeviationReason",
                table: "PurchaseOrderLine");

            migrationBuilder.DropColumn(
                name: "PriceListId",
                table: "PurchaseOrderLine");

            migrationBuilder.DropColumn(
                name: "PriceSource",
                table: "PurchaseOrderLine");

            migrationBuilder.DropColumn(
                name: "SourceRfqLineId",
                table: "PurchaseOrderLine");

            migrationBuilder.DropColumn(
                name: "HasPriceException",
                table: "PurchaseOrder");

            migrationBuilder.DropColumn(
                name: "IsOutOfContract",
                table: "PurchaseOrder");
        }
    }
}
