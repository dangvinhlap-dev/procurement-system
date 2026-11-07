using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proc.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Ngành hàng NCC chọn từ danh mục Nhóm hàng (design.md mục 5.3): bảng SupplierCategory (NCC × nhóm hàng). Cột chữ Supplier.SupplyScope
    /// giữ nguyên dữ liệu, chỉ ngừng dùng (xóa ở bản sau – CLAUDE.md mục 8). Down() xóa bảng – mất ngành hàng đã chọn (nhật ký vẫn giữ).
    /// </summary>
    public partial class V0_20_0_NganhHangNcc : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SupplierCategory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemCategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierCategory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierCategory_ItemCategory_ItemCategoryId",
                        column: x => x.ItemCategoryId,
                        principalTable: "ItemCategory",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierCategory_Supplier_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Supplier",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierCategory_ItemCategoryId",
                table: "SupplierCategory",
                column: "ItemCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierCategory_SupplierId_ItemCategoryId",
                table: "SupplierCategory",
                columns: new[] { "SupplierId", "ItemCategoryId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SupplierCategory");
        }
    }
}
