using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proc.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Bản 0.15.0: phiếu điều chỉnh / kiểm kê kho (ADJ – design.md mục 6.1): lý do, số biên bản kiểm kê trên phiếu; chiều tăng/giảm trên dòng.
    /// </summary>
    public partial class V0_15_0_PhieuDieuChinhKho : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Direction",
                table: "InventoryDocumentLine",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CountReportNo",
                table: "InventoryDocument",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Reason",
                table: "InventoryDocument",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_InventoryDocumentLine_Direction",
                table: "InventoryDocumentLine",
                sql: "\"Direction\" IS NULL OR \"Direction\" IN (-1, 1)");
        }

        /// <summary>
        /// Gỡ cột của phiếu điều chỉnh. MẤT DỮ LIỆU nếu đã có phiếu ADJ: mất lý do, số biên bản và chiều tăng/giảm của dòng (sổ kho vẫn
        /// giữ chiều nên tồn không đổi, nhưng phiếu ADJ không còn mở/sửa đúng được) – chỉ chạy khi đã sao lưu (CLAUDE.md mục 8).
        /// </summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_InventoryDocumentLine_Direction",
                table: "InventoryDocumentLine");

            migrationBuilder.DropColumn(
                name: "Direction",
                table: "InventoryDocumentLine");

            migrationBuilder.DropColumn(
                name: "CountReportNo",
                table: "InventoryDocument");

            migrationBuilder.DropColumn(
                name: "Reason",
                table: "InventoryDocument");
        }
    }
}
