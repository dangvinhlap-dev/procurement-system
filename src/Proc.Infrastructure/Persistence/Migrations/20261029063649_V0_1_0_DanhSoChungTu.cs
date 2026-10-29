using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proc.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Bản 0.1.0 – đánh số chứng từ: cấu hình mẫu số theo công ty (DocumentNumbering) và bộ đếm theo kỳ (DocumentSequence).
    /// Lưu ý: Down() xóa hai bảng – mất cấu hình mẫu số và số hiện tại; sau khi nâng cấp lại, số đếm lại từ 1
    /// (bảng chứng từ có ràng buộc duy nhất nên không lưu được số trùng).
    /// </summary>
    public partial class V0_1_0_DanhSoChungTu : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DocumentNumbering",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocType = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Format = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentNumbering", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentNumbering_Company_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DocumentSequence",
                columns: table => new
                {
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocType = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Period = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: false),
                    CurrentNumber = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentSequence", x => new { x.CompanyId, x.DocType, x.Period });
                    table.ForeignKey(
                        name: "FK_DocumentSequence_Company_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentNumbering_CompanyId_DocType",
                table: "DocumentNumbering",
                columns: new[] { "CompanyId", "DocType" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentNumbering");

            migrationBuilder.DropTable(
                name: "DocumentSequence");
        }
    }
}
