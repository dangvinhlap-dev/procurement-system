using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proc.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Lát C3 (design.md mục 18.24): bảng ngân sách bộ phận × tháng, cột kiểm tra ngân sách trên PR/PO.
    /// </summary>
    public partial class V0_18_0_NganSachBoPhan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "BudgetAmount",
                table: "PurchaseRequest",
                type: "numeric(19,2)",
                precision: 19,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BudgetUsedAmount",
                table: "PurchaseRequest",
                type: "numeric(19,2)",
                precision: 19,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsOverBudget",
                table: "PurchaseRequest",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "BudgetAmount",
                table: "PurchaseOrder",
                type: "numeric(19,2)",
                precision: 19,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BudgetUsedAmount",
                table: "PurchaseOrder",
                type: "numeric(19,2)",
                precision: 19,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsOverBudget",
                table: "PurchaseOrder",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "DepartmentBudget",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Month = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(19,2)", precision: 19, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DepartmentBudget", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DepartmentBudget_Department_DepartmentId_CompanyId",
                        columns: x => new { x.DepartmentId, x.CompanyId },
                        principalTable: "Department",
                        principalColumns: new[] { "Id", "CompanyId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DepartmentBudget_CompanyId_DepartmentId_Year_Month",
                table: "DepartmentBudget",
                columns: new[] { "CompanyId", "DepartmentId", "Year", "Month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DepartmentBudget_DepartmentId_CompanyId",
                table: "DepartmentBudget",
                columns: new[] { "DepartmentId", "CompanyId" });
        }

        /// <summary>Gỡ lát C3 – <b>mất dữ liệu</b>: toàn bộ ngân sách bộ phận và số liệu kiểm tra ngân sách trên PR/PO (AuditLog vẫn giữ).</summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DepartmentBudget");

            migrationBuilder.DropColumn(
                name: "BudgetAmount",
                table: "PurchaseRequest");

            migrationBuilder.DropColumn(
                name: "BudgetUsedAmount",
                table: "PurchaseRequest");

            migrationBuilder.DropColumn(
                name: "IsOverBudget",
                table: "PurchaseRequest");

            migrationBuilder.DropColumn(
                name: "BudgetAmount",
                table: "PurchaseOrder");

            migrationBuilder.DropColumn(
                name: "BudgetUsedAmount",
                table: "PurchaseOrder");

            migrationBuilder.DropColumn(
                name: "IsOverBudget",
                table: "PurchaseOrder");
        }
    }
}
