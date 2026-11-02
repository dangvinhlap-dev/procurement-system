using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proc.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Lát F2 (design.md mục 18.18): mục tiêu 9 KPI Ban Cung ứng và số ngày tính tồn chậm trên tham số công ty; dòng cũ nhận mục tiêu
    /// mặc định. Down() xóa các cột – mất mục tiêu KPI đã khai báo.
    /// </summary>
    public partial class V0_13_0_MucTieuKpi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "KpiContractTarget",
                table: "CompanyParameter",
                type: "numeric(9,4)",
                precision: 9,
                scale: 4,
                nullable: false,
                defaultValue: 95m);

            migrationBuilder.AddColumn<decimal>(
                name: "KpiEmergencyTarget",
                table: "CompanyParameter",
                type: "numeric(9,4)",
                precision: 9,
                scale: 4,
                nullable: false,
                defaultValue: 5m);

            migrationBuilder.AddColumn<decimal>(
                name: "KpiOnTimeTarget",
                table: "CompanyParameter",
                type: "numeric(9,4)",
                precision: 9,
                scale: 4,
                nullable: false,
                defaultValue: 95m);

            migrationBuilder.AddColumn<decimal>(
                name: "KpiQualityTarget",
                table: "CompanyParameter",
                type: "numeric(9,4)",
                precision: 9,
                scale: 4,
                nullable: false,
                defaultValue: 98m);

            migrationBuilder.AddColumn<decimal>(
                name: "KpiSavingTarget",
                table: "CompanyParameter",
                type: "numeric(9,4)",
                precision: 9,
                scale: 4,
                nullable: false,
                defaultValue: 3m);

            migrationBuilder.AddColumn<decimal>(
                name: "KpiSlaTarget",
                table: "CompanyParameter",
                type: "numeric(9,4)",
                precision: 9,
                scale: 4,
                nullable: false,
                defaultValue: 95m);

            migrationBuilder.AddColumn<int>(
                name: "KpiSlowMovingDays",
                table: "CompanyParameter",
                type: "integer",
                nullable: false,
                defaultValue: 90);

            migrationBuilder.AddColumn<decimal>(
                name: "KpiSlowMovingTarget",
                table: "CompanyParameter",
                type: "numeric(9,4)",
                precision: 9,
                scale: 4,
                nullable: false,
                defaultValue: 10m);

            migrationBuilder.AddColumn<decimal>(
                name: "KpiSupplierReviewTarget",
                table: "CompanyParameter",
                type: "numeric(9,4)",
                precision: 9,
                scale: 4,
                nullable: false,
                defaultValue: 100m);

            migrationBuilder.AddColumn<int>(
                name: "KpiViolationTarget",
                table: "CompanyParameter",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "KpiContractTarget",
                table: "CompanyParameter");

            migrationBuilder.DropColumn(
                name: "KpiEmergencyTarget",
                table: "CompanyParameter");

            migrationBuilder.DropColumn(
                name: "KpiOnTimeTarget",
                table: "CompanyParameter");

            migrationBuilder.DropColumn(
                name: "KpiQualityTarget",
                table: "CompanyParameter");

            migrationBuilder.DropColumn(
                name: "KpiSavingTarget",
                table: "CompanyParameter");

            migrationBuilder.DropColumn(
                name: "KpiSlaTarget",
                table: "CompanyParameter");

            migrationBuilder.DropColumn(
                name: "KpiSlowMovingDays",
                table: "CompanyParameter");

            migrationBuilder.DropColumn(
                name: "KpiSlowMovingTarget",
                table: "CompanyParameter");

            migrationBuilder.DropColumn(
                name: "KpiSupplierReviewTarget",
                table: "CompanyParameter");

            migrationBuilder.DropColumn(
                name: "KpiViolationTarget",
                table: "CompanyParameter");
        }
    }
}
