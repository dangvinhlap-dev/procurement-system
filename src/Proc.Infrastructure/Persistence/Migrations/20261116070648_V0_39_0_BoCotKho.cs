using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proc.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Bản 0.39.0: xóa cột kho tổng / kho phụ (Type, kho cha, tồn âm) của Warehouse và 7 cột thông số tồn kho của Item – ngừng dùng từ 0.38.0
    /// (design.md mục 19.4; CLAUDE.md mục 8: đánh dấu ngừng dùng trước, xóa ở bản sau). <b>Down() mất dữ liệu</b>: tạo lại cột với giá trị mặc
    /// định – mọi kho thành kho phụ (Type = 2), mặt hàng không còn tồn tối thiểu / tối đa / an toàn / điểm đặt hàng lại cũ.
    /// </summary>
    public partial class V0_39_0_BoCotKho : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Warehouse_Warehouse_ParentWarehouseId_CompanyId",
                table: "Warehouse");

            migrationBuilder.DropIndex(
                name: "IX_Warehouse_CompanyId_Main",
                table: "Warehouse");

            migrationBuilder.DropIndex(
                name: "IX_Warehouse_ParentWarehouseId_CompanyId",
                table: "Warehouse");

            migrationBuilder.DropColumn(
                name: "AllowNegativeStock",
                table: "Warehouse");

            migrationBuilder.DropColumn(
                name: "ParentWarehouseId",
                table: "Warehouse");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "Warehouse");

            migrationBuilder.DropColumn(
                name: "IsCritical",
                table: "Item");

            migrationBuilder.DropColumn(
                name: "MaxStock",
                table: "Item");

            migrationBuilder.DropColumn(
                name: "MinStock",
                table: "Item");

            migrationBuilder.DropColumn(
                name: "ReorderPoint",
                table: "Item");

            migrationBuilder.DropColumn(
                name: "SafetyStock",
                table: "Item");

            migrationBuilder.DropColumn(
                name: "TrackExpiry",
                table: "Item");

            migrationBuilder.DropColumn(
                name: "TrackLot",
                table: "Item");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AllowNegativeStock",
                table: "Warehouse",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "ParentWarehouseId",
                table: "Warehouse",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Type",
                table: "Warehouse",
                type: "integer",
                nullable: false,
                // 2 = kho phụ (WarehouseType.Sub của 0.38.0): bản 0.38.0 ghi Sub cho mọi kho, không vướng chỉ mục "một kho Main" tạo lại dưới đây.
                defaultValue: 2);

            migrationBuilder.AddColumn<bool>(
                name: "IsCritical",
                table: "Item",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "MaxStock",
                table: "Item",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MinStock",
                table: "Item",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ReorderPoint",
                table: "Item",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SafetyStock",
                table: "Item",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "TrackExpiry",
                table: "Item",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "TrackLot",
                table: "Item",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Warehouse_CompanyId_Main",
                table: "Warehouse",
                column: "CompanyId",
                unique: true,
                filter: "\"Type\" = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Warehouse_ParentWarehouseId_CompanyId",
                table: "Warehouse",
                columns: new[] { "ParentWarehouseId", "CompanyId" });

            migrationBuilder.AddForeignKey(
                name: "FK_Warehouse_Warehouse_ParentWarehouseId_CompanyId",
                table: "Warehouse",
                columns: new[] { "ParentWarehouseId", "CompanyId" },
                principalTable: "Warehouse",
                principalColumns: new[] { "Id", "CompanyId" },
                onDelete: ReferentialAction.Restrict);
        }
    }
}
