using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proc.Infrastructure.Persistence.Migrations
{
    /// <summary>Lát C6 (design.md mục 18.15): thêm tiền hóa đơn NCC (gồm VAT) vào phiếu nhập mua để đối chiếu PO – nhận – hóa đơn.</summary>
    public partial class V0_13_0_TienHoaDonPhieuNhap : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "InvoiceAmount",
                table: "InventoryDocument",
                type: "numeric(19,2)",
                precision: 19,
                scale: 2,
                nullable: true);
        }

        /// <summary>Gỡ cột – MẤT tiền hóa đơn đã nhập trên phiếu nhập mua (số, ký hiệu, ngày hóa đơn giữ nguyên).</summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InvoiceAmount",
                table: "InventoryDocument");
        }
    }
}
