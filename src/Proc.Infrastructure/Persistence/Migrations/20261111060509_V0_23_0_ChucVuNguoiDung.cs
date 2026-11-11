using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proc.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Bản 0.23.0 (lát Z2, design.md mục 20.2 D5): thêm cột Chức vụ cho người dùng – hiện ở "Kiểm tra phê duyệt" của chứng từ.
    /// Down() xóa cột: mất chức vụ đã nhập.
    /// </summary>
    public partial class V0_23_0_ChucVuNguoiDung : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Position",
                table: "User",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Position",
                table: "User");
        }
    }
}
