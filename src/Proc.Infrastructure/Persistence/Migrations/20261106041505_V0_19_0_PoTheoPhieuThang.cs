using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proc.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Lát G3 (design.md mục 18.25): dòng PO ghi phiếu duyệt NCC và giá tháng phủ mặt hàng; cờ PO "ngoài danh sách/giá đã duyệt" (chặng POL).
    /// PO cũ nhận cờ false – tính lại khi sửa phiếu nháp. Down() xóa hai cột: mất liên kết phiếu tháng và cờ đã ghi trên PO (AuditLog vẫn giữ).
    /// </summary>
    public partial class V0_19_0_PoTheoPhieuThang : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "MonthlyApprovalLineId",
                table: "PurchaseOrderLine",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsOffApprovedList",
                table: "PurchaseOrder",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderLine_MonthlyApprovalLineId",
                table: "PurchaseOrderLine",
                column: "MonthlyApprovalLineId");

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseOrderLine_MonthlySupplierApprovalLine_MonthlyApprov~",
                table: "PurchaseOrderLine",
                column: "MonthlyApprovalLineId",
                principalTable: "MonthlySupplierApprovalLine",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseOrderLine_MonthlySupplierApprovalLine_MonthlyApprov~",
                table: "PurchaseOrderLine");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrderLine_MonthlyApprovalLineId",
                table: "PurchaseOrderLine");

            migrationBuilder.DropColumn(
                name: "MonthlyApprovalLineId",
                table: "PurchaseOrderLine");

            migrationBuilder.DropColumn(
                name: "IsOffApprovedList",
                table: "PurchaseOrder");
        }
    }
}
