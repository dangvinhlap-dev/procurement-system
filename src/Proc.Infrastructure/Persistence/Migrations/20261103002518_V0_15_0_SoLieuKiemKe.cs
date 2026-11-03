using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proc.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Số liệu kiểm kê trên dòng phiếu điều chỉnh (ADJ-3 – design.md mục 18.23): tồn sổ lúc lưu và SL thực đếm, ĐVT cơ sở.
    /// </summary>
    public partial class V0_15_0_SoLieuKiemKe : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "BookQty",
                table: "InventoryDocumentLine",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CountedQty",
                table: "InventoryDocumentLine",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);
        }

        /// <summary>Gỡ hai cột – MẤT số tồn sổ, thực đếm đã ghi trên dòng phiếu kiểm kê (SL điều chỉnh vẫn còn).</summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BookQty",
                table: "InventoryDocumentLine");

            migrationBuilder.DropColumn(
                name: "CountedQty",
                table: "InventoryDocumentLine");
        }
    }
}
