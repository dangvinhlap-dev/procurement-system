using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proc.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Lát H2 (design.md mục 18.25 – Q6): phiếu duyệt NCC và giá tháng có giải trình ngoại lệ và cờ "lượt gửi gần nhất có ngoại lệ" (qua chặng
    /// Chủ tịch/TGĐ – loại duyệt GTX). Thêm cột cho phép null / mặc định false, không đổi dữ liệu cũ.
    /// </summary>
    public partial class V0_20_0_GthNgoaiLe : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExceptionNote",
                table: "MonthlySupplierApproval",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasException",
                table: "MonthlySupplierApproval",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        /// <remarks>Mất giải trình ngoại lệ và cờ ngoại lệ của các phiếu (AuditLog vẫn giữ); các bước chặng 2 đã sinh vẫn nằm trong lịch sử duyệt.</remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExceptionNote",
                table: "MonthlySupplierApproval");

            migrationBuilder.DropColumn(
                name: "HasException",
                table: "MonthlySupplierApproval");
        }
    }
}
