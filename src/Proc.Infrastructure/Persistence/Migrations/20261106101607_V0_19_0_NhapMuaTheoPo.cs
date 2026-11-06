using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proc.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Lát R4 (design.md mục 18.26): tham số công ty "Nhập mua bắt buộc theo đơn mua hàng" (cột RequireReceiptPo, mặc định tắt).
    /// Down() xóa cột – mất giá trị tham số đã bật (bật lại ở màn hình SEC sau khi nâng cấp lại).
    /// </summary>
    public partial class V0_19_0_NhapMuaTheoPo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "RequireReceiptPo",
                table: "CompanyParameter",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RequireReceiptPo",
                table: "CompanyParameter");
        }
    }
}
