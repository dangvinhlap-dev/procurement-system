using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proc.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Lát P4b (design.md 19.5, P4-8): người và giờ Phòng CUVT gửi kết quả chọn thầu cho NCC. RFQ đã chọn thầu trước bản này chưa gửi kết quả –
    /// NCC thấy "Đang xét chọn" đến khi Phòng CUVT bấm gửi.
    /// </summary>
    public partial class V0_26_0_GuiKetQuaChonThau : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ResultSentAt",
                table: "Rfq",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ResultSentBy",
                table: "Rfq",
                type: "uuid",
                nullable: true);
        }

        /// <summary>Xóa hai cột – mất thông tin người, giờ gửi kết quả (thư đã gửi vẫn còn trong SupplierPortalEmail).</summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ResultSentAt",
                table: "Rfq");

            migrationBuilder.DropColumn(
                name: "ResultSentBy",
                table: "Rfq");
        }
    }
}
