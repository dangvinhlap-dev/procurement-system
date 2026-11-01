using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proc.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Lát B1 – duyệt bảng giá NCC (design.md mục 8.5): cột lượt gửi duyệt và lý do từ chối; trạng thái mới Chờ duyệt = 4 lưu ở cột Status
    /// sẵn có. Bảng giá đã hiệu lực trước bản này giữ nguyên (không bắt duyệt lại). Down() đưa bảng đang chờ duyệt về Nháp và mất lượt
    /// gửi, lý do từ chối (lịch sử ApprovalStep vẫn giữ).
    /// </summary>
    public partial class V0_11_0_DuyetBangGiaNcc : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ApprovalRound",
                table: "SupplierPriceList",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "RejectReason",
                table: "SupplierPriceList",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Bản cũ không biết trạng thái 4 (Chờ duyệt) – đưa về Nháp (1) để sửa/đưa vào hiệu lực như trước.
            migrationBuilder.Sql("UPDATE \"SupplierPriceList\" SET \"Status\" = 1 WHERE \"Status\" = 4;");

            migrationBuilder.DropColumn(
                name: "ApprovalRound",
                table: "SupplierPriceList");

            migrationBuilder.DropColumn(
                name: "RejectReason",
                table: "SupplierPriceList");
        }
    }
}
