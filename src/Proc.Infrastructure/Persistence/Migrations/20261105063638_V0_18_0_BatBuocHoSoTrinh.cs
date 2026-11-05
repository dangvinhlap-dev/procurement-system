using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proc.Infrastructure.Persistence.Migrations
{
    /// <summary>Tham số công ty "Bắt buộc đủ hồ sơ trình khi chọn thầu" (design.md mục 8.6), mặc định tắt.</summary>
    public partial class V0_18_0_BatBuocHoSoTrinh : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "RequireRfqDossierComplete",
                table: "CompanyParameter",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <summary>Xóa cột – mất lựa chọn bật/tắt của các công ty (về hành vi không chặn).</summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RequireRfqDossierComplete",
                table: "CompanyParameter");
        }
    }
}
