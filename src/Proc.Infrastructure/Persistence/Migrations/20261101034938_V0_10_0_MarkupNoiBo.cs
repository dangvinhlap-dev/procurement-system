using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proc.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Bản 0.10.0 – % markup nội bộ mặc định theo công ty (design.md mục 1 câu hỏi 2, mục 5.4). Down() xóa cột – mất % markup đã
    /// khai báo, gợi ý quay về giá vốn (0%); bảng giá nội bộ và chứng từ không đổi.
    /// </summary>
    public partial class V0_10_0_MarkupNoiBo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "InternalMarkupPercent",
                table: "CompanyParameter",
                type: "numeric(9,4)",
                precision: 9,
                scale: 4,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InternalMarkupPercent",
                table: "CompanyParameter");
        }
    }
}
