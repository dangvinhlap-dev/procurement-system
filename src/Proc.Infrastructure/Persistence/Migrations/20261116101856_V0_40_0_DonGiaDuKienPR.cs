using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proc.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Bản 0.40.0: thêm cờ <c>EstPriceManual</c> cho dòng đề nghị mua – đơn giá do người lập nhập vì mặt hàng chưa có giá nhận hàng gần nhất
    /// (design.md mục 10.2, hướng B người dùng chốt 16/11/2026). Dòng cũ mặc định false. Down() bỏ cột: chỉ mất cờ, đơn giá vẫn giữ.
    /// </summary>
    public partial class V0_40_0_DonGiaDuKienPR : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EstPriceManual",
                table: "PurchaseRequestLine",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EstPriceManual",
                table: "PurchaseRequestLine");
        }
    }
}
