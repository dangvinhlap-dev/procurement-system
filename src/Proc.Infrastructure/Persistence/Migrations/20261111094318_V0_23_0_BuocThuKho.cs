using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proc.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Lát Y3c (design.md mục 19.3): bước duyệt "Thủ kho nơi nhận" ghi nơi nhận của đề nghị mua (<c>ApprovalStep.WarehouseId</c>).
    /// </summary>
    public partial class V0_23_0_BuocThuKho : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "WarehouseId",
                table: "ApprovalStep",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalStep_WarehouseId",
                table: "ApprovalStep",
                column: "WarehouseId");

            migrationBuilder.AddForeignKey(
                name: "FK_ApprovalStep_Warehouse_WarehouseId",
                table: "ApprovalStep",
                column: "WarehouseId",
                principalTable: "Warehouse",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <summary>
        /// Gỡ cột nơi nhận của bước duyệt. <b>Mất dữ liệu</b>: bước Thủ kho đã sinh mất nơi nhận – bản cũ không hiểu loại người duyệt này, phiếu
        /// đang chờ ở bước Thủ kho phải rút về nháp rồi gửi lại sau khi quay về.
        /// </summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ApprovalStep_Warehouse_WarehouseId",
                table: "ApprovalStep");

            migrationBuilder.DropIndex(
                name: "IX_ApprovalStep_WarehouseId",
                table: "ApprovalStep");

            migrationBuilder.DropColumn(
                name: "WarehouseId",
                table: "ApprovalStep");
        }
    }
}
