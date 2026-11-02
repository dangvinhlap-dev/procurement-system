using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proc.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Lát C5 (design.md mục 18.14): kiểm tra chất lượng, sai lệch trên dòng phiếu kho (SL chứng từ giao, SL không đạt, kết quả, ghi chú) và hai
    /// tham số công ty (bắt buộc kiểm tra khi nhận hàng; người lập PO không ghi sổ nhận hàng của PO đó) – mặc định tắt.
    /// </summary>
    public partial class V0_13_0_KiemTraNhanHang : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DeliveredQty",
                table: "InventoryDocumentLine",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QcNote",
                table: "InventoryDocumentLine",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "QcResult",
                table: "InventoryDocumentLine",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "RejectedQty",
                table: "InventoryDocumentLine",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "RequireReceiptQc",
                table: "CompanyParameter",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SeparatePoCreatorFromReceipt",
                table: "CompanyParameter",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <summary>Gỡ các cột – MẤT DỮ LIỆU: kết quả kiểm tra, SL không đạt, SL chứng từ giao, ghi chú kiểm tra và hai tham số (AuditLog vẫn giữ).</summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeliveredQty",
                table: "InventoryDocumentLine");

            migrationBuilder.DropColumn(
                name: "QcNote",
                table: "InventoryDocumentLine");

            migrationBuilder.DropColumn(
                name: "QcResult",
                table: "InventoryDocumentLine");

            migrationBuilder.DropColumn(
                name: "RejectedQty",
                table: "InventoryDocumentLine");

            migrationBuilder.DropColumn(
                name: "RequireReceiptQc",
                table: "CompanyParameter");

            migrationBuilder.DropColumn(
                name: "SeparatePoCreatorFromReceipt",
                table: "CompanyParameter");
        }
    }
}
