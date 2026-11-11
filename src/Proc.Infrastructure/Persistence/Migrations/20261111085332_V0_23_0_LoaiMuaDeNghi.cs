using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proc.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Lát Y3a (0.23.0, design.md mục 19.3): đề nghị mua có loại mua A/B/DV (phiếu cũ = 2 – B hàng hóa) và công trình (chỉ loại A, khóa ngoại
    /// kèm CompanyId để không gắn công trình công ty khác). Down mất loại mua và công trình đã chọn trên các đề nghị.
    /// </summary>
    public partial class V0_23_0_LoaiMuaDeNghi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Category",
                table: "PurchaseRequest",
                type: "integer",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.AddColumn<Guid>(
                name: "ConstructionProjectId",
                table: "PurchaseRequest",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_ConstructionProject_Id_CompanyId",
                table: "ConstructionProject",
                columns: new[] { "Id", "CompanyId" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequest_ConstructionProjectId_CompanyId",
                table: "PurchaseRequest",
                columns: new[] { "ConstructionProjectId", "CompanyId" });

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseRequest_ConstructionProject_ConstructionProjectId_C~",
                table: "PurchaseRequest",
                columns: new[] { "ConstructionProjectId", "CompanyId" },
                principalTable: "ConstructionProject",
                principalColumns: new[] { "Id", "CompanyId" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseRequest_ConstructionProject_ConstructionProjectId_C~",
                table: "PurchaseRequest");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseRequest_ConstructionProjectId_CompanyId",
                table: "PurchaseRequest");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_ConstructionProject_Id_CompanyId",
                table: "ConstructionProject");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "PurchaseRequest");

            migrationBuilder.DropColumn(
                name: "ConstructionProjectId",
                table: "PurchaseRequest");
        }
    }
}
