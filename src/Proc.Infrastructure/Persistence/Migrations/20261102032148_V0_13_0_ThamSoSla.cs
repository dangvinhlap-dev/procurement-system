using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proc.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Lát C4 (design.md mục 18.13): tham số SLA mua hàng theo công ty – hàng đã có hợp đồng/bảng giá (1), chưa có NCC được duyệt (5), hàng
    /// khẩn (1) ngày làm việc. Công ty đã có dòng tham số nhận giá trị mặc định.
    /// </summary>
    public partial class V0_13_0_ThamSoSla : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SlaPricedDays",
                table: "CompanyParameter",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "SlaSourcingDays",
                table: "CompanyParameter",
                type: "integer",
                nullable: false,
                defaultValue: 5);

            migrationBuilder.AddColumn<int>(
                name: "SlaUrgentDays",
                table: "CompanyParameter",
                type: "integer",
                nullable: false,
                defaultValue: 1);
        }

        /// <summary>Gỡ 3 cột SLA – mất giá trị SLA công ty đã sửa (AuditLog vẫn giữ).</summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SlaPricedDays",
                table: "CompanyParameter");

            migrationBuilder.DropColumn(
                name: "SlaSourcingDays",
                table: "CompanyParameter");

            migrationBuilder.DropColumn(
                name: "SlaUrgentDays",
                table: "CompanyParameter");
        }
    }
}
