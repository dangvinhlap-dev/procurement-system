using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proc.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// NCC áp dụng cho công ty và NCC – sản phẩm (design.md mục 5.3, 5.9): bảng SupplierCompany, SupplierItem.
    /// Down() xóa hai bảng – MẤT danh sách NCC từng công ty đang dùng và mặt hàng từng NCC bán.
    /// </summary>
    public partial class V0_2_0_NccCongTySanPham : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SupplierCompany",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    LocalCode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierCompany", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierCompany_Company_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierCompany_Supplier_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Supplier",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SupplierItem",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierItemCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    PurchaseUomId = table.Column<Guid>(type: "uuid", nullable: false),
                    Moq = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    LeadTimeDays = table.Column<int>(type: "integer", nullable: true),
                    IsPreferred = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierItem", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierItem_Item_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Item",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierItem_Supplier_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Supplier",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierItem_Uom_PurchaseUomId",
                        column: x => x.PurchaseUomId,
                        principalTable: "Uom",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierCompany_CompanyId_LocalCode",
                table: "SupplierCompany",
                columns: new[] { "CompanyId", "LocalCode" },
                unique: true,
                filter: "\"LocalCode\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierCompany_SupplierId_CompanyId",
                table: "SupplierCompany",
                columns: new[] { "SupplierId", "CompanyId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierItem_ItemId",
                table: "SupplierItem",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierItem_ItemId_Preferred",
                table: "SupplierItem",
                column: "ItemId",
                unique: true,
                filter: "\"IsPreferred\"");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierItem_PurchaseUomId",
                table: "SupplierItem",
                column: "PurchaseUomId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierItem_SupplierId_ItemId",
                table: "SupplierItem",
                columns: new[] { "SupplierId", "ItemId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SupplierCompany");

            migrationBuilder.DropTable(
                name: "SupplierItem");
        }
    }
}
