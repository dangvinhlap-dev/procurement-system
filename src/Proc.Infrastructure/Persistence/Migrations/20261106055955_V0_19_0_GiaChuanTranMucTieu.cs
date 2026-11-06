using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proc.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Lát G4b (design.md mục 18.25): giá chuẩn, giá trần, giá mục tiêu (không bắt buộc) trên dòng phiếu duyệt NCC và giá tháng.
    /// Down() xóa ba cột – mất các giá này trên phiếu đã lập (AuditLog vẫn giữ).
    /// </summary>
    public partial class V0_19_0_GiaChuanTranMucTieu : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CeilingPrice",
                table: "MonthlySupplierApprovalLine",
                type: "numeric(19,6)",
                precision: 19,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "StandardPrice",
                table: "MonthlySupplierApprovalLine",
                type: "numeric(19,6)",
                precision: 19,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TargetPrice",
                table: "MonthlySupplierApprovalLine",
                type: "numeric(19,6)",
                precision: 19,
                scale: 6,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CeilingPrice",
                table: "MonthlySupplierApprovalLine");

            migrationBuilder.DropColumn(
                name: "StandardPrice",
                table: "MonthlySupplierApprovalLine");

            migrationBuilder.DropColumn(
                name: "TargetPrice",
                table: "MonthlySupplierApprovalLine");
        }
    }
}
