using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proc.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Lát Y7 (design.md mục 19.3, Q10): PO thêm loại mua và công trình để in bảng theo dõi mua hàng 4A/4B. PO cũ mặc định loại B (2); PO có
    /// dòng lấy từ đề nghị loại A được điền lại loại A + công trình của đề nghị (PO cũ trộn nhiều công trình lấy một công trình bất kỳ).
    /// </summary>
    public partial class V0_24_0_PoBieuMau4 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Category",
                table: "PurchaseOrder",
                type: "integer",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.AddColumn<Guid>(
                name: "ConstructionProjectId",
                table: "PurchaseOrder",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrder_ConstructionProjectId_CompanyId",
                table: "PurchaseOrder",
                columns: new[] { "ConstructionProjectId", "CompanyId" });

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseOrder_ConstructionProject_ConstructionProjectId_Com~",
                table: "PurchaseOrder",
                columns: new[] { "ConstructionProjectId", "CompanyId" },
                principalTable: "ConstructionProject",
                principalColumns: new[] { "Id", "CompanyId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql("""
                UPDATE "PurchaseOrder" o
                SET "Category" = 1, "ConstructionProjectId" = x."ProjectId"
                FROM (
                    SELECT ol."OrderId", (MIN(r."ConstructionProjectId"::text))::uuid AS "ProjectId"
                    FROM "PurchaseOrderLine" ol
                    JOIN "PurchaseRequestLine" l ON l."Id" = ol."SourcePrLineId"
                    JOIN "PurchaseRequest" r ON r."Id" = l."RequestId"
                    WHERE r."Category" = 1 AND r."ConstructionProjectId" IS NOT NULL
                    GROUP BY ol."OrderId"
                ) x
                WHERE o."Id" = x."OrderId";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Mất dữ liệu: loại mua và công trình của PO (tính lại được từ dòng đề nghị nguồn).
            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseOrder_ConstructionProject_ConstructionProjectId_Com~",
                table: "PurchaseOrder");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrder_ConstructionProjectId_CompanyId",
                table: "PurchaseOrder");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "PurchaseOrder");

            migrationBuilder.DropColumn(
                name: "ConstructionProjectId",
                table: "PurchaseOrder");
        }
    }
}
