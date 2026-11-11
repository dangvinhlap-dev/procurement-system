using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proc.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Bản 0.23.0 (lát Z4c, design.md mục 20.2 E2): bảng giá có trạng thái Đã hủy (giá trị 5 – cột Status không đổi kiểu) và lý do hủy, để hủy
    /// bảng giá nháp theo dây chuyền RFQ. Down() xóa cột lý do: mất lý do hủy đã ghi (bảng giá đã hủy vẫn giữ Status = 5).
    /// </summary>
    public partial class V0_23_0_HuyBangGia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CancelReason",
                table: "SupplierPriceList",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CancelReason",
                table: "SupplierPriceList");
        }
    }
}
