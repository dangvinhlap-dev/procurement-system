using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proc.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Bản 0.5.0 – mua bán nội bộ (design.md mục 4, 6.3): bảng InternalPrice (bảng giá nội bộ), cột InventoryDocument.CounterCompanyId
    /// (công ty đối ứng của phiếu xuất bán/nhập mua nội bộ). Down() xóa bảng và cột – mất bảng giá nội bộ và liên kết công ty đối ứng
    /// của các phiếu bán/nhập nội bộ (phiếu vẫn còn, liên kết ICS ↔ ICR qua LinkedDocumentId vẫn giữ).
    /// </summary>
    public partial class V0_5_0_MuaBanNoiBo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CounterCompanyId",
                table: "InventoryDocument",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "InternalPrice",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    BuyerCompanyId = table.Column<Guid>(type: "uuid", nullable: true),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(19,6)", precision: 19, scale: 6, nullable: false),
                    VatCode = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: true),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InternalPrice", x => x.Id);
                    table.CheckConstraint("CK_InternalPrice_Buyer", "\"BuyerCompanyId\" IS NULL OR \"BuyerCompanyId\" <> \"CompanyId\"");
                    table.ForeignKey(
                        name: "FK_InternalPrice_Company_BuyerCompanyId",
                        column: x => x.BuyerCompanyId,
                        principalTable: "Company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InternalPrice_Company_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InternalPrice_Item_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Item",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryDocument_CounterCompanyId",
                table: "InventoryDocument",
                column: "CounterCompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_InternalPrice_BuyerCompanyId",
                table: "InternalPrice",
                column: "BuyerCompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_InternalPrice_CompanyId_ItemId_BuyerCompanyId_ValidFrom",
                table: "InternalPrice",
                columns: new[] { "CompanyId", "ItemId", "BuyerCompanyId", "ValidFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_InternalPrice_ItemId",
                table: "InternalPrice",
                column: "ItemId");

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryDocument_Company_CounterCompanyId",
                table: "InventoryDocument",
                column: "CounterCompanyId",
                principalTable: "Company",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InventoryDocument_Company_CounterCompanyId",
                table: "InventoryDocument");

            migrationBuilder.DropTable(
                name: "InternalPrice");

            migrationBuilder.DropIndex(
                name: "IX_InventoryDocument_CounterCompanyId",
                table: "InventoryDocument");

            migrationBuilder.DropColumn(
                name: "CounterCompanyId",
                table: "InventoryDocument");
        }
    }
}
