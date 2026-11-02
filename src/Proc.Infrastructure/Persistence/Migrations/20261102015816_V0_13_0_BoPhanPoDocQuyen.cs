using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proc.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Lát C1b (design.md mục 18.10): bộ phận đặt hàng trên PO (xét bộ phận được phép đặt của hợp đồng) và phiếu NCC độc quyền ghi nhận trên
    /// dòng PO. PO cũ không có bộ phận, dòng cũ không có phiếu độc quyền.
    /// </summary>
    public partial class V0_13_0_BoPhanPoDocQuyen : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SoleSourceApprovalId",
                table: "PurchaseOrderLine",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DepartmentId",
                table: "PurchaseOrder",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderLine_SoleSourceApprovalId",
                table: "PurchaseOrderLine",
                column: "SoleSourceApprovalId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrder_DepartmentId",
                table: "PurchaseOrder",
                column: "DepartmentId");

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseOrder_Department_DepartmentId",
                table: "PurchaseOrder",
                column: "DepartmentId",
                principalTable: "Department",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseOrderLine_SoleSourceApproval_SoleSourceApprovalId",
                table: "PurchaseOrderLine",
                column: "SoleSourceApprovalId",
                principalTable: "SoleSourceApproval",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <summary>Gỡ hai cột – MẤT DỮ LIỆU: bộ phận đặt hàng của PO và phiếu độc quyền đã ghi trên dòng PO (AuditLog vẫn giữ).</summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseOrder_Department_DepartmentId",
                table: "PurchaseOrder");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseOrderLine_SoleSourceApproval_SoleSourceApprovalId",
                table: "PurchaseOrderLine");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrderLine_SoleSourceApprovalId",
                table: "PurchaseOrderLine");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrder_DepartmentId",
                table: "PurchaseOrder");

            migrationBuilder.DropColumn(
                name: "SoleSourceApprovalId",
                table: "PurchaseOrderLine");

            migrationBuilder.DropColumn(
                name: "DepartmentId",
                table: "PurchaseOrder");
        }
    }
}
