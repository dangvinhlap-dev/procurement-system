using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proc.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Lát X7 (design.md mục 19.3): PO ghi công ty bán khi đặt cho NCC nội bộ (PO cũ điền lại theo NCC, trừ NCC nội bộ của chính công ty
    /// của PO); bảng InternalSaleConfirmation lưu quyết định xác nhận / từ chối của công ty bán.
    /// </summary>
    public partial class V0_24_0_DonBanNoiBo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "InternalSellerCompanyId",
                table: "PurchaseOrder",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "InternalSaleConfirmation",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchaseOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderNo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    BuyerCompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Decision = table.Column<int>(type: "integer", nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InternalSaleConfirmation", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InternalSaleConfirmation_Company_BuyerCompanyId",
                        column: x => x.BuyerCompanyId,
                        principalTable: "Company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InternalSaleConfirmation_Company_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InternalSaleConfirmation_PurchaseOrder_PurchaseOrderId",
                        column: x => x.PurchaseOrderId,
                        principalTable: "PurchaseOrder",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrder_InternalSellerCompanyId",
                table: "PurchaseOrder",
                column: "InternalSellerCompanyId",
                filter: "\"InternalSellerCompanyId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_InternalSaleConfirmation_BuyerCompanyId",
                table: "InternalSaleConfirmation",
                column: "BuyerCompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_InternalSaleConfirmation_CompanyId",
                table: "InternalSaleConfirmation",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_InternalSaleConfirmation_PurchaseOrderId",
                table: "InternalSaleConfirmation",
                column: "PurchaseOrderId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseOrder_Company_InternalSellerCompanyId",
                table: "PurchaseOrder",
                column: "InternalSellerCompanyId",
                principalTable: "Company",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql("""
                UPDATE "PurchaseOrder" AS o
                SET "InternalSellerCompanyId" = s."InternalCompanyId"
                FROM "Supplier" AS s
                WHERE s."Id" = o."SupplierId" AND s."InternalCompanyId" IS NOT NULL AND s."InternalCompanyId" <> o."CompanyId";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Mất dữ liệu: quyết định xác nhận / từ chối đơn bán nội bộ (không tính lại được); công ty bán trên PO tính lại được từ NCC.
            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseOrder_Company_InternalSellerCompanyId",
                table: "PurchaseOrder");

            migrationBuilder.DropTable(
                name: "InternalSaleConfirmation");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrder_InternalSellerCompanyId",
                table: "PurchaseOrder");

            migrationBuilder.DropColumn(
                name: "InternalSellerCompanyId",
                table: "PurchaseOrder");
        }
    }
}
