using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proc.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Mã hàng theo NCC trên dòng PO (design.md mục 5.9): chép từ NCC – sản phẩm lúc lưu/gửi duyệt để phiếu in gửi NCC ghi mã NCC hiểu.
    /// PO cũ để trống. Down() xóa cột – mất mã đã chép (vẫn tra lại được ở NCC – sản phẩm).
    /// </summary>
    public partial class V0_20_0_MaHangNccTrenPo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SupplierItemCode",
                table: "PurchaseOrderLine",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SupplierItemCode",
                table: "PurchaseOrderLine");
        }
    }
}
