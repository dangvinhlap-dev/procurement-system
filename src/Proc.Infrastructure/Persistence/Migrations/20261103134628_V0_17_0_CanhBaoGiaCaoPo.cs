using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proc.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Tham số công ty "% cảnh báo giá cao trên PO" (design.md mục 8.5), mặc định 10% cho mọi công ty đã có dòng tham số.
    /// </summary>
    public partial class V0_17_0_CanhBaoGiaCaoPo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "PoHighPriceWarnPercent",
                table: "CompanyParameter",
                type: "numeric(9,4)",
                precision: 9,
                scale: 4,
                nullable: false,
                defaultValue: 10m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Mất giá trị % cảnh báo giá cao đã sửa ở SEC (chỉ là tham số, không mất chứng từ).
            migrationBuilder.DropColumn(
                name: "PoHighPriceWarnPercent",
                table: "CompanyParameter");
        }
    }
}
