using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proc.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Lát Y3b (design.md mục 19.3): thủ kho nơi nhận nhập tồn trên đề nghị mua – dòng PR thêm SL cần sử dụng (RequiredQty) và tồn (StockQty);
    /// SL cũ (Qty) nay là SL cần mua. Đầu PR ghi người, thời điểm xác nhận tồn.
    /// Down: mất tồn thủ kho đã nhập và SL cần sử dụng; Qty giữ SL cần mua (không khôi phục SL trước khi trừ tồn vì có thể đã lên PO).
    /// </summary>
    public partial class V0_23_0_ThuKhoNhapTon : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "RequiredQty",
                table: "PurchaseRequestLine",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            // Dòng có trước 0.23.0 chưa có tồn thủ kho: SL cần sử dụng = SL đề nghị (cần mua).
            migrationBuilder.Sql("UPDATE \"PurchaseRequestLine\" SET \"RequiredQty\" = \"Qty\";");

            migrationBuilder.AddColumn<decimal>(
                name: "StockQty",
                table: "PurchaseRequestLine",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "StockConfirmedAt",
                table: "PurchaseRequest",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "StockConfirmedBy",
                table: "PurchaseRequest",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RequiredQty",
                table: "PurchaseRequestLine");

            migrationBuilder.DropColumn(
                name: "StockQty",
                table: "PurchaseRequestLine");

            migrationBuilder.DropColumn(
                name: "StockConfirmedAt",
                table: "PurchaseRequest");

            migrationBuilder.DropColumn(
                name: "StockConfirmedBy",
                table: "PurchaseRequest");
        }
    }
}
