using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proc.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Danh mục công ty và kho (design.md mục 5.6): mã số thuế công ty không trùng. Down() chỉ bỏ chỉ mục, không mất dữ liệu.
    /// Nếu CSDL đã có hai công ty trùng mã số thuế thì Up() lỗi – sửa dữ liệu trước khi nâng cấp.
    /// </summary>
    public partial class V0_2_0_CongTyKho : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Company_TaxCode",
                table: "Company",
                column: "TaxCode",
                unique: true,
                filter: "\"TaxCode\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Company_TaxCode",
                table: "Company");
        }
    }
}
