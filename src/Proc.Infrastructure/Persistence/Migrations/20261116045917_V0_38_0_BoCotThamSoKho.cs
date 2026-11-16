using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proc.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Xóa 4 cột tham số công ty của phân hệ kho đã gỡ (lát K3, 0.31.0 – design.md 19.4): % markup nội bộ, nhập mua bắt buộc theo PO, mục tiêu
    /// và số ngày KPI tồn chậm luân chuyển. Ngừng dùng từ 0.31.0, xóa ở bản sau theo CLAUDE.md mục 8 (không xóa cột cùng bản ngừng dùng).
    /// </summary>
    public partial class V0_38_0_BoCotThamSoKho : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InternalMarkupPercent",
                table: "CompanyParameter");

            migrationBuilder.DropColumn(
                name: "KpiSlowMovingDays",
                table: "CompanyParameter");

            migrationBuilder.DropColumn(
                name: "KpiSlowMovingTarget",
                table: "CompanyParameter");

            migrationBuilder.DropColumn(
                name: "RequireReceiptPo",
                table: "CompanyParameter");
        }

        /// <summary>
        /// Tạo lại 4 cột với giá trị mặc định (0%, tắt, 10%, 90 ngày) – <b>mất giá trị cũ</b> từng công ty đã khai; không chức năng nào còn đọc
        /// các cột này nên bản cũ vẫn chạy đúng.
        /// </summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "InternalMarkupPercent",
                table: "CompanyParameter",
                type: "numeric(9,4)",
                precision: 9,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

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

            migrationBuilder.AddColumn<bool>(
                name: "RequireReceiptPo",
                table: "CompanyParameter",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
