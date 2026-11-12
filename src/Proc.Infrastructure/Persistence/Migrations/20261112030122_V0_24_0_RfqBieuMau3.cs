using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proc.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Lát Y6 (design.md mục 19.1 Q7, 19.3): RFQ thêm loại mua, công trình, biểu mẫu 3A/3a/3B, lý do chọn; tham số công ty ngưỡng so sánh 3 NCC.
    /// RFQ cũ là loại B (giá trị mặc định 2), chưa có biểu mẫu.
    /// </summary>
    public partial class V0_24_0_RfqBieuMau3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Category",
                table: "Rfq",
                type: "integer",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.AddColumn<Guid>(
                name: "ConstructionProjectId",
                table: "Rfq",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Form",
                table: "Rfq",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SelectionReason",
                table: "Rfq",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RfqComparisonThreshold",
                table: "CompanyParameter",
                type: "numeric(19,2)",
                precision: 19,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateIndex(
                name: "IX_Rfq_ConstructionProjectId_CompanyId",
                table: "Rfq",
                columns: new[] { "ConstructionProjectId", "CompanyId" });

            migrationBuilder.AddForeignKey(
                name: "FK_Rfq_ConstructionProject_ConstructionProjectId_CompanyId",
                table: "Rfq",
                columns: new[] { "ConstructionProjectId", "CompanyId" },
                principalTable: "ConstructionProject",
                principalColumns: new[] { "Id", "CompanyId" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Mất dữ liệu: loại mua, công trình, biểu mẫu, lý do chọn của RFQ và ngưỡng so sánh 3 NCC của công ty.
            migrationBuilder.DropForeignKey(
                name: "FK_Rfq_ConstructionProject_ConstructionProjectId_CompanyId",
                table: "Rfq");

            migrationBuilder.DropIndex(
                name: "IX_Rfq_ConstructionProjectId_CompanyId",
                table: "Rfq");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "Rfq");

            migrationBuilder.DropColumn(
                name: "ConstructionProjectId",
                table: "Rfq");

            migrationBuilder.DropColumn(
                name: "Form",
                table: "Rfq");

            migrationBuilder.DropColumn(
                name: "SelectionReason",
                table: "Rfq");

            migrationBuilder.DropColumn(
                name: "RfqComparisonThreshold",
                table: "CompanyParameter");
        }
    }
}
